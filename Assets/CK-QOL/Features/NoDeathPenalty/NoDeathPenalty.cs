using CK_QOL.Core.Config;
using CK_QOL.Core.Features;

namespace CK_QOL.Features.NoDeathPenalty
{
    internal sealed class NoDeathPenalty : FeatureBase<NoDeathPenalty>
    {
        public NoDeathPenalty()
        {
            ConfigBase.Create(this);
        }

        #region Configuration

        public override bool IsEnabled => NoDeathPenaltyConfig.ApplyIsEnabled(this);

        #endregion Configuration

        #region IFeature

        public override string Name => nameof(NoDeathPenalty);
        public override string DisplayName => "No Death Penalty";
        public override string Description => "Removes the behaviour, that the inventory gets lost after death.";
        public override FeatureType FeatureType => FeatureType.Server;

        #endregion IFeature

    }
}