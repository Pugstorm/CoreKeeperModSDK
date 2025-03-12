using System;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;

namespace Unity.Physics.Tests.Base.Containers
{
    class ElementPoolTests
    {
        // must be blittable, so we cannot use bool as member,
        //see https://docs.microsoft.com/en-us/dotnet/framework/interop/blittable-and-non-blittable-types
        public struct PoolTestElement : IPoolElement
        {
            private int _allocated;

            public int TestIndex { get; set; }

            public bool IsAllocated
            {
                get => _allocated == 0;
                set => _allocated = value ? 1 : 0;
            }

            void IPoolElement.MarkFree(int nextFree)
            {
                IsAllocated = false;
                NextFree = nextFree;
            }

            public int NextFree { get; set; }
        }

        [Test]
        public unsafe void CreateEmpty([Values(1, 100, 200)] int count)
        {
            PoolTestElement* elements = stackalloc PoolTestElement[count];
            ElementPoolBase poolBase = new ElementPoolBase(elements, count);
            var pool = new ElementPool<PoolTestElement> { ElementPoolBase = &poolBase };

            var numElems = 0;
            foreach (var elem in pool.Elements)
            {
                numElems++;
            }

            Assert.IsTrue(numElems == 0);
            Assert.IsTrue(pool.Capacity == count);
            Assert.IsTrue(pool.PeakCount == 0);
        }

        [Test]
        public unsafe void InsertAndClear([Values(1, 100, 200)] int count)
        {
            PoolTestElement* elements = stackalloc PoolTestElement[count];
            ElementPoolBase poolBase = new ElementPoolBase(elements, count);
            var pool = new ElementPool<PoolTestElement> { ElementPoolBase = &poolBase };

            for (var i = 0; i < count; ++i)
            {
                pool.Allocate(new PoolTestElement { TestIndex  = i});
                Assert.IsTrue(pool[i].IsAllocated);
            }

            Assert.IsTrue(pool.Capacity == count);

            var numElems = 0;
            foreach (var elem in pool.Elements)
            {
                Assert.IsTrue(pool[numElems].TestIndex == numElems);
                numElems++;
            }

            Assert.IsTrue(numElems == count);
            Assert.IsTrue(pool.PeakCount == count);

            pool.Clear();

            numElems = 0;
            foreach (var elem in pool.Elements)
            {
                numElems++;
            }

            Assert.IsTrue(numElems == 0);
            Assert.IsTrue(pool.PeakCount == 0);
        }

        [Test]
        public unsafe void Copy([Values(1, 100, 200)] int count)
        {
            PoolTestElement* elements = stackalloc PoolTestElement[count];
            ElementPoolBase poolBase = new ElementPoolBase(elements, count);
            var pool = new ElementPool<PoolTestElement> { ElementPoolBase = &poolBase };

            PoolTestElement* anotherElements = stackalloc PoolTestElement[count];
            ElementPoolBase anotherPoolBase = new ElementPoolBase(anotherElements, count);
            var anotherPool = new ElementPool<PoolTestElement> { ElementPoolBase = &anotherPoolBase };

            Assert.IsTrue(pool.Capacity == anotherPool.Capacity);

            for (var i = 0; i < count; ++i)
            {
                pool.Allocate(new PoolTestElement { TestIndex = i });
                Assert.IsTrue(pool[i].IsAllocated);
            }

            anotherPool.CopyFrom(pool);

            Assert.IsTrue(pool.PeakCount == anotherPool.PeakCount);

            for (var i = 0; i < count; ++i)
            {
                Assert.IsTrue(pool[i].TestIndex == i);
                Assert.IsTrue(anotherPool[i].TestIndex == i);
                Assert.IsTrue(anotherPool[i].IsAllocated);
            }
        }
    }
}
