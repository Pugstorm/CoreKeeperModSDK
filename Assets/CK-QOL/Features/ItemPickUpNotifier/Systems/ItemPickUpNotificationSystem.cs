using System.Collections.Generic;
using CK_QOL.Core.Helpers;
using Inventory;
using Unity.Collections;
using Unity.Entities;

namespace CK_QOL.Features.ItemPickUpNotifier.Systems
{
	/// <summary>
	///     Represents the system responsible for managing and aggregating item pick-up notifications within the game client.
	///     This system monitors inventory changes and aggregates multiple pick-up events over a configurable delay period
	///     to reduce notification spam.
	/// </summary>
	/// <remarks>
	///     The system listens for changes in the player's inventory and caches details of picked-up items. It uses an
	///     aggregation delay to display notifications, combining multiple pick-up events into a single message.
	/// </remarks>
	[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
	[UpdateInGroup(typeof(InventorySystemGroup))]
	public partial class ItemPickUpNotificationSystem : PugSimulationSystemBase
	{
		private Dictionary<int, PickupEntry> _cachedPickups;
		private Entity _localPlayerEntity;

		/// <summary>
		///     Called when the system is created. Initializes the cached pickups and sets up requirements for update.
		/// </summary>
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

		/// <summary>
		///     Called when the system is destroyed. Clears the cached pickups.
		/// </summary>
		protected override void OnDestroy()
		{
			_cachedPickups.Clear();
			base.OnDestroy();
		}

		/// <summary>
		///     Called every update tick to monitor inventory changes and handle item pick-up notifications.
		/// </summary>
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

			// Loop through inventory changes and track picked-up items.
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

				// If time since last change exceeds the aggregate delay, notify the player.
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

		/// <summary>
		///     Represents a cached pick-up entry, used to aggregate item amounts and manage notifications.
		/// </summary>
		private class PickupEntry
		{
			public int TotalAmount { get; set; }
			public Rarity Rarity { get; set; }
			public FixedString64Bytes DisplayName { get; set; }
			public float TimeSinceLastChange { get; set; }
		}
	}
}