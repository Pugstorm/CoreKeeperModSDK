using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Entities.SystemAPI;

namespace Unity.Entities.Tests
{
    readonly partial struct EntityTestAspect : IAspect
    {
        readonly RefRW<EcsTestDataEntity> _entityData;
        public void SetEntity(Entity entity) => _entityData.ValueRW.value1 = entity;
    }

    public partial class IdiomaticCSharpForEachIterationTests : ECSTestsFixture
    {
        public enum QueryExtension
        {
            All,
            Any,
            None,
            Disabled,
            Absent,
            Present,
            NoExtension
        }

        public enum QueryReturnType
        {
            ParenthesizedVariable,
            IdentifierName,
            TupleWithNamedElementsAndExplicitTypes,
            TupleWithNamedElementsAndImplicitTypes,
            TupleWithoutNamedElements,
            TupleWithDiscardedElement
        }

        public struct SystemQueryData : IComponentData
        {
            public QueryExtension extensionToTest;
            public QueryReturnType returnTypeToTest;
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        T GetManagedComponent<T>(Entity entity) where T : IComponentData => m_Manager.GetComponentObject<T>(entity);
#endif

        T GetAspect<T>(Entity entity) where T : struct, IAspect, IAspectCreate<T> => default(T).CreateAspect(entity, ref EmptySystem.CheckedStateRef);
        T GetAspect<T>(SystemHandle system) where T : struct, IAspect, IAspectCreate<T> => default(T).CreateAspect(system.m_Entity, ref EmptySystem.CheckedStateRef);
        T GetComponent<T>(Entity entity) where T : unmanaged, IComponentData => m_Manager.GetComponentData<T>(entity);
        T GetComponent<T>(SystemHandle system) where T : unmanaged, IComponentData => m_Manager.GetComponentData<T>(system);
        T GetSharedComponent<T>(Entity entity) where T : struct, ISharedComponentData => m_Manager.GetSharedComponentManaged<T>(entity);
        DynamicBuffer<T> GetDynamicBuffer<T>(Entity entity) where T : unmanaged, IBufferElementData => m_Manager.GetBuffer<T>(entity);

        bool TryGetComponent<T>(Entity entity, out T componentData) where T : unmanaged, IComponentData
        {
            if (m_Manager.HasComponent<T>(entity))
            {
                componentData = m_Manager.GetComponentData<T>(entity);
                return true;
            }
            componentData = default;
            return false;
        }

        public struct Multiplier : ISharedComponentData
        {
            public int Value { get; set; }
        }

        partial class IterateThroughUnmanagedSharedComponentSystem : SystemBase
        {
            protected override void OnCreate()
            {
                var multiplier = new Multiplier { Value = 5 };

                for (int i = 0; i < 1000; i++)
                {
                    var entity = EntityManager.CreateEntity();
                    EntityManager.AddComponentData(entity, new EcsTestData(inValue: entity.Index));
                    EntityManager.AddSharedComponentManaged(entity, multiplier);
                }
                base.OnCreate();
            }

            protected override void OnUpdate()
            {
                foreach (var (myAspect, multiplier) in Query<MyAspect, Multiplier>())
                {
                    var current = myAspect._Data.ValueRO.value;
                    myAspect._Data.ValueRW = new EcsTestData(inValue: current * multiplier.Value);
                }
            }
        }

        partial class IterateThroughComponent_WithOptionsSystem : SystemBase
        {
            public Entity Entity { get; private set; }

            protected override void OnCreate()
            {
                Entity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(Entity, new EcsTestData { value = 0 });
                EntityManager.AddComponentData(Entity, new Disabled());

                base.OnCreate();
            }

            protected override void OnUpdate()
            {
                foreach (var ecsTestData in Query<RefRW<EcsTestData>>().WithOptions(EntityQueryOptions.IncludeDisabledEntities))
                    ecsTestData.ValueRW.value += 1;
            }
        }

        struct EnableableAllTag : IComponentData, IEnableableComponent {}
        struct EnableableAnyTag : IComponentData, IEnableableComponent {}
        struct EnableableNoneTag : IComponentData, IEnableableComponent {}
        struct EnableableDisabledTag : IComponentData, IEnableableComponent {}
        struct EnableableAbsentTag : IComponentData, IEnableableComponent {}
        struct EnableablePresentTag : IComponentData, IEnableableComponent {}
        partial class IterateThroughEnableableComponents_TrackProcessedEntities : SystemBase
        {
            public NativeList<Entity> ProcessedEntities;
            protected override void OnCreate()
            {
                ProcessedEntities = new NativeList<Entity>(Allocator.Persistent);
            }

            protected override void OnDestroy()
            {
                ProcessedEntities.Dispose();
            }

            protected override void OnUpdate()
            {
                ProcessedEntities.Clear();
                foreach (var (_, entity) in Query<EnableableAllTag>()
                             .WithAny<EnableableAnyTag>()
                             .WithNone<EnableableNoneTag>()
                             .WithDisabled<EnableableDisabledTag>()
                             .WithAbsent<EnableableAbsentTag>()
                             .WithPresent<EnableablePresentTag>()
                             .WithEntityAccess())
                {
                    ProcessedEntities.Add(entity);
                }
            }
        }
        partial struct DisabledChangeFilterSystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponent<SumData>(state.SystemHandle);

                var archetype = state.EntityManager.CreateArchetype(typeof(EcsTestDataEnableable));

                const int entityCount = 1000;
                var allEntities = state.EntityManager.CreateEntity(archetype, entityCount, Allocator.Temp);

                for (int i = 0; i < entityCount; ++i)
                {
                    state.EntityManager.AddComponentData(allEntities[i], new EcsTestDataEnableable(1));
                    state.EntityManager.SetComponentEnabled<EcsTestDataEnableable>(allEntities[i], i % 2 != 0); // Set every other entity to be disabled
                }
            }

            public void OnUpdate(ref SystemState state)
            {
                foreach (var entity in state.EntityManager.UniversalQuery.ToEntityArray(state.WorldUpdateAllocator))
                    state.EntityManager.SetComponentData(entity, new EcsTestDataEnableable(2)); // Set ALL EcsTestDataEnableable values to 2, regardless of whether they are enabled or not

                ref var sumDisabledChangeFilter = ref state.EntityManager.GetComponentDataRW<SumData>(state.SystemHandle).ValueRW;

                // The next line tests that we generate the correct backing entity query -- we should only iterate through EcsTestDataEnableable values that are disabled
                foreach (var ecsTestDataEnableable in Query<EcsTestDataEnableable>().WithDisabled<EcsTestDataEnableable>().WithChangeFilter<EcsTestDataEnableable>())
                    sumDisabledChangeFilter.sum += ecsTestDataEnableable.value;
            }
        }
        partial struct IterateThroughEnableableComponentsWithVariousExtensionsSystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponent<SumData>(state.SystemHandle);
                state.EntityManager.AddComponent<SystemQueryData>(state.SystemHandle);

                var archetype = state.EntityManager.CreateArchetype(typeof(EcsTestDataEnableable));
                const int entityCount = 1000;
                var allEntities = state.EntityManager.CreateEntity(archetype, entityCount, Allocator.Temp);

                for (int i = 0; i < entityCount; ++i)
                    state.EntityManager.SetComponentEnabled<EcsTestDataEnableable>(allEntities[i], i % 5 != 0);
            }

            public void OnUpdate(ref SystemState state)
            {
                var queryExtensionToTest = state.EntityManager.GetComponentData<SystemQueryData>(state.SystemHandle).extensionToTest;
                ref var numDisabledComponents = ref state.EntityManager.GetComponentDataRW<SumData>(state.SystemHandle).ValueRW;

                switch (queryExtensionToTest)
                {
                    case QueryExtension.Disabled:
                        foreach (var _ in Query<EnabledRefRO<EcsTestDataEnableable>>().WithDisabled<EcsTestDataEnableable>())
                            numDisabledComponents.sum++;
                        break;
                    case QueryExtension.None:
                        foreach (var _ in Query<EnabledRefRO<EcsTestDataEnableable>>().WithNone<EcsTestDataEnableable>())
                            numDisabledComponents.sum++;
                        break;
                    case QueryExtension.Any:
                        foreach (var _ in Query<EnabledRefRO<EcsTestDataEnableable>>().WithAny<EcsTestDataEnableable>())
                            numDisabledComponents.sum++;
                        break;
                    case QueryExtension.All:
                        foreach (var _ in Query<EnabledRefRO<EcsTestDataEnableable>>().WithAll<EcsTestDataEnableable>())
                            numDisabledComponents.sum++;
                        break;
                }
            }
        }

        public struct SumData : IComponentData
        {
            public int sum;
        }

        public struct SharedComponentFilterData : IComponentData
        {
            public bool IsManaged;
        }

        partial class IterateThroughComponent_WithEnableableComponent_EnableSystem : SystemBase
        {
            public Entity Entity { get; private set; }
            private EntityQuery _query;
            private ComponentTypeHandle<EcsTestDataEnableable> _typeHandle;

            protected override void OnCreate()
            {
                Entity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(Entity, new EcsTestDataEnableable { value = 0 });
                EntityManager.AddComponentData(Entity, new EcsTestData { value=0 });
                EntityManager.SetComponentEnabled<EcsTestDataEnableable>(Entity, false);

                using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestDataEnableable>()
                    .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState);
                _query = GetEntityQuery(queryBuilder);

                _typeHandle = GetComponentTypeHandle<EcsTestDataEnableable>(false);

                base.OnCreate();
            }

            struct EnableTheThingJob : IJobChunk
            {
                public ComponentTypeHandle<EcsTestDataEnableable> TypeHandle;
                public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
                {
                    chunk.SetComponentEnabled(ref TypeHandle, 0, true);
                }
            }

            protected override void OnUpdate()
            {
                // Schedule a job to re-enable the single entity
                _typeHandle.Update(this);
                Dependency = new EnableTheThingJob
                {
                    TypeHandle = _typeHandle,
                }.ScheduleParallel(_query, Dependency);
            }
        }

        partial class IterateThroughComponent_WithEnableableComponent_WithRunningJobSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                // We expect the job scheduled by the previous system to be automatically completed by this foreach
                // before it begins its iteration
                foreach (var ecsTestData in Query<RefRW<EcsTestData>>().WithAll<EcsTestDataEnableable>())
                    ecsTestData.ValueRW.value += 1;
            }
        }

        partial struct IterateThroughComponent_WithoutRefRO : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponent<SumData>(state.SystemHandle);
                for (int i = 1; i <= 10; i++)
                {
                    var entity = state.EntityManager.CreateEntity();
                    state.EntityManager.AddComponentData(entity, new EcsTestData(i));
                }
            }

            public void OnUpdate(ref SystemState state)
            {
                ref var sumData = ref state.EntityManager.GetComponentDataRW<SumData>(state.SystemHandle).ValueRW;
                foreach (var ecsTestData in Query<EcsTestData>())
                    sumData.sum += ecsTestData.value;
            }
        }

        partial struct IterateThroughTagComponents : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent(
                    entity,
                    new ComponentTypeSet(
                        ComponentType.ReadOnly<EcsTestTag>(),
                        ComponentType.ReadWrite<AnotherEcsTestTag>(),
                        ComponentType.ReadWrite<EcsTestTagEnableable>()));
            }

            public void OnUpdate(ref SystemState state)
            {
                var taggedEntity = GetSingletonEntity<EcsTestTag>();

                foreach (var (tagRefRO, tagRefRW, enabledRefRw, entity) in Query<RefRO<EcsTestTag>, RefRW<AnotherEcsTestTag>, EnabledRefRW<EcsTestTagEnableable>>().WithEntityAccess())
                {
                    Assert.That(entity == taggedEntity);

                    Assert.DoesNotThrow(() => Debug.Log(tagRefRO.ValueRO));

                    Assert.DoesNotThrow(() => Debug.Log(tagRefRW.ValueRW));
                    Assert.DoesNotThrow(() => Debug.Log(tagRefRW.ValueRO));

                    enabledRefRw.ValueRW = !enabledRefRw.ValueRO;
                    Assert.IsFalse(enabledRefRw.ValueRO);
                }
            }
        }

        partial class IterateThroughComponent_WithChangeFilterSystem : SystemBase
        {
            public Entity Entity { get; private set; }

            protected override void OnCreate()
            {
                Entity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(Entity, new EcsTestData { value = 0 });

                base.OnCreate();
            }

            protected override void OnUpdate()
            {
                const int increment = 1;

                foreach (var ecsTestData in Query<RefRW<EcsTestData>>().WithChangeFilter<EcsTestData>())
                    ecsTestData.ValueRW.value += increment; // Bump version number to 1

                AfterUpdateVersioning();
                BeforeUpdateVersioning();

                // This should not run
                foreach (var ecsTestData in Query<RefRW<EcsTestData>>().WithChangeFilter<EcsTestData>())
                    ecsTestData.ValueRW.value += increment;
            }
        }

        partial struct IterateThroughComponent_WithSharedComponentFilterSystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponent<SumData>(state.SystemHandle);
                state.EntityManager.AddComponent<SharedComponentFilterData>(state.SystemHandle);
            }

            public void OnUpdate(ref SystemState state)
            {
                var sharedComponentFilterData = state.EntityManager.GetComponentData<SharedComponentFilterData>(state.SystemHandle);
                ref var sumData = ref state.EntityManager.GetComponentDataRW<SumData>(state.SystemHandle).ValueRW;

                if (sharedComponentFilterData.IsManaged)
                    foreach (var result in Query<RefRO<EcsTestData>>().WithSharedComponentFilterManaged(new ManagedSharedData1(val: 1)))
                        sumData.sum += result.ValueRO.value;
                else
                    foreach (var result in Query<RefRO<EcsTestData>>().WithSharedComponentFilter(new SharedData1(1)))
                        sumData.sum += result.ValueRO.value;

            }
        }

        partial struct SameBackingQuery_NoInterferenceSystem : ISystem
        {
            public void OnUpdate(ref SystemState state)
            {
                foreach (var ecsTestData in Query<RefRW<EcsTestData>>().WithSharedComponentFilter(new SharedData1(1)))
                    ecsTestData.ValueRW.value = 1111;

                foreach (var ecsTestData in Query<RefRW<EcsTestData>>().WithAll<SharedData1>())
                    ecsTestData.ValueRW.value = 2222;
            }
        }

        partial class IterateThroughManagedSharedComponentSystem : SystemBase
        {
            protected override void OnCreate()
            {
                var sharedString = new EcsStringSharedComponent { Value = "Hello world!" };

                for (int i = 0; i < 1000; i++)
                {
                    var entity = EntityManager.CreateEntity();
                    EntityManager.AddComponentData(entity, new EcsTestData(inValue: entity.Index));
                    EntityManager.AddSharedComponentManaged(entity, sharedString);
                }
                base.OnCreate();
            }

            protected override void OnUpdate()
            {
                foreach (var (myAspect, sharedString) in Query<MyAspect, EcsStringSharedComponent>())
                {
                    var current = myAspect._Data.ValueRO.value;
                    myAspect._Data.ValueRW = new EcsTestData(inValue: current * sharedString.Value.Length);
                }
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        partial class IterateThroughManagedComponentSystem : SystemBase
        {
            protected override void OnCreate()
            {
                for (int i = 0; i < 1000; i++)
                {
                    var entity = EntityManager.CreateEntity();
                    EntityManager.AddComponentData(entity, new EcsTestData(inValue: entity.Index));
                    EntityManager.AddComponentData(entity, new EcsTestManagedComponent { nullField = new ClassWithClassFields { ClassWithString = new ClassWithString { String = "Hello World!" }}});
                }
                base.OnCreate();
            }

            protected override void OnUpdate()
            {
                foreach (var (myAspect, managedComponent) in Query<MyAspect, EcsTestManagedComponent>())
                {
                    myAspect._Data.ValueRW = new EcsTestData(inValue: int.MaxValue);
                    managedComponent.nullField = null;
                }
            }
        }
#endif
        partial class IterateThroughAspectsAndComponents_WithNestedForEachSystem : SystemBase
        {
            public NativeArray<Entity> CreatedEntities { get; private set; }

            protected override void OnCreate()
            {
                var entity0 = EntityManager.CreateEntity();
                EntityManager.AddComponent<EcsTestData>(entity0);
                EntityManager.AddComponent<EcsTestData2>(entity0);

                var entity1 = EntityManager.CreateEntity();
                EntityManager.AddComponent<EcsTestData3>(entity1);

                var entity2 = EntityManager.CreateEntity();
                EntityManager.AddComponent<EcsTestData3>(entity2);

                CreatedEntities = new NativeArray<Entity>(new[] { entity0, entity1, entity2 }, Allocator.Persistent);
            }

            protected override void OnUpdate()
            {
                foreach (var myAspect in Query<MyAspect>())
                {
                    myAspect._Data.ValueRW = new EcsTestData(inValue: 1);
                    myAspect._Data2.ValueRW = new EcsTestData2(inValue: 2);

                    foreach (var ecsTestData3 in Query<RefRW<EcsTestData3>>())
                    {
                        ecsTestData3.ValueRW.value0 = myAspect._Data.ValueRO.value;
                        ecsTestData3.ValueRW.value1 = myAspect._Data2.ValueRO.value0;
                        ecsTestData3.ValueRW.value2 = myAspect._Data2.ValueRO.value1;
                    }
                }
            }
        }

        partial struct IterateThroughAspectsAndComponentsSystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponent(state.SystemHandle,
                    new ComponentTypeSet(new[]
                    {
                        ComponentType.ReadWrite<SystemQueryData>(),
                        ComponentType.ReadWrite<EcsTestData>(),
                        ComponentType.ReadWrite<EcsTestData2>(),
                        ComponentType.ReadWrite<EcsTestData3>(),
                        ComponentType.ReadOnly<EcsTestTag>(),
                        ComponentType.ReadOnly<EcsTestDataEnableable>()
                    }));
                state.EntityManager.SetComponentEnabled<EcsTestDataEnableable>(state.SystemHandle, true);
            }

            public void OnUpdate(ref SystemState state)
            {
                var queryExtensionToTest = state.EntityManager.GetComponentData<SystemQueryData>(state.SystemHandle).extensionToTest;
                switch (queryExtensionToTest)
                {
                    case QueryExtension.NoExtension:
                        foreach (var (myAspect, ecsTestData3) in Query<MyAspect, RefRW<EcsTestData3>>().WithOptions(EntityQueryOptions.IncludeSystems))
                        {
                            myAspect._Data.ValueRW = new EcsTestData { value = 10 };
                            myAspect._Data2.ValueRW = new EcsTestData2 { value0 = 20, value1 = 30 };

                            ecsTestData3.ValueRW.value0 = 10;
                            ecsTestData3.ValueRW.value1 = 20;
                            ecsTestData3.ValueRW.value2 = 30;
                        }
                        break;
                    case QueryExtension.All:
                        foreach (var (myAspect, ecsTestData3) in Query<MyAspect, RefRW<EcsTestData3>>().WithAll<EcsTestTag>().WithOptions(EntityQueryOptions.IncludeSystems))
                        {
                            myAspect._Data.ValueRW = new EcsTestData { value = 10 };
                            myAspect._Data2.ValueRW = new EcsTestData2 { value0 = 20, value1 = 30 };

                            ecsTestData3.ValueRW.value0 = 10;
                            ecsTestData3.ValueRW.value1 = 20;
                            ecsTestData3.ValueRW.value2 = 30;
                        }
                        break;
                    case QueryExtension.Any:
                        foreach (var (myAspect, ecsTestData3) in Query<MyAspect, RefRW<EcsTestData3>>().WithAny<EcsIntElement>().WithAny<EcsTestTag>().WithOptions(EntityQueryOptions.IncludeSystems))
                        {
                            myAspect._Data.ValueRW = new EcsTestData { value = 10 };
                            myAspect._Data2.ValueRW = new EcsTestData2 { value0 = 20, value1 = 30 };

                            ecsTestData3.ValueRW.value0 = 10;
                            ecsTestData3.ValueRW.value1 = 20;
                            ecsTestData3.ValueRW.value2 = 30;
                        }
                        break;
                    case QueryExtension.None:
                        foreach (var (myAspect, ecsTestData3) in Query<MyAspect, RefRW<EcsTestData3>>().WithNone<EcsTestTag>().WithOptions(EntityQueryOptions.IncludeSystems))
                        {
                            myAspect._Data.ValueRW = new EcsTestData { value = 10 };
                            myAspect._Data2.ValueRW = new EcsTestData2 { value0 = 20, value1 = 30 };

                            ecsTestData3.ValueRW.value0 = 10;
                            ecsTestData3.ValueRW.value1 = 20;
                            ecsTestData3.ValueRW.value2 = 30;
                        }
                        break;
                    case QueryExtension.Absent:
                        foreach (var (myAspect, ecsTestData3) in Query<MyAspect, RefRW<EcsTestData3>>().WithAbsent<EcsTestTag>().WithOptions(EntityQueryOptions.IncludeSystems))
                        {
                            myAspect._Data.ValueRW = new EcsTestData { value = 10 };
                            myAspect._Data2.ValueRW = new EcsTestData2 { value0 = 20, value1 = 30 };

                            ecsTestData3.ValueRW.value0 = 10;
                            ecsTestData3.ValueRW.value1 = 20;
                            ecsTestData3.ValueRW.value2 = 30;
                        }
                        break;
                    case QueryExtension.Disabled:
                        foreach (var (myAspect, ecsTestData3) in Query<MyAspect, RefRW<EcsTestData3>>().WithDisabled<EcsTestDataEnableable>().WithOptions(EntityQueryOptions.IncludeSystems))
                        {
                            myAspect._Data.ValueRW = new EcsTestData { value = 10 };
                            myAspect._Data2.ValueRW = new EcsTestData2 { value0 = 20, value1 = 30 };

                            ecsTestData3.ValueRW.value0 = 10;
                            ecsTestData3.ValueRW.value1 = 20;
                            ecsTestData3.ValueRW.value2 = 30;
                        }
                        break;
                    case QueryExtension.Present:
                        foreach (var (myAspect, ecsTestData3) in Query<MyAspect, RefRW<EcsTestData3>>().WithPresent<EcsTestDataEnableable>().WithOptions(EntityQueryOptions.IncludeSystems))
                        {
                            myAspect._Data.ValueRW = new EcsTestData { value = 10 };
                            myAspect._Data2.ValueRW = new EcsTestData2 { value0 = 20, value1 = 30 };

                            ecsTestData3.ValueRW.value0 = 10;
                            ecsTestData3.ValueRW.value1 = 20;
                            ecsTestData3.ValueRW.value2 = 30;
                        }
                        break;
                }
            }
        }

        partial struct IterateThroughAspectsAndComponents_WithDifferentReturnTypesSystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponent(state.SystemHandle, ComponentType.ReadWrite<SystemQueryData>());

                for (int i = 0; i < 2; i++)
                {
                    var entity = state.EntityManager.CreateEntity();
                    state.EntityManager.AddComponent(
                        entity,
                        new ComponentTypeSet(
                            ComponentType.ReadWrite<EcsTestData>(),
                            ComponentType.ReadWrite<EcsTestData2>(),
                            ComponentType.ReadWrite<EcsTestData3>(),
                            ComponentType.ReadOnly<EcsTestTag>()));
                }
            }

            public void OnUpdate(ref SystemState state)
            {
                int entityCount = 0;
                var ReturnTypeToTest = state.EntityManager.GetComponentData<SystemQueryData>(state.SystemHandle).returnTypeToTest;
                switch (ReturnTypeToTest)
                {
                    case QueryReturnType.IdentifierName:
                        foreach (var queryReturnType in Query<MyAspect, RefRW<EcsTestData3>>())
                        {
                            entityCount++;

                            queryReturnType.Item1._Data.ValueRW = new EcsTestData { value = entityCount };

                            var myAspect = queryReturnType.Item1;
                            myAspect._Data2.ValueRW = new EcsTestData2 { value0 = entityCount, value1 = entityCount };

                            queryReturnType.Item2.ValueRW.value0 = entityCount;

                            // Our source-generator solution changes the type of `queryReturnType.Item2` to `Unity.Entities.InternalCompilerInterface.UncheckedRefRW<Unity.Entities.Tests.EcsTestData3>`
                            // behind the scenes so that calling `ValueRW` and `ValueRO` will not trigger safety checks. (Safety checks are only crucial when dealing with long-lived `RefRW`/`RefRO` instances.)
                            // This change behind the scenes is not exposed to users, and the next line tests that the usage of explicit types (`RefRW<EcsTestData3>` in this case) is correctly
                            // supported.
                            RefRW<EcsTestData3> ecsTestData3 = queryReturnType.Item2;
                            ecsTestData3.ValueRW.value1 = entityCount;
                            ecsTestData3.ValueRW.value2 = entityCount;
                        }
                        break;
                    case QueryReturnType.TupleWithNamedElementsAndExplicitTypes:
                        foreach ((RefRW<EcsTestData> ecsTestData, RefRW<EcsTestData2> ecsTestData2, RefRW<EcsTestData3> ecsTestData3) queryReturnType
                                 in Query<RefRW<EcsTestData>, RefRW<EcsTestData2>, RefRW<EcsTestData3>>())
                        {
                            entityCount++;

                            queryReturnType.ecsTestData.ValueRW = new EcsTestData { value = entityCount };
                            queryReturnType.ecsTestData2.ValueRW = new EcsTestData2 { value0 = entityCount, value1 = entityCount };

                            queryReturnType.ecsTestData3.ValueRW.value0 = entityCount;
                            queryReturnType.ecsTestData3.ValueRW.value1 = entityCount;
                            queryReturnType.ecsTestData3.ValueRW.value2 = entityCount;
                        }
                        break;
                    case QueryReturnType.TupleWithNamedElementsAndImplicitTypes:
                        foreach ((var myAspect, var ecsTestData3) in Query<MyAspect, RefRW<EcsTestData3>>())
                        {
                            entityCount++;

                            myAspect._Data.ValueRW = new EcsTestData { value = entityCount };
                            myAspect._Data2.ValueRW = new EcsTestData2 { value0 = entityCount, value1 = entityCount };

                            ecsTestData3.ValueRW.value0 = entityCount;
                            ecsTestData3.ValueRW.value1 = entityCount;
                            ecsTestData3.ValueRW.value2 = entityCount;
                        }
                        break;
                    case QueryReturnType.TupleWithoutNamedElements:
                        foreach ((MyAspect, RefRW<EcsTestData3>) queryReturnType in Query<MyAspect, RefRW<EcsTestData3>>())
                        {
                            entityCount++;

                            queryReturnType.Item1._Data.ValueRW = new EcsTestData { value = entityCount };

                            var myAspect = queryReturnType.Item1;
                            myAspect._Data2.ValueRW = new EcsTestData2 { value0 = entityCount, value1 = entityCount };

                            queryReturnType.Item2.ValueRW.value0 = entityCount;

                            var ecsTestData3 = queryReturnType.Item2;
                            ecsTestData3.ValueRW.value1 = entityCount;
                            ecsTestData3.ValueRW.value2 = entityCount;
                        }
                        break;
                    case QueryReturnType.ParenthesizedVariable:
                        foreach (var (myAspect, ecsTestData3) in Query<MyAspect, RefRW<EcsTestData3>>())
                        {
                            entityCount++;

                            myAspect._Data.ValueRW = new EcsTestData { value = entityCount };
                            myAspect._Data2.ValueRW = new EcsTestData2 { value0 = entityCount, value1 = entityCount };

                            ecsTestData3.ValueRW.value0 = entityCount;
                            ecsTestData3.ValueRW.value1 = entityCount;
                            ecsTestData3.ValueRW.value2 = entityCount;
                        }
                        break;
                    case QueryReturnType.TupleWithDiscardedElement:
                        foreach (var (myAspect, ecsTestData3, _) in Query<MyAspect, RefRW<EcsTestData3>>().WithEntityAccess())
                        {
                            entityCount++;

                            myAspect._Data.ValueRW = new EcsTestData { value = entityCount };
                            myAspect._Data2.ValueRW = new EcsTestData2 { value0 = entityCount, value1 = entityCount };

                            ecsTestData3.ValueRW.value0 = entityCount;
                            ecsTestData3.ValueRW.value1 = entityCount;
                            ecsTestData3.ValueRW.value2 = entityCount;
                        }
                        break;
                }
            }
        }

        partial struct IterateThroughAspectsAndComponents_EnsureDependencyCompletedSystem_Schedule : ISystem
        {
            public NativeArray<int> syncHandle;
            public void OnCreate(ref SystemState state) =>
                state.EntityManager.CreateEntity(
                    ComponentType.ReadWrite<EcsTestData>(),
                    ComponentType.ReadWrite<EcsTestData2>(),
                    ComponentType.ReadWrite<EcsTestData3>(),
                    ComponentType.ReadOnly<EcsTestTag>()
                );

            public partial struct TestJob : IJobEntity
            {
                public NativeArray<int> syncHandle;
                void Execute(MyAspect myAspect, ref EcsTestData3 ecsTestData3)
                {
                    syncHandle[0] = 1;

                    myAspect._Data.ValueRW = new EcsTestData(10);
                    myAspect._Data2.ValueRW = new EcsTestData2(20);

                    ecsTestData3 = new EcsTestData3(30);
                }
            }

            public void OnUpdate(ref SystemState state) => new TestJob{syncHandle = syncHandle}.Schedule();
            }

        partial struct IterateThroughAspectsAndComponents_EnsureDependencyCompletedSystem_MainThread : ISystem
        {
            public NativeArray<int> syncHandle;
            public bool ForceDependencyCompletion { get; set; }
            // Writing to syncHandle will fail unless all jobs are done writing to it.
            // Original idea: https://unity.slack.com/archives/CE7DZN2H1/p1656923150762329?thread_ts=1656916738.070989&cid=CE7DZN2H1
            public void OnUpdate(ref SystemState state)
            {
                if (ForceDependencyCompletion)
                {
                    // ContainerXXXX.CompleteDependencyBeforeRW(ref SystemState) is generated before every foreach statement
                    foreach (var _ in Query<MyAspect, RefRW<EcsTestData3>>()) {}
                    syncHandle[0] = 2;
                }
                else
                {
                    var syncHandle1 = syncHandle;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    Assert.Throws<InvalidOperationException>(() =>
                    {
                        syncHandle1[0] = 2;
                    });
#endif
                    state.CompleteDependency();
                    syncHandle1[0] = 2;
                }
            }
        }

        public partial struct IterateThroughAspectsAndComponents_MultipleUsersWrittenPartsInSystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponent(state.SystemHandle,
                    new ComponentTypeSet(
                        ComponentType.ReadWrite<EcsTestData>(),
                        ComponentType.ReadWrite<EcsTestData2>(),
                        ComponentType.ReadWrite<EcsTestData3>(),
                        ComponentType.ReadOnly<EcsTestTag>()));

                state.EntityManager.SetComponentData(state.SystemHandle, new EcsTestData(10));
                state.EntityManager.SetComponentData(state.SystemHandle, new EcsTestData2(20));
                state.EntityManager.SetComponentData(state.SystemHandle, new EcsTestData3(30));
            }

            public void OnUpdate(ref SystemState state)
            {
                foreach (var myAspect in Query<MyAspect>().WithOptions(EntityQueryOptions.IncludeSystems))
                {
                    myAspect._Data.ValueRW.value += myAspect._Data2.ValueRO.value0 + myAspect._Data2.ValueRO.value1; // 10 + 20 + 20 == 50
                    OnUpdate1(ref state);
                }
            }
        }

        public partial struct IterateThroughAspectsAndComponents_MultipleUsersWrittenPartsInSystem
        {
            public void OnUpdate1(ref SystemState state)
            {
                foreach (var (myAspect, ecsTestData3) in SystemAPI.Query<MyAspect, RefRW<EcsTestData3>>().WithOptions(EntityQueryOptions.IncludeSystems))
                {
                    ecsTestData3.ValueRW.value0 += myAspect._Data.ValueRO.value; // 30 + 50 == 80
                    ecsTestData3.ValueRW.value1 += myAspect._Data.ValueRO.value; // 30 + 50 == 80
                    ecsTestData3.ValueRW.value2 += myAspect._Data.ValueRO.value; // 30 + 50 == 80
                }
            }
        }

        internal struct IntBufferElement : IBufferElementData
        {
            internal int Value { get; set; }
        }

        partial struct IterateThroughDynamicBufferSystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                const int numEntities = 100;

                for (int i = 0; i < numEntities; i++)
                {
                    var entity = state.EntityManager.CreateEntity();
                    state.EntityManager.AddComponent<EcsTestData>(entity);
                    state.EntityManager.SetComponentData(entity, new EcsTestData(inValue: 10));

                    var buffer = state.EntityManager.AddBuffer<IntBufferElement>(entity);

                    buffer.Add(entity.Index % 5 == 0
                        ? new IntBufferElement { Value = 5 }
                        : new IntBufferElement { Value = 0 });
                }
            }

            public void OnUpdate(ref SystemState state)
            {
                foreach (var (ecsTestData, intBufferElements) in Query<RefRO<EcsTestData>, DynamicBuffer<IntBufferElement>>())
                {
                    var first = intBufferElements[0];
                    var second = new IntBufferElement { Value = first.Value * ecsTestData.ValueRO.value };

                    intBufferElements.Add(second);
                }
            }
        }

        partial struct IterateThroughSingleComponent_WithoutRefSystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponent<EcsTestData>(state.SystemHandle);

                for (int i = 0; i <= 10; i++)
                {
                    var entity = state.EntityManager.CreateEntity();
                    state.EntityManager.AddComponentData(entity, new EcsTestData3(inValue: i));
                }
            }

            public void OnUpdate(ref SystemState state)
            {
                ref var sumData = ref state.EntityManager.GetComponentDataRW<EcsTestData>(state.SystemHandle).ValueRW;

                foreach (var (ecsTestData3, _) in Query<EcsTestData3>().WithEntityAccess())
                    sumData.value += ecsTestData3.value0;
            }
        }

        partial struct IterateThroughSingleComponentSystem : ISystem
        {
            public void OnCreate(ref SystemState state) =>
                state.EntityManager.AddComponent(state.SystemHandle, ComponentType.ReadWrite<EcsTestData3>());

            public void OnUpdate(ref SystemState state)
            {
                foreach (var ecsTestData3 in Query<RefRW<EcsTestData3>>().WithOptions(EntityQueryOptions.IncludeSystems))
                {
                    var testData3_ValueRW = ecsTestData3;
                    testData3_ValueRW.ValueRW.value0 = 10;

                    ecsTestData3.ValueRW.value1 = 10;
                    ecsTestData3.ValueRW.value2 = 10;
                }
            }
        }

        partial struct IterateThroughEmptyComponentSystem : ISystem
        {
            public void OnCreate(ref SystemState state) => state.EntityManager.CreateEntity(ComponentType.ReadOnly<EcsTestTag>());

            public void OnUpdate(ref SystemState state)
            {
                int numEntitiesWithTestTag = 0;

                foreach (var _ in Query<EcsTestTag>())
                    numEntitiesWithTestTag++;

                Assert.AreEqual(1, numEntitiesWithTestTag);
            }
        }

        partial struct IterateThroughValueTypeComponentsSystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponent(state.SystemHandle,
                    new ComponentTypeSet(
                        ComponentType.ReadOnly<EcsTestData2>(),
                        ComponentType.ReadWrite<EcsTestData3>()));
                state.EntityManager.SetComponentData(state.SystemHandle, new EcsTestData2(inValue: 10));
            }

            public void OnUpdate(ref SystemState state)
            {
                foreach (var (ecsTestData3, ecsTestData2) in Query<RefRW<EcsTestData3>, RefRO<EcsTestData2>>().WithOptions(EntityQueryOptions.IncludeSystems))
                {
                    var testData3_ValueRW = ecsTestData3;
                    testData3_ValueRW.ValueRW.value0 = 1 * ecsTestData2.ValueRO.value0;

                    ecsTestData3.ValueRW.value1 = 2 * ecsTestData2.ValueRO.value0;
                    ecsTestData3.ValueRW.value2 = 3 * ecsTestData2.ValueRO.value0;
                }
            }
        }

        partial struct ForEachIterationOverComponents_WithEntityAccessSystem : ISystem
        {
            public void OnCreate(ref SystemState state) =>
                state.EntityManager.AddComponent(state.SystemHandle, ComponentType.ReadWrite<EcsTestDataEntity>());

            public void OnUpdate(ref SystemState state)
            {
                foreach (var (data, entity) in Query<RefRW<EcsTestDataEntity>>().WithEntityAccess().WithOptions(EntityQueryOptions.IncludeSystems))
                    data.ValueRW.value1 = entity;
            }
        }

        partial struct ForEachIterationOverComponents_WithEntityAccessToTupleSystem : ISystem
        {
            public void OnCreate(ref SystemState state) =>
                state.EntityManager.AddComponent(state.SystemHandle, ComponentType.ReadWrite<EcsTestDataEntity>());

            public void OnUpdate(ref SystemState state)
            {
                foreach (var item in Query<RefRW<EcsTestDataEntity>>().WithEntityAccess().WithOptions(EntityQueryOptions.IncludeSystems))
                    item.Item1.ValueRW.value1 = item.Item2;
            }
        }

        partial struct ForEachIterationOverAspect_WithEntityAccessSystem : ISystem
        {
            public void OnCreate(ref SystemState state) =>
                state.EntityManager.AddComponent(state.SystemHandle, ComponentType.ReadWrite<EcsTestDataEntity>());

            public void OnUpdate(ref SystemState state)
            {
                foreach (var (aspect, entity) in Query<EntityTestAspect>().WithEntityAccess().WithOptions(EntityQueryOptions.IncludeSystems))
                    aspect.SetEntity(entity);
            }
        }

        struct MartialArtsAbilityComponent : IEnableableComponent, IComponentData
        {
            public int Value;
        }

        struct LinearVelocity : IComponentData
        {
            public float2 Value;
        }

        partial struct IterateThroughEnabledComponentsSystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponent<MartialArtsAbilityComponent>(state.SystemHandle);

                for (int i = 1; i <= 8; i++)
                {
                    var e = state.EntityManager.CreateEntity();

                    state.EntityManager.AddComponentData(e, new EcsTestData());

                    state.EntityManager.AddComponentData(e, new LinearVelocity());

                    state.EntityManager.AddComponentData(e, new MartialArtsAbilityComponent{Value = i});
                    bool isPowerOfTwo = (i & (i-1)) == 0;
                    state.EntityManager.SetComponentEnabled<MartialArtsAbilityComponent>(e, isPowerOfTwo);
                }
            }

            public void OnUpdate(ref SystemState state)
            {
                ref var sumData = ref state.EntityManager.GetComponentDataRW<MartialArtsAbilityComponent>(state.SystemHandle).ValueRW;
                foreach (var component in Query<MyAspect, MartialArtsAbilityComponent>().WithAll<LinearVelocity>())
                    sumData.Value += component.Item2.Value;
            }
        }
        partial struct IterateThroughValueTypeEnableableComponentSystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponent<MartialArtsAbilityComponent>(state.SystemHandle);

                var entity1 = state.EntityManager.CreateEntity();

                state.EntityManager.AddComponentData(entity1, new MartialArtsAbilityComponent{ Value = 1 });
                state.EntityManager.SetComponentEnabled<MartialArtsAbilityComponent>(entity1, true);

                var entity2 = state.EntityManager.CreateEntity();

                state.EntityManager.AddComponentData(entity2, new MartialArtsAbilityComponent{ Value = 2 });
                state.EntityManager.SetComponentEnabled<MartialArtsAbilityComponent>(entity2, true);
            }

            public void OnUpdate(ref SystemState state)
            {
                ref var sumData = ref state.EntityManager.GetComponentDataRW<MartialArtsAbilityComponent>(state.SystemHandle).ValueRW;
                foreach (var component in Query<MartialArtsAbilityComponent>())
                    sumData.Value += component.Value;
            }
        }
        struct DisabledData : IComponentData
        {
            public bool IsComponentEnabled;
            public bool DisableComponent;
        }
        public partial struct IterateThroughValueTypeEnableableComponentRefSystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                var entity1 = state.EntityManager.CreateEntity();

                state.EntityManager.AddComponentData(entity1, new MartialArtsAbilityComponent{ Value = 1 });
                state.EntityManager.SetComponentEnabled<MartialArtsAbilityComponent>(entity1, true);
                state.EntityManager.AddComponent(state.SystemHandle, ComponentType.ReadWrite<DisabledData>());
            }

            public void OnUpdate(ref SystemState state)
            {
                ref var DisabledState = ref state.EntityManager.GetComponentDataRW<DisabledData>(state.SystemHandle).ValueRW;
                if (DisabledState.DisableComponent)
                {
                    foreach (var componentRef in Query<EnabledRefRW<MartialArtsAbilityComponent>>())
                    {
                        componentRef.ValueRW = false;
                        DisabledState.IsComponentEnabled = componentRef.ValueRO;
                    }
                }
                else
                    foreach (var componentRef in Query<EnabledRefRO<MartialArtsAbilityComponent>>())
                        DisabledState.IsComponentEnabled = componentRef.ValueRO;
            }
        }

        [Test]
        public void ForEachIterationWithDifferentReturnTypesFromQueryEnumerable([Values] QueryReturnType queryReturnType)
        {
            var systemHandle = World.GetOrCreateSystem<IterateThroughAspectsAndComponents_WithDifferentReturnTypesSystem>();

            ref var queryData = ref World.EntityManager.GetComponentDataRW<SystemQueryData>(systemHandle).ValueRW;
            queryData.returnTypeToTest = queryReturnType;
            systemHandle.Update(World.Unmanaged);

            var entityQuery = World.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>(), ComponentType.ReadWrite<EcsTestData2>(), ComponentType.ReadWrite<EcsTestData3>());
            var allEntities = entityQuery.ToEntityArray(Allocator.Temp);

            Assert.AreEqual(2, allEntities.Length);

            for (int i = 0; i < allEntities.Length; i++)
            {
                var entity = allEntities[i];

                var ecsTestData = GetComponent<EcsTestData>(entity);
                var ecsTestData2 = GetComponent<EcsTestData2>(entity);
                var ecsTestData3 = GetComponent<EcsTestData3>(entity);

                Assert.AreEqual(i + 1, ecsTestData.value);
                Assert.AreEqual(i + 1, ecsTestData2.value0);
                Assert.AreEqual(i + 1, ecsTestData2.value1);

                Assert.AreEqual(i + 1, ecsTestData3.value0);
                Assert.AreEqual(i + 1, ecsTestData3.value1);
                Assert.AreEqual(i + 1, ecsTestData3.value2);
            }

            allEntities.Dispose();
        }

        [Test]
        public void ForEachIteration_ThroughUnmanagedSharedComponent()
        {
            var system = World.GetOrCreateSystemManaged<IterateThroughUnmanagedSharedComponentSystem>();
            system.Update();

            var entityQuery = system.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<EcsTestData>(), ComponentType.ReadOnly<Multiplier>());
            var allEntities = entityQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in allEntities)
            {
                var myAspect = GetAspect<MyAspect>(entity);
                Assert.AreEqual(entity.Index * 5, myAspect._Data.ValueRO.value);
            }
            allEntities.Dispose();
        }

        [Test]
        public void ForEachIteration_ThroughComponent_WithOptions()
        {
            var system = World.GetOrCreateSystemManaged<IterateThroughComponent_WithOptionsSystem>();
            system.Update();

            var ecsTestData = GetComponent<EcsTestData>(system.Entity);
            Assert.AreEqual(1, ecsTestData.value);
        }

        [Test]
        public void ForEachIteration_ThroughComponent_WithEnableableComponent_WithRunningJob()
        {
            var enableSystem = World.CreateSystemManaged<IterateThroughComponent_WithEnableableComponent_EnableSystem>();
            var foreachSystem = World.CreateSystem<IterateThroughComponent_WithEnableableComponent_WithRunningJobSystem>();
            enableSystem.Update();
            foreachSystem.Update(World.Unmanaged);

            var ecsTestData = GetComponent<EcsTestData>(enableSystem.Entity);
            Assert.AreEqual(1, ecsTestData.value);
        }

        [Test]
        public void ForEachIteration_WithEnableableComponents_ProcessCorrectEntities()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EnableableAllTag),
                typeof(EnableableAnyTag), typeof(EnableableNoneTag), typeof(EnableableDisabledTag));
            int entityCount = 10_000;
            var allEntities = m_Manager.CreateEntity(archetype, entityCount, Allocator.Temp);
            for (int i = 0; i < entityCount; ++i)
            {
                var e = allEntities[i];
                if ((i / 5) % 2 == 0)
                    m_Manager.SetComponentEnabled<EnableableAllTag>(e, false);
                if ((i / 7) % 2 == 0)
                    m_Manager.SetComponentEnabled<EnableableAnyTag>(e, false);
                if ((i / 11) % 2 == 0)
                    m_Manager.SetComponentEnabled<EnableableNoneTag>(e, false);
                if ((i / 13) % 2 == 0)
                    m_Manager.SetComponentEnabled<EnableableDisabledTag>(e, false);
                if ((i / 17) % 2 == 0)
                {
                    m_Manager.AddComponent<EnableableAbsentTag>(e);
                    if (i % 2 == 0)
                        m_Manager.SetComponentEnabled<EnableableAbsentTag>(e, false);
                }
                if ((i / 19) % 2 == 0)
                {
                    m_Manager.AddComponent<EnableablePresentTag>(e);
                    if (i % 2 == 0)
                        m_Manager.SetComponentEnabled<EnableablePresentTag>(e, false);
                }
            }

            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EnableableAllTag>()
                .WithAny<EnableableAnyTag>()
                .WithNone<EnableableNoneTag>()
                .WithDisabled<EnableableDisabledTag>()
                .WithAbsent<EnableableAbsentTag>()
                .WithPresent<EnableablePresentTag>()
                .Build(m_Manager);
            using var expectedEntities = query.ToEntityArray(Allocator.Temp);

            var sys = World.CreateSystemManaged<IterateThroughEnableableComponents_TrackProcessedEntities>();
            sys.Update();
            CollectionAssert.AreEqual(expectedEntities.ToArray(), sys.ProcessedEntities.AsArray().ToArray());
        }
        [Test]
        public void DisabledChangeFilterTest()
        {
            var system = World.GetOrCreateSystem<DisabledChangeFilterSystem>();
            system.Update(World.Unmanaged);

            Assert.AreEqual(expected: 1000, actual: World.EntityManager.GetComponentData<SumData>(system).sum);
        }
        [Test]
        public void ForEachIteration_IterateThroughEnableableComponents(
            [Values(QueryExtension.None, QueryExtension.Disabled, QueryExtension.Any, QueryExtension.All)] QueryExtension queryExtension)
        {
            var system = World.GetOrCreateSystem<IterateThroughEnableableComponentsWithVariousExtensionsSystem>();
            ref var queryData = ref World.EntityManager.GetComponentDataRW<SystemQueryData>(system).ValueRW;
            queryData.extensionToTest = queryExtension;

            system.Update(World.Unmanaged);

            switch (queryExtension)
            {
                case QueryExtension.Any:
                case QueryExtension.All:
                    Assert.AreEqual(expected: 800, actual: World.EntityManager.GetComponentData<SumData>(system).sum);
                    break;
                case QueryExtension.None:
                case QueryExtension.Disabled:
                    Assert.AreEqual(expected: 200, actual: World.EntityManager.GetComponentData<SumData>(system).sum);
                    break;
            }
        }

        [Test]
        public void ForEachIteration_IterateThroughTagComponent()
            => World.GetOrCreateSystem<IterateThroughTagComponents>().Update(World.Unmanaged);

        [Test]
        public void ForEachIteration_ThroughComponent_WithChangeFilter()
        {
            var system = World.GetOrCreateSystemManaged<IterateThroughComponent_WithChangeFilterSystem>();
            system.Update();

            var ecsTestData = GetComponent<EcsTestData>(system.Entity);
            Assert.AreEqual(1, ecsTestData.value);
        }

        [Test]
        public void ForEachIteration_ThroughValueTypeComponentWithoutRefRO()
        {
            var system = World.GetOrCreateSystem<IterateThroughComponent_WithoutRefRO>();
            system.Update(World.Unmanaged);

            Assert.AreEqual(55, World.EntityManager.GetComponentData<SumData>(system).sum);
        }

        [Test]
        public void ForEachIteration_ThroughComponent_WithSharedComponentFilter([Values] bool isManaged)
        {
            // Creating entities here instead of inside `IterateThroughComponent_WithSharedComponentFilterSystem.OnCreate()`
            // in order to circumvent a bug in `EntityManagerDebug.CheckInternalConsistency()`:
            // See: https://unity.slack.com/archives/C01HYKXLM42/p1654006277589939
            var entity0 = World.EntityManager.CreateEntity(typeof(EcsTestData));
            World.EntityManager.SetComponentData(entity0, new EcsTestData(1));
            if (isManaged)
                World.EntityManager.AddSharedComponentManaged(entity0, new ManagedSharedData1(1));
            else
                World.EntityManager.AddSharedComponent(entity0, new SharedData1(1));

            var entity1 = World.EntityManager.CreateEntity(typeof(EcsTestData));
            World.EntityManager.SetComponentData(entity1, new EcsTestData(10));
            if (isManaged)
                World.EntityManager.AddSharedComponentManaged(entity1, new ManagedSharedData1(1));
            else
                World.EntityManager.AddSharedComponent(entity1, new SharedData1(1));

            var entity2 = World.EntityManager.CreateEntity(typeof(EcsTestData));
            World.EntityManager.SetComponentData(entity2, new EcsTestData(100));
            if (isManaged)
                World.EntityManager.AddSharedComponentManaged(entity2, new ManagedSharedData1(2));
            else
                World.EntityManager.AddSharedComponent(entity2, new SharedData1(2));

            var system = World.GetOrCreateSystem<IterateThroughComponent_WithSharedComponentFilterSystem>();

            ref var sharedComponentFilterData = ref World.EntityManager.GetComponentDataRW<SharedComponentFilterData>(system).ValueRW;
            sharedComponentFilterData.IsManaged = isManaged;

            system.Update(World.Unmanaged);

            Assert.AreEqual(11, World.EntityManager.GetComponentData<SumData>(system).sum);

            World.EntityManager.DestroyEntity(entity0);
            World.EntityManager.DestroyEntity(entity1);
            World.EntityManager.DestroyEntity(entity2);
        }

        [Test]
        public void TwoForEachIterationsWithSameBackingQuery_OneWithSharedComponentFilter_OneWithout_TestNoFilterInterference()
        {
            // Creating entities here instead of inside `IterateThroughComponent_WithSharedComponentFilterSystem.OnCreate()`
            // in order to circumvent a bug in `EntityManagerDebug.CheckInternalConsistency()`:
            // See: https://unity.slack.com/archives/C01HYKXLM42/p1654006277589939
            var entity0 = World.EntityManager.CreateEntity(typeof(EcsTestData));
            World.EntityManager.SetComponentData(entity0, new EcsTestData(1));
            World.EntityManager.AddSharedComponent(entity0, new SharedData1(1));

            var entity1 = World.EntityManager.CreateEntity(typeof(EcsTestData));
            World.EntityManager.SetComponentData(entity1, new EcsTestData(10));
            World.EntityManager.AddSharedComponent(entity1, new SharedData1(1));

            var entity2 = World.EntityManager.CreateEntity(typeof(EcsTestData));
            World.EntityManager.SetComponentData(entity2, new EcsTestData(100));
            World.EntityManager.AddSharedComponent(entity2, new SharedData1(2));

            var system = World.GetOrCreateSystem<SameBackingQuery_NoInterferenceSystem>();
            system.Update(World.Unmanaged);

            Assert.AreEqual(2222, World.EntityManager.GetComponentData<EcsTestData>(entity0).value);
            Assert.AreEqual(2222, World.EntityManager.GetComponentData<EcsTestData>(entity1).value);
            Assert.AreEqual(2222, World.EntityManager.GetComponentData<EcsTestData>(entity2).value);

            World.EntityManager.DestroyEntity(entity0);
            World.EntityManager.DestroyEntity(entity1);
            World.EntityManager.DestroyEntity(entity2);
        }

        [Test]
        public void ForEachIteration_ThroughManagedSharedComponent()
        {
            var system = World.GetOrCreateSystemManaged<IterateThroughManagedSharedComponentSystem>();
            system.Update();

            var entityQuery = system.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<EcsTestData>(), ComponentType.ReadOnly<EcsStringSharedComponent>());
            var allEntities = entityQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in allEntities)
            {
                var myAspect = GetAspect<MyAspect>(entity);
                var sharedString = GetSharedComponent<EcsStringSharedComponent>(entity);
                Assert.AreEqual(entity.Index * sharedString.Value.Length, myAspect._Data.ValueRO.value);
            }
            allEntities.Dispose();
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void ForEachIteration_ThroughManagedComponent()
        {
            var system = World.GetOrCreateSystemManaged<IterateThroughManagedComponentSystem>();
            system.Update();

            var entityQuery = system.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<EcsTestData>(), ComponentType.ReadOnly<EcsTestManagedComponent>());
            var allEntities = entityQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in allEntities)
            {
                var myAspect = GetAspect<MyAspect>(entity);
                var ecsTestManagedComponent = GetManagedComponent<EcsTestManagedComponent>(entity);

                Assert.AreEqual(null, ecsTestManagedComponent.nullField);
                Assert.AreEqual(int.MaxValue, myAspect._Data.ValueRO.value);
            }
            allEntities.Dispose();
        }
#endif
        [Test]
        public void ForEachIteration_ThroughAspectsAndComponents_UsingNestedForEach()
        {
            var system = World.GetOrCreateSystemManaged<IterateThroughAspectsAndComponents_WithNestedForEachSystem>();
            system.Update();

            var myAspect = GetAspect<MyAspect>(system.CreatedEntities[0]);
            Assert.AreEqual(1, myAspect._Data.ValueRO.value);
            Assert.AreEqual(2, myAspect._Data2.ValueRO.value0);
            Assert.AreEqual(2, myAspect._Data2.ValueRO.value1);

            var firstEcsTestData3 = GetComponent<EcsTestData3>(system.CreatedEntities[1]);
            Assert.AreEqual(1, firstEcsTestData3.value0);
            Assert.AreEqual(2, firstEcsTestData3.value1);
            Assert.AreEqual(2, firstEcsTestData3.value2);

            var secondEcsTestData3 = GetComponent<EcsTestData3>(system.CreatedEntities[2]);
            Assert.AreEqual(1, secondEcsTestData3.value0);
            Assert.AreEqual(2, secondEcsTestData3.value1);
            Assert.AreEqual(2, secondEcsTestData3.value2);

            system.CreatedEntities.Dispose();
        }

        [Test]
        public void ForEachIteration_ThroughAspectsAndComponents([Values] QueryExtension queryExtension)
        {
            var system = World.GetOrCreateSystem<IterateThroughAspectsAndComponentsSystem>();
            ref var queryData = ref World.EntityManager.GetComponentDataRW<SystemQueryData>(system).ValueRW;
            queryData.extensionToTest = queryExtension;

            system.Update(World.Unmanaged);

            var myAspect = GetAspect<MyAspect>(system);
            var ecsTestData3 = GetComponent<EcsTestData3>(system);

            switch (queryExtension)
            {
                case QueryExtension.Disabled:
                case QueryExtension.Absent:
                case QueryExtension.None:
                    Assert.AreEqual(0, myAspect._Data.ValueRO.value);
                    Assert.AreEqual(0, myAspect._Data2.ValueRO.value0);
                    Assert.AreEqual(0, myAspect._Data2.ValueRO.value1);

                    Assert.AreEqual(0, ecsTestData3.value0);
                    Assert.AreEqual(0, ecsTestData3.value1);
                    Assert.AreEqual(0, ecsTestData3.value2);
                    break;
                default:
                    Assert.AreEqual(10, myAspect._Data.ValueRO.value);
                    Assert.AreEqual(20, myAspect._Data2.ValueRO.value0);
                    Assert.AreEqual(30, myAspect._Data2.ValueRO.value1);

                    Assert.AreEqual(10, ecsTestData3.value0);
                    Assert.AreEqual(20, ecsTestData3.value1);
                    Assert.AreEqual(30, ecsTestData3.value2);
                    break;
            }
        }

        [Test]
        public void ForEachIteration_ThroughSingleComponent_WithoutRef()
        {
            var system = World.GetOrCreateSystem<IterateThroughSingleComponent_WithoutRefSystem>();
            system.Update(World.Unmanaged);

            ref var sumData = ref World.EntityManager.GetComponentDataRW<EcsTestData>(system).ValueRW;
            Assert.AreEqual(55, sumData.value);
        }

        [Test]

        public void ForEachIteration_ThroughSingleComponent()
        {
            var system = World.GetOrCreateSystem<IterateThroughSingleComponentSystem>();
            system.Update(World.Unmanaged);

            var ecsTestData3 = GetComponent<EcsTestData3>(system);

            Assert.AreEqual(10, ecsTestData3.value0);
            Assert.AreEqual(10, ecsTestData3.value1);
            Assert.AreEqual(10, ecsTestData3.value2);
        }

        [Test]
        public void ForEachIteration_ThroughEmptyComponent()
        {
            var systemRef = World.GetOrCreateSystem<IterateThroughEmptyComponentSystem>();
            systemRef.Update(World.Unmanaged);
        }

        [Test]
        public void ForEachIteration_ThroughComponentsOnly_RefRW_RefRO()
        {
            var system = World.GetOrCreateSystem<IterateThroughValueTypeComponentsSystem>();
            system.Update(World.Unmanaged);

            var ecsTestData3 = GetComponent<EcsTestData3>(system);

            Assert.AreEqual(10, ecsTestData3.value0);
            Assert.AreEqual(20, ecsTestData3.value1);
            Assert.AreEqual(30, ecsTestData3.value2);
        }

        [Test]
        public unsafe void ForEachIteration_WithDependency([Values] bool forceDependencyCompletion)
        {
            // Allocate
            using var syncHandle = new NativeArray<int>(1, Allocator.Persistent);

            // Get Systems
            var scheduler = World.GetOrCreateSystem<IterateThroughAspectsAndComponents_EnsureDependencyCompletedSystem_Schedule>();
            var mainThread = World.GetOrCreateSystem<IterateThroughAspectsAndComponents_EnsureDependencyCompletedSystem_MainThread>();

            ref var schedulerSys = ref World.Unmanaged.GetUnsafeSystemRef<IterateThroughAspectsAndComponents_EnsureDependencyCompletedSystem_Schedule>(scheduler);
            ref var mainThreadSys = ref World.Unmanaged.GetUnsafeSystemRef<IterateThroughAspectsAndComponents_EnsureDependencyCompletedSystem_MainThread>(mainThread);

            schedulerSys.syncHandle = syncHandle;
            mainThreadSys.syncHandle = syncHandle;
            mainThreadSys.ForceDependencyCompletion = forceDependencyCompletion;

            scheduler.Update(World.Unmanaged);
            mainThread.Update(World.Unmanaged);
        }

        [Test]
        public void ForEachIteration_MultiplePartsInPartialType()
        {
            var system = World.GetOrCreateSystem<IterateThroughAspectsAndComponents_MultipleUsersWrittenPartsInSystem>();
            system.Update(World.Unmanaged);

            var myAspect = GetAspect<MyAspect>(system);
            var ecsTestData3 = GetComponent<EcsTestData3>(system);

            Assert.AreEqual(50, myAspect._Data.ValueRO.value);
            Assert.AreEqual(20, myAspect._Data2.ValueRO.value0);
            Assert.AreEqual(20, myAspect._Data2.ValueRO.value1);

            Assert.AreEqual(80, ecsTestData3.value0);
            Assert.AreEqual(80, ecsTestData3.value1);
            Assert.AreEqual(80, ecsTestData3.value2);
        }


        [Test]
        public void ForEachIterationThroughDynamicBuffer()
        {
            var system = World.GetOrCreateSystem<IterateThroughDynamicBufferSystem>();
            system.Update(World.Unmanaged);

            var allEntities = World.EntityManager.GetAllEntities();

            foreach (var entity in allEntities)
            {
                var intBufferElements = GetDynamicBuffer<IntBufferElement>(entity);
                Assert.AreEqual(intBufferElements.Length, 2);

                var first = intBufferElements[0];
                var second = intBufferElements[1];

                if (entity.Index % 5 == 0)
                {
                    Assert.AreEqual(first.Value, 5);
                    Assert.AreEqual(second.Value, 50);
                }
                else
                {
                    Assert.AreEqual(first.Value, 0);
                    Assert.AreEqual(second.Value, 0);
                }
            }
            allEntities.Dispose();
        }

        [Test]
        public void ForEachIterationOverComponents_WithEntityAccess()
        {
            var system = World.GetOrCreateSystem<ForEachIterationOverComponents_WithEntityAccessSystem>();
            system.Update(World.Unmanaged);

            var ecsTestData = GetComponent<EcsTestDataEntity>(system);

            Assert.AreEqual(system.m_Entity, ecsTestData.value1);
        }

        [Test]
        public void ForEachIterationOverComponents_WithEntityAccess_ToTupleType()
        {
            var system = World.GetOrCreateSystem<ForEachIterationOverComponents_WithEntityAccessToTupleSystem>();
            system.Update(World.Unmanaged);

            var ecsTestData = GetComponent<EcsTestDataEntity>(system);

            Assert.AreEqual(system.m_Entity, ecsTestData.value1);
        }

        [Test]
        public void ForEachIterationOverAspect_WithEntityAccess()
        {
            var system = World.GetOrCreateSystem<ForEachIterationOverAspect_WithEntityAccessSystem>();
            system.Update(World.Unmanaged);

            var ecsTestData = GetComponent<EcsTestDataEntity>(system);

            Assert.AreEqual(system.m_Entity, ecsTestData.value1);
        }

        [Test]
        public void ForEachIterationThroughEnabledComponents()
        {
            var system = World.GetOrCreateSystem<IterateThroughEnabledComponentsSystem>();

            system.Update(World.Unmanaged);
            var martialArtsAbilityEnabledCount = World.EntityManager.GetComponentData<MartialArtsAbilityComponent>(system);

            // of the 8 entities created, only the powers of 2 should be enabled & included in the sum.
            Assert.AreEqual(expected: 1+2+4+8, actual: martialArtsAbilityEnabledCount.Value);
        }

        [Test]
        public void ForEachIterationThroughEnableableComponent()
        {
            var system = World.GetOrCreateSystem<IterateThroughValueTypeEnableableComponentSystem>();
            system.Update(World.Unmanaged);
            var martialArtsAbility = World.EntityManager.GetComponentData<MartialArtsAbilityComponent>(system);

            Assert.AreEqual(expected: 3, actual: martialArtsAbility.Value);
        }

        [Test]
        public void ForEachIterationThroughEnableableComponentRef([Values] bool disabledComponent)
        {
            var system = World.GetOrCreateSystem<IterateThroughValueTypeEnableableComponentRefSystem>();
            ref var DisabledState = ref World.EntityManager.GetComponentDataRW<DisabledData>(system).ValueRW;
            DisabledState.DisableComponent = disabledComponent;
            system.Update(World.Unmanaged);

            DisabledState = ref World.EntityManager.GetComponentDataRW<DisabledData>(system).ValueRW;
            Assert.AreEqual(!disabledComponent, DisabledState.IsComponentEnabled);
        }

        partial class IdiomaticForEach_WithChangeFilter_Variations : SystemBase
        {
            protected override void OnUpdate()
            {
                // change filter in query, read-only
                foreach (var (data1, data2) in Query<RefRO<EcsTestData>, RefRW<EcsTestData2>>().WithChangeFilter<EcsTestData>())
                    data2.ValueRW.value0 = data1.ValueRO.value;

                // change filter in query, read-write
                foreach (var data3 in Query<RefRW<EcsTestData3>>().WithChangeFilter<EcsTestData3>())
                    data3.ValueRW.value0++;

                // change filter not in query
                foreach (var data4 in Query<RefRW<EcsTestData4>>().WithChangeFilter<EcsTestData5>())
                    data4.ValueRW.value0++;
            }
        }

        [Test]
        public unsafe void IdiomaticForEach_WithChangeFilter_UsesReadOnly()
        {
            var system = World.GetOrCreateSystem<IdiomaticForEach_WithChangeFilter_Variations>();
            var state = World.Unmanaged.ResolveSystemState(system);

            var types0 = state->EntityQueries[0].GetQueryTypes();
            var expected0 = new [] { ComponentType.ReadOnly<EcsTestData>(), ComponentType.ReadWrite<EcsTestData2>() };
            CollectionAssert.AreEquivalent(expected0, types0);

            var types1 = state->EntityQueries[1].GetQueryTypes();
            var expected1 = new [] { ComponentType.ReadWrite<EcsTestData3>() };
            CollectionAssert.AreEquivalent(expected1, types1);

            var types2 = state->EntityQueries[2].GetQueryTypes();
            var expected2 = new [] { ComponentType.ReadOnly<EcsTestData5>(), ComponentType.ReadWrite<EcsTestData4>() };
            CollectionAssert.AreEquivalent(expected2, types2);
        }
    }
}
