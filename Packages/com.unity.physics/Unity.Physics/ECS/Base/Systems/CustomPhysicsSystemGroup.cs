using Unity.Entities;

namespace Unity.Physics.Systems
{
#if !HAVOK_PHYSICS_EXISTS

    /// <summary>
    /// This abstract class can be used to create a system group for a custom physics world.
    /// To create a custom physics group, derive from this class and implement empty constructor
    /// which calls one of two constructors of this class, and potentially implement some of the
    /// other virtual functions.
    /// </summary>
    /// <remarks>
    /// If class that derives CustomPhysicsSystemGroup doesn't need the Simulation to be recreated on each OnStartRunning then it should override
    /// OnStartRunning and guard instantiation of new Simulation so it happens only once. It also has to override OnStopRunning and move disposing
    /// of simulation to it's own Dispose method.
    /// </remarks>
    public abstract partial class CustomPhysicsSystemGroup : CustomPhysicsSystemGroupBase
    {
        private Simulation m_StoredSimulation;

        /// <summary>
        /// Constructor. Your subclass needs to implement an empty constructor which is calling this one to properly set up the world index.
        /// </summary>
        /// <param name="worldIndex"> A world index for a physics world. </param>
        /// <param name="shareStaticColliders"> Should static colliders be shared between main world and this one. </param>
        protected CustomPhysicsSystemGroup(uint worldIndex, bool shareStaticColliders) : base(worldIndex, shareStaticColliders)
        {
            m_StoredSimulation = default;
        }

        /// <summary>
        /// Creates new simulations for the SystemGroup.
        /// </summary>
        /// <remarks>
        /// It instantiates and initializes a new physics Simulation only if RequireForUpdate condition defined in OnCreated method is met.
        /// </remarks>
        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            m_StoredSimulation = Simulation.Create();
        }

        /// <summary>
        /// Disposes custom physics simulation when RequireForUpdate condition stops being met.
        /// </summary>
        /// <remarks>
        /// It disposes Simulation if RequireForUpdate condition defined in OnCreated method are not met any more.
        /// </remarks>
        protected override void OnStopRunning()
        {
            base.OnStopRunning();

            m_StoredSimulation.Dispose();
        }

        /// <summary>
        /// Called before the systems in this group are updated. It is useful in cases of needing to store system state (such as NativeArrays, NativeLists etc), before it is ran in a custom group.
        /// If overriding this method, make sure to call base.PreGroupUpdateCallback().
        /// </summary>
        protected override void PreGroupUpdateCallback()
        {
            var broadphaseSystem = World.Unmanaged.GetExistingUnmanagedSystem<BroadphaseSystem>();
            ref var boradphaseData = ref EntityManager.GetComponentDataRW<BroadphaseData>(broadphaseSystem).ValueRW;
            var currentSimulation = boradphaseData.m_UnityPhysicsSimulation;
            boradphaseData.m_UnityPhysicsSimulation = m_StoredSimulation;
            m_StoredSimulation = currentSimulation;

            ref SystemState pickerSystemState = ref World.Unmanaged.GetExistingSystemState<PhysicsSimulationPickerSystem>();
            pickerSystemState.Enabled = false; // disable simulation switching in custom worlds
        }

        /// <summary>
        /// Called after the systems in this group are updated. It is useful in cases of needing to restore system state (such as NativeArrays, NativeLists etc), after it is ran in a custom group.
        /// If overriding this method, make sure to call base.PostGroupUpdateCallback().
        /// </summary>
        protected override void PostGroupUpdateCallback()
        {
            var broadphaseSystem = World.Unmanaged.GetExistingUnmanagedSystem<BroadphaseSystem>();
            ref var boradphaseData = ref EntityManager.GetComponentDataRW<BroadphaseData>(broadphaseSystem).ValueRW;
            var currentSimulation = boradphaseData.m_UnityPhysicsSimulation;
            boradphaseData.m_UnityPhysicsSimulation = m_StoredSimulation;
            m_StoredSimulation = currentSimulation;

            ref SystemState pickerSystemState = ref World.Unmanaged.GetExistingSystemState<PhysicsSimulationPickerSystem>();
            pickerSystemState.Enabled = true; // enable switching back in main world
        }
    }

#endif
}
