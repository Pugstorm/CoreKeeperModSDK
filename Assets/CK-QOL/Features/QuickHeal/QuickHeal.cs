using CK_QOL.Core;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.QuickHeal
{
	/// <summary>
	/// 	Represents the "Healable Binding" feature in the game, which allows players to quickly heal using a configured or next available healable item from their inventory.
	///		This feature provides a key binding to trigger healing actions and automatically manages the inventory to locate and consume the healable item efficiently.
	/// 	
	/// 	The class manages the following functionalities:
	/// 	<list type="bullet">
	/// 	    <item>
	/// 	        <description>Configuration of the feature's enabled state and healable slot index,
	///				which determines the preferred slot to search for a healable item (<see cref="EquipmentSlotIndex"/>).</description>
	/// 	    </item>
	/// 	    <item>
	/// 	        <description>Defines key bindings for quick healing, allowing users to set a custom key  to trigger the healable action (<see cref="ApplyKeyBinds"/> method).</description>
	/// 	    </item>
	/// 	    <item>
	/// 	        <description>Executes the logic to find, equip, and consume a healable item from the inventory, 
	/// 	        ensuring efficient healing actions during gameplay (<see cref="Execute"/> and <see cref="ConsumeHealable"/> methods).</description>
	/// 	    </item>
	/// 	    <item>
	/// 	        <description>Monitors for key input and manages the healing process, including resetting the inventory state after healing (<see cref="Update"/> method).</description>
	/// 	    </item>
	/// 	</list>
	/// </summary>
	/// <remarks>
	/// 	This class extends the <see cref="FeatureBase{TFeature}"/> base class to inherit common feature behavior, 
	/// 	including singleton instantiation, configuration management, and execution control.
	///		It provides an optimized mechanism for healing by leveraging game input handling and inventory management functionalities.
	/// </remarks>
	internal sealed class QuickHeal : FeatureBase<QuickHeal>
	{
		#region IFeature

		public override string Name => nameof(QuickHeal);
		public override string DisplayName => "Quick Heal";
		public override string Description => "Adds a binding to quickly heal with the configured, or next available healable item.";
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature

		#region Configurations

		internal string KeyBindName => $"{ModSettings.ShortName}_{Name}";
		internal int EquipmentSlotIndex { get; private set; }

		private void ApplyConfigurations()
		{
			ConfigBase.Create(this);
			IsEnabled = QuickHealConfig.ApplyIsEnabled(this);
			EquipmentSlotIndex = QuickHealConfig.ApplyEquipmentSlotIndex(this);
		}

		private void ApplyKeyBinds()
		{
			RewiredExtensionModule.AddKeybind(KeyBindName, "Quick Heal", KeyboardKeyCode.G);
		}

		#endregion Configurations

		private int _previousSlotIndex = -1;

		public QuickHeal()
		{
			ApplyConfigurations();
			ApplyKeyBinds();
		}

		public override bool CanExecute() =>
			base.CanExecute()
			&& Entry.RewiredPlayer != null
			&& Manager.main.player != null
			&& !(Manager.input?.textInputIsActive ?? false);

		public override void Execute()
		{
			if (!CanExecute())
			{
				return;
			}

			var player = Manager.main.player;

			var foundValidHealable = TryFindHealable(player);
			if (foundValidHealable)
			{
				ConsumeHealable(player);
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
		///     Attempts to find a healable item in the player's inventory and consume it.
		/// </summary>
		/// <param name="player">The player controller to access inventory.</param>
		/// <returns>
		///		The index of the slot where a healable item was found;
		///		otherwise, -1 if no healable item was found.
		/// </returns>
		private bool TryFindHealable(PlayerController player)
		{
			_previousSlotIndex = player.equippedSlotIndex;

			// Check if there's a healable item in the predefined slot.
			if (IsHealable(player.playerInventoryHandler.GetObjectData(EquipmentSlotIndex)))
			{
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

				// Swap the item to the healable slot.
				player.playerInventoryHandler.Swap(player, playerInventoryIndex, player.playerInventoryHandler, EquipmentSlotIndex);

				return true;
			}

			return false;
		}

		/// <summary>
		///     Checks if the provided object data corresponds to a healable item.
		/// </summary>
		/// <param name="objectData">The object data to check.</param>
		/// <returns>
		///		<see langword="true" /> if the object is Healable;
		///		otherwise, <see langword="false" />.
		/// </returns>
		private static bool IsHealable(ObjectDataCD objectData)
		{
			return objectData.objectID is ObjectID.HealingPotion or ObjectID.GreaterHealingPotion;
		}

		/// <summary>
		///     Consumes the healable item from the specified slot.
		///     This method equips the slot containing the healable item, resets the input trigger, and re-equips the slot to ensure proper consumption.
		/// </summary>
		/// <param name="player">The player controller to access inventory and use items.</param>
		private void ConsumeHealable(PlayerController player)
		{
			player.EquipSlot(EquipmentSlotIndex);

			// Get the input history component and reset the secondInteractUITriggered flag to 'false'.
			// This is likely needed to ensure that the game recognizes a fresh input action on the next trigger.
			var inputHistoryFake = EntityUtility.GetComponentData<ClientInputHistoryCD>(player.entity, player.world);
			inputHistoryFake.secondInteractUITriggered = false;
			EntityUtility.SetComponentData(player.entity, player.world, inputHistoryFake);

			// Re-equip the slot to reinitialize the item state, ensuring that any side effects of the input reset are neutralized.
			player.EquipSlot(EquipmentSlotIndex);

			// Set the secondInteractUITriggered flag to 'true' to simulate the "right-click" or "use" action on the item.
			var inputHistoryConsume = EntityUtility.GetComponentData<ClientInputHistoryCD>(player.entity, player.world);
			inputHistoryConsume.secondInteractUITriggered = true;
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
	}
}