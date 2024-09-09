using CK_QOL_Collection.Core.Configuration;
using CoreLib.Data.Configuration;

namespace CK_QOL_Collection.Features.CraftingRange
{
    /// <summary>
    ///     Configuration for the 'Crafting Range' feature.
    /// </summary>
    internal class CraftingRangeConfiguration : IFeatureConfiguration
	{
		private ConfigEntry<int> _chestLimitEntry;
		private ConfigEntry<float> _distanceEntry;
		private ConfigEntry<bool> _enabledEntry;
		public int ChestLimit => _chestLimitEntry.Value;
		public float Distance => _distanceEntry.Value;

		/// <inheritdoc />
		public bool Enabled => _enabledEntry.Value;

		/// <inheritdoc />
		public string SectionName => nameof(CraftingRange);

        /// <summary>
        ///     Binds the settings for this feature configuration.
        /// </summary>
        /// <param name="configFile">The configuration file to bind settings to.</param>
        public void BindSettings(ConfigFile configFile)
		{
			var enabledAcceptableValues = new AcceptableValueList<bool>(true, false);
			var enabledDescription = new ConfigDescription("Enable the 'Crafting Range' (Client) feature?", enabledAcceptableValues);
			_enabledEntry = configFile.Bind(SectionName, nameof(Enabled), true, enabledDescription);

			var chestLimitAcceptableValues = new AcceptableValueRange<int>(1, 8);
			var chestLimitDescription = new ConfigDescription("The maximum amount of chests to include. Currently more than 8 chests will break the game.", chestLimitAcceptableValues);
			_chestLimitEntry = configFile.Bind(SectionName, nameof(ChestLimit), 8, chestLimitDescription);
			
			var distanceAcceptableValues = new AcceptableValueRange<float>(5f, 50f);
			var distanceDescription = new ConfigDescription("The range to determine chests in proximity.", distanceAcceptableValues);
			_distanceEntry = configFile.Bind(SectionName, nameof(Distance), 20f, distanceDescription);
		}
	}
}