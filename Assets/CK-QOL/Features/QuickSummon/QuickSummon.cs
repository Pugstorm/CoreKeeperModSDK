using CK_QOL.Core;
using CK_QOL.Core.Features;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.QuickSummon
{
	/// <summary>
	///     Provides the "Quick Summon" feature, allowing players to quickly equip a configured summoning tome,
	///     use a summon spell, and swap back to the previously equipped item.
	///     This feature allows players to bind a specific key to execute a summoning action seamlessly.
	///     The following tomes are supported:
	///     <list type="bullet">
	///         <item>
	///             <description>
	///                 <see cref="ObjectID.TomeOfRange" />
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 <see cref="ObjectID.TomeOfOrbit" />
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 <see cref="ObjectID.TomeOfMelee" />
	///             </description>
	///         </item>
	///     </list>
	/// </summary>
	/// <remarks>
	///     The "Quick Summon" feature allows players to rapidly switch to a summoning tome, cast a spell, and revert back to
	///     their previously equipped item. This class inherits from <see cref="QuickActionFeatureBase{TFeature}" /> to provide
	///     common functionality for equipping and using items.
	/// </remarks>
	internal sealed class QuickSummon : FeatureBase<QuickSummon>
	{
		private int _fromSlotIndex = -1;
		private int _previousSlotIndex = -1;
		private ObjectID _tomeID = ObjectID.None;

		/// <summary>
		///     Initializes a new instance of the <see cref="QuickSummon" /> class, applying configuration settings and binding
		///     the key for the summon action.
		/// </summary>
		public QuickSummon()
		{
			var config = new QuickSummonConfig(this);
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

			if (Entry.RewiredPlayer.GetButtonDown(KeyBindNameX))
			{
				_tomeID = ObjectID.TomeOfRange;
				Execute();
			}

			if (Entry.RewiredPlayer.GetButtonDown(KeyBindNameK))
			{
				_tomeID = ObjectID.TomeOfOrbit;
				Execute();
			}

			if (Entry.RewiredPlayer.GetButtonDown(KeyBindNameL))
			{
				_tomeID = ObjectID.TomeOfMelee;
				Execute();
			}

			if (Entry.RewiredPlayer.GetButtonUp(KeyBindNameX) || Entry.RewiredPlayer.GetButtonUp(KeyBindNameK) || Entry.RewiredPlayer.GetButtonUp(KeyBindNameL))
			{
				_tomeID = ObjectID.None;
				SwapBackToPreviousSlot();
			}
		}

		public override void Execute()
		{
			var player = Manager.main.player;

			if (TryFindSummonTome(player))
			{
				CastSummonSpell(player);
			}
		}

		/// <summary>
		///     Attempts to find a summoning tome in the player's inventory.
		/// </summary>
		private bool TryFindSummonTome(PlayerController player)
		{
			_previousSlotIndex = player.equippedSlotIndex;
			_fromSlotIndex = -1;
			// Check if the summoning tome is in the predefined slot.
			if (IsSummonTome(player.playerInventoryHandler.GetObjectData(EquipmentSlotIndex), _tomeID))
			{
				_fromSlotIndex = EquipmentSlotIndex;

				return true;
			}

			// If the tome is not in the predefined slot, search through the inventory.
			var playerInventorySize = player.playerInventoryHandler.size;
			for (var playerInventoryIndex = 0; playerInventoryIndex < playerInventorySize; playerInventoryIndex++)
			{
				if (IsSummonTome(player.playerInventoryHandler.GetObjectData(playerInventoryIndex), _tomeID))
				{
					_fromSlotIndex = playerInventoryIndex;

					return true;
				}
			}

			return false;
		}

		/// <summary>
		///     Checks if the provided object data corresponds to the currently configured summoning tome.
		/// </summary>
		private bool IsSummonTome(ObjectDataCD objectData, ObjectID tomeID)
		{
			return objectData.objectID == tomeID;
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
			var inputHistory = EntityUtility.GetComponentData<ClientInputHistoryCD>(player.entity, player.world);
			inputHistory.secondInteractUITriggered = true;
			EntityUtility.SetComponentData(player.entity, player.world, inputHistory);

			// Simulate "right-click" or "use" action on the item.
			// Swap back to the original item.
			if (_fromSlotIndex != EquipmentSlotIndex && player.playerInventoryHandler.GetObjectData(_fromSlotIndex).objectID != ObjectID.None)
			{
				player.playerInventoryHandler.Swap(player, _fromSlotIndex, player.playerInventoryHandler, EquipmentSlotIndex);
			}

			// Setting it here makes it somehow "smoother"...?
			EntityUtility.SetComponentData(player.entity, player.world, inputHistory);
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

		/// <inheritdoc />
		public override string Name => nameof(QuickSummon);

		/// <inheritdoc />
		public override string DisplayName => "Quick Summon";

		/// <inheritdoc />
		public override string Description => "Quickly equips or switches the preferred summoning tome, casts a summon spell, and swaps back to the previous item.";

		/// <inheritdoc />
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature

		#region Configurations

		internal string KeyBindNameX => $"{ModSettings.ShortName}_{Name}-TomeOfTheDark";
		internal string KeyBindNameK => $"{ModSettings.ShortName}_{Name}-TomeOfTheDeep";
		internal string KeyBindNameL => $"{ModSettings.ShortName}_{Name}-TomeOfTheDead";
		internal int EquipmentSlotIndex { get; }

		public void SetupKeyBindings()
		{
			RewiredExtensionModule.AddKeybind(KeyBindNameX, "Quick Summon Tome of the Dark", KeyboardKeyCode.J);
			RewiredExtensionModule.AddKeybind(KeyBindNameK, "Quick Summon Tome of the Deep", KeyboardKeyCode.K);
			RewiredExtensionModule.AddKeybind(KeyBindNameL, "Quick Summon Tome of the Dead", KeyboardKeyCode.L);
		}

		#endregion Configurations
	}
}