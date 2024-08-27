using NUnit.Framework;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.NetCode.Tests;
using Unity.Jobs;
using UnityEngine;
using Unity.NetCode;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

namespace Unity.NetCode.Tests
{
    public class GhostGroupGhostConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent(entity, new GhostOwner());
            // Dependency on the name
            baker.DependsOn(gameObject);
            if (gameObject.name == "ParentGhost")
            {
                baker.AddBuffer<GhostGroup>(entity);
                baker.AddComponent(entity, default(GhostGroupRoot));
            }
            else
                baker.AddComponent(entity, default(GhostChildEntity));
        }
    }
    public struct GhostGroupRoot : IComponentData
    {}
    public class GhostGroupTests
    {
        [Test]
        public void EntityMarkedAsChildIsNotSent()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var ghostGameObject = new GameObject();
                ghostGameObject.name = "ParentGhost";
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GhostGroupGhostConverter();
                var childGhostGameObject = new GameObject();
                childGhostGameObject.name = "ChildGhost";
                childGhostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GhostGroupGhostConverter();

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject, childGhostGameObject));

                testWorld.CreateWorlds(true, 1);

                testWorld.SpawnOnServer(ghostGameObject);
                testWorld.SpawnOnServer(childGhostGameObject);

                var serverEnt = testWorld.TryGetSingletonEntity<GhostGroupRoot>(testWorld.ServerWorld);
                var serverChildEnt = testWorld.TryGetSingletonEntity<GhostChildEntity>(testWorld.ServerWorld);
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEnt, new GhostOwner{NetworkId = 42});
                testWorld.ServerWorld.EntityManager.SetComponentData(serverChildEnt, new GhostOwner{NetworkId = 43});

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                // Go in-game
                testWorld.GoInGame();

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 64; ++i)
                    testWorld.Tick(frameTime);

                // Check that the client world has the right thing and value
                var clientEnt = testWorld.TryGetSingletonEntity<GhostGroupRoot>(testWorld.ClientWorlds[0]);
                var clientChildEnt = testWorld.TryGetSingletonEntity<GhostChildEntity>(testWorld.ClientWorlds[0]);
                Assert.AreEqual(42, testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostOwner>(clientEnt).NetworkId);
                Assert.AreEqual(Entity.Null, clientChildEnt);
            }
        }
        [Test]
        public void EntityMarkedAsChildIsSentAsPartOfGroup()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var ghostGameObject = new GameObject();
                ghostGameObject.name = "ParentGhost";
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GhostGroupGhostConverter();
                var childGhostGameObject = new GameObject();
                childGhostGameObject.name = "ChildGhost";
                childGhostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GhostGroupGhostConverter();

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject, childGhostGameObject));

                testWorld.CreateWorlds(true, 1);

                testWorld.SpawnOnServer(ghostGameObject);
                testWorld.SpawnOnServer(childGhostGameObject);

                var serverEnt = testWorld.TryGetSingletonEntity<GhostGroupRoot>(testWorld.ServerWorld);
                var serverChildEnt = testWorld.TryGetSingletonEntity<GhostChildEntity>(testWorld.ServerWorld);
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEnt, new GhostOwner{NetworkId = 42});
                testWorld.ServerWorld.EntityManager.SetComponentData(serverChildEnt, new GhostOwner{NetworkId = 43});
                testWorld.ServerWorld.EntityManager.GetBuffer<GhostGroup>(serverEnt).Add(new GhostGroup{Value = serverChildEnt});

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                // Go in-game
                testWorld.GoInGame();

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 64; ++i)
                    testWorld.Tick(frameTime);

                // Check that the client world has the right thing and value
                var clientEnt = testWorld.TryGetSingletonEntity<GhostGroupRoot>(testWorld.ClientWorlds[0]);
                var clientChildEnt = testWorld.TryGetSingletonEntity<GhostChildEntity>(testWorld.ClientWorlds[0]);
                Assert.AreEqual(42, testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostOwner>(clientEnt).NetworkId);
                Assert.AreNotEqual(Entity.Null, clientChildEnt);
                Assert.AreEqual(43, testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostOwner>(clientChildEnt).NetworkId);
            }
        }
        [Test]
        public void CanHaveManyGhostGroupGhostTypes()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var ghostGameObjects = new GameObject[64];

                for (int i = 0; i < 32; ++i)
                {
                    var ghostGameObject = new GameObject();
                    ghostGameObject.name = "ParentGhost";
                    ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GhostGroupGhostConverter();
                    var childGhostGameObject = new GameObject();
                    childGhostGameObject.name = "ChildGhost";
                    childGhostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GhostGroupGhostConverter();

                    ghostGameObjects[i] = ghostGameObject;
                    ghostGameObjects[i+32] = childGhostGameObject;
                }

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObjects));

                testWorld.CreateWorlds(true, 1);

                for (int i = 0; i < 32; ++i)
                {
                    var serverEnt = testWorld.SpawnOnServer(ghostGameObjects[i]);
                    var serverChildEnt = testWorld.SpawnOnServer(ghostGameObjects[i+32]);

                    testWorld.ServerWorld.EntityManager.SetComponentData(serverEnt, new GhostOwner{NetworkId = 42});
                    testWorld.ServerWorld.EntityManager.SetComponentData(serverChildEnt, new GhostOwner{NetworkId = 43});
                    testWorld.ServerWorld.EntityManager.GetBuffer<GhostGroup>(serverEnt).Add(new GhostGroup{Value = serverChildEnt});
                }

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                // Go in-game
                testWorld.GoInGame();

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 64; ++i)
                    testWorld.Tick(frameTime);

                // Check that the client world has the right thing and value
                var ghostQuery = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(GhostOwner));
                var groupQuery = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(GhostGroup));
                Assert.AreEqual(64, ghostQuery.CalculateEntityCount());
                Assert.AreEqual(32, groupQuery.CalculateEntityCount());
            }
        }
        [Test]
        public void CanHaveManyGhostGroupsOfSameType()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var ghostGameObjects = new GameObject[2];

                for (int i = 0; i < 1; ++i)
                {
                    var ghostGameObject = new GameObject();
                    ghostGameObject.name = "ParentGhost";
                    ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GhostGroupGhostConverter();
                    var childGhostGameObject = new GameObject();
                    childGhostGameObject.name = "ChildGhost";
                    childGhostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GhostGroupGhostConverter();

                    ghostGameObjects[i] = ghostGameObject;
                    ghostGameObjects[i+1] = childGhostGameObject;
                }

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObjects));

                testWorld.CreateWorlds(true, 1);

                for (int i = 0; i < 32; ++i)
                {
                    var serverEnt = testWorld.SpawnOnServer(ghostGameObjects[0]);
                    var serverChildEnt = testWorld.SpawnOnServer(ghostGameObjects[1]);

                    testWorld.ServerWorld.EntityManager.SetComponentData(serverEnt, new GhostOwner{NetworkId = 42});
                    testWorld.ServerWorld.EntityManager.SetComponentData(serverChildEnt, new GhostOwner{NetworkId = 43});
                    testWorld.ServerWorld.EntityManager.GetBuffer<GhostGroup>(serverEnt).Add(new GhostGroup{Value = serverChildEnt});
                }

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                // Go in-game
                testWorld.GoInGame();

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 64; ++i)
                    testWorld.Tick(frameTime);

                // Check that the client world has the right thing and value
                var ghostQuery = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(GhostOwner));
                var groupQuery = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(typeof(GhostGroup));
                Assert.AreEqual(64, ghostQuery.CalculateEntityCount());
                Assert.AreEqual(32, groupQuery.CalculateEntityCount());
            }
        }
    }
}
