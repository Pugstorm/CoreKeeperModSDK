using System;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

namespace Unity.Physics.Tests.Collision.Geometry
{
    class ConvexHullTests
    {
        [Test]
        public unsafe void BuildConvexHull2D()
        {
            // Build circle.
            var expectedCom = new float3(4, 5, 3);
            const int n = 1024;
            float3* points = stackalloc float3[n];
            Aabb domain = Aabb.Empty;
            for (int i = 0; i < n; ++i)
            {
                float angle = (float)i / n * 2 * (float)math.PI;
                points[i] = expectedCom + new float3(math.cos(angle), math.sin(angle), 0);
                domain.Include(points[i]);
            }
            ConvexHullBuilderStorage builder = new ConvexHullBuilderStorage(8192, Allocator.Temp, domain, 0.0f, ConvexHullBuilder.IntResolution.High);
            for (int i = 0; i < n; ++i)
            {
                builder.Builder.AddPoint(points[i]);
            }
            builder.Builder.UpdateHullMassProperties();
            var massProperties = builder.Builder.HullMassProperties;
            Assert.IsTrue(math.all(math.abs(massProperties.CenterOfMass - expectedCom) < 1e-4f));
            Assert.AreEqual(math.PI, massProperties.SurfaceArea, 1e-4f);
        }

        private static void TestAdd128(ulong highA, ulong lowA, ulong highB, ulong lowB, ulong highExpected, ulong lowExpected)
        {
            Int128 sum = new Int128 { High = highA, Low = lowA } +new Int128 { High = highB, Low = lowB };
            Assert.AreEqual(highExpected, sum.High);
            Assert.AreEqual(lowExpected, sum.Low);
        }

        private static void TestSub128(ulong highA, ulong lowA, ulong highB, ulong lowB, ulong highExpected, ulong lowExpected)
        {
            Int128 diff = new Int128 { High = highA, Low = lowA } -new Int128 { High = highB, Low = lowB };
            Assert.AreEqual(highExpected, diff.High);
            Assert.AreEqual(lowExpected, diff.Low);
        }

        private static void TestMul6464(long a, long b, ulong highExpected, ulong lowExpected)
        {
            Int128 product = Int128.Mul(a, b);
            Assert.AreEqual(highExpected, product.High);
            Assert.AreEqual(lowExpected, product.Low);
        }

        private static void TestMul6432(long a, int b, ulong highExpected, ulong lowExpected)
        {
            Int128 product = Int128.Mul(a, b);
            Assert.AreEqual(highExpected, product.High);
            Assert.AreEqual(lowExpected, product.Low);
            TestMul6464(a, b, highExpected, lowExpected);
        }

        [Test]
        public unsafe void Int128Arithmetic()
        {
            TestAdd128(0, 0, 0, 0, 0, 0); // 0 + 0 = 0
            TestAdd128(0, 1, 0, 1, 0, 2); // 1 + 1 = 2
            TestAdd128(1, 1, 1, 1, 2, 2); // no carry
            TestAdd128(1, 2, 3, 4, 4, 6); // no carry
            TestAdd128(0, ~0UL, 0, 10, 1, 9); // carry
            TestAdd128(0, ~0UL, 0, 10, 1, 9); // carry

            TestSub128(0, 0, 0, 0, 0, 0); // 0 - 0 = 0
            TestSub128(1, 1, 1, 1, 0, 0); // 1 - 1 = 0
            TestSub128(3, 4, 1, 2, 2, 2); // no carry
            TestSub128(10, 0, 0, 1, 9, ~0UL); // carry
            TestSub128(1, 2, 3, 4, ~2UL, ~2UL + 1); // negative result
            TestSub128(0, 0, 0, 1, ~0UL, ~0UL); // 0 - 1 = -1
            TestSub128(0x8000000000000000UL, 0, 0, 1, 0x7FFFFFFFFFFFFFFFUL, ~0UL); // minValue - 1 = maxValue

            TestMul6432(0, 0, 0, 0); // 0 * 0 = 0
            TestMul6432(0, 10, 0, 0); // 0 * 10 = 0
            TestMul6432(10, 0, 0, 0); // 10 * 0 = 0
            TestMul6432(1, 10, 0, 10); // 1 * 10 = 10
            TestMul6432(10, 1, 0, 10); // 10 * 1 = 10
            TestMul6432(-1, 10, ~0UL, ~10UL + 1); // -1 * 10 = -10
            TestMul6432(-10, 1, ~0UL, ~10UL + 1); // -10 * 1 = -10
            TestMul6432(1, -10, ~0UL, ~10UL + 1); // -1 * 10 = -10
            TestMul6432(10, -1, ~0UL, ~10UL + 1); // -10 * 1 = -10
            TestMul6432(-1, -10, 0, 10); // -1 * -10 = 10
            TestMul6432(-10, -1, 0, 10); // -10 * -1 = 10
            TestMul6432(0x1000000000000, 0x10000, 1, 0); // 2^48 * 2^16 = 2^64
            TestMul6432(0x1000000000000, 0x10001, 1, 0x1000000000000UL); // 2^48 * (2^16 + 1) = 2^64 + 2^48
            TestMul6432(0x1000000000001, 0x10000, 1, 0x10000); // (2^48 + 1) * 2^16 = 2^64 + 2^16
            TestMul6432(0xF2CD7818E5DDF49, 123, 7, 0x4A8B8B3F671A4813UL);

            TestMul6464(0x100000000, 0x100000000, 1, 0); // 2^32 * 2^32 = 2^64
            TestMul6464(0x100000000, 0x100000001, 1, 0x100000000UL); // 2^32 * (2^32 + 1) = 2^64 + 2^32
            TestMul6464(0xDB42E7F146018E5, 0x312CCFC8E9FD00A9, 0x2A1E2FDC3A18376UL, 0x64A5E96D7AC16F2DUL);
        }
    }
}
