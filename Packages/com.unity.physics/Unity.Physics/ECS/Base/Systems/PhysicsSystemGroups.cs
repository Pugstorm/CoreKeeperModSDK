using Unity.Entities;
using System;
using System.Collections.Generic;
using Unity.Transforms;

namespace Unity.Physics.Systems
{
    /// <summary>
    /// The physics system group. Covers all physics systems in the engine. Consists of <see cref="PhysicsInitializeGroup"/>
    /// , <see cref="PhysicsSimulationGroup"/>, and <see cref="ExportPhysicsWorld"/> which run in
    /// that order.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class PhysicsSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// This abstract class can be used to create a system group for a custom physics world.
    /// You most likely want to use <see cref="CustomPhysicsSystemGroup"/>, as you don't need to implement callback methods there.
    /// </summary>
    public abstract partial class CustomPhysicsSystemGroupBase : ComponentSystemGroup
    {
        /// <summary>   PhysicsWorldData. </summary>
        protected PhysicsWorldData m_WorldData;
        /// <summary>   PhysicsWorldIndex. </summary>
        protected PhysicsWorldIndex m_WorldFilter;

        private uint m_WorldIndex;
        private bool m_DidCloneSystems;
        private bool m_ShareStaticColliders;

        /// <summary>
        /// Constructor. Your subclass needs to implement an empty constructor which is calling this one to properly set up the world index.
        /// </summary>
        /// <param name="worldIndex"> A world index for a physics world. </param>
        /// <param name="shareStaticColliders"> Should static colliders be shared between main world and this one. </param>
        public CustomPhysicsSystemGroupBase(uint worldIndex, bool shareStaticColliders)
        {
            m_WorldIndex = worldIndex;
            m_ShareStaticColliders = shareStaticColliders;
            EnableSystemSorting = false;
        }

        /// <summary>
        /// An interface method to specify an additional set of managed systems which are copied to the
        /// custom physics world. This will be called the first time OnUpdate runs.
        /// </summary>
        ///
        /// <param name="systems">  The systems. </param>
        protected virtual void AddExistingSystemsToUpdate(List<Type> systems)
        {}
        /// <summary>
        /// An interface method to specify an additional set of unmanaged systems which are copied to the
        /// custom physics world. This will be called the first time OnUpdate runs.
        /// </summary>
        ///
        /// <param name="systems">  The systems. </param>
        protected virtual void AddExistingUnmanagedSystemsToUpdate(List<Type> systems)
        {}

        /// <summary>
        /// Called before the systems in this group are updated. It is useful in cases of needing to store system state (such as NativeArrays, NativeLists etc), before it is ran in a custom group.
        /// </summary>
        protected abstract void PreGroupUpdateCallback();

        /// <summary>
        /// Called after the systems in this group are updated. It is useful in cases of needing to restore system state (such as NativeArrays, NativeLists etc), after it is ran in a custom group.
        /// </summary>
        protected abstract void PostGroupUpdateCallback();

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            m_WorldFilter = new PhysicsWorldIndex(m_WorldIndex);
            m_WorldData = new PhysicsWorldData(ref CheckedStateRef, m_WorldFilter);
            if (m_ShareStaticColliders)
                m_WorldData.StaticEntityGroup.ResetFilter();
        }

        protected override void OnStopRunning()
        {
            m_WorldData.Dispose();

            base.OnStopRunning();
        }

        protected override void OnUpdate()
        {
            // Can't switch if the sim singleton is null
            if (SystemAPI.GetSingleton<SimulationSingleton>().Type == SimulationType.NoPhysics)
                return;

            // copy systems from PhysicsSystemGroup if first time
            if (!m_DidCloneSystems)
            {
                AddSystemToUpdateList(World.GetExistingSystemManaged<PhysicsInitializeGroup>());
                AddSystemToUpdateList(World.GetExistingSystemManaged<PhysicsSimulationGroup>());
                AddSystemToUpdateList(World.Unmanaged.GetExistingUnmanagedSystem<ExportPhysicsWorld>());
                AddSystemToUpdateList(World.GetExistingSystemManaged<BeforePhysicsSystemGroup>());
                AddSystemToUpdateList(World.GetExistingSystemManaged<AfterPhysicsSystemGroup>());
                AddSystemToUpdateList(World.GetExistingSystem<Unity.Physics.GraphicsIntegration.BufferInterpolatedRigidBodiesMotion>());
                AddSystemToUpdateList(World.GetExistingSystem<Unity.Physics.GraphicsIntegration.CopyPhysicsVelocityToSmoothing>());
                AddSystemToUpdateList(World.GetExistingSystem<Unity.Physics.GraphicsIntegration.RecordMostRecentFixedTime>());
                AddSystemToUpdateList(World.Unmanaged.GetExistingUnmanagedSystem<SyncCustomPhysicsProxySystem>());

                var userSystems = new List<Type>();
                AddExistingSystemsToUpdate(userSystems);
                foreach (var sys in userSystems)
                {
                    AddSystemToUpdateList(World.GetExistingSystemManaged(sys));
                }
                userSystems.Clear();
                AddExistingUnmanagedSystemsToUpdate(userSystems);
                foreach (var sys in userSystems)
                {
                    AddSystemToUpdateList(World.Unmanaged.GetExistingUnmanagedSystem(sys));
                }

                m_DidCloneSystems = true;
                EnableSystemSorting = true;
            }

            ref var bpwData = ref SystemAPI.GetSingletonRW<BuildPhysicsWorldData>().ValueRW;

            // change active physics world
            var prevWorld = bpwData.PhysicsData;
            var prevFilter = bpwData.WorldFilter;
            var prevSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            bpwData.PhysicsData = m_WorldData;
            bpwData.WorldFilter = m_WorldFilter;

            SystemAPI.SetSingleton(new PhysicsWorldSingleton { PhysicsWorld = m_WorldData.PhysicsWorld, PhysicsWorldIndex = m_WorldFilter });

            PreGroupUpdateCallback();

            base.OnUpdate();

            PostGroupUpdateCallback();

            // restore active physics world
            m_WorldData = bpwData.PhysicsData;
            m_WorldFilter = bpwData.WorldFilter;
            bpwData.PhysicsData = prevWorld;
            bpwData.WorldFilter = prevFilter;

            SystemAPI.SetSingleton(prevSingleton);
        }
    }

    /// <summary>
    /// The first group to run in physics pipeline. It creates the <see cref="PhysicsWorld"/> from
    /// ECS physics components. The most important system in this world is <see cref="BuildPhysicsWorld"/>
    /// .
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(PhysicsSimulationGroup))]
    public partial class PhysicsInitializeGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// The second group to run in physics pipeline. It simulates the world. If you want to modify
    /// data mid-simulation, your system should run in this group. It consists of <see cref="PhysicsCreateBodyPairsGroup"/>
    /// , <see cref="PhysicsCreateContactsGroup"/>, <see cref="PhysicsCreateJacobiansGroup"/> and <see cref="PhysicsSolveAndIntegrateGroup"/>
    /// groups which run in that order.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsInitializeGroup))]
    [UpdateBefore(typeof(ExportPhysicsWorld))]
    public partial class PhysicsSimulationGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// The first system group to run in <see cref="PhysicsSimulationGroup"/>. It finds all
    /// potentitaly overlapping body-pairs in the simulation. After it has finished, and before <see cref="PhysicsCreateContactsGroup"/>
    /// starts, you can modify those pairs by implementing IBodyPairsJob.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSimulationGroup))]
    [UpdateBefore(typeof(PhysicsCreateContactsGroup))]
    public partial class PhysicsCreateBodyPairsGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// The second system group to run in <see cref="PhysicsSimulationGroup"/>. It is doing collision
    /// detection on body pairs generated by <see cref="PhysicsCreateBodyPairsGroup"/>, and generates
    /// contacts. After it has finished, and before <see cref="PhysicsCreateJacobiansGroup"/> starts,
    /// you can modify those contacts by implementing IContactsJob.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSimulationGroup))]
    [UpdateAfter(typeof(PhysicsCreateBodyPairsGroup))]
    [UpdateBefore(typeof(PhysicsCreateJacobiansGroup))]
    public partial class PhysicsCreateContactsGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// The third system group to run in <see cref="PhysicsSimulationGroup"/>. It is converting
    /// contacts generated by <see cref="PhysicsCreateContactsGroup"/> to jacobians. After it has
    /// finished, and before <see cref="PhysicsSolveAndIntegrateGroup"/> starts, you can modify
    /// jacobians by implementing IJacobiansJob.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSimulationGroup))]
    [UpdateAfter(typeof(PhysicsCreateContactsGroup))]
    [UpdateBefore(typeof(PhysicsSolveAndIntegrateGroup))]
    public partial class PhysicsCreateJacobiansGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// The final system group to run in <see cref="PhysicsSimulationGroup"/>. It is solving the
    /// jacobians generated by <see cref="PhysicsCreateJacobiansGroup"/>, and writing the results of
    /// the simulation to the <see cref="DynamicsWorld"/>.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSimulationGroup))]
    [UpdateAfter(typeof(PhysicsCreateJacobiansGroup))]
    public partial class PhysicsSolveAndIntegrateGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// A system group that runs before physics.
    /// In almost all cases, this provides no behaviour difference over manually typing [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))][UpdateBefore(typeof(PhysicsSystemGroup))].
    /// The only benefit of using [UpdateInGroup(BeforePhysicsSystemGroup)] is the fact that the systems which update in this group will be correctly copied by <see cref="CustomPhysicsSystemGroup"/> in cases of using multiple worlds.
    /// If using [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))][UpdateBefore(typeof(PhysicsSystemGroup))] the [UpdateBefore] attribute will be invalid in <see cref="CustomPhysicsSystemGroup"/>.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(PhysicsInitializeGroup))]
    public partial class BeforePhysicsSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// A system group that runs after physics.
    /// In almost all cases, this provides no behaviour difference over manually typing [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))][UpdateAfter(typeof(PhysicsSystemGroup))].
    /// The only benefit of using [UpdateInGroup(AfterPhysicsSystemGroup)] is the fact that the systems which update in this group will be correctly copied by <see cref="CustomPhysicsSystemGroup"/> in cases of using multiple worlds.
    /// If using [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))][UpdateAfter(typeof(PhysicsSystemGroup))] the [UpdateAfter] attribute will be invalid in <see cref="CustomPhysicsSystemGroup"/>.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(ExportPhysicsWorld))]
    public partial class AfterPhysicsSystemGroup : ComponentSystemGroup
    {
    }
}
