#if UNITY_EDITOR && !NETCODE_NDEBUG
#define NETCODE_DEBUG
#endif

using Unity.Entities;
using System;
using Unity.Core;
using Unity.Collections;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.GraphicsIntegration;
using Unity.Physics.Systems;
using Unity.Transforms;
using System.Collections.Generic;
using Unity.Burst;

namespace Unity.NetCode
{
    class NetcodePhysicsRateManager : IRateManager
    {
        private bool m_DidUpdate;
        private EntityQuery m_PredictedGhostPhysicsQuery;
        private EntityQuery m_LagCompensationQuery;
        private EntityQuery m_NetworkTimeQuery;
        public NetcodePhysicsRateManager(ComponentSystemGroup group)
        {
            m_PredictedGhostPhysicsQuery = group.World.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PredictedGhost>(), ComponentType.ReadOnly<PhysicsVelocity>());
            m_LagCompensationQuery = group.World.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<LagCompensationConfig>());
            m_NetworkTimeQuery = group.World.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkTime>());
        }
        public bool ShouldGroupUpdate(ComponentSystemGroup group)
        {
            if (m_DidUpdate)
            {
                m_DidUpdate = false;
                return false;
            }
            // Check if physics needs to update, this is only really needed on the client where predicted physics is expensive
            if (m_PredictedGhostPhysicsQuery.IsEmptyIgnoreFilter)
            {
                if (m_LagCompensationQuery.IsEmptyIgnoreFilter)
                    return false;
                var netTime = m_NetworkTimeQuery.GetSingleton<NetworkTime>();
                if (!netTime.IsFirstTimeFullyPredictingTick)
                    return false;
            }
            m_DidUpdate = true;
            return true;
        }
        public float Timestep
        {
            get
            {
                throw new System.NotImplementedException();
            }
            set
            {
                throw new System.NotImplementedException();
            }
        }
    }

    /// <summary>
    /// A system which setup physics for prediction. It will move the PhysicsSystemGroup
    /// to the PredictedFixedStepSimulationSystemGroup.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class PredictedPhysicsConfigSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            MovePhysicsSystems();
            var physGrp = World.GetExistingSystemManaged<PhysicsSystemGroup>();
            physGrp.RateManager = new NetcodePhysicsRateManager(physGrp);
            World.GetExistingSystemManaged<InitializationSystemGroup>().RemoveSystemFromUpdateList(this);
        }
        bool MovePhysicsSystem(Type systemType, Dictionary<Type, bool> physicsSystemTypes)
        {
            if (physicsSystemTypes.ContainsKey(systemType))
                return false;
            var attribs = TypeManager.GetSystemAttributes(systemType, typeof(UpdateBeforeAttribute));
            foreach (var attr in attribs)
            {
                var dep = attr as UpdateBeforeAttribute;
                if (physicsSystemTypes.ContainsKey(dep.SystemType))
                {
                    physicsSystemTypes[systemType] = true;
                    return true;
                }
            }
            attribs = TypeManager.GetSystemAttributes(systemType, typeof(UpdateAfterAttribute));
            foreach (var attr in attribs)
            {
                var dep = attr as UpdateAfterAttribute;
                if (physicsSystemTypes.ContainsKey(dep.SystemType))
                {
                    physicsSystemTypes[systemType] = true;
                    return true;
                }
            }
            return false;
        }
        void MovePhysicsSystems()
        {
            var srcGrp = World.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            var dstGrp = World.GetExistingSystemManaged<PredictedFixedStepSimulationSystemGroup>();

            var physicsSystemTypes = new Dictionary<Type, bool>();
            physicsSystemTypes.Add(typeof(PhysicsSystemGroup), true);

            bool didMove = true;
            var fixedUpdateSystems = srcGrp.ManagedSystems;
            while (didMove)
            {
                didMove = false;
                foreach (var system in fixedUpdateSystems)
                {
                    var systemType = system.GetType();
                    didMove |= MovePhysicsSystem(systemType, physicsSystemTypes);
                }
            }
            foreach (var system in fixedUpdateSystems)
            {
                if (physicsSystemTypes.ContainsKey(system.GetType()))
                {
                    srcGrp.RemoveSystemFromUpdateList(system);
                    dstGrp.AddSystemToUpdateList(system);
                }
            }
        }
    }

    /// <summary>
    /// If a singleton of this type exists in the world any non-ghost with dynamic physics
    /// in the default physics world on the client will be moved to the indicated physics
    /// world index.
    /// This is required because the predicted physics loop cannot process objects which
    /// are not rolled back.
    /// </summary>
    public struct PredictedPhysicsNonGhostWorld : IComponentData
    {
        /// <summary>
        /// The physics world index to move entities to.
        /// </summary>
        public uint Value;
    }

    /// <summary>
    /// A system used to detect invalid dynamic physics objects in the predicted
    /// physics world on clients. This system also moves entities to the correct
    /// world if PredictedPhysicsNonGhostWorld exists and is not 0.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [BurstCompile]
    public partial struct PredictedPhysicsValidationSystem : ISystem
    {
        #if NETCODE_DEBUG
        private bool m_DidPrintError;
        #endif
        private EntityQuery m_Query;
        public void OnCreate(ref SystemState state)
        {
            // If not debug, require the singleton for update
            #if !NETCODE_DEBUG
            state.RequireForUpdate<PredictedPhysicsNonGhostWorld>();
            #endif
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PhysicsVelocity, PhysicsWorldIndex>()
                .WithNone<GhostInstance>();
            m_Query = state.GetEntityQuery(builder);
            m_Query.SetSharedComponentFilter(new PhysicsWorldIndex(0));
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!m_Query.IsEmpty)
            {
                if (SystemAPI.TryGetSingleton<PredictedPhysicsNonGhostWorld>(out var targetWorld))
                {
                    // Go through all things and set the new target world. This is a structural change so need to be careful
                    state.EntityManager.SetSharedComponent(m_Query, new PhysicsWorldIndex(targetWorld.Value));
                }
                #if NETCODE_DEBUG
                else if (!m_DidPrintError)
                {
                    // If debug, print a warning once telling users what to do,
                    // and show them the first problem entity (for easy debugging).
                    var erredEntities = m_Query.ToEntityArray(Allocator.Temp);
                    FixedString512Bytes error = $"[{state.WorldUnmanaged.Name}] The default physics world contains {erredEntities.Length} dynamic physics objects which are not ghosts. This is not supported! In order to have client-only physics, you must setup a custom physics world:";
                    foreach (var erredEntity in erredEntities)
                    {
                        FixedString512Bytes tempFs = "\n- ";
                        tempFs.Append(erredEntity.ToFixedString());
                        tempFs.Append(' ');
                        state.EntityManager.GetName(erredEntity, out var entityName);
                        tempFs.Append(entityName);

                        var formatError = error.Append(tempFs);
                        if (formatError == FormatError.Overflow)
                            break;
                    }
                    SystemAPI.GetSingleton<NetDebug>().LogError(error);
                    m_DidPrintError = true;
                    state.RequireForUpdate<PredictedPhysicsNonGhostWorld>();
                }
                #endif
            }
        }
    }

    /// <summary>
    /// System to make sure prediction switching smoothing happens after physics motion smoothing and overwrites the results
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(SwitchPredictionSmoothingSystem))]
    [UpdateAfter(typeof(SmoothRigidBodiesGraphicalMotion))]
    public partial class SwitchPredictionSmoothingPhysicsOrderingSystem : SystemBase
    {
        internal struct Disabled : IComponentData
        {}
        protected override void OnCreate()
        {
            RequireForUpdate<Disabled>();
        }

        protected override void OnUpdate()
        {
        }
    }
}
