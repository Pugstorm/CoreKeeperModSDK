using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CK_QOL.Core.Helpers;
using HarmonyLib;
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

			var currentPlayer = Manager.main.player;
			var currentPlayerEntity = currentPlayer.entity;

			// Ensure the marker is a player marker and not the current player
			if (__instance.markerType != MapMarkerType.Player || __instance.player.entity == currentPlayerEntity)
			{
				return;
			}

			var requiredGems = Wormhole.Instance.RequiredAncientGemstones;

			// Check if the player has the required amount of Ancient Gemstones
			if (!InventoryHandlerHelper.HasItemAmount(currentPlayer.playerInventoryHandler, ObjectID.AncientGemstone, requiredGems))
			{
				return;
			}

			// Remove the required Ancient Gemstones and teleport the player
			InventoryHandlerHelper.RemoveItems(currentPlayer.playerInventoryHandler, ObjectID.AncientGemstone, requiredGems);
			TeleportPlayerToPosition(currentPlayer, __instance.mapMarkerEntity);
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
			// Check if the Wormhole feature is enabled
			if (!Wormhole.Instance.IsEnabled)
			{
				return;
			}

			// Get the current player
			var currentPlayer = Manager.main.player;
			var currentPlayerEntity = currentPlayer.entity;

			// Check if the hovered marker is a player marker and not the current player
			if (__instance.markerType != MapMarkerType.Player || __instance.player.entity == currentPlayerEntity)
			{
				return;
			}

			var ancientGemstoneContainedBuffer = InventoryHandlerHelper.GetContainedObjectsBufferForObject(currentPlayer.playerInventoryHandler, ObjectID.AncientGemstone).FirstOrDefault();
			var itemName = PlayerController.GetObjectName(ancientGemstoneContainedBuffer, true).text;

			var teleportDescription = new TextAndFormatFields
			{
				text = $"Teleport: {Wormhole.Instance.RequiredAncientGemstones}x {itemName}",
				color = Color.cyan,
				dontLocalize = true
			};

			__result.Add(teleportDescription);
		}

		/// <summary>
		///     Teleports the player to the position associated with the specified map marker entity.
		/// </summary>
		/// <param name="player">The player controller responsible for the teleportation.</param>
		/// <param name="mapMarkerEntity">The entity representing the map marker to teleport to.</param>
		private static void TeleportPlayerToPosition(PlayerController player, Entity mapMarkerEntity)
		{
			if (!EntityUtility.HasComponentData<LocalTransform>(mapMarkerEntity, player.world))
			{
				return;
			}

			var markerTransform = EntityUtility.GetComponentData<LocalTransform>(mapMarkerEntity, player.world);
			player.transform.position = markerTransform.Position;
		}
	}
}