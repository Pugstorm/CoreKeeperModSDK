namespace Unity.Entities
{
    /// <summary>
    /// The group of systems that runs just after the bakers but before <see cref="BakingSystemGroup"/>.
    /// </summary>
    /// <remarks>
    /// The transform components are added during the execution of this group.
    /// </remarks>
    ///
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial class TransformBakingSystemGroup : ComponentSystemGroup { }
}
