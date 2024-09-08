using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace CK_QOL_Collection.Core.Patches
{
    /// <summary>
    ///     Contains Harmony patches for the <see cref="ItemDiscoveryUI" /> class to modify how item discoveries are displayed.
    ///     This patch customizes the display of discovered items in the game to support custom mod text.
    /// </summary>
    [HarmonyPatch(typeof(ItemDiscoveryUI))]
    internal static class ItemDiscoveryUIPatches
    {
        /// <summary>
        ///     A prefix patch for the <see cref="ItemDiscoveryUI.ShowDiscoveredItem(List{string}, Rarity)" /> method.
        ///     Modifies the behavior to handle custom mod text prefixed with "-CK-QOL-" and manages the discovery text UI elements.
        /// </summary>
        /// <param name="__instance">The instance of <see cref="ItemDiscoveryUI" /> being patched.</param>
        /// <param name="texts">The list of text strings representing discovered items.</param>
        /// <param name="rarity">The rarity of the discovered item.</param>
        /// <returns>
        ///     <see langword="false" /> if the patch modifies the behavior to skip the original method execution;
        ///     otherwise, <see langword="true" /> to continue with the original method execution.
        /// </returns>
        [HarmonyPrefix, HarmonyPatch(nameof(ItemDiscoveryUI.ShowDiscoveredItem), typeof(List<string>), typeof(Rarity))]
        private static bool ShowDiscoveredItem(ItemDiscoveryUI __instance, ref List<string> texts, Rarity rarity)
        {
            // Check if the text list has exactly one item and if it starts with the custom prefix "-CK-QOL-".
            if (texts.Count is < 1 or > 1 || !texts[0].StartsWith("-CK-QOL-"))
            {
                // If the text does not match the criteria, continue with the original method execution.
                return true;
            }

            var text = texts[0];

            // Iterate over discovery texts to find an inactive text object.
            int discoveryTextsIndex;
            for (discoveryTextsIndex = 0; discoveryTextsIndex < __instance.discoveryTexts.Count; discoveryTextsIndex++)
            {
                if (__instance.discoveryTexts[discoveryTextsIndex].gameObject.activeSelf)
                {
                    continue;
                }

                // Activate the inactive text object with the custom text and rarity.
                __instance.discoveryTexts[discoveryTextsIndex].Activate(text, rarity, __instance);
                break;
            }

            // If no inactive text object was found, instantiate a new one and activate it.
            if (discoveryTextsIndex == __instance.discoveryTexts.Count)
            {
                var itemDiscoveryTextUI = Object.Instantiate(__instance.discoveryTextPrefab, __instance.container);
                __instance.discoveryTexts.Add(itemDiscoveryTextUI);
                itemDiscoveryTextUI.Activate(text, rarity, __instance);
            }

            // Position all active text objects properly by stacking them vertically.
            var zero = Vector3.zero;
            for (var activeTextsCount = __instance.activeTexts.Count - 1; activeTextsCount >= 0; activeTextsCount--)
            {
                if (!__instance.activeTexts[activeTextsCount].gameObject.activeSelf)
                {
                    continue;
                }

                // Align the active text object's position and adjust the offset for the next text.
                __instance.activeTexts[activeTextsCount].transform.localPosition = zero;
                zero += new Vector3(0f, __instance.activeTexts[activeTextsCount].pugText.dimensions.height, 0f);
            }

            // Skip the original method execution to apply the custom behavior.
            return false;
        }
    }
}