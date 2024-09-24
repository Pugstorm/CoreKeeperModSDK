using CK_QOL.Core.Config;
using CK_QOL.Core.Features;

namespace CK_QOL.Features.ItemPickUpNotifier
{
    internal sealed class ItemPickUpNotifier : FeatureBase<ItemPickUpNotifier>
    {
        public ItemPickUpNotifier()
        {
            ConfigBase.Create(this);
        }

        #region IFeature

        public override string Name => nameof(ItemPickUpNotifier);
        public override string DisplayName => "Item Pick-Up Notifier";
        public override string Description => "Notifies when picking up items from the ground.";
        public override FeatureType FeatureType => FeatureType.Client;

        #endregion IFeature

        #region Configurations

        public override bool IsEnabled => ItemPickUpNotifierConfig.ApplyIsEnabled(this);

        internal float AggregateDelay => ItemPickUpNotifierConfig.ApplyAggregateDelay(this);

        #endregion Configurations

    }
}