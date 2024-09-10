using CK_QOL_Collection.Core.Feature.Configuration;
using CoreLib.Data.Configuration;

namespace CK_QOL_Collection.Features.NoDeathPenalty
{
	/// <summary>
	///     Configuration for the 'No Death Penalty' feature.
	/// </summary>
	internal class NoDeathPenaltyConfiguration : IFeatureConfiguration
	{
		private ConfigEntry<bool> _enabledEntry;

		public string SectionName => nameof(NoDeathPenalty);

		/// <inheritdoc />
		public bool Enabled => _enabledEntry.Value;

		/// <inheritdoc />
		public void BindSettings(ConfigFile configFile)
		{
			var enabledAcceptableValues = new AcceptableValueList<bool>(true, false);
			var enabledDescription = new ConfigDescription("Enable the 'No Death Penalty' (Server) feature?", enabledAcceptableValues);
			_enabledEntry = configFile.Bind(SectionName, nameof(Enabled), false, enabledDescription);
		}
	}
}