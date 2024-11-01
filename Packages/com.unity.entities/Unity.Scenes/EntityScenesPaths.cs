using System;
using System.Collections.Generic;
using Unity.Collections;
using System.Linq;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes
{
    internal class EntityScenesPaths
    {
        internal enum PathType
        {
            EntitiesUnityObjectReferences,
            EntitiesAssetDependencyGUIDs,
            EntitiesBinary,
            EntitiesHeader,
            EntitiesWeakAssetRefs,
            EntitiesGlobalUsage,
            EntitiesExportedTypes
        }

        internal static string GetExtension(PathType pathType)
        {
            switch (pathType)
            {
                // these must all be lowercase
                case PathType.EntitiesUnityObjectReferences: return "asset";
                case PathType.EntitiesBinary: return "entities";
                case PathType.EntitiesHeader: return "entityheader";
                case PathType.EntitiesAssetDependencyGUIDs: return "dependencies";
                case PathType.EntitiesWeakAssetRefs: return "weakassetrefs";
                case PathType.EntitiesGlobalUsage: return "globalusage";
                case PathType.EntitiesExportedTypes: return "exportedtypes";
            }

            throw new ArgumentException($"Unknown PathType {pathType}");
        }

        internal static Type SubSceneImporterType = null;
        internal const string k_SceneInfoFileName = "scene_info.bin";
        internal const string k_EntitySceneSubDir = "EntityScenes";

        internal static string FullPathForFile(string rootPath, string relPath)
        {
#if ENABLE_CONTENT_DELIVERY
            if(Entities.Content.ContentDeliveryGlobalState.PathRemapFunc == null)
                return $"{rootPath}/{relPath}";
            return Entities.Content.ContentDeliveryGlobalState.PathRemapFunc(relPath);
#else
            return $"{rootPath}/{relPath}";
#endif
        }

        internal static string RelativePathForSceneFile(Hash128 sceneGUID, PathType type, int sectionIndex)
        {
            return $"{k_EntitySceneSubDir}/{GetFileName(sceneGUID, type, sectionIndex)}";
        }

        internal static string RelativePathForSceneInfoFile => $"{k_EntitySceneSubDir}/{k_SceneInfoFileName}";

#if UNITY_EDITOR

        static Dictionary<Hash128, string> s_HashToString = new Dictionary<Hash128, string>();


        internal static Hash128 GetSubSceneArtifactHash(Hash128 sceneGUID, Hash128 buildConfigurationGUID, bool isBuildingForEditor, ImportMode importMode)
        {
            var guid = SceneWithBuildConfigurationGUIDs.EnsureExistsFor(sceneGUID, buildConfigurationGUID, isBuildingForEditor, out var mustRequestRefresh);
            if (mustRequestRefresh)
                UnityEditor.AssetDatabase.Refresh();

            if (!s_HashToString.TryGetValue(guid, out var guidString))
                guidString = s_HashToString[guid] = guid.ToString();
            return AssetDatabaseCompatibility.GetArtifactHash(guidString, SubSceneImporterType, importMode);
        }

        // This specialization is faster than calling GetLoadPathFromArtifactPaths with a null section index
        internal static string GetHeaderPathFromArtifactPaths(string[] paths)
        {
            int length = paths.Length;

            for (int i = 0; i < length; ++i)
            {
                var path = paths[i];
                if (path.EndsWith(".entityheader", StringComparison.Ordinal))
                {
                    return path;
                }
            }
            return null;
        }

        internal static string GetLoadPathFromArtifactPaths(string[] paths, PathType type)
        {
            var extension = GetExtension(type);
            return paths.FirstOrDefault(p => p.EndsWith(extension, StringComparison.Ordinal));
        }

        internal static string GetLoadPathFromArtifactPaths(string[] paths, PathType type, int sectionIndex)
        {
            var extension = $"{sectionIndex}.{GetExtension(type)}";
            return paths.FirstOrDefault(p => p.EndsWith(extension, StringComparison.Ordinal));
        }


#endif // UNITY_EDITOR
        internal static string GetFileName(Hash128 sceneGUID, PathType type, int sectionIndex)
        {
            var extension = GetExtension(type);
            switch (type)
            {
                case PathType.EntitiesBinary:
                    return $"{sceneGUID}.{sectionIndex}.{extension}";
                case PathType.EntitiesHeader:
                case PathType.EntitiesExportedTypes:
                    return $"{sceneGUID}.{extension}";
                case PathType.EntitiesUnityObjectReferences:
                    return $"{sceneGUID}.{sectionIndex}.{extension}";
                default:
                    throw new ArgumentException();
            }
        }

        static unsafe string MakeRandomFileName()
        {
            const int kFilenameLen = 16;
            var filenameBuffer = stackalloc char[kFilenameLen];
            var rng = new Mathematics.Random(((uint) Time.realtimeSinceStartup) + 1);

            for(int i = 0; i < kFilenameLen; ++i)
                filenameBuffer[i] = rng.NextBool() ? (char) rng.NextInt('a', 'z') : (char) rng.NextInt('0', '9');

            return new string(filenameBuffer, 0, kFilenameLen);
        }

        internal static int GetSectionIndexFromPath(string path)
        {
            var dot = new FixedString32Bytes(".");
            var localStr = new FixedString512Bytes(path);

            // Find the extension '.'
            var index = localStr.LastIndexOf(dot);
            if (index < 0) // no '.' characters so return default '0'
                return 0;

            // Found the extension dot, so null it and search for a section number '.' delimiter
            localStr[index] = 0;
            index = localStr.LastIndexOf(dot, index);
            if(index < 0)
                return 0;

            index++;
            int parsedInt = 0;
            localStr.Parse(ref index, ref parsedInt);

            return parsedInt;
        }
    }
}
