using System.Collections.Generic;
using CK_QOL_Collection.Core.Patches;

namespace CK_QOL_Collection.Core.Helpers
{
	/// <summary>
	///		Provides utility methods for displaying text notifications in the CK QOL Collection mod.
	///		This class handles the formatting and queueing of item discovery notifications to ensure they are displayed correctly.
	/// </summary>
	internal static class TextHelper
	{
		/// <summary>
		///		Displays a notification text for a discovered item with a specific rarity.
		///		Adds the text to the notification queue to be processed by <see cref="ItemDiscoveryUIPatches"/>.
		/// </summary>
		/// <param name="text">The text to be displayed for the discovered item.</param>
		/// <param name="rarity">The rarity of the discovered item.</param>
		internal static void DisplayText(string text, Rarity rarity)
		{
			// Prefix the text to distinguish it as a mod-related discovery.
			var modText = $"-CK-QOL-{text}";

			// Use the game's UI manager to show the discovered item text.
			Manager.ui.ShowDiscoveredItemText(new List<string> { modText }, rarity);
		}
	}
}