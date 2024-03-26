using Unity.Entities;
using UnityEngine;

namespace Unity.NetCode.Hybrid
{
    /// <summary>
    /// If this component is added to a GameObject used as a GhostPresentationGameObjectPrefabReference
    /// it will be setup with references to the entity and world owning this GameObject
    /// instance.
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL(HelpURLs.GhostPresentationGameObjectEntityOwner)]
    public class GhostPresentationGameObjectEntityOwner : MonoBehaviour
    {
        /// <summary>
        /// The world in which the entity owning this GameObject exists.
        /// </summary>
        public World World {get; internal set;}
        /// <summary>
        /// The entity owning this GameObject.
        /// </summary>
        public Entity Entity {get; internal set;}
    }
}
