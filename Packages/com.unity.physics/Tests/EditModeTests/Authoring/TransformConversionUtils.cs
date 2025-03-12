using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Unity.Physics.Tests.Authoring
{
    internal static class TransformConversionUtils
    {
        public static readonly RigidTransform k_SharedDataChildTransformation =
            new RigidTransform(quaternion.EulerZXY(math.PI / 4), new float3(1f, 2f, 3f));

        public static void ConvertHierarchyAndUpdateTransformSystemsVerifyEntityExists(GameObject gameObjectHierarchyRoot, EntityQueryDesc query)
        {
            ConvertHierarchyAndUpdateTransformSystems<LocalToWorld>(gameObjectHierarchyRoot, query, false);
        }

        public static T ConvertHierarchyAndUpdateTransformSystems<T>(GameObject gameObjectHierarchyRoot)
            where T : unmanaged, IComponentData
        {
            // query with read/write to trigger update of transform system
            var query = new EntityQueryDesc { All = new[] { typeof(PhysicsCollider), ComponentType.ReadWrite<T>() } };
            return ConvertHierarchyAndUpdateTransformSystems<T>(gameObjectHierarchyRoot, query, true);
        }

        static T ConvertHierarchyAndUpdateTransformSystems<T>(
            GameObject gameObjectHierarchyRoot, EntityQueryDesc query, bool returnData
        )
            where T : unmanaged, IComponentData
        {
            if (
                returnData // i.e. value of post-conversion data will be asserted
                && !query.All.Contains(ComponentType.ReadWrite<T>())
                && !query.Any.Contains(ComponentType.ReadWrite<T>())
            )
                Assert.Fail($"{nameof(query)} must contain {ComponentType.ReadWrite<T>()} in order to trigger update of transform system");

            var queryStr = query.ToReadableString();

            using (var world = new World("Test world"))
            using (var blobAssetStore = new BlobAssetStore(128))
            {
                // convert GameObject hierarchy
                BaseHierarchyConversionTest.ConvertBakeGameObject(gameObjectHierarchyRoot, world, blobAssetStore);

                // trigger update of transform systems
                using (var group = world.EntityManager.CreateEntityQuery(query))
                {
                    using (var entities = group.ToEntityArray(Allocator.TempJob))
                    {
                        Assert.That(
                            entities.Length, Is.EqualTo(1),
                            $"Conversion systems produced unexpected number of physics entities {queryStr}"
                        );
                    }

                    var localToWorldSystem = world.GetOrCreateSystem<LocalToWorldSystem>();
                    ref var localToWorldSystemState = ref world.Unmanaged.ResolveSystemStateRef(localToWorldSystem);
                    var lastVersion = localToWorldSystemState.GlobalSystemVersion;

                    world.GetOrCreateSystem<ParentSystem>().Update(world.Unmanaged);
                    localToWorldSystem.Update(world.Unmanaged);

                    world.EntityManager.CompleteAllTrackedJobs();

                    using (var entities = group.ToEntityArray(Allocator.TempJob))
                    {
                        Assume.That(
                            entities.Length, Is.EqualTo(1),
                            $"Updating transform systems resulted in unexpected number of physics entities {queryStr}"
                        );
                    }

                    if (!returnData)
                        return default;

                    using (var chunks = group.ToArchetypeChunkArray(Allocator.TempJob))
                    {
                        var localToWorldTypeHandle = localToWorldSystemState.GetComponentTypeHandle<LocalToWorld>();
                        // assume transform systems ran if LocalToWorld chunk version has increased
                        var changed = chunks[0].DidChange(
                            ref localToWorldTypeHandle, lastVersion
                        );
                        Assume.That(
                            changed, Is.True,
                            $"Transform systems did not run. Is {typeof(T)} an input for any transform system?"
                        );
                    }

                    using (var data = group.ToComponentDataArray<T>(Allocator.TempJob))
                        return data[0];
                }
            }
        }
    }
}
