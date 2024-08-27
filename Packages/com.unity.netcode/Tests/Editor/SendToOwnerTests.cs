using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using Unity.NetCode.LowLevel.Unsafe;

namespace Unity.NetCode.Tests
{
    class SendToOwnerTests
    {
        public class TestComponentConverter : TestNetCodeAuthoring.IConverter
        {
            public void Bake(GameObject gameObject, IBaker baker)
            {
                var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
                baker.AddComponent<GhostOwner>(entity);
                baker.AddComponent<GhostPredictedOnly>(entity);
                baker.AddComponent<GhostInterpolatedOnly>(entity);
                baker.AddComponent<GhostGen_IntStruct>(entity);
                baker.AddComponent<GhostTypeIndex>(entity);
                baker.AddBuffer<GhostGenBuffer_ByteBuffer>(entity);
                baker.AddBuffer<GhostGenTest_Buffer>(entity);
            }
        }

        void ChangeSendToOwnerOption(World world)
        {
            using var query = world.EntityManager.CreateEntityQuery(typeof(GhostCollection));
            var entity = query.GetSingletonEntity();
            var collection = world.EntityManager.GetBuffer<GhostComponentSerializer.State>(entity);
            for (int i = 0; i < collection.Length; ++i)
            {
                var c = collection[i];
                if (c.ComponentType.GetManagedType() == typeof(GhostGen_IntStruct))
                {
                    c.SendToOwner = SendToOwnerType.SendToOwner;
                    collection[i] = c;
                }
                else if (c.ComponentType.GetManagedType() == typeof(GhostTypeIndex))
                {
                    c.SendToOwner = SendToOwnerType.SendToNonOwner;
                    collection[i] = c;
                }
                else if (c.ComponentType.GetManagedType() == typeof(GhostPredictedOnly))
                {
                    c.SendToOwner = SendToOwnerType.SendToOwner;
                    collection[i] = c;
                }
                else if (c.ComponentType.GetManagedType() == typeof(GhostInterpolatedOnly))
                {
                    c.SendToOwner = SendToOwnerType.SendToNonOwner;
                    collection[i] = c;
                }
                else if (c.ComponentType.GetManagedType() == typeof(GhostGenTest_Buffer))
                {
                    c.SendToOwner = SendToOwnerType.SendToNonOwner;
                    collection[i] = c;
                }
            }
        }

        [Test]
        [TestCase(GhostModeMask.All, GhostMode.OwnerPredicted)]
        [TestCase(GhostModeMask.All, GhostMode.Interpolated)]
        [TestCase(GhostModeMask.All, GhostMode.Predicted)]
        [TestCase(GhostModeMask.Interpolated, GhostMode.Interpolated)]
        [TestCase(GhostModeMask.Predicted, GhostMode.Predicted)]
        public void SendToOwner_Clients_ReceiveTheCorrectData(GhostModeMask modeMask, GhostMode mode)
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true,typeof(InputSystem));

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new TestComponentConverter();
                var ghostConfig = ghostGameObject.AddComponent<GhostAuthoringComponent>();
                ghostConfig.SupportedGhostModes = modeMask;
                ghostConfig.DefaultGhostMode = mode;
                //Some context about where owner make sense:
                //interpolated ghost: does even make sense that a ghost has an owner? Yes, it does and it is usually the server.
                //                    Can be a player ??? Yes it can. In that case, the player can still control the ghost via command but it will not predict the
                //                    ghost movement. Only the server will compute the correct position. The client will always see a delayed and interpolated replica.
                //Predicted ghost: owner make absolutely sense.
                //OwnerPredicted: by definition
                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));
                testWorld.CreateWorlds(true, 2);
                //Here I do a trick: I will wait until the CollectionSystem is run and the component collection built.
                //Then I will change the serializer flags a little to make them behave the way I want.
                //This is a temporary hack, can be remove whe override per prefab will be available.
                using var queryServer = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostCollection>());
                using var queryClient0 = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostCollection>());
                using var queryClient1 = testWorld.ClientWorlds[1].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostCollection>());
                while (true)
                {
                    testWorld.Tick(1.0f/60f);
                    if (queryServer.IsEmptyIgnoreFilter || queryClient0.IsEmptyIgnoreFilter || queryClient1.IsEmptyIgnoreFilter)
                        continue;
                    if (testWorld.ServerWorld.EntityManager.GetBuffer<GhostComponentSerializer.State>(queryServer.GetSingletonEntity()).Length == 0 ||
                        testWorld.ServerWorld.EntityManager.GetBuffer<GhostComponentSerializer.State>(queryServer.GetSingletonEntity()).Length == 0 ||
                        testWorld.ServerWorld.EntityManager.GetBuffer<GhostComponentSerializer.State>(queryServer.GetSingletonEntity()).Length == 0)
                        continue;
                    ChangeSendToOwnerOption(testWorld.ServerWorld);
                    ChangeSendToOwnerOption(testWorld.ClientWorlds[0]);
                    ChangeSendToOwnerOption(testWorld.ClientWorlds[1]);
                    break;
                }

                testWorld.Connect(1.0f/60.0f);
                testWorld.GoInGame();

                var net1 = testWorld.TryGetSingletonEntity<NetworkId>(testWorld.ClientWorlds[0]);
                var netId1 = testWorld.ClientWorlds[0].EntityManager.GetComponentData<NetworkId>(net1);

                var serverEnt = testWorld.SpawnOnServer(ghostGameObject);
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEnt, new GhostGen_IntStruct {IntValue = 10000});
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEnt, new GhostTypeIndex {Value = 20000});
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEnt, new GhostPredictedOnly {Value = 30000});
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEnt, new GhostInterpolatedOnly {Value = 40000});
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEnt, new GhostOwner { NetworkId = netId1.Value});
                var serverBuffer1 = testWorld.ServerWorld.EntityManager.GetBuffer<GhostGenBuffer_ByteBuffer>(serverEnt);
                serverBuffer1.Capacity = 10;
                for (int i = 0; i < 10; ++i)
                    serverBuffer1.Add(new GhostGenBuffer_ByteBuffer{Value = (byte)(10 + i)});
                var serverBuffer2 = testWorld.ServerWorld.EntityManager.GetBuffer<GhostGenTest_Buffer>(serverEnt);
                serverBuffer2.Capacity = 10;
                for (int i = 0; i < 10; ++i)
                    serverBuffer2.Add(new GhostGenTest_Buffer());

                for(int i=0;i<16;++i)
                    testWorld.Tick(1.0f/60.0f);

                serverBuffer1 = testWorld.ServerWorld.EntityManager.GetBuffer<GhostGenBuffer_ByteBuffer>(serverEnt);
                serverBuffer2 = testWorld.ServerWorld.EntityManager.GetBuffer<GhostGenTest_Buffer>(serverEnt);
                var serverComp1 = testWorld.ServerWorld.EntityManager.GetComponentData<GhostGen_IntStruct>(serverEnt);
                var serverComp2 = testWorld.ServerWorld.EntityManager.GetComponentData<GhostTypeIndex>(serverEnt);
                var predictedOnly = testWorld.ServerWorld.EntityManager.GetComponentData<GhostPredictedOnly>(serverEnt);
                var interpOnly = testWorld.ServerWorld.EntityManager.GetComponentData<GhostInterpolatedOnly>(serverEnt);

                for (int i = 0; i < 2; ++i)
                {
                    var clientEnt = testWorld.TryGetSingletonEntity<GhostOwner>(testWorld.ClientWorlds[i]);
                    var clientComp1_ToOwner = testWorld.ClientWorlds[i].EntityManager.GetComponentData<GhostGen_IntStruct>(clientEnt);
                    var clientComp2_NonOwner = testWorld.ClientWorlds[i].EntityManager.GetComponentData<GhostTypeIndex>(clientEnt);
                    var clientPredOnly = testWorld.ClientWorlds[i].EntityManager.GetComponentData<GhostPredictedOnly>(clientEnt);
                    var clientInterpOnly = testWorld.ClientWorlds[i].EntityManager.GetComponentData<GhostInterpolatedOnly>(clientEnt);

                    var clientBuffer1 = testWorld.ClientWorlds[i].EntityManager.GetBuffer<GhostGenBuffer_ByteBuffer>(clientEnt);
                    var clientBuffer2 = testWorld.ClientWorlds[i].EntityManager.GetBuffer<GhostGenTest_Buffer>(clientEnt);

                    Assert.AreEqual(i==0,serverComp1.IntValue == clientComp1_ToOwner.IntValue,$"Client {i}");
                    Assert.AreEqual(i==1,serverComp2.Value == clientComp2_NonOwner.Value,$"Client {i}");

                    //The component are sent to all the clients and only the SendToOwner matter
                    if (mode == GhostMode.Predicted)
                    {
                        Assert.AreEqual(i==0,predictedOnly.Value == clientPredOnly.Value, $"Client {i}");
                        Assert.AreEqual(false,interpOnly.Value == clientInterpOnly.Value,  $"Client {i}");
                    }
                    else if (mode == GhostMode.Interpolated)
                    {
                        Assert.AreEqual(false,predictedOnly.Value == clientPredOnly.Value, $"Client {i}");
                        Assert.AreEqual(i==1,interpOnly.Value == clientInterpOnly.Value, $"Client {i}");
                    }
                    else if(mode == GhostMode.OwnerPredicted)
                    {
                        Assert.AreEqual(i==0,predictedOnly.Value == clientPredOnly.Value,$"Client {i}");
                        Assert.AreEqual(i==1,interpOnly.Value == clientInterpOnly.Value,$"Client {i}");
                    }
                    Assert.AreEqual(true, 10 ==clientBuffer1.Length);
                    Assert.AreEqual(i==1,10 ==clientBuffer2.Length);
                    Assert.AreEqual(i==0,0 ==clientBuffer2.Length);
                    for (int k = 0; k < clientBuffer1.Length; ++k)
                        Assert.AreEqual(serverBuffer1[k].Value, clientBuffer1[k].Value,$"Client {i}");
                    for (int k = 0; k < clientBuffer2.Length; ++k)
                        Assert.AreEqual(serverBuffer2[k].IntValue, clientBuffer2[k].IntValue,$"Client {i}");
                }
            }
        }
    }
}
