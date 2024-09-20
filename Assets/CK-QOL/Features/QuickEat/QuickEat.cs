using System.Collections.Generic;
using System.Linq;
using CK_QOL.Core;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.QuickEat
{
	/// <summary>
	///     Represents the "Eatable Binding" feature in the game, which allows players to quickly heal using a configured or
	///     next available Eatable item from their inventory.
	///     This feature provides a key binding to trigger healing actions and automatically manages the inventory to locate
	///     and consume the Eatable item efficiently.
	///     The class manages the following functionalities:
	///     <list type="bullet">
	///         <item>
	///             <description>
	///                 Configuration of the feature's enabled state and Eatable slot index,
	///                 which determines the preferred slot to search for a Eatable item (<see cref="EquipmentSlotIndex" />).
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 Defines key bindings for quick healing, allowing users to set a custom key  to trigger the
	///                 Eatable action (<see cref="ApplyKeyBinds" /> method).
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 Executes the logic to find, equip, and consume a Eatable item from the inventory,
	///                 ensuring efficient healing actions during gameplay (<see cref="Execute" /> and
	///                 <see cref="ConsumeEatable" /> methods).
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 Monitors for key input and manages the healing process, including resetting the inventory
	///                 state after healing (<see cref="Update" /> method).
	///             </description>
	///         </item>
	///     </list>
	/// </summary>
	/// <remarks>
	///     This class extends the <see cref="FeatureBase{TFeature}" /> base class to inherit common feature behavior,
	///     including singleton instantiation, configuration management, and execution control.
	///     It provides an optimized mechanism for healing by leveraging game input handling and inventory management
	///     functionalities.
	/// </remarks>
	internal sealed class QuickEat : FeatureBase<QuickEat>
	{
		private int _fromSlotIndex = -1;
		private int _previousSlotIndex = -1;

		public QuickEat()
		{
			ApplyConfigurations();
			ApplyKeyBinds();
		}

		public override bool CanExecute()
		{
			return base.CanExecute()
			       && Entry.RewiredPlayer != null
			       && Manager.main.player != null
			       && !(Manager.input?.textInputIsActive ?? false);
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

			var playerInventorySize = player.playerInventoryHandler.size;
			for (var playerInventoryIndex = 0; playerInventoryIndex < playerInventorySize; playerInventoryIndex++)
			{
				if (!IsEatable(player.playerInventoryHandler.GetObjectData(playerInventoryIndex)))
				{
					continue;
				}

				eatableItems.Add(playerInventoryIndex, player.playerInventoryHandler.GetObjectData(playerInventoryIndex));
			}

			if (eatableItems.Count == 0)
			{
				return false;
			}

			var firstEatableItem = eatableItems.FirstOrDefault();
			_fromSlotIndex = firstEatableItem.Key;

			var firstCookedFood = eatableItems.OrderByDescending(i => PugDatabase.HasComponent<CookedFoodCD>(i.Value)).FirstOrDefault();
			if (firstCookedFood.Key == default || firstCookedFood.Value.Equals(default))
			{
				return true;
			}

			_fromSlotIndex = firstCookedFood.Key;

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

		public override string Name => nameof(QuickEat);
		public override string DisplayName => "Quick Eat";
		public override string Description => "Adds a binding to quickly eat with the configured, or next available eatable item.";
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature

		#region Configurations

		internal string KeyBindName => $"{ModSettings.ShortName}_{Name}";
		internal int EquipmentSlotIndex { get; private set; }

		private void ApplyConfigurations()
		{
			ConfigBase.Create(this);
			IsEnabled = QuickEatConfig.ApplyIsEnabled(this);
			EquipmentSlotIndex = QuickEatConfig.ApplyEquipmentSlotIndex(this);
		}

		private void ApplyKeyBinds()
		{
			RewiredExtensionModule.AddKeybind(KeyBindName, DisplayName, KeyboardKeyCode.F);
		}

		#endregion Configurations
	}
}