using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Scripting.APIUpdating;

namespace Unity.Scenes
{
    /// <summary>
    /// Component that represents a Scene or a Scene Section bounding volume
    /// </summary>
    [MovedFrom(true, "Unity.Entities", "Unity.Entities.Hybrid")]
    public struct SceneBoundingVolume : IComponentData
    {
        /// <summary>
        /// Bounding volume
        /// </summary>
        public MinMaxAABB Value;
    }
}
