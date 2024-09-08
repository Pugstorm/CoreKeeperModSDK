using HarmonyLib;
using UnityEngine;

namespace CK_QOL_Collection.Core.Patches
{
    /// <summary>
    ///     Contains Harmony patches for the <see cref="ItemDiscoveryTextUI" /> class to modify how item discovery text is
    ///     activated and displayed.
    ///     This patch customizes the display for items discovered with a custom prefix used by the mod.
    /// </summary>
    [HarmonyPatch(typeof(ItemDiscoveryTextUI))]
    internal static class ItemDiscoveryTextUIPatches
    {
        /// <summary>
        ///     A prefix patch for the <see cref="ItemDiscoveryTextUI.Activate(string, Rarity, ItemDiscoveryUI)" /> method.
        ///     Modifies the behavior to handle custom mod text prefixed with "-CK-QOL-" and customizes the text display.
        /// </summary>
        /// <param name="__instance">The instance of <see cref="ItemDiscoveryTextUI" /> being patched.</param>
        /// <param name="___color">The color reference for the text based on item rarity.</param>
        /// <param name="___activeTimer">The timer controlling the active state duration of the discovery text.</param>
        /// <param name="text">The text string representing the discovered item.</param>
        /// <param name="rarity">The rarity of the discovered item.</param>
        /// <param name="itemDiscoveryUI">The parent UI element managing the display of discovered items.</param>
        /// <returns>
        ///     <see langword="false" /> if the patch modifies the behavior to skip the original method execution;
        ///     otherwise, <see langword="true" /> to continue with the original method execution.
        /// </returns>
        [HarmonyPrefix, HarmonyPatch(nameof(ItemDiscoveryTextUI.Activate), typeof(string), typeof(Rarity), typeof(ItemDiscoveryUI))]
        private static bool Activate(ItemDiscoveryTextUI __instance, ref Color ___color, ref TimerSimple ___activeTimer, string text, Rarity rarity, ItemDiscoveryUI itemDiscoveryUI)
        {
            // Check if the text starts with the custom prefix "-CK-QOL-".
            if (!text.StartsWith("-CK-QOL-"))
            {
                // If not, continue with the original method execution.
                return true;
            }

            // Remove the custom prefix to prepare the text for display.
            text = text.Replace("-CK-QOL-", string.Empty);

            // Assign the parent ItemDiscoveryUI to the current instance.
            __instance.itemDiscoveryUI = itemDiscoveryUI;
            itemDiscoveryUI.activeTexts.Add(__instance);

            // Render the text using the provided content.
            // The original format field assignment is commented out; Render method is directly used instead.
            // __instance.pugText.formatFields = new[] { text };
            __instance.pugText.Render(text);

            // Set the text color based on the item's rarity.
            ___color = Manager.text.GetRarityColor(rarity);
            __instance.pugText.SetTempColor(___color);

            // Start the timer to control the duration for which the text remains active.
            ___activeTimer.Start();

            // Activate the game object to display the text.
            __instance.gameObject.SetActive(true);

            // Skip the original method execution to apply the custom behavior.
            return false;
        }
    }
}