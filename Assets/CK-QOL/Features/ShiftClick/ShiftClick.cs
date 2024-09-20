using System.Linq;
using CK_QOL.Core;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.ShiftClick
{
	internal sealed class ShiftClick : FeatureBase<ShiftClick>
	{
		private readonly ObjectType[] _ignoredItemTypes =
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
			ApplyConfigurations();
			ApplyKeyBinds();
		}

		public override bool CanExecute()
		{
			return base.CanExecute()
			       && Entry.RewiredPlayer != null
			       && (Manager.main.currentSceneHandler?.isInGame ?? false)
			       && Manager.main.player?.playerInventoryHandler != null
			       && Manager.ui.isPlayerInventoryShowing
			       && !IsAnyIgnoredUIOpen();
		}

		public override void Execute()
		{
			var player = Manager.main.player;
			var inventorySlotUI = Manager.ui.currentSelectedUIElement as InventorySlotUI;
			var index = inventorySlotUI == null
				? -1
				: inventorySlotUI.inventorySlotIndex;

			var inventoryHandler = player.playerInventoryHandler;
			var chestInventoryHandler = player.activeInventoryHandler;

			var itemData = inventoryHandler.GetObjectData(index);
			var objectInfo = PugDatabase.GetObjectInfo(itemData.objectID);

			if (itemData.objectID == ObjectID.None || index == -1 || inventorySlotUI == null)
			{
				return;
			}

			if (inventorySlotUI.slotType == ItemSlotsUIType.ChestSlot)
			{
				var itemDataChest = chestInventoryHandler.GetObjectData(index);
				var objectInfoChest = PugDatabase.GetObjectInfo(itemDataChest.objectID);

				var emptySlot = GetEmptyInventoryIndex(inventoryHandler, objectInfoChest, -1);
				MoveInventoryItem(player, chestInventoryHandler, inventoryHandler, index, emptySlot);

				return;
			}

			if (inventorySlotUI.slotType != ItemSlotsUIType.PlayerInventorySlot)
			{
				return;
			}

			if (Manager.ui.isChestInventoryUIShowing)
			{
				var emptySlot = GetIndexOfItemInInventory(chestInventoryHandler, objectInfo.isStackable ? objectInfo.objectID : ObjectID.None);
				MoveInventoryItem(player, inventoryHandler, chestInventoryHandler, index, emptySlot);

				return;
			}

			if (_ignoredItemTypes.Contains(objectInfo.objectType))
			{
				return;
			}

			var inventorySlot = GetEmptyInventoryIndex(inventoryHandler, objectInfo, index);
			MoveInventoryItem(player, inventoryHandler, inventoryHandler, index, inventorySlot);
		}

		public override void Update()
		{
			if (!CanExecute())
			{
				return;
			}

			var input = Manager.input.singleplayerInputModule;
			var modifierKeyHeldDown = input.IsButtonCurrentlyDown((PlayerInput.InputType)RewiredExtensionModule.GetKeybindId(KeyBindName));
			var interactedWithUI = input.rewiredPlayer.GetButtonDown((int)PlayerInput.InputType.UI_INTERACT);

			if (modifierKeyHeldDown && interactedWithUI)
			{
				Execute();
			}
		}

		private static bool IsAnyIgnoredUIOpen()
		{
			var ignoredUIElementsAreOpened = new[]
			{
				Manager.ui.cookingCraftingUI.isShowing,
				Manager.ui.processResourcesCraftingUI.isShowing,
				Manager.ui.isSalvageAndRepairUIShowing,
				Manager.ui.bossStatueUI.isShowing,
				Manager.ui.isBuyUIShowing,
				Manager.ui.isSellUIShowing
			};

			return ignoredUIElementsAreOpened.Any(element => element);
		}

		private static int GetEmptyInventoryIndex(InventoryHandler inventoryHandler, ObjectInfo objectInfo, int startingIndex = 0)
		{
			var isItemStackable = objectInfo.isStackable;
			var objectID = isItemStackable ? objectInfo.objectID : ObjectID.None;

			var index = startingIndex != -1 && startingIndex < 10 ? 10 : 0;

			var firstFound = GetIndexOfItemInInventory(inventoryHandler, objectID, index);

			if (!isItemStackable)
			{
				return firstFound;
			}

			var nextItemKind = GetIndexOfItemInInventory(inventoryHandler, objectID, 0, firstFound);
			var firstStackableSlot = FindFirstStackableSlot(startingIndex, firstFound, nextItemKind);

			return firstStackableSlot ?? GetIndexOfItemInInventory(inventoryHandler, ObjectID.None, index);
		}

		private static int GetIndexOfItemInInventory(InventoryHandler inventoryHandler, ObjectID objectID, int index = 0, int skipIndex = -1)
		{
			for (var i = index; i < inventoryHandler.size; i++)
			{
				var objData = inventoryHandler.GetObjectData(i);

				if (objData.objectID == objectID && i != skipIndex)
				{
					return i;
				}
			}

			return -1;
		}

		private static int? FindFirstStackableSlot(int initialValue, int first, int second)
		{
			if (first == initialValue && second != -1)
			{
				return second;
			}

			if (second == initialValue && first != -1)
			{
				return first;
			}

			// edge case - when inventory is filled with stackable items of the same kind
			if (first != second && first != initialValue && first != -1)
			{
				return first;
			}

			return null;
		}

		private static void MoveInventoryItem(PlayerController player, InventoryHandler primaryInventoryHandler, InventoryHandler secondaryInventoryHandler, int index, int emptySlot)
		{
			if (emptySlot == -1)
			{
				return;
			}

			primaryInventoryHandler.TryMoveTo(player, index, secondaryInventoryHandler, emptySlot);
		}

		#region IFeature

		public override string Name => nameof(ShiftClick);
		public override string DisplayName => "Shift + Click";
		public override string Description => "Allows quick moving of items between inventories.";
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature

		#region Configuration

		internal string KeyBindName => $"{ModSettings.ShortName}_{Name}";

		private void ApplyConfigurations()
		{
			ConfigBase.Create(this);
			IsEnabled = ShiftClickConfig.ApplyIsEnabled(this);
		}

		private void ApplyKeyBinds()
		{
			RewiredExtensionModule.AddKeybind(KeyBindName, DisplayName, KeyboardKeyCode.LeftShift);
			RewiredExtensionModule.SetDefaultControllerBinding(KeyBindName, GamepadTemplate.elementId_rightTrigger);
		}

		#endregion Configuration
	}
}