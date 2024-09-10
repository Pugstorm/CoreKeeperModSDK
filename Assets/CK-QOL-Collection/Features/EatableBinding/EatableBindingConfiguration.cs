using CK_QOL_Collection.Core.Feature.Configuration;
using CoreLib.Data.Configuration;

namespace CK_QOL_Collection.Features.EatableBinding
{
	/// <summary>
	///     Configuration for the 'Eatable Binding' feature.
	/// </summary>
	internal class EatableBindingConfiguration : IFeatureConfiguration
	{
		private ConfigEntry<int> _eatableSlotIndexEntry;
		private ConfigEntry<bool> _enabledEntry;

		/// <summary>
		///		Gets the index of the eatable slot in the inventory.
		/// </summary>
		public int EatableSlotIndex => _eatableSlotIndexEntry.Value;
		
		/// <summary>
		///		Gets the section name for the configuration.
		/// </summary>
		public string SectionName => nameof(EatableBinding);

		/// <inheritdoc />
		public bool Enabled => _enabledEntry.Value;

		/// <inheritdoc />
		public void BindSettings(ConfigFile configFile)
		{
			var enabledAcceptableValues = new AcceptableValueList<bool>(true, false);
			var enabledDescription = new ConfigDescription("Enable the 'Eatable Binding' (Client) feature?", enabledAcceptableValues);
			_enabledEntry = configFile.Bind(SectionName, nameof(Enabled), false, enabledDescription);

			var slotIndexAcceptableValues = new AcceptableValueRange<int>(0, 9);
			var slotIndexDescription = new ConfigDescription("Set the eatable slot index. It's the number/count of the slot minus 1.", slotIndexAcceptableValues);
			_eatableSlotIndexEntry = configFile.Bind(SectionName, nameof(EatableSlotIndex), 8, slotIndexDescription);
		}
	}
}