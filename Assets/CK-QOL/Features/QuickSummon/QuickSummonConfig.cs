using CK_QOL.Core.Config;
using CoreLib.Data.Configuration;

namespace CK_QOL.Features.QuickSummon
{
	/// <summary>
	///     Configuration class for the <see cref="QuickSummon" /> feature, handling key binding, enabled state, and equipment slot.
	///     This class uses <see cref="ConfigBase{TFeature}" /> to manage the configuration settings for QuickSummon.
	/// </summary>
	internal sealed class QuickSummonConfig : ConfigBase<QuickSummon>
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="QuickSummonConfig" /> class for the given feature.
		/// </summary>
		/// <param name="feature">
		///     The <see cref="QuickSummon" /> feature being configured.
		/// </param>
		public QuickSummonConfig(QuickSummon feature) : base(feature)
		{
		}

		/// <summary>
		///     Overrides the default enabled value for <see cref="QuickSummon" />.
		/// </summary>
		protected override bool DefaultIsEnabled => true;

		/// <summary>
		///     Applies the equipment slot index setting for QuickSummon.
		/// </summary>
		/// <returns>The index of the equipment slot.</returns>
		public int ApplyEquipmentSlotIndex()
		{
			var acceptableValues = new AcceptableValueRange<int>(0, 9);
			var description = new ConfigDescription("The equipment slot index for summoning tomes.", acceptableValues);
			var definition = new ConfigDefinition(Feature.Name, nameof(Feature.EquipmentSlotIndex));

			var entry = Config.Bind(definition, 0, description);

			return entry.Value;
		}
	}
}