using CK_QOL_Collection.Core.Configuration;
using CoreLib.Data.Configuration;

namespace CK_QOL_Collection.Features.QuickStash
{
	/// <summary>
	///     Configuration for the 'Quick Stash' feature.
	/// </summary>
	internal class QuickStashConfiguration : IFeatureConfiguration
	{
		private ConfigEntry<float> _distanceEntry;
		private ConfigEntry<bool> _enabledEntry;

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
			var enabledDescription = new ConfigDescription("Enable the 'Quick Stash' feature?", enabledAcceptableValues);
			_enabledEntry = configFile.Bind(SectionName, nameof(Enabled), true, enabledDescription);

			var distanceAcceptableValues = new AcceptableValueRange<float>(5f, 50f);
			var distanceDescription = new ConfigDescription("Maximum distance to search for nearby chests.", distanceAcceptableValues);
			_distanceEntry = configFile.Bind(SectionName, nameof(Distance), 20f, distanceDescription);
		}
	}
}