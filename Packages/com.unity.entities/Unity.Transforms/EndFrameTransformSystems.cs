using System;
using Unity.Entities;

namespace Unity.Transforms
{
    /// <summary>
    /// A system group containing systems that process entity transformation data.
    /// </summary>
    /// <remarks>
    /// This group includes systems that update any entity transformation hierarchies, compute an up-to-date <see cref="LocalToWorld"/> matrix.
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    public partial class TransformSystemGroup : ComponentSystemGroup
    {
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(TransformSystemGroup))]
    public partial struct ParentSystem : ISystem
    {
    }
}
