using System.Diagnostics.CodeAnalysis;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CoreLib.Data.Configuration;

namespace CK_QOL.Features.ItemPickUpNotifier
{
	[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
	internal sealed class ItemPickUpNotifierConfig : ConfigBase
	{
		internal static bool ApplyIsEnabled(IFeature feature)
		{
			var acceptableValues = new AcceptableValueList<bool>(true, false);
			var description = new ConfigDescription($"Enable the '{feature.DisplayName}' ({feature.FeatureType}) feature? {feature.Description}", acceptableValues);
			var definition = new ConfigDefinition(feature.Name, nameof(feature.IsEnabled));
			var entry = Config.Bind(definition, false, description);

			return entry.Value;
		}
		
		internal static float ApplyAggregateDelay(ItemPickUpNotifier feature)
		{
			var acceptableValues = new AcceptableValueRange<float>(1f, 30f);
			var description = new ConfigDescription("The delay in seconds to aggregate picked up items before displaying the notification.", acceptableValues);
			var definition = new ConfigDefinition(feature.Name, nameof(feature.AggregateDelay));
			var entry = Config.Bind(definition, 1.5f, description);

			return entry.Value;
		}
	}
}