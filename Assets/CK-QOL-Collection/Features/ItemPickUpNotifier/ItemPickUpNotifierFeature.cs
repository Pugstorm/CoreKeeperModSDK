using CK_QOL_Collection.Core.Feature;

namespace CK_QOL_Collection.Features.ItemPickUpNotifier
{
	/// <summary>
	///     Represents the 'Item Pick-Up Notifier' feature of the mod.
	///     This feature notifies players when they pick up new items by comparing inventory changes.
	/// </summary>
	/// <remarks>
	///     This feature works by tracking changes in the player's inventory and notifying the player whenever a new item is detected.
	///     It compares the current state of the inventory with the previous state and identifies any discrepancies.
	/// </remarks>
	/// <seealso cref="Manager.main" />
	/// <seealso cref="Manager.ui" />
	internal class ItemPickUpNotifierFeature : FeatureBase
	{
		/// <summary>
		///     Gets the configuration settings for the 'Item Pick-Up Notifier' feature.
		/// </summary>
		public ItemPickUpNotifierConfiguration Config { get; }

		/// <summary>
		///     Initializes a new instance of the <see cref="ItemPickUpNotifierFeature" /> class.
		///     Sets up the 'Item Pick-Up Notifier' feature using the configuration settings.
		/// </summary>
		public ItemPickUpNotifierFeature()
			: base(nameof(ItemPickUpNotifier))
		{
			// Cast the base Configuration to the specific configuration for this feature
			Config = (ItemPickUpNotifierConfiguration)Configuration;
		}
	}
}