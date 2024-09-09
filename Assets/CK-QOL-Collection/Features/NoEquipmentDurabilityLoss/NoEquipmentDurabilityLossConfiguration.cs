using CK_QOL_Collection.Core.Configuration;
using CoreLib.Data.Configuration;

namespace CK_QOL_Collection.Features.NoEquipmentDurabilityLoss
{
	/// <summary>
	///     Configuration for the 'No Equipment Durability Loss' feature.
	/// </summary>
	internal class NoEquipmentDurabilityLossConfiguration : IFeatureConfiguration
	{
		private ConfigEntry<bool> _enabledEntry;

		public string SectionName => nameof(NoEquipmentDurabilityLoss);

		/// <inheritdoc />
		public bool Enabled => _enabledEntry.Value;

		/// <inheritdoc />
		public void BindSettings(ConfigFile configFile)
		{
			var enabledAcceptableValues = new AcceptableValueList<bool>(true, false);
			var enabledDescription = new ConfigDescription("Enable the 'No Equipment Durability Loss' (Server) feature?", enabledAcceptableValues);
			_enabledEntry = configFile.Bind(SectionName, nameof(Enabled), false, enabledDescription);
		}
	}
}