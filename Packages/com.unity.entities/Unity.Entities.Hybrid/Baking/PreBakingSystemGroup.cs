namespace Unity.Entities
{
    /// <summary>
    /// The group of systems that runs before the bakers execute.
    /// </summary>
    /// <remarks>
    /// A typical use of this group is to perform clean up
    /// before the bakers and the baking systems execute.
    /// </remarks>
    ///
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial class PreBakingSystemGroup : ComponentSystemGroup { }
}
