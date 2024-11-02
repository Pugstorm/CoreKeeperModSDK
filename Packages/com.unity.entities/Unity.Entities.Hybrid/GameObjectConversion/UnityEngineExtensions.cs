using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityObject = UnityEngine.Object;
using static Unity.Debug;

namespace Unity.Entities.Conversion
{
    static class UnityEngineExtensions
    {
        /// <summary>
        /// Returns an EntityGuid that can be used as a guid within a (non-persistent) session to refer to an entity generated
        /// from a UnityEngine.Object. The primary entity will be index 0, and additional entities will have increasing
        /// indices.
        /// </summary>
        public static EntityGuid ComputeEntityGuid(this UnityObject @this, uint namespaceId, int serial)
        {
            if (@this is Component component)
                @this = component.gameObject;
            return new EntityGuid(@this.GetInstanceID(), 0, namespaceId, (uint)serial);
        }

        public static bool IsPrefab(this GameObject @this) =>
            !@this.scene.IsValid();

        public static bool IsAsset(this UnityObject @this) =>
            !(@this is GameObject) && !(@this is Component);

        public static bool IsActiveIgnorePrefab(this GameObject @this)
        {
            if (!@this.IsPrefab())
                return @this.activeInHierarchy;

            var parent = @this.transform;
            while (parent != null)
            {
                if (!parent.gameObject.activeSelf)
                    return false;

                parent = parent.parent;
            }

            return true;
        }

        public static bool IsComponentDisabled(this Component @this)
        {
            switch (@this)
            {
                case Renderer  r: return !r.enabled;
                case Collider  c: return !c.enabled;
                case LODGroup  l: return !l.enabled;
                case Behaviour b: return !b.enabled;
            }

            return false;
        }

        public static bool GetComponentsBaking(this GameObject gameObject, List<Component> componentsCache)
        {
            int outputIndex = 0;
            gameObject.GetComponents(componentsCache);

            for (var i = 0; i != componentsCache.Count; i++)
            {
                var component = componentsCache[i];

                if (component == null)
                    LogWarning($"The referenced script is missing on {gameObject.name} (index {i} in components list)", gameObject);
                else
                {
                    componentsCache[outputIndex] = component;

                    outputIndex++;
                }
            }

            componentsCache.RemoveRange(outputIndex, componentsCache.Count - outputIndex);
            return true;
        }
    }
}
