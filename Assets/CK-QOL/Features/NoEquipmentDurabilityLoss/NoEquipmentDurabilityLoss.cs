using CK_QOL.Core.Config;
using CK_QOL.Core.Features;

namespace CK_QOL.Features.NoEquipmentDurabilityLoss
{
	internal sealed class NoEquipmentDurabilityLoss : FeatureBase<NoEquipmentDurabilityLoss>
	{
		public NoEquipmentDurabilityLoss()
		{
			ConfigBase.Create(this);
			IsEnabled = NoEquipmentDurabilityLossConfig.ApplyIsEnabled(this);
		}

		#region IFeature

		public override string Name => nameof(NoEquipmentDurabilityLoss);
		public override string DisplayName => "No Equipment Durability Loss";
		public override string Description => "Removes the durability loss of equipment.";
		public override FeatureType FeatureType => FeatureType.Server;

		#endregion IFeature
	}
}