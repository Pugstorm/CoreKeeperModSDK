using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

namespace Unity.Physics.Systems
{
#if !HAVOK_PHYSICS_EXISTS

    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsSimulationGroup), OrderFirst = true)]
    [CreateBefore(typeof(PhysicsInitializeGroup))]
    [CreateBefore(typeof(PhysicsCreateBodyPairsGroup))]
    [CreateBefore(typeof(PhysicsCreateContactsGroup))]
    [CreateBefore(typeof(PhysicsCreateJacobiansGroup))]
    [CreateBefore(typeof(PhysicsSolveAndIntegrateGroup))]
    [CreateBefore(typeof(BroadphaseSystem))]
    internal unsafe partial struct PhysicsSimulationPickerSystem : ISystem
    {
        public SimulationType m_SimulationType;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_SimulationType = SimulationType.NoPhysics;

            var entity = state.EntityManager.CreateEntity();

            state.EntityManager.AddComponentData(entity, new SimulationSingleton
            {
                Type = SimulationType.NoPhysics,
                m_SimulationPtr = null
            });
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<PhysicsStep>(out PhysicsStep stepComponent))
            {
                stepComponent = PhysicsStep.Default;
            }

            if (stepComponent.SimulationType != m_SimulationType)
            {
                switch (m_SimulationType)
                {
                    // signal the systems that they are disabled and schedule disposal of simulation
                    case SimulationType.NoPhysics:
                    {
                        var dummyPhysicsSimulationSystem = state.WorldUnmanaged.GetExistingUnmanagedSystem<DummySimulationSystem>();
                        ref var systemState = ref state.WorldUnmanaged.ResolveSystemStateRef(dummyPhysicsSimulationSystem);

                        state.EntityManager.GetComponentDataRW<DummySimulationData>(dummyPhysicsSimulationSystem).ValueRW.DisableSystemChain(ref systemState);
                        break;
                    }
                    case SimulationType.UnityPhysics:
                    {
                        var unityPhysicsSimulationSystem = state.WorldUnmanaged.GetExistingUnmanagedSystem<BroadphaseSystem>();

                        ref var systemState = ref state.WorldUnmanaged.ResolveSystemStateRef(unityPhysicsSimulationSystem);
                        state.EntityManager.GetComponentDataRW<BroadphaseData>(unityPhysicsSimulationSystem).ValueRW.DisableSystemChain(ref systemState);
                        break;
                    }
                    default:
                        SafetyChecks.ThrowNotSupportedException($"Simulation type {m_SimulationType} not supported!");
                        break;
                }

                m_SimulationType = stepComponent.SimulationType;

                // In case of simulation type switch, we also need to update simulation singleton entity
                SimulationSingleton simulationSingleton = new SimulationSingleton { m_SimulationPtr = null, Type = m_SimulationType };
                switch (m_SimulationType)
                {
                    case SimulationType.NoPhysics:
                    {
                        var dummyPhysicsSimulationSystem = state.WorldUnmanaged.GetExistingUnmanagedSystem<DummySimulationSystem>();
                        ref var systemState = ref state.WorldUnmanaged.ResolveSystemStateRef(dummyPhysicsSimulationSystem);

                        state.EntityManager.GetComponentDataRW<DummySimulationData>(dummyPhysicsSimulationSystem).ValueRW.EnableSystemChain(ref systemState);
                    }
                    break;
                    case SimulationType.UnityPhysics:
                    {
                        var unityPhysicsSimulationSystem = state.WorldUnmanaged.GetExistingUnmanagedSystem<BroadphaseSystem>();

                        ref var systemState = ref state.WorldUnmanaged.ResolveSystemStateRef(unityPhysicsSimulationSystem);
                        ref var broadphaseData = ref state.EntityManager.GetComponentDataRW<BroadphaseData>(unityPhysicsSimulationSystem).ValueRW;
                        broadphaseData.EnableSystemChain(ref systemState);
                        simulationSingleton.InitializeFromSimulation(ref broadphaseData.m_UnityPhysicsSimulation);
                        break;
                    }
                    default:
                        SafetyChecks.ThrowNotSupportedException($"Simulation type {m_SimulationType} not supported!");
                        break;
                }

                SystemAPI.SetSingleton<SimulationSingleton>(simulationSingleton);
            }
        }
    }

#endif

    struct BroadphaseData : IComponentData
    {
        internal bool m_SimulationDisposed;
        internal Simulation m_UnityPhysicsSimulation;

        internal unsafe void SetUnityPhysicsSystemsActivationState(bool activationState, ref SystemState broadphaseSystem)
        {
            broadphaseSystem.Enabled = activationState;

            ref SystemState narrowphaseSystem = ref broadphaseSystem.WorldUnmanaged.ResolveSystemStateRef(broadphaseSystem.WorldUnmanaged.GetExistingUnmanagedSystem<NarrowphaseSystem>());
            ref SystemState createJacobiansSystem = ref broadphaseSystem.WorldUnmanaged.ResolveSystemStateRef(broadphaseSystem.WorldUnmanaged.GetExistingUnmanagedSystem<CreateJacobiansSystem>());
            ref SystemState solveAndIntegrateSystem = ref broadphaseSystem.WorldUnmanaged.ResolveSystemStateRef(broadphaseSystem.WorldUnmanaged.GetExistingUnmanagedSystem<SolveAndIntegrateSystem>());

            narrowphaseSystem.Enabled = activationState;
            createJacobiansSystem.Enabled = activationState;
            solveAndIntegrateSystem.Enabled = activationState;
        }

        internal unsafe void DisableSystemChain(ref SystemState broadphaseSystem)
        {
            UnityEngine.Assertions.Assert.IsFalse(m_SimulationDisposed);
            m_UnityPhysicsSimulation.Dispose();
            m_SimulationDisposed = true;

            SetUnityPhysicsSystemsActivationState(false, ref broadphaseSystem);
        }

        internal unsafe void EnableSystemChain(ref SystemState broadphaseSystem)
        {
            UnityEngine.Assertions.Assert.IsTrue(m_SimulationDisposed);
            m_UnityPhysicsSimulation = Simulation.Create();
            m_SimulationDisposed = false;

            SetUnityPhysicsSystemsActivationState(true, ref broadphaseSystem);
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsCreateBodyPairsGroup))]
    [CreateAfter(typeof(PhysicsInitializeGroup))]
    internal partial struct BroadphaseSystem : ISystem
    {
        internal EntityQuery m_PhysicsColliderQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.Enabled = false;

            state.EntityManager.AddComponentData<StepInputSingleton>(state.SystemHandle, default);

            m_PhysicsColliderQuery = state.GetEntityQuery(ComponentType.ReadWrite<PhysicsCollider>());
            SystemAPI.GetSingletonRW<SimulationSingleton>();

            state.EntityManager.AddComponentData(state.SystemHandle, new BroadphaseData
            {
                m_SimulationDisposed = true,
            });
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            ref var broadphaseData = ref SystemAPI.GetSingletonRW<BroadphaseData>().ValueRW;
            if (!broadphaseData.m_SimulationDisposed)
            {
                broadphaseData.m_UnityPhysicsSimulation.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<PhysicsStep>(out PhysicsStep stepComponent))
            {
                stepComponent = PhysicsStep.Default;
            }

            var buildPhysicsData = SystemAPI.GetSingleton<BuildPhysicsWorldData>();

            bool multiThreaded = stepComponent.MultiThreaded > 0;
            SimulationStepInput stepInput = new SimulationStepInput()
            {
                World = SystemAPI.GetSingletonRW<PhysicsWorldSingleton>().ValueRW.PhysicsWorld,
                TimeStep = SystemAPI.Time.DeltaTime,
                Gravity = stepComponent.Gravity,
                SynchronizeCollisionWorld = stepComponent.SynchronizeCollisionWorld > 0,
                NumSolverIterations = stepComponent.SolverIterationCount,
                SolverStabilizationHeuristicSettings = stepComponent.SolverStabilizationHeuristicSettings,
                HaveStaticBodiesChanged = buildPhysicsData.PhysicsData.HaveStaticBodiesChanged
            };

            // Chain the previous frame's disposes before new frame can allocate.
            ref var broadphaseData = ref SystemAPI.GetSingletonRW<BroadphaseData>().ValueRW;
            state.Dependency = broadphaseData.m_UnityPhysicsSimulation.ScheduleBroadphaseJobs(stepInput,
                    state.Dependency, multiThreaded)
                .FinalExecutionHandle;

            SystemAPI.SetSingleton<StepInputSingleton>(new StepInputSingleton { StepInput = stepInput, MultiThreaded = multiThreaded });
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsCreateContactsGroup))]
    [CreateAfter(typeof(PhysicsInitializeGroup))]
    internal partial struct NarrowphaseSystem : ISystem
    {
        internal EntityQuery m_PhysicsColliderQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.Enabled = false;

            m_PhysicsColliderQuery = state.GetEntityQuery(ComponentType.ReadWrite<PhysicsCollider>());
            SystemAPI.GetSingletonRW<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var simSingleton = SystemAPI.GetSingletonRW<SimulationSingleton>().ValueRW;
            var stepInputSingleton = SystemAPI.GetSingletonRW<StepInputSingleton>().ValueRO;

            unsafe
            {
                state.Dependency = simSingleton.AsSimulationPtr()->ScheduleNarrowphaseJobs(stepInputSingleton.StepInput, state.Dependency, stepInputSingleton.MultiThreaded).FinalExecutionHandle;
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsCreateJacobiansGroup))]
    [CreateAfter(typeof(PhysicsInitializeGroup))]
    internal partial struct CreateJacobiansSystem : ISystem
    {
        internal EntityQuery m_PhysicsColliderQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.Enabled = false;

            m_PhysicsColliderQuery = state.GetEntityQuery(ComponentType.ReadWrite<PhysicsCollider>());
            SystemAPI.GetSingletonRW<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var simSingleton = SystemAPI.GetSingletonRW<SimulationSingleton>().ValueRW;
            var stepInputSingleton = SystemAPI.GetSingletonRW<StepInputSingleton>().ValueRO;

            unsafe
            {
                state.Dependency = simSingleton.AsSimulationPtr()->ScheduleCreateJacobiansJobs(stepInputSingleton.StepInput, state.Dependency, stepInputSingleton.MultiThreaded).FinalExecutionHandle;
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsSolveAndIntegrateGroup))]
    [CreateAfter(typeof(PhysicsInitializeGroup))]
    internal partial struct SolveAndIntegrateSystem : ISystem
    {
        internal EntityQuery m_PhysicsColliderQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.Enabled = false;

            m_PhysicsColliderQuery = state.GetEntityQuery(ComponentType.ReadWrite<PhysicsCollider>());
            SystemAPI.GetSingletonRW<PhysicsWorldSingleton>();
            SystemAPI.GetSingletonRW<BuildPhysicsWorldData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var simSingleton = SystemAPI.GetSingletonRW<SimulationSingleton>().ValueRW;
            var stepInputSingleton = SystemAPI.GetSingletonRW<StepInputSingleton>().ValueRO;

            unsafe
            {
                state.Dependency = simSingleton.AsSimulationPtr()->ScheduleSolveAndIntegrateJobs(stepInputSingleton.StepInput, state.Dependency, stepInputSingleton.MultiThreaded).FinalExecutionHandle;
            }
        }
    }
}
