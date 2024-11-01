using System;
using NUnit.Framework;
using Unity.Entities;
using Unity.Jobs;

namespace Unity.Entities.Tests.ForEachCodegen
{
    [TestFixture]
    public partial class SimpleJobTests : ECSTestsFixture
    {
        private SimpleJobSystem TestSystem;

        [SetUp]
        public void SetUp()
        {
            TestSystem = World.GetOrCreateSystemManaged<SimpleJobSystem>();
        }

        [Test]
        public void SimpleJob()
        {
            using (var myArray = new Collections.NativeArray<int>(10, Collections.Allocator.Persistent))
            {
                TestSystem.TestSimple(myArray);
                Assert.AreEqual(12, myArray[5]);
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on Atomic Safety Handles for detecting disposed containers")]
        public void JobWithDisposeOnCompletion()
        {
            var testArray = new Collections.NativeArray<int>(10, Collections.Allocator.Persistent);

            TestSystem.TestDisposeOnCompletion(testArray);

            Assert.Throws<ObjectDisposedException>(() =>
            {
                testArray[0] = 1;
            });
        }

        [Test]
        public void NoError_WithCodeComponentLookup()
        {
            TestSystem.WithCodeComponentLookup();
        }

        [Test]
        public void NoError_WithBufferLookup()
        {
            TestSystem.WithBufferLookup();
        }

        public partial class SimpleJobSystem : SystemBase
        {
            public struct FooElement : IBufferElementData
            {
                public int Value;
            }

            public struct FooComponent : IComponentData
            {
                public int Value;
            };

            protected override void OnUpdate() {}

            static void SetValues(Collections.NativeArray<int> myArray, int value)
            {
                for (int i = 0; i < myArray.Length; i++)
                {
                    myArray[i] = value;
                }
            }

            public void TestSimple(Collections.NativeArray<int> myArray)
            {
                int capturedValue = 12;
                Job.WithCode(() =>
                {
                    SetValues(myArray, capturedValue);
                }).Schedule();
                Dependency.Complete();
            }

            public void TestDisposeOnCompletion(Collections.NativeArray<int> testArray)
            {
                Job.WithDisposeOnCompletion(testArray).WithCode(() =>
                {
                    testArray[0] = 1;
                }).Schedule();
                Dependency.Complete();
            }

            public void WithCodeComponentLookup()
            {
                var e = EntityManager.CreateEntity();
                EntityManager.AddComponentData(e, new FooComponent { Value = 0 });

                Job.WithCode(() =>
                {
                    var lookup = GetComponentLookup<FooComponent>(false);
                    lookup[e] = new FooComponent { Value = 1 };
                }).Run();

                FooComponent fooComponent = EntityManager.GetComponentData<FooComponent>(e);
                Assert.AreEqual(fooComponent.Value, 1);
            }

            public void WithBufferLookup()
            {
                var e = EntityManager.CreateEntity();
                EntityManager.AddBuffer<FooElement>(e);

                Job.WithCode(() =>
                {
                    var bfe = GetBufferLookup<FooElement>(false);
                    bfe[e].Add(new FooElement { Value = 1 });
                }).Run();

                Assert.AreEqual(GetBufferLookup<FooElement>(true)[e][0].Value, 1);
            }
        }
    }
}
