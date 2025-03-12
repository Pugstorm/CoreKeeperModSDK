using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Physics.Authoring
{
    struct ColliderInstanceId : IEquatable<ColliderInstanceId>
    {
        public ColliderInstanceId(Hash128 blobDataHash, int authoringComponentId)
        {
            BlobDataHash = blobDataHash;
            AuthoringComponentId = authoringComponentId;
        }

        readonly Hash128 BlobDataHash;
        readonly int AuthoringComponentId;

        public bool Equals(ColliderInstanceId other) =>
            BlobDataHash.Equals(other.BlobDataHash) && AuthoringComponentId == other.AuthoringComponentId;

        public override bool Equals(object obj) => obj is ColliderInstanceId other && Equals(other);

        public override int GetHashCode() =>
            (int)math.hash(new uint2((uint)BlobDataHash.GetHashCode(), (uint)AuthoringComponentId));

        public static bool operator==(ColliderInstanceId left, ColliderInstanceId right) => left.Equals(right);

        public static bool operator!=(ColliderInstanceId left, ColliderInstanceId right) => !left.Equals(right);
    }

    // structure with minimal data needed to incrementally convert a shape that is possibly part of a compound collider
    struct ColliderInstanceBaking : IEquatable<ColliderInstanceBaking>
    {
        public int AuthoringComponentId;
        public int ConvertedAuthoringInstanceID; // Instance ID of the GameObject with the Collider
        public int ConvertedBodyInstanceID;      // Instance ID of the GameObject with the body
        public Entity BodyEntity;
        public Entity ShapeEntity;
        public Entity ChildEntity;
        public RigidTransform BodyFromShape;
        public Hash128 Hash;

        public static RigidTransform GetCompoundFromChild(Transform shape, Transform body)
        {
            if (shape == body)
            {
                return RigidTransform.identity;
            }
            // else:

            // if body has pure uniform scale, it doesn't get baked into the colliders and we need to consider it when computing the
            // shape's relative transform.
            var bodyLocalToWorld = (float4x4)body.transform.localToWorldMatrix;
            var shapeLocalToWorld = (float4x4)shape.transform.localToWorldMatrix;

            if (bodyLocalToWorld.HasShear() || bodyLocalToWorld.HasNonUniformScale())
            {
                var worldFromBody = Math.DecomposeRigidBodyTransform(bodyLocalToWorld);
                var worldFromShape = Math.DecomposeRigidBodyTransform(shapeLocalToWorld);
                var relTransform = math.mul(math.inverse(worldFromBody), worldFromShape);
                return relTransform;
            }
            else
            {
                var worldFromBody = bodyLocalToWorld;
                var worldFromShape = Math.DecomposeRigidBodyTransform(shapeLocalToWorld);
                var relTransform = math.mul(math.inverse(new float4x4(worldFromBody)), new float4x4(worldFromShape));
                return Math.DecomposeRigidBodyTransform(relTransform);
            }
        }

        public bool Equals(ColliderInstanceBaking other)
        {
            return AuthoringComponentId == other.AuthoringComponentId
                && ConvertedAuthoringInstanceID == other.ConvertedAuthoringInstanceID
                && ConvertedBodyInstanceID == other.ConvertedBodyInstanceID
                && BodyEntity.Equals(other.BodyEntity)
                && ShapeEntity.Equals(other.ShapeEntity)
                && BodyFromShape.Equals(other.BodyFromShape)
                && Hash.Equals(other.Hash);
        }

        public override bool Equals(object obj) => obj is ColliderInstanceBaking other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = AuthoringComponentId;
                hashCode = (hashCode * 397) ^ ConvertedAuthoringInstanceID;
                hashCode = (hashCode * 397) ^ ConvertedBodyInstanceID;
                hashCode = (hashCode * 397) ^ BodyEntity.GetHashCode();
                hashCode = (hashCode * 397) ^ ShapeEntity.GetHashCode();
                hashCode = (hashCode * 397) ^ BodyFromShape.GetHashCode();
                hashCode = (hashCode * 397) ^ Hash.GetHashCode();
                return hashCode;
            }
        }

        public ColliderInstanceId ToColliderInstanceId() => new ColliderInstanceId(Hash, AuthoringComponentId);
    }

    internal struct ShapeComputationDataBaking
    {
        public ColliderInstanceBaking Instance;

        public uint ForceUniqueIdentifier;
        public Material Material;
        public CollisionFilter CollisionFilter;

        // TODO: use union to share the same memory zone for each different type
        public ShapeType ShapeType;
        public BoxGeometry BoxProperties;
        public CapsuleGeometry CapsuleProperties;
        public CylinderGeometry CylinderProperties;
        public SphereGeometry SphereProperties;
        public float3x4 PlaneVertices;
        public ConvexInputBaking ConvexHullProperties;
        public MeshInputBaking MeshProperties;

        public float4x4 BodyFromShape => new float4x4(Instance.BodyFromShape);
    }

    internal struct MeshInputBaking
    {
        public int VerticesStart;
        public int VertexCount;
        public int TrianglesStart;
        public int TriangleCount;
        public CollisionFilter Filter;
        public Material Material;
    }

    internal struct ConvexInputBaking
    {
        public ConvexHullGenerationParameters GenerationParameters;
        public int PointsStart;
        public int PointCount;
        public CollisionFilter Filter;
        public Material Material;
    }
}
