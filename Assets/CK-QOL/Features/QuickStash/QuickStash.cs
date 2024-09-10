using System.Linq;
using CK_QOL.Core;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CK_QOL.Core.Helpers;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.QuickStash
{
	/// <summary>
	/// 	Represents the "Quick Stash" feature in the game, allowing players to quickly transfer inventory items into nearby chests within a specified range. 
	/// 	This feature provides options for configuring the maximum range and the number of chests that are considered during the stashing operation,
	///		and also supports customizable key bindings to trigger the action.
	/// 	
	/// 	The class manages the following functionalities:
	/// 	<list type="bullet">
	/// 	    <item>
	/// 	        <description>Configuration of the feature's enabled state and stashing parameters, 
	/// 	        including maximum range (<see cref="MaxRange"/>) and maximum chest limit (<see cref="MaxChests"/>).</description>
	/// 	    </item>
	/// 	    <item>
	/// 	        <description>Defining key bindings for activating the quick stash action,
	///				allowing users to customize the key combination used to perform the operation (<see cref="ApplyKeyBinds"/> method).</description>
	/// 	    </item>
	/// 	    <item>
	/// 	        <description>Executing the logic to find nearby chests within the configured range, 
	/// 	        and transferring items from the player's inventory to these chests (<see cref="Execute"/> method).</description>
	/// 	    </item>
	/// 	    <item>
	/// 	        <description>Checking conditions for whether the quick stash feature can be executed, 
	/// 	        such as whether the player is in-game and has access to a valid inventory (<see cref="CanExecute"/> method).</description>
	/// 	    </item>
	/// 	    <item>
	/// 	        <description>Monitoring for key input and triggering the quick stash operation when the configured key combination is pressed (<see cref="Update"/> method).</description>
	/// 	    </item>
	/// 	</list>
	/// </summary>
	/// <remarks>
	///		This class extends the <see cref="FeatureBase{TFeature}"/> base class to inherit common feature behavior, 
	///		including singleton instantiation, configuration management, and execution control.
	/// </remarks>
	internal sealed class QuickStash : FeatureBase<QuickStash>
	{
		#region IFeature
		
		public override string Name => nameof(QuickStash);
		public override string DisplayName => "Quick Stash";
		public override string Description => "Allows quick stashing of inventory items into nearby chests.";
		public override FeatureType FeatureType => FeatureType.Client;
		
		#endregion IFeature
		
		#region Configuration

		internal string KeyBindName => $"{ModSettings.ShortName}_{Name}";
		internal float MaxRange { get; private set; }
		internal int MaxChests { get; private set; }

		private void ApplyConfigurations()
		{
			ConfigBase.Create(this);
			IsEnabled = QuickStashConfig.ApplyIsEnabled(this);
			MaxRange = QuickStashConfig.ApplyMaxRange(this);
			MaxChests = QuickStashConfig.ApplyMaxChests(this);
		}

		private void ApplyKeyBinds()
		{
			RewiredExtensionModule.AddKeybind(KeyBindName, "Quick Stash Items", KeyboardKeyCode.A, ModifierKey.Control);
		}
		
		#endregion Configuration

		public QuickStash()
		{
			ApplyConfigurations();
			ApplyKeyBinds();
		}
		
		public override bool CanExecute()
		{
			return base.CanExecute()
			       && Entry.RewiredPlayer != null
			       && (Manager.main.currentSceneHandler?.isInGame ?? false)
			       && Manager.main.player?.playerInventoryHandler != null;
		}

		public override void Execute()
		{
			if (!CanExecute())
			{
				return;
			}

			var player = Manager.main.player;
			var nearbyChests = ChestHelper.GetNearbyChests(MaxRange)
				.Take(MaxChests)
				.ToList();

			var stashedIntoChestsCount = 0;
			foreach (var inventoryHandler in nearbyChests.Select(chest => chest.inventoryHandler).Where(inventoryHandler => inventoryHandler != null))
			{
				player.playerInventoryHandler.QuickStack(player, inventoryHandler);
				stashedIntoChestsCount++;
			}

			TextHelper.DisplayText(stashedIntoChestsCount == 0
				? "Quick Stash: No chests found!"
				: $"Quick Stash: {stashedIntoChestsCount} chests", Rarity.Legendary);
		}

		public override void Update()
		{
			if (!CanExecute())
			{
				return;
			}

			if (Entry.RewiredPlayer.GetButtonDown(KeyBindName))
			{
				Execute();
			}
		}
	}
}