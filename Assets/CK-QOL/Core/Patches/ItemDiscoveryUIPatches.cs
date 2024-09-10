using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine;

namespace CK_QOL.Core.Patches
{
	/// <summary>
	///		Contains Harmony patches for the <see cref="ItemDiscoveryUI" /> class to display custom texts in-game.
	/// </summary>
	[HarmonyPatch(typeof(ItemDiscoveryUI))]
	internal static class ItemDiscoveryUIPatches
	{
		private const int MaxActiveTexts = 5;
		private static readonly Queue<(string text, Rarity rarity)> NotificationQueue = new();
		private static bool _isProcessingQueue;
		
		/// <summary>
		///		Harmony prefix patch for the <see cref="ItemDiscoveryUI.ShowDiscoveredItem(List{string}, Rarity)" /> method.
		///		Modifies the behavior to handle custom mod text prefixed with <see cref="ModSettings.ShortName"/> and customizes the text display.
		/// </summary>
		/// <returns>
		///		<see langword="false" /> if the patch modifies the behavior to skip the original method execution;
		///		otherwise, <see langword="true" /> to continue with the original method execution.
		/// </returns>
		[HarmonyPrefix, HarmonyPatch(nameof(ItemDiscoveryUI.ShowDiscoveredItem), typeof(List<string>), typeof(Rarity))]
		[SuppressMessage("ReSharper", "InconsistentNaming")]
		private static bool ShowDiscoveredItem(ItemDiscoveryUI __instance, ref List<string> texts, Rarity rarity)
		{
			if (texts.Count is < 1 or > 1 || !texts[0].StartsWith(ModSettings.ShortName))
			{
				return true;
			}
			
			NotificationQueue.Enqueue((texts[0], rarity));
			if (!_isProcessingQueue)
			{
				__instance.StartCoroutine(ProcessNotificationQueue(__instance));
			}

			return false;
		}

		/// <summary>
		///		Processes the notification queue to display notifications while respecting the maximum active text limit.
		/// </summary>
		/// <returns>An enumerator for the coroutine that processes the notification queue.</returns>
		private static IEnumerator<WaitForSeconds> ProcessNotificationQueue(ItemDiscoveryUI instance)
		{
			_isProcessingQueue = true;

			while (NotificationQueue.Count > 0)
			{
				while (instance.activeTexts.Count < MaxActiveTexts && NotificationQueue.Count > 0)
				{
					var (text, rarity) = NotificationQueue.Dequeue();
					ActivateNotification(instance, text, rarity);
				}

				yield return new WaitForSeconds(0.1f);
			}

			_isProcessingQueue = false;
		}

		/// <summary>
		///		Activates a notification on the screen by using available or newly instantiated text objects.
		/// </summary>
		private static void ActivateNotification(ItemDiscoveryUI instance, string text, Rarity rarity)
		{
			int index1;
			
			for (index1 = 0; index1 < instance.discoveryTexts.Count; ++index1)
			{
				if (instance.discoveryTexts[index1].gameObject.activeSelf)
				{
					continue;
				}
				
				instance.discoveryTexts[index1].Activate(text, rarity, instance);
				break;
			}
			
			if (index1 == instance.discoveryTexts.Count)
			{
				var itemDiscoveryTextUi = Object.Instantiate(instance.discoveryTextPrefab, instance.container);
				instance.discoveryTexts.Add(itemDiscoveryTextUi);
				itemDiscoveryTextUi.Activate(text, rarity, instance);
			}
			
			var zero = Vector3.zero;
			for (var index2 = instance.activeTexts.Count - 1; index2 >= 0; --index2)
			{
				if (!instance.activeTexts[index2].gameObject.activeSelf)
				{
					continue;
				}
				
				instance.activeTexts[index2].transform.localPosition = zero;
				zero += new Vector3(0.0f, instance.activeTexts[index2].pugText.dimensions.height, 0.0f);
			}
		}
	}
}