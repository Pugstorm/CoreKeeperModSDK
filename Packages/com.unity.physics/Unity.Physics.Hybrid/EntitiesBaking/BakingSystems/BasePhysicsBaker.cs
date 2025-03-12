using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Unity.Physics.Authoring
{
    /// <summary>
    /// An empty component that is used to indicate if the root of a compound collider has been baked
    /// </summary>
    [TemporaryBakingType]
    public struct PhysicsRootBaked : IComponentData {}

    /// <summary>
    /// Component that specifies data relating to compound colliders and blobs. Note that all colliders will have this
    /// component. The presence of this component indicates that this collider is a root of a collider, which may or may
    /// not be a compound.
    /// </summary>
    [BakingType]
    public struct PhysicsCompoundData : IComponentData
    {
        /// <summary> A hash associated with a compound collider </summary>
        public Unity.Entities.Hash128 Hash;

        /// <summary> Instance ID of the GameObject associated with the body </summary>
        public int ConvertedBodyInstanceID;

        /// <summary> Indicates if a blob is associated to a collider </summary>
        public bool AssociateBlobToBody;

        /// <summary> A flag to indicate that calculation of a compound blob should be deferred </summary>
        public bool DeferredCompoundBlob;

        /// <summary> A flag to indicate that a compound collider has been calculated </summary>
        public bool RegisterBlob;
    }

    internal abstract class BasePhysicsBaker<T> : Baker<T> where T : Component
    {
        bool HasNonIdentityScale(Transform bodyTransform)
        {
            return ((float4x4)(bodyTransform.transform.localToWorldMatrix)).HasNonIdentityScale();
        }

        /// <summary>
        /// Post processing set up of this entity's transformation.
        /// </summary>
        /// <param name="bodyTransform">Transformation of this entity.</param>
        /// <param name="motionType">Motion type of this entity. Default is BodyMotionType.Static.</param>
        protected void PostProcessTransform(Transform bodyTransform, BodyMotionType motionType = BodyMotionType.Static)
        {
            Transform transformParent = bodyTransform.parent;
            bool haveParentEntity    = transformParent != null;
            bool haveBakedTransform  = IsStatic();
            float4x4 localToWorld = bodyTransform.localToWorldMatrix;
            bool hasShear = localToWorld.HasShear();
            bool hasNonIdentityScale = HasNonIdentityScale(bodyTransform);
            bool unparent = motionType != BodyMotionType.Static || hasShear || hasNonIdentityScale || !haveParentEntity || haveBakedTransform;

            if (unparent)
            {
                // Use ManualOverride to unparent the entity.
                // In this mode we can manually produce the transform components and prevent
                // any child/parent entity relationships from being automatically created.
                var entity = GetEntity(TransformUsageFlags.ManualOverride);

                var rigidBodyTransform = Math.DecomposeRigidBodyTransform(localToWorld);
                var compositeScale = math.mul(
                    math.inverse(new float4x4(rigidBodyTransform)),
                    localToWorld
                );

                AddComponent(entity, new LocalToWorld { Value = localToWorld });

                var uniformScale = 1.0f;
                if (hasShear || localToWorld.HasNonUniformScale())
                {
                    AddComponent(entity, new PostTransformMatrix { Value = compositeScale });
                }
                else
                {
                    uniformScale = bodyTransform.lossyScale[0];
                }

                LocalTransform transform = LocalTransform.FromPositionRotationScale(rigidBodyTransform.pos,
                    rigidBodyTransform.rot, uniformScale);
                AddComponent(entity, transform);
            }
        }
    }
}
