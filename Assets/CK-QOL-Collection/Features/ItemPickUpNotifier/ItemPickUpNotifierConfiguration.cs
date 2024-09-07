using CK_QOL_Collection.Core.Configuration;
using CoreLib.Data.Configuration;

namespace CK_QOL_Collection.Features.ItemPickUpNotifier
{
    /// <summary>
    ///     Configuration for the Item Pick-Up Notifier feature.
    /// </summary>
    internal class ItemPickUpNotifierConfiguration : IFeatureConfiguration
	{
		private ConfigEntry<bool> _enabledEntry;

		/// <inheritdoc />
		public bool Enabled => _enabledEntry.Value;

		/// <inheritdoc />
		public string SectionName => nameof(ItemPickUpNotifier);

		/// <inheritdoc />
		public void BindSettings(ConfigFile configFile)
		{
			var enabledAcceptableValues = new AcceptableValueList<bool>(true, false);
			var enabledDescription = new ConfigDescription("Enable the 'Item Pick-Up Notifier' feature?", enabledAcceptableValues);
			_enabledEntry = configFile.Bind(SectionName, nameof(Enabled), true, enabledDescription);
		}
	}
}