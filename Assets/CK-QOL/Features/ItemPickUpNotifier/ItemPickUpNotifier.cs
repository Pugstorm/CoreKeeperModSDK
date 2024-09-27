using CK_QOL.Core.Features;

namespace CK_QOL.Features.ItemPickUpNotifier
{
	/// <summary>
	///     Provides the "Item Pick-Up Notifier" feature, which displays notifications when items are picked up from the
	///     ground.
	///     The feature aggregates multiple pick-up events within a configurable delay period to reduce notification spam.
	/// </summary>
	internal sealed class ItemPickUpNotifier : FeatureBase<ItemPickUpNotifier>
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="ItemPickUpNotifier" /> class and applies the configuration settings.
		/// </summary>
		public ItemPickUpNotifier()
		{
			var config = new ItemPickUpNotifierConfig(this);
			IsEnabled = config.ApplyIsEnabled();
			AggregateDelay = config.ApplyAggregateDelay();
		}

		#region Configurations

		/// <summary>
		///     Gets the delay, in seconds, used to aggregate picked-up items before displaying the notification.
		/// </summary>
		internal float AggregateDelay { get; }

		#endregion Configurations

		#region IFeature

		public override string Name => nameof(ItemPickUpNotifier);
		public override string DisplayName => "Item Pick-Up Notifier";
		public override string Description => "Notifies when picking up items from the ground.";
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature

	}
}