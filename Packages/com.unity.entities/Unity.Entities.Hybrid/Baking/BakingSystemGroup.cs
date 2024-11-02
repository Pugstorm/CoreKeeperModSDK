namespace Unity.Entities
{
    /// <summary>
    /// The group of systems where baking systems run by default.
    /// </summary>
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial class BakingSystemGroup : ComponentSystemGroup { }
}
