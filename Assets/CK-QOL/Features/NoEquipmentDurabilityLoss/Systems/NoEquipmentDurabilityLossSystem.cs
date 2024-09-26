using PlayerEquipment;
using Unity.Entities;

namespace CK_QOL.Features.NoEquipmentDurabilityLoss.Systems
{
	/// <summary>
	///     Represents the system responsible for preventing equipment durability loss within the game.
	///     This system ensures that all equipment remains at maximum durability and does not degrade over time or through use.
	/// </summary>
	/// <remarks>
	///     The <see cref="NoEquipmentDurabilityLossSystem" /> class integrates with the server-side simulation, handling
	///     durability management logic to prevent equipment degradation in real-time.
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

			// Disable all durability reduction triggers
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

			// Reset the durability of all equipped items to their maximum values
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
				equippedObject.ValueRW.containedObject.objectData.amount = durabilityComponent.IsReinforced(equippedObject.ValueRW.containedObject.objectData.amount)
					? durabilityComponent.maxDurability * 2
					: durabilityComponent.maxDurability;
			}

			base.OnUpdate();
		}
	}
}