using System;
using NUnit.Framework;
using Unity.Mathematics;

namespace Unity.Physics.Tests.Collision.Queries
{
    class RayCastTests
    {
        [Test]
        public void RayVsTriangle()
        {
            // triangle
            var v1 = new float3(-1, -1, 0);
            var v2 = new float3(0, 1, 0);
            var v3 = new float3(1, -1, 0);

            {
                var origin = new float3(0, 0, -2);
                var direction = new float3(0, 0, 4);

                float fraction = 1;
                bool hit = RaycastQueries.RayTriangle(origin, direction, v1, v2, v3, ref fraction, out float3 normal);
                Assert.IsTrue(hit);
                Assert.IsTrue(fraction == 0.5);
            }

            {
                var origin = new float3(0, 0, 2);
                var direction = new float3(0, 0, -4);

                float fraction = 1;
                bool hit = RaycastQueries.RayTriangle(origin, direction, v1, v2, v3, ref fraction, out float3 normal);
                Assert.IsTrue(hit);
                Assert.IsTrue(fraction == 0.5);
            }

            {
                var origin = new float3(1, -1, -2);
                var direction = new float3(0, 0, 4);

                float fraction = 1;
                bool hit = RaycastQueries.RayTriangle(origin, direction, v1, v2, v3, ref fraction, out float3 normal);
                Assert.IsTrue(hit);
                Assert.IsTrue(fraction == 0.5);
            }

            {
                var origin = new float3(2, 0, -2);
                var direction = new float3(0, 0, 4);

                float fraction = 1;
                bool hit = RaycastQueries.RayTriangle(origin, direction, v1, v2, v3, ref fraction, out float3 normal);
                Assert.IsFalse(hit);
            }

            {
                var origin = new float3(2, 0, -2);
                var direction = new float3(0, 0, -4);

                float fraction = 1;
                bool hit = RaycastQueries.RayTriangle(origin, direction, v1, v2, v3, ref fraction, out float3 normal);
                Assert.IsFalse(hit);
                Assert.IsTrue(math.all(normal == new float3(0, 0, 0)));
            }

            {
                v1 = new float3(-4, 0, 0);
                v2 = new float3(-5, 0, -1);
                v3 = new float3(-4, 0, -1);

                var origin = new float3(-4.497f, 0.325f, -0.613f);
                var direction = new float3(0f, -10f, 0f);

                float fraction = 1;
                bool hit = RaycastQueries.RayTriangle(origin, direction, v1, v2, v3, ref fraction, out float3 normal);
                Assert.IsTrue(hit);
            }
        }
    }
}
