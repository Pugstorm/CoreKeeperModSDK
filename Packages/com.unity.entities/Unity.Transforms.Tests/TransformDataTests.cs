using System;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;
using Assert = Unity.Assertions.Assert;

namespace Unity.Entities.Tests
{
    [TestFixture]
    partial class TransformDataTests : ECSTestsFixture
    {
        const float k_Tolerance = 0.001f;

        static bool AreNearlyEqual(float a, float b, float tolerance)
        {
            return math.abs(a - b) <= tolerance;
        }

        static bool AreNearlyEqual(float4 a, float4 b, float tolerance)
        {
            return AreNearlyEqual(a.x, b.x, tolerance) && AreNearlyEqual(a.y, b.y, tolerance) && AreNearlyEqual(a.z, b.z, tolerance) && AreNearlyEqual(a.w, b.w, tolerance);
        }

        static bool AreNearlyEqual(quaternion a, quaternion b, float tolerance)
        {
            return AreNearlyEqual(a.value.x, b.value.x, tolerance) && AreNearlyEqual(a.value.y, b.value.y, tolerance) && AreNearlyEqual(a.value.z, b.value.z, tolerance) && AreNearlyEqual(a.value.w, b.value.w, tolerance);
        }

        static bool AreNearlyEqual(float3 a, float3 b, float tolerance)
        {
            return AreNearlyEqual(a.x, b.x, tolerance) && AreNearlyEqual(a.y, b.y, tolerance) && AreNearlyEqual(a.z, b.z, tolerance);
        }

        static bool AreNearlyEqual(float4x4 a, float4x4 b, float tolerance)
        {
            return AreNearlyEqual(a.c0, b.c0, tolerance) && AreNearlyEqual(a.c1, b.c1, tolerance) && AreNearlyEqual(a.c2, b.c2, tolerance) && AreNearlyEqual(a.c3, b.c3, tolerance);
        }

        static bool AreNearlyEqual(LocalTransform a, LocalTransform b, float tolerance)
        {
            return AreNearlyEqual(a.Position, b.Position, tolerance) && AreNearlyEqual(a.Rotation.value, b.Rotation.value, tolerance) && AreNearlyEqual(a.Scale, b.Scale, tolerance);
        }

        static LocalTransform GetTestTransform1()
        {
            var rotation = quaternion.Euler(math.PI / 2, math.PI / 3, math.PI / 4);
            var position = new float3(2, 3, 4);
            var scale = 0.5f;
            return new LocalTransform
            {
                Rotation = rotation,
                Position = position,
                Scale = scale
            };
        }

        static float4x4 GetTestMatrix1()
        {
            var rotation = quaternion.Euler(math.PI / 2, math.PI / 3, math.PI / 4);
            var position = new float3(2, 3, 4);
            var scale = 0.5f;
            return float4x4.TRS(position, rotation, scale);
        }

        static float4x4 GetTestMatrix1NoScale()
        {
            var rotation = quaternion.Euler(math.PI / 2, math.PI / 3, math.PI / 4);
            var position = new float3(2, 3, 4);
            var scale = 1.0f;
            return float4x4.TRS(position, rotation, scale);
        }

        static LocalTransform GetTestTransform2()
        {
            var rotation = quaternion.Euler(math.PI / 3, math.PI / 4, math.PI / 5);
            var position = new float3(3, 4, 5);
            var scale = 0.6f;
            return new LocalTransform
            {
                Rotation = rotation,
                Position = position,
                Scale = scale
            };
        }

        [Test]
        public void TDT_Identity()
        {
            var transform = LocalTransform.Identity;
            Assert.IsTrue(transform.ToMatrix().Equals(float4x4.identity));
        }

        [Test]
        public void TDT_Inverse()
        {
            var transform = GetTestTransform1();
            var controlMatrix = math.inverse(transform.ToMatrix());
            var invertedTransformMatrix = transform.Inverse().ToMatrix();
            Assert.IsTrue(AreNearlyEqual(invertedTransformMatrix, controlMatrix, k_Tolerance));
        }

        [Test]
        public void TDT_InverseMatrix()
        {
            var transform = GetTestTransform1();
            var matrix = math.mul(transform.ToMatrix(), transform.ToInverseMatrix());
            Assert.IsTrue(AreNearlyEqual(matrix, float4x4.identity, k_Tolerance));
        }

        [Test]
        public void TDT_TransformTransform()
        {
            var transform1 = GetTestTransform1();
            var transform2 = GetTestTransform2();
            var controlMatrix = math.mul(transform1.ToMatrix(), transform2.ToMatrix());
            var transformedTransformMatrix = transform1.TransformTransform(transform2).ToMatrix();
            Assert.IsTrue(AreNearlyEqual(transformedTransformMatrix, controlMatrix, k_Tolerance));
        }

        [Test]
        public void TDT_InverseTransformTransform()
        {
            var transform1 = GetTestTransform1();
            var transform2 = GetTestTransform2();
            var controlMatrix = math.mul(math.inverse(transform1.ToMatrix()), transform2.ToMatrix());
            var inverseTransformedTransformMatrix = transform1.InverseTransformTransform(transform2).ToMatrix();
            Assert.IsTrue(AreNearlyEqual(inverseTransformedTransformMatrix, controlMatrix, k_Tolerance));
        }

        [Test]
        public void TDT_Right()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1();
            Assert.IsTrue(AreNearlyEqual(transform.Right(), math.normalize(matrix.c0.xyz), k_Tolerance));
        }

        [Test]
        public void TDT_Up()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1();
            Assert.IsTrue(AreNearlyEqual(transform.Up(), math.normalize(matrix.c1.xyz), k_Tolerance));
        }

        [Test]
        public void TDT_Forward()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1();
            Assert.IsTrue(AreNearlyEqual(transform.Forward(), math.normalize(matrix.c2.xyz), k_Tolerance));
        }

        [Test]
        public void TDT_TransformPoint()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1();
            var point = new float3(1, 2, 3);
            Assert.IsTrue(AreNearlyEqual(transform.TransformPoint(point), math.transform(matrix, point), k_Tolerance));
        }

        [Test]
        public void TDT_InverseTransformPoint()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1();
            var point = new float3(1, 2, 3);
            Assert.IsTrue(AreNearlyEqual(transform.InverseTransformPoint(point), math.transform(math.inverse(matrix), point), k_Tolerance));
        }

        [Test]
        public void TDT_TransformDirection()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1NoScale();
            var direction = math.normalize(new float3(1, 2, 3));
            Assert.IsTrue(AreNearlyEqual(transform.TransformDirection(direction), math.rotate(matrix, direction), k_Tolerance));
        }

        [Test]
        public void TDT_InverseTransformDirection()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1NoScale();
            var direction = math.normalize(new float3(1, 2, 3));
            Assert.IsTrue(AreNearlyEqual(transform.InverseTransformDirection(direction), math.rotate(math.inverse(matrix), direction), k_Tolerance));
        }

        [Test]
        public void TDT_TransformRotation()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1NoScale();
            var rotation = quaternion.Euler(math.PI / 4, math.PI / 3, math.PI / 2);
            Assert.IsTrue(AreNearlyEqual(transform.TransformRotation(rotation), math.mul(new quaternion(matrix), rotation), k_Tolerance));
        }

        [Test]
        public void TDT_InverseTransformRotation()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1NoScale();
            var rotation = quaternion.Euler(math.PI / 4, math.PI / 3, math.PI / 2);
            Assert.IsTrue(AreNearlyEqual(transform.InverseTransformRotation(rotation), math.mul(new quaternion(math.inverse(matrix)), rotation), k_Tolerance));
        }

        [Test]
        public void TDT_TransformScale()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1();
            var scale = 3.5f;
            Assert.IsTrue(AreNearlyEqual(transform.TransformScale(scale), math.length(matrix.c0.xyz) * scale, k_Tolerance));
        }

        [Test]
        public void TDT_InverseTransformScale()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1();
            var scale = 3.5f;
            Assert.IsTrue(AreNearlyEqual(transform.InverseTransformScale(scale), scale / math.length(matrix.c0.xyz), k_Tolerance));
        }

        [Test]
        public void TDT_ToMatrix()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1();
            Assert.IsTrue(AreNearlyEqual(transform.ToMatrix(), matrix, k_Tolerance));
        }

        [Test]
        public void TDT_ToInverseMatrix()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1();
            Assert.IsTrue(AreNearlyEqual(transform.Inverse().ToMatrix(), math.inverse(matrix), k_Tolerance));
        }

        static readonly TestCaseData[] k_FromMatrixTestCaseData =
        {
            new (float4x4.identity, new LocalTransform() { Position = float3.zero, Rotation = quaternion.identity, Scale = 1.0f }),
            new (float4x4.TRS(float3.zero, quaternion.identity, 2.0f), new LocalTransform() { Position = float3.zero, Rotation = quaternion.identity, Scale = 2.0f }),
            new (float4x4.TRS(new float3(1.0f, 2.0f, 3.0f), quaternion.EulerXYZ(math.radians(new float3(10.0f, 20f, -45f))), 2.0f), new LocalTransform() { Position = new float3(1.0f, 2.0f, 3.0f), Rotation = quaternion.EulerXYZ(math.radians(new float3(10.0f, 20f, -45f))), Scale = 2.0f }),
            new (new float4x4(2.0f, 0.0f, 0.0f, 0.0f,  0.0f, 2.0f, 0.0f, 0.0f,  0.0f, 0.0f, 2.0f, 0.0f,  0.0f, 0.0f, 0.0f, 1.0f), new LocalTransform() { Position = float3.zero, Rotation = quaternion.identity, Scale = 2.0f }),
            new (new float4x4(1.0f, 0.0f, 0.0f, 1.0f,  0.0f, 1.0f, 0.0f, 2.0f,  0.0f, 0.0f, 1.0f, 3.0f,  0.0f, 0.0f, 0.0f, 1.0f), new LocalTransform() { Position = new float3(1.0f, 2.0f, 3.0f), Rotation = quaternion.identity, Scale = 1.0f }),
            new (new float4x4(0.0f, 0.0f, 1.0f, 1.0f,  0.0f, 1.0f, 0.0f, 2.0f,  -1.0f, 0.0f, 0.0f, 3.0f,  0.0f, 0.0f, 0.0f, 1.0f), new LocalTransform() { Position = new float3(1.0f, 2.0f, 3.0f), Rotation = quaternion.RotateY(math.radians(90.0f)), Scale = 1.0f }),
        };

        static readonly TestCaseData[] k_FromMatrixInvalidTestCaseData =
        {
            new (new float4x4(1.0f, 0.0f, 0.0f, 0.0f,  1.0f, 0.0f, 0.0f, 0.0f,  1.0f, 0.0f, 0.0f, 0.0f,  0.0f, 0.0f, 0.0f, 1.0f)),  // Non-scaled, skewed
            new (new float4x4(1.0f, 0.0f, 0.0f, 0.0f,  1.0f, 2.0f, 0.0f, 0.0f,  1.0f, 0.0f, 2.0f, 0.0f,  0.0f, 0.0f, 0.0f, 1.0f)),  // Non-uniform scaled, skewed
            new (new float4x4(1.0f, 0.0f, 0.0f, 0.0f,  0.0f, 2.0f, 0.0f, 0.0f,  0.0f, 0.0f, 3.0f, 0.0f,  0.0f, 0.0f, 0.0f, 1.0f)),  // Non-uniform scaled, orthogonal
        };

        [TestCaseSource(nameof(k_FromMatrixTestCaseData))]
        public void TDT_FromMatrixSafe(float4x4 input, LocalTransform expected)
        {
            LocalTransform localTransform = LocalTransform.FromMatrixSafe(input);
            Assert.IsTrue(AreNearlyEqual(localTransform, expected, k_Tolerance));
        }

        [TestCaseSource(nameof(k_FromMatrixInvalidTestCaseData))]
        public void TDT_FromMatrixSafe_Throws(float4x4 input)
        {
            NUnit.Framework.Assert.Throws<ArgumentException>( () => LocalTransform.FromMatrixSafe(input));
        }

        [TestCaseSource(nameof(k_FromMatrixTestCaseData))]
        public void TDT_FromMatrix(float4x4 input, LocalTransform expected)
        {
            LocalTransform localTransform = LocalTransform.FromMatrix(input);
            Assert.IsTrue(AreNearlyEqual(localTransform, expected, k_Tolerance));
        }

        [Test]
        public void TDT_Translate()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1();
            transform = transform.Translate(new float3(1, 2, 3));
            matrix.c3.xyz += new float3(1, 2, 3);
            Assert.IsTrue(AreNearlyEqual(transform.ToMatrix(), matrix, k_Tolerance));
        }

        [Test]
        public void TDT_Scale()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1();
            transform = transform.ApplyScale(3.5f);
            matrix = math.mul(matrix, float4x4.Scale(3.5f));
            Assert.IsTrue(AreNearlyEqual(transform.ToMatrix(), matrix, k_Tolerance));
        }

        [Test]
        public void TDT_RotateX()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1();
            transform = transform.RotateX(3.5f);
            matrix = math.mul(matrix, float4x4.RotateX(3.5f));
            Assert.IsTrue(AreNearlyEqual(transform.ToMatrix(), matrix, k_Tolerance));
        }

        [Test]
        public void TDT_RotateY()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1();
            transform = transform.RotateY(3.5f);
            matrix = math.mul(matrix, float4x4.RotateY(3.5f));
            Assert.IsTrue(AreNearlyEqual(transform.ToMatrix(), matrix, k_Tolerance));
        }

        [Test]
        public void TDT_RotateZ()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1();
            transform = transform.RotateZ(3.5f);
            matrix = math.mul(matrix, float4x4.RotateZ(3.5f));
            Assert.IsTrue(AreNearlyEqual(transform.ToMatrix(), matrix, k_Tolerance));
        }

        [Test]
        public void TDT_Rotate()
        {
            var transform = GetTestTransform1();
            var matrix = GetTestMatrix1();
            var rotation = quaternion.Euler(math.PI / 3, math.PI / 4, math.PI / 5);
            transform = transform.Rotate(rotation);
            matrix = math.mul(matrix, new float4x4(rotation, float3.zero));
            Assert.IsTrue(AreNearlyEqual(transform.ToMatrix(), matrix, k_Tolerance));
        }
    }
}
