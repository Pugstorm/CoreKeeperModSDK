using CK_QOL.Core.Config;
using CoreLib.Data.Configuration;

namespace CK_QOL.Features.QuickHeal
{
	/// <summary>
	///     Configuration class for the <see cref="QuickHeal" /> feature, handling key binding, enabled state, and equipment slot.
	///     This class uses <see cref="ConfigBase{TFeature}" /> to manage the configuration settings for QuickHeal.
	/// </summary>
	internal sealed class QuickHealConfig : ConfigBase<QuickHeal>
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="QuickHealConfig" /> class for the given feature.
		/// </summary>
		/// <param name="feature">The <see cref="QuickHeal" /> feature being configured.</param>
		public QuickHealConfig(QuickHeal feature) : base(feature)
		{
		}

		/// <summary>
		///     Overrides the default enabled value for <see cref="QuickHeal" />.
		/// </summary>
		protected override bool DefaultIsEnabled => true;

		/// <summary>
		///     Applies the equipment slot index setting for QuickHeal.
		/// </summary>
		/// <returns>The index of the equipment slot.</returns>
		public int ApplyEquipmentSlotIndex()
		{
			var acceptableValues = new AcceptableValueRange<int>(0, 9);
			var description = new ConfigDescription("The equipment slot index for healing potions.", acceptableValues);
			var definition = new ConfigDefinition(Feature.Name, nameof(Feature.EquipmentSlotIndex));

			var entry = Config.Bind(definition, 9, description);

			return entry.Value;
		}
	}
}