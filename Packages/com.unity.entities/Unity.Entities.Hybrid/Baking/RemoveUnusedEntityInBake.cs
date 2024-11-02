namespace Unity.Entities
{
    /// <summary>
    /// The entity will be stripped out before it appears in the live game world.
    /// For example, entities that are not referenced at all.
    /// </summary>
    struct RemoveUnusedEntityInBake : IComponentData
    {
    }
}
