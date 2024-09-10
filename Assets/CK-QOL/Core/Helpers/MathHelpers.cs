using UnityEngine;

namespace CK_QOL.Core.Helpers
{
    /// <summary>
    ///     Provides mathematical helper methods for various calculations related to game objects.
    /// </summary>
    internal static class MathHelpers
	{
        /// <summary>
        ///     Determines whether a target position is within a specified distance from a given position.
        /// </summary>
        /// <param name="position">The starting position to measure from.</param>
        /// <param name="target">The target position to measure the distance to.</param>
        /// <param name="distance">The maximum allowed distance between the starting position and the target position.</param>
        /// <returns>
        ///     <see langword="true" /> if the target is within the specified distance from the given position;
        ///		otherwise, <see langword="false" />.
        /// </returns>
        /// <seealso cref="Vector3.Distance(Vector3, Vector3)" />
        internal static bool IsInRange(Vector3 position, Vector3 target, float distance)
		{
			return Vector3.Distance(position, target) < distance;
		}
	}
}