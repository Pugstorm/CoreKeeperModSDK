using UnityEngine;

namespace Unity.Mathematics
{
    /// <summary>
    /// Extensions to Unity.Mathematics to deal with converting Bounds to and from AABB
    /// </summary>
    public static class AABBExtensions
    {
        /// <summary>
        /// Convert a Bounds struct to an AABB struct
        /// </summary>
        /// <param name="bounds">the Bounds struct to convert to an AABB struct</param>
        /// <returns>An AABB struct that is the same as the bounds struct</returns>
        public static AABB ToAABB(this Bounds bounds)
        {
            return new AABB { Center = bounds.center, Extents = bounds.extents};
        }

        /// <summary>
        /// Convert an AABB struct to a Bounds struct
        /// </summary>
        /// <param name="aabb">the AABB struct to convert to a Bounds struct</param>
        /// <returns>A Bounds struct that is the same as the AABB struct</returns>
        public static Bounds ToBounds(this AABB aabb)
        {
            return new Bounds { center = aabb.Center, extents = aabb.Extents};
        }
    }
}
