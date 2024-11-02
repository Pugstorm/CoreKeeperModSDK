using Unity.Entities;
using UnityEngine;

namespace Unity.Scenes
{
    /// <summary>
    /// The group of systems responsible for loading and unloading scenes.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.Streaming,
        WorldSystemFilterFlags.Default | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.Streaming)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class SceneSystemGroup : ComponentSystemGroup
    {
    }
}
