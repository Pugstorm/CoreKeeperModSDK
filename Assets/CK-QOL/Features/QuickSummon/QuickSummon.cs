using CK_QOL.Core;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.QuickSummon
{
    /// <summary>
    ///     Represents the "Quick Summon" feature, allowing players to quickly equip the "Tome of the Dark" 
    ///     use a summon spell, and swap back to the previously equipped item.
    ///     At Some point, will work out how to handle the other 2 summoning books.
    /// </summary>
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
        ///     Attempts to find the "Tome of the Dark" in the player's inventory.
        /// </summary>
        /// <param name="player">The player controller to access inventory.</param>
        /// <returns>
        ///     <see langword="true" /> if the tome is found; otherwise, <see langword="false" />.
        /// </returns>
        private bool TryFindSummonTome(PlayerController player)
        {
            _previousSlotIndex = player.equippedSlotIndex;
            _fromSlotIndex = -1;

            // Check if the Tome of the Dark is in the predefined slot.
            if (IsSummonTome(player.playerInventoryHandler.GetObjectData(EquipmentSlotIndex)))
            {
                return true;
            }

            // Look through the inventory if not in the predefined slot.
            var playerInventorySize = player.playerInventoryHandler.size;
            for (var playerInventoryIndex = 0; playerInventoryIndex < playerInventorySize; playerInventoryIndex++)
            {
                if (!IsSummonTome(player.playerInventoryHandler.GetObjectData(playerInventoryIndex)))
                {
                    continue;
                }

                _fromSlotIndex = playerInventoryIndex; // Store the slot with the Tome of the Dark.
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Checks if the provided object data corresponds to the "Tome of the Dark".
        /// </summary>
        /// <param name="objectData">The object data to check.</param>
        /// <returns>
        ///     <see langword="true" /> if the object is the summon tome; otherwise, <see langword="false" />.
        /// </returns>
private static bool IsSummonTome(ObjectDataCD objectData)
{
    return objectData.objectID == ObjectID.TomeOfRange; // Using TomeOfRange as the correct ID
}


        /// <summary>
        ///     Equips the "Tome of the Dark", uses the summon spell, and swaps back to the previous item.
        /// </summary>
        /// <param name="player">The player controller.</param>
        private void CastSummonSpell(PlayerController player)
        {
            // Equip the Tome of the Dark to the designated slot.
            player.playerInventoryHandler.Swap(player, _fromSlotIndex, player.playerInventoryHandler, EquipmentSlotIndex);
            player.EquipSlot(EquipmentSlotIndex);

            // Trigger the summon spell action.
            var inputHistory = EntityUtility.GetComponentData<ClientInputHistoryCD>(player.entity, player.world);
            inputHistory.secondInteractUITriggered = false;
            EntityUtility.SetComponentData(player.entity, player.world, inputHistory);

            // Re-equip the slot to ensure proper spell activation.
            player.EquipSlot(EquipmentSlotIndex);
            inputHistory.secondInteractUITriggered = true;
            EntityUtility.SetComponentData(player.entity, player.world, inputHistory);

            // Swap back to the original item.
            if (_fromSlotIndex != -1 && player.playerInventoryHandler.GetObjectData(_fromSlotIndex).objectID != ObjectID.None)
            {
                player.playerInventoryHandler.Swap(player, _fromSlotIndex, player.playerInventoryHandler, EquipmentSlotIndex);
            }
        }

        /// <summary>
        ///     Swaps back to the previously equipped slot after using the summon spell.
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
        public override string Description => "Quickly equips 'Tome of the Dark', casts a summon spell, and swaps back to the previous item.";
        public override FeatureType FeatureType => FeatureType.Client;

        #endregion IFeature

        #region Configurations

        internal string KeyBindName => $"{ModSettings.ShortName}_{Name}";
        internal int EquipmentSlotIndex { get; private set; }

        private void ApplyConfigurations()
        {
            ConfigBase.Create(this);
            IsEnabled = QuickSummonConfig.ApplyIsEnabled(this);
            EquipmentSlotIndex = QuickSummonConfig.ApplyEquipmentSlotIndex(this);
        }

        private void ApplyKeyBinds()
        {
            RewiredExtensionModule.AddKeybind(KeyBindName, "Quick Summon", KeyboardKeyCode.X);
        }

        #endregion Configurations
    }
}
