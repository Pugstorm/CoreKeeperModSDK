using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Unity.Scenes.Editor.Tests
{
    [DisableAutoCreation]
    class AssignTransformUsageBaker : Baker<UnityEngine.Transform>
    {
        internal static readonly Dictionary<GameObject, TransformUsageFlags> Flags = new();
        internal static readonly Dictionary<GameObject, RuntimeTransformComponentFlags> AddManualComponents = new();

        public override void Bake(UnityEngine.Transform authoring)
        {
            Entity entity;
            if (Flags.TryGetValue(authoring.gameObject, out var flags))
            {
                entity = GetEntity(authoring, flags);

                if (AddManualComponents.TryGetValue(authoring.gameObject, out var manualComponents))
                {
                    if ((manualComponents & RuntimeTransformComponentFlags.LocalToWorld) != 0)
                        AddComponent<LocalToWorld>(entity, new LocalToWorld {Value = authoring.localToWorldMatrix});

                    if ((manualComponents & RuntimeTransformComponentFlags.RequestParent) != 0)
                    {
                        if ((manualComponents & RuntimeTransformComponentFlags.LocalTransform) != 0)
                            AddComponent<LocalTransform>(entity,
                                LocalTransform.FromPositionRotationScale(authoring.localPosition, authoring.localRotation,
                                    authoring.localScale.x));

                        AddComponent<Parent>(entity, new Parent
                        {
                            Value = GetEntity(authoring.parent, TransformUsageFlags.None)
                        });
                    }
                    else
                    {
                        if ((manualComponents & RuntimeTransformComponentFlags.LocalTransform) != 0)
                            AddComponent<LocalTransform>(entity,
                                LocalTransform.FromPositionRotationScale(authoring.position, authoring.rotation,
                                    authoring.lossyScale.x));
                    }
                }
            }
        }
    }

    [Flags]
    public enum ExpectedConvertedTransformResults
    {
        Nothing                     = 0,
        HasLocalToWorld             = 1,
        HasLocalTransform           = 1 << 1,
        HasPostTransformMatrix      = 1 << 2,
        HasParent                   = 1 << 3,
        HasWorldSpaceData           = 1 << 4,
        HasNonUniformScale          = 1 << 5,
        HasValidRuntimeParent       = 1 << 6
    }

    public static class TestTransformUsageFlagsHelper
    {
        public static bool Has(ExpectedConvertedTransformResults expectedDescription, ExpectedConvertedTransformResults flag)
        {
            return (expectedDescription & flag) != 0;
        }

        const float k_Tolerance = 0.001f;

        static bool AreEqual(float v1, float v2)
        {
            return math.abs(v1 - v2) <= k_Tolerance;
        }

        static bool AreEqual(in float3 v1, in float3 v2)
        {
            return math.abs(v1.x - v2.x) <= k_Tolerance &&
                math.abs(v1.y - v2.y) <= k_Tolerance &&
                math.abs(v1.z - v2.z) <= k_Tolerance;
        }

        static bool AreEqual(in quaternion v1, in quaternion v2)
        {
            return AreEqual(v1.value, v2.value);
        }

        static bool AreEqual(in float4 v1, in float4 v2)
        {
            return math.abs(v1.x - v2.x) <= k_Tolerance &&
                math.abs(v1.y - v2.y) <= k_Tolerance &&
                math.abs(v1.w - v2.w) <= k_Tolerance &&
                math.abs(v1.z - v2.z) <= k_Tolerance;
        }

        static bool AreEqual(in float4x4 v1, in float4x4 v2)
        {
            return AreEqual(v1.c0, v2.c0) &&
                AreEqual(v1.c1, v2.c1) &&
                AreEqual(v1.c2, v2.c2) &&
                AreEqual(v1.c3, v2.c3);
        }

        public static void VerifyBakedTransformData(EntityManager entityManager, ExpectedConvertedTransformResults expectedDescription, Transform transform, TransformAuthoring authoring, Entity entity, Entity parentEntity)
        {
            // Check TransformAuthoring values
            var localPositionRef = transform.localPosition;
            var localRotationRef = transform.localRotation;
            var localScaleRef = transform.localScale;

            Assert.IsTrue(AreEqual((float3) localPositionRef, authoring.LocalPosition));
            Assert.IsTrue(AreEqual((quaternion) localRotationRef, authoring.LocalRotation));
            Assert.IsTrue(AreEqual((float3) localScaleRef, authoring.LocalScale));

            Assert.IsTrue(AreEqual((float3) transform.position, authoring.Position));
            Assert.IsTrue(AreEqual((quaternion) transform.rotation, authoring.Rotation));
            Assert.IsTrue(AreEqual((float4x4) transform.localToWorldMatrix, authoring.LocalToWorld));

            Assert.AreEqual(parentEntity, authoring.AuthoringParent);

            if (Has(expectedDescription, ExpectedConvertedTransformResults.HasValidRuntimeParent))
            {
                Assert.AreEqual(parentEntity, authoring.RuntimeParent);
            }
            else
            {
                Assert.AreEqual(default(Entity), authoring.RuntimeParent);
            }

            // Check Entity Components and Values
            bool expectsLocalToWorld = Has(expectedDescription, ExpectedConvertedTransformResults.HasLocalToWorld);
            Assert.AreEqual(expectsLocalToWorld, entityManager.HasComponent<LocalToWorld>(entity));
            if (expectsLocalToWorld)
            {
                // Check the values are the expected ones
                var data = entityManager.GetComponentData<LocalToWorld>(entity);
                Assert.IsTrue(AreEqual(authoring.LocalToWorld, data.Value));
            }

            bool expectsLocalTransform = Has(expectedDescription, ExpectedConvertedTransformResults.HasLocalTransform);
            Assert.AreEqual(expectsLocalTransform, entityManager.HasComponent<LocalTransform>(entity));
            if (expectsLocalTransform)
            {
                // Check the values are the expected ones
                var data = entityManager.GetComponentData<LocalTransform>(entity);
                if (Has(expectedDescription, ExpectedConvertedTransformResults.HasWorldSpaceData))
                {
                    Assert.IsTrue(AreEqual(authoring.Position, data.Position));
                    Assert.IsTrue(AreEqual(authoring.Rotation, data.Rotation));

                    if (Has(expectedDescription, ExpectedConvertedTransformResults.HasNonUniformScale))
                    {
                        Assert.IsTrue(AreEqual(1f, data.Scale));
                    }
                    else
                    {
                        Assert.IsTrue(AreEqual(((float3)transform.lossyScale).x, data.Scale));
                    }
                }
                else
                {
                    Assert.IsTrue(AreEqual( authoring.LocalPosition, data.Position));
                    Assert.IsTrue(AreEqual(authoring.LocalRotation, data.Rotation));
                    if (Has(expectedDescription, ExpectedConvertedTransformResults.HasNonUniformScale))
                    {
                        Assert.IsTrue(AreEqual(1f, data.Scale));
                    }
                    else
                    {
                        Assert.IsTrue(AreEqual(localScaleRef.x, data.Scale));
                    }
                }
            }

            bool expectsPostTransformMatrix = Has(expectedDescription, ExpectedConvertedTransformResults.HasPostTransformMatrix);
            Assert.AreEqual(expectsPostTransformMatrix, entityManager.HasComponent<PostTransformMatrix>(entity));
            if (expectsPostTransformMatrix)
            {
                // Check the values are the expected ones
                var data = entityManager.GetComponentData<PostTransformMatrix>(entity);

                // If a PostTransformMatrix is requested, then all the scale must be in the matrix, even if it is uniform
                if (Has(expectedDescription, ExpectedConvertedTransformResults.HasWorldSpaceData))
                {
                    Assert.IsTrue(AreEqual(float4x4.Scale(transform.lossyScale), data.Value));
                }
                else
                {
                    Assert.IsTrue(AreEqual(float4x4.Scale(localScaleRef), data.Value));
                }
            }

            bool expectsParent = Has(expectedDescription, ExpectedConvertedTransformResults.HasParent);
            Assert.AreEqual(expectsParent, entityManager.HasComponent<Parent>(entity));
            if (expectsParent)
            {
                // Check the values are the expected ones
                var data = entityManager.GetComponentData<Parent>(entity);
                if (Has(expectedDescription, ExpectedConvertedTransformResults.HasValidRuntimeParent))
                {
                    Assert.AreEqual(authoring.RuntimeParent, data.Value);
                }
                else
                {
                    Assert.AreEqual(default(Entity), data.Value);
                }
            }
        }
    }
}