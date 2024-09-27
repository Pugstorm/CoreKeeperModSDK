using System.Collections.Generic;
using System.Linq;
using CK_QOL.Core.Features;
using CK_QOL.Core.Helpers;

namespace CK_QOL.Features.ChestAutoUnlock
{
	/// <summary>
	///     Provides the "Chest Auto Unlock" feature, allowing players to automatically unlock chests by transferring
	///     required key items from their inventory to the chest's inventory.
	///     This feature detects locked chests and, if the player has all necessary items, automatically moves the required
	///     items to the chest to unlock it.
	/// </summary>
	/// <remarks>
	///     The "Chest Auto Unlock" feature is triggered when the player interacts with a locked chest. It checks if the player
	///     has
	///     all the required items to unlock the chest and moves the required items from the player's inventory to the chest's
	///     inventory. The feature also depends on whether the chest has valid inventory slot requirements.
	/// </remarks>
	internal sealed class ChestAutoUnlock : FeatureBase<ChestAutoUnlock>
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="ChestAutoUnlock" /> class and applies the configuration settings.
		/// </summary>
		public ChestAutoUnlock()
		{
			var config = new ChestAutoUnlockConfig(this);
			IsEnabled = config.ApplyIsEnabled();
		}

		/// <inheritdoc />
		public override bool CanExecute()
		{
			return base.CanExecute() && Manager.main.player?.playerInventoryHandler != null && Manager.ui.isChestInventoryUIShowing &&
				((Manager.main.player?.activeInventoryHandler?.entityMonoBehaviour as Chest)?.inventoryHandler?.HasValidInventorySlotRequirementBuffer() ?? false);
		}

		/// <summary>
		///     Handles the update loop for the Chest Auto Unlock feature.
		///     This method continuously checks if the conditions for unlocking a chest are met, and if so, it executes the
		///     unlocking process.
		/// </summary>
		public override void Update()
		{
			if (!CanExecute())
			{
				return;
			}

			Execute();
		}

		/// <summary>
		///     Executes the chest auto-unlock process. It checks whether the player has all required items to unlock the chest,
		///     and if they do, it moves the required items from the player's inventory to the chest's inventory.
		/// </summary>
		public override void Execute()
		{
			var player = Manager.main.player;
			var chest = player.activeInventoryHandler?.entityMonoBehaviour as Chest;

			var requiredObjectIDs = InventoryHandlerHelper.GetRequiredObjectIDs(chest!.inventoryHandler);
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
		///     Checks if the player has all required items to unlock the chest.
		/// </summary>
		/// <param name="player">The player controller.</param>
		/// <param name="requiredObjectIDs">A collection of object IDs required to unlock the chest.</param>
		/// <returns>
		///     <see langword="true" /> if the player has all the required items; otherwise, <see langword="false" />.
		/// </returns>
		private static bool PlayerHasAllRequiredItems(PlayerController player, IEnumerable<ObjectID> requiredObjectIDs)
		{
			return requiredObjectIDs.Select(objectID => InventoryHandlerHelper.GetIndexOfItem(player.playerInventoryHandler, objectID)).All(playerItemIndex => playerItemIndex != InventoryHandlerHelper.InvalidIndex);
		}

		/// <summary>
		///     Transfers the required items from the player's inventory to the chest's inventory.
		/// </summary>
		/// <param name="player">The player controller.</param>
		/// <param name="chest">The chest to which the items will be transferred.</param>
		/// <param name="requiredObjectIDs">A collection of object IDs that need to be transferred to the chest.</param>
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

		#region IFeature

		/// <inheritdoc />
		public override string Name => nameof(ChestAutoUnlock);

		/// <inheritdoc />
		public override string DisplayName => "Chest Auto Unlock";

		/// <inheritdoc />
		public override string Description => "Allows quick unlocking of locked chests by using available keys.";

		/// <inheritdoc />
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature

	}
}