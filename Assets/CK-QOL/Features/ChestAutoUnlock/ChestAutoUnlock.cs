using System.Collections.Generic;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using Unity.Entities;

namespace CK_QOL.Features.ChestAutoUnlock
{
    /// <summary>
    ///     Represents the "Chest Auto Unlock" feature, automatically transferring the required items from the
    ///     player's inventory to a locked chest's inventory when the chest is opened.
    ///     This feature simplifies the chest unlocking process by moving the required keys from the player's inventory to the
    ///     chest without manual input.
    ///     The class manages the following functionalities:
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 Configuration of the feature's enabled state, allowing users to toggle the auto unlock feature
    ///                 through the configuration system (<see cref="ApplyConfigurations" /> method).
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Executes the logic for determining whether the player has the required key items in their
    ///                 inventory and transfers them to the locked chest automatically (<see cref="Execute" /> method).
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Handles the transfer of key items from the player's inventory to the chest's inventory if the
    ///                 chest has slot requirements and the player has the required items (
    ///                 <see cref="TransferRequiredItemsToChest" /> method).
    ///             </description>
    ///         </item>
    ///     </list>
    /// </summary>
    /// <remarks>
    ///     This class extends the <see cref="FeatureBase{TFeature}" /> base class to inherit common feature behavior,
    ///     including singleton instantiation, configuration management, and execution control.
    ///     It streamlines chest unlocking by automating item transfers and simplifies interaction with locked chests.
    /// </remarks>
    internal sealed class ChestAutoUnlock : FeatureBase<ChestAutoUnlock>
	{
		private Chest _lastCheckedChest;

		public ChestAutoUnlock()
		{
			ApplyConfigurations();
		}

		public override bool CanExecute()
		{
			return base.CanExecute()
			       && Manager.main.player?.playerInventoryHandler != null
			       && Manager.ui.isChestInventoryUIShowing;
		}

		public override void Update()
		{
			if (!CanExecute())
			{
				return;
			}

			var player = Manager.main.player;
			var chest = player.activeInventoryHandler?.entityMonoBehaviour as Chest;

			if (chest == null || !chest.inventoryHandler.HasValidInventorySlotRequirementBuffer())
			{
				return;
			}

			Execute();
		}

		public override void Execute()
		{
			var player = Manager.main.player;
			var chest = player.activeInventoryHandler.entityMonoBehaviour as Chest;

			var slotRequirementBuffer = GetSlotRequirementBuffer(chest);
			if (slotRequirementBuffer is not { Length: > 0 })
			{
				return;
			}

			foreach (var slotRequirement in slotRequirementBuffer.Value)
			{
				var requiredKeys = new List<ObjectID>();
				foreach (var id in slotRequirement.acceptsObjectIds)
				{
					requiredKeys.Add(id);
				}

				if (requiredKeys.Count <= 0)
				{
					continue;
				}

				TransferRequiredItemsToChest(player, chest, requiredKeys);
			}
		}

        /// <summary>
        ///     Fetches the InventorySlotRequirementBuffer from the chest's inventory entity.
        /// </summary>
        /// <param name="chest">The chest entity.</param>
        /// <returns>A nullable buffer of InventorySlotRequirementBuffer if it exists.</returns>
        private static DynamicBuffer<InventorySlotRequirementBuffer>? GetSlotRequirementBuffer(Chest chest)
		{
			if (EntityUtility.TryGetBuffer(chest.inventoryHandler.inventoryEntity, chest.world, out DynamicBuffer<InventorySlotRequirementBuffer> slotRequirementBuffer))
			{
				return slotRequirementBuffer;
			}

			return null;
		}

        /// <summary>
        ///     Transfers the required key items from the player's inventory to the chest's inventory.
        ///     This method ensures that the chest receives the necessary items for unlocking.
        /// </summary>
        /// <param name="player">The player controller.</param>
        /// <param name="chest">The chest entity.</param>
        /// <param name="requiredKeys">List of required ObjectIDs (keys).</param>
        /// <returns>True if the keys were successfully transferred; otherwise, false.</returns>
        private static bool TransferRequiredItemsToChest(PlayerController player, Chest chest, List<ObjectID> requiredKeys)
		{
			var playerInventory = player.playerInventoryHandler;
			var chestInventory = chest.inventoryHandler;

			for (var i = 0; i < playerInventory.size; i++)
			{
				var objectData = playerInventory.GetObjectData(i);
				if (!requiredKeys.Contains(objectData.objectID))
				{
					continue;
				}

				var emptySlotIndex = GetEmptyInventoryIndex(chestInventory);
				if (emptySlotIndex == -1)
				{
					return false;
				}

				MoveInventoryItem(player, playerInventory, chestInventory, i, emptySlotIndex);

				return true;

			}

			return false;
		}

        /// <summary>
        ///     Moves an item between inventories, facilitating the transfer of key items between the player's inventory
        ///     and the chest's inventory.
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

        /// <summary>
        ///     Finds the first available slot in the chest's inventory that is empty.
        ///     This method ensures that key items can be placed in available slots.
        /// </summary>
        /// <param name="inventoryHandler">The chest's inventory handler.</param>
        /// <returns>The index of the first available slot.</returns>
        private static int GetEmptyInventoryIndex(InventoryHandler inventoryHandler)
		{
			for (var i = 0; i < inventoryHandler.size; i++)
			{
				var objData = inventoryHandler.GetObjectData(i);

				if (objData.objectID == ObjectID.None)
				{
					return i;
				}
			}

			return -1;
		}

		#region Configuration

        /// <summary>
        ///     Applies the configuration settings for this feature, determining whether it is enabled or disabled.
        /// </summary>
        private void ApplyConfigurations()
		{
			ConfigBase.Create(this);
			IsEnabled = ChestAutoUnlockConfig.ApplyIsEnabled(this);
		}

		#endregion Configuration

		#region IFeature

		public override string Name => nameof(ChestAutoUnlock);
		public override string DisplayName => "Chest Auto Unlock";
		public override string Description => "Allows quick unlocking of locked chests by using available keys.";
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature
	}
}