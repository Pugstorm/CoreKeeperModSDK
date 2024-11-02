using System;
using static Unity.Mathematics.math;

namespace Unity.Mathematics
{
    ///<summary>
    /// An AABB, or axis-aligned bounding box, is a simple bounding shape, typically used for quick determination
    /// of whether two objects may intersect. If the AABBs that enclose each object do not intersect, then logically
    /// the objects also may not intersect. This AABB struct is formulated as a center and a size, rather than as a
    /// minimum and maximum coordinate. Therefore, there may be issues at extreme coordinates, such as FLT_MAX or infinity.
    ///</summary>
    [Serializable]
    public partial struct AABB
    {
        /// <summary>
        /// The location of the center of the AABB
        /// </summary>
        public float3 Center;

        /// <summary>
        /// A 3D vector from the center of the AABB, to the corner of the AABB with maximum XYZ values
        /// </summary>
        public float3 Extents;

        /// <summary>
        /// The size of the AABB
        /// </summary>
        /// <returns>The size of the AABB, in three dimensions. All three dimensions must be positive.</returns>
        public float3 Size { get { return Extents * 2; } }

        /// <summary>
        /// The minimum coordinate of the AABB
        /// </summary>
        /// <returns>The minimum coordinate of the AABB, in three dimensions.</returns>
        public float3 Min { get { return Center - Extents; } }

        /// <summary>
        /// The maximum coordinate of the AABB
        /// </summary>
        /// <returns>The maximum coordinate of the AABB, in three dimensions.</returns>
        public float3 Max { get { return Center + Extents; } }

        /// <summary>Returns a string representation of the AABB.</summary>
        /// <returns>a string representation of the AABB.</returns>
        public override string ToString()
        {
            return $"AABB(Center:{Center}, Extents:{Extents}";
        }

        /// <summary>
        /// Returns whether a point in 3D space is contained by the AABB, or not. Because math is done
        /// to compute the minimum and maximum coordinates of the AABB, overflow is possible for extreme values.
        /// </summary>
        /// <param name="point">The point to check for whether it's contained by the AABB</param>
        /// <returns>True if the point is contained, and false if the point is not contained by the AABB.</returns>
        public bool Contains(float3 point) => all(point >= Min & point <= Max);

        /// <summary>
        /// Returns whether the AABB contains another AABB completely. Because math is done
        /// to compute the minimum and maximum coordinates of the AABBs, overflow is possible for extreme values.
        /// </summary>
        /// <param name="b">The AABB to check for whether it's contained by this AABB</param>
        /// <returns>True if the AABB is contained, and false if it is not.</returns>
        public bool Contains(AABB b) => all((Min <= b.Min) & (Max >= b.Max));

        static float3 RotateExtents(float3 extents, float3 m0, float3 m1, float3 m2)
        {
            return abs(m0 * extents.x) + abs(m1 * extents.y) + abs(m2 * extents.z);
        }

        /// <summary>
        /// Transforms an AABB by a 4x4 transformation matrix, and returns a new AABB that contains the transformed
        /// AABB completely.
        /// </summary>
        /// <param name="transform">The 4x4 transformation matrix, with which to transform the AABB</param>
        /// <param name="localBounds">The AABB to transform by the matrix</param>
        /// <returns>A new AABB that contains the transformed AABB.</returns>
        public static AABB Transform(float4x4 transform, AABB localBounds)
        {
            AABB transformed;
            transformed.Extents = RotateExtents(localBounds.Extents, transform.c0.xyz, transform.c1.xyz, transform.c2.xyz);
            transformed.Center = math.transform(transform, localBounds.Center);
            return transformed;
        }

        /// <summary>
        /// Determines the squared distance from a point to the nearest point to it that is contained by an AABB.
        /// </summary>
        /// <param name="point">The point to find the distance from</param>
        /// <returns>The squared distance from the point to the nearest point to it that is contained by the AABB.</returns>
        public float DistanceSq(float3 point)
        {
            return lengthsq(max(abs(point - Center), Extents) - Extents);
        }
    }
}
