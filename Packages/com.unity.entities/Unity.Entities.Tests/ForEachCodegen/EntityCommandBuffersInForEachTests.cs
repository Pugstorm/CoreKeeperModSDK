using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Tests
{
    public enum ScheduleType
    {
        Run,
        Schedule,
        ScheduleParallel
    }

    public enum PlaybackType
    {
        Immediate,
        Deferred
    }

    [TestFixture]
    public partial class EntityCommandBuffersInForEachTests : ECSTestsFixture
    {
        private EntityCommandsParameterInForEach_TestSystem EntityCommandsInForEachTestSystem => World.GetOrCreateSystemManaged<EntityCommandsParameterInForEach_TestSystem>();

        private partial class TestEntityCommandBufferSystem : EntityCommandBufferSystem{}

        protected partial class EntityCommandsParameterInForEach_TestSystem : SystemBase
        {
            protected override void OnCreate()
            {
            }

            protected override void OnUpdate()
            {
            }

#region AddComponentToEntity/Entities
            public void AddComponentToEntity_WithImmediatePlayback()
            {
                var entity = EntityManager.CreateEntity();

                Entities
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                    .WithoutBurst()
#endif
                    .WithImmediatePlayback()
                    .ForEach(
                        (Entity e, EntityCommandBuffer ecb) =>
                        {
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                            ecb.AddComponent(e, new EcsTestManagedComponent());
                            ecb.AddComponent<EcsTestManagedComponent2>(e);
#endif
                            ecb.AddComponent<EcsTestTag>(e);
                            ecb.AddComponent(e, new EcsTestData());
                            ecb.AddComponent(e, ComponentType.ReadOnly<EcsTestData2>());
                            ecb.AddComponent(e, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData3>(), ComponentType.ReadOnly<EcsTestData4>()));
                        })
                    .Run();

#if !UNITY_DISABLE_MANAGED_COMPONENTS
                Assert.IsTrue(EntityManager.HasComponent<EcsTestManagedComponent>(entity));
                Assert.IsTrue(EntityManager.HasComponent<EcsTestManagedComponent2>(entity));
#endif
                Assert.IsTrue(EntityManager.HasComponent<EcsTestTag>(entity));
                Assert.IsTrue(EntityManager.HasComponent<EcsTestData>(entity));
                Assert.IsTrue(EntityManager.HasComponent<EcsTestData2>(entity));
                Assert.IsTrue(EntityManager.HasComponent<EcsTestData3>(entity));
                Assert.IsTrue(EntityManager.HasComponent<EcsTestData4>(entity));
            }

            public void AddComponentToEntity_WithDeferredPlayback(ScheduleType scheduleType)
            {
                Entity entity = EntityManager.CreateEntity();

                const int componentArrayLength = 1;

                var ecsTestData5Array = CollectionHelper.CreateNativeArray<EcsTestData5>(componentArrayLength, EntityManager.World.UpdateAllocator.ToAllocator);
                for (int i = 0; i < componentArrayLength; i++)
                {
                    ecsTestData5Array[i] = new EcsTestData5();
                }

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                    {
                        Entities
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                            .WithoutBurst()
#endif
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                                    ecb.AddComponent(e, new EcsTestManagedComponent());
                                    ecb.AddComponent<EcsTestManagedComponent2>(e);
#endif
                                    ecb.AddComponent(e, new EcsTestData());
                                    ecb.AddComponent<EcsTestTag>(e);
                                    ecb.AddComponent(e, ComponentType.ReadOnly<EcsTestData2>());
                                    ecb.AddComponent(e, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData3>(), ComponentType.ReadOnly<EcsTestData4>()));
                                    ecb.AddComponent(e, ecsTestData5Array[0]);
                                })
                            .Run();
                        break;
                    }
                    case ScheduleType.ScheduleParallel:
                    {
                        Entities
                            .WithReadOnly(ecsTestData5Array)
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.AddComponent(e, new EcsTestData());
                                    ecb.AddComponent<EcsTestTag>(e);
                                    ecb.AddComponent(e, ComponentType.ReadOnly<EcsTestData2>());
                                    ecb.AddComponent(e, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData3>(), ComponentType.ReadOnly<EcsTestData4>()));
                                    ecb.AddComponent(e, ecsTestData5Array[0]);
                                })
                            .ScheduleParallel();
                        break;
                    }
                    case ScheduleType.Schedule:
                    {
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.AddComponent(e, new EcsTestData());
                                    ecb.AddComponent<EcsTestTag>(e);
                                    ecb.AddComponent(e, ComponentType.ReadOnly<EcsTestData2>());
                                    ecb.AddComponent(e, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData3>(), ComponentType.ReadOnly<EcsTestData4>()));
                                    ecb.AddComponent(e, ecsTestData5Array[0]);
                                })
                            .Schedule();
                        break;
                    }
                }

                var testEntityCommandBufferSystem = World.GetExistingSystemManaged<TestEntityCommandBufferSystem>();
                testEntityCommandBufferSystem.Update();

                Assert.IsTrue(EntityManager.HasComponent<EcsTestTag>(entity));
                Assert.IsTrue(EntityManager.HasComponent<EcsTestData>(entity));
                Assert.IsTrue(EntityManager.HasComponent<EcsTestData2>(entity));
                Assert.IsTrue(EntityManager.HasComponent<EcsTestData3>(entity));
                Assert.IsTrue(EntityManager.HasComponent<EcsTestData4>(entity));
                Assert.IsTrue(EntityManager.HasComponent<EcsTestData5>(entity));

#if !UNITY_DISABLE_MANAGED_COMPONENTS
                if (scheduleType == ScheduleType.Run)
                {
                    Assert.IsTrue(EntityManager.HasComponent<EcsTestManagedComponent>(entity));
                    Assert.IsTrue(EntityManager.HasComponent<EcsTestManagedComponent2>(entity));
                }
#endif
                ecsTestData5Array.Dispose();
            }

            public void AddComponentToEntities_WithImmediatePlayback()
            {
                var entities = EntityManager.CreateEntity(EntityManager.CreateArchetype(), 2, World.UpdateAllocator.ToAllocator);
                Entities
                    .WithImmediatePlayback()
                    .ForEach(
                        (EntityCommandBuffer ecb) =>
                        {
                            ecb.AddComponent(entities, new EcsTestData());
                            ecb.AddComponent<EcsTestTag>(entities);
                            ecb.AddComponent(entities, ComponentType.ReadOnly<EcsTestData2>());
                            ecb.AddComponent(entities, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData3>(), ComponentType.ReadOnly<EcsTestData4>()));
                        })
                    .Run();

                foreach (var entity in entities)
                {
                    Assert.IsTrue(EntityManager.HasComponent<EcsTestTag>(entity));
                    Assert.IsTrue(EntityManager.HasComponent<EcsTestData>(entity));
                    Assert.IsTrue(EntityManager.HasComponent<EcsTestData2>(entity));
                    Assert.IsTrue(EntityManager.HasComponent<EcsTestData3>(entity));
                    Assert.IsTrue(EntityManager.HasComponent<EcsTestData4>(entity));
                }
            }

            public void AddComponentToEntities_WithDeferredPlayback(ScheduleType scheduleType)
            {
                var entities = EntityManager.CreateEntity(EntityManager.CreateArchetype(), 2, World.UpdateAllocator.ToAllocator);

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                    {
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.AddComponent(entities, new EcsTestData());
                                    ecb.AddComponent<EcsTestTag>(entities);
                                    ecb.AddComponent(entities, ComponentType.ReadOnly<EcsTestData2>());
                                    ecb.AddComponent(entities, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData3>(), ComponentType.ReadOnly<EcsTestData4>()));
                                })
                            .Run();
                        break;
                    }
                    case ScheduleType.ScheduleParallel:
                    {
                        Entities
                            .WithReadOnly(entities)
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.AddComponent(entities, new EcsTestData());
                                    ecb.AddComponent<EcsTestTag>(entities);
                                    ecb.AddComponent(entities, ComponentType.ReadOnly<EcsTestData2>());
                                    ecb.AddComponent(entities,new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData3>(), ComponentType.ReadOnly<EcsTestData4>()));
                                })
                            .ScheduleParallel();
                        break;
                    }
                    case ScheduleType.Schedule:
                    {
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.AddComponent(entities, new EcsTestData());
                                    ecb.AddComponent<EcsTestTag>(entities);
                                    ecb.AddComponent(entities, ComponentType.ReadOnly<EcsTestData2>());
                                    ecb.AddComponent(entities, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData3>(), ComponentType.ReadOnly<EcsTestData4>()));
                                })
                            .Schedule();
                        break;
                    }
                }

                var testEntityCommandBufferSystem = World.GetExistingSystemManaged<TestEntityCommandBufferSystem>();
                testEntityCommandBufferSystem.Update();

                foreach (var entity in entities)
                {
                    Assert.IsTrue(EntityManager.HasComponent<EcsTestTag>(entity));
                    Assert.IsTrue(EntityManager.HasComponent<EcsTestData>(entity));
                    Assert.IsTrue(EntityManager.HasComponent<EcsTestData2>(entity));
                    Assert.IsTrue(EntityManager.HasComponent<EcsTestData3>(entity));
                    Assert.IsTrue(EntityManager.HasComponent<EcsTestData4>(entity));
                }
            }
#endregion

#region Add/SetSharedComponentToEntity/Entities
            public void AddSetSharedComponentToEntity_WithImmediatePlayback()
            {
                var entity = EntityManager.CreateEntity(ComponentType.ReadWrite<EcsTestSharedComp>());

                Entities
                    .WithImmediatePlayback()
                    .ForEach(
                        (Entity e, EntityCommandBuffer ecb) =>
                        {
                            ecb.SetSharedComponent(e, new EcsTestSharedComp {value = 10});
                            ecb.AddSharedComponent(e, new EcsTestSharedComp2());
                        })
                    .Run();

                Assert.AreEqual(10, EntityManager.GetSharedComponent<EcsTestSharedComp>(entity).value);
                Assert.IsTrue(EntityManager.HasComponent<EcsTestSharedComp2>(entity));
            }

            public void AddSetSharedComponentToEntities_WithImmediatePlayback()
            {
                var entities = EntityManager.CreateEntity(EntityManager.CreateArchetype(typeof(EcsTestSharedComp)), 2, Allocator.Temp);

                Entities
                    .WithImmediatePlayback()
                    .ForEach(
                        (EntityCommandBuffer entityCommandBuffer) =>
                        {
                            entityCommandBuffer.SetSharedComponent(entities, new EcsTestSharedComp {value = 10});
                            entityCommandBuffer.AddSharedComponent(entities, new EcsTestSharedComp2());
                        })
                    .Run();

                foreach (var entity in entities)
                {
                    Assert.AreEqual(10, EntityManager.GetSharedComponent<EcsTestSharedComp>(entity).value);
                    Assert.IsTrue(EntityManager.HasComponent<EcsTestSharedComp2>(entity));
                }
                entities.Dispose();
            }

            public void AddSetSharedComponentToEntity_WithDeferredPlayback(ScheduleType scheduleType)
            {
                var entity = EntityManager.CreateEntity(ComponentType.ReadWrite<EcsTestSharedComp>());

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.SetSharedComponent(e, new EcsTestSharedComp {value = 10});
                                    ecb.AddSharedComponent(e, new EcsTestSharedComp2());
                                })
                            .Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.SetSharedComponent(e, new EcsTestSharedComp { value = 10 });
                                    ecb.AddSharedComponent(e, new EcsTestSharedComp2());
                                })
                            .Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.SetSharedComponent(e, new EcsTestSharedComp { value = 10 });
                                    ecb.AddSharedComponent(e, new EcsTestSharedComp2());
                                })
                            .ScheduleParallel();
                        break;
                }

                var testEntityCommandBufferSystem = World.GetExistingSystemManaged<TestEntityCommandBufferSystem>();
                testEntityCommandBufferSystem.Update();

                Assert.AreEqual(10, EntityManager.GetSharedComponentManaged<EcsTestSharedComp>(entity).value);
                Assert.IsTrue(EntityManager.HasComponent<EcsTestSharedComp2>(entity));
            }

            public void AddSetSharedComponentToEntities_WithDeferredPlayback(ScheduleType scheduleType)
            {
                var entities = EntityManager.CreateEntity(EntityManager.CreateArchetype(ComponentType.ReadWrite<EcsTestSharedComp>()), 2, World.UpdateAllocator.ToAllocator);

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.SetSharedComponent(entities, new EcsTestSharedComp {value = 10});
                                    ecb.AddSharedComponent(entities, new EcsTestSharedComp2());
                                })
                            .Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.SetSharedComponent(entities, new EcsTestSharedComp {value = 10});
                                    ecb.AddSharedComponent(entities, new EcsTestSharedComp2());
                                })
                            .Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities
                            .WithReadOnly(entities)
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.SetSharedComponent(entities, new EcsTestSharedComp {value = 10});
                                    ecb.AddSharedComponent(entities, new EcsTestSharedComp2());
                                })
                            .ScheduleParallel();
                        break;
                }

                var testEntityCommandBufferSystem = World.GetExistingSystemManaged<TestEntityCommandBufferSystem>();
                testEntityCommandBufferSystem.Update();

                foreach (var entity in entities)
                {
                    Assert.AreEqual(10, EntityManager.GetSharedComponentManaged<EcsTestSharedComp>(entity).value);
                    Assert.IsTrue(EntityManager.HasComponent<EcsTestSharedComp2>(entity));
                }
            }
#endregion

#region CreateEntity
            public void CreateEntity_WithImmediatePlayback()
            {
                var archetype = EntityManager.CreateArchetype(typeof(EcsTestTag));
                EntityManager.CreateEntity(archetype);

                Entities
                    .WithImmediatePlayback()
                    .ForEach(
                        (EntityCommandBuffer ecb) =>
                        {
                            ecb.CreateEntity();
                            ecb.CreateEntity(archetype);
                        })
                    .Run();

                Assert.AreEqual(3, EntityManager.GetAllEntities().Length);
                Assert.AreEqual(2, EntityManager.CreateEntityQuery(typeof(EcsTestTag)).CalculateEntityCount());
            }

            public void CreateEntity_WithDeferredPlayback(ScheduleType scheduleType)
            {
                var archetype = EntityManager.CreateArchetype(typeof(EcsTestTag));
                EntityManager.CreateEntity(archetype);

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.CreateEntity();
                                    ecb.CreateEntity(archetype);
                                })
                            .Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.CreateEntity();
                                    ecb.CreateEntity(archetype);
                                })
                            .Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.CreateEntity();
                                    ecb.CreateEntity(archetype);
                                })
                            .ScheduleParallel();
                        break;
                }

                var testEntityCommandBufferSystem = World.GetExistingSystemManaged<TestEntityCommandBufferSystem>();
                testEntityCommandBufferSystem.Update();

                Assert.AreEqual(3, EntityManager.GetAllEntities().Length);
                Assert.AreEqual(2, EntityManager.CreateEntityQuery(typeof(EcsTestTag)).CalculateEntityCount());
            }
#endregion

#region DestroyEntity/Entities
            public void DestroyEntity_WithImmediatePlayback()
            {
                var entity = EntityManager.CreateEntity();

                Entities
                    .WithImmediatePlayback()
                    .ForEach(
                        (Entity e, EntityCommandBuffer ecb) =>
                        {
                            ecb.DestroyEntity(e);
                        })
                    .Run();

                Assert.IsFalse(EntityManager.Exists(entity));
            }

            public void DestroyEntity_WithDeferredPlayback(ScheduleType scheduleType)
            {
                var entity = EntityManager.CreateEntity();

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.DestroyEntity(e);
                                })
                            .Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.DestroyEntity(e);
                                })
                            .Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.DestroyEntity(e);
                                })
                            .ScheduleParallel();
                        break;
                }
                var testEntityCommandBufferSystem = World.GetExistingSystemManaged<TestEntityCommandBufferSystem>();
                testEntityCommandBufferSystem.Update();

                Assert.IsFalse(EntityManager.Exists(entity));
            }


            public void DestroyEntities_WithImmediatePlayback()
            {
                var entities = EntityManager.CreateEntity(EntityManager.CreateArchetype(), 2, Allocator.Temp);

                Entities
                    .WithImmediatePlayback()
                    .ForEach(
                        (EntityCommandBuffer ecb) =>
                        {
                            ecb.DestroyEntity(entities);
                        })
                    .Run();

                Assert.AreEqual(0, EntityManager.GetAllEntities().Length);
                entities.Dispose();
            }

            public void DestroyEntities_WithDeferredPlayback(ScheduleType scheduleType)
            {
                var entities = EntityManager.CreateEntity(EntityManager.CreateArchetype(), 2, World.UpdateAllocator.ToAllocator);

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.DestroyEntity(entities);
                                })
                            .Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.DestroyEntity(entities);
                                })
                            .Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities
                            .WithReadOnly(entities)
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.DestroyEntity(entities);
                                })
                            .ScheduleParallel();
                        break;
                }

                var testEntityCommandBufferSystem = World.GetExistingSystemManaged<TestEntityCommandBufferSystem>();
                testEntityCommandBufferSystem.Update();

                Assert.AreEqual(0, EntityManager.GetAllEntities().Length);
            }
#endregion

#region DynamicBufferFunctionality
            public void AddSetAppendBuffer_WithDeferredPlayback(ScheduleType scheduleType)
            {
                var entity = EntityManager.CreateEntity();

                var dynamicBuffer = EntityManager.AddBuffer<EcsIntElement>(entity);
                dynamicBuffer.Add(new EcsIntElement{Value = 1});

                var intArray = CollectionHelper.CreateNativeArray<EcsIntElement>(5, EntityManager.World.UpdateAllocator.ToAllocator);

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach((Entity e, EntityCommandBuffer ecb) =>
                            {
                                var ecsIntElements = ecb.SetBuffer<EcsIntElement>(e);
                                ecsIntElements.Add(new EcsIntElement{Value = 2});

                                var ecsIntElement2s = ecb.AddBuffer<EcsIntElement2>(e);
                                ecsIntElement2s.Add(new EcsIntElement2());

                                ecb.AppendToBuffer(e, new EcsIntElement {Value = 3});
                                ecb.AppendToBuffer(e, intArray[0]);
                            }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach((Entity e, EntityCommandBuffer ecb) =>
                            {
                                var ecsIntElements = ecb.SetBuffer<EcsIntElement>(e);
                                ecsIntElements.Add(new EcsIntElement{Value = 2});

                                var ecsIntElement2s = ecb.AddBuffer<EcsIntElement2>(e);
                                ecsIntElement2s.Add(new EcsIntElement2());

                                ecb.AppendToBuffer(e, new EcsIntElement {Value = 3});
                                ecb.AppendToBuffer(e, intArray[0]);
                            }).Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities
                            .WithReadOnly(intArray)
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach((Entity e, EntityCommandBuffer ecb) =>
                            {
                                var ecsIntElements = ecb.SetBuffer<EcsIntElement>(e);
                                ecsIntElements.Add(new EcsIntElement{Value = 2});

                                var ecsIntElement2s = ecb.AddBuffer<EcsIntElement2>(e);
                                ecsIntElement2s.Add(new EcsIntElement2());

                                ecb.AppendToBuffer(e, new EcsIntElement {Value = 3});
                                ecb.AppendToBuffer(e, intArray[0]);
                            }).ScheduleParallel();
                        break;
                }

                var testEntityCommandBufferSystem = World.GetExistingSystemManaged<TestEntityCommandBufferSystem>();
                testEntityCommandBufferSystem.Update();

                var intElements = EntityManager.GetBuffer<EcsIntElement>(entity);
                Assert.AreEqual(expected: 3, intElements.Length);
                Assert.AreEqual(expected: 2, intElements[0].Value);
                Assert.AreEqual(expected: 3, intElements[1].Value);
                Assert.AreEqual(expected: 0, intElements[2].Value);

                var intElement2s = EntityManager.GetBuffer<EcsIntElement2>(entity);
                Assert.AreEqual(expected: 1, intElement2s.Length);

                intArray.Dispose();
            }

            public void AddSetAppendBuffer_WithImmediatePlayback()
            {
                var entity = EntityManager.CreateEntity();
                var dynamicBuffer = EntityManager.AddBuffer<EcsIntElement>(entity);
                dynamicBuffer.Add(new EcsIntElement{Value = 1});

                Entities
                    .WithImmediatePlayback()
                    .ForEach((Entity e, EntityCommandBuffer ecb) =>
                    {
                        var ecsIntElements = ecb.SetBuffer<EcsIntElement>(e);
                        ecsIntElements.Add(new EcsIntElement{Value = 2});

                        var ecsIntElement2s = ecb.AddBuffer<EcsIntElement2>(e);
                        ecsIntElement2s.Add(new EcsIntElement2());

                        ecb.AppendToBuffer(e, new EcsIntElement {Value = 3});
                    }).Run();

                var intElements = EntityManager.GetBuffer<EcsIntElement>(entity);
                Assert.AreEqual(expected: 2, intElements.Length);
                Assert.AreEqual(expected: 2, intElements[0].Value);
                Assert.AreEqual(expected: 3, intElements[1].Value);

                var intElement2s = EntityManager.GetBuffer<EcsIntElement2>(entity);
                Assert.AreEqual(expected: 1, intElement2s.Length);
            }
#endregion

#region EntityQueryFunctionality
            public void AddComponentForEntityQuery(PlaybackType playbackType)
            {
                var entity = EntityManager.CreateEntity(typeof(EcsTestTag));
                var entityQuery = EntityManager.CreateEntityQuery(typeof(EcsTestTag));

                switch (playbackType)
                {
                    case PlaybackType.Immediate:
                    {
                        Entities
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                            .WithoutBurst()
#endif
                            .WithImmediatePlayback()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.AddComponent(entityQuery, new EcsTestData());

                                    ecb.AddComponent<EcsTestData2>(entityQuery, EntityQueryCaptureMode.AtPlayback);
                                    ecb.AddComponent(entityQuery, ComponentType.ReadOnly<EcsTestData3>(), EntityQueryCaptureMode.AtPlayback);
                                    ecb.AddComponent(entityQuery, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData4>(), ComponentType.ReadOnly<EcsTestData5>()),
                                        EntityQueryCaptureMode.AtPlayback);
                                    ecb.AddSharedComponent(entityQuery, new EcsTestSharedComp(), EntityQueryCaptureMode.AtPlayback);
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                                    ecb.AddComponent(entityQuery, new EcsTestManagedComponent());
                                    ecb.AddComponentObject(entityQuery, new EcsTestManagedComponent2());
#endif
                                })
                            .Run();
                        break;
                    }
                    case PlaybackType.Deferred:
                    {
                        Entities
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                            .WithoutBurst()
#endif
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.AddComponent(entityQuery, new EcsTestData());

                                    ecb.AddComponent<EcsTestData2>(entityQuery, EntityQueryCaptureMode.AtPlayback);
                                    ecb.AddComponent(entityQuery, ComponentType.ReadOnly<EcsTestData3>(), EntityQueryCaptureMode.AtPlayback);
                                    ecb.AddComponent(entityQuery, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData4>(), ComponentType.ReadOnly<EcsTestData5>()),
                                        EntityQueryCaptureMode.AtPlayback);
                                    ecb.AddSharedComponent(entityQuery, new EcsTestSharedComp(), EntityQueryCaptureMode.AtPlayback);
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                                    ecb.AddComponent(entityQuery, new EcsTestManagedComponent());
                                    ecb.AddComponentObject(entityQuery, new EcsTestManagedComponent2());
#endif
                                })
                            .Run();

                        var testEntityCommandBufferSystem = World.GetExistingSystemManaged<TestEntityCommandBufferSystem>();
                        testEntityCommandBufferSystem.Update();
                        break;
                    }
                }
                Assert.IsTrue(EntityManager.HasComponent<EcsTestData>(entity));

                Assert.IsTrue(EntityManager.HasComponent<EcsTestData2>(entity));
                Assert.IsTrue(EntityManager.HasComponent<EcsTestData3>(entity));
                Assert.IsTrue(EntityManager.HasComponent<EcsTestData4>(entity));
                Assert.IsTrue(EntityManager.HasComponent<EcsTestData5>(entity));
                Assert.IsTrue(EntityManager.HasComponent<EcsTestSharedComp>(entity));
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                Assert.IsTrue(EntityManager.HasComponent<EcsTestManagedComponent>(entity));
                Assert.IsTrue(EntityManager.HasComponent<EcsTestManagedComponent2>(entity));
#endif
            }

            public void SetComponentForEntityQuery(PlaybackType playbackType)
            {
                var entity = EntityManager.CreateEntity(
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                    ComponentType.ReadWrite<EcsTestManagedComponent>(),
                    ComponentType.ReadWrite<EcsTestManagedComponent2>(),
#endif
                    ComponentType.ReadWrite<EcsTestSharedComp>());

                var entityQuery = EntityManager.CreateEntityQuery(
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                    ComponentType.ReadWrite<EcsTestManagedComponent>(),
                    ComponentType.ReadWrite<EcsTestManagedComponent2>(),
#endif
                    ComponentType.ReadWrite<EcsTestSharedComp>());

                switch (playbackType)
                {
                    case PlaybackType.Immediate:
                        Entities
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                            .WithoutBurst()
#endif
                            .WithImmediatePlayback()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                                    ecb.SetComponentObject(entityQuery, new EcsTestManagedComponent{ value = "MyNewValue1" });
                                    ecb.SetComponent(entityQuery, new EcsTestManagedComponent2 { value2 = "MyNewValue2" });
#endif
                                    ecb.SetSharedComponent(entityQuery, new EcsTestSharedComp { value = 10 }, EntityQueryCaptureMode.AtPlayback);
                                })
                            .Run();
                        break;
                    case PlaybackType.Deferred:
                        Entities
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                            .WithoutBurst()
#endif
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                                    ecb.SetComponentObject(entityQuery, new EcsTestManagedComponent{ value = "MyNewValue1" });
                                    ecb.SetComponent(entityQuery, new EcsTestManagedComponent2 { value2 = "MyNewValue2" });
#endif
                                    ecb.SetSharedComponent(entityQuery, new EcsTestSharedComp { value = 10 }, EntityQueryCaptureMode.AtPlayback);
                                })
                            .Run();

                        var testEntityCommandBufferSystem = World.GetExistingSystemManaged<TestEntityCommandBufferSystem>();
                        testEntityCommandBufferSystem.Update();
                        break;
                }
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                Assert.AreEqual("MyNewValue1", EntityManager.GetComponentData<EcsTestManagedComponent>(entity).value);
                Assert.AreEqual("MyNewValue2", EntityManager.GetComponentData<EcsTestManagedComponent2>(entity).value2);
#endif
                Assert.AreEqual(10, EntityManager.GetSharedComponentManaged<EcsTestSharedComp>(entity).value);
            }

            public void RemoveComponentForEntityQuery(PlaybackType playbackType)
            {
                var entity = EntityManager.CreateEntity(ComponentType.ReadOnly<EcsTestTag>(), ComponentType.ReadOnly<EcsTestData>(), ComponentType.ReadOnly<EcsTestData2>(), ComponentType.ReadOnly<EcsTestData3>(), ComponentType.ReadOnly<EcsTestData4>());
                var entityQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<EcsTestTag>());

                switch (playbackType)
                {
                    case PlaybackType.Immediate:
                        Entities
                            .WithImmediatePlayback()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.RemoveComponent<EcsTestData>(entityQuery, EntityQueryCaptureMode.AtPlayback);
                                    ecb.RemoveComponent(entityQuery, ComponentType.ReadOnly<EcsTestData2>(), EntityQueryCaptureMode.AtPlayback);
                                    ecb.RemoveComponent(entityQuery, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData3>(), ComponentType.ReadOnly<EcsTestData4>()),
                                        EntityQueryCaptureMode.AtPlayback);
                                })
                            .Run();
                        break;
                    case PlaybackType.Deferred:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.RemoveComponent<EcsTestData>(entityQuery, EntityQueryCaptureMode.AtPlayback);
                                    ecb.RemoveComponent(entityQuery, ComponentType.ReadOnly<EcsTestData2>(), EntityQueryCaptureMode.AtPlayback);
                                    ecb.RemoveComponent(entityQuery, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData3>(), ComponentType.ReadOnly<EcsTestData4>()),
                                        EntityQueryCaptureMode.AtPlayback);
                                })
                            .Run();

                        var testEntityCommandBufferSystem = World.GetExistingSystemManaged<TestEntityCommandBufferSystem>();
                        testEntityCommandBufferSystem.Update();
                        break;
                }

                Assert.IsTrue(EntityManager.HasComponent<EcsTestTag>(entity));
                Assert.IsFalse(EntityManager.HasComponent<EcsTestData>(entity));
                Assert.IsFalse(EntityManager.HasComponent<EcsTestData2>(entity));
                Assert.IsFalse(EntityManager.HasComponent<EcsTestData3>(entity));
                Assert.IsFalse(EntityManager.HasComponent<EcsTestData4>(entity));
            }

            public void DestroyEntitiesForEntityQuery(PlaybackType playbackType)
            {
                var entities = EntityManager.CreateEntity(EntityManager.CreateArchetype(), 10, Allocator.Temp);
                for (int i = 0; i < entities.Length / 2; i++)
                {
                    EntityManager.AddComponent<EcsTestTag>(entities[i]);
                }

                var entityQuery = EntityManager.CreateEntityQuery(typeof(EcsTestTag));

                switch (playbackType)
                {
                    case PlaybackType.Immediate:
                    {
                        Entities
                            .WithImmediatePlayback()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.DestroyEntity(entityQuery, EntityQueryCaptureMode.AtPlayback);
                                })
                            .Run();
                        break;
                    }
                    case PlaybackType.Deferred:
                    {
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.DestroyEntity(entityQuery, EntityQueryCaptureMode.AtPlayback);
                                })
                            .Run();

                        var testEntityCommandBufferSystem = World.GetExistingSystemManaged<TestEntityCommandBufferSystem>();
                        testEntityCommandBufferSystem.Update();
                        break;
                    }
                }
                Assert.AreEqual(expected: 5, EntityManager.UniversalQuery.CalculateEntityCount());
                entities.Dispose();
            }
#endregion

#region InstantiateEntity/Entities
            public void InstantiateEntity_WithImmediatePlayback()
            {
                var entity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(entity, new EcsTestData(10));

                Entities
                    .WithImmediatePlayback()
                    .ForEach(
                        (Entity e, EntityCommandBuffer ecb) =>
                        {
                            ecb.Instantiate(e);
                            ecb.Instantiate(e);

                            ecb.DestroyEntity(e);
                        })
                    .Run();

                var allEntities = EntityManager.GetAllEntities();
                Assert.AreEqual(2, allEntities.Length);

                foreach (var e in allEntities)
                    Assert.AreEqual(10, EntityManager.GetComponentData<EcsTestData>(e).value);
            }

            public void InstantiateEntity_WithDeferredPlayback(ScheduleType scheduleType)
            {
                var entity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(entity, new EcsTestData(10));

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.Instantiate(e);
                                    ecb.Instantiate(e);

                                    ecb.DestroyEntity(e);
                                })
                            .Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.Instantiate(e);
                                    ecb.Instantiate(e);

                                    ecb.DestroyEntity(e);
                                })
                            .Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.Instantiate(e);
                                    ecb.Instantiate(e);

                                    ecb.DestroyEntity(e);
                                })
                            .ScheduleParallel();
                        break;
                }

                var testEntityCommandBufferSystem = World.GetExistingSystemManaged<TestEntityCommandBufferSystem>();
                testEntityCommandBufferSystem.Update();

                var allEntities = EntityManager.GetAllEntities();
                Assert.AreEqual(2, allEntities.Length);

                foreach (var e in allEntities)
                    Assert.AreEqual(10, EntityManager.GetComponentData<EcsTestData>(e).value);
            }

            public void InstantiateEntities_WithImmediatePlayback()
            {
                var entity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(entity, new EcsTestData(10));

                var entitiesNativeArray = CollectionHelper.CreateNativeArray<Entity>(5, Allocator.Temp);

                Entities
                    .WithImmediatePlayback()
                    .ForEach(
                        (Entity e, EntityCommandBuffer ecb) =>
                        {
                            ecb.Instantiate(e, entitiesNativeArray);
                        })
                    .Run();

                var allEntities = EntityManager.GetAllEntities();
                Assert.AreEqual(6, allEntities.Length);

                foreach (var e in allEntities)
                    Assert.AreEqual(10, EntityManager.GetComponentData<EcsTestData>(e).value);

                entitiesNativeArray.Dispose();
            }

            public void InstantiateEntities_WithDeferredPlayback(ScheduleType scheduleType)
            {
                var entity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(entity, new EcsTestData(10));

                var entitiesNativeArray = CollectionHelper.CreateNativeArray<Entity>(5, World.UpdateAllocator.ToAllocator);

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.Instantiate(e, entitiesNativeArray);
                                })
                            .Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.Instantiate(e, entitiesNativeArray);
                                })
                            .Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities
                            .WithNativeDisableParallelForRestriction(entitiesNativeArray)
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.Instantiate(e, entitiesNativeArray);
                                })
                            .ScheduleParallel();
                        break;
                }

                var testEntityCommandBufferSystem = World.GetExistingSystemManaged<TestEntityCommandBufferSystem>();
                testEntityCommandBufferSystem.Update();

                var allEntities = EntityManager.GetAllEntities();
                Assert.AreEqual(6, allEntities.Length);

                foreach (var e in allEntities)
                    Assert.AreEqual(10, EntityManager.GetComponentData<EcsTestData>(e).value);
            }
#endregion

#region RemoveComponentFromEntity/Entities
            public void RemoveComponentFromEntity_WithImmediatePlayback()
            {
                var entityArchetype = EntityManager.CreateArchetype(ComponentType.ReadOnly<EcsTestTag>(), ComponentType.ReadOnly<EcsTestData>(), ComponentType.ReadOnly<EcsTestData2>(), ComponentType.ReadOnly<EcsTestData3>());
                var entity = EntityManager.CreateEntity(entityArchetype);

                Entities
                    .WithImmediatePlayback()
                    .ForEach(
                        (Entity e, EntityCommandBuffer ecb) =>
                        {
                            ecb.RemoveComponent<EcsTestTag>(e);
                            ecb.RemoveComponent(e, ComponentType.ReadOnly<EcsTestData>());
                            ecb.RemoveComponent(e, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData2>(), ComponentType.ReadOnly<EcsTestData3>()));
                        })
                    .Run();

                Assert.IsFalse(EntityManager.HasComponent<EcsTestTag>(entity));
                Assert.IsFalse(EntityManager.HasComponent<EcsTestData>(entity));
                Assert.IsFalse(EntityManager.HasComponent<EcsTestData2>(entity));
                Assert.IsFalse(EntityManager.HasComponent<EcsTestData3>(entity));
            }

            public void RemoveComponentFromEntities_WithImmediatePlayback()
            {
                var entityArchetype = EntityManager.CreateArchetype(ComponentType.ReadOnly<EcsTestTag>(), ComponentType.ReadOnly<EcsTestData>(), ComponentType.ReadOnly<EcsTestData2>(), ComponentType.ReadOnly<EcsTestData3>());
                var entities = EntityManager.CreateEntity(entityArchetype, 2, World.UpdateAllocator.ToAllocator);

                Entities
                    .WithImmediatePlayback()
                    .ForEach(
                        (EntityCommandBuffer ecb) =>
                        {
                            ecb.RemoveComponent<EcsTestTag>(entities);
                            ecb.RemoveComponent(entities,ComponentType.ReadOnly<EcsTestData>());
                            ecb.RemoveComponent(entities, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData2>(), ComponentType.ReadOnly<EcsTestData3>()));
                        })
                    .Run();

                foreach (var entity in entities)
                {
                    Assert.IsFalse(EntityManager.HasComponent<EcsTestTag>(entity));
                    Assert.IsFalse(EntityManager.HasComponent<EcsTestData>(entity));
                    Assert.IsFalse(EntityManager.HasComponent<EcsTestData2>(entity));
                    Assert.IsFalse(EntityManager.HasComponent<EcsTestData3>(entity));
                }
            }

            public void RemoveComponentFromEntity_WithDeferredPlayback(ScheduleType scheduleType)
            {
                var entityArchetype = EntityManager.CreateArchetype(ComponentType.ReadOnly<EcsTestTag>(), ComponentType.ReadOnly<EcsTestData>(), ComponentType.ReadOnly<EcsTestData2>(), ComponentType.ReadOnly<EcsTestData3>());
                var entity = EntityManager.CreateEntity(entityArchetype);

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.RemoveComponent<EcsTestTag>(e);
                                    ecb.RemoveComponent(e,ComponentType.ReadOnly<EcsTestData>());
                                    ecb.RemoveComponent(e, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData2>(), ComponentType.ReadOnly<EcsTestData3>()));
                                })
                            .Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.RemoveComponent<EcsTestTag>(e);
                                    ecb.RemoveComponent(e,ComponentType.ReadOnly<EcsTestData>());
                                    ecb.RemoveComponent(e, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData2>(), ComponentType.ReadOnly<EcsTestData3>()));
                                })
                            .Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.RemoveComponent<EcsTestTag>(e);
                                    ecb.RemoveComponent(e,ComponentType.ReadOnly<EcsTestData>());
                                    ecb.RemoveComponent(e, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData2>(), ComponentType.ReadOnly<EcsTestData3>()));
                                })
                            .ScheduleParallel();
                        break;
                }

                var testEntityCommandBufferSystem = World.GetExistingSystemManaged<TestEntityCommandBufferSystem>();
                testEntityCommandBufferSystem.Update();

                Assert.IsFalse(EntityManager.HasComponent<EcsTestTag>(entity));
                Assert.IsFalse(EntityManager.HasComponent<EcsTestData>(entity));
                Assert.IsFalse(EntityManager.HasComponent<EcsTestData2>(entity));
                Assert.IsFalse(EntityManager.HasComponent<EcsTestData3>(entity));
            }

            public void RemoveComponentFromEntities_WithDeferredPlayback(ScheduleType scheduleType)
            {
                var entityArchetype = EntityManager.CreateArchetype(ComponentType.ReadOnly<EcsTestTag>(), ComponentType.ReadOnly<EcsTestData>(), ComponentType.ReadOnly<EcsTestData2>(), ComponentType.ReadOnly<EcsTestData3>());
                var entities = EntityManager.CreateEntity(entityArchetype, 2, World.UpdateAllocator.ToAllocator);

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.RemoveComponent<EcsTestTag>(entities);
                                    ecb.RemoveComponent(entities,ComponentType.ReadOnly<EcsTestData>());
                                    ecb.RemoveComponent(entities, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData2>(), ComponentType.ReadOnly<EcsTestData3>()));
                                })
                            .Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.RemoveComponent<EcsTestTag>(entities);
                                    ecb.RemoveComponent(entities,ComponentType.ReadOnly<EcsTestData>());
                                    ecb.RemoveComponent(entities, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData2>(), ComponentType.ReadOnly<EcsTestData3>()));
                                })
                            .Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities
                            .WithReadOnly(entities)
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (EntityCommandBuffer ecb) =>
                                {
                                    ecb.RemoveComponent<EcsTestTag>(entities);
                                    ecb.RemoveComponent(entities,ComponentType.ReadOnly<EcsTestData>());
                                    ecb.RemoveComponent(entities, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData2>(), ComponentType.ReadOnly<EcsTestData3>()));
                                })
                            .ScheduleParallel();
                        break;
                }

                var testEntityCommandBufferSystem = World.GetExistingSystemManaged<TestEntityCommandBufferSystem>();
                testEntityCommandBufferSystem.Update();

                foreach (var entity in entities)
                {
                    Assert.IsFalse(EntityManager.HasComponent<EcsTestTag>(entity));
                    Assert.IsFalse(EntityManager.HasComponent<EcsTestData>(entity));
                    Assert.IsFalse(EntityManager.HasComponent<EcsTestData2>(entity));
                    Assert.IsFalse(EntityManager.HasComponent<EcsTestData3>(entity));
                }
            }
#endregion

#region SetComponent/SetComponentEnabled/SetName
            public void SetComponent_SetComponentEnabled_SetName_WithImmediatePlayback()
            {
                var entityArchetype =
                    EntityManager.CreateArchetype(
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                        typeof(EcsTestManagedComponentEnableable),
                        typeof(EcsTestManagedComponent),
#endif
                        ComponentType.ReadWrite<EcsTestData>(),
                        ComponentType.ReadWrite<EcsTestDataEnableable>(),
                        ComponentType.ReadWrite<EcsTestDataEnableable2>());

                var entity = EntityManager.CreateEntity(entityArchetype);

                EntityManager.SetComponentEnabled<EcsTestDataEnableable>(entity, value: false);
                EntityManager.SetComponentEnabled<EcsTestDataEnableable2>(entity, value: false);

#if !UNITY_DISABLE_MANAGED_COMPONENTS
                EntityManager.SetComponentEnabled<EcsTestManagedComponentEnableable>(entity, value: false);
#endif

                Entities
                    .WithoutBurst()
                    .WithImmediatePlayback()
                    .ForEach(
                        (Entity e, EntityCommandBuffer ecb) =>
                        {
                            ecb.SetComponent(e, new EcsTestData(11));
                            ecb.SetComponentEnabled<EcsTestDataEnableable>(e, value: true);
                            ecb.SetComponentEnabled(e, ComponentType.ReadWrite<EcsTestDataEnableable2>(), value: true);
#if !DOTS_DISABLE_DEBUG_NAMES
                            ecb.SetName(e, new FixedString64Bytes("Test Name"));
#endif
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                            ecb.SetComponent(e, new EcsTestManagedComponent { value = "NewString"} );
                            ecb.SetComponentEnabled<EcsTestManagedComponentEnableable>(e, value: true);
#endif
                        })
                    .Run();

                Assert.IsTrue(EntityManager.IsComponentEnabled<EcsTestDataEnableable>(entity));
                Assert.IsTrue(EntityManager.IsComponentEnabled<EcsTestDataEnableable2>(entity));

                EntityManager.GetName(entity, out var name);
                Assert.AreEqual(expected: 11, EntityManager.GetComponentData<EcsTestData>(entity).value);
#if !DOTS_DISABLE_DEBUG_NAMES
                Assert.AreEqual(expected: "Test Name", name);
#endif
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                Assert.IsTrue(EntityManager.IsComponentEnabled<EcsTestManagedComponentEnableable>(entity));
                Assert.AreEqual("NewString", EntityManager.GetComponentData<EcsTestManagedComponent>(entity).value);
#endif
            }

            public void SetComponent_SetComponentEnabled_SetName_WithDeferredPlayback(ScheduleType scheduleType)
            {
                var entity =
                    EntityManager.CreateEntity(
                        EntityManager.CreateArchetype(
                            ComponentType.ReadWrite<EcsTestData>(),
                            ComponentType.ReadWrite<EcsTestDataEnableable>(),
                            ComponentType.ReadWrite<EcsTestDataEnableable2>()));

                EntityManager.SetComponentEnabled<EcsTestDataEnableable>(entity, value: false);
                EntityManager.SetComponentEnabled<EcsTestDataEnableable2>(entity, value: false);

#if !UNITY_DISABLE_MANAGED_COMPONENTS
                if (scheduleType == ScheduleType.Run)
                {
                    EntityManager.AddComponent(entity, typeof(EcsTestManagedComponentEnableable));
                    EntityManager.SetComponentEnabled<EcsTestManagedComponentEnableable>(entity, false);

                    EntityManager.AddComponent(entity, typeof(EcsTestManagedComponent));
                }
#endif

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities
                            .WithoutBurst()
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                                    ecb.SetComponentEnabled<EcsTestManagedComponentEnableable>(entity, value: true);
                                    ecb.SetComponent(e, new EcsTestManagedComponent {value = "NewString"});
#endif

                                    ecb.SetComponent(e, new EcsTestData(11));
                                    ecb.SetComponentEnabled<EcsTestDataEnableable>(e, value: true);
                                    ecb.SetComponentEnabled(e, ComponentType.ReadWrite<EcsTestDataEnableable2>(), value: true);
#if !DOTS_DISABLE_DEBUG_NAMES
                                    ecb.SetName(e, new FixedString64Bytes("Test Name"));
#endif

                                })
                            .Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities
                            .WithoutBurst()
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.SetComponent(e, new EcsTestData(11));
                                    ecb.SetComponentEnabled<EcsTestDataEnableable>(e, value: true);
                                    ecb.SetComponentEnabled(e, ComponentType.ReadWrite<EcsTestDataEnableable2>(), value: true);
#if !DOTS_DISABLE_DEBUG_NAMES
                                    ecb.SetName(e, new FixedString64Bytes("Test Name"));
#endif
                                })
                            .Schedule();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities
                            .WithoutBurst()
                            .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                            .ForEach(
                                (Entity e, EntityCommandBuffer ecb) =>
                                {
                                    ecb.SetComponent(e, new EcsTestData(11));
                                    ecb.SetComponentEnabled<EcsTestDataEnableable>(e, value: true);
                                    ecb.SetComponentEnabled(e, ComponentType.ReadWrite<EcsTestDataEnableable2>(), value: true);
#if !DOTS_DISABLE_DEBUG_NAMES
                                    ecb.SetName(e, new FixedString64Bytes("Test Name"));
#endif
                                })
                            .ScheduleParallel();
                        break;
                }

                var testEntityCommandBufferSystem = World.GetExistingSystemManaged<TestEntityCommandBufferSystem>();
                testEntityCommandBufferSystem.Update();

                Assert.AreEqual(expected: 11, EntityManager.GetComponentData<EcsTestData>(entity).value);
                Assert.IsTrue(EntityManager.IsComponentEnabled<EcsTestDataEnableable>(entity));
                Assert.IsTrue(EntityManager.IsComponentEnabled<EcsTestDataEnableable2>(entity));

                EntityManager.GetName(entity, out var name);
#if !DOTS_DISABLE_DEBUG_NAMES
                Assert.AreEqual(expected: "Test Name", name);
#endif
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                if (scheduleType == ScheduleType.Run)
                {
                    Assert.IsTrue(EntityManager.IsComponentEnabled<EcsTestManagedComponentEnableable>(entity));
                    Assert.AreEqual("NewString", EntityManager.GetComponentData<EcsTestManagedComponent>(entity).value);
                }
#endif
            }
#endregion

#region InvokingMethodsDifferently
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            public void InvokingExtensionMethods_ByQualifyingWithExtensionClassType()
            {
                var entity = EntityManager.CreateEntity();

                Entities
                    .WithoutBurst()
                    .WithImmediatePlayback()
                    .ForEach((Entity e, EntityCommandBuffer ecb) =>
                    {
                        EntityCommandBufferManagedComponentExtensions.AddComponent(ecb, e, new EcsTestManagedComponent()); // Without named arguments
                        EntityCommandBufferManagedComponentExtensions.AddComponent(component: new EcsTestManagedComponent2(), e: e, ecb: ecb); // With named arguments
                    }).Run();

                Assert.IsTrue(EntityManager.HasComponent<EcsTestManagedComponent>(entity));
                Assert.IsTrue(EntityManager.HasComponent<EcsTestManagedComponent2>(entity));
            }
#endif

            public void InvokingMethods_WithNamedArguments()
            {
                var entity = EntityManager.CreateEntity(EntityManager.CreateArchetype(ComponentType.ReadOnly<EcsTestData>()));

                Entities
                    .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                    .ForEach((Entity e, EntityCommandBuffer ecb) =>
                    {
                        ecb.SetComponent(component: new EcsTestData{ value = 10 }, e: e);
                    }).ScheduleParallel();

                var testEntityCommandBufferSystem = World.GetExistingSystemManaged<TestEntityCommandBufferSystem>();
                testEntityCommandBufferSystem.Update();

                Assert.AreEqual(10, EntityManager.GetComponentData<EcsTestData>(entity).value);
            }
#endregion
        }

        [Test] public void AddComponentToEntity_WithImmediatePlayback() => EntityCommandsInForEachTestSystem.AddComponentToEntity_WithImmediatePlayback();
        [Test] public void AddComponentToEntities_WithImmediatePlayback() => EntityCommandsInForEachTestSystem.AddComponentToEntities_WithImmediatePlayback();
        [Test] public void AddComponentToEntity_WithDeferredPlayback([Values] ScheduleType scheduleType) => EntityCommandsInForEachTestSystem.AddComponentToEntity_WithDeferredPlayback(scheduleType);
        [Test] public void AddComponentToEntities_WithDeferredPlayback([Values] ScheduleType scheduleType)=> EntityCommandsInForEachTestSystem.AddComponentToEntities_WithDeferredPlayback(scheduleType);
        [Test] public void AddSetSharedComponentToEntity_WithImmediatePlayback() => EntityCommandsInForEachTestSystem.AddSetSharedComponentToEntity_WithImmediatePlayback();
        [Test] public void AddSetSharedComponentToEntities_WithImmediatePlayback() => EntityCommandsInForEachTestSystem.AddSetSharedComponentToEntities_WithImmediatePlayback();
        [Test] public void AddSetSharedComponentToEntity_WithDeferredPlayback([Values] ScheduleType scheduleType) => EntityCommandsInForEachTestSystem.AddSetSharedComponentToEntity_WithDeferredPlayback(scheduleType);
        [Test] public void AddSetSharedComponentToEntities_WithDeferredPlayback([Values] ScheduleType scheduleType) => EntityCommandsInForEachTestSystem.AddSetSharedComponentToEntities_WithDeferredPlayback(scheduleType);
        [Test] public void CreateEntity_WithImmediatePlayback() => EntityCommandsInForEachTestSystem.CreateEntity_WithImmediatePlayback();
        [Test] public void CreateEntity_WithDeferredPlayback([Values] ScheduleType scheduleType) => EntityCommandsInForEachTestSystem.CreateEntity_WithDeferredPlayback(scheduleType);
        [Test] public void DestroyEntity_WithImmediatePlayback() => EntityCommandsInForEachTestSystem.DestroyEntity_WithImmediatePlayback();
        [Test] public void DestroyEntities_WithImmediatePlayback() => EntityCommandsInForEachTestSystem.DestroyEntities_WithImmediatePlayback();
        [Test] public void DestroyEntity_WithDeferredPlayback([Values] ScheduleType scheduleType) => EntityCommandsInForEachTestSystem.DestroyEntity_WithDeferredPlayback(scheduleType);
        [Test] public void DestroyEntities_WithDeferredPlayback([Values] ScheduleType scheduleType) => EntityCommandsInForEachTestSystem.DestroyEntities_WithDeferredPlayback(scheduleType);
        [Test] public void AddSetAppendBuffer_WithImmediatePlayback() => EntityCommandsInForEachTestSystem.AddSetAppendBuffer_WithImmediatePlayback();
        [Test] public void AddSetAppendBuffer_WithDeferredPlayback([Values] ScheduleType scheduleType) => EntityCommandsInForEachTestSystem.AddSetAppendBuffer_WithDeferredPlayback(scheduleType);
        [Test] public void AddComponentForEntityQuery([Values] PlaybackType playbackType) => EntityCommandsInForEachTestSystem.AddComponentForEntityQuery(playbackType);
        [Test] public void DestroyEntitiesForEntityQuery([Values] PlaybackType playbackType) => EntityCommandsInForEachTestSystem.DestroyEntitiesForEntityQuery(playbackType);
        [Test] public void SetComponentForEntityQuery([Values] PlaybackType playbackType) => EntityCommandsInForEachTestSystem.SetComponentForEntityQuery(playbackType);
        [Test] public void RemoveComponentForEntityQuery([Values] PlaybackType playbackType) => EntityCommandsInForEachTestSystem.RemoveComponentForEntityQuery(playbackType);
        [Test] public void InstantiateEntity_WithImmediatePlayback() => EntityCommandsInForEachTestSystem.InstantiateEntity_WithImmediatePlayback();
        [Test] public void InstantiateEntities_WithImmediatePlayback() => EntityCommandsInForEachTestSystem.InstantiateEntities_WithImmediatePlayback();
        [Test] public void InstantiateEntity_WithDeferredPlayback([Values] ScheduleType scheduleType) => EntityCommandsInForEachTestSystem.InstantiateEntity_WithDeferredPlayback(scheduleType);
        [Test] public void InstantiateEntities_WithDeferredPlayback([Values] ScheduleType scheduleType) => EntityCommandsInForEachTestSystem.InstantiateEntities_WithDeferredPlayback(scheduleType);
        [Test] public void RemoveComponentFromEntity_WithImmediatePlayback() => EntityCommandsInForEachTestSystem.RemoveComponentFromEntity_WithImmediatePlayback();
        [Test] public void RemoveComponentFromEntities_WithImmediatePlayback() => EntityCommandsInForEachTestSystem.RemoveComponentFromEntities_WithImmediatePlayback();
        [Test] public void RemoveComponentFromEntity_WithDeferredPlayback([Values] ScheduleType scheduleType) => EntityCommandsInForEachTestSystem.RemoveComponentFromEntity_WithDeferredPlayback(scheduleType);
        [Test] public void RemoveComponentFromEntities_WithDeferredPlayback([Values] ScheduleType scheduleType) => EntityCommandsInForEachTestSystem.RemoveComponentFromEntities_WithDeferredPlayback(scheduleType);
        [Test] public void SetComponent_SetComponentEnabled_SetName_WithImmediatePlayback() => EntityCommandsInForEachTestSystem.SetComponent_SetComponentEnabled_SetName_WithImmediatePlayback();
        [Test] public void SetComponent_SetComponentEnabled_SetName_WithDeferredPlayback([Values] ScheduleType scheduleType) => EntityCommandsInForEachTestSystem.SetComponent_SetComponentEnabled_SetName_WithDeferredPlayback(scheduleType);
        [Test] public void InvokingMethods_WithNamedArguments() => EntityCommandsInForEachTestSystem.InvokingMethods_WithNamedArguments();
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test] public void InvokingExtensionMethods_ByQualifyingWithExtensionClassType() => EntityCommandsInForEachTestSystem.InvokingExtensionMethods_ByQualifyingWithExtensionClassType();
#endif
    }
}
