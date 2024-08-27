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
    public class GhostTypeIndexConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            baker.DependsOn(gameObject);
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent(entity, new GhostTypeIndex {Value = gameObject.name == "GhostTypeIndex1Test" ? 1 : 0});
        }
    }

    public struct GhostTypeIndex : IComponentData
    {
        [GhostField] public int Value;
    }
    public class GhostTypeTests
    {
        void VerifyGhostTypes(World w)
        {
            var type = ComponentType.ReadOnly<GhostTypeIndex>();
            using var query = w.EntityManager.CreateEntityQuery(type);
            var count = new NativeArray<int>(2, Allocator.Temp);
            var ghosts = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < ghosts.Length; ++i)
            {
                var typeIndex = w.EntityManager.GetComponentData<GhostTypeIndex>(ghosts[i]);
                count[typeIndex.Value] = count[typeIndex.Value] + 1;
            }
            Assert.AreEqual(2, count[0]);
            Assert.AreEqual(2, count[1]);
        }
        [Test]
        public void GhostsWithSameArchetypeAreDifferent()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var ghostGameObject0 = new GameObject();
                ghostGameObject0.AddComponent<TestNetCodeAuthoring>().Converter = new GhostTypeIndexConverter();
                ghostGameObject0.name = "GhostTypeIndex0Test";

                var ghostGameObject1 = new GameObject();
                ghostGameObject1.AddComponent<TestNetCodeAuthoring>().Converter = new GhostTypeIndexConverter();
                ghostGameObject1.name = "GhostTypeIndex1Test";

                Assert.IsTrue(testWorld.CreateGhostCollection(
                    ghostGameObject0, ghostGameObject1));

                testWorld.CreateWorlds(true, 1);

                testWorld.SpawnOnServer(ghostGameObject0);
                testWorld.SpawnOnServer(ghostGameObject0);
                testWorld.SpawnOnServer(ghostGameObject1);
                testWorld.SpawnOnServer(ghostGameObject1);

                VerifyGhostTypes(testWorld.ServerWorld);

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                // Go in-game
                testWorld.GoInGame();

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 64; ++i)
                    testWorld.Tick(frameTime);

                // Assert that replicated version is correct
                VerifyGhostTypes(testWorld.ClientWorlds[0]);
            }
        }
    }
}
