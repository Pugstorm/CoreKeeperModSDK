using CK_QOL.Core.Config;
using CoreLib.Data.Configuration;

namespace CK_QOL.Features.ItemPickUpNotifier
{
	/// <summary>
	///     Configuration class for the <see cref="ItemPickUpNotifier" /> feature, handling the enabled state and aggregate delay.
	///     This class uses <see cref="ConfigBase{TFeature}" /> to manage the configuration settings for ItemPickUpNotifier.
	/// </summary>
	internal sealed class ItemPickUpNotifierConfig : ConfigBase<ItemPickUpNotifier>
	{
		public ItemPickUpNotifierConfig(ItemPickUpNotifier feature) : base(feature)
		{
		}

		/// <summary>
		///     Overrides the default enabled value for <see cref="ItemPickUpNotifier" />.
		/// </summary>
		protected override bool DefaultIsEnabled => true;

		/// <summary>
		///     Applies the aggregate delay setting for ItemPickUpNotifier.
		/// </summary>
		/// <returns>The delay in seconds for aggregating item pickups before showing notifications.</returns>
		public float ApplyAggregateDelay()
		{
			var acceptableValues = new AcceptableValueRange<float>(1f, 10f);
			var description = new ConfigDescription("The delay in seconds to aggregate picked-up items before displaying the notification.", acceptableValues);
			var definition = new ConfigDefinition(Feature.Name, nameof(Feature.AggregateDelay));

			var entry = Config.Bind(definition, 2f, description);

			return entry.Value;
		}
	}
}