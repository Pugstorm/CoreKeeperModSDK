using System.Linq;
using CK_QOL.Core;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CK_QOL.Core.Helpers;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.ShiftClick
{
	internal sealed class ShiftClick : FeatureBase<ShiftClick>
	{
		private static readonly ObjectType[] IgnoredItemTypes =
		{
			ObjectType.Helm,
			ObjectType.BreastArmor,
			ObjectType.PantsArmor,
			ObjectType.Necklace,
			ObjectType.Ring,
			ObjectType.Bag,
			ObjectType.Lantern,
			ObjectType.Offhand,
			ObjectType.Pet
		};

		public ShiftClick()
		{
			ConfigBase.Create(this);
			IsEnabled = ShiftClickConfig.ApplyIsEnabled(this);

			RewiredExtensionModule.AddKeybind(KeyBindName, DisplayName, KeyboardKeyCode.LeftShift);
			RewiredExtensionModule.SetDefaultControllerBinding(KeyBindName, GamepadTemplate.elementId_rightTrigger);
		}

		#region Configuration

		private string KeyBindName => $"{ModSettings.ShortName}_{Name}";

		#endregion Configuration

		public override bool CanExecute()
		{
			return base.CanExecute()
			       && Entry.RewiredPlayer != null
			       && Manager.main.currentSceneHandler?.isInGame == true
			       && Manager.main.player?.playerInventoryHandler != null
			       && Manager.ui.isPlayerInventoryShowing
			       && !IsAnyIgnoredUIOpen();
		}

		public override void Update()
		{
			if (!CanExecute())
			{
				return;
			}

			var isModifierKeyHeldDown = Entry.RewiredPlayer.GetButton(KeyBindName);
			var hasInteractedWithUI = Entry.RewiredPlayer.GetButtonDown((int)PlayerInput.InputType.UI_INTERACT);

			if (isModifierKeyHeldDown && hasInteractedWithUI)
			{
				Execute();
			}
		}

		public override void Execute()
		{
			var player = Manager.main.player;
			var inventorySlotUI = Manager.ui.currentSelectedUIElement as InventorySlotUI;

			var index = inventorySlotUI?.inventorySlotIndex ?? InventoryHandlerHelper.InvalidIndex;
			if (index == InventoryHandlerHelper.InvalidIndex || inventorySlotUI == null)
			{
				return;
			}

			var inventoryHandler = player.playerInventoryHandler;
			var chestInventoryHandler = player.activeInventoryHandler;

			var objectID = inventoryHandler.GetObjectData(index).objectID;
			if (objectID == ObjectID.None)
			{
				return;
			}

			switch (inventorySlotUI.slotType)
			{
				case ItemSlotsUIType.ChestSlot:
					HandleChestSlot(player, chestInventoryHandler, inventoryHandler, objectID, index);

					break;
				case ItemSlotsUIType.PlayerInventorySlot:
					HandlePlayerSlot(player, inventoryHandler, chestInventoryHandler, objectID, index);

					break;
			}
		}

		/// <summary>
		///     Handles moving items from the chest to the player's inventory.
		/// </summary>
		/// <param name="player">The player controller managing the inventory.</param>
		/// <param name="chestHandler">The chest's inventory handler.</param>
		/// <param name="playerHandler">The player's inventory handler.</param>
		/// <param name="objectID">The object id of the item being moved.</param>
		/// <param name="index">The index of the item in the chest's inventory.</param>
		/// <remarks>
		///     Inventory slots will be considered before equipment slots.
		/// </remarks>
		private static void HandleChestSlot(PlayerController player, InventoryHandler chestHandler, InventoryHandler playerHandler, ObjectID objectID, int index)
		{
			var availableSlot = InventoryHandlerHelper.GetNextAvailableIndex(playerHandler, objectID, InventoryHandlerHelper.PlayerBackpackStartingIndex);
			if (availableSlot == InventoryHandlerHelper.InvalidIndex)
			{
				availableSlot = InventoryHandlerHelper.GetNextAvailableIndex(playerHandler, objectID);
			}

			InventoryHandlerHelper.MoveItem(player, chestHandler, playerHandler, index, availableSlot);
		}

		/// <summary>
		///     Handles moving items within the player's inventory or to a chest.
		/// </summary>
		/// <param name="player">The player controller managing the inventory.</param>
		/// <param name="playerHandler">The player's inventory handler.</param>
		/// <param name="chestHandler">The chest's inventory handler.</param>
		/// <param name="objectID">The object id of the item being moved.</param>
		/// <param name="index">The index of the item in the player's inventory.</param>
		private static void HandlePlayerSlot(PlayerController player, InventoryHandler playerHandler, InventoryHandler chestHandler, ObjectID objectID, int index)
		{
			if (IgnoredItemTypes.Contains(PugDatabase.GetObjectInfo(objectID).objectType))
			{
				return;
			}

			if (Manager.ui.isChestInventoryUIShowing)
			{
				var availableSlot = InventoryHandlerHelper.GetNextAvailableIndex(chestHandler, objectID);
				InventoryHandlerHelper.MoveItem(player, playerHandler, chestHandler, index, availableSlot);
			}
			else
			{
				var availableSlot = InventoryHandlerHelper.InvalidIndex;

				if (index < InventoryHandlerHelper.PlayerBackpackStartingIndex)
				{
					availableSlot = InventoryHandlerHelper.GetNextAvailableIndex(playerHandler, objectID, InventoryHandlerHelper.PlayerBackpackStartingIndex);
				}
				else
				{
					availableSlot = InventoryHandlerHelper.GetNextAvailableIndex(playerHandler, objectID, limitIndex: InventoryHandlerHelper.PlayerBackpackStartingIndex);
				}

				InventoryHandlerHelper.MoveItem(player, playerHandler, playerHandler, index, availableSlot);
			}
		}

		/// <summary>
		///     Determines if any ignored UI elements, such as crafting or repair UIs, are open.
		/// </summary>
		/// <returns>True if any ignored UI elements are open, otherwise false.</returns>
		private static bool IsAnyIgnoredUIOpen()
		{
			return new[]
			{
				Manager.ui.cookingCraftingUI.isShowing,
				Manager.ui.processResourcesCraftingUI.isShowing,
				Manager.ui.isSalvageAndRepairUIShowing,
				Manager.ui.bossStatueUI.isShowing,
				Manager.ui.isBuyUIShowing,
				Manager.ui.isSellUIShowing
			}.Any(element => element);
		}

		#region IFeature

		public override string Name => nameof(ShiftClick);
		public override string DisplayName => "Shift + Click";
		public override string Description => "Allows quick moving of items between different inventories.";
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature
	}
}