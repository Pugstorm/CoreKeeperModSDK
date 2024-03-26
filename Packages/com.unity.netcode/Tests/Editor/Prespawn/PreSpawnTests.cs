using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.NetCode.Tests;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Mathematics;

namespace Unity.NetCode.PrespawnTests
{
    public struct EnableVerifyGhostIds : IComponentData
    {}

    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial class VerifyGhostIds : SystemBase
    {
        public int Matches = 0;
        public static int GhostsPerScene = 7;
        private EntityQuery _ghostComponentQuery;
        private EntityQuery _preSpawnedGhostIdsQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<EnableVerifyGhostIds>();
            _ghostComponentQuery = GetEntityQuery(typeof(GhostInstance), typeof(PreSpawnedGhostIndex));
            _preSpawnedGhostIdsQuery = GetEntityQuery(typeof(GhostInstance), typeof(PreSpawnedGhostIndex));
        }

        protected override void OnUpdate()
        {
            var ghostComponents = _ghostComponentQuery.ToComponentDataArray<GhostInstance>(Allocator.Temp);
            var preSpawnedGhostIds = _preSpawnedGhostIdsQuery.ToComponentDataArray<PreSpawnedGhostIndex>(Allocator.Temp);
            {
                Matches = 0;
                var idList = new List<int>();
                for (int i = 0; i < ghostComponents.Length; ++i)
                {
                    if (ghostComponents[i].ghostId != 0 && preSpawnedGhostIds.Length >= i)
                    {
                        // Since ghost IDs will get patched across multiple scenes they might not match exactly
                        var ghostId = (int)(ghostComponents[i].ghostId & ~PrespawnHelper.PrespawnGhostIdBase);
                        var diff = ghostId - preSpawnedGhostIds[i].Value - 1;
                        Assert.That(diff % GhostsPerScene == 0, "Prespawned ID not applied properly preID=" + preSpawnedGhostIds[i].Value + " ghostID=" + ghostId);
                        Matches++;
                        idList.Add(ghostId);
                    }
                }

                if (idList.Count == ghostComponents.Length)
                {
                    idList.Sort();
                    for (int i = 0; i < idList.Count - 1; ++i)
                    {
                        Assert.That(idList[i] == idList[i + 1] - 1,
                            "Ghost IDs not in incrementing order [i=" + idList[i] + " i+1=" + idList[i + 1] + "]");
                    }
                }
            }
        }
    }

    public class PreSpawnTests : TestWithSceneAsset
    {
        void CheckAllPrefabsInWorlds(NetCodeTestWorld testWorld)
        {
            CheckAllPrefabsInWorld(testWorld.ServerWorld);
            for(int i=0;i<testWorld.ClientWorlds.Length;++i)
                CheckAllPrefabsInWorld(testWorld.ClientWorlds[i]);
        }

        void CheckAllPrefabsInWorld(World world)
        {
            //TODO: dispose these
            Assert.IsFalse(world.EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new [] {ComponentType.ReadOnly<PreSpawnedGhostIndex>()},
                    Options = EntityQueryOptions.IncludeDisabledEntities
                }).IsEmptyIgnoreFilter);
            Assert.IsFalse(world.EntityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new [] {ComponentType.ReadOnly<NetCodePrespawnTag>()},
                    Options = EntityQueryOptions.IncludeDisabledEntities
                }).IsEmptyIgnoreFilter);
            //Check that prefab that does not have the NetCodePrespawnTag does not have any PreSpawnedGhostId
            var query = world.EntityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<Prefab>(),
                        ComponentType.ReadOnly<PreSpawnedGhostIndex>(),
                    },
                    None = new []
                    {
                        ComponentType.ReadOnly<NetCodePrespawnTag>()
                    },
                    Options = EntityQueryOptions.IncludeDisabledEntities
                });
            Assert.IsTrue(query.IsEmptyIgnoreFilter);
        }

        // Checks that prefabs and runtime spawned ghosts don't get the prespawn id component applied to them
        [Test]
        public void PrespawnIdComponentDoesntLeaksToOtherEntitiesInScene()
        {
            var prefab = SubSceneHelper.CreateSimplePrefab(ScenePath, "nonghost");
            var ghost = SubSceneHelper.CreateSimplePrefab(ScenePath, "ghost", typeof(GhostAuthoringComponent), typeof(NetCodePrespawnAuthoring));
            var scene = SubSceneHelper.CreateEmptyScene(ScenePath, "Parent");
            var subScene = SubSceneHelper.CreateSubScene(scene, Path.GetDirectoryName(scene.path), "Sub0", 5, 5, ghost, Vector3.zero);
            for (int i = 0; i < 10; ++i)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                SceneManager.MoveGameObjectToScene(go, scene);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scene.path);
            SceneManager.SetActiveScene(scene);
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1);
                SubSceneHelper.LoadSubSceneInWorlds(testWorld);
                CheckAllPrefabsInWorlds(testWorld);
            }
        }

        [Test]
        public void PrespawnIdComponentDoesntLeaksToOtherEntitiesInSubScene()
        {
            var prefab = SubSceneHelper.CreateSimplePrefab(ScenePath, "nonghost");
            var ghost = SubSceneHelper.CreateSimplePrefab(ScenePath, "ghost", typeof(GhostAuthoringComponent), typeof(NetCodePrespawnAuthoring));
            var scene = SubSceneHelper.CreateEmptyScene(ScenePath, "Parent");
            SubSceneHelper.CreateSubScene(scene,Path.GetDirectoryName(scene.path), "Sub0", 5, 5, ghost,
                new Vector3(0f, 0f, 0f));
            SubSceneHelper.CreateSubScene(scene,Path.GetDirectoryName(scene.path), "Sub1", 5, 5, prefab,
                new Vector3(5f, 0f, 0f));
            SceneManager.SetActiveScene(scene);
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1);
                SubSceneHelper.LoadSubSceneInWorlds(testWorld);
                CheckAllPrefabsInWorlds(testWorld);
            }
        }

        [Test]
        public void WithNoPrespawnsScenesAreNotInitialized()
        {
            var scene = SubSceneHelper.CreateEmptyScene(ScenePath, "Parent");
            SubSceneHelper.CreateSubScene(scene,Path.GetDirectoryName(scene.path), "Sub0", 0, 0, null, Vector3.zero);
            SceneManager.SetActiveScene(scene);
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1);
                SubSceneHelper.LoadSubSceneInWorlds(testWorld);
                testWorld.Connect(1.0f/60f, 4);
                testWorld.GoInGame();
                // Give Prespawn ghosts processing a chance to run a bunch of times
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(1.0f/60f);
                var query = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(PrespawnsSceneInitialized));
                Assert.AreEqual(0, query.CalculateEntityCount());
            }
        }

        [Test]
        public void VerifyPreSpawnIDsAreApplied()
        {
            VerifyGhostIds.GhostsPerScene = 25;
            var ghost = SubSceneHelper.CreateSimplePrefab(ScenePath, "ghost", typeof(GhostAuthoringComponent));
            var scene = SubSceneHelper.CreateEmptyScene(ScenePath, "Parent");
            SubSceneHelper.CreateSubScene(scene,Path.GetDirectoryName(scene.path), "Sub0", 5, 5, ghost, Vector3.zero);
            SceneManager.SetActiveScene(scene);
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(VerifyGhostIds));
                testWorld.CreateWorlds(true, 1);
                SubSceneHelper.LoadSubSceneInWorlds(testWorld);
                testWorld.ServerWorld.EntityManager.CreateEntity(typeof(EnableVerifyGhostIds));
                testWorld.ClientWorlds[0].EntityManager.CreateEntity(typeof(EnableVerifyGhostIds));
                testWorld.Connect(1.0f / 60f, 4);
                testWorld.GoInGame();
                for(int i=0;i<64;++i)
                {
                    testWorld.Tick(1.0f / 60f);
                    if (testWorld.ServerWorld.GetExistingSystemManaged<VerifyGhostIds>().Matches == VerifyGhostIds.GhostsPerScene &&
                        testWorld.ClientWorlds[0].GetExistingSystemManaged<VerifyGhostIds>().Matches == VerifyGhostIds.GhostsPerScene)
                        break;
                }
                var prespawned = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(PreSpawnedGhostIndex)).CalculateEntityCount();
                Assert.AreEqual(VerifyGhostIds.GhostsPerScene, prespawned, "Didn't find expected amount of prespawned entities in the server subscene");
                prespawned = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(PreSpawnedGhostIndex)).CalculateEntityCount();
                Assert.AreEqual(VerifyGhostIds.GhostsPerScene, prespawned, "Didn't find expected amount of prespawned entities in the client subscene");
                Assert.AreEqual(VerifyGhostIds.GhostsPerScene, testWorld.ServerWorld.GetExistingSystemManaged<VerifyGhostIds>().Matches, "Prespawn components added but didn't get ghost ID applied at runtime on server");
                Assert.AreEqual(VerifyGhostIds.GhostsPerScene, testWorld.ClientWorlds[0].GetExistingSystemManaged<VerifyGhostIds>().Matches, "Prespawn components added but didn't get ghost ID applied at runtime on client");
            }
        }

        [Test]
        public void DestroyedPreSpawnedObjectsCleanup()
        {
            VerifyGhostIds.GhostsPerScene = 7;
            var ghost = SubSceneHelper.CreateSimplePrefab(ScenePath, "ghost", typeof(GhostAuthoringComponent));
            var scene = SubSceneHelper.CreateEmptyScene(ScenePath, "Parent");
            SubSceneHelper.CreateSubScene(scene,Path.GetDirectoryName(scene.path), "Sub0", 1, VerifyGhostIds.GhostsPerScene, ghost, Vector3.zero);
            SceneManager.SetActiveScene(scene);

            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 2);
                SubSceneHelper.LoadSubSceneInWorlds(testWorld);
                testWorld.Connect(1.0f / 60f, 4);
                //Set in game the first client
                testWorld.SetInGame(0);
                // Delete one prespawned entity on the server
                var deletedId = 0;
                var q = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(GhostInstance), ComponentType.ReadOnly<PreSpawnedGhostIndex>());
                var prespawnedQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(GhostInstance), ComponentType.ReadOnly<PreSpawnedGhostIndex>());
                for (int i = 0; i < 16; ++i)
                {
                    testWorld.Tick(1.0f/60.0f);
                    var prespawnedGhost = q.ToComponentDataArray<GhostInstance>(Allocator.Temp);
                    // Filter for GhostComoponent and grab it after prespawn processing is done (ghost id valid)
                    if (prespawnedGhost.Length == 0 || (prespawnedGhost.Length > 0 && prespawnedGhost[0].ghostId == 0))
                    {
                        prespawnedGhost.Dispose();
                        continue;
                    }

                    deletedId = prespawnedGhost[0].ghostId;
                    var prespawned = prespawnedQuery.ToEntityArray(Allocator.Temp);
                    testWorld.ServerWorld.EntityManager.DestroyEntity(prespawned[0]);
                    prespawned.Dispose();
                    prespawnedGhost.Dispose();
                    break;
                }
                Assert.True(deletedId < 0);
                // Server now has one less prespawned ghosts since one was deleted, updated the count
                testWorld.SetInGame(1);
                // Check that the deleted entity has been cleaned up on the second client
                bool exists = false;
                int prespawnedCount = 0;
                var query = testWorld.ClientWorlds[1].EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new [] {ComponentType.ReadOnly<PreSpawnedGhostIndex>()},
                    Options = EntityQueryOptions.IncludeDisabledEntities
                });
                prespawnedCount = query.CalculateEntityCount();
                for (int i = 0; i < 128; ++i)
                {
                    testWorld.Tick(1.0f/60.0f);
                    exists = false;
                    var prespawnedData = query.ToComponentDataArray<PreSpawnedGhostIndex>(Allocator.Temp);
                    prespawnedCount = prespawnedData.Length;
                    for (int j = 0; j < prespawnedData.Length; ++j)
                    {
                        // Entity will be loaded from subscene data, wait until it goes missing after first ghost snapshot update
                        if (prespawnedData[j].Value == deletedId)
                            exists = true;
                    }
                    prespawnedData.Dispose();
                    if (!exists)
                        break;
                }

                Assert.True(prespawnedCount > 0);
                Assert.False(exists, "Found the prespawned entity which should be deleted");
            }
        }

        // Checking expected behaviour:
        // - 4 prespawns and 1 spawn created
        // - client connects and disconnects
        // - server retains that count, no cleanup (5)
        // - client cleans runtime spawns only (4), prespawns are a part of the subscene
        [Test]
        public void GhostCleanup()
        {
            VerifyGhostIds.GhostsPerScene = 7;
            var ghost = SubSceneHelper.CreateSimplePrefab(ScenePath, "ghost", typeof(GhostAuthoringComponent));
            var scene = SubSceneHelper.CreateEmptyScene(ScenePath, "Parent");
            SubSceneHelper.CreateSubScene(scene,Path.GetDirectoryName(scene.path), "Sub0", 1, VerifyGhostIds.GhostsPerScene,
                ghost, Vector3.zero);
            SceneManager.SetActiveScene(scene);

            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(VerifyGhostIds));
                testWorld.CreateGhostCollection(new GameObject("DynamicGhost"));
                testWorld.CreateWorlds(true, 1);
                SubSceneHelper.LoadSubSceneInWorlds(testWorld);
                testWorld.ServerWorld.EntityManager.CreateEntity(typeof(EnableVerifyGhostIds));
                testWorld.ClientWorlds[0].EntityManager.CreateEntity(typeof(EnableVerifyGhostIds));
                testWorld.Connect(1.0f / 60f, 4);
                testWorld.GoInGame();
                // If servers spawns something before connection is in game it will be registered as a prespawned entity
                // Wait until prespawned ghosts have been initialized
                var query = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(PreSpawnedGhostIndex),
                    typeof(GhostInstance));
                for (int i = 0; i < 16; ++i)
                {
                    testWorld.Tick(1.0f/60f);
                    var prespawns = query.CalculateEntityCount();
                    if (prespawns > 0)
                        break;
                }

                // Spawn something
                testWorld.SpawnOnServer(0);
                var ghostCount = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(GhostInstance), typeof(PreSpawnedGhostIndex)).CalculateEntityCount();
                // Wait until it's spawned on client
                int currentCount = 0;
                var clientQuery = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(GhostInstance), typeof(PreSpawnedGhostIndex));
                for (int i = 0; i < 64 && currentCount != ghostCount; ++i)
                {
                    testWorld.Tick(1.0f/60f);
                    currentCount = clientQuery.CalculateEntityCount();
                }
                Assert.That(ghostCount == currentCount, "Client did not spawn runtime entity (clientCount=" + currentCount + " serverCount=" + ghostCount + ")");

                // Verify spawned entities to not contain the prespawn id component
                var prespawnCount = testWorld.ServerWorld.EntityManager
                    .CreateEntityQuery(typeof(PreSpawnedGhostIndex), typeof(GhostInstance)).CalculateEntityCount();
                Assert.AreEqual(VerifyGhostIds.GhostsPerScene, prespawnCount, "Runtime spawned server entity got prespawn component added");
                prespawnCount = testWorld.ClientWorlds[0].EntityManager
                    .CreateEntityQuery(typeof(PreSpawnedGhostIndex), typeof(GhostInstance)).CalculateEntityCount();
                Assert.AreEqual(VerifyGhostIds.GhostsPerScene, prespawnCount, "Runtime spawned client entity got prespawn component added");

                testWorld.ClientWorlds[0].EntityManager.AddComponent<NetworkStreamRequestDisconnect>(
                    testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(NetworkStreamConnection)).GetSingletonEntity());

                // Wait until ghosts have been cleaned up
                int serverGhostCount = 0;
                int clientGhostCount = 0;
                int expectedServerGhostCount = VerifyGhostIds.GhostsPerScene + 2; //Also the ghost list
                int expectedClientGhostCount = VerifyGhostIds.GhostsPerScene; //only the prespawn should remain
                var serverGhosts = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(GhostInstance));
                var clientGhosts = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(GhostInstance));
                for (int i = 0; i < 16; ++i)
                {
                    testWorld.Tick(1.0f/60f);
                    // clientGhostCount will be 6 for a bit as it creates an initial archetype ghost and later a delayed one when on the right tick
                    serverGhostCount = serverGhosts.CalculateEntityCount();
                    clientGhostCount = clientGhosts.CalculateEntityCount();
                    //Debug.Log("serverCount=" + serverGhostCount + " clientCount=" + clientGhostCount);
                    //DumpGhosts(serverWorld, clientWorld);
                    if (serverGhostCount == expectedServerGhostCount && clientGhostCount == 0)
                        break;
                }
                Assert.That(serverGhostCount == expectedServerGhostCount, "Server ghosts not correct (count=" + serverGhostCount + " should be " + expectedServerGhostCount);
                Assert.That(clientGhostCount == expectedClientGhostCount, "Ghosts not cleaned up on client (count=" + clientGhostCount + " should be " + expectedClientGhostCount);
            }
        }

        [Test]
        public void MultipleSubscenes()
        {
            const int GhostScenes = 3;
            VerifyGhostIds.GhostsPerScene = 4;
            var ghost = SubSceneHelper.CreateSimplePrefab(ScenePath, "ghost", typeof(GhostAuthoringComponent));
            var scene = SubSceneHelper.CreateEmptyScene(ScenePath, "Parent");
            for (int i = 0; i < GhostScenes; ++i)
            {
                SubSceneHelper.CreateSubScene(scene,Path.GetDirectoryName(scene.path), $"Sub_{i}", 2, 2, ghost,
                    new Vector3(i*2.0f, 0.0f, 0.0f)); }
            SceneManager.SetActiveScene(scene);
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(VerifyGhostIds));
                testWorld.CreateWorlds(true, 1);
                SubSceneHelper.LoadSubSceneInWorlds(testWorld);
                testWorld.ServerWorld.EntityManager.CreateEntity(typeof(EnableVerifyGhostIds));
                testWorld.ClientWorlds[0].EntityManager.CreateEntity(typeof(EnableVerifyGhostIds));
                testWorld.Connect(1.0f / 60f, 4);
                testWorld.GoInGame();
                for(int i=0;i<64;++i)
                    testWorld.Tick(1.0f/60.0f);

                int prespawnedGhostCount = VerifyGhostIds.GhostsPerScene*GhostScenes;
                var prespawned = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(PreSpawnedGhostIndex)).CalculateEntityCount();
                Assert.AreEqual(prespawnedGhostCount, prespawned, "Didn't find expected amount of prespawned entities in the server subscene");
                prespawned = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(PreSpawnedGhostIndex)).CalculateEntityCount();
                Assert.AreEqual(prespawnedGhostCount, prespawned, "Didn't find expected amount of prespawned entities in the client subscene");
                Assert.AreEqual(prespawnedGhostCount, testWorld.ServerWorld.GetExistingSystemManaged<VerifyGhostIds>().Matches, "Prespawn components added but didn't get ghost ID applied at runtime on server");
                Assert.AreEqual(prespawnedGhostCount, testWorld.ClientWorlds[0].GetExistingSystemManaged<VerifyGhostIds>().Matches, "Prespawn components added but didn't get ghost ID applied at runtime on client");
            }
        }

        [Test]
        [Ignore("DOTS-6619 Test instability, causes crash when loading subscenes")]
        public void ManyPrespawnedObjects()
        {
            const int SubSceneCount = 10;
            const int GhostsPerScene = 500;
            var ghost = SubSceneHelper.CreateSimplePrefab(ScenePath, "ghost", typeof(GhostAuthoringComponent));
            var scene = SubSceneHelper.CreateEmptyScene(ScenePath, "Parent");
            for (int i = 0; i < 10; ++i)
            {
                SubSceneHelper.CreateSubScene(scene,Path.GetDirectoryName(scene.path), $"Sub_{i}", 10, 50, ghost,
                    new Vector3((i%5)*10f, 0.0f, (i/5)*50f));
            }
            SceneManager.SetActiveScene(scene);
            VerifyGhostIds.GhostsPerScene = GhostsPerScene;
            var prespawnedGhostCount = GhostsPerScene * SubSceneCount;
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(VerifyGhostIds));
                testWorld.CreateWorlds(true, 1);
                SubSceneHelper.LoadSubSceneInWorlds(testWorld);
                testWorld.ServerWorld.EntityManager.CreateEntity(typeof(EnableVerifyGhostIds));
                testWorld.ClientWorlds[0].EntityManager.CreateEntity(typeof(EnableVerifyGhostIds));
                testWorld.Connect(1.0f / 60f, 4);
                testWorld.GoInGame();
                for (int i=0; i<64;++i)
                {
                    testWorld.Tick(1.0f / 60f);
                    if (testWorld.ServerWorld.GetExistingSystemManaged<VerifyGhostIds>().Matches == VerifyGhostIds.GhostsPerScene &&
                        testWorld.ClientWorlds[0].GetExistingSystemManaged<VerifyGhostIds>().Matches == VerifyGhostIds.GhostsPerScene)
                        break;
                }

                var prespawned = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(PreSpawnedGhostIndex)).CalculateEntityCount();
                Assert.AreEqual(prespawnedGhostCount, prespawned, "Didn't find expected amount of prespawned entities in the server subscene");
                prespawned = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(PreSpawnedGhostIndex)).CalculateEntityCount();
                Assert.AreEqual(prespawnedGhostCount, prespawned, "Didn't find expected amount of prespawned entities in the client subscene");
                Assert.AreEqual(prespawnedGhostCount, testWorld.ServerWorld.GetExistingSystemManaged<VerifyGhostIds>().Matches, "Prespawn components added but didn't get ghost ID applied at runtime on server");
                Assert.AreEqual(prespawnedGhostCount, testWorld.ClientWorlds[0].GetExistingSystemManaged<VerifyGhostIds>().Matches, "Prespawn components added but didn't get ghost ID applied at runtime on client");

                var clientGhosts = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(GhostInstance), ComponentType.ReadOnly<PreSpawnedGhostIndex>())
                    .ToComponentDataArray<GhostInstance>(Allocator.Temp);
                var clientGhostPos = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(GhostInstance), typeof(LocalTransform), ComponentType.ReadOnly<PreSpawnedGhostIndex>())
                    .ToComponentDataArray<LocalTransform>(Allocator.Temp);
                var serverGhosts = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(GhostInstance), ComponentType.ReadOnly<PreSpawnedGhostIndex>())
                    .ToComponentDataArray<GhostInstance>(Allocator.Temp);
                var serverGhostPos = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(GhostInstance), typeof(LocalTransform), ComponentType.ReadOnly<PreSpawnedGhostIndex>())
                    .ToComponentDataArray<LocalTransform>(Allocator.Temp);

                var serverPosLookup = new NativeParallelHashMap<int, float3>(serverGhostPos.Length, Allocator.Temp);
                Assert.AreEqual(clientGhostPos.Length, serverGhostPos.Length);
                // Fill a hashmap with mapping from server ghost id to server position
                for (int i = 0; i < serverGhosts.Length; ++i)
                {
                    serverPosLookup.Add(serverGhosts[i].ghostId, serverGhostPos[i].Position);
                }
                for (int i = 0; i < clientGhosts.Length; ++i)
                {
                    Assert.IsTrue(PrespawnHelper.IsPrespawGhostId(clientGhosts[i].ghostId), "Prespawned ghosts not initialized");
                    // Verify that the client ghost id exists on the server with the same position
                    Assert.IsTrue(serverPosLookup.TryGetValue(clientGhosts[i].ghostId, out var serverPos));
                    Assert.LessOrEqual(math.distance(clientGhostPos[i].Position, serverPos), 0.001f);

                    // Remove the server ghost id which we already matched against to make sure htere are no duplicates
                    serverPosLookup.Remove(clientGhosts[i].ghostId);
                }
                // Verify that there are no additional server entities
                Assert.AreEqual(0, serverPosLookup.Count());
            }
        }

        [Test]
        public void PrefabVariantAreHandledCorrectly()
        {
            var ghost = SubSceneHelper.CreateSimplePrefab(ScenePath, "ghost", typeof(GhostAuthoringComponent));
            var variant = SubSceneHelper.CreatePrefabVariant(ghost);
            var scene = SubSceneHelper.CreateEmptyScene(ScenePath, "Parent");
            SubSceneHelper.CreateSubSceneWithPrefabs(scene,Path.GetDirectoryName(scene.path), "Sub0", new []{ghost, variant}, 5);
            SceneManager.SetActiveScene(scene);
            VerifyGhostIds.GhostsPerScene = 10;
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(VerifyGhostIds));
                testWorld.CreateWorlds(true, 1);
                SubSceneHelper.LoadSubSceneInWorlds(testWorld);
                testWorld.ServerWorld.EntityManager.CreateEntity(typeof(EnableVerifyGhostIds));
                testWorld.ClientWorlds[0].EntityManager.CreateEntity(typeof(EnableVerifyGhostIds));
                testWorld.Connect(1.0f / 60f, 4);
                testWorld.GoInGame();
                for(int i=0;i<64;++i)
                {
                    testWorld.Tick(1.0f / 60f);
                    if (testWorld.ServerWorld.GetExistingSystemManaged<VerifyGhostIds>().Matches == VerifyGhostIds.GhostsPerScene &&
                        testWorld.ClientWorlds[0].GetExistingSystemManaged<VerifyGhostIds>().Matches == VerifyGhostIds.GhostsPerScene)
                        break;
                }
                var prespawned = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(PreSpawnedGhostIndex)).CalculateEntityCount();
                Assert.AreEqual(VerifyGhostIds.GhostsPerScene, prespawned, "Didn't find expected amount of prespawned entities in the server subscene");
                prespawned = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(PreSpawnedGhostIndex)).CalculateEntityCount();
                Assert.AreEqual(VerifyGhostIds.GhostsPerScene, prespawned, "Didn't find expected amount of prespawned entities in the client subscene");
                Assert.AreEqual(VerifyGhostIds.GhostsPerScene, testWorld.ServerWorld.GetExistingSystemManaged<VerifyGhostIds>().Matches, "Prespawn components added but didn't get ghost ID applied at runtime on server");
                Assert.AreEqual(VerifyGhostIds.GhostsPerScene, testWorld.ClientWorlds[0].GetExistingSystemManaged<VerifyGhostIds>().Matches, "Prespawn components added but didn't get ghost ID applied at runtime on client");
            }
        }

        [Test]
        public void PrefabModelsAreHandledCorrectly()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.unity.netcode/Tests/Editor/Prespawn/Assets/Whitebox_Ground_1600x1600_A.prefab");
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.unity.netcode/Tests/Editor/Prespawn/Assets/Whitebox_Ground_1600x1600_A Variant.prefab");
            var scene = SubSceneHelper.CreateEmptyScene(ScenePath, "Parent");
            SubSceneHelper.CreateSubSceneWithPrefabs(scene,Path.GetDirectoryName(scene.path), "Sub0", new []{prefab, variant}, 2);
            SceneManager.SetActiveScene(scene);
            VerifyGhostIds.GhostsPerScene = 4;
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(VerifyGhostIds));
                testWorld.CreateWorlds(true, 1);
                SubSceneHelper.LoadSubSceneInWorlds(testWorld);
                testWorld.ServerWorld.EntityManager.CreateEntity(typeof(EnableVerifyGhostIds));
                testWorld.ClientWorlds[0].EntityManager.CreateEntity(typeof(EnableVerifyGhostIds));
                testWorld.Connect(1.0f / 60f, 4);
                testWorld.GoInGame();
                for(int i=0;i<64;++i)
                {
                    testWorld.Tick(1.0f / 60f);
                    if (testWorld.ServerWorld.GetExistingSystemManaged<VerifyGhostIds>().Matches == VerifyGhostIds.GhostsPerScene &&
                        testWorld.ClientWorlds[0].GetExistingSystemManaged<VerifyGhostIds>().Matches == VerifyGhostIds.GhostsPerScene)
                        break;
                }
                var prespawned = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(PreSpawnedGhostIndex)).CalculateEntityCount();
                Assert.AreEqual(VerifyGhostIds.GhostsPerScene, prespawned, "Didn't find expected amount of prespawned entities in the server subscene");
                prespawned = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(PreSpawnedGhostIndex)).CalculateEntityCount();
                Assert.AreEqual(VerifyGhostIds.GhostsPerScene, prespawned, "Didn't find expected amount of prespawned entities in the client subscene");
                Assert.AreEqual(VerifyGhostIds.GhostsPerScene, testWorld.ServerWorld.GetExistingSystemManaged<VerifyGhostIds>().Matches, "Prespawn components added but didn't get ghost ID applied at runtime on server");
                Assert.AreEqual(VerifyGhostIds.GhostsPerScene, testWorld.ClientWorlds[0].GetExistingSystemManaged<VerifyGhostIds>().Matches, "Prespawn components added but didn't get ghost ID applied at runtime on client");
            }
        }

        [Test]
        public void MulitpleSubScenesWithSameObjectsPositionAreHandledCorrectly()
        {
            var ghost = SubSceneHelper.CreateSimplePrefab(ScenePath, "ghost", typeof(GhostAuthoringComponent));
            var scene = SubSceneHelper.CreateEmptyScene(ScenePath, "Parent");
            SubSceneHelper.CreateSubScene(scene,Path.GetDirectoryName(scene.path), "Sub0", 1, 5, ghost, Vector3.zero);
            SubSceneHelper.CreateSubScene(scene,Path.GetDirectoryName(scene.path), "Sub1", 1, 5, ghost, Vector3.zero);
            SceneManager.SetActiveScene(scene);
            VerifyGhostIds.GhostsPerScene = 5;
            var totalPrespawned = 2 * VerifyGhostIds.GhostsPerScene;
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(VerifyGhostIds));
                testWorld.CreateWorlds(true, 1);
                SubSceneHelper.LoadSubSceneInWorlds(testWorld);
                testWorld.ServerWorld.EntityManager.CreateEntity(typeof(EnableVerifyGhostIds));
                testWorld.ClientWorlds[0].EntityManager.CreateEntity(typeof(EnableVerifyGhostIds));
                testWorld.Connect(1.0f / 60f, 4);
                testWorld.GoInGame();
                for(int i=0;i<64;++i)
                {
                    testWorld.Tick(1.0f / 60f);
                    if (testWorld.ServerWorld.GetExistingSystemManaged<VerifyGhostIds>().Matches == VerifyGhostIds.GhostsPerScene &&
                        testWorld.ClientWorlds[0].GetExistingSystemManaged<VerifyGhostIds>().Matches == VerifyGhostIds.GhostsPerScene)
                        break;
                }
                var prespawned = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(PreSpawnedGhostIndex)).CalculateEntityCount();
                Assert.AreEqual(totalPrespawned, prespawned, "Didn't find expected amount of prespawned entities in the server subscene");
                prespawned = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(PreSpawnedGhostIndex)).CalculateEntityCount();
                Assert.AreEqual(totalPrespawned, prespawned, "Didn't find expected amount of prespawned entities in the client subscene");
                Assert.AreEqual(totalPrespawned, testWorld.ServerWorld.GetExistingSystemManaged<VerifyGhostIds>().Matches, "Prespawn components added but didn't get ghost ID applied at runtime on server");
                Assert.AreEqual(totalPrespawned, testWorld.ClientWorlds[0].GetExistingSystemManaged<VerifyGhostIds>().Matches, "Prespawn components added but didn't get ghost ID applied at runtime on client");
            }
        }

        [Test, Ignore("Inconclusive CI error: Package Test - netcode [mac, trunk DOTS Monorepo]: [TimeoutExceptionMessage]: Timeout while waiting for a log message, no editor logging has happened during the timeout window! #6210")]
        public void MismatchedPrespawnClientServerScenesCantConnect()
        {
            var ghost = SubSceneHelper.CreateSimplePrefab(ScenePath, "ghost", typeof(GhostAuthoringComponent));
            var parentScene = SubSceneHelper.CreateEmptyScene(ScenePath, "Scene1");
            var subScene = SubSceneHelper.CreateSubScene(parentScene, Path.GetDirectoryName(parentScene.path), $"SubScene1", 10, 50, ghost,
                    Vector3.zero);
            SceneManager.SetActiveScene(parentScene);

            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1, true);
                SubSceneHelper.LoadSubSceneInWorlds(testWorld, subScene);

                //Tamper some prespawn on the server or the client such that their data aren't the same.
                var query = testWorld.ServerWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<PreSpawnedGhostIndex>(),
                    ComponentType.ReadOnly<Disabled>());
                var entities = query.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < 10; ++i)
                {
                    testWorld.ServerWorld.EntityManager.SetComponentData(entities[i],
                        LocalTransform.FromPosition(new float3(-10000, 10, 10 * i)));
                }
                entities.Dispose();

                testWorld.Connect(1.0f / 60f, 4);
                testWorld.GoInGame();

                UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new Regex(@"Subscene (\w+) baseline mismatch."));
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new Regex(@"Subscene (\w+) baseline mismatch."));
                for(int i=0;i<10;++i)
                    testWorld.Tick(1.0f/60.0f);

                //FIXME: at a certain point I should disconnect if I don't load the second subscene

                // Verify connection is now disconnected
                var conQuery = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
                Assert.AreEqual(0, conQuery.CalculateEntityCount());
            }
        }

        [Test]
        public void ServerTickWrapAroundDoesnNotCauseIssue()
        {
            var ghost = SubSceneHelper.CreateSimplePrefab(ScenePath, "ghost", typeof(GhostAuthoringComponent));
            var scene = SubSceneHelper.CreateEmptyScene(ScenePath, "Parent");
            SubSceneHelper.CreateSubSceneWithPrefabs(scene,Path.GetDirectoryName(scene.path), "Sub0", new []{ghost}, 5);
            SceneManager.SetActiveScene(scene);
            VerifyGhostIds.GhostsPerScene = 5;
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(VerifyGhostIds));
                testWorld.CreateWorlds(true, 1);
                SubSceneHelper.LoadSubSceneInWorlds(testWorld);
                testWorld.SetServerTick(new NetworkTick((UInt32.MaxValue>>1) - 16));
                testWorld.ServerWorld.EntityManager.CreateEntity(typeof(EnableVerifyGhostIds));
                testWorld.ClientWorlds[0].EntityManager.CreateEntity(typeof(EnableVerifyGhostIds));
                testWorld.Connect(1.0f / 60f, 4);
                testWorld.GoInGame();
                for(int i=0;i<32;++i)
                {
                    if (testWorld.GetNetworkTime(testWorld.ServerWorld).ServerTick.TickIndexForValidTick >= (UInt32.MaxValue>>1) - 3)
                        testWorld.SpawnOnServer(0);
                    testWorld.Tick(1.0f / 60f);
                    if (testWorld.ServerWorld.GetExistingSystemManaged<VerifyGhostIds>().Matches == VerifyGhostIds.GhostsPerScene &&
                        testWorld.ClientWorlds[0].GetExistingSystemManaged<VerifyGhostIds>().Matches == VerifyGhostIds.GhostsPerScene)
                        break;
                }
                var prespawned = testWorld.ServerWorld.EntityManager.CreateEntityQuery(typeof(PreSpawnedGhostIndex)).CalculateEntityCount();
                Assert.AreEqual(VerifyGhostIds.GhostsPerScene, prespawned, "Didn't find expected amount of prespawned entities in the server subscene");
                prespawned = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(PreSpawnedGhostIndex)).CalculateEntityCount();
                Assert.AreEqual(VerifyGhostIds.GhostsPerScene, prespawned, "Didn't find expected amount of prespawned entities in the client subscene");
                Assert.AreEqual(VerifyGhostIds.GhostsPerScene, testWorld.ServerWorld.GetExistingSystemManaged<VerifyGhostIds>().Matches, "Prespawn components added but didn't get ghost ID applied at runtime on server");
                Assert.AreEqual(VerifyGhostIds.GhostsPerScene, testWorld.ClientWorlds[0].GetExistingSystemManaged<VerifyGhostIds>().Matches, "Prespawn components added but didn't get ghost ID applied at runtime on client");
            }
        }

        [Test]
        public void PrespawnsCanGetRelevantAgain()
        {
            int rows = 5;
            int columns = 2;
            var ghost = SubSceneHelper.CreateSimplePrefab(ScenePath, "ghost", typeof(GhostAuthoringComponent));
            var parentScene = SubSceneHelper.CreateEmptyScene(ScenePath, "Scene1");
            var subScene = SubSceneHelper.CreateSubScene(parentScene, Path.GetDirectoryName(parentScene.path), $"SubScene1", rows, columns, ghost,
                    Vector3.zero);
            SceneManager.SetActiveScene(parentScene);

            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1, true);
                SubSceneHelper.LoadSubSceneInWorlds(testWorld, subScene);

                testWorld.Connect(1.0f / 60f, 4);
                testWorld.GoInGame();

                for(int i=0;i<16;++i)
                    testWorld.Tick(1.0f/60.0f);

                ref var ghostRelevancy = ref testWorld.GetSingletonRW<GhostRelevancy>(testWorld.ServerWorld).ValueRW;
                ghostRelevancy.GhostRelevancyMode = GhostRelevancyMode.SetIsIrrelevant;
                var relevancySet = ghostRelevancy.GhostRelevancySet;
                var query = testWorld.ServerWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<GhostInstance>(), ComponentType.ReadOnly<PreSpawnedGhostIndex>());
                var ghostComponents = query.ToComponentDataArray<GhostInstance>(Allocator.Temp);
                for (int i = 0; i < ghostComponents.Length; ++i)
                {
                    var ghostId = ghostComponents[i].ghostId;
                    relevancySet.Add(new RelevantGhostForConnection(1, ghostId), 1);
                }

                for(int i=0;i<16;++i)
                    testWorld.Tick(1.0f/60.0f);

                // Verify all ghosts have despawned
                query = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<GhostInstance>(), ComponentType.ReadOnly<PreSpawnedGhostIndex>());
                Assert.AreEqual(0, query.CalculateEntityCount());

                relevancySet.Clear();

                for(int i=0;i<16;++i)
                    testWorld.Tick(1.0f/60.0f);

                // Verify all ghosts have been spawned again
                query = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<GhostInstance>(), ComponentType.ReadOnly<PreSpawnedGhostIndex>());
                //+1 because there is also the scene list
                Assert.AreEqual(rows*columns, query.CalculateEntityCount());
            }
        }
    }
}
