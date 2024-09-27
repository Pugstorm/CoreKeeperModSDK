using CK_QOL.Core.Features;

namespace CK_QOL.Features.NoDeathPenalty
{
	/// <summary>
	///     Provides the "No Death Penalty" feature, which removes the default behavior where the player's inventory is lost
	///     upon death. With this feature enabled, the player's inventory will remain intact after dying.
	/// </summary>
	internal sealed class NoDeathPenalty : FeatureBase<NoDeathPenalty>
	{
		public NoDeathPenalty()
		{
			var config = new NoDeathPenaltyConfig(this);
			IsEnabled = config.ApplyIsEnabled();
		}

		#region IFeature

		public override string Name => nameof(NoDeathPenalty);
		public override string DisplayName => "No Death Penalty";
		public override string Description => "Removes the behaviour, that the inventory gets lost after death.";
		public override FeatureType FeatureType => FeatureType.Server;

		#endregion IFeature

	}
}