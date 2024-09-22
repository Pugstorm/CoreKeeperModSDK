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
            // This method will be invoked in the Update method to check for key presses.
        }

        public override void Update()
        {
            if (!CanExecute())
            {
                return;
            }

            if (Entry.RewiredPlayer.GetButtonDown(KeyBindNameX))
            {
                ExecuteTome(0); // Tome of the Dark
            }

            if (Entry.RewiredPlayer.GetButtonDown(KeyBindNameK))
            {
                ExecuteTome(1); // Tome of the Deep
            }

            if (Entry.RewiredPlayer.GetButtonDown(KeyBindNameL))
            {
                ExecuteTome(2); // Tome of the Dead
            }

            if (Entry.RewiredPlayer.GetButtonUp(KeyBindNameX) || 
                Entry.RewiredPlayer.GetButtonUp(KeyBindNameK) || 
                Entry.RewiredPlayer.GetButtonUp(KeyBindNameL))
            {
                SwapBackToPreviousSlot();
            }
        }

        private void ExecuteTome(int tomeSlotIndex)
        {
            var player = Manager.main.player;

            if (TryFindSummonTome(player, tomeSlotIndex))
            {
                CastSummonSpell(player);
            }
        }
		/// <summary>
		///     Attempts to find a summoning tome in the player's inventory.

        private bool TryFindSummonTome(PlayerController player, int tomeSlotIndex)
        {
            _previousSlotIndex = player.equippedSlotIndex;
            _fromSlotIndex = -1;

            ObjectID tomeID = tomeSlotIndex switch
            {
                0 => ObjectID.TomeOfRange,
                1 => ObjectID.TomeOfOrbit,
                2 => ObjectID.TomeOfMelee,
                _ => ObjectID.None
            };
			// Check if the summoning tome is in the predefined slot.
            if (IsSummonTome(player.playerInventoryHandler.GetObjectData(EquipmentSlotIndex), tomeID))
            {
                _fromSlotIndex = EquipmentSlotIndex;
                return true;
            }

			// If the tome is not in the predefined slot, search through the inventory.
            var playerInventorySize = player.playerInventoryHandler.size;
            for (var playerInventoryIndex = 0; playerInventoryIndex < playerInventorySize; playerInventoryIndex++)
            {
                if (IsSummonTome(player.playerInventoryHandler.GetObjectData(playerInventoryIndex), tomeID))
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

        public override string Name => nameof(QuickSummon);
        public override string DisplayName => "Quick Summon";
        public override string Description => "Quickly equips a summoning tome, casts a summon spell, and swaps back to the previous item.";
        public override FeatureType FeatureType => FeatureType.Client;

        #endregion IFeature

        #region Configurations

        internal string KeyBindNameX => $"{ModSettings.ShortName}_{Name}_TomeOfTheDark";
        internal string KeyBindNameK => $"{ModSettings.ShortName}_{Name}_TomeOfTheDeep";
        internal string KeyBindNameL => $"{ModSettings.ShortName}_{Name}_TomeOfTheDead";
        internal int EquipmentSlotIndex { get; private set; }

        private void ApplyConfigurations()
        {
            ConfigBase.Create(this);
            IsEnabled = QuickSummonConfig.ApplyIsEnabled(this);
            EquipmentSlotIndex = QuickSummonConfig.ApplyEquipmentSlotIndex(this);
        }

        private void ApplyKeyBinds()
        {
            RewiredExtensionModule.AddKeybind(KeyBindNameX, "Quick Summon Tome of the Dark", KeyboardKeyCode.X);
            RewiredExtensionModule.AddKeybind(KeyBindNameK, "Quick Summon Tome of the Deep", KeyboardKeyCode.K);
            RewiredExtensionModule.AddKeybind(KeyBindNameL, "Quick Summon Tome of the Dead", KeyboardKeyCode.L);
        }

        #endregion Configurations
    }
}
