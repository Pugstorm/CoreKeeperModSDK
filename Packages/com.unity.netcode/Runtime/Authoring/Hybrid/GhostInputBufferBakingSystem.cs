using Unity.Entities;

namespace Unity.NetCode
{
    // Needs to run before GhostAuthoringBakingSystem so the buffer is there before ghost processing, putting it in the normal baking group ensures that since GhostAuthoringBakingSystem is in PostBakingSystemGroup
    [UpdateInGroup(typeof(BakingSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [BakingVersion("cmarastoni", 1)]
    internal partial class GhostInputBufferBakingSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            //ATTENTION! This singleton entity is always destroyed in the first non-incremental pass, because in the first import
            //the baking system clean all the Entities in the world when you open a sub-scene.
            //We recreate the entity here "lazily", so everything behave as expected.
            if (!SystemAPI.TryGetSingleton<GhostComponentSerializerCollectionData>(out var serializerCollectionData))
            {
                var systemGroup = World.GetExistingSystemManaged<GhostComponentSerializerCollectionSystemGroup>();
                EntityManager.CreateSingleton(systemGroup.ghostComponentSerializerCollectionDataCache);
                serializerCollectionData = systemGroup.ghostComponentSerializerCollectionDataCache;
            }
            foreach (var input in serializerCollectionData.InputComponentBufferMap)
            {
                var addBufferQuery = GetEntityQuery(
                    new EntityQueryDesc
                    {
                        All = new[]
                        {
                            input.Key
                        },
                        None = new []
                        {
                            input.Value
                        },
                        Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab
                    });
                EntityManager.AddComponent(addBufferQuery, input.Value);
            }
        }
    }
}
