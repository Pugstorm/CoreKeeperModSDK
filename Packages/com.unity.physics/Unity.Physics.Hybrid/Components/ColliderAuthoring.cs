using System.ComponentModel;
using UnityEngine;

namespace Unity.Physics.Authoring
{
    /// <summary>
    /// Shape types supported in the collider baking systems.
    /// </summary>
    public enum ShapeType
    {
        /// <summary>   Box shape type </summary>
        Box        =  0,
        /// <summary>   Capsule shape type </summary>
        Capsule    =  1,
        /// <summary>   Sphere shape type </summary>
        Sphere     =  2,
        /// <summary>   Cylinder shape type </summary>
        Cylinder   =  3,
        /// <summary>   Plane shape type </summary>
        Plane      =  4,

        // extra space to accommodate other possible primitives in the future

        /// <summary>   Convex hull shape type </summary>
        ConvexHull = 30,
        /// <summary>   Mesh shape type </summary>
        Mesh       = 31
    }
}
