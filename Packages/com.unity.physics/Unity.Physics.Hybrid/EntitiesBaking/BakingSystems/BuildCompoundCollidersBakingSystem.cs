using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

using UnityEngine.Profiling;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Physics.Authoring
{
    /// <summary>
    /// Baking system for colliders: Stage 3 Compound collider baking system.
    /// </summary>
    [UpdateBefore(typeof(EndColliderBakingSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial class BuildCompoundCollidersBakingSystem : SystemBase
    {
        BeginColliderBakingSystem m_BeginColliderBakingSystem;

        BlobAssetComputationContext<int, Collider> BlobComputationContext =>
            m_BeginColliderBakingSystem.BlobComputationContext;

        EntityQuery m_RebakedRootQuery;
        EntityQuery m_RootQuery;
        EntityQuery m_ColliderSourceQuery;
        ComponentTypeSet m_StaticCleanUpTypes;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_BeginColliderBakingSystem = World.GetOrCreateSystemManaged<BeginColliderBakingSystem>();
            m_RebakedRootQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<PhysicsRootBaked>() },
                Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
            });
            m_RootQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<PhysicsCompoundData>() },
                Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
            });
            m_ColliderSourceQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<PhysicsColliderBakedData>() },
                Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
            });

            m_StaticCleanUpTypes = new ComponentTypeSet(
                typeof(PhysicsWorldIndex),
                typeof(PhysicsCollider),
                typeof(PhysicsColliderKeyEntityPair)
            );
        }

        struct ChildInstance : IComparable<ChildInstance>
        {
            public Hash128 Hash;
            public bool IsLeaf;
            public CompoundCollider.ColliderBlobInstance Child;

            public int CompareTo(ChildInstance other) => Hash.CompareTo(other.Hash);
        }

        struct DeferredCompoundResult
        {
            public Hash128 Hash;
            public BlobAssetReference<Collider> Result;
        }

        /// <summary>
        /// A job that processes all PhysicsColliderBakedData components and adds a ChildInstance to a writer. This writer
        /// keeps track of all the children on a root entity. Each ChildInstance contains the hash of the parent
        /// blob and also adds a CompoundCollider.ColliderBlobInstance for the child.
        /// </summary>
        [WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)]
        partial struct ChildrenGatheringJobHandleJob : IJobEntity
        {
            [ReadOnly] public NativeParallelHashMap<Entity, int> rootEntitiesLookUp;
            [ReadOnly] public BlobAssetComputationContext<int, Collider> blobComputationContext;
            public NativeParallelMultiHashMap<Entity, ChildInstance>.ParallelWriter childrenPerRootWriter;
            private void Execute(in PhysicsColliderBakedData blobBakingData)
            {
                // Check if we care about this entity
                if (rootEntitiesLookUp.ContainsKey(blobBakingData.BodyEntity))
                {
                    blobComputationContext.GetBlobAsset(blobBakingData.Hash, out var blobAsset);
                    childrenPerRootWriter.Add(blobBakingData.BodyEntity, new ChildInstance()
                    {
                        Hash = blobBakingData.Hash,
                        IsLeaf = blobBakingData.IsLeafEntityBody,
                        Child = new CompoundCollider.ColliderBlobInstance
                        {
                            Collider = blobAsset,
                            CompoundFromChild = blobBakingData.BodyFromShape,
                            Entity = blobBakingData.ChildEntity,
                        }
                    });
                }
            }
        }

        /// <summary>
        /// For each entity that has a PhysicsCompoundData component and a PhysicsCollider, this job gathers all
        /// ChildInstance data (from ChildrenGatheringJobHandleJob) and processes the child colliders to either: add
        /// hashes, create compound colliders and determine if the BlobAsset needs to be calculated, or it removes the
        /// flags that indicate this work needs to be done.
        /// </summary>
        [WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)]
        partial struct BlobCalculationJobHandleJob : IJobEntity
        {
            [ReadOnly] public NativeParallelMultiHashMap<Entity, ChildInstance> childrenPerRoot;
            [ReadOnly] public BlobAssetComputationContext<int, Collider> blobComputationContext;
            [ReadOnly] public NativeParallelHashMap<Entity, int> rootEntitiesLookUp;
            public NativeParallelHashMap<Hash128, int>.ParallelWriter deduplicationHashMapWriter;
            [NativeDisableParallelForRestriction] public NativeArray<DeferredCompoundResult> deferredCompoundResults;

            private void Execute(Entity rootEntity, ref PhysicsCompoundData rootBaking, ref PhysicsCollider rootCollider)
            {
                // Reset values in case of incremental
                rootCollider.Value = default;
                rootBaking.Hash = default;
                rootBaking.AssociateBlobToBody = false;
                rootBaking.DeferredCompoundBlob = false;
                rootBaking.RegisterBlob = false;

                if (childrenPerRoot.TryGetFirstValue(rootEntity, out var instance, out var it))
                {
                    // Look ahead to check if there is only one child and if it is a leaf, as this is a fast path
                    var itLookAhead = it;
                    if (instance.IsLeaf && !childrenPerRoot.TryGetNextValue(out var other, ref itLookAhead))
                    {
                        // Fast Path, we can reuse the blob data straightaway
                        rootCollider.Value = instance.Child.Collider;
                        rootBaking.Hash = instance.Hash;
                        rootBaking.AssociateBlobToBody = false;
                        rootBaking.DeferredCompoundBlob = false;
                    }
                    else
                    {
                        // We need to store the data in a list, so it can be sorted
                        var colliders = new NativeList<ChildInstance>(1, Allocator.Temp);
                        colliders.Add(instance);
                        while (childrenPerRoot.TryGetNextValue(out instance, ref it))
                        {
                            colliders.Add(instance);
                        }

                        // sort children by hash to ensure deterministic results
                        // required because instance ID on hash map key is non-deterministic between runs,
                        // but it affects the order of values returned by NativeParallelMultiHashMap
                        colliders.Sort();

                        // Calculate compound hash
                        var hashGenerator = new xxHash3.StreamingState(false);
                        foreach (var collider in colliders)
                        {
                            hashGenerator.Update(collider.Hash);
                            hashGenerator.Update(collider.Child.CompoundFromChild);
                        }

                        Hash128 compoundHash = new Hash128(hashGenerator.DigestHash128());
                        rootBaking.Hash = compoundHash;
                        rootBaking.AssociateBlobToBody = true;

                        if (blobComputationContext.NeedToComputeBlobAsset(compoundHash))
                        {
                            // We need to deduplicate before creating new compound colliders
                            int rootIndex = rootEntitiesLookUp[rootEntity];

                            if (!deduplicationHashMapWriter.TryAdd(rootBaking.Hash, rootIndex))
                            {
                                // We are not the first one to try adding this hash, so we are not responsible for calculating the compound blob and we will need to collect it later
                                rootBaking.DeferredCompoundBlob = true;
                                rootBaking.RegisterBlob = false;
                            }
                            else
                            {
                                // Calculate the Compound Collider
                                rootBaking.DeferredCompoundBlob = false;
                                rootBaking.RegisterBlob = true;
                                var colliderBlobs = new NativeArray<CompoundCollider.ColliderBlobInstance>(colliders.Length, Allocator.Temp);
                                for (int index = 0; index < colliders.Length; ++index)
                                {
                                    colliderBlobs[index] = colliders[index].Child;
                                }

                                // Create compound collider
                                // Note: by always using the same blob id here we ensure the collider blob can be shared among PhysicsCollider components
                                // in all scenarios (e.g., the differ comparing the memory and deciding that the blob is the same)
                                rootCollider.Value = CompoundCollider.CreateInternal(colliderBlobs, ColliderConstants.k_SharedBlobID);
                                deferredCompoundResults[rootIndex] = new DeferredCompoundResult()
                                {
                                    Hash = compoundHash,
                                    Result = rootCollider.Value
                                };
                                colliderBlobs.Dispose();
                            }
                        }
                        else
                        {
                            // Look up the blob in the BlobComputationContext
                            blobComputationContext.GetBlobAsset(compoundHash, out var blob);
                            rootCollider.Value = blob;
                            rootBaking.DeferredCompoundBlob = false;
                            rootBaking.RegisterBlob = false;
                        }
                        colliders.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// A job that goes through all the PhysicsCompoundData components and creates a buffer of
        /// PhysicsColliderKeyEntityPair for all of the entities where AssociatedBlobToBody is true
        /// </summary>
        [BurstCompile]
        struct AddColliderKeyEntityPairBufferJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<PhysicsCompoundData> PhysicsCompoundDataHandle;
            public EntityTypeHandle Entities;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(Entities);
                var compoundDataArray = chunk.GetNativeArray(ref PhysicsCompoundDataHandle);
                for (int i = 0; i < entities.Length; ++i)
                {
                    if (compoundDataArray[i].AssociateBlobToBody)
                    {
                        ECB.AddBuffer<PhysicsColliderKeyEntityPair>(unfilteredChunkIndex, entities[i]);
                    }
                }
            }
        };

        [WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)]
        partial struct DeferredResolutionJobHandleJob : IJobEntity
        {
            [ReadOnly] public NativeParallelHashMap<Hash128, int> deduplicationHashMap;
            [ReadOnly] public NativeArray<DeferredCompoundResult> deferredCompoundResults;

            private void Execute(ref DynamicBuffer<PhysicsColliderKeyEntityPair> colliderKeyEntityBuffer, ref PhysicsCollider rootCollider, in PhysicsCompoundData rootBaking)
            {
                colliderKeyEntityBuffer.Clear();

                if (rootBaking.AssociateBlobToBody)
                {
                    if (rootBaking.DeferredCompoundBlob)
                    {
                        // We need to collect the compound blob
                        int resultIndex = deduplicationHashMap[rootBaking.Hash];
                        rootCollider.Value = deferredCompoundResults[resultIndex].Result;
                    }

                    // Fill in the children colliders
                    unsafe
                    {
                        var compoundCollider = (CompoundCollider*)rootCollider.ColliderPtr;
                        for (int i = 0; i < compoundCollider->NumChildren; i++)
                        {
                            ref var child = ref compoundCollider->Children[i];
                            colliderKeyEntityBuffer.Add(new PhysicsColliderKeyEntityPair()
                            {
                                Entity = child.Entity,
                                Key = compoundCollider->ConvertChildIndexToColliderKey(i)
                            });

                            // Note: Once we populated the PhysicsColliderKeyEntityPair buffer,
                            // we reset the Entity members in the collider blob to Entity.Null since they will not be guaranteed to
                            // be valid after baking. Only Entities which are directly within components will be automatically
                            // updated by the Entities framework when their internal IDs change.
                            child.Entity = Entity.Null;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// For each entity with a PhysicsCompoundData component, if the blob is associated to a collider and if the
        /// compound collider has been calculated, then register the blob asset with the BlobAssetStore.
        /// </summary>
        [WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)]
        partial struct BlobContextUpdateJobHandleJob : IJobEntity
        {
            public BlobAssetComputationContext<int, Collider> blobComputationContext;
            [ReadOnly] public NativeParallelHashMap<Hash128, int> deduplicationHashMap;
            [ReadOnly] public NativeArray<DeferredCompoundResult> deferredCompoundResults;

            private void Execute(in PhysicsCompoundData rootBaking)
            {
                if (rootBaking.AssociateBlobToBody && rootBaking.RegisterBlob)
                {
                    // Register the blob asset if needed
                    int resultIndex = deduplicationHashMap[rootBaking.Hash];
                    blobComputationContext.AddComputedBlobAsset(rootBaking.Hash, deferredCompoundResults[resultIndex].Result);
                }
            }
        }
        protected override void OnUpdate()
        {
            Profiler.BeginSample("Build Compound Colliders");

            int maxRootCount = m_RootQuery.CalculateEntityCount();

            // Collect the root entities that need regeneration because they were rebaked
            using var rootEntities = m_RebakedRootQuery.ToEntityArray(Allocator.TempJob);
            NativeParallelHashMap<Entity, int> rootEntitiesLookUp = new NativeParallelHashMap<Entity, int>(maxRootCount, Allocator.TempJob);
            int count = 0;
            foreach (var rootEntity in rootEntities)
            {
                rootEntitiesLookUp.Add(rootEntity, count);
                ++count;
            }

            // Collect all the entities that have PhysicsColliderBakedData components, and check if the root entity exists
            // in the m_RootQuery. Add this entity to the rootEntitiesLookUp hashmap if it isn't there already
            if (m_ColliderSourceQuery.CalculateChunkCount() > 0)
            {
                using var rebakedColliders = m_ColliderSourceQuery.ToComponentDataArray<PhysicsColliderBakedData>(Allocator.TempJob);
                foreach (var collider in rebakedColliders)
                {
                    if (!rootEntitiesLookUp.ContainsKey(collider.BodyEntity) && m_RootQuery.Matches(collider.BodyEntity))
                    {
                        rootEntitiesLookUp.Add(collider.BodyEntity, count);
                        ++count;
                    }
                }
            }

            // Collect the relevant collider info for each root
            var maxColliderCount = m_ColliderSourceQuery.CalculateEntityCount();

            var deferredCompoundResults = new NativeArray<DeferredCompoundResult>(count, Allocator.TempJob);
            var deduplicationHashMap = new NativeParallelHashMap<Hash128, int>(maxRootCount, Allocator.TempJob);
            var deduplicationHashMapWriter = deduplicationHashMap.AsParallelWriter();

            NativeParallelMultiHashMap<Entity, ChildInstance> childrenPerRoot = new NativeParallelMultiHashMap<Entity, ChildInstance>(maxColliderCount, Allocator.TempJob);
            var childrenPerRootWriter = childrenPerRoot.AsParallelWriter();
            var blobComputationContext = BlobComputationContext;

            JobHandle childrenGatheringJobHandle = new ChildrenGatheringJobHandleJob
            {
                rootEntitiesLookUp = rootEntitiesLookUp,
                blobComputationContext = blobComputationContext,
                childrenPerRootWriter = childrenPerRootWriter
            }.ScheduleParallel(default(JobHandle));

            // We need to compose the blobs
            JobHandle blobCalculationJobHandle = new BlobCalculationJobHandleJob
            {
                childrenPerRoot = childrenPerRoot,
                blobComputationContext = blobComputationContext,
                rootEntitiesLookUp = rootEntitiesLookUp,
                deduplicationHashMapWriter = deduplicationHashMapWriter,
                deferredCompoundResults = deferredCompoundResults
            }.ScheduleParallel(childrenGatheringJobHandle);

            Profiler.EndSample();

            // Add the PhysicsColliderKeyEntityPair buffer to the root entities
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            JobHandle addBufferJobHandle = new AddColliderKeyEntityPairBufferJob
            {
                PhysicsCompoundDataHandle = GetComponentTypeHandle<PhysicsCompoundData>(true),
                Entities = GetEntityTypeHandle(),
                ECB = ecb.AsParallelWriter()
            }.Schedule(m_RootQuery, blobCalculationJobHandle);

            // Update the blob assets relation to the authoring component
            JobHandle blobContextUpdateJobHandle = new BlobContextUpdateJobHandleJob
            {
                blobComputationContext = blobComputationContext,
                deduplicationHashMap = deduplicationHashMap,
                deferredCompoundResults = deferredCompoundResults
            }.Schedule(blobCalculationJobHandle);

            var combinedJobHandle = JobHandle.CombineDependencies(blobContextUpdateJobHandle, addBufferJobHandle);

            combinedJobHandle.Complete();

            ecb.Playback(EntityManager);
            ecb.Dispose();

            // Update the PhysicsColliderKeyEntityPair buffer
            new DeferredResolutionJobHandleJob
            {
                deduplicationHashMap = deduplicationHashMap,
                deferredCompoundResults = deferredCompoundResults
            }.ScheduleParallel(addBufferJobHandle).Complete();

            // Check for unused StaticOptimizeEntity roots
            ecb = new EntityCommandBuffer(WorldUpdateAllocator);
            foreach (var(_, entity) in SystemAPI.Query<RefRO<StaticOptimizePhysicsBaking>>()
                     .WithEntityAccess()
                     .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
            {
                if (!childrenPerRoot.ContainsKey(entity))
                {
                    // There was a StaticOptimizeEntity root that was not used, so we need to clean up the added components
                    ecb.RemoveComponent(entity, m_StaticCleanUpTypes);
                }
            }
            ecb.Playback(EntityManager);
            ecb.Dispose();

            var handle = JobHandle.CombineDependencies(deduplicationHashMap.Dispose(combinedJobHandle), deferredCompoundResults.Dispose(combinedJobHandle), childrenPerRoot.Dispose(combinedJobHandle));
            Dependency = JobHandle.CombineDependencies(handle, rootEntitiesLookUp.Dispose(combinedJobHandle));
        }
    }
}
