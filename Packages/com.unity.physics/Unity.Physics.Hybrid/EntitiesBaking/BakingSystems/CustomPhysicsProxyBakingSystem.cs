using Unity.Entities;
using Unity.Transforms;

namespace Unity.Physics.Authoring
{
    /// <summary>
    ///     Custom physics proxy baking system
    /// </summary>
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial class CustomPhysicsProxyBakingSystem : SystemBase
    {
        protected override void OnUpdate()
        {

            var transformFromEntity = GetComponentLookup<LocalTransform>();

            var physicsMassFromEntity = GetComponentLookup<PhysicsMass>();
            var physicsColliderFromEntity = GetComponentLookup<PhysicsCollider>();
            foreach (var (driver, entity) in SystemAPI.Query<RefRW<CustomPhysicsProxyDriver>>().WithEntityAccess()
                         .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
            {

                transformFromEntity[entity] = transformFromEntity[driver.ValueRW.rootEntity];

                physicsMassFromEntity[entity] = PhysicsMass.CreateKinematic(physicsColliderFromEntity[driver.ValueRW.rootEntity].MassProperties);
                physicsColliderFromEntity[entity] = physicsColliderFromEntity[driver.ValueRW.rootEntity];
            }
        }
    }
}
