using System.Collections.Generic;
using CK_QOL.Core.Patches;

namespace CK_QOL.Core.Helpers
{
	/// <summary>
	///		Provides utility methods for displaying custom text notifications.
	/// </summary>
	/// <remarks>
	///		Uses the in-game functionality of text displaying when discovery new items.
	/// </remarks>
	/// <seealso cref="ItemDiscoveryUIPatches"/>
	/// <seealso cref="ItemDiscoveryTextUIPatches"/>
	internal static class TextHelper
	{
		/// <summary>
		///		Displays a colored notification text defined by the rarity.		
		/// </summary>
		/// <param name="text">The text to be displayed.</param>
		/// <param name="rarity">The rarity to control the color.</param>
		/// <remarks>
		///		Adds the text to the notification queue to be processed by <see cref="ItemDiscoveryUIPatches"/>.
		/// </remarks>
		/// <seealso cref="UIManager.ShowDiscoveredItemText"/>
		internal static void DisplayText(string text, Rarity rarity = Rarity.Poor)
		{
			text = $"{ModSettings.ShortName}{text}";

			Manager.ui.ShowDiscoveredItemText(new List<string> { text }, rarity);
		}
	}
}