using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace CK_QOL.Features.CraftingRange.Patches
{
    [HarmonyPatch(typeof(CraftingHandler))]
	internal static class CraftingHandlerPatches
	{
		[HarmonyPrefix, HarmonyPatch(nameof(CraftingHandler.GetAnyNearbyChests), new Type[] { })]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static bool GetAnyNearbyChests(ref List<Chest> __result)
		{
			if (!CraftingRange.Instance.IsEnabled)
			{
				return true;
			}

			__result = CraftingRange.Instance.Chests;

			return false;
		}
	}
}