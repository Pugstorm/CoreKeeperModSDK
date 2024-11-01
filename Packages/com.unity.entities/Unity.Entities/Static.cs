using System;
using Unity.Entities;

namespace Unity.Transforms
{
    /// <summary>
    /// This component is added during conversion by GameObjects that are marked with StaticOptimizeEntity.
    /// </summary>
    public struct Static : IComponentData
    {
    }
}
