using CK_QOL.Core.Config;

namespace CK_QOL.Features.ChestAutoUnlock
{
	/// <summary>
	///     Configuration class for the <see cref="ChestAutoUnlock" /> feature, handling the enabled state.
	///     This class uses <see cref="ConfigBase{TFeature}" /> to manage the configuration settings for ChestAutoUnlock.
	/// </summary>
	internal sealed class ChestAutoUnlockConfig : ConfigBase<ChestAutoUnlock>
	{
		public ChestAutoUnlockConfig(ChestAutoUnlock feature) : base(feature)
		{
		}

		/// <summary>
		///     Overrides the default enabled value for <see cref="ChestAutoUnlockConfig" />.
		/// </summary>
		protected override bool DefaultIsEnabled => true;
	}
}