using System.Collections.Generic;
using CK_QOL_Collection.Core;

namespace CK_QOL_Collection.Features.ItemPickUpNotifier
{
    /// <summary>
    ///     Represents the Item Pick-Up Notifier feature of the mod.
    ///     This feature notifies players when they pick up new items by comparing inventory changes.
    /// </summary>
    /// <remarks>
    ///     This feature works by tracking changes in the player's inventory and notifying the player whenever a new item is
    ///     detected.
    ///     It compares the current state of the inventory with the previous state and identifies any discrepancies.
    /// </remarks>
    /// <seealso cref="Manager.main" />
    /// <seealso cref="Manager.ui" />
    internal class ItemPickUpNotifierFeature : FeatureBase
	{
		private readonly ItemPickUpNotifierConfiguration _config;
		private InventoryHandler _currentInventoryHandler;
		private List<ContainedObjectsBuffer> _previousInventoryObjects = new();

        /// <summary>
        ///     Initializes a new instance of the <see cref="ItemPickUpNotifierFeature" /> class.
        ///     Sets up the Item Pick-Up Notifier feature using the configuration settings.
        /// </summary>
        public ItemPickUpNotifierFeature()
			: base(nameof(ItemPickUpNotifier))
		{
			_config = (ItemPickUpNotifierConfiguration)Configuration;
		}

        /// <inheritdoc />
        /// <remarks>
        ///     Checks whether the feature can execute by validating that no inventory UI is open, no cutscenes are playing, and
        ///     the scene handler is ready.
        /// </remarks>
        public override bool CanExecute() => 
	        base.CanExecute()
	        && Manager.main.player
	        && !Manager.ui.isPlayerInventoryShowing 
	        && !Manager.sceneHandler.cutsceneIsPlaying 
	        && Manager.sceneHandler.isSceneHandlerReady;

        /// <summary>
        ///     Executes the Item Pick-Up Notifier feature, checking for new items added to the player's inventory.
        ///     If new items are detected, a notification is displayed to the player.
        /// </summary>
        public override void Execute()
		{
			if (!CanExecute())
			{
				return;
			}

			if (_previousInventoryObjects.Count == 0)
			{
				InitializePreviousInventoryState();
			}

			CompareInventoryStateAndNotify();
		}

        /// <summary>
        ///     Updates the state of the Item Pick-Up Notifier feature, managing inventory changes and executing the notifier logic
        ///     as needed.
        /// </summary>
        public override void Update()
		{
			if (!CanExecute())
			{
				return;
			}

			_currentInventoryHandler = Manager.main.player.playerInventoryHandler;
			Execute();

			CheckActiveInventoryHandlers();
		}

        /// <summary>
        ///     Initializes the previous inventory state for comparison.
        /// </summary>
        /// <remarks>
        ///     This method is called the first time the notifier executes, setting up the initial state of the inventory.
        ///     It creates a list of default inventory objects corresponding to the player's current inventory size.
        /// </remarks>
        private void InitializePreviousInventoryState()
		{
			var playerInventoryHandler = Manager.main.player.playerInventoryHandler;

			// Initialize the list with the player's inventory size.
			_previousInventoryObjects = new List<ContainedObjectsBuffer>(playerInventoryHandler.maxSize);
			for (var i = 0; i < playerInventoryHandler.maxSize; i++)
			{
				_previousInventoryObjects.Add(default);
			}
		}

        /// <summary>
        ///     Compares the current inventory state with the previous state and notifies the player of any new items.
        /// </summary>
        /// <remarks>
        ///     This method checks each slot in the player's inventory against the stored state to detect new items.
        /// </remarks>
        private void CompareInventoryStateAndNotify()
		{
			for (var i = 0; i < Manager.main.player.playerInventoryHandler.size; i++)
			{
				var containedObjectData = _currentInventoryHandler.GetContainedObjectData(i);
				if (containedObjectData.objectID == ObjectID.None || _previousInventoryObjects[i].Equals(containedObjectData))
				{
					// Continue if the slot is empty or the item has not changed.
					continue;
				}

				// Notify the player of the new item.
				NotifyPlayerOfNewItem(containedObjectData);

				// Update the previous inventory state to the current state.
				_previousInventoryObjects[i] = containedObjectData;
			}
		}

        /// <summary>
        ///     Notifies the player of a new item detected in the inventory.
        /// </summary>
        /// <param name="containedObjectData">The data of the item to notify about.</param>
        /// <remarks>
        ///     This method formats the item information and uses the game's UI manager to display a notification to the player.
        /// </remarks>
        private static void NotifyPlayerOfNewItem(ContainedObjectsBuffer containedObjectData)
        {
	        var text = PlayerController.GetObjectName(containedObjectData, true).text;
			var rarity = PugDatabase.GetObjectInfo(containedObjectData.objectID).rarity;
			
			Manager.ui.ShowDiscoveredItemText(new List<string> { $"-CK-QOL-{text}"}, rarity);
		}

        /// <summary>
        ///     Checks all active inventory handlers for changes and executes the notifier logic if needed.
        /// </summary>
        /// <remarks>
        ///     This method ensures that all relevant inventory handlers (e.g., crafting and output inventories) are checked for
        ///     new items.
        ///     It is called during each update cycle to ensure that all changes are detected.
        /// </remarks>
        private void CheckActiveInventoryHandlers()
		{
			// Check if the player has an active inventory handler that is not the main inventory.
			if (Manager.main.player.activeInventoryHandler != null && Manager.main.player.activeInventoryHandler != Manager.main.player.playerInventoryHandler)
			{
				_currentInventoryHandler = Manager.main.player.activeInventoryHandler;
				Execute();
			}

			// Check if there is an active crafting handler and its inventory is different from the main inventory.
			if (Manager.main.player.activeCraftingHandler == null || Manager.main.player.activeCraftingHandler.inventoryHandler == Manager.main.player.playerInventoryHandler)
			{
				return;
			}

			_currentInventoryHandler = Manager.main.player.activeCraftingHandler.inventoryHandler;
			Execute();

			// Check the output inventory handler from the crafting handler for changes.
			if (Manager.main.player.activeCraftingHandler?.outputInventoryHandler == null)
			{
				return;
			}

			_currentInventoryHandler = Manager.main.player.activeCraftingHandler.outputInventoryHandler;
			Execute();
		}
	}
}