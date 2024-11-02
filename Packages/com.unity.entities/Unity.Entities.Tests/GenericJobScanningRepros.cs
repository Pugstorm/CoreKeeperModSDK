using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Transforms;
using System.Text.RegularExpressions;
using Assert = UnityEngine.Assertions.Assert;

// This file contains two user examples of generic jobs that we are trying to keep supporting with generic job type detection.
// Please leave them here.

namespace Unity.Entities.Tests.CustomerProvided.Forum1
{
    public struct Foo
    {
        public int num;
    }

    public struct Bar : IBufferElementData
    {
        public int num;
    }

    public struct Baz : IComponentData
    {
        public int num;
    }

    public interface IJobCustom<T> where T : struct, IBufferElementData
    {
        void Execute(Entity entity, ref T t0, ref Baz baz, NativeArray<Foo> foos);
    }

    public static class TestUtility<TJob, T0> where TJob : unmanaged, IJobCustom<T0> where T0 : unmanaged, IBufferElementData
    {
        [BurstCompile]
        public struct WrapperJob : IJobChunk
        {
            // wrapped job ends up here
            public TJob wrappedJob;

            [ReadOnly]
            [DeallocateOnJobCompletion]
            public NativeArray<Foo> foos;

            [ReadOnly]
            public EntityTypeHandle entityType;
            public ComponentTypeHandle<Baz> bazType;
            public BufferTypeHandle<T0> t0Type;
            public byte isReadOnly_T0;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // This job is not written to support queries with enableable component types.
                Assert.IsFalse(useEnabledMask);

                var entities = chunk.GetNativeArray(entityType);
                var bazArray = chunk.GetNativeArray(ref bazType);
                var t0Buffers = chunk.GetBufferAccessor(ref t0Type);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var entity = entities[i];
                    var baz = bazArray[i];
                    var t0Buffer = t0Buffers[i];

                    for (int iT0 = 0; iT0 < t0Buffer.Length; iT0++)
                    {
                        var t0 = t0Buffer[iT0];

                        // wrapped job Execute method is called
                        wrappedJob.Execute(entity, ref t0, ref baz, foos);

                        bazArray[i] = baz;

                        if (isReadOnly_T0 == 0)
                        {
                            t0Buffer[iT0] = t0;
                        }
                    }
                }
            }
        }

        public static JobHandle Schedule(ref SystemState state, EntityQuery query, JobHandle dependentOn, TJob jobData)
        {
            WrapperJob wrapperJob = new WrapperJob
            {
                wrappedJob = jobData,
                foos = new NativeArray<Foo>(10, Allocator.TempJob),
                entityType = state.EntityManager.GetEntityTypeHandle(),
                bazType = state.EntityManager.GetComponentTypeHandle<Baz>(false),
                t0Type = state.EntityManager.GetBufferTypeHandle<T0>(false),
                isReadOnly_T0 = 0
            };

            return wrapperJob.ScheduleParallel(query, dependentOn);
        }

        public static JobHandle Schedule(CustomSystemBase system, JobHandle dependentOn, TJob jobData)
        {
            EntityQuery query = GetExistingEntityQuery(system);

            if (query == default)
            {
                ComponentType[] componentTypes;
                if (!componentTypesByJobType.TryGetValue(typeof(TJob), out componentTypes))
                {
                    componentTypes = GetIJobCustomComponentTypes();
                }

                query = system.GetEntityQueryPublic(componentTypes);

                system.RequireForUpdate(query);

                if (query.CalculateChunkCount() == 0)
                {
                    return dependentOn;
                }
            }

            WrapperJob wrapperJob = new WrapperJob
            {
                wrappedJob = jobData,
                foos = new NativeArray<Foo>(10, Allocator.TempJob),
                entityType = system.EntityManager.GetEntityTypeHandle(),
                bazType = system.EntityManager.GetComponentTypeHandle<Baz>(false),
                t0Type = system.EntityManager.GetBufferTypeHandle<T0>(false),
                isReadOnly_T0 = 0
            };

            return wrapperJob.ScheduleParallel(query, dependentOn);
        }

        private static Dictionary<Type, ComponentType[]> componentTypesByJobType = new Dictionary<Type, ComponentType[]>();

        private static unsafe EntityQuery GetExistingEntityQuery(ComponentSystemBase system)
        {
            ComponentType[] componentTypes;
            if (!componentTypesByJobType.TryGetValue(typeof(TJob), out componentTypes))
            {
                return default;
            }

            fixed (ComponentType* componentTypesPtr = componentTypes)
            {
                using var builder = new EntityQueryBuilder(Allocator.Temp, componentTypesPtr, componentTypes.Length);
                for (var i = 0; i != system.EntityQueries.Length; i++)
                {
                    if (system.EntityQueries[i].CompareQuery(builder))
                        return system.EntityQueries[i];
                }
            }

            return default;
        }

        private static ComponentType[] GetIJobCustomComponentTypes()
        {
            // Simplified for testing purposes. The real version uses reflection to get this data, and caches it for later.
            return new ComponentType[]
                {
                ComponentType.ReadWrite<T0>(),
                ComponentType.ReadWrite<Baz>()
                };
        }
    }

    public abstract partial class CustomSystemBase : SystemBase
    {
        public EntityQuery GetEntityQueryPublic(ComponentType[] componentTypes)
        {
            return GetEntityQuery(componentTypes);
        }
    }
    public partial class MySystem : CustomSystemBase
    {
        protected override void OnCreate()
        {
            // Creating a single entity for testing purposes
            Entity entity = EntityManager.CreateEntity(ComponentType.ReadWrite<Bar>(), ComponentType.ReadWrite<Baz>());

            DynamicBuffer<Bar> barBuffer = EntityManager.GetBuffer<Bar>(entity);
            barBuffer.Add(new Bar { num = 0 });
        }

        protected override void OnUpdate()
        {
            Dependency = TestUtility<WrappedCustomJob, Bar>.Schedule(this, Dependency, new WrappedCustomJob
            {
                toAdd = 10
            });
        }

        public struct WrappedCustomJob : IJobCustom<Bar>
        {
            public int toAdd;

            public void Execute(Entity entity, ref Bar t0, ref Baz baz, NativeArray<Foo> foos)
            {
                // do custom logic here
                t0.num += toAdd;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public partial struct MyISystem : ISystem
    {
        EntityQuery query;

        public void OnCreate(ref SystemState state)
        {
            // Creating a single entity for testing purposes
            Entity entity = state.EntityManager.CreateEntity(ComponentType.ReadWrite<Bar>(), ComponentType.ReadWrite<Baz>());

            DynamicBuffer<Bar> barBuffer = state.EntityManager.GetBuffer<Bar>(entity);
            barBuffer.Add(new Bar { num = 0 });

            query = state.GetEntityQuery(new EntityQueryDesc()
            {
                All = new ComponentType[] { ComponentType.ReadOnly<Bar>(), ComponentType.ReadOnly<Baz>() },
            });
        }

        [BurstCompile(CompileSynchronously = true)]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = TestUtility<WrappedCustomJob, Bar>.Schedule(ref state, query, state.Dependency, new WrappedCustomJob
            {
                toAdd = 10
            });
        }

        public struct WrappedCustomJob : IJobCustom<Bar>
        {
            public int toAdd;

            public void Execute(Entity entity, ref Bar t0, ref Baz baz, NativeArray<Foo> foos)
            {
                // do custom logic here
                t0.num += toAdd;
            }
        }
    }

    public class UserGenericJobCode1 : ECSTestsFixture
    {
        [Test]
        public void ReflectionDataForHiddenGenerics()
        {
            var repro = World.GetOrCreateSystemManaged<MySystem>();
            var simulationGroup = World.GetOrCreateSystemManaged<SimulationSystemGroup>();
            simulationGroup.AddSystemToUpdateList(repro);
            World.Update();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void NoReflectionDataForHiddenGenericsInBurst()
        {
            // This should throw, as we can't pre-make the reflection data for this type of generic job
            if (IsBurstEnabled())
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Reflection data"));
            var repro = World.GetOrCreateSystem<MyISystem>();
            var simulationGroup = World.GetOrCreateSystemManaged<SimulationSystemGroup>();
            simulationGroup.AddSystemToUpdateList(repro);
            World.Update();
            if (!IsBurstEnabled())
                LogAssert.NoUnexpectedReceived();
        }
    }
}

namespace Unity.Entities.Tests.CustomerProvided.Forum2
{
    public struct Foo : IComponentData
    {
        public int value;
    }

    public static class TestUtility<T> where T : unmanaged, IComponentData
    {
        [BurstCompile]
        public struct ProcessChunks : IJobChunk
        {
            public ComponentTypeHandle<T> typeHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // This job is not written to support queries with enableable component types.
                Assert.IsFalse(useEnabledMask);

                for (int i = 0; i < 10000; i++)
                {
                    var testDataArray = chunk.GetNativeArray(ref typeHandle);
                    testDataArray[0] = new T();
                }
            }
        }
    }

    public partial class MySystem : SystemBase
    {
        EntityQuery query;

        protected override void OnCreate()
        {
            query = GetEntityQuery(ComponentType.ReadWrite<Foo>());

            // for testing purposes. Create an entity with a Foo component
            EntityManager.CreateEntity(ComponentType.ReadWrite<Foo>());
        }

        protected override void OnUpdate()
        {
            Dependency = new TestUtility<Foo>.ProcessChunks
            {
                typeHandle = GetComponentTypeHandle<Foo>()
            }.ScheduleParallel(query, Dependency);
        }
    }

    public class UserGenericJobCode2 : ECSTestsFixture
    {
        [Test]
        public void DoesntCrashEditor()
        {
            var repro = World.GetOrCreateSystemManaged<MySystem>();
            var simulationGroup = World.GetOrCreateSystemManaged<SimulationSystemGroup>();
            simulationGroup.AddSystemToUpdateList(repro);
            World.Update();
        }
    }
}


namespace Unity.Entities.Tests.CustomerProvided.Forum1_Tweaked
{
    public struct Foo
    {
        public int num;
    }

    public struct Bar : IBufferElementData
    {
        public int num;
    }

    public struct Baz : IComponentData
    {
        public int num;
    }

    public interface IJobCustom<T> where T : struct, IBufferElementData
    {
        void Execute(Entity entity, ref T t0, ref Baz baz, NativeArray<Foo> foos);
    }

    public static class TestUtility<TJob, T0> where TJob : struct, IJobCustom<T0> where T0 : unmanaged, IBufferElementData
    {
        public struct BlahBlah<Q> where Q : unmanaged
        {
            public WrapperJob Thing;

            [BurstCompile]
            public struct WrapperJob : IJobChunk
            {
                // wrapped job ends up here
                public TJob wrappedJob;

                [ReadOnly]
                public NativeArray<Foo> foos;

                [ReadOnly]
                public EntityTypeHandle entityType;
                public ComponentTypeHandle<Baz> bazType;
                public BufferTypeHandle<T0> t0Type;
                public byte isReadOnly_T0;
                public Q stuff;

                public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
                {
                    var entities = chunk.GetNativeArray(entityType);
                    var bazArray = chunk.GetNativeArray(ref bazType);
                    var t0Buffers = chunk.GetBufferAccessor(ref t0Type);

                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var entity = entities[i];
                        var baz = bazArray[i];
                        var t0Buffer = t0Buffers[i];

                        for (int iT0 = 0; iT0 < t0Buffer.Length; iT0++)
                        {
                            var t0 = t0Buffer[iT0];

                            // wrapped job Execute method is called
                            wrappedJob.Execute(entity, ref t0, ref baz, foos);

                            bazArray[i] = baz;

                            if (isReadOnly_T0 == 0)
                            {
                                t0Buffer[iT0] = t0;
                            }
                        }
                    }
                }
            }
        }

        public static bool MakeJob<Q>(CustomSystemBase system, TJob jobData, out BlahBlah<Q> job, out EntityQuery query) where Q: unmanaged
        {
            query = GetExistingEntityQuery(system);
            job = default;

            if (query == default)
            {
                ComponentType[] componentTypes;
                if (!componentTypesByJobType.TryGetValue(typeof(TJob), out componentTypes))
                {
                    componentTypes = GetIJobCustomComponentTypes();
                }

                query = system.GetEntityQueryPublic(componentTypes);

                system.RequireForUpdate(query);

                if (query.CalculateChunkCount() == 0)
                {
                    return false;
                }
            }

            job = new BlahBlah<Q>
            {
                Thing = new BlahBlah<Q>.WrapperJob
                {
                    wrappedJob = jobData,
                    foos = CollectionHelper.CreateNativeArray<Foo, RewindableAllocator>(10, ref system.EntityManager.World.UpdateAllocator),
                    entityType = system.EntityManager.GetEntityTypeHandle(),
                    bazType = system.EntityManager.GetComponentTypeHandle<Baz>(false),
                    t0Type = system.EntityManager.GetBufferTypeHandle<T0>(false),
                    isReadOnly_T0 = 0
                }
            };

            return true;
        }

        private static Dictionary<Type, ComponentType[]> componentTypesByJobType = new Dictionary<Type, ComponentType[]>();

        private static unsafe EntityQuery GetExistingEntityQuery(ComponentSystemBase system)
        {
            ComponentType[] componentTypes;
            if (!componentTypesByJobType.TryGetValue(typeof(TJob), out componentTypes))
            {
                return default;
            }

            fixed (ComponentType* componentTypesPtr = componentTypes)
            {
                using var builder = new EntityQueryBuilder(Allocator.Temp, componentTypesPtr, componentTypes.Length);
                for (var i = 0; i != system.EntityQueries.Length; i++)
                {
                    if (system.EntityQueries[i].CompareQuery(builder))
                        return system.EntityQueries[i];
                }
            }

            return default;
        }

        private static ComponentType[] GetIJobCustomComponentTypes()
        {
            // Simplified for testing purposes. The real version uses reflection to get this data, and caches it for later.
            return new ComponentType[]
                {
                ComponentType.ReadWrite<T0>(),
                ComponentType.ReadWrite<Baz>()
                };
        }
    }

    public abstract partial class CustomSystemBase : SystemBase
    {
        public EntityQuery GetEntityQueryPublic(ComponentType[] componentTypes)
        {
            return GetEntityQuery(componentTypes);
        }
    }
    public partial class MySystem : CustomSystemBase
    {
        protected override void OnCreate()
        {
            // Creating a single entity for testing purposes
            Entity entity = EntityManager.CreateEntity(ComponentType.ReadWrite<Bar>(), ComponentType.ReadWrite<Baz>());

            DynamicBuffer<Bar> barBuffer = EntityManager.GetBuffer<Bar>(entity);
            barBuffer.Add(new Bar { num = 0 });
        }

        protected override void OnUpdate()
        {
            if (TestUtility<WrappedCustomJob, Bar>.MakeJob<float>(this, new WrappedCustomJob { toAdd = 10 }, out var resultJob, out var query))
            {
                Dependency = resultJob.Thing.ScheduleParallel(query, Dependency);
            }
        }

        public struct WrappedCustomJob : IJobCustom<Bar>
        {
            public int toAdd;

            public void Execute(Entity entity, ref Bar t0, ref Baz baz, NativeArray<Foo> foos)
            {
                // do custom logic here
                t0.num += toAdd;
            }
        }
    }

    public class UserGenericJobCode3 : ECSTestsFixture
    {
        [Test]
        public void ReflectionDataForVisibleGenerics()
        {
            var repro = World.GetOrCreateSystemManaged<MySystem>();
            var simulationGroup = World.GetOrCreateSystemManaged<SimulationSystemGroup>();
            simulationGroup.AddSystemToUpdateList(repro);
            World.Update();
        }
    }
}

namespace Unity.Entities.Tests.CustomerProvided.Forum3
{
    public abstract partial class CustomSystem<TComponent> : SystemBase where TComponent : unmanaged, IComponentData
    {
        protected interface IProcessor
        {
            void Execute(ref TComponent component);
        }

        [BurstCompile]
        protected struct WrapperJob<TProcessor> : IJobChunk where TProcessor : struct, IProcessor
        {
            public TProcessor processor;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // This job is not written to support queries with enableable component types.
                Assert.IsFalse(useEnabledMask);

                TComponent component = default;

                processor.Execute(ref component);
            }
        }

        protected WrapperJob<TProcessor> CreateJob<TProcessor>(TProcessor processor) where TProcessor : struct, IProcessor
        {
            return new WrapperJob<TProcessor>
            {
                processor = processor
            };
        }
    }

    public partial class TestSystem : CustomSystem<LocalTransform>
    {
        protected override void OnUpdate()
        {
            CreateJob(new TestProcessor
            {
            }).Run(EntityManager.UniversalQuery);
        }

        struct TestProcessor : IProcessor
        {
            public void Execute(ref LocalTransform transform)
            {
            }
        }
    }

    public class UserGenericJobCode4 : ECSTestsFixture
    {
        [Test]
        public void ReflectionDataForVisibleGenerics()
        {
            var repro = World.GetOrCreateSystemManaged<TestSystem>();
            repro.Update();
        }
    }

}
