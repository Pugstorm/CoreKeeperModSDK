using System.Diagnostics.CodeAnalysis;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CoreLib.Data.Configuration;

namespace CK_QOL.Features.NoEquipmentDurabilityLoss
{
	[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
	internal sealed class NoEquipmentDurabilityLossConfig : ConfigBase
	{
		internal static bool ApplyIsEnabled(IFeature feature)
		{
			var acceptableValues = new AcceptableValueList<bool>(true, false);
			var description = new ConfigDescription($"Enable the '{feature.DisplayName}' ({feature.FeatureType}) feature? {feature.Description}", acceptableValues);
			var definition = new ConfigDefinition(feature.Name, nameof(feature.IsEnabled));
			
			var entry = Config.Bind(definition, false, description);

			return entry.Value;
		}
	}
}