using System.Collections.Generic;
using CK_QOL.Core.Helpers;
using Inventory;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace CK_QOL.Features.ItemPickUpNotifier.Systems
{
	/// <summary>
	///     Represents the system responsible for managing and aggregating item pick-up notifications within the game client.
	///     This system runs as part of the client-side simulation and listens for inventory changes related to item pickups,
	///     collecting and displaying aggregated notifications to the player.
	///     The system performs the following functions:
	///     <list type="bullet">
	///         <item>
	///             <description>
	///                 Monitors inventory changes to detect when items are picked up by the player,
	///                 using the <see cref="InventoryChangeBuffer" /> component to track relevant events.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 Caches the details of picked-up items (e.g., total amount, rarity, and display name)
	///                 using a <see cref="NativeParallelHashMap{TKey,TValue}" /> for efficient aggregation.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 Aggregates multiple pick-up events over a configurable delay period (
	///                 <see cref="ItemPickUpNotifier.AggregateDelay" />),
	///                 reducing notification spam by combining multiple events into a single message.
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 Displays aggregated notifications to the player at regular intervals,
	///                 utilizing the game's text display system to show the total items picked up within the specified delay
	///                 period.
	///             </description>
	///         </item>
	///     </list>
	///     This system is enabled and controlled by the <see cref="ItemPickUpNotifier" /> feature,
	///     which provides the necessary configuration settings and determines whether the system should be active based on the
	///     feature's enabled state.
	/// </summary>
	/// <remarks>
	///     The <see cref="ItemPickUpNotificationSystem" /> class extends <see cref="PugSimulationSystemBase" /> to integrate
	///     with the game's simulation framework,
	///     running in the client-side simulation context to handle item pick-up notifications in real-time.
	/// </remarks>
	[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
	[UpdateInGroup(typeof(InventorySystemGroup))]
	[BurstCompile(DisableDirectCall = true)]
	public partial class ItemPickUpNotificationSystem : PugSimulationSystemBase
	{
		private Dictionary<int, PickupEntry> _cachedPickups;
		private Entity _localPlayerEntity;

		protected override void OnCreate()
		{
			if (isServer || !ItemPickUpNotifier.Instance.IsEnabled)
			{
				return;
			}

			base.OnCreate();
			RequireForUpdate<InventoryChangeBuffer>();

			_cachedPickups = new Dictionary<int, PickupEntry>();
		}

		protected override void OnDestroy()
		{
			_cachedPickups.Clear();
			base.OnDestroy();
		}

		protected override void OnUpdate()
		{
			if (isServer || !ItemPickUpNotifier.Instance.IsEnabled)
			{
				return;
			}

			if (_localPlayerEntity == Entity.Null)
			{
				var playerController = Manager.main?.player;
				if (playerController?.isLocal ?? false)
				{
					_localPlayerEntity = playerController.entity;
				}
				else
				{
					return;
				}
			}

			var containedObjectsBufferLookup = GetBufferLookup<ContainedObjectsBuffer>(true);

			// ReSharper disable once Unity.Entities.MustBeSurroundedWithRefRwRo
			foreach (var inventoryChanges in SystemAPI.Query<DynamicBuffer<InventoryChangeBuffer>>())
			{
				foreach (var change in inventoryChanges)
				{
					if (change.inventoryChangeData.inventoryAction != InventoryAction.MoveOrDropAllItems)
					{
						continue;
					}

					if (change.playerEntity != _localPlayerEntity)
					{
						continue;
					}

					var sourceInventory = change.inventoryChangeData.inventory1;
					if (!containedObjectsBufferLookup.HasBuffer(sourceInventory))
					{
						continue;
					}

					var itemsBuffer = containedObjectsBufferLookup[sourceInventory];

					foreach (var item in itemsBuffer)
					{
						if (item.objectID == ObjectID.None)
						{
							continue;
						}

						var amount = item.amount;
						if (PugDatabase.GetObjectInfo(item.objectID, item.variation) is { isStackable: false })
						{
							amount = 1;
						}

						var objectIdHash = item.objectData.objectID.GetHashCode();

						if (_cachedPickups.TryGetValue(objectIdHash, out var existingEntry))
						{
							existingEntry.TotalAmount += amount;
							existingEntry.TimeSinceLastChange = 0f;
						}
						else
						{
							var text = PlayerController.GetObjectName(item, true).text;
							var rarity = PugDatabase.GetObjectInfo(item.objectData.objectID).rarity;

							_cachedPickups[objectIdHash] = new PickupEntry
							{
								TotalAmount = amount,
								Rarity = rarity,
								DisplayName = text,
								TimeSinceLastChange = 0f
							};
						}
					}
				}
			}

			UpdateTimersAndHandleNotifications();

			base.OnUpdate();
		}

		/// <summary>
		///     Updates the timers for all cached pickups and handles the display of notifications for items whose amounts
		///     haven't changed for the defined delay.
		/// </summary>
		private void UpdateTimersAndHandleNotifications()
		{
			var aggregateDelay = ItemPickUpNotifier.Instance.AggregateDelay;
			var notifiedPickups = new List<int>();

			foreach (var (objectIdHash, pickupEntry) in _cachedPickups)
			{
				// Increment the time since the last change for this item.
				pickupEntry.TimeSinceLastChange += SystemAPI.Time.DeltaTime;

				// If time since last change exceeds the aggregate delay, we notify.
				if (pickupEntry.TimeSinceLastChange >= aggregateDelay)
				{
					TextHelper.DisplayNotification($"{pickupEntry.DisplayName} x{pickupEntry.TotalAmount}", pickupEntry.Rarity);
					notifiedPickups.Add(objectIdHash);
				}
			}

			foreach (var objectIdHash in notifiedPickups)
			{
				_cachedPickups.Remove(objectIdHash);
			}
		}

		private class PickupEntry
		{
			public int TotalAmount { get; set; }
			public Rarity Rarity { get; set; }
			public FixedString64Bytes DisplayName { get; set; }
			public float TimeSinceLastChange { get; set; }
		}
	}
}