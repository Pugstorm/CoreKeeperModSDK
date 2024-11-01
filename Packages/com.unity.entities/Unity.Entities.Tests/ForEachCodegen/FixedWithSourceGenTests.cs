using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Tests;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.Entities.CodeGen.Tests
{
    public partial class FixedWithSourceGenTests : ECSTestsFixture
    {
        static object[] Source =
        {
            new TestCaseData(typeof(DOTS_838)),
            new TestCaseData(typeof(DOTS_1589)),
            new TestCaseData(typeof(DOTS_1826)),
            new TestCaseData(typeof(DOTS_1951)),
            new TestCaseData(typeof(DOTS_1977)),
            new TestCaseData(typeof(DOTS_2700)),
            new TestCaseData(typeof(DOTS_2707)),
            new TestCaseData(typeof(DOTS_2824)),
            new TestCaseData(typeof(DOTS_2976)),
            new TestCaseData(typeof(DOTS_1684)),
        };

        [TestCaseSource(nameof(Source))]
        public void RegressionTests_GenerateNoErrors(Type type)
        {
            Assert.DoesNotThrow(() => Create(type).Update());
        }

        [MethodImpl(MethodImplOptions.NoInlining)] static void Ignore<T>(T _) { }

        World Create(Type type)
        {
            if (World.GetOrCreateSystemManaged(type) is SystemBase system)
            {
                return World;
            }
            throw new ArgumentException($"{type} is not a valid {typeof(SystemBase)}");
        }

        partial class DOTS_1684 : SystemBase
        {
            protected override void OnUpdate()
            {
                var deltaTime = SystemAPI.Time.DeltaTime;
                DoAction(() =>
                {
                    Entities
                        .WithName("RotationSpeedSystem_ForEach")
                        .ForEach((ref LocalTransform transform) =>
                        {
                            transform.Rotation = math.mul(math.normalize(transform.Rotation), quaternion.AxisAngle(math.up(), deltaTime));
                        })
                        .ScheduleParallel();
                });
            }

            void DoAction(Action action) => action();
        }

        partial class DOTS_2976 : SystemBase
        {
            protected override void OnUpdate()
            {
                var array = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(1, ref World.UpdateAllocator);
                try
                {
                    Job
                        .WithName("DOTS_2976")
                        .WithDisposeOnCompletion(array)
                        // ReSharper disable once AccessToDisposedClosure
                        .WithCode(() => array[0] = 123)
                        .Run();
                }
                catch
                {
                    throw;
                }
            }
        }

        partial class DOTS_2824 : SystemBase
        {
            protected override void OnUpdate()
            {
                var testData = GetComponentLookup<EcsTestData>();
                bool somethingChanged = default;

                Entities
                    .WithName("DOTS_2824")
                    .ForEach((Entity e) =>
                    {
                        somethingChanged |= testData.DidChange(e, LastSystemVersion);
                    })
                    .WithoutBurst()
                    .Run();

                Ignore(somethingChanged);
            }
        }

        partial class DOTS_838 : SystemBase
        {
            protected override void OnUpdate()
            {
                var shared = new SharedData1{ value = 0x42 };

                var e = EntityManager.CreateEntity();
                EntityManager.SetSharedComponentManaged(e, shared);
                EntityManager.AddComponentData(e, new EcsTestData {value = 1});
                var b = EntityManager.AddBuffer<EcsIntElement>(e);
                b.Add(5);

                Entity writeTo = Entity.Null;
                Entities
                    .WithStructuralChanges()
                    .WithoutBurst()
                    .WithSharedComponentFilter(shared)
                    .ForEach((Entity entity, ref EcsTestData t, in DynamicBuffer<EcsIntElement> buffer) =>
                    {
                        int q = 0;
                        foreach (var n in buffer)
                        {
                            q += n.Value;
                        }
                        if ((q > 0) && EntityManager.HasComponent<EcsTestData2>(entity))
                        {
                            EntityManager.SetComponentData(entity, LocalTransform.FromPosition(.0f, 1.0f, .0f));
                            var r = EntityManager.Instantiate(entity);
                            EntityManager.AddComponentData(EntityManager.Instantiate(entity), new LocalTransform());
                            writeTo = entity;
                        }
                    })
                    .Run();
                Assert.AreNotEqual(Entity.Null, writeTo);
            }
        }

        partial class DOTS_1589 : SystemBase
        {
            protected override void OnUpdate()
            {
                var foo = GetComponentLookup<EcsTestData>();
                var bar = GetComponentLookup<EcsTestData2>();

                bool somethingChanged = default;
                Entities.ForEach((Entity e) =>
                {
                    somethingChanged = foo.DidChange(e, LastSystemVersion) ||
                                       bar.DidChange(e, LastSystemVersion);
                })
                .WithoutBurst()
                .Run();
                Ignore(somethingChanged);
            }
        }

        partial class DOTS_1826 : SystemBase
        {
            protected override void OnUpdate()
            {
                var e = EntityManager.CreateEntity();
                EntityManager.AddComponentData(e, new EcsTestData
                {
                    value = 0x42
                });
                var foo = GetComponentLookup<EcsTestData>(true);

                int fromGet = default;
                int fromCom = default;
                Entities
                    .WithName("DOTS_1826")
                    .WithoutBurst()
                    .ForEach((Entity entity, in EcsTestData testData) =>
                    {
                        fromGet = SystemAPI.GetComponent<EcsTestData>(entity).value;
                        fromCom = foo[entity].value;

                    })
                    .Run();

                Assert.AreEqual(0x42, fromGet);
                Assert.AreEqual(0x42, fromCom);
            }
        }

        partial class DOTS_2700 : SystemBase
        {
            protected override void OnUpdate()
            {
                var array = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(1, ref World.UpdateAllocator);
                try
                {
                    Job
                        .WithName("DOTS_2700")
                        .WithDisposeOnCompletion(array)
                        // ReSharper disable once AccessToDisposedClosure
                        .WithCode(() => Ignore(array.Length))
                        .Run();
                }
                catch
                {
                    throw;
                }
            }
        }

        partial class DOTS_1977 : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities
                    .WithName("DOTS_1977")
                    .WithAll<EcsTestData>()
                    .ForEach((Entity e, in EcsTestData2 x, in EcsTestData3 y, in EcsTestData4 z) =>
                    {
                        Ignore(SystemAPI.HasComponent<EcsTestData>(e));
                        Ignore(x);
                        Ignore(y);
                        Ignore(z);
                    })
                    .Run();
            }
        }

        partial class DOTS_1951 : SystemBase
        {
            protected override void OnUpdate()
            {
                Ignore(EntityManager.CreateEntity());
                var q = Entity.Null;
                Entities
                    .WithName("DOTS_1951")
                    .ForEach((Entity e) => { q = e; })
                    .WithoutBurst()
                    .Run();
                Assert.AreNotEqual(Entity.Null, q);
            }
        }

        partial class DOTS_2707 : SystemBase
        {
            struct SomeSharedComponent : ISharedComponentData
            {
                public int Value;
            }

            struct SomeComponent : IComponentData
            {
                public int Value;
            }

            protected override void OnCreate()
            {
                var entityA = EntityManager.CreateEntity(typeof(SomeSharedComponent), typeof(SomeComponent));
                var entityB = EntityManager.CreateEntity(typeof(SomeSharedComponent), typeof(SomeComponent));

                EntityManager.SetSharedComponentManaged(entityA, new SomeSharedComponent { Value = 123 });
                EntityManager.SetSharedComponentManaged(entityB, new SomeSharedComponent { Value = 234 });
            }

            protected override void OnUpdate()
            {
                var filter = new SomeSharedComponent {Value = 123};

                Entities
                    .WithName("DOTS_2707_A")
                    .WithSharedComponentFilter(filter)
                    .ForEach((ref SomeComponent component) =>
                    {
                        component.Value = 1111;
                    })
                    .Run();

                const int expected = 2222;
                Entities
                    .WithName("DOTS_2707_B")
                    .WithAll<SomeSharedComponent>()
                    .ForEach((ref SomeComponent component) =>
                    {
                        component.Value = expected;
                    })
                    .Run();

                bool expectedValue = true;
                int iteratedEntities = 0;
                Entities
                    .WithName("DOTS_2707_C")
                    .WithoutBurst()
                    .ForEach((in SomeComponent component) =>
                    {
                        ++iteratedEntities;
                        expectedValue &= expected == component.Value;
                    })
                    .Run();

                Assert.True(expectedValue && (2 == iteratedEntities), $"Not all '{typeof(SomeComponent)}' values set to {expected}");
                Ignore(filter.Value);
            }
        }

    }
}
