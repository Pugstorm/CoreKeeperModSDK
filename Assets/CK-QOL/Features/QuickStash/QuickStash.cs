using System.Linq;
using CK_QOL.Core;
using CK_QOL.Core.Features;
using CK_QOL.Core.Helpers;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.QuickStash
{
	/// <summary>
	///     Provides the "Quick Stash" feature, allowing players to quickly stash items from their inventory into nearby
	///     chests. This feature works by automatically finding nearby chests and moving stackable items from the player's
	///     inventory to matching items in the chests.
	/// </summary>
	internal sealed class QuickStash : FeatureBase<QuickStash>, IKeyBindableFeature
	{
		public QuickStash()
		{
			var config = new QuickStashConfig(this);
			IsEnabled = config.ApplyIsEnabled();
			MaxRange = config.ApplyMaxRange();
			MaxChests = config.ApplyMaxChests();

			SetupKeyBindings();
		}

		public override bool CanExecute()
		{
			return base.CanExecute() && Entry.RewiredPlayer != null && (Manager.main.currentSceneHandler?.isInGame ?? false) && Manager.main.player?.playerInventoryHandler != null;
		}

		public override void Update()
		{
			if (!CanExecute())
			{
				return;
			}

			if (Entry.RewiredPlayer.GetButtonDown(KeyBindName))
			{
				Execute();
			}
		}

		public override void Execute()
		{
			if (!CanExecute())
			{
				return;
			}

			var player = Manager.main.player;
			var playerInventoryHandler = player.playerInventoryHandler;

			var nearbyChests = ChestHelper.GetNearbyChests(MaxRange).Take(MaxChests).ToList();

			if (!nearbyChests.Any())
			{
				TextHelper.DisplayText("No available chests to stash found!");

				return;
			}

			var stashedIntoChestsCount = 0;

			// Iterate through the player's inventory, skipping equipment slots (0-9).
			for (var playerInventorySlotIndex = InventoryHandlerHelper.PlayerBackpackStartingIndex; playerInventorySlotIndex < playerInventoryHandler.size; playerInventorySlotIndex++)
			{
				var itemData = playerInventoryHandler.GetObjectData(playerInventorySlotIndex);
				if (itemData.objectID == ObjectID.None)
				{
					continue;
				}

				var objectInfo = PugDatabase.GetObjectInfo(itemData.objectID);
				if (objectInfo == null || objectInfo.objectID == ObjectID.None || !objectInfo.isStackable)
				{
					continue;
				}

				// Search through nearby chests to find a matching item to stack with.
				foreach (var chest in nearbyChests)
				{
					var chestInventoryHandler = chest.inventoryHandler;
					if (chestInventoryHandler == null)
					{
						continue;
					}

					var chestSlot = InventoryHandlerHelper.GetIndexOfItem(chestInventoryHandler, itemData.objectID);
					if (chestSlot == InventoryHandlerHelper.InvalidIndex)
					{
						continue;
					}

					// Move the item from the player's inventory to the chest and count the stash.
					playerInventoryHandler.TryMoveTo(player, playerInventorySlotIndex, chestInventoryHandler, chestSlot);
					stashedIntoChestsCount++;

					// Break out of the chest loop since the item has been moved.
					break;
				}
			}

			TextHelper.DisplayText(stashedIntoChestsCount == 0 ? "Nothing could be stashed." : $"Stashed into {stashedIntoChestsCount} chests.");
		}

		#region IFeature

		public override string Name => nameof(QuickStash);
		public override string DisplayName => "Quick Stash";
		public override string Description => "Allows quick stashing of inventory items into nearby chests.";
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature

		#region Configuration

		internal float MaxRange { get; }
		internal int MaxChests { get; }

		public string KeyBindName => $"{ModSettings.ShortName}_{Name}";

		public void SetupKeyBindings()
		{
			RewiredExtensionModule.AddKeybind(KeyBindName, DisplayName, KeyboardKeyCode.A, ModifierKey.Control);
		}

		#endregion Configuration

	}
}