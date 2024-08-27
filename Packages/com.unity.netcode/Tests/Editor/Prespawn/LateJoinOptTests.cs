using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using Unity.NetCode.Tests;
using Unity.Transforms;
using UnityEngine.SceneManagement;

namespace Unity.NetCode.PrespawnTests
{
    struct ServerOnlyTag : IComponentData
    {
    }

    public class LateJoinOptTests : TestWithSceneAsset
    {
        private static void CheckPrespawnArePresent(int numObjects, NetCodeTestWorld testWorld)
        {
            //Before going in game there should N prespawned objects
            using var serverGhosts = testWorld.ServerWorld.EntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new [] { ComponentType.ReadOnly(typeof(PreSpawnedGhostIndex))},
                Options = EntityQueryOptions.IncludeDisabledEntities
            });
            Assert.AreEqual(numObjects, serverGhosts.CalculateEntityCount());
            for (int i = 0; i < testWorld.ClientWorlds.Length; ++i)
            {
                using var clientGhosts = testWorld.ClientWorlds[i].EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new [] { ComponentType.ReadOnly(typeof(PreSpawnedGhostIndex))},
                    Options = EntityQueryOptions.IncludeDisabledEntities
                });
                Assert.AreEqual(numObjects, clientGhosts.CalculateEntityCount());
            }
        }

        private static void CheckComponents(int numObjects, NetCodeTestWorld testWorld)
        {

            Assert.IsFalse(testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(SomeData),typeof(Disabled)).IsEmpty);
            Assert.IsFalse(testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(SomeDataElement), typeof(Disabled)).IsEmpty);

            for (int i = 0; i < testWorld.ClientWorlds.Length; ++i)
            {
                Assert.IsFalse(testWorld.ClientWorlds[i].EntityManager.CreateEntityQuery(typeof(SomeData),typeof(Disabled)).IsEmpty);
                Assert.IsFalse(testWorld.ClientWorlds[i].EntityManager.CreateEntityQuery(typeof(SomeDataElement),typeof(Disabled)).IsEmpty);
            }
        }

        int FindGhostType(in DynamicBuffer<GhostCollectionPrefab> ghostCollection, GhostType ghostTypeComponent)
        {
            int ghostType;
            for (ghostType = 0; ghostType < ghostCollection.Length; ++ghostType)
            {
                if (ghostCollection[ghostType].GhostType == ghostTypeComponent)
                    break;
            }
            if (ghostType >= ghostCollection.Length)
                return -1;
            return ghostType;
        }

        private void CheckBaselineAreCreated(World world)
        {
            //Before going in game there should N prespawned objects
            var baselines = world.EntityManager.CreateEntityQuery(typeof(PrespawnGhostBaseline));
            Assert.IsFalse(baselines.IsEmptyIgnoreFilter);
            var entities = baselines.ToEntityArray(Allocator.Temp);
            var ghostCollectionEntity = world.EntityManager.CreateEntityQuery(typeof(GhostCollection)).GetSingletonEntity();
            var ghostCollection = world.EntityManager.GetBuffer<GhostCollectionPrefabSerializer>(ghostCollectionEntity);
            var ghostPrefabs = world.EntityManager.GetBuffer<GhostCollectionPrefab>(ghostCollectionEntity);
            var ghostComponentIndex = world.EntityManager.GetBuffer<GhostCollectionComponentIndex>(ghostCollectionEntity);
            var ghostSerializers = world.EntityManager.GetBuffer<GhostComponentSerializer.State>(ghostCollectionEntity);
            Assert.AreEqual(3, ghostCollection.Length);
            foreach (var ent in entities)
            {
                var buffer = world.EntityManager.GetBuffer<PrespawnGhostBaseline>(ent);
                Assert.AreNotEqual(0, buffer.Length);
                //Check that the baseline contains what we expect
                unsafe
                {
                    var ghost = world.EntityManager.GetComponentData<GhostInstance>(ent);
                    Assert.AreEqual(-1, ghost.ghostType);
                    var ghostType = world.EntityManager.GetComponentData<GhostType>(ent);
                    var idx = FindGhostType(ghostPrefabs, ghostType);
                    Assert.AreNotEqual(-1, idx);
                    //Need to lookup who is it
                    var typeData = ghostCollection[idx];
                    byte* snapshotPtr = (byte*) buffer.GetUnsafeReadOnlyPtr();
                    int changeMaskUints = GhostComponentSerializer.ChangeMaskArraySizeInUInts(typeData.ChangeMaskBits);
                    var snapshotOffset = GhostComponentSerializer.SnapshotSizeAligned(4 + changeMaskUints * 4);
                    for (int cm = 0; cm < changeMaskUints; ++cm)
                        Assert.AreEqual(0, ((uint*)snapshotPtr)[cm]);
                    var offset = snapshotOffset;
                    for (int comp = 0; comp < typeData.NumComponents; ++comp)
                    {
                        int serializerIdx = ghostComponentIndex[typeData.FirstComponent + comp].SerializerIndex;
                        if (ghostSerializers[serializerIdx].ComponentType.IsBuffer)
                        {
                            Assert.AreEqual(16, ((uint*)(snapshotPtr + offset))[0]);
                            Assert.AreEqual(GhostComponentSerializer.SnapshotSizeAligned(sizeof(uint)), ((uint*)(snapshotPtr + offset))[1]);
                        }
                        offset += GhostComponentSerializer.SizeInSnapshot(ghostSerializers[serializerIdx]);
                    }
                    if (typeData.NumBuffers > 0)
                    {
                        var dynamicDataPtr = snapshotPtr + typeData.SnapshotSize;
                        var bufferSize = ((uint*)dynamicDataPtr)[0];
                        Assert.AreEqual(GhostComponentSerializer.SnapshotSizeAligned(sizeof(uint)) +
                                        GhostComponentSerializer.SnapshotSizeAligned(16*sizeof(uint)), bufferSize);
                    }
                }
            }
        }

        void ValidateReceivedSnapshotData(World clientWorld)
        {
            using var query = clientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostInstance>(), ComponentType.ReadOnly<PreSpawnedGhostIndex>());
            using var collectionQuery = clientWorld.EntityManager.CreateEntityQuery(typeof(GhostCollection));
            var entities = query.ToEntityArray(Allocator.Temp);
            var ghostCollectionEntity = collectionQuery.GetSingletonEntity();
            var ghostCollection = clientWorld.EntityManager.GetBuffer<GhostCollectionPrefabSerializer>(ghostCollectionEntity);
            var ghostComponentIndex = clientWorld.EntityManager.GetBuffer<GhostCollectionComponentIndex>(ghostCollectionEntity);
            var ghostSerializers = clientWorld.EntityManager.GetBuffer<GhostComponentSerializer.State>(ghostCollectionEntity);

            unsafe
            {
                for (int i = 0; i < entities.Length; ++i)
                {
                    var ghost = clientWorld.EntityManager.GetComponentData<GhostInstance>(entities[i]);
                    Assert.AreNotEqual(-1, ghost.ghostType);
                    var typeData = ghostCollection[ghost.ghostType];
                    var snapshotData = clientWorld.EntityManager.GetComponentData<SnapshotData>(entities[i]);
                    var snapshotBuffer = clientWorld.EntityManager.GetBuffer<SnapshotDataBuffer>(entities[i]);

                    byte* snapshotPtr = (byte*)snapshotBuffer.GetUnsafeReadOnlyPtr();
                    int changeMaskUints = GhostComponentSerializer.ChangeMaskArraySizeInUInts(typeData.ChangeMaskBits);
                    int snapshotSize = typeData.SnapshotSize;
                    var snapshotOffset = GhostComponentSerializer.SnapshotSizeAligned(4 + changeMaskUints*4);
                    snapshotPtr += snapshotSize * snapshotData.LatestIndex;
                    uint* changeMask = (uint*)(snapshotPtr+4);

                    //Check that all the masks are zero
                    for (int cm = 0; cm < changeMaskUints; ++cm)
                        Assert.AreEqual(0, changeMask[cm]);

                    var offset = snapshotOffset;
                    for (int comp = 0; comp < typeData.NumComponents; ++comp)
                    {
                        int serializerIdx = ghostComponentIndex[typeData.FirstComponent + comp].SerializerIndex;
                        if (ghostSerializers[serializerIdx].ComponentType.IsBuffer)
                        {
                            Assert.AreEqual(16, ((uint*)(snapshotPtr + offset))[0]);
                            Assert.AreEqual(0, ((uint*)(snapshotPtr + offset))[1]);
                        }
                        offset += GhostComponentSerializer.SizeInSnapshot(ghostSerializers[serializerIdx]);
                    }
                    if (typeData.NumBuffers > 0)
                    {
                        var dynamicData = clientWorld.EntityManager.GetBuffer<SnapshotDynamicDataBuffer>(entities[i]);
                        byte* dynamicPtr = (byte*) dynamicData.GetUnsafeReadOnlyPtr();
                        var bufferSize = ((uint*) dynamicPtr)[snapshotData.LatestIndex];
                        Assert.AreEqual(GhostComponentSerializer.SnapshotSizeAligned(sizeof(uint)) +
                                        GhostComponentSerializer.SnapshotSizeAligned(16*sizeof(uint)), bufferSize);
                    }
                }
            }
        }

        void TestRunner(int numClients, int numObjectsPerPrefabs, int numPrefabs,
            uint[] initialDataSize,
            uint[] initialAvgBitsPerEntity,
            uint[] avgBitsPerEntity,
            bool enableFallbackBaseline)
        {
            var numObjects = numObjectsPerPrefabs * numPrefabs;
            var uncompressed = new uint[numClients];
            var totalDataReceived = new uint[numClients];
            var numReceived = new uint[numClients];
            using (var testWorld = new NetCodeTestWorld())
            {
                //Create a scene with a subscene and a bunch of objects in it
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, numClients);
                var mode = enableFallbackBaseline ? "WithBaseline" : "NoBaseline";

                //Stream the sub scene in
                SubSceneHelper.LoadSubSceneInWorlds(testWorld);
                float frameTime = 1.0f / 60.0f;
                testWorld.Connect(frameTime);
                CheckPrespawnArePresent(numObjects, testWorld);
                CheckComponents(numObjects, testWorld);
                //To Disable the prespawn optimization, just remove the baselines
                if (!enableFallbackBaseline)
                {
                    using var query = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(PrespawnGhostBaseline));
                    testWorld.ServerWorld.EntityManager.RemoveComponent<PrespawnGhostBaseline>(query);
                    for (int i = 0; i < testWorld.ClientWorlds.Length; ++i)
                    {
                        using var clientQuery = testWorld.ClientWorlds[i].EntityManager.CreateEntityQuery(typeof(PrespawnGhostBaseline));
                        testWorld.ClientWorlds[i].EntityManager.RemoveComponent<PrespawnGhostBaseline>(clientQuery);
                    }
                }

                testWorld.GoInGame();

                var connections = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PrespawnSectionAck>()).ToEntityArray(Allocator.Temp);
                for (int i = 0; i< 32; ++i)
                {
                    testWorld.Tick(frameTime);
                    bool allSceneAcked = false;
                    foreach (var connection in (connections))
                    {
                        var buffer = testWorld.ServerWorld.EntityManager.GetBuffer<PrespawnSectionAck>(connection);
                        allSceneAcked |= buffer.Length > 0;
                    }

                    if (allSceneAcked)
                        break;
                }
                // ----------------------------------------------------------------
                // From heere one the server will start sending some ghosts.
                // ----------------------------------------------------------------
                uint newObjects = 0;
                uint totalSceneData = 0;
                for(int tick=0;tick<32;++tick)
                {
                    testWorld.Tick(frameTime);
                    for (int i = 0; i < testWorld.ClientWorlds.Length; ++i)
                    {
                        var netStats = testWorld.ClientWorlds[i].EntityManager.GetComponentData<GhostStatsCollectionSnapshot>(testWorld.TryGetSingletonEntity<GhostStatsCollectionSnapshot>(testWorld.ClientWorlds[i])).Data;
                        totalSceneData += netStats[5];
                        for (int gtype = 0; gtype < numPrefabs; ++gtype)
                        {
                            numReceived[i] += netStats[3*gtype + 7];
                            totalDataReceived[i] += netStats[3*gtype + 8];
                            uncompressed[i] += netStats[3*gtype + 9];
                        }
                        if(enableFallbackBaseline)
                            ValidateReceivedSnapshotData(testWorld.ClientWorlds[i]);

                        //When the total uncompressed object equals 0 means no new ghosts is received
                        //This is always true for enableFallbackBaseline is true
                        newObjects = 0;
                        for (int gtype = 0; gtype < numPrefabs; ++gtype)
                            newObjects += netStats[3*gtype + 9];
                    }

                    if (newObjects == 0 && numReceived[0] >= numObjects)
                        break;
                }

                //Without late join opt, the received data is more or equals than the totalCompressedDataBits for sure
                for (int i = 0; i < testWorld.ClientWorlds.Length; ++i)
                {
                    //Store the "join" data size
                    initialAvgBitsPerEntity[i] = totalDataReceived[i] / numReceived[i];
                    initialDataSize[i] = totalDataReceived[i];
                    Debug.Log($"{mode} Client {i} Initial Join: {numReceived[i]} - {totalDataReceived[i]} - {initialAvgBitsPerEntity[i]}");
                }

                //For the subsequent ticks the expectation is to reach the 0 bits for the entity data (everything is stationary).
                //Only header, masks, ghost ids, and baselines will be sent. The size should remain almost constant. It can still
                //change a bit because of the baselines and tick encoding)
                for (int tick = 0; tick < 32; ++tick)
                {
                    testWorld.Tick(frameTime);
                    for (int i = 0; i < testWorld.ClientWorlds.Length; ++i)
                    {
                        var netStats = testWorld.ClientWorlds[i].EntityManager.GetComponentData<GhostStatsCollectionSnapshot>(testWorld.TryGetSingletonEntity<GhostStatsCollectionSnapshot>(testWorld.ClientWorlds[i])).Data;
                        for (int gtype = 0; gtype < numPrefabs; ++gtype)
                        {
                            Assert.AreEqual(0, netStats[3*gtype + 9]); //No new object
                            numReceived[i] += netStats[3*gtype + 7];
                            totalDataReceived[i] += netStats[3*gtype + 8];
                        }
                        ValidateReceivedSnapshotData(testWorld.ClientWorlds[i]);
                    }
                }

                for (int i = 0; i < testWorld.ClientWorlds.Length; ++i)
                {
                    avgBitsPerEntity[i] = totalDataReceived[i] / numReceived[i];
                    Debug.Log($"{mode} Client {i} At Regime: {numReceived[i]} - {totalDataReceived[i]} - {avgBitsPerEntity[i]}");
                }
            }
        }

        [Test]
        public void DataSentWithFallbackBaselineAreLessThanWithout()
        {
            const int numObjectsPerPrefab = 32;
            const int numClients = 1;
            const int numPrefabs = 4;

            //Set the scene with multiple prefab types
            var prefab1 = SubSceneHelper.CreateSimplePrefab(ScenePath, "Simple", typeof(GhostAuthoringComponent));
            var prefab2 = SubSceneHelper.CreateSimplePrefab(ScenePath, "WithData", typeof(GhostAuthoringComponent),
                typeof(SomeDataAuthoring));
            var prefab3 = SubSceneHelper.CreateSimplePrefab(ScenePath, "WithBuffer", typeof(GhostAuthoringComponent),
                typeof(SomeDataElementAuthoring));
            GameObject withChildren = new GameObject("WithChildren", typeof(GhostAuthoringComponent));
            GameObject children1 = new GameObject("Child1", typeof(SomeDataAuthoring));
            GameObject children2 = new GameObject("Child2", typeof(SomeDataAuthoring));
            children1.transform.parent = withChildren.transform;
            children2.transform.parent = withChildren.transform;
            var prefab4 = SubSceneHelper.CreatePrefab(ScenePath, withChildren);

            var parentScene = SubSceneHelper.CreateEmptyScene(ScenePath, "LateJoinTest");
            SubSceneHelper.CreateSubSceneWithPrefabs(parentScene, ScenePath, "subscene", new[]
            {
                prefab1,
                prefab2,
                prefab3,
                prefab4,
            }, numObjectsPerPrefab);
            var initialDataSize = new uint[numClients];
            var initialAvgBitsPerEntity = new uint[numClients];
            var averageEntityBits = new uint[numClients];
            TestRunner(numClients, numObjectsPerPrefab, numPrefabs, initialDataSize, initialAvgBitsPerEntity, averageEntityBits, false);
            var initialDataSizeWithFallback = new uint[numClients];
            var initialAvgBitsPerEntityWithFallback = new uint[numClients];
            var averageEntityBitsWithFallback = new uint[numClients];
            TestRunner(numClients, numObjectsPerPrefab, numPrefabs, initialDataSizeWithFallback,
                initialAvgBitsPerEntityWithFallback, averageEntityBitsWithFallback, true);
            for (int i = 0; i < numClients; ++i)
            {
                Assert.LessOrEqual(initialDataSizeWithFallback[i], initialDataSize[i]);
                Assert.LessOrEqual(initialAvgBitsPerEntityWithFallback[i], initialAvgBitsPerEntity[i]);
                //The average initial size should be less or equals to the one without opt
                Assert.LessOrEqual(initialAvgBitsPerEntityWithFallback[i], averageEntityBits[i]);
                Assert.LessOrEqual(averageEntityBitsWithFallback[i], averageEntityBits[i]);
            }

        }

        [Test]
        public void Test_BaselineAreCreated()
        {
            //Set the scene with multiple prefab types
            const int numObjects = 10;
            var prefab1 = SubSceneHelper.CreateSimplePrefab(ScenePath, "WithData", typeof(GhostAuthoringComponent),
                typeof(SomeDataAuthoring));
            var prefab2 = SubSceneHelper.CreateSimplePrefab(ScenePath, "WithBuffer", typeof(GhostAuthoringComponent),
                typeof(SomeDataElementAuthoring));
            var parentScene = SubSceneHelper.CreateEmptyScene(ScenePath, "LateJoinTest");
            SubSceneHelper.CreateSubSceneWithPrefabs(parentScene, ScenePath, "subscene", new[]
            {
                prefab1,
                prefab2
            }, numObjects);

            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1);
                //Stream the sub scene in
                SubSceneHelper.LoadSubSceneInWorlds(testWorld);
                float frameTime = 1.0f / 60.0f;
                testWorld.Connect(frameTime);
                CheckPrespawnArePresent(numObjects*2, testWorld);
                CheckComponents(numObjects*2, testWorld);
                testWorld.GoInGame();
                //Run some another tick to retrieve and process the prefabs and initialize the baselines
                for(int i=0;i<2;++i)
                    testWorld.Tick(frameTime);
                CheckBaselineAreCreated(testWorld.ServerWorld);
                for (int i = 0; i < testWorld.ClientWorlds.Length; ++i)
                {
                    CheckBaselineAreCreated(testWorld.ClientWorlds[i]);
                }
            }
        }

        [Test]
        public void UsingStaticOptimzationServerDoesNotSendData()
        {
            const int numObjects = 10;
            //Set the scene with multiple prefab types
            var prefab = SubSceneHelper.CreateSimplePrefab(ScenePath, "WithData", typeof(GhostAuthoringComponent),
                typeof(SomeDataAuthoring));
            prefab.GetComponent<GhostAuthoringComponent>().OptimizationMode = GhostOptimizationMode.Static;
            PrefabUtility.SavePrefabAsset(prefab);

            var parentScene = SubSceneHelper.CreateEmptyScene(ScenePath, "LateJoinTest");
            SubSceneHelper.CreateSubSceneWithPrefabs(parentScene, ScenePath, "subscene", new[]
            {
                prefab,
            }, numObjects);

            using (var testWorld = new NetCodeTestWorld())
            {
                //Create a scene with a subscene and a bunch of objects in it
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1);

                //Stream the sub scene in
                SubSceneHelper.LoadSubSceneInWorlds(testWorld);
                float frameTime = 1.0f / 60.0f;
                testWorld.Connect(frameTime);
                CheckPrespawnArePresent(numObjects, testWorld);
                testWorld.GoInGame();

                uint uncompressed = 0;
                uint totalDataReceived = 0;
                uint numReceived = 0;
                var collection = testWorld.TryGetSingletonEntity<GhostCollection>(testWorld.ClientWorlds[0]);
                var recvGhostMapSingleton = testWorld.TryGetSingletonEntity<SpawnedGhostEntityMap>(testWorld.ClientWorlds[0]);
                for(int tick=0;tick<16;++tick)
                {
                    testWorld.Tick(frameTime);
                    var netStats = testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostStatsCollectionSnapshot>(testWorld.TryGetSingletonEntity<GhostStatsCollectionSnapshot>(testWorld.ClientWorlds[0])).Data;
                    //Skip the frist ghost type (is be the subscene list)
                    if (netStats.Length > 6)
                    {
                        numReceived += netStats[7];
                        totalDataReceived += netStats[8];
                        uncompressed += netStats[9];
                    }
                }
                Assert.AreEqual(0, numReceived);
                Assert.AreEqual(0, uncompressed);

                var serverQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostInstance>(), ComponentType.ReadOnly<PreSpawnedGhostIndex>());
                var serverGhosts = serverQuery.ToComponentDataArray<GhostInstance>(Allocator.Temp);
                var serverEntities = serverQuery.ToEntityArray(Allocator.Temp);
                var ghostCollectionEntity = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(GhostCollection)).GetSingletonEntity();

                //Make a structural change and verify that entities are not sent (no changes in respect to the 0 baselines)
                for (int i = 8; i < 10; ++i)
                {
                    //I will add a tag. This should cause changes on the server side but on the client, that still see the entities
                    //as unchanged
                    testWorld.ServerWorld.EntityManager.AddComponent<ServerOnlyTag>(serverEntities[i]);
                }

                //We have now two chunks with like this
                // Chunk 1    Entities
                //              0 1 2 3 4 5 6 7
                // changed:     n n n n n n n n
                // Chunk 2:
                //              8 9
                // changed:     n n

                for (int i = 0; i < 16; ++i)
                {
                    testWorld.Tick(frameTime);
                    var netStats = testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostStatsCollectionSnapshot>(testWorld.TryGetSingletonEntity<GhostStatsCollectionSnapshot>(testWorld.ClientWorlds[0])).Data;
                    if (netStats.Length > 6)
                    {
                        numReceived += netStats[7];
                        totalDataReceived += netStats[8];
                        uncompressed += netStats[9];
                    }
                }
                Assert.AreEqual(0, numReceived);
                Assert.AreEqual(0, uncompressed);
                Assert.GreaterOrEqual(totalDataReceived, 0);

                //Change some components for entities 0,1,2
                for (int i = 0; i < 3; ++i)
                {
                    var data = testWorld.ServerWorld.EntityManager.GetComponentData<SomeData>(serverEntities[i]);
                    data.Value += 100;
                    testWorld.ServerWorld.EntityManager.SetComponentData(serverEntities[i], data);
                }

                //Since ghost are interpolated I need at least 2 tick to see the reflected values on the components
                for (int i = 0; i < 2; ++i)
                {
                    testWorld.Tick(frameTime);
                    var netStats = testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostStatsCollectionSnapshot>(testWorld.TryGetSingletonEntity<GhostStatsCollectionSnapshot>(testWorld.ClientWorlds[0])).Data;
                    if (netStats.Length > 7)
                    {
                        //Even if I change 3 entities, I still receive the full chunk (8 now) delta compressed, but only once.
                        Assert.AreEqual(8, netStats[7]);
                        Assert.GreaterOrEqual(netStats[8], 0);
                        Assert.AreEqual(0, netStats[9]);
                    }
                }
                for (int i = 0; i < 3; ++i)
                {
                    var ghost = new SpawnedGhost{ghostId = serverGhosts[i].ghostId, spawnTick = serverGhosts[i].spawnTick};
                    var ent = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SpawnedGhostEntityMap>(recvGhostMapSingleton).Value[ghost];
                    var serverData = testWorld.ServerWorld.EntityManager.GetComponentData<SomeData>(serverEntities[i]);
                    var clientdata = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SomeData>(ent);
                    Assert.AreEqual(serverData.Value, clientdata.Value);
                }

                {
                    var ghostCollection = testWorld.ClientWorlds[0].EntityManager.GetBuffer<GhostCollectionPrefabSerializer>(ghostCollectionEntity);
                    //Check that the change masks for the other entities are still 0
                    for (int i = 3; i < 8; ++i)
                    {
                        var ghost = new SpawnedGhost{ghostId = serverGhosts[i].ghostId, spawnTick = serverGhosts[i].spawnTick};
                        var ent = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SpawnedGhostEntityMap>(recvGhostMapSingleton).Value[ghost];
                        var snapshotData = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SnapshotData>(ent);
                        var snapshotBuffer = testWorld.ClientWorlds[0].EntityManager.GetBuffer<SnapshotDataBuffer>(ent);
                        var ghostType = testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostInstance>(ent).ghostType;
                        var typeData = ghostCollection[ghostType];
                        int snapshotSize = typeData.SnapshotSize;
                        unsafe
                        {
                            byte* snapshotPtr = (byte*)snapshotBuffer.GetUnsafeReadOnlyPtr();
                            snapshotPtr += snapshotSize * snapshotData.LatestIndex;
                            int changeMaskUints = GhostComponentSerializer.ChangeMaskArraySizeInUInts(typeData.ChangeMaskBits);
                            uint* changeMask = (uint*)(snapshotPtr+4);
                            //Check that all the masks are zero
                            for (int cm = 0; cm < changeMaskUints; ++cm)
                                Assert.AreEqual(0, changeMask[cm]);
                        }
                    }
                }
                //Entities 8,9 are still not received
                for (int i = 8; i < 10; ++i)
                {
                    var ghost = new SpawnedGhost{ghostId = serverGhosts[i].ghostId, spawnTick = serverGhosts[i].spawnTick};
                    var ent = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SpawnedGhostEntityMap>(recvGhostMapSingleton).Value[ghost];
                    var ghostType = testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostInstance>(ent).ghostType;
                    Assert.AreEqual(-1, ghostType);
                }
                //From here on I should receive 0 again (since the zerochange frame has been acked)
                numReceived = 0;
                totalDataReceived = 0;
                for (int i = 0; i < 16; ++i)
                {
                    testWorld.Tick(frameTime);
                    var netStats = testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostStatsCollectionSnapshot>(testWorld.TryGetSingletonEntity<GhostStatsCollectionSnapshot>(testWorld.ClientWorlds[0])).Data;
                    if (netStats.Length > 6)
                    {
                        numReceived += netStats[7];
                        totalDataReceived += netStats[8];
                        uncompressed += netStats[9];
                    }
                    Assert.AreEqual(0, numReceived);
                    Assert.AreEqual(0, uncompressed);
                    Assert.GreaterOrEqual(totalDataReceived, 0);
                }
                //Now make a structural change and verify that entities are sent again (since we made changes in respect to the baseline)
                for (int i = 3; i < 6; ++i)
                {
                    testWorld.ServerWorld.EntityManager.AddComponent<ServerOnlyTag>(serverEntities[i]);
                }
                //We have now two chunks with like this
                // Chunk 1    Entitites
                //              0 1 2 6 7
                // changed:     y y y n n
                // Chunk 2:
                //              3 4 5 8 9
                // changed:     n n n n n
                //
                // Will will not receive the 2nd chunk, even though the 3,4,5 version has been changed since they were in the first chunk.
                // Since we detect that all change are actually zero and we are using all fallback baseline and nothing has changed
                numReceived = 0;
                totalDataReceived = 0;
                for (int i = 0; i < 4; ++i)
                {
                    testWorld.Tick(frameTime);
                    var netStats = testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostStatsCollectionSnapshot>(testWorld.TryGetSingletonEntity<GhostStatsCollectionSnapshot>(testWorld.ClientWorlds[0])).Data;
                    if (netStats.Length > 6)
                    {
                        numReceived += netStats[7];
                        totalDataReceived += netStats[8];
                        uncompressed += netStats[9];
                    }
                }
                Assert.AreEqual(5, numReceived);
                Assert.AreEqual(0, uncompressed);
                Assert.GreaterOrEqual(totalDataReceived, 0);
                //Still entities 8,9 are not received yet
                for (int i = 8; i < 10; ++i)
                {
                    var ghost = new SpawnedGhost{ghostId = serverGhosts[i].ghostId, spawnTick = serverGhosts[i].spawnTick};
                    var ent = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SpawnedGhostEntityMap>(recvGhostMapSingleton).Value[ghost];
                    var ghostType = testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostInstance>(ent).ghostType;
                    Assert.AreEqual(-1, ghostType);
                }

                {
                    //And all change masks for the received entities are 0
                    var ghostCollection = testWorld.ClientWorlds[0].EntityManager.GetBuffer<GhostCollectionPrefabSerializer>(ghostCollectionEntity);
                    for (int i = 0; i < 8; ++i)
                    {
                        var ghost = new SpawnedGhost { ghostId = serverGhosts[i].ghostId, spawnTick = serverGhosts[i].spawnTick };
                        var ent = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SpawnedGhostEntityMap>(recvGhostMapSingleton).Value[ghost];
                        var snapshotData = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SnapshotData>(ent);
                        var snapshotBuffer = testWorld.ClientWorlds[0].EntityManager.GetBuffer<SnapshotDataBuffer>(ent);
                        var ghostType = testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostInstance>(ent)                            .ghostType;
                        var typeData = ghostCollection[ghostType];
                        int snapshotSize = typeData.SnapshotSize;
                        unsafe
                        {
                            byte* snapshotPtr = (byte*)snapshotBuffer.GetUnsafeReadOnlyPtr();
                            snapshotPtr += snapshotSize * snapshotData.LatestIndex;
                            int changeMaskUints = GhostComponentSerializer.ChangeMaskArraySizeInUInts(typeData.ChangeMaskBits);
                            uint* changeMask = (uint*)(snapshotPtr + 4);
                            //Check that all the masks are zero
                            for (int cm = 0; cm < changeMaskUints; ++cm)
                                Assert.AreEqual(0, changeMask[cm]);
                        }
                    }
                }
                //Finally change two entities in the second chunk
                for (int i = 3; i < 5; ++i)
                {
                    var data = testWorld.ServerWorld.EntityManager.GetComponentData<SomeData>(serverEntities[i]);
                    data.Value += 100;
                    testWorld.ServerWorld.EntityManager.SetComponentData(serverEntities[i], data);
                }
                numReceived = 0;
                totalDataReceived = 0;
                for (int i = 0; i < 4; ++i)
                {
                    testWorld.Tick(frameTime);
                    var netStats = testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostStatsCollectionSnapshot>(testWorld.TryGetSingletonEntity<GhostStatsCollectionSnapshot>(testWorld.ClientWorlds[0])).Data;
                    if (netStats.Length > 6)
                    {
                        numReceived += netStats[7];
                        totalDataReceived += netStats[8];
                        uncompressed += netStats[9];
                    }
                }
                //2x5
                Assert.AreEqual(10, numReceived);
                {
                    //Entities 8,9 received but with no changes
                    var ghostCollection = testWorld.ClientWorlds[0].EntityManager.GetBuffer<GhostCollectionPrefabSerializer>(ghostCollectionEntity);
                    for (int i = 8; i < 10; ++i)
                    {
                        var ghost = new SpawnedGhost { ghostId = serverGhosts[i].ghostId, spawnTick = serverGhosts[i].spawnTick };
                        var ent = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SpawnedGhostEntityMap>(recvGhostMapSingleton).Value[ghost];
                        var ghostType = testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostInstance>(ent).ghostType;
                        Assert.AreNotEqual(-1, ghostType);
                        var snapshotData = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SnapshotData>(ent);
                        var snapshotBuffer = testWorld.ClientWorlds[0].EntityManager.GetBuffer<SnapshotDataBuffer>(ent);
                        var typeData = ghostCollection[ghostType];
                        int snapshotSize = typeData.SnapshotSize;
                        unsafe
                        {
                            byte* snapshotPtr = (byte*)snapshotBuffer.GetUnsafeReadOnlyPtr();
                            snapshotPtr += snapshotSize * snapshotData.LatestIndex;
                            int changeMaskUints = GhostComponentSerializer.ChangeMaskArraySizeInUInts(typeData.ChangeMaskBits);
                            uint* changeMask = (uint*)(snapshotPtr + 4);
                            //Check that all the masks are zero
                            for (int cm = 0; cm < changeMaskUints; ++cm)
                                Assert.AreEqual(0, changeMask[cm]);
                        }
                    }
                }
                for (int i = 3; i < 5; ++i)
                {
                    var ghost = new SpawnedGhost{ghostId = serverGhosts[i].ghostId, spawnTick = serverGhosts[i].spawnTick};
                    var ent = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SpawnedGhostEntityMap>(recvGhostMapSingleton).Value[ghost];
                    var serverData = testWorld.ServerWorld.EntityManager.GetComponentData<SomeData>(serverEntities[i]);
                    var clientdata = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SomeData>(ent);
                    Assert.AreEqual(serverData.Value, clientdata.Value);
                }
            }
        }
    }
}
