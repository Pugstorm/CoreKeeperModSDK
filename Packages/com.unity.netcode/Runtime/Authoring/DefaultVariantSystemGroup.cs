using Unity.Entities;

namespace Unity.NetCode
{
    /// <summary>
    /// Group that contains all the systems responsible for registering/setting up the default Ghost Variants (see <see cref="GhostComponentVariationAttribute"/>).
    /// The system group OnCreate method finalizes the default mapping inside its own `OnCreate` method, by collecting from all the registered
    /// <see cref="DefaultVariantSystemBase"/> systems the set of variant to use.
    /// The order in which variants are set in the map is governed by the creation order (see <see cref="CreateAfterAttribute"/>, <see cref="CreateBeforeAttribute"/>).
    /// <remarks>
    /// The group is present in both baking and client/server worlds.
    /// </remarks>
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.BakingSystem)]
    public partial class DefaultVariantSystemGroup : ComponentSystemGroup
    {
        protected override void OnCreate()
        {
            base.OnCreate();

            // This may look out of place here, but this SystemGroup is used as a "marker",
            // indicating that all "Serializer Registration" and "Default Variant Registration" systems have completed.
            // It needed to be a SystemGroup, and "DefaultVariants" are registered at the same time as serializers.
            var data = SystemAPI.GetSingletonRW<GhostComponentSerializerCollectionData>().ValueRW;
            data.CollectionFinalized.Value = 1;
        }
    }
}
