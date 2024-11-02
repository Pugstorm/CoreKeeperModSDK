using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine.Profiling;

namespace Unity.Entities.Tests
{
#if !UNITY_DISABLE_MANAGED_COMPONENTS
    [TestFixture]
    sealed class ManagedObjectUtilityTests
    {
#pragma warning disable CS0649
        class ClassWithPrimitives
        {
            public int A;
            public float B;
            public bool C;
            public string D;
        }

        class ClassWithNestedClass
        {
            public ClassWithPrimitives Nested;
        }

        class ClassWithCollections
        {
            public List<int> IntList;
            public List<Entity> EntityList;
        }

        class ClassWithBlobAssetReference
        {
            public BlobAssetReference<int> BlobAssetReference;
        }

        class ClassWithPolymorphicField
        {
            public BaseClassWithNoBlobAssetReference Value;
        }

        class BaseClassWithNoBlobAssetReference
        {
            
        }

        class SubClassWithBlobAssetReference : BaseClassWithNoBlobAssetReference
        {
            public BlobAssetReference<int> BlobAssetReference;
        }
        
#pragma warning restore CS0649

        [Test]
        public void ManagedObjectClone_Null()
        {
            var dst = new ManagedObjectClone().Clone(null);
            Assert.That(dst, Is.Null);
        }

        [Test]
        public void ManagedObjectClone_ClassWithPrimitives()
        {
            var src = new ClassWithPrimitives {A = 1, B = 2.3f, C = true, D = null};
            var dst = new ManagedObjectClone().Clone(src) as ClassWithPrimitives;

            Assert.That(dst, Is.Not.Null);
            Assert.That(dst, Is.Not.SameAs(src));
            Assert.That(dst.A, Is.EqualTo(src.A));
            Assert.That(dst.B, Is.EqualTo(src.B));
            Assert.That(dst.C, Is.EqualTo(src.C));
            Assert.That(dst.D, Is.EqualTo(src.D));
        }

        [Test]
        public void ManagedObjectClone_ClassWithNestedClass()
        {
            var src = new ClassWithNestedClass {Nested = new ClassWithPrimitives()};
            var dst = new ManagedObjectClone().Clone(src) as ClassWithNestedClass;

            Assert.That(dst, Is.Not.Null);
            Assert.That(dst, Is.Not.SameAs(src));
            Assert.That(dst.Nested, Is.Not.SameAs(src.Nested));
        }

        [Test]
        public void ManagedObjectClone_ClassWithCollections()
        {
            var src = new ClassWithCollections {IntList = new List<int> {1, 5, 9}};
            var dst = new ManagedObjectClone().Clone(src) as ClassWithCollections;

            Assert.That(dst, Is.Not.Null);
            Assert.That(dst, Is.Not.SameAs(src));
            Assert.That(dst.IntList, Is.Not.SameAs(src.IntList));
            Assert.That(dst.IntList.Count, Is.EqualTo(src.IntList.Count));
            Assert.That(dst.IntList, Is.EquivalentTo(src.IntList));
        }

        [Test]
        public void ManagedObjectClone_ClassWithBlobAssetReference()
        {
            using (var blobAssetReference = BlobAssetReference<int>.Create(13))
            {
                var src = new ClassWithBlobAssetReference
                {
                    BlobAssetReference = blobAssetReference
                };
                
                var dst = new ManagedObjectClone().Clone(src) as ClassWithBlobAssetReference;

                Assert.That(dst, Is.Not.Null);
                Assert.That(dst, Is.Not.SameAs(src));
                Assert.That(dst.BlobAssetReference.Value, Is.EqualTo(13));
            }
        }

        [Test]
        public void ManagedObjectEquals_Null()
        {
            var managedObjectEquals = new ManagedObjectEqual();
            Assert.That(managedObjectEquals.CompareEqual(null, null), Is.True);
            Assert.That(managedObjectEquals.CompareEqual(new ClassWithPrimitives(), null), Is.False);
            Assert.That(managedObjectEquals.CompareEqual(null, new ClassWithPrimitives()), Is.False);
        }

        [Test]
        public void ManagedObjectEquals_ClassWithPrimitives()
        {
            var a = new ClassWithPrimitives {A = 1, B = 2.3f, C = true, D = null};
            var b = new ClassWithPrimitives {A = 1, B = 2.3f, C = true, D = null};
            var c = new ClassWithPrimitives {A = 2, B = 2.3f, C = true, D = null};

            var managedObjectEquals = new ManagedObjectEqual();
            Assert.That(managedObjectEquals.CompareEqual(a, b), Is.True);
            Assert.That(managedObjectEquals.CompareEqual(a, c), Is.False);
        }

        [Test]
        public void ManagedObjectBlobs_WhenBlobAssetReferenceIsNull()
        {
            var a = new ClassWithBlobAssetReference();
            var managedObjectBlobs = new ManagedObjectBlobs();

            using (var blobAssets = new NativeList<BlobAssetPtr>(1, Allocator.Temp))
            using (var blobAssetsMap = new NativeParallelHashMap<BlobAssetPtr, int>(1, Allocator.Temp))
            {
                managedObjectBlobs.GatherBlobAssetReferences(a, blobAssets, blobAssetsMap);

                Assert.That(blobAssets.Length, Is.EqualTo(0));
                Assert.That(blobAssetsMap.Count(), Is.EqualTo(0));
            }
        }

        [Test]
        public void ManagedObjectBlobs_ClassWithPolymorphicField()
        {
            var managedObjectBlobs = new ManagedObjectBlobs();

            var a = new ClassWithPolymorphicField()
            {
                Value = new BaseClassWithNoBlobAssetReference()
            };
            
            using (var blobAssets = new NativeList<BlobAssetPtr>(1, Allocator.Temp))
            using (var blobAssetsMap = new NativeParallelHashMap<BlobAssetPtr, int>(1, Allocator.Temp))
            {
                managedObjectBlobs.GatherBlobAssetReferences(a, blobAssets, blobAssetsMap);

                Assert.That(blobAssets.Length, Is.EqualTo(0));
                Assert.That(blobAssetsMap.Count(), Is.EqualTo(0));
            }

            using (var blobAssetReference = BlobAssetReference<int>.Create(13))
            {
                var b = new ClassWithPolymorphicField
                {
                    Value = new SubClassWithBlobAssetReference
                    {
                        BlobAssetReference = blobAssetReference
                    }
                };

                using (var blobAssets = new NativeList<BlobAssetPtr>(1, Allocator.Temp))
                using (var blobAssetsMap = new NativeParallelHashMap<BlobAssetPtr, int>(1, Allocator.Temp))
                {
                    managedObjectBlobs.GatherBlobAssetReferences(b, blobAssets, blobAssetsMap);

                    Assert.That(blobAssets.Length, Is.EqualTo(1));
                    Assert.That(blobAssetsMap.Count(), Is.EqualTo(1));
                }
            }
        }
        
        [Test]
        public void ManagedObjectBlobs_DoesNotAllocate()
        {
            var managedObjectBlobs = new ManagedObjectBlobs();
            
            using (var blobAssets = new NativeList<BlobAssetPtr>(1, Allocator.Temp))
            using (var blobAssetsMap = new NativeParallelHashMap<BlobAssetPtr, int>(1, Allocator.Temp))
            {
                var classWithPrimitives = new ClassWithPrimitives();
                var classWithNestedClass = new ClassWithNestedClass {Nested = new ClassWithPrimitives()};
                var classWithCollections = new ClassWithCollections {EntityList = null, IntList = new List<int>{1,2,3,4}};
                
                ValidateNoGCAllocs(() => { managedObjectBlobs.GatherBlobAssetReferences(classWithPrimitives, blobAssets, blobAssetsMap); });
                ValidateNoGCAllocs(() => { managedObjectBlobs.GatherBlobAssetReferences(classWithNestedClass, blobAssets, blobAssetsMap); });
                ValidateNoGCAllocs(() => { managedObjectBlobs.GatherBlobAssetReferences(classWithCollections, blobAssets, blobAssetsMap); });
            }
        }
        
        static Recorder AllocRecorder = Recorder.Get("GC.Alloc");

        static int CountGCAllocs(Action action)
        {
            AllocRecorder.FilterToCurrentThread();
            AllocRecorder.enabled = false;
            AllocRecorder.enabled = true;

            action();

            AllocRecorder.enabled = false;
            return AllocRecorder.sampleBlockCount;
        }
        
        static void ValidateNoGCAllocs(Action action)
        {
            // warmup
            CountGCAllocs(action);

            // actual test
            var count = CountGCAllocs(action);
            if (count != 0)
                throw new AssertionException($"Expected 0 GC allocations but there were {count}");
        }
    }
#endif
}
