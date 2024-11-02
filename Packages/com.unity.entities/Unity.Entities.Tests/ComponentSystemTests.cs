using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Scripting;
using Unity.Burst;
using System.Collections.Generic;
using Unity.Burst.Intrinsics;
using static Unity.Entities.SystemAPI;

namespace Unity.Entities.Tests
{
    partial class ComponentSystemTests : ECSTestsFixture
    {
        partial class TestGroup : ComponentSystemGroup
        {
        }

        partial class TestSystem : SystemBase
        {
            public bool Created = false;

            protected override void OnUpdate()
            {
            }

            protected override void OnCreate()
            {
                Created = true;
            }

            protected override void OnDestroy()
            {
                Created = false;
            }
        }

        partial class DerivedTestSystem : TestSystem
        {
            protected override void OnUpdate()
            {
            }
        }

        partial class ThrowExceptionSystem : TestSystem
        {
            protected override void OnCreate()
            {
                throw new System.Exception();
            }

            protected override void OnUpdate()
            {
            }
        }

        partial class ScheduleJobAndDestroyArray : SystemBase
        {
            NativeArray<int> test = new NativeArray<int>(10, Allocator.Persistent);

            new struct Job : IJob
            {
                public NativeArray<int> test;

                public void Execute() { }
            }

            protected override void OnUpdate()
            {
                Dependency = new Job() { test = test }.Schedule(Dependency);
            }

            protected override void OnDestroy()
            {
                // We expect this to not throw an exception since the jobs scheduled
                // by this system should be synced before the system is destroyed
                test.Dispose();
            }
        }

        [Test]
        public void Create()
        {
            var system = World.CreateSystemManaged<TestSystem>();
            Assert.AreEqual(system, World.GetExistingSystemManaged<TestSystem>());
            Assert.IsTrue(system.Created);
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void ComponentSystem_CheckExistsAfterDestroy_CorrectMessage()
        {
            var destroyedSystem = World.CreateSystemManaged<TestSystem>();
            World.DestroySystemManaged(destroyedSystem);
            Assert.That(() => { destroyedSystem.ShouldRunSystem(); },
                Throws.InvalidOperationException.With.Message.Contains("destroyed"));
        }

#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void ComponentSystem_CheckExistsBeforeCreate_CorrectMessage()
        {
            var incompleteSystem = new TestSystem();
            Assert.That(() => { incompleteSystem.ShouldRunSystem(); },
                Throws.InvalidOperationException.With.Message.Contains("initialized"));
        }
#endif

        [Test]
        public void CreateAndDestroy()
        {
            var system = World.CreateSystemManaged<TestSystem>();
            World.DestroySystemManaged(system);
            Assert.AreEqual(null, World.GetExistingSystemManaged<TestSystem>());
            Assert.IsFalse(system.Created);
        }

        [Test]
        public void GetOrCreateSystemReturnsSameSystem()
        {
            var system = World.GetOrCreateSystemManaged<TestSystem>();
            Assert.AreEqual(system, World.GetOrCreateSystemManaged<TestSystem>());
        }

        [Test]
        public void InheritedSystem()
        {
            var system = World.CreateSystemManaged<DerivedTestSystem>();
            Assert.AreEqual(system, World.GetExistingSystemManaged<DerivedTestSystem>());
            Assert.AreEqual(system, World.GetExistingSystemManaged<TestSystem>());

            World.DestroySystemManaged(system);

            Assert.AreEqual(null, World.GetExistingSystemManaged<DerivedTestSystem>());
            Assert.AreEqual(null, World.GetExistingSystemManaged<TestSystem>());

            Assert.IsFalse(system.Created);
        }

        [Test]
        public void CreateNonSystemThrows()
        {
            Assert.Throws<ArgumentException>(() => { World.CreateSystemManaged(typeof(Entity)); });
        }

        [Test]
        public void GetOrCreateNonSystemThrows()
        {
            Assert.Throws<ArgumentException>(() => { World.GetOrCreateSystemManaged(typeof(Entity)); });
        }

        [Test]
        public void OnCreateThrowRemovesSystem()
        {
            Assert.Throws<Exception>(() => { World.CreateSystemManaged<ThrowExceptionSystem>(); });
            Assert.AreEqual(null, World.GetExistingSystemManaged<ThrowExceptionSystem>());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void DisposeSystemEntityQueryThrows()
        {
            var system = World.CreateSystemManaged<EmptySystem>();
            var group = system.GetEntityQuery(typeof(EcsTestData));
            Assert.Throws<InvalidOperationException>(() => group.Dispose());
        }

        [Test]
        public void DestroySystemTwiceThrows()
        {
            var system = World.CreateSystemManaged<TestSystem>();
            World.DestroySystemManaged(system);
            Assert.Throws<ArgumentException>(() => World.DestroySystemManaged(system));
        }

        [Test]
        public void DestroySystemWhileJobUsingArrayIsRunningWorks()
        {
            var system = World.CreateSystemManaged<ScheduleJobAndDestroyArray>();
            system.Update();
            World.DestroySystemManaged(system);
        }

        [Test]
        public void CreateTwoSystemsOfSameType()
        {
            var systemA = World.CreateSystemManaged<TestSystem>();
            var systemB = World.CreateSystemManaged<TestSystem>();
            // CreateSystem makes a new system
            Assert.AreNotEqual(systemA, systemB);
            // Return first system
            Assert.AreEqual(systemA, World.GetOrCreateSystemManaged<TestSystem>());
        }

        [Test]
        public void CreateTwoSystemsAfterDestroyReturnSecond()
        {
            var systemA = World.CreateSystemManaged<TestSystem>();
            var systemB = World.CreateSystemManaged<TestSystem>();
            World.DestroySystemManaged(systemA);

            Assert.AreEqual(systemB, World.GetExistingSystemManaged<TestSystem>());
        }

        [Test]
        public void CreateTwoSystemsAfterDestroyReturnFirst()
        {
            var systemA = World.CreateSystemManaged<TestSystem>();
            var systemB = World.CreateSystemManaged<TestSystem>();
            World.DestroySystemManaged(systemB);

            Assert.AreEqual(systemA, World.GetExistingSystemManaged<TestSystem>());
        }

        [Test]
        public void GetEntityQuery()
        {
            ComponentType[] ro_rw = { ComponentType.ReadOnly<EcsTestData>(), typeof(EcsTestData2) };
            ComponentType[] rw_rw = { typeof(EcsTestData), typeof(EcsTestData2) };
            ComponentType[] rw = { typeof(EcsTestData) };

            var ro_rw0_system = EmptySystem.GetEntityQuery(ro_rw);
            var rw_rw_system = EmptySystem.GetEntityQuery(rw_rw);
            var rw_system = EmptySystem.GetEntityQuery(rw);

            Assert.AreEqual(ro_rw0_system, EmptySystem.GetEntityQuery(ro_rw));
            Assert.AreEqual(rw_rw_system, EmptySystem.GetEntityQuery(rw_rw));
            Assert.AreEqual(rw_system, EmptySystem.GetEntityQuery(rw));

            Assert.AreEqual(3, EmptySystem.EntityQueries.Length);
        }

        [Test]
        public void GetEntityQuery_ArchetypeQuery()
        {
            var queryDesc1 = new ComponentType[] { typeof(EcsTestData) };
            var queryDesc2 = new EntityQueryDesc { All = new ComponentType[] { typeof(EcsTestData) } };
            var queryDesc3 = new EntityQueryDesc { All = new ComponentType[] { typeof(EcsTestData), typeof(EcsTestData2) } };

            var query1 = EmptySystem.GetEntityQuery(queryDesc1);
            var query2 = EmptySystem.GetEntityQuery(queryDesc2);
            var query3 = EmptySystem.GetEntityQuery(queryDesc3);

            Assert.AreEqual(query1, EmptySystem.GetEntityQuery(queryDesc1));
            Assert.AreEqual(query2, EmptySystem.GetEntityQuery(queryDesc2));
            Assert.AreEqual(query3, EmptySystem.GetEntityQuery(queryDesc3));

            Assert.AreEqual(2, EmptySystem.EntityQueries.Length);
        }

        [Test]
        public void GetEntityQuery_ComponentTypeArchetypeQueryEquality()
        {
            var queryDesc1 = new ComponentType[] { typeof(EcsTestData) };
            var queryDesc2 = new EntityQueryDesc { All = new ComponentType[] { typeof(EcsTestData) } };
            var queryDesc3 = new EntityQueryDesc { All = new[] { ComponentType.ReadWrite<EcsTestData>() } };

            var query1 = EmptySystem.GetEntityQuery(queryDesc1);
            var query2 = EmptySystem.GetEntityQuery(queryDesc2);
            var query3 = EmptySystem.GetEntityQuery(queryDesc3);

            Assert.AreEqual(query1, query2);
            Assert.AreEqual(query2, query3);
            Assert.AreEqual(1, EmptySystem.EntityQueries.Length);
        }

        [Test]
        public void GetEntityQuery_RespectsRWAccessInequality()
        {
            var queryDesc1 = new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<EcsTestData>(), ComponentType.ReadWrite<EcsTestData2>() } };
            var queryDesc2 = new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<EcsTestData>(), ComponentType.ReadOnly<EcsTestData2>() } };

            var query1 = EmptySystem.GetEntityQuery(queryDesc1);
            var query2 = EmptySystem.GetEntityQuery(queryDesc2);

            Assert.AreNotEqual(query1, query2);
            Assert.AreEqual(2, EmptySystem.EntityQueries.Length);
        }

        [Test]
        public void GetEntityQuery_OrderIndependent()
        {
            var queryTypes1 = new ComponentType[] { typeof(EcsTestData), typeof(EcsTestData2) };
            var queryTypes2 = new ComponentType[] { typeof(EcsTestData2), typeof(EcsTestData) };

            var query1 = EmptySystem.GetEntityQuery(queryTypes1);
            var query2 = EmptySystem.GetEntityQuery(queryTypes2);

            Assert.AreEqual(query1, query2);
            Assert.AreEqual(1, EmptySystem.EntityQueries.Length);

            var queryDesc3 = new EntityQueryDesc { All = new ComponentType[] { typeof(EcsTestData2), typeof(EcsTestData3) } };
            var queryDesc4 = new EntityQueryDesc { All = new ComponentType[] { typeof(EcsTestData3), typeof(EcsTestData2) } };

            var query3 = EmptySystem.GetEntityQuery(queryDesc3);
            var query4 = EmptySystem.GetEntityQuery(queryDesc4);

            Assert.AreEqual(query3, query4);
            Assert.AreEqual(2, EmptySystem.EntityQueries.Length);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void GetEntityQuery_WithEntity_Throws()
        {
            // Entity is always included as an implicit type. Including it in the components list
            // messes with query equality testing.
            ComponentType[] e = { typeof(Entity), typeof(EcsTestData) };
            Assert.Throws<ArgumentException>(() => EmptySystem.GetEntityQuery(e));
            Assert.Throws<ArgumentException>(() => EmptySystem.GetEntityQuery(typeof(Entity)));
            Assert.Throws<ArgumentException>(() => EmptySystem.GetEntityQuery(ComponentType.ReadOnly<Entity>()));

            var queryDescWithEntity = new EntityQueryDesc { All = new[] { ComponentType.ReadWrite<EcsTestData>(), ComponentType.ReadOnly<Entity>() } };
            var goodQueryDesc = new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<EcsTestData2>() } };
            Assert.Throws<ArgumentException>(() => EmptySystem.GetEntityQuery(queryDescWithEntity), "Entity type should not be allowed in EntityQueryDesc");
            Assert.Throws<ArgumentException>(() => EmptySystem.GetEntityQuery(goodQueryDesc, queryDescWithEntity), "Fails with Entity is in second EntityQueryDesc");

            Assert.Throws<ArgumentException>(() =>
            {
                var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<Entity>();
                EmptySystem.GetEntityQuery(builder);
            }, "Entity type should not be allowed in EntityQueryBuilder");
        }

        [Test]
        public void GetEntityQuery_WithDuplicates()
        {
            // Currently duplicates will create two seperate groups doing the same thing...
            ComponentType[] dup_1 = { typeof(EcsTestData2) };
            ComponentType[] dup_2 = { typeof(EcsTestData2), typeof(EcsTestData3) };

            var dup1_system = EmptySystem.GetEntityQuery(dup_1);
            var dup2_system = EmptySystem.GetEntityQuery(dup_2);

            Assert.AreEqual(dup1_system, EmptySystem.GetEntityQuery(dup_1));
            Assert.AreEqual(dup2_system, EmptySystem.GetEntityQuery(dup_2));

            Assert.AreEqual(2, EmptySystem.EntityQueries.Length);
        }

        [Test]
        public void UpdateDestroyedSystemThrows()
        {
            var system = EmptySystem;
            World.DestroySystemManaged(system);
            Assert.Throws<InvalidOperationException>(system.Update);
        }

        partial class SharedComponentTypeHandleUpdateSystem : SystemBase
        {
            private SharedComponentTypeHandle<EcsTestSharedComp> sharedComponentTypeHandle1;
            private Entity _entity;
            protected override void OnCreate()
            {
                _entity = EntityManager.CreateEntity(typeof(EcsTestSharedComp));
                sharedComponentTypeHandle1 = GetSharedComponentTypeHandle<EcsTestSharedComp>();
            }

            protected override void OnUpdate()
            {
                var sharedComponentTypeHandle2 = GetSharedComponentTypeHandle<EcsTestSharedComp>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // A cached handle is not guaranteed to match a newly-created handle if other systems have run in the interim.
                Assert.AreNotEqual(sharedComponentTypeHandle2.m_Safety, sharedComponentTypeHandle1.m_Safety);
#endif
                // After updating the cached handle, these values (and the handles as a whole) should match
                sharedComponentTypeHandle1.Update(this);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.AreEqual(sharedComponentTypeHandle2.m_Safety, sharedComponentTypeHandle1.m_Safety);
#endif
            }
        }

        partial class DynamicSharedComponentTypeHandleUpdateSystem : SystemBase
        {
            private DynamicSharedComponentTypeHandle sharedComponentTypeHandle1;
            private Entity _entity;
            protected override void OnCreate()
            {
                _entity = EntityManager.CreateEntity(typeof(EcsTestSharedComp));
                sharedComponentTypeHandle1 = GetDynamicSharedComponentTypeHandle(typeof(EcsTestSharedComp));
            }

            protected override void OnUpdate()
            {
                var sharedComponentTypeHandle2 = GetDynamicSharedComponentTypeHandle(typeof(EcsTestSharedComp));

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // A cached handle is not guaranteed to match a newly-created handle if other systems have run in the interim.
                Assert.AreNotEqual(sharedComponentTypeHandle2.m_Safety, sharedComponentTypeHandle1.m_Safety);
#endif

                // After updating the cached handle, these values (and the handles as a whole) should match
                sharedComponentTypeHandle1.Update(this);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.AreEqual(sharedComponentTypeHandle2.m_Safety, sharedComponentTypeHandle1.m_Safety);
#endif
            }
        }

        partial class ComponentLookup_UpdateSystem : SystemBase
        {
            private ComponentLookup<EcsTestData> _lookup1;
            private Entity _entity;
            protected override void OnCreate()
            {
                _entity = EntityManager.CreateEntity(typeof(EcsTestData));
                _lookup1 = GetComponentLookup<EcsTestData>();
            }

            protected override void OnUpdate()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                //accessing a potentially stale ComponentLookup before Update() will throw an exception
                Assert.Throws<ArgumentException>(() => _lookup1.HasComponent(_entity));
#endif

                var lookup2 = GetComponentLookup<EcsTestData>();
                // A cached handle is not guaranteed to match a newly-created handle if other systems have run in the interim.
                Assert.AreNotEqual(lookup2.GlobalSystemVersion, _lookup1.GlobalSystemVersion);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.AreNotEqual(lookup2.m_Safety, _lookup1.m_Safety);
#endif
                // After updating the cached handle, these values (and the handles as a whole) should match
                _lookup1.Update(this);
                Assert.AreEqual(lookup2.GlobalSystemVersion, _lookup1.GlobalSystemVersion);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.AreEqual(lookup2.m_Safety, _lookup1.m_Safety);
#endif
                Assert.IsTrue(_lookup1.HasComponent(_entity));
            }
        }

        partial class DynamicComponentTypeHandleUpdateSystem : SystemBase
        {
            private DynamicComponentTypeHandle dynamicComponentTypeHandle1;
            private Entity _entity;
            protected override void OnCreate()
            {
                //using a buffer to ensure both safety handles are being updated
                _entity = EntityManager.CreateEntity(typeof(EcsIntElement));
                dynamicComponentTypeHandle1 = GetDynamicComponentTypeHandle(typeof(EcsIntElement));
            }

            protected override void OnUpdate()
            {
                var dynamicComponentTypeHandle2 = GetDynamicComponentTypeHandle(typeof(EcsIntElement));
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // A cached handle is not guaranteed to match a newly-created handle if other systems have run in the interim.
                Assert.AreNotEqual(dynamicComponentTypeHandle2.m_Safety0, dynamicComponentTypeHandle1.m_Safety0);
                Assert.AreNotEqual(dynamicComponentTypeHandle2.m_Safety1, dynamicComponentTypeHandle1.m_Safety1);
#endif
                Assert.AreNotEqual(dynamicComponentTypeHandle2.m_GlobalSystemVersion, dynamicComponentTypeHandle1.m_GlobalSystemVersion);
                // After updating the cached handle, these values (and the handles as a whole) should match
                dynamicComponentTypeHandle1.Update(this);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.AreEqual(dynamicComponentTypeHandle2.m_Safety0, dynamicComponentTypeHandle1.m_Safety0);
                Assert.AreEqual(dynamicComponentTypeHandle2.m_Safety1, dynamicComponentTypeHandle1.m_Safety1);
#endif
                Assert.AreEqual(dynamicComponentTypeHandle2.m_GlobalSystemVersion, dynamicComponentTypeHandle1.m_GlobalSystemVersion);
            }
        }

        partial class BufferLookupUpdateSystem : SystemBase
        {
            private BufferLookup<EcsIntElement> bufferLookup;
            private Entity _entity;
            protected override void OnCreate()
            {
                _entity = EntityManager.CreateEntity();
                var buffer = EntityManager.AddBuffer<EcsIntElement>(_entity);
                buffer.Add(new EcsIntElement
                {
                    Value = 42
                });
                bufferLookup = GetBufferLookup<EcsIntElement>();

            }

            protected override void OnUpdate()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                //accessing a potentially stale BFE before Update() will throw an exception
                //a direct access (as opposed to HasComponent) also ensures that the arrayInvalidationSafety is stale
                Assert.Throws<ArgumentException>(() =>
                {
                    var value = bufferLookup[_entity];
                });
#endif

                var bufferLookup2 = GetBufferLookup<EcsIntElement>();
                // A cached handle is not guaranteed to match a newly-created handle if other systems have run in the interim.
                Assert.AreNotEqual(bufferLookup2.GlobalSystemVersion, bufferLookup.GlobalSystemVersion);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.AreNotEqual(bufferLookup2.m_Safety0, bufferLookup.m_Safety0);
                Assert.AreNotEqual(bufferLookup2.m_ArrayInvalidationSafety, bufferLookup.m_ArrayInvalidationSafety);
#endif
                // After updating the cached handle, these values (and the handles as a whole) should match
                bufferLookup.Update(this);
                Assert.AreEqual(bufferLookup2.GlobalSystemVersion, bufferLookup.GlobalSystemVersion);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.AreEqual(bufferLookup2.m_Safety0, bufferLookup.m_Safety0);
                Assert.AreEqual(bufferLookup2.m_ArrayInvalidationSafety, bufferLookup.m_ArrayInvalidationSafety);
#endif
                var value = bufferLookup[_entity];
                Assert.AreEqual(42,value);
            }
        }

        partial class EntityTypeHandleUpdateSystem : SystemBase
        {
            private EntityTypeHandle entityTypeHandle1;
            private Entity _entity;
            protected override void OnCreate()
            {
                //using a buffer to ensure both safety handles are being updated
                _entity = EntityManager.CreateEntity();
                entityTypeHandle1 = GetEntityTypeHandle();
            }

            protected override void OnUpdate()
            {
                var entityTypeHandle2 = GetEntityTypeHandle();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // A cached handle is not guaranteed to match a newly-created handle if other systems have run in the interim.
                Assert.AreNotEqual(entityTypeHandle2.m_Safety, entityTypeHandle1.m_Safety);
#endif
                // After updating the cached handle, these values (and the handles as a whole) should match
                entityTypeHandle1.Update(this);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.AreEqual(entityTypeHandle2.m_Safety, entityTypeHandle1.m_Safety);
#endif
            }
        }

        [Test]
        public void ComponentLookup_SystemBase_UpdateWorks()
        {
            var dummy = World.CreateSystemManaged<ComponentLookup_UpdateSystem>();

            World.Update();
            World.Update();
            World.Update();
        }

        [Test]
        public void BufferLookup_SystemBase_UpdateWorks()
        {
            var dummy = World.CreateSystemManaged<BufferLookupUpdateSystem>();

            World.Update();
            World.Update();
            World.Update();
        }

        [Test]
        public void SharedComponentTypeHandle_SystemBase_UpdateWorks()
        {
            var dummy = World.CreateSystemManaged<ComponentLookup_UpdateSystem>();

            World.Update();
            World.Update();
            World.Update();
        }

        [Test]
        public void DynamicSharedComponent_SystemBase_UpdateWorks()
        {
            var dummy = World.CreateSystemManaged<DynamicSharedComponentTypeHandleUpdateSystem>();

            World.Update();
            World.Update();
            World.Update();
        }

        [Test]
        public void DynamicComponentTypeComponent_SystemBase_UpdateWorks()
        {
            var dummy = World.CreateSystemManaged<DynamicComponentTypeHandleUpdateSystem>();

            World.Update();
            World.Update();
            World.Update();
        }

        [Test]
        public void EntityTypeHandle_SystemBase_UpdateWorks()
        {
            var dummy = World.CreateSystemManaged<EntityTypeHandleUpdateSystem>();

            World.Update();
            World.Update();
            World.Update();
        }

        [DisableAutoCreation]
        class ParentWithDisableAutoCreation
        {
        }
        class ChildWithoutDisableAutoCreation : ParentWithDisableAutoCreation
        {
        }

        [Test]
        public void DisableAutoCreation_DoesNotInherit()
        {
            Type parentType = typeof(ParentWithDisableAutoCreation);
            Type childType = typeof(ChildWithoutDisableAutoCreation);
            // Parent has the DisableAutoCreation attribute
            var parentAttributes = parentType.GetCustomAttributes(false);
            Assert.AreEqual(1, parentAttributes.Length);
            Assert.AreEqual(typeof(DisableAutoCreationAttribute), parentAttributes[0].GetType());
            // Child does not inherit the attribute, even if inherit=true is passed
            var childAttributes = childType.GetCustomAttributes(true);
            Assert.AreEqual(0, childAttributes.Length);
        }

        [Test]
        public void ComponentLookup_TryGetComponent_Works()
        {
            var entityA = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entityA, new EcsTestData
            {
                value = 0
            });
            var entityB = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entityB, new EcsTestData
            {
                value = 1
            });
            var entityC = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entityC, new EcsTestData
            {
                value = 2
            });

            var array = m_Manager.GetComponentLookup<EcsTestData>();

            Assert.IsTrue(array.TryGetComponent(entityA, out var componentDataA));
            Assert.IsTrue(array.TryGetComponent(entityB, out var componentDataB));
            Assert.IsTrue(array.TryGetComponent(entityC, out var componentDataC));

            Assert.AreEqual(0, componentDataA.value);
            Assert.AreEqual(1, componentDataB.value);
            Assert.AreEqual(2, componentDataC.value);

        }

        struct ComponentLookupContainerJob : IJob
        {
            public ComponentLookup<EcsTestContainerData> array;

            public void Execute() { }
        }

        struct ComponentLookupJob : IJob
        {
            public ComponentLookup<EcsTestData> array;

            public void Execute() { }
        }

        [Test]
        public void ComponentLookup_ComponentWithContainer_Works()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestContainerData));
            var component = new EcsTestContainerData();
            component.Create();
            m_Manager.SetComponentData(entity, component);
            var array = m_Manager.GetComponentLookup<EcsTestContainerData>();
            Assert.AreEqual(array[entity], component);
            Assert.AreEqual(array[entity].data[1], component.data[1]);
            component.Destroy();
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on jobs debugger")]
        public void ComponentLookup_ComponentWithContainerInJob_Throws()
        {
            var job = new ComponentLookupContainerJob();
            job.array = m_Manager.GetComponentLookup<EcsTestContainerData>();
            var e = Assert.Throws<InvalidOperationException>(() => job.Schedule());
            Assert.IsTrue(e.Message.Contains("Nested native containers are illegal in jobs"));
        }

        [Test]
        public void ComponentLookup_InJob_Works()
        {
            var job = new ComponentLookupJob();
            job.array = m_Manager.GetComponentLookup<EcsTestData>();
            Assert.DoesNotThrow(() => job.Schedule().Complete());
        }

        [Test]
        public void ComponentLookup_TryGetComponent_HasTagComponent()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestTag));
            var array = m_Manager.GetComponentLookup<EcsTestTag>();
            Assert.IsTrue(array.TryGetComponent(entity,out var tagComponent));
            Assert.AreEqual(default(EcsTestTag),tagComponent);
        }

        [Test]
        public void ComponentLookup_GetComponent_Works()
        {
            var entityA = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entityA, new EcsTestData
            {
                value = 0
            });
            var entityB = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entityB, new EcsTestData
            {
                value = 1
            });
            var entityC = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entityC, new EcsTestData
            {
                value = 2
            });

            var array = m_Manager.GetComponentLookup<EcsTestData>();

            Assert.AreEqual(0, array[entityA].value);
            Assert.AreEqual(1, array[entityB].value);
            Assert.AreEqual(2, array[entityC].value);
        }

        [Test]
        public void ComponentLookup_GetComponent_ReturnsDefault()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestTag));
            ComponentLookup<EcsTestTag> array = default;
            EcsTestTag component;
            Assert.DoesNotThrow(() =>
            {
                array = m_Manager.GetComponentLookup<EcsTestTag>();
                 component = array[entity];
            });
            Assert.AreEqual(default(EcsTestTag),component);
        }

        [Test]
        public void ComponentLookup_SetComponent_NoOp()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestTag));
            ComponentLookup<EcsTestTag> array = default;
            EcsTestTag component;
            Assert.DoesNotThrow(() =>
            {
                array = m_Manager.GetComponentLookup<EcsTestTag>();
                array[entity] = component;
            });
            Assert.AreEqual(default(EcsTestTag),array[entity]);
        }

        [Test]
        public void ComponentLookup_TryGetComponent_NoComponent()
        {
            var entity = m_Manager.CreateEntity();
            var array = m_Manager.GetComponentLookup<EcsTestData>();
            Assert.IsFalse(array.TryGetComponent(entity, out var componentData));
            Assert.AreEqual(componentData, default(EcsTestData));
        }

        [Test]
        public void ComponentLookup_TryGetComponent_FullyUpdatesLookupCache()
        {
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetypeX = m_Manager.CreateArchetype(typeof(EcsTestTag));

            var entityA = m_Manager.CreateEntity(archetypeA);
            m_Manager.SetComponentData(entityA, new EcsTestData(17));
            var entityX = m_Manager.CreateEntity(archetypeX);

            var lookup = m_Manager.GetComponentLookup<EcsTestData>();

            // For a while, TryGetComponent left the lookup.LookupCache in an inconsistent state. We can't inspect the
            // (private) LookupCache directly, so instead we'll test the observable effect of an stale cache: a particular
            // sequence of calls that results in an attempted out-of-bounds memory write to chunk data.

            // the get[] accessor fully updates the LookupCache, and returns correct data.
            EcsTestData data = lookup[entityA];
            Assert.AreEqual(17, data.value);
            // A failed TryGetComponent() will succeed, only (before the fix) only updates the cache's IndexInArchetype,
            // setting it to -1; the other cache fields are untouched.
            Assert.IsFalse(lookup.TryGetComponent(entityX, out data));
            // The set[] accessor will *NOT* update the LookupCache, because cache.Archetype still matches.
            // Before the fix, this will pass IndexInArchetype=-1 to SetChangeVersion(), which asserts / stomps unrelated memory.
            Assert.DoesNotThrow(() => { lookup[entityA] = new EcsTestData(23); });
        }

        struct BufferLookupContainerJob : IJob
        {
            public BufferLookup<EcsTestContainerElement> array;

            public void Execute() { }
        }

        struct BufferLookupJob : IJob
        {
            public BufferLookup<EcsIntElement> array;

            public void Execute() { }
        }

        [Test]
        public void BufferLookup_ElementWithContainer_Works()
        {
            var entity = m_Manager.CreateEntity();
            var element = new EcsTestContainerElement();
            element.Create();
            m_Manager.AddBuffer<EcsTestContainerElement>(entity);
            m_Manager.GetBuffer<EcsTestContainerElement>(entity).Add(element);

            var array = m_Manager.GetBufferLookup<EcsTestContainerElement>();
            Assert.IsTrue(array.TryGetBuffer(entity, out var bufferData));

            Assert.AreEqual(bufferData[0].data[1], element.data[1]);

            element.Destroy();
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on jobs debugger")]
        public void BufferLookup_ElementWithContainerInJob_Throws()
        {
            var job = new BufferLookupContainerJob();
            var entity = m_Manager.CreateEntity();
            var element = new EcsTestContainerElement();
            m_Manager.AddBuffer<EcsTestContainerElement>(entity);
            m_Manager.GetBuffer<EcsTestContainerElement>(entity).Add(element);

            job.array = m_Manager.GetBufferLookup<EcsTestContainerElement>();
            var e = Assert.Throws<InvalidOperationException>(() => job.Schedule());
            Assert.IsTrue(e.Message.Contains("Nested native containers are illegal in jobs"));
        }

        [Test]
        public void BufferLookup_InJob_Works()
        {
            var job = new BufferLookupJob();
            var entity = m_Manager.CreateEntity();
            var element = new EcsIntElement();
            m_Manager.AddBuffer<EcsIntElement>(entity);
            m_Manager.GetBuffer<EcsIntElement>(entity).Add(element);

            job.array = m_Manager.GetBufferLookup<EcsIntElement>();
            Assert.DoesNotThrow(() => job.Schedule().Complete());
        }

        [Test]
        public void BufferLookup_HasBuffer_Works()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddBuffer<EcsIntElement>(entity);
            m_Manager.GetBuffer<EcsIntElement>(entity).AddRange(new NativeArray<EcsIntElement>(new EcsIntElement[] { 0, 1, 2 }, Allocator.Temp));

            var array = m_Manager.GetBufferLookup<EcsIntElement>();

            Assert.IsTrue(array.HasBuffer(entity));
        }

        [Test]
        public void BufferLookup_TryGetBuffer_Works()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddBuffer<EcsIntElement>(entity);
            m_Manager.GetBuffer<EcsIntElement>(entity).AddRange(new NativeArray<EcsIntElement>(new EcsIntElement[] { 0, 1, 2 }, Allocator.Temp));

            var array = m_Manager.GetBufferLookup<EcsIntElement>();

            Assert.IsTrue(array.TryGetBuffer(entity, out var bufferData));
            CollectionAssert.AreEqual(new EcsIntElement[] { 0, 1, 2 }, bufferData.ToNativeArray(Allocator.Temp).ToArray());
        }

        [Test]
        public void BufferLookup_TryGetBuffer_NoComponent()
        {
            var entity = m_Manager.CreateEntity();
            var array = m_Manager.GetBufferLookup<EcsIntElement>();
            Assert.IsFalse(array.TryGetBuffer(entity, out var bufferData));
            //I can't do an equivalence check to default since equals appears to not be implemented
            Assert.IsFalse(bufferData.IsCreated);
        }

        [Test]
        public void BufferLookup_TryGetBuffer_FullyUpdatesLookupCache()
        {
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsIntElement));
            var archetypeX = m_Manager.CreateArchetype(typeof(EcsTestTag));

            var entityA = m_Manager.CreateEntity(archetypeA);
            var buffer = m_Manager.GetBuffer<EcsIntElement>(entityA);
            buffer.Add(new EcsIntElement{Value = 17});
            var entityX = m_Manager.CreateEntity(archetypeX);

            var lookup = m_Manager.GetBufferLookup<EcsIntElement>();

            // For a while, TryGetComponent left the lookup.LookupCache in an inconsistent state. We can't inspect the
            // (private) LookupCache directly, so instead we'll test the observable effect of an stale cache: a particular
            // sequence of calls that results in an attempted out-of-bounds memory write to chunk data.

            // the get[] accessor fully updates the LookupCache, and returns correct data.
            buffer = lookup[entityA];
            Assert.AreEqual(1, buffer.Length);
            Assert.AreEqual(17, buffer[0].Value);
            // A failed TryGetBuffer() will succeed, only (before the fix) only updates the cache's IndexInArchetype,
            // setting it to -1; the other cache fields are untouched.
            Assert.IsFalse(lookup.TryGetBuffer(entityX, out buffer));
            // The get[] accessor for a read/write BFE will *NOT* update the LookupCache, because cache.Archetype still matches.
            // Before the fix, this will pass IndexInArchetype=-1 to SetChangeVersion(), which asserts / stomps unrelated memory.
            Assert.DoesNotThrow(() => { buffer = lookup[entityA]; });
        }

#if UNITY_ENTITIES_RUNTIME_TOOLING
        partial class SystemThatTakesTime : SystemBase
        {
            private int updateCount = 0;
            protected override void OnUpdate()
            {
                var start = Stopwatch.StartNew();

                updateCount++;
                long howlongtowait = updateCount * 2;
                while (start.ElapsedMilliseconds < howlongtowait)
                    ;
            }
        }

        [Test]
        public void RuntimeToolingSystemTiming()
        {
            var s1 = World.CreateSystem<SystemThatTakesTime>();

            s1.Update();
            Assert.Greater(s1.SystemElapsedTicks, 0);
            Assert.Greater(s1.SystemStartTicks, 0);
            Assert.Greater(s1.SystemEndTicks, s1.SystemStartTicks);
            Assert.GreaterOrEqual(s1.SystemElapsedMilliseconds, 2);

            s1.Update();
            Assert.GreaterOrEqual(s1.SystemElapsedMilliseconds, 4);

            s1.Enabled = false;
            // check that the time still represents the last time it updated, even if
            // we disabled it in the meantime
            Assert.Greater(s1.SystemElapsedTicks, 0);
            Assert.Greater(s1.SystemStartTicks, 0);
            Assert.Greater(s1.SystemEndTicks, s1.SystemStartTicks);
            Assert.GreaterOrEqual(s1.SystemElapsedMilliseconds, 4);

            s1.Update();
            Assert.AreEqual(0, s1.SystemElapsedTicks);
        }
#endif

        public partial class NonPreservedTestSystem : SystemBase
        {
            public string m_Test;

            public NonPreservedTestSystem() { m_Test = ""; }

            //This is essentially what removing [Preserve] would accomplish with max code stripping.
            //public NonPreservedTestSystem(string inputParam) { m_Test = inputParam; }
            protected override void OnUpdate() { }
        }

        [Preserve]
        public partial class PreservedTestSystem : SystemBase
        {
            public string m_Test;

            public PreservedTestSystem() { m_Test = ""; }
            public PreservedTestSystem(string inputParam) { m_Test = inputParam; }
            protected override void OnUpdate() { }
        }

        partial struct UnmanagedSystemWithSyncPointAfterSchedule : ISystem
        {
            struct MyJob : IJobChunk
            {
                public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
                {
                }
            }

            private EntityQuery m_Query;

            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.CreateEntity(typeof(EcsTestData));
                m_Query = state.GetEntityQuery(typeof(EcsTestData));
            }

            public void OnUpdate(ref SystemState state)
            {
                state.GetComponentTypeHandle<EcsTestData>();
                state.Dependency = new MyJob().ScheduleParallel(m_Query, state.Dependency);
                state.EntityManager.CreateEntity();
            }
        }

        [Test]
        public void ISystem_CanHaveSyncPointAfterSchedule()
        {
            var group = World.CreateSystemManaged<TestGroup>();
            var sys = World.CreateSystem<UnmanagedSystemWithSyncPointAfterSchedule>();
            group.AddSystemToUpdateList(sys);
            Assert.DoesNotThrow(() => group.Update());
        }

        partial class UpdateCountSystem : SystemBase
        {
            public int UpdateCount = 0;
            protected override void OnUpdate()
            {
                Entities.ForEach((ref EcsTestData data) => { }).Run();
                ++UpdateCount;
            }
        }

        partial class WithoutRequireMatchingQueriesForUpdate : UpdateCountSystem
        {
        }
        [RequireMatchingQueriesForUpdate]
        partial class WithRequireMatchingQueriesForUpdate : UpdateCountSystem
        {
        }
        partial class DerivedSystemWithRequireMatchingOnBaseSystem : WithRequireMatchingQueriesForUpdate
        {
        }

        partial class WithRequireForUpdate : UpdateCountSystem
        {
            protected override void OnCreate()
            {
                RequireForUpdate<EcsTestData>();
            }
        }
        partial class WithRequireQueryForUpdate : UpdateCountSystem
        {
            protected override void OnCreate()
            {
                RequireForUpdate(GetEntityQuery(typeof(EcsTestData)));
            }
        }
        partial class WithRequireEcsTestData2ForUpdate : UpdateCountSystem
        {
            protected override void OnCreate()
            {
                RequireForUpdate<EcsTestData2>();
            }
        }

        partial class WithRequireEitherQueryForUpdate : UpdateCountSystem
        {
            protected override void OnCreate()
            {
                var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>()
                    .AddAdditionalQuery().WithAll<EcsTestData2>();
                RequireForUpdate(GetEntityQuery(builder));
            }
        }

        partial class WithRequireAnyForUpdateParams : UpdateCountSystem
        {
            protected override void OnCreate()
            {
                var q1 = GetEntityQuery(typeof(EcsTestData));
                var q2 = GetEntityQuery(typeof(EcsTestData2));
                RequireAnyForUpdate(q1, q2);
            }
        }

        partial class WithRequireAnyForUpdateNativeArray : UpdateCountSystem
        {
            protected override void OnCreate()
            {
                var q1 = GetEntityQuery(typeof(EcsTestData));
                var q2 = GetEntityQuery(typeof(EcsTestData2));
                var arr = new NativeArray<EntityQuery>(2, Allocator.Temp);
                arr[0] = q1;
                arr[1] = q2;
                RequireAnyForUpdate(arr);
            }
        }

        unsafe partial class WithRequireAnyForUpdateSystemStateParams : UpdateCountSystem
        {
            protected override void OnCreate()
            {
                var q1 = GetEntityQuery(typeof(EcsTestData));
                var q2 = GetEntityQuery(typeof(EcsTestData2));
                CheckedState()->RequireAnyForUpdate(q1, q2);
            }
        }

        partial class WithRequireAnyAndRequiredTagForUpdate : UpdateCountSystem
        {
            protected override void OnCreate()
            {
                var q1 = GetEntityQuery(typeof(EcsTestData));
                var q2 = GetEntityQuery(typeof(EcsTestData2));

                RequireAnyForUpdate(q1, q2);
                RequireForUpdate<EcsTestTag>();
            }
        }

        partial class WithRequireEcsTestDataEnableableForUpdate : UpdateCountSystem
        {
            internal EntityQuery RequiredQuery;

            protected override void OnCreate()
            {
                RequiredQuery = GetEntityQuery(typeof(EcsTestDataEnableable));
                RequireForUpdate(RequiredQuery);
            }
        }

        [Test]
        public void SystemBase_RequireMatchingQueriesForUpdate_Works()
        {
            var sys1 = World.CreateSystemManaged<WithoutRequireMatchingQueriesForUpdate>();
            sys1.Update();
            Assert.AreEqual(1, sys1.UpdateCount);

            var sys2 = World.CreateSystemManaged<WithRequireMatchingQueriesForUpdate>();
            sys2.Update();
            Assert.AreEqual(0, sys2.UpdateCount);

            m_Manager.CreateEntity(typeof(EcsTestData));

            sys2.Update();
            Assert.AreEqual(1, sys2.UpdateCount);
        }

        [Test]
        public void SystemBase_DerivedSystemWithRequireMatchingOnBaseSystem_RequiresMatching()
        {
            // System should respect attribute when it exists on the base class
            var sys = World.CreateSystemManaged<DerivedSystemWithRequireMatchingOnBaseSystem>();
            sys.Update();
            Assert.AreEqual(0, sys.UpdateCount);

            m_Manager.CreateEntity(typeof(EcsTestData));

            sys.Update();
            Assert.AreEqual(1, sys.UpdateCount);
        }

        [Test]
        public void SystemBase_RequireForUpdate_Works()
        {
            var sys1 = World.CreateSystemManaged<WithRequireForUpdate>();
            var sys2 = World.CreateSystemManaged<WithRequireQueryForUpdate>();

            sys1.Update();
            sys2.Update();
            Assert.AreEqual(0, sys1.UpdateCount);
            Assert.AreEqual(0, sys2.UpdateCount);

            m_Manager.CreateEntity(typeof(EcsTestData));

            sys1.Update();
            sys2.Update();
            Assert.AreEqual(1, sys1.UpdateCount);
            Assert.AreEqual(1, sys2.UpdateCount);
        }

        [Test]
        public void SystemBase_RequireForUpdate_OnlyRequiredQuery_Works()
        {
            var sys = World.CreateSystemManaged<WithRequireEcsTestData2ForUpdate>();

            sys.Update();
            Assert.AreEqual(0, sys.UpdateCount, "Doesn't update with no matching queries.");

            var data1 = m_Manager.CreateEntity(typeof(EcsTestData));

            sys.Update();
            Assert.AreEqual(0, sys.UpdateCount, "Doesn't update without required component, even if ForEach matches.");

            var data2 = m_Manager.CreateEntity(typeof(EcsTestData2));

            sys.Update();
            Assert.AreEqual(1, sys.UpdateCount, "Updates if required component exists.");

            m_Manager.DestroyEntity(data1);

            sys.Update();
            Assert.AreEqual(2, sys.UpdateCount, "Still updates if required component exists, even if ForEach doesn't match.");

            m_Manager.DestroyEntity(data2);

            sys.Update();
            Assert.AreEqual(2, sys.UpdateCount, "Stops updating when required component is removed.");
        }

        [Test]
        public void SystemBase_RequireForUpdate_MultipartQuery_UpdatesIfAnyMatches()
        {
            var sys = World.CreateSystemManaged<WithRequireEitherQueryForUpdate>();

            sys.Update();
            Assert.AreEqual(0, sys.UpdateCount, "Doesn't update with no matching queries");

            var data1 = m_Manager.CreateEntity(typeof(EcsTestData));

            sys.Update();
            Assert.AreEqual(1, sys.UpdateCount, "Updates if only first component exists");

            var data2 = m_Manager.CreateEntity(typeof(EcsTestData2));

            sys.Update();
            Assert.AreEqual(2, sys.UpdateCount, "Updates if both components exist");

            m_Manager.DestroyEntity(data1);

            sys.Update();
            Assert.AreEqual(3, sys.UpdateCount, "Updates if only second component exists");

            m_Manager.DestroyEntity(data2);

            sys.Update();
            Assert.AreEqual(3, sys.UpdateCount, "Stops updating when both components are removed");
        }

        public enum RequireMethod
        {
            ParamsArray,
            NativeArray,
            SystemStateParamsArray
        }

        [Test]
        public void SystemBase_RequireAnyForUpdate_UpdatesIfAnyMatches([Values]RequireMethod method)
        {
            UpdateCountSystem sys;
            switch (method)
            {
                case RequireMethod.ParamsArray:
                    sys = World.CreateSystemManaged<WithRequireAnyForUpdateParams>();
                    break;
                case RequireMethod.NativeArray:
                    sys = World.CreateSystemManaged<WithRequireAnyForUpdateNativeArray>();
                    break;
                case RequireMethod.SystemStateParamsArray:
                    sys = World.CreateSystemManaged<WithRequireAnyForUpdateSystemStateParams>();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(method), method, null);
            }

            sys.Update();
            Assert.AreEqual(0, sys.UpdateCount, "Doesn't update with no matching queries");

            var data1 = m_Manager.CreateEntity(typeof(EcsTestData));

            sys.Update();
            Assert.AreEqual(1, sys.UpdateCount, "Updates if only first component exists");

            var data2 = m_Manager.CreateEntity(typeof(EcsTestData2));

            sys.Update();
            Assert.AreEqual(2, sys.UpdateCount, "Updates if both components exist");

            m_Manager.DestroyEntity(data1);

            sys.Update();
            Assert.AreEqual(3, sys.UpdateCount, "Updates if only second component exists");

            m_Manager.DestroyEntity(data2);

            sys.Update();
            Assert.AreEqual(3, sys.UpdateCount, "Stops updating when both components are removed");
        }

        [Test]
        public void SystemBase_RequireAnyForUpdate_AND_RequireTag()
        {
            var sys = World.CreateSystemManaged<WithRequireAnyAndRequiredTagForUpdate>();

            sys.Update();
            Assert.AreEqual(0, sys.UpdateCount, "Doesn't update with no matching queries");

            var data1 = m_Manager.CreateEntity(typeof(EcsTestData));

            sys.Update();
            Assert.AreEqual(0, sys.UpdateCount, "Does update if optional component exists but not required component");

            var tag = m_Manager.CreateEntity(typeof(EcsTestTag));

            sys.Update();
            Assert.AreEqual(1, sys.UpdateCount, "Updates if required and one optional components exist");

            var data2 = m_Manager.CreateEntity(typeof(EcsTestData2));

            sys.Update();
            Assert.AreEqual(2, sys.UpdateCount, "Updates if required and both optional components exist");

            m_Manager.DestroyEntity(data1);

            sys.Update();
            Assert.AreEqual(3, sys.UpdateCount, "Updates if required and second optional components exist");

            m_Manager.DestroyEntity(tag);

            sys.Update();
            Assert.AreEqual(3, sys.UpdateCount, "Stops updating if both required-optional components are missing, even though required component exists.");
        }

        [Test]
        public void SystemBase_RequireForUpdate_IgnoresFilter()
        {
            var sys = World.CreateSystemManaged<WithRequireEcsTestDataEnableableForUpdate>();

            sys.Update();
            Assert.AreEqual(0, sys.UpdateCount, "Doesn't update without matching query.");

            var entity = m_Manager.CreateEntity(typeof(EcsTestDataEnableable));

            sys.Update();
            Assert.AreEqual(1, sys.UpdateCount, "Updates with required matching query.");

            sys.RequiredQuery.SetChangedVersionFilter(typeof(EcsTestDataEnableable));

            sys.Update();
            Assert.AreEqual(2, sys.UpdateCount, "Required query ignores filter, still updates.");

            //m_Manager.SetEnabled(entity, false); // This is a structural change that adds the Disabled tag
            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entity, false);

            sys.Update();
            Assert.AreEqual(3, sys.UpdateCount, "Required query ignores disabled flags, still updates.");
        }

        struct UpdateCountData : IComponentData
        {
            public int UpdateCount;
            public EntityQuery RequiredQuery;
        }

        [RequireMatchingQueriesForUpdate]
        partial struct WithRequireMatchingQueriesForUpdateUnmanaged : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponent<UpdateCountData>(state.SystemHandle);
            }
            public void OnUpdate(ref SystemState state)
            {
                foreach (var data in Query<RefRW<EcsTestData>>()) { }
                state.EntityManager.GetComponentDataRW<UpdateCountData>(state.SystemHandle).ValueRW.UpdateCount++;
            }
        }

        partial struct WithoutRequireMatchingQueriesForUpdateUnmanaged : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponent<UpdateCountData>(state.SystemHandle);
            }
            public void OnUpdate(ref SystemState state)
            {
                foreach (var data in Query<RefRW<EcsTestData>>()) { }
                state.EntityManager.GetComponentDataRW<UpdateCountData>(state.SystemHandle).ValueRW.UpdateCount++;
            }
        }

        partial struct WithRequireForUpdateUnmanaged : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate<EcsTestData>();
                state.EntityManager.AddComponent<UpdateCountData>(state.SystemHandle);
            }
            public void OnUpdate(ref SystemState state)
            {
                foreach (var data in Query<RefRW<EcsTestData>>()) { }
                state.EntityManager.GetComponentDataRW<UpdateCountData>(state.SystemHandle).ValueRW.UpdateCount++;
            }

        }
        partial struct WithRequireQueryForUpdateUnmanaged : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate(state.GetEntityQuery(typeof(EcsTestData)));
                state.EntityManager.AddComponent<UpdateCountData>(state.SystemHandle);
            }
            public void OnUpdate(ref SystemState state)
            {
                foreach (var data in Query<RefRW<EcsTestData>>()) { }
                state.EntityManager.GetComponentDataRW<UpdateCountData>(state.SystemHandle).ValueRW.UpdateCount++;
            }

        }
        partial struct WithRequireEcsTestData2ForUpdateUnmanaged : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate<EcsTestData2>();
                state.EntityManager.AddComponent<UpdateCountData>(state.SystemHandle);
            }
            public void OnUpdate(ref SystemState state)
            {
                foreach (var data in Query<RefRW<EcsTestData>>()) { }
                state.EntityManager.GetComponentDataRW<UpdateCountData>(state.SystemHandle).ValueRW.UpdateCount++;
            }

        }
        partial struct WithRequireEcsTestDataEnableableForUpdateUnmanaged : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                var RequiredQuery = state.GetEntityQuery(typeof(EcsTestDataEnableable));
                state.EntityManager.AddComponentData(state.SystemHandle, new UpdateCountData
                {
                    RequiredQuery = RequiredQuery
                });
                state.RequireForUpdate(RequiredQuery);
            }
            public void OnUpdate(ref SystemState state)
            {
                foreach (var data in Query<RefRW<EcsTestData>>()) { }
                state.EntityManager.GetComponentDataRW<UpdateCountData>(state.SystemHandle).ValueRW.UpdateCount++;
            }
        }

        [Test]
        public void ISystem_RequireMatchingQueriesForUpdate_Works()
        {
            var group = World.CreateSystemManaged<TestGroup>();
            var sys1 = World.CreateSystem<WithoutRequireMatchingQueriesForUpdateUnmanaged>();
            var sys2 = World.CreateSystem<WithRequireMatchingQueriesForUpdateUnmanaged>();
            group.AddSystemToUpdateList(sys1);
            group.AddSystemToUpdateList(sys2);

            group.Update();
            Assert.AreEqual(1, World.EntityManager.GetComponentData<UpdateCountData>(sys1).UpdateCount);
            Assert.AreEqual(0, World.EntityManager.GetComponentData<UpdateCountData>(sys2).UpdateCount);

            m_Manager.CreateEntity(typeof(EcsTestData));

            group.Update();
            Assert.AreEqual(1, World.EntityManager.GetComponentData<UpdateCountData>(sys2).UpdateCount);
        }

        [Test]
        public void ISystem_RequireForUpdate_Works()
        {
            var group = World.CreateSystemManaged<TestGroup>();
            var sys1 = World.CreateSystem<WithRequireForUpdateUnmanaged>();
            var sys2 = World.CreateSystem<WithRequireQueryForUpdateUnmanaged>();
            group.AddSystemToUpdateList(sys1);
            group.AddSystemToUpdateList(sys2);

            group.Update();
            Assert.AreEqual(0, World.EntityManager.GetComponentData<UpdateCountData>(sys1).UpdateCount);
            Assert.AreEqual(0, World.EntityManager.GetComponentData<UpdateCountData>(sys2).UpdateCount);

            m_Manager.CreateEntity(typeof(EcsTestData));

            group.Update();
            Assert.AreEqual(1, World.EntityManager.GetComponentData<UpdateCountData>(sys1).UpdateCount);
            Assert.AreEqual(1, World.EntityManager.GetComponentData<UpdateCountData>(sys2).UpdateCount);
        }

        [Test]
        public void ISystem_RequireForUpdate_OnlyRequiredQuery_Works()
        {
            var group = World.CreateSystemManaged<TestGroup>();
            var sys = World.CreateSystem<WithRequireEcsTestData2ForUpdateUnmanaged>();
            group.AddSystemToUpdateList(sys);

            group.Update();
            Assert.AreEqual(0, World.EntityManager.GetComponentData<UpdateCountData>(sys).UpdateCount, "Doesn't update with no matching queries.");

            var data1 = m_Manager.CreateEntity(typeof(EcsTestData));

            group.Update();
            Assert.AreEqual(0, World.EntityManager.GetComponentData<UpdateCountData>(sys).UpdateCount, "Doesn't update without required component, even if ForEach matches.");

            var data2 = m_Manager.CreateEntity(typeof(EcsTestData2));

            group.Update();
            Assert.AreEqual(1, World.EntityManager.GetComponentData<UpdateCountData>(sys).UpdateCount, "Updates if required component exists.");

            m_Manager.DestroyEntity(data1);

            group.Update();
            Assert.AreEqual(2, World.EntityManager.GetComponentData<UpdateCountData>(sys).UpdateCount, "Still updates if required component exists, even if ForEach doesn't match.");

            m_Manager.DestroyEntity(data2);

            group.Update();
            Assert.AreEqual(2, World.EntityManager.GetComponentData<UpdateCountData>(sys).UpdateCount, "Stops updating when required component is removed.");
        }

        [Test]
        public void ISystem_RequireForUpdate_IgnoresFilter()
        {
            var group = World.CreateSystemManaged<TestGroup>();
            var sys = World.CreateSystem<WithRequireEcsTestDataEnableableForUpdateUnmanaged>();

            group.AddSystemToUpdateList(sys);

            group.Update();
            Assert.AreEqual(0, World.EntityManager.GetComponentData<UpdateCountData>(sys).UpdateCount, "Doesn't update without matching query.");

            var entity = m_Manager.CreateEntity(typeof(EcsTestDataEnableable));

            group.Update();
            Assert.AreEqual(1, World.EntityManager.GetComponentData<UpdateCountData>(sys).UpdateCount, "Updates with required matching query.");

            World.EntityManager.GetComponentDataRW<UpdateCountData>(sys).ValueRW.
                RequiredQuery.SetChangedVersionFilter(typeof(EcsTestDataEnableable));

            group.Update();
            Assert.AreEqual(2, World.EntityManager.GetComponentData<UpdateCountData>(sys).UpdateCount, "Required query ignores filter, still updates.");

            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entity, false);

            group.Update();
            Assert.AreEqual(3, World.EntityManager.GetComponentData<UpdateCountData>(sys).UpdateCount, "Required query ignores disabled flags, still updates.");
        }

        [WorldSystemFilter((WorldSystemFilterFlags)(1 << 20))]   // unused filter flag
        partial struct WorldSystemFilterISystem : ISystem
        {
        }

        [WorldSystemFilter((WorldSystemFilterFlags)(1 << 20))]   // unused filter flag
        partial class WorldSystemFilterSystem : SystemBase
        {
            protected override void OnUpdate() { }
        }

        [Test]
        public void ISystem_WorldSystemFiltering_Exists()
        {
            Assert.AreEqual((WorldSystemFilterFlags)(1 << 20), TypeManager.GetSystemFilterFlags(typeof(WorldSystemFilterISystem)));
        }

        [Test]
        public void WorldUpdateAllocatorResetSystem_Exists()
        {
            Assert.AreEqual((WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.Editor), TypeManager.GetSystemFilterFlags(typeof(WorldUpdateAllocatorResetSystem)));
        }


        [Test]
        public void SystemBase_WorldSystemFiltering_Exists()
        {
            Assert.AreEqual((WorldSystemFilterFlags)(1 << 20), TypeManager.GetSystemFilterFlags(typeof(WorldSystemFilterSystem)));
        }

        [BurstCompile]
        partial struct BurstCompiledUnmanagedSystem : ISystem
        {
            [BurstCompile]
            struct MyJob : IJobChunk
            {
                public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
                {
                }
            }

            private EntityQuery m_Query;
            private EntityQuery m_QueryWithAspect;

            [BurstCompile]
            public void OnCreate(ref SystemState state)
            {
                var myTypes = new NativeArray<ComponentType>(1, Allocator.Temp);
                myTypes[0] = ComponentType.ReadWrite<EcsTestData>();
                var arch = state.EntityManager.CreateArchetype(myTypes);

                state.EntityManager.CreateEntity(arch);
                m_Query = state.GetEntityQuery(myTypes);

                myTypes.Dispose();

                m_QueryWithAspect = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().WithAspect<MyAspect>()
                    .Build(ref state);
            }

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                state.GetComponentTypeHandle<EcsTestData>();
                state.Dependency = new MyJob().ScheduleParallel(m_Query, state.Dependency);
                state.Dependency = new MyJob().ScheduleParallel(m_QueryWithAspect, state.Dependency);
                state.EntityManager.CreateEntity();
            }
        }

        struct UnmanagedSystemHandleData : IComponentData
        {
            public SystemHandle other;
        }

        unsafe partial struct UnmanagedSystemWithRefA : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponentData(state.SystemHandle, new UnmanagedSystemHandleData
                {
                    other = state.WorldUnmanaged.GetExistingUnmanagedSystem<UnmanagedSystemWithRefB>()
                });
            }

            }

        unsafe partial struct UnmanagedSystemWithRefB : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponentData(state.SystemHandle, new UnmanagedSystemHandleData
                {
                    other = state.WorldUnmanaged.GetExistingUnmanagedSystem<UnmanagedSystemWithRefA>()
                });
            }

            }

        [Test]
        public void UnmanagedSystemRefsBatchCreateWorks()
        {
            var tmp = new NativeList<SystemTypeIndex>(2, Allocator.Temp);
            tmp.Add(TypeManager.GetSystemTypeIndex<UnmanagedSystemWithRefA>());
            tmp.Add(TypeManager.GetSystemTypeIndex<UnmanagedSystemWithRefB>());

            World.Unmanaged.GetOrCreateUnmanagedSystems(tmp);

            var sysA = World.Unmanaged.GetExistingUnmanagedSystem<UnmanagedSystemWithRefA>();
            var sysB = World.Unmanaged.GetExistingUnmanagedSystem<UnmanagedSystemWithRefB>();

            Assert.IsTrue(World.Unmanaged.IsSystemValid(sysA));
            Assert.IsTrue(World.Unmanaged.IsSystemValid(sysB));

            Assert.IsTrue(World.EntityManager.GetComponentData<UnmanagedSystemHandleData>(sysA).other == sysB);
            Assert.IsTrue(World.EntityManager.GetComponentData<UnmanagedSystemHandleData>(sysB).other == sysA);
        }

        partial class BufferLookup_CacheRegressionTest_System : SystemBase
        {
            Entity m_EntityA;
            Entity m_EntityB;
            BufferLookup<EcsIntElement> m_BufferLookup;

            protected override void OnCreate()
            {
                m_EntityA = EntityManager.CreateEntity(typeof(EcsIntElement));
                m_EntityB = EntityManager.CreateEntity(typeof(EcsIntElement));

                m_BufferLookup = GetBufferLookup<EcsIntElement>();
            }

            protected override void OnUpdate()
            {
                // This is a regression test for a subtle cache invalidation in component lookup.

                m_BufferLookup.Update(this);

                EntityManager.GetBuffer<EcsIntElement>(m_EntityA).Add(123);
                EntityManager.GetBuffer<EcsIntElement>(m_EntityB).Add(234);

                // This first test of the contents of the buffers looks pointless, but it is
                // actually quite relevant because using the lookup initializes the cache it contains.

                {
                    var arrayA = m_BufferLookup[m_EntityA].Reinterpret<int>().AsNativeArray().ToArray();
                    CollectionAssert.AreEqual(new[] { 123 }, arrayA);

                    var arrayB = m_BufferLookup[m_EntityB].Reinterpret<int>().AsNativeArray().ToArray();
                    CollectionAssert.AreEqual(new[] { 234 }, arrayB);
                }

                // The first two AddComponent below move the entities to a new chunk.
                // Since the previous chunk is left empty, it gets deallocated.

                EntityManager.AddComponent<EcsTestTag>(m_EntityA);
                EntityManager.AddComponent<EcsTestTag>(m_EntityB);

                // The next two AddComponent blow move the entities to a new chunk.
                // But at this point, the previously deallocated chunk gets recycled.
                // The entities are now back into their initial chunk, but with a different archetype.

                EntityManager.AddComponent<EcsTestData>(m_EntityA);
                EntityManager.AddComponent<EcsTestData>(m_EntityB);

                m_BufferLookup.Update(this);

                EntityManager.GetBuffer<EcsIntElement>(m_EntityA).Add(1234);
                EntityManager.GetBuffer<EcsIntElement>(m_EntityB).Add(2345);

                // Because the type indices of buffer components are always larger than the
                // indices of regular components, the buffers are now located at a different
                // offset in the chunk.
                // If the cache in the lookup incorrectly assumes that the same chunk implies
                // the same archetype, the cache contains an invalid offset.

                {
                    var arrayA = m_BufferLookup[m_EntityA].Reinterpret<int>().AsNativeArray().ToArray();
                    CollectionAssert.AreEqual(new[] { 123, 1234 }, arrayA);

                    var arrayB = m_BufferLookup[m_EntityB].Reinterpret<int>().AsNativeArray().ToArray();
                    CollectionAssert.AreEqual(new[] { 234, 2345 }, arrayB);
                }
            }
        }

        [Test]
        public void BufferLookup_CacheRegressionTest()
        {
            var system = World.CreateSystemManaged<BufferLookup_CacheRegressionTest_System>();
            system.Update();
        }
    }
}
