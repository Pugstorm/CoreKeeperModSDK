using CK_QOL.Core;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.QuickSummon
{
	/// <summary>
	///     Represents the "Quick Summon" feature, allowing players to quickly equip a configured summoning tome,
	///     use a summon spell, and swap back to the previously equipped item.
	///     This feature provides a key binding to trigger summoning actions and automatically manages the inventory to locate
	///     and equip the tome efficiently.
	///     The class manages the following functionalities:
	///     <list type="bullet">
	///         <item>
	///             <description>
	///                 Configuration of the feature's enabled state and summoning tome slot index,
	///                 which determines the preferred slot to search for the tome (<see cref="EquipmentSlotIndex" />).
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 Defines key bindings for quick summoning, allowing users to set a custom key to trigger the
	///                 summon action (<see cref="ApplyKeyBinds" /> method).
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 Executes the logic to find, equip, and cast the summon spell using the configured tome,
	///                 ensuring efficient summoning actions during gameplay (<see cref="Execute" /> and
	///                 <see cref="CastSummonSpell" /> methods).
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 Monitors for key input and manages the summoning process, including resetting the inventory
	///                 state after casting the summon spell (<see cref="Update" /> method).
	///             </description>
	///         </item>
	///     </list>
	/// </summary>
	/// <remarks>
	///     This class extends the <see cref="FeatureBase{TFeature}" /> base class to inherit common feature behavior,
	///     including singleton instantiation, configuration management, and execution control.
	///     It provides an optimized mechanism for summoning by leveraging game input handling and inventory management
	///     functionalities.
	/// </remarks>
	internal sealed class QuickSummon : FeatureBase<QuickSummon>
	{
		private int _fromSlotIndex = -1;
		private int _previousSlotIndex = -1;

		public QuickSummon()
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

			if (TryFindSummonTome(player))
			{
				CastSummonSpell(player);
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
		///     Attempts to find a summoning tome in the player's inventory.
		/// </summary>
		/// <param name="player">The player controller to access inventory.</param>
		/// <returns>
		///     <see langword="true" /> if the tome is found; otherwise, <see langword="false" />.
		/// </returns>
		private bool TryFindSummonTome(PlayerController player)
		{
			_previousSlotIndex = player.equippedSlotIndex;
			_fromSlotIndex = -1;

			// Check if the summoning tome is in the predefined slot.
			if (IsSummonTome(player.playerInventoryHandler.GetObjectData(EquipmentSlotIndex)))
			{
				_fromSlotIndex = EquipmentSlotIndex;

				return true;
			}

			// If the tome is not in the predefined slot, search through the inventory.
			var playerInventorySize = player.playerInventoryHandler.size;
			for (var playerInventoryIndex = 0; playerInventoryIndex < playerInventorySize; playerInventoryIndex++)
			{
				if (!IsSummonTome(player.playerInventoryHandler.GetObjectData(playerInventoryIndex)))
				{
					continue;
				}

				_fromSlotIndex = playerInventoryIndex;

				return true;
			}

			return false;
		}

		/// <summary>
		///     Checks if the provided object data corresponds to the currently configured summoning tome.
		/// </summary>
		/// <param name="objectData">The object data to check.</param>
		/// <returns>
		///     <see langword="true" /> if the object is the summoning tome; otherwise, <see langword="false" />.
		/// </returns>
		private bool IsSummonTome(ObjectDataCD objectData)
		{
			return objectData.objectID == GetObjectIDForTomeType(TomeType);
		}

		/// <summary>
		///     Maps the <see cref="TomeType" /> to the corresponding <see cref="ObjectID" />.
		/// </summary>
		/// <param name="tomeType">The tome type to map.</param>
		/// <returns>The corresponding <see cref="ObjectID" />.</returns>
		private ObjectID GetObjectIDForTomeType(TomeType tomeType)
		{
			return tomeType switch
			{
				TomeType.TomeOfTheDark => ObjectID.TomeOfRange,
				TomeType.TomeOfTheDeep => ObjectID.TomeOfOrbit,
				TomeType.TomeOfTheDead => ObjectID.TomeOfMelee,
				_ => ObjectID.None
			};
		}

		/// <summary>
		///     Equips the summoning tome, casts the summon spell, and swaps back to the previous item.
		/// </summary>
		/// <param name="player">The player controller.</param>
		private void CastSummonSpell(PlayerController player)
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
		///     Swaps back to the previously equipped slot after casting the summon spell.
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

		public override string Name => nameof(QuickSummon);
		public override string DisplayName => "Quick Summon";
		public override string Description => "Quickly equips a summoning tome, casts a summon spell, and swaps back to the previous item.";
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature

		#region Configurations

		internal string KeyBindName => $"{ModSettings.ShortName}_{Name}";
		internal int EquipmentSlotIndex { get; private set; }
		internal TomeType TomeType { get; private set; }

		private void ApplyConfigurations()
		{
			ConfigBase.Create(this);
			IsEnabled = QuickSummonConfig.ApplyIsEnabled(this);
			EquipmentSlotIndex = QuickSummonConfig.ApplyEquipmentSlotIndex(this);
			TomeType = QuickSummonConfig.ApplyTomeType(this);
		}

		private void ApplyKeyBinds()
		{
			RewiredExtensionModule.AddKeybind(KeyBindName, DisplayName, KeyboardKeyCode.X);
		}

		#endregion Configurations
	}
}