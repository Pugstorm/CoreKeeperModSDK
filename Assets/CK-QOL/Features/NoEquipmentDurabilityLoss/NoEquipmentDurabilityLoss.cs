using CK_QOL.Core.Features;

namespace CK_QOL.Features.NoEquipmentDurabilityLoss
{
	/// <summary>
	///     Provides the "No Equipment Durability Loss" feature, which ensures that equipment no longer loses durability
	///     during gameplay.
	/// </summary>
	internal sealed class NoEquipmentDurabilityLoss : FeatureBase<NoEquipmentDurabilityLoss>
	{
		public NoEquipmentDurabilityLoss()
		{
			var config = new NoEquipmentDurabilityLossConfig(this);
			IsEnabled = config.ApplyIsEnabled();
		}

		#region IFeature

		public override string Name => nameof(NoEquipmentDurabilityLoss);
		public override string DisplayName => "No Equipment Durability Loss";
		public override string Description => "Removes the durability loss of equipment.";
		public override FeatureType FeatureType => FeatureType.Server;

		#endregion IFeature

	}
}