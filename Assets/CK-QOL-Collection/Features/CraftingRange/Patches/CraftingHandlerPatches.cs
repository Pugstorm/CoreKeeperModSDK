using System;
using System.Collections.Generic;
using CK_QOL_Collection.Core;
using HarmonyLib;

namespace CK_QOL_Collection.Features.CraftingRange.Patches
{
    /// <summary>
    ///     Contains Harmony patches for the <see cref="CraftingHandler" /> class to modify crafting behavior for the Crafting
    ///     Range feature.
    /// </summary>
    [HarmonyPatch(typeof(CraftingHandler))]
	internal static class CraftingHandlerPatches
	{
        /// <summary>
        ///     A prefix patch for the <see cref="CraftingHandler.GetAnyNearbyChests()" /> method.
        ///     Overrides the result to use chests from the 'Crafting Range' feature if it is enabled.
        /// </summary>
        /// <param name="__result">The reference to the result list of chests that will be modified.</param>
        /// <returns>
        ///     <see langword="false" /> if the 'Crafting Range' feature is enabled to prevent original method execution;
        ///     otherwise, <see langword="true" />.
        /// </returns>
        [HarmonyPrefix]
		[HarmonyPatch(nameof(CraftingHandler.GetAnyNearbyChests), new Type[] { })]
		private static bool GetAnyNearbyChests(ref List<Chest> __result)
		{
			var craftingRangeFeature = FeatureManager.Instance.GetFeature<CraftingRangeFeature>();
			if (craftingRangeFeature is not { IsEnabled: true })
			{
				// Continue with the original method execution.
				return true;
			}

			__result = craftingRangeFeature.NearbyChests;

			// Skip the original method execution.
			return false;
		}

		// REMARK: The following patches are currently unnecessary because all of these methods call CraftingHandler.GetAnyNearbyChests() and use its return value. 
		// If any of these methods' behaviors change in future game updates, we can uncomment the relevant patch below.
		// /// <summary>
		// ///     A prefix patch for the <see cref="CraftingHandler.HasMaterialsInCraftingInventoryToCraftRecipe(CraftingHandler.RecipeInfo, bool, System.Collections.Generic.List{Chest}, bool, int)" /> method.
		// ///     Modifies the list of chests to take materials from, using those from the 'Crafting Range' feature.
		// /// </summary>
		// [HarmonyPrefix] [HarmonyPatch(nameof(CraftingHandler.HasMaterialsInCraftingInventoryToCraftRecipe), typeof(CraftingHandler.RecipeInfo), typeof(bool), typeof(List<Chest>), typeof(bool), typeof(int))]
		// private static void HasMaterialsInCraftingInventoryToCraftRecipe(CraftingHandler.RecipeInfo recipeInfo, bool checkPlayerInventoryToo, ref List<Chest> nearbyChestsToTakeMaterialsFrom, bool useRequiredObjectsSetInRecipeInfo, int multiplier)
		// {
		//     var craftingRangeFeature = FeatureManager.Instance.GetFeature<Feature>();
		//     if (craftingRangeFeature is not { IsEnabled: true })
		//     {
		//         return;
		//     }
		//
		//     nearbyChestsToTakeMaterialsFrom = craftingRangeFeature.NearbyChests;
		// }
		//
		// /// <summary>
		// ///     A prefix patch for the <see cref="CraftingHandler.GetCraftingMaterialInfosForRecipe(CraftingHandler.RecipeInfo, System.Collections.Generic.List{Chest}, bool, bool, bool)" /> method.
		// ///     Modifies the list of chests to take materials from, using those from the 'Crafting Range' feature.
		// /// </summary>
		// [HarmonyPrefix] [HarmonyPatch(nameof(CraftingHandler.GetCraftingMaterialInfosForRecipe), typeof(CraftingHandler.RecipeInfo), typeof(List<Chest>), typeof(bool), typeof(bool), typeof(bool))]
		// private static void GetCraftingMaterialInfosForRecipe(CraftingHandler.RecipeInfo recipeInfo, ref List<Chest> nearbyChestsToTakeMaterialsFrom, bool isRepairing, bool isReinforcing, bool isCookedFood)
		// {
		//     var craftingRangeFeature = FeatureManager.Instance.GetFeature<Feature>();
		//     if (craftingRangeFeature is not { IsEnabled: true })
		//     {
		//         return;
		//     }
		//
		//     nearbyChestsToTakeMaterialsFrom = craftingRangeFeature.NearbyChests;
		// }
		//
		// /// <summary>
		// ///     A prefix patch for the <see cref="CraftingHandler.GetCraftingMaterialInfosForUpgrade" /> method.
		// ///     Modifies the list of chests to take materials from, using those from the 'Crafting Range' feature.
		// /// </summary>
		// [HarmonyPrefix] [HarmonyPatch(nameof(CraftingHandler.GetCraftingMaterialInfosForUpgrade))]
		// private static void GetCraftingMaterialInfosForUpgrade(int level, ref List<Chest> nearbyChestsToTakeMaterialsFrom)
		// {
		//     var craftingRangeFeature = FeatureManager.Instance.GetFeature<Feature>();
		//     if (craftingRangeFeature is not { IsEnabled: true })
		//     {
		//         return;
		//     }
		//
		//     nearbyChestsToTakeMaterialsFrom = craftingRangeFeature.NearbyChests;
		// }
	}
}