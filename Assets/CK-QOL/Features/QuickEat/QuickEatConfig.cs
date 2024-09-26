using CK_QOL.Core.Config;
using CoreLib.Data.Configuration;

namespace CK_QOL.Features.QuickEat
{
	/// <summary>
	///     Configuration class for the <see cref="QuickEat" /> feature, handling key binding, enabled state, and equipment slot.
	///     This class uses <see cref="ConfigBase{TFeature}" /> to manage the configuration settings for QuickEat.
	/// </summary>
	internal sealed class QuickEatConfig : ConfigBase<QuickEat>
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="QuickEatConfig" /> class for the given feature.
		/// </summary>
		/// <param name="feature">The <see cref="QuickEat" /> feature being configured.</param>
		public QuickEatConfig(QuickEat feature) : base(feature)
		{
		}

		/// <summary>
		///     Overrides the default enabled value for <see cref="QuickEat" />.
		/// </summary>
		protected override bool DefaultIsEnabled => true;

		/// <summary>
		///     Applies the equipment slot index setting for QuickEat.
		/// </summary>
		/// <returns>The index of the equipment slot.</returns>
		public int ApplyEquipmentSlotIndex()
		{
			var acceptableValues = new AcceptableValueRange<int>(0, 9);
			var description = new ConfigDescription("The equipment slot index for eatable items.", acceptableValues);
			var definition = new ConfigDefinition(Feature.Name, nameof(Feature.EquipmentSlotIndex));

			var entry = Config.Bind(definition, 8, description);

			return entry.Value;
		}
	}
}