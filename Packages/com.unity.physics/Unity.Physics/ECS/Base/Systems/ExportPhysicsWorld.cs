using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine.Assertions;

namespace Unity.Physics.Systems
{
    /// <summary>
    /// A system which copies transforms and velocities from the physics world back to the original
    /// entity components. The last system to run in <see cref="PhysicsSystemGroup"/>.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    [CreateAfter(typeof(BuildPhysicsWorld))]
    [BurstCompile]
    public partial struct ExportPhysicsWorld : ISystem
    {
        private PhysicsWorldExporter.ExportPhysicsWorldTypeHandles m_ComponentTypeHandles;

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !UNITY_PHYSICS_DISABLE_INTEGRITY_CHECKS
        private IntegrityComponentHandles m_IntegrityCheckHandles;
#endif

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_ComponentTypeHandles = new PhysicsWorldExporter.ExportPhysicsWorldTypeHandles(ref state);
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !UNITY_PHYSICS_DISABLE_INTEGRITY_CHECKS

            m_IntegrityCheckHandles = new IntegrityComponentHandles(ref state);
#endif
            // Register a ReadOnly deps on PhysicsWorldSingleton
            SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            JobHandle handle = state.Dependency;

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !UNITY_PHYSICS_DISABLE_INTEGRITY_CHECKS
            handle = CheckIntegrity(ref state, handle);
#endif
            ref var buildPhysicsData = ref SystemAPI.GetSingletonRW<BuildPhysicsWorldData>().ValueRW;
            handle = PhysicsWorldExporter.SchedulePhysicsWorldExport(ref state, ref m_ComponentTypeHandles, buildPhysicsData.PhysicsData.PhysicsWorld, handle, buildPhysicsData.PhysicsData.DynamicEntityGroup);

            // Combine implicit output dependency with user one
            state.Dependency = JobHandle.CombineDependencies(state.Dependency, handle);
        }

        #region Integrity checks

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !UNITY_PHYSICS_DISABLE_INTEGRITY_CHECKS

        internal struct IntegrityComponentHandles
        {
            public ComponentTypeHandle<LocalTransform> LocalTransformType;
            public ComponentTypeHandle<PhysicsCollider> PhysicsColliderType;
            public ComponentTypeHandle<PhysicsVelocity> PhysicsVelocityType;

            public IntegrityComponentHandles(ref SystemState state)
            {
                LocalTransformType = state.GetComponentTypeHandle<LocalTransform>(true);
                PhysicsColliderType = state.GetComponentTypeHandle<PhysicsCollider>(true);
                PhysicsVelocityType = state.GetComponentTypeHandle<PhysicsVelocity>(true);
            }

            public void Update(ref SystemState state)
            {
                LocalTransformType.Update(ref state);
                PhysicsColliderType.Update(ref state);
                PhysicsVelocityType.Update(ref state);
            }
        }

        internal JobHandle CheckIntegrity(ref SystemState state, JobHandle inputDeps)
        {
            m_IntegrityCheckHandles.Update(ref state);

            var localTransformType = m_IntegrityCheckHandles.LocalTransformType;
            var physicsColliderType = m_IntegrityCheckHandles.PhysicsColliderType;
            var physicsVelocityType = m_IntegrityCheckHandles.PhysicsVelocityType;

            var buildPhysicsData = SystemAPI.GetSingleton<BuildPhysicsWorldData>();

            var checkDynamicBodyIntegrity = new CheckDynamicBodyIntegrity
            {
                IntegrityCheckMap = buildPhysicsData.IntegrityCheckMap,
                LocalTransformType = localTransformType,
                PhysicsVelocityType = physicsVelocityType,
                PhysicsColliderType = physicsColliderType
            };

            inputDeps = checkDynamicBodyIntegrity.Schedule(buildPhysicsData.DynamicEntityGroup, inputDeps);

            var checkStaticBodyColliderIntegrity = new CheckColliderIntegrity
            {
                IntegrityCheckMap = buildPhysicsData.IntegrityCheckMap,
                PhysicsColliderType = physicsColliderType
            };

            inputDeps = checkStaticBodyColliderIntegrity.Schedule(buildPhysicsData.StaticEntityGroup, inputDeps);

            var checkTotalIntegrity = new CheckTotalIntegrity
            {
                IntegrityCheckMap = buildPhysicsData.IntegrityCheckMap
            };

            return checkTotalIntegrity.Schedule(inputDeps);
        }

        [BurstCompile]
        internal struct CheckDynamicBodyIntegrity : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<LocalTransform> LocalTransformType;
            [ReadOnly] public ComponentTypeHandle<PhysicsVelocity> PhysicsVelocityType;
            [ReadOnly] public ComponentTypeHandle<PhysicsCollider> PhysicsColliderType;
            public NativeParallelHashMap<uint, long> IntegrityCheckMap;

            internal static void DecrementIfExists(NativeParallelHashMap<uint, long> integrityCheckMap, uint systemVersion)
            {
                if (integrityCheckMap.TryGetValue(systemVersion, out long occurences))
                {
                    integrityCheckMap.Remove(systemVersion);
                    integrityCheckMap.Add(systemVersion, occurences - 1);
                }
            }

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                DecrementIfExists(IntegrityCheckMap, chunk.GetOrderVersion());
                DecrementIfExists(IntegrityCheckMap, chunk.GetChangeVersion(ref PhysicsVelocityType));

                if (chunk.Has(ref LocalTransformType))
                {
                    DecrementIfExists(IntegrityCheckMap, chunk.GetChangeVersion(ref LocalTransformType));
                }

                if (chunk.Has(ref PhysicsColliderType))
                {
                    DecrementIfExists(IntegrityCheckMap, chunk.GetChangeVersion(ref PhysicsColliderType));

                    var colliders = chunk.GetNativeArray(ref PhysicsColliderType);
                    CheckColliderFilterIntegrity(colliders);
                }
            }
        }

        [BurstCompile]
        internal struct CheckColliderIntegrity : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<PhysicsCollider> PhysicsColliderType;
            public NativeParallelHashMap<uint, long> IntegrityCheckMap;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                if (chunk.Has(ref PhysicsColliderType))
                {
                    CheckDynamicBodyIntegrity.DecrementIfExists(IntegrityCheckMap, chunk.GetChangeVersion(ref PhysicsColliderType));

                    var colliders = chunk.GetNativeArray(ref PhysicsColliderType);
                    CheckColliderFilterIntegrity(colliders);
                }
            }
        }

        [BurstCompile]
        internal struct CheckTotalIntegrity : IJob
        {
            public NativeParallelHashMap<uint, long> IntegrityCheckMap;
            public void Execute()
            {
                var values = IntegrityCheckMap.GetValueArray(Allocator.Temp);
                var validIntegrity = true;
                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i] != 0)
                    {
                        validIntegrity = false;
                        break;
                    }
                }
                if (!validIntegrity)
                {
                    SafetyChecks.ThrowInvalidOperationException("Adding/removing components or changing position/rotation/velocity/collider ECS data" +
                        " on dynamic entities during physics step");
                }
            }
        }

        // Verifies combined collision filter of compound colliders
        // ToDo: add the same for mesh once per-triangle filters are supported
        private static void CheckColliderFilterIntegrity(NativeArray<PhysicsCollider> colliders)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider.IsValid && collider.Value.Value.Type == ColliderType.Compound)
                {
                    unsafe
                    {
                        var compoundCollider = (CompoundCollider*)collider.ColliderPtr;

                        var rootFilter = compoundCollider->GetCollisionFilter();
                        var combinedFilter = CollisionFilter.Zero;

                        for (int childIndex = 0; childIndex < compoundCollider->NumChildren; childIndex++)
                        {
                            combinedFilter = CollisionFilter.CreateUnion(combinedFilter, compoundCollider->GetCollisionFilter(compoundCollider->ConvertChildIndexToColliderKey(childIndex)));
                        }

                        // GroupIndex has no concept of union. Creating one from children has no guarantees
                        // that it will be the same as the GroupIndex of the root, so we can't compare those two.
                        // Setting combinedFilter's GroupIndex to rootFilter's will exclude GroupIndex from comparing the two filters.
                        combinedFilter.GroupIndex = rootFilter.GroupIndex;

                        // Check that the combined filter (excluding GroupIndex) of all children is the same as root filter.
                        // If not, it means user has forgotten to call RefreshCollisionFilter() on the CompoundCollider.
                        if (!rootFilter.Equals(combinedFilter))
                        {
                            SafetyChecks.ThrowInvalidOperationException("CollisionFilter of a compound collider is not a union of its children. " +
                                "You must call CompoundCollider.RefreshCollisionFilter() to update the root filter after changing child filters.");
                        }
                    }
                }
            }
        }

#endif

        #endregion
    }
}
