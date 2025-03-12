using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Physics.Authoring
{
    /// <summary>
    /// Marks a primary entity as a static root when building the compound colliders.
    /// </summary>
    [TemporaryBakingType]
    struct StaticOptimizePhysicsBaking : IComponentData {}

    /// <summary>
    /// Component added on additional entities in bakers to mark the static root found during the baking of a collider.
    /// </summary>
    /// <remarks>
    /// Multiple bakers may find the same static root body. The system <see cref="StaticOptimizeBakingSystem"/>
    /// adds the component <see cref="StaticOptimizePhysicsBaking"/> to the static root primary entity.
    /// </remarks>
    [BakingType]
    struct BakeStaticRoot : IComponentData
    {
        public Entity Body;
        public int ConvertedBodyInstanceID;
    }

    [BurstCompile]
    [UpdateBefore(typeof(BuildCompoundCollidersBakingSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    partial struct StaticOptimizeBakingSystem : ISystem
    {
        EntityQuery _ChangedBakeStaticRootQuery;
        EntityQuery _PreviousBakeStaticRootQuery;
        ComponentTypeSet _RootComponents;
        NativeHashSet<Entity> _StaticRootState; // Holds the set of static roots baked in a previous iteration.

        [BurstCompile]
        public void OnCreate(ref SystemState systemState)
        {
            _StaticRootState = new NativeHashSet<Entity>(10, Allocator.Persistent);

            _PreviousBakeStaticRootQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<BakeStaticRoot>()
                .WithNone<BakedEntity>()
                .Build(ref systemState);

            _ChangedBakeStaticRootQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<BakeStaticRoot, BakedEntity>()
                .Build(ref systemState);

            _RootComponents = new ComponentTypeSet(
                ComponentType.ReadWrite<StaticOptimizePhysicsBaking>(),
                ComponentType.ReadWrite<PhysicsWorldIndex>(),
                ComponentType.ReadWrite<PhysicsCompoundData>(),
                ComponentType.ReadWrite<PhysicsCollider>());
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState systemState)
        {
            _StaticRootState.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState systemState)
        {
            var previousStaticRoots = _PreviousBakeStaticRootQuery.ToComponentDataArray<BakeStaticRoot>(Allocator.Temp);
            var changedStaticRoots = _ChangedBakeStaticRootQuery.ToComponentDataArray<BakeStaticRoot>(Allocator.Temp);

            var capacity = math.max(previousStaticRoots.Length, changedStaticRoots.Length);
            var uniqueRoots = new NativeHashMap<Entity, BakeStaticRoot>(capacity, Allocator.Temp);

            // clear the root components from roots that are no longer needed
            GetUniqueRoots(previousStaticRoots, ref uniqueRoots);
            var oldState = _StaticRootState.ToNativeArray(Allocator.Temp);
            for (int i = 0, count = oldState.Length; i < count; ++i)
            {
                var r = oldState[i];
                if (!uniqueRoots.ContainsKey(r))
                {
                    systemState.EntityManager.RemoveComponent(r, _RootComponents);
                    _StaticRootState.Remove(r);
                }
            }

            // add the root components on the new static roots
            uniqueRoots.Clear();
            GetUniqueRoots(changedStaticRoots, ref uniqueRoots);
            foreach (var kv in uniqueRoots)
            {
                var rootEntity = kv.Value.Body;
                _StaticRootState.Add(rootEntity);
                systemState.EntityManager.AddComponent(rootEntity, _RootComponents);

                systemState.EntityManager.SetSharedComponent(rootEntity, new PhysicsWorldIndex());

                systemState.EntityManager.SetComponentData(rootEntity, new PhysicsCompoundData()
                {
                    AssociateBlobToBody = false,
                    ConvertedBodyInstanceID = kv.Value.ConvertedBodyInstanceID,
                    Hash = default,
                });
            }
        }

        [BurstCompile]
        static void GetUniqueRoots(in NativeArray<BakeStaticRoot> rootMarkers, ref NativeHashMap<Entity, BakeStaticRoot> bodyRoots)
        {
            for (int i = 0, count = rootMarkers.Length; i < count; ++i)
            {
                var bakedStaticRoot = rootMarkers[i];
                var rootEntity = bakedStaticRoot.Body;
                if (bodyRoots.ContainsKey(rootEntity))
                    continue;

                bodyRoots.Add(rootEntity, bakedStaticRoot);
            }
        }
    }
}
