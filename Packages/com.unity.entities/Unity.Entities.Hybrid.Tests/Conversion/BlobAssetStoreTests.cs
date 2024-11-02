using System;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Entities.Tests
{
    public class BlobAssetStoreTests
    {
        protected BlobAssetStore m_Store;

        [SetUp]
        public void Setup()
        {
            m_Store = new BlobAssetStore(128);
        }

        [TearDown]
        public void TearDown()
        {
            m_Store.Dispose();
        }

        protected Hash128 FromInt(int v) => new Hash128((uint)v, 1, 2, 3);
        protected Hash128 FromByte(byte v) => new Hash128((uint)v, 1, 2, 3);
        protected Hash128 FromFloat(float v) => new Hash128((uint)v.GetHashCode(), 1, 2, 3);

        [Test]
        public void TestCacheAccess()
        {
            var a0 = BlobAssetReference<int>.Create(0);
            var a1 = BlobAssetReference<int>.Create(1);
            var a2 = BlobAssetReference<float>.Create(2.0f);

            var k0 = FromInt(a0.Value);
            var k1 = FromInt(a1.Value);
            var k2 = FromFloat(a2.Value);

            Assert.IsTrue(m_Store.TryAdd(k0, ref a0));
            Assert.IsFalse(m_Store.TryAdd(k0, ref a0));
            Assert.IsTrue(m_Store.TryGetTest<int>(k0, out var ra0));
            Assert.AreEqual(0, ra0.Value);
            Assert.AreEqual(0, m_Store.CacheMiss);
            Assert.AreEqual(1, m_Store.CacheHit);

            Assert.IsFalse(m_Store.TryGetTest<int>(k1, out var ra1));
            Assert.IsTrue(m_Store.TryAdd(k1, ref a1));
            Assert.IsTrue(m_Store.TryGetTest<int>(k1, out ra1));
            Assert.AreEqual(1, ra1.Value);
            Assert.AreEqual(1, m_Store.CacheMiss);
            Assert.AreEqual(2, m_Store.CacheHit);

            Assert.IsFalse(m_Store.TryGetTest<float>(k2, out var ra2));
            Assert.IsTrue(m_Store.TryAdd(k2, ref a2));
            Assert.IsTrue(m_Store.TryGetTest(k2, out ra2));
            Assert.AreEqual(2.0f, ra2.Value);
            Assert.AreEqual(2, m_Store.CacheMiss);
            Assert.AreEqual(3, m_Store.CacheHit);
        }

        [Test]
        public void TestCacheAccessWithDifferentTypeSameKey()
        {
            var a0 = BlobAssetReference<int>.Create(10);
            var a1 = BlobAssetReference<byte>.Create(10);

            var k = FromInt(a0.Value);

            Assert.IsTrue(m_Store.TryAdd(k, ref a0));
            Assert.IsTrue(m_Store.TryAdd(k, ref a1));

            m_Store.TryGet<int>(k, out var ra0);
            m_Store.TryGet<byte>(k, out var ra1);

            Assert.AreEqual(a0, ra0);
            Assert.AreEqual(a1, ra1);
        }

        [Test]
        public unsafe void TestCacheClearWithDispose()
        {
            var a0 = BlobAssetReference<int>.Create(0);
            var a1 = BlobAssetReference<int>.Create(1);
            var a2 = BlobAssetReference<float>.Create(2.0f);

            var k0 = FromInt(a0.Value);
            var k1 = FromInt(a1.Value);
            var k2 = FromFloat(a2.Value);

            Assert.IsTrue(m_Store.TryAdd(k0, ref a0));
            Assert.IsTrue(m_Store.TryGet<int>(k0, out var ra0));

            Assert.IsTrue(m_Store.TryAdd(k1, ref a1));
            Assert.IsTrue(m_Store.TryGet<int>(k1, out var ra1));

            m_Store.ResetCache(true);

            Assert.Throws<InvalidOperationException>(() => a0.GetUnsafePtr());
            Assert.Throws<InvalidOperationException>(() => a1.GetUnsafePtr());

            Assert.IsFalse(m_Store.TryGet(k0, out ra0));
            Assert.IsFalse(m_Store.TryGet(k0, out ra1));

            Assert.IsTrue(m_Store.TryAdd(k2, ref a2));
            Assert.IsTrue(m_Store.TryGet<float>(k2, out var ra2));
        }

        [Test]
        public unsafe void TestCacheClearWithoutDispose()
        {
            var a0 = BlobAssetReference<int>.Create(0);
            var a1 = BlobAssetReference<int>.Create(1);

            var k0 = FromInt(a0.Value);
            var k1 = FromInt(a1.Value);

            Assert.IsTrue(m_Store.TryAdd(k0, ref a0));
            Assert.IsTrue(m_Store.TryAdd(k1, ref a1));

            m_Store.ResetCache(false);

            Assert.DoesNotThrow(() => a0.GetUnsafePtr());
            Assert.DoesNotThrow(() => a1.GetUnsafePtr());

            a0.m_data.UnprotectAgainstDisposal();
            a0.m_data.Dispose();
            a1.m_data.UnprotectAgainstDisposal();
            a1.m_data.Dispose();
        }

        [Test]
        public void TestTryAddWithContentHash()
        {
            var a0 = BlobAssetReference<int>.Create(0);
            var a0Duplicate = BlobAssetReference<int>.Create(0);
            var a1 = BlobAssetReference<int>.Create(1);
            var a0Float = BlobAssetReference<float>.Create(0);

            Assert.IsTrue(m_Store.TryAdd(ref a0));
            Assert.IsFalse(m_Store.TryAdd(ref a0Duplicate));
            Assert.IsTrue(m_Store.TryAdd(ref a1));
            Assert.IsTrue(m_Store.TryAdd(ref a0Float));

            Assert.AreEqual(0, a0.Value);
            Assert.AreEqual(0, a0Duplicate.Value);
            Assert.AreEqual(1, a1.Value);
            Assert.AreEqual(0.0F, a0Float.Value);
        }

        #if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void BlobValidationChecks()
        {
            var blobBuilder = new BlobBuilder(Allocator.Temp);
            blobBuilder.ConstructRoot<int>() = 5;
            var tempBlob = blobBuilder.CreateBlobAssetReference<int>(Allocator.Temp);
            var nullBlob = default(BlobAssetReference<int>);

            Assert.Throws<ArgumentException>(() => m_Store.TryAdd(ref tempBlob));
            Assert.Throws<NullReferenceException>(() => m_Store.TryAdd(ref nullBlob));
        }
        #endif
    }
}
