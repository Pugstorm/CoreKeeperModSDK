using System.Collections.Generic;
using System.Linq;
using CoreLib.Util.Extensions;
using UnityEngine;

namespace CK_QOL_Collection.Core.Helpers
{
    /// <summary>
    /// Provides helper methods for operations related to chests in the game.
    /// </summary>
    internal static class ChestHelper
    {
        /// <summary>
        /// The name of the general pool chest in the game scene.
        /// </summary>
        private const string PoolChestName = "Pool Chest";

        /// <summary>
        /// The name of the boss chest pool in the game scene.
        /// </summary>
        private const string PoolBossChestName = "Pool BossChest";

        /// <summary>
        /// The name of the non-paintable chest pool in the game scene.
        /// </summary>
        private const string PoolNonPaintableChestChestName = "Pool NonPaintableChest";

        /// <summary>
        /// Retrieves a list of chests that are within a certain distance from the player.
        /// </summary>
        /// <param name="maxDistance">
        /// The maximum distance (in game units) from the player within which chests should be considered.
        /// If not specified, defaults to the maximum value of a <see cref="float"/>.
        /// </param>
        /// <returns>
        /// A list of <see cref="Chest"/> objects that are active and within the specified distance from the player.
        /// The list is ordered by the distance from the player in ascending order.
        /// </returns>
        /// <remarks>
        /// This method uses the player's world position to find nearby chests within the specified distance. 
        /// It searches across three predefined pools: general pool chests, boss pool chests, and non-paintable chests.
        /// </remarks>
        /// <seealso cref="MathHelpers.IsInRange(Vector3, Vector3, float)"/>
        /// <seealso cref="UnityEngine.GameObject"/>
        /// <seealso cref="UnityEngine.Transform"/>
        internal static IEnumerable<Chest> GetNearbyChests(float maxDistance = float.MaxValue)
        {
            // Retrieve the player controller object from the game's main manager.
            var player = Manager.main.player;
            
            // If the player or their inventory handler is not available, return an empty list.
            if (player?.playerInventoryHandler == null)
            {
                return Enumerable.Empty<Chest>();
            }

            // Find the chest pools in the game scene by their names.
            var poolChest = GameObject.Find(PoolChestName).transform;
            var poolBossChest = GameObject.Find(PoolBossChestName).transform;
            var poolNonPaintableChest = GameObject.Find(PoolNonPaintableChestChestName).transform;

            // Retrieve all active child objects from each chest pool.
            var allChests = poolChest.GetAllChildren().Where(obj => obj.gameObject.activeSelf).ToList();
            allChests.AddRange(poolBossChest.GetAllChildren().Where(obj => obj.gameObject.activeSelf).ToList());
            allChests.AddRange(poolNonPaintableChest.GetAllChildren().Where(obj => obj.gameObject.activeSelf).ToList());

            // Get the player's current world position.
            var playerPosition = player.WorldPosition;

            // Filter and order the chests based on their distance to the player.
            return allChests
                .Select(chestTransform => chestTransform.GetComponent<Chest>())
                .Where(chestComponent => chestComponent != null && MathHelpers.IsInRange(playerPosition, chestComponent.WorldPosition, maxDistance))
                .OrderBy(chestComponent => Vector3.Distance(playerPosition, chestComponent.WorldPosition));
        }
    }
}