using System;
using System.Collections.Generic;
using HarmonyLib;

namespace CK_QOL_Collection.Features.CraftingRange.Patches
{
    /// <summary>
    /// Contains Harmony patches for the <see cref="CraftingHandler"/> class to modify crafting behavior for the Crafting Range feature.
    /// </summary>
    [HarmonyPatch(typeof(CraftingHandler))]
    internal static class CraftingHandlerPatches
    {
        /// <summary>
        /// A prefix patch for the <see cref="CraftingHandler.GetAnyNearbyChests()"/> method.
        /// Overrides the result to use chests from the Crafting Range feature if it is enabled.
        /// </summary>
        /// <param name="__result">The reference to the result list of chests that will be modified.</param>
        /// <returns><see langword="false"/> if the Crafting Range feature is enabled to prevent original method execution; otherwise, <see langword="true"/>.</returns>
        [HarmonyPrefix, HarmonyPatch(nameof(CraftingHandler.GetAnyNearbyChests), new Type[] { })]
        private static bool GetAnyNearbyChests(ref List<Chest> __result)
        {
            if (!FeatureManager.Instance.CraftingRange.IsEnabled)
            {
                return true; // Continue with the original method execution.
            }
            
            __result = FeatureManager.Instance.CraftingRange.NearbyChests;
            return false; // Skip the original method execution.
        }
        
        //REMARK: Currently not needed, since all of these are calling CraftingHandler.GetAnyNearbyChests() and use it's return value as base.
        // /// <summary>
        // /// A prefix patch for the <see cref="CraftingHandler.HasMaterialsInCraftingInventoryToCraftRecipe"/> method.
        // /// Modifies the list of chests to take materials from, using those from the Crafting Range feature.
        // /// </summary>
        // /// <param name="recipeInfo">Information about the recipe to craft.</param>
        // /// <param name="checkPlayerInventoryToo">Indicates whether to check the player's inventory as well.</param>
        // /// <param name="nearbyChestsToTakeMaterialsFrom">The reference to the list of nearby chests from which to take materials.</param>
        // /// <param name="useRequiredObjectsSetInRecipeInfo">Indicates whether to use required objects specified in the recipe info.</param>
        // /// <param name="multiplier">The multiplier for the amount of materials needed.</param>
        // [HarmonyPrefix, HarmonyPatch(nameof(CraftingHandler.HasMaterialsInCraftingInventoryToCraftRecipe), typeof(CraftingHandler.RecipeInfo), typeof(bool), typeof(List<Chest>), typeof(bool), typeof(int))]
        // public static void HasMaterialsInCraftingInventoryToCraftRecipe(CraftingHandler.RecipeInfo recipeInfo, bool checkPlayerInventoryToo, ref List<Chest> nearbyChestsToTakeMaterialsFrom, bool useRequiredObjectsSetInRecipeInfo, int multiplier)
        // {            
        //     if (!FeatureManager.Instance.CraftingRange.IsEnabled)
        //     {
        //         return;
        //     }
        //     
        //     nearbyChestsToTakeMaterialsFrom = FeatureManager.Instance.CraftingRange.NearbyChests;
        // }
        //
        // /// <summary>
        // /// A prefix patch for the <see cref="CraftingHandler.GetCraftingMaterialInfosForRecipe"/> method.
        // /// Modifies the list of chests to take materials from, using those from the Crafting Range feature.
        // /// </summary>
        // /// <param name="recipeInfo">Information about the recipe to craft.</param>
        // /// <param name="nearbyChestsToTakeMaterialsFrom">The reference to the list of nearby chests from which to take materials.</param>
        // /// <param name="isRepairing">Indicates whether the crafting operation is a repair.</param>
        // /// <param name="isReinforcing">Indicates whether the crafting operation is a reinforcement.</param>
        // /// <param name="isCookedFood">Indicates whether the crafting operation involves cooked food.</param>
        // [HarmonyPrefix, HarmonyPatch(nameof(CraftingHandler.GetCraftingMaterialInfosForRecipe), typeof(CraftingHandler.RecipeInfo), typeof(List<Chest>), typeof(bool), typeof(bool), typeof(bool))]
        // public static void GetCraftingMaterialInfosForRecipe(CraftingHandler.RecipeInfo recipeInfo, ref List<Chest> nearbyChestsToTakeMaterialsFrom, bool isRepairing, bool isReinforcing, bool isCookedFood)
        // {            
        //     if (!FeatureManager.Instance.CraftingRange.IsEnabled)
        //     {
        //         return;
        //     }
        //     
        //     nearbyChestsToTakeMaterialsFrom = FeatureManager.Instance.CraftingRange.NearbyChests;
        // }
        //
        // /// <summary>
        // /// A prefix patch for the <see cref="CraftingHandler.GetCraftingMaterialInfosForUpgrade"/> method.
        // /// Modifies the list of chests to take materials from, using those from the Crafting Range feature.
        // /// </summary>
        // /// <param name="level">The level to which the item is being upgraded.</param>
        // /// <param name="nearbyChestsToTakeMaterialsFrom">The reference to the list of nearby chests from which to take materials.</param>
        // [HarmonyPrefix, HarmonyPatch(nameof(CraftingHandler.GetCraftingMaterialInfosForUpgrade))]
        // public static void GetCraftingMaterialInfosForUpgrade(int level, ref List<Chest> nearbyChestsToTakeMaterialsFrom)
        // {            
        //     if (!FeatureManager.Instance.CraftingRange.IsEnabled)
        //     {
        //         return;
        //     }
        //     
        //     nearbyChestsToTakeMaterialsFrom = FeatureManager.Instance.CraftingRange.NearbyChests;
        // }
    }
}