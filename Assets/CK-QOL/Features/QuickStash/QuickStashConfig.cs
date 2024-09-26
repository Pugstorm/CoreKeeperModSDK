using CK_QOL.Core.Config;
using CoreLib.Data.Configuration;

namespace CK_QOL.Features.QuickStash
{
	/// <summary>
	///     Configuration class for the <see cref="QuickStash" /> feature, handling key binding, enabled state, range, and max chests.
	///     This class uses <see cref="ConfigBase{TFeature}" /> to manage the configuration settings for QuickStash.
	/// </summary>
	internal sealed class QuickStashConfig : ConfigBase<QuickStash>
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="QuickStashConfig" /> class for the given feature.
		/// </summary>
		/// <param name="feature">The <see cref="QuickStash" /> feature being configured.</param>
		public QuickStashConfig(QuickStash feature) : base(feature)
		{
		}

		/// <summary>
		///     Overrides the default enabled value for <see cref="QuickStash" />.
		/// </summary>
		protected override bool DefaultIsEnabled => true;

		/// <summary>
		///     Applies the maximum range setting for QuickStash.
		/// </summary>
		/// <returns>The maximum range to detect nearby chests.</returns>
		public float ApplyMaxRange()
		{
			var acceptableValues = new AcceptableValueRange<float>(1f, 50f);
			var description = new ConfigDescription("The maximum range to detect nearby chests.", acceptableValues);
			var definition = new ConfigDefinition(Feature.Name, nameof(Feature.MaxRange));

			var entry = Config.Bind(definition, 25f, description);

			return entry.Value;
		}

		/// <summary>
		///     Applies the maximum chests setting for QuickStash.
		/// </summary>
		/// <returns>The maximum number of chests to include in the stash operation.</returns>
		public int ApplyMaxChests()
		{
			var acceptableValues = new AcceptableValueRange<int>(1, 50);
			var description = new ConfigDescription("The maximum number of chests to include.", acceptableValues);
			var definition = new ConfigDefinition(Feature.Name, nameof(Feature.MaxChests));

			var entry = Config.Bind(definition, 10, description);

			return entry.Value;
		}
	}
}