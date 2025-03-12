using UnityEngine;

namespace Unity.Physics.Authoring
{
    /// <summary>
    /// Describes how a rigid body will be simulated in the run-time.
    /// </summary>
    public enum BodyMotionType
    {
        /// <summary>
        /// The physics solver will move the rigid body and handle its collision response with other bodies, based on its physical properties.
        /// </summary>
        Dynamic,
        /// <summary>
        /// The physics solver will move the rigid body according to its velocity, but it will be treated as though it has infinite mass.
        /// It will generate a collision response with any rigid bodies that lie in its path of motion, but will not be affected by them.
        /// </summary>
        Kinematic,
        /// <summary>
        /// The physics solver will not move the rigid body.
        /// Any transformations applied to it will be treated as though it is teleporting.
        /// </summary>
        Static
    }

    /// <summary>
    /// Describes how a rigid body's motion in its graphics representation should be smoothed when
    /// the rendering framerate is greater than the fixed step rate used by physics.
    /// </summary>
    public enum BodySmoothing
    {
        /// <summary>
        /// The body's graphics representation will display its current position and orientation from the perspective of the physics solver.
        /// </summary>
        None,
        /// <summary>
        /// The body's graphics representation will display a smooth result between the two most recent physics simulation ticks.
        /// The result is one tick behind, but will not mis-predict the body's position and orientation.
        /// However, it can make the body appear as if it changes direction before making contact with other bodies, particularly when the physics tick rate is low.
        /// See <seealso cref="GraphicsIntegration.GraphicalSmoothingUtility.Interpolate"/> for details.
        /// </summary>
        Interpolation,
        /// <summary>
        /// The body's graphics representation will display a smooth result by projecting into the future based on its current velocity.
        /// The result is thus up-to-date, but can mis-predict the body's position and orientation since any future collision response has not yet been resolved.
        /// See <seealso cref="GraphicsIntegration.GraphicalSmoothingUtility.Extrapolate"/> for details.
        /// </summary>
        Extrapolation
    }
}
