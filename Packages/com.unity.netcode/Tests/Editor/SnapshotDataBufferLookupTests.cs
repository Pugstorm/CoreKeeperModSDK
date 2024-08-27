using NUnit.Framework;
using NUnit.Framework.Internal;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.NetCode.Tests.Editor
{
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostSpawnClassificationSystemGroup))]
    [UpdateAfter(typeof(GhostSpawnClassificationSystem))]
    [CreateAfter(typeof(GhostCollectionSystem))]
    [CreateAfter(typeof(GhostReceiveSystem))]
    partial struct TestSpawnBufferClassifier : ISystem
    {
        private LowLevel.SnapshotDataLookupHelper lookupHelper;
        private BufferLookup<SnapshotDataBuffer> snapshotBufferLookup;
        public int ClassifiedPredictedSpawns { get; private set; }
        public void OnCreate(ref SystemState state)
        {
            lookupHelper = new LowLevel.SnapshotDataLookupHelper(ref state,
                SystemAPI.GetSingletonEntity<GhostCollection>(),
                SystemAPI.GetSingletonEntity<SpawnedGhostEntityMap>());
            snapshotBufferLookup = state.GetBufferLookup<SnapshotDataBuffer>(true);
            state.RequireForUpdate<GhostCollection>();
            state.RequireForUpdate<GhostSpawnQueue>();
            state.RequireForUpdate<PredictedGhostSpawn>();
            state.RequireForUpdate<NetworkId>();
        }

        public void OnUpdate(ref SystemState state)
        {
            lookupHelper.Update(ref state);
            snapshotBufferLookup.Update(ref state);
            var snapshotLookup = lookupHelper.CreateSnapshotBufferLookup();
            var predictedSpawnList = SystemAPI.GetSingletonBuffer<PredictedGhostSpawn>(true);

            foreach (var (spawnBuffer, spawnDataBuffer)
                     in SystemAPI.Query<DynamicBuffer<GhostSpawnBuffer>, DynamicBuffer<SnapshotDataBuffer>>()
                         .WithAll<GhostSpawnQueue>())
            {
                for (int i = 0; i < spawnBuffer.Length; ++i)
                {
                    UnityEngine.Debug.LogWarning($"Checking ghost {i}");
                    var ghost = spawnBuffer[i];
                    Assert.IsTrue(snapshotLookup.HasGhostOwner(ghost));
                    Assert.IsTrue(snapshotLookup.HasComponent<LocalTransform>(ghost.GhostType));
                    Assert.IsTrue(snapshotLookup.HasComponent<SomeData>(ghost.GhostType));
                    Assert.IsTrue(snapshotLookup.HasBuffer<GhostGenTest_Buffer>(ghost.GhostType));
                    Assert.AreEqual(1, snapshotLookup.GetGhostOwner(ghost, spawnDataBuffer));
                    Assert.IsTrue(snapshotLookup.TryGetComponentDataFromSpawnBuffer(ghost, spawnDataBuffer, out LocalTransform transform));
                    Assert.IsTrue(math.distance(new float3(40f, 10f, 90f), transform.Position) < 1.0e-4f);
                    Assert.IsTrue(snapshotLookup.TryGetComponentDataFromSpawnBuffer(ghost, spawnDataBuffer, out SomeData someData));
                    Assert.AreEqual(10000, someData.Value);
                    Assert.IsTrue(snapshotLookup.TryGetComponentDataFromSpawnBuffer(ghost, spawnDataBuffer, out GhostOwner ownerComponent));
                    Assert.AreEqual(1, ownerComponent.NetworkId);

                    if (ghost.SpawnType != GhostSpawnBuffer.Type.Predicted || ghost.HasClassifiedPredictedSpawn || ghost.PredictedSpawnEntity != Entity.Null)
                        continue;
                    for(int j=0;j<predictedSpawnList.Length;++j)
                    {
                        if (predictedSpawnList[j].ghostType == spawnBuffer[i].GhostType)
                        {
                            Assert.IsTrue(snapshotBufferLookup.HasBuffer(predictedSpawnList[j].entity));
                            var historyBuffer = snapshotBufferLookup[predictedSpawnList[j].entity];
                            Assert.IsTrue(snapshotLookup.TryGetComponentDataFromSnapshotHistory(ghost.GhostType, historyBuffer, out LocalTransform predictedTx));
                            Assert.AreEqual(transform.Position, predictedTx.Position);
                            Assert.IsTrue(snapshotLookup.TryGetComponentDataFromSnapshotHistory(ghost.GhostType, historyBuffer, out SomeData predSomeData));
                            Assert.AreEqual(someData.Value, predSomeData.Value);
                            Assert.IsTrue(snapshotLookup.TryGetComponentDataFromSnapshotHistory(ghost.GhostType, historyBuffer, out GhostOwner predOwnerComp));
                            Assert.AreEqual(ownerComponent.NetworkId, predOwnerComp.NetworkId);
                            ++ClassifiedPredictedSpawns;
                        }
                    }
                }
            }
        }
    }

    public class SnapshotDataBufferLookupTests
    {
        [Test]
        public void ComponentCanBeInspected()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(TestSpawnBufferClassifier));
                testWorld.CreateGhostCollection();
                testWorld.CreateWorlds(true, 1);
                BuildPrefab(testWorld.ServerWorld, "TestPrefab");
                BuildPrefab(testWorld.ClientWorlds[0], "TestPrefab");
                testWorld.Connect(1f / 60f);
                testWorld.GoInGame();
                for(var i=0;i<32;++i)
                    testWorld.Tick(1.0f/60f);
                Assert.AreEqual(testWorld.GetSingletonBuffer<GhostCollectionPrefab>(testWorld.ServerWorld).Length,1);
                Assert.AreEqual(testWorld.GetSingletonBuffer<GhostCollectionPrefab>(testWorld.ClientWorlds[0]).Length,1);
                var serverGhost = testWorld.ServerWorld.EntityManager.Instantiate(testWorld.GetSingletonBuffer<GhostCollectionPrefab>(testWorld.ServerWorld).ElementAt(0).GhostPrefab);
                SetComponentsData(testWorld.ServerWorld, serverGhost);
                for(var i=0;i<32;++i)
                    testWorld.Tick(1.0f/60f);
            }
        }

        [Test]
        public void ComponentCanBeExtractedFromPredictedSpawnBuffer()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(TestSpawnBufferClassifier));
                testWorld.CreateGhostCollection();
                testWorld.CreateWorlds(true, 1);
                BuildPrefab(testWorld.ServerWorld, "TestPrefab");
                var clientPrefab = BuildPrefab(testWorld.ClientWorlds[0], "TestPrefab");
                Assert.IsTrue(testWorld.ClientWorlds[0].EntityManager.HasComponent<PredictedGhostSpawnRequest>(clientPrefab));
                testWorld.Connect(1f / 60f);
                testWorld.GoInGame();
                for(var i=0;i<32;++i)
                    testWorld.Tick(1.0f/60f);
                Assert.AreEqual(testWorld.GetSingletonBuffer<GhostCollectionPrefab>(testWorld.ServerWorld).Length,1);
                Assert.AreEqual(testWorld.GetSingletonBuffer<GhostCollectionPrefab>(testWorld.ClientWorlds[0]).Length,1);
                //Predict the spawning on the client. And match the one coming from server
                var clientGhost = testWorld.ClientWorlds[0].EntityManager.Instantiate(clientPrefab);
                Assert.IsTrue(testWorld.ClientWorlds[0].EntityManager.HasComponent<PredictedGhostSpawnRequest>(clientGhost));
                SetComponentsData(testWorld.ClientWorlds[0], clientGhost);
                for(var i=0;i<2;++i)
                    testWorld.Tick(1.0f/60f);
                var serverGhost = testWorld.ServerWorld.EntityManager.Instantiate(testWorld.GetSingletonBuffer<GhostCollectionPrefab>(testWorld.ServerWorld).ElementAt(0).GhostPrefab);
                SetComponentsData(testWorld.ServerWorld, serverGhost);
                for(var i=0;i<32;++i)
                    testWorld.Tick(1.0f/60f);
                var classifier = testWorld.ClientWorlds[0].GetExistingSystem<TestSpawnBufferClassifier>();
                var systemRef = testWorld.ClientWorlds[0].Unmanaged.GetUnsafeSystemRef<TestSpawnBufferClassifier>(classifier);
                Assert.AreEqual(1, systemRef.ClassifiedPredictedSpawns);
            }
        }

        [Test]
        public void ComponentCanBeExtractedForDifferentGhostTypes()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(TestSpawnBufferClassifier));
                testWorld.CreateGhostCollection();
                testWorld.CreateWorlds(true, 1);
                var clientGhostPrefabs = new Entity[5];
                for (int i = 0; i < clientGhostPrefabs.Length; ++i)
                {
                    BuildPrefab(testWorld.ServerWorld, $"TestPrefab_{i}");
                    clientGhostPrefabs[i] = BuildPrefab(testWorld.ClientWorlds[0], $"TestPrefab_{i}");
                    Assert.IsTrue(testWorld.ClientWorlds[0].EntityManager.HasComponent<PredictedGhostSpawnRequest>(clientGhostPrefabs[i]));
                }


                testWorld.Connect(1f / 60f);
                testWorld.GoInGame();
                for(var i=0;i<32;++i)
                    testWorld.Tick(1.0f/60f);
                Assert.AreEqual(clientGhostPrefabs.Length, testWorld.GetSingletonBuffer<GhostCollectionPrefab>(testWorld.ServerWorld).Length);
                Assert.AreEqual(clientGhostPrefabs.Length, testWorld.GetSingletonBuffer<GhostCollectionPrefab>(testWorld.ClientWorlds[0]).Length);
                //Predict the spawning on the client. And match the one coming from server
                for (int i = 0; i < clientGhostPrefabs.Length; ++i)
                {
                    var clientGhost = testWorld.ClientWorlds[0].EntityManager.Instantiate(clientGhostPrefabs[i]);
                    Assert.IsTrue(testWorld.ClientWorlds[0].EntityManager.HasComponent<PredictedGhostSpawnRequest>(clientGhost));
                    SetComponentsData(testWorld.ClientWorlds[0], clientGhost);
                }
                for(var i=0;i<2;++i)
                    testWorld.Tick(1.0f/60f);

                for (int i = 0; i < clientGhostPrefabs.Length; ++i)
                {
                    var serverGhost = testWorld.ServerWorld.EntityManager.Instantiate(testWorld.GetSingletonBuffer<GhostCollectionPrefab>(testWorld.ServerWorld).ElementAt(i).GhostPrefab);
                    SetComponentsData(testWorld.ServerWorld, serverGhost);
                }
                for(var i=0;i<32;++i)
                    testWorld.Tick(1.0f/60f);
                var classifier = testWorld.ClientWorlds[0].GetExistingSystem<TestSpawnBufferClassifier>();
                var systemRef = testWorld.ClientWorlds[0].Unmanaged.GetUnsafeSystemRef<TestSpawnBufferClassifier>(classifier);
                Assert.AreEqual(clientGhostPrefabs.Length, systemRef.ClassifiedPredictedSpawns);
            }
        }

        private void SetComponentsData(World world, Entity entity)
        {
            world.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(40f,10f, 90f));
            world.EntityManager.SetComponentData(entity, new GhostOwner { NetworkId = 1});
            world.EntityManager.SetComponentData(entity, new SomeData { Value = 10000 });
            world.EntityManager.GetBuffer<GhostGenTest_Buffer>(entity).Add(new GhostGenTest_Buffer{IntValue = 10});
        }

        private Entity BuildPrefab(World world, string prefabName)
        {
            var archetype = world.EntityManager.CreateArchetype(
                new ComponentType(typeof(Transforms.LocalTransform)),
                new ComponentType(typeof(GhostOwner)),
                new ComponentType(typeof(GhostGenTest_Buffer)),
                new ComponentType(typeof(SomeData)));
            var prefab = world.EntityManager.CreateEntity(archetype);
            GhostPrefabCreation.ConvertToGhostPrefab(world.EntityManager, prefab, new GhostPrefabCreation.Config
            {
                Name = prefabName,
                Importance = 1000,
                SupportedGhostModes = GhostModeMask.All,
                DefaultGhostMode = GhostMode.OwnerPredicted,
                OptimizationMode = GhostOptimizationMode.Dynamic,
                UsePreSerialization = false
            });
            return prefab;
        }
    }
}
