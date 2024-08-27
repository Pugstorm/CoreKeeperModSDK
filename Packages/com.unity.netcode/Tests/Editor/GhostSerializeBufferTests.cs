using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.NetCode.Editor;
using Unity.NetCode.LowLevel.Unsafe;
using UnityEditor.Compilation;

namespace Unity.NetCode.Tests
{
    public struct GhostGenTest_Buffer : IBufferElementData
    {
        [GhostField] public int IntValue;
        [GhostField] public uint UIntValue;
        [GhostField] public bool BoolValue;
        [GhostField(Quantization = 10)] public float FloatValue;
    }

    public struct GhostGen_InterpolatedStruct : IComponentData
    {
        [GhostField(Smoothing = SmoothingAction.Interpolate)] public float FloatValue;
    }

    public struct GhostGen_IntStruct : IComponentData
    {
        [GhostField] public int IntValue;
    }

    public struct GhostGen_CompositeStruct
    {
        [GhostField] public int IntValue1;
        [GhostField] public int IntValue2;
        [GhostField] public int IntValue3;
    }

    public struct GhostGen_BufferInterpolated : IBufferElementData
    {
        //Buffers will discard the Interpolate attribute for either the field members and / or any struct sub-fields
        [GhostField(Smoothing = SmoothingAction.Interpolate)] public float FloatValue;
        [GhostField] public GhostGen_InterpolatedStruct Vec;
    }

    public struct GhostGenBuffer_BufferComposite : IBufferElementData
    {
        [GhostField(Composite = true)] public GhostGen_CompositeStruct Field1;
    }

    public struct GhostGenBuffer_ByteBuffer : IBufferElementData
    {
        [GhostField] public byte Value;
    }

    public class GhostByteBufferAuthoringComponent : MonoBehaviour
    {
    }

    class GhostByteBufferAuthoringComponentBaker : Baker<GhostByteBufferAuthoringComponent>
    {
        public override void Bake(GhostByteBufferAuthoringComponent authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddBuffer<GhostGenBuffer_ByteBuffer>(entity);
        }
    }

    public class GhostGenBufferAuthoringComponent : MonoBehaviour
    {
    }

    class GhostGenBufferAuthoringComponentBaker : Baker<GhostGenBufferAuthoringComponent>
    {
        public override void Bake(GhostGenBufferAuthoringComponent authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddBuffer<GhostGenTest_Buffer>(entity);
        }
    }

    static class BufferTestHelper
    {
        public static void SetBufferValues(World testWorld, Entity entity, int size, int baseBalue)
        {
            var serverBuffer = testWorld.EntityManager.GetBuffer<GhostGenTest_Buffer>(entity);
            serverBuffer.ResizeUninitialized(size);
            for (int i = 0; i < size; ++i)
            {
                int value = (i + 1) * 1000 + baseBalue;
                serverBuffer[i] = (new GhostGenTest_Buffer
                {
                    IntValue = value,
                    UIntValue = (uint) ++value,
                    BoolValue = true,
                    FloatValue = ++value
                });
            }
        }

        public static void SetByteBufferValues(World testWorld, Entity entity, int size, int baseBalue)
        {
            var buffer = testWorld.EntityManager.GetBuffer<GhostGenBuffer_ByteBuffer>(entity);
            buffer.ResizeUninitialized(size);
            for (int i = 0; i < buffer.Length; ++i)
                buffer[i] = new GhostGenBuffer_ByteBuffer {Value = (byte) (baseBalue * (i + 1))};
        }

        public static void CheckBuffersValues(NetCodeTestWorld testWorld, Entity serverEntity, Entity clientEntity, bool shouldMatch)
        {
            Assert.AreNotEqual(Entity.Null, serverEntity);
            Assert.AreNotEqual(Entity.Null, clientEntity);
            var serverBuffer = testWorld.ServerWorld.EntityManager.GetBuffer<GhostGenTest_Buffer>(serverEntity);
            var clientBuffer = testWorld.ClientWorlds[0].EntityManager.GetBuffer<GhostGenTest_Buffer>(clientEntity);

            if (shouldMatch)
            {
                Assert.AreEqual(serverBuffer.Length, clientBuffer.Length);
                for (int i = 0; i < serverBuffer.Length; ++i)
                {
                    var bs = serverBuffer[i];
                    var cs = clientBuffer[i];
                    Assert.AreEqual(bs.IntValue, cs.IntValue);
                    Assert.AreEqual(bs.UIntValue, cs.UIntValue);
                    Assert.AreEqual(bs.BoolValue, cs.BoolValue);
                    Assert.AreEqual(bs.FloatValue, cs.FloatValue);
                }
            }
            else
            {
                // TODO: Seems like buffers sometimes have the same buffer length even when unsent. Assert.AreNotEqual(serverBuffer.Length, clientBuffer.Length);
                for (int i = 0; i < serverBuffer.Length && i < clientBuffer.Length; ++i)
                {
                    var bs = serverBuffer[i];
                    var cs = clientBuffer[i];
                    Assert.AreNotEqual(bs.IntValue, cs.IntValue);
                    Assert.AreNotEqual(bs.UIntValue, cs.UIntValue);
                    Assert.AreNotEqual(bs.BoolValue, cs.BoolValue);
                    Assert.AreNotEqual(bs.FloatValue, cs.FloatValue);
                }
            }
        }

        public static void CheckByteBufferValues(NetCodeTestWorld testWorld, Entity serverEntity, Entity clientEntity)
        {
            var serverByteBuffer = testWorld.ServerWorld.EntityManager.GetBuffer<GhostGenBuffer_ByteBuffer>(serverEntity);
            var clientByteBuffer = testWorld.ClientWorlds[0].EntityManager.GetBuffer<GhostGenBuffer_ByteBuffer>(clientEntity);
            Assert.AreEqual(serverByteBuffer.Length, clientByteBuffer.Length);
            for (int i = 0; i < serverByteBuffer.Length; ++i)
                Assert.AreEqual(serverByteBuffer[i].Value, clientByteBuffer[i].Value);
        }

        public static Entity[] GetClientEntities(NetCodeTestWorld testWorld, Entity[] entities)
        {
            var ghostMapSingleton = testWorld.TryGetSingletonEntity<SpawnedGhostEntityMap>(testWorld.ClientWorlds[0]);
            var entityMap = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SpawnedGhostEntityMap>(ghostMapSingleton).Value;
            var clientEntities = new Entity[entities.Length];
            for (int i = 0; i < entities.Length; ++i)
            {
                var ghost = testWorld.ServerWorld.EntityManager.GetComponentData<GhostInstance>(entities[i]);
                Assert.IsTrue(entityMap.TryGetValue(
                    new SpawnedGhost {ghostId = ghost.ghostId, spawnTick = ghost.spawnTick}, out clientEntities[i]));
            }
            return clientEntities;
        }

        //Validate that client dynamic snapshot data as the content layout has we expect
        public static void ValidateMultiBufferSnapshotDataContents(in DynamicBuffer<SnapshotDynamicDataBuffer> dynamicBuffer,
            int structBufLen, int b1, int byteBufLen, int b2)
        {
            Assert.IsTrue(structBufLen<32);
            Assert.IsTrue(byteBufLen<32);
            unsafe
            {
                var pointer = (uint*) dynamicBuffer.GetUnsafeReadOnlyPtr();
                var expectedSize = GhostComponentSerializer.SnapshotSizeAligned((structBufLen * 16 + 16) + (16 + 4 * byteBufLen));
                for (int i = 0; i < 32; ++i)
                {
                    var dataSize = pointer[i];
                    Assert.AreEqual(expectedSize, dataSize, $"DynamicBuffer<SnapshotDynamicDataBuffer>[{i}]");
                }

                pointer += 32;
                var stride = (dynamicBuffer.Length - 128) / 32;
                int maskUints1 = (((structBufLen * 4 + 31) & ~31) / 32 + 3) & ~3;
                int maskUints2 = (((byteBufLen + 31) & ~31) / 32 + 3) & ~3;

                void CheckByteBuffer(uint*ptr, int len)
                {
                    for (int k = 0; k < len; ++k)
                    {
                        Assert.AreEqual((byte) ((k + 1) * b2), *ptr);
                        ptr += 1;
                    }
                }
                void CheckStructBuffer(uint*ptr, int len)
                {
                    for (int k = 0; k < structBufLen; ++k)
                    {
                        Assert.AreEqual(1000 * (1 + k) + b1, ptr[0]);
                        Assert.AreEqual(1000 * (1 + k) + 1 + b1, ptr[1]);
                        Assert.AreEqual(1, ptr[2]);
                        Assert.AreEqual(10000 * (1 + k) + (b1 + 2) * 10, ptr[3]);
                        ptr += 4;
                    }
                }

                for (int i = 0; i < 32; ++i)
                {
                    var oldPtr = pointer;
                    pointer += maskUints2;
                    CheckByteBuffer(pointer, byteBufLen);
                    pointer += GhostComponentSerializer.SnapshotSizeAligned(4*byteBufLen)/4;
                    pointer += maskUints1;
                    CheckStructBuffer(pointer, structBufLen);
                    pointer += GhostComponentSerializer.SnapshotSizeAligned(16 * structBufLen)/4;
                    pointer = oldPtr + stride / 4;
                }
            }
        }
    }

    [TestFixture]
    public partial class DynamicBufferSerializationTests
    {
        [Test]
        public void BuffersAreSerialized()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<GhostGenBufferAuthoringComponent>();

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);
                var serverEntity = testWorld.SpawnOnServer(ghostGameObject);
                BufferTestHelper.SetBufferValues(testWorld.ServerWorld, serverEntity, 3, 6);

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                // Go in-game
                testWorld.GoInGame();

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(frameTime);

                var clientEntities = BufferTestHelper.GetClientEntities(testWorld, new [] {serverEntity});
                BufferTestHelper.CheckBuffersValues(testWorld, serverEntity, clientEntities[0], true);
                BufferTestHelper.SetBufferValues(testWorld.ServerWorld, serverEntity, 3, 10);
                for (int i = 0; i < 8; ++i)
                    testWorld.Tick(frameTime);
                BufferTestHelper.CheckBuffersValues(testWorld, serverEntity, clientEntities[0], true);
            }
        }

        [Test]
        public void BuffersCanChangeSize()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<GhostGenBufferAuthoringComponent>();

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);
                var serverEntity = testWorld.SpawnOnServer(ghostGameObject);

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                // Go in-game
                testWorld.GoInGame();

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 64; ++i)
                    testWorld.Tick(frameTime);

                var clientEntities = BufferTestHelper.GetClientEntities(testWorld, new [] {serverEntity});
                //Buffer are empty on both sides
                BufferTestHelper.CheckBuffersValues(testWorld, serverEntity, clientEntities[0], true);
                //Set bufferrs
                BufferTestHelper.SetBufferValues(testWorld.ServerWorld, serverEntity, 3, 10);
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(frameTime);
                BufferTestHelper.CheckBuffersValues(testWorld, serverEntity, clientEntities[0], true);
                //Shrink
                BufferTestHelper.SetBufferValues(testWorld.ServerWorld, serverEntity, 2, 20);
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(frameTime);
                BufferTestHelper.CheckBuffersValues(testWorld, serverEntity, clientEntities[0], true);
                //Resize larger
                BufferTestHelper.SetBufferValues(testWorld.ServerWorld, serverEntity, 5, 30);
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(frameTime);
                BufferTestHelper.CheckBuffersValues(testWorld, serverEntity, clientEntities[0], true);
            }
        }

        [Test]
        public void MultipleBuffersCanChangeSize()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<GhostByteBufferAuthoringComponent>();
                ghostGameObject.AddComponent<GhostGenBufferAuthoringComponent>();

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);
                var serverEntity = testWorld.SpawnOnServer(ghostGameObject);

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                // Go in-game
                testWorld.GoInGame();

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(frameTime);

                var clientEntities = BufferTestHelper.GetClientEntities(testWorld, new [] {serverEntity});

                void Validate(int len1, int b1, int len2, int b2)
                {
                    var dynamicBuffer = testWorld.ClientWorlds[0].EntityManager
                        .GetBuffer<NetCode.SnapshotDynamicDataBuffer>(clientEntities[0]);
                    BufferTestHelper.ValidateMultiBufferSnapshotDataContents(dynamicBuffer, len1, b1, len2, b2);
                    BufferTestHelper.CheckBuffersValues(testWorld, serverEntity, clientEntities[0], true);
                }

                //Set buffers values
                BufferTestHelper.SetByteBufferValues(testWorld.ServerWorld, serverEntity, 10, 10);
                BufferTestHelper.SetBufferValues(testWorld.ServerWorld, serverEntity, 3, 0);

                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(frameTime);
                Validate(3, 0, 10, 10);
                //Shrink second buffer
                BufferTestHelper.SetBufferValues(testWorld.ServerWorld, serverEntity, 2, 20);
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(frameTime);
                Validate(2, 20, 10, 10);
                //Resize second buffer
                BufferTestHelper.SetBufferValues(testWorld.ServerWorld, serverEntity, 5, 30);
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(frameTime);
                Validate(5, 30, 10, 10);
                //Shrink first buffer
                BufferTestHelper.SetByteBufferValues(testWorld.ServerWorld, serverEntity, 5, 100);
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(frameTime);
                Validate(5, 30, 5, 100);
                //Resize first buffer
                BufferTestHelper.SetByteBufferValues(testWorld.ServerWorld, serverEntity, 15, 1000);
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(frameTime);
                Validate(5, 30, 15, 1000);
            }
        }

        public class GhostBufferMixedTypesConverter : TestNetCodeAuthoring.IConverter
        {
            public void Bake(GameObject gameObject, IBaker baker)
            {
                var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
                baker.AddComponent<GhostGen_IntStruct>(entity);
                baker.AddComponent<GhostGen_InterpolatedStruct>(entity);
                baker.AddBuffer<GhostGen_BufferInterpolated>(entity);
                baker.AddBuffer<GhostGenTest_Buffer>(entity);
                baker.AddBuffer<GhostGenBuffer_BufferComposite>(entity);
            }
        }

        [Test]
        public void BuffersSupportMultipleBuffers()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GhostBufferMixedTypesConverter();

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);
                testWorld.SpawnOnServer(ghostGameObject);

                var serverEntity = testWorld.TryGetSingletonEntity<GhostGen_IntStruct>(testWorld.ServerWorld);
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEntity, new GhostGen_IntStruct
                {
                    IntValue = 10
                });
                testWorld.ServerWorld.EntityManager.SetComponentData(serverEntity, new GhostGen_InterpolatedStruct
                {
                    FloatValue = 20.0f
                });
                var bufInterpolated =
                    testWorld.ServerWorld.EntityManager.GetBuffer<GhostGen_BufferInterpolated>(serverEntity);
                bufInterpolated.ResizeUninitialized(2);
                for (int i = 0; i < 2; ++i)
                {
                    int value = (i + 1) * 10000;
                    bufInterpolated[i] = new GhostGen_BufferInterpolated
                    {
                        FloatValue = ++value
                    };
                }

                var serverBuffer = testWorld.ServerWorld.EntityManager.GetBuffer<GhostGenTest_Buffer>(serverEntity);
                serverBuffer.ResizeUninitialized(2);
                for (int i = 0; i < 2; ++i)
                {
                    int value = (i + 1) * 1000;
                    serverBuffer[i] = (new GhostGenTest_Buffer
                    {
                        IntValue = ++value,
                        UIntValue = (uint) ++value,
                        BoolValue = true,
                        FloatValue = ++value
                    });
                }

                var bufComposite =
                    testWorld.ServerWorld.EntityManager.GetBuffer<GhostGenBuffer_BufferComposite>(serverEntity);
                bufComposite.ResizeUninitialized(2);
                for (int i = 0; i < 2; ++i)
                {
                    int value = i;
                    bufComposite[i] = new GhostGenBuffer_BufferComposite
                    {
                        Field1 = new GhostGen_CompositeStruct
                        {
                            IntValue1 = ++value,
                            IntValue2 = ++value,
                            IntValue3 = ++value
                        }
                    };
                }

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                // Go in-game
                testWorld.GoInGame();

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 64; ++i)
                    testWorld.Tick(frameTime);

                serverEntity = testWorld.TryGetSingletonEntity<GhostGen_IntStruct>(testWorld.ServerWorld);
                var clientEntity = testWorld.TryGetSingletonEntity<GhostGen_IntStruct>(testWorld.ClientWorlds[0]);
                Assert.AreEqual(10,
                    testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostGen_IntStruct>(clientEntity)
                        .IntValue);
                Assert.AreEqual(20.0f,
                    testWorld.ClientWorlds[0].EntityManager.GetComponentData<GhostGen_InterpolatedStruct>(clientEntity)
                        .FloatValue);

                var clientBufInterpolated = testWorld.ClientWorlds[0].EntityManager
                    .GetBuffer<GhostGen_BufferInterpolated>(clientEntity);
                var serverBufInterpolated =
                    testWorld.ServerWorld.EntityManager.GetBuffer<GhostGen_BufferInterpolated>(serverEntity);
                Assert.AreEqual(serverBufInterpolated.Length, clientBufInterpolated.Length);
                for (int i = 0; i < serverBufInterpolated.Length; ++i)
                {
                    var bs = serverBufInterpolated[i];
                    var cs = clientBufInterpolated[i];
                    Assert.AreEqual(bs.FloatValue, cs.FloatValue);
                }

                var clientBuffer = testWorld.ClientWorlds[0].EntityManager.GetBuffer<GhostGenTest_Buffer>(clientEntity);
                serverBuffer = testWorld.ServerWorld.EntityManager.GetBuffer<GhostGenTest_Buffer>(serverEntity);
                Assert.AreEqual(serverBuffer.Length, clientBuffer.Length);
                for (int i = 0; i < serverBuffer.Length; ++i)
                {
                    var bs = serverBuffer[i];
                    var cs = clientBuffer[i];
                    Assert.AreEqual(bs.IntValue, cs.IntValue);
                    Assert.AreEqual(bs.UIntValue, cs.UIntValue);
                    Assert.AreEqual(bs.BoolValue, cs.BoolValue);
                    Assert.AreEqual(bs.FloatValue, cs.FloatValue);
                }

                var clientBufComposite = testWorld.ClientWorlds[0].EntityManager
                    .GetBuffer<GhostGenBuffer_BufferComposite>(clientEntity);
                var serverBufComposite =
                    testWorld.ServerWorld.EntityManager.GetBuffer<GhostGenBuffer_BufferComposite>(serverEntity);
                Assert.AreEqual(serverBufComposite.Length, clientBufComposite.Length);
                for (int i = 0; i < serverBufComposite.Length; ++i)
                {
                    var bs = serverBufComposite[i];
                    var cs = clientBufComposite[i];
                    Assert.AreEqual(bs.Field1.IntValue1, cs.Field1.IntValue1);
                    Assert.AreEqual(bs.Field1.IntValue2, cs.Field1.IntValue2);
                    Assert.AreEqual(bs.Field1.IntValue3, cs.Field1.IntValue3);
                }
            }
        }

        [Test]
        public void BuffersSentWithFragmentedPipelineAreReceivedCorrectly()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<GhostByteBufferAuthoringComponent>();

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);
                testWorld.SpawnOnServer(ghostGameObject);
                var serverEntity = testWorld.TryGetSingletonEntity<GhostGenBuffer_ByteBuffer>(testWorld.ServerWorld);
                BufferTestHelper.SetByteBufferValues(testWorld.ServerWorld, serverEntity, 800, 0);
                //Because of the size the entity will be only sent using fragmentation. Buffers does not support
                //sending partial contents
                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);
                // Go in-game
                testWorld.GoInGame();
                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(frameTime);
                var clientEntities = BufferTestHelper.GetClientEntities(testWorld, new [] {serverEntity});
                BufferTestHelper.CheckByteBufferValues(testWorld, serverEntity, clientEntities[0]);
            }
        }

        [Test]
        public void BuffersSentInPartialChunkAreReceivedCorrectly()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<GhostByteBufferAuthoringComponent>();

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);
                //This should be about 3000 kb of data plus some extra for other components
                //It should end be sent in 2/3 chunk
                var serverEntities = new Entity[20];
                for (int i = 0; i < 20; ++i)
                {
                    serverEntities[i] = testWorld.SpawnOnServer(ghostGameObject);
                    BufferTestHelper.SetByteBufferValues(testWorld.ServerWorld, serverEntities[i], 15, 10);
                }

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                // Go in-game
                testWorld.GoInGame();

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(frameTime);

                var clientEntities = BufferTestHelper.GetClientEntities(testWorld, serverEntities);
                for (int i = 0; i < serverEntities.Length; ++i)
                {
                    BufferTestHelper.CheckByteBufferValues(testWorld, serverEntities[i], clientEntities[i]);
                }
            }
        }

        [DisableAutoCreation]
        partial class ForceSerializeBufferSystem : DefaultVariantSystemBase
        {
            protected override void RegisterDefaultVariants(Dictionary<ComponentType, Rule> defaultVariants)
            {
                defaultVariants.Add(typeof(GhostGenTest_Buffer), Rule.ForAll(typeof(GhostGenTest_Buffer)));
            }
        }
        [DisableAutoCreation]
        partial class ForceSerializeOnlyChildBufferSystem : DefaultVariantSystemBase
        {
            protected override void RegisterDefaultVariants(Dictionary<ComponentType, Rule> defaultVariants)
            {
                defaultVariants.Add(typeof(GhostGenTest_Buffer), Rule.Unique(typeof(DontSerializeVariant), typeof(GhostGenTest_Buffer)));
            }
        }

        [DisableAutoCreation]
        partial class ForceDontSerializeBufferSystem : DefaultVariantSystemBase
        {
            protected override void RegisterDefaultVariants(Dictionary<ComponentType, Rule> defaultVariants)
            {
                defaultVariants.Add(typeof(GhostGenTest_Buffer), Rule.ForAll(typeof(DontSerializeVariant)));
            }
        }

        [Test]
        public void ChildEntitiesBuffersAreSerializedCorrectly([Values]SendForChildrenTestCase sendForChildrenTestCase)
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                switch (sendForChildrenTestCase)
                {
                    case SendForChildrenTestCase.YesViaExplicitVariantRule:
                        testWorld.TestSpecificAdditionalSystems.Add(typeof(ForceSerializeBufferSystem));
                        break;
                    case SendForChildrenTestCase.YesViaExplicitVariantOnlyAllowChildrenToReplicateRule:
                        testWorld.TestSpecificAdditionalSystems.Add(typeof(ForceSerializeOnlyChildBufferSystem));
                        break;
                    case SendForChildrenTestCase.NoViaExplicitDontSerializeVariantRule:
                        testWorld.TestSpecificAdditionalSystems.Add(typeof(ForceDontSerializeBufferSystem));
                        break;
                }
                testWorld.Bootstrap(true);

                var ghostGameObject = new GameObject();
                //Parent and children can have different buffers types (or the same)
                ghostGameObject.AddComponent<GhostByteBufferAuthoringComponent>();
                int numChild = 1;
                for (int i = 0; i < numChild; ++i)
                {
                    var childGo = new GameObject("child");
                    childGo.AddComponent<GhostGenBufferAuthoringComponent>();
                    childGo.transform.parent = ghostGameObject.transform;

                    // Ensure the buffer on the child is serialized:
                    if (sendForChildrenTestCase == SendForChildrenTestCase.YesViaInspectionComponentOverride)
                    {
                        var childInspectionComponent = childGo.AddComponent<GhostAuthoringInspectionComponent>();
                        var fullTypeName = typeof(GhostGenTest_Buffer).FullName;
                        childInspectionComponent.ComponentOverrides = new[]
                        {
                            new GhostAuthoringInspectionComponent.ComponentOverride
                            {
                                FullTypeName = fullTypeName,
                                PrefabType = GhostPrefabType.All,
                                SendTypeOptimization = GhostSendType.AllClients,
                                VariantHash = GhostVariantsUtility.UncheckedVariantHashNBC(fullTypeName, fullTypeName),
                            },
                        };
                    }
                }

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);
                var serverEntity = testWorld.SpawnOnServer(ghostGameObject);
                var serverEntityGroup = testWorld.ServerWorld.EntityManager.GetBuffer<LinkedEntityGroup>(serverEntity);
                Assert.AreEqual(2, serverEntityGroup.Length);
                BufferTestHelper.SetByteBufferValues(testWorld.ServerWorld, serverEntityGroup[0].Value, 10, 10);
                BufferTestHelper.SetBufferValues(testWorld.ServerWorld, serverEntityGroup[1].Value, 3, 0);

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                // Go in-game
                testWorld.GoInGame();

                // Let the game run for a bit so the ghosts are spawned on the client
                // This requires 32 states to be sent, and we need a few frames for the ghost types to be synchronized
                const int sendIterationCount = 32 + 4;
                for (int i = 0; i < sendIterationCount; ++i)
                    testWorld.Tick(frameTime);

                var clientEntities = BufferTestHelper.GetClientEntities(testWorld, new [] {serverEntity});
                serverEntityGroup = testWorld.ServerWorld.EntityManager.GetBuffer<LinkedEntityGroup>(serverEntity);
                var clientEntityGroup = testWorld.ClientWorlds[0].EntityManager.GetBuffer<LinkedEntityGroup>(clientEntities[0]);
                Assert.AreEqual(2, clientEntityGroup.Length);
                Assert.AreEqual(2, serverEntityGroup.Length);

                //Verify that the client snapshot data contains the right things
                var shouldChildReceiveData = GhostSerializationTestsForEnableableBits.IsExpectedToReplicateBuffer<GhostGenTest_Buffer>(sendForChildrenTestCase, false);
                var dynamicBuffer = testWorld.ClientWorlds[0].EntityManager.GetBuffer<NetCode.SnapshotDynamicDataBuffer>(clientEntities[0]);
                if(shouldChildReceiveData)
                    BufferTestHelper.ValidateMultiBufferSnapshotDataContents(dynamicBuffer, 3, 0, 10, 10);
                BufferTestHelper.CheckByteBufferValues(testWorld, serverEntityGroup[0].Value,
                    clientEntityGroup[0].Value);
                BufferTestHelper.CheckBuffersValues(testWorld, serverEntityGroup[1].Value, clientEntityGroup[1].Value, shouldChildReceiveData);
                //Change values
                BufferTestHelper.SetByteBufferValues(testWorld.ServerWorld, serverEntityGroup[0].Value, 10, 30);
                BufferTestHelper.SetBufferValues(testWorld.ServerWorld, serverEntityGroup[1].Value, 3, 5);
                for (int i = 0; i < sendIterationCount; ++i)
                    testWorld.Tick(frameTime);
                BufferTestHelper.CheckByteBufferValues(testWorld, serverEntityGroup[0].Value,
                    clientEntityGroup[0].Value);
                BufferTestHelper.CheckBuffersValues(testWorld, serverEntityGroup[1].Value, clientEntityGroup[1].Value, shouldChildReceiveData);
                //Shrink child buffer
                BufferTestHelper.SetBufferValues(testWorld.ServerWorld, serverEntityGroup[1].Value, 2, 20);
                for (int i = 0; i < sendIterationCount; ++i)
                    testWorld.Tick(frameTime);
                BufferTestHelper.CheckByteBufferValues(testWorld, serverEntityGroup[0].Value,
                    clientEntityGroup[0].Value);
                BufferTestHelper.CheckBuffersValues(testWorld, serverEntityGroup[1].Value, clientEntityGroup[1].Value, shouldChildReceiveData);
                //Grow child buffer
                BufferTestHelper.SetBufferValues(testWorld.ServerWorld, serverEntityGroup[1].Value, 5, 30);
                for (int i = 0; i < sendIterationCount; ++i)
                    testWorld.Tick(frameTime);
                BufferTestHelper.CheckByteBufferValues(testWorld, serverEntityGroup[0].Value,
                    clientEntityGroup[0].Value);
                BufferTestHelper.CheckBuffersValues(testWorld, serverEntityGroup[1].Value, clientEntityGroup[1].Value, shouldChildReceiveData);
                //Shrink root buffer
                BufferTestHelper.SetByteBufferValues(testWorld.ServerWorld, serverEntityGroup[0].Value, 5, 50);
                for (int i = 0; i < sendIterationCount; ++i)
                    testWorld.Tick(frameTime);
                BufferTestHelper.CheckByteBufferValues(testWorld, serverEntityGroup[0].Value,
                    clientEntityGroup[0].Value);
                BufferTestHelper.CheckBuffersValues(testWorld, serverEntityGroup[1].Value, clientEntityGroup[1].Value, shouldChildReceiveData);
                //grow root buffer
                BufferTestHelper.SetByteBufferValues(testWorld.ServerWorld, serverEntityGroup[0].Value, 15, 100);
                for (int i = 0; i < sendIterationCount; ++i)
                    testWorld.Tick(frameTime);
                BufferTestHelper.CheckByteBufferValues(testWorld, serverEntityGroup[0].Value, clientEntityGroup[0].Value);
                BufferTestHelper.CheckBuffersValues(testWorld, serverEntityGroup[1].Value, clientEntityGroup[1].Value, shouldChildReceiveData);
            }
        }


        public class GhostGroupGhostConverter : TestNetCodeAuthoring.IConverter
        {
            public void Bake(GameObject gameObject, IBaker baker)
            {
                var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
                baker.AddComponent(entity, new GhostOwner());
                baker.DependsOn(gameObject);
                if (gameObject.name == "ParentGhost")
                {
                    baker.AddBuffer<GhostGroup>(entity);
                }
                else
                {
                    baker.AddComponent(entity, default(GhostChildEntity));
                    baker.AddBuffer<GhostGenBuffer_ByteBuffer>(entity);
                }
            }
        }

        [Test]
        public void GhostGroupBuffersAreSerialized()
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
                var serverRoot = testWorld.SpawnOnServer(ghostGameObject);
                var serverChild = testWorld.SpawnOnServer(childGhostGameObject);
                testWorld.ServerWorld.EntityManager.GetBuffer<GhostGroup>(serverRoot).Add(new GhostGroup {Value = serverChild});
                var buffer = testWorld.ServerWorld.EntityManager.GetBuffer<GhostGenBuffer_ByteBuffer>(serverChild);
                BufferTestHelper.SetByteBufferValues(testWorld.ServerWorld, serverChild, 10, 10);

                float frameTime = 1.0f / 60.0f;
                testWorld.Connect(frameTime);
                testWorld.GoInGame();
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(frameTime);

                var clientEntities = BufferTestHelper.GetClientEntities(testWorld, new [] {serverRoot, serverChild});
                BufferTestHelper.CheckByteBufferValues(testWorld, serverChild, clientEntities[1]);
            }
        }

        [GhostComponent(PrefabType = GhostPrefabType.Server)]
        public struct GhostServerOnlyBuffer : IBufferElementData
        {
            [GhostField] public byte Value;
        }

        [GhostComponent(PrefabType = GhostPrefabType.Client)]
        public struct GhostClientOnlyBuffer : IBufferElementData
        {
            [GhostField] public byte Value;
        }

        [GhostComponent(PrefabType = GhostPrefabType.AllPredicted, SendDataForChildEntity = true)]
        public struct GhostPredictedOnlyBuffer : IBufferElementData
        {
            [GhostField] public float Value;
        }

        [GhostComponent(PrefabType = GhostPrefabType.InterpolatedClient, SendDataForChildEntity = true)]
        public struct GhostInterpolatedOnlyBuffer : IBufferElementData
        {
            [GhostField] public byte Value;
        }

        unsafe struct GenericConverter<T>: TestNetCodeAuthoring.IConverter where T: unmanaged, IBufferElementData
        {
            public void Bake(GameObject gameObject, IBaker baker)
            {
                var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
                baker.AddBuffer<T>(entity);
            }
        }

        [Test]
        [TestCase(typeof(GhostServerOnlyBuffer), true, false, TestName = "ServerOnly")]
        [TestCase(typeof(GhostClientOnlyBuffer), false, true, TestName = "ClientOnly")]
        public void BuffersAreNotSerialized(Type bufferType, bool presentOnServer, bool presentOnClient)
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);

                var conv = typeof(GenericConverter<>);
                var args = new []{bufferType};
                var converterType = conv.MakeGenericType(args);
                var converter = Activator.CreateInstance(converterType) as TestNetCodeAuthoring.IConverter;

                var ghostGameObject = new GameObject();
                var ghostConfig = ghostGameObject.AddComponent<GhostAuthoringComponent>();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = converter;
                ghostConfig.DefaultGhostMode = GhostMode.Interpolated;
                ghostConfig.SupportedGhostModes = GhostModeMask.Interpolated;

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);

                float frameTime = 1.0f / 60.0f;
                testWorld.Connect(frameTime);
                testWorld.GoInGame();

                var serverEntity = testWorld.SpawnOnServer(ghostGameObject);
                Assert.AreEqual(presentOnServer, testWorld.ServerWorld.EntityManager.HasComponent(serverEntity, bufferType));

                var serverCollectionEntity = testWorld.TryGetSingletonEntity<GhostCollectionPrefabSerializer>(testWorld.ServerWorld);
                var clientCollectionEntity = testWorld.TryGetSingletonEntity<GhostCollectionPrefabSerializer>(testWorld.ClientWorlds[0]);
                Assert.AreNotEqual(Entity.Null, serverCollectionEntity);
                Assert.AreNotEqual(Entity.Null, clientCollectionEntity);

                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(frameTime);

                var ghostType = testWorld.ServerWorld.EntityManager.GetComponentData<GhostInstance>(serverEntity).ghostType;
                var serverCollection = testWorld.ServerWorld.EntityManager.GetBuffer<GhostCollectionPrefabSerializer>(serverCollectionEntity);
                Assert.AreEqual(0, serverCollection[ghostType].NumBuffers);

                var clientEntities = BufferTestHelper.GetClientEntities(testWorld, new []{serverEntity});
                Assert.AreEqual(presentOnClient, testWorld.ClientWorlds[0].EntityManager.HasComponent(clientEntities[0], bufferType));
                Assert.AreEqual(presentOnClient, testWorld.ClientWorlds[0].EntityManager.HasComponent<SnapshotDynamicDataBuffer>(clientEntities[0]));
                var clientCollection = testWorld.ClientWorlds[0].EntityManager.GetBuffer<GhostCollectionPrefabSerializer>(clientCollectionEntity);
                Assert.AreEqual(0, clientCollection[ghostType].NumBuffers);
            }
        }


        [DisableAutoCreation]
        [RequireMatchingQueriesForUpdate]
        [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
        [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
        public partial class BufferTestPredictionSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
                var deltaTime = SystemAPI.Time.DeltaTime;
                var bufferFromEntity = GetBufferLookup<GhostPredictedOnlyBuffer>();
                //FIXME: updating child entities is not efficient this way.
                Entities.WithAll<Simulate, GhostInstance>().ForEach((in DynamicBuffer<LinkedEntityGroup> group) =>
                {
                    for (int i = 0; i < group.Length; ++i)
                    {
                        var e = group[i];
                        var buf = bufferFromEntity[e.Value];
                        var t = (int) (tick.TickIndexForValidTick % buf.Length);
                        var v = buf[t];
                        v.Value += deltaTime * 60.0f;
                        buf[t] = v;
                    }
                }).Run();
            }
        }

        [Test]
        public void PredictedGhostsBackupAndRestoreBufferCorrectly()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true,  typeof(BufferTestPredictionSystem));

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GenericConverter<GhostPredictedOnlyBuffer>();
                var child = new GameObject();
                child.AddComponent<TestNetCodeAuthoring>().Converter = new GenericConverter<GhostPredictedOnlyBuffer>();
                child.transform.parent = ghostGameObject.transform;
                var ghostConfig = ghostGameObject.AddComponent<GhostAuthoringComponent>();
                ghostConfig.DefaultGhostMode = GhostMode.Predicted;
                ghostConfig.SupportedGhostModes = GhostModeMask.Predicted;
                ghostConfig.HasOwner = true;

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);

                float frameTime = 1.0f / 60.0f;
                testWorld.Connect(frameTime);
                testWorld.GoInGame();

                //Disable the prediction logic
                testWorld.ServerWorld.GetExistingSystemManaged<BufferTestPredictionSystem>().Enabled = false;
                testWorld.ClientWorlds[0].GetExistingSystemManaged<BufferTestPredictionSystem>().Enabled = false;

                //Spawn the entity and init the buffer
                var serverEntity = testWorld.SpawnOnServer(ghostGameObject);
                {
                    testWorld.ServerWorld.EntityManager.SetComponentData(serverEntity, new GhostOwner {NetworkId = 0});
                    var group = testWorld.ServerWorld.EntityManager.GetBuffer<LinkedEntityGroup>(serverEntity);
                    for(int e=0;e<2;++e)
                    {
                        var buffer = testWorld.ServerWorld.EntityManager.GetBuffer<GhostPredictedOnlyBuffer>(group[e].Value);
                        buffer.ResizeUninitialized(16);
                        for (int i = 0; i < 16; ++i)
                            buffer[i] = new GhostPredictedOnlyBuffer {Value = 10.0f * i};
                    }
                }

                for (int i = 0; i < 8; ++i)
                    testWorld.Tick(frameTime);

                var clientEntities = BufferTestHelper.GetClientEntities(testWorld, new [] {serverEntity});
                var serverEntityGroup = testWorld.ServerWorld.EntityManager.GetBuffer<LinkedEntityGroup>(serverEntity);
                var clientEntityGroup = testWorld.ClientWorlds[0].EntityManager.GetBuffer<LinkedEntityGroup>(clientEntities[0]);
                Assert.AreEqual(2, clientEntityGroup.Length);
                Assert.AreEqual(2, serverEntityGroup.Length);

                var serverBuffers = new[]
                {
                    testWorld.ServerWorld.EntityManager.GetBuffer<GhostPredictedOnlyBuffer>(serverEntityGroup[0].Value),
                    testWorld.ServerWorld.EntityManager.GetBuffer<GhostPredictedOnlyBuffer>(serverEntityGroup[1].Value)
                };
                var clientBuffers = new[]
                {
                    testWorld.ClientWorlds[0].EntityManager.GetBuffer<GhostPredictedOnlyBuffer>(clientEntityGroup[0].Value),
                    testWorld.ClientWorlds[0].EntityManager.GetBuffer<GhostPredictedOnlyBuffer>(clientEntityGroup[1].Value)
                };

                Assert.AreEqual(serverBuffers[0].Length, clientBuffers[0].Length);
                Assert.AreEqual(serverBuffers[1].Length, clientBuffers[1].Length);

                testWorld.ServerWorld.GetExistingSystemManaged<BufferTestPredictionSystem>().Enabled = true;
                testWorld.ClientWorlds[0].GetExistingSystemManaged<BufferTestPredictionSystem>().Enabled = true;
                var firstPredTick = testWorld.GetNetworkTime(testWorld.ServerWorld).ServerTick;
                firstPredTick.Increment();
                for (int i = 0; i < 32; ++i)
                {
                    testWorld.Tick(frameTime / 4.0f);
                }
                testWorld.ServerWorld.GetExistingSystemManaged<BufferTestPredictionSystem>().Enabled = false;
                testWorld.ClientWorlds[0].GetExistingSystemManaged<BufferTestPredictionSystem>().Enabled = false;

                var networkTimeClient = testWorld.GetNetworkTime(testWorld.ClientWorlds[0]);
                var curTick = networkTimeClient.ServerTick;

                serverBuffers = new[]
                {
                    testWorld.ServerWorld.EntityManager.GetBuffer<GhostPredictedOnlyBuffer>(serverEntityGroup[0].Value),
                    testWorld.ServerWorld.EntityManager.GetBuffer<GhostPredictedOnlyBuffer>(serverEntityGroup[1].Value)
                };
                clientBuffers = new[]
                {
                    testWorld.ClientWorlds[0].EntityManager.GetBuffer<GhostPredictedOnlyBuffer>(clientEntityGroup[0].Value),
                    testWorld.ClientWorlds[0].EntityManager.GetBuffer<GhostPredictedOnlyBuffer>(clientEntityGroup[1].Value)
                };

                var networkTimeServer = testWorld.GetNetworkTime(testWorld.ServerWorld);
                for (var i = firstPredTick; i != curTick; i.Increment())
                {
                    var expected = ((int)i.TickIndexForValidTick % clientBuffers[0].Length)*10.0f + 1.0f;
                    Assert.AreEqual(expected, clientBuffers[0][(int)i.TickIndexForValidTick%clientBuffers[0].Length].Value);
                    Assert.AreEqual(expected, clientBuffers[1][(int)i.TickIndexForValidTick%clientBuffers[1].Length].Value);
                    if (networkTimeServer.ServerTick.IsNewerThan(curTick))
                    {
                        Assert.AreEqual(expected, serverBuffers[0][(int)i.TickIndexForValidTick%serverBuffers[0].Length].Value);
                        Assert.AreEqual(expected, serverBuffers[1][(int)i.TickIndexForValidTick%serverBuffers[1].Length].Value);
                    }
                }
                //run a bit and check everything is in sync
                for (int i = 0; i < 8; ++i)
                    testWorld.Tick(frameTime);

                for (int i = 0; i < 16; ++i)
                {
                    Assert.AreEqual(serverBuffers[0][i].Value, clientBuffers[0][i].Value);
                    Assert.AreEqual(serverBuffers[1][i].Value, clientBuffers[1][i].Value);
                }

                //Check that if the buffer size change, the prediction buffer is resized and everything still works
                serverBuffers[0].ResizeUninitialized(22);
                serverBuffers[1].ResizeUninitialized(20);
                for (int i = 0; i < 22; ++i)
                    serverBuffers[0][i] = new GhostPredictedOnlyBuffer {Value = 10.0f * i};
                for (int i = 0; i < 20; ++i)
                    serverBuffers[1][i] = new GhostPredictedOnlyBuffer {Value = 20.0f * i};

                //run a bit and check everything is in sync
                for (int i = 0; i < 8; ++i)
                    testWorld.Tick(frameTime);

                clientBuffers = new[]
                {
                    testWorld.ClientWorlds[0].EntityManager.GetBuffer<GhostPredictedOnlyBuffer>(clientEntityGroup[0].Value),
                    testWorld.ClientWorlds[0].EntityManager.GetBuffer<GhostPredictedOnlyBuffer>(clientEntityGroup[1].Value)
                };
                for (int i = 0; i < 22; ++i)
                {
                    Assert.AreEqual(serverBuffers[0][i].Value, clientBuffers[0][i].Value);
                }
                for (int i = 0; i < 20; ++i)
                {
                    Assert.AreEqual(serverBuffers[1][i].Value, clientBuffers[1][i].Value);
                }


            }
        }

        [UpdateInGroup(typeof(GhostSpawnClassificationSystemGroup))]
        [UpdateAfter(typeof(GhostSpawnClassificationSystem))]
        [DisableAutoCreation]
        [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
        public partial class TestSpawnClassificationSystem : SystemBase
        {
            public NativeList<Entity> m_PredictedEntities;
            protected override void OnCreate()
            {
                RequireForUpdate<GhostSpawnQueue>();
                RequireForUpdate<PredictedGhostSpawnList>();
                m_PredictedEntities = new NativeList<Entity>(5,Allocator.Persistent);
            }

            protected override void OnDestroy()
            {
                m_PredictedEntities.Dispose();
            }

            protected override void OnUpdate()
            {
                var spawnListEntity = SystemAPI.GetSingletonEntity<PredictedGhostSpawnList>();
                var spawnListFromEntity = GetBufferLookup<PredictedGhostSpawn>();
                var predictedEntities = m_PredictedEntities;
                Entities
                    .WithAll<GhostSpawnQueue>()
                    .ForEach((DynamicBuffer<GhostSpawnBuffer> ghosts) =>
                    {
                        var spawnList = spawnListFromEntity[spawnListEntity];
                        for (int i = 0; i < ghosts.Length; ++i)
                        {
                            var ghost = ghosts[i];
                            if (ghost.SpawnType != GhostSpawnBuffer.Type.Predicted)
                                continue;
                            for (int j = 0; j < spawnList.Length; ++j)
                            {
                                if (ghost.GhostType == spawnList[j].ghostType &&
                                    math.abs(ghost.ServerSpawnTick.TicksSince(spawnList[j].spawnTick)) < 5)
                                {
                                    ghost.PredictedSpawnEntity = spawnList[j].entity;
                                    spawnList[j] = spawnList[spawnList.Length-1];
                                    spawnList.RemoveAt(spawnList.Length - 1);
                                    predictedEntities.Add(ghost.PredictedSpawnEntity);
                                    break;
                                }
                            }
                            ghosts[i] = ghost;
                        }
                    }).Run();
            }
        }

        [DisableAutoCreation]
        [RequireMatchingQueriesForUpdate]
        [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
        public partial class SpawnPredictedGhost : SystemBase
        {
            public NetworkTick spawnAtTick = NetworkTick.Invalid;
            public Entity spawnedEntity;
            Entity SpawnPredictedEntity(int baseValue)
            {
                var prefabsList = SystemAPI.GetSingletonEntity<NetCodeTestPrefabCollection>();
                var prefabs = EntityManager.GetBuffer<NetCodeTestPrefab>(prefabsList);
                var entity = EntityManager.Instantiate(prefabs[0].Value);
                BufferTestHelper.SetByteBufferValues(World, entity, 5, baseValue);
                return entity;
            }
            protected override void OnUpdate()
            {
                var netTime = SystemAPI.GetSingleton<NetworkTime>();
                if (spawnAtTick.IsValid && !spawnAtTick.IsNewerThan(NetworkTimeHelper.LastFullServerTick(netTime)))
                {
                    if(SystemAPI.HasSingleton<UnscaledClientTime>())
                        spawnedEntity = SpawnPredictedEntity(10);
                    else
                        spawnedEntity = SpawnPredictedEntity(100);
                    spawnAtTick = NetworkTick.Invalid;
                }
            }
        }

        [Test]
        public void PredictedSpawnedGhostSerializeBufferCorrectly()
        {

            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(TestSpawnClassificationSystem), typeof(SpawnPredictedGhost));

                var ghostGameObject = new GameObject("PredictedSpawnedTestGhost");
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GenericConverter<GhostGenBuffer_ByteBuffer>();
                var ghostConfig = ghostGameObject.AddComponent<GhostAuthoringComponent>();
                ghostConfig.DefaultGhostMode = GhostMode.OwnerPredicted;
                ghostConfig.SupportedGhostModes = GhostModeMask.Predicted;
                ghostConfig.HasOwner = true;

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);

                float frameTime = 1.0f / 60.0f;
                testWorld.Connect(frameTime);
                testWorld.GoInGame();

                //run for a bit to sync server time and ghost collections
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(frameTime);

                //Spawn entity on both client and server
                Entity clientEntity = Entity.Null;
                Entity serverEntity = Entity.Null;
                var networkTimeServer = testWorld.GetNetworkTime(testWorld.ServerWorld);
                var clientWorld = testWorld.ClientWorlds[0];
                var networkTimeClient = testWorld.GetNetworkTime(clientWorld);
                var spawnTick = networkTimeServer.ServerTick;
                spawnTick.Add(5);
                var clientSpawnTick = networkTimeClient.ServerTick;
                clientSpawnTick.Increment();
                if (clientSpawnTick.IsNewerThan(spawnTick))
                    spawnTick = clientSpawnTick;
                testWorld.ServerWorld.GetExistingSystemManaged<SpawnPredictedGhost>().spawnAtTick = spawnTick;
                clientWorld.GetExistingSystemManaged<SpawnPredictedGhost>().spawnAtTick = spawnTick;
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(frameTime);

                serverEntity = testWorld.ServerWorld.GetExistingSystemManaged<SpawnPredictedGhost>().spawnedEntity;
                clientEntity = clientWorld.GetExistingSystemManaged<SpawnPredictedGhost>().spawnedEntity;
                //Check that entities match
                Assert.AreNotEqual(serverEntity, Entity.Null);
                Assert.AreNotEqual(clientEntity, Entity.Null);
                var clientEntities = BufferTestHelper.GetClientEntities(testWorld, new [] {serverEntity});
                Assert.AreEqual(clientEntity, clientEntities[0]);
                //Check we classified correctly
                var classificationSystem = clientWorld.GetExistingSystemManaged<TestSpawnClassificationSystem>();
                Assert.AreEqual(1, classificationSystem.m_PredictedEntities.Length);
                Assert.AreEqual(clientEntity, classificationSystem.m_PredictedEntities[0]);
                //And buffers are the same
                BufferTestHelper.CheckByteBufferValues(testWorld, serverEntity, clientEntity);
            }
        }
    }
}
