using System.Diagnostics.CodeAnalysis;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CoreLib.Data.Configuration;

namespace CK_QOL.Features.QuickHeal
{
	[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
	internal sealed class QuickHealConfig : ConfigBase
	{
		internal static bool ApplyIsEnabled(IFeature feature)
		{
			var acceptableValues = new AcceptableValueList<bool>(true, false);
			var description = new ConfigDescription($"Enable the '{feature.DisplayName}' ({feature.FeatureType}) feature? {feature.Description}", acceptableValues);
			var definition = new ConfigDefinition(feature.Name, nameof(feature.IsEnabled));
			var entry = Config.Bind(definition, false, description);

			return entry.Value;
		}
		
		internal static int ApplyEquipmentSlotIndex(QuickHeal feature)
		{
			var acceptableValues = new AcceptableValueRange<int>(0, 9);
			var description = new ConfigDescription("Set the healable slot index. It's the count/number of the slot minus 1.", acceptableValues);
			var definition = new ConfigDefinition(feature.Name, nameof(feature.EquipmentSlotIndex));
			var entry = Config.Bind(definition, 9, description);

			return entry.Value;
		}
	}
}