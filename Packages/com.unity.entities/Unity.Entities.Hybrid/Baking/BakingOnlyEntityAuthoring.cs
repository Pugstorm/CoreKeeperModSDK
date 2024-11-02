using UnityEngine;

namespace Unity.Entities.Hybrid.Baking
{
    /// <summary>
    /// Add a BakingOnlyEntity authoring component to your game object to mark it as Bake Only. It and all children will be
    /// stripped out before they appear in the live game world. Its additional entities and its children additional entities
    /// are exempt from this and will appear in the live game world.
    /// </summary>
    [DisallowMultipleComponent]
    public class BakingOnlyEntityAuthoring : MonoBehaviour { }
}
