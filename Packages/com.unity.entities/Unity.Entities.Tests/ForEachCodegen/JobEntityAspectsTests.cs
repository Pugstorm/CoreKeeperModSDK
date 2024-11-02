using System;
using NUnit.Framework;
using Unity.Burst;

using Unity.Entities;
using Unity.Collections;
using Unity.Entities.Tests;
using UnityEngine;

namespace Unity.Entities.Tests
{
    internal readonly partial struct MyAspectIJE : IAspect
    {
        public readonly RefRO<EcsTestData> Data;
    }

    internal readonly partial struct MyAspectIJE2 : IAspect
    {
        public readonly RefRW<EcsTestData2> Data;
    }

    partial class JobEntityAspectsTests : ECSTestsFixture
    {
        public partial class TestSystem : SystemBase
        {
            partial struct NoAccessSpecifierJob : IJobEntity {
                public NativeReference<int> Count;
                void Execute(MyAspectIJE data) => Count.Value++;
            }

            partial struct TwoOverlappingJob : IJobEntity {
                public NativeReference<int> Count;
                void Execute(MyAspectIJE myAspect, MyAspectIJE2 myAspect2) => Count.Value++;
            }

            partial struct ComponentOverlappingJob : IJobEntity {
                public NativeReference<int> Count;
                void Execute(MyAspectIJE myAspect, in EcsTestData2 data2) => Count.Value++;
            }

            protected override void OnUpdate()
            {
                using var ref0 = new NativeReference<int>(Allocator.TempJob);
                new NoAccessSpecifierJob {Count = ref0}.Run();
                Assert.AreEqual(ref0.Value, 2);

                // overlapping aspects
                using var ref3 = new NativeReference<int>(Allocator.TempJob);
                new TwoOverlappingJob {Count = ref3}.Run();
                Assert.AreEqual(ref3.Value, 1);

                using var ref4 = new NativeReference<int>(Allocator.TempJob);
                new ComponentOverlappingJob {Count = ref4}.Run();
                Assert.AreEqual(ref4.Value, 1);
            }
        }

        [Test]
        public void AspectIJETest()
        {
            var test = World.GetOrCreateSystemManaged<TestSystem>();
            m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.CreateEntity(typeof(EcsTestData2));
            m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            test.Update();
        }
    }
}
