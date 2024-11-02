using System;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting.APIUpdating;

namespace Unity.Scenes
{
    /// <summary>
    /// Information for resources to be loaded at runtime.
    /// </summary>
    [MovedFrom(true, "Unity.Entities.Hybrid", "Unity.Entities.Hybrid")]
    public struct ResourceMetaData
    {
        /// <summary>
        /// Currently Scene types are supported, assetbundles will need to be supported when dependencies are implemented.
        /// </summary>
        public enum Type
        {
            /// <summary>
            /// The resource type is not supported.
            /// </summary>
            Unknown,
            /// <summary>
            /// The resource is a Scene.
            /// </summary>
            Scene,
        }

        /// <summary>
        /// The guid of the asset.
        /// </summary>
        public Hash128 ResourceId;

        /// <summary>
        /// The type of resource.
        /// </summary>
        public Type ResourceType;
    }

    /// <summary>
    /// Container for resource data.
    /// </summary>
    [MovedFrom(true, "Unity.Entities.Hybrid", "Unity.Entities.Hybrid")]
    public struct ResourceCatalogData
    {
        /// <summary>
        /// File format needs to change anytime the data layout for this class changes.
        /// </summary>
        public static readonly int CurrentFileFormatVersion = 1;
        /// <summary>
        /// The resource data.
        /// </summary>
        public BlobArray<ResourceMetaData> resources;

        /// <summary>
        /// Path information for resources.  This is separate to keep the resources data streamlined as using paths is slow.
        /// </summary>
        public BlobArray<BlobString> paths;

        /// <summary>
        /// Slow path to lookup guid from a path.  This first checks the passed in path then just the filename, then the lowercase version of the filename.
        /// </summary>
        /// <param name="path">The resource path.</param>
        /// <returns>The guid for the resource.</returns>
        public Hash128 GetGUIDFromPath(string path)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                var currentPath = paths[i].ToString();
                if (path == currentPath)
                    return resources[i].ResourceId;

                // TODO: All of these should die so we have a 100% consistent use of paths. Why do we need this at all?
                // TODO: DOTS-3330
                var currentPathWithoutExtension = GetFileNameWithoutExtension(currentPath);
                if (path == currentPathWithoutExtension)
                {
                    Debug.LogWarning("Deprecation Warning - Use of GetGUIDFromPath working without extensions is obsolete. (RemovedAfter 2021-02-05)");
                    return resources[i].ResourceId;
                }

                var currentPathWithoutExtensionLower = currentPathWithoutExtension.ToLower();
                var pathLower = path.ToLower();
                if (pathLower == currentPathWithoutExtensionLower)
                {
                    Debug.LogWarning("Deprecation Warning - Use of GetGUIDFromPath working with lower-case paths is obsolete. (RemovedAfter 2021-02-05)");
                    return resources[i].ResourceId;
                }
            }
            return default;
        }

        /// <summary>
        /// Gets the path of a resource from a GUID.
        /// </summary>
        /// <param name="guid">The GUID of the resource.</param>
        /// <returns>Returns the path of the resource.</returns>
        public string GetPathFromGUID(Hash128 guid)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                if (resources[i].ResourceId == guid)
                    return paths[i].ToString();
            }
            return default;
        }

        internal static string GetFileNameWithoutExtension(string path)
        {
            int i = path.Length - 1;
            for(; i > 0; --i)
            {
                if (path[i] == '.')
                    break;
            }

            if (i <= 0)
                return path;

            return path.Substring(0, i);
        }
    }
}
