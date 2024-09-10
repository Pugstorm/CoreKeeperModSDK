using CK_QOL_Collection.Core.Feature.Configuration;
using CoreLib.Data.Configuration;

namespace CK_QOL_Collection.Features.HealableBinding
{
	/// <summary>
	///     Configuration for the 'Healable Binding' feature.
	/// </summary>
	internal class HealableBindingConfiguration : IFeatureConfiguration
	{
		private ConfigEntry<int> _healableSlotIndexEntry;
		private ConfigEntry<bool> _enabledEntry;

		/// <summary>
		///		Gets the index of the healable slot in the inventory.
		/// </summary>
		public int HealableSlotIndex => _healableSlotIndexEntry.Value;
		
		/// <summary>
		///		Gets the section name for the configuration.
		/// </summary>
		public string SectionName => nameof(HealableBinding);

		/// <inheritdoc />
		public bool Enabled => _enabledEntry.Value;

		/// <inheritdoc />
		public void BindSettings(ConfigFile configFile)
		{
			var enabledAcceptableValues = new AcceptableValueList<bool>(true, false);
			var enabledDescription = new ConfigDescription("Enable the 'Healable Binding' (Client) feature?", enabledAcceptableValues);
			_enabledEntry = configFile.Bind(SectionName, nameof(Enabled), false, enabledDescription);

			var slotIndexAcceptableValues = new AcceptableValueRange<int>(0, 9);
			var slotIndexDescription = new ConfigDescription("Set the healable slot index. It's the count of the slot minus 1.", slotIndexAcceptableValues);
			_healableSlotIndexEntry = configFile.Bind(SectionName, nameof(HealableSlotIndex), 9, slotIndexDescription);
		}
	}
}