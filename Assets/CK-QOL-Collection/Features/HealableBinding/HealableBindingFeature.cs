using System.Collections;
using CK_QOL_Collection.Core.Feature;
using CK_QOL_Collection.Core.Feature.Configuration;
using CK_QOL_Collection.Core.Helpers;
using CK_QOL_Collection.Features.HealableBinding.KeyBinds;
using Rewired;
using UnityEngine;

namespace CK_QOL_Collection.Features.HealableBinding
{
    /// <summary>
    ///     Represents the 'Healable Binding' feature of the mod.
    ///     This feature allows players to quickly consume healable items from their inventory.
    /// </summary>
    internal class HealableBindingFeature : FeatureBase
    {
        private readonly Player _rewiredPlayer;
        private readonly string _keyBindName;
        private int _previousSlotIndex = -1; // To store the previous equipped slot index

        /// <summary>
        ///     Gets the configuration settings for the 'Healable Binding' feature.
        /// </summary>
        public HealableBindingConfiguration Config { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="HealableBindingFeature" /> class.
        ///     Sets up input handling for the 'Healable Binding' feature.
        /// </summary>
        public HealableBindingFeature()
            : base(nameof(HealableBinding))
        {
            Config = (HealableBindingConfiguration)Configuration;
            _rewiredPlayer = Entry.RewiredPlayer;
            _keyBindName = KeyBindManager.Instance.GetKeyBind<HealableBindingKeyBind>()?.KeyBindName ?? string.Empty;
        }

        /// <inheritdoc />
        public override bool CanExecute() =>
            base.CanExecute()
            && _rewiredPlayer != null
            && Manager.main.player != null
            && !(Manager.input?.textInputIsActive ?? false);

        /// <inheritdoc />
        public override void Execute()
        {
            if (!CanExecute())
            {
                return;
            }

            var player = Manager.main.player;

            var healableSlotIndex = TryFindHealableAndConsume(player);
            if (healableSlotIndex > -1)
            {
                ConsumeHealable(player, healableSlotIndex);
            }
        }

        /// <summary>
        ///     Attempts to find a healable item in the player's inventory and consume it.
        /// </summary>
        /// <param name="player">The player controller to access inventory.</param>
        /// <returns>The index of the slot where a healable item was found; otherwise, -1 if no healable item was found.</returns>
        private int TryFindHealableAndConsume(PlayerController player)
        {
            // Store the current equipped slot index.
            _previousSlotIndex = player.equippedSlotIndex;

            // Check if there's an Healable item in the predefined slot.
            var healableSlotIndex = Config.HealableSlotIndex;
            var foundValidHealableSlotIndex = IsHealable(player.playerInventoryHandler.GetObjectData(healableSlotIndex));

            // If there's no Healable item in the slot, look through the inventory.
            if (foundValidHealableSlotIndex)
            {
                return healableSlotIndex;
            }

            var playerInventorySize = player.playerInventoryHandler.size;
            for (var playerInventoryIndex = 0; playerInventoryIndex < playerInventorySize; playerInventoryIndex++)
            {
                if (!IsHealable(player.playerInventoryHandler.GetObjectData(playerInventoryIndex)))
                {
                    continue;
                }

                // Swap the item to the healable slot.
                player.playerInventoryHandler.Swap(player, playerInventoryIndex, player.playerInventoryHandler, healableSlotIndex);

                return healableSlotIndex;
            }

            //No valid healable found.
            return -1;
        }

        /// <inheritdoc />
        public override void Update()
        {
            if (!CanExecute())
            {
                return;
            }

            if (_rewiredPlayer.GetButtonDown(_keyBindName))
            {
                Execute();
            }

            // Check if the key is released to swap back to the previous slot.
            if (_rewiredPlayer.GetButtonUp(_keyBindName))
            {
                SwapBackToPreviousSlot();
            }
        }

        /// <summary>
        ///     Checks if the provided object data corresponds to a healable item.
        /// </summary>
        /// <param name="objectData">The object data to check.</param>
        /// <returns>True if the object is Healable; otherwise, false.</returns>
        private static bool IsHealable(ObjectDataCD objectData)
        {
            return objectData.objectID is ObjectID.HealingPotion or ObjectID.GreaterHealingPotion;
        }

        /// <summary>
        ///     Consumes the healable item from the specified slot.
        ///     This method equips the slot containing the healable item, resets the input trigger, and re-equips the slot to ensure proper consumption.
        /// </summary>
        /// <param name="player">The player controller to access inventory and use items.</param>
        /// <param name="healableSlotIndex">The inventory slot index where the Healable item is located.</param>
        private static void ConsumeHealable(PlayerController player, int healableSlotIndex)
        {
            // Equip the slot to make sure the correct item is selected
            player.EquipSlot(healableSlotIndex);

            // Get the input history component and reset the secondInteractUITriggered flag to 'false'.
            // This is likely needed to ensure that the game recognizes a fresh input action on the next trigger.
            var inputHistoryFake = EntityUtility.GetComponentData<ClientInputHistoryCD>(player.entity, player.world);
            inputHistoryFake.secondInteractUITriggered = false;
            EntityUtility.SetComponentData(player.entity, player.world, inputHistoryFake);

            // Re-equip the slot to reinitialize the item state, ensuring that any side effects of the input reset are neutralized.
            player.EquipSlot(healableSlotIndex);

            // Set the secondInteractUITriggered flag to 'true' to simulate the "right-click" or "use" action on the item.
            var inputHistoryConsume = EntityUtility.GetComponentData<ClientInputHistoryCD>(player.entity, player.world);
            inputHistoryConsume.secondInteractUITriggered = true;
            EntityUtility.SetComponentData(player.entity, player.world, inputHistoryConsume);
        }

        /// <summary>
        ///     Swaps back to the previously equipped slot after consuming the healable.
        /// </summary>
        private void SwapBackToPreviousSlot()
        {
            if (_previousSlotIndex == -1)
            {
                return;
            }

            // Swap back to the previous slot.
            Manager.main.player.EquipSlot(_previousSlotIndex);
        }
    }
}