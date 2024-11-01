using System;
using NUnit.Framework;
using static Unity.Entities.SystemAPI;

namespace Unity.Entities.Tests
{
    internal readonly partial struct MyAspectEFE : IAspect
    {
        public readonly RefRW<Unity.Entities.Tests.EcsTestData> Data;
    }
    internal readonly partial struct MyAspectEFE2 : IAspect
    {
        public readonly RefRW<Unity.Entities.Tests.EcsTestData2> Data;
    }

    partial class AspectEFETests : ECSTestsFixture
    {
        public partial class TestSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                int count = 0;
                Entities.ForEach((MyAspectEFE myAspect) => { ++count; }).Run();
                Assert.AreEqual(count, 2);

                // overlapping aspects
                count = 0;
                Entities.ForEach((MyAspectEFE myAspect, MyAspectEFE2 myAspect2) => { ++count; }).Run();
                Assert.AreEqual(count, 1);

                count = 0;
                Entities.ForEach((MyAspectEFE myAspect, in Unity.Entities.Tests.EcsTestData2 data2) => { ++count; }).Run();
                Assert.AreEqual(count, 1);

                count = 0;
                Entities.ForEach((Entity e, in EcsTestData data) =>
                {
                    var a = SystemAPI.GetAspect<MyAspectEFE>(e);
                    ++count;
                }).Run();
                Assert.AreEqual(count, 2);
            }
        }

        [Test]
        public void AspectEFETest()
        {
            var test = World.GetOrCreateSystemManaged<TestSystem>();

            var e0 = m_Manager.CreateEntity(typeof(EcsTestData));
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData2));
            var e2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            test.Update();
        }
    }
}
