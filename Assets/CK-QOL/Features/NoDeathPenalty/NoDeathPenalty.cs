using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CK_QOL.Features.NoDeathPenalty.Patches;
using CK_QOL.Features.NoDeathPenalty.Systems;
using PugMod;

namespace CK_QOL.Features.NoDeathPenalty
{
	/// <summary>
	/// 	Represents the "No Death Penalty" feature in the game, which prevents the loss of the player's inventory upon death.
	///		This feature modifies the default behavior so that the player's inventory remains intact after they respawn, effectively removing any penalty related to inventory loss due to death.
	/// 	
	/// 	The class manages the following functionalities:
	/// 	<list type="bullet">
	/// 	    <item>
	/// 	        <description>Configuration of the feature's enabled state, which determines whether the no-death penalty behavior should be active (<see cref="IFeature.IsEnabled"/>).</description>
	/// 	    </item>
	/// 	    <item>
	/// 	        <description>Applies Harmony patches to modify inventory management logic, ensuring that the player's inventory is preserved after death (<see cref="Execute"/> method).</description>
	/// 	    </item>
	/// 	    <item>
	/// 	        <description>Integrates with the <see cref="NoDeathPenaltySystem"/> to manage server-side simulation logic, preventing inventory loss events from being processed.</description>
	/// 	    </item>
	/// 	</list>
	/// </summary>
	/// <remarks>
	/// 	This class extends the <see cref="FeatureBase{TFeature}"/> base class to inherit common feature behavior, 
	/// 	including singleton instantiation, configuration management, and execution control. The feature relies on 
	/// 	the <see cref="NoDeathPenaltySystem"/> and the <see cref="InventoryCreatePatches"/> Harmony patches to achieve its functionality.
	/// </remarks>
	internal sealed class NoDeathPenalty : FeatureBase<NoDeathPenalty>
	{
		#region IFeature

		public override string Name => nameof(NoDeathPenalty);
		public override string DisplayName => "No Death Penalty";
		public override string Description => "Removes the behaviour, that the inventory gets lost after death.";
		public override FeatureType FeatureType => FeatureType.Server;
		
		#endregion IFeature

		#region Configuration

		private void ApplyConfigurations()
		{
			ConfigBase.Create(this);
			IsEnabled = NoDeathPenaltyConfig.ApplyIsEnabled(this);
		}
		
		#endregion Configuration

		public NoDeathPenalty()
		{
			ApplyConfigurations();
		}

		public override void Execute()
		{
			if (!CanExecute())
			{
				return;
			}

			API.ModLoader.ApplyHarmonyPatch(Entry.ModInfo.ModId, typeof(InventoryCreatePatches));
		}
	}
}