using System;
using System.IO;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode.PrespawnTests;
using Unity.Scenes;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.NetCode.Tests
{
    public class TestEnterExitGame : TestWithSceneAsset
    {
        private void UnloadSubScene(World world)
        {
            var subScene = Object.FindFirstObjectByType<SubScene>();
            SceneSystem.UnloadScene(world.Unmanaged, subScene.SceneGUID, SceneSystem.UnloadParameters.DestroyMetaEntities);
        }

        [Test]
        public void PrespawnSystemResetWhenExitGame()
        {
            const int numClients = 2;
            const int numObjects = 10;
            var prefab = SubSceneHelper.CreateSimplePrefab(ScenePath, "simple", typeof(GhostAuthoringComponent));
            var parentScene = SubSceneHelper.CreateEmptyScene(ScenePath, "TestEnterExit");
            var subScene = SubSceneHelper.CreateSubSceneWithPrefabs(parentScene, Path.GetDirectoryName(parentScene.path), "SubScene",
                new[] {prefab}, numObjects);
            using (var testWorld = new NetCodeTestWorld())
            {
                //Create a scene with a subscene and a bunch of objects in it
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, numClients);
                //Stream the sub scene in
                SubSceneHelper.LoadSubSceneInWorlds(testWorld);
                float frameTime = 1.0f / 60.0f;
                testWorld.Connect(frameTime);
                var firstTimeJoinStats = new uint[testWorld.ClientWorlds.Length * 3];
                var rejoinStats = new uint[testWorld.ClientWorlds.Length * 3];
                testWorld.GoInGame();
                int firstJoinTickCount = 0;
                int rejoinTickCount = 0;
                for(int i=0;i<32;++i)
                {
                    ++firstJoinTickCount;
                    testWorld.Tick(frameTime);
                    for (int client = 0; client < testWorld.ClientWorlds.Length; ++client)
                    {
                        var netStats = testWorld.ClientWorlds[client].EntityManager.GetComponentData<GhostStatsCollectionSnapshot>(testWorld.TryGetSingletonEntity<GhostStatsCollectionSnapshot>(testWorld.ClientWorlds[client])).Data;
                        //Gather some stats for later, it will be used to make some comparison
                        if (netStats.Length == 10)
                        {
                            firstTimeJoinStats[3 * client] += netStats[7]; //entities in the packet
                            firstTimeJoinStats[3 * client + 1] += netStats[8]; //byte received
                            firstTimeJoinStats[3 * client + 2] += netStats[9]; //uncompressed entities
                        }
                    }
                    if (firstTimeJoinStats[0] >= numObjects)
                        break;
                }
                //make each client exit and re-entering the game, one at the time.
                //verify that they are receiving the data we expect
                for (int client = 0; client < numClients; ++client)
                {
                    rejoinTickCount = 0;
                    testWorld.RemoveFromGame(client);
                    UnloadSubScene(testWorld.ClientWorlds[client]);
                    //Run some ticks to reset all the internal data structure.
                    for (int k = 0; k < 6; ++k)
                        testWorld.Tick(frameTime);
                    //Verify that all the mappings are empty
                    var netStats = testWorld.ClientWorlds[client].EntityManager.GetComponentData<GhostStatsCollectionSnapshot>(testWorld.TryGetSingletonEntity<GhostStatsCollectionSnapshot>(testWorld.ClientWorlds[client])).Data;
                    var recvGhostMapSingleton = testWorld.TryGetSingletonEntity<SpawnedGhostEntityMap>(testWorld.ClientWorlds[client]);
                    Assert.AreEqual(0, testWorld.ClientWorlds[client].EntityManager.GetComponentData<SpawnedGhostEntityMap>(recvGhostMapSingleton).Value.Count());
                    Assert.AreEqual(4, netStats.Length);
                    var inGame = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>(),
                        ComponentType.Exclude<NetworkStreamInGame>()).ToEntityArray(Allocator.Temp);
                    Assert.AreEqual(1, inGame.Length);
                    Assert.AreEqual(0, testWorld.ServerWorld.EntityManager.GetBuffer<PrespawnSectionAck>(inGame[0]).Length);
                    inGame.Dispose();
                    //Reconnect the client, it should get again all the data
                    SubSceneHelper.LoadSubScene(testWorld.ClientWorlds[client]);
                    testWorld.SetInGame(client);
                    //Re-run the exact same ticks count as the previous join. It should get the same data
                    for(int k=0;k<32;++k)
                    {
                        ++rejoinTickCount;
                        testWorld.Tick(frameTime);
                        netStats = testWorld.ClientWorlds[client].EntityManager.GetComponentData<GhostStatsCollectionSnapshot>(testWorld.TryGetSingletonEntity<GhostStatsCollectionSnapshot>(testWorld.ClientWorlds[client])).Data;
                        if (netStats.Length == 10)
                        {
                            rejoinStats[3 * client] += netStats[7]; //entities in the packet
                            rejoinStats[3 * client + 1] += netStats[8]; //byte received
                            rejoinStats[3 * client + 2] += netStats[9]; //uncompressed entities
                        }
                        if (rejoinStats[3 * client] >= numObjects)
                            break;
                    }
                    //Plus 1 to account for the extra tick due to the lazy prespawn initialization
                    //This is not reliable. Because of a bug in the ClientSystemGroup. Put a +1
                    Assert.IsTrue(rejoinTickCount>=firstJoinTickCount &&
                                  rejoinTickCount<firstJoinTickCount+2,
                                  "The number of ticks necessary to receive all the ghosts must be the same");
                    //Check that we received the exact same number of entities as we did when the client joined
                    Assert.AreEqual(rejoinStats[3 * client], firstTimeJoinStats[3 * client], "re-joining client must receive the same number of entities as the first time");
                    //Byte received can be a little different because of the ticks encoding so they could be not
                    //exact the same. 1 byte margin may be enough
                    const int extraMargin = 8;
                    Assert.GreaterOrEqual(rejoinStats[3 * client + 1], firstTimeJoinStats[3 * client + 1]);
                    Assert.LessOrEqual(rejoinStats[3 * client + 1], firstTimeJoinStats[3 * client + 1] + extraMargin);
                }

                //Exit from game. Stop streaming on both clients and server
                testWorld.ExitFromGame();
                UnloadSubScene(testWorld.ServerWorld);
                for (int i = 0; i < numClients; ++i)
                    UnloadSubScene(testWorld.ClientWorlds[i]);
                //Run at least one tick for proper reset of all the systems
                for (int k = 0; k < 4; ++k)
                    testWorld.Tick(frameTime);
                //What I want to check:
                // 1 - all data is clean up on the server
                // 2- no prespawn data present
                for (int i = 0; i < 2; ++i)
                {
                    var netStats = testWorld.ClientWorlds[i].EntityManager.GetComponentData<GhostStatsCollectionSnapshot>(testWorld.TryGetSingletonEntity<GhostStatsCollectionSnapshot>(testWorld.ClientWorlds[i])).Data;
                    var recvGhostMapSingleton = testWorld.TryGetSingletonEntity<SpawnedGhostEntityMap>(testWorld.ClientWorlds[i]);
                    Assert.AreEqual(0, testWorld.ClientWorlds[i].EntityManager.GetComponentData<SpawnedGhostEntityMap>(recvGhostMapSingleton).Value.Count(), "client spawn map must be empty");
                    Assert.AreEqual(Entity.Null, testWorld.TryGetSingletonEntity<SubScenePrespawnBaselineResolved>(testWorld.ClientWorlds[i]));
                    Assert.AreEqual(4, netStats.Length, "client ghost stats must be empty");

                    var appliedPredictionTicks = testWorld.ClientWorlds[i].EntityManager.GetComponentData<GhostPredictionGroupTickState>(testWorld.TryGetSingletonEntity<GhostPredictionGroupTickState>(testWorld.ClientWorlds[i])).AppliedPredictedTicks;
                    Assert.AreEqual(0, appliedPredictionTicks.Count(), "client prediction tick must be 0");
                }
                var sendGhostMapSingleton = testWorld.TryGetSingletonEntity<SpawnedGhostEntityMap>(testWorld.ServerWorld);
                Assert.AreEqual(0, testWorld.ServerWorld.EntityManager.GetComponentData<SpawnedGhostEntityMap>(sendGhostMapSingleton).Value.Count(), "server ghost map must be empty");
                Assert.AreEqual(Entity.Null, testWorld.TryGetSingletonEntity<SubScenePrespawnBaselineResolved>(testWorld.ServerWorld));
                Assert.AreEqual(0, testWorld.ServerWorld.EntityManager.GetComponentData<SpawnedGhostEntityMap>(sendGhostMapSingleton).ServerDestroyedPrespawns.Length, "server prespawn despawn list must be empty");
                var serverConnections = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>()).ToEntityArray(Allocator.Temp);
                Assert.AreEqual(0, testWorld.ServerWorld.EntityManager.GetBuffer<PrespawnSectionAck>(serverConnections[0]).Length);
                Assert.AreEqual(0, testWorld.ServerWorld.EntityManager.GetBuffer<PrespawnSectionAck>(serverConnections[1]).Length);
                //Re-enter the game and check that all the objects are received again. Same tick counts too
                SubSceneHelper.LoadSubSceneInWorlds(testWorld);
                testWorld.GoInGame();
                for (int i = 0; i < numClients; ++i)
                {
                    rejoinStats[3*i] = 0;
                    rejoinStats[3*i+1] = 0;
                    rejoinStats[3*i+2] = 0;
                }

                rejoinTickCount = 0;
                for (int i = 0; i < 16; ++i)
                {
                    ++rejoinTickCount;
                    testWorld.Tick(frameTime);
                    for (int client = 0; client < testWorld.ClientWorlds.Length; ++client)
                    {
                        var netStats = testWorld.ClientWorlds[client].EntityManager.GetComponentData<GhostStatsCollectionSnapshot>(testWorld.TryGetSingletonEntity<GhostStatsCollectionSnapshot>(testWorld.ClientWorlds[client])).Data;
                        if (netStats.Length == 10)
                        {
                            rejoinStats[3 * client] += netStats[7];
                            rejoinStats[3 * client + 1] += netStats[8];
                            rejoinStats[3 * client + 2] += netStats[9];
                        }
                    }
                    if (rejoinStats[0] >= numObjects)
                        break;
                }
                //Plus 1 to account for the extra tick due to the lazy prespawn initialization
                Assert.IsTrue(rejoinTickCount>=firstJoinTickCount &&
                              rejoinTickCount<firstJoinTickCount+2,
                    "re-joining the server should take the same number of ticks");
                for (int client = 0; client < testWorld.ClientWorlds.Length; ++client)
                {
                    Assert.AreEqual(firstTimeJoinStats[3*client], rejoinStats[3*client], "client must receive the same number of ghosts");
                    Assert.AreEqual(firstTimeJoinStats[3*client+2], rejoinStats[3*client+2], "client must received the same number of uncompressed entities (0)");
                    //Byte received can be a little different because of the ticks encoding. Since tick is increasing
                    //we can say almost greater than equals, up to a certain margin eventually
                    const int extraMargin = 8;
                    Assert.IsTrue(rejoinStats[3*client+1] >= firstTimeJoinStats[3*client+1] &&
                                  rejoinStats[3*client+1] <= firstTimeJoinStats[3*client+1] + extraMargin,
                        "client must receive ~same amount of bytes");
                }
            }
        }
    }
}
