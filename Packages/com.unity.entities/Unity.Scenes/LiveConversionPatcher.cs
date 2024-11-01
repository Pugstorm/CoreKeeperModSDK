using System;
using Unity.Assertions;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe.NotBurstCompatible;
using Unity.Entities;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes
{
    /// <summary>
    /// Tag component for disabling live conversion of scene entities.
    /// </summary>
    public struct DisableLiveConversion : IComponentData
    {
    }

    /// <summary>
    /// Options for how the conversion system runs and makes the results available in the Editor.
    /// </summary>
    public enum LiveConversionMode
    {
        /// <summary>
        /// Disable live conversion. The conversion system doesn't run when the authoring data is changed.
        /// </summary>
        Disabled = 0,
        /// <summary>
        /// Run the conversion when building the player.
        /// </summary>
        LiveConvertStandalonePlayer,
        /// <summary>
        /// Enable live conversion is enabled and display the authoring data in the scene view.
        /// </summary>
        SceneViewShowsAuthoring,
        /// <summary>
        /// Enable the live conversion and display the result of the conversion in the scene view.
        /// </summary>
        SceneViewShowsRuntime,
    }

    struct LiveConversionChangeSet : IDisposable
    {
        public Hash128         SceneGUID;
        public EntityChangeSet Changes;
        public string          SceneName;
        public bool            UnloadAllPreviousEntities;
        public int             FramesToRetainBlobAssets;

        public void Dispose()
        {
            Changes.Dispose();
        }

        #if UNITY_EDITOR
        public byte[] Serialize()
        {
            var buffer = new UnsafeAppendBuffer(1024, 16, Allocator.Persistent);

            EntityChangeSetSerialization.ResourcePacket.SerializeResourcePacket(Changes, ref buffer);

            buffer.Add(SceneGUID);
            buffer.AddNBC(SceneName);
            buffer.Add(UnloadAllPreviousEntities);
            buffer.Add(FramesToRetainBlobAssets);

            var outputArray = buffer.ToBytesNBC();
            buffer.Dispose();
            return outputArray;
        }

        #endif

        unsafe public static LiveConversionChangeSet Deserialize(EntityChangeSetSerialization.ResourcePacket resource, GlobalAssetObjectResolver resolver)
        {
            var reader = resource.ChangeSet.AsReader();

            LiveConversionChangeSet changeSet;
            changeSet.Changes = EntityChangeSetSerialization.Deserialize(&reader, resource.GlobalObjectIds, resolver);
            reader.ReadNext(out changeSet.SceneGUID);
            reader.ReadNextNBC(out changeSet.SceneName);
            reader.ReadNext(out changeSet.UnloadAllPreviousEntities);
            reader.ReadNext(out changeSet.FramesToRetainBlobAssets);

            return changeSet;
        }
    }


    class LiveConversionPatcher
    {
        public struct LiveConvertedSceneCleanup : ICleanupComponentData, IEquatable<LiveConvertedSceneCleanup>
        {
            public Hash128 Scene;

            public bool Equals(LiveConvertedSceneCleanup other)
            {
                return Scene.Equals(other.Scene);
            }

            public override int GetHashCode()
            {
                return Scene.GetHashCode();
            }
        }


        private World _DstWorld;
        EntityQuery _AddedScenesQuery;
        private EntityQuery _RemovedScenesQuery;
        public LiveConversionPatcher(World destinationWorld)
        {
            _DstWorld = destinationWorld;

            _AddedScenesQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<SceneTag>()
                .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)
                .Build(_DstWorld.EntityManager);
            _AddedScenesQuery.SetSharedComponentFilter(new SceneTag { SceneEntity = Entity.Null});

            _RemovedScenesQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<LiveConvertedSceneCleanup>()
                .WithNone<SceneReference>()
                .Build(_DstWorld.EntityManager);
        }

        public void Dispose()
        {
            _AddedScenesQuery.Dispose();
            _RemovedScenesQuery.Dispose();
        }

        struct RemoveLiveConversionSceneState : IJobChunk
        {
            public Hash128 DeleteGuid;
            public  EntityCommandBuffer Commands;

            [ReadOnly] public ComponentTypeHandle<LiveConvertedSceneCleanup> LiveConvertedSceneStateHandle;
            [ReadOnly] public EntityTypeHandle EntitiesHandle;

            public void Execute(Entity entity, in LiveConvertedSceneCleanup scene)
            {
                if (scene.Scene == DeleteGuid)
                    Commands.RemoveComponent<LiveConvertedSceneCleanup>(entity);
            }

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                var entities = chunk.GetNativeArray(EntitiesHandle);
                var liveConvertedSceneStates = chunk.GetNativeArray(ref LiveConvertedSceneStateHandle);
                int count = chunk.Count;
                for (int i = 0; i < count; ++i)
                {
                    var liveConvertedSceneState = liveConvertedSceneStates[i];
                    Execute(entities[i], in liveConvertedSceneState);
                }
            }
        }

        public unsafe void UnloadScene(Hash128 sceneGUID)
        {
            var dstEntities = _DstWorld.EntityManager;
            var sceneSystem = _DstWorld.GetExistingSystem<SceneSystem>();

            ref var state = ref _DstWorld.Unmanaged.ResolveSystemStateRef(sceneSystem);

            var sceneEntity = SceneSystem.GetLiveConvertedSceneEntity(ref state, sceneGUID);

            dstEntities.RemoveComponent<DisableSceneResolveAndLoad>(sceneEntity);
            dstEntities.RemoveComponent<LiveConvertedSceneCleanup>(sceneEntity);
            SceneSystem.UnloadSceneSectionMetaEntitiesOnly(_DstWorld.Unmanaged, sceneEntity, false);

            // Cleanup leftover LiveConvertedSceneCleanup
            // (This happens if the scene entity got destroyed)
            var job = new RemoveLiveConversionSceneState
            {
                DeleteGuid = sceneGUID,
                Commands = new EntityCommandBuffer(Allocator.TempJob),
                LiveConvertedSceneStateHandle = _DstWorld.EntityManager.GetComponentTypeHandle<LiveConvertedSceneCleanup>(true),
                EntitiesHandle = _DstWorld.EntityManager.GetEntityTypeHandle()
            };
            job.Run(_RemovedScenesQuery);

            job.Commands.Playback(dstEntities);
            job.Commands.Dispose();
        }

        public unsafe void ApplyPatch(LiveConversionChangeSet changeSet)
        {
            var dstEntities = _DstWorld.EntityManager;
            var sceneSystem = _DstWorld.GetExistingSystem<SceneSystem>();
            ref var state = ref *_DstWorld.Unmanaged.ResolveSystemState(sceneSystem);

            Entity sectionEntity = Entity.Null;
            var sceneEntity = SceneSystem.GetLiveConvertedSceneEntity(ref state, changeSet.SceneGUID);

            //@TODO: Check if the scene or section is requested to be loaded
            if (sceneEntity == Entity.Null)
            {
                Debug.LogWarning($"'{changeSet.SceneName}' (Scene GUID {changeSet.SceneGUID}) was ignored in live conversion since it is not loaded.");
                return;
            }

            var patcherBlobAssetSystem = _DstWorld.GetOrCreateSystemManaged<EntityPatcherBlobAssetSystem>();
            patcherBlobAssetSystem.SetFramesToRetainBlobAssets(changeSet.FramesToRetainBlobAssets);

            // Unload scene
            if (changeSet.UnloadAllPreviousEntities)
            {
                //@Todo: Can we try to keep scene & section entities alive? (In case user put custom data on it)
                SceneSystem.UnloadSceneSectionMetaEntitiesOnly(_DstWorld.Unmanaged, sceneEntity, false);

                // Create section
                sectionEntity = dstEntities.CreateEntity();
                dstEntities.AddComponentData(sectionEntity, new SceneSectionStreamingSystem.StreamingState { Status = SceneSectionStreamingSystem.StreamingStatus.Loaded});
                dstEntities.AddComponentData(sectionEntity, new IsSectionLoaded());
                dstEntities.AddComponentData(sectionEntity, new DisableSceneResolveAndLoad());
                dstEntities.AddComponentData(sectionEntity, new SceneEntityReference {SceneEntity = sceneEntity});

                // Configure scene
                dstEntities.AddComponentData(sceneEntity, new DisableSceneResolveAndLoad());
                dstEntities.AddComponentData(sceneEntity, new LiveConvertedSceneCleanup { Scene = changeSet.SceneGUID });

                dstEntities.AddBuffer<ResolvedSectionEntity>(sceneEntity).Add(new ResolvedSectionEntity { SectionEntity = sectionEntity});

#if UNITY_EDITOR
                var sceneNameFs64 = new FixedString64Bytes();
                FixedStringMethods.CopyFromTruncated(ref sceneNameFs64, "SceneSection (Live converted): " + changeSet.SceneName);
                dstEntities.SetName(sectionEntity, sceneNameFs64);
                FixedStringMethods.CopyFromTruncated(ref sceneNameFs64, "Scene (Live converted): " + changeSet.SceneName);
                dstEntities.SetName(sceneEntity, sceneNameFs64);
#endif
            }
            else
            {
                var resolvedSectionEntities = dstEntities.GetBuffer<ResolvedSectionEntity>(sceneEntity);
                if (resolvedSectionEntities.Length > 0)
                {
                    sectionEntity = resolvedSectionEntities[0].SectionEntity;
                }
            }

            // SceneTag.SceneEntity == Entity.Null is reserved for new entities added via live link.
            if (_AddedScenesQuery.CalculateChunkCount() != 0)
            {
                Debug.LogWarning("SceneTag.SceneEntity must not reference Entity.Null. Destroying Entities.");
                dstEntities.DestroyEntity(_AddedScenesQuery);
            }

            EntityPatcher.ApplyChangeSet(_DstWorld.EntityManager, changeSet.Changes);

            if (sectionEntity != Entity.Null)
            {
                dstEntities.SetSharedComponentManaged(_AddedScenesQuery, new SceneTag {SceneEntity = sectionEntity});
            }

            EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
        }
    }
}
