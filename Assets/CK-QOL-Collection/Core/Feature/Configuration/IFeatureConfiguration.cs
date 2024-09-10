using CoreLib.Data.Configuration;

namespace CK_QOL_Collection.Core.Feature.Configuration
{
    /// <summary>
    ///     Interface for feature configuration settings.
    /// </summary>
    internal interface IFeatureConfiguration
	{
        /// <summary>
        ///     Gets the section name of the configuration.
        /// </summary>
        string SectionName { get; }

        /// <summary>
        ///     Gets a value indicating whether the feature is enabled.
        /// </summary>
        bool Enabled { get; }

        /// <summary>
        ///     Binds settings for this feature configuration.
        /// </summary>
        /// <param name="configFile">The configuration file to bind settings to.</param>
        void BindSettings(ConfigFile configFile);
	}
}