// Uncomment this line only if you need to generate csv export.
// Do not commit this line enabled, make sure to discard that change.
//#define GENERATE_CSV_EXPORT

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using NUnit.Framework;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using static Unity.Entities.EntitiesJournaling;

namespace Unity.Entities.Tests
{
    [TestFixture]
    unsafe partial class EntitiesJournalingTests : ECSTestsFixture
    {
#if !UNITY_ANDROID // APK bundling breaks reading from streamingAssets (DOTS-7038)
        static readonly string k_CSVExportFilePath;

        static EntitiesJournalingTests()
        {
#if UNITY_EDITOR
#if !DOTS_DISABLE_DEBUG_NAMES
            k_CSVExportFilePath = "Packages/com.unity.entities/Unity.Entities.Tests/Journaling/entities-journaling-export.csv";
#else
            k_CSVExportFilePath = "Packages/com.unity.entities/Unity.Entities.Tests/Journaling/entities-journaling-export-no-debug-names.csv";
#endif
#else
#if !DOTS_DISABLE_DEBUG_NAMES
            k_CSVExportFilePath = Path.Combine(UnityEngine.Application.streamingAssetsPath, "Journaling", "entities-journaling-export.csv");
#else
            k_CSVExportFilePath = Path.Combine(UnityEngine.Application.streamingAssetsPath, "Journaling", "entities-journaling-export-no-debug-names.csv");
#endif
#endif
        }
#endif

        public partial class TestEmptySystemManaged : SystemBase
        {
            protected override void OnUpdate() { }
        }

        public partial struct TestEmptySystemUnmanaged : ISystem
        {
            }

        public partial class TestSystemManaged : SystemBase
        {
            protected override void OnCreate()
            {
                var entity = EntityManager.CreateEntity(typeof(EcsTestData));
                EntityManager.SetComponentData(entity, new EcsTestData { value = 1 });
            }

            protected override void OnUpdate() { }
        }

        public partial struct TestSystemUnmanaged : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                var entity = state.EntityManager.CreateEntity(typeof(EcsTestData));
                state.EntityManager.SetComponentData(entity, new EcsTestData { value = 1 });
            }
        }

        public partial class TestSystemCreatingOtherSystems : SystemBase
        {
            public SystemHandle ManagedSystem { get; private set; }
            public SystemHandle UnmanagedSystem { get; private set; }

            protected override void OnCreate()
            {
                ManagedSystem = World.CreateSystem<TestEmptySystemManaged>();
                UnmanagedSystem = World.CreateSystem<TestEmptySystemUnmanaged>();
            }

            protected override void OnUpdate() { }
        }

        public partial class TestComponentSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities
                    .ForEach((ref EcsTestData writable1, ref EcsTestData2 writable2, in EcsTestData3 readOnly) =>
                    {
                        writable1.value += 1;
                    }).Run();
            }
        }

        public partial class TestComponentWithoutBurstSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities
                    .WithoutBurst()
                    .ForEach((ref EcsTestData writable1, ref EcsTestData2 writable2, in EcsTestData3 readOnly) =>
                    {
                        writable1.value += 1;
                    }).Run();
            }
        }

        public partial class TestComponentWithStructuralChangesSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities
                    .WithStructuralChanges()
                    .ForEach((ref EcsTestData writable1, ref EcsTestData2 writable2, in EcsTestData3 readOnly) =>
                    {
                        writable1.value += 1;
                    }).Run();
            }
        }

        public partial class TestSharedComponentWithoutBurstSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities
                    .WithoutBurst()
                    .ForEach((EcsTestSharedComp writable1, EcsTestSharedComp2 writable2, in EcsTestSharedComp3 readOnly) =>
                    {
                        writable1.value += 1;
                    }).Run();
            }
        }

        public partial class TestSharedComponentWithStructuralChangesSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities
                    .WithStructuralChanges()
                    .ForEach((EcsTestSharedComp writable1, EcsTestSharedComp2 writable2, in EcsTestSharedComp3 readOnly) =>
                    {
                        writable1.value += 1;
                    }).Run();
            }
        }

        public partial class TestBufferElementSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities
                    .ForEach((ref DynamicBuffer<EcsIntElement> writable1, ref DynamicBuffer<EcsIntElement2> writable2, in DynamicBuffer<EcsIntElement3> readOnly) =>
                    {
                        writable1.Add(new EcsIntElement { Value = 1 });
                    }).Run();
            }
        }

        public partial class TestBufferElementWithoutBurstSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities
                    .WithoutBurst()
                    .ForEach((ref DynamicBuffer<EcsIntElement> writable1, ref DynamicBuffer<EcsIntElement2> writable2, in DynamicBuffer<EcsIntElement3> readOnly) =>
                    {
                        writable1.Add(new EcsIntElement { Value = 1 });
                    }).Run();
            }
        }

        public partial class TestBufferElementWithStructuralChangesSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities
                    .WithStructuralChanges()
                    .ForEach((ref DynamicBuffer<EcsIntElement> writable1, ref DynamicBuffer<EcsIntElement2> writable2, in DynamicBuffer<EcsIntElement3> readOnly) =>
                    {
                        writable1.Add(new EcsIntElement { Value = 1 });
                    }).Run();
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        public partial class TestManagedComponentWithoutBurstSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities
                    .WithoutBurst()
                    .ForEach((EcsTestManagedComponent writable1, EcsTestManagedComponent2 writable2, in EcsTestManagedComponent3 readOnly) =>
                    {
                        writable1.value = "hello";
                    }).Run();
            }
        }

        public partial class TestManagedComponentWithStructuralChangesSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities
                    .WithStructuralChanges()
                    .ForEach((EcsTestManagedComponent writable1, EcsTestManagedComponent2 writable2, in EcsTestManagedComponent3 readOnly) =>
                    {
                        writable1.value = "hello";
                    }).Run();
            }
        }
#endif

        static T[] ToArray<T>(T value) => new T[] { value };
        static T[] ToArray<T>(NativeArray<T> array) where T : struct => array.ToArray();
        static ComponentType[] ToArray(Type type) => new ComponentType[] { type };
        static ComponentType[] ToArray(params Type[] types) => types.Select(t => new ComponentType(t)).ToArray();
        static T[] MakeArray<T>(int length, T value)
        {
            var array = new T[length];
            for (var i = 0; i < length; ++i)
                array[i] = value;
            return array;
        }

        class RecordDesc
        {
            public ulong Index { get; private set; }
            public RecordType RecordType { get; private set; }
            public World World { get; private set; }
            public SystemHandle ExecutingSystem { get; private set; }
            public SystemHandle OriginSystem { get; private set; }
            public Entity[] Entities { get; private set; }
            public ComponentType[] ComponentTypes { get; private set; }
            public object Data { get; private set; }

            public RecordDesc(ulong index, RecordType recordType, World world = null, SystemHandle executingSystem = default, SystemHandle originSystem = default, Entity[] entities = null, ComponentType[] componentTypes = null, object data = null)
            {
                Index = index;
                RecordType = recordType;
                World = world;
                ExecutingSystem = executingSystem;
                OriginSystem = originSystem;
                Entities = entities;
                ComponentTypes = componentTypes;
                Data = data;
            }
        }

        static void CheckRecord(RecordDesc expected, RecordView actual, int recordI)
        {
            Assert.That(actual.Index, Is.EqualTo(expected.Index));
            Assert.That(actual.RecordType, Is.EqualTo(expected.RecordType));

            // Can't test -- this value is non-deterministic
            //Assert.That(actual.FrameIndex, Is.Zero);

            if (expected.World == null)
            {
                Assert.That(actual.World.Reference, Is.Null);
                Assert.That(actual.World.SequenceNumber, Is.Zero);
            }
            else
            {
                Assert.That(actual.World.Reference, Is.EqualTo(expected.World));
                Assert.That(actual.World.SequenceNumber, Is.EqualTo(expected.World.SequenceNumber));
            }

            Assert.That(actual.ExecutingSystem.Handle, Is.EqualTo(expected.ExecutingSystem), $"For record {recordI} actual: {actual.ExecutingSystem.Handle.m_Entity} expected: {expected.ExecutingSystem.m_Entity}");
            Assert.That(actual.OriginSystem.Handle, Is.EqualTo(expected.OriginSystem), $"For record {recordI}");

            if (expected.Entities == null)
            {
                Assert.That(actual.Entities.Length, Is.Zero);
            }
            else
            {
                Assert.That(actual.Entities.Length, Is.EqualTo(expected.Entities.Length));
                for (var i = 0; i < expected.Entities.Length; ++i)
                {
                    var entity = expected.Entities[i];
                    var entityView = actual.Entities[i];
                    Assert.That(entityView.Index, Is.EqualTo(entity.Index), $"For entity {i} in record {recordI}");
                    Assert.That(entityView.Version, Is.EqualTo(entity.Version));
                    if (expected.World != null)
                        Assert.That(entityView.WorldSequenceNumber, Is.EqualTo(expected.World.SequenceNumber));
                }
            }

            if (expected.ComponentTypes == null)
            {
                Assert.That(actual.ComponentTypes.Length, Is.Zero);
            }
            else
            {
                var actualTypeIndexes = actual.ComponentTypes.Select(c => c.TypeIndex).ToArray();
                var expectedTypeIndexes = expected.ComponentTypes.Select(c => c.TypeIndex).ToArray();
                Assert.That(actualTypeIndexes, Is.EquivalentTo(expectedTypeIndexes));
            }

            if (expected.Data == null)
            {
                if (actual.Data is IList actualDataList)
                    Assert.That(actualDataList.Count, Is.Zero);
                else
                    Assert.That(actual.Data, Is.Null);
            }
            else
            {
                Assert.That(actual.Data, Is.EqualTo(expected.Data));
            }
        }

        static void CheckRecords(params RecordDesc[] expected)
        {
            var actual = GetRecords(Ordering.Ascending);
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; ++i)
                CheckRecord(expected[i], actual[i], i);
        }

        static void CheckRecordsWithPostProcess(params RecordDesc[] expected)
        {
            var actual = GetRecords(Ordering.Ascending);
            RecordViewArrayUtility.ConvertGetRWsToSets(in actual);
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; ++i)
                CheckRecord(expected[i], actual[i], i);
        }

        class RecordScope : IDisposable
        {
            public RecordScope()
            {
                Clear();
            }

            public void Dispose()
            {
                Enabled = false;
            }
        }

        bool m_LastEnabled;
        int m_LastTotalMemoryMB;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            m_LastEnabled = Preferences.Enabled;
            m_LastTotalMemoryMB = Preferences.TotalMemoryMB;

            Shutdown();
            Preferences.Enabled = true;
            Preferences.TotalMemoryMB = 64;
            Initialize();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            Shutdown();
            Preferences.Enabled = m_LastEnabled;
            Preferences.TotalMemoryMB = m_LastTotalMemoryMB;
            Initialize();
        }

        [SetUp]
        public override void Setup()
        {
            Clear();
            Enabled = true;
            base.Setup();
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
            Enabled = false;
            Clear();
        }

        [Test]
        public void Enabled_WhenNotInitialized_CanBeSet()
        {
            // Shutdown journaling to test that it gets initialized
            Shutdown();

            // Now verify we can enable journaling while its not initialized
            Assert.That(Enabled, Is.False);
            Enabled = true;
            Assert.That(Enabled, Is.True);
        }

        [Test]
        public void SystemAdded_Managed()
        {
            TestSystemManaged system;
            Entity entity;
            using (var scope = new RecordScope())
            {
                system = World.GetOrCreateSystemManaged<TestSystemManaged>();
                entity = m_Manager.CreateEntity(); // Intentionally created outside of system
            }

            var systemHandle = system.SystemHandle;

            // Find the entity that was created in OnCreate
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            using var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.That(entities.Length, Is.EqualTo(1));

            using var systemQuery = m_Manager.CreateEntityQuery(typeof(SystemInstance));
            using var systemEntities = systemQuery.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.That(systemEntities.Length, Is.EqualTo(1));

            CheckRecords(
                new RecordDesc(0, RecordType.CreateEntity, World, entities: ToArray(systemEntities), componentTypes: ToArray(typeof(SystemInstance), typeof(Simulate))),
                new RecordDesc(1, RecordType.GetComponentDataRW, World, entities: ToArray(systemEntities), componentTypes: ToArray(typeof(SystemInstance)), data: ToArray(new SystemInstance())),
                new RecordDesc(2, RecordType.SystemAdded, World, data: new SystemView(&systemHandle)),

                // Verify OnCreate can record changes
                new RecordDesc(3, RecordType.CreateEntity, World, executingSystem: system.SystemHandle, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestData), typeof(Simulate))),
                new RecordDesc(4, RecordType.GetComponentDataRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData())),

                // Verify entity created outside of system really has no system
                new RecordDesc(5, RecordType.CreateEntity, World, entities: ToArray(entity))
            );
        }

        [Test]
        public void SystemAdded_Unmanaged()
        {
            SystemHandle system;
            Entity entity;

            using (var scope = new RecordScope())
            {
                system = World.GetOrCreateSystem<TestSystemUnmanaged>();
                entity = m_Manager.CreateEntity(); // Intentionally created outside of system
            }

            var systemState = World.Unmanaged.ResolveSystemState(system);

            // Find the entity that was created from ECB
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            using var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.That(entities.Length, Is.EqualTo(1));

            using var systemQuery = m_Manager.CreateEntityQuery(typeof(SystemInstance));
            using var systemEntities = systemQuery.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.That(systemEntities.Length, Is.EqualTo(1));

            CheckRecords(
                new RecordDesc(0, RecordType.CreateEntity, World, entities: ToArray(systemEntities), componentTypes: ToArray(typeof(SystemInstance), typeof(Simulate))),
                new RecordDesc(1, RecordType.GetComponentDataRW, World, entities: ToArray(systemEntities), componentTypes: ToArray(typeof(SystemInstance)), data: ToArray(new SystemInstance())),
                new RecordDesc(2, RecordType.SystemAdded, World, data: new SystemView(&system)),

                // Verify OnCreate can record changes
                new RecordDesc(3, RecordType.CreateEntity, World, executingSystem: system, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestData), typeof(Simulate))),
                new RecordDesc(4, RecordType.GetComponentDataRW, World, executingSystem: system, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData())),

                // Verify entity created outside of system really has no system
                new RecordDesc(5, RecordType.CreateEntity, World, entities: ToArray(entity))
            );
        }

        [Test]
        public void SystemRemoved_Managed()
        {
            var system = World.GetOrCreateSystemManaged<TestSystemManaged>();
            var systemHandle = system.SystemHandle;

            using (var scope = new RecordScope())
            {
                World.DestroySystemManaged(system);
            }

            CheckRecords(
                new RecordDesc(0, RecordType.SystemRemoved, World, data: new SystemView(&systemHandle)),
                new RecordDesc(1, RecordType.DestroyEntity, World, entities: ToArray(systemHandle.m_Entity))
            );
        }

        [Test]
        public void SystemRemoved_Unmanaged()
        {
            var system = World.GetOrCreateSystem<TestSystemUnmanaged>();

            using (var scope = new RecordScope())
            {
                World.DestroySystem(system);
            }

            CheckRecords(
                new RecordDesc(0, RecordType.SystemRemoved, World, data: new SystemView(&system)),
                new RecordDesc(1, RecordType.DestroyEntity, World, entities: ToArray(system.m_Entity))
            );
        }

        [Test]
        public void SystemCreatingOtherSystems()
        {
            TestSystemCreatingOtherSystems system;
            using (var scope = new RecordScope())
            {
                system = World.GetOrCreateSystemManaged<TestSystemCreatingOtherSystems>();
            }

            var systemHandle = system.SystemHandle;
            var managedSystem = system.ManagedSystem;
            var unmanagedSystem = system.UnmanagedSystem;

            using var systemQuery = m_Manager.CreateEntityQuery(typeof(SystemInstance));
            using var systemEntities = systemQuery.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.That(systemEntities.Length, Is.EqualTo(3));

            CheckRecords(
                new RecordDesc(0, RecordType.CreateEntity, World, entities: ToArray(systemEntities.GetSubArray(0, 1)), componentTypes: ToArray(typeof(SystemInstance), typeof(Simulate))),
                new RecordDesc(1, RecordType.GetComponentDataRW, World, entities: ToArray(systemEntities.GetSubArray(0, 1)), componentTypes: ToArray(typeof(SystemInstance)), data: ToArray(new SystemInstance())),
                new RecordDesc(2, RecordType.SystemAdded, World, data: new SystemView(&systemHandle)),

                new RecordDesc(3, RecordType.CreateEntity, World, executingSystem: system.SystemHandle, entities: ToArray(systemEntities.GetSubArray(1, 1)), componentTypes: ToArray(typeof(SystemInstance), typeof(Simulate))),
                new RecordDesc(4, RecordType.GetComponentDataRW, World, executingSystem: system.SystemHandle, entities: ToArray(systemEntities.GetSubArray(1, 1)), componentTypes: ToArray(typeof(SystemInstance)), data: ToArray(new SystemInstance())),
                new RecordDesc(5, RecordType.SystemAdded, World, executingSystem: system.SystemHandle, data: new SystemView(&managedSystem)),

                new RecordDesc(6, RecordType.CreateEntity, World, executingSystem: system.SystemHandle, entities: ToArray(systemEntities.GetSubArray(2, 1)), componentTypes: ToArray(typeof(SystemInstance), typeof(Simulate))),
                new RecordDesc(7, RecordType.GetComponentDataRW, World, entities: ToArray(systemEntities.GetSubArray(2, 1)), componentTypes: ToArray(typeof(SystemInstance)), data: ToArray(new SystemInstance())),
                new RecordDesc(8, RecordType.SystemAdded, World, executingSystem: system.SystemHandle, data: new SystemView(&unmanagedSystem))
            );
        }

        [Test]
        public void CreateEntity()
        {
            Entity entity;
            using (var scope = new RecordScope())
            {
                entity = m_Manager.CreateEntity();
            }

            CheckRecords(
                new RecordDesc(0, RecordType.CreateEntity, World, entities: ToArray(entity))
            );
        }

        [Test]
        public void CreateEntity_WithArchetype()
        {
            Entity entity;
            using (var scope = new RecordScope())
            {
                entity = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsTestData)));
            }

            CheckRecords(
                new RecordDesc(0, RecordType.CreateEntity, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData), typeof(Simulate)))
            );
        }

        [Test]
        public void CreateEntity_WithComponentTypes()
        {
            Entity entity;
            using (var scope = new RecordScope())
            {
                entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            }

            CheckRecords(
                new RecordDesc(0, RecordType.CreateEntity, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData), typeof(EcsTestData2), typeof(Simulate)))
            );
        }

        [Test]
        public void CreateEntity_WithArchetypeAndEntityArray()
        {
            using (var entities = new NativeArray<Entity>(10, Allocator.Temp))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsTestData)), entities);
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.CreateEntity, World, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestData), typeof(Simulate)))
                );
            }
        }

        [Test]
        public void CreateEntity_WithArchetypeAndEntityCount()
        {
            using (var scope = new RecordScope())
            {
                m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsTestData)), 10);
            }

            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            using (var entities = query.ToEntityArray(Allocator.Temp))
            {
                CheckRecords(
                    new RecordDesc(0, RecordType.CreateEntity, World, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestData), typeof(Simulate)))
                );
            }
        }

        [Test]
        public void CreateEntity_WithArchetypeAndEntityCountAndAllocator()
        {
            NativeArray<Entity> entities;
            using (var scope = new RecordScope())
            {
                entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsTestData)), 10, Allocator.Temp);
            }

            CheckRecords(
                new RecordDesc(0, RecordType.CreateEntity, World, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestData), typeof(Simulate)))
            );

            entities.Dispose();
        }

        [Test]
        public void DestroyEntity()
        {
            var entity = m_Manager.CreateEntity();
            using (var scope = new RecordScope())
            {
                m_Manager.DestroyEntity(entity);
            }

            CheckRecords(
                new RecordDesc(0, RecordType.DestroyEntity, World, entities: ToArray(entity))
            );
        }

        [Test]
        public void DestroyEntity_WithQuery()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.DestroyEntity(query);
                }
            }

            CheckRecords(
                new RecordDesc(0, RecordType.DestroyEntity, World, entities: ToArray(entity))
            );
        }

        [Test]
        public void DestroyEntity_WithEntityArray()
        {
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsTestData)), 10, Allocator.Temp))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.DestroyEntity(entities);
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.DestroyEntity, World, entities: ToArray(entities))
                );
            }
        }

        [Test]
        public void AddComponent()
        {
            var entity = m_Manager.CreateEntity();
            using (var scope = new RecordScope())
            {
                m_Manager.AddComponent(entity, typeof(EcsTestData));
            }

            CheckRecords(
                new RecordDesc(0, RecordType.AddComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData)))
            );
        }

        [Test]
        public void AddComponent_WithComponentTypeSet()
        {
            var entity = m_Manager.CreateEntity();
            using (var scope = new RecordScope())
            {
                m_Manager.AddComponent(entity, new ComponentTypeSet(new ComponentType[] { typeof(EcsTestData), typeof(EcsTestData2) }));
            }

            CheckRecords(
                new RecordDesc(0, RecordType.AddComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData), typeof(EcsTestData2)))
            );
        }

        [Test]
        public void AddComponent_WithQuery()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.AddComponent(query, typeof(EcsTestData2));
                }
            }

            CheckRecords(
                new RecordDesc(0, RecordType.AddComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData2)))
            );
        }

        [Test]
        public void AddComponent_WithEntityArray()
        {
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsTestData)), 10, Allocator.Temp))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.AddComponent(entities, typeof(EcsTestData2));
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.AddComponent, World, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestData2)))
                );
            }
        }

        [Test]
        public void AddComponentData()
        {
            var entity = m_Manager.CreateEntity();
            using (var scope = new RecordScope())
            {
                m_Manager.AddComponentData(entity, new EcsTestData(42));
            }

            CheckRecords(
                new RecordDesc(0, RecordType.AddComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData))),
                new RecordDesc(1, RecordType.GetComponentDataRW, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData()))
            );
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void AddComponentManaged()
        {
            var entity = m_Manager.CreateEntity();
            using (var scope = new RecordScope())
            {
                m_Manager.AddComponent(entity, typeof(EcsTestManagedComponent));
            }

            CheckRecords(
                new RecordDesc(0, RecordType.AddComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestManagedComponent)))
            );
        }

        [Test]
        public void AddComponentDataManaged()
        {
            var entity = m_Manager.CreateEntity();
            using (var scope = new RecordScope())
            {
                m_Manager.AddComponentData(entity, new EcsTestManagedComponent());
            }

            CheckRecords(
                new RecordDesc(0, RecordType.AddComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestManagedComponent))),
                new RecordDesc(1, RecordType.GetComponentObjectRW, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestManagedComponent)))
            );
        }
#endif

        [Test]
        public void AddSharedComponent()
        {
            var entity = m_Manager.CreateEntity();
            using (var scope = new RecordScope())
            {
                m_Manager.AddSharedComponent(entity, new EcsTestSharedComp(42));
            }

            CheckRecords(
                new RecordDesc(0, RecordType.AddComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestSharedComp))),
                new RecordDesc(1, RecordType.SetSharedComponentData, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestSharedComp)), data: ToArray(new EcsTestSharedComp(42)))
            );
        }

        [Test]
        public void AddSharedComponent_WithEntityArray()
        {
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(), 10, Allocator.Temp))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.AddSharedComponent(entities, new EcsTestSharedComp(42));
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.AddComponent, World, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestSharedComp))),
                    new RecordDesc(1, RecordType.SetSharedComponentData, World, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestSharedComp)), data: ToArray(new EcsTestSharedComp(42)))
                );
            }
        }

        [Test]
        public void AddSharedComponent_WithQuery()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.AddSharedComponent(query, new EcsTestSharedComp(42));
                }
            }

            CheckRecords(
                new RecordDesc(0, RecordType.AddComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestSharedComp))),
                new RecordDesc(1, RecordType.SetSharedComponentData, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestSharedComp)), data: ToArray(new EcsTestSharedComp(42)))
            );
        }

        [Test]
        public void AddSharedComponent_WithChunkArray()
        {
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsTestData)), 10, Allocator.Temp))
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            using (var chunks = query.ToArchetypeChunkArray(Allocator.Temp))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.AddSharedComponent(chunks, new EcsTestSharedComp(42));
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.AddComponent, World, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestSharedComp))),
                    new RecordDesc(1, RecordType.SetSharedComponentData, World, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestSharedComp)), data: ToArray(new EcsTestSharedComp(42)))
                );
            }
        }

        [Test]
        public void AddSharedComponentManaged()
        {
            var entity = m_Manager.CreateEntity();
            using (var scope = new RecordScope())
            {
                m_Manager.AddSharedComponentManaged(entity, new EcsTestSharedCompManaged("hello"));
            }

            CheckRecords(
                new RecordDesc(0, RecordType.AddComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestSharedCompManaged))),
                new RecordDesc(1, RecordType.SetSharedComponentData, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestSharedCompManaged)))
            );
        }

        [Test]
        public void AddSharedComponentManaged_WithEntityArray()
        {
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(), 10, Allocator.Temp))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.AddSharedComponentManaged(entities, new EcsTestSharedCompManaged("hello"));
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.AddComponent, World, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestSharedCompManaged))),
                    new RecordDesc(1, RecordType.SetSharedComponentData, World, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestSharedCompManaged)))
                );
            }
        }

        [Test]
        public void AddSharedComponentManaged_WithQuery()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.AddSharedComponentManaged(query, new EcsTestSharedCompManaged("hello"));
                }
            }

            CheckRecords(
                new RecordDesc(0, RecordType.AddComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestSharedCompManaged))),
                new RecordDesc(1, RecordType.SetSharedComponentData, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestSharedCompManaged)))
            );
        }

        [Test]
        public void AddSharedComponentManaged_WithChunkArray()
        {
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsTestData)), 10, Allocator.Temp))
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            using (var chunks = query.ToArchetypeChunkArray(Allocator.Temp))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.AddSharedComponentManaged(chunks, new EcsTestSharedCompManaged("hello"));
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.AddComponent, World, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestSharedCompManaged))),
                    new RecordDesc(1, RecordType.SetSharedComponentData, World, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestSharedCompManaged)))
                );
            }
        }

        [Test]
        public void AddChunkComponentData()
        {
            var entity = m_Manager.CreateEntity();
            using (var scope = new RecordScope())
            {
                m_Manager.AddChunkComponentData<EcsTestData>(entity);
            }

            var chunkEntity = m_Manager.GetChunk(entity).m_Chunk.MetaChunkEntity;
            CheckRecords(
                new RecordDesc(0, RecordType.AddComponent, World, entities: ToArray(entity), componentTypes: ToArray(ComponentType.ChunkComponent<EcsTestData>())),
                new RecordDesc(1, RecordType.GetComponentDataRW, World, entities: ToArray(chunkEntity), componentTypes: ToArray(typeof(ChunkHeader)), data: ToArray(ChunkHeader.Null))
            );
        }

        [Test]
        public void AddChunkComponentData_WithQuery()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.AddChunkComponentData(query, new EcsTestData2(42));
                }
            }

            var chunkEntity = m_Manager.GetChunk(entity).m_Chunk.MetaChunkEntity;
            CheckRecords(
                new RecordDesc(0, RecordType.AddComponent, World, entities: ToArray(entity), componentTypes: ToArray(ComponentType.ChunkComponent<EcsTestData2>())),
                new RecordDesc(1, RecordType.GetComponentDataRW, World, entities: ToArray(chunkEntity), componentTypes: ToArray(typeof(ChunkHeader)), data: ToArray(ChunkHeader.Null)),
                new RecordDesc(2, RecordType.GetComponentDataRW, World, entities: ToArray(chunkEntity), componentTypes: ToArray(typeof(EcsTestData2)), data: ToArray(new EcsTestData2()))
            );
        }

        [Test]
        public void AddBuffer()
        {
            var entity = m_Manager.CreateEntity();
            using (var scope = new RecordScope())
            {
                m_Manager.AddBuffer<EcsIntElement>(entity);
            }

            CheckRecords(
                new RecordDesc(0, RecordType.AddComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsIntElement))),
                new RecordDesc(1, RecordType.GetBufferRW, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsIntElement)))
            );
        }

        [Test]
        public void RemoveComponent()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            using (var scope = new RecordScope())
            {
                m_Manager.RemoveComponent(entity, typeof(EcsTestData));
            }

            CheckRecords(
                new RecordDesc(0, RecordType.RemoveComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData)))
            );
        }

        [Test]
        public void RemoveComponent_WithComponentTypeSet()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            using (var scope = new RecordScope())
            {
                m_Manager.RemoveComponent(entity, new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2)));
            }

            CheckRecords(
                new RecordDesc(0, RecordType.RemoveComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData), typeof(EcsTestData2)))
            );
        }

        [Test]
        public void RemoveComponent_WithQuery()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.RemoveComponent(query, typeof(EcsTestData));
                }
            }

            CheckRecords(
                new RecordDesc(0, RecordType.RemoveComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData)))
            );
        }

        [Test]
        public void RemoveComponent_WithQueryAndComponentTypeSet()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2)))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.RemoveComponent(query, new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2)));
                }
            }

            CheckRecords(
                new RecordDesc(0, RecordType.RemoveComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData), typeof(EcsTestData2)))
            );
        }

        [Test]
        public void RemoveComponent_WithEntityArray()
        {
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsTestData)), 10, Allocator.Temp))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.RemoveComponent(entities, typeof(EcsTestData));
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.RemoveComponent, World, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestData)))
                );
            }
        }

        [Test]
        public void RemoveComponent_WithEntityArrayAndComponentTypeSet()
        {
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2)), 10, Allocator.Temp))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.RemoveComponent(entities, new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2)));
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.RemoveComponent, World, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestData), typeof(EcsTestData2)))
                );
            }
        }

        [Test]
        public void SetComponentEnabled()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestDataEnableable));
            using (var scope = new RecordScope())
            {
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entity, false);
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entity, true);
            }

            CheckRecords(
                new RecordDesc(0, RecordType.DisableComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestDataEnableable))),
                new RecordDesc(1, RecordType.EnableComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestDataEnableable)))
            );
        }

        [Test]
        public void SetComponentEnabled_WithChunkComponentTypeHandle()
        {
            var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false);
            var entity = m_Manager.CreateEntity(typeof(EcsTestDataEnableable));
            var chunk = m_Manager.GetChunk(entity);

            using (var scope = new RecordScope())
            {
                for (var i = 0; i < chunk.Count; ++i)
                    chunk.SetComponentEnabled(ref typeHandle, i, false);
                for (var i = 0; i < chunk.Count; ++i)
                    chunk.SetComponentEnabled(ref typeHandle, i, true);
            }

            CheckRecords(
                new RecordDesc(0, RecordType.DisableComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestDataEnableable))),
                new RecordDesc(1, RecordType.EnableComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestDataEnableable)))
            );
        }

        [Test]
        public void SetComponentEnabled_WithChunkDynamicComponentTypeHandle()
        {
            var typeHandle = m_Manager.GetDynamicComponentTypeHandle(typeof(EcsTestDataEnableable));
            var entity = m_Manager.CreateEntity(typeof(EcsTestDataEnableable));
            var chunk = m_Manager.GetChunk(entity);

            using (var scope = new RecordScope())
            {
                for (var i = 0; i < chunk.Count; ++i)
                    chunk.SetComponentEnabled(ref typeHandle, i, false);
                for (var i = 0; i < chunk.Count; ++i)
                    chunk.SetComponentEnabled(ref typeHandle, i, true);
            }

            CheckRecords(
                new RecordDesc(0, RecordType.DisableComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestDataEnableable))),
                new RecordDesc(1, RecordType.EnableComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestDataEnableable)))
            );
        }

        [Test]
        public void SetComponentEnabled_WithChunkBufferTypeHandle()
        {
            var typeHandle = m_Manager.GetBufferTypeHandle<EcsIntElementEnableable>(false);
            var entity = m_Manager.CreateEntity(typeof(EcsIntElementEnableable));
            var chunk = m_Manager.GetChunk(entity);

            using (var scope = new RecordScope())
            {
                for (var i = 0; i < chunk.Count; ++i)
                    chunk.SetComponentEnabled(ref typeHandle, i, false);
                for (var i = 0; i < chunk.Count; ++i)
                    chunk.SetComponentEnabled(ref typeHandle, i, true);
            }

            CheckRecords(
                new RecordDesc(0, RecordType.DisableComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsIntElementEnableable))),
                new RecordDesc(1, RecordType.EnableComponent, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsIntElementEnableable)))
            );
        }

        [Test]
        public void SetComponentData()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddComponent(entity, typeof(EcsTestData));

            using (var scope = new RecordScope())
            {
                m_Manager.SetComponentData(entity, new EcsTestData(42));
            }

            CheckRecords(
                new RecordDesc(0, RecordType.GetComponentDataRW, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData()))
            );
        }

        [Test]
        public void SetComponentData_EntitiesForEach()
        {
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3)), 3, Allocator.Temp))
            {
                var system = World.GetOrCreateSystemManaged<TestComponentSystem>();
                using (var scope = new RecordScope())
                {
                    system.Update();
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.GetComponentDataRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestData)), data: MakeArray(entities.Length, new EcsTestData())),
                    new RecordDesc(1, RecordType.GetComponentDataRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestData2)), data: MakeArray(entities.Length, new EcsTestData2()))
                );
            }
        }

        [Test]
        public void SetComponentData_EntitiesForEach_WithoutBurst()
        {
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3)), 3, Allocator.Temp))
            {
                var system = World.GetOrCreateSystemManaged<TestComponentWithoutBurstSystem>();
                using (var scope = new RecordScope())
                {
                    system.Update();
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.GetComponentDataRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestData)), data: MakeArray(entities.Length, new EcsTestData())),
                    new RecordDesc(1, RecordType.GetComponentDataRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestData2)), data: MakeArray(entities.Length, new EcsTestData2()))
                );
            }
        }

        [Test]
        public void SetComponentData_EntitiesForEach_WithStructuralChanges()
        {
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3)), 3, Allocator.Temp))
            {
                var system = World.GetOrCreateSystemManaged<TestComponentWithStructuralChangesSystem>();
                using (var scope = new RecordScope())
                {
                    system.Update();
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.GetComponentDataRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[0]), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData())),
                    new RecordDesc(1, RecordType.GetComponentDataRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[1]), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData())),
                    new RecordDesc(2, RecordType.GetComponentDataRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[2]), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData()))
                );
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void SetComponentDataManaged()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestManagedComponent));
            using (var scope = new RecordScope())
            {
                m_Manager.SetComponentData(entity, new EcsTestManagedComponent());
            }

            CheckRecords(
                new RecordDesc(0, RecordType.GetComponentObjectRW, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestManagedComponent)))
            );
        }

        [Test]
        public void SetComponentDataManaged_EntitiesForEach_WithoutBurst()
        {
            var componentTypes = new ComponentType[] { typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2), typeof(EcsTestManagedComponent3) };
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(componentTypes), 3, Allocator.Temp))
            {
                var system = World.GetOrCreateSystemManaged<TestManagedComponentWithoutBurstSystem>();
                foreach (var entity in entities)
                {
                    m_Manager.SetComponentData(entity, new EcsTestManagedComponent());
                    m_Manager.SetComponentData(entity, new EcsTestManagedComponent2());
                    m_Manager.SetComponentData(entity, new EcsTestManagedComponent3());
                }

                using (var scope = new RecordScope())
                {
                    system.Update();
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.GetComponentObjectRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestManagedComponent))),
                    new RecordDesc(1, RecordType.GetComponentObjectRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestManagedComponent2))),
                    new RecordDesc(2, RecordType.GetComponentObjectRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestManagedComponent3)))
                );
            }
        }

        [Test]
        public void SetComponentDataManaged_EntitiesForEach_WithStructuralChanges()
        {
            var componentTypes = new ComponentType[] { typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2), typeof(EcsTestManagedComponent3) };
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(componentTypes), 3, Allocator.Temp))
            {
                var system = World.GetOrCreateSystemManaged<TestManagedComponentWithStructuralChangesSystem>();
                foreach (var entity in entities)
                {
                    m_Manager.SetComponentData(entity, new EcsTestManagedComponent());
                    m_Manager.SetComponentData(entity, new EcsTestManagedComponent2());
                    m_Manager.SetComponentData(entity, new EcsTestManagedComponent3());
                }

                using (var scope = new RecordScope())
                {
                    system.Update();
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.GetComponentObjectRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[0]), componentTypes: ToArray(typeof(EcsTestManagedComponent))),
                    new RecordDesc(1, RecordType.GetComponentObjectRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[0]), componentTypes: ToArray(typeof(EcsTestManagedComponent2))),
                    new RecordDesc(2, RecordType.GetComponentObjectRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[0]), componentTypes: ToArray(typeof(EcsTestManagedComponent3))),
                    new RecordDesc(3, RecordType.GetComponentObjectRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[1]), componentTypes: ToArray(typeof(EcsTestManagedComponent))),
                    new RecordDesc(4, RecordType.GetComponentObjectRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[1]), componentTypes: ToArray(typeof(EcsTestManagedComponent2))),
                    new RecordDesc(5, RecordType.GetComponentObjectRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[1]), componentTypes: ToArray(typeof(EcsTestManagedComponent3))),
                    new RecordDesc(6, RecordType.GetComponentObjectRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[2]), componentTypes: ToArray(typeof(EcsTestManagedComponent))),
                    new RecordDesc(7, RecordType.GetComponentObjectRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[2]), componentTypes: ToArray(typeof(EcsTestManagedComponent2))),
                    new RecordDesc(8, RecordType.GetComponentObjectRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[2]), componentTypes: ToArray(typeof(EcsTestManagedComponent3)))
                );
            }
        }
#endif

        [Test]
        public void SetSharedComponent()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestSharedComp));
            using (var scope = new RecordScope())
            {
                m_Manager.SetSharedComponent(entity, new EcsTestSharedComp(42));
            }

            CheckRecords(
                new RecordDesc(0, RecordType.SetSharedComponentData, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestSharedComp)), data: ToArray(new EcsTestSharedComp(42)))
            );
        }

        [Test]
        public void SetSharedComponent_WithEntityArray()
        {
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsTestSharedComp)), 10, Allocator.Temp))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.SetSharedComponent(entities, new EcsTestSharedComp(42));
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.SetSharedComponentData, World, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestSharedComp)), data: ToArray(new EcsTestSharedComp(42)))
                );
            }
        }

        [Test]
        public void SetSharedComponent_WithQuery()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestSharedComp));
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.SetSharedComponent(query, new EcsTestSharedComp(42));
                }
            }

            CheckRecords(
                new RecordDesc(0, RecordType.SetSharedComponentData, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestSharedComp)), data: ToArray(new EcsTestSharedComp(42)))
            );
        }

        [Test]
        public void SetSharedComponentManaged()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestSharedCompManaged));
            using (var scope = new RecordScope())
            {
                m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedCompManaged("hello"));
            }

            CheckRecords(
                new RecordDesc(0, RecordType.SetSharedComponentData, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestSharedCompManaged)))
            );
        }

        [Test]
        public void SetSharedComponentManaged_WithEntityArray()
        {
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsTestSharedCompManaged)), 10, Allocator.Temp))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.SetSharedComponentManaged(entities, new EcsTestSharedCompManaged("hello"));
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.SetSharedComponentData, World, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestSharedCompManaged)))
                );
            }
        }

        [Test]
        public void SetSharedComponentManaged_WithQuery()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestSharedCompManaged));
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                using (var scope = new RecordScope())
                {
                    m_Manager.SetSharedComponentManaged(query, new EcsTestSharedCompManaged("hello"));
                }
            }

            CheckRecords(
                new RecordDesc(0, RecordType.SetSharedComponentData, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestSharedCompManaged)))
            );
        }

        [Test]
        public void SetChunkComponentData()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddChunkComponentData<EcsTestData>(entity);

            var chunk = m_Manager.GetChunk(entity);
            using (var scope = new RecordScope())
            {
                m_Manager.SetChunkComponentData(chunk, new EcsTestData(42));
            }

            var chunkEntity = chunk.m_Chunk.MetaChunkEntity;
            CheckRecords(
                new RecordDesc(0, RecordType.GetComponentDataRW, World, entities: ToArray(chunkEntity), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData()))
            );
        }

        [Test]
        public void SetBuffer_EntitiesForEach()
        {
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsIntElement), typeof(EcsIntElement2), typeof(EcsIntElement3)), 3, Allocator.Temp))
            {
                var system = World.GetOrCreateSystemManaged<TestBufferElementSystem>();
                using (var scope = new RecordScope())
                {
                    system.Update();
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.GetBufferRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsIntElement))),
                    new RecordDesc(1, RecordType.GetBufferRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsIntElement2)))
                );
            }
        }

        [Test]
        public void SetBuffer_EntitiesForEach_WithoutBurst()
        {
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsIntElement), typeof(EcsIntElement2), typeof(EcsIntElement3)), 3, Allocator.Temp))
            {
                var system = World.GetOrCreateSystemManaged<TestBufferElementWithoutBurstSystem>();
                using (var scope = new RecordScope())
                {
                    system.Update();
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.GetBufferRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsIntElement))),
                    new RecordDesc(1, RecordType.GetBufferRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsIntElement2)))
                );
            }
        }

        [Test]
        public void SetBuffer_EntitiesForEach_WithStructuralChanges()
        {
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsIntElement), typeof(EcsIntElement2), typeof(EcsIntElement3)), 3, Allocator.Temp))
            {
                var system = World.GetOrCreateSystemManaged<TestBufferElementWithStructuralChangesSystem>();
                using (var scope = new RecordScope())
                {
                    system.Update();
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.GetBufferRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[0]), componentTypes: ToArray(typeof(EcsIntElement))),
                    new RecordDesc(1, RecordType.GetBufferRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[0]), componentTypes: ToArray(typeof(EcsIntElement2))),
                    new RecordDesc(2, RecordType.GetBufferRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[0]), componentTypes: ToArray(typeof(EcsIntElement3))),
                    new RecordDesc(3, RecordType.GetBufferRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[1]), componentTypes: ToArray(typeof(EcsIntElement))),
                    new RecordDesc(4, RecordType.GetBufferRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[1]), componentTypes: ToArray(typeof(EcsIntElement2))),
                    new RecordDesc(5, RecordType.GetBufferRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[1]), componentTypes: ToArray(typeof(EcsIntElement3))),
                    new RecordDesc(6, RecordType.GetBufferRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[2]), componentTypes: ToArray(typeof(EcsIntElement))),
                    new RecordDesc(7, RecordType.GetBufferRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[2]), componentTypes: ToArray(typeof(EcsIntElement2))),
                    new RecordDesc(8, RecordType.GetBufferRW, World, executingSystem: system.SystemHandle, entities: ToArray(entities[2]), componentTypes: ToArray(typeof(EcsIntElement3)))
                );
            }
        }

        [Test]
        public void GetComponentDataRawRW()
        {
            var entity = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsTestData)));
            using (var scope = new RecordScope())
            {
                m_Manager.GetComponentDataRawRW(entity, TypeManager.GetTypeIndex<EcsTestData>());
            }

            CheckRecords(
                new RecordDesc(0, RecordType.GetComponentDataRW, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData()))
            );
        }

        [Test]
        public void GetComponentObject()
        {
            var entity = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(UnityEngine.Camera)));
            using (var scope = new RecordScope())
            {
                m_Manager.GetComponentObject<UnityEngine.Camera>(entity);
            }

            CheckRecords(
                new RecordDesc(0, RecordType.GetComponentObjectRW, World, entities: ToArray(entity), componentTypes: ToArray(typeof(UnityEngine.Camera)))
            );
        }

        [Test]
        public void GetBuffer()
        {
            var entity = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsIntElement)));
            using (var scope = new RecordScope())
            {
                m_Manager.GetBuffer<EcsIntElement>(entity);
            }

            CheckRecords(
                new RecordDesc(0, RecordType.GetBufferRW, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsIntElement)))
            );
        }

        [Test]
        public void GetDynamicComponentDataArrayReinterpret()
        {
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2)), 3, Allocator.Temp))
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData2), typeof(EcsTestData)))
            using (var chunks = query.ToArchetypeChunkArray(Allocator.Temp))
            {
                var ecsTestDataDynamic = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadWrite(typeof(EcsTestData)));
                var ecsTestDataDynamic2 = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadWrite(typeof(EcsTestData2)));
                using (var scope = new RecordScope())
                {
                    for (var i = 0; i < chunks.Length; ++i)
                    {
                        var chunk = chunks[i];
                        var chunkEcsTestData = chunk.GetDynamicComponentDataArrayReinterpret<int>(ref ecsTestDataDynamic, UnsafeUtility.SizeOf<int>());
                        var chunkEcsTestData2 = chunk.GetDynamicComponentDataArrayReinterpret<int2>(ref ecsTestDataDynamic2, UnsafeUtility.SizeOf<int2>());
                    }
                }

                CheckRecords(
                    new RecordDesc(0, RecordType.GetComponentDataRW, World, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestData)), data: MakeArray(entities.Length, new EcsTestData())),
                    new RecordDesc(1, RecordType.GetComponentDataRW, World, entities: ToArray(entities), componentTypes: ToArray(typeof(EcsTestData2)), data: MakeArray(entities.Length, new EcsTestData2()))
                );
            }
        }

        [Test]
        public void SystemVersionToSystemHandle()
        {
            var system = World.GetOrCreateSystemManaged<TestComponentSystem>();
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3));
            var entity = m_Manager.CreateEntity(archetype);

            // Test that we support global system version wrap around
            m_ManagerDebug.SetGlobalSystemVersion(uint.MaxValue - 2);

            using (var scope = new RecordScope())
            {
                system.Update();
                m_Manager.SetComponentData(entity, new EcsTestData(42));
                system.Update();
                m_Manager.SetComponentData(entity, new EcsTestData(42));
            }

            CheckRecords(
                // First system.Update
                new RecordDesc(0, RecordType.GetComponentDataRW, World, executingSystem: system.SystemHandle, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData())),
                new RecordDesc(1, RecordType.GetComponentDataRW, World, executingSystem: system.SystemHandle, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData2)), data: ToArray(new EcsTestData2())),

                // First EntityManager.SetComponentData outside system
                new RecordDesc(2, RecordType.GetComponentDataRW, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData { value = 1 })),

                // Second system.Update
                new RecordDesc(3, RecordType.GetComponentDataRW, World, executingSystem: system.SystemHandle, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData { value = 42 })),
                new RecordDesc(4, RecordType.GetComponentDataRW, World, executingSystem: system.SystemHandle, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData2)), data: ToArray(new EcsTestData2())),

                // Second EntityManager.SetComponentData outside system
                new RecordDesc(5, RecordType.GetComponentDataRW, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData { value = 43 }))
            );
        }

        [Test]
        public void ConvertGetRWsToSets()
        {
            var system = World.GetOrCreateSystemManaged<TestComponentSystem>();
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3));
            var entity = m_Manager.CreateEntity(archetype);

            using (var scope = new RecordScope())
            {
                system.Update();
                system.Update();
                system.Update();
            }

            CheckRecordsWithPostProcess(
                // First system.Update
                new RecordDesc(0, RecordType.SetComponentData, World, executingSystem: system.SystemHandle, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData { value = 1 })),
                new RecordDesc(1, RecordType.SetComponentData, World, executingSystem: system.SystemHandle, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData2)), data: ToArray(new EcsTestData2())),

                // Second system.Update
                new RecordDesc(2, RecordType.SetComponentData, World, executingSystem: system.SystemHandle, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData { value = 2 })),
                new RecordDesc(3, RecordType.SetComponentData, World, executingSystem: system.SystemHandle, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData2)), data: ToArray(new EcsTestData2())),

                // Third system.Update (because its last, it won't be converted)
                new RecordDesc(4, RecordType.GetComponentDataRW, World, executingSystem: system.SystemHandle, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData { value = 2 })),
                new RecordDesc(5, RecordType.GetComponentDataRW, World, executingSystem: system.SystemHandle, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData2)), data: ToArray(new EcsTestData2()))
            );

            // Verify we can get records again, and results will not change
            CheckRecordsWithPostProcess(
                // First system.Update
                new RecordDesc(0, RecordType.SetComponentData, World, executingSystem: system.SystemHandle, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData { value = 1 })),
                new RecordDesc(1, RecordType.SetComponentData, World, executingSystem: system.SystemHandle, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData2)), data: ToArray(new EcsTestData2())),

                // Second system.Update
                new RecordDesc(2, RecordType.SetComponentData, World, executingSystem: system.SystemHandle, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData { value = 2 })),
                new RecordDesc(3, RecordType.SetComponentData, World, executingSystem: system.SystemHandle, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData2)), data: ToArray(new EcsTestData2())),

                // Third system.Update (because its last, it won't be converted)
                new RecordDesc(4, RecordType.GetComponentDataRW, World, executingSystem: system.SystemHandle, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData { value = 2 })),
                new RecordDesc(5, RecordType.GetComponentDataRW, World, executingSystem: system.SystemHandle, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData2)), data: ToArray(new EcsTestData2()))
            );
        }

#if !UNITY_ANDROID // APK bundling breaks reading from streamingAssets (DOTS-7038)
        [Test]
        public void ExportToCSV()
        {
            using (var entities = m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3)), 3, Allocator.Temp))
            {
                World.GetOrCreateSystemManaged<TestComponentSystem>().Update();

                var lines = EntitiesJournaling.ExportToCSV();
#if GENERATE_CSV_EXPORT
                File.WriteAllLines(k_CSVExportFilePath, lines, Encoding.UTF8);
#else
                var actual = lines.ToArray();
                var expected = File.ReadAllLines(k_CSVExportFilePath, Encoding.UTF8);
                Assert.IsTrue(actual.Length > 1);
                Assert.IsTrue(expected.Length > 1);
                Assert.AreEqual(expected.Length, actual.Length);

                var frameIndexColumnNumber = Array.IndexOf(expected[0].Split(','), "FrameIndex");
                var entitiesIndexColumnNumber = Array.IndexOf(expected[0].Split(','), "Entities");

                // start at 1 to skip the CSV header
                for (int line = 1; line < expected.Length; ++line)
                {
                    var actualLine = actual[line];
                    var expectedLine = expected[line];

#if !ENTITY_STORE_V1
                    // global allocation means we cannot guarantee determinism of entity IDs for that kind of tests
                    actualLine = Regex.Replace(actualLine, @"\((\d+):(\d+)\)", "<EntityID>");
                    expectedLine = Regex.Replace(expectedLine, @"\((\d+):(\d+)\)", "<EntityID>");
#endif

                    var actualColumns = actualLine.Split(',');
                    var expectedColumns = expectedLine.Split(',');
                    Assert.AreEqual(expectedColumns.Length, actualColumns.Length);

                    int numColumns = expectedColumns.Length;
                    for (int column = 0; column < numColumns; ++column)
                    {
                        // Frame indices are non-deterministic so skip comparing them
                        if (column == frameIndexColumnNumber)
                            continue;

                        Assert.AreEqual(expectedColumns[column], actualColumns[column]);
                    }
                }
#endif
            }
        }
#endif

        [Test]
        public void VerifyOriginSystemID()
        {
            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var entityOnlyArchetype = m_Manager.CreateArchetypeWithoutSimulateComponent(null, 0);
            var entity = ecb.CreateEntity(entityOnlyArchetype);
            ecb.AddComponent(entity, new EcsTestData(42));
            ecb.Playback(m_Manager);

            // Find the entity that was created from ECB
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            using var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.That(entities.Length, Is.EqualTo(1));
            entity = entities[0];

            var originSystem = ecb.SystemID > 0 ? World.Unmanaged.GetExistingUnmanagedSystem(TypeManager.GetSystemType(ecb.SystemID)) : World.Unmanaged.ExecutingSystem;
            CheckRecords(
                new RecordDesc(0, RecordType.CreateEntity, World, originSystem: originSystem, entities: ToArray(entity)),
                new RecordDesc(1, RecordType.AddComponent, World, originSystem: originSystem, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData))),
                new RecordDesc(2, RecordType.GetComponentDataRW, World, entities: ToArray(entity), componentTypes: ToArray(typeof(EcsTestData)), data: ToArray(new EcsTestData()))
            );
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void EntityJournalErrorAdded_DoesNotExist_EntityManager()
        {
            var e = m_Manager.CreateEntity();
            m_Manager.DestroyEntity(e);
            Assert.That(() => m_Manager.AddComponent<EcsTestData>(e),
                Throws.InvalidOperationException.With.Message.Contains("(" + e.Index + ":" + e.Version + ")"));

            Clear();

            var arch = m_Manager.CreateArchetype(typeof(EcsTestData));
            var entities = m_Manager.CreateEntity(arch, 5, World.UpdateAllocator.ToAllocator);
            var destroyedEntity = entities[0];
            m_Manager.DestroyEntity(entities[0]);
            Assert.That(() => m_Manager.AddComponent<EcsTestData>(entities),
                Throws.ArgumentException.With.Message.Contains("(" + destroyedEntity.Index + ":" + destroyedEntity.Version + ")"));
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void EntityJournalErrorAdded_DoesNotHaveComponent_EntityManager()
        {
            var e = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.RemoveComponent<EcsTestData>(e);
            Assert.That(() => m_Manager.SetComponentData(e, new EcsTestData { value = 10 }),
                Throws.ArgumentException.With.Message.Contains("(" + e.Index + ":" + e.Version + ")"));
        }

        partial class TestECBPlaybackSystem : EntityCommandBufferSystem { }
        partial class TestECBRecordingSystem_DestroyedErrorMessage : SystemBase
        {
            protected override void OnUpdate()
            {
                var ecb = World.GetOrCreateSystemManaged<TestECBPlaybackSystem>().CreateCommandBuffer();
                var e = ecb.CreateEntity();
                // NOTE: ECB playback being bursted does not add to the error message, so we are disabling it for the test
                ecb.m_Data->m_MainThreadChain.m_CanBurstPlayback = false;
                ecb.DestroyEntity(e);
                ecb.AddComponent<EcsTestData>(e);
            }
        }

        partial class TestECBRecordingSystem_RemovedErrorMessage : SystemBase
        {
            protected override void OnUpdate()
            {
                var ecb = World.GetOrCreateSystemManaged<TestECBPlaybackSystem>().CreateCommandBuffer();
                var e = ecb.CreateEntity();
                // NOTE: ECB playback being bursted does not add to the error message, so we are disabling it for the test
                ecb.m_Data->m_MainThreadChain.m_CanBurstPlayback = false;
                ecb.AddComponent<EcsTestData>(e);
                ecb.RemoveComponent<EcsTestData>(e);
                ecb.SetComponent(e, new EcsTestData { value = 42 });
            }
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void EntityJournalErrorAdded_ValidOriginSystem_DestroyedEntity()
        {
            using (var world = new World("World A"))
            {
                var ecbRecordingSystem = world.GetOrCreateSystemManaged<TestECBRecordingSystem_DestroyedErrorMessage>();
                var ecbPlaybackSystem = world.GetOrCreateSystemManaged<TestECBPlaybackSystem>();

                ecbRecordingSystem.Update();
                Assert.That(() => ecbPlaybackSystem.Update(),
                    Throws.ArgumentException.With.Message.Contains(
                        $"This command was requested from system {ecbRecordingSystem}."));
            }
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void EntityJournalErrorAdded_ValidOriginSystem_RemovedComponent()
        {
            using (var world = new World("World A"))
            {
                var ecbRecordingSystem = world.GetOrCreateSystemManaged<TestECBRecordingSystem_RemovedErrorMessage>();
                var ecbPlaybackSystem = world.GetOrCreateSystemManaged<TestECBPlaybackSystem>();

                ecbRecordingSystem.Update();
                Assert.That(() => ecbPlaybackSystem.Update(),
                    Throws.ArgumentException.With.Message.Contains(
                        $"This command was requested from system {ecbRecordingSystem}."));
            }
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void EntityJournalErrorAdded_DoesNotExist_EntityCommandBuffer()
        {
            var e = m_Manager.CreateEntity();
            var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            ecb.AddComponent<EcsTestData>(e);
            // NOTE: ECB playback being bursted does not add to the error message, so we are disabling it for the test
            ecb.m_Data->m_MainThreadChain.m_CanBurstPlayback = false;
            m_Manager.DestroyEntity(e);
            Assert.That(() => ecb.Playback(m_Manager),
                Throws.InvalidOperationException.With.Message.Contains("(" + e.Index + ":" + e.Version + ")"));
            ecb.Dispose();

            var arch = m_Manager.CreateArchetype(typeof(EcsTestData));
            var entities = m_Manager.CreateEntity(arch, 5, World.UpdateAllocator.ToAllocator);
            var destroyedEntity = entities[0];

            var ecb2 = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                ecb2.AddComponent(query, typeof(EcsTestData), EntityQueryCaptureMode.AtRecord);
            }
            // NOTE: ECB playback being bursted does not add to the error message, so we are disabling it for the test
            ecb2.m_Data->m_MainThreadChain.m_CanBurstPlayback = false;

            m_Manager.DestroyEntity(entities[0]);
            Assert.That(() => ecb2.Playback(m_Manager),
                Throws.InvalidOperationException.With.Message.Contains("(" + destroyedEntity.Index + ":" + destroyedEntity.Version + ")"));
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void EntityJournalErrorAdded_DoesNotHaveComponent_EntityCommandBuffer()
        {
            var e = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.RemoveComponent<EcsTestData>(e);
            var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            ecb.SetComponent(e, new EcsTestData { value = 10 });
            // NOTE: ECB playback being bursted does not add to the error message, so we are disabling it for the test
            ecb.m_Data->m_MainThreadChain.m_CanBurstPlayback = false;
            Assert.That(() => ecb.Playback(m_Manager),
                Throws.ArgumentException.With.Message.Contains("(" + e.Index + ":" + e.Version + ")"));

            var arch = m_Manager.CreateArchetype(typeof(EcsTestSharedComp));
            var entities = m_Manager.CreateEntity(arch, 5, World.UpdateAllocator.ToAllocator);
            var removedEntity = entities[0];

            var ecb2 = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp)))
            {
                ecb2.SetSharedComponent(query, new EcsTestSharedComp { value = 10 }, EntityQueryCaptureMode.AtRecord);
            }
            // NOTE: ECB playback being bursted does not add to the error message, so we are disabling it for the test
            ecb2.m_Data->m_MainThreadChain.m_CanBurstPlayback = false;

            m_Manager.RemoveComponent<EcsTestSharedComp>(entities[0]);
            Assert.That(() => ecb2.Playback(m_Manager),
                Throws.ArgumentException.With.Message.Contains("(" + removedEntity.Index + ":" + removedEntity.Version + ")"));
        }
    }
}
#endif
