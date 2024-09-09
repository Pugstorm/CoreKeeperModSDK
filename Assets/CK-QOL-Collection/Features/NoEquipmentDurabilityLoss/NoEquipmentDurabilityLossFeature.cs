using CK_QOL_Collection.Core.Feature;

namespace CK_QOL_Collection.Features.NoEquipmentDurabilityLoss
{
	/// <summary>
	///     Represents the 'No Equipment Durability Loss' feature of the mod.
	///     This feature allows players to keep their whole inventory after dying.
	/// </summary>
	internal class NoEquipmentDurabilityLossFeature : FeatureBase
	{
		/// <summary>
		///     Gets the configuration settings for the 'No Equipment Durability Loss' feature.
		/// </summary>
		public NoEquipmentDurabilityLossConfiguration Config { get; }

		/// <summary>
		///     Initializes a new instance of the <see cref="NoEquipmentDurabilityLossFeature" /> class.
		///     Sets up the 'No Equipment Durability Loss' feature using the configuration settings.
		/// </summary>
		public NoEquipmentDurabilityLossFeature()
			: base(nameof(NoEquipmentDurabilityLoss))
		{
			Config = (NoEquipmentDurabilityLossConfiguration)Configuration;
		}
	}
}