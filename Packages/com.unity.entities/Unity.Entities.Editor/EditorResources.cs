using System;
using System.IO;
using UnityEditor;

namespace Unity.Entities.Editor
{
    static class EditorResources
    {
        /// <summary>
        /// Load a built-in editor resource.
        /// </summary>
        /// <typeparam name="T">The type of resource to load.</typeparam>
        /// <param name="path">The resource path. Must include the extension.</param>
        /// <param name="logError">Whether or not an error should be logged to the console.</param>
        /// <returns>The loaded resource if it was found, <see langword="null"/> otherwise.</returns>
        public static T Load<T>(string path, bool logError) where T : UnityEngine.Object
        {
            var resource = AssetDatabase.LoadAssetAtPath<T>(path);
            if (resource == null || !resource)
                resource = EditorGUIUtility.Load(path) as T;

            if (logError && (resource == null || !resource))
                UnityEngine.Debug.LogError($"Missing resource at {path}.");

            return resource;
        }

        /// <summary>
        /// Load a package built-in editor resource, with a fallback to built-in editor resource.
        /// </summary>
        /// <typeparam name="T">The type of resource to load.</typeparam>
        /// <param name="packageId">The package identifier.</param>
        /// <param name="path">The resource path. Must include the extension.</param>
        /// <param name="logError">Whether or not an error should be logged to the console.</param>
        /// <returns>The loaded resource if it was found, <see langword="null"/> otherwise.</returns>
        public static T Load<T>(string packageId, string path, bool logError) where T : UnityEngine.Object
        {
            var resource = Load<T>(GetPackagePath(packageId, path), false);
            return resource != null && resource ? resource : Load<T>(path, logError);
        }

        /// <summary>
        /// Load a built-in editor texture resource.
        /// </summary>
        /// <typeparam name="T">The type of texture resource to load.</typeparam>
        /// <param name="path">The texture resource path. Must include the extension.</param>
        /// <param name="logError">Whether or not an error should be logged to the console.</param>
        /// <returns>The loaded texture resource if it was found, <see langword="null"/> otherwise.</returns>
        public static T LoadTexture<T>(string path, bool logError) where T : UnityEngine.Texture
        {
            path = GetTexturePathWithFilePrefix(path);

            var resource = default(T);
            if (EditorGUIUtility.pixelsPerPoint > 1.0)
                resource = Load<T>(AppendHighDPISuffix(path), false);

            if (resource == null || !resource)
                resource = Load<T>(path, logError);

            return resource;
        }

        /// <summary>
        /// Load a package built-in editor texture resource, with a fallback to built-in editor resource.
        /// </summary>
        /// <typeparam name="T">The type of texture resource to load.</typeparam>
        /// <param name="packageId"></param>
        /// <param name="path">The texture resource path. Must include the extension.</param>
        /// <param name="logError">Whether or not an error should be logged to the console.</param>
        /// <returns>The loaded texture resource if it was found, <see langword="null"/> otherwise.</returns>
        public static T LoadTexture<T>(string packageId, string path, bool logError) where T : UnityEngine.Texture
        {
            path = GetTexturePathWithFolderPrefix(path);

            var resource = default(T);
            if (EditorGUIUtility.pixelsPerPoint > 1.0)
                resource = Load<T>(packageId, AppendHighDPISuffix(path), false);

            if (resource == null || !resource)
                resource = Load<T>(packageId, path, false);

            if (resource == null || !resource)
                resource = Load<T>(path, logError);

            return resource;
        }

        static string GetPackagePath(string packageId, string path)
        {
            return Path.Combine("Packages", packageId, "Editor Default Resources", path).ToForwardSlash();
        }

        static string GetTexturePathWithFolderPrefix(string path)
        {
            return Path.Combine("icons", EditorGUIUtility.isProSkin ? "dark" : "light", path).ToForwardSlash();
        }

        static string GetTexturePathWithFilePrefix(string path)
        {
            const string darkSkinPrefix = "d_";
            var name = Path.GetFileNameWithoutExtension(path);
            return EditorGUIUtility.isProSkin && !name.StartsWith(darkSkinPrefix, StringComparison.Ordinal) ?
                path.ReplaceLastOccurrence(name, darkSkinPrefix + name) : path;
        }

        static string AppendHighDPISuffix(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            return path.ReplaceLastOccurrence(name, name + "@2x");
        }
    }
}
