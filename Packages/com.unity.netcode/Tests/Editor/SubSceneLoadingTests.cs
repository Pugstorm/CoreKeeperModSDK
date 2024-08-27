using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode.PrespawnTests;
using UnityEditor;
using UnityEngine;
using Unity.Scenes;
using Unity.Transforms;
using UnityEngine.SceneManagement;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.NetCode.Tests
{
    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(GhostCollectionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial class LoadingGhostCollectionSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var collectionEntity = SystemAPI.GetSingletonEntity<GhostCollection>();
            var ghostCollection = EntityManager.GetBuffer<GhostCollectionPrefab>(collectionEntity);
            var subScenes = GetEntityQuery(ComponentType.ReadOnly<SubSceneWithPrespawnGhosts>()).ToEntityArray(Allocator.Temp);
            var anyLoaded = false;
            for (int i = 0; i < subScenes.Length; ++i)
                anyLoaded |= SceneSystem.IsSceneLoaded(World.Unmanaged, subScenes[i]);
            for (int g = 0; g < ghostCollection.Length; ++g)
            {
                var ghost = ghostCollection[g];
                if (ghost.GhostPrefab == Entity.Null && !anyLoaded)
                {
                    ghost.Loading = GhostCollectionPrefab.LoadingState.LoadingActive;
                    ghostCollection[g] = ghost;
                }
            }
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial class UpdatePrespawnGhostTransform : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<SubScenePrespawnBaselineResolved>();
        }

        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            Entities
                .WithAll<PreSpawnedGhostIndex>()
                .ForEach((ref LocalTransform transform) =>
                {
                    transform.Position = new float3(transform.Position.x, transform.Position.y + deltaTime*60.0f, transform.Position.z);
                }).Schedule();
        }
    }

    static class SubSceneStreamingTestHelper
    {
        static public DynamicBuffer<PrespawnSceneLoaded> GetPrespawnLoaded(in NetCodeTestWorld testWorld, World world)
        {
            var collection = testWorld.TryGetSingletonEntity<PrespawnSceneLoaded>(world);
            Assert.AreNotEqual(Entity.Null, collection, "The PrespawnLoaded entity does not exist");
            return world.EntityManager.GetBuffer<PrespawnSceneLoaded>(collection);
        }
    }

    public partial class SubSceneLoadingTests : TestWithSceneAsset
    {
        [Test]
        public void SubSceneListIsSentToClient()
        {
            //Set the scene with multiple prefab types
            const int numObjects = 10;
            var prefab1 = SubSceneHelper.CreateSimplePrefab(ScenePath, "WithData1", typeof(GhostAuthoringComponent),
                typeof(SomeDataAuthoring));
            var prefab2 = SubSceneHelper.CreateSimplePrefab(ScenePath, "WithData2", typeof(GhostAuthoringComponent),
                typeof(SomeDataElementAuthoring));
            var parentScene = SubSceneHelper.CreateEmptyScene(ScenePath, "LateJoinTest");
            var subScene = SubSceneHelper.CreateSubSceneWithPrefabs(parentScene, ScenePath, "subscene", new[]
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
                testWorld.GoInGame();
                var query = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PrespawnsSceneInitialized>());
                Assert.IsTrue(query.IsEmptyIgnoreFilter);
                //First tick
                // - the Populate prespawn should run and add the ghosts to the mapping on the server.
                // - the scene list is populated
                testWorld.Tick(frameTime);
                query = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SubScenePrespawnBaselineResolved>());
                Assert.IsFalse(query.IsEmptyIgnoreFilter);
                //On the client we should received the prefabs. But prespawn asn subscenes are not initialized now (next frame)
                query = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SubScenePrespawnBaselineResolved>());
                Assert.IsTrue(query.IsEmptyIgnoreFilter);
                //Second tick: server will send the subscene list ghost
                testWorld.Tick(frameTime);
                query = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PrespawnsSceneInitialized>());
                Assert.IsFalse(query.IsEmptyIgnoreFilter);
                query = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SubScenePrespawnBaselineResolved>());
                Assert.IsFalse(query.IsEmptyIgnoreFilter);
                //Third tick: prespawn ghost start streaming
                for (int i = 0; i < 10; ++i)
                {
                    testWorld.Tick(frameTime);
                    var collection = testWorld.TryGetSingletonEntity<PrespawnSceneLoaded>(testWorld.ClientWorlds[0]);
                    if(collection != Entity.Null)
                        break;
                }
                var prespawnLoaded = SubSceneStreamingTestHelper.GetPrespawnLoaded(testWorld, testWorld.ClientWorlds[0]);
                Assert.AreEqual(1, prespawnLoaded.Length);

                //Need one more tick now to have the ghost map updated
                testWorld.Tick(frameTime);

                var sendGhostMapSingleton = testWorld.TryGetSingletonEntity<SpawnedGhostEntityMap>(testWorld.ServerWorld);
                var sendGhostMap = testWorld.ServerWorld.EntityManager.GetComponentData<SpawnedGhostEntityMap>(sendGhostMapSingleton);
                Assert.AreEqual(21, sendGhostMap.Value.Count());
                var recvGhostMapSingleton = testWorld.TryGetSingletonEntity<SpawnedGhostEntityMap>(testWorld.ClientWorlds[0]);
                var recvGhostMap = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SpawnedGhostEntityMap>(recvGhostMapSingleton);
                Assert.AreEqual(21, recvGhostMap.ClientGhostEntityMap.Count());
                Assert.AreEqual(21, recvGhostMap.Value.Count());
                //Check that they are identically mapped.
                foreach (var kv in sendGhostMap.Value)
                {
                    var ghost = kv.Key;
                    if (PrespawnHelper.IsRuntimeSpawnedGhost(ghost.ghostId))
                        continue;
                    var serverPrespawnId = testWorld.ServerWorld.EntityManager.GetComponentData<PreSpawnedGhostIndex>(kv.Value);
                    Assert.AreEqual(PrespawnHelper.MakePrespawnGhostId(serverPrespawnId.Value + 1), ghost.ghostId);
                    var clientGhost = recvGhostMap.Value[ghost];
                    var clientPrespawnId = testWorld.ClientWorlds[0].EntityManager.GetComponentData<PreSpawnedGhostIndex>(clientGhost);
                    Assert.AreEqual(PrespawnHelper.MakePrespawnGhostId(clientPrespawnId.Value + 1), ghost.ghostId);
                    Assert.AreEqual(serverPrespawnId.Value, clientPrespawnId.Value);
                }
            }
        }

        struct SetSomeDataJob : IJobChunk
        {
            public ComponentTypeHandle<SomeData> someDataHandle;
            public int offset;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // This job is not written to support queries with enableable component types.
                Assert.IsFalse(useEnabledMask);

                var array = chunk.GetNativeArray(ref someDataHandle);
                for (int i = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; ++i)
                {
                    array[i] = new SomeData {Value = offset + i};
                }
            }
        }

        [Test]
        public void ClientLoadSceneWhileInGame()
        {
            //The test is composed by two subscene.
            //The server load both scenes before having clients in game.
            //The client will load only the first one and then the second one after a bit

            const int numObjects = 5;
            var ghostPrefab = SubSceneHelper.CreateSimplePrefab(ScenePath, "WithData1", typeof(GhostAuthoringComponent),
                typeof(SomeDataAuthoring));
            var parentScene = SubSceneHelper.CreateEmptyScene(ScenePath, "StreamTest");
            var sub0 = SubSceneHelper.CreateSubSceneWithPrefabs(
                parentScene,
                ScenePath, "Sub0", new[]
                {
                    ghostPrefab,
                }, numObjects);
            var sub1 = SubSceneHelper.CreateSubSceneWithPrefabs(
                parentScene,
                ScenePath, "sub1", new[]
                {
                    ghostPrefab,
                }, numObjects, 5.0f);

            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1);
                //Stream the sub scene in
                SubSceneHelper.LoadSubScene(testWorld.ServerWorld, sub0, sub1);
                SubSceneHelper.LoadSubScene(testWorld.ClientWorlds[0], sub0);
                float frameTime = 1.0f / 60.0f;
                testWorld.Connect(frameTime);
                testWorld.GoInGame();
                for (int i = 0; i < 16; ++i)
                {
                    testWorld.Tick(frameTime);
                }

                var someDataQuery = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SomeData>());
                Assert.IsFalse(someDataQuery.IsEmptyIgnoreFilter);
                Assert.AreEqual(5, someDataQuery.CalculateEntityCount());

                //Modify some data on the server
                var subsceneList = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SubSceneWithPrespawnGhosts>())
                    .ToComponentDataArray<SubSceneWithPrespawnGhosts>(Allocator.Temp);
                var q = testWorld.ServerWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<PreSpawnedGhostIndex>(),
                    ComponentType.ReadWrite<SomeData>(), ComponentType.ReadOnly<SubSceneGhostComponentHash>());
                for (int i = 0; i < subsceneList.Length; ++i)
                {
                    q.SetSharedComponentFilter(new SubSceneGhostComponentHash
                    {
                        Value = subsceneList[i].SubSceneHash
                    });
                    var job = new SetSomeDataJob
                    {
                        someDataHandle = testWorld.ServerWorld.EntityManager.GetComponentTypeHandle<SomeData>(false),
                        offset = 100 + i * 100
                    };
                    Unity.Entities.Internal.InternalCompilerInterface.JobChunkInterface.RunWithoutJobs(ref job, q);
                }

                SubSceneHelper.LoadSubScene(testWorld.ClientWorlds[0], sub1);
                //Run some frame.
                for (int i = 0; i < 16; ++i)
                {
                    testWorld.Tick(frameTime);
                }

                q = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PreSpawnedGhostIndex>());
                Assert.AreEqual(10, q.CalculateEntityCount());

                //Check everything is in sync
                q = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<PreSpawnedGhostIndex>(),
                    ComponentType.ReadWrite<SomeData>(), ComponentType.ReadOnly<SubSceneGhostComponentHash>());
                for (int i = 0; i < subsceneList.Length; ++i)
                {
                    q.SetSharedComponentFilter(new SubSceneGhostComponentHash
                    {
                        Value = subsceneList[i].SubSceneHash
                    });
                    var data = q.ToComponentDataArray<SomeData>(Allocator.Temp);
                    Assert.AreEqual(1, q.CalculateChunkCount());
                    for (int d = 0; d < numObjects; ++d)
                    {
                        Assert.AreEqual(100 + 100 * i + d, data[d].Value);
                    }
                    data.Dispose();
                }
            }
        }

        [Test]
        public void ServerAndClientsLoadSceneInGame()
        {
            //The test is composed by one scene.
            //The server and client starts without scene loaded.
            //The server initiate the load first
            //The client will then follow and load the scene as well.
            //Ghost should be synched



            const int numObjects = 5;
            var ghostPrefab = SubSceneHelper.CreateSimplePrefab(ScenePath, "WithData1", typeof(GhostAuthoringComponent),
                typeof(SomeDataAuthoring));
            var parentScene = SubSceneHelper.CreateEmptyScene(ScenePath, "StreamTest");
            var sub0 = SubSceneHelper.CreateSubSceneWithPrefabs(
                parentScene,
                ScenePath, "Sub0", new[]
                {
                    ghostPrefab,
                }, numObjects);
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(LoadingGhostCollectionSystem));
                testWorld.CreateWorlds(true, 1);
                //Just create the scene entities proxies but not load any content
                SubSceneHelper.LoadSceneSceneProxies(sub0.SceneGUID, testWorld, 1.0f/60.0f, 200);
                float frameTime = 1.0f / 60.0f;
                testWorld.Connect(frameTime);
                testWorld.GoInGame();
                //Run some frames, nothing should be synched or sent here
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);
                Assert.AreEqual(Entity.Null, testWorld.TryGetSingletonEntity<PrespawnSceneLoaded>(testWorld.ServerWorld));
                Assert.AreEqual(Entity.Null, testWorld.TryGetSingletonEntity<PrespawnSceneLoaded>(testWorld.ClientWorlds[0]));
                //Server will load first. Wait some frame
                SubSceneHelper.LoadSubSceneAsync(testWorld.ServerWorld, testWorld, sub0.SceneGUID, frameTime);
                //No subscene are ready on the client
                Assert.IsTrue(testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PrespawnsSceneInitialized>()).IsEmpty);
                Assert.IsTrue(testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SubScenePrespawnBaselineResolved>()).IsEmpty);
                //Run some frames, so the ghost scene list is synchronized
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);
                var subSceneList = SubSceneStreamingTestHelper.GetPrespawnLoaded(testWorld, testWorld.ClientWorlds[0]);
                Assert.AreEqual(1, subSceneList.Length);
                //Client load the scene now
                SubSceneHelper.LoadSubSceneAsync(testWorld.ClientWorlds[0], testWorld, sub0.SceneGUID, frameTime);
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);
                //Modify the data on the server
                {
                    var q = testWorld.ServerWorld.EntityManager.CreateEntityQuery(
                        ComponentType.ReadOnly<PreSpawnedGhostIndex>(), ComponentType.ReadWrite<SomeData>());
                    var job = new SetSomeDataJob
                    {
                        someDataHandle = testWorld.ServerWorld.EntityManager.GetComponentTypeHandle<SomeData>(false),
                        offset = 100
                    };
                    Unity.Entities.Internal.InternalCompilerInterface.JobChunkInterface.RunWithoutJobs(ref job, q);
                }

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);
                //Check everything is in sync
                {
                    var q = testWorld.ServerWorld.EntityManager.CreateEntityQuery(
                        ComponentType.ReadOnly<PreSpawnedGhostIndex>(),
                        ComponentType.ReadWrite<SomeData>());
                    var data = q.ToComponentDataArray<SomeData>(Allocator.Temp);
                    for (int i = 0; i < numObjects; ++i)
                    {
                        Assert.AreEqual(100 + i, data[i].Value);
                    }
                    data.Dispose();
                }
            }
        }

        [Test]
        public void ServerInitiatedSceneUnload()
        {
            Dictionary<ulong, uint2> GetIdsRanges(World world, in DynamicBuffer<PrespawnSceneLoaded> subSceneList)
            {
                //Get all the ids and collect the ranges from the ghost components. They are going to be used later
                //for checking ids re-use
                var ranges = new Dictionary<ulong, uint2>();
                using var q = world.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<GhostInstance>(),
                    ComponentType.ReadOnly<SubSceneGhostComponentHash>());
                for (int i = 0; i < subSceneList.Length; ++i)
                {
                    q.SetSharedComponentFilter(new SubSceneGhostComponentHash
                    {
                        Value = subSceneList[i].SubSceneHash
                    });
                    var ghostComponents = q.ToComponentDataArray<GhostInstance>(Allocator.Temp);
                    var range = new uint2(uint.MaxValue, uint.MinValue);
                    for (int k = 0; k < ghostComponents.Length; ++k)
                    {
                        range.x = math.min(range.x, (uint)ghostComponents[k].ghostId);
                        range.y = math.max(range.y, (uint)ghostComponents[k].ghostId);
                    }
                    ranges.Add(subSceneList[i].SubSceneHash, range);
                    ghostComponents.Dispose();
                }

                return ranges;
            }

            //The test is composed by two scene.
            //The server and client starts with both scene loaded.
            //The server will unload one scene
            //The client will then follow (after a bit) and unload the scene as well.
            //The server and the client will then reload the scene again
            const int numObjects = 5;
            var ghostPrefab = SubSceneHelper.CreateSimplePrefab(ScenePath, "WithData1", typeof(GhostAuthoringComponent),
                typeof(SomeDataAuthoring));
            var parentScene = SubSceneHelper.CreateEmptyScene(ScenePath, "StreamTest");
            var sub0 = SubSceneHelper.CreateSubSceneWithPrefabs(
                parentScene,
                ScenePath, "Sub0", new[]
                {
                    ghostPrefab,
                }, numObjects);
            SubSceneHelper.CreateSubSceneWithPrefabs(
                parentScene,
                ScenePath, "Sub1", new[]
                {
                    ghostPrefab,
                }, numObjects);
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1);
                SubSceneHelper.LoadSubSceneInWorlds(testWorld);
                float frameTime = 1.0f / 60.0f;
                testWorld.Connect(frameTime);
                testWorld.GoInGame();
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                var subSceneList = SubSceneStreamingTestHelper.GetPrespawnLoaded(testWorld, testWorld.ServerWorld);
                var idsRanges = GetIdsRanges(testWorld.ServerWorld, subSceneList);
                //Server will unload the first scene. This will despawn ghosts and also update the scene list
                SceneSystem.UnloadScene(testWorld.ServerWorld.Unmanaged, sub0.SceneGUID, SceneSystem.UnloadParameters.DestroyMetaEntities);
                for (int i = 0; i < 16; ++i)
                {
                    testWorld.Tick(frameTime);
                }
                //Scene list should be 1 now
                subSceneList = SubSceneStreamingTestHelper.GetPrespawnLoaded(testWorld, testWorld.ServerWorld);
                Assert.AreEqual(1, subSceneList.Length);
                subSceneList = SubSceneStreamingTestHelper.GetPrespawnLoaded(testWorld, testWorld.ClientWorlds[0]);
                Assert.AreEqual(1, subSceneList.Length);
                //Only 5 ghost should be present on both
                var query = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PreSpawnedGhostIndex>());
                Assert.AreEqual(numObjects, query.CalculateEntityCount());
                query = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PreSpawnedGhostIndex>());
                Assert.AreEqual(numObjects, query.CalculateEntityCount());
                //Unload the scene on the client too
                SceneSystem.UnloadScene(testWorld.ClientWorlds[0].Unmanaged, sub0.SceneGUID, SceneSystem.UnloadParameters.DestroyMetaEntities);
                //And nothing should break
                for (int i = 0; i < 16; ++i)
                {
                    testWorld.Tick(frameTime);
                }
                //Then re-load the scene. The ids should be reused and everything should be in sync again
                SubSceneHelper.LoadSubScene(testWorld.ServerWorld, sub0);
                for (int i = 0; i < 16; ++i)
                {
                    testWorld.Tick(frameTime);
                }
                subSceneList = SubSceneStreamingTestHelper.GetPrespawnLoaded(testWorld, testWorld.ServerWorld);
                Assert.AreEqual(2, subSceneList.Length);
                subSceneList = SubSceneStreamingTestHelper.GetPrespawnLoaded(testWorld, testWorld.ClientWorlds[0]);
                Assert.AreEqual(2, subSceneList.Length);
                //Check that the assigned id for the sub0 are the same as before
                var newRanges = GetIdsRanges(testWorld.ServerWorld, subSceneList);
                for (int i = 0; i < subSceneList.Length; ++i)
                    Assert.AreEqual(idsRanges[subSceneList[i].SubSceneHash], newRanges[subSceneList[i].SubSceneHash]);
                SubSceneHelper.LoadSubScene(testWorld.ClientWorlds[0], sub0);
                for (int i = 0; i < 16; ++i)
                {
                    testWorld.Tick(frameTime);
                }
            }
        }

        [Test]
        public void ClientLoadUnloadScene()
        {
            const int numObjects = 5;
            var ghostPrefab = SubSceneHelper.CreateSimplePrefab(ScenePath, "WithData1", typeof(GhostAuthoringComponent),
                typeof(SomeDataAuthoring));
            var parentScene = SubSceneHelper.CreateEmptyScene(ScenePath, "StreamTest");
            var subScenes = new SubScene[4];
            for(int i=0;i<4;++i)
            {
                subScenes[i] = SubSceneHelper.CreateSubSceneWithPrefabs(
                    parentScene,
                    ScenePath, $"Sub{i}", new[]
                    {
                        ghostPrefab,
                    }, numObjects);
            }
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(LoadingGhostCollectionSystem), typeof(UpdatePrespawnGhostTransform));
                testWorld.CreateWorlds(true, 1);
                float frameTime = 1.0f / 60.0f;
                SubSceneHelper.LoadSubScene(testWorld.ServerWorld, subScenes);
                testWorld.Connect(frameTime);
                //Here it is already required to have something that tell the client he need to load the prefabs
                testWorld.GoInGame();
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                var subSceneList = SubSceneStreamingTestHelper.GetPrespawnLoaded(testWorld, testWorld.ServerWorld);
                Assert.AreEqual(4, subSceneList.Length);

                //Load/Unload all the scene 1 by 1
                for (int scene = 0; scene < 2; ++scene)
                {
                    //Client load the first scene
                    SubSceneHelper.LoadSubSceneAsync(testWorld.ClientWorlds[0], testWorld, subScenes[scene].SceneGUID, frameTime);
                    //Run another bunch of frame to have the scene initialized
                    for (int i = 0; i < 4; ++i)
                        testWorld.Tick(frameTime);
                    var subSceneEntity = testWorld.TryGetSingletonEntity<PrespawnsSceneInitialized>(testWorld.ClientWorlds[0]);
                    Assert.AreNotEqual(Entity.Null, subSceneEntity);
                    //Only 5 ghost should be present
                    var query = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PreSpawnedGhostIndex>(), ComponentType.ReadOnly<LocalTransform>());
                    Assert.AreEqual(numObjects, query.CalculateEntityCount());

                    //Now I should receive the ghost with their state changed
                    for (int i = 0; i < 16; ++i)
                        testWorld.Tick(frameTime);

                    var translations = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);

                    for (int i = 0; i < translations.Length; ++i)
                        Assert.AreNotEqual(0.0f, translations[i]);

                    //Unload the scene on the client
                    SceneSystem.UnloadScene(
                        testWorld.ClientWorlds[0].Unmanaged,
                        subScenes[scene].SceneGUID);
                    for (int i = 0; i < 16; ++i)
                        testWorld.Tick(frameTime);
                    //0 ghost should be preset
                    query = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PreSpawnedGhostIndex>());
                    Assert.AreEqual(0, query.CalculateEntityCount());
                }
            }
        }

        [Test]
        public void ClientReceiveDespawedGhostsWhenReloadingScene()
        {
            const int numObjects = 5;
            var ghostPrefab = SubSceneHelper.CreateSimplePrefab(ScenePath, "SimpleGhost", typeof(GhostAuthoringComponent),
                typeof(SomeDataAuthoring));
            var parentScene = SubSceneHelper.CreateEmptyScene(ScenePath, "StreamTest");
            var subScenes = new SubScene[2];
            for(int i=0;i<subScenes.Length;++i)
            {
                subScenes[i] = SubSceneHelper.CreateSubSceneWithPrefabs(
                    parentScene,
                    ScenePath, $"Sub{i}", new[]
                    {
                        ghostPrefab,
                    }, numObjects);
            }

            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1);
                SubSceneHelper.LoadSubScene(testWorld.ServerWorld, subScenes);
                SubSceneHelper.LoadSubScene(testWorld.ClientWorlds[0], subScenes[0]);

                testWorld.Connect(1.0f / 60f);
                testWorld.GoInGame();

                //synch scene 0 but not scene 1
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(1.0f / 60.0f);

                var sendGhostMapSingleton = testWorld.TryGetSingletonEntity<SpawnedGhostEntityMap>(testWorld.ServerWorld);
                var spawnMap = testWorld.ServerWorld.EntityManager.GetComponentData<SpawnedGhostEntityMap>(sendGhostMapSingleton).Value;
                //Host despawn 2 ghost in scene 0 and 2 ghost in scene 1
                var despawnedGhosts = new[]
                {
                    new SpawnedGhost
                    {
                        ghostId = PrespawnHelper.MakePrespawnGhostId(1),
                        spawnTick = NetworkTick.Invalid
                    },
                    new SpawnedGhost
                    {
                        ghostId = PrespawnHelper.MakePrespawnGhostId(4),
                        spawnTick = NetworkTick.Invalid
                    },
                    new SpawnedGhost
                    {
                        ghostId = PrespawnHelper.MakePrespawnGhostId(8),
                        spawnTick = NetworkTick.Invalid
                    },
                    new SpawnedGhost
                    {
                        ghostId = PrespawnHelper.MakePrespawnGhostId(9),
                        spawnTick = NetworkTick.Invalid
                    },
                };

                //Swap the element in the list to match the query order
                var query = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SceneSectionData>());
                var sceneSectionDatas = query.ToComponentDataArray<SceneSectionData>(Allocator.Temp);
                if (sceneSectionDatas[0].SceneGUID != subScenes[0].SceneGUID)
                {
                    var t1 = despawnedGhosts[0];
                    despawnedGhosts[0] = despawnedGhosts[2];
                    despawnedGhosts[2] = t1;
                    t1 = despawnedGhosts[1];
                    despawnedGhosts[1] = despawnedGhosts[3];
                    despawnedGhosts[3] = t1;
                }

                for(int i=0;i<despawnedGhosts.Length;++i)
                    testWorld.ServerWorld.EntityManager.DestroyEntity(spawnMap[despawnedGhosts[i]]);

                //Client should despawn the two ghosts in scene 0
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(1.0f / 60.0f);

                var recvGhostMapSingleton = testWorld.TryGetSingletonEntity<SpawnedGhostEntityMap>(testWorld.ClientWorlds[0]);
                var clientSpawnMap = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SpawnedGhostEntityMap>(recvGhostMapSingleton).Value;
                //3 prespawn and 1 ghost for the list
                Assert.AreEqual(4, clientSpawnMap.Count());
                Assert.IsFalse(clientSpawnMap.ContainsKey(despawnedGhosts[0]));
                Assert.IsFalse(clientSpawnMap.ContainsKey(despawnedGhosts[1]));

                //Client load scene 2. Should receive the despawn
                SubSceneHelper.LoadSubSceneAsync(testWorld.ClientWorlds[0], testWorld, subScenes[1].SceneGUID, 1.0f/60.0f);
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(1.0f / 60.0f);

                clientSpawnMap = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SpawnedGhostEntityMap>(recvGhostMapSingleton).Value;
                //6 prespawn and 1 ghost for the list
                Assert.AreEqual(7, clientSpawnMap.Count());
                Assert.IsFalse(clientSpawnMap.ContainsKey(despawnedGhosts[0]));
                Assert.IsFalse(clientSpawnMap.ContainsKey(despawnedGhosts[1]));
                Assert.IsFalse(clientSpawnMap.ContainsKey(despawnedGhosts[2]));
                Assert.IsFalse(clientSpawnMap.ContainsKey(despawnedGhosts[3]));

                //Client unload scene 0 and then reload it later it should receive the despawns
                //Unload the scene on the client
                SceneSystem.UnloadScene(testWorld.ClientWorlds[0].Unmanaged,
                    subScenes[0].SceneGUID);
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(1.0f / 60.0f);

                clientSpawnMap = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SpawnedGhostEntityMap>(recvGhostMapSingleton).Value;
                //3 prespawn and 1 ghost for the list
                Assert.AreEqual(4, clientSpawnMap.Count());
                Assert.IsFalse(clientSpawnMap.ContainsKey(despawnedGhosts[2]));
                Assert.IsFalse(clientSpawnMap.ContainsKey(despawnedGhosts[3]));

                SubSceneHelper.LoadSubSceneAsync(testWorld.ClientWorlds[0], testWorld, subScenes[0].SceneGUID, 1.0f/60.0f);
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(1.0f / 60.0f);

                clientSpawnMap = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SpawnedGhostEntityMap>(recvGhostMapSingleton).Value;
                //6 prespawn and 1 ghost for the list
                Assert.AreEqual(7, clientSpawnMap.Count());
                Assert.IsFalse(clientSpawnMap.ContainsKey(despawnedGhosts[0]));
                Assert.IsFalse(clientSpawnMap.ContainsKey(despawnedGhosts[1]));
                Assert.IsFalse(clientSpawnMap.ContainsKey(despawnedGhosts[2]));
                Assert.IsFalse(clientSpawnMap.ContainsKey(despawnedGhosts[3]));
            }
        }
    }
}
