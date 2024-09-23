using System.Linq;

namespace CK_QOL.Core.Helpers
{
    /// <summary>
    ///     A static helper class that provides utility functions to manage item movements, search for available slots,
    ///     and transfer items between inventories in the game. It also defines constants and methods to interact
    ///     with the inventory system efficiently.
    /// </summary>
    /// <remarks>
    ///     This class interacts with the inventory system through various methods that help in searching, moving, and
    ///     organizing items. It centralizes the core logic needed for manipulating inventory slots, checking available
    ///     space, and handling required items in chests.
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
        ///     Searches for the first occurrence of an item with the specified <paramref name="objectID" /> in the inventory.
        /// </summary>
        /// <param name="inventoryHandler">The inventory handler responsible for managing the inventory.</param>
        /// <param name="objectID">The object ID to search for within the inventory.</param>
        /// <param name="startingIndex">The index to start searching from (defaults to <see cref="DefaultStartingIndex" />).</param>
        /// <param name="skipIndex">An optional index to skip during the search (defaults to <see cref="InvalidIndex" />).</param>
        /// <returns>
        ///     The index of the first occurrence of the item with the specified <paramref name="objectID" />,
        ///     or <see cref="InvalidIndex" /> if no matching item is found.
        /// </returns>
        internal static int GetIndexOfItem(InventoryHandler inventoryHandler, ObjectID objectID, int startingIndex = DefaultStartingIndex, int skipIndex = InvalidIndex)
        {
            for (var i = startingIndex; i < inventoryHandler.size; i++)
            {
                var objectData = inventoryHandler.GetObjectData(i);
                if (objectData.objectID == objectID && i != skipIndex)
                {
                    return i;
                }
            }

            return InvalidIndex;
        }

        /// <summary>
        ///     Finds the next available empty slot in the inventory, starting from the specified index.
        /// </summary>
        /// <param name="inventoryHandler">The inventory handler managing the inventory.</param>
        /// <param name="startingIndex">The index to start searching from (defaults to <see cref="DefaultStartingIndex" />).</param>
        /// <returns>
        ///     The index of the first available empty slot in the inventory, or <see cref="InvalidIndex" /> if no empty slot is
        ///     found.
        /// </returns>
        internal static int GetNextAvailableIndex(InventoryHandler inventoryHandler, int startingIndex = DefaultStartingIndex)
        {
            for (var i = startingIndex; i < inventoryHandler.size; i++)
            {
                var objectData = inventoryHandler.GetObjectData(i);
                if (objectData.objectID == ObjectID.None)
                {
                    return i;
                }
            }

            return InvalidIndex;
        }

        /// <summary>
        ///     Finds the next available empty slot or stackable slot for the specified <paramref name="objectID" />.
        /// </summary>
        /// <param name="inventoryHandler">The inventory handler managing the inventory.</param>
        /// <param name="objectID">The object ID to find a slot for.</param>
        /// <param name="startingIndex">The index to start searching from (defaults to <see cref="DefaultStartingIndex" />).</param>
        /// <returns>
        ///     The index of the next available slot that can stack the item, or <see cref="InvalidIndex" /> if no suitable slot is
        ///     found.
        /// </returns>
        internal static int GetNextAvailableIndex(InventoryHandler inventoryHandler, ObjectID objectID, int startingIndex = DefaultStartingIndex)
        {
            if (objectID == ObjectID.None)
            {
                return GetNextAvailableIndex(inventoryHandler, startingIndex);
            }

            var objectInfo = PugDatabase.GetObjectInfo(objectID);
            if (objectInfo is { isStackable: false })
            {
                return GetNextAvailableIndex(inventoryHandler, startingIndex);
            }

            var indexOfItem = GetIndexOfItem(inventoryHandler, objectID, startingIndex);
            return indexOfItem == InvalidIndex
                ? GetNextAvailableIndex(inventoryHandler, startingIndex)
                : indexOfItem;
        }

        /// <summary>
        ///     Moves an item from the source inventory to the target inventory.
        /// </summary>
        /// <param name="player">The player controller handling the movement.</param>
        /// <param name="sourceHandler">The inventory handler for the source inventory.</param>
        /// <param name="targetHandler">The inventory handler for the target inventory.</param>
        /// <param name="sourceIndex">The index of the item in the source inventory.</param>
        /// <param name="targetIndex">The index of the slot in the target inventory to move the item to.</param>
        /// <remarks>
        ///     If either the <paramref name="sourceIndex" /> or the <paramref name="targetIndex" /> is invalid,
        ///     the movement will not be performed.
        /// </remarks>
        internal static void MoveItem(PlayerController player, InventoryHandler sourceHandler, InventoryHandler targetHandler, int sourceIndex, int targetIndex)
        {
            if (sourceIndex == InvalidIndex || targetIndex == InvalidIndex)
            {
                return;
            }

            sourceHandler.TryMoveTo(player, sourceIndex, targetHandler, targetIndex);
        }

        /// <summary>
        ///     Retrieves the required object IDs from the slot requirements of the inventory.
        /// </summary>
        /// <param name="inventoryHandler">The inventory handler managing the inventory with slot requirements.</param>
        /// <returns>
        ///     An enumerable collection of <see cref="ObjectID" />s that are required by the inventory slots.
        ///     If the inventory has no slot requirements, an empty collection is returned.
        /// </returns>
        internal static ObjectID[] GetRequiredObjectIDs(InventoryHandler inventoryHandler)
        {
            if (!inventoryHandler.HasValidInventorySlotRequirementBuffer())
            {
                return Enumerable.Empty<ObjectID>().ToArray();
            }

            if (!EntityUtility.TryGetBuffer<InventorySlotRequirementBuffer>(inventoryHandler.inventoryEntity, inventoryHandler.entityMonoBehaviour.world, out var inventorySlotRequirementBuffer))
            {
                return Enumerable.Empty<ObjectID>().ToArray();
            }

            return inventorySlotRequirementBuffer.SelectMany(slotRequirement => slotRequirement.acceptsObjectIds).ToArray();
        }
    }
}