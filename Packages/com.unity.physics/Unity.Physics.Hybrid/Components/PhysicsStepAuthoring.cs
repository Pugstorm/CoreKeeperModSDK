using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Physics.PhysicsStep;

namespace Unity.Physics.Authoring
{
    /// <summary>
    ///     Parameters describing how to step the physics simulation.<para/>
    ///     If this component is not present, default values will be used.
    /// </summary>
    [AddComponentMenu("Entities/Physics/Physics Step")]
    [DisallowMultipleComponent]
    [HelpURL(HelpURLs.PhysicsStepAuthoring)]
    public sealed class PhysicsStepAuthoring : MonoBehaviour
    {
        PhysicsStepAuthoring() {}

        /// <summary>
        ///     Specifies the type of physics engine to be used.
        /// </summary>
        public SimulationType SimulationType
        {
            get => m_SimulationType;
            set => m_SimulationType = value;
        }
        [SerializeField]
        [Tooltip("Specifies the type of physics engine to be used.")]
        SimulationType m_SimulationType = Default.SimulationType;

        /// <summary>
        ///     Specifies the amount of gravity present in the physics simulation.
        /// </summary>
        public float3 Gravity
        {
            get => m_Gravity;
            set => m_Gravity = value;
        }
        [SerializeField]
        [Tooltip("Specifies the amount of gravity present in the physics simulation.")]
        float3 m_Gravity = Default.Gravity;

        /// <summary>
        ///     Specifies the number of solver iterations the physics engine will perform.<para/>
        ///     Higher values mean more stability, but also worse performance.
        /// </summary>
        public int SolverIterationCount
        {
            get => m_SolverIterationCount;
            set => m_SolverIterationCount = value;
        }
        [SerializeField]
        [Tooltip("Specifies the number of solver iterations the physics engine will perform.\n" +
            "Higher values mean more stability, but also worse performance.")]
        int m_SolverIterationCount = Default.SolverIterationCount;


        /// <summary>
        ///    Enables the contact solver stabilization heuristic.
        /// </summary>
        public bool EnableSolverStabilizationHeuristic
        {
            get => m_EnableSolverStabilizationHeuristic;
            set => m_EnableSolverStabilizationHeuristic = value;
        }
        [SerializeField]
        [Tooltip("Enables the contact solver stabilization heuristic.")]
        bool m_EnableSolverStabilizationHeuristic = Default.SolverStabilizationHeuristicSettings.EnableSolverStabilization;

        /// <summary>
        ///     Enables multi-threaded processing.<para/>
        ///     Enabling this option will maximize the use of parallelization in the entire simulation pipeline while disabling it will result in minimal thread usage.
        /// </summary>
        public bool MultiThreaded
        {
            get => m_MultiThreaded;
            set => m_MultiThreaded = value;
        }
        [SerializeField]
        [Tooltip("Enables multi-threaded processing.\n" +
            "Enabling this option will maximize the use of parallelization in the entire simulation pipeline while disabling it will result in minimal thread usage.")]
        bool m_MultiThreaded = Default.MultiThreaded > 0 ? true : false;


        /// <summary>
        ///     Specifies whether to update the collision world an additional time after the step for more precise collider queries.
        /// </summary>
        public bool SynchronizeCollisionWorld
        {
            get => m_SynchronizeCollisionWorld;
            set => m_SynchronizeCollisionWorld = value;
        }
        [SerializeField]
        [Tooltip("Specifies whether to update the collision world an additional time after the step for more precise collider queries.")]
        bool m_SynchronizeCollisionWorld = Default.SynchronizeCollisionWorld > 0 ? true : false;

        internal PhysicsStep AsComponent => new PhysicsStep
        {
            SimulationType = SimulationType,
            Gravity = Gravity,
            SolverIterationCount = SolverIterationCount,
            SolverStabilizationHeuristicSettings = EnableSolverStabilizationHeuristic ?
                new Solver.StabilizationHeuristicSettings
            {
                EnableSolverStabilization = true,
                EnableFrictionVelocities = Default.SolverStabilizationHeuristicSettings.EnableFrictionVelocities,
                VelocityClippingFactor = Default.SolverStabilizationHeuristicSettings.VelocityClippingFactor,
                InertiaScalingFactor = Default.SolverStabilizationHeuristicSettings.InertiaScalingFactor
            } :
            Solver.StabilizationHeuristicSettings.Default,
            MultiThreaded = (byte)(MultiThreaded ? 1 : 0),
            SynchronizeCollisionWorld = (byte)(SynchronizeCollisionWorld ? 1 : 0)
        };

        void OnValidate()
        {
            SolverIterationCount = math.max(1, SolverIterationCount);
        }
    }

    internal class PhysicsStepBaker : Baker<PhysicsStepAuthoring>
    {
        public override void Bake(PhysicsStepAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, authoring.AsComponent);
        }
    }
}
