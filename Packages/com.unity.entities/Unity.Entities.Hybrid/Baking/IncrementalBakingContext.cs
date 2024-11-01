using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Conversion;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Unity.Entities.Baking
{

    // This translates change events high level instructions (Instantiate prefab / Destroy hierarchy)
    // into a flattened list of instructions on what to bake / revert / create / destroy
    // It does this incrementally and it also handles dependencies that were registered in the previous bake.
    internal struct IncrementalBakingContext
    {
        internal struct IncrementalBakeInstructions
        {
            // This needs an IEquatable because we are storing it in a HashSet, which will use an implicit Equals otherwise
            // this Implicit equality triggers calls into the Engine due to the Component reference here, which is horribly slow at MegaCity scale.
            internal struct BakeComponent : IEquatable<BakeComponent>
            {
                public int             GameObjectInstanceID;
                public int             ComponentID;
                public Component       Component;

                public bool Equals(BakeComponent other)
                {
                    return ComponentID == other.ComponentID;
                }

                public override bool Equals(object obj)
                {
                    return obj is BakeComponent other && Equals(other);
                }

                public override int GetHashCode()
                {
                    return ComponentID;
                }
            }

            // NOTE: All following lists are cleaned already. No duplicates and no invalid objects.

            // The components that need to be baked (This is both added or changed components)
            // These components also need to be reverted (If they existed before)
            public HashSet<BakeComponent> BakeComponents;

            /// <summary>
            /// The GameObjects to bake when some GameObject property has changed (static, active).
            /// </summary>
            /// <remarks>Contains both the created and the changed GameObjects.</remarks>
            public HashSet<GameObject> BakeGameObjects;

            // The components that need to be reverted, because it was removed (Not present in bake components)
            public UnsafeParallelHashSet<int>     RevertComponents;

            // The individual game objects that were created.
            // (Note all components on created objects are already part of BakeComponents)
            // (Thus the only purpose of this is to potentially create the matching entities in batch)
            public List<GameObject>    CreatedGameObjects;

            // The individual game objects that were destroyed, all components that were previously baked are also on the RevertComponents list.
            // (Used for cleaning up entities, that no longer have their source game object)
            public NativeList<int>     DestroyedGameObjects;

            // The individual game objects that might have had their name changed.
            // If a game object is in the CreatedGameObjects, then it will NOT be in PotentiallyRenamedGameObjects.
            public List<GameObject>    PotentiallyRenamedGameObjects;

            // The list of all transforms that were changed.
            // Note this list is the changes to Local TRS values or reparenting.
            // (Changes to TRS still need to be pushed down the hierarchy to get list of changed LocalToWorld matrices)
            public NativeList<int>     ChangedTransforms;

            private bool created;

            public IncrementalBakeInstructions(Allocator allocator)
            {
                CreatedGameObjects = new List<GameObject>(1024);
                DestroyedGameObjects = new NativeList<int>(Allocator.Persistent);
                BakeComponents = new HashSet<BakeComponent>();
                BakeGameObjects = new HashSet<GameObject>(1024);
                RevertComponents = new UnsafeParallelHashSet<int>(16, Allocator.Persistent);
                PotentiallyRenamedGameObjects = new List<GameObject>();
                ChangedTransforms = new NativeList<int>(16, Allocator.Persistent);

                created = true;
            }

            public void Dispose()
            {
                CreatedGameObjects = null;
                if (DestroyedGameObjects.IsCreated)
                    DestroyedGameObjects.Dispose();
                BakeComponents = null;
                BakeGameObjects = null;
                if (RevertComponents.IsCreated)
                    RevertComponents.Dispose();
                PotentiallyRenamedGameObjects = null;
                if (ChangedTransforms.IsCreated)
                    ChangedTransforms.Dispose();
                created = false;
            }

            public void Clear(bool clearTransforms = true)
            {
                BakeComponents.Clear();
                BakeGameObjects.Clear();
                RevertComponents.Clear();
                CreatedGameObjects.Clear();
                DestroyedGameObjects.Clear();
                PotentiallyRenamedGameObjects.Clear();
                if(clearTransforms)
                    ChangedTransforms.Clear();
            }

            public bool HasChanged
            {
                get
                {
                    return CreatedGameObjects.Count != 0 ||
                           DestroyedGameObjects.Length != 0 ||
                           BakeComponents.Count != 0 ||
                           BakeGameObjects.Count != 0 ||
                           RevertComponents.Count() != 0 ||
                           PotentiallyRenamedGameObjects.Count != 0 ||
                           ChangedTransforms.Length != 0;
                }
            }

            public bool IsCreated
            {
                get { return created; }
            }
        }

        // These are storing actual state
        public GameObjectComponents   _Components;
        public IncrementalHierarchy   _Hierarchy;
        public BakeDependencies       _Dependencies;
        public Scene                  _Scene;


        // The following are simply caches to avoid allocation from frame to frame

        // output being generated by intialize / update.
        // Cached here to avoid continous allocations
        IncrementalBakeInstructions   _BakeInstructionsCache;
        List<Component>               _ComponentCache;
        List<Component>               _ComponentAddedCache;
        List<Component>               _ComponentsExistingCache;
        IncrementalBakingData         _IncrementalBakingDataCache;

        static readonly string _InitialBakeInstructionsMarkerStr = "BuildInitialBakeInstructions";
        static readonly string _IncrementalBakeInstructionsMarkerStr = "BuildIncrementalBakeInstructions";
        static readonly string _AdditionalObjectsInstructionsMarkerStr = "BuildAdditionalObjectsInstructions";

        static readonly ProfilerMarker _InitialBakeInstructionsMarker = new ProfilerMarker(_InitialBakeInstructionsMarkerStr);
        static readonly ProfilerMarker _IncrementalBakeInstructionsMarker = new ProfilerMarker(_IncrementalBakeInstructionsMarkerStr);
        static readonly ProfilerMarker _AdditionalObjectsInstructionsMarker = new ProfilerMarker(_AdditionalObjectsInstructionsMarkerStr);
        static readonly ProfilerMarker _changedComponentsIncludingDependenciesMarker = new ProfilerMarker("ChangedComponentsIncludingDependencies");

        static ProfilerMarker _parentChangeHierarchyMarker = new ProfilerMarker("ParentChangesInHierarchy");
        static ProfilerMarker _deleteFromHierarchyMarker = new ProfilerMarker("DeleteFromHierarchy");
        static ProfilerMarker _registerNewInstancesMarker = new ProfilerMarker("FindAllReconvertGameObjects");
        static ProfilerMarker _collectNewGameObjectsMarker = new ProfilerMarker(nameof(CollectAllGameObjects));
        static string _updateHierarchyMarkerStr = nameof(UpdateHierarchy);
        static ProfilerMarker _updateHierarchyMarker = new ProfilerMarker(_updateHierarchyMarkerStr);
        static ProfilerMarker _handleChangedMarker = new ProfilerMarker("HandleChanged");
        static ProfilerMarker _parentWithChildrenOrderChangedMarker = new ProfilerMarker("ParentWithChildrenOrderChanged");

        internal static string[] CollectImportantProfilerMarkerStrings()
        {
            return new string [] {
                _InitialBakeInstructionsMarkerStr,
                _IncrementalBakeInstructionsMarkerStr,
                _updateHierarchyMarkerStr
            };
        }

        public void Dispose()
        {
            _Hierarchy.Dispose();
            _Dependencies.Dispose();
            _Components.Dispose();

            _IncrementalBakingDataCache.Dispose();
            _BakeInstructionsCache.Dispose();
            _ComponentCache = null;
        }

        public bool DidBake(Component component)
        {
            if (_BakeInstructionsCache.IsCreated && component != null)
            {
                var componentID = component.GetInstanceID();
                foreach (var bakeComponent in _BakeInstructionsCache.BakeComponents)
                {
                    if (bakeComponent.ComponentID == componentID)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool DidBake(GameObject go)
        {
            if (_BakeInstructionsCache.IsCreated && go != null)
            {
                var objectID = go.GetInstanceID();
                foreach (var bakeComponent in _BakeInstructionsCache.BakeComponents)
                {
                    if (bakeComponent.GameObjectInstanceID == objectID)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // This function for testing purposes
        // and it shouldn't be used outside tests
        internal void ClearDidBake()
        {
            _BakeInstructionsCache.Clear();
        }

        public IncrementalBakeInstructions BuildInitialInstructions(Scene scene, GameObject[] sceneRoots, ref TransformAuthoringBaking transformAuthoringBaking)
        {
            using var marker = _InitialBakeInstructionsMarker.Auto();

            Dispose();

            _Scene = scene;
            _BakeInstructionsCache = new IncrementalBakeInstructions(Allocator.Persistent);
            _ComponentCache = new List<Component>();
            _ComponentAddedCache = new List<Component>();
            _ComponentsExistingCache = new List<Component>();
            _IncrementalBakingDataCache = IncrementalBakingData.Create();
            _Dependencies = new BakeDependencies(Allocator.Persistent);

            // Flatten full list of game objects we need to create entities for
            {
                foreach (var root in sceneRoots)
                {
                    var additionalGameObjects = root.GetComponentsInChildren<Transform>(true).Select(transform =>
                    {
                        // Debug.Log($"BuildInitialInstructions - CreateGameObject: {transform.gameObject.GetInstanceID()} (transform.gameObject)");
                        return transform.gameObject;
                    }).ToArray();
                    _BakeInstructionsCache.CreatedGameObjects.AddRange(additionalGameObjects);
                    _BakeInstructionsCache.BakeGameObjects.UnionWith(additionalGameObjects);

                    _BakeInstructionsCache.ChangedTransforms.Add(root.gameObject.GetInstanceID());
                }
            }

            var revertComponents = _BakeInstructionsCache.RevertComponents;

            // Collect all bake components and make sure _Components state matches current Scene state
            _Components = new GameObjectComponents(Allocator.Persistent);
            foreach (var gameObject in _BakeInstructionsCache.CreatedGameObjects)
            {
                gameObject.GetComponentsBaking(_ComponentCache);
                foreach (var com in _ComponentCache)
                {
                    //@TODO-opt: We know here that nothing will be reverted, and everything added...
                    _ComponentAddedCache.Clear();
                    _ComponentsExistingCache.Clear();
                    _Components.UpdateGameObject(gameObject, _ComponentCache, _ComponentAddedCache, _ComponentsExistingCache, ref revertComponents);
                    // Debug.Log($"BuildInitialInstructions - Bake: {gameObject.GetInstanceID()} ({gameObject.name}) Component: {com.GetInstanceID()} ({com})");

                    _BakeInstructionsCache.BakeComponents.Add(new IncrementalBakeInstructions.BakeComponent { GameObjectInstanceID = gameObject.GetInstanceID(), Component = com, ComponentID = com.GetInstanceID()});
                }
            }

            // Make sure hierarchy state matches current Scene state.
            IncrementalHierarchyFunctions.Build(sceneRoots, out _Hierarchy, Allocator.Persistent);

            var jobHandle = transformAuthoringBaking.Prepare(_Hierarchy, _BakeInstructionsCache.ChangedTransforms);
            jobHandle.Complete();

            return _BakeInstructionsCache;
        }

        public void CollectGameObjectsWithTransformChanged(ref IncrementalBakingBatch batch, NativeList<int> instanceIDs)
        {
            // Consider modified transform
            var changes = batch.ChangedComponents;
            for (int i = 0; i < changes.Count; i++)
            {
                if (changes[i] is Transform)
                {
                    instanceIDs.Add(changes[i].gameObject.GetInstanceID());
                }
            }

            // Consider baking of hierarchies
            if (batch.BakeHierarchyInstanceIds.Length > 0)
                _BakeInstructionsCache.ChangedTransforms.AddRange(batch.BakeHierarchyInstanceIds);
            if (batch.ForceBakeHierarchyInstanceIds.Length > 0)
                _BakeInstructionsCache.ChangedTransforms.AddRange(batch.ForceBakeHierarchyInstanceIds);

            // Consider reparenting
            if (!batch.ParentChangeInstanceIds.IsEmpty)
            {
                using var parentChangeKeys = batch.ParentChangeInstanceIds.GetKeyArray(Allocator.TempJob);
                _BakeInstructionsCache.ChangedTransforms.AddRange(parentChangeKeys);
            }
        }

        public IncrementalBakeInstructions BuildIncrementalInstructions(IncrementalBakingBatch batch, ref TransformAuthoringBaking transformAuthoringBaking, bool assetsChanged)
        {
            using var marker = _IncrementalBakeInstructionsMarker.Auto();

            // Using knowledge of the previous hieararchy / new game object hierarchy and the batch of changes,\
            // flatten that to simpler non-hierarchical changes (Eg. all game objects are in ChangedGameObjects for a prefab that might come in as a single prefab instantiate operation)
            UpdateHierarchy(batch, ref _IncrementalBakingDataCache);

            _BakeInstructionsCache.Clear();
            var changedAuthoringObjectsIncludingDependencies = new UnsafeParallelHashSet<int>(1024, Allocator.TempJob);

            var revertComponents = _BakeInstructionsCache.RevertComponents;
            var destroyedGameObjects = _BakeInstructionsCache.DestroyedGameObjects;

            CollectGameObjectsWithTransformChanged(ref batch, _BakeInstructionsCache.ChangedTransforms);

            var prepareJobHandle = transformAuthoringBaking.Prepare(_Hierarchy, _BakeInstructionsCache.ChangedTransforms);

            _BakeInstructionsCache.BakeGameObjects.UnionWith(_IncrementalBakingDataCache.ChangedGameObjects.Select(x => x.gameObject));

            // _IncrementalConversionDataCache.ChangedGameObjects implies that the game object was either created or new components were added or a component was removed.
            // _Components has a copy of the last converted authoring gameobject -> component list. Thus we can create precise instructions for:
            // - which game objects were created
            // - which components need to be reverted (Because they were removed)
            // - which components were added (not reverted, but need to run bake)
            // - which components changed (revert & bake)
            foreach (var changedGameObject in _IncrementalBakingDataCache.ChangedGameObjects)
            {
                bool recreateEntity = (changedGameObject.mode == IncrementalBakingData.ChangedGameObjectMode.RecreateAll);
                bool isForced = recreateEntity || (changedGameObject.mode == IncrementalBakingData.ChangedGameObjectMode.ForceBake);
                var gameObject = changedGameObject.gameObject;
                gameObject.GetComponentsBaking(_ComponentCache);

                _ComponentAddedCache.Clear();
                _ComponentsExistingCache.Clear();

                int goInstanceId = gameObject.GetInstanceID();
                if (_Components.UpdateGameObject(gameObject, _ComponentCache, _ComponentAddedCache, _ComponentsExistingCache, ref revertComponents) || recreateEntity)
                {
                    IncrementalBakingLog.RecordGameObjectNew(goInstanceId);
                    _BakeInstructionsCache.CreatedGameObjects.Add(gameObject);
                }
                else
                {
                    IncrementalBakingLog.RecordGameObjectChanged(goInstanceId);
                    _BakeInstructionsCache.PotentiallyRenamedGameObjects.Add(gameObject);
                    _IncrementalBakingDataCache.ChangedGameObjectProperties.Add(IncrementalBakingData.GameObjectProperties.CalculateProperties(gameObject));
                }

                foreach (var com in _ComponentAddedCache)
                {
                    _BakeInstructionsCache.BakeComponents.Add(new IncrementalBakeInstructions.BakeComponent { GameObjectInstanceID = goInstanceId, Component = com, ComponentID = com.GetInstanceID()});
                    IncrementalBakingLog.RecordComponentNew(com.GetInstanceID());

                    IncrementalBakingLog.RecordComponentBake(com.GetInstanceID(), ComponentBakeReason.NewComponent, com.GetInstanceID(), TypeManager.GetTypeIndex(com.GetType()));
                }

                if (isForced)
                {
                    foreach (var com in _ComponentsExistingCache)
                    {
                        var comId = com.GetInstanceID();

                        // Add for revert
                        _BakeInstructionsCache.RevertComponents.Add(comId);

                        // Add for baking
                        _BakeInstructionsCache.BakeComponents.Add(new IncrementalBakeInstructions.BakeComponent { GameObjectInstanceID = goInstanceId, Component = com, ComponentID = com.GetInstanceID()});
                        IncrementalBakingLog.RecordComponentBake(comId, ComponentBakeReason.UpdatePrefabInstance, goInstanceId, TypeManager.GetTypeIndex(com.GetType()));
                    }
                }
            }

            //foreach (var componentID in _IncrementalConversionDataCache.ChangedComponents)
            //    Debug.Log($"BuildIncrementalInstructions - ChangedComponents: {componentID.GetInstanceID()} ({componentID.gameObject.name}) ({componentID.GetType().Name})");

            var changedSceneTransforms = new ChangedSceneTransforms
            {
                Hierarchy = _Hierarchy.AsReadOnly(),
                Transforms = _Hierarchy.TransformAuthorings,
                ChangedLocalToWorldIndices = _BakeInstructionsCache.ChangedTransforms
            };

            _Dependencies.CalculateDependencies(ref _Components, ref _IncrementalBakingDataCache, changedSceneTransforms, ref changedAuthoringObjectsIncludingDependencies, prepareJobHandle, assetsChanged);

            // _IncrementalConversionDataCache.RemovedInstanceIds is a flattened list of game object instance IDs.
            // - All previous components need to be reverted
            // - The entities for those game objects need to be destroyed
            //@TODO: DOTS-5454
            foreach (var gameObjectID in _IncrementalBakingDataCache.RemovedGameObjects)
            {
                changedAuthoringObjectsIncludingDependencies.Remove(gameObjectID);

                // Debug.Log($"BuildIncrementalInstructions: Destroy GameObject: {gameObjectID}" );
                foreach (var component in _Components.GetComponents(gameObjectID))
                {
                    revertComponents.Add(component.InstanceID);
                    changedAuthoringObjectsIncludingDependencies.Remove(component.InstanceID);

                    IncrementalBakingLog.RecordComponentDestroyed(component.InstanceID);
                }

                if (_Components.DestroyGameObject(gameObjectID))
                {
                    destroyedGameObjects.Add(gameObjectID);

                    IncrementalBakingLog.RecordGameObjectDestroyed(gameObjectID);
                }
            }

            using var changedComponentsMarker = _changedComponentsIncludingDependenciesMarker.Auto();

            // _IncrementalConversionDataCache.ChangedComponents are components where content of the component has changed (not added / removed)
            // - Thus we need to revert & bake them
            foreach (var componentID in changedAuthoringObjectsIncludingDependencies)
            {
                var obj =  Resources.InstanceIDToObject(componentID);
                if (obj is GameObject gameObject)
                {
                    _BakeInstructionsCache.BakeGameObjects.Add(gameObject);
                    continue;
                }

                var component = (Component) obj;
                if (component == null)
                {
                    //@TODO: DOTS-5454
                    // Disabling this error, because it is possible to have removed components in changedComponentsIncludingDependencies when an entity creates a dependency on itself.
                    // A dependency on itself via GetComponent is a valid use case as there could be multiple components of the same type on the GameObject and the returning order could be relevant for the baker.
                    // When DOTS-5454 is resolved, then this should not be possible and the error should be brought back.
                    //Debug.LogError("Component marked for baking has been destroyed.");
                    continue;
                }

                //@TODO: DOTS-5455
                var gameObjectID = component.gameObject.GetInstanceID();
                if (!_Components.HasComponent(gameObjectID, componentID))
                {
                    Debug.LogError("Changed component but not known on game object");
                    continue;
                }

                //Debug.Log($"BuildIncrementalInstructions - ChangedComponentOrDependency: {componentID} ({component.gameObject.name}) ({component.GetType().Name})");

                // Add for bake
                var bake = new IncrementalBakeInstructions.BakeComponent { GameObjectInstanceID = gameObjectID, Component = component, ComponentID = componentID};
                _BakeInstructionsCache.BakeComponents.Add(bake);
            }

            changedAuthoringObjectsIncludingDependencies.Dispose();

            return _BakeInstructionsCache;
        }

        public unsafe IncrementalBakeInstructions BuildAdditionalInstructions(NativeArray<int> additionalObjects, NativeArray<int> destroyedPrefabs, ref TransformAuthoringBaking transformAuthoringBaking)
        {
            using var marker = _AdditionalObjectsInstructionsMarker.Auto();

            _BakeInstructionsCache.Clear(false);

            var allGameObjects = new List<GameObject>();
            foreach (var gameObjectID in additionalObjects)
            {
                var go = (GameObject)Resources.InstanceIDToObject(gameObjectID);
                IncrementalHierarchyFunctions.AddRecurse(_Hierarchy, go, allGameObjects);
            }

            // Make sure all Prefabs are reverted and destroyed
            foreach (var gameObjectID in destroyedPrefabs)
            {
                // Debug.Log($"BuildIncrementalInstructions: Destroy GameObject: {gameObjectID}" );
                foreach (var component in _Components.GetComponents(gameObjectID))
                {
                    _BakeInstructionsCache.RevertComponents.Add(component.InstanceID);
                    IncrementalBakingLog.RecordComponentDestroyed(component.InstanceID);
                }

                _BakeInstructionsCache.DestroyedGameObjects.Add(gameObjectID);
                IncrementalBakingLog.RecordGameObjectDestroyed(gameObjectID);

            }
            _BakeInstructionsCache.BakeGameObjects.UnionWith(allGameObjects);

            foreach (var changedGameObject in allGameObjects)
            {
                var gameObject = changedGameObject.gameObject;
                var gameObjectID = gameObject.GetInstanceID();

                // Add changed transforms
                _BakeInstructionsCache.ChangedTransforms.Add(gameObjectID);

                gameObject.GetComponentsBaking(_ComponentCache);
                foreach (var com in _ComponentCache)
                {
                    _ComponentAddedCache.Clear();
                    _ComponentsExistingCache.Clear();
                    _Components.AddGameObject(gameObject, _ComponentCache);

                    var componentID = com.GetInstanceID();

                    // Add for bake
                    _BakeInstructionsCache.BakeComponents.Add(new IncrementalBakeInstructions.BakeComponent { GameObjectInstanceID = gameObject.GetInstanceID(), Component = com, ComponentID = componentID});

                    IncrementalBakingLog.RecordGameObjectNew(gameObjectID);
                }
            }

            return _BakeInstructionsCache;
        }

        public void DestroyGameObjectData(int gameObjectID)
        {
            _Components.DestroyGameObject(gameObjectID);
        }


        //********
        static readonly List<Object> ObjectCache = new List<Object>();
        static List<Object> InstanceIdToObject(NativeArray<int> instanceIds)
        {
            Resources.InstanceIDToObjectList(instanceIds, ObjectCache);
            return ObjectCache;
        }

        static void CopyToList(NativeParallelHashSet<int> xs, NativeList<int> output)
        {
            foreach (var x in xs)
                output.Add(x);
        }

        void FilterOutValidObjects(NativeList<int> instanceIds)
        {
            var objs = InstanceIdToObject(instanceIds.AsArray());
            for (int i = instanceIds.Length - 1; i >= 0; i--)
            {
                if (objs[i] == null)
                    continue;
                var go = objs[i] as GameObject;
                if (!go.IsPrefab() && go.scene != _Scene)
                    continue;
                instanceIds.RemoveAtSwapBack(i);
            }
        }

        // This cannot be a job, because we need to remove things from a TransformAccessArray. That's not possible in
        // jobs, because it needs to happen in a non-job context. Even if it is running on the main thread, it will
        // pretend it's not.
        [BurstCompile]
        unsafe struct RemoveFromHierarchy
        {
            public IncrementalHierarchy Hierarchy;
            public NativeArray<int> DeletedInstanceIds;
            public NativeArray<int> BakeHierarchyInstanceIds;
            public NativeArray<int> ForceBakeHierarchyInstanceIds;
            public NativeList<int> RemovedInstanceIds;

            static readonly ProfilerMarker Marker = new ProfilerMarker(nameof(RemoveFromHierarchy));

            public static void Execute(void* ptr)
            {
                Marker.Begin();
                ref RemoveFromHierarchy data = ref UnsafeUtility.AsRef<RemoveFromHierarchy>(ptr);

                int capacity = data.DeletedInstanceIds.Length + data.BakeHierarchyInstanceIds.Length + data.ForceBakeHierarchyInstanceIds.Length;
                var deletedInstances = new NativeParallelHashSet<int>(capacity, Allocator.TempJob);
                {
                    data.Hierarchy.AsReadOnly().CollectHierarchyInstanceIds(data.DeletedInstanceIds, deletedInstances);
                    data.Hierarchy.AsReadOnly().CollectHierarchyInstanceIds(data.BakeHierarchyInstanceIds, deletedInstances);
                    data.Hierarchy.AsReadOnly().CollectHierarchyInstanceIds(data.ForceBakeHierarchyInstanceIds, deletedInstances);
                    var arr = deletedInstances.ToNativeArray(Allocator.Temp);
                    IncrementalHierarchyFunctions.Remove(data.Hierarchy, arr);
                    data.RemovedInstanceIds.AddRange(arr);
                }
                deletedInstances.Dispose();

                Marker.End();
            }

            public void RunWithBurst()
            {
                RemoveFromHierarchy data = this;
                Execute(UnsafeUtility.AddressOf(ref data));
            }

        }

        void UpdateHierarchy(IncrementalBakingBatch batch, ref IncrementalBakingData outData)
        {
            using (var m = _updateHierarchyMarker.Auto())
            {
                outData.Clear();

                if (batch.DeletedAssets.Length != 0)
                    outData.DeletedAssets.AddRange(batch.DeletedAssets);
                if (batch.ChangedAssets.Length != 0)
                    outData.ChangedAssets.AddRange(batch.ChangedAssets);

                outData.LightBakingChanged |= batch.LightBakingChanged;

                var requestHierarchyBake = new NativeList<int>(Allocator.Temp);
                requestHierarchyBake.AddRange(batch.BakeHierarchyInstanceIds);

                // Apply all parenting changes.
                if (!batch.ParentChangeInstanceIds.IsEmpty)
                {
                    using (_parentChangeHierarchyMarker.Auto())
                    {

                        var parentChanges = batch.ParentChangeInstanceIds.GetKeyValueArrays(Allocator.Temp);
                        using (var changeFailed = new NativeList<int>(Allocator.TempJob))
                        {
                            var changeSuccessful = outData.ParentChangeInstanceIds;
                            IncrementalHierarchyFunctions.ChangeParents(_Hierarchy, parentChanges, changeFailed, changeSuccessful);
                            {
                                // When we failed to reparent something, we have to check:
                                //  - Either we failed because the parent was already deleted and this child must also be deleted,
                                //  - Or we failed because the child was never in the hierarchy to begin with, in which case we
                                //    should track it
                                var objs = InstanceIdToObject(changeFailed.AsArray());
                                for (int i = 0; i < objs.Count; i++)
                                {
                                    var go = objs[i] as GameObject;
                                    if (go == null || go.scene != _Scene)
                                    {
                                        // This might happen when an object is made a child of another object and then deleted.
                                        outData.RemovedGameObjects.Add(changeFailed[i]);
                                    }
                                    else
                                    {
                                        // This might happen when an object is created, a child is moved, and then the original
                                        // object is deleted again.
                                        requestHierarchyBake.Add(changeFailed[i]);
                                    }
                                }
                            }

                            using (var visitedInstances = new NativeParallelHashSet<int>(0, Allocator.TempJob))
                            {
                                _Hierarchy.AsReadOnly().CollectHierarchyInstanceIds(changeFailed.AsArray(), visitedInstances);
                                CopyToList(visitedInstances, outData.RemovedGameObjects);
                            }


                            IncrementalHierarchyFunctions.Remove(_Hierarchy, changeFailed.AsArray());
                        }

                        // Consider all the transform when reparenting as modified components
                        {
                            if (!outData.ParentChangeInstanceIds.IsEmpty)
                            {
                                // Extract the successful GameObject IDs
                                NativeArray<int> transformIDs = new NativeArray<int>(outData.ParentChangeInstanceIds.Length, Allocator.Temp);
                                for (int index = 0; index < outData.ParentChangeInstanceIds.Length; ++index)
                                {
                                    transformIDs[index] = outData.ParentChangeInstanceIds[index].InstanceId;
                                }

                                var objs = InstanceIdToObject(transformIDs);
                                var transformTypeIndex = TypeManager.GetTypeIndex<Transform>();
                                foreach (var obj in objs)
                                {
                                    GameObject go = (obj as GameObject);
                                    if (go != null)
                                    {
                                        var transform = go.transform;
                                        outData.ChangedComponents.Add(new IncrementalBakingData.ChangedComponentsInfo()
                                        {
                                            instanceID = transform.GetInstanceID(),
                                            unityTypeIndex = transformTypeIndex
                                        });
                                    }
                                }
                            }
                        }
                    }
                }

                // Update the hierarchy if siblings of a same parent got reordered
                if (batch.ParentWithChildrenOrderChangedInstanceIds.Length != 0)
                {
                    using (_parentWithChildrenOrderChangedMarker.Auto())
                    {
                        foreach (var parentInstanceIds in batch.ParentWithChildrenOrderChangedInstanceIds)
                        {
                            IncrementalHierarchyFunctions.ChangeChildrenOrderInParent(_Hierarchy, parentInstanceIds);
                            outData.ParentWithChildrenOrderChangedInstanceIds.Add((parentInstanceIds));
                        }
                    }
                }

                // Remove all deleted instances from the hierarchy, plus their children. Do the same for all instances that
                // require a clean conversion.
                bool hasExplicitDeletions = batch.DeletedInstanceIds.Length != 0;
                if (hasExplicitDeletions || batch.BakeHierarchyInstanceIds.Length != 0 || batch.ForceBakeHierarchyInstanceIds.Length != 0)
                {
                    using (_deleteFromHierarchyMarker.Auto())
                    {
                        new RemoveFromHierarchy
                        {
                            Hierarchy = _Hierarchy,
                            DeletedInstanceIds = batch.DeletedInstanceIds,
                            BakeHierarchyInstanceIds = batch.BakeHierarchyInstanceIds,
                            ForceBakeHierarchyInstanceIds = batch.ForceBakeHierarchyInstanceIds,
                            RemovedInstanceIds = outData.RemovedGameObjects,
                        }.RunWithBurst();
                        FilterOutValidObjects(outData.RemovedGameObjects);
                    }
                }

                // Classify all clean conversions as either new or changed GameObjects.
                if (!requestHierarchyBake.IsEmpty)
                {
                    var cleanConversionGameObjects = new HashSet<GameObject>();
                    CollectAllGameObjects(_Scene, ref _Hierarchy, requestHierarchyBake.AsArray(), cleanConversionGameObjects);
                    using (_registerNewInstancesMarker.Auto())
                    {
                        foreach (var go in cleanConversionGameObjects)
                        {
                            IncrementalHierarchyFunctions.TryAddSingle(_Hierarchy, go);
                            outData.ChangedGameObjects.Add((go, IncrementalBakingData.ChangedGameObjectMode.Normal));
                        }
                    }
                }

                if (batch.ForceBakeHierarchyInstanceIds.Length != 0)
                {
                    var cleanConversionGameObjects = new HashSet<GameObject>();
                    CollectAllGameObjects(_Scene, ref _Hierarchy, batch.ForceBakeHierarchyInstanceIds, cleanConversionGameObjects);
                    using (_registerNewInstancesMarker.Auto())
                    {
                        foreach (var go in cleanConversionGameObjects)
                        {
                            IncrementalHierarchyFunctions.TryAddSingle(_Hierarchy, go);
                            outData.ChangedGameObjects.Add((go, IncrementalBakingData.ChangedGameObjectMode.ForceBake));
                        }
                    }
                }

                if (batch.RecreateInstanceIds.Length != 0)
                {
                    var cleanConversionGameObjects = new HashSet<GameObject>();
                    CollectGameObjects(_Scene, batch.RecreateInstanceIds, cleanConversionGameObjects);
                    using (_registerNewInstancesMarker.Auto())
                    {
                        foreach (var go in cleanConversionGameObjects)
                        {
                            if (go != null)
                            {
                                UnityEngine.Debug.LogWarning($"The primary entity for the GameObject {go.name} was deleted in a previous baking pass. This forces to rebake the whole GameObject. Consider using BakingOnlyEntityAuthoring instead.");

                                IncrementalHierarchyFunctions.TryAddSingle(_Hierarchy, go);
                                outData.ChangedGameObjects.Add((go, IncrementalBakingData.ChangedGameObjectMode.RecreateAll));
                            }
                        }
                    }
                }


                // Look at all instances that have been changed. These are all instances that have changed in-place and we
                // only need to reconvert the GameObject locally (plus all dependents).
                if (batch.ChangedInstanceIds.Length != 0)
                {
                    using (_handleChangedMarker.Auto())
                    {
                        var objs = InstanceIdToObject(batch.ChangedInstanceIds);
                        if (!hasExplicitDeletions)
                        {
                            // If nothing has been deleted, we can get away with doing fewer checks.
                            for (int i = 0; i < objs.Count; i++)
                            {
                                var obj = objs[i] as GameObject;
                                outData.ChangedGameObjects.Add((obj, IncrementalBakingData.ChangedGameObjectMode.Normal));

                                IncrementalHierarchyFunctions.UpdateActiveAndStaticState(_Hierarchy, batch.ChangedInstanceIds[i], obj.activeSelf, obj.isStatic);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < objs.Count; i++)
                            {
                                var obj = objs[i] as GameObject;

                                // Exclude destroyed objects and objects that are not in the scene anymore. This can happen when a
                                // root object is created, one of its children is dirtied, and the root is deleted or moved to
                                // another scene in a single frame.
                                if (obj == null || obj.scene != _Scene)
                                    continue;
                                outData.ChangedGameObjects.Add((obj, IncrementalBakingData.ChangedGameObjectMode.Normal));

                                IncrementalHierarchyFunctions.UpdateActiveAndStaticState(_Hierarchy, batch.ChangedInstanceIds[i], obj.activeSelf, obj.isStatic);
                            }
                        }
                    }
                }

                /*
                // If we still have dependencies on components instead of GameObjects, then they must have been added
                // before because we only had a reference to a destroyed component. There are two scenarios: Either the
                // component was on a then-destroyed GameObject, or the component itself was destroyed (but its
                // corresponding GameObject was alive).
                // In any case, we need to scan the changed GameObjects to resolve the component instances.
                if (_Dependencies.HasUnresolvedComponentInstanceIds)
                {
                    TryResolveComponentInstanceIds(outData.ChangedGameObjects);
                }
                */

                //outData.ReconvertHierarchyRequests.AddRange(batch.ReconvertHierarchyInstanceIds);

                if (hasExplicitDeletions)
                {
                    // If there have been any deletions, this might invalidate any component because it might have been
                    // deleted or it might have been moved to another scene.
                    foreach (var c in batch.ChangedComponents)
                    {
                        if (c == null || c.gameObject.scene != _Scene)
                            continue;

                        outData.ChangedComponents.Add(new IncrementalBakingData.ChangedComponentsInfo()
                        {
                            instanceID = c.GetInstanceID(),
                            unityTypeIndex = TypeManager.GetTypeIndex(c.GetType())
                        });
                    }
                }
                else
                {
                    foreach (var c in batch.ChangedComponents)
                    {
                        outData.ChangedComponents.Add(new IncrementalBakingData.ChangedComponentsInfo()
                        {
                            instanceID = c.GetInstanceID(),
                            unityTypeIndex = TypeManager.GetTypeIndex(c.GetType())
                        });
                    }
                }

#if UNITY_EDITOR
                if (LiveConversionSettings.EnableInternalDebugValidation)
                {
                    IncrementalHierarchyFunctions.Validate(_Scene, _Hierarchy);
                }
#endif
            }
        }
/*
        void TryResolveComponentInstanceIds(List<GameObject> gameObjects)
        {
            List<Component> components = new List<Component>();
            var type = typeof(Component);
            foreach (var go in gameObjects)
            {
                go.GetComponents(type, components);
                _Dependencies.ResolveComponentInstanceIds(go.GetInstanceID(), components);
                components.Clear();
            }
        }
*/
        static void CollectAllGameObjects(Scene scene, ref IncrementalHierarchy hierarchy,
            NativeArray<int> reconvertedObjects, HashSet<GameObject> outputObjects)

        {
            _collectNewGameObjectsMarker.Begin();
            var stack = new Stack<GameObject>();
            var objs = InstanceIdToObject(reconvertedObjects);
            for (int i = 0; i < objs.Count; i++)
            {
                var obj = objs[i] as GameObject;
                if (obj == null && objs[i] != null)
                    throw new ArgumentException(
                        $"InstanceId {reconvertedObjects[i]} does not correspond to a {nameof(GameObject)}, found {objs[i]} instead");

                // Ignore objects that where destroyed. This can happen when in a single frame an object is created,
                // a child is dirtied and added for reconversion, and then the parent is removed. In that case we don't
                // get a notification that this game object is destroyed - we only know that for the parent and the
                // easiest way to clean up the list is to ignore the destroyed objects here.
                if (obj == null)
                    continue;

                // Ignore objects that are not in the scene anymore. This can happen when a parent object is moved out
                // of the scene.
                if (!obj.IsPrefab() && obj.scene != scene)
                    continue;

                // Ignore objects that are not root objects or have a parent that wasn't already converted. In that case
                // we must have an event for the parent as well and later code assumes that we get the parent first.
                if (obj.transform.parent != null && !hierarchy.IndexByInstanceId.ContainsKey(obj.transform.parent.gameObject.GetInstanceID()))
                    continue;

                stack.Push(obj);
            }

            while (stack.Count > 0)
            {
                var top = stack.Pop();
                if (!outputObjects.Contains(top))
                {
                    outputObjects.Add(top);
                    int n = top.transform.childCount;
                    for (int c = 0; c < n; c++)
                        stack.Push(top.transform.GetChild(c).gameObject);
                }
            }

            _collectNewGameObjectsMarker.End();
        }

        static void CollectGameObjects(Scene scene, NativeArray<int> reconvertedObjects, HashSet<GameObject> outputObjects)

        {
            _collectNewGameObjectsMarker.Begin();
            var objs = InstanceIdToObject(reconvertedObjects);
            for (int i = 0; i < objs.Count; i++)
            {
                var obj = objs[i] as GameObject;
                if (obj == null && objs[i] != null)
                    throw new ArgumentException(
                        $"InstanceId {reconvertedObjects[i]} does not correspond to a {nameof(GameObject)}, found {objs[i]} instead");

                // Ignore objects that where destroyed. This can happen when in a single frame an object is created,
                // a child is dirtied and added for reconversion, and then the parent is removed. In that case we don't
                // get a notification that this game object is destroyed - we only know that for the parent and the
                // easiest way to clean up the list is to ignore the destroyed objects here.
                if (obj == null)
                    continue;

                // Ignore objects that are not in the scene anymore. This can happen when a parent object is moved out
                // of the scene.
                if (!obj.IsPrefab() && obj.scene != scene)
                    continue;

                if (!outputObjects.Contains(obj))
                    outputObjects.Add(obj);
            }

            _collectNewGameObjectsMarker.End();
        }
    }
}
