using System;
using System.Diagnostics.CodeAnalysis;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CoreLib.Data.Configuration;

namespace CK_QOL.Features.QuickSummon
{
	[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
	internal sealed class QuickSummonConfig : ConfigBase
	{
		internal static bool ApplyIsEnabled(IFeature feature)
		{
			var acceptableValues = new AcceptableValueList<bool>(true, false);
			var description = new ConfigDescription($"Enable the '{feature.DisplayName}' ({feature.FeatureType}) feature? {feature.Description}", acceptableValues);
			var definition = new ConfigDefinition(feature.Name, nameof(feature.IsEnabled));
			var entry = Config.Bind(definition, true, description);

			return entry.Value;
		}

		internal static int ApplyEquipmentSlotIndex(QuickSummon feature)
		{
			var acceptableValues = new AcceptableValueRange<int>(0, 9);
			var description = new ConfigDescription("Set the summoning tome slot index. It's the count/number of the slot minus 1.", acceptableValues);
			var definition = new ConfigDefinition(feature.Name, nameof(feature.EquipmentSlotIndex));
			var entry = Config.Bind(definition, 0, description);

			return entry.Value;
		}

		internal static TomeType ApplyTomeType(QuickSummon feature)
		{
			var acceptableValues = new AcceptableValueList<string>(nameof(TomeType.TomeOfTheDark), nameof(TomeType.TomeOfTheDeep), nameof(TomeType.TomeOfTheDead));
			var description = new ConfigDescription("Set the summoning tome type.", acceptableValues);
			var definition = new ConfigDefinition(feature.Name, nameof(feature.TomeType));
			var entry = Config.Bind(definition, nameof(TomeType), description);
			var tomeString = entry.Value;

			return Enum.TryParse(tomeString, out TomeType tomeType)
				? tomeType
				: TomeType.None;
		}
	}
}