using System.Collections.Generic;
using CK_QOL_Collection.Features.CraftingRange;
using CK_QOL_Collection.Features.EatableBinding;
using CK_QOL_Collection.Features.HealableBinding;
using CK_QOL_Collection.Features.ItemPickUpNotifier;
using CK_QOL_Collection.Features.NoDeathPenalty;
using CK_QOL_Collection.Features.NoEquipmentDurabilityLoss;
using CK_QOL_Collection.Features.QuickStash;
using CoreLib.Data.Configuration;
using PugMod;

namespace CK_QOL_Collection.Core.Feature.Configuration
{
    /// <summary>
    ///     Handles the configuration settings for the CK_QOL_Collection mod.
    ///     Provides functionality to initialize and manage settings for various features.
    /// </summary>
    internal static class ConfigurationManager
    {
        /// <summary>
        ///     Dictionary to hold feature configuration sections dynamically.
        /// </summary>
        private static readonly Dictionary<string, IFeatureConfiguration> FeatureConfigurations = new();

        /// <summary>
        ///     Gets the configuration file used to store and manage settings for the mod.
        /// </summary>
        private static ConfigFile ConfigFile { get; set; }

        /// <summary>
        ///     Gets a value indicating whether the general mod configuration is enabled.
        /// </summary>
        public static bool IsModEnabled => GetGeneralConfiguration()?.Enabled ?? false;

        /// <summary>
        ///     Initializes the configuration settings for the mod.
        /// </summary>
        /// <param name="modInfo">The loaded mod information.</param>
        /// <returns>The initialized <see cref="ConfigFile" /> instance containing the mod's configuration.</returns>
        internal static ConfigFile Initialize(LoadedMod modInfo)
        {
            ConfigFile = new ConfigFile($"{ModSettings.Name}/{ModSettings.Name}.cfg", true, modInfo);

            LoadFeatureConfigurations();

            foreach (var featureConfig in FeatureConfigurations.Values)
            {
                featureConfig.BindSettings(ConfigFile);
            }

            return ConfigFile;
        }

        /// <summary>
        ///     Loads feature configurations into the FeatureConfigurations dictionary manually.
        /// </summary>
        private static void LoadFeatureConfigurations()
        {
            var generalConfig = new GeneralConfiguration();
            FeatureConfigurations[generalConfig.SectionName] = generalConfig;

            var craftingRangeConfig = new CraftingRangeConfiguration();
            FeatureConfigurations[craftingRangeConfig.SectionName] = craftingRangeConfig;

            var quickStashConfig = new QuickStashConfiguration();
            FeatureConfigurations[quickStashConfig.SectionName] = quickStashConfig;

            var noDeathPenaltyConfig = new NoDeathPenaltyConfiguration();
            FeatureConfigurations[noDeathPenaltyConfig.SectionName] = noDeathPenaltyConfig;

            var itemPickUpNotifierConfig = new ItemPickUpNotifierConfiguration();
            FeatureConfigurations[itemPickUpNotifierConfig.SectionName] = itemPickUpNotifierConfig;
            
            var noEquipmentDurabilityLossConfig = new NoEquipmentDurabilityLossConfiguration();
            FeatureConfigurations[noEquipmentDurabilityLossConfig.SectionName] = noEquipmentDurabilityLossConfig;
            
            var eatableBindingConfig = new EatableBindingConfiguration();
            FeatureConfigurations[eatableBindingConfig.SectionName] = eatableBindingConfig;
            
            var healableBindingConfig = new HealableBindingConfiguration();
            FeatureConfigurations[healableBindingConfig.SectionName] = healableBindingConfig;
        }

        /// <summary>
        ///     Gets a specific feature configuration by its section name.
        /// </summary>
        /// <param name="sectionName">The section name of the feature configuration.</param>
        /// <returns>The corresponding <see cref="IFeatureConfiguration" /> instance.</returns>
        internal static IFeatureConfiguration GetFeatureConfiguration(string sectionName) 
            => FeatureConfigurations.GetValueOrDefault(sectionName);

        /// <summary>
        ///     Gets the general configuration section.
        /// </summary>
        /// <returns>The <see cref="GeneralConfiguration"/> instance if it exists; otherwise, null.</returns>
        private static GeneralConfiguration GetGeneralConfiguration()
            => FeatureConfigurations.TryGetValue("General", out var config) 
                ? config as GeneralConfiguration 
                : null;
    }
}
