using System.Collections;
using CK_QOL_Collection.Core.Feature;
using CK_QOL_Collection.Core.Feature.Configuration;
using CK_QOL_Collection.Core.Helpers;
using CK_QOL_Collection.Features.EatableBinding.KeyBinds;
using Rewired;
using UnityEngine;

namespace CK_QOL_Collection.Features.EatableBinding
{
    /// <summary>
    ///     Represents the 'Eatable Binding' feature of the mod.
    ///     This feature allows players to quickly consume eatable items from their inventory.
    /// </summary>
    internal class EatableBindingFeature : FeatureBase
    {
        private readonly Player _rewiredPlayer;
        private readonly string _keyBindName;
        private int _previousSlotIndex = -1; // To store the previous equipped slot index

        /// <summary>
        ///     Gets the configuration settings for the 'Eatable Binding' feature.
        /// </summary>
        public EatableBindingConfiguration Config { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="EatableBindingFeature" /> class.
        ///     Sets up input handling for the 'Eatable Binding' feature.
        /// </summary>
        public EatableBindingFeature()
            : base(nameof(EatableBinding))
        {
            Config = (EatableBindingConfiguration)Configuration;
            _rewiredPlayer = Entry.RewiredPlayer;
            _keyBindName = KeyBindManager.Instance.GetKeyBind<EatableBindingKeyBind>()?.KeyBindName ?? string.Empty;
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

            var eatableSlotIndex = TryFindEatableAndConsume(player);
            if (eatableSlotIndex > -1)
            {
                ConsumeEatable(player, eatableSlotIndex);
            }
        }

        /// <summary>
        ///     Attempts to find an eatable item in the player's inventory and consume it.
        /// </summary>
        /// <param name="player">The player controller to access inventory.</param>
        /// <returns>The index of the slot where an eatable item was found; otherwise, -1 if no eatable item was found.</returns>
        private int TryFindEatableAndConsume(PlayerController player)
        {
            // Store the current equipped slot index.
            _previousSlotIndex = player.equippedSlotIndex;

            // Check if there's an eatable item in the predefined slot.
            var eatableSlotIndex = Config.EatableSlotIndex;
            var foundValidEatableSlotIndex = IsEatable(player.playerInventoryHandler.GetObjectData(eatableSlotIndex));

            // If there's no eatable item in the slot, look through the inventory.
            if (foundValidEatableSlotIndex)
            {
                return eatableSlotIndex;
            }

            var playerInventorySize = player.playerInventoryHandler.size;
            for (var playerInventoryIndex = 0; playerInventoryIndex < playerInventorySize; playerInventoryIndex++)
            {
                if (!IsEatable(player.playerInventoryHandler.GetObjectData(playerInventoryIndex)))
                {
                    continue;
                }

                // Swap the item to the eatable slot.
                player.playerInventoryHandler.Swap(player, playerInventoryIndex, player.playerInventoryHandler, eatableSlotIndex);

                return eatableSlotIndex;
            }

            //No valid eatable found.
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
        ///     Checks if the provided object data corresponds to an eatable item.
        /// </summary>
        /// <param name="objectData">The object data to check.</param>
        /// <returns>True if the object is eatable; otherwise, false.</returns>
        private static bool IsEatable(ObjectDataCD objectData)
        {
            if (objectData.objectID == ObjectID.None)
            {
                return false;
            }

            var objectInfo = PugDatabase.GetObjectInfo(objectData.objectID, objectData.variation);

            return objectInfo is { objectType: ObjectType.Eatable };
        }

        /// <summary>
        ///     Consumes the eatable item from the specified slot.
        ///     This method equips the slot containing the eatable item, resets the input trigger, and re-equips the slot to ensure proper consumption.
        /// </summary>
        /// <param name="player">The player controller to access inventory and use items.</param>
        /// <param name="eatableSlotIndex">The inventory slot index where the eatable item is located.</param>
        private static void ConsumeEatable(PlayerController player, int eatableSlotIndex)
        {
            // Equip the slot to make sure the correct item is selected
            player.EquipSlot(eatableSlotIndex);

            // Get the input history component and reset the secondInteractUITriggered flag to 'false'.
            // This is likely needed to ensure that the game recognizes a fresh input action on the next trigger.
            var inputHistoryFake = EntityUtility.GetComponentData<ClientInputHistoryCD>(player.entity, player.world);
            inputHistoryFake.secondInteractUITriggered = false;
            EntityUtility.SetComponentData(player.entity, player.world, inputHistoryFake);

            // Re-equip the slot to reinitialize the item state, ensuring that any side effects of the input reset are neutralized.
            player.EquipSlot(eatableSlotIndex);

            // Set the secondInteractUITriggered flag to 'true' to simulate the "right-click" or "use" action on the item.
            var inputHistoryConsume = EntityUtility.GetComponentData<ClientInputHistoryCD>(player.entity, player.world);
            inputHistoryConsume.secondInteractUITriggered = true;
            EntityUtility.SetComponentData(player.entity, player.world, inputHistoryConsume);
        }

        /// <summary>
        ///     Swaps back to the previously equipped slot after consuming the eatable.
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