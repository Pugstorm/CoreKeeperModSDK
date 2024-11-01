using Unity.Collections;

namespace Unity.Entities
{
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.EntitySceneOptimizations)]
    internal partial class RemoveEntityInBakeDestroyEntitiesSystem : SystemBase
    {
        EntityQuery _DestroyRemoveEntityInBake;
        protected override void OnCreate()
        {
            _DestroyRemoveEntityInBake = new EntityQueryBuilder(Allocator.Temp)
                .WithAny<RemoveUnusedEntityInBake, BakingOnlyEntity>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                .Build(this);
        }

        protected override void OnUpdate()
        {
            EntityManager.DestroyEntity(_DestroyRemoveEntityInBake);
        }
    }
}
