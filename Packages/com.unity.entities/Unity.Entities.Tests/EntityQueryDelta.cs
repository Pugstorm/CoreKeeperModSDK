using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Entities.Tests
{
    [TestFixture]
    partial class EntityQueryDelta : ECSTestsFixture
    {
        private static Entity[] nothing = {};
        // * TODO: using out of date version cached ComponentDataArray should give exception... (We store the System order version in it...)
        // * TODO: Using monobehaviour as delta inputs?
        // * TODO: Self-delta-mutation doesn't trigger update (ComponentLookup.cs)
        // /////@TODO: GlobalSystemVersion can't be pulled from m_Entities... indeterministic
        // * TODO: Chained delta works
        // How can this work? Need to use specialized job type because the number of entities to be
        // processed isn't known until running the job... Need some kind of late binding of parallel for length etc...
        // How do we prevent incorrect usage / default...

        public partial class DeltaCheckSystem : SystemBase
        {
            public Entity[] Expected;

            protected override void OnUpdate()
            {
                var group = GetEntityQuery(typeof(EcsTestData));
                group.SetChangedVersionFilter(typeof(EcsTestData));

                var actualEntityArray = group.ToEntityArray(World.UpdateAllocator.ToAllocator);
                var systemVersion = GlobalSystemVersion;
                var lastSystemVersion = LastSystemVersion;

                CollectionAssert.AreEqual(Expected, actualEntityArray);
            }

            public void UpdateExpectedResults(Entity[] expected)
            {
                Expected = expected;
                Update();
            }
        }

        [Test]
        public void CreateEntityTriggersChange()
        {
            Entity[] entity = new Entity[] { m_Manager.CreateEntity(typeof(EcsTestData)) };
            var deltaCheckSystem = World.CreateSystemManaged<DeltaCheckSystem>();
            deltaCheckSystem.UpdateExpectedResults(entity);
        }

        public enum ChangeMode
        {
            SetComponentData,
            SetComponentLookup,
        }

#pragma warning disable 649
        unsafe struct GroupRW
        {
            public EcsTestData* Data;
        }

        unsafe struct GroupRO
        {
            [Collections.ReadOnly]
            public EcsTestData* Data;
        }
#pragma warning restore 649

        // Running SetValue should change the chunk version for the data it's writing to.
        unsafe void SetValue(int index, int value, ChangeMode mode)
        {
            EmptySystem.Update();
            var entityArray = EmptySystem.GetEntityQuery(typeof(EcsTestData)).ToEntityArray(World.UpdateAllocator.ToAllocator);
            var entity = entityArray[index];

            if (mode == ChangeMode.SetComponentData)
            {
                m_Manager.SetComponentData(entity, new EcsTestData(value));
            }
            else if (mode == ChangeMode.SetComponentLookup)
            {
                //@TODO: Chaining correctness... Definitely not implemented right now...
                var array = EmptySystem.GetComponentLookup<EcsTestData>(false);
                array[entity] = new EcsTestData(value);
            }
        }

        // Running GetValue should not trigger any changes to chunk version.
        void GetValue(ChangeMode mode)
        {
            EmptySystem.Update();
            var entityArray = EmptySystem.GetEntityQuery(typeof(EcsTestData)).ToEntityArray(World.UpdateAllocator.ToAllocator);

            if (mode == ChangeMode.SetComponentData)
            {
                for (int i = 0; i != entityArray.Length; i++)
                    m_Manager.GetComponentData<EcsTestData>(entityArray[i]);
            }
            else if (mode == ChangeMode.SetComponentLookup)
            {
                for (int i = 0; i != entityArray.Length; i++)
                    m_Manager.GetComponentData<EcsTestData>(entityArray[i]);
            }
        }

        [Test]
        public void ChangeEntity([Values] ChangeMode mode)
        {
            var entity0 = m_Manager.CreateEntity(typeof(EcsTestData));
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

            var deltaCheckSystem0 = World.CreateSystemManaged<DeltaCheckSystem>();
            var deltaCheckSystem1 = World.CreateSystemManaged<DeltaCheckSystem>();

            // Chunk versions are considered changed upon creation and until after they're first updated.
            deltaCheckSystem0.UpdateExpectedResults(new Entity[] { entity0, entity1 });

            // First update of chunks.
            SetValue(0, 2, mode);
            SetValue(1, 2, mode);
            deltaCheckSystem0.UpdateExpectedResults(new Entity[] { entity0, entity1 });

            // Now that everything has been updated, the change filter won't trigger until we explicitly change something.
            deltaCheckSystem0.UpdateExpectedResults(nothing);

            // Change entity0's chunk.
            SetValue(0, 3, mode);
            deltaCheckSystem0.UpdateExpectedResults(new Entity[] { entity0 });

            // Change entity1's chunk.
            SetValue(1, 3, mode);
            deltaCheckSystem0.UpdateExpectedResults(new Entity[] { entity1 });

            // Already did the initial changes to these chunks in another system, so a change in this context is based on the system's change version.
            deltaCheckSystem1.UpdateExpectedResults(new Entity[] { entity0, entity1 });

            deltaCheckSystem0.UpdateExpectedResults(nothing);
            deltaCheckSystem1.UpdateExpectedResults(nothing);
        }

        [Test]
        public void GetEntityDataDoesNotChange([Values] ChangeMode mode)
        {
            var entity0 = m_Manager.CreateEntity(typeof(EcsTestData));
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var deltaCheckSystem = World.CreateSystemManaged<DeltaCheckSystem>();

            // First update of chunks after creation.
            SetValue(0, 2, mode);
            SetValue(1, 2, mode);
            deltaCheckSystem.UpdateExpectedResults(new Entity[] { entity0, entity1 });
            deltaCheckSystem.UpdateExpectedResults(nothing);

            // Now ensure that GetValue does not trigger a change on the EntityQuery.
            GetValue(mode);
            deltaCheckSystem.UpdateExpectedResults(nothing);
        }

        [Test]
        public void ChangeEntityWrap()
        {
            m_Manager.Debug.SetGlobalSystemVersion(uint.MaxValue - 3);

            var entity = m_Manager.CreateEntity(typeof(EcsTestData));

            var deltaCheckSystem = World.CreateSystemManaged<DeltaCheckSystem>();

            for (int i = 0; i != 7; i++)
            {
                SetValue(0, 1, ChangeMode.SetComponentData);
                deltaCheckSystem.UpdateExpectedResults(new Entity[] { entity });
            }

            deltaCheckSystem.UpdateExpectedResults(nothing);
        }

        [Test]
        public void NoChangeEntityWrap()
        {
            m_Manager.Debug.SetGlobalSystemVersion(uint.MaxValue - 3);

            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            SetValue(0, 2, ChangeMode.SetComponentData);

            var deltaCheckSystem = World.CreateSystemManaged<DeltaCheckSystem>();
            deltaCheckSystem.UpdateExpectedResults(new Entity[] { entity });

            for (int i = 0; i != 7; i++)
                deltaCheckSystem.UpdateExpectedResults(nothing);
        }

        public partial class DeltaProcessComponentSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities.ForEach((ref EcsTestData2 output, in EcsTestData input) =>
                {
                    output.value0 += input.value + 100;
                }).Schedule();
            }
        }

        [Test]
        public void IJobProcessComponentDeltaWorks()
        {
            var entity0 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3));
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

            var deltaSystem = World.CreateSystemManaged<DeltaProcessComponentSystem>();

            // First update of chunks after creation.
            SetValue(0, -100, ChangeMode.SetComponentData);
            SetValue(1, -100, ChangeMode.SetComponentData);
            deltaSystem.Update();
            Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestData2>(entity0).value0);
            Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestData2>(entity1).value0);

            // Change entity0's chunk.
            SetValue(0, 2, ChangeMode.SetComponentData);

            // Test [ChangedFilter] for real now.
            deltaSystem.Update();

            // Only entity0 should have changed.
            Assert.AreEqual(100 + 2, m_Manager.GetComponentData<EcsTestData2>(entity0).value0);

            // entity1.value0 should be unchanged from 0.
            Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestData2>(entity1).value0);
        }

        public partial class DeltaProcessComponentSystemUsingRun : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities.WithChangeFilter<EcsTestData>().ForEach((ref EcsTestData2 output, in EcsTestData input) =>
                {
                    output.value0 += input.value + 100;
                }).Run();
            }
        }

        [Test]
        public void IJobProcessComponentDeltaWorksWhenUsingRun()
        {
            var entity0 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3));
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

            var deltaSystem = World.CreateSystemManaged<DeltaProcessComponentSystemUsingRun>();

            // First update of chunks after creation.
            SetValue(0, -100, ChangeMode.SetComponentData);
            SetValue(1, -100, ChangeMode.SetComponentData);

            deltaSystem.Update();

            // Test [ChangedFilter] for real now.
            SetValue(0, 2, ChangeMode.SetComponentData);

            deltaSystem.Update();

            // Only entity0 should have changed.
            Assert.AreEqual(100 + 2, m_Manager.GetComponentData<EcsTestData2>(entity0).value0);

            // entity1.value0 should be unchanged from 0.
            Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestData2>(entity1).value0);
        }

        public partial class ModifyComponentSystem1Comp : SystemBase
        {
            public EcsTestSharedComp m_sharedComp;

            protected override void OnUpdate()
            {
                Entities.WithSharedComponentFilter(m_sharedComp).ForEach((ref EcsTestData data) =>
                {
                    data = new EcsTestData(100);
                }).Schedule();
            }
        }

        public partial class DeltaModifyComponentSystem1Comp : SystemBase
        {
            protected override void OnUpdate()
            {
                if (LastSystemVersion == 0)
                {
                    Entities.WithChangeFilter<EcsTestData>().ForEach((ref EcsTestData output) => { output.value = 0; }).Schedule();
                }
                else
                {
                    Entities.WithChangeFilter<EcsTestData>().ForEach((ref EcsTestData output) => { output.value += 150; }).Schedule();
                }
            }
        }

        [Test]
        public void ChangedFilterJobAfterAnotherJob1Comp()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var entities = new NativeArray<Entity>(10000, Allocator.Persistent);
            m_Manager.CreateEntity(archetype, entities);

            var modifySystem = World.CreateSystemManaged<ModifyComponentSystem1Comp>();
            var deltaSystem = World.CreateSystemManaged<DeltaModifyComponentSystem1Comp>();

            // First update of chunks after creation.
            modifySystem.Update();
            deltaSystem.Update();

            modifySystem.m_sharedComp = new EcsTestSharedComp(456);
            for (int i = 123; i < entities.Length; i += 345)
            {
                m_Manager.SetSharedComponentManaged(entities[i], modifySystem.m_sharedComp);
            }

            modifySystem.Update();
            deltaSystem.Update();

            foreach (var entity in entities)
            {
                if (m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entity).value == 456)
                {
                    FastAssert.AreEqual(250, m_Manager.GetComponentData<EcsTestData>(entity).value);
                }
                else
                {
                    FastAssert.AreEqual(0, m_Manager.GetComponentData<EcsTestData>(entity).value);
                }
            }
            entities.Dispose();
        }

        public partial class ModifyComponentSystem2Comp : SystemBase
        {
            public EcsTestSharedComp m_sharedComp;

            protected override void OnUpdate()
            {
                Entities.WithSharedComponentFilter(m_sharedComp).ForEach((ref EcsTestData data, ref EcsTestData2 data2) =>
                {
                    data = new EcsTestData(100);
                    data2 = new EcsTestData2(102);
                }).Schedule();
            }
        }

        public partial class DeltaModifyComponentSystem2Comp : SystemBase
        {
            public enum Variant
            {
                FirstComponentChanged,
                SecondComponentChanged,
            }

            public Variant variant;

            protected override void OnUpdate()
            {
                if (LastSystemVersion == 0)
                {
                    Entities.ForEach((ref EcsTestData output, ref EcsTestData2 output2) =>
                    {
                        output.value = 0;
                        output2.value0 = 0;
                    }).Schedule();
                }
                else
                {
                    switch (variant)
                    {
                        case Variant.FirstComponentChanged:
                            Entities.WithChangeFilter<EcsTestData>().ForEach((ref EcsTestData output, ref EcsTestData2 output2) =>
                            {
                                output.value += 150;
                                output2.value0 += 152;
                            }).Schedule();
                            return;
                        case Variant.SecondComponentChanged:
                            Entities.WithChangeFilter<EcsTestData2>().ForEach((ref EcsTestData output, ref EcsTestData2 output2) =>
                            {
                                output.value += 150;
                                output2.value0 += 152;
                            }).Schedule();
                            return;
                    }
                    throw new NotImplementedException();
                }
            }
        }

        [Test]
        public void ChangedFilterJobAfterAnotherJob2Comp([Values] DeltaModifyComponentSystem2Comp.Variant variant)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));
            var entities = new NativeArray<Entity>(10000, Allocator.Persistent);
            m_Manager.CreateEntity(archetype, entities);

            // All entities have just been created, so they're all technically "changed".
            var modifSystem = World.CreateSystemManaged<ModifyComponentSystem2Comp>();
            var deltaSystem = World.CreateSystemManaged<DeltaModifyComponentSystem2Comp>();

            // First update of chunks after creation.
            modifSystem.Update();
            deltaSystem.Update();

            deltaSystem.variant = variant;

            modifSystem.m_sharedComp = new EcsTestSharedComp(456);
            for (int i = 123; i < entities.Length; i += 345)
            {
                m_Manager.SetSharedComponentManaged(entities[i], modifSystem.m_sharedComp);
            }

            modifSystem.Update();
            deltaSystem.Update();

            foreach (var entity in entities)
            {
                if (m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entity).value == 456)
                {
                    FastAssert.AreEqual(250, m_Manager.GetComponentData<EcsTestData>(entity).value);
                    FastAssert.AreEqual(254, m_Manager.GetComponentData<EcsTestData2>(entity).value0);
                }
                else
                {
                    FastAssert.AreEqual(0, m_Manager.GetComponentData<EcsTestData>(entity).value);
                }
            }

            entities.Dispose();
        }

        public partial class ModifyComponentSystem3Comp : SystemBase
        {
            public EcsTestSharedComp m_sharedComp;

            protected override void OnUpdate()
            {
                Entities
                    .WithSharedComponentFilter(m_sharedComp)
                    .ForEach((ref EcsTestData data, ref EcsTestData2 data2, ref EcsTestData3 data3) =>
                    {
                        data = new EcsTestData(100);
                        data2 = new EcsTestData2(102);
                        data3 = new EcsTestData3(103);
                    }).Schedule();
            }
        }

        public partial class DeltaModifyComponentSystem3Comp : SystemBase
        {
            public enum Variant
            {
                FirstComponentChanged,
                SecondComponentChanged,
                ThirdComponentChanged,
            }

            public Variant variant;

            protected override void OnUpdate()
            {
                if (LastSystemVersion == 0)
                {
                    Entities.ForEach((ref EcsTestData output, ref EcsTestData2 output2, ref EcsTestData3 output3) =>
                    {
                        output.value = 0;
                        output2.value0 = 0;
                        output3.value0 = 0;
                    }).Schedule();

                    return;
                }

                switch (variant)
                {
                    case Variant.FirstComponentChanged:
                        Entities
                            .WithChangeFilter<EcsTestData>()
                            .ForEach((ref EcsTestData data, ref EcsTestData2 data2, ref EcsTestData3 data3) =>
                            {
                                data.value += 150;
                                data2.value0 += 152;
                                data3.value0 += 153;
                            }).Schedule();
                        return;
                    case Variant.SecondComponentChanged:
                            Entities
                                .WithChangeFilter<EcsTestData2>()
                                .ForEach((ref EcsTestData data, ref EcsTestData2 data2, ref EcsTestData3 data3) =>
                                {
                                    data.value += 150;
                                    data2.value0 += 152;
                                    data3.value0 += 153;
                                }).Schedule();
                        return;
                    case Variant.ThirdComponentChanged:
                        Entities
                            .WithChangeFilter<EcsTestData3>()
                            .ForEach((ref EcsTestData data, ref EcsTestData2 data2, ref EcsTestData3 data3) =>
                            {
                                data.value += 150;
                                data2.value0 += 152;
                                data3.value0 += 153;
                            }).Schedule();
                        return;
                }

                throw new NotImplementedException();
            }
        }

        [Test]
        public void ChangedFilterJobAfterAnotherJob3Comp([Values] DeltaModifyComponentSystem3Comp.Variant variant)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestSharedComp));
            var entities = new NativeArray<Entity>(10000, Allocator.Persistent);
            m_Manager.CreateEntity(archetype, entities);

            var modifySystem = World.CreateSystemManaged<ModifyComponentSystem3Comp>();
            var deltaSystem = World.CreateSystemManaged<DeltaModifyComponentSystem3Comp>();

            // First update of chunks after creation.
            modifySystem.Update();
            deltaSystem.Update();

            deltaSystem.variant = variant;

            modifySystem.m_sharedComp = new EcsTestSharedComp(456);
            for (int i = 123; i < entities.Length; i += 345)
            {
                m_Manager.SetSharedComponentManaged(entities[i], modifySystem.m_sharedComp);
            }

            modifySystem.Update();
            deltaSystem.Update();

            foreach (var entity in entities)
            {
                if (m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entity).value == 456)
                {
                    FastAssert.AreEqual(250, m_Manager.GetComponentData<EcsTestData>(entity).value);
                    FastAssert.AreEqual(254, m_Manager.GetComponentData<EcsTestData2>(entity).value0);
                    FastAssert.AreEqual(256, m_Manager.GetComponentData<EcsTestData3>(entity).value0);
                }
                else
                {
                    FastAssert.AreEqual(0, m_Manager.GetComponentData<EcsTestData>(entity).value);
                }
            }

            entities.Dispose();
        }

        partial class ChangeFilter1TestSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities
                    .WithChangeFilter<EcsTestData2>()
                    .ForEach((ref EcsTestData output, in EcsTestData2 output2) => { output.value = output2.value0; })
                    .Schedule();
            }
        }

        [Test]
        public void ChangeFilterWorksWithOneTypes()
        {
            var e = m_Manager.CreateEntity();
            var system = World.GetOrCreateSystemManaged<ChangeFilter1TestSystem>();
            m_Manager.AddComponentData(e, new EcsTestData(0));
            m_Manager.AddComponentData(e, new EcsTestData2(1));

            system.Update();
            m_Manager.Debug.SetGlobalSystemVersion(10);

            Assert.AreEqual(1, m_Manager.GetComponentData<EcsTestData>(e).value);

            m_Manager.SetComponentData(e, new EcsTestData2(5));

            system.Update();
            m_Manager.Debug.SetGlobalSystemVersion(20);

            Assert.AreEqual(5, m_Manager.GetComponentData<EcsTestData>(e).value);

            m_Manager.SetComponentData(e, new EcsTestData(100));

            system.Update();
            m_Manager.Debug.SetGlobalSystemVersion(30);

            Assert.AreEqual(100, m_Manager.GetComponentData<EcsTestData>(e).value);
        }

        partial class ChangeFilter2TestSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities
                    .WithChangeFilter<EcsTestData2>()
                    .WithChangeFilter<EcsTestData3>()
                    .ForEach((ref EcsTestData output, in EcsTestData2 output2, in EcsTestData3 output3) =>
                    {
                        output.value = output2.value0 + output3.value0;
                    })
                    .Schedule();
            }
        }

        [Test]
        public void ChangeFilterWorksWithTwoTypes()
        {
            var e = m_Manager.CreateEntity();
            var system = World.GetOrCreateSystemManaged<ChangeFilter2TestSystem>();
            m_Manager.AddComponentData(e, new EcsTestData(0));
            m_Manager.AddComponentData(e, new EcsTestData2(1));
            m_Manager.AddComponentData(e, new EcsTestData3(2));

            system.Update();
            m_Manager.Debug.SetGlobalSystemVersion(10);

            Assert.AreEqual(3, m_Manager.GetComponentData<EcsTestData>(e).value);

            m_Manager.SetComponentData(e, new EcsTestData2(5));

            system.Update();
            m_Manager.Debug.SetGlobalSystemVersion(20);

            Assert.AreEqual(7, m_Manager.GetComponentData<EcsTestData>(e).value);

            m_Manager.SetComponentData(e, new EcsTestData3(7));

            system.Update();
            m_Manager.Debug.SetGlobalSystemVersion(30);

            Assert.AreEqual(12, m_Manager.GetComponentData<EcsTestData>(e).value);

            m_Manager.SetComponentData(e, new EcsTestData2(8));
            m_Manager.SetComponentData(e, new EcsTestData3(9));

            system.Update();

            AssetHasChangeVersion<EcsTestData2>(e, 30);
            AssetHasChangeVersion<EcsTestData3>(e, 30);

            m_Manager.Debug.SetGlobalSystemVersion(40);

            Assert.AreEqual(17, m_Manager.GetComponentData<EcsTestData>(e).value);

            m_Manager.SetComponentData(e, new EcsTestData(100));
            AssetHasChangeVersion<EcsTestData>(e, 40);

            system.Update();
            m_Manager.Debug.SetGlobalSystemVersion(50);

            // Result Unchanged because inputs unchanged.
            AssetHasChangeVersion<EcsTestData2>(e, 30);
            AssetHasChangeVersion<EcsTestData3>(e, 30);
            AssetHasChangeVersion<EcsTestData>(e, 40);

            Assert.AreEqual(100, m_Manager.GetComponentData<EcsTestData>(e).value);
        }

        partial class SpawnerSystem : SystemBase
        {
            private EntityQuery _query;

            protected override void OnCreate()
            {
                _query = GetEntityQuery(typeof(EcsTestData));
                RequireForUpdate<EcsTestTag>();
            }

            protected override void OnStartRunning()
            {
                // Increment all EcsTestData components, then add a new one.

                // Component data should not normally be accessed like this, this is just
                // to test in-place changes outside of OnUpdate.
                var arr = _query.ToComponentDataArray<EcsTestData>(Allocator.Temp);
                for (var i = 0; i < arr.Length; i++)
                {
                    var data = arr[i];
                    data.value++;
                    arr[i] = data;
                }
                _query.CopyFromComponentDataArray(arr);

                var entity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(entity, new EcsTestData { value = 1 });
            }

            protected override void OnStopRunning()
            {
                // Double all EcsTestData component values
                var arr = _query.ToComponentDataArray<EcsTestData>(Allocator.Temp);
                for (var i = 0; i < arr.Length; i++)
                {
                    var data = arr[i];
                    data.value *= 2;
                    arr[i] = data;
                }
                _query.CopyFromComponentDataArray(arr);
            }

            protected override void OnUpdate()
            {
            }
        }

        partial class ReactiveSystem : SystemBase
        {
            public JobHandle LastDependency;
            protected override void OnUpdate()
            {
                Entities
                    .WithChangeFilter<EcsTestData>()
                    .ForEach((ref EcsTestData data) =>
                    {
                        data.value++;
                    }).Schedule();

                LastDependency = Dependency;
            }
        }

        [Test]
        public void ChangeFilterWorksWithOnStartRunningAndOnStopRunning()
        {
            var group = World.CreateSystemManaged<SimulationSystemGroup>();
            var spawnerSys = World.CreateSystemManaged<SpawnerSystem>();
            var reactiveSys = World.CreateSystemManaged<ReactiveSystem>();

            group.AddSystemToUpdateList(spawnerSys);
            group.AddSystemToUpdateList(reactiveSys);

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            var singletonEntity = m_Manager.CreateEntity();
            // Add required component for SpawnerSystem
            m_Manager.AddComponent<EcsTestTag>(singletonEntity);

            Assert.AreEqual(0, query.CalculateEntityCount(), "Should start with no entities");

            World.Update();

            reactiveSys.LastDependency.Complete();
            Assert.AreEqual(1, query.CalculateEntityCount(), "OnStartRunning should have created 1 entity");
            var array = query.ToComponentDataArray<EcsTestData>(Allocator.Temp);
            Assert.AreEqual(2, array[0].value, "Change-filtered system should have incremented value");

            World.Update();

            reactiveSys.LastDependency.Complete();
            var array2 = query.ToComponentDataArray<EcsTestData>(Allocator.Temp);
            Assert.AreEqual(2, array2[0].value, "Change-filtered system should not run with no changes to component");

            // Remove the required component to stop the SpawnerSystem from running
            m_Manager.RemoveComponent<EcsTestTag>(singletonEntity);
            World.Update();

            reactiveSys.LastDependency.Complete();
            var array3 = query.ToComponentDataArray<EcsTestData>(Allocator.Temp);
            // OnStopRunning doubles previous component, ReactiveSystem should increment after that
            Assert.AreEqual(5, array3[0].value, "Change-filtered system should run with changes from OnStopRunning");

            World.Update();

            reactiveSys.LastDependency.Complete();
            var array4 = query.ToComponentDataArray<EcsTestData>(Allocator.Temp);
            Assert.AreEqual(5, array4[0].value, "Change-filtered system should not run with no changes to component");

            // Reenable to test OnStartRunning after initial run
            m_Manager.AddComponent<EcsTestTag>(singletonEntity);
            World.Update();

            reactiveSys.LastDependency.Complete();
            var array5 = query.ToComponentDataArray<EcsTestData>(Allocator.Temp);
            Assert.AreEqual(2, query.CalculateEntityCount(), "Restarted system should have created 1 entity in OnStartRunning");
            // OnStartRunning increments existing value and ReactiveSystem increments changed components again
            Assert.AreEqual(7, array5[0].value, "Change-filtered system should increment existing entity that was changed");
            Assert.AreEqual(2, array5[1].value, "Change-filtered system should increment new entity created in OnStartRunning");

            array5[1] = new EcsTestData { value = 8 };
            query.CopyFromComponentDataArray(array5);

            World.Update();

            reactiveSys.LastDependency.Complete();
            var array6 = query.ToComponentDataArray<EcsTestData>(Allocator.Temp);
            Assert.AreEqual(9, array6[1].value, "Change-filtered system should increment existing entity that was changed without accompanying structural change in the chunk");
        }

        [BurstCompile(CompileSynchronously = true)]
        partial struct SpawnerSystemUnmanaged : ISystem, ISystemStartStop
        {
            private EntityQuery _query;

            public void OnCreate(ref SystemState state)
            {
                _query = state.GetEntityQuery(typeof(EcsTestData));
                state.RequireForUpdate<EcsTestTag>();
            }

            public void OnStartRunning(ref SystemState state)
            {
                // Increment all EcsTestData components, then add a new one.

                // Component data should not normally be accessed like this, this is just
                // to test in-place changes outside of OnUpdate.
                var arr = _query.ToComponentDataArray<EcsTestData>(Allocator.Temp);
                for (var i = 0; i < arr.Length; i++)
                {
                    var data = arr[i];
                    data.value++;
                    arr[i] = data;
                }
                _query.CopyFromComponentDataArray(arr);

                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, new EcsTestData { value = 1 });
            }

            public void OnStopRunning(ref SystemState state)
            {
                // Double all EcsTestData component values
                var arr = _query.ToComponentDataArray<EcsTestData>(Allocator.Temp);
                for (var i = 0; i < arr.Length; i++)
                {
                    var data = arr[i];
                    data.value *= 2;
                    arr[i] = data;
                }
                _query.CopyFromComponentDataArray(arr);
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        partial struct ReactiveSystemUnmanaged : ISystem
        {
            private EntityQuery _query;

            public partial struct IncrementTestDataJob : IJobEntity
            {
                public float deltaTime;
                public void Execute(ref EcsTestData data)
                {
                    data.value++;
                }
            }

            [BurstCompile(CompileSynchronously = true)]
            public void OnCreate(ref SystemState state)
            {
                _query = state.GetEntityQuery(ComponentType.ReadWrite<EcsTestData>());
                _query.AddChangedVersionFilter(ComponentType.ReadWrite<EcsTestData>());
            }
            [BurstCompile(CompileSynchronously = true)]
            public void OnUpdate(ref SystemState state)
            {
                state.Dependency = new IncrementTestDataJob().Schedule(_query, state.Dependency);
            }
        }

        [Ignore("DOTS-6905 Needs re-evaluated after we solve the NullReferenceException issues")]
        [Test]
        public void ChangeFilterWorksWithOnStartRunningAndOnStopRunningUnmanaged()
        {
            var group = World.CreateSystemManaged<SimulationSystemGroup>();
            var spawnerSys = World.CreateSystem<SpawnerSystemUnmanaged>();
            var reactiveSys = World.CreateSystem<ReactiveSystemUnmanaged>();
            var reactiveSysState = World.Unmanaged.ResolveSystemStateRef(reactiveSys);

            group.AddSystemToUpdateList(spawnerSys);
            group.AddSystemToUpdateList(reactiveSys);

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            var singletonEntity = m_Manager.CreateEntity();
            // Add required component for SpawnerSystem
            m_Manager.AddComponent<EcsTestTag>(singletonEntity);

            Assert.AreEqual(0, query.CalculateEntityCount(), "Should start with no entities");

            World.Update();

            reactiveSysState.Dependency.Complete();
            Assert.AreEqual(1, query.CalculateEntityCount(), "OnStartRunning should have created 1 entity");
            var array = query.ToComponentDataArray<EcsTestData>(Allocator.Temp);
            Assert.AreEqual(2, array[0].value, "Change-filtered system should have incremented value");

            World.Update();

            reactiveSysState.Dependency.Complete();
            var array2 = query.ToComponentDataArray<EcsTestData>(Allocator.Temp);
            Assert.AreEqual(2, array2[0].value, "Change-filtered system should not run with no changes to component");

            // Remove the required component to stop the SpawnerSystem from running
            m_Manager.RemoveComponent<EcsTestTag>(singletonEntity);
            World.Update();

            reactiveSysState.Dependency.Complete();
            var array3 = query.ToComponentDataArray<EcsTestData>(Allocator.Temp);
            // OnStopRunning doubles previous component, ReactiveSystem should increment after that
            Assert.AreEqual(5, array3[0].value, "Change-filtered system should run with changes from OnStopRunning");

            World.Update();

            reactiveSysState.Dependency.Complete();
            var array4 = query.ToComponentDataArray<EcsTestData>(Allocator.Temp);
            Assert.AreEqual(5, array4[0].value, "Change-filtered system should not run with no changes to component");

            // Reenable to test OnStartRunning after initial run
            m_Manager.AddComponent<EcsTestTag>(singletonEntity);
            World.Update();

            reactiveSysState.Dependency.Complete();
            var array5 = query.ToComponentDataArray<EcsTestData>(Allocator.Temp);
            Assert.AreEqual(2, query.CalculateEntityCount(), "Restarted system should have created 1 entity in OnStartRunning");
            // OnStartRunning increments existing value and ReactiveSystem increments changed components again
            Assert.AreEqual(7, array5[0].value, "Change-filtered system should increment existing entity that was changed");
            Assert.AreEqual(2, array5[1].value, "Change-filtered system should increment new entity created in OnStartRunning");

            array5[1] = new EcsTestData { value = 8 };
            query.CopyFromComponentDataArray(array5);

            World.Update();

            reactiveSysState.Dependency.Complete();
            var array6 = query.ToComponentDataArray<EcsTestData>(Allocator.Temp);
            Assert.AreEqual(9, array6[1].value, "Change-filtered system should increment existing entity that was changed without accompanying structural change in the chunk");
        }
    }
}
