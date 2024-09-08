using CK_QOL_Collection.Core.Configuration;
using CK_QOL_Collection.Core.Feature;
using CK_QOL_Collection.Core.Helpers;
using CK_QOL_Collection.Features.QuickStash.KeyBinds;
using Rewired;

namespace CK_QOL_Collection.Features.QuickStash
{
    /// <summary>
    ///     Represents the 'Quick Stash' feature of the mod.
    ///     This feature allows players to quickly stash items into nearby chests.
    /// </summary>
    internal class QuickStashFeature : FeatureBase
    {
        private readonly Player _rewiredPlayer;
        private readonly string _keyBindName;

        /// <summary>
        ///     Gets the configuration settings for the 'Quick Stash' feature.
        /// </summary>
        public QuickStashConfiguration Config { get; }
        
        /// <summary>
        ///     Initializes a new instance of the <see cref="QuickStashFeature" /> class.
        ///     Sets up input handling for the 'Quick Stash' feature.
        /// </summary>
        public QuickStashFeature()
            : base(nameof(QuickStash))
        {
            Config = (QuickStashConfiguration)Configuration;
            _rewiredPlayer = Entry.RewiredPlayer;
            _keyBindName = KeyBindManager.Instance.GetKeyBind<QuickStashKeyBind>()?.KeyBindName ?? string.Empty;
        }

        /// <inheritdoc />
        public override bool CanExecute() =>
            base.CanExecute()
            && _rewiredPlayer != null
            && Manager.main.currentSceneHandler.isInGame
            && Manager.main.player?.playerInventoryHandler != null;

        /// <inheritdoc />
        public override void Execute()
        {
            if (!CanExecute())
            {
                return;
            }

            var player = Manager.main.player;
            var maxDistance = Config.Distance;
            var nearbyChests = ChestHelper.GetNearbyChests(maxDistance);

            var stashedIntoChestsCount = 0;
            foreach (var chest in nearbyChests)
            {
                var inventoryHandler = chest.inventoryHandler;
                if (inventoryHandler == null)
                {
                    continue;
                }

                // Perform the quick stash action.
                player.playerInventoryHandler.QuickStack(player, inventoryHandler);
                stashedIntoChestsCount++;
            }

            //TODO: Get a list of stashed items to display.
            TextHelper.DisplayText(stashedIntoChestsCount == 0 
                ? "Nothing stashed!" 
                : $"Stashed into {stashedIntoChestsCount} chests.", rarity: Rarity.Rare);
        }

        /// <inheritdoc />
        public override void Update()
        {
            if (!CanExecute())
            {
                return;
            }

            if (_rewiredPlayer.GetButtonDown(_keyBindName))
            {
                Execute();
            }
        }
    }
}