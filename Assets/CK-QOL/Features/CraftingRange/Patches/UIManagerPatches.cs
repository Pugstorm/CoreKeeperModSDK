using HarmonyLib;

namespace CK_QOL.Features.CraftingRange.Patches
{
    [HarmonyPatch(typeof(UIManager))]
	internal static class UIManagerPatches
	{
        /// <summary>
        ///     A prefix patch for the <see cref="UIManager.OnPlayerInventoryOpen" /> method.
        ///     Searches for nearby chests when the <see cref="PlayerController"/> inventory is opened, corresponding to the <see cref="CraftingRange"/> feature.
        /// </summary>
        [HarmonyPrefix, HarmonyPatch(nameof(UIManager.OnPlayerInventoryOpen))]
        private static void OnPlayerInventoryOpen()
		{
			if (!CraftingRange.Instance.IsEnabled)
			{
				return;
			}

			if (Manager.main.player.activeCraftingHandler != null)
			{
				CraftingRange.Instance.Execute();
			}
		}
	}
}