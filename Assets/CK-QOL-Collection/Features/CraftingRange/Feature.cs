using System.Collections.Generic;
using System.Linq;
using CK_QOL_Collection.Core;
using CK_QOL_Collection.Core.Helpers;

namespace CK_QOL_Collection.Features.CraftingRange
{
    /// <summary>
    /// Represents the Crafting Range feature of the mod.
    /// This feature allows players to interact with chests within a specified range.
    /// </summary>
    internal class Feature : FeatureBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Feature"/> class.
        /// Sets up the Crafting Range feature using the configuration settings.
        /// </summary>
        public Feature()
            : base(Configuration.Sections.CraftingRange.Name, Configuration.Sections.CraftingRange.IsEnabled)
        {
        }

        /// <summary>
        /// Gets the list of nearby chests that are within the crafting range.
        /// </summary>
        internal List<Chest> NearbyChests { get; } = new();

        /// <summary>
        /// Searches for nearby chests that are within the configured crafting range.
        /// </summary>
        /// <remarks>
        /// The search is limited to a maximum number of chests, as determined by the configuration settings.
        /// This limit is currently set to 8 due to restrictions in the game logic.
        /// </remarks>
        public override void Execute()
        {
            if (!CanExecute())
            {
                return;
            }

            NearbyChests.Clear();

            var maxDistance = Configuration.Sections.CraftingRange.Options.Distance.Value;
            var chestLimit = Configuration.Sections.CraftingRange.Options.ChestLimit.Value;

            // Find nearby chests within the specified range and limit. Currently limited to 8 chests due to game logic constraints.
            // Thanks to soupofcan (Rats in Bikini) from CK Discord for finding out: Pug.ECS.Components.Inventory.CraftActionData
            var nearbyChests = ChestHelper.GetNearbyChests(maxDistance)
                .Take(chestLimit)
                .ToList();

            NearbyChests.AddRange(nearbyChests);
        }
    }
}