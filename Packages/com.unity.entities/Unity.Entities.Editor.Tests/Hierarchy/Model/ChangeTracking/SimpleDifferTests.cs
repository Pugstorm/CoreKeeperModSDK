using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Editor.Tests
{
    class SimpleDifferTests
    {
        SimpleDiffer<int> m_Differ;

        [SetUp]
        public void Setup()
        {
            m_Differ = new SimpleDiffer<int>(128, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            m_Differ.Dispose();
        }

        [Test]
        public void DetectsCreatedAndRemovedItems()
        {
            var created = new NativeList<int>(Allocator.Persistent);
            var removed = new NativeList<int>(Allocator.Persistent);
            try
            {
                {
                    using var n = new NativeArray<int>(new[] { 1, 2, 3 }, Allocator.Temp);
                    m_Differ.GetCreatedAndRemovedItems(n, created, removed);
                }
                Assert.That(created.AsArray().ToArray(), Is.EquivalentTo(new[] { 1, 2, 3 }));
                Assert.That(removed.AsArray().ToArray(), Is.Empty);

                {
                    using var n = new NativeArray<int>(new[] { 1, 2, 3 }, Allocator.Temp);
                    m_Differ.GetCreatedAndRemovedItems(n, created, removed);
                }
                Assert.That(created.AsArray().ToArray(), Is.Empty);
                Assert.That(removed.AsArray().ToArray(), Is.Empty);

                {
                    using var n = new NativeArray<int>(new[] { 1, 2, 5 }, Allocator.Temp);
                    m_Differ.GetCreatedAndRemovedItems(n, created, removed);
                }
                Assert.That(created.AsArray().ToArray(), Is.EquivalentTo(new[] { 5 }));
                Assert.That(removed.AsArray().ToArray(), Is.EquivalentTo(new[] { 3 }));

                {
                    using var n = new NativeArray<int>(new[] { 1, 2, 3, 4, 6 }, Allocator.Temp);
                    m_Differ.GetCreatedAndRemovedItems(n, created, removed);
                }
                Assert.That(created.AsArray().ToArray(), Is.EquivalentTo(new[] { 3, 4, 6 }));
                Assert.That(removed.AsArray().ToArray(), Is.EquivalentTo(new[] { 5 }));
            }
            finally
            {
                created.Dispose();
                removed.Dispose();
            }
        }
    }
}
