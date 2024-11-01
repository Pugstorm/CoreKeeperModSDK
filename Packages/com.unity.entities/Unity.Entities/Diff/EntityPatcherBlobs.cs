using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Properties;

namespace Unity.Entities
{
    [DisableAutoCreation]
    unsafe partial class EntityPatcherBlobAssetSystem : SystemBase
    {
        DynamicBlobAssetBatch* m_BlobAssetBatchPtr;

        static EntityQueryDesc s_EntityGuidQueryDesc;

        static EntityQueryDesc EntityGuidQueryDesc
        {
            get
            {
                return s_EntityGuidQueryDesc ?? (s_EntityGuidQueryDesc = new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        typeof(EntityGuid)
                    },
                    Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab
                });
            }
        }

        protected override void OnCreate()
        {
            m_BlobAssetBatchPtr = DynamicBlobAssetBatch.Allocate(Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            DynamicBlobAssetBatch.Free(m_BlobAssetBatchPtr);
            m_BlobAssetBatchPtr = null;
        }

        public void SetFramesToRetainBlobAssets(int framesToRetainBlobAssets)
        {
            m_BlobAssetBatchPtr->SetFramesToRetainBlobAssets(framesToRetainBlobAssets);
        }

        public void AllocateBlobAsset(void* ptr, int len, ulong hash)
        {
            m_BlobAssetBatchPtr->AllocateBlobAsset(ptr, len, hash);
        }

        public void ReleaseBlobAsset(EntityManager entityManager, ulong hash)
        {
            m_BlobAssetBatchPtr->ReleaseBlobAsset(entityManager, hash);
        }

        public bool TryGetBlobAsset(ulong hash, out BlobAssetPtr ptr)
        {
            return m_BlobAssetBatchPtr->TryGetBlobAsset(hash, out ptr);
        }

        public void ReleaseUnusedBlobAssets()
        {
            using (var chunks = EntityManager.CreateEntityQuery(EntityGuidQueryDesc).ToArchetypeChunkArray(Allocator.TempJob))
            using (var blobAssets = EntityDiffer.GetBlobAssetsWithDistinctHash(EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore, EntityManager.GetCheckedEntityDataAccess()->ManagedComponentStore, chunks, Allocator.TempJob))
            {
                m_BlobAssetBatchPtr->RemoveUnusedBlobAssets(blobAssets.BlobAssetsMap);
            }
        }

        protected override void OnUpdate() {}
    }

    public static unsafe partial class EntityPatcher
    {
        static void ApplyBlobAssetChanges(
            EntityManager entityManager,
            NativeArray<EntityGuid> packedEntityGuids,
            NativeParallelMultiHashMap<int, Entity> packedEntities,
            NativeArray<ComponentType> packedTypes,
            NativeArray<BlobAssetChange> createdBlobAssets,
            NativeArray<byte> createdBlobAssetData,
            NativeArray<ulong> destroyedBlobAssets,
            NativeArray<BlobAssetReferenceChange> blobAssetReferenceChanges)
        {
            if (createdBlobAssets.Length == 0 && blobAssetReferenceChanges.Length == 0 && destroyedBlobAssets.Length == 0)
                return;

            s_ApplyBlobAssetChangesProfilerMarker.Begin();

            var managedObjectBlobAssetReferencePatches = new NativeParallelMultiHashMap<EntityComponentPair, ManagedObjectBlobAssetReferencePatch>(blobAssetReferenceChanges.Length, Allocator.Temp);

            var patcherBlobAssetSystem = entityManager.World.GetOrCreateSystemManaged<EntityPatcherBlobAssetSystem>();

            var blobAssetDataPtr = (byte*)createdBlobAssetData.GetUnsafePtr();

            for (var i = 0; i < createdBlobAssets.Length; i++)
            {
                if (!patcherBlobAssetSystem.TryGetBlobAsset(createdBlobAssets[i].Hash, out _))
                {
                    patcherBlobAssetSystem.AllocateBlobAsset(blobAssetDataPtr, createdBlobAssets[i].Length, createdBlobAssets[i].Hash);
                }

                blobAssetDataPtr += createdBlobAssets[i].Length;
            }

            for (var i = 0; i < destroyedBlobAssets.Length; i++)
            {
                patcherBlobAssetSystem.ReleaseBlobAsset(entityManager, destroyedBlobAssets[i]);
            }

            for (var i = 0; i < blobAssetReferenceChanges.Length; i++)
            {
                var packedComponent = blobAssetReferenceChanges[i].Component;
                var component = packedTypes[packedComponent.PackedTypeIndex];
                var targetOffset = blobAssetReferenceChanges[i].Offset;

                BlobAssetReferenceData targetBlobAssetReferenceData = default;
                if (patcherBlobAssetSystem.TryGetBlobAsset(blobAssetReferenceChanges[i].Value, out var blobAssetPtr))
                {
                    targetBlobAssetReferenceData = new BlobAssetReferenceData {m_Ptr = (byte*)blobAssetPtr.Data};
                }

                if (packedEntities.TryGetFirstValue(packedComponent.PackedEntityIndex, out var entity, out var iterator))
                {
                    do
                    {
                        if (!entityManager.Exists(entity))
                        {
                            Debug.LogWarning($"ApplyBlobAssetReferencePatches<{component}>({packedEntityGuids[packedComponent.PackedEntityIndex]}) but entity to patch does not exist.");
                        }
                        else if (!entityManager.HasComponent(entity, component))
                        {
                            Debug.LogWarning($"ApplyBlobAssetReferencePatches<{component}>({packedEntityGuids[packedComponent.PackedEntityIndex]}) but component in entity to patch does not exist.");
                        }
                        else
                        {
                            if (component.IsBuffer)
                            {
                                var pointer = (byte*)entityManager.GetBufferRawRW(entity, component.TypeIndex);
                                UnsafeUtility.MemCpy(pointer + targetOffset, &targetBlobAssetReferenceData, sizeof(BlobAssetReferenceData));
                            }
                            else if (component.IsManagedComponent || component.IsSharedComponent)
                            {
                                managedObjectBlobAssetReferencePatches.Add(
                                    new EntityComponentPair { Entity = entity, Component = component },
                                    new ManagedObjectBlobAssetReferencePatch { Id = targetOffset, Target = blobAssetReferenceChanges[i].Value });
                            }
                            else
                            {
                                var pointer = (byte*)entityManager.GetComponentDataRawRW(entity, component.TypeIndex);
                                UnsafeUtility.MemCpy(pointer + targetOffset, &targetBlobAssetReferenceData, sizeof(BlobAssetReferenceData));
                            }
                        }
                    }
                    while (packedEntities.TryGetNextValue(out entity, ref iterator));
                }
            }
            s_ApplyBlobAssetChangesProfilerMarker.End();

            var managedObjectPatcher = new ManagedObjectBlobAssetReferencePatcher(patcherBlobAssetSystem);

            // Apply all managed entity patches
            using (var keys = managedObjectBlobAssetReferencePatches.GetKeyArray(Allocator.Temp))
            {
                keys.Sort();
                var uniqueCount = keys.Unique();

                for (var i = 0; i < uniqueCount; i++)
                {
                    var pair = keys[i];
                    var patches = managedObjectBlobAssetReferencePatches.GetValuesForKey(pair);

                    if (pair.Component.IsManagedComponent)
                    {
                        var obj = entityManager.GetComponentObject<object>(pair.Entity, pair.Component);
                        managedObjectPatcher.ApplyPatches(ref obj, patches);
                    }
                    else if (pair.Component.TypeIndex.IsManagedSharedComponent)
                    {
                        var obj = entityManager.GetSharedComponentData(pair.Entity, pair.Component.TypeIndex);
                        managedObjectPatcher.ApplyPatches(ref obj, patches);
                        entityManager.SetSharedComponentDataBoxedDefaultMustBeNull(pair.Entity, pair.Component.TypeIndex, obj);
                    }
                    else if (pair.Component.IsSharedComponent && !pair.Component.TypeIndex.IsManagedSharedComponent)
                    {
                        var access = entityManager.GetCheckedEntityDataAccess();
                        var changes = access->BeginStructuralChanges();
                        var sharedComponentIndex = access->EntityComponentStore->GetSharedComponentDataIndex(pair.Entity, pair.Component.TypeIndex);
                        var dataPtr = (byte*)access->EntityComponentStore->GetSharedComponentDataAddr_Unmanaged(sharedComponentIndex, pair.Component.TypeIndex);
                        foreach (var patch in patches)
                        {
                            var targetOffset = patch.Id;

                            BlobAssetReferenceData targetBlobAssetReferenceData;
                            if (patcherBlobAssetSystem.TryGetBlobAsset(patch.Target, out var blobAssetPtr))
                            {
                                targetBlobAssetReferenceData = new BlobAssetReferenceData {m_Ptr = (byte*)blobAssetPtr.Data};
                            }
                            UnsafeUtility.MemCpy(dataPtr + targetOffset, &targetBlobAssetReferenceData, sizeof(BlobAssetReferenceData));
                        }

                        var hashCode = 0;
                        if (dataPtr != null)
                            hashCode = TypeManager.GetHashCode(dataPtr, pair.Component.TypeIndex);

                        access->SetSharedComponentDataAddrDefaultMustBeNullDuringStructuralChange(pair.Entity, pair.Component.TypeIndex, hashCode, dataPtr);
                        access->EndStructuralChanges(ref changes);
                    }

                    patches.Dispose();
                }
            }

            managedObjectBlobAssetReferencePatches.Dispose();

            // Workaround to catch some special cases where the memory is never released. (e.g. reloading a scene, toggling live-link on/off).
            patcherBlobAssetSystem.ReleaseUnusedBlobAssets();
        }

        class ManagedObjectBlobAssetReferencePatcher : PropertyVisitor, Properties.IVisitPropertyAdapter<BlobAssetReferenceData>
        {
            EntityPatcherBlobAssetSystem m_EntityPatcherBlobAssetSystem;
            NativeParallelMultiHashMap<EntityComponentPair, ManagedObjectBlobAssetReferencePatch>.Enumerator Patches;

            public ManagedObjectBlobAssetReferencePatcher(EntityPatcherBlobAssetSystem entityPatcherBlobAssetSystem)
            {
                m_EntityPatcherBlobAssetSystem = entityPatcherBlobAssetSystem;
                AddAdapter(this);
            }

            public void ApplyPatches(ref object obj, NativeParallelMultiHashMap<EntityComponentPair, ManagedObjectBlobAssetReferencePatch>.Enumerator patches)
            {
                Patches = patches;
                PropertyContainer.Accept(this, ref obj);
            }

            void IVisitPropertyAdapter<BlobAssetReferenceData>.Visit<TContainer>(in VisitContext<TContainer, BlobAssetReferenceData> context, ref TContainer container, ref BlobAssetReferenceData value)
            {
                // Make a copy for we can re-use the enumerator
                var patches = Patches;

                foreach (var patch in patches)
                {
                    if (value.m_Align8Union == patch.Id)
                    {
                        if (m_EntityPatcherBlobAssetSystem.TryGetBlobAsset(patch.Target, out var blobAssetPtr))
                        {
                            value = new BlobAssetReferenceData {m_Ptr = (byte*) blobAssetPtr.Data};
                        }
                        else
                        {
                            value = new BlobAssetReferenceData();
                        }

                        break;
                    }
                }
            }
        }
    }
}
