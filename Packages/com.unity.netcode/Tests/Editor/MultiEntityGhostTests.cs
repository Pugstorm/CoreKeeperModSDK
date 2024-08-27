using System.Collections.Generic;
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
    public class MultiEntityGhostConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent(entity, new GhostOwner());
            baker.AddComponent(entity, new ChildLevelComponent());
            var transform = baker.GetComponent<Transform>();
            baker.DependsOn(transform.parent);
            if (transform.parent == null)
                baker.AddComponent(entity, new TopLevelGhostEntity());
        }
    }
    public struct TopLevelGhostEntity : IComponentData
    {}
    [GhostComponent(SendDataForChildEntity = false)]
    public struct ChildLevelComponent : IComponentData
    {
        [GhostField] public int Value;
    }
    public class MultiEntityGhostTests
    {
        [Test]
        public void ChildEntityDataReplicationCanBeDisabledViaFlagOnGhostComponentAttribute()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new MultiEntityGhostConverter();
                var childGhost = new GameObject();
                childGhost.transform.parent = ghostGameObject.transform;
                childGhost.AddComponent<TestNetCodeAuthoring>().Converter = new MultiEntityGhostConverter();

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);

                testWorld.SpawnOnServer(ghostGameObject);

                var serverEnt = testWorld.TryGetSingletonEntity<TopLevelGhostEntity>(testWorld.ServerWorld);
                Assert.IsTrue(testWorld.ServerWorld.EntityManager.HasComponent<LinkedEntityGroup>(serverEnt));
                var serverEntityGroup = testWorld.ServerWorld.EntityManager.GetBuffer<LinkedEntityGroup>(serverEnt);
                Assert.AreEqual(2, serverEntityGroup.Length);
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEntityGroup[0].Value, new ChildLevelComponent{Value = 42});
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEntityGroup[1].Value, new ChildLevelComponent{Value = 42});

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                // Go in-game
                testWorld.GoInGame();

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 64; ++i)
                    testWorld.Tick(frameTime);

                // Check that the client world has the right thing and value
                var clientEnt = testWorld.TryGetSingletonEntity<TopLevelGhostEntity>(testWorld.ClientWorlds[0]);
                Assert.IsTrue(testWorld.ClientWorlds[0].EntityManager.HasComponent<LinkedEntityGroup>(clientEnt));
                var clientEntityGroup = testWorld.ClientWorlds[0].EntityManager.GetBuffer<LinkedEntityGroup>(clientEnt);
                Assert.AreEqual(2, clientEntityGroup.Length);
                Assert.AreEqual(42, testWorld.ClientWorlds[0].EntityManager.GetComponentData<ChildLevelComponent>(clientEntityGroup[0].Value).Value);
                Assert.AreEqual(0, testWorld.ClientWorlds[0].EntityManager.GetComponentData<ChildLevelComponent>(clientEntityGroup[1].Value).Value);
            }
        }
        [Test]
        public void ChildEntityDataCanBeReplicatedViaFlagOnGhostComponentAttribute()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new MultiEntityGhostConverter();
                var childGhost = new GameObject();
                childGhost.transform.parent = ghostGameObject.transform;
                childGhost.AddComponent<TestNetCodeAuthoring>().Converter = new MultiEntityGhostConverter();

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);

                testWorld.SpawnOnServer(ghostGameObject);

                var serverEnt = testWorld.TryGetSingletonEntity<TopLevelGhostEntity>(testWorld.ServerWorld);
                Assert.IsTrue(testWorld.ServerWorld.EntityManager.HasComponent<LinkedEntityGroup>(serverEnt));
                var serverEntityGroup = testWorld.ServerWorld.EntityManager.GetBuffer<LinkedEntityGroup>(serverEnt);
                Assert.AreEqual(2, serverEntityGroup.Length);
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEntityGroup[0].Value, new GhostOwner{NetworkId = 42});
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEntityGroup[1].Value, new GhostOwner{NetworkId = 42});

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                // Go in-game
                testWorld.GoInGame();

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 64; ++i)
                    testWorld.Tick(frameTime);

                // Check that the client world has the right thing and value
                var clientEnt = testWorld.TryGetSingletonEntity<TopLevelGhostEntity>(testWorld.ClientWorlds[0]);
                Assert.IsTrue(testWorld.ClientWorlds[0].EntityManager.HasComponent<LinkedEntityGroup>(clientEnt));
                var clientEntityGroup = testWorld.ClientWorlds[0].EntityManager.GetBuffer<LinkedEntityGroup>(clientEnt);
                Assert.AreEqual(2, clientEntityGroup.Length);
                Assert.AreEqual(42, testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostOwner>(clientEntityGroup[0].Value).NetworkId);
                Assert.AreEqual(42, testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostOwner>(clientEntityGroup[1].Value).NetworkId);
            }
        }
    }
}
