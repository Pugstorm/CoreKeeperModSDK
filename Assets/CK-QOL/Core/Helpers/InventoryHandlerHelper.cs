using System.Collections.Generic;
using System.Linq;
using Inventory;
using Unity.Entities;
using UnityEngine;

namespace CK_QOL.Core.Helpers
{
	/// <summary>
	///     A static helper class that provides utility functions to manage item movements, search for available slots,
	///     and transfer items between inventories in the game.
	///     The class includes methods to search for specific items in inventories, find available empty slots, and handle
	///     interactions between player and chest inventories. It also provides support for moving items from one inventory to
	///     another and checking slot requirements.
	/// </summary>
	/// <remarks>
	///     This class interacts with the inventory system through various methods that centralize the core logic needed for
	///     manipulating inventory slots, moving items, and handling specific object interactions like stackable items and slot
	///     requirements in chests.
	/// </remarks>
	internal static class InventoryHandlerHelper
	{
		/// <summary>
		///     Constant representing an invalid index in the inventory.
		/// </summary>
		internal const int InvalidIndex = -1;

		/// <summary>
		///     Constant representing the default starting index when searching through an inventory.
		/// </summary>
		internal const int DefaultStartingIndex = 0;

		/// <summary>
		///     Constant representing the starting index for player backpack slots, based on equipment slots.
		/// </summary>
		internal const int PlayerBackpackStartingIndex = PlayerController.MAX_EQUIPMENT_SLOTS;

		/// <summary>
		///     Checks if the specified inventory has at least the given amount of a particular item.
		/// </summary>
		/// <param name="inventoryHandler">The inventory handler instance.</param>
		/// <param name="objectID">The item ID to check for.</param>
		/// <param name="requiredAmount">The required amount of the item.</param>
		/// <returns>True if the inventory has at least the required amount of the item; otherwise, false.</returns>
		internal static bool HasItemAmount(InventoryHandler inventoryHandler, ObjectID objectID, int requiredAmount)
		{
			return inventoryHandler.GetExistingAmountOfObject(objectID) >= requiredAmount;
		}

		/// <summary>
		///     Searches for the first occurrence of an item with the specified <paramref name="objectID" /> in the inventory.
		///     This method iterates over the inventory starting from the specified index and returns the index of the first
		///     matching item.
		/// </summary>
		/// <param name="inventoryHandler">The inventory handler responsible for managing the inventory.</param>
		/// <param name="objectID">The object ID to search for within the inventory.</param>
		/// <param name="startingIndex">
		///     The index to start searching from (defaults to <see cref="DefaultStartingIndex" />).
		/// </param>
		/// <param name="skipIndex">
		///     An optional index to skip during the search (defaults to <see cref="InvalidIndex" />).
		/// </param>
		/// <param name="limitIndex">
		///     An optional index to limit during the search (defaults to <see cref="int.MaxValue" />).
		/// </param>
		/// <returns>
		///     The index of the first occurrence of the item with the specified <paramref name="objectID" />,
		///     or <see cref="InvalidIndex" /> if no matching item is found.
		/// </returns>
		/// <remarks>
		///     This method is particularly useful when looking for stackable items or when trying to determine if a specific
		///     object already exists in an inventory.
		/// </remarks>
		internal static int GetIndexOfItem(InventoryHandler inventoryHandler, ObjectID objectID, int startingIndex = DefaultStartingIndex, int skipIndex = InvalidIndex, int limitIndex = int.MaxValue)
		{
			if (inventoryHandler == null || objectID == ObjectID.None)
			{
				ModLogger.Error("Invalid inventory handler or object ID.");

				return InvalidIndex;
			}

			for (var i = startingIndex; i < inventoryHandler.size && i < limitIndex; i++)
			{
				if (inventoryHandler.HasObject(i, objectID) && i != skipIndex)
				{
					return i;
				}
			}

			return InvalidIndex;
		}

		/// <summary>
		///     Finds the next available empty slot in the inventory, starting from the specified index.
		///     An empty slot is represented by an <see cref="ObjectID.None" /> value.
		/// </summary>
		/// <param name="inventoryHandler">The inventory handler managing the inventory.</param>
		/// <param name="startingIndex">
		///     The index to start searching from (defaults to <see cref="DefaultStartingIndex" />).
		/// </param>
		/// <param name="skipIndex">
		///     An optional index to skip during the search (defaults to <see cref="InvalidIndex" />).
		/// </param>
		/// <param name="limitIndex">
		///     An optional index to limit during the search (defaults to <see cref="int.MaxValue" />).
		/// </param>
		/// <returns>
		///     The index of the first available empty slot in the inventory, or <see cref="InvalidIndex" /> if no empty slot is
		///     found.
		/// </returns>
		/// <seealso cref="GetIndexOfItem" />
		internal static int GetNextAvailableIndex(InventoryHandler inventoryHandler, int startingIndex = DefaultStartingIndex, int skipIndex = InvalidIndex, int limitIndex = int.MaxValue)
		{
			return GetIndexOfItem(inventoryHandler, ObjectID.None, startingIndex, skipIndex, limitIndex);
		}

		/// <summary>
		///     Finds the next available empty slot or stackable slot for the specified <paramref name="objectID" />.
		///     This method looks for an empty slot or a slot where the specified object can be stacked (if the object is stackable).
		/// </summary>
		/// <param name="inventoryHandler">The inventory handler managing the inventory.</param>
		/// <param name="objectID">The object ID to find a slot for.</param>
		/// <param name="startingIndex">
		///     The index to start searching from (defaults to <see cref="DefaultStartingIndex" />).
		/// </param>
		/// <param name="skipIndex">
		///     An optional index to skip during the search (defaults to <see cref="InvalidIndex" />).
		/// </param>
		/// <param name="limitIndex">
		///     An optional index to limit during the search (defaults to <see cref="int.MaxValue" />).
		/// </param>
		/// <returns>
		///     The index of the next available slot that can stack the item, or <see cref="InvalidIndex" /> if no suitable slot is
		///     found.
		/// </returns>
		/// <remarks>
		///     This method is useful for features like auto-stashing or quick stacking of items.
		/// </remarks>
		internal static int GetNextAvailableIndex(InventoryHandler inventoryHandler, ObjectID objectID, int startingIndex = DefaultStartingIndex, int skipIndex = InvalidIndex, int limitIndex = int.MaxValue)
		{
			if (objectID == ObjectID.None)
			{
				return GetNextAvailableIndex(inventoryHandler, startingIndex, skipIndex, limitIndex);
			}

			var objectInfo = PugDatabase.GetObjectInfo(objectID);
			if (objectInfo is { isStackable: false })
			{
				return GetNextAvailableIndex(inventoryHandler, startingIndex, skipIndex, limitIndex);
			}

			var indexOfItem = GetIndexOfItem(inventoryHandler, objectID, startingIndex, skipIndex, limitIndex);

			return indexOfItem == InvalidIndex ? GetNextAvailableIndex(inventoryHandler, startingIndex, skipIndex, limitIndex) : indexOfItem;
		}

		/// <summary>
		///     Moves an item from the source inventory to the target inventory.
		///     If either the source or target index is invalid, the movement will not be performed.
		/// </summary>
		/// <param name="player">The player controller handling the movement.</param>
		/// <param name="sourceHandler">The inventory handler for the source inventory.</param>
		/// <param name="targetHandler">The inventory handler for the target inventory.</param>
		/// <param name="sourceIndex">The index of the item in the source inventory.</param>
		/// <param name="targetIndex">The index of the slot in the target inventory to move the item to.</param>
		/// <remarks>
		///     This method allows you to seamlessly move items between player inventory and chest inventory, handling all checks
		///     internally.
		/// </remarks>
		internal static void MoveItem(PlayerController player, InventoryHandler sourceHandler, InventoryHandler targetHandler, int sourceIndex, int targetIndex)
		{
			if (sourceHandler == null || targetHandler == null)
			{
				ModLogger.Error("Invalid source or target inventory handler.");

				return;
			}

			if (sourceIndex == InvalidIndex || targetIndex == InvalidIndex)
			{
				ModLogger.Warn("Invalid move operation: source or target index is invalid.");

				return;
			}

			sourceHandler.TryMoveTo(player, sourceIndex, targetHandler, targetIndex);
		}

		/// <summary>
		///     Retrieves the required object IDs from the slot requirements of the inventory.
		///     This method is useful for chest auto-unlock features, where the player must have specific items in the inventory
		///     to open the chest.
		/// </summary>
		/// <param name="inventoryHandler">The inventory handler managing the inventory with slot requirements.</param>
		/// <returns>
		///     An enumerable collection of <see cref="ObjectID" />s that are required by the inventory slots.
		///     If the inventory has no slot requirements, an empty collection is returned.
		/// </returns>
		internal static ObjectID[] GetRequiredObjectIDs(InventoryHandler inventoryHandler)
		{
			if (inventoryHandler == null || !inventoryHandler.HasValidInventorySlotRequirementBuffer())
			{
				ModLogger.Warn("Invalid inventory handler or no valid slot requirements.");

				return Enumerable.Empty<ObjectID>().ToArray();
			}

			if (!EntityUtility.TryGetBuffer<InventorySlotRequirementBuffer>(inventoryHandler.inventoryEntity, inventoryHandler.entityMonoBehaviour.world, out var inventorySlotRequirementBuffer))
			{
				ModLogger.Warn("Failed to get slot requirements buffer.");

				return Enumerable.Empty<ObjectID>().ToArray();
			}

			var acceptedObjectIds = new List<ObjectID>();

			foreach (var slotRequirement in inventorySlotRequirementBuffer)
			{
				acceptedObjectIds.AddRange(slotRequirement.acceptsObjectIds);
			}

			return acceptedObjectIds.ToArray();
		}

		/// <summary>
		///     Removes a specific amount of items from the inventory.
		///     This method uses SetAmount to decrement the stack size instead of destroying the entire stack.
		/// </summary>
		/// <param name="inventoryHandler">The inventory handler instance.</param>
		/// <param name="objectID">The item ID to remove.</param>
		/// <param name="amount">The amount of the item to remove.</param>
		public static void RemoveItems(InventoryHandler inventoryHandler, ObjectID objectID, int amount)
		{
			for (var i = 0; i < inventoryHandler.size; i++)
			{
				if (!inventoryHandler.HasObject(i, objectID))
				{
					continue;
				}

				var objectData = inventoryHandler.GetObjectData(i);
				var removalAmount = Mathf.Min(objectData.amount, amount);

				Create.SetAmount(inventoryHandler.inventoryEntity, i, objectID, objectData.amount - removalAmount);

				amount -= removalAmount;

				if (amount <= 0)
				{
					break;
				}
			}
		}

		/// <summary>
		///     Gets the <see cref="ContainedObjectsBuffer" /> entries for a specific <see cref="ObjectID" /> from the given <see cref="InventoryHandler" />.
		/// </summary>
		/// <param name="inventoryHandler">The inventory handler to search within.</param>
		/// <param name="objectId">
		///     The object ID to search for (e.g., <see cref="ObjectID.AncientGemstone" />).
		/// </param>
		/// <returns>
		///     A list of <see cref="ContainedObjectsBuffer" /> containing the specified object ID.
		/// </returns>
		public static List<ContainedObjectsBuffer> GetContainedObjectsBufferForObject(InventoryHandler inventoryHandler, ObjectID objectId)
		{
			var containedObjects = new List<ContainedObjectsBuffer>();

			if (inventoryHandler == null || inventoryHandler.inventoryEntity == Entity.Null)
			{
				return containedObjects;
			}

			var buffer = EntityUtility.GetBuffer<ContainedObjectsBuffer>(inventoryHandler.inventoryEntity, Manager.main.player.world);

			foreach (var containedObject in buffer)
			{
				if (containedObject.objectID == objectId)
				{
					containedObjects.Add(containedObject);
				}
			}

			return containedObjects;
		}
	}
}