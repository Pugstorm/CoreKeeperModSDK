using PlayerEquipment;
using Unity.Entities;

namespace CK_QOL.Features.NoEquipmentDurabilityLoss.Systems
{
    /// <summary>
    ///     Represents the system responsible for preventing equipment durability loss within the game. 
    ///     This system operates on the server-side simulation to ensure that all equipment remains at maximum durability and does not degrade over time or through use.
    ///     
    ///     The system performs the following functions:
    ///     <list type="bullet">
    ///         <item>
    ///             <description>Disables durability reduction triggers for all equipment,
    ///             ensuring that no durability loss occurs by setting damage values and percentages to zero and disabling associated components.</description>
    ///         </item>
    ///         <item>
    ///             <description>Resets the durability of all equipped items to their maximum values,
///                 maintaining full durability for all equipment regardless of in-game actions.</description>
    ///         </item>
    ///     </list>
    ///     
    ///     This system is controlled by the <see cref="NoEquipmentDurabilityLoss"/> feature,
    ///     which provides configuration settings and determines whether the system should be active based on the feature's enabled state.
    /// </summary>
    /// <remarks>
    ///     The <see cref="NoEquipmentDurabilityLossSystem"/> class extends <see cref="PugSimulationSystemBase"/> to integrate with the game's server-side simulation framework,
    ///     running in the server simulation context to ensure that equipment durability loss is effectively prevented in real-time.
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(EndPredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(ChangeDurabilitySystem))]
    public partial class NoEquipmentDurabilityLossSystem : PugSimulationSystemBase
    {
        protected override void OnCreate()
        {
            if (!NoEquipmentDurabilityLoss.Instance.IsEnabled)
            {
                return;
            }
            
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            if (!NoEquipmentDurabilityLoss.Instance.IsEnabled)
            {
                return;
            }

            foreach (var (allTrigger, entity) in SystemAPI.Query<RefRW<ReduceDurabilityOfAllEquipmentTriggerCD>>().WithEntityAccess())
            {
                allTrigger.ValueRW.damage = 0;
                allTrigger.ValueRW.percentage = 0f;
                SystemAPI.SetComponentEnabled<ReduceDurabilityOfAllEquipmentTriggerCD>(entity, false);
            }

            foreach (var (equippedTrigger, entity) in SystemAPI.Query<RefRW<ReduceDurabilityOfEquippedTriggerCD>>().WithEntityAccess())
            {
                equippedTrigger.ValueRW.triggerCounter = 0;
                SystemAPI.SetComponentEnabled<ReduceDurabilityOfEquippedTriggerCD>(entity, false);
            }

            foreach (var equippedObject in SystemAPI.Query<RefRW<EquippedObjectCD>>())
            {
                if (equippedObject.ValueRO.containedObject.objectID is ObjectID.None or ObjectID.CattleCage)
                {
                    continue;
                }

                if (!SystemAPI.HasComponent<DurabilityCD>(equippedObject.ValueRO.equipmentPrefab))
                {
                    continue;
                }
                    
                var durabilityComponent = SystemAPI.GetComponent<DurabilityCD>(equippedObject.ValueRO.equipmentPrefab);
                equippedObject.ValueRW.containedObject.objectData.amount = durabilityComponent.maxDurability;
            }

            base.OnUpdate();
        }
    }
}