using System.Diagnostics.CodeAnalysis;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CoreLib.Data.Configuration;

namespace CK_QOL.Features.QuickStash
{
	[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
	internal sealed class QuickStashConfig : ConfigBase
	{
        internal static bool ApplyIsEnabled(IFeature feature)
		{
			var acceptableValues = new AcceptableValueList<bool>(true, false);
			var description = new ConfigDescription($"Enable the '{feature.DisplayName}' ({feature.FeatureType}) feature? {feature.Description}", acceptableValues);
			var definition = new ConfigDefinition(feature.Name, nameof(feature.IsEnabled));
			
			var entry = Config.Bind(definition, true, description);

			return entry.Value;
		}
		
		internal static float ApplyMaxRange(QuickStash feature)
		{
			var acceptableValues = new AcceptableValueRange<float>(1f, 50f);
			var description = new ConfigDescription("The maximum range to determine chests in proximity.", acceptableValues);
			var definition = new ConfigDefinition(feature.Name, nameof(feature.MaxRange));
			
			var entry = Config.Bind(definition, 25f, description);

			return entry.Value;
		}
		
		internal static int ApplyMaxChests(QuickStash feature)
		{
			var acceptableValues = new AcceptableValueRange<int>(1, 50);
			var description = new ConfigDescription("The maximum amount of chests to include. Depending on the range very many chests could be considered.", acceptableValues);
			var definition = new ConfigDefinition(feature.Name, nameof(feature.MaxChests));
			
			var entry = Config.Bind(definition, 25, description);

			return entry.Value;
		}
	}
}