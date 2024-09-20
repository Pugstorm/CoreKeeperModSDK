using System.Linq;
using CK_QOL.Core;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.ShiftClick
{
	/// <summary>
	///     Represents the "Shift + Click" feature, allowing players to quickly move items between their inventory and
	///     other containers such as chests. This feature provides a key binding that enables users to transfer items
	///     with a simple key and mouse click combination.
	///     The class manages the following functionalities:
	///     <list type="bullet">
	///         <item>
	///             <description>
	///                 Configuration of the feature's enabled state and key bindings for quickly moving items between
	///                 inventories (<see cref="ApplyKeyBinds" /> method).
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 Executes the logic for determining which items are being clicked, then moves them to the target
	///                 inventory if an appropriate slot is available (<see cref="Execute" /> method).
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 Monitors key inputs and manages the movement of items between the player's inventory and other
	///                 inventories, such as chests, using the Shift + Click shortcut (<see cref="Update" /> method).
	///             </description>
	///         </item>
	///     </list>
	/// </summary>
	/// <remarks>
	///     This class extends the <see cref="FeatureBase{TFeature}" /> base class to inherit common feature behavior,
	///     including singleton instantiation, configuration management, and execution control.
	///     It provides an optimized mechanism for item management using input handling and inventory management.
	/// </remarks>
	internal sealed class ShiftClick : FeatureBase<ShiftClick>
	{
		// Define item types that should be ignored during Shift + Click operations.
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

		/// <summary>
		///     Determines if any of the ignored UI elements, such as crafting or repair UIs, are open.
		/// </summary>
		/// <returns><see langword="true" /> if any ignored UI elements are open; otherwise, <see langword="false" />.</returns>
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

		/// <summary>
		///     Finds the first available slot in the inventory that is empty or can stack the item, if stackable.
		/// </summary>
		/// <param name="inventoryHandler">The player's inventory handler.</param>
		/// <param name="objectInfo">Information about the object to be moved.</param>
		/// <param name="startingIndex">The starting index for the search.</param>
		/// <returns>The index of the first available slot.</returns>
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

		/// <summary>
		///     Searches the player's inventory for an item that matches the specified <see cref="ObjectID" />.
		///     This method can skip a specified index to avoid conflicts during item movement operations.
		/// </summary>
		/// <param name="inventoryHandler">The inventory handler responsible for managing the player's inventory.</param>
		/// <param name="objectID">The ID of the object to search for.</param>
		/// <param name="index">
		///     The starting index for the search. Defaults to 0, which means the search will start from the beginning
		///     of the inventory.
		/// </param>
		/// <param name="skipIndex">
		///     An optional index to skip during the search. Defaults to -1, meaning no index will be skipped.
		///     This can be used to avoid selecting the same item being moved.
		/// </param>
		/// <returns>
		///     The index of the first item found that matches the specified <see cref="ObjectID" />. If no item is found,
		///     returns -1.
		/// </returns>
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

		/// <summary>
		///     Determines the first available stackable slot between two specified item indices. This method is useful
		///     when dealing with stackable items to ensure that items are placed in existing stacks if possible.
		/// </summary>
		/// <param name="initialValue">
		///     The initial value used to compare with the first and second indices. Typically this represents the current
		///     item being checked.
		/// </param>
		/// <param name="first">The index of the first potential stackable slot.</param>
		/// <param name="second">The index of the second potential stackable slot.</param>
		/// <returns>
		///     The index of the first stackable slot if one is found. If no slot is found, returns <see langword="null" />.
		/// </returns>
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

			// Handles edge case when the inventory is filled with stackable items of the same kind.
			if (first != second && first != initialValue && first != -1)
			{
				return first;
			}

			return null;
		}

		/// <summary>
		///     Moves an item between inventories by trying to place it in the target slot.
		/// </summary>
		/// <param name="player">The player controller.</param>
		/// <param name="primaryInventoryHandler">The source inventory handler.</param>
		/// <param name="secondaryInventoryHandler">The target inventory handler.</param>
		/// <param name="index">The index of the item in the source inventory.</param>
		/// <param name="emptySlot">The target slot in the destination inventory.</param>
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