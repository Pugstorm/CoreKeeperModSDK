using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Unity.Physics.Authoring
{
    internal static class ColliderExtensions
    {
        public static GameObject FindFirstEnabledAncestor<T>(GameObject shape, List<T> buffer) where T : Component
        {
            // include inactive in case the supplied shape GameObject is a prefab that has not been instantiated
            shape.GetComponentsInParent(true, buffer);
            GameObject result = null;
            for (int i = 0, count = buffer.Count; i < count; ++i)
            {
                if (
                    (buffer[i] as UnityEngine.Collider)?.enabled ??
                    (buffer[i] as MonoBehaviour)?.enabled ?? true)
                {
                    result = buffer[i].gameObject;
                    break;
                }
            }
            buffer.Clear();
            return result;
        }

        public static bool FindTopmostStaticEnabledAncestor(GameObject gameObject, out GameObject topStatic)
        {
            topStatic = null;
            if (gameObject == null)
                return false;

            // get the list of ancestors and set a dependency on the entire ancestor hierarchy
            var parents = new List<GameObject>();
            GetParents(gameObject, parents);

            for (int i = parents.Count - 1; i >= 0; --i)
            {
                // find the top most static parent and take a dependency on it and all its non-static ancestors
                if (IsStatic(parents[i]))
                {
                    topStatic = parents[i];
                    break;
                }
            }

            return topStatic != null;
        }

        static bool IsStatic(GameObject go)
        {
            return go != null && (go.isStatic || go.TryGetComponent(out StaticOptimizeEntity _));
        }

        static void GetParents(GameObject go, List<GameObject> parents)
        {
            parents.Clear();
            Transform parentTransform = go.transform.parent;
            while (parentTransform != null)
            {
                parents.Add(parentTransform.gameObject);
                parentTransform = parentTransform.parent;
            }
        }

        // TODO: revisit readable requirement when conversion is editor-only
        public static bool IsValidForConversion(this UnityEngine.Mesh mesh, GameObject host)
        {
#if UNITY_EDITOR
            // anything in a sub-scene is fine because it is converted at edit time, but run-time ConvertToEntity will fail
            if (
                host.gameObject.scene.isSubScene
                // isSubScene is false in AssetImportWorker during sub-scene import
                || UnityEditor.AssetDatabase.IsAssetImportWorkerProcess())
                return true;
#endif
            return mesh.isReadable;
        }
    }
}
