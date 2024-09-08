using CK_QOL_Collection.Core.Feature;
using Inventory;
using Unity.Entities;
using Unity.NetCode;

namespace CK_QOL_Collection.Features.NoDeathPenalty.Systems
{
    /// <summary>
    ///     Represents a system that disables the death penalty in the game.
    ///     This system ensures that the player's inventory is not lost upon death.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(UpdateHealthSystemGroup))]
    [UpdateBefore(typeof(InitMoveInventorySystem))]
    public partial class NoDeathPenaltySystem : PugSimulationSystemBase
    {
        /// <summary>
        ///     Called when the system is created.
        ///     Ensures that the system requires the <see cref="InventoryChangeBuffer" /> component for execution.
        /// </summary>
        protected override void OnCreate()
        {
            base.OnCreate();

            RequireForUpdate<InventoryChangeBuffer>();
        }

        /// <summary>
        ///     Updates the system, disabling inventory changes when a player dies to prevent inventory loss.
        /// </summary>
        protected override void OnUpdate()
        {
            var noDeathPenaltyFeature = FeatureManager.Instance.GetFeature<NoDeathPenaltyFeature>();
            if (noDeathPenaltyFeature is not { IsEnabled: true })
            {
                return;
            }
            
            var initialMoveInventoryFromLookup = GetComponentLookup<InitialMoveInventoryFromCD>();

            // Move GetComponentLookup inside the job to ensure the lookup is up-to-date.
            Entities
                .WithAll<InitialMoveInventoryFromCD>()
                .ForEach((Entity entity, ref InitialMoveInventoryFromCD initialMoveInventoryFromCD) =>
                {
                    // Disable the component to prevent further inventory movement on death.
                    initialMoveInventoryFromLookup.SetComponentEnabled(entity, false);
                    // Set the entityFrom field to null to ensure no inventory movement occurs.
                    initialMoveInventoryFromCD.entityFrom = Entity.Null;
                }).Schedule();

            base.OnUpdate();
        }
    }
}