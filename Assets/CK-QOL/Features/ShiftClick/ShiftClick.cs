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
	///             ,
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
			ApplyConfigurations();
			ApplyKeyBinds();
		}

		public override bool CanExecute()
		{
			return base.CanExecute()
			       && Entry.RewiredPlayer != null
			       && Manager.main.currentSceneHandler?.isInGame == true
			       && Manager.main.player?.playerInventoryHandler != null
			       && Manager.ui.isPlayerInventoryShowing
			       && !IsAnyIgnoredUIOpen();
		}

		public override void Execute()
		{
			var player = Manager.main.player;
			var inventorySlotUI = Manager.ui.currentSelectedUIElement as InventorySlotUI;
			var index = inventorySlotUI?.inventorySlotIndex ?? -1;

			if (index == -1 || inventorySlotUI == null)
			{
				return;
			}

			var inventoryHandler = player.playerInventoryHandler;
			var chestInventoryHandler = player.activeInventoryHandler;
			var itemData = inventoryHandler.GetObjectData(index);
			var objectInfo = PugDatabase.GetObjectInfo(itemData.objectID);

			if (itemData.objectID == ObjectID.None)
			{
				return;
			}

			switch (inventorySlotUI.slotType)
			{
				case ItemSlotsUIType.ChestSlot:
					HandleChestSlot(player, chestInventoryHandler, inventoryHandler, index);

					return;
				case ItemSlotsUIType.PlayerInventorySlot:
					HandlePlayerSlot(player, inventoryHandler, chestInventoryHandler, objectInfo, index);

					break;
			}
		}

		/// <summary>
		///     Monitors input and processes the Shift + Click action if the conditions are met.
		/// </summary>
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
		///     Handles moving items from the chest to the player's inventory.
		/// </summary>
		/// <param name="player">The player controller managing the inventory.</param>
		/// <param name="chestHandler">The chest's inventory handler.</param>
		/// <param name="playerHandler">The player's inventory handler.</param>
		/// <param name="index">The index of the item in the chest's inventory.</param>
		private static void HandleChestSlot(PlayerController player, InventoryHandler chestHandler, InventoryHandler playerHandler, int index)
		{
			var itemDataChest = chestHandler.GetObjectData(index);
			var objectInfoChest = PugDatabase.GetObjectInfo(itemDataChest.objectID);
			var emptySlot = GetEmptyInventoryIndex(playerHandler, objectInfoChest, -1);

			MoveInventoryItem(player, chestHandler, playerHandler, index, emptySlot);
		}

		/// <summary>
		///     Handles moving items within the player's inventory or to a chest.
		/// </summary>
		/// <param name="player">The player controller managing the inventory.</param>
		/// <param name="playerHandler">The player's inventory handler.</param>
		/// <param name="chestHandler">The chest's inventory handler.</param>
		/// <param name="objectInfo">The object information of the item being moved.</param>
		/// <param name="index">The index of the item in the player's inventory.</param>
		private static void HandlePlayerSlot(PlayerController player, InventoryHandler playerHandler, InventoryHandler chestHandler, ObjectInfo objectInfo, int index)
		{
			if (IgnoredItemTypes.Contains(objectInfo.objectType))
			{
				return;
			}

			if (Manager.ui.isChestInventoryUIShowing)
			{
				var emptySlot = GetIndexOfItemInInventory(chestHandler, objectInfo.isStackable ? objectInfo.objectID : ObjectID.None);
				MoveInventoryItem(player, playerHandler, chestHandler, index, emptySlot);
			}
			else
			{
				var inventorySlot = GetEmptyInventoryIndex(playerHandler, objectInfo, index);
				MoveInventoryItem(player, playerHandler, playerHandler, index, inventorySlot);
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

		/// <summary>
		///     Finds the first available slot in the inventory that is empty or can stack the item, if stackable.
		/// </summary>
		/// <param name="inventoryHandler">The player's inventory handler.</param>
		/// <param name="objectInfo">Information about the object to be moved.</param>
		/// <param name="startingIndex">The starting index for the search.</param>
		/// <returns>The index of the first available slot.</returns>
		private static int GetEmptyInventoryIndex(InventoryHandler inventoryHandler, ObjectInfo objectInfo, int startingIndex = 0)
		{
			var objectID = objectInfo.isStackable ? objectInfo.objectID : ObjectID.None;
			var index = startingIndex != -1 && startingIndex < 10 ? 10 : 0;

			var firstFound = GetIndexOfItemInInventory(inventoryHandler, objectID, index);
			if (!objectInfo.isStackable)
			{
				return firstFound;
			}

			var nextItemIndex = GetIndexOfItemInInventory(inventoryHandler, objectID, 0, firstFound);
			var firstStackableSlot = FindFirstStackableSlot(startingIndex, firstFound, nextItemIndex);

			return firstStackableSlot ?? GetIndexOfItemInInventory(inventoryHandler, ObjectID.None, index);
		}

		/// <summary>
		///     Searches the player's inventory for an item that matches the specified <see cref="ObjectID" />.
		/// </summary>
		/// <param name="inventoryHandler">The inventory handler responsible for managing the player's inventory.</param>
		/// <param name="objectID">The ID of the object to search for.</param>
		/// <param name="index">The starting index for the search.</param>
		/// <param name="skipIndex">An optional index to skip during the search.</param>
		/// <returns>
		///     The index of the first item found that matches the specified <see cref="ObjectID" />.
		///     If no item is found, returns -1.
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
		///     Determines the first available stackable slot between two specified item indices.
		/// </summary>
		/// <param name="initialValue">The initial value used to compare with the first and second indices.</param>
		/// <param name="first">The index of the first potential stackable slot.</param>
		/// <param name="second">The index of the second potential stackable slot.</param>
		/// <returns>The index of the first stackable slot if one is found, otherwise null.</returns>
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

			if (first != second && first != initialValue && first != -1)
			{
				return first;
			}

			return null;
		}

		/// <summary>
		///     Moves an item between inventories.
		/// </summary>
		/// <param name="player">The player controller handling the movement.</param>
		/// <param name="sourceHandler">The source inventory handler.</param>
		/// <param name="targetHandler">The target inventory handler.</param>
		/// <param name="sourceIndex">The index of the item in the source inventory.</param>
		/// <param name="targetIndex">The index to move the item to in the target inventory.</param>
		private static void MoveInventoryItem(PlayerController player, InventoryHandler sourceHandler, InventoryHandler targetHandler, int sourceIndex, int targetIndex)
		{
			if (targetIndex == -1)
			{
				return;
			}

			sourceHandler.TryMoveTo(player, sourceIndex, targetHandler, targetIndex);
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