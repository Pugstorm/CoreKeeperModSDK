using System;
using HarmonyLib;

namespace CK_QOL_Collection.Features.CraftingRange.Patches
{
    /// <summary>
    /// Contains Harmony patches for the <see cref="UIManager"/> class to modify UI behavior related to the Crafting Range feature.
    /// </summary>
    [HarmonyPatch(typeof(UIManager))]
    internal static class UIManagerPatches
    {
        /// <summary>
        /// A postfix patch for the <see cref="UIManager.OnPlayerInventoryOpen"/> method.
        /// Searches for nearby chests when the player inventory is opened, if the Crafting Range feature is enabled.
        /// </summary>
        [HarmonyPostfix, HarmonyPatch(nameof(UIManager.OnPlayerInventoryOpen), new Type[] { })]
        private static void OnPlayerInventoryOpenPrefix()
        {
            if (!FeatureManager.Instance.CraftingRange.IsEnabled)
            {
                return;
            }
            
            // Check if the player has an active crafting handler and search for nearby chests.
            if (Manager.main.player.activeCraftingHandler != null)
            {
                FeatureManager.Instance.CraftingRange.Execute();
            }
        }
    }
}