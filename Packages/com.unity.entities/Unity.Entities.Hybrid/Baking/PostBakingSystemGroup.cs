namespace Unity.Entities
{
    /// <summary>
    /// The group of systems that runs after <see cref="BakingSystemGroup"/>.
    /// </summary>
    /// <remarks>
    /// This group runs after the companion components and after <see cref="LinkedEntityGroup"/> have been resolved.
    /// You would normally place a system in this group if you want access to companion components or <see cref="LinkedEntityGroup"/> and
    /// want to do some post-proccessing on those.
    /// </remarks>
    ///
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial class PostBakingSystemGroup : ComponentSystemGroup { }
}
