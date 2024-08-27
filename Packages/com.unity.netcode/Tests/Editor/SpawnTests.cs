using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Entity = Unity.Entities.Entity;
using SystemBase = Unity.Entities.SystemBase;
using WorldSystemFilterFlags = Unity.Entities.WorldSystemFilterFlags;

namespace Unity.NetCode.Tests
{
    public struct Data : IComponentData
    {
        [GhostField]
        public int Value;
    }

    public struct ChildData : IComponentData
    {
        public int Value;
    }

    public class DataConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent<Data>(entity);
        }
    }

    public class ChildDataConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent<ChildData>(entity);
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class UpdateDataSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref Data data) =>
            {
                data.Value++;
            }).Run();
        }
    }

    [TestFixture]
    public partial class SpawnTests
    {
        [DisableAutoCreation]
        [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
        [UpdateInGroup(typeof(GhostSpawnClassificationSystemGroup))]
        [UpdateAfter(typeof(GhostSpawnClassificationSystem))]
        public partial class TestSpawnClassificationSystem : SystemBase
        {
            // Track which entities have been handled by this classification system
            public NativeList<Entity> PredictedEntities;
            protected override void OnCreate()
            {
                RequireForUpdate<GhostSpawnQueue>();
                RequireForUpdate<PredictedGhostSpawnList>();
                PredictedEntities = new NativeList<Entity>(5,Allocator.Persistent);
            }

            protected override void OnDestroy()
            {
                PredictedEntities.Dispose();
            }

            protected override void OnUpdate()
            {
                var spawnListEntity = SystemAPI.GetSingletonEntity<PredictedGhostSpawnList>();
                var spawnListFromEntity = GetBufferLookup<PredictedGhostSpawn>();
                var predictedEntities = PredictedEntities;
                Entities
                    .WithAll<GhostSpawnQueue>()
                    .ForEach((DynamicBuffer<GhostSpawnBuffer> ghosts) =>
                    {
                        var spawnList = spawnListFromEntity[spawnListEntity];
                        for (int i = 0; i < ghosts.Length; ++i)
                        {
                            var ghost = ghosts[i];
                            if (ghost.SpawnType != GhostSpawnBuffer.Type.Predicted || ghost.HasClassifiedPredictedSpawn || ghost.PredictedSpawnEntity != Entity.Null)
                                continue;

                            // Only classify the first item in the list (default system will then catch the rest) and
                            // handle it no matter what (no spawn tick checks etc)
                            if (spawnList.Length > 1)
                            {
                                if (ghost.GhostType == spawnList[0].ghostType)
                                {
                                    ghost.PredictedSpawnEntity = spawnList[0].entity;
                                    ghost.HasClassifiedPredictedSpawn = true;
                                    spawnList[0] = spawnList[spawnList.Length-1];
                                    spawnList.RemoveAt(spawnList.Length - 1);
                                    predictedEntities.Add(ghost.PredictedSpawnEntity);
                                    ghosts[i] = ghost;
                                    break;
                                }
                            }
                        }
                    }).Run();
            }
        }

        /* Set up 2 prefabs with a predicted ghost and interpolated ghost
         *  - Verify spawning the predicted one on the client works as expected
         *  - Verify server spawning interpolated ghosts works as well
         *  - Verify the prefabs on the clients have the right components set up
         *  - Verify a locally spawned predicted ghost is properly synchronized to other clients.
         *  - Uses default spawn classification system
         */
        [Test]
        public void PredictSpawnGhost()
        {
            const int PREDICTED = 0;
            const int INTERPOLATED = 1;
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(UpdateDataSystem));

                // Predicted ghost
                var predictedGhostGO = new GameObject("PredictedGO");
                predictedGhostGO.AddComponent<TestNetCodeAuthoring>().Converter = new DataConverter();
                var ghostConfig = predictedGhostGO.AddComponent<GhostAuthoringComponent>();
                ghostConfig.DefaultGhostMode = GhostMode.OwnerPredicted;
                ghostConfig.SupportedGhostModes = GhostModeMask.Predicted;
                ghostConfig.HasOwner = true;

                // One child nested on predicted ghost
                var predictedGhostGOChild = new GameObject("PredictedGO-Child");
                predictedGhostGOChild.AddComponent<TestNetCodeAuthoring>().Converter = new ChildDataConverter();
                predictedGhostGOChild.transform.parent = predictedGhostGO.transform;

                // Interpolated ghost
                var interpolatedGhostGO = new GameObject("InterpolatedGO");
                interpolatedGhostGO.AddComponent<TestNetCodeAuthoring>().Converter = new DataConverter();
                ghostConfig = interpolatedGhostGO.AddComponent<GhostAuthoringComponent>();
                ghostConfig.DefaultGhostMode = GhostMode.Interpolated;
                ghostConfig.SupportedGhostModes = GhostModeMask.Interpolated;

                Assert.IsTrue(testWorld.CreateGhostCollection(predictedGhostGO, interpolatedGhostGO));

                testWorld.CreateWorlds(true, 1);

                float frameTime = 1.0f / 60.0f;
                testWorld.Connect(frameTime);
                testWorld.GoInGame();

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                // Predictively spawn ghost on client
                var prefabsListQuery = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(NetCodeTestPrefabCollection));
                var prefabList = prefabsListQuery.ToEntityArray(Allocator.Temp)[0];
                var prefabs = testWorld.ClientWorlds[0].EntityManager.GetBuffer<NetCodeTestPrefab>(prefabList);
                var predictedPrefab = prefabs[PREDICTED].Value;
                var clientEntity = testWorld.ClientWorlds[0].EntityManager.Instantiate(predictedPrefab);

                // Verify you've instantiated the predict spawn version of the prefab
                Assert.IsTrue(testWorld.ClientWorlds[0].EntityManager.HasComponent<PredictedGhostSpawnRequest>(clientEntity));

                // Verify the predicted ghost has a linked entity (the child on the GO)
                var linkedEntities = testWorld.ClientWorlds[0].EntityManager.GetBuffer<LinkedEntityGroup>(clientEntity);
                Assert.AreEqual(2, linkedEntities.Length);

                // server spawns normal ghost for the client spawned one
                prefabsListQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(NetCodeTestPrefabCollection));
                prefabList = prefabsListQuery.ToEntityArray(Allocator.Temp)[0];
                prefabs = testWorld.ServerWorld.EntityManager.GetBuffer<NetCodeTestPrefab>(prefabList);
                Assert.IsFalse(testWorld.ServerWorld.EntityManager.HasComponent<PredictedGhostSpawnRequest>(prefabs[PREDICTED].Value));
                testWorld.ServerWorld.EntityManager.Instantiate(prefabs[PREDICTED].Value);


                for (int i = 0; i < 5; ++i)
                    testWorld.Tick(frameTime);

                //The request has been consumed.
                Assert.IsFalse(testWorld.ClientWorlds[0].EntityManager.HasComponent<PredictedGhostSpawnRequest>(clientEntity));

                // Verify ghost field data has been updated on the clients instance, and we only have one entity spawned
                var compQuery = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Data>());
                var clientData = compQuery.ToComponentDataArray<Data>(Allocator.Temp);
                Assert.AreEqual(1, clientData.Length);
                Assert.IsTrue(clientData[0].Value > 1);

                // server spawns normal interpolated ghost
                prefabsListQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(NetCodeTestPrefabCollection));
                prefabList = prefabsListQuery.ToEntityArray(Allocator.Temp)[0];
                prefabs = testWorld.ServerWorld.EntityManager.GetBuffer<NetCodeTestPrefab>(prefabList);
                testWorld.ServerWorld.EntityManager.Instantiate(prefabs[INTERPOLATED].Value);
                Assert.IsFalse(testWorld.ServerWorld.EntityManager.HasComponent<PredictedGhostSpawnRequest>(prefabs[INTERPOLATED].Value));

                for (int i = 0; i < 5; ++i)
                    testWorld.Tick(frameTime);

                // Verify ghost field data has been updated on the clients instance for the predicted entity we spawned
                compQuery = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new ComponentType[] { typeof(Data), typeof(PredictedGhost) },
                });
                compQuery.ToComponentDataArray<Data>(Allocator.Temp);
                Assert.AreEqual(1, clientData.Length);
                Assert.IsTrue(clientData[0].Value > 1);

                // Verify the interpolated ghost has also propagated to the client and updated
                compQuery = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new ComponentType[] { typeof(Data) },
                    None = new ComponentType[] { typeof(PredictedGhost) }
                });
                compQuery.ToComponentDataArray<Data>(Allocator.Temp);
                Assert.AreEqual(1, clientData.Length);
                Assert.IsTrue(clientData[0].Value > 1);

                // On client there are two predicted prefabs, one for predicted spawning and one normal server spawn
                var queryDesc = new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        typeof(Data),
                        typeof(Prefab),
                        typeof(PredictedGhost)
                    },
                    Options = EntityQueryOptions.IncludePrefab
                };
                compQuery = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(queryDesc);
                Assert.AreEqual(1, compQuery.CalculateEntityCount());

                // Verify children are correctly replicated in the prefab copy.
                // Iterate though the LinkedEntityGroup of each predicted prefab
                // check the child entity listed there and verify it's linking back to the parent
                var entityPrefabs = compQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entityPrefabs.Length; ++i)
                {
                    var parentEntity = entityPrefabs[i];
                    var links = testWorld.ClientWorlds[0].EntityManager.GetBuffer<LinkedEntityGroup>(parentEntity);
                    Assert.AreEqual(2, links.Length);
                    var child = links[1].Value;
                    var parentLink = testWorld.ClientWorlds[0].EntityManager.GetComponentData<Parent>(child).Value;
                    Assert.AreEqual(parentEntity, parentLink);
                }

                // Server will have 2 prefabs (interpolated, predicted)
                compQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(queryDesc);
                Assert.AreEqual(2, compQuery.CalculateEntityCount());
            }
        }

        [Test]
        public void CustomSpawnClassificationSystem()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(TestSpawnClassificationSystem));

                // Predicted ghost
                var predictedGhostGO = new GameObject("PredictedGO");
                predictedGhostGO.AddComponent<TestNetCodeAuthoring>().Converter = new DataConverter();
                var ghostConfig = predictedGhostGO.AddComponent<GhostAuthoringComponent>();
                ghostConfig.DefaultGhostMode = GhostMode.OwnerPredicted;
                ghostConfig.SupportedGhostModes = GhostModeMask.Predicted;
                ghostConfig.HasOwner = true;

                Assert.IsTrue(testWorld.CreateGhostCollection(predictedGhostGO));

                testWorld.CreateWorlds(true, 1);

                float frameTime = 1.0f / 60.0f;
                testWorld.Connect(frameTime);
                testWorld.GoInGame();

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                // Predictively spawn ghost on client
                var prefabsListQuery = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(NetCodeTestPrefabCollection));
                var prefabList = prefabsListQuery.ToEntityArray(Allocator.Temp)[0];
                var prefabs = testWorld.ClientWorlds[0].EntityManager.GetBuffer<NetCodeTestPrefab>(prefabList);
                var predictedPrefab = prefabs[0].Value;

                // Instantiate two ghosts on the same frame
                testWorld.ClientWorlds[0].EntityManager.Instantiate(predictedPrefab);
                testWorld.ClientWorlds[0].EntityManager.Instantiate(predictedPrefab);

                // Server spawns normal ghost for the client spawned one
                prefabsListQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(NetCodeTestPrefabCollection));
                prefabList = prefabsListQuery.ToEntityArray(Allocator.Temp)[0];
                prefabs = testWorld.ServerWorld.EntityManager.GetBuffer<NetCodeTestPrefab>(prefabList);

                // Server also instantiates twice
                testWorld.ServerWorld.EntityManager.Instantiate(prefabs[0].Value);
                testWorld.ServerWorld.EntityManager.Instantiate(prefabs[0].Value);

                for (int i = 0; i < 5; ++i)
                    testWorld.Tick(frameTime);

                // Verify the custom spawn classification system ran instead of the default only for the first spawn
                var classifiedGhosts = testWorld.ClientWorlds[0].GetExistingSystemManaged<TestSpawnClassificationSystem>();
                Assert.AreEqual(1, classifiedGhosts.PredictedEntities.Length);

                // Verify we have the right amount of total ghosts spawned
                var compQuery = testWorld.ClientWorlds[0].EntityManager
                    .CreateEntityQuery(typeof(Data));
                Assert.AreEqual(2, compQuery.CalculateEntityCount());
            }
        }
    }
}
