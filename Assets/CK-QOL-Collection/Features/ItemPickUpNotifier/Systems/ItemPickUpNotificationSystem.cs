using CK_QOL_Collection.Core.Feature;
using CK_QOL_Collection.Core.Helpers;
using Inventory;
using Unity.Collections;
using Unity.Entities;

namespace CK_QOL_Collection.Features.ItemPickUpNotifier.Systems
{
    /// <summary>
    ///     System that detects and notifies when a player picks up items into their inventory from the ground or containers.
    ///     This system gathers inventory changes every frame, aggregates them, and logs the results after a configurable delay to reduce notification spam.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(InventorySystemGroup))]
    public partial class ItemPickUpNotificationSystem : PugSimulationSystemBase
    {
        private NativeParallelHashMap<int, (int totalAmount, Rarity rarity, FixedString64Bytes displayName)> _cachedPickups;
        private float _timeSinceLastLog;
        private Entity _localPlayerEntity;

        private bool _isEnabled;
        private float _logDelay;

        /// <summary>
        ///     Called when the system is created. Ensures the system requires updates when an InventoryChangeBuffer is present.
        ///     Initializes the cache for inventory changes and retrieves configuration settings.
        /// </summary>
        protected override void OnCreate()
        {
            var itemPickUpNotifierFeature = FeatureManager.Instance.GetFeature<ItemPickUpNotifierFeature>();
            _isEnabled = itemPickUpNotifierFeature.IsEnabled;
            _logDelay = itemPickUpNotifierFeature.Config.LogDelay;
            
            if (isServer || !_isEnabled)
            {
                return;
            }

            base.OnCreate();

            RequireForUpdate<InventoryChangeBuffer>();

            _cachedPickups = new NativeParallelHashMap<int, (int totalAmount, Rarity rarity, FixedString64Bytes displayName)>(16, Allocator.Persistent);
            _timeSinceLastLog = 0f;
        }
        
        /// <summary>
        ///     Called when the system is destroyed. Disposes of the cached changes list.
        /// </summary>
        protected override void OnDestroy()
        {
            if (_cachedPickups.IsCreated)
            {
                _cachedPickups.Dispose();
            }

            base.OnDestroy();
        }

        /// <summary>
        ///     Called on each frame update to check for item pickups and log them.
        /// </summary>
        protected override void OnUpdate()
        {
            if (isServer || !_isEnabled)
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

            if (_timeSinceLastLog >= _logDelay)
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