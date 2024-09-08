using CK_QOL_Collection.Core.Feature;
using HarmonyLib;
using Inventory;

namespace CK_QOL_Collection.Features.NoDeathPenalty.Patches
{
	/// <summary>
	///     Contains Harmony patches for the <see cref="Inventory.Create" /> class to modify the inventory behavior after
	///     player death.
	///     Ensures that the player's inventory is preserved by adjusting the inventory movement logic.
	/// </summary>
	[HarmonyPatch(typeof(Create))]
	internal static class InventoryCreatePatches
	{
		/// <summary>
		///     A postfix patch for the <see cref="Create.MoveInventory" /> method.
		///     Modifies the result to ensure that the inventory is moved correctly, preserving the player's inventory after death.
		/// </summary>
		/// <param name="__result">
		///     The result of the inventory change operation, which is modified to retain the original
		///     inventory.
		/// </param>
		[HarmonyPostfix, HarmonyPatch(nameof(Create.MoveInventory))]
		public static void MoveInventory(ref InventoryChangeData __result)
		{
			var noDeathPenaltyFeature = FeatureManager.Instance.GetFeature<NoDeathPenaltyFeature>();
			if (noDeathPenaltyFeature is not { IsEnabled: true })
			{
				return;
			}
			
			// inventoryAction = InventoryAction.MoveInventory
			// inventory1 = inventoryFrom (Entity)
			// entityOrInventory2 = inventoryTo (Entity)
			// index1 = fromStartIndex (int)
			// index2 = amountOfSlots (int)
			// index3 = toStartIndex (int)

			// Check conditions to determine if inventory preservation is needed.
			// Skips the toolbar slots.
			if (__result.index2 != -1 && __result.index3 != 10)
			{
				// Set the destination inventory to be the same as the source inventory to prevent inventory loss.
				__result.entityOrInventory2 = __result.inventory1;
			}
		}
	}
}