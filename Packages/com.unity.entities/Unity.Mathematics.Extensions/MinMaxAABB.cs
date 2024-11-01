using System;
using static Unity.Mathematics.math;

namespace Unity.Mathematics
{
    /// <summary>
    /// Axis aligned bounding box (AABB) stored in min and max form.
    /// </summary>
    /// <remarks>
    /// Axis aligned bounding boxes (AABB) are boxes where each side is parallel with one of the Cartesian coordinate axes
    /// X, Y, and Z. AABBs are useful for approximating the region an object (or collection of objects) occupies and quickly
    /// testing whether or not that object (or collection of objects) is relevant. Because they are axis aligned, they
    /// are very cheap to construct and perform overlap tests with them.
    /// </remarks>
    [System.Serializable]
    public struct MinMaxAABB
    {
        /// <summary>
        /// The minimum point contained by the AABB.
        /// </summary>
        /// <remarks>
        /// If any component of <see cref="Min"/> is greater than <see cref="Max"/> then this AABB is invalid.
        /// </remarks>
        public float3 Min;

        /// <summary>
        /// The maximum point contained by the AABB.
        /// </summary>
        /// <remarks>
        /// If any component of <see cref="Max"/> is less than <see cref="Min"/> then this AABB is invalid.
        /// </remarks>
        public float3 Max;

        /// <summary>
        /// Is this AABB empty? It is empty only if the minimum and maximum coordinates are at opposing infinities.
        /// </summary>
        /// <returns>Whether the AABB is empty (its minimum and maximum coordinates are at opposing infinities)</returns>
        public bool IsEmpty
        {
            get { return this.Equals(Empty); }
        }

        /// <summary>
        /// An empty AABB - where the minimum and maximum coordinates are at opposing infinities.
        /// </summary>
        public static MinMaxAABB Empty
        {
            get { return new MinMaxAABB { Min = float3(float.PositiveInfinity), Max = float3(float.NegativeInfinity) }; }
        }

        /// <summary>
        /// Make this AABB into the smallest AABB that contains both this AABB, and another AABB.
        /// </summary>
        /// <param name="aabb">The other AABB</param>
        public void Encapsulate(MinMaxAABB aabb)
        {
            Min = math.min(Min, aabb.Min);
            Max = math.max(Max, aabb.Max);
        }

        /// <summary>
        /// Encapsulates the given AABB.
        /// </summary>
        /// <remarks>
        /// Modifies this AABB so that it contains the given AABB. If the given AABB is already contained by this AABB,
        /// then this AABB doesn't change.
        /// </remarks>
        /// <param name="point">AABB to encapsulate.</param>
        public void Encapsulate(float3 point)
        {
            Min = math.min(Min, point);
            Max = math.max(Max, point);
        }

        /// <summary>
        /// Make a MinMaxAABB from an AABB (an AABB which has a center and extents)
        /// </summary>
        /// <param name="aabb">The AABB to convert to a MinMaxAABB</param>
        /// <returns>Returns the new AABB.</returns>
        public static implicit operator MinMaxAABB(AABB aabb)
        {
            return new MinMaxAABB {Min = aabb.Center - aabb.Extents, Max = aabb.Center + aabb.Extents};
        }

        /// <summary>
        /// Make an AABB (an AABB which has a center and extents) from a MinMaxAABB
        /// </summary>
        /// <param name="aabb">The MinMaxAABB to convert to an AABB</param>
        /// <returns>Returns the new AABB.</returns>
        public static implicit operator AABB(MinMaxAABB aabb)
        {
            return new AABB { Center = (aabb.Min + aabb.Max) * 0.5F, Extents = (aabb.Max - aabb.Min) * 0.5F};
        }

        /// <summary>
        /// Determine whether this MinMaxAABB is the same as another MinMaxAABB
        /// </summary>
        /// <param name="other">The MinMaxAABB to compare against.</param>
        /// <returns>True if the MinMaxAABB is the same as the other parameter.</returns>
        public bool Equals(MinMaxAABB other)
        {
            return Min.Equals(other.Min) && Max.Equals(other.Max);
        }
    }
}
