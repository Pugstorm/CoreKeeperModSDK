using CoreLib.Data.Configuration;

namespace CK_QOL_Collection.Core.Configuration
{
	/// <summary>
	///     Configuration for general settings of the mod.
	/// </summary>
	internal class GeneralConfiguration : IFeatureConfiguration
	{
		private ConfigEntry<bool> _enabledEntry;

		public string SectionName => "General";

		/// <inheritdoc />
		public bool Enabled => _enabledEntry.Value;

		/// <inheritdoc />
		public void BindSettings(ConfigFile configFile)
		{
			var enabledAcceptableValues = new AcceptableValueList<bool>(true, false);
			var enabledDescription = new ConfigDescription("Enable the mod?", enabledAcceptableValues);
			_enabledEntry = configFile.Bind(SectionName, nameof(Enabled), true, enabledDescription);
		}
	}
}