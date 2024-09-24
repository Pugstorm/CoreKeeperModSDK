using System.Linq;
using CK_QOL.Core;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CK_QOL.Core.Helpers;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.QuickStash
{
	internal sealed class QuickStash : FeatureBase<QuickStash>
	{
		public QuickStash()
		{
			ConfigBase.Create(this);
			IsEnabled = QuickStashConfig.ApplyIsEnabled(this);
			MaxRange = QuickStashConfig.ApplyMaxRange(this);
			MaxChests = QuickStashConfig.ApplyMaxChests(this);

			RewiredExtensionModule.AddKeybind(KeyBindName, DisplayName, KeyboardKeyCode.A, ModifierKey.Control);
		}

		public override bool CanExecute()
		{
			return base.CanExecute()
			       && Entry.RewiredPlayer != null
			       && (Manager.main.currentSceneHandler?.isInGame ?? false)
			       && Manager.main.player?.playerInventoryHandler != null;
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

			var nearbyChests = ChestHelper.GetNearbyChests(MaxRange)
				.Take(MaxChests)
				.ToList();

			if (!nearbyChests.Any())
			{
				TextHelper.DisplayNotification("Quick Stash: No chests found", Rarity.Legendary);

				return;
			}

			var stashedIntoChestsCount = 0;

			foreach (var chest in nearbyChests)
			{
				var chestInventoryHandler = chest.inventoryHandler;
				if (chestInventoryHandler == null)
				{
					continue;
				}

				// Iterate through the player's inventory, skipping equipment slots (0-9).
				for (var playerInventorySlotIndex = PlayerController.MAX_EQUIPMENT_SLOTS; playerInventorySlotIndex < playerInventoryHandler.size; playerInventorySlotIndex++)
				{
					var itemData = playerInventoryHandler.GetObjectData(playerInventorySlotIndex);
					if (itemData.objectID == ObjectID.None)
					{
						continue;
					}

					var objectInfo = PugDatabase.GetObjectInfo(itemData.objectID);
					if (!objectInfo.isStackable)
					{
						continue;
					}

					var chestSlot = InventoryHandlerHelper.GetIndexOfItem(chestInventoryHandler, itemData.objectID);
					if (chestSlot == InventoryHandlerHelper.InvalidIndex)
					{
						continue;
					}

					playerInventoryHandler.TryMoveTo(player, playerInventorySlotIndex, chestInventoryHandler, chestSlot);
					stashedIntoChestsCount++;
				}
			}

			TextHelper.DisplayNotification(stashedIntoChestsCount == 0
				? "Quick Stash: Nothing to stack"
				: $"Quick Stash: {stashedIntoChestsCount} chests", Rarity.Legendary);
		}

		#region IFeature

		public override string Name => nameof(QuickStash);
		public override string DisplayName => "Quick Stash";
		public override string Description => "Allows quick stashing of inventory items into nearby chests.";
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature

		#region Configuration

		private string KeyBindName => $"{ModSettings.ShortName}_{Name}";
		internal float MaxRange { get; }
		internal int MaxChests { get; }

		#endregion Configuration
	}
}