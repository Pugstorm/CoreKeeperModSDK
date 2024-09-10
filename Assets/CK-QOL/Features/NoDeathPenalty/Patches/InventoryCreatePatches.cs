using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Inventory;

namespace CK_QOL.Features.NoDeathPenalty.Patches
{
	[HarmonyPatch(typeof(Create))]
	internal static class InventoryCreatePatches
	{
		[HarmonyPostfix, HarmonyPatch(nameof(Create.MoveInventory))]
		[SuppressMessage("ReSharper", "InconsistentNaming")]
		private static void MoveInventory(ref InventoryChangeData __result)
		{
			if (!NoDeathPenalty.Instance.IsEnabled)
			{
				return;
			}
			
			// inventoryAction = InventoryAction.MoveInventory
			// inventory1 = inventoryFrom (Entity)
			// entityOrInventory2 = inventoryTo (Entity)
			// index1 = fromStartIndex (int)
			// index2 = amountOfSlots (int)
			// index3 = toStartIndex (int)

			// Check conditions to determine if inventory preservation is needed and skips the toolbar slots.
			if (__result.index2 != -1 && __result.index3 != 10)
			{
				// Set the destination inventory to be the same as the source inventory to prevent inventory loss.
				__result.entityOrInventory2 = __result.inventory1;
			}
		}
	}
}