using System.Collections.Generic;
using System.Linq;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CK_QOL.Core.Helpers;

namespace CK_QOL.Features.ChestAutoUnlock
{
	/// <summary>
	///     Represents the "Chest Auto Unlock" feature, automatically transferring the required items from the
	///     player's inventory to a locked chest's inventory when the chest is opened.
	///     This feature simplifies the chest unlocking process by moving the required keys from the player's inventory to the
	///     chest without manual input.
	///     The class manages the following functionalities:
	///     <list type="bullet">
	///         <item>
	///             <description>
	///                 Configuration of the feature's enabled state, allowing users to toggle the auto unlock feature
	///                 through the configuration system (<see cref="ApplyConfigurations" /> method).
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 Executes the logic for determining whether the player has the required key items in their
	///                 inventory and transfers them to the locked chest automatically (<see cref="Execute" /> method).
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 Handles the transfer of key items from the player's inventory to the chest's inventory if the
	///                 chest has slot requirements and the player has the required items (
	///                 <see cref="TransferRequiredItemsToChest" /> method).
	///             </description>
	///         </item>
	///     </list>
	/// </summary>
	/// <remarks>
	///     This class extends the <see cref="FeatureBase{TFeature}" /> base class to inherit common feature behavior,
	///     including singleton instantiation, configuration management, and execution control.
	///     It streamlines chest unlocking by automating item transfers and simplifies interaction with locked chests.
	/// </remarks>
	internal sealed class ChestAutoUnlock : FeatureBase<ChestAutoUnlock>
    {
        private Chest _lastCheckedChest;

        public ChestAutoUnlock()
        {
            ApplyConfigurations();
        }

        public override bool CanExecute()
        {
            return base.CanExecute()
                && Manager.main.player?.playerInventoryHandler != null
                && Manager.ui.isChestInventoryUIShowing;
        }

        public override void Update()
        {
            if (!CanExecute())
            {
                return;
            }

            var player = Manager.main.player;
            var chest = player.activeInventoryHandler?.entityMonoBehaviour as Chest;

            if (chest == null || !chest.inventoryHandler.HasValidInventorySlotRequirementBuffer())
            {
                return;
            }

            Execute();
        }

        public override void Execute()
        {
            var player = Manager.main.player;
            var chest = player.activeInventoryHandler?.entityMonoBehaviour as Chest;

            if (chest == null)
            {
                return;
            }

            var requiredObjectIDs = InventoryHandlerHelper.GetRequiredObjectIDs(chest.inventoryHandler);
            if (!requiredObjectIDs.Any())
            {
                return;
            }

            if (PlayerHasAllRequiredItems(player, requiredObjectIDs))
            {
                TransferRequiredItemsToChest(player, chest, requiredObjectIDs);
            }
        }

        /// <summary>
        ///     Checks if the player has all the required items in their inventory needed to unlock the chest.
        /// </summary>
        /// <param name="player">The player controller handling the inventory.</param>
        /// <param name="requiredObjectIDs">The collection of ObjectIDs required to unlock the chest.</param>
        /// <returns><see langword="true" /> if the player has all required items, otherwise <see langword="false" />.</returns>
        private static bool PlayerHasAllRequiredItems(PlayerController player, IEnumerable<ObjectID> requiredObjectIDs)
        {
            return requiredObjectIDs
                .Select(objectID => InventoryHandlerHelper.GetIndexOfItem(player.playerInventoryHandler, objectID))
                .All(playerItemIndex => playerItemIndex != InventoryHandlerHelper.InvalidIndex);

        }

        /// <summary>
        ///     Transfers all required items from the player's inventory to the chest's inventory,
        ///     ensuring that the chest can be unlocked by moving the required items in bulk.
        ///     Before moving the items, this method ensures that the player has all the required items
        ///     to unlock the chest. If any item is missing, no transfer occurs.
        /// </summary>
        /// <param name="player">The player controller responsible for handling inventory actions.</param>
        /// <param name="chest">The chest entity containing the inventory to unlock.</param>
        /// <param name="requiredObjectIDs">A collection of ObjectIDs representing the items needed to unlock the chest.</param>
        private static void TransferRequiredItemsToChest(PlayerController player, Chest chest, IEnumerable<ObjectID> requiredObjectIDs)
        {
            var playerInventory = player.playerInventoryHandler;
            var chestInventory = chest.inventoryHandler;

            foreach (var objectID in requiredObjectIDs)
            {
                var playerItemIndex = InventoryHandlerHelper.GetIndexOfItem(playerInventory, objectID);
                var chestEmptySlotIndex = InventoryHandlerHelper.GetNextAvailableIndex(chestInventory);

                if (chestEmptySlotIndex == InventoryHandlerHelper.InvalidIndex)
                {
                    return;
                }

                InventoryHandlerHelper.MoveItem(player, playerInventory, chestInventory, playerItemIndex, chestEmptySlotIndex);
            }
        }

        #region Configuration

        /// <summary>
        ///     Applies the configuration settings for this feature, determining whether it is enabled or disabled.
        /// </summary>
        private void ApplyConfigurations()
        {
            ConfigBase.Create(this);
            IsEnabled = ChestAutoUnlockConfig.ApplyIsEnabled(this);
        }

        #endregion Configuration

        #region IFeature

        public override string Name => nameof(ChestAutoUnlock);
        public override string DisplayName => "Chest Auto Unlock";
        public override string Description => "Allows quick unlocking of locked chests by using available keys.";
        public override FeatureType FeatureType => FeatureType.Client;

        #endregion IFeature

    }
}