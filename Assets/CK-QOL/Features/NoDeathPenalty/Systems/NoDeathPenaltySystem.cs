using Inventory;
using Unity.Entities;
using Unity.NetCode;

namespace CK_QOL.Features.NoDeathPenalty.Systems
{
    /// <summary>
    ///     Represents the system responsible for preventing inventory loss due to player death within the game's server-side simulation.
    ///     This system operates by disabling components that trigger inventory movement on player death, ensuring that the player's inventory is not transferred or lost when they die.
    ///     
    ///     The system performs the following functions:
    ///     <list type="bullet">
    ///         <item>
    ///             <description>Disables the <see cref="InitialMoveInventoryFromCD"/> component on relevant entities to prevent inventory movement triggered by player death.</description>
    ///         </item>
    ///         <item>
    ///             <description>Clears references to the entity from which the inventory would normally be moved, ensuring that no unintended inventory transfer occurs.</description>
    ///         </item>
    ///     </list>
    ///     
    ///     This system is controlled by the <see cref="NoDeathPenalty"/> feature, which provides configuration settings and determines whether the system should be active based on the feature's enabled state.
    /// </summary>
    /// <remarks>
    ///     The <see cref="NoDeathPenaltySystem"/> class extends <see cref="PugSimulationSystemBase"/> to integrate with the game's server-side simulation framework,
    ///     running in the server simulation context to handle inventory management logic in real-time.
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(UpdateHealthSystemGroup))]
    [UpdateBefore(typeof(InitMoveInventorySystem))]
    public partial class NoDeathPenaltySystem : PugSimulationSystemBase
    {
        protected override void OnCreate()
        {
            if (!NoDeathPenalty.Instance.IsEnabled)
            {
                return;
            }
            
            base.OnCreate();

            RequireForUpdate<InventoryChangeBuffer>();
        }

        protected override void OnUpdate()
        {
            if (!NoDeathPenalty.Instance.IsEnabled)
            {
                return;
            }
            
            var initialMoveInventoryFromLookup = GetComponentLookup<InitialMoveInventoryFromCD>();

            Entities
                .WithAll<InitialMoveInventoryFromCD>()
                .ForEach((Entity entity, ref InitialMoveInventoryFromCD initialMoveInventoryFromCD) =>
                {
                    initialMoveInventoryFromLookup.SetComponentEnabled(entity, false);
                    initialMoveInventoryFromCD.entityFrom = Entity.Null;
                })
                .Schedule();

            base.OnUpdate();
        }
    }
}