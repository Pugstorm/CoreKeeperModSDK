using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Assertions;

namespace Unity.Physics.Systems
{
    // Make sure that:
    // 1. BuildPhysicsWorldDependencyResolver is always updated just before BuildPhysicsWorld
    // 2. BuildPhysicsWorld is the last system to be updated in [PhysicsInitializeGroup]
    // This is done to prevent race conditions if users put something to UpdateIn[PhysicsInitializeGroup].
    // They shouldn't be doing so, but it is nice to prevent unresolvable race conditions.
    [UpdateInGroup(typeof(PhysicsInitializeGroup), OrderLast = true)]
    internal partial class PhysicsInitializeGroupInternal : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(PhysicsInitializeGroupInternal), OrderFirst = true)]
    [CreateAfter(typeof(BuildPhysicsWorld))]
    [BurstCompile]
    internal partial struct BuildPhysicsWorldDependencyResolver : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            SystemAPI.GetSingletonRW<PhysicsWorldSingleton>();
            SystemAPI.GetSingletonRW<SimulationSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var buildPhysicsData = ref SystemAPI.GetSingletonRW<BuildPhysicsWorldData>().ValueRW;
            buildPhysicsData.AddInputDependencyToComplete(state.Dependency);
        }
    }

    /// <summary>
    /// Group responsible to build the physics world data and wait for any dependencies before the
    /// next simulation.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsInitializeGroupInternal), OrderLast = true)]
    [UpdateAfter(typeof(BuildPhysicsWorldDependencyResolver))]
    public partial class PhysicsBuildWorldGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// Public system data for this world's instance of a <see cref="BuildPhysicsWorld"/> system.
    ///
    /// Contains physics world data based on the entity world. The physics world data will contain a
    /// rigid body for every entity which has a rigid body component, and a joint for every entity
    /// which has a joint component.
    /// </summary>
    public struct BuildPhysicsWorldData : IComponentData
    {
        internal JobHandle m_InputDependencyToComplete;

        /// <summary>
        /// Information describing the <see cref="PhysicsWorldData"/>. Important : avoid using
        /// BuildPhysicsWorldData.PhysicsWorldData.PhysicsWorld. Use <see cref="PhysicsWorldSingleton"/>
        /// instead.
        /// </summary>
        public PhysicsWorldData PhysicsData;

        /// <summary>   A filter specifying the world. </summary>
        public PhysicsWorldIndex WorldFilter;

        /// <summary>   Gets the group the dynamic bodies belongs to. </summary>
        ///
        /// <value> The dynamic entity group. </value>
        public EntityQuery DynamicEntityGroup => PhysicsData.DynamicEntityGroup;

        /// <summary>   Gets the group the static bodies belongs to. </summary>
        ///
        /// <value> The static entity group. </value>
        public EntityQuery StaticEntityGroup => PhysicsData.StaticEntityGroup;

#if !UNITY_PHYSICS_DISABLE_JOINTS
        /// <summary>   Gets the group the joints belongs to. </summary>
        ///
        /// <value> The joint entity group. </value>
        public EntityQuery JointEntityGroup => PhysicsData.JointEntityGroup;
#endif

        /// <summary>   True if the static bodies have changed this frame. </summary>
        ///
        /// <value> The have static bodies changed flag. </value>
        public NativeReference<int> HaveStaticBodiesChanged => PhysicsData.HaveStaticBodiesChanged;

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !UNITY_PHYSICS_DISABLE_INTEGRITY_CHECKS
        internal NativeParallelHashMap<uint, long> IntegrityCheckMap;
#endif

        /// <summary>
        /// Adds the dependency that BuildPhysicsWorld will complete on the next OnUpdate() call.
        /// Multiple dependencies can be added this way (they are combined).
        /// BuildPhysicsWorld resets the PhysicsWorld immediately in the OnUpdate() method (not through jobs),
        /// so any jobs that rely on that data should use this to make sure their data is not ruined before they access it.
        /// </summary>
        internal void AddInputDependencyToComplete(JobHandle dependencyToComplete)
        {
            m_InputDependencyToComplete = JobHandle.CombineDependencies(m_InputDependencyToComplete, dependencyToComplete);
        }

        /// <summary>
        /// Complete and reset all pending input dependencies.
        /// </summary>
        public void CompleteInputDependency()
        {
            m_InputDependencyToComplete.Complete();
            m_InputDependencyToComplete = default;
        }
    }

    /// <summary>
    /// A system which builds the physics world based on the entity world. The world will contain a
    /// rigid body for every entity which has a rigid body component, and a joint for every entity
    /// which has a joint component.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsBuildWorldGroup))]
    public partial struct BuildPhysicsWorld : ISystem
    {
        private int previousNumStaticBodies;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var worldFilter = new PhysicsWorldIndex();
            var physicsData = new PhysicsWorldData(ref state, worldFilter);
            state.EntityManager.AddComponentData(state.SystemHandle, new BuildPhysicsWorldData
            {
                WorldFilter = worldFilter,
                PhysicsData = physicsData,
            });

            state.EntityManager.CreateSingleton(
                new PhysicsWorldSingleton
                {
                    PhysicsWorld = physicsData.PhysicsWorld,
                    PhysicsWorldIndex = worldFilter
                });
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            ref var buildPhysicsData = ref state.EntityManager.GetComponentDataRW<BuildPhysicsWorldData>(state.SystemHandle).ValueRW;
            buildPhysicsData.PhysicsData.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var buildPhysicsData = ref SystemAPI.GetSingletonRW<BuildPhysicsWorldData>().ValueRW;

            float timeStep = SystemAPI.Time.DeltaTime;

            if (!SystemAPI.TryGetSingleton<PhysicsStep>(out PhysicsStep stepComponent))
            {
                stepComponent = PhysicsStep.Default;
            }

            state.Dependency = JobHandle.CombineDependencies(buildPhysicsData.m_InputDependencyToComplete, state.Dependency);

            state.Dependency = PhysicsWorldBuilder.SchedulePhysicsWorldBuild(ref state, ref buildPhysicsData.PhysicsData, state.Dependency,
                timeStep, stepComponent.MultiThreaded > 0, stepComponent.Gravity, state.LastSystemVersion, ref previousNumStaticBodies);

            SystemAPI.SetSingleton(new PhysicsWorldSingleton
            {
                PhysicsWorld = buildPhysicsData.PhysicsData.PhysicsWorld,
                PhysicsWorldIndex = buildPhysicsData.WorldFilter
            });
        }
    }

    #region Integrity checks

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !UNITY_PHYSICS_DISABLE_INTEGRITY_CHECKS
    /// <summary>
    /// Schedule check for integrity jobs in the Editor or in development build if the
    /// UNITY_PHYSICS_DISABLE_INTEGRITY_CHECKS define is set.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsBuildWorldGroup), OrderLast = true)]
    [CreateAfter(typeof(BuildPhysicsWorld))]
    internal partial struct IntegrityCheckSystem : ISystem
    {
        NativeParallelHashMap<uint, long> m_IntegrityCheckMap;
        SystemHandle m_BuildSystemHandle;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_IntegrityCheckMap = new NativeParallelHashMap<uint, long>(4, Allocator.Persistent);
            m_BuildSystemHandle = state.WorldUnmanaged.GetExistingUnmanagedSystem<BuildPhysicsWorld>();
            ref var buildPhysicsWorldData = ref state.EntityManager.GetComponentDataRW<BuildPhysicsWorldData>(m_BuildSystemHandle).ValueRW;
            buildPhysicsWorldData.IntegrityCheckMap = m_IntegrityCheckMap;
            // To inject the right dependencies
            state.GetComponentLookup<PhysicsWorldSingleton>();
        }

        public void OnDestroy(ref SystemState state)
        {
            m_IntegrityCheckMap.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // these getter are not waiting for depedencies on the main thread, and that would avoid blocking for no
            // reason.
            var buildPhysicsData = SystemAPI.GetSingleton<BuildPhysicsWorldData>();
            state.Dependency = new ClearIntegrityCheckMapJob { IntegrityCheckMap = buildPhysicsData.IntegrityCheckMap }.Schedule(state.Dependency);
            // Should be un-necessary to update the handles at this point (there are no structural changes at this point)
            // has been added as extra safety measure.
            // buildPhysicsData.PhysicsData.ComponentHandles.Update(ref state);
            state.Dependency = new PhysicsIntegrityCheckJobs.RecordDynamicBodyIntegrity
            {
                IntegrityCheckMap = buildPhysicsData.IntegrityCheckMap,
                LocalTransformType = buildPhysicsData.PhysicsData.ComponentHandles.LocalTransformType,
                PhysicsVelocityType = buildPhysicsData.PhysicsData.ComponentHandles.PhysicsVelocityType,
                PhysicsColliderType = buildPhysicsData.PhysicsData.ComponentHandles.PhysicsColliderType
            }.Schedule(buildPhysicsData.PhysicsData.DynamicEntityGroup, state.Dependency);
            state.Dependency = new PhysicsIntegrityCheckJobs.RecordColliderIntegrity
            {
                IntegrityCheckMap = buildPhysicsData.IntegrityCheckMap,
                PhysicsColliderType = buildPhysicsData.PhysicsData.ComponentHandles.PhysicsColliderType
            }.Schedule(buildPhysicsData.PhysicsData.StaticEntityGroup, state.Dependency);;
        }

        [BurstCompile]
        private struct ClearIntegrityCheckMapJob : IJob
        {
            public NativeParallelHashMap<uint, long> IntegrityCheckMap;

            public void Execute()
            {
                this.IntegrityCheckMap.Clear();
            }
        }
    }

    internal static class PhysicsIntegrityCheckJobs
    {
        [BurstCompile]
        internal struct RecordDynamicBodyIntegrity : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<LocalTransform> LocalTransformType;

            [ReadOnly] public ComponentTypeHandle<PhysicsVelocity> PhysicsVelocityType;
            [ReadOnly] public ComponentTypeHandle<PhysicsCollider> PhysicsColliderType;

            public NativeParallelHashMap<uint, long> IntegrityCheckMap;

            internal static void AddOrIncrement(NativeParallelHashMap<uint, long> integrityCheckMap,
                uint systemVersion)
            {
                if (integrityCheckMap.TryGetValue(systemVersion, out long occurences))
                {
                    integrityCheckMap.Remove(systemVersion);
                    integrityCheckMap.Add(systemVersion, occurences + 1);
                }
                else
                {
                    integrityCheckMap.Add(systemVersion, 1);
                }
            }

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                AddOrIncrement(IntegrityCheckMap, chunk.GetOrderVersion());
                AddOrIncrement(IntegrityCheckMap, chunk.GetChangeVersion(ref PhysicsVelocityType));

                if (chunk.Has(ref LocalTransformType))
                {
                    AddOrIncrement(IntegrityCheckMap, chunk.GetChangeVersion(ref LocalTransformType));
                }

                if (chunk.Has(ref PhysicsColliderType))
                {
                    AddOrIncrement(IntegrityCheckMap, chunk.GetChangeVersion(ref PhysicsColliderType));
                }
            }
        }

        [BurstCompile]
        internal struct RecordColliderIntegrity : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<PhysicsCollider> PhysicsColliderType;
            public NativeParallelHashMap<uint, long> IntegrityCheckMap;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                if (chunk.Has(ref PhysicsColliderType))
                {
                    RecordDynamicBodyIntegrity.AddOrIncrement(IntegrityCheckMap,
                        chunk.GetChangeVersion(ref PhysicsColliderType));
                }
            }
        }
    }
#endif

    #endregion

    #region Analytics

#if UNITY_EDITOR && ENABLE_CLOUD_SERVICES_ANALYTICS
    internal struct PhysicsAnalyticsSingleton : IComponentData
    {
        public SimulationType m_SimulationType;
        public uint m_MaxNumberOfStaticBodiesInAScene;
        public uint m_MaxNumberOfDynamicBodiesInAScene;

        public uint m_MaxNumberOfConvexesInAScene;
        public uint m_MaxNumberOfSpheresInAScene;
        public uint m_MaxNumberOfCapsulesInAScene;
        public uint m_MaxNumberOfTrianglesInAScene;
        public uint m_MaxNumberOfQuadsInAScene;
        public uint m_MaxNumberOfBoxesInAScene;
        public uint m_MaxNumberOfCylindersInAScene;
        public uint m_MaxNumberOfMeshesInAScene;
        public uint m_MaxNumberOfCompoundsInAScene;
        public uint m_MaxNumberOfTerrainsInAScene;

        public uint m_MaxNumberOfLinearConstraintsInAScene;
        public uint m_MaxNumberOfAngularConstraintsInAScene;
        public uint m_MaxNumberOfRotationMotorsInAScene;
        public uint m_MaxNumberOfAngularVelocityMotorsInAScene;
        public uint m_MaxNumberOfPositionMotorsInAScene;
        public uint m_MaxNumberOfLinearVelocityMotorsInAScene;

        public void Clear()
        {
            m_SimulationType = SimulationType.NoPhysics;
            m_MaxNumberOfStaticBodiesInAScene = 0;
            m_MaxNumberOfDynamicBodiesInAScene = 0;
            m_MaxNumberOfConvexesInAScene = 0;
            m_MaxNumberOfSpheresInAScene = 0;
            m_MaxNumberOfCapsulesInAScene = 0;
            m_MaxNumberOfTrianglesInAScene = 0;
            m_MaxNumberOfQuadsInAScene = 0;
            m_MaxNumberOfBoxesInAScene = 0;
            m_MaxNumberOfCylindersInAScene = 0;
            m_MaxNumberOfMeshesInAScene = 0;
            m_MaxNumberOfCompoundsInAScene = 0;
            m_MaxNumberOfTerrainsInAScene = 0;
            m_MaxNumberOfLinearConstraintsInAScene = 0;
            m_MaxNumberOfAngularConstraintsInAScene = 0;
            m_MaxNumberOfRotationMotorsInAScene = 0;
            m_MaxNumberOfAngularVelocityMotorsInAScene = 0;
            m_MaxNumberOfPositionMotorsInAScene = 0;
            m_MaxNumberOfLinearVelocityMotorsInAScene = 0;
        }
    }

    [UpdateInGroup(typeof(PhysicsInitializeGroupInternal), OrderLast = true)]
    [UpdateAfter(typeof(PhysicsBuildWorldGroup))]
    internal partial struct PhysicsAnalyticsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var physicsAnalyticsSingleton = new PhysicsAnalyticsSingleton();
            physicsAnalyticsSingleton.Clear();
            state.EntityManager.CreateSingleton(physicsAnalyticsSingleton);

            // To inject the right dependencies
            state.GetComponentLookup<PhysicsWorldSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out PhysicsStep stepComponent))
            {
                stepComponent = PhysicsStep.Default;
            }

            // Store simulation type
            var analyticsData = SystemAPI.GetSingletonRW<PhysicsAnalyticsSingleton>();
            analyticsData.ValueRW.m_SimulationType = stepComponent.SimulationType;


            var buildPhysicsData = SystemAPI.GetSingleton<BuildPhysicsWorldData>();

#if !UNITY_PHYSICS_DISABLE_JOINTS
            var jointAnalyticsJob = new AnalyticsJobs.PhysicsJointsAnalyticsJob
            {
                Joints = buildPhysicsData.PhysicsData.PhysicsWorld.Joints,
                PhysicsAnalyticsSingleton = analyticsData
            }.Schedule(state.Dependency);
#endif

            var bodiesAnalyticsJob = new AnalyticsJobs.PhysicsBodiesAnalyticsJob
            {
                World = buildPhysicsData.PhysicsData.PhysicsWorld,
                PhysicsAnalyticsSingleton = analyticsData
            }.Schedule(state.Dependency);

#if !UNITY_PHYSICS_DISABLE_JOINTS
            state.Dependency = JobHandle.CombineDependencies(jointAnalyticsJob, bodiesAnalyticsJob);
#else
            state.Dependency = bodiesAnalyticsJob;
#endif
        }
    }

    [BurstCompile]
    internal static class AnalyticsJobs
    {
#if !UNITY_PHYSICS_DISABLE_JOINTS
        [BurstCompile]
        internal struct PhysicsJointsAnalyticsJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public RefRW<PhysicsAnalyticsSingleton> PhysicsAnalyticsSingleton;
            [ReadOnly] public NativeArray<Joint> Joints;

            [BurstCompile]
            public void Execute()
            {
                uint numLinearConstraints = 0;
                uint numAngularConstraints = 0;
                uint numPositionMotors = 0;
                uint numRotationMotors = 0;
                uint numAngularVelocityMotors = 0;
                uint numLinearVelocityMotors = 0;
                for (int i = 0; i < Joints.Length; i++)
                {
                    Joint joint = Joints[i];
                    var constraints = joint.Constraints.GetConstraints();

                    for (int j = 0; j < constraints.Length; j++)
                    {
                        var constraint = constraints[j];

                        switch (constraint.Type)
                        {
                            case ConstraintType.Linear:
                                numLinearConstraints++;
                                break;
                            case ConstraintType.Angular:
                                numAngularConstraints++;
                                break;
                            case ConstraintType.PositionMotor:
                                numPositionMotors++;
                                break;
                            case ConstraintType.RotationMotor:
                                numRotationMotors++;
                                break;
                            case ConstraintType.AngularVelocityMotor:
                                numAngularVelocityMotors++;
                                break;
                            case ConstraintType.LinearVelocityMotor:
                                numLinearVelocityMotors++;
                                break;
                        }
                    }
                }

                PhysicsAnalyticsSingleton.ValueRW.m_MaxNumberOfLinearConstraintsInAScene = math.max(
                    PhysicsAnalyticsSingleton.ValueRO.m_MaxNumberOfLinearConstraintsInAScene, numLinearConstraints);
                PhysicsAnalyticsSingleton.ValueRW.m_MaxNumberOfAngularConstraintsInAScene = math.max(
                    PhysicsAnalyticsSingleton.ValueRO.m_MaxNumberOfAngularConstraintsInAScene,
                    numAngularConstraints);
                PhysicsAnalyticsSingleton.ValueRW.m_MaxNumberOfPositionMotorsInAScene = math.max(
                    PhysicsAnalyticsSingleton.ValueRO.m_MaxNumberOfPositionMotorsInAScene, numPositionMotors);
                PhysicsAnalyticsSingleton.ValueRW.m_MaxNumberOfRotationMotorsInAScene = math.max(
                    PhysicsAnalyticsSingleton.ValueRO.m_MaxNumberOfRotationMotorsInAScene, numRotationMotors);
                PhysicsAnalyticsSingleton.ValueRW.m_MaxNumberOfAngularVelocityMotorsInAScene = math.max(
                    PhysicsAnalyticsSingleton.ValueRO.m_MaxNumberOfAngularVelocityMotorsInAScene,
                    numAngularVelocityMotors);
                PhysicsAnalyticsSingleton.ValueRW.m_MaxNumberOfLinearVelocityMotorsInAScene = math.max(
                    PhysicsAnalyticsSingleton.ValueRO.m_MaxNumberOfLinearVelocityMotorsInAScene,
                    numLinearVelocityMotors);
            }
        }
#endif

        [BurstCompile]
        internal struct PhysicsBodiesAnalyticsJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public RefRW<PhysicsAnalyticsSingleton> PhysicsAnalyticsSingleton;
            [ReadOnly] public PhysicsWorld World;

            [BurstCompile]
            public void Execute()
            {
                uint numConvexes = 0;
                uint numSpheres = 0;
                uint numCapsules = 0;
                uint numTriangles = 0;
                uint numQuads = 0;
                uint numBoxes = 0;
                uint numCylinders = 0;
                uint numMeshes = 0;
                uint numCompounds = 0;
                uint numTerrains = 0;

                // Note: There is always a default rigid body in the world, which has a null collider.
                // This check prevents issues in an extremely rare case when a physics world is reset by the user
                // and filled with uninitialized memory.
                if (World.NumBodies <= 1) return;

                PhysicsAnalyticsSingleton.ValueRW.m_MaxNumberOfStaticBodiesInAScene = math.max(
                    PhysicsAnalyticsSingleton.ValueRO.m_MaxNumberOfStaticBodiesInAScene,
                    (uint)World.NumStaticBodies);
                PhysicsAnalyticsSingleton.ValueRW.m_MaxNumberOfDynamicBodiesInAScene = math.max(
                    PhysicsAnalyticsSingleton.ValueRO.m_MaxNumberOfDynamicBodiesInAScene,
                    (uint)World.NumDynamicBodies);

                for (int i = 0; i < World.Bodies.Length; i++)
                {
                    var body = World.Bodies[i];
                    if (!body.Collider.IsCreated)
                        continue;

                    switch (body.Collider.Value.Type)
                    {
                        case ColliderType.Convex:
                            numConvexes++;
                            break;
                        case ColliderType.Sphere:
                            numSpheres++;
                            break;
                        case ColliderType.Capsule:
                            numCapsules++;
                            break;
                        case ColliderType.Triangle:
                            numTriangles++;
                            break;
                        case ColliderType.Quad:
                            numQuads++;
                            break;
                        case ColliderType.Box:
                            numBoxes++;
                            break;
                        case ColliderType.Cylinder:
                            numCylinders++;
                            break;
                        case ColliderType.Mesh:
                            numMeshes++;
                            break;
                        case ColliderType.Compound:
                            numCompounds++;
                            break;
                        case ColliderType.Terrain:
                            numTerrains++;
                            break;
                    }
                }

                PhysicsAnalyticsSingleton.ValueRW.m_MaxNumberOfConvexesInAScene =
                    math.max(PhysicsAnalyticsSingleton.ValueRO.m_MaxNumberOfConvexesInAScene, numConvexes);
                PhysicsAnalyticsSingleton.ValueRW.m_MaxNumberOfSpheresInAScene =
                    math.max(PhysicsAnalyticsSingleton.ValueRO.m_MaxNumberOfSpheresInAScene, numSpheres);
                PhysicsAnalyticsSingleton.ValueRW.m_MaxNumberOfCapsulesInAScene =
                    math.max(PhysicsAnalyticsSingleton.ValueRO.m_MaxNumberOfCapsulesInAScene, numCapsules);
                PhysicsAnalyticsSingleton.ValueRW.m_MaxNumberOfTrianglesInAScene =
                    math.max(PhysicsAnalyticsSingleton.ValueRO.m_MaxNumberOfTrianglesInAScene, numTriangles);
                PhysicsAnalyticsSingleton.ValueRW.m_MaxNumberOfQuadsInAScene =
                    math.max(PhysicsAnalyticsSingleton.ValueRO.m_MaxNumberOfQuadsInAScene, numQuads);
                PhysicsAnalyticsSingleton.ValueRW.m_MaxNumberOfBoxesInAScene =
                    math.max(PhysicsAnalyticsSingleton.ValueRO.m_MaxNumberOfBoxesInAScene, numBoxes);
                PhysicsAnalyticsSingleton.ValueRW.m_MaxNumberOfCylindersInAScene =
                    math.max(PhysicsAnalyticsSingleton.ValueRO.m_MaxNumberOfCylindersInAScene, numCylinders);
                PhysicsAnalyticsSingleton.ValueRW.m_MaxNumberOfMeshesInAScene =
                    math.max(PhysicsAnalyticsSingleton.ValueRO.m_MaxNumberOfMeshesInAScene, numMeshes);
                PhysicsAnalyticsSingleton.ValueRW.m_MaxNumberOfCompoundsInAScene =
                    math.max(PhysicsAnalyticsSingleton.ValueRO.m_MaxNumberOfCompoundsInAScene, numCompounds);
                PhysicsAnalyticsSingleton.ValueRW.m_MaxNumberOfTerrainsInAScene =
                    math.max(PhysicsAnalyticsSingleton.ValueRO.m_MaxNumberOfTerrainsInAScene, numTerrains);
            }
        }
    }

#endif //UNITY_EDITOR && ENABLE_CLOUD_SERVICES_ANALYTICS

    #endregion

    struct DummySimulationData : IComponentData
    {
        byte dummyData; // Without data, the component is zero-sized, and accessing it will result in error
        internal DummySimulation m_Simulation;

        internal unsafe void DisableSystemChain(ref SystemState systemStateRef)
        {
            systemStateRef.Enabled = false;
            m_Simulation.Dispose();
        }

        internal unsafe void EnableSystemChain(ref SystemState systemStateRef)
        {
            systemStateRef.Enabled = true;
            m_Simulation = new DummySimulation();
        }
    }

    [UpdateInGroup(typeof(PhysicsSimulationGroup))]
    [BurstCompile]
    internal partial struct DummySimulationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.EntityManager.AddComponentData(state.SystemHandle, new DummySimulationData
            {
                // This one is enabled by default
                m_Simulation = new DummySimulation(),
            });
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            state.EntityManager.GetComponentDataRW<DummySimulationData>(state.SystemHandle).ValueRW.m_Simulation
                .Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // do nothing;
        }
    }
}
