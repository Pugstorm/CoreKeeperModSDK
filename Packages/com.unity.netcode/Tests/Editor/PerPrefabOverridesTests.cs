using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode.LowLevel.Unsafe;
using UnityEngine;

namespace Unity.NetCode.Tests
{
    // TODO - Test case to ensure the default variants for all components on the root level entity are `DefaultSerialization`.
    // TODO - Test case to ensure manually specified defaults are respected.
    // TODO - Test case to ensure the default variant for all components on all child entities are `DontSerializeVariant`.
    // TODO - Test case for usage of `ClientOnlyVariant`.

    [TestFixture]
    public class PerPrefabOverridesTests
    {
        public class GhostConverter : TestNetCodeAuthoring.IConverter
        {
            public void Bake(GameObject gameObject, IBaker baker)
            {
                var transform = baker.GetComponent<Transform>();
                baker.DependsOn(transform.parent);
                var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
                if(transform.parent == null)
                    baker.AddComponent(entity, new GhostOwner { NetworkId = -1});
                baker.AddComponent(entity, new GhostGen_IntStruct());
            }
        }

        GameObject[] CreatePrefabs(string[] names)
        {
            var collection = new GameObject[names.Length];
            for (int i = 0; i < names.Length; ++i)
            {
                var ghostGameObject = new GameObject(names[i]);
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GhostConverter();
                var childGhost = new GameObject("Child");
                childGhost.transform.parent = ghostGameObject.transform;
                childGhost.AddComponent<TestNetCodeAuthoring>().Converter = new GhostConverter();
                var nestedChildGhost = new GameObject("NestedChild");
                nestedChildGhost.transform.parent = childGhost.transform;
                nestedChildGhost.AddComponent<TestNetCodeAuthoring>().Converter = new GhostConverter();
                var authoring = ghostGameObject.AddComponent<GhostAuthoringComponent>();
                authoring.DefaultGhostMode = GhostMode.OwnerPredicted;
                authoring.SupportedGhostModes = GhostModeMask.All;
                collection[i] = ghostGameObject;
            }

            return collection;
        }

        //Check that the component prefab serializer and indexes are initialized as expected
        void CheckCollection(World world, int serializerIndex, int entityIndex)
        {
            using var collectionQuery = world.EntityManager.CreateEntityQuery(typeof(GhostCollection));
            var collection = collectionQuery.GetSingletonEntity();
            var ghostSerializerCollection = world.EntityManager.GetBuffer<GhostCollectionPrefabSerializer>(collection);
            var ghostComponentIndex = world.EntityManager.GetBuffer<GhostCollectionComponentIndex>(collection);
            Assert.AreEqual(4, ghostSerializerCollection.Length);
            //First 3 (all, predicted, interpolated) should have the component (also the GhostGen_IntStruct)
            for (int i = 0; i < ghostSerializerCollection.Length; ++i)
            {
                if(serializerIndex != ghostComponentIndex[ghostSerializerCollection[i].FirstComponent].SerializerIndex)
                    continue;
                if (ghostSerializerCollection[i].NumComponents == 5)
                {
                    Assert.AreEqual(1, ghostSerializerCollection[i].NumChildComponents);
                    Assert.AreEqual(2, ghostComponentIndex.AsNativeArray()
                        .GetSubArray(ghostSerializerCollection[i].FirstComponent, 5)
                        .Count(t => t.SerializerIndex == serializerIndex));
                }
                //The (none) variant should have 4
                else if (ghostSerializerCollection[i].NumComponents == 4)
                {
                    Assert.AreEqual(entityIndex==0?1:0, ghostSerializerCollection[i].NumChildComponents);
                    Assert.AreEqual(1, ghostComponentIndex.AsNativeArray()
                        .GetSubArray(ghostSerializerCollection[i].FirstComponent, 4)
                        .Count(t => t.SerializerIndex == serializerIndex));
                }
                else
                {
                    Assert.Fail("Invalid number of componenent");
                }
            }
        }

        [Test]
        public void OverrideComponentPrefabType_RootEntity()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                var names = new[] {"ServerOnly", "ClientOnly", "PredictedOnly", "InterpolatedOnly"};
                var prefabTypes = new[] {GhostPrefabType.Server, GhostPrefabType.Client, GhostPrefabType.InterpolatedClient, GhostPrefabType.PredictedClient};
                var collection = CreatePrefabs(names);
                //overrides the component prefab types in different prefabs
                for (int i = 0; i < prefabTypes.Length; ++i)
                {
                    var gameObject = collection[i];
                    var inspection = gameObject.AddComponent<GhostAuthoringInspectionComponent>();
                    inspection.ComponentOverrides = new []
                    {
                        new GhostAuthoringInspectionComponent.ComponentOverride
                        {
                            FullTypeName = typeof(GhostGen_IntStruct).FullName,
                            PrefabType = prefabTypes[i],
                            SendTypeOptimization = GhostSendType.AllClients,
                            VariantHash = 0
                        }
                    };
                }

                Assert.IsTrue(testWorld.CreateGhostCollection(collection));
                testWorld.CreateWorlds(true, 1);

                //Register serializers and setup all the system
                for(int i=0;i<16;++i)
                    testWorld.Tick(1.0f/60.0f);

                //Then check the expected results
                var ghostCollection = testWorld.TryGetSingletonEntity<NetCodeTestPrefabCollection>(testWorld.ServerWorld);
                var prefabList = testWorld.ServerWorld.EntityManager.GetBuffer<NetCodeTestPrefab>(ghostCollection).ToNativeArray(Allocator.Temp);
                Assert.AreEqual(4, prefabList.Length);
                for (int i = 0; i < prefabList.Length; ++i)
                {
                    if ((prefabTypes[i] & GhostPrefabType.Server) != 0)
                        Assert.IsTrue(testWorld.ServerWorld.EntityManager.HasComponent<GhostGen_IntStruct>(prefabList[i].Value));
                    else
                        Assert.IsFalse(testWorld.ServerWorld.EntityManager.HasComponent<GhostGen_IntStruct>(prefabList[i].Value));
                    var linkedGroupBuffer = testWorld.ServerWorld.EntityManager.GetBuffer<LinkedEntityGroup>(prefabList[i].Value);
                    Assert.IsTrue(testWorld.ServerWorld.EntityManager.HasComponent<GhostGen_IntStruct>(linkedGroupBuffer[1].Value));
                }

                ghostCollection = testWorld.TryGetSingletonEntity<NetCodeTestPrefabCollection>(testWorld.ClientWorlds[0]);
                prefabList = testWorld.ClientWorlds[0].EntityManager.GetBuffer<NetCodeTestPrefab>(ghostCollection).ToNativeArray(Allocator.Temp);
                Assert.AreEqual(4, prefabList.Length);
                for (int i = 0; i < prefabList.Length; ++i)
                {
                    if ((prefabTypes[i] & GhostPrefabType.Client) != 0)
                        Assert.IsTrue(testWorld.ClientWorlds[0].EntityManager.HasComponent<GhostGen_IntStruct>(prefabList[i].Value));
                    else
                        Assert.IsFalse(testWorld.ClientWorlds[0].EntityManager.HasComponent<GhostGen_IntStruct>(prefabList[i].Value));
                    var linkedGroupBuffer = testWorld.ClientWorlds[0].EntityManager.GetBuffer<LinkedEntityGroup>(prefabList[i].Value);
                    Assert.IsTrue(testWorld.ClientWorlds[0].EntityManager.HasComponent<GhostGen_IntStruct>(linkedGroupBuffer[1].Value));
                }
            }
        }

        [Test]
        public void OverrideComponentPrefabType_ChildEntity()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                var names = new[] {"ServerOnly", "ClientOnly", "PredictedOnly", "InterpolatedOnly"};
                var prefabTypes = new[] {GhostPrefabType.Server, GhostPrefabType.Client, GhostPrefabType.InterpolatedClient, GhostPrefabType.PredictedClient};
                var collection = CreatePrefabs(names);
                //Only modify child behaviors
                for (int i = 0; i < prefabTypes.Length; ++i)
                {
                    var gameObject = collection[i];
                    var child = gameObject.transform.GetChild(0);
                    child.gameObject.AddComponent<GhostAuthoringInspectionComponent>().ComponentOverrides = new []
                    {
                        new GhostAuthoringInspectionComponent.ComponentOverride
                        {
                            FullTypeName = typeof(GhostGen_IntStruct).FullName,
                            PrefabType = prefabTypes[i],
                            SendTypeOptimization = GhostSendType.AllClients,
                            VariantHash = 0
                        }
                    };
                }

                Assert.IsTrue(testWorld.CreateGhostCollection(collection));
                testWorld.CreateWorlds(true, 1);

                //Register serializers and setup all the system
                for(int i=0;i<16;++i)
                    testWorld.Tick(1.0f/60.0f);

                //Then check the expected results
                //Server
                var ghostCollection = testWorld.TryGetSingletonEntity<NetCodeTestPrefabCollection>(testWorld.ServerWorld);
                var prefabList = testWorld.ServerWorld.EntityManager.GetBuffer<NetCodeTestPrefab>(ghostCollection).ToNativeArray(Allocator.Temp);
                Assert.AreEqual(4, prefabList.Length);
                for (int i = 0; i < prefabList.Length; ++i)
                {
                    Assert.IsTrue(testWorld.ServerWorld.EntityManager.HasComponent<GhostGen_IntStruct>(prefabList[i].Value));
                    var linkedGroupBuffer = testWorld.ServerWorld.EntityManager.GetBuffer<LinkedEntityGroup>(prefabList[i].Value);
                    if ((prefabTypes[i] & GhostPrefabType.Server) != 0)
                        Assert.IsTrue(testWorld.ServerWorld.EntityManager.HasComponent<GhostGen_IntStruct>(linkedGroupBuffer[1].Value));
                    else
                        Assert.IsFalse(testWorld.ServerWorld.EntityManager.HasComponent<GhostGen_IntStruct>(linkedGroupBuffer[1].Value), "{0} should not have ChildComponent", names[i]);
                }
                //Client
                ghostCollection = testWorld.TryGetSingletonEntity<NetCodeTestPrefabCollection>(testWorld.ClientWorlds[0]);
                prefabList = testWorld.ClientWorlds[0].EntityManager.GetBuffer<NetCodeTestPrefab>(ghostCollection).ToNativeArray(Allocator.Temp);
                Assert.AreEqual(4, prefabList.Length);
                for (int i = 0; i < prefabList.Length; ++i)
                {
                    var linkedGroupBuffer = testWorld.ClientWorlds[0].EntityManager.GetBuffer<LinkedEntityGroup>(prefabList[i].Value);
                    Assert.IsTrue(testWorld.ClientWorlds[0].EntityManager.HasComponent<GhostGen_IntStruct>(prefabList[i].Value));
                    if ((prefabTypes[i] & GhostPrefabType.Client) != 0)
                        Assert.IsTrue(testWorld.ClientWorlds[0].EntityManager.HasComponent<GhostGen_IntStruct>(linkedGroupBuffer[1].Value));
                    else
                        Assert.IsFalse(testWorld.ClientWorlds[0].EntityManager.HasComponent<GhostGen_IntStruct>(linkedGroupBuffer[1].Value));
                }
            }
        }

        [Test]
        public void OverrideComponentPrefabType_NestedChildEntity()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                var names = new[] {"ServerOnly", "ClientOnly", "PredictedOnly", "InterpolatedOnly"};
                var prefabTypes = new[] {GhostPrefabType.Server, GhostPrefabType.Client, GhostPrefabType.InterpolatedClient, GhostPrefabType.PredictedClient};
                var collection = CreatePrefabs(names);
                // Only modify nested child behaviors
                for (int i = 0; i < prefabTypes.Length; ++i)
                {
                    var gameObject = collection[i];

                    var child = gameObject.transform.GetChild(0);
                    var nestedChild = child.GetChild(0);
                    nestedChild.gameObject.AddComponent<GhostAuthoringInspectionComponent>().ComponentOverrides = new []
                    {
                        new GhostAuthoringInspectionComponent.ComponentOverride
                        {
                            FullTypeName = typeof(GhostGen_IntStruct).FullName,
                            PrefabType = prefabTypes[i],
                            SendTypeOptimization = GhostSendType.AllClients,
                            VariantHash = 0
                        }
                    };
                }

                Assert.IsTrue(testWorld.CreateGhostCollection(collection));
                testWorld.CreateWorlds(true, 1);

                //Register serializers and setup all the system
                for(int i=0;i<16;++i)
                    testWorld.Tick(1.0f/60.0f);

                //Then check the expected results
                //Server
                var ghostCollection = testWorld.TryGetSingletonEntity<NetCodeTestPrefabCollection>(testWorld.ServerWorld);
                var prefabList = testWorld.ServerWorld.EntityManager.GetBuffer<NetCodeTestPrefab>(ghostCollection).ToNativeArray(Allocator.Temp);
                Assert.AreEqual(4, prefabList.Length);
                for (int i = 0; i < prefabList.Length; ++i)
                {
                    Assert.IsTrue(testWorld.ServerWorld.EntityManager.HasComponent<GhostGen_IntStruct>(prefabList[i].Value));
                    var linkedGroupBuffer = testWorld.ServerWorld.EntityManager.GetBuffer<LinkedEntityGroup>(prefabList[i].Value);
                    if ((prefabTypes[i] & GhostPrefabType.Server) != 0)
                        Assert.IsTrue(testWorld.ServerWorld.EntityManager.HasComponent<GhostGen_IntStruct>(linkedGroupBuffer[2].Value));
                    else
                        Assert.IsFalse(testWorld.ServerWorld.EntityManager.HasComponent<GhostGen_IntStruct>(linkedGroupBuffer[2].Value), "{0} should not have ChildComponent", names[i]);
                }
                //Client
                ghostCollection = testWorld.TryGetSingletonEntity<NetCodeTestPrefabCollection>(testWorld.ClientWorlds[0]);
                prefabList = testWorld.ClientWorlds[0].EntityManager.GetBuffer<NetCodeTestPrefab>(ghostCollection).ToNativeArray(Allocator.Temp);
                Assert.AreEqual(4, prefabList.Length);
                for (int i = 0; i < prefabList.Length; ++i)
                {
                    var linkedGroupBuffer = testWorld.ClientWorlds[0].EntityManager.GetBuffer<LinkedEntityGroup>(prefabList[i].Value);
                    Assert.IsTrue(testWorld.ClientWorlds[0].EntityManager.HasComponent<GhostGen_IntStruct>(prefabList[i].Value));
                    if ((prefabTypes[i] & GhostPrefabType.Client) != 0)
                        Assert.IsTrue(testWorld.ClientWorlds[0].EntityManager.HasComponent<GhostGen_IntStruct>(linkedGroupBuffer[2].Value));
                    else
                        Assert.IsFalse(testWorld.ClientWorlds[0].EntityManager.HasComponent<GhostGen_IntStruct>(linkedGroupBuffer[2].Value));
                }
            }
        }

        [Test]
        public void OverrideComponentSendType_RootEntity()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                var names = new[] {"All", "Interpolated", "Predicted", "None"};
                var sendTypes = new[] {GhostSendType.AllClients, GhostSendType.OnlyInterpolatedClients, GhostSendType.OnlyPredictedClients, (GhostSendType)0};
                var collection = CreatePrefabs(names);
                for (int i = 0; i < sendTypes.Length; ++i)
                {
                    var inspection = collection[i].AddComponent<GhostAuthoringInspectionComponent>();
                    inspection.ComponentOverrides = new []
                    {
                        new GhostAuthoringInspectionComponent.ComponentOverride
                        {
                            FullTypeName = typeof(GhostGen_IntStruct).FullName,
                            PrefabType = GhostPrefabType.All,
                            SendTypeOptimization = sendTypes[i],
                            VariantHash = 0,
                        }
                    };
                }

                Assert.IsTrue(testWorld.CreateGhostCollection(collection));
                testWorld.CreateWorlds(true, 1);

                //Register serializers and setup all the system
                for(int i=0;i<16;++i)
                    testWorld.Tick(1.0f/60.0f);

                //In order to get the collection setup I need to enter in game
                testWorld.Connect(1.0f / 60f);
                testWorld.GoInGame();

                for (int i = 0; i < collection.Length; ++i)
                    testWorld.SpawnOnServer(collection[i]);

                for(int i=0;i<16;++i)
                    testWorld.Tick(1.0f/60.0f);

                //Then check the expected results
                var collectionEntity = testWorld.TryGetSingletonEntity<GhostCollection>(testWorld.ServerWorld);
                var ghostCollection = testWorld.ServerWorld.EntityManager.GetBuffer<GhostCollectionComponentIndex>(collectionEntity);
                var ghostComponentCollection = testWorld.ServerWorld.EntityManager.GetBuffer<GhostCollectionComponentType>(collectionEntity);

                var type = TypeManager.GetTypeIndex(typeof(GhostGen_IntStruct));
                var index = 0;
                while (index < ghostCollection.Length && ghostComponentCollection[ghostCollection[index].ComponentIndex].Type.TypeIndex != type) ++index;
                var serializerIndex = ghostCollection[index].SerializerIndex;


                CheckCollection(testWorld.ServerWorld, serializerIndex, 0);
                CheckCollection(testWorld.ClientWorlds[0], serializerIndex, 0);
            }
        }

        [Test]
        public void OverrideComponentSendType_ChildEntity()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                var names = new[] {"All", "Interpolated", "Predicted", "None"};
                var sendTypes = new[] {GhostSendType.AllClients, GhostSendType.OnlyInterpolatedClients, GhostSendType.OnlyPredictedClients, (GhostSendType)0};
                var collection = CreatePrefabs(names);
                for (int i = 0; i < sendTypes.Length; ++i)
                {
                    var gameObject = collection[i];
                    var child = gameObject.transform.GetChild(0);
                    child.gameObject.AddComponent<GhostAuthoringInspectionComponent>().ComponentOverrides = new[]
                    {
                        new GhostAuthoringInspectionComponent.ComponentOverride
                        {
                            FullTypeName = typeof(GhostGen_IntStruct).FullName,
                            PrefabType = GhostPrefabType.All,
                            SendTypeOptimization = sendTypes[i],
                            VariantHash = 0
                        }
                    };
                }

                Assert.IsTrue(testWorld.CreateGhostCollection(collection));
                testWorld.CreateWorlds(true, 1);

                //Register serializers and setup all the system
                for(int i=0;i<16;++i)
                    testWorld.Tick(1.0f/60.0f);

                //In order to get the collection setup I need to enter in game
                testWorld.Connect(1.0f / 60f);
                testWorld.GoInGame();

                for (int i = 0; i < collection.Length; ++i)
                    testWorld.SpawnOnServer(collection[i]);

                for(int i=0;i<16;++i)
                    testWorld.Tick(1.0f/60.0f);

                //Then check the expected results
                var collectionEntity = testWorld.TryGetSingletonEntity<GhostCollection>(testWorld.ServerWorld);
                var ghostCollection = testWorld.ServerWorld.EntityManager.GetBuffer<GhostCollectionComponentIndex>(collectionEntity);
                var ghostComponentCollection = testWorld.ServerWorld.EntityManager.GetBuffer<GhostCollectionComponentType>(collectionEntity);

                var type = TypeManager.GetTypeIndex(typeof(GhostGen_IntStruct));
                var index = 0;
                while (index < ghostCollection.Length && ghostComponentCollection[ghostCollection[index].ComponentIndex].Type.TypeIndex != type)
                    ++index;
                var serializerIndex = ghostCollection[index].SerializerIndex;

                CheckCollection(testWorld.ServerWorld, serializerIndex, 1);
                CheckCollection(testWorld.ClientWorlds[0], serializerIndex, 1);
            }
        }

        [Test]
        public void OverrideComponentSendType_NestedChildEntity()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                var names = new[] {"All", "Interpolated", "Predicted", "None"};
                var sendTypes = new[] {GhostSendType.AllClients, GhostSendType.OnlyInterpolatedClients, GhostSendType.OnlyPredictedClients, (GhostSendType)0};
                var collection = CreatePrefabs(names);
                for (int i = 0; i < sendTypes.Length; ++i)
                {
                    var gameObject = collection[i];
                    var child = gameObject.transform.GetChild(0);
                    var nestedChild = child.GetChild(0);
                    nestedChild.gameObject.AddComponent<GhostAuthoringInspectionComponent>().ComponentOverrides = new []
                    {
                        new GhostAuthoringInspectionComponent.ComponentOverride
                        {
                            FullTypeName = typeof(GhostGen_IntStruct).FullName,
                            PrefabType = GhostPrefabType.All,
                            SendTypeOptimization = sendTypes[i],
                            VariantHash = 0
                        }
                    };
                }

                Assert.IsTrue(testWorld.CreateGhostCollection(collection));
                testWorld.CreateWorlds(true, 1);

                //Register serializers and setup all the system
                for(int i=0;i<16;++i)
                    testWorld.Tick(1.0f/60.0f);

                //In order to get the collection setup I need to enter in game
                testWorld.Connect(1.0f / 60f);
                testWorld.GoInGame();

                for (int i = 0; i < collection.Length; ++i)
                    testWorld.SpawnOnServer(collection[i]);

                for(int i=0;i<16;++i)
                    testWorld.Tick(1.0f/60.0f);

                //Then check the expected results
                var collectionEntity = testWorld.TryGetSingletonEntity<GhostCollection>(testWorld.ServerWorld);
                var ghostCollection = testWorld.ServerWorld.EntityManager.GetBuffer<GhostCollectionComponentIndex>(collectionEntity);
                var ghostComponentCollection = testWorld.ServerWorld.EntityManager.GetBuffer<GhostCollectionComponentType>(collectionEntity);

                var type = TypeManager.GetTypeIndex(typeof(GhostGen_IntStruct));
                var index = 0;
                while (index < ghostCollection.Length && ghostComponentCollection[ghostCollection[index].ComponentIndex].Type.TypeIndex != type)
                {
                    ++index;
                }
                var serializerIndex = ghostCollection[index].SerializerIndex;

                CheckCollection(testWorld.ServerWorld, serializerIndex, 2);
                CheckCollection(testWorld.ClientWorlds[0], serializerIndex, 2);
            }
        }

        /// <summary>A client only variant we can assign.</summary>
        [GhostComponentVariation(typeof(Transforms.LocalTransform), nameof(TransformVariantTest))]
        [GhostComponent(PrefabType=GhostPrefabType.All, SendTypeOptimization=GhostSendType.AllClients)]
        public struct TransformVariantTest
        {
            [GhostField(Quantization=100, Smoothing=SmoothingAction.InterpolateAndExtrapolate)]
            public float3 Position;

            [GhostField(Quantization=100, Smoothing=SmoothingAction.InterpolateAndExtrapolate)]
            public float Scale;

            [GhostField(Quantization=1000, Smoothing=SmoothingAction.InterpolateAndExtrapolate)]
            public quaternion Rotation;
        }

        [Test]
        public void SerializationVariant_AreAppliedToBothRootAndChildEntities()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1);
                var ghostGameObject = new GameObject("Root");
                var childGhost = new GameObject("Child");
                childGhost.transform.parent = ghostGameObject.transform;
                var nestedChildGhost = new GameObject("NestedChild");
                nestedChildGhost.transform.parent = childGhost.transform;
                var authoring = ghostGameObject.AddComponent<GhostAuthoringComponent>();
                var inspection = ghostGameObject.AddComponent<GhostAuthoringInspectionComponent>();
                authoring.DefaultGhostMode = GhostMode.Interpolated;
                authoring.SupportedGhostModes = GhostModeMask.All;

                //Setup a variant for both root and child entity and check that the runtime serializer use this one to serialize data
                var attrType = typeof(TransformVariantTest).GetCustomAttribute<GhostComponentVariationAttribute>();
                ulong hash = 0;

                using var collectionQuery = testWorld.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostComponentSerializerCollectionData>());
                var collectionData = collectionQuery.GetSingleton<GhostComponentSerializerCollectionData>();
                foreach (var ssIndex in collectionData.SerializationStrategiesComponentTypeMap.GetValuesForKey(attrType.ComponentType))
                {
                    var ss = collectionData.SerializationStrategies[ssIndex];
                    if (ss.DisplayName.ToString().Contains(nameof(TransformVariantTest)))
                    {
                        hash = ss.Hash;
                        goto found;
                    }
                }
                Assert.Fail($"Couldn't find {nameof(TransformVariantTest)} to apply it!");

                found:
                Assert.AreNotEqual(0, hash);
                inspection.ComponentOverrides = new[]
                {
                    new GhostAuthoringInspectionComponent.ComponentOverride
                    {
                        FullTypeName = typeof(Transforms.LocalTransform).FullName,
                        PrefabType = GhostPrefabType.All,
                        SendTypeOptimization = GhostSendType.AllClients,
                        VariantHash = hash
                    },
                };
                childGhost.AddComponent<NetcodeTransformUsageFlagsTestAuthoring>();
                childGhost.AddComponent<GhostAuthoringInspectionComponent>().ComponentOverrides = new[]
                {
                    new GhostAuthoringInspectionComponent.ComponentOverride
                    {
                        FullTypeName = typeof(Transforms.LocalTransform).FullName,
                        PrefabType = GhostPrefabType.All,
                        SendTypeOptimization = GhostSendType.AllClients,
                        VariantHash = hash
                    },
                };
                nestedChildGhost.AddComponent<NetcodeTransformUsageFlagsTestAuthoring>();
                nestedChildGhost.AddComponent<GhostAuthoringInspectionComponent>().ComponentOverrides = new[]
                {
                    new GhostAuthoringInspectionComponent.ComponentOverride
                    {
                        FullTypeName = typeof(Transforms.LocalTransform).FullName,
                        PrefabType = GhostPrefabType.All,
                        SendTypeOptimization = GhostSendType.AllClients,
                        VariantHash = hash
                    }
                };

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject), "Cannot create ghost collection");
                testWorld.BakeGhostCollection(testWorld.ServerWorld);
                testWorld.BakeGhostCollection(testWorld.ClientWorlds[0]);

                //Register serializers and setup all the system
                for(int i=0;i<16;++i)
                    testWorld.Tick(1.0f/60.0f);

                //In order to get the collection setup I need to enter in game
                testWorld.Connect(1.0f / 60f);
                testWorld.GoInGame();
                testWorld.SpawnOnServer(ghostGameObject);

                for(int i=0;i<16;++i)
                    testWorld.Tick(1.0f/60.0f);

                var typeIndex = TypeManager.GetTypeIndex<Transforms.LocalTransform>();

                //Then check the expected results
                var collection = testWorld.TryGetSingletonEntity<GhostCollection>(testWorld.ServerWorld);
                var ghostSerializerCollection = testWorld.ServerWorld.EntityManager.GetBuffer<GhostComponentSerializer.State>(collection);
                //Check that the variant has been registered
                bool variantIsPresent = false;
                foreach (var t in ghostSerializerCollection)
                    variantIsPresent |= t.VariantHash == hash;
                Assert.IsTrue(variantIsPresent);

                var componentIndex = testWorld.ServerWorld.EntityManager.GetBuffer<GhostCollectionComponentIndex>(collection);
                var ghostPrefabCollection = testWorld.ServerWorld.EntityManager.GetBuffer<GhostCollectionPrefabSerializer>(collection);
                //And verify that the component associated with the ghost for the transform point to this index
                for (int i = 0; i < ghostPrefabCollection[0].NumComponents;++i)
                {
                    var idx = componentIndex[ghostPrefabCollection[0].FirstComponent + i];
                    if (ghostSerializerCollection[idx.SerializerIndex].ComponentType.TypeIndex == typeIndex)
                    {
                        Assert.IsTrue(ghostSerializerCollection[idx.SerializerIndex].VariantHash == hash);
                    }
                }
            }
        }

        [Test]
        public void AddPrefabOverride_InRoot_ComputesGameObjectReference()
        {
            AddPrefabOverride_ComputesGameObjectReference((collection, i) => collection[i]);
        }

        [Test]
        public void AddPrefabOverride_InChild_ComputesGameObjectReference()
        {
            AddPrefabOverride_ComputesGameObjectReference((collection, i) => collection[i].transform.GetChild(0).gameObject);
        }

        [Test]
        public void AddPrefabOverride_InNestedChild_ComputesGameObjectReference()
        {
            AddPrefabOverride_ComputesGameObjectReference((collection, i) => collection[i].transform.GetChild(0).GetChild(0).gameObject);
        }

        private void AddPrefabOverride_ComputesGameObjectReference(Func<GameObject[], int, GameObject> testTransform)
        {
            using var testWorld = new NetCodeTestWorld();
            testWorld.Bootstrap(true);
            var names = new[] { "All", "Interpolated", "Predicted", "None" };
            var sendTypes = new[]
                { GhostSendType.AllClients, GhostSendType.OnlyInterpolatedClients, GhostSendType.OnlyPredictedClients, GhostSendType.DontSend };
            var collection = CreatePrefabs(names);
            for (int i = 0; i < sendTypes.Length; ++i)
            {
                var goFromFunc = testTransform(collection, i);
                const int exampleEntityIndex = 66;
                var inspection = goFromFunc.GetComponent<GhostAuthoringInspectionComponent>() ?? goFromFunc.AddComponent<GhostAuthoringInspectionComponent>();

                var entityGuid = new EntityGuid
                {
                    a = (ulong)goFromFunc.GetInstanceID(),
                    b = exampleEntityIndex,
                };
                var componentOverride = inspection.GetOrAddPrefabOverride(typeof(GhostGen_IntStruct), entityGuid, (GhostPrefabType) GhostAuthoringInspectionComponent.ComponentOverride.NoOverride);

                var ghostAuthoringComponent = collection[i].GetComponent<GhostAuthoringComponent>();
                Assert.IsNotNull(ghostAuthoringComponent);
                var allComponentOverrides = GhostAuthoringInspectionComponent.CollectAllComponentOverridesInInspectionComponents(ghostAuthoringComponent, false);
                var foundInspection = allComponentOverrides.First(x => x.Item1 == goFromFunc);
                Assert.AreEqual(foundInspection.Item1.GetInstanceID(), entityGuid.OriginatingId, $"entityGuid.OriginatingId '{entityGuid.OriginatingId}' did not match game object set '{goFromFunc}'");
                Assert.AreEqual(foundInspection.Item2.EntityIndex, exampleEntityIndex, "EntityIndex should have been set!");
            }
        }
    }
}
