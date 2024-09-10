using HarmonyLib;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace CK_QOL_Collection.Core.Patches
{
	/// <summary>
	///		Contains Harmony patches for the <see cref="ItemDiscoveryUI" /> class to modify how item discoveries are displayed.
	///		instance patch customizes the display of discovered items in the game to support custom mod text and limits the number of active texts.
	/// </summary>
	[HarmonyPatch(typeof(ItemDiscoveryUI))]
	internal static class ItemDiscoveryUIPatches
	{
		/// <summary>
		///		The maximum number of active texts that can be displayed at any given time.
		/// </summary>
		private const int MaxActiveTexts = 5;

		/// <summary>
		///		A queue to store notifications that are waiting to be displayed.
		/// </summary>
		private static readonly Queue<(string text, Rarity rarity)> NotificationQueue = new();

		/// <summary>
		///		A flag indicating whether the notification queue is currently being processed.
		/// </summary>
		private static bool isProcessingQueue;
		
		/// <summary>
		///		Harmony prefix patch for the <see cref="ItemDiscoveryUI.ShowDiscoveredItem(List{string}, Rarity)" /> method.
		///		Modifies the behavior to handle custom mod text prefixed with "-CK-QOL-" and manages the discovery text UI elements.
		/// </summary>
		/// <param name="__instance">The instance of <see cref="ItemDiscoveryUI" /> being patched.</param>
		/// <param name="texts">The list of text strings representing discovered items.</param>
		/// <param name="rarity">The rarity of the discovered item.</param>
		/// <returns>
		///		<see langword="false" /> if the patch modifies the behavior to skip the original method execution;
		///		otherwise, <see langword="true" /> to continue with the original method execution.
		/// </returns>
		[HarmonyPrefix, HarmonyPatch(nameof(ItemDiscoveryUI.ShowDiscoveredItem), typeof(List<string>), typeof(Rarity))]
		[SuppressMessage("ReSharper", "InconsistentNaming")]
		private static bool ShowDiscoveredItem(ItemDiscoveryUI __instance, ref List<string> texts, Rarity rarity)
		{
			// Check if the text is mod-related
			if (texts.Count is < 1 or > 1 || !texts[0].StartsWith("-CK-QOL-"))
			{
				return true; // Continue with the original method execution for non-mod texts
			}

			var text = texts[0];
			
			NotificationQueue.Enqueue((text, rarity));
			
			if (!isProcessingQueue)
			{
				__instance.StartCoroutine(ProcessNotificationQueue(__instance));
			}

			// Skip the original method execution
			return false;
		}

		/// <summary>
		/// Processes the notification queue to display notifications while respecting the maximum active text limit.
		/// </summary>
		/// <param name="instance">The instance of <see cref="ItemDiscoveryUI" /> managing the display of discovered items.</param>
		/// <returns>An enumerator for the coroutine that processes the notification queue.</returns>
		private static IEnumerator<WaitForSeconds> ProcessNotificationQueue(ItemDiscoveryUI instance)
		{
			isProcessingQueue = true;

			while (NotificationQueue.Count > 0)
			{
				// Check if there are active texts already exceeding the limit
				while (instance.activeTexts.Count < MaxActiveTexts && NotificationQueue.Count > 0)
				{
					var (text, rarity) = NotificationQueue.Dequeue();

					// Activate new notifications until we reach the limit
					ActivateNotification(instance, text, rarity);
				}

				// Wait for a short period before checking again
				yield return new WaitForSeconds(0.1f);
			}

			isProcessingQueue = false;
		}

		/// <summary>
		///		Activates a notification on the screen by using available or newly instantiated text objects.
		/// </summary>
		/// <param name="instance">The instance of <see cref="ItemDiscoveryUI" /> managing the display of discovered items.</param>
		/// <param name="text">The text string representing the discovered item.</param>
		/// <param name="rarity">The rarity of the discovered item.</param>
		private static void ActivateNotification(ItemDiscoveryUI instance, string text, Rarity rarity)
		{
			int index1;
			for (index1 = 0; index1 < instance.discoveryTexts.Count; ++index1)
			{
				if (!instance.discoveryTexts[index1].gameObject.activeSelf)
				{
					instance.discoveryTexts[index1].Activate(text, rarity, instance);
					break;
				}
			}
			if (index1 == instance.discoveryTexts.Count)
			{
				var itemDiscoveryTextUi = Object.Instantiate<ItemDiscoveryTextUI>(instance.discoveryTextPrefab, instance.container);
				instance.discoveryTexts.Add(itemDiscoveryTextUi);
				itemDiscoveryTextUi.Activate(text, rarity, instance);
			}
			var zero = Vector3.zero;
			for (var index2 = instance.activeTexts.Count - 1; index2 >= 0; --index2)
			{
				if (instance.activeTexts[index2].gameObject.activeSelf)
				{
					instance.activeTexts[index2].transform.localPosition = zero;
					zero += new Vector3(0.0f, instance.activeTexts[index2].pugText.dimensions.height, 0.0f);
				}
			}
		}
	}
}