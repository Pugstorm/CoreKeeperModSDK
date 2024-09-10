using CK_QOL.Core.Helpers;
using Inventory;
using Unity.Collections;
using Unity.Entities;

namespace CK_QOL.Features.ItemPickUpNotifier.Systems
{
	/// <summary>
	/// 	Represents the system responsible for managing and aggregating item pick-up notifications within the game client.
	///		This system runs as part of the client-side simulation and listens for inventory changes related to item pickups, collecting and displaying aggregated notifications to the player.
	/// 	
	/// 	The system performs the following functions:
	/// 	<list type="bullet">
	/// 	    <item>
	/// 	        <description>Monitors inventory changes to detect when items are picked up by the player, 
	/// 	        using the <see cref="InventoryChangeBuffer"/> component to track relevant events.</description>
	/// 	    </item>
	/// 	    <item>
	/// 	        <description>Caches the details of picked-up items (e.g., total amount, rarity, and display name) 
	/// 	        using a <see cref="NativeParallelHashMap{TKey,TValue}"/> for efficient aggregation.</description>
	/// 	    </item>
	/// 	    <item>
	/// 	        <description>Aggregates multiple pick-up events over a configurable delay period (<see cref="ItemPickUpNotifier.AggregateDelay"/>),
	///				reducing notification spam by combining multiple events into a single message.</description>
	/// 	    </item>
	/// 	    <item>
	/// 	        <description>Displays aggregated notifications to the player at regular intervals, 
	/// 	        utilizing the game's text display system to show the total items picked up within the specified delay period.</description>
	/// 	    </item>
	/// 	</list>
	/// 	
	/// 	This system is enabled and controlled by the <see cref="ItemPickUpNotifier"/> feature, 
	/// 	which provides the necessary configuration settings and determines whether the system should be active based on the feature's enabled state.
	/// </summary>
	/// <remarks>
	/// 	The <see cref="ItemPickUpNotificationSystem"/> class extends <see cref="PugSimulationSystemBase"/> to integrate with the game's simulation framework,
	///		running in the client-side simulation context to handle item pick-up notifications in real-time.
	/// </remarks>
	[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
	[UpdateInGroup(typeof(InventorySystemGroup))]
	public partial class ItemPickUpNotificationSystem : PugSimulationSystemBase
	{
		private NativeParallelHashMap<int, (int totalAmount, Rarity rarity, FixedString64Bytes displayName)> _cachedPickups;
		private float _timeSinceLastLog;
		private Entity _localPlayerEntity;

		protected override void OnCreate()
		{
			if (isServer || !ItemPickUpNotifier.Instance.IsEnabled)
			{
				return;
			}

			base.OnCreate();

			RequireForUpdate<InventoryChangeBuffer>();

			_cachedPickups = new NativeParallelHashMap<int, (int totalAmount, Rarity rarity, FixedString64Bytes displayName)>(16, Allocator.Persistent);
			_timeSinceLastLog = 0f;
		}

		protected override void OnDestroy()
		{
			if (_cachedPickups.IsCreated)
			{
				_cachedPickups.Dispose();
			}

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
			var cachedPickups = _cachedPickups;
			var localPlayerEntity = _localPlayerEntity;

			Entities
				.WithNone<EntityDestroyedCD>()
				.WithAll<InventoryChangeBuffer>()
				.ForEach((Entity _, in DynamicBuffer<InventoryChangeBuffer> inventoryChanges) =>
				{
					foreach (var change in inventoryChanges)
					{
						if (change.inventoryChangeData.inventoryAction != InventoryAction.MoveOrDropAllItems)
						{
							continue;
						}

						if (change.playerEntity != localPlayerEntity)
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
							if (item.objectData.objectID == ObjectID.None)
							{
								continue;
							}

							var objectIdHash = item.objectData.objectID.GetHashCode();
							if (cachedPickups.TryGetValue(objectIdHash, out var existing))
							{
								cachedPickups[objectIdHash] = (existing.totalAmount + item.amount, existing.rarity, existing.displayName);
							}
							else
							{
								var text = PlayerController.GetObjectName(item, true).text;
								var rarity = PugDatabase.GetObjectInfo(item.objectData.objectID).rarity;

								cachedPickups[objectIdHash] = (item.amount, rarity, text);
							}
						}
					}
				})
				.WithoutBurst()
				.ScheduleParallel();

			_timeSinceLastLog += SystemAPI.Time.DeltaTime;

			if (_timeSinceLastLog >= ItemPickUpNotifier.Instance.AggregateDelay)
			{
				CompleteDependency();

				foreach (var item in _cachedPickups)
				{
					var (amount, rarity, text) = item.Value;
					TextHelper.DisplayText($"{text} x{amount}", rarity);
				}

				_cachedPickups.Clear();
				_timeSinceLastLog = 0f;
			}

			base.OnUpdate();
		}
	}
}