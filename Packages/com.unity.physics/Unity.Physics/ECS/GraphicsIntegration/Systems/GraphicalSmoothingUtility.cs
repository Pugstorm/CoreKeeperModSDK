using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Physics.Extensions;
using Unity.Transforms;

namespace Unity.Physics.GraphicsIntegration
{
    /// <summary>
    /// Utility functions for smoothing the motion of rigid bodies' graphical representations when physics steps at a lower frequency than rendering.
    /// </summary>
    public static class GraphicalSmoothingUtility
    {
        /// <summary>
        /// Compute a simple extrapolated transform for the graphical representation of a rigid body using its <c>currentTransform</c> and <c>currentVelocity</c>.
        /// Because bodies' motion may change the next time physics steps (e.g., as the result of a collision), using this method can mis-predict their future locations, causing them to appear to snap into place the next time physics steps.
        /// It generally results in a solid contact and kick from collisions, albeit with some interpenetration, as well as slight jitter on small, smooth velocity changes (e.g., the top of a parabolic arc).
        /// <code>
        /// Simulation:                Graphical Extrapolation:
        ///
        ///               O (t=2)                     O (t=2)
        /// (t=0) O      /               (t=0) O     o
        ///        \    /                       o   o
        ///         \  O (t=1)                   o O (t=1)
        /// _________\/_________       ___________o________
        ///                                        o
        /// </code>
        /// </summary>
        /// <param name="currentTransform">The transform of the rigid body after physics has stepped (i.e., the value of its <see cref="Unity.Transforms.LocalTransform"/> components).</param>
        /// <param name="currentVelocity">The velocity of the rigid body after physics has stepped (i.e., the value of its <see cref="PhysicsVelocity"/> component).</param>
        /// <param name="mass">The body's <see cref="PhysicsMass"/> component.</param>
        /// <param name="timeAhead">A value indicating how many seconds the current elapsed time for graphics is ahead of the elapsed time when physics last stepped.</param>
        /// <returns>
        /// An extrapolated transform for a rigid body's graphical representation, suitable for constructing its <c>LocalToWorld</c> matrix before rendering.
        /// See also <seealso cref="BuildLocalToWorld"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RigidTransform Extrapolate(
            in RigidTransform currentTransform, in PhysicsVelocity currentVelocity, in PhysicsMass mass, float timeAhead
        )
        {
            var newTransform = currentTransform;
            currentVelocity.Integrate(mass, timeAhead, ref newTransform.pos, ref newTransform.rot);
            return newTransform;
        }

        /// <summary>
        /// Compute a simple interpolated transform for the graphical representation of a rigid body between <c>previousTransform</c> and <c>currentTransform</c>.
        /// Because bodies' motion is often deflected during a physics step when there is a contact event, using this method can make bodies appear to change direction before a collision is actually visible.
        /// <code>
        /// Simulation:                Graphical Interpolation:
        ///
        ///               O (t=2)                     O (t=2)
        /// (t=0) O      /               (t=0) O     o
        ///        \    /                        o  o
        ///         \  O (t=1)                     O (t=1)
        /// _________\/_________       ____________________
        ///
        /// </code>
        /// (Note that for cartoons, an animator would use squash and stretch to force a body to make contact even if it is technically not hitting on a specific frame.)
        /// See <see cref="InterpolateUsingVelocity"/> for an alternative approach.
        /// </summary>
        /// <param name="previousTransform">The transform of the rigid body before physics stepped.</param>
        /// <param name="currentTransform">The transform of the rigid body after physics has stepped (i.e., the value of its <see cref="Unity.Transforms.LocalTransform"/> components).</param>
        /// <param name="normalizedTimeAhead">A value in the range [0, 1] indicating how many seconds the current elapsed time for graphics is ahead of the elapsed time when physics last stepped, as a proportion of the fixed timestep used by physics.</param>
        /// <returns>
        /// An interpolated transform for a rigid body's graphical representation, suitable for constructing its <c>LocalToWorld</c> matrix before rendering.
        /// See also <seealso cref="BuildLocalToWorld"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RigidTransform Interpolate(
            in RigidTransform previousTransform,
            in RigidTransform currentTransform,
            float normalizedTimeAhead
        )
        {
            return new RigidTransform(
                math.nlerp(previousTransform.rot, currentTransform.rot, normalizedTimeAhead),
                math.lerp(previousTransform.pos, currentTransform.pos, normalizedTimeAhead)
            );
        }

        /// <summary>
        /// Compute an interpolated transform for the graphical representation of a rigid body between its previous transform and current transform, integrating forward using an interpolated velocity value.
        /// This method tries to achieve a compromise between the visual artifacts of <see cref="Interpolate"/> and <see cref="Extrapolate"/>.
        /// While integrating forward using <c>previousVelocity</c> alone would exhibit behavior similar to <see cref="Extrapolate"/>, doing so in conjunction with <c>previousTransform</c> results in smoother motion for small velocity changes.
        /// Collisions can still appear premature when physics has a low tick rate, however, as when using <see cref="Interpolate"/>.
        /// </summary>
        /// <param name="previousTransform">The transform of the rigid body before physics stepped.</param>
        /// <param name="previousVelocity">The velocity of the rigid body before physics stepped.</param>
        /// <param name="currentVelocity">The velocity of the rigid body after physics has stepped (i.e., the value of its <see cref="PhysicsVelocity"/> component).</param>
        /// <param name="mass">The body's <see cref="PhysicsMass"/> component.</param>
        /// <param name="timeAhead">A value indicating how many seconds the current elapsed time for graphics is ahead of the elapsed time when physics last stepped.</param>
        /// <param name="normalizedTimeAhead">A value in the range [0, 1] indicating how many seconds the current elapsed time for graphics is ahead of the elapsed time when physics last stepped, as a proportion of the fixed timestep used by physics.</param>
        /// <returns>
        /// An interpolated transform for a rigid body's graphical representation, suitable for constructing its <c>LocalToWorld</c> matrix before rendering.
        /// See also <seealso cref="BuildLocalToWorld"/>.
        /// </returns>
        public static RigidTransform InterpolateUsingVelocity(
            in RigidTransform previousTransform,
            in PhysicsVelocity previousVelocity,
            in PhysicsVelocity currentVelocity,
            in PhysicsMass mass,
            float timeAhead, float normalizedTimeAhead
        )
        {
            var newTransform = previousTransform;

            // Partially integrate with old velocities
            previousVelocity.Integrate(mass, timeAhead * (1f - normalizedTimeAhead), ref newTransform.pos, ref newTransform.rot);
            // Blend the previous and current velocities
            var interpolatedVelocity = new PhysicsVelocity
            {
                Linear = math.lerp(previousVelocity.Linear, currentVelocity.Linear, normalizedTimeAhead),
                Angular = math.lerp(previousVelocity.Angular, currentVelocity.Angular, normalizedTimeAhead)
            };
            // Then finish integration with blended velocities
            interpolatedVelocity.Integrate(mass, timeAhead * normalizedTimeAhead, ref newTransform.pos, ref newTransform.rot);

            return newTransform;
        }

        /// <summary>
        /// Construct a <c>LocalToWorld</c> matrix for a rigid body's graphical representation.
        /// </summary>
        /// <param name="i">The index of the rigid body in the chunk.<br/>
        ///                 Used to look up the body's <see cref="PostTransformMatrix"/> component in the provided
        ///                 <c>postTransformMatrices</c> array, if any (see <c>hasPostTransformMatrix</c> parameter).</param>
        /// <param name="transform">The body's world space transform.</param>
        /// <param name="uniformScale">The body's uniform scale.</param>
        /// <param name="hasPostTransformMatrix"><c>true</c> if the rigid body has a <see cref="PostTransformMatrix"/> component; otherwise, <c>false</c>.</param>
        /// <param name="postTransformMatrices">The array of post transform matrices in the chunk.</param>
        /// <returns>A LocalToWorld matrix to use in place of those produced by default.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        public static LocalToWorld BuildLocalToWorld(
            int i, RigidTransform transform,
            float uniformScale,
            bool hasPostTransformMatrix,
            NativeArray<PostTransformMatrix> postTransformMatrices
        )
        {
            var tr = new float4x4(transform);
            if (hasPostTransformMatrix)
            {
                var m = postTransformMatrices[i].Value;
                return new LocalToWorld { Value = math.mul(new float4x4(transform), m) };
            }
            else if (uniformScale != 0)
                return new LocalToWorld { Value = float4x4.TRS(transform.pos, transform.rot, new float3(uniformScale)) };
            else
                return new LocalToWorld { Value = new float4x4(transform) };
        }
    }
}
