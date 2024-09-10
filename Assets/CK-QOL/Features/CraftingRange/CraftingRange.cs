using System.Collections.Generic;
using System.Linq;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CK_QOL.Core.Helpers;

namespace CK_QOL.Features.CraftingRange
{
	/// <summary>
	/// 	Represents the crafting range feature in the game, which extends the range within which  a player can interact with workbenches and nearby chests. 
	/// 	This feature provides configuration options for adjusting the maximum range and the number  of chests that can be considered within proximity for crafting operations.
	/// 	
	/// 	The class manages the following functionalities:
	/// 	<list type="bullet">
	/// 	    <item>
	/// 	        <description>Configuration of the feature's enabled state and crafting range settings, 
	/// 	        including maximum range (<see cref="MaxRange"/>) and maximum chest limit (<see cref="MaxChests"/>).</description>
	/// 	    </item>
	/// 	    <item>
	/// 	        <description>Determining which chests are within the specified maximum range and adding them to a list of nearby chests (<see cref="Chests"/>).</description>
	/// 	    </item>
	/// 	    <item>
	/// 	        <description>Executing the logic to clear and update the list of nearby chests based on the configured range and chest limits (<see cref="Execute"/> method).</description>
	/// 	    </item>
	/// 	</list>
	/// </summary>
	///<remarks>
	///		This class extends the <see cref="FeatureBase{TFeature}"/> base class to inherit common feature behavior,
	///		including singleton instantiation, configuration management, and execution control.
	/// </remarks>
	internal sealed class CraftingRange : FeatureBase<CraftingRange>
	{
		#region IFeature
		
		public override string Name => nameof(CraftingRange);
		public override string DisplayName => "Crafting Range";
		public override string Description => "Extends the crafting range for different kinds of work benches.";
		public override FeatureType FeatureType => FeatureType.Client;
		
		#endregion IFeature
		
		#region Configuration

		internal float MaxRange { get; private set; }
		internal int MaxChests { get; private set; }

		private void ApplyConfigurations()
		{
			ConfigBase.Create(this);
			IsEnabled = CraftingRangeConfig.ApplyIsEnabled(this);
			MaxRange = CraftingRangeConfig.ApplyMaxRange(this);
			MaxChests = CraftingRangeConfig.ApplyMaxChests(this);
		}
		
		#endregion Configuration
		
		internal List<Chest> Chests { get; } = new();
		
		public CraftingRange()
		{
			ApplyConfigurations();
		}
		
		public override void Execute()
		{
			if (!CanExecute())
			{
				return;
			}

			Chests.Clear();

			var nearbyChests = ChestHelper.GetNearbyChests(MaxRange)
				.Take(MaxChests)
				.ToList();

			Chests.AddRange(nearbyChests);
		}
	}
}