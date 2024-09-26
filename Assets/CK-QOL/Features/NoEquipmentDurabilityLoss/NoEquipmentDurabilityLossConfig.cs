using CK_QOL.Core.Config;

namespace CK_QOL.Features.NoEquipmentDurabilityLoss
{
	/// <summary>
	///     Configuration class for the <see cref="NoEquipmentDurabilityLoss" /> feature, handling the enabled state.
	///     This class uses <see cref="ConfigBase{TFeature}" /> to manage the configuration settings for NoEquipmentDurabilityLoss.
	/// </summary>
	internal sealed class NoEquipmentDurabilityLossConfig : ConfigBase<NoEquipmentDurabilityLoss>
	{
		public NoEquipmentDurabilityLossConfig(NoEquipmentDurabilityLoss feature) : base(feature)
		{
		}

		/// <summary>
		///     Overrides the default enabled value for <see cref="NoEquipmentDurabilityLoss" />.
		/// </summary>
		protected override bool DefaultIsEnabled => false;
	}
}