using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CK_QOL.Features.NoEquipmentDurabilityLoss.Systems;

namespace CK_QOL.Features.NoEquipmentDurabilityLoss
{
	/// <summary>
	/// 	Represents the "No Equipment Durability Loss" feature in the game, which prevents the 
	/// 	durability loss of equipment, effectively making all equipment indestructible.
	/// 	
	/// 	This feature disables the reduction in durability for all equipment items, ensuring that 
	/// 	equipment remains in perfect condition regardless of usage or damage taken.
	/// 	
	/// 	The class manages the following functionalities:
	/// 	<list type="bullet">
	/// 	    <item>
	/// 	        <description>Configuration of the feature's enabled state to determine whether the durability loss prevention should be active (<see cref="IFeature.IsEnabled"/>).</description>
	/// 	    </item>
	/// 	    <item>
	/// 	        <description>Integration with the <see cref="NoEquipmentDurabilityLossSystem"/> to handle the prevention of equipment durability loss in the game's simulation.</description>
	/// 	    </item>
	/// 	</list>
	/// </summary>
	/// <remarks>
	/// 	This class extends the <see cref="FeatureBase{TFeature}"/> base class to inherit common feature behavior, including singleton instantiation, configuration management, and execution control.
	///		The actual logic for preventing equipment durability loss is managed by the <see cref="NoEquipmentDurabilityLossSystem"/>.
	/// </remarks>
	internal sealed class NoEquipmentDurabilityLoss : FeatureBase<NoEquipmentDurabilityLoss>
	{
		#region IFeature

		public override string Name => nameof(NoEquipmentDurabilityLoss);
		public override string DisplayName => "No Equipment Durability Loss";
		public override string Description => "Removes the durability loss of equipment.";
		public override FeatureType FeatureType => FeatureType.Server;
		
		#endregion IFeature
		
		#region Configuration

		private void ApplyConfigurations()
		{
			ConfigBase.Create(this);
			IsEnabled = NoEquipmentDurabilityLossConfig.ApplyIsEnabled(this);
		}
		
		#endregion Configuration

		public NoEquipmentDurabilityLoss()
		{
			ApplyConfigurations();
		}
	}
}