using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Profiling;

namespace Unity.NetCode.Tests
{
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    partial class TestChangeFilter : SystemBase
    {
        public NativeHashMap<Entity, int> changedEntities;
        public NativeHashMap<ComponentType, int> changedComponents;
        public NativeList<ComponentType> checkForChanges;
        private uint lastSystemVersion;
        public NativeList<EntityQuery> queries;
        protected override void OnCreate()
        {
            changedEntities = new NativeHashMap<Entity, int>(100, Allocator.Persistent);
            checkForChanges = new NativeList<ComponentType>(Allocator.Persistent);
            changedComponents = new NativeHashMap<ComponentType, int>(100, Allocator.Persistent);
            queries = new NativeList<EntityQuery>(Allocator.Temp);
        }

        public void BuildQueries()
        {
            //Can't use the correct IgnoreEnableComponentState with change filtering,
            //it will incorrectly report entities as changed all the time.
            queries.Clear();
            foreach (var componentType in checkForChanges)
            {
                queries.Add(GetEntityQuery(new EntityQueryDesc[]
                {
                    new EntityQueryDesc()
                    {
                        All = new []{componentType},
                        Options = EntityQueryOptions.IgnoreComponentEnabledState
                    }
                }));
            }
        }

        protected override void OnDestroy()
        {
            checkForChanges.Dispose();
            changedEntities.Dispose();
            changedComponents.Dispose();
        }

        protected override void OnUpdate()
        {
            var entityHandle = GetEntityTypeHandle();
            for (var index = 0; index < checkForChanges.Length; index++)
            {
                var componentType = checkForChanges[index];
                var query = queries[index];
                var chunks = query.ToArchetypeChunkArray(Allocator.Temp);
                var typeHandle = GetDynamicComponentTypeHandle(componentType);
                bool hasChangedChunks = false;
                foreach (var chunk in chunks)
                {
                    if (!chunk.DidChange(ref typeHandle, LastSystemVersion))
                        continue;
                    hasChangedChunks = true;
                    var entities = chunk.GetNativeArray(entityHandle);
                    for (int i = 0; i < entities.Length; ++i)
                    {
                        changedEntities.TryGetValue(entities[i], out var timeChanged);
                        changedEntities[entities[i]] = timeChanged + 1;
                    }
                }

                if (hasChangedChunks)
                {
                    changedComponents.TryGetValue(componentType, out var count);
                    changedComponents[componentType] = count + 1;
                }
            }
        }
    }
    [DisableAutoCreation]
    [CreateBefore(typeof(DefaultVariantSystemGroup))]
    [UpdateInGroup(typeof(DefaultVariantSystemGroup))]
    partial class TestChangeFilterDefaultConfig : DefaultVariantSystemBase
    {
        protected override void RegisterDefaultVariants(Dictionary<ComponentType, Rule> defaultVariants)
        {
            defaultVariants.Add(new ComponentType(typeof(EnableableComponent_0)), Rule.OnlyChildren(typeof(EnableableComponent_0)));
            defaultVariants.Add(new ComponentType(typeof(EnableableComponent_1)), Rule.OnlyChildren(typeof(EnableableComponent_1)));
            defaultVariants.Add(new ComponentType(typeof(EnableableComponent_2)), Rule.OnlyChildren(typeof(EnableableComponent_2)));
            defaultVariants.Add(new ComponentType(typeof(EnableableComponent_3)), Rule.OnlyChildren(typeof(EnableableComponent_3)));
            defaultVariants.Add(new ComponentType(typeof(EnableableBuffer_0)), Rule.OnlyChildren(typeof(EnableableBuffer_0)));
            defaultVariants.Add(new ComponentType(typeof(EnableableBuffer_1)), Rule.OnlyChildren(typeof(EnableableBuffer_1)));
            defaultVariants.Add(new ComponentType(typeof(EnableableBuffer_2)), Rule.OnlyChildren(typeof(EnableableBuffer_2)));
        }
    }

    public class ChangeFilterTests
    {
        [TestCase(1)]
        [TestCase(10)]
        public void RestoreFromBackupDoesNotAffecUnchangeComponents(int entityCount)
        {
            using var testWorld = new NetCodeTestWorld();
            testWorld.Bootstrap(true, typeof(TestChangeFilterDefaultConfig), typeof(TestChangeFilter));
            testWorld.CreateWorlds(true, 1);
            testWorld.CreateGhostCollection();
            var prefab = CreatePrefab(testWorld.ServerWorld.EntityManager);
            CreatePrefab(testWorld.ClientWorlds[0].EntityManager);
            testWorld.Connect(1f / 60f, 8);
            testWorld.GoInGame();

            var dt = 1f / 60f;
            for (int i = 0; i < 32; ++i)
                testWorld.Tick(dt);

            var testFilter = testWorld.ClientWorlds[0].GetOrCreateSystemManaged<TestChangeFilter>();
            //All components are replicated and use the default variant for serialisation.
            testFilter.checkForChanges = new NativeList<ComponentType>(Allocator.Temp);
            testFilter.checkForChanges.Add(ComponentType.ReadOnly<LocalTransform>());
            testFilter.checkForChanges.Add(ComponentType.ReadOnly<EnableableComponent_0>());
            testFilter.checkForChanges.Add(ComponentType.ReadOnly<EnableableComponent_1>());
            testFilter.checkForChanges.Add(ComponentType.ReadOnly<EnableableComponent_2>());
            testFilter.checkForChanges.Add(ComponentType.ReadOnly<EnableableComponent_3>());
            testFilter.checkForChanges.Add(ComponentType.ReadOnly<EnableableBuffer_0>());
            testFilter.checkForChanges.Add(ComponentType.ReadOnly<EnableableBuffer_1>());
            testFilter.checkForChanges.Add(ComponentType.ReadOnly<EnableableBuffer_2>());
            testFilter.BuildQueries();

            NativeList<Entity> serverEntities = new NativeList<Entity>(Allocator.Temp);
            for (int i = 0; i < entityCount; ++i)
                serverEntities.Add(testWorld.ServerWorld.EntityManager.Instantiate(prefab));

            //Spawn all entities, reach a stable state.
            for (int i = 0; i < 32; ++i)
                testWorld.Tick(dt);

            //Expectation: all entities has changed, all components has changed once!
            //There is no data changes, so apart from the first spawn no change filter should trigger
            Assert.AreEqual(6*entityCount, testFilter.changedEntities.Count);
            Assert.AreEqual(testFilter.checkForChanges.Length, testFilter.changedComponents.Count, "All components must have been changed at least once because of the new spawn");
            //Checks that also partial ticks does not invalidate the filters
            testFilter.changedEntities.Clear();
            testFilter.changedComponents.Clear();
            for (int i = 0; i < 128; ++i)
                testWorld.Tick(dt/3f);


            testWorld.ClientWorlds[0].EntityManager.CompleteAllTrackedJobs();
            if (testFilter.changedComponents.Count != 0)
            {
                //report the changed components and the changed entities to help understand what happens
                var sb = new StringBuilder();
                sb.Append($"Expecting no component changes when restoring from backup, but {testFilter.changedComponents.Count} components has their version bumped.\n");
                sb.Append("\nChanged Components:\n");
                foreach (var component in testFilter.changedComponents)
                    sb.Append(component.Key);
                sb.Append("\nChanged Entities:\n");
                foreach (var entity in testFilter.changedEntities)
                    sb.Append(entity.Key);
                Assert.Fail(sb.ToString());
            }
            //if data is modified by the server, only the change component should be reported as modified.
            //Also the modified component should be reported has changed only once
            ChangeComponentValue<EnableableComponent_1>(testWorld.ServerWorld);
            ChangeComponentValue<EnableableComponent_2>(testWorld.ServerWorld);
            ChangeBuffer<EnableableBuffer_0>(testWorld.ServerWorld);
            ChangeBuffer<EnableableBuffer_1>(testWorld.ServerWorld, 8);
            testFilter.changedEntities.Clear();
            testFilter.changedComponents.Clear();
            for (int i = 0; i < 128; ++i)
                testWorld.Tick(dt/3f);
            testWorld.ServerWorld.EntityManager.CompleteAllTrackedJobs();
            testWorld.ClientWorlds[0].EntityManager.CompleteAllTrackedJobs();

            Assert.AreEqual(4, testFilter.changedComponents.Count, "Expect only EnableableComponent_1,EnableableComponent_2, EnableableBuffer_0, EnableableBuffer_1 changed");
            Assert.AreEqual(6*entityCount, testFilter.changedEntities.Count, "Expected all entities has some component changed");
            Assert.IsTrue(testFilter.changedComponents.ContainsKey(ComponentType.ReadOnly<EnableableComponent_1>()), "Expect EnableableComponent_1 changed");
            Assert.IsTrue(testFilter.changedComponents.ContainsKey(ComponentType.ReadOnly<EnableableComponent_2>()), "Expect EnableableComponent_2 changed");
            Assert.IsTrue(testFilter.changedComponents.ContainsKey(ComponentType.ReadOnly<EnableableBuffer_0>()), "Expect EnableableBuffer_0 changed");
            Assert.IsTrue(testFilter.changedComponents.ContainsKey(ComponentType.ReadOnly<EnableableBuffer_1>()), "Expect EnableableBuffer_1 changed");
            //In presence of partial snapshot, all depends how the server send the data.
            //We can check the == 1 when we use a very small number of entities.
            Assert.GreaterOrEqual(testFilter.changedComponents[ComponentType.ReadOnly<EnableableComponent_1>()], 1, "The componnet should have been reported has changed at least once");
            Assert.GreaterOrEqual(testFilter.changedComponents[ComponentType.ReadOnly<EnableableComponent_2>()], 1, "The componnet should have been reported has changed at least once");
            Assert.GreaterOrEqual(testFilter.changedComponents[ComponentType.ReadOnly<EnableableBuffer_0>()], 1, "The componnet should have been reported has changed at least once");
            Assert.GreaterOrEqual(testFilter.changedComponents[ComponentType.ReadOnly<EnableableBuffer_1>()], 1, "The componnet should have been reported has changed at least once");
            //If nothing changes here, no changes should be reported
            testFilter.changedEntities.Clear();
            testFilter.changedComponents.Clear();
            for (int i = 0; i < 32; ++i)
                testWorld.Tick(dt/3f);
            Assert.AreEqual(0, testFilter.changedComponents.Count, "No component should change, if server does not change the data again");

            var ghostQuery = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(GhostInstance));
            var rooGhosts = ghostQuery.ToEntityArray(Allocator.Temp);
            //Changing parent only component does not change child.
            ChangeComponentValue<EnableableComponent_1>(testWorld.ServerWorld, FilterEntity.OnlyParent, iteration:1);
            testFilter.changedEntities.Clear();
            testFilter.changedComponents.Clear();
            for (int i = 0; i < 128; ++i)
                testWorld.Tick(dt/3f);
            testWorld.ServerWorld.EntityManager.CompleteAllTrackedJobs();
            testWorld.ClientWorlds[0].EntityManager.CompleteAllTrackedJobs();
            Assert.AreEqual(entityCount, testFilter.changedEntities.Count, "Only the root should have changed");
            Assert.IsTrue(testFilter.changedEntities.ContainsKey(rooGhosts[0]));
            Assert.AreEqual(1, testFilter.changedComponents.Count, "Expect only EnableableComponent_1 is changed");
            Assert.IsTrue(testFilter.changedComponents.ContainsKey(ComponentType.ReadOnly<EnableableComponent_1>()));
            Assert.GreaterOrEqual(testFilter.changedComponents[ComponentType.ReadOnly<EnableableComponent_1>()], 1);

            //Changing component only on child entities does not invalidate the parent entity
            ChangeComponentValue<EnableableComponent_1>(testWorld.ServerWorld, FilterEntity.OnlyChildren, iteration:2);
            ChangeComponentValue<EnableableComponent_2>(testWorld.ServerWorld, FilterEntity.OnlyChildren, iteration:2);
            ChangeComponentValue<EnableableComponent_3>(testWorld.ServerWorld, FilterEntity.OnlyChildren, iteration:2);
            ChangeBuffer<EnableableBuffer_0>(testWorld.ServerWorld, filterEntity:FilterEntity.OnlyChildren, iteration:2);
            ChangeBuffer<EnableableBuffer_1>(testWorld.ServerWorld, 8, FilterEntity.OnlyChildren, iteration:2);
            testFilter.changedEntities.Clear();
            testFilter.changedComponents.Clear();
            for (int i = 0; i < 128; ++i)
                testWorld.Tick(dt/3f);
            testWorld.ServerWorld.EntityManager.CompleteAllTrackedJobs();
            testWorld.ClientWorlds[0].EntityManager.CompleteAllTrackedJobs();
            Assert.AreEqual(5, testFilter.changedComponents.Count);
            Assert.AreEqual(5*entityCount, testFilter.changedEntities.Count);
            Assert.IsFalse(testFilter.changedEntities.ContainsKey(rooGhosts[0]));
            Assert.IsTrue(testFilter.changedComponents.ContainsKey(ComponentType.ReadOnly<EnableableComponent_1>()));
            Assert.IsTrue(testFilter.changedComponents.ContainsKey(ComponentType.ReadOnly<EnableableComponent_2>()));
            Assert.IsTrue(testFilter.changedComponents.ContainsKey(ComponentType.ReadOnly<EnableableComponent_3>()));
            Assert.IsTrue(testFilter.changedComponents.ContainsKey(ComponentType.ReadOnly<EnableableBuffer_0>()));
            Assert.IsTrue(testFilter.changedComponents.ContainsKey(ComponentType.ReadOnly<EnableableBuffer_1>()));
            Assert.GreaterOrEqual(testFilter.changedComponents[ComponentType.ReadOnly<EnableableComponent_1>()], 1);
            Assert.GreaterOrEqual(testFilter.changedComponents[ComponentType.ReadOnly<EnableableComponent_2>()], 1);
            Assert.GreaterOrEqual(testFilter.changedComponents[ComponentType.ReadOnly<EnableableComponent_3>()], 1);
            Assert.GreaterOrEqual(testFilter.changedComponents[ComponentType.ReadOnly<EnableableBuffer_0>()], 1);
            Assert.GreaterOrEqual(testFilter.changedComponents[ComponentType.ReadOnly<EnableableBuffer_1>()], 1);

            //Changing component on a specific entity, only affect the entities and components for that chunk.
            var spawnMap = testWorld.GetSingleton<SpawnedGhostEntityMap>(testWorld.ClientWorlds[0]);
            for (var entIndex = 0; entIndex < serverEntities.Length; entIndex++)
            {
                var ent = serverEntities[entIndex];
                var serverGroup = testWorld.ServerWorld.EntityManager.GetBuffer<LinkedEntityGroup>(ent).ToNativeArray(Allocator.Temp);
                var ghost = testWorld.ServerWorld.EntityManager.GetComponentData<GhostInstance>(ent);
                spawnMap.Value.TryGetValue(new SpawnedGhost(ghost.ghostId,ghost.spawnTick), out var clientRoot);
                var clientGroup = testWorld.ClientWorlds[0].EntityManager.GetBuffer<LinkedEntityGroup>(clientRoot).ToNativeArray(Allocator.Temp);
                for (var index = 0; index < serverGroup.Length; index++)
                {
                    var entity = serverGroup[index];
                    var clientEntity = clientGroup[index];
                    TestSingleComponentChange<EnableableComponent_0>(testWorld, entity.Value, clientEntity.Value);
                    TestSingleComponentChange<EnableableComponent_1>(testWorld, entity.Value, clientEntity.Value);
                    TestSingleComponentChange<EnableableComponent_2>(testWorld, entity.Value, clientEntity.Value);
                    TestSingleComponentChange<EnableableComponent_3>(testWorld, entity.Value, clientEntity.Value);
                    TestSingleBuffChange<EnableableBuffer_0>(testWorld, entity.Value, clientEntity.Value);
                    TestSingleBuffChange<EnableableBuffer_1>(testWorld, entity.Value, clientEntity.Value);
                    TestSingleBuffChange<EnableableBuffer_2>(testWorld, entity.Value, clientEntity.Value);
                }
            }
        }

        private static void TestSingleComponentChange<T>(NetCodeTestWorld testWorld, Entity entity, Entity clientEntity)
            where T: unmanaged, IComponentData, IEnableableComponent, IComponentValue
        {
            if (!testWorld.ServerWorld.EntityManager.HasComponent<T>(entity))
                return;
            var value = new T();
            value.SetValue(entity.Index);
            testWorld.ServerWorld.EntityManager.SetComponentData(entity, value);
            TestLoop(testWorld, clientEntity, ComponentType.ReadOnly<T>());
            testWorld.ServerWorld.EntityManager.SetComponentEnabled<T>(entity, false);
            TestLoop(testWorld, clientEntity, ComponentType.ReadOnly<T>());
            testWorld.ServerWorld.EntityManager.SetComponentEnabled<T>(entity, true);
            TestLoop(testWorld, clientEntity, ComponentType.ReadOnly<T>());
        }
        private static void TestSingleBuffChange<T>(NetCodeTestWorld testWorld, Entity entity, Entity clientEntity)
            where T: unmanaged, IBufferElementData, IEnableableComponent, IComponentValue
        {
            if (!testWorld.ServerWorld.EntityManager.HasComponent<T>(entity))
                return;
            var buffer = testWorld.ServerWorld.EntityManager.GetBuffer<T>(entity);
            buffer.ResizeUninitialized(10);
            TestLoop(testWorld, clientEntity, ComponentType.ReadOnly<T>());
            buffer = testWorld.ServerWorld.EntityManager.GetBuffer<T>(entity);
            for (int i = 0; i < buffer.Length; ++i)
                buffer.ElementAt(i).SetValue(entity.Index);
            TestLoop(testWorld, clientEntity, ComponentType.ReadOnly<T>());
        }

        private static void TestLoop(NetCodeTestWorld testWorld, Entity entity, ComponentType componentType)
        {
            var testFilter = testWorld.ClientWorlds[0].GetExistingSystemManaged<TestChangeFilter>();
            testFilter.changedEntities.Clear();
            testFilter.changedComponents.Clear();
            for (int i = 0; i < 8; ++i)
            {
                testWorld.Tick(1f/20f);
                testWorld.ServerWorld.EntityManager.CompleteAllTrackedJobs();
                testWorld.ClientWorlds[0].EntityManager.CompleteAllTrackedJobs();
            }
            Assert.IsTrue(testFilter.changedEntities.ContainsKey(entity), $"Expect entity {entity} changed");
            Assert.IsTrue(testFilter.changedComponents.ContainsKey(componentType), $"Expect component {componentType} changed for entity {entity}");
            Assert.AreEqual(1, testFilter.changedComponents.Count, $"Expected only {componentType} changed.");
            var expectedTouchedEntities = testWorld.ClientWorlds[0].EntityManager.GetChunk(entity).Count;
            Assert.AreEqual(expectedTouchedEntities, testFilter.changedEntities.Count, "Expected only one entities that has the same archetype are affected");
            Assert.AreEqual(testFilter.changedComponents[componentType], 1, $"Expected {componentType} changed only once");
        }

        enum FilterEntity
        {
            BothParentAndChildren,
            OnlyParent,
            OnlyChildren
        }

        private void ChangeComponentValue<T>(World world,
            FilterEntity filterEntity= FilterEntity.BothParentAndChildren,
            int iteration = 0) where T: unmanaged, IComponentData, IComponentValue
        {
            var builder = new EntityQueryBuilder(Allocator.Temp);
            builder.WithAll<T>();
            if(filterEntity == FilterEntity.OnlyParent)
                builder.WithAll<GhostInstance>();
            else if (filterEntity == FilterEntity.OnlyChildren)
                builder.WithAll<GhostChildEntity>();
            using var ghosts = world.EntityManager.CreateEntityQuery(builder);
            using var chunks = ghosts.ToArchetypeChunkArray(Allocator.Temp);
            var t1 = world.EntityManager.GetComponentTypeHandle<T>(false);
            foreach (var chunk in chunks)
            {
                unsafe
                {
                    var c1 = (T*)chunk.GetNativeArray(ref t1).GetUnsafePtr();
                    for (int i = 0; i < chunk.Count; ++i)
                        c1[i].SetValue(typeof(T).GetHashCode() * (i+i) + iteration*1000);
                }
            }
        }
        private void ChangeBuffer<T>(World world, int newLen = -1,
            FilterEntity filterEntity= FilterEntity.BothParentAndChildren, int iteration=0) where T: unmanaged, IBufferElementData, IComponentValue
        {
            var builder = new EntityQueryBuilder(Allocator.Temp);
            builder.WithAll<T>();
            if(filterEntity == FilterEntity.OnlyParent)
                builder.WithAll<GhostInstance>();
            else if (filterEntity == FilterEntity.OnlyChildren)
                builder.WithAll<GhostChildEntity>();
            using var ghosts = world.EntityManager.CreateEntityQuery(builder);
            using var chunks = ghosts.ToArchetypeChunkArray(Allocator.Temp);
            var t1 = world.EntityManager.GetBufferTypeHandle<T>(false);
            foreach (var chunk in chunks)
            {
                var bufferAccessor = chunk.GetBufferAccessor(ref t1);
                for (int i = 0; i < chunk.Count; ++i)
                {
                    if(newLen >= 0)
                        bufferAccessor[i].ResizeUninitialized(newLen);
                    var len = bufferAccessor[i].Length;
                    for (int k = 0; k < len; ++k)
                        bufferAccessor[i].ElementAt(k).SetValue(typeof(T).GetHashCode() * (i + i) + iteration*1000);
                }
            }
        }

        private static Entity CreatePrefab(EntityManager entityManager)
        {
            //This create a ghost with 5 child entites, of which 3 in the same chunk, and other 2 in distinct chunks
            //for an an overall use of 4 archetypes per ghost.
            var prefab = entityManager.CreateEntity();
            entityManager.AddComponentData(prefab, new EnableableComponent_0{value = 1});
            entityManager.AddComponentData(prefab, new EnableableComponent_1{value = 2});
            entityManager.AddComponentData(prefab, new EnableableComponent_2{value = 3});
            entityManager.AddComponentData(prefab, new EnableableComponent_3{value = 4});
            entityManager.AddComponentData(prefab, LocalTransform.Identity);
            entityManager.AddComponent<GhostOwner>(prefab);
            entityManager.AddBuffer<EnableableBuffer_0>(prefab).ResizeUninitialized(3);
            entityManager.AddBuffer<EnableableBuffer_1>(prefab).ResizeUninitialized(4);
            entityManager.AddBuffer<EnableableBuffer_2>(prefab).ResizeUninitialized(5);
            entityManager.AddBuffer<LinkedEntityGroup>(prefab);
            entityManager.GetBuffer<LinkedEntityGroup>(prefab).Add(prefab);
            for (int i = 0; i < 5; ++i)
            {
                var child = entityManager.CreateEntity();
                entityManager.AddComponent<Prefab>(child);
                if (i < 3)
                {
                    entityManager.AddComponentData(child, new EnableableComponent_1{value = 10 + i});
                    entityManager.AddComponentData(child, new EnableableComponent_2{value = 20 + i});
                    entityManager.AddComponentData(child, new EnableableComponent_3{value = 30 + i});
                    entityManager.AddBuffer<EnableableBuffer_0>(child).ResizeUninitialized(3);
                    entityManager.AddBuffer<EnableableBuffer_1>(child).ResizeUninitialized(4);
                }
                else if (i == 3)
                {
                    entityManager.AddComponentData(child, new EnableableComponent_1{value = 10 + i});
                    entityManager.AddComponentData(child, new EnableableComponent_2{value = 20 + i});
                }
                else if (i == 4)
                {
                    entityManager.AddComponentData(child, new EnableableComponent_0{value = 10 + i});
                    entityManager.AddComponentData(child, new EnableableComponent_1{value = 30 + i});
                    entityManager.AddBuffer<EnableableBuffer_0>(child).ResizeUninitialized(3);
                }
                entityManager.GetBuffer<LinkedEntityGroup>(prefab).Add(child);
            }

            GhostPrefabCreation.ConvertToGhostPrefab(entityManager, prefab, new GhostPrefabCreation.Config
            {
                Name = "TestPrefab",
                Importance = 0,
                SupportedGhostModes = GhostModeMask.Predicted,
                DefaultGhostMode = GhostMode.Predicted,
                OptimizationMode = GhostOptimizationMode.Dynamic,
                UsePreSerialization = false
            });
            return prefab;
        }
    }
}
