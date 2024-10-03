using CK_QOL.Core;
using CK_QOL.Core.Features;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.QuickHeal
{
	/// <summary>
	///     Provides the "Quick Heal" feature, allowing players to quickly equip and use a healing item, such as a healing
	///     potion, and then automatically revert to the previously equipped item.
	///     The following items are supported:
	///     <list type="bullet">
	///         <item>
	///             <description>
	///                 <see cref="ObjectID.HealingPotion" />
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 <see cref="ObjectID.GreaterHealingPotion" />
	///             </description>
	///         </item>
	///     </list>
	/// </summary>
	/// <remarks>
	///     The "Quick Heal" feature allows players to heal rapidly during combat without manually searching through their
	///     inventory.
	///     This class inherits from <see cref="QuickActionFeatureBase{TFeature}" /> to provide common functionality for item
	///     management.
	/// </remarks>
	internal sealed class QuickHeal : FeatureBase<QuickHeal>, IKeyBindableFeature
	{
		private int _fromSlotIndex = -1;
		private int _previousSlotIndex = -1;

		/// <summary>
		///     Initializes a new instance of the <see cref="QuickHeal" /> class, applying configuration settings and binding
		///     the key for the healing action.
		/// </summary>
		public QuickHeal()
		{
			var config = new QuickHealConfig(this);
			IsEnabled = config.ApplyIsEnabled();
			EquipmentSlotIndex = config.ApplyEquipmentSlotIndex();

			SetupKeyBindings();
		}

		public override bool CanExecute()
		{
			return base.CanExecute() && Entry.RewiredPlayer != null && Manager.main.player != null && !(Manager.input?.textInputIsActive ?? false);
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

		public override void Execute()
		{
			if (!CanExecute())
			{
				return;
			}

			var player = Manager.main.player;

			if (TryFindHealable(player))
			{
				ConsumeHealable(player);
			}
		}

		/// <summary>
		///     Attempts to find a healable item in the player's inventory and consume it.
		/// </summary>
		/// <param name="player">The player controller to access inventory.</param>
		/// <returns>
		///     The index of the slot where a healable item was found;
		///     otherwise, -1 if no healable item was found.
		/// </returns>
		private bool TryFindHealable(PlayerController player)
		{
			_previousSlotIndex = player.equippedSlotIndex;
			_fromSlotIndex = -1;

			// Check if there's a healable item in the predefined slot.
			if (IsHealable(player.playerInventoryHandler.GetObjectData(EquipmentSlotIndex)))
			{
				_fromSlotIndex = EquipmentSlotIndex;

				return true;
			}

			// If there's no healable item in the slot, look through the inventory.
			var playerInventorySize = player.playerInventoryHandler.size;
			for (var playerInventoryIndex = 0; playerInventoryIndex < playerInventorySize; playerInventoryIndex++)
			{
				if (!IsHealable(player.playerInventoryHandler.GetObjectData(playerInventoryIndex)))
				{
					continue;
				}

				// Store the slot we're swapping from.
				_fromSlotIndex = playerInventoryIndex;

				return true;
			}

			return false;
		}

		/// <summary>
		///     Checks if the provided object data corresponds to a healable item.
		/// </summary>
		/// <param name="objectData">The object data to check.</param>
		/// <returns>
		///     <see langword="true" /> if the object is healable;
		///     otherwise, <see langword="false" />.
		/// </returns>
		private static bool IsHealable(ObjectDataCD objectData)
		{
			return objectData.objectID is ObjectID.HealingPotion or ObjectID.GreaterHealingPotion;
		}

		/// <summary>
		///     Consumes the healable item from the specified slot.
		///     This method equips the slot containing the healable item, resets the input trigger, and re-equips the slot to
		///     ensure proper consumption.
		/// </summary>
		/// <param name="player">The player controller to access inventory and use items.</param>
		private void ConsumeHealable(PlayerController player)
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
			if (_fromSlotIndex != EquipmentSlotIndex && player.playerInventoryHandler.GetObjectData(_fromSlotIndex).objectID != ObjectID.None)
			{
				player.playerInventoryHandler.Swap(player, _fromSlotIndex, player.playerInventoryHandler, EquipmentSlotIndex);
			}

			// Setting it here makes it somehow "smoother"...?
			EntityUtility.SetComponentData(player.entity, player.world, inputHistoryConsume);
		}

		/// <summary>
		///     Swaps back to the previously equipped slot after consuming the healable.
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
		public override string Name => nameof(QuickHeal);

		/// <inheritdoc />
		public override string DisplayName => "Quick Heal";

		/// <inheritdoc />
		public override string Description => "Quickly equips or switches the preferred healable item, consumes it, and swaps back to the previous item.";

		/// <inheritdoc />
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature

		#region Configurations

		internal int EquipmentSlotIndex { get; }

		/// <inheritdoc />
		public string KeyBindName => $"{ModSettings.ShortName}_{Name}";

		public void SetupKeyBindings()
		{
			RewiredExtensionModule.AddKeybind(KeyBindName, DisplayName, KeyboardKeyCode.G);
		}

		#endregion Configurations
	}
}