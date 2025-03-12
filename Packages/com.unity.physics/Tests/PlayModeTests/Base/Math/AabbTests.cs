using NUnit.Framework;
using static Unity.Mathematics.math;
using Assert = UnityEngine.Assertions.Assert;
using float3 = Unity.Mathematics.float3;
using quaternion = Unity.Mathematics.quaternion;
using Random = Unity.Mathematics.Random;
using RigidTransform = Unity.Mathematics.RigidTransform;
using TestUtils = Unity.Physics.Tests.Utils.TestUtils;

namespace Unity.Physics.Tests.Base.Math
{
    class AabbTests
    {
        const float k_pi2 = 1.57079632679489f;

        [Test]
        public void TestAabb()
        {
            float3 v0 = float3(100, 200, 300);
            float3 v1 = float3(200, 300, 400);
            float3 v2 = float3(50, 100, 350);

            Aabb a0; a0.Min = float3.zero; a0.Max = v0;
            Aabb a1; a1.Min = float3.zero; a1.Max = v1;
            Aabb a2; a2.Min = v2; a2.Max = v1;
            Aabb a3; a3.Min = v2; a3.Max = v0;

            Assert.IsTrue(a0.IsValid);
            Assert.IsTrue(a1.IsValid);
            Assert.IsTrue(a2.IsValid);
            Assert.IsFalse(a3.IsValid);

            Assert.IsTrue(a1.Contains(a0));
            Assert.IsFalse(a0.Contains(a1));
            Assert.IsTrue(a1.Contains(a2));
            Assert.IsFalse(a2.Contains(a1));
            Assert.IsFalse(a0.Contains(a2));
            Assert.IsFalse(a2.Contains(a0));

            // Test Union / Intersect
            {
                Aabb unionAabb = a0;
                unionAabb.Include(a1);
                Assert.IsTrue(unionAabb.Min.x == 0);
                Assert.IsTrue(unionAabb.Min.y == 0);
                Assert.IsTrue(unionAabb.Min.z == 0);
                Assert.IsTrue(unionAabb.Max.x == a1.Max.x);
                Assert.IsTrue(unionAabb.Max.y == a1.Max.y);
                Assert.IsTrue(unionAabb.Max.z == a1.Max.z);

                Aabb intersectAabb = a2;
                intersectAabb.Intersect(a3);
                Assert.IsTrue(intersectAabb.Min.x == 50);
                Assert.IsTrue(intersectAabb.Min.y == 100);
                Assert.IsTrue(intersectAabb.Min.z == 350);
                Assert.IsTrue(intersectAabb.Max.x == a3.Max.x);
                Assert.IsTrue(intersectAabb.Max.y == a3.Max.y);
                Assert.IsTrue(intersectAabb.Max.z == a3.Max.z);
            }

            // Test Expand / Contains
            {
                Aabb a5; a5.Min = v2; a5.Max = v1;
                float3 testPoint = float3(v2.x - 1.0f, v1.y + 1.0f, .5f * (v2.z + v1.z));
                Assert.IsFalse(a5.Contains(testPoint));

                a5.Expand(1.5f);
                Assert.IsTrue(a5.Contains(testPoint));
            }

            // Test transform
            {
                Aabb ut; ut.Min = v0; ut.Max = v1;

                // Identity transform should not modify aabb
                Aabb outAabb = Unity.Physics.Math.TransformAabb(ut, RigidTransform.identity);

                TestUtils.AreEqual(ut.Min, outAabb.Min, 1e-3f);

                // Test translation
                outAabb = Unity.Physics.Math.TransformAabb(ut, new RigidTransform(quaternion.identity, float3(100.0f, 0.0f, 0.0f)));

                Assert.AreEqual(outAabb.Min.x, 200);
                Assert.AreEqual(outAabb.Min.y, 200);
                Assert.AreEqual(outAabb.Max.x, 300);
                Assert.AreEqual(outAabb.Max.z, 400);

                // Test rotation
                quaternion rot = quaternion.EulerXYZ(0.0f, 0.0f, k_pi2);
                outAabb = Unity.Physics.Math.TransformAabb(ut, new RigidTransform(rot, float3.zero));

                TestUtils.AreEqual(outAabb.Min, float3(-300.0f, 100.0f, 300.0f), 1e-3f);
                TestUtils.AreEqual(outAabb.Max, float3(-200.0f, 200.0f, 400.0f), 1e-3f);
                TestUtils.AreEqual(outAabb.SurfaceArea, ut.SurfaceArea, 1e-2f);
            }
        }

        [Test]
        public void TestAabbTransform()
        {
            Random rnd = new Random(0x12345678);
            for (int i = 0; i < 100; i++)
            {
                quaternion r = rnd.NextQuaternionRotation();
                float3 t = rnd.NextFloat3();

                Aabb orig = new Aabb();
                orig.Include(rnd.NextFloat3());
                orig.Include(rnd.NextFloat3());

                Aabb outAabb1 = Unity.Physics.Math.TransformAabb(orig, new RigidTransform(r, t));

                Physics.Math.MTransform bFromA = new Physics.Math.MTransform(r, t);
                Aabb outAabb2 = Unity.Physics.Math.TransformAabb(orig, bFromA);

                TestUtils.AreEqual(outAabb1.Min, outAabb2.Min, 1e-3f);
                TestUtils.AreEqual(outAabb1.Max, outAabb2.Max, 1e-3f);
            }
        }
    }
}
