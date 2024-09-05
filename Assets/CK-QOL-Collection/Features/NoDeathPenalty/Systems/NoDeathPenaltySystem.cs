using CK_QOL_Collection.Core;
using Inventory;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace CK_QOL_Collection.Features.NoDeathPenalty.Systems
{
    /// <summary>
    ///     Represents a system that disables the death penalty in the game.
    ///     This system ensures that the player's inventory is not lost upon death.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(InitMoveInventorySystem))]
    [UpdateAfter(typeof(UpdateHealthSystemGroup))]
    public partial class NoDeathPenaltySystem : PugSimulationSystemBase
    {
        /// <summary>
        ///     Called when the system is created.
        ///     Ensures that the system requires the <see cref="InventoryChangeBuffer"/> component for execution.
        /// </summary>
        protected override void OnCreate()
        {
            RequireForUpdate<InventoryChangeBuffer>();
            base.OnCreate();
        }

        /// <summary>
        ///     Updates the system, disabling inventory changes when a player dies to prevent inventory loss.
        /// </summary>
        [BurstCompile]
        protected override void OnUpdate()
        {
            // TODO: DOTS can't access the configuration. Find a workaround.
            // if (!Configuration.Sections.NoDeathPenalty.IsEnabled)
            // {
            //     return;
            // }

            var initialMoveInventoryFromLookup = GetComponentLookup<InitialMoveInventoryFromCD>();

            Entities.WithAll<InitialMoveInventoryFromCD>().ForEach((Entity entity) =>
            {
                // Disable the component to prevent further inventory movement on death.
                initialMoveInventoryFromLookup.SetComponentEnabled(entity, false);

                var initialMoveInventoryFromCD = initialMoveInventoryFromLookup.GetRefRW(entity).ValueRW;
                if (initialMoveInventoryFromCD.entityFrom == Entity.Null)
                {
                    return;
                }

                // Set the entityFrom field to null to ensure no inventory movement occurs.
                initialMoveInventoryFromCD.entityFrom = Entity.Null;

            }).Schedule();

            base.OnUpdate();
        }
    }
}