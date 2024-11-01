using NUnit.Framework;
using Unity.Entities.Tests;
using Unity.PerformanceTesting;

namespace Unity.Entities.PerformanceTests
{
    [Category("Performance")]
    partial class ComponentLookupPerformanceTests : ECSTestsFixture
    {

        enum ScheduleMode
        {
            Parallel, Single, Run
        }

        partial class PerfTestSystem : SystemBase
        {
            public bool ReadOnly;
            public ScheduleMode Schedule;

            ComponentLookup<EcsTestData> m_Lookup;
            protected override void OnCreate()
            {
                m_Lookup = GetComponentLookup<EcsTestData>(ReadOnly);
            }

            protected override void OnUpdate()
            {
                var lookup = m_Lookup;
                if (ReadOnly)
                {
                    if (Schedule == ScheduleMode.Run)
                    {
                        Entities.WithReadOnly(lookup).ForEach((ref EcsTestDataEntity data) =>
                        {
                            data.value0 += lookup[data.value1].value;
                        }).Run();
                    }
                    else if (Schedule == ScheduleMode.Parallel)
                    {
                        Entities.WithReadOnly(lookup).ForEach((ref EcsTestDataEntity data) =>
                        {
                            data.value0 += lookup[data.value1].value;
                        }).ScheduleParallel();
                        CompleteDependency();
                    }
                    else if (Schedule == ScheduleMode.Single)
                    {
                        Entities.WithReadOnly(lookup).ForEach((ref EcsTestDataEntity data) =>
                        {
                            data.value0 += lookup[data.value1].value;
                        }).Schedule();
                        CompleteDependency();
                    }
                }
                else
                {
                    if (Schedule == ScheduleMode.Run)
                    {
                        Entities.ForEach((ref EcsTestDataEntity data) =>
                        {
                            lookup[data.value1] = new EcsTestData(5);
                        }).Run();
                    }
                    else if (Schedule == ScheduleMode.Parallel)
                    {
                        Entities.WithNativeDisableParallelForRestriction(lookup).ForEach((ref EcsTestDataEntity data) =>
                        {
                            lookup[data.value1] = new EcsTestData(5);
                        }).ScheduleParallel();
                        CompleteDependency();
                    }
                    else if (Schedule == ScheduleMode.Single)
                    {
                        Entities.ForEach((ref EcsTestDataEntity data) =>
                        {
                            lookup[data.value1] = new EcsTestData(5);
                        }).Schedule();
                        CompleteDependency();
                    }
                }
            }
        }

        void RunPerfTestSystem(bool readOnly, ScheduleMode schedule)
        {
            var system = World.GetOrCreateSystemManaged<PerfTestSystem>();
            var name = (readOnly ? "ReadOnly" : "Write") + "_" + schedule.ToString();
            Measure.Method(() =>
            {
                system.ReadOnly = readOnly;
                system.Schedule = schedule;
                system.Update();
            })
            .SampleGroup(name)
            .MeasurementCount(10)
            .IterationsPerMeasurement(1)
            .Run();
        }

        [Test, Performance]
        [Category("Performance")] // bug: this redundant category here required because our current test runner ignores Category on a fixture for generated test methods
        public void ComponentLookup_Performance_Write([Values(10000, 1000000)] int entityCount)
        {
            var targetArchetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestData4), typeof(EcsTestData5));
            var targetEntities = m_Manager.CreateEntity(targetArchetype, entityCount, World.UpdateAllocator.ToAllocator);

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEntity));
            var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);

            for (int i = 0;i != entityCount;i++)
                m_Manager.SetComponentData(entities[i], new EcsTestDataEntity { value1 = targetEntities[i] });

            targetEntities.Dispose();
            entities.Dispose();

            RunPerfTestSystem(true, ScheduleMode.Run);
            RunPerfTestSystem(true, ScheduleMode.Single);
            RunPerfTestSystem(true, ScheduleMode.Parallel);

            RunPerfTestSystem(false, ScheduleMode.Run);
            RunPerfTestSystem(false, ScheduleMode.Single);
            RunPerfTestSystem(false, ScheduleMode.Parallel);
        }

        partial class HasGetPerformanceSystem : SystemBase
        {
            public bool ReadOnly;
            public ScheduleMode Schedule;
            public bool UseHasComponent; //either use the if(hasComponent...) componentFromEntity[entity] path of the tryGetComponent path.
            protected override void OnUpdate()
            {
                if (UseHasComponent)
                    RunHasComponent();
                else
                    RunTryGetComponent();
            }

            ComponentLookup<EcsTestData5> m_Lookup;
            protected override void OnCreate()
            {
                m_Lookup = GetComponentLookup<EcsTestData5>(ReadOnly);
            }

            private void RunHasComponent()
            {
                var lookup = m_Lookup;
                if (ReadOnly)
                {
                    if (Schedule == ScheduleMode.Run)
                    {
                        Entities.WithReadOnly(lookup).ForEach((ref EcsTestDataEntity data) =>
                        {
                            if(lookup.HasComponent(data.value1))
                                data.value0 += lookup[data.value1].value4;
                        }).Run();
                    }
                    else if (Schedule == ScheduleMode.Parallel)
                    {
                        Entities.WithReadOnly(lookup).ForEach((ref EcsTestDataEntity data) =>
                        {
                            if(lookup.HasComponent(data.value1))
                                data.value0 += lookup[data.value1].value4;
                        }).ScheduleParallel();
                        CompleteDependency();
                    }
                    else if (Schedule == ScheduleMode.Single)
                    {
                        Entities.WithReadOnly(lookup).ForEach((ref EcsTestDataEntity data) =>
                        {
                            if(lookup.HasComponent(data.value1))
                                data.value0 += lookup[data.value1].value4;
                        }).Schedule();
                        CompleteDependency();
                    }
                }
                else
                {
                    if (Schedule == ScheduleMode.Run)
                    {
                        Entities.ForEach((ref EcsTestDataEntity data) =>
                        {
                            if(lookup.HasComponent(data.value1))
                                data.value0 = lookup[data.value1].value4;
                        }).Run();
                    }
                    else if (Schedule == ScheduleMode.Parallel)
                    {
                        Entities.WithNativeDisableParallelForRestriction(lookup).ForEach((ref EcsTestDataEntity data) =>
                        {
                            if(lookup.HasComponent(data.value1))
                                data.value0 = lookup[data.value1].value4;
                        }).ScheduleParallel();
                        CompleteDependency();
                    }
                    else if (Schedule == ScheduleMode.Single)
                    {
                        Entities.ForEach((ref EcsTestDataEntity data) =>
                        {
                            if(lookup.HasComponent(data.value1))
                                data.value0 = lookup[data.value1].value4;
                        }).Schedule();
                        CompleteDependency();
                    }
                }
            }

            private void RunTryGetComponent()
            {
                var lookup = m_Lookup;
                if (ReadOnly)
                {
                    if (Schedule == ScheduleMode.Run)
                    {
                        Entities.WithReadOnly(lookup).ForEach((ref EcsTestDataEntity data) =>
                        {
                            if(lookup.TryGetComponent(data.value1, out var componentData))
                                data.value0 += componentData.value4;
                        }).Run();
                    }
                    else if (Schedule == ScheduleMode.Parallel)
                    {
                        Entities.WithReadOnly(lookup).ForEach((ref EcsTestDataEntity data) =>
                        {
                            if(lookup.TryGetComponent(data.value1, out var componentData))
                                data.value0 += componentData.value4;
                        }).ScheduleParallel();
                        CompleteDependency();
                    }
                    else if (Schedule == ScheduleMode.Single)
                    {
                        Entities.WithReadOnly(lookup).ForEach((ref EcsTestDataEntity data) =>
                        {
                            if(lookup.TryGetComponent(data.value1, out var componentData))
                                data.value0 += componentData.value4;
                        }).Schedule();
                        CompleteDependency();
                    }

                }
                else
                {
                    if (Schedule == ScheduleMode.Run)
                    {
                        Entities.ForEach((ref EcsTestDataEntity data) =>
                        {
                            if(lookup.TryGetComponent(data.value1, out var componentData))
                                data.value0 = componentData.value4;
                        }).Run();
                    }
                    else if (Schedule == ScheduleMode.Parallel)
                    {
                        Entities.WithNativeDisableParallelForRestriction(lookup).ForEach((ref EcsTestDataEntity data) =>
                        {
                            if(lookup.TryGetComponent(data.value1, out var componentData))
                                data.value0 = componentData.value4;
                        }).ScheduleParallel();
                        CompleteDependency();
                    }
                    else if (Schedule == ScheduleMode.Single)
                    {
                        Entities.ForEach((ref EcsTestDataEntity data) =>
                        {
                            if(lookup.TryGetComponent(data.value1, out var componentData))
                                data.value0 = componentData.value4;
                        }).Schedule();
                        CompleteDependency();
                    }
                }
            }
        }

        void RunHasComponentSystem(bool readOnly, bool useHasComponent, ScheduleMode schedule)
        {
            var system = World.GetOrCreateSystemManaged<HasGetPerformanceSystem>();
            var name = (readOnly ? "ReadOnly" : "Write") + "_" + schedule.ToString();
            Measure.Method(() =>
                {
                    system.ReadOnly = readOnly;
                    system.Schedule = schedule;
                    system.UseHasComponent = useHasComponent;
                    system.Update();
                })
                .SampleGroup(name)
                .MeasurementCount(10)
                .IterationsPerMeasurement(1)
                .WarmupCount(1)
                .Run();
        }

        [Test, Performance]
        [Category("Performance")] // bug: this redundant category here required because our current test runner ignores Category on a fixture for generated test methods
        public void ComponentLookup_Performance_HasComponent([Values(10000, 1000000)] int entityCount, [Values] bool useHasComponent)
        {
            var targetArchetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestData4));

            var targetEntities = m_Manager.CreateEntity(targetArchetype, entityCount, World.UpdateAllocator.ToAllocator);

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEntity));
            var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);


            for (int i = 0; i != entityCount;i++)
                m_Manager.SetComponentData(entities[i], new EcsTestDataEntity { value1 = targetEntities[i] });

            //set every other entity with component data "latest" in typeindex. Which happens to be based on insertion order according to a quick debug log
            for (int i = 0; i < entityCount; i += 2)
            {
                m_Manager.AddComponentData(targetEntities[i], new EcsTestData5
                {
                    value0 = i,
                    value1 = i + 1,
                    value2 = i + 2,
                    value3 = i + 3,
                    value4 = i + 4
                });
            }

            targetEntities.Dispose();
            entities.Dispose();

            RunHasComponentSystem(true,useHasComponent, ScheduleMode.Run);
            RunHasComponentSystem(true,useHasComponent, ScheduleMode.Single);
            RunHasComponentSystem(true,useHasComponent, ScheduleMode.Parallel);

            RunHasComponentSystem(false,useHasComponent, ScheduleMode.Run);
            RunHasComponentSystem(false,useHasComponent, ScheduleMode.Single);
            RunHasComponentSystem(false,useHasComponent, ScheduleMode.Parallel);
        }
    }
}
