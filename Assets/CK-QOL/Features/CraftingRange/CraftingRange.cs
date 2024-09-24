using System.Collections.Generic;
using System.Linq;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CK_QOL.Core.Helpers;

namespace CK_QOL.Features.CraftingRange
{
    internal sealed class CraftingRange : FeatureBase<CraftingRange>
    {
        public CraftingRange()
        {
            ConfigBase.Create(this);
        }

        internal List<Chest> Chests { get; } = new();

        public override void Execute()
        {
            if (!CanExecute()) return;

            Chests.Clear();

            var nearbyChests = ChestHelper.GetNearbyChests(MaxRange)
                .Take(MaxChests)
                .ToList();

            Chests.AddRange(nearbyChests);
        }

        #region IFeature

        public override string Name => nameof(CraftingRange);
        public override string DisplayName => "Crafting Range";
        public override string Description => "Extends the crafting range for all kind of work benches.";
        public override FeatureType FeatureType => FeatureType.Client;

        #endregion IFeature

        #region Configuration

        public override bool IsEnabled => CraftingRangeConfig.ApplyIsEnabled(this);
        internal float MaxRange => CraftingRangeConfig.ApplyMaxRange(this);
        internal int MaxChests => CraftingRangeConfig.ApplyMaxChests(this);

        #endregion Configuration

    }
}