using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;

namespace Unity.Physics.Tests.Authoring
{
    class HashableShapeInputs_UnitTests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() => HashUtility.Initialize();

        static UnityEngine.Mesh MeshA => UnityEngine.Resources.GetBuiltinResource<UnityEngine.Mesh>("New-Plane.fbx");
        static UnityEngine.Mesh MeshB => UnityEngine.Resources.GetBuiltinResource<UnityEngine.Mesh>("New-Cylinder.fbx");

        static Hash128 GetHash128_FromMesh(
            UnityEngine.Mesh mesh, float4x4 leafToBody,
            ConvexHullGenerationParameters convexHullGenerationParameters = default,
            Material material = default,
            CollisionFilter filter = default,
            float4x4 shapeFromBody = default,
            uint uniqueIdentifier = default,
            int[] includedIndices = default,
            float[] blendShapeWeights = default
        )
        {
            using (var allIncludedIndices = new NativeList<int>(0, Allocator.TempJob))
            using (var allBlendShapeWeights = new NativeList<float>(0, Allocator.TempJob))
            using (var indices = new NativeArray<int>(includedIndices ?? Array.Empty<int>(), Allocator.TempJob))
            using (var blendWeights = new NativeArray<float>(blendShapeWeights ?? Array.Empty<float>(), Allocator.TempJob))
                return HashableShapeInputs.GetHash128(
                    uniqueIdentifier, convexHullGenerationParameters, material, filter, shapeFromBody,
                    new NativeArray<HashableShapeInputs>(1, Allocator.Temp)
                    {
                        [0] = HashableShapeInputs.FromSkinnedMesh(
                            mesh, leafToBody,
                            indices,
                            allIncludedIndices,
                            blendWeights,
                            allBlendShapeWeights
                        )
                    }, allIncludedIndices.AsArray(), allBlendShapeWeights.AsArray()
                );
        }

        [Test]
        public void GetHash128_WhenBothUninitialized_IsEqual()
        {
            var a = HashableShapeInputs.GetHash128(default, default, default, default, default, default, default, default);
            var b = HashableShapeInputs.GetHash128(default, default, default, default, default, default, default, default);

            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void GetHash128_WhenBothDefault_IsEqual()
        {
            var a = HashableShapeInputs.GetHash128(
                0u, ConvexHullGenerationParameters.Default, Material.Default, CollisionFilter.Default, float4x4.identity,
                new NativeArray<HashableShapeInputs>(1, Allocator.Temp) { [0] = default },
                new NativeList<int>(0, Allocator.Temp).AsArray(),
                new NativeList<float>(0, Allocator.Temp).AsArray()
            );
            var b = HashableShapeInputs.GetHash128(
                0u, ConvexHullGenerationParameters.Default, Material.Default, CollisionFilter.Default, float4x4.identity,
                new NativeArray<HashableShapeInputs>(1, Allocator.Temp) { [0] = default },
                new NativeList<int>(0, Allocator.Temp).AsArray(),
                new NativeList<float>(0, Allocator.Temp).AsArray()
            );

            Assert.That(a, Is.EqualTo(b));
        }

        static readonly TestCaseData[] k_GetHash128TestCases =
        {
            new TestCaseData(
                1u, MeshA, ConvexHullGenerationParameters.Default, Material.Default, CollisionFilter.Default,
                2u, MeshA, default(ConvexHullGenerationParameters), Material.Default, CollisionFilter.Default
            ).Returns(false).SetName("Unique identifiers differ (not equal)"),
            new TestCaseData(
                0u, MeshA, ConvexHullGenerationParameters.Default, Material.Default, CollisionFilter.Default,
                0u, MeshA, default(ConvexHullGenerationParameters), Material.Default, CollisionFilter.Default
            ).Returns(false).SetName("Convex hull parameters differ (not equal)"),
            new TestCaseData(
                0u, MeshA, ConvexHullGenerationParameters.Default, Material.Default, CollisionFilter.Default,
                0u, MeshA, ConvexHullGenerationParameters.Default, default(Material), CollisionFilter.Default
            ).Returns(false).SetName("Materials differ (not equal)"),
            new TestCaseData(
                0u, MeshA, ConvexHullGenerationParameters.Default, Material.Default, CollisionFilter.Default,
                0u, MeshA, ConvexHullGenerationParameters.Default, Material.Default, default(CollisionFilter)
            ).Returns(false).SetName("Filters differ (not equal)"),
            new TestCaseData(
                0u, MeshA, ConvexHullGenerationParameters.Default, Material.Default, CollisionFilter.Default,
                0u, MeshB, ConvexHullGenerationParameters.Default, Material.Default, CollisionFilter.Default
            ).Returns(false).SetName("Mesh keys differ (not equal)")
        };

        [TestCaseSource(nameof(k_GetHash128TestCases))]
        public bool GetHash128_WhenDifferentInputs_CheckEquality(
            uint ida, UnityEngine.Mesh ma, ConvexHullGenerationParameters ha, Material mta, CollisionFilter fa,
            uint idb, UnityEngine.Mesh mb, ConvexHullGenerationParameters hb, Material mtb, CollisionFilter fb
        )
        {
            var a = GetHash128_FromMesh(ma, float4x4.identity, ha, mta, fa, float4x4.identity, uniqueIdentifier: ida);
            var b = GetHash128_FromMesh(mb, float4x4.identity, hb, mtb, fb, float4x4.identity, uniqueIdentifier: idb);

            return a.Equals(b);
        }

        [TestCase(1.1f, 1.1f, 1.1f, ExpectedResult = false, TestName = "Different scale (not equal)")]
        [TestCase(1.0001f, 1.0001f, 1.0001f, ExpectedResult = true, TestName = "Pretty close (equal)")]
        [TestCase(-1f, 1f, 1f, ExpectedResult = false, TestName = "Reflected X (not equal)")]
        [TestCase(1f, -1f, 1f, ExpectedResult = false, TestName = "Reflected Y (not equal)")]
        [TestCase(1f, 1f, -1f, ExpectedResult = false, TestName = "Reflected Z (not equal)")]
        [TestCase(1f, -1f, -1f, ExpectedResult = false, TestName = "(1f, -1f, -1f) (not equal)")]
        [TestCase(-1f, 1f, -1f, ExpectedResult = false, TestName = "(-1f, 1f, -1f) (not equal)")]
        [TestCase(-1f, -1f, 1f, ExpectedResult = false, TestName = "(-1f, -1f, 1f) (not equal)")]
        public bool GetHash128_WhenDifferentScale_CheckEqualityWithIdentity(
            float x, float y, float z
        )
        {
            var a = GetHash128_FromMesh(MeshA, float4x4.identity, ConvexHullGenerationParameters.Default, Material.Default, CollisionFilter.Default, float4x4.identity);
            var b = GetHash128_FromMesh(MeshA, float4x4.identity, ConvexHullGenerationParameters.Default, Material.Default, CollisionFilter.Default, float4x4.Scale(x, y, z));

            return a.Equals(b);
        }

        [Test]
        public void GetHash128_WhenDifferentPositions_NotEqual()
        {
            var a = GetHash128_FromMesh(MeshA, float4x4.Translate(1f));
            var b = GetHash128_FromMesh(MeshA, float4x4.Translate(1.1f));

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void GetHash128_WhenDifferentOrientations_NotEqual()
        {
            var a = GetHash128_FromMesh(MeshA, float4x4.RotateX(math.PI / 180f));
            var b = GetHash128_FromMesh(MeshA, float4x4.RotateX(math.PI / 180f * 1.05f));

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void GetHash128_WhenDifferentScales_NotEqual()
        {
            var a = GetHash128_FromMesh(MeshA, float4x4.Scale(1f));
            var b = GetHash128_FromMesh(MeshA, float4x4.Scale(1.01f));

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void GetHash128_WhenDifferentShears_NotEqual()
        {
            var a = GetHash128_FromMesh(MeshA, float4x4.Scale(2f));
            var b = GetHash128_FromMesh(MeshA, math.mul(float4x4.Scale(2f), float4x4.EulerZXY(math.PI / 4)));

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void GetHash128_WhenDifferentBlendShapeWeights_NotEqual(
            [Values(new[] { 1f }, new[] { 0f, 0f })] float[] otherSkinWeights
        )
        {
            var a = GetHash128_FromMesh(MeshA, float4x4.identity, blendShapeWeights: new[] { 0f });
            var b = GetHash128_FromMesh(MeshA, float4x4.identity, blendShapeWeights: otherSkinWeights);

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void GetHash128_WhenDifferentIncludedIndices_NotEqual(
            [Values(new[] { 1 }, new[] { 0, 0 })] int[] otherIncludedIndices
        )
        {
            var a = GetHash128_FromMesh(MeshA, float4x4.identity, includedIndices: new[] { 0 });
            var b = GetHash128_FromMesh(MeshA, float4x4.identity, includedIndices: otherIncludedIndices);

            Assert.That(a, Is.Not.EqualTo(b));
        }

        HashableShapeInputs InputsWithIndicesAndBlendShapeWeights(
            int[] indices, float[] weights, NativeList<int> allIncludedIndices, NativeList<float> allBlendShapeWeights
        )
        {
            return HashableShapeInputs.FromSkinnedMesh(
                MeshA,
                float4x4.identity,
                new NativeArray<int>(indices, Allocator.Temp),
                allIncludedIndices,
                new NativeArray<float>(weights, Allocator.Temp),
                allBlendShapeWeights
            );
        }

        [Test]
        public void GetHash128_WhenMultipleInputs_WithDifferentIncludedIndices_NotEqual()
        {
            using (var allIndices = new NativeList<int>(0, Allocator.TempJob))
            using (var allWeights = new NativeList<float>(0, Allocator.TempJob))
            {
                Hash128 a, b;

                using (var inputs = new NativeList<HashableShapeInputs>(2, Allocator.TempJob)
                   {
                       Length = 2,
                       [0] = InputsWithIndicesAndBlendShapeWeights(new[] { 0 }, Array.Empty<float>(), allIndices, allWeights),
                       [1] = InputsWithIndicesAndBlendShapeWeights(new[] { 0, 0 }, Array.Empty<float>(), allIndices, allWeights)
                   })
                {
                    a = HashableShapeInputs.GetHash128(
                        default, default, default, default, float4x4.identity, inputs.AsArray(), allIndices.AsArray(), allWeights.AsArray()
                    );
                }

                allIndices.Clear();
                allWeights.Clear();
                using (var inputs = new NativeList<HashableShapeInputs>(2, Allocator.TempJob)
                   {
                       Length = 2,
                       [0] = InputsWithIndicesAndBlendShapeWeights(new[] { 0, 0 }, Array.Empty<float>(), allIndices, allWeights),
                       [1] = InputsWithIndicesAndBlendShapeWeights(new[] { 0 }, Array.Empty<float>(), allIndices, allWeights)
                   })
                {
                    b = HashableShapeInputs.GetHash128(
                        default, default, default, default, float4x4.identity, inputs.AsArray(), allIndices.AsArray(), allWeights.AsArray()
                    );
                }

                Assert.That(a, Is.Not.EqualTo(b));
            }
        }

        [Test]
        public void GetHash128_WhenMultipleInputs_WithDifferentBlendShapeWeights_NotEqual()
        {
            using (var allIndices = new NativeList<int>(0, Allocator.TempJob))
            using (var allWeights = new NativeList<float>(0, Allocator.TempJob))
            {
                Hash128 a, b;

                using (var inputs = new NativeList<HashableShapeInputs>(2, Allocator.TempJob)
                   {
                       Length = 2,
                       [0] = InputsWithIndicesAndBlendShapeWeights(Array.Empty<int>(), new[] { 0f }, allIndices, allWeights),
                       [1] = InputsWithIndicesAndBlendShapeWeights(Array.Empty<int>(), new[] { 0f, 0f }, allIndices, allWeights)
                   })
                {
                    a = HashableShapeInputs.GetHash128(
                        default, default, default, default, float4x4.identity, inputs.AsArray(), allIndices.AsArray(), allWeights.AsArray()
                    );
                }

                allIndices.Clear();
                allWeights.Clear();
                using (var inputs = new NativeList<HashableShapeInputs>(2, Allocator.TempJob)
                   {
                       Length = 2,
                       [0] = InputsWithIndicesAndBlendShapeWeights(Array.Empty<int>(), new[] { 0f, 0f }, allIndices, allWeights),
                       [1] = InputsWithIndicesAndBlendShapeWeights(Array.Empty<int>(), new[] { 0f }, allIndices, allWeights)
                   })
                {
                    b = HashableShapeInputs.GetHash128(
                        default, default, default, default, float4x4.identity, inputs.AsArray(), allIndices.AsArray(), allWeights.AsArray()
                    );
                }

                Assert.That(a, Is.Not.EqualTo(b));
            }
        }

        static readonly TestCaseData[] k_EqualsWithinToleranceTestCases =
        {
            new TestCaseData(float4x4.identity).SetName("Identity (equal)").Returns(true),
            new TestCaseData(float4x4.Translate(0.00001f)).SetName("Small translation (equal)").Returns(true),
            new TestCaseData(float4x4.Translate(1000f)).SetName("Large translation (equal)").Returns(true),
            new TestCaseData(float4x4.EulerZXY(math.PI / 720f)).SetName("Small rotation (equal)").Returns(true),
            new TestCaseData(float4x4.EulerZXY(math.PI * 0.95f)).SetName("Large rotation (equal)").Returns(true),
            new TestCaseData(float4x4.Scale(0.00001f)).SetName("Small scale (equal)").Returns(true),
            new TestCaseData(float4x4.Scale(1000f)).SetName("Large scale (equal)").Returns(true),
            new TestCaseData(float4x4.TRS(new float3(1000f), quaternion.EulerZXY(math.PI / 9f), new float3(0.1f))).SetName("Several transformations (equal)").Returns(true),
        };

        [TestCaseSource(nameof(k_EqualsWithinToleranceTestCases))]
        public bool GetHash128_WhenInputsImprecise_ReturnsExpectedValue(float4x4 leafToBody)
        {
            var a = GetHash128_FromMesh(MeshA, leafToBody);

            var t = float4x4.TRS(new float3(1 / 9f), quaternion.EulerZXY(math.PI / 9f), new float3(1 / 27f));
            t = math.mul(t, math.inverse(t));
            Assume.That(t, Is.Not.EqualTo(float4x4.identity));
            t = math.mul(t, leafToBody);
            Assume.That(t, Is.Not.EqualTo(leafToBody));
            var b = GetHash128_FromMesh(MeshA, t);

            return a.Equals(b);
        }

        // following are slow tests used for local regression testing only
        /*
        [Test]
        public void GetHash128_Rotated90DegreeIntervals_WhenInputsImprecise_Equal(
            [Values(-360f, -270f, -180f, -90f, 0f, 90f, 180f, 270f, 360f)]float rotateX,
            [Values(-360f, -270f, -180f, -90f, 0f, 90f, 180f, 270f, 360f)]float rotateY,
            [Values(-360f, -270f, -180f, -90f, 0f, 90f, 180f, 270f, 360f)]float rotateZ
        )
        {
            var leafToBody = float4x4.EulerZXY(math.radians(rotateZ), math.radians(rotateY), math.radians(rotateX));

            var equalsWithinTolerance = GetHash128_WhenInputsImprecise_ReturnsExpectedValue(leafToBody);

            Assert.That(equalsWithinTolerance, Is.True);
        }
        */
    }
}
