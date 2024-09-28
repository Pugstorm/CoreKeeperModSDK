using CK_QOL.Core.Config;
using CoreLib.Data.Configuration;

namespace CK_QOL.Features.Wormhole
{
	/// <summary>
	///     Provides configuration options for the Wormhole feature, allowing the user to define the number of Ancient Gemstones
	///     required for each teleportation.
	/// </summary>
	internal class WormholeConfig : ConfigBase<Wormhole>
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="WormholeConfig" /> class, using the provided Wormhole feature.
		/// </summary>
		/// <param name="feature">The Wormhole feature to configure.</param>
		public WormholeConfig(Wormhole feature) : base(feature)
		{
		}

		/// <summary>
		///     Overrides the default enabled state for the Wormhole feature, ensuring it is enabled by default.
		/// </summary>
		protected override bool DefaultIsEnabled => true;

		/// <summary>
		///     Applies the configured number of Ancient Gemstones required for each teleportation.
		///     The amount is configurable between 0 and 25, with a default of 3.
		/// </summary>
		/// <returns>Returns the configured number of Ancient Gemstones required for teleportation.</returns>
		public int ApplyRequiredAncientGemstones()
		{
			var acceptableValues = new AcceptableValueRange<int>(0, 25);
			var description = new ConfigDescription("The amount of Ancient Gemstones required for each teleportation.", acceptableValues);
			var definition = new ConfigDefinition(Feature.Name, nameof(Feature.RequiredAncientGemstones));

			var entry = Config.Bind(definition, 3, description);

			return entry.Value;
		}
		
		public bool ApplyAllMarkersAllowed()
		{
			var acceptableValues = new AcceptableValueList<bool>(true, false);
			var description = new ConfigDescription("Are all markers allowed for teleportation?", acceptableValues);
			var definition = new ConfigDefinition(Feature.Name, nameof(Feature.AllMarkersAllowed));

			var entry = Config.Bind(definition, false, description);

			return entry.Value;
		}
	}
}