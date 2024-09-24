using System.Collections.Generic;
using CK_QOL.Core.Patches;
using CoreLib.Util;
using UnityEngine;

namespace CK_QOL.Core.Helpers
{
	/// <summary>
	///     Provides utility methods for displaying custom text notifications.
	/// </summary>
	/// <remarks>
	///     Uses the in-game functionality of text displaying when discovery new items.
	/// </remarks>
	/// <seealso cref="ItemDiscoveryUIPatches" />
	/// <seealso cref="ItemDiscoveryTextUIPatches" />
	internal static class TextHelper
	{
		internal static Vector3 DefaultTextPosition => Manager.main.player.RenderPosition + new Vector3(0, 1.5f, 0);

		/// <summary>
		///     Displays a colored notification text defined by the rarity.
		/// </summary>
		/// <param name="text">The text to be displayed.</param>
		/// <param name="rarity">The rarity to control the color.</param>
		/// <remarks>
		///     Adds the text to the notification queue to be processed by <see cref="ItemDiscoveryUIPatches" />.
		/// </remarks>
		/// <seealso cref="UIManager.ShowDiscoveredItemText" />
		internal static void DisplayNotification(string text, Rarity rarity = Rarity.Poor)
		{
			text = $"{ModSettings.ShortName}{text}";
			Manager.ui.ShowDiscoveredItemText(new List<string>
			{
				text
			}, rarity);
		}

		/// <summary>
		///     Displays a colored text defined by the rarity.
		/// </summary>
		/// <param name="text">The text to be displayed.</param>
		/// <param name="rarity">The rarity to control the color.</param>
		/// <param name="position">The position where the text will be placed.</param>
		/// <remarks>
		///     By default, the text will be displayed above the player.
		/// </remarks>
		/// <seealso cref="DefaultTextPosition" />
		internal static void DisplayText(string text, Rarity rarity = Rarity.Poor, Vector3 position = default)
		{
			if (position == default)
			{
				position = DefaultTextPosition;
			}

			var textManager = GameManagers.GetManager<TextManager>();
			var color = Manager.text.GetRarityColor(rarity);

			textManager.SpawnCoolText(text, position, color, TextManager.FontFace.thinSmall, 0.2f, 1, 2, 0.8f, 0.8f);
		}
	}
}