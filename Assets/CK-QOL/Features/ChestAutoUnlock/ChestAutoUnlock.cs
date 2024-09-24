using System.Collections.Generic;
using System.Linq;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CK_QOL.Core.Helpers;

namespace CK_QOL.Features.ChestAutoUnlock
{
	internal sealed class ChestAutoUnlock : FeatureBase<ChestAutoUnlock>
	{
		public ChestAutoUnlock()
		{
			ConfigBase.Create(this);
			IsEnabled = ChestAutoUnlockConfig.ApplyIsEnabled(this);
		}

		public override bool CanExecute()
		{
			return base.CanExecute()
			       && Manager.main.player?.playerInventoryHandler != null
			       && Manager.ui.isChestInventoryUIShowing
			       && ((Manager.main.player?.activeInventoryHandler?.entityMonoBehaviour as Chest)?.inventoryHandler?.HasValidInventorySlotRequirementBuffer() ?? false);
		}

		public override void Update()
		{
			if (!CanExecute())
			{
				return;
			}

			Execute();
		}

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

		private static bool PlayerHasAllRequiredItems(PlayerController player, IEnumerable<ObjectID> requiredObjectIDs)
		{
			return requiredObjectIDs
				.Select(objectID => InventoryHandlerHelper.GetIndexOfItem(player.playerInventoryHandler, objectID))
				.All(playerItemIndex => playerItemIndex != InventoryHandlerHelper.InvalidIndex);
		}

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

		public override string Name => nameof(ChestAutoUnlock);
		public override string DisplayName => "Chest Auto Unlock";
		public override string Description => "Allows quick unlocking of locked chests by using available keys.";
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature
	}
}