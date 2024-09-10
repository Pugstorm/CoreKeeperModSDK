using System.Linq;
using CK_QOL_Collection.Core.Feature;
using CK_QOL_Collection.Core.Feature.Configuration;
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
            var chestLimit = Config.ChestLimit;

            var nearbyChests = ChestHelper.GetNearbyChests(maxDistance)
                .Take(chestLimit)
                .ToList();

            var stashedIntoChestsCount = 0;
            foreach (var inventoryHandler in nearbyChests.Select(chest => chest.inventoryHandler).Where(inventoryHandler => inventoryHandler != null))
            {
                player.playerInventoryHandler.QuickStack(player, inventoryHandler);
                stashedIntoChestsCount++;
            }

            TextHelper.DisplayText(stashedIntoChestsCount == 0 
                ? "Quick Stash: No chests found!" 
                : $"Quick Stash: {stashedIntoChestsCount} chests.", Rarity.Legendary);
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