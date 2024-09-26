using CK_QOL.Core.Config;
using CoreLib.Data.Configuration;

namespace CK_QOL.Features.CraftingRange
{
	/// <summary>
	///     Provides configuration options for the "Crafting Range" feature, allowing customization of maximum range
	///     and the number of chests that can be included in the crafting range.
	/// </summary>
	internal class CraftingRangeConfig : ConfigBase<CraftingRange>
	{
		public CraftingRangeConfig(CraftingRange feature) : base(feature)
		{
		}

		/// <summary>
		///     Overrides the default enabled value for <see cref="CraftingRange" />.
		/// </summary>
		protected override bool DefaultIsEnabled => true;

		public float ApplyMaxRange()
		{
			var acceptableValues = new AcceptableValueRange<float>(1f, 50f);
			var description = new ConfigDescription("Maximum range to determine nearby chests.", acceptableValues);
			var definition = new ConfigDefinition(Feature.Name, nameof(Feature.MaxRange));

			var entry = Config.Bind(definition, 25f, description);

			return entry.Value;
		}

		public int ApplyMaxChests()
		{
			var acceptableValues = new AcceptableValueRange<int>(1, 50);
			var description = new ConfigDescription("Maximum number of chests to include in crafting range.", acceptableValues);
			var definition = new ConfigDefinition(Feature.Name, nameof(Feature.MaxChests));

			var entry = Config.Bind(definition, 10, description);

			return entry.Value;
		}
	}
}