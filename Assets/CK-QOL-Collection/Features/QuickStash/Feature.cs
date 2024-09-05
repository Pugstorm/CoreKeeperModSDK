using System.Linq;
using CK_QOL_Collection.Core;
using CK_QOL_Collection.Core.Helpers;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL_Collection.Features.QuickStash
{
    /// <summary>
    /// Represents the Quick Stash feature of the mod.
    /// This feature allows players to quickly stash items into nearby chests.
    /// </summary>
    internal class Feature : FeatureBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Feature"/> class.
        /// Sets up input handling for the Quick Stash feature.
        /// </summary>
        public Feature()
            : base(Configuration.Sections.QuickStash.Name, Configuration.Sections.QuickStash.IsEnabled)
        {
            _rewiredPlayer = Entry.RewiredPlayer;
        }

        /// <summary>
        /// The Rewired player object used for detecting input.
        /// </summary>
        private Player _rewiredPlayer;

        /// <summary>
        /// Executes the Quick Stash feature, transferring items from the player's inventory to nearby chests.
        /// </summary>
        public override void Execute()
        {
            if (!CanExecute())
            {
                return;
            }
            
            var player = Manager.main.player;
            if (player?.playerInventoryHandler == null)
            {
                return;
            }
            
            var maxDistance = Configuration.Sections.QuickStash.Options.Distance.Value;
            var nearbyChests = ChestHelper.GetNearbyChests(maxDistance);
            
            // Iterate through the nearby chests and attempt to quick stash items.
            foreach (var chest in nearbyChests)
            {
                Logger.Info(chest.name);
                var inventoryHandler = chest.inventoryHandler;
                if (inventoryHandler == null)
                {
                    continue;
                }
                Logger.Info(inventoryHandler.entityMonoBehaviour.name);
                player.playerInventoryHandler.QuickStack(player, inventoryHandler);
            }
        }

        /// <summary>
        /// Updates the state of the Quick Stash feature, checking for input and triggering the feature if appropriate.
        /// </summary>
        public override void Update()
        {
            if (_rewiredPlayer == null || !Manager.main.currentSceneHandler.isInGame)
            {
                return;
            }
            
            // Check if the Quick Stash key binding has been pressed.
            if (_rewiredPlayer.GetButtonDown(Configuration.Sections.QuickStash.KeyBinds.QuickStashKeyBindName))
            {
                Execute();
            }
        }
    }
}