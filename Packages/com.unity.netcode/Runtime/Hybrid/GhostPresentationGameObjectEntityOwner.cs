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

        /// <summary>
        /// Convenience method to initialize the debug mesh bounds.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="world"></param>
        public void Initialize(Entity entity, World world)
        {
            Entity = entity;
            World = world;
#if UNITY_EDITOR
            var ghostBounds = new GhostDebugMeshBounds().Initialize(gameObject, entity, world);
            world.EntityManager.AddComponentData(entity, ghostBounds);
#endif
        }
    }
}
