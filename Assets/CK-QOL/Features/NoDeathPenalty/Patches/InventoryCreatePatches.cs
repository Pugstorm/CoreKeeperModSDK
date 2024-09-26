using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Inventory;

namespace CK_QOL.Features.NoDeathPenalty.Patches
{
	/// <summary>
	///     Harmony patch for modifying the behavior of the <see cref="Create.MoveInventory" /> method to prevent inventory
	///     loss after death. This patch ensures that inventory transfer is blocked when the No Death Penalty feature is
	///     enabled.
	/// </summary>
	[HarmonyPatch(typeof(Create))]
	internal static class InventoryCreatePatches
	{
		/// <summary>
		///     A postfix patch for the <see cref="Create.MoveInventory" /> method that prevents inventory movement when the
		///     No Death Penalty feature is enabled.
		/// </summary>
		/// <param name="__result">The result of the inventory movement operation, which is modified to prevent loss.</param>
		[HarmonyPostfix]
		[HarmonyPatch(nameof(Create.MoveInventory))]
		[SuppressMessage("ReSharper", "InconsistentNaming")]
		private static void MoveInventory(ref InventoryChangeData __result)
		{
			if (!NoDeathPenalty.Instance.IsEnabled)
			{
				return;
			}

			// Check conditions to determine if inventory preservation is needed and skips the toolbar slots.
			if (__result.index2 != -1 && __result.index3 != 10)
			{
				// Set the destination inventory to be the same as the source inventory to prevent inventory loss.
				__result.entityOrInventory2 = __result.inventory1;
			}
		}
	}
}