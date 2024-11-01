using System;
using Unity.Mathematics;

namespace Unity.Entities
{
    /// <summary>
    /// TransformAuthoring is automatically created for every single entity created during baking.
    /// It gives access to the complete data the authoring Transform carries, the components exist only during baking.
    /// The TransformAuthoring component is stripped out automatically at runtime.
    ///
    /// This can be used by systems that want to access the original authoring transform data in baking systems, without relying on the runtime transforms existing.
    /// In many cases we want to bake our authoring state into more optimal runtime representations.
    /// For example Unity.Physics bakes all colliders that may be children in the game object hierarchy into the entity containing the rigidbody.
    /// Hence the game objects with those colliders might not be required to be entities at runtime at all,
    /// but in baking systems we need to be able to access the TransformAuthoring data to correctly bake the colliders based on their positions relative to the parent rigidbody.
    /// </summary>
    [BakingType]
    public struct TransformAuthoring : IComponentData, IEquatable<TransformAuthoring>
    {
        // Local TRS values, relative to authoring parent.
        /// <summary>
        /// Local position as found in the Transform Component.
        /// </summary>
        public float3                LocalPosition;
        /// <summary>
        /// Local rotation as found in the Transform Component.
        /// </summary>
        public quaternion            LocalRotation;
        /// <summary>
        /// Local scale as found in the Transform Component.
        /// </summary>
        public float3                LocalScale;

        // World space TRS values.
        /// <summary>
        /// World space position as found in the Transform Component.
        /// </summary>
        public float3                Position;
        /// <summary>
        /// World space rotation as found in the Transform Component.
        /// </summary>
        public quaternion            Rotation;
        /// <summary>
        /// Local to world matrix as found in the Transform Component.
        /// </summary>
        public float4x4              LocalToWorld;

        // The authoring transform parent.
        /// <summary>
        /// Authoring parent entity.
        /// </summary>
        public Entity                AuthoringParent;

        // The recommended RuntimeParent & transform usage based on resolved hierarchical transform usage.
        /// <summary>
        /// Runtime parent entity.
        /// </summary>
        /// <remarks>The RuntimeParent entity doesn't always match with the AuthoringParent entity, because it depends on the value of RuntimeTransformUsage.</remarks>
        public Entity                RuntimeParent;
        /// <summary>
        /// <see cref="TransformUsageFlags"/> value applied to this entity.
        /// </summary>
        internal RuntimeTransformComponentFlags   RuntimeTransformUsage;

        /// <summary>
        /// Version number to detect changes to this component.
        /// </summary>
        /// <remarks>The version number is increased any time there is a change to this component.</remarks>
        public uint                  ChangeVersion;

        /// <summary>
        /// Compares two TransformAuthoring instances to determine if they are equal.
        /// </summary>
        /// <param name="other">A TransformAuthoring.</param>
        /// <returns>True if all the fields from the current instance and <paramref name="other"/> are equal.</returns>
        public bool Equals(TransformAuthoring other)
        {
            return LocalPosition.Equals(other.LocalPosition) && LocalRotation.Equals(other.LocalRotation) && LocalScale.Equals(other.LocalScale) && Position.Equals(other.Position) && Rotation.Equals(other.Rotation) && LocalToWorld.Equals(other.LocalToWorld) && AuthoringParent == other.AuthoringParent && RuntimeParent == other.RuntimeParent && RuntimeTransformUsage == other.RuntimeTransformUsage;
        }
    }

    /// <summary>
    /// Contains information to identify the parent of an additional entity.
    /// </summary>
    /// <remarks>This component has a <see cref="BakingTypeAttribute"/>.</remarks>
    [BakingType]
    public struct AdditionalEntityParent : IComponentData
    {
        /// <summary>
        /// Represents a primary entity that matches the GameObject that created the additional entity.
        /// </summary>
        public Entity Parent;
        /// <summary>
        /// Represents a unique Instance ID of the GameObject that created the additional entity.
        /// </summary>
        public int    ParentInstanceID;
    }
}
