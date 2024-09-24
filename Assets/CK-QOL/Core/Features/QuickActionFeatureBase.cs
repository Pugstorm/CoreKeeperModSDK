using System;
using System.Collections.Generic;
using System.Linq;
using CK_QOL.Core.Helpers;

namespace CK_QOL.Core.Features
{
    internal abstract class QuickActionFeatureBase<TFeature> : FeatureBase<TFeature>
        where TFeature : QuickActionFeatureBase<TFeature>, new()
    {
        protected const float PressThreshold = 0.7f;
        protected int FromSlotIndex = InventoryHandlerHelper.InvalidIndex;
        protected int PreviousSlotIndex = InventoryHandlerHelper.InvalidIndex;

        protected virtual Func<KeyValuePair<int, ObjectDataCD>, object> SortingFunction => _ => 0;

        protected abstract bool IsTargetItem(ObjectDataCD objectData);

        public override bool CanExecute()
        {
            return base.CanExecute()
                && Entry.RewiredPlayer != null
                && Manager.main.player != null
                && !(Manager.input?.textInputIsActive ?? false);
        }

        public override void Update()
        {
            if (!CanExecute())
            {
                return;
            }

            if (Entry.RewiredPlayer.GetButtonTimedPressUp(KeyBindName, 0f, PressThreshold))
            {
                Execute();
            }
            else if (Entry.RewiredPlayer.GetButtonTimedPressDown(KeyBindName, PressThreshold))
            {
                SwitchToNextItem();
            }
        }

        public override void Execute()
        {
            if (!CanExecute())
            {
                return;
            }

            var player = Manager.main.player;
            if (TryFindTargetItem(player))
            {
                TriggerAction(player);
            }
        }

        /// <summary>
        ///     Attempts to find the target item in the player's inventory and triggers the action.
        ///     If it finds the item, it updates the <see cref="FromSlotIndex" /> with the found index.
        /// </summary>
        /// <param name="player">The player controller to access inventory.</param>
        /// <param name="startingIndex">
        ///     The index to start searching from (defaults to
        ///     <see cref="InventoryHandlerHelper.DefaultStartingIndex" />).
        /// </param>
        /// <returns>True if a target item was found, otherwise false.</returns>
        protected virtual bool TryFindTargetItem(PlayerController player, int startingIndex = InventoryHandlerHelper.DefaultStartingIndex)
        {
            PreviousSlotIndex = player.equippedSlotIndex;
            FromSlotIndex = InventoryHandlerHelper.InvalidIndex;

            // First check predefined equipment slot.
            if (IsTargetItem(player.playerInventoryHandler.GetObjectData(EquipmentSlotIndex)))
            {
                FromSlotIndex = EquipmentSlotIndex;
                return true;
            }

            // Search through the inventory and apply sorting logic.
            var targetItems = new Dictionary<int, ObjectDataCD>();
            for (var playerInventoryIndex = startingIndex; playerInventoryIndex < player.playerInventoryHandler.size; playerInventoryIndex++)
            {
                var objectData = player.playerInventoryHandler.GetObjectData(playerInventoryIndex);
                if (IsTargetItem(objectData))
                {
                    targetItems.Add(playerInventoryIndex, objectData);
                }
            }

            if (targetItems.Count == 0)
            {
                return false;
            }

            // Apply sorting logic defined by the derived class.
            var sortedItems = targetItems.OrderBy(SortingFunction).FirstOrDefault();
            FromSlotIndex = sortedItems.Key;

            return FromSlotIndex != InventoryHandlerHelper.InvalidIndex;
        }

        /// <summary>
        ///     Switches to the next available item in the player's inventory by searching from the next slot after the currently
        ///     equipped item.
        ///     If no item is found, it will wrap around and search from the beginning of the inventory.
        /// </summary>
        protected virtual void SwitchToNextItem()
        {
            var player = Manager.main.player;

            if (!TryFindTargetItem(player, FromSlotIndex + 1))
            {
                // If nothing was found, wrap around and start from the beginning.
                TryFindTargetItem(player);
            }

            TriggerAction(player);
        }

        /// <summary>
        ///     Executes the action by equipping and using the target item.
        /// </summary>
        /// <param name="player">The player controller.</param>
        protected virtual void TriggerAction(PlayerController player)
        {
            // Swap the item to the correct slot and equip it.
            if (FromSlotIndex != EquipmentSlotIndex)
            {
                player.playerInventoryHandler.Swap(player, FromSlotIndex, player.playerInventoryHandler, EquipmentSlotIndex);
            }

            player.EquipSlot(EquipmentSlotIndex);

            // Reset input history and re-equip the item.
            var inputHistoryFake = EntityUtility.GetComponentData<ClientInputHistoryCD>(player.entity, player.world);
            inputHistoryFake.secondInteractUITriggered = false;
            EntityUtility.SetComponentData(player.entity, player.world, inputHistoryFake);

            player.EquipSlot(EquipmentSlotIndex);

            // Simulate "right-click" or "use" action on the item.
            var inputHistoryConsume = EntityUtility.GetComponentData<ClientInputHistoryCD>(player.entity, player.world);
            inputHistoryConsume.secondInteractUITriggered = true;

            // Swap back to the original item.
            if (FromSlotIndex != EquipmentSlotIndex)
            {
                player.playerInventoryHandler.Swap(player, FromSlotIndex, player.playerInventoryHandler, EquipmentSlotIndex);
            }

            // Setting it here makes it somehow "smoother"...?
            EntityUtility.SetComponentData(player.entity, player.world, inputHistoryConsume);

            if (PreviousSlotIndex == InventoryHandlerHelper.InvalidIndex)
            {
                return;
            }

            // Swaps back to the previously equipped slot after the action is completed.
            Manager.main.player.EquipSlot(PreviousSlotIndex);
        }

        #region Configuration

        protected abstract string KeyBindName { get; }
        public abstract int EquipmentSlotIndex { get; }

        #endregion Configuration

    }
}