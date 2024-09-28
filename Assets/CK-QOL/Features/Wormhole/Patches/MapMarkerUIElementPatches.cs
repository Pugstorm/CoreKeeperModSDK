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
			if (!TryGetTargetEntity(__instance, out _))
			{
				return;
			}

			var currentPlayer = Manager.main.player;
			var requiredGems = Wormhole.Instance.RequiredAncientGemstones;
			var hasEnoughGems = InventoryHandlerHelper.HasItemAmount(currentPlayer.playerInventoryHandler, ObjectID.AncientGemstone, requiredGems);

			var teleportDescription = new TextAndFormatFields
			{
				text = $"{ModSettings.ShortName}-{Wormhole.Instance.Name}",
				color = hasEnoughGems ? Color.green : Color.red,
				formatFields = new[]
				{
					requiredGems.ToString(),
					LocalizationManager.GetTranslation("Items/AncientGemstone")
				}
			};

			__result ??= new List<TextAndFormatFields>();
			__result.Add(teleportDescription);
		}

		/// <summary>
		///     Called when the player clicks on a map marker. If the conditions are met, the player will be teleported
		///     to the target's position, consuming the required amount of Ancient Gemstones.
		/// </summary>
		/// <param name="__instance">
		///     The instance of the <see cref="MapMarkerUIElement" /> that was clicked.
		/// </param>
		[HarmonyPrefix]
		[HarmonyPatch(nameof(MapMarkerUIElement.OnLeftClicked))]
		[SuppressMessage("ReSharper", "InconsistentNaming")]
		private static void OnLeftClicked(MapMarkerUIElement __instance)
		{
			if (!TryGetTargetEntity(__instance, out var target))
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
		///     Attempts to retrieve the target entity for the Wormhole action based on the given map marker instance.
		/// </summary>
		/// <param name="instance">
		///     The <see cref="MapMarkerUIElement"/> instance representing the map marker being interacted with.
		/// </param>
		/// <param name="targetEntity">
		///     When this method returns, contains the target <see cref="Entity"/> to act upon if the method returns <c>true</c>;
		///     otherwise, contains the default value.
		/// </param>
		/// <returns>
		///     <c>true</c> if the Wormhole action can proceed and a valid target entity is found; otherwise, <c>false</c>.
		/// </returns>
		/// <remarks>
		///     This method determines whether the Wormhole feature can be activated based on the following conditions:
		///     <list type="bullet">
		///         <item>
		///             <description>The Wormhole feature is enabled (<see cref="Wormhole.IsEnabled"/> is <c>true</c>).</description>
		///         </item>
		///         <item>
		///             <description>The current player is not the target player.</description>
		///         </item>
		///         <item>
		///             <description>
		///                 If the marker type is <see cref="MapMarkerType.Player"/>, the target player is not <c>null</c>,
		///                 and the target entity is set to the target player's entity.
		///             </description>
		///         </item>
		///         <item>
		///             <description>
		///                 If the marker type is not <see cref="MapMarkerType.Player"/>, and <see cref="Wormhole.AllMarkersAllowed"/>
		///                 is <c>true</c>, and the map marker entity has a valid <see cref="LocalTransform"/>,
		///                 the target entity is set to the map marker entity.
		///             </description>
		///         </item>
		///     </list>
		///     If all relevant conditions are met, the method assigns the appropriate target entity and returns <c>true</c>,
		///     indicating that the Wormhole action can proceed.
		/// </remarks>
		private static bool TryGetTargetEntity(MapMarkerUIElement instance, out Entity targetEntity)
		{
			targetEntity = default;

			if (!Wormhole.Instance.IsEnabled)
			{
				return false;
			}

			var currentPlayer = Manager.main.player;
			var targetPlayer = instance.player;
			var mapMarkerEntity = instance.mapMarkerEntity;

			if (currentPlayer == targetPlayer)
			{
				return false;
			}

			if (instance.markerType == MapMarkerType.Player)
			{
				if (targetPlayer == null)
				{
					return false;
				}

				targetEntity = targetPlayer.entity;
			}
			else
			{
				if (!Wormhole.Instance.AllMarkersAllowed)
				{
					return false;
				}

				if (!EntityUtility.HasComponentData<LocalTransform>(mapMarkerEntity, Manager.ecs.ClientWorld))
				{
					return false;
				}

				targetEntity = mapMarkerEntity;
			}

			return true;
		}

		/// <summary>
		///     Teleports the player to the position associated with the specified map marker entity.
		/// </summary>
		/// <param name="player">The player controller responsible for the teleportation.</param>
		/// <param name="targetEntity">The entity representing the map marker to teleport to.</param>
		/// <returns>
		///     <c>true</c> if the teleportation was successful; otherwise, <c>false</c>.
		/// </returns>
		private static bool TryTeleportPlayerToPosition(PlayerController player, Entity targetEntity)
		{
			if (!EntityUtility.HasComponentData<LocalTransform>(targetEntity, Manager.ecs.ClientWorld))
			{
				return false;
			}

			var markerTransform = EntityUtility.GetComponentData<LocalTransform>(targetEntity, Manager.ecs.ClientWorld);

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