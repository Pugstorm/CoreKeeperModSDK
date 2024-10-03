using System.Collections.Generic;
using System.Linq;
using CK_QOL.Core;
using CK_QOL.Core.Features;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.QuickEat
{
	/// <summary>
	///     Provides the "Quick Eat" feature, allowing players to quickly equip and consume an eatable item, such as food, and
	///     then revert to the previously equipped item.
	///     This feature prioritizes cooked food and non-potion items for consumption.
	/// </summary>
	/// <remarks>
	///     The "Quick Eat" feature helps players quickly consume food without manually searching their inventory.
	///     It sorts available items based on whether they are cooked food, ensuring the most beneficial items are consumed
	///     first.
	///     This class inherits from <see cref="QuickActionFeatureBase{TFeature}" /> to provide common functionality for item
	///     consumption.
	/// </remarks>
	internal sealed class QuickEat : FeatureBase<QuickEat>, IKeyBindableFeature
	{
		private int _fromSlotIndex = -1;
		private int _previousSlotIndex = -1;

		/// <summary>
		///     Initializes a new instance of the <see cref="QuickEat" /> class, applying configuration settings and binding
		///     the key for the eating action.
		/// </summary>
		public QuickEat()
		{
			var config = new QuickEatConfig(this);
			IsEnabled = config.ApplyIsEnabled();
			EquipmentSlotIndex = config.ApplyEquipmentSlotIndex();

			SetupKeyBindings();
		}

		public override bool CanExecute()
		{
			return base.CanExecute() && Entry.RewiredPlayer != null && Manager.main.player != null && !(Manager.input?.textInputIsActive ?? false);
		}

		public override void Execute()
		{
			if (!CanExecute())
			{
				return;
			}

			var player = Manager.main.player;

			if (TryFindEatable(player))
			{
				ConsumeEatable(player);
			}
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

			if (Entry.RewiredPlayer.GetButtonUp(KeyBindName))
			{
				SwapBackToPreviousSlot();
			}
		}

		/// <summary>
		///     Attempts to find an eatable item in the player's inventory and consume it.
		/// </summary>
		/// <param name="player">The player controller to access inventory.</param>
		/// <returns>
		///     The index of the slot where an eatable item was found;
		///     otherwise, -1 if no eatable item was found.
		/// </returns>
		private bool TryFindEatable(PlayerController player)
		{
			_previousSlotIndex = player.equippedSlotIndex;
			_fromSlotIndex = -1;

			// Check if there's an eatable item in the predefined slot.
			if (IsEatable(player.playerInventoryHandler.GetObjectData(EquipmentSlotIndex)))
			{
				_fromSlotIndex = EquipmentSlotIndex;

				return true;
			}


			// If there's no eatable item in the slot, look through the inventory and find the first eatable item that's not a potion, prioritizing cooked items first.
			var eatableItems = new Dictionary<int, ObjectDataCD>();
			for (var playerInventoryIndex = 0; playerInventoryIndex < player.playerInventoryHandler.size; playerInventoryIndex++)
			{
				var objectData = player.playerInventoryHandler.GetObjectData(playerInventoryIndex);
				if (!IsEatable(objectData))
				{
					continue;
				}

				eatableItems.Add(playerInventoryIndex, objectData);
			}

			if (eatableItems.Count == 0)
			{
				return false;
			}

			var eatableToConsume = eatableItems.OrderByDescending(item => PugDatabase.HasComponent<CookedFoodCD>(item.Value) ? 0 : 1).First();
			_fromSlotIndex = eatableToConsume.Key;

			return true;
		}

		/// <summary>
		///     Checks if the provided object data corresponds to an eatable item.
		/// </summary>
		/// <param name="objectData">The object data to check.</param>
		/// <returns>
		///     <see langword="true" /> if the object is eatable;
		///     otherwise, <see langword="false" />.
		/// </returns>
		private static bool IsEatable(ObjectDataCD objectData)
		{
			// Ignore bad items and potions. Some items like milk are eatable, but don't improve the 'food' bar.
			if (objectData.objectID == ObjectID.None || PugDatabase.HasComponent<PotionCD>(objectData))
			{
				return false;
			}

			var objectInfo = PugDatabase.GetObjectInfo(objectData.objectID, objectData.variation);

			return objectInfo is { objectType: ObjectType.Eatable };
		}

		/// <summary>
		///     Consumes the Eatable item from the specified slot.
		///     This method equips the slot containing the eatable item, resets the input trigger, and re-equips the slot to ensure
		///     proper consumption.
		/// </summary>
		/// <param name="player">The player controller to access inventory and use items.</param>
		private void ConsumeEatable(PlayerController player)
		{
			// Swap the item to the correct slot and equip it.
			if (_fromSlotIndex != EquipmentSlotIndex)
			{
				player.playerInventoryHandler.Swap(player, _fromSlotIndex, player.playerInventoryHandler, EquipmentSlotIndex);
			}

			player.EquipSlot(EquipmentSlotIndex);

			// Reset input history and re-equip the item.
			var inputHistoryFake = EntityUtility.GetComponentData<ClientInputHistoryCD>(player.entity, player.world);
			inputHistoryFake.secondInteractUITriggered = false;
			EntityUtility.SetComponentData(player.entity, player.world, inputHistoryFake);

			player.EquipSlot(EquipmentSlotIndex);

			// Simulate "right-click" or "use" action on the item.
			var inputHistoryConsume = EntityUtility.GetComponentData<ClientInputHistoryCD>(player.entity, player.world);
			inputHistoryConsume.secondInteractUITriggered = true;

			// Swap back to the original item.
			if (_fromSlotIndex != EquipmentSlotIndex)
			{
				player.playerInventoryHandler.Swap(player, _fromSlotIndex, player.playerInventoryHandler, EquipmentSlotIndex);
			}

			// Setting it here makes it somehow "smoother"...?
			EntityUtility.SetComponentData(player.entity, player.world, inputHistoryConsume);
		}

		/// <summary>
		///     Swaps back to the previously equipped slot after consuming the eatable item.
		/// </summary>
		private void SwapBackToPreviousSlot()
		{
			if (_previousSlotIndex == -1)
			{
				return;
			}

			Manager.main.player.EquipSlot(_previousSlotIndex);
		}

		#region IFeature

		/// <inheritdoc />
		public override string Name => nameof(QuickEat);

		/// <inheritdoc />
		public override string DisplayName => "Quick Eat";

		/// <inheritdoc />
		public override string Description => "Quickly equips or switches the preferred eatable item, consumes it, and swaps back to the previous item.";

		/// <inheritdoc />
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature

		#region Configurations

		internal int EquipmentSlotIndex { get; }

		/// <inheritdoc />
		public string KeyBindName => $"{ModSettings.ShortName}_{Name}";

		public void SetupKeyBindings()
		{
			RewiredExtensionModule.AddKeybind(KeyBindName, DisplayName, KeyboardKeyCode.F);
		}

		#endregion Configurations
	}
}