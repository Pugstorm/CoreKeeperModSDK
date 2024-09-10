using CK_QOL_Collection.Core.Feature.Configuration;
using CoreLib.Data.Configuration;

namespace CK_QOL_Collection.Features.QuickStash
{
	/// <summary>
	///     Configuration for the 'Quick Stash' feature.
	/// </summary>
	internal class QuickStashConfiguration : IFeatureConfiguration
	{
		private ConfigEntry<int> _chestLimitEntry;
		private ConfigEntry<float> _distanceEntry;
		private ConfigEntry<bool> _enabledEntry;
		
		public int ChestLimit => _chestLimitEntry.Value;
		
		/// <summary>
		///     Gets the configured distance for detecting nearby chests.
		/// </summary>
		public float Distance => _distanceEntry.Value;

		public string SectionName => nameof(QuickStash);

		/// <inheritdoc />
		public bool Enabled => _enabledEntry.Value;

		/// <inheritdoc />
		public void BindSettings(ConfigFile configFile)
		{
			var enabledAcceptableValues = new AcceptableValueList<bool>(true, false);
			var enabledDescription = new ConfigDescription("Enable the 'Quick Stash' (Client) feature?", enabledAcceptableValues);
			_enabledEntry = configFile.Bind(SectionName, nameof(Enabled), true, enabledDescription);
			
			var chestLimitAcceptableValues = new AcceptableValueRange<int>(1, 50);
			var chestLimitDescription = new ConfigDescription("The maximum amount of chests to include. Depending on the range very many chests could be considered.", chestLimitAcceptableValues);
			_chestLimitEntry = configFile.Bind(SectionName, nameof(ChestLimit), 20, chestLimitDescription);
			
			var distanceAcceptableValues = new AcceptableValueRange<float>(5f, 50f);
			var distanceDescription = new ConfigDescription("Maximum distance to search for nearby chests.", distanceAcceptableValues);
			_distanceEntry = configFile.Bind(SectionName, nameof(Distance), 20f, distanceDescription);
		}
	}
}