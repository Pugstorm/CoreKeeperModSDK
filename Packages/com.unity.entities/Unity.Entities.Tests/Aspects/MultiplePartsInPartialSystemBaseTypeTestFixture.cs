using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Tests
{
    partial class MultiplePartsInPartialSystemBaseTypeTestFixture : ECSTestsFixture
    {
        public T GetAspect<T>(Entity entity) where T : struct, IAspect, IAspectCreate<T>
            => default(T).CreateAspect(entity, ref EmptySystem.CheckedStateRef);

        public T GetComponent<T>(Entity entity) where T : unmanaged, IComponentData => m_Manager.GetComponentData<T>(entity);

        public UserWrittenPartialType UserWrittenPartialTypeSystem => World.GetOrCreateSystemManaged<UserWrittenPartialType>();

        [Test]
        public void MultiplePartsInPartialSystemBase_RunsCorrectly()
        {
            UserWrittenPartialTypeSystem.Update();

            var myAspect = GetAspect<MyAspect>(UserWrittenPartialTypeSystem.Entity_WithMyAspect_EcsTestData3_EcsTestTag);
            var ecsTestData3 = GetComponent<EcsTestData3>(UserWrittenPartialTypeSystem.Entity_WithMyAspect_EcsTestData3_EcsTestTag);

            Assert.AreEqual(50, myAspect._Data.ValueRO.value);
            Assert.AreEqual(20, myAspect._Data2.ValueRO.value0);
            Assert.AreEqual(20, myAspect._Data2.ValueRO.value1);

            Assert.AreEqual(90, ecsTestData3.value0);
            Assert.AreEqual(90, ecsTestData3.value1);
            Assert.AreEqual(90, ecsTestData3.value2);

            Assert.IsFalse(UserWrittenPartialTypeSystem.EntityManager.HasComponent<EcsTestTag>(UserWrittenPartialTypeSystem.Entity_WithMyAspect_EcsTestData3_EcsTestTag));
        }

        public partial class UserWrittenPartialType : SystemBase
        {
            public Entity Entity_WithMyAspect_EcsTestData3_EcsTestTag { get; private set; }

            protected override void OnCreate()
            {
                base.OnCreate();
                Entity_WithMyAspect_EcsTestData3_EcsTestTag =
                    EntityManager.CreateEntity(
                        ComponentType.ReadWrite<EcsTestData>(),
                        ComponentType.ReadWrite<EcsTestData2>(),
                        ComponentType.ReadWrite<EcsTestData3>(),
                        ComponentType.ReadOnly<EcsTestTag>());

                EntityManager.SetComponentData(Entity_WithMyAspect_EcsTestData3_EcsTestTag, new EcsTestData(10));
                EntityManager.SetComponentData(Entity_WithMyAspect_EcsTestData3_EcsTestTag, new EcsTestData2(20));
                EntityManager.SetComponentData(Entity_WithMyAspect_EcsTestData3_EcsTestTag, new EcsTestData3(30));
            }

            protected override void OnUpdate()
            {
                Entities.ForEach((MyAspect myAspect) =>
                {
                    myAspect._Data.ValueRW.value += myAspect._Data2.ValueRO.value0 + myAspect._Data2.ValueRO.value1; // 10 + 20 + 20 == 50
                }).Run();

                OnUpdate(additionToEcsTestData3: 10);
            }
        }

        public partial class UserWrittenPartialType
        {
            public void OnUpdate(int additionToEcsTestData3)
            {
                Entities.WithAll<EcsTestTag>().ForEach((MyAspect myAspect, ref EcsTestData3 ecsTestData3) =>
                {
                    ecsTestData3.value0 += myAspect._Data.ValueRO.value + additionToEcsTestData3; // 30 + 50 + 10 == 90
                    ecsTestData3.value1 += myAspect._Data.ValueRO.value + additionToEcsTestData3; // 30 + 50 + 10 == 90
                    ecsTestData3.value2 += myAspect._Data.ValueRO.value + additionToEcsTestData3; // 30 + 50 + 10 == 90
                }).Run();

                Method2();
            }
        }

        public partial class UserWrittenPartialType
        {
            public void Method2()
            {
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                Job.WithoutBurst()
                    .WithCode(() => ecb.RemoveComponent<EcsTestTag>(Entity_WithMyAspect_EcsTestData3_EcsTestTag))
                    .Run();
                ecb.Playback(EntityManager);
                ecb.Dispose();
            }
        }
    }
}
