using CK_QOL_Collection.Core.Feature;
using PlayerEquipment;
using Unity.Entities;

namespace CK_QOL_Collection.Features.NoEquipmentDurabilityLoss.Systems
{
    /// <summary>
    ///     Represents a system that disables durability loss for player equipment and items.
    ///     This system ensures that durability is not reduced when specific triggers are active.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(EndPredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(ChangeDurabilitySystem))]
    public partial class NoEquipmentDurabilityLossSystem : PugSimulationSystemBase
    {
        private bool _isEnabled;

        /// <summary>
        ///     Called when the system is created.
        ///     Ensures that the system requires the appropriate components for execution.
        /// </summary>
        protected override void OnCreate()
        {
            base.OnCreate();

            var noEquipmentDurabilityLossFeature = FeatureManager.Instance.GetFeature<NoEquipmentDurabilityLossFeature>();
            _isEnabled = noEquipmentDurabilityLossFeature?.IsEnabled ?? false;
        }

        /// <summary>
        ///     Updates the system, modifying durability loss triggers to prevent equipment damage.
        /// </summary>
        protected override void OnUpdate()
        {
            if (!_isEnabled)
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
                if (equippedObject.ValueRW.containedObject.objectID == ObjectID.None)
                {
                    continue;
                }
                
                if (!SystemAPI.HasComponent<DurabilityCD>(equippedObject.ValueRW.equipmentPrefab))
                {
                    continue;
                }
                    
                var durabilityComponent = SystemAPI.GetComponent<DurabilityCD>(equippedObject.ValueRW.equipmentPrefab);
                equippedObject.ValueRW.containedObject.objectData.amount = durabilityComponent.maxDurability;
            }

            base.OnUpdate();
        }
    }
}