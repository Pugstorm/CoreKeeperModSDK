using CK_QOL_Collection.Core.Configuration;
using CoreLib.Data.Configuration;

namespace CK_QOL_Collection.Features.ItemPickUpNotifier
{
    /// <summary>
    ///     Configuration for the 'Item Pick-Up Notifier' feature.
    /// </summary>
    internal class ItemPickUpNotifierConfiguration : IFeatureConfiguration
	{
		private ConfigEntry<float> _logDelayEntry;
		private ConfigEntry<bool> _enabledEntry;

		/// <summary>
		///     Gets the delay in seconds between aggregated log messages to avoid spamming.
		/// </summary>
		public float LogDelay => _logDelayEntry.Value;
		/// <inheritdoc />
		public bool Enabled => _enabledEntry.Value;

		/// <inheritdoc />
		public string SectionName => nameof(ItemPickUpNotifier);

		/// <inheritdoc />
		public void BindSettings(ConfigFile configFile)
		{
			var enabledAcceptableValues = new AcceptableValueList<bool>(true, false);
			var enabledDescription = new ConfigDescription("Enable the 'Item Pick-Up Notifier' feature?", enabledAcceptableValues);
			_enabledEntry = configFile.Bind(SectionName, nameof(Enabled), false, enabledDescription);
			
			var logDelayAcceptableValues = new AcceptableValueRange<float>(1f, 30f);
			var logDelayDescription = new ConfigDescription("The delay in seconds to aggregate picked up items before displaying the notification.", logDelayAcceptableValues);
			_logDelayEntry = configFile.Bind(SectionName, nameof(LogDelay), 1.5f, logDelayDescription);
		}
	}
}