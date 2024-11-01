using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Baking;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Entities
{
    /// <summary>
    /// Provides methods to control the baking process, and provides access to the <see cref="BlobAssetStore"/> used
    /// during baking.
    /// </summary>
    [DisableAutoCreation]
    public partial class BakingSystem : SystemBase
    {
        /// <summary>
        /// Called when this system is created.
        /// </summary>
        protected override void OnCreate()
        {
            _BakingContext = default;
            Scene = default;
            _BakedEntities = new BakedEntityData(EntityManager);
            _TransformAuthoringBaking = new TransformAuthoringBaking(EntityManager);
#if UNITY_EDITOR
            _RefreshVersion = 0;
#endif

            BakerDataUtility.Initialize();
        }

        internal void PrepareForBaking(BakingSettings bakingSettings, Scene scene)
        {
            BakingSettings = bakingSettings;
            Scene = scene;

            _BakedEntities.ConfigureDefaultArchetypes(BakingSettings, Scene);
        }

        /// <summary>
        /// Called when this system is destroyed.
        /// </summary>
        protected override void OnDestroy()
        {
            _BakedEntities.Dispose();
            _BakingContext.Dispose();
            _TransformAuthoringBaking.Dispose();
        }

        /// <summary>
        /// Called when this system is updated.
        /// </summary>
        protected override void OnUpdate() { }

        internal BakingSettings    BakingSettings;
        Scene             Scene;

        BakedEntityData          _BakedEntities;
        IncrementalBakingContext _BakingContext;
        TransformAuthoringBaking _TransformAuthoringBaking;

        List<Component>          _ComponentsCache = new List<Component>();
        List<Transform>          _TransformCache = new List<Transform>();

        /// <summary>
        /// Access to the <see cref="BlobAssetStore"/> used during baking.
        /// </summary>
        /// <remarks>
        /// The blob assets created by baking systems need to
        /// be registered in the <see cref="BlobAssetStore"/>.
        /// </remarks>
        public BlobAssetStore BlobAssetStore => BakingSettings.BlobAssetStore;

#if UNITY_EDITOR
        // The last refresh version of the asset database (If it changes, we recheck if any of the assets we depend on have changed)
        ulong                    _RefreshVersion;
#endif

        static ProfilerMarker s_ClearCaches = new ProfilerMarker("Baking.ClearCaches");
        static ProfilerMarker s_FullBake = new ProfilerMarker("Baking.FullBake");
        static ProfilerMarker s_DeletedEntitiesCheck = new ProfilerMarker("Baking.DeletedEntitiesCheck");

        internal bool IsLiveConversion()
        {
            return (BakingSettings.BakingFlags & (BakingUtility.BakingFlags.SceneViewLiveConversion | BakingUtility.BakingFlags.GameViewLiveConversion)) != 0;
        }

        internal Entity GetEntity(Component component)
        {
            return _BakedEntities.GetEntity(component);
        }
        internal Entity GetEntity(GameObject gameObject)
        {
            return _BakedEntities.GetEntity(gameObject);
        }

        internal UnsafeHashSet<Entity> GetEntitiesForBakers(Component component)
        {
            return _BakedEntities.GetEntitiesForBakers(component);
        }

        internal Entity GetPrimaryEntity(Component component)
        {
            return _BakedEntities.GetPrimaryEntity(component);
        }

        internal bool Bake(IncrementalBakingChangeTracker changeTracker, GameObject[] cleanRootGameObjects)
        {
            s_ClearCaches.Begin();
            _ComponentsCache.Clear();
            _TransformCache.Clear();
            s_ClearCaches.End();

            IncrementalBakingLog.Begin();

#if UNITY_EDITOR
            bool assetsChanged = (AssetDatabase.GlobalArtifactDependencyVersion != _RefreshVersion);
            _RefreshVersion = AssetDatabase.GlobalArtifactDependencyVersion;
#else
            bool assetsChanged = false;
#endif

            IncrementalBakingContext.IncrementalBakeInstructions instructions;

            if (cleanRootGameObjects != null)
            {
                ClearBakedEntities();

#if UNITY_EDITOR
                if (BakingSettings.PrefabRoot != null)
                    RegisterPrefabForBaking(BakingSettings.PrefabRoot);
#endif
                instructions = _BakingContext.BuildInitialInstructions(Scene, cleanRootGameObjects, ref _TransformAuthoringBaking);
            }
            else
            {
#if UNITY_EDITOR
                if(assetsChanged)
                    _BakedEntities.UpdatePrefabs(changeTracker);
#endif

                IncrementalBakingBatch batch = new IncrementalBakingBatch();
                changeTracker.FillBatch(ref batch);
                CheckForUserDeletedEntities(ref batch);
                instructions = _BakingContext.BuildIncrementalInstructions(batch, ref _TransformAuthoringBaking, assetsChanged);

                // The asset pipeline might have changed some assets, yet there is nothing that actually changed as a result
                if (!instructions.HasChanged)
                    return false;
            }

            _BakedEntities.ApplyBakeInstructions(ref _BakingContext._Dependencies, instructions, BlobAssetStore, BakingSettings, ref _BakingContext._Components);

            // We must continue to iterate additional game objects as prefabs can express prefabs and so on
            // In addition, Prefabs can be un-referenced and in need of being destroyed
            while (_BakedEntities.HasAdditionalGameObjectsToBake() || _BakedEntities.HasPrefabsToCheck())
            {
                var additionalObjectsToBake = _BakedEntities.GetAndClearAdditionalObjectsToBake(Allocator.Temp);
                var prefabObjectsToDestroy = _BakedEntities.GetAndClearPrefabObjectsToDestroy(ref _BakingContext._Hierarchy, Allocator.Temp);
                instructions = _BakingContext.BuildAdditionalInstructions(additionalObjectsToBake, prefabObjectsToDestroy, ref _TransformAuthoringBaking);

                additionalObjectsToBake.Dispose();
                prefabObjectsToDestroy.Dispose();

                var jobHandle = _TransformAuthoringBaking.Prepare(_BakingContext._Hierarchy, instructions.ChangedTransforms);
                jobHandle.Complete();

                _BakedEntities.ApplyBakeInstructions(ref _BakingContext._Dependencies, instructions, BlobAssetStore, BakingSettings, ref _BakingContext._Components);
            }

            // apply the static and active state on the additional entities
            _BakedEntities.AdditionalEntitiesApplyActiveStaticState();

            IncrementalBakingLog.WriteLog(ref _BakingContext._Components);
            IncrementalBakingLog.End();

            _BakedEntities.UpdateTransforms(_TransformAuthoringBaking);
            return true;
        }

        private void CheckForUserDeletedEntities(ref IncrementalBakingBatch batch)
        {
            using (s_DeletedEntitiesCheck.Auto())
            {
                using var deleted = _BakedEntities.RemoveInvalidEntities(Allocator.TempJob);
                if (deleted.IsCreated && !deleted.IsEmpty)
                {
                    // We store the values in an array
                    batch.RecreateInstanceIds = deleted.ToArray(Allocator.Temp);
                }
            }
        }

#if UNITY_EDITOR
        internal UnsafeList<BakeDependencies.AssetState> GetAllAssetDependencies()
        {
            return _BakingContext._Dependencies.GetAllAssetDependencies();
        }

        void RegisterPrefabForBaking(GameObject prefab)
        {
            if (!_BakedEntities._GameObjectToEntity.ContainsKey(prefab.GetInstanceID()))
                _BakedEntities.CreateEntityForPrefab(prefab);
        }
#endif

        private void ClearBakedEntities()
        {
            _BakingContext.Dispose();
            _BakedEntities.Clear();
            EntityManager.DestroyEntity(EntityManager.UniversalQuery);
        }


        internal void UpdateReferencedEntities()
        {
            _BakedEntities.UpdateReferencedEntities();
        }

        internal bool DidBake(Component component)
        {
            return _BakingContext.DidBake(component);
        }

        internal bool DidBake(GameObject go)
        {
            return _BakingContext.DidBake(go);
        }

        // This function for testing purposes
        // and it shouldn't be used outside tests
        internal void ClearDidBake()
        {
            _BakingContext.ClearDidBake();
        }
    }
}
