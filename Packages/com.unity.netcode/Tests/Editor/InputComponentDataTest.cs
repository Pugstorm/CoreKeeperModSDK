using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode.LowLevel.Unsafe;
using Unity.Transforms;
using UnityEngine;

namespace Unity.NetCode.Tests
{
    public struct InputComponentData : IInputComponentData
    {
        public int Horizontal;
        public int Vertical;
        public InputEvent Jump;
    }

    public struct InputRemoteTestComponentData : IInputComponentData
    {
        [GhostField] public int Horizontal;
        [GhostField] public int Vertical;
        [GhostField] public InputEvent Jump;
    }

    [GhostComponent(PrefabType=GhostPrefabType.AllPredicted)]
    public struct InputComponentDataAllPredicted : IInputComponentData
    {
        public int Horizontal;
        public int Vertical;
        public InputEvent Jump;
    }

    [GhostComponent(PrefabType=GhostPrefabType.AllPredicted)]
    public struct InputComponentDataAllPredictedWithGhostFields : IInputComponentData
    {
        [GhostField] public int Horizontal;
        [GhostField] public int Vertical;
        [GhostField] public InputEvent Jump;
    }

    [GhostComponent(PrefabType=GhostPrefabType.Server)]
    public struct InputComponentDataServerOnly : IInputComponentData
    {
        public int Horizontal;
        public int Vertical;
        public InputEvent Jump;
    }

    [GhostComponent(PrefabType = GhostPrefabType.Client, OwnerSendType = SendToOwnerType.SendToOwner, SendTypeOptimization = GhostSendType.OnlyInterpolatedClients, SendDataForChildEntity = false)]
    public struct InputComponentDataWithGhostComponent : IInputComponentData
    {
        public int Horizontal;
        public int Vertical;
        public InputEvent Jump;
    }

    [GhostComponent(PrefabType = GhostPrefabType.Client, OwnerSendType = SendToOwnerType.SendToOwner, SendTypeOptimization = GhostSendType.OnlyInterpolatedClients, SendDataForChildEntity = false)]
    public struct InputComponentDataWithGhostComponentAndGhostFields : IInputComponentData
    {
        [GhostField] public int Horizontal;
        [GhostField] public int Vertical;
        [GhostField] public InputEvent Jump;
    }

    public class InputRemoteTestComponentDataConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent<InputRemoteTestComponentData>(entity);
        }
    }

    public class InputComponentDataConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent<InputComponentData>(entity);
        }
    }

    public class InputComponentDataAllPredictedConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent<InputComponentDataAllPredicted>(entity);
        }
    }

    public class InputComponentDataAllPredictedWithGhostFieldsConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent<InputComponentDataAllPredictedWithGhostFields>(entity);
        }
    }

    public class InputComponentDataServerOnlyConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent<InputComponentDataServerOnly>(entity);
        }
    }

    public class InputComponentDataWithGhostComponentConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent<InputComponentDataWithGhostComponent>(entity);
        }
    }

    public class InputComponentDataWithGhostComponentAndGhostFieldsConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent<InputComponentDataWithGhostComponentAndGhostFields>(entity);
        }
    }

    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    [DisableAutoCreation]
    public partial class GatherInputsSystem : SystemBase
    {
        private int m_WaitTicks = 1; // Must wait 1 tick as the copy system starts one frame delayed
        private bool m_DidSetEvent; // Only set the jump event once
        protected override void OnCreate()
        {
            RequireForUpdate<InputComponentData>();
            RequireForUpdate<NetworkStreamInGame>();
        }
        protected override void OnUpdate()
        {
            var didSetEvent = m_DidSetEvent;
            var waitTicks = m_WaitTicks;
            Entities
                .WithAll<GhostOwnerIsLocal>()
                .ForEach((ref InputComponentData inputData) =>
                {
                    inputData = default;
                    inputData.Horizontal = 1;
                    inputData.Vertical = 1;
                    if (!didSetEvent)
                    {
                        if (waitTicks > 0)
                        {
                            waitTicks--;
                        }
                        else
                        {
                            didSetEvent = true;
                            inputData.Jump.Set();
                        }
                    }
                }).Run();
            m_DidSetEvent = didSetEvent;
            m_WaitTicks = waitTicks;
        }
    }

    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    [DisableAutoCreation]
    public partial class GatherInputsRemoteTestSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<InputRemoteTestComponentData>();
            RequireForUpdate<NetworkStreamInGame>();
        }
        protected override void OnUpdate()
        {
            // Inputs are only gathered on the local player, so if any inputs are set on
            // the remote player it's because they were fetched from the buffer (replicated via ghost system)
            Entities
                .WithAll<GhostOwnerIsLocal>()
                .ForEach((ref InputRemoteTestComponentData inputData) =>
                {
                    inputData = default;
                    inputData.Horizontal = 1;
                    inputData.Vertical = 1;
                }).Run();
        }
    }

    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial class ProcessInputsSystem : SystemBase
    {
        public int EventCounter;
        protected override void OnCreate()
        {
            RequireForUpdate<InputComponentData>();
        }
        protected override void OnUpdate()
        {
            var eventCounter = EventCounter;
            Entities.WithAll<Simulate>().ForEach(
                (ref InputComponentData input, ref LocalTransform trans) =>
                {
                    var newPosition = new float3();
                    if (input.Jump.IsSet)
                        eventCounter++;
                    newPosition.x = input.Horizontal;
                    newPosition.z = input.Vertical;
                    trans = trans.WithPosition(newPosition);
                }).Run();

            EventCounter = eventCounter;
        }
    }

    public class InputComponentDataTest
    {
        private const float m_DeltaTime = 1.0f / 60.0f;

        [Test]
        public void InputComponentData_IsCorrectlySynchronized()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(GatherInputsSystem), typeof(ProcessInputsSystem));

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new InputComponentDataConverter();
                var ghostConfig = ghostGameObject.AddComponent<GhostAuthoringComponent>();
                ghostConfig.HasOwner = true;
                ghostConfig.SupportAutoCommandTarget = true;
                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));
                testWorld.CreateWorlds(true, 1);
                testWorld.Connect(m_DeltaTime);
                testWorld.GoInGame();

                var serverConnectionEnt = testWorld.TryGetSingletonEntity<NetworkId>(testWorld.ServerWorld);
                var clientConnectionEnt = testWorld.TryGetSingletonEntity<NetworkId>(testWorld.ClientWorlds[0]);
                var netId = testWorld.ClientWorlds[0].EntityManager.GetComponentData<NetworkId>(clientConnectionEnt).Value;

                var serverEnt = testWorld.SpawnOnServer(ghostGameObject);
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEnt, new GhostOwner {NetworkId = netId});

                // Wait for client spawn
                Entity clientEnt = Entity.Null;
                for (int i = 0; i < 16; ++i)
                {
                    clientEnt = testWorld.TryGetSingletonEntity<InputComponentData>(testWorld.ClientWorlds[0]);
                    if (clientEnt != Entity.Null) break;
                    testWorld.Tick(m_DeltaTime);
                }

                clientEnt = testWorld.TryGetSingletonEntity<InputComponentData>(testWorld.ClientWorlds[0]);
                Assert.AreNotEqual(Entity.Null, clientEnt);

                testWorld.ServerWorld.EntityManager.SetComponentData(serverConnectionEnt, new CommandTarget{targetEntity = serverEnt});
                testWorld.ClientWorlds[0].EntityManager.SetComponentData(clientConnectionEnt, new CommandTarget{targetEntity = clientEnt});

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(m_DeltaTime);

                // The IInputComponentData should have been copied to buffer, sent to server, and then transform
                // result sent back to the client.
                var transform = testWorld.ClientWorlds[0].EntityManager.GetComponentData<LocalTransform>(clientEnt);
                Assert.AreEqual(1f, transform.Position.x);
                Assert.AreEqual(1f, transform.Position.z);

                // Event should only fire once on the server (but can multiple times on client because of prediction loop)
                var serverInputSystem = testWorld.ServerWorld.GetExistingSystemManaged<ProcessInputsSystem>();
                Assert.AreEqual(1, serverInputSystem.EventCounter);
            }
        }

        /* Validate that remote predicted input is properly synchronized when input fields are
         * marked as ghost fields. The input buffer should be present on all clients and be
         * filled with the input values of each one as well.
         */
        [Test]
        public void InputComponentData_InputBufferIsRemotePredictedWhenAppropriate()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(GatherInputsRemoteTestSystem));

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new InputRemoteTestComponentDataConverter();
                var ghostConfig = ghostGameObject.AddComponent<GhostAuthoringComponent>();
                ghostConfig.HasOwner = true;
                ghostConfig.SupportAutoCommandTarget = true;
                ghostConfig.SupportedGhostModes = GhostModeMask.All;
                ghostConfig.DefaultGhostMode = GhostMode.Predicted;
                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));
                testWorld.CreateWorlds(true, 2);
                testWorld.Connect(m_DeltaTime);
                testWorld.GoInGame();

                using var serverConnectionQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
                var serverConnectionEntities = serverConnectionQuery.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(2, serverConnectionEntities.Length);
                var serverConnectionEntToClient1 = serverConnectionEntities[0];
                var serverConnectionEntToClient2 = serverConnectionEntities[1];
                var clientConnectionEnt1 = testWorld.TryGetSingletonEntity<NetworkId>(testWorld.ClientWorlds[0]);
                var clientConnectionEnt2 = testWorld.TryGetSingletonEntity<NetworkId>(testWorld.ClientWorlds[1]);
                var netId1 = testWorld.ClientWorlds[0].EntityManager.GetComponentData<NetworkId>(clientConnectionEnt1).Value;
                var netId2 = testWorld.ClientWorlds[1].EntityManager.GetComponentData<NetworkId>(clientConnectionEnt2).Value;

                var serverEntPlayer1 = testWorld.SpawnOnServer(ghostGameObject);
                var serverEntPlayer2 = testWorld.SpawnOnServer(ghostGameObject);
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEntPlayer1, new GhostOwner {NetworkId = netId1});
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEntPlayer2, new GhostOwner {NetworkId = netId2});

                // Wait for client spawn
                using EntityQuery clientQuery1 = testWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<InputRemoteTestComponentData>());
                using EntityQuery clientQuery2 = testWorld.ClientWorlds[1].EntityManager.CreateEntityQuery(ComponentType.ReadOnly<InputRemoteTestComponentData>());
                for (int i = 0; i < 16; ++i)
                {
                    if (clientQuery1.CalculateEntityCount() == 2 && clientQuery2.CalculateEntityCount() == 2) break;
                    testWorld.Tick(m_DeltaTime);
                }

                using var inputsQueryOnClient1 = testWorld.ClientWorlds[0].EntityManager
                    .CreateEntityQuery(ComponentType.ReadOnly<InputRemoteTestComponentData>());
                var playersOnClient1 = inputsQueryOnClient1.ToEntityArray(Allocator.Temp);
                var clientEnt1OwnPlayer = playersOnClient1[0];
                var ghostOwnerOnPlayer1OnClient1 = testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostOwner>(clientEnt1OwnPlayer);
                Assert.AreEqual(1, ghostOwnerOnPlayer1OnClient1.NetworkId);

                using var inputsQueryOnClient2 = testWorld.ClientWorlds[1].EntityManager
                    .CreateEntityQuery(ComponentType.ReadOnly<InputRemoteTestComponentData>());
                var playersOnClient2 = inputsQueryOnClient2.ToEntityArray(Allocator.Temp);
                var clientEnt2OwnPlayer = playersOnClient2[1];
                var ghostOwnerOnPlayer2OnClient2 = testWorld.ClientWorlds[1].EntityManager.GetComponentData<GhostOwner>(clientEnt2OwnPlayer);
                Assert.AreEqual(2, ghostOwnerOnPlayer2OnClient2.NetworkId);

                testWorld.ServerWorld.EntityManager.SetComponentData(serverConnectionEntToClient1, new CommandTarget{targetEntity = serverEntPlayer1});
                testWorld.ServerWorld.EntityManager.SetComponentData(serverConnectionEntToClient2, new CommandTarget{targetEntity = serverEntPlayer2});
                testWorld.ClientWorlds[0].EntityManager.SetComponentData(clientConnectionEnt1, new CommandTarget{targetEntity = clientEnt1OwnPlayer});
                testWorld.ClientWorlds[1].EntityManager.SetComponentData(clientConnectionEnt2, new CommandTarget{targetEntity = clientEnt2OwnPlayer});

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(m_DeltaTime);

                // Input buffer must be added to the prefab
                Assert.IsTrue(testWorld.ServerWorld.EntityManager.HasBuffer<InputBufferData<InputRemoteTestComponentData>>(serverEntPlayer1));
                Assert.IsTrue(testWorld.ServerWorld.EntityManager.HasBuffer<InputBufferData<InputRemoteTestComponentData>>(serverEntPlayer2));
                Assert.IsTrue(testWorld.ClientWorlds[0].EntityManager.HasBuffer<InputBufferData<InputRemoteTestComponentData>>(clientEnt1OwnPlayer));
                Assert.IsTrue(testWorld.ClientWorlds[1].EntityManager.HasBuffer<InputBufferData<InputRemoteTestComponentData>>(clientEnt2OwnPlayer));

                // Validate that client 2 actually has input buffer data (copied to component) from client 1 and reversed
                testWorld.ClientWorlds[0].EntityManager.CompleteAllTrackedJobs();
                testWorld.ClientWorlds[1].EntityManager.CompleteAllTrackedJobs();
                var inputsOnClient1 = inputsQueryOnClient1.ToComponentDataArray<InputRemoteTestComponentData>(Allocator.Temp);
                Assert.AreEqual(2, inputsOnClient1.Length);
                Assert.AreEqual(1, inputsOnClient1[0].Horizontal);
                Assert.AreEqual(1, inputsOnClient1[0].Vertical);
                Assert.AreEqual(1, inputsOnClient1[1].Horizontal);
                Assert.AreEqual(1, inputsOnClient1[1].Vertical);

                var inputsOnClient2 = inputsQueryOnClient2.ToComponentDataArray<InputRemoteTestComponentData>(Allocator.Temp);
                Assert.AreEqual(2, inputsOnClient2.Length);
                Assert.AreEqual(1, inputsOnClient2[0].Horizontal);
                Assert.AreEqual(1, inputsOnClient2[0].Vertical);
                Assert.AreEqual(1, inputsOnClient2[1].Horizontal);
                Assert.AreEqual(1, inputsOnClient2[1].Vertical);
            }
        }

        [Test]
        public void InputComponentData_WithDefaultGhostComponents()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var gameObject0 = new GameObject();
                gameObject0.AddComponent<GhostAuthoringComponent>();
                gameObject0.AddComponent<TestNetCodeAuthoring>().Converter = new InputRemoteTestComponentDataConverter();
                gameObject0.name = $"{nameof(InputRemoteTestComponentData)}Test";

                var gameObject1 = new GameObject();
                gameObject1.AddComponent<GhostAuthoringComponent>();
                gameObject1.AddComponent<TestNetCodeAuthoring>().Converter = new InputComponentDataConverter();
                gameObject1.name = $"{nameof(InputComponentData)}Test";

                Assert.IsTrue(testWorld.CreateGhostCollection(
                    gameObject0, gameObject1));

                testWorld.CreateWorlds(true, 1);
                float frameTime = 1.0f / 60.0f;
                testWorld.Connect(frameTime);
                testWorld.GoInGame();

                testWorld.SpawnOnServer(gameObject0);
                testWorld.SpawnOnServer(gameObject1);
                CheckComponent(testWorld.ServerWorld, ComponentType.ReadOnly<InputComponentData>(), 1);
                CheckComponent(testWorld.ServerWorld, ComponentType.ReadOnly<InputRemoteTestComponentData>(), 1);
                CheckComponent(testWorld.ServerWorld, ComponentType.ReadOnly<InputBufferData<InputComponentData>>(), 1);
                CheckComponent(testWorld.ServerWorld, ComponentType.ReadOnly<InputBufferData<InputRemoteTestComponentData>>(), 1);

                for (int i = 0; i < 64; ++i)
                    testWorld.Tick(frameTime);

                CheckComponent(testWorld.ClientWorlds[0], ComponentType.ReadOnly<InputComponentData>(), 1);
                CheckComponent(testWorld.ClientWorlds[0], ComponentType.ReadOnly<InputRemoteTestComponentData>(), 1);
                CheckComponent(testWorld.ClientWorlds[0], ComponentType.ReadOnly<InputBufferData<InputComponentData>>(), 1);
                CheckComponent(testWorld.ClientWorlds[0], ComponentType.ReadOnly<InputBufferData<InputRemoteTestComponentData>>(), 1);
            }
        }

        [Test]
        public void InputComponentData_WithAllPredictedGhost()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var gameObject0 = new GameObject();
                var ghostAuthoringComponent = gameObject0.AddComponent<GhostAuthoringComponent>();
                ghostAuthoringComponent.HasOwner = true;
                ghostAuthoringComponent.SupportAutoCommandTarget = true;
                ghostAuthoringComponent.SupportedGhostModes = GhostModeMask.Predicted;
                gameObject0.AddComponent<TestNetCodeAuthoring>().Converter = new InputComponentDataAllPredictedConverter();
                gameObject0.name = $"{nameof(InputComponentDataAllPredicted)}Test";

                var gameObject1 = new GameObject();
                ghostAuthoringComponent = gameObject1.AddComponent<GhostAuthoringComponent>();
                ghostAuthoringComponent.HasOwner = true;
                ghostAuthoringComponent.SupportAutoCommandTarget = true;
                ghostAuthoringComponent.SupportedGhostModes = GhostModeMask.Predicted;
                gameObject1.AddComponent<TestNetCodeAuthoring>().Converter = new InputComponentDataAllPredictedWithGhostFieldsConverter();
                gameObject1.name = $"{nameof(InputComponentDataAllPredictedWithGhostFields)}Test";

                Assert.IsTrue(testWorld.CreateGhostCollection(
                    gameObject0, gameObject1));

                testWorld.CreateWorlds(true, 1);
                float frameTime = 1.0f / 60.0f;
                testWorld.Connect(frameTime);
                testWorld.GoInGame();

                var clientConnectionEnt = testWorld.TryGetSingletonEntity<NetworkId>(testWorld.ClientWorlds[0]);
                var netId = testWorld.ClientWorlds[0].EntityManager.GetComponentData<NetworkId>(clientConnectionEnt).Value;

                var predictedEnt = testWorld.SpawnOnServer(gameObject0);
                testWorld.ServerWorld.EntityManager.SetComponentData(predictedEnt, new GhostOwner {NetworkId = netId});
                predictedEnt = testWorld.SpawnOnServer(gameObject1);
                testWorld.ServerWorld.EntityManager.SetComponentData(predictedEnt, new GhostOwner {NetworkId = netId});

                CheckComponent(testWorld.ServerWorld, ComponentType.ReadOnly<InputComponentDataAllPredictedWithGhostFields>(), 1);
                CheckComponent(testWorld.ServerWorld, ComponentType.ReadOnly<InputComponentDataAllPredicted>(), 1);

                for (int i = 0; i < 64; ++i)
                    testWorld.Tick(frameTime);

                CheckComponent(testWorld.ClientWorlds[0], ComponentType.ReadOnly<InputComponentDataAllPredictedWithGhostFields>(), 1);
                CheckComponent(testWorld.ClientWorlds[0], ComponentType.ReadOnly<InputComponentDataAllPredicted>(), 1);
            }
        }

        /* Test
         *   - IInputComponent with GhostComponent attribute
         *   - IInputComponent with GhostFields and GhostComponent attribute
         * In all cases the ghost component config (like prefab type) from the input component
         * should copy over to the generated input buffer.
         */
        [Test]
        public void InputComponentData_BufferCopiesGhostComponentConfigFromInputComponent()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var gameObject0 = new GameObject();
                gameObject0.AddComponent<GhostAuthoringComponent>();
                gameObject0.AddComponent<TestNetCodeAuthoring>().Converter = new InputComponentDataWithGhostComponentConverter();
                gameObject0.name = $"{nameof(InputComponentDataWithGhostComponent)}Test";

                var gameObject1 = new GameObject();
                gameObject1.AddComponent<GhostAuthoringComponent>();
                gameObject1.AddComponent<TestNetCodeAuthoring>().Converter = new InputComponentDataWithGhostComponentAndGhostFieldsConverter();
                gameObject1.name = $"{nameof(InputComponentDataWithGhostComponentAndGhostFields)}Test";

                Assert.IsTrue(testWorld.CreateGhostCollection(
                    gameObject0, gameObject1));

                testWorld.CreateWorlds(true, 1);

                // We can only validate the remote predicted component in the ghost component collection
                // as the normal component does not get a buffer which is a ghost component (to be
                // synced in snapshots) but is just handled as a command is
                using var collectionQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostComponentSerializerCollectionData>());
                var collectionData = collectionQuery.GetSingleton<GhostComponentSerializerCollectionData>();
                GhostComponentSerializer.State inputBufferWithGhostFieldsSerializerState = default;
                var inputBufferType = ComponentType.ReadWrite<InputBufferData<InputComponentDataWithGhostComponent>>();
                var inputBufferWithFieldsType = ComponentType.ReadWrite<InputBufferData<InputComponentDataWithGhostComponentAndGhostFields>>();
                foreach (var state in collectionData.Serializers)
                {
                    if (state.ComponentType.CompareTo(inputBufferWithFieldsType) == 0)
                        inputBufferWithGhostFieldsSerializerState = state;
                }

                // There should be empty variant configs for all the other types (input components and buffer without ghost fields)
                // where we'll find the prefab type registered
                var foundVariantForInputBuffer = false;
                var foundVariantForInputComponent = false;
                var foundVariantForInputComponentWithFields = false;
                foreach (var nonSerializedStrategies in collectionData.SerializationStrategies)
                {
                    if (nonSerializedStrategies.IsSerialized != 0)
                        continue;

                    if (nonSerializedStrategies.Component.CompareTo(inputBufferType) == 0)
                    {
                        foundVariantForInputBuffer = true;
                        Assert.AreEqual(GhostPrefabType.Client, nonSerializedStrategies.PrefabType);
                    }
                    if (nonSerializedStrategies.Component.CompareTo(ComponentType.ReadWrite<InputComponentDataWithGhostComponent>()) == 0)
                    {
                        foundVariantForInputComponent = true;
                        Assert.AreEqual(GhostPrefabType.Client, nonSerializedStrategies.PrefabType);
                    }
                    if (nonSerializedStrategies.Component.CompareTo(ComponentType.ReadWrite<InputComponentDataWithGhostComponentAndGhostFields>()) == 0)
                    {
                        foundVariantForInputComponentWithFields = true;
                        Assert.AreEqual(GhostPrefabType.Client, nonSerializedStrategies.PrefabType);
                    }
                }
                Assert.IsTrue(foundVariantForInputBuffer);
                Assert.IsTrue(foundVariantForInputComponent);
                Assert.IsTrue(foundVariantForInputComponentWithFields);

                Assert.AreEqual(GhostPrefabType.Client, inputBufferWithGhostFieldsSerializerState.PrefabType);
                Assert.AreEqual((int)GhostSendType.OnlyInterpolatedClients, (int)inputBufferWithGhostFieldsSerializerState.SendMask);
                // TODO: Fix test-case using new info that component data on child entities is NOT sent by default. Assert.AreEqual(0, inputBufferWithGhostFieldsSerializerState.SendForChildEntities);
                // SendToOwnerType will be forced to SendToNonOwner (was set to SendToOwner)
                Assert.AreEqual(SendToOwnerType.SendToNonOwner, inputBufferWithGhostFieldsSerializerState.SendToOwner);

                // A spawn on server should result in no input or buffer components as it was configured
                // to be client only via the ghost component attributes on the input component struct
                testWorld.SpawnOnServer(gameObject0);
                testWorld.SpawnOnServer(gameObject1);

                CheckComponent(testWorld.ServerWorld, ComponentType.ReadOnly<InputComponentDataWithGhostComponent>(), 0);
                CheckComponent(testWorld.ServerWorld, inputBufferType, 0);
                CheckComponent(testWorld.ServerWorld, ComponentType.ReadOnly<InputComponentDataWithGhostComponentAndGhostFields>(), 0);
                CheckComponent(testWorld.ServerWorld, inputBufferWithFieldsType, 0);

                float frameTime = 1.0f / 60.0f;
                testWorld.Connect(frameTime);
                testWorld.GoInGame();
                for (int i = 0; i < 64; ++i)
                    testWorld.Tick(frameTime);

                CheckComponent(testWorld.ClientWorlds[0], ComponentType.ReadOnly<InputComponentDataWithGhostComponent>(), 1);
                CheckComponent(testWorld.ClientWorlds[0], inputBufferType, 1);
                CheckComponent(testWorld.ClientWorlds[0], ComponentType.ReadOnly<InputComponentDataWithGhostComponentAndGhostFields>(), 1);
                CheckComponent(testWorld.ClientWorlds[0], inputBufferWithFieldsType, 1);
            }
        }

        void CheckComponent(World w, ComponentType testType, int expectedCount)
        {
            using var query = w.EntityManager.CreateEntityQuery(testType);
            using (var ghosts = query.ToEntityArray(Allocator.Temp))
            {
                var compCount = ghosts.Length;
                Assert.AreEqual(expectedCount, compCount);
            }
        }
    }
}
