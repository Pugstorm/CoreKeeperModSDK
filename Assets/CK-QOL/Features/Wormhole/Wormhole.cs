using CK_QOL.Core.Features;

namespace CK_QOL.Features.Wormhole
{
	/// <summary>
	///     The Wormhole feature allows players to teleport to other players by clicking on their map markers.
	///     This feature is configurable and requires a specific number of Ancient Gemstones to be spent on each teleportation.
	/// </summary>
	/// <remarks>
	///     The feature utilizes a configurable system where the required amount of Ancient Gemstones can be adjusted via the <see cref="WormholeConfig" /> class.
	///     By default, the player needs two Ancient Gemstones per teleportation, but this can be changed in the configuration.
	/// </remarks>
	internal sealed class Wormhole : FeatureBase<Wormhole>
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="Wormhole" /> class and applies the necessary configuration settings.
		/// </summary>
		public Wormhole()
		{
			var config = new WormholeConfig(this);
			IsEnabled = config.ApplyIsEnabled();
			RequiredAncientGemstones = config.ApplyRequiredAncientGemstones();
		}

		#region Configuration

		/// <summary>
		///     Gets the number of Ancient Gemstones required for each teleportation.
		///     This value is configurable and defaults to 2 if not set in the configuration.
		/// </summary>
		internal int RequiredAncientGemstones { get; }

		#endregion Configuration

		#region IFeature

		/// <summary>
		///     Gets the name of the feature, used for internal identification.
		/// </summary>
		public override string Name => nameof(Wormhole);

		/// <summary>
		///     Gets the display name of the feature, used in UI elements.
		/// </summary>
		public override string DisplayName => "Wormhole";

		/// <summary>
		///     Gets the description of the feature, providing a brief overview of its functionality.
		/// </summary>
		public override string Description => "Allows the player to teleport to other players.";

		/// <summary>
		///     Defines the type of the feature, in this case, a client-side feature.
		/// </summary>
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature

	}
}