using System.Collections.Generic;
using System.Linq;
using CK_QOL.Core.Features;
using CK_QOL.Core.Helpers;

namespace CK_QOL.Features.CraftingRange
{
	/// <summary>
	///     Provides the "Crafting Range" feature, which extends the range of crafting stations by including nearby chests
	///     within a specified maximum range. This allows players to access more chests during crafting without manually
	///     opening them.
	/// </summary>
	/// <remarks>
	///     The feature limits the number of chests that can be included in the crafting range to avoid game-breaking issues.
	///     The <see cref="CraftingRangeConfig" /> class manages the configuration for this feature, including the maximum
	///     range and the maximum number of chests that can be included.
	/// </remarks>
	internal sealed class CraftingRange : FeatureBase<CraftingRange>
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="CraftingRange" /> class and applies the configuration settings.
		/// </summary>
		public CraftingRange()
		{
			var config = new CraftingRangeConfig(this);
			IsEnabled = config.ApplyIsEnabled();
			MaxRange = config.ApplyMaxRange();
			MaxChests = config.ApplyMaxChests();
		}

		/// <summary>
		///     Gets the list of chests that are currently within the crafting range.
		/// </summary>
		internal List<Chest> Chests { get; } = new();

		/// <summary>
		///     Executes the logic to find and store the nearby chests within the specified maximum range.
		///     It clears the current list of chests and updates it with chests that are within the defined range and limit.
		/// </summary>
		public override void Execute()
		{
			if (!CanExecute())
			{
				return;
			}

			Chests.Clear();

			var nearbyChests = ChestHelper.GetNearbyChests(MaxRange).Take(MaxChests).ToList();

			Chests.AddRange(nearbyChests);
		}

		#region IFeature

		public override string Name => nameof(CraftingRange);
		public override string DisplayName => "Crafting Range";
		public override string Description => "Extends the crafting range for all kinds of work benches.";
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature

		#region Configuration

		internal float MaxRange { get; }
		internal int MaxChests { get; }

		#endregion Configuration
	}
}