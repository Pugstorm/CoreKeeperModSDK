using CK_QOL.Core.Config;

namespace CK_QOL.Features.ShiftClick
{
	/// <summary>
	///     Configuration class for the <see cref="ShiftClick" /> feature, handling the key binding and enabled state.
	///     This class uses <see cref="ConfigBase{TFeature}" /> to manage the configuration settings for ShiftClick.
	/// </summary>
	internal sealed class ShiftClickConfig : ConfigBase<ShiftClick>
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="ShiftClickConfig" /> class for the given feature.
		/// </summary>
		/// <param name="feature">The <see cref="ShiftClick" /> feature being configured.</param>
		public ShiftClickConfig(ShiftClick feature) : base(feature)
		{
		}

		/// <summary>
		///     Overrides the default enabled value for <see cref="ShiftClick" />.
		/// </summary>
		protected override bool DefaultIsEnabled => true;
	}
}