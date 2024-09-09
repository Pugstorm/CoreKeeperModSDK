using System;
using System.Diagnostics.CodeAnalysis;
using CK_QOL_Collection.Core.Feature;
using HarmonyLib;

namespace CK_QOL_Collection.Features.CraftingRange.Patches
{
    /// <summary>
    ///     Contains Harmony patches for the <see cref="UIManager" /> class to modify UI behavior related to the 'Crafting
    ///     Range' feature.
    /// </summary>
    [HarmonyPatch(typeof(UIManager))]
	internal static class UIManagerPatches
	{
        /// <summary>
        ///     A postfix patch for the <see cref="UIManager.OnPlayerInventoryOpen" /> method.
        ///     Searches for nearby chests when the player inventory is opened, if the 'Crafting Range' feature is enabled.
        /// </summary>
        [HarmonyPrefix, HarmonyPatch(nameof(UIManager.OnPlayerInventoryOpen), new Type[] { })]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static void OnPlayerInventoryOpenPrefix()
		{
			var craftingRangeFeature = FeatureManager.Instance.GetFeature<CraftingRangeFeature>();
			if (craftingRangeFeature is not { IsEnabled: true })
			{
				return;
			}

			// Check if the player has an active crafting handler and search for nearby chests.
			if (Manager.main.player.activeCraftingHandler != null)
			{
				craftingRangeFeature.Execute();
			}
		}
	}
}