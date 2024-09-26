using CK_QOL.Core.Features;
using CoreLib.Data.Configuration;

namespace CK_QOL.Core.Config
{
	/// <summary>
	///     Abstract base class for managing feature configuration settings.
	///     Handles loading the configuration file for a feature and applying common configuration properties.
	/// </summary>
	/// <typeparam name="TFeature">The type of the feature being configured.</typeparam>
	internal abstract class ConfigBase<TFeature> where TFeature : IFeature
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="ConfigBase{TFeature}" /> class.
		/// </summary>
		/// <param name="feature">The feature for which the configuration file is being created.</param>
		protected ConfigBase(TFeature feature)
		{
			Feature = feature;
			Config = new ConfigFile($"{ModSettings.ShortName}/{Feature.Name}.cfg", true, Entry.ModInfo);
		}

		protected ConfigFile Config { get; }
		protected TFeature Feature { get; }

		/// <summary>
		///     Gets the default value for the "IsEnabled" property.
		///     Derived feature configs can override this to specify feature-specific defaults.
		/// </summary>
		protected virtual bool DefaultIsEnabled => true;

		/// <summary>
		///     Applies the "IsEnabled" setting for the feature, determining if it should be active.
		/// </summary>
		/// <returns>
		///     A boolean value indicating whether the feature is enabled based on the configuration settings.
		/// </returns>
		public bool ApplyIsEnabled()
		{
			var acceptableValues = new AcceptableValueList<bool>(true, false);
			var description = new ConfigDescription($"Enable the '{Feature.DisplayName}' ({Feature.FeatureType}) feature? {Feature.Description}", acceptableValues);
			var definition = new ConfigDefinition(Feature.Name, nameof(Feature.IsEnabled));

			var entry = Config.Bind(definition, DefaultIsEnabled, description);

			return entry.Value;
		}
	}
}