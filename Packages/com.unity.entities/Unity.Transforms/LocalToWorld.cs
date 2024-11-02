using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Transforms
{
    /// <summary>
    /// The local-to-world transformation matrix for an entity
    /// </summary>
    /// <remarks>
    /// This matrix is primarily intended for consumption by the rendering systems.
    ///
    /// The matrix value is generally updated automatically by <see cref="LocalToWorldSystem"/> based on the entity's
    /// <see cref="LocalTransform"/>.
    ///
    /// This component value may be out of date or invalid while the <see cref="SimulationSystemGroup"/> is running; it
    /// is only updated when the <see cref="TransformSystemGroup"/> runs).
    /// It may also contain additional offsets applied for graphical smoothing purposes.
    /// Therefore, while the <see cref="LocalToWorld"/> component may be useful as a fast approximation of an entity's
    /// world-space transformation when its latency is acceptable, it should not be relied one when an accurate,
    /// up-to-date world transform is needed for simulation purposes. In those cases, use the
    /// <see cref="TransformHelpers.ComputeWorldTransformMatrix"/> method.
    ///
    /// If a system writes to this component directly outside of the Entities transform systems using a <see cref="WriteGroupAttribute"/>,
    /// <see cref="LocalToWorldSystem"/> will not overwrite this entity's matrix. In this case, the writing system is
    /// also responsible for applying the entity's <see cref="PostTransformMatrix"/> component (if present).
    /// </remarks>
    [Serializable]
    public struct LocalToWorld : IComponentData
    {
        /// <summary>
        /// The transformation matrix
        /// </summary>
        public float4x4 Value;

        /// <summary>
        /// The "right" vector, in the entity's world-space.
        /// </summary>
        public float3 Right => new float3(Value.c0.x, Value.c0.y, Value.c0.z);

        /// <summary>
        /// The "up" vector, in the entity's world-space.
        /// </summary>
        public float3 Up => new float3(Value.c1.x, Value.c1.y, Value.c1.z);

        /// <summary>
        /// The "forward" vector, in the entity's world-space.
        /// </summary>
        public float3 Forward => new float3(Value.c2.x, Value.c2.y, Value.c2.z);

        /// <summary>
        /// The "entity's" position in world-space.
        /// </summary>
        public float3 Position => new float3(Value.c3.x, Value.c3.y, Value.c3.z);

        /// <summary>
        /// The "entity's" orientation in world-space.
        /// </summary>
        public quaternion Rotation => new quaternion(math.orthonormalize(new float3x3(Value)));
    }
}
