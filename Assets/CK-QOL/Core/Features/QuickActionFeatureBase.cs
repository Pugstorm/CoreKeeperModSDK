using System;
using System.Collections.Generic;
using System.Linq;
using CK_QOL.Core.Helpers;

namespace CK_QOL.Core.Features
{
	/// <summary>
	///     Represents the base functionality for "Quick Action" features, such as Quick Summon, Quick Heal, and Quick Eat,
	///     which allow players to quickly execute specific actions based on their inventory.
	///     This class provides a standardized framework for these features by managing inventory slots, key bindings, and
	///     input handling logic for quick item switching and action execution.
	///     The class covers the following core functionalities:
	///     <list type="bullet">
	///         <item>
	///             <description>
	///                 Defines the key binding mechanism for triggering the quick action (<see cref="Update" />).
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 Handles item switching by searching for target items in the player's inventory (
	///                 <see cref="SwitchToNextItem" /> and
	///                 <see cref="TryFindAnyTargetItem" />).
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 Executes the action by equipping, using, and then restoring the previously equipped item (
	///                 <see cref="Execute" /> and
	///                 <see cref="TriggerAction" />).
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 Resets the player's inventory state after the action is completed (<see cref="EquipPreviousItem" />).
	///             </description>
	///         </item>
	///     </list>
	/// </summary>
	/// <typeparam name="TFeature">The specific type of the feature inheriting from this class.</typeparam>
	/// <remarks>
	///     This abstract class is inherited by specific feature implementations such as Quick Summon, Quick Heal, and Quick
	///     Eat.
	///     It builds upon the <see cref="FeatureBase{TFeature}" /> class, adding inventory handling and key binding mechanisms
	///     for quick action-related features.
	/// </remarks>
	internal abstract class QuickActionFeatureBase<TFeature> : FeatureBase<TFeature>, IKeyBindableFeature where TFeature : QuickActionFeatureBase<TFeature>, new()
	{
		private bool _actionExecuted;

		protected int FromSlotIndex = InventoryHandlerHelper.InvalidIndex;
		protected ObjectID LastSelectedObjectID = ObjectID.None;
		protected int PreviousSlotIndex = InventoryHandlerHelper.InvalidIndex;

		/// <summary>
		///     Sorting function to determine the order in which items should be selected.
		///     Derived classes can override this to define custom sorting logic.
		/// </summary>
		protected virtual Func<KeyValuePair<int, ObjectDataCD>, object> SortingFunction => _ => 0;

		/// <summary>
		///     Determines whether the current object data represents the target item for the feature.
		/// </summary>
		/// <param name="objectData">The object data of the item to be checked.</param>
		/// <returns>
		///     <see langword="true" /> if the object data matches the target item; otherwise, <see langword="false" />.
		/// </returns>
		protected abstract bool IsTargetItem(ObjectDataCD objectData);

		/// <inheritdoc />
		public override bool CanExecute()
		{
			return base.CanExecute() && Entry.RewiredPlayer != null && Manager.main.player != null && !(Manager.input?.textInputIsActive ?? false);
		}

		/// <summary>
		///     Manages the update loop for the quick action feature, which monitors player input and executes the relevant action
		///     based on key presses.
		///     The update logic handles three main types of input interactions:
		///     <list type="bullet">
		///         <item>
		///             <description>
		///                 Single press: Executes the quick action by finding and equipping the target item (
		///                 <see cref="Execute" />).
		///             </description>
		///         </item>
		///         <item>
		///             <description>
		///                 Double press: Switches to the next available item in the player's inventory (
		///                 <see cref="SwitchToNextItem" />).
		///             </description>
		///         </item>
		///         <item>
		///             <description>
		///                 Key release: Re-equips the previously equipped item after the action is completed (
		///                 <see cref="EquipPreviousItem" />).
		///             </description>
		///         </item>
		///     </list>
		///     This method is part of the feature's update loop and is called continuously to monitor player input.
		/// </summary>
		/// <remarks>
		///     This method ensures that the quick action feature is executed only when certain conditions are met, such as when
		///     the player is not interacting with other UI elements or engaged in text input.
		///     It handles the complexity of switching between items, executing actions, and restoring the player's original state
		///     after the action completes.
		/// </remarks>
		public override void Update()
		{
			if (!CanExecute())
			{
				return;
			}

			// Handle single key press: execute the action.
			if (Entry.RewiredPlayer.GetButtonSinglePressDown(KeyBindName))
			{
				FromSlotIndex = InventoryHandlerHelper.InvalidIndex;
				PreviousSlotIndex = InventoryHandlerHelper.InvalidIndex;

				if (LastSelectedObjectID == ObjectID.None)
				{
					SwitchToNextItem();
				}

				// Execute the quick action (e.g., equip and use item).
				Execute();
				_actionExecuted = true;
			}

			// Handle double key press: switch to the next item.
			if (Entry.RewiredPlayer.GetButtonDoublePressDown(KeyBindName))
			{
				SwitchToNextItem();
			}

			// Handle key release: equip the previous item if an action was performed.
			if (Entry.RewiredPlayer.GetButtonUp(KeyBindName) && _actionExecuted)
			{
				EquipPreviousItem();
				_actionExecuted = false;
			}
		}

		/// <summary>
		///     Executes the quick action by attempting to find a target item in the player's inventory and using it.
		///     The method follows this logic:
		///     <list type="bullet">
		///         <item>
		///             <description>
		///                 If a specific target item (by <see cref="LastSelectedObjectID" />) is already identified, it will
		///                 attempt
		///                 to use it first (<see cref="TryFindSpecificTarget" />).
		///             </description>
		///         </item>
		///         <item>
		///             <description>
		///                 If no specific target item is found, it will search for any valid item in the inventory
		///                 that matches the feature's target criteria (<see cref="TryFindAnyTargetItem" />).
		///             </description>
		///         </item>
		///         <item>
		///             <description>
		///                 If a target item is found, the feature triggers the associated action (e.g., equip and use the item)
		///                 (<see cref="TriggerAction" />).
		///             </description>
		///         </item>
		///         <item>
		///             <description>
		///                 If no item is found, the inventory state is reset.
		///             </description>
		///         </item>
		///     </list>
		/// </summary>
		/// <remarks>
		///     This method ensures the quick action feature properly finds the relevant item in the player's inventory and
		///     performs the desired action. It is typically called in response to a key press detected in the
		///     <see cref="Update" />
		///     method.
		/// </remarks>
		public override void Execute()
		{
			if (!CanExecute())
			{
				return;
			}

			var player = Manager.main.player;

			// First, try to find a specific item by its ObjectID.
			if (TryFindSpecificTarget(player, LastSelectedObjectID) || TryFindAnyTargetItem(player))
			{
				// If a valid target is found, trigger the corresponding action.
				TriggerAction(player);
			}
			else
			{
				// Reset inventory slot indices if no valid item is found.
				FromSlotIndex = InventoryHandlerHelper.InvalidIndex;
				PreviousSlotIndex = InventoryHandlerHelper.InvalidIndex;
			}
		}

		/// <summary>
		///     Attempts to find the specific ObjectID in the player's inventory.
		/// </summary>
		/// <param name="player">The player controller.</param>
		/// <param name="objectID">The ObjectID to search for.</param>
		/// <returns>
		///     <see langword="true" /> if the objectID is found; otherwise, <see langword="false" />.
		/// </returns>
		protected virtual bool TryFindSpecificTarget(PlayerController player, ObjectID objectID)
		{
			if (objectID == ObjectID.None)
			{
				return false;
			}

			var playerInventoryHandler = player.playerInventoryHandler;

			// First check predefined equipment slot.
			if (playerInventoryHandler.GetObjectData(EquipmentSlotIndex).objectID == objectID)
			{
				FromSlotIndex = EquipmentSlotIndex;

				return true;
			}

			// Search through the player's inventory for the specific objectID.
			var foundIndex = InventoryHandlerHelper.GetIndexOfItem(playerInventoryHandler, objectID);
			if (foundIndex != InventoryHandlerHelper.InvalidIndex)
			{
				FromSlotIndex = foundIndex;

				return true;
			}

			FromSlotIndex = InventoryHandlerHelper.InvalidIndex;

			return false;
		}

		/// <summary>
		///     Attempts to find the target item in the player's inventory and triggers the action.
		/// </summary>
		/// <param name="player">The player controller to access inventory.</param>
		/// <param name="startingIndex">
		///     The index to start searching from (defaults to <see cref="InventoryHandlerHelper.DefaultStartingIndex" />).
		/// </param>
		/// <returns>
		///     <see langword="true" /> if a target item was found; otherwise, <see langword="false" />.
		/// </returns>
		/// <remarks>
		///     This method applies a sorting function to find the best target item in the player's inventory.
		/// </remarks>
		protected virtual bool TryFindAnyTargetItem(PlayerController player, int startingIndex = InventoryHandlerHelper.DefaultStartingIndex)
		{
			// First check predefined equipment slot.
			if (IsTargetItem(player.playerInventoryHandler.GetObjectData(EquipmentSlotIndex)))
			{
				FromSlotIndex = EquipmentSlotIndex;

				return true;
			}

			// Search through the inventory and apply sorting logic.
			var targetItems = new Dictionary<int, ObjectDataCD>();
			for (var playerInventoryIndex = startingIndex; playerInventoryIndex < player.playerInventoryHandler.size; playerInventoryIndex++)
			{
				var objectData = player.playerInventoryHandler.GetObjectData(playerInventoryIndex);
				if (IsTargetItem(objectData))
				{
					targetItems.Add(playerInventoryIndex, objectData);
				}
			}

			if (targetItems.Count == 0)
			{
				return false;
			}

			// Apply sorting logic defined by the derived class.
			var itemToUse = targetItems.OrderBy(SortingFunction).First();
			FromSlotIndex = itemToUse.Key;
			LastSelectedObjectID = itemToUse.Value.objectID;

			return true;
		}

		/// <summary>
		///     Switches to the next available item in the player's inventory by searching from the next slot after the currently
		///     equipped item.
		///     If no item is found, it will wrap around and search from the beginning of the inventory.
		/// </summary>
		protected virtual void SwitchToNextItem()
		{
			var player = Manager.main.player;
			var foundItem = TryFindAnyTargetItem(player, FromSlotIndex + 1);

			if (!foundItem && FromSlotIndex != InventoryHandlerHelper.InvalidIndex)
			{
				// If nothing was found, wrap around and start from the beginning.
				foundItem = TryFindAnyTargetItem(player);
			}

			if (!foundItem)
			{
				return;
			}

			var item = player.playerInventoryHandler.GetContainedObjectData(FromSlotIndex);
			var text = PlayerController.GetObjectName(item, true).text;
			var rarity = PugDatabase.GetObjectInfo(item.objectData.objectID).rarity;

			TextHelper.DisplayText(text, rarity);
		}

		/// <summary>
		///     Executes the action by equipping and using the target item.
		/// </summary>
		/// <param name="player">The player controller.</param>
		protected virtual void TriggerAction(PlayerController player)
		{
			PreviousSlotIndex = player.equippedSlotIndex;

			// Swap the item to the correct slot, when needed...
			if (FromSlotIndex != EquipmentSlotIndex)
			{
				player.playerInventoryHandler.Swap(player, FromSlotIndex, player.playerInventoryHandler, EquipmentSlotIndex);
			}

			// ...and equip it.
			player.EquipSlot(EquipmentSlotIndex);

			// Reset input history...
			var inputHistoryFake = EntityUtility.GetComponentData<ClientInputHistoryCD>(player.entity, player.world);
			inputHistoryFake.secondInteractUITriggered = false;
			EntityUtility.SetComponentData(player.entity, player.world, inputHistoryFake);

			// ...and re-equip the item.
			player.EquipSlot(EquipmentSlotIndex);

			// Simulate "right-click" or "use" action on the item.
			var inputHistoryConsume = EntityUtility.GetComponentData<ClientInputHistoryCD>(player.entity, player.world);
			inputHistoryConsume.secondInteractUITriggered = true;

			// Swap back to the original item, if needed.
			if (FromSlotIndex != EquipmentSlotIndex && player.playerInventoryHandler.GetObjectData(FromSlotIndex).objectID != ObjectID.None)
			{
				player.playerInventoryHandler.Swap(player, FromSlotIndex, player.playerInventoryHandler, EquipmentSlotIndex);
			}

			// Setting it here makes it somehow "smoother"...?!
			EntityUtility.SetComponentData(player.entity, player.world, inputHistoryConsume);
		}

		private void EquipPreviousItem()
		{
			if (PreviousSlotIndex == InventoryHandlerHelper.InvalidIndex)
			{
				return;
			}

			Manager.main.player.EquipSlot(PreviousSlotIndex);
		}

		#region Configuration

		/// <summary>
		///     The key binding name for the feature.
		///     Must be implemented by the derived class to specify the appropriate key bind.
		/// </summary>
		public abstract string KeyBindName { get; }

		public virtual void SetupKeyBindings()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		///     The equipment slot index used by the feature.
		///     Must be implemented by the derived class to specify the appropriate slot.
		/// </summary>
		public abstract int EquipmentSlotIndex { get; }

		#endregion Configuration
	}
}