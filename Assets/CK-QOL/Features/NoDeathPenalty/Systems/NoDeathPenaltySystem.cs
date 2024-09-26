using Inventory;
using Unity.Entities;
using Unity.NetCode;

namespace CK_QOL.Features.NoDeathPenalty.Systems
{
	/// <summary>
	///     Represents the system responsible for preventing inventory loss due to player death within the game's server-side
	///     simulation. This system operates by disabling components that trigger inventory movement on player death, ensuring
	///     that the player's inventory is not transferred or lost when they die.
	/// </summary>
	/// <remarks>
	///     The <see cref="NoDeathPenaltySystem" /> class integrates with the server-side simulation, handling inventory
	///     management logic in real-time to prevent inventory loss after death.
	/// </remarks>
	[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
	[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
	[UpdateAfter(typeof(UpdateHealthSystemGroup))]
	[UpdateBefore(typeof(InitMoveInventorySystem))]
	public partial struct NoDeathPenaltySystem : ISystem
	{
		/// <summary>
		///     Called when the system is created, sets up required components and conditions.
		/// </summary>
		public void OnCreate(ref SystemState state)
		{
			if (!NoDeathPenalty.Instance.IsEnabled)
			{
				return;
			}

			// Require the InventoryChangeBuffer for the system to update
			state.RequireForUpdate<InventoryChangeBuffer>();
		}

		/// <summary>
		///     Called when the system is updated, prevents inventory loss on player death.
		/// </summary>
		public void OnUpdate(ref SystemState state)
		{
			if (!NoDeathPenalty.Instance.IsEnabled)
			{
				return;
			}

			// Query for entities with InitialMoveInventoryFromCD component
			var initialMoveInventoryFromLookup = SystemAPI.GetComponentLookup<InitialMoveInventoryFromCD>();

			// Use SystemAPI Query to modify the inventory movement logic
			foreach (var (initialMoveInventoryFromCD, entity) in SystemAPI.Query<RefRW<InitialMoveInventoryFromCD>>().WithEntityAccess())
			{
				initialMoveInventoryFromLookup.SetComponentEnabled(entity, false);
				initialMoveInventoryFromCD.ValueRW.entityFrom = Entity.Null;
			}
		}
	}
}