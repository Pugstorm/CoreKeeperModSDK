using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Unity.NetCode
{
    /// <summary>
    /// Self contained component to hold a mesh's bounds for debug drawing.
    /// </summary>
    /// <remarks>
    /// This should stay active even when the GameObject is inactive. This is really showing boxes for the netcode of your GameObject, which is linked to the entity lifecycle
    /// If the entity is still moving and your GO is inactive, you'd potentially still want to know about it.
    /// </remarks>
    public struct GhostDebugMeshBounds : IComponentData
    {
        static List<Renderer> s_AllRenderers = new();
        /// <summary>
        /// The bounds for this entity, used to draw a debug box. Should be in local space, with the center at the object's origin.
        /// </summary>
        public Bounds GlobalBounds;

        /// <summary>
        /// Convenience method to initialize the debug mesh bounds for GameObjects.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="entity"></param>
        /// <param name="world"></param>
        /// <returns></returns>
        public GhostDebugMeshBounds Initialize(GameObject gameObject, Entity entity, World world)
        {
            gameObject.GetComponentsInChildren<Renderer>(includeInactive: true, results: s_AllRenderers);
            world.EntityManager.AddComponent<LocalToWorld>(entity); // required for rendering a little cross for debug drawer
            if (s_AllRenderers.Count != 0)
            {
                GlobalBounds = s_AllRenderers[0].localBounds;
                GlobalBounds.center = gameObject.transform.InverseTransformPoint(s_AllRenderers[0].bounds.center); // with localBounds, center is zero, so we need to adjust
                for (int i = 1; i < s_AllRenderers.Count; i++)
                {
                    var currentBounds = s_AllRenderers[i].localBounds;
                    currentBounds.center = gameObject.transform.InverseTransformPoint(s_AllRenderers[i].bounds.center); // with localBounds, center is zero, so we need to adjust
                    GlobalBounds.Encapsulate(currentBounds);
                }
            }

            return this;
        }
    }
}
