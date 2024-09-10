using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine;

namespace CK_QOL.Core.Patches
{
    /// <summary>
    ///     Contains Harmony patches for the <see cref="ItemDiscoveryTextUI" /> class to display custom texts in-game.
    /// </summary>
    [HarmonyPatch(typeof(ItemDiscoveryTextUI))]
    internal static class ItemDiscoveryTextUIPatches
    {
        /// <summary>
        ///     A prefix patch for the <see cref="ItemDiscoveryTextUI.Activate(string, Rarity, ItemDiscoveryUI)" /> method.
        ///     Modifies the behavior to handle custom mod text prefixed with <see cref="ModSettings.ShortName"/> and customizes the text display.
        /// </summary>
        /// <returns>
        ///     <see langword="false" /> if the patch modifies the behavior to skip the original method execution;
        ///     otherwise, <see langword="true" /> to continue with the original method execution.
        /// </returns>
        [HarmonyPrefix, HarmonyPatch(nameof(ItemDiscoveryTextUI.Activate), typeof(string), typeof(Rarity), typeof(ItemDiscoveryUI))]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static bool Activate(ItemDiscoveryTextUI __instance, ref Color ___color, ref TimerSimple ___activeTimer, string text, Rarity rarity, ItemDiscoveryUI itemDiscoveryUI)
        {
            if (!text.StartsWith(ModSettings.ShortName))
            {
                return true;
            }

            text = text.Replace(ModSettings.ShortName, string.Empty);

            __instance.itemDiscoveryUI = itemDiscoveryUI;
            itemDiscoveryUI.activeTexts.Add(__instance);
            __instance.pugText.Render(text);
            ___color = Manager.text.GetRarityColor(rarity);
            __instance.pugText.SetTempColor(___color);
            ___activeTimer.Start();
            __instance.gameObject.SetActive(true);

            return false;
        }
    }
}