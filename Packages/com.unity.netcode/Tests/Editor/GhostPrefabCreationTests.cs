using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.NetCode.LowLevel.Unsafe;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Profiling;

namespace Unity.NetCode.Tests
{
    public class GhostPrefabCreationTests
    {
        [Test]
        public void CreateGhostPrefabWithChildren()
        {
            using var testWorld = new NetCodeTestWorld();
            testWorld.Bootstrap(true);
            testWorld.CreateWorlds(true, 1);
            CreatePrefab(testWorld.ServerWorld.EntityManager);
            CreatePrefab(testWorld.ClientWorlds[0].EntityManager);
            testWorld.Connect(1f/60f, 4);
            testWorld.GoInGame();
            //register the prefab to the ghost collection system
            testWorld.Tick(1f/60f);
            var serverCollection = testWorld.TryGetSingletonEntity<GhostCollection>(testWorld.ServerWorld);
            var components = testWorld.ServerWorld.EntityManager.GetBuffer<GhostCollectionComponentType>(serverCollection);
            var types = testWorld.ServerWorld.EntityManager.GetBuffer<GhostCollectionPrefabSerializer>(serverCollection);
            var indices = testWorld.ServerWorld.EntityManager.GetBuffer<GhostCollectionComponentIndex>(serverCollection);
            var serializers = testWorld.ServerWorld.EntityManager.GetBuffer<GhostComponentSerializer.State>(serverCollection);
            var typeData = types[0];
            //all the component should be serialized on the root entity
            for (int i = 0; i < typeData.NumComponents - typeData.NumChildComponents; ++i)
            {
                Assert.AreNotEqual(GhostVariantsUtility.DontSerializeHash, serializers[indices[i + typeData.FirstComponent].SerializerIndex].VariantHash);
            }
            for (int i = typeData.NumComponents - typeData.NumChildComponents; i < typeData.NumComponents; ++i)
            {
                var compIdx = indices[i + typeData.FirstComponent].ComponentIndex;
                if (components[compIdx].Type == ComponentType.ReadWrite<EnableableComponent_0>() ||
                    components[compIdx].Type == ComponentType.ReadWrite<EnableableBuffer_0>())
                {
                    Assert.AreNotEqual(GhostVariantsUtility.DontSerializeHash, serializers[indices[i + typeData.FirstComponent].SerializerIndex].VariantHash);
                }
                else
                {
                    Assert.AreEqual(GhostVariantsUtility.DontSerializeHash, serializers[indices[i + typeData.FirstComponent].SerializerIndex].VariantHash);
                }
            }
        }

        private static Entity CreatePrefab(EntityManager entityManager)
        {
            //This create a ghost with 5 child entites, of which 3 in the same chunk, and other 2 in distinct chunks
            //for an an overall use of 4 archetypes per ghost.
            var prefab = entityManager.CreateEntity();
            entityManager.AddComponentData(prefab, new EnableableComponent_0());
            entityManager.AddComponentData(prefab, new EnableableComponent_1());
            entityManager.AddComponentData(prefab, new EnableableComponent_2());
            entityManager.AddComponentData(prefab, new EnableableComponent_3());
            entityManager.AddComponentData(prefab, LocalTransform.Identity);
            entityManager.AddComponent<GhostOwner>(prefab);
            entityManager.AddBuffer<EnableableBuffer_0>(prefab);
            entityManager.AddBuffer<EnableableBuffer_1>(prefab);
            entityManager.AddBuffer<EnableableBuffer_2>(prefab);
            var child = entityManager.CreateEntity();
            var linkedEntityGroups = entityManager.AddBuffer<LinkedEntityGroup>(prefab);
            linkedEntityGroups.Add(prefab);
            linkedEntityGroups.Add(child);
            entityManager.AddComponent<Prefab>(child);
            entityManager.AddComponentData(child, new EnableableComponent_0());
            entityManager.AddComponentData(child, new EnableableComponent_1());
            entityManager.AddBuffer<EnableableBuffer_0>(child);
            entityManager.AddBuffer<EnableableBuffer_1>(child);


            //Setup some variants to replicate EnableableComponent_0 and EnableableBuffer_0 using overrides.
            //Expect EnableableComponent_1 .. EnableableComponent_3 are not replicated, as well as the EnableableBuffer_1
            var overrides = SetupComponentOverrides();

            GhostPrefabCreation.ConvertToGhostPrefab(entityManager, prefab, new GhostPrefabCreation.Config
            {
                Name = "TestPrefab",
                Importance = 0,
                SupportedGhostModes = GhostModeMask.All,
                DefaultGhostMode = GhostMode.Interpolated,
                OptimizationMode = GhostOptimizationMode.Dynamic,
                UsePreSerialization = false
            },overrides);

            return prefab;
        }

        private static NativeParallelHashMap<GhostPrefabCreation.Component, GhostPrefabCreation.ComponentOverride> SetupComponentOverrides()
        {
            var overrides = new NativeParallelHashMap<GhostPrefabCreation.Component, GhostPrefabCreation.ComponentOverride>(16, Allocator.Temp);
            overrides.Add(
                new GhostPrefabCreation.Component
                {
                    ComponentType = ComponentType.ReadOnly<EnableableComponent_0>(),
                    ChildIndex = 1
                },
                new GhostPrefabCreation.ComponentOverride
                {
                    OverrideType = GhostPrefabCreation.ComponentOverrideType.Variant,
                    Variant = GhostVariantsUtility.CalculateVariantHashForComponent(ComponentType.ReadOnly<EnableableComponent_0>())
                });
            overrides.Add(
                new GhostPrefabCreation.Component
                {
                    ComponentType = ComponentType.ReadOnly<EnableableBuffer_0>(),
                    ChildIndex = 1
                },
                new GhostPrefabCreation.ComponentOverride
                {
                    OverrideType = GhostPrefabCreation.ComponentOverrideType.Variant,
                    Variant = GhostVariantsUtility.CalculateVariantHashForComponent(ComponentType.ReadOnly<EnableableBuffer_0>())
                });
            return overrides;
        }
    }
}
