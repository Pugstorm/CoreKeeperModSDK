using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CK_QOL.Features.ItemPickUpNotifier.Systems;

namespace CK_QOL.Features.ItemPickUpNotifier
{
	/// <summary>
	/// 	Represents the "Item Pick-Up Notifier" feature in the game, which provides notifications whenever items are picked up from the ground.
	///		This feature aggregates multiple pick-ups into a single message based on a configurable delay, enhancing clarity and reducing notification spam.
	/// 	
	/// 	The core logic for this feature is handled by the <see cref="ItemPickUpNotificationSystem"/> class, 
	/// 	which operates within the game's simulation system to track item pickups and display aggregated notifications.
	/// 	
	/// 	The class manages the following functionalities:
	/// 	<list type="bullet">
	/// 	    <item>
	/// 	        <description>Configuration of the feature's enabled state and notification parameters, 
	/// 	        such as the aggregation delay (<see cref="AggregateDelay"/>), which controls how often notifications are displayed.</description>
	/// 	    </item>
	/// 	    <item>
	/// 	        <description>Integration with the <see cref="ItemPickUpNotificationSystem"/> to handle real-time aggregation and display of item pickup notifications.</description>
	/// 	    </item>
	/// 	</list>
	/// </summary>
	/// <remarks>
	/// 	This class extends the <see cref="FeatureBase{TFeature}"/> base class to inherit common feature behavior, including singleton instantiation, configuration management, and execution control. 
	/// 	The actual logic for detecting and aggregating item pick-ups is managed by the <see cref="ItemPickUpNotificationSystem"/>.
	/// </remarks>
	internal sealed class ItemPickUpNotifier : FeatureBase<ItemPickUpNotifier>
	{
		#region IFeature

		public override string Name => nameof(ItemPickUpNotifier);
		public override string DisplayName => "Item Pick-Up Notifier";
		public override string Description => "Notifies when picking up items from the ground.";
		public override FeatureType FeatureType => FeatureType.Client;
		
		#endregion IFeature

		#region Configurations

		internal float AggregateDelay { get; private set; }

		private void ApplyConfigurations()
		{
			ConfigBase.Create(this);
			IsEnabled = ItemPickUpNotifierConfig.ApplyIsEnabled(this);
			AggregateDelay = ItemPickUpNotifierConfig.ApplyAggregateDelay(this);
		}
		
		#endregion Configurations

		public ItemPickUpNotifier()
		{
			ApplyConfigurations();
		}
	}
}