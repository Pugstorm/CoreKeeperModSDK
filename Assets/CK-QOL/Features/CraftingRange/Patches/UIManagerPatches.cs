using HarmonyLib;

namespace CK_QOL.Features.CraftingRange.Patches
{
	/// <summary>
	///     Harmony patch to modify the behavior of <see cref="UIManager.OnPlayerInventoryOpen" /> method.
	///     This patch ensures that nearby chests are searched and stored when the player's inventory is opened, which
	///     is useful for the <see cref="CraftingRange" /> feature.
	/// </summary>
	[HarmonyPatch(typeof(UIManager))]
	internal static class UIManagerPatches
	{
		/// <summary>
		///     A prefix patch for the <see cref="UIManager.OnPlayerInventoryOpen" /> method.
		///     Searches for nearby chests when the <see cref="PlayerController" /> inventory is opened, corresponding to the
		///     <see cref="CraftingRange" /> feature.
		/// </summary>
		[HarmonyPrefix]
		[HarmonyPatch(nameof(UIManager.OnPlayerInventoryOpen))]
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