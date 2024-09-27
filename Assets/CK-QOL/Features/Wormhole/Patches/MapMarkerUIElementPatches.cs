using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CK_QOL.Core;
using CK_QOL.Core.Helpers;
using HarmonyLib;
using I2.Loc;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace CK_QOL.Features.Wormhole.Patches
{
	/// <summary>
	///     A Harmony patch that modifies the behavior of the <see cref="MapMarkerUIElement.OnLeftClicked" /> method.
	///     This patch adds the ability for players to teleport to other players by clicking on their map markers,
	///     provided the teleportation feature is enabled and they have enough Ancient Gemstones.
	/// </summary>
	[HarmonyPatch(typeof(MapMarkerUIElement))]
	internal static class MapMarkerUIElementPatches
	{
		/// <summary>
		///     Called when the player clicks on a map marker. If the clicked marker belongs to another player and
		///     the Wormhole feature is enabled, the player will be teleported to that player's position, consuming
		///     a set amount of Ancient Gemstones.
		/// </summary>
		/// <param name="__instance">
		///     The instance of the <see cref="MapMarkerUIElement" /> that was clicked.
		/// </param>
		[HarmonyPrefix]
		[HarmonyPatch(nameof(MapMarkerUIElement.OnLeftClicked))]
		[SuppressMessage("ReSharper", "InconsistentNaming")]
		private static void OnLeftClicked(MapMarkerUIElement __instance)
		{
			if (!Wormhole.Instance.IsEnabled)
			{
				return;
			}

			Entity target;

			if (__instance.markerType == MapMarkerType.Player && __instance.player != null)
			{
				target = __instance.player.entity;
			}
			/*else if (EntityUtility.HasComponentData<LocalTransform>(__instance.mapMarkerEntity, Manager.ecs.ClientWorld))
			{
				target = __instance.mapMarkerEntity;
			}*/
			else
			{
				return;
			}

			var currentPlayer = Manager.main.player;
			var requiredGems = Wormhole.Instance.RequiredAncientGemstones;

			if (!InventoryHandlerHelper.HasItemAmount(currentPlayer.playerInventoryHandler, ObjectID.AncientGemstone, requiredGems))
			{
				return;
			}

			if (TryTeleportPlayerToPosition(currentPlayer, target))
			{
				InventoryHandlerHelper.RemoveItems(currentPlayer.playerInventoryHandler, ObjectID.AncientGemstone, requiredGems);
			}
		}

		/// <summary>
		///     Modifies the hover description of the map marker to include teleportation information if applicable.
		/// </summary>
		/// <param name="__instance">
		///     The instance of the <see cref="MapMarkerUIElement" /> being hovered over.
		/// </param>
		/// <param name="__result">The original hover description text returned by the method.</param>
		[HarmonyPostfix]
		[HarmonyPatch(nameof(MapMarkerUIElement.GetHoverDescription))]
		[SuppressMessage("ReSharper", "InconsistentNaming")]
		private static void GetHoverDescription(MapMarkerUIElement __instance, ref List<TextAndFormatFields> __result)
		{
			if (!Wormhole.Instance.IsEnabled)
			{
				return;
			}

			if (__instance.markerType != MapMarkerType.Player || __instance.player != null /* || !EntityUtility.HasComponentData<LocalTransform>(__instance.mapMarkerEntity, Manager.ecs.ClientWorld)*/)
			{
				return;
			}

			var currentPlayer = Manager.main.player;
			var requiredGems = Wormhole.Instance.RequiredAncientGemstones;
			var doesPlayerHasEnoughAncientGemstones = InventoryHandlerHelper.HasItemAmount(currentPlayer.playerInventoryHandler, ObjectID.AncientGemstone, requiredGems);

			var teleportDescription = new TextAndFormatFields
			{
				text = $"{ModSettings.ShortName}-{Wormhole.Instance.Name}",
				color = doesPlayerHasEnoughAncientGemstones ? Color.green : Color.red,
				formatFields = new[]
				{
					Wormhole.Instance.RequiredAncientGemstones.ToString(),
					LocalizationManager.GetTranslation("Items/AncientGemstone")
				}
			};

			__result ??= new List<TextAndFormatFields>();
			__result.Add(teleportDescription);
		}

		/// <summary>
		///     Teleports the player to the position associated with the specified map marker entity.
		/// </summary>
		/// <param name="player">The player controller responsible for the teleportation.</param>
		/// <param name="mapMarkerEntity">The entity representing the map marker to teleport to.</param>
		private static bool TryTeleportPlayerToPosition(PlayerController player, Entity mapMarkerEntity)
		{
			if (!EntityUtility.HasComponentData<LocalTransform>(mapMarkerEntity, Manager.ecs.ClientWorld))
			{
				return false;
			}

			var markerTransform = EntityUtility.GetComponentData<LocalTransform>(mapMarkerEntity, Manager.ecs.ClientWorld);

			Manager.ui.mapUI.ToggleMap();
			player.QueueInputAction(new UIInputActionData
			{
				action = UIInputAction.Teleport,
				position = markerTransform.Position.ToFloat2()
			});

			return true;
		}
	}
}