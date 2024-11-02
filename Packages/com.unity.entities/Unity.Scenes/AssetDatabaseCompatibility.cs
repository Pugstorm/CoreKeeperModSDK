#if UNITY_EDITOR
using Unity.Collections;
using System;
using UnityEditor;
using UnityEditor.Experimental;
using Hash128 = UnityEngine.Hash128;
using System.Runtime.CompilerServices;

namespace Unity.Scenes
{
    internal enum ImportMode
    {
        Synchronous,
        Asynchronous,
        NoImport
    }

    internal static class AssetDatabaseCompatibility
    {
        internal static bool IsAssetImportWorkerProcess() => AssetDatabase.IsAssetImportWorkerProcess();
        internal static void UnregisterCustomDependencyPrefixFilter(string prefixFilter) => AssetDatabase.UnregisterCustomDependencyPrefixFilter(prefixFilter);
        internal static void RegisterCustomDependency(string dependency, Hash128 hashOfValue) => AssetDatabase.RegisterCustomDependency(dependency, hashOfValue);

        internal static string GuidToPath(GUID guid)
        {
            return AssetDatabase.GUIDToAssetPath(guid);
        }
        public static GUID PathToGUID(string assetPath)
        {
            return AssetDatabase.GUIDFromAssetPath(assetPath);
        }

        public static Hash128 GetAssetDependencyHash(GUID guid)
        {
            return AssetDatabase.GetAssetDependencyHash(guid);
        }

        internal static Hash128 GetArtifactHash(GUID guid, Type importerType, ImportMode mode)
        {
            switch (mode)
            {
                case ImportMode.Asynchronous:
                    return AssetDatabaseExperimental.ProduceArtifactAsync(new ArtifactKey(guid, importerType)).value;
                case ImportMode.Synchronous:
                    return AssetDatabaseExperimental.ProduceArtifact(new ArtifactKey(guid, importerType)).value;
                case ImportMode.NoImport:
                    return AssetDatabaseExperimental.LookupArtifact(new ArtifactKey(guid, importerType)).value;
            }

            return default;
        }

        internal static Hash128 GetArtifactHash(string guid, Type importerType, ImportMode mode)
        {
            return GetArtifactHash(new GUID(guid), importerType, mode);
        }


        internal static Hash128 ProduceArtifact(ArtifactKey artifactKey)
        {
            return AssetDatabaseExperimental.ProduceArtifact(artifactKey).value;
        }

        internal static bool GetArtifactPaths(Hash128 artifactHash, out string[] paths)
        {
            return AssetDatabaseExperimental.GetArtifactPaths(new ArtifactID
            {
                value = artifactHash
            }, out paths);
        }

        internal static void ProduceArtifactsAsync(NativeArray<GUID> guids, Type assetImportType, NativeList<Hash128> artifacts)
        {
            artifacts.ResizeUninitialized(guids.Length);

            //@TODO: use batch API when it lands in trunk
            var res = AssetDatabaseExperimental.ProduceArtifactsAsync(guids.ToArray(), assetImportType);
            for (int i = 0; i != guids.Length; i++)
                artifacts[i] = res[i].value;
        }
        internal static void ProduceArtifactsAsync(NativeArray<GUID> guids, Type assetImportType)
        {
            //@TODO: use batch API when it lands in trunk
            AssetDatabaseExperimental.ProduceArtifactsAsync(guids.ToArray(), assetImportType);
        }

        internal static void ProduceArtifacts(NativeArray<GUID> guids, Type assetImportType, NativeArray<Hash128> artifacts)
        {
            //@TODO: use batch API when it lands in trunk
            for (int i = 0; i != guids.Length; i++)
                artifacts[i] = AssetDatabaseExperimental.ProduceArtifact(new ArtifactKey(guids[i], assetImportType))
                    .value;
        }

        static void ProduceArtifacts(ArtifactKey[] artifactKeys, Hash128[] artifacts)
        {
            //@TODO: use batch API when it lands in trunk
            for (int i = 0; i != artifactKeys.Length; i++)
                artifacts[i] = AssetDatabaseExperimental.ProduceArtifact(artifactKeys[i]).value;
        }

        internal static bool ProduceArtifactsRefreshIfNecessary(ArtifactKey[] artifactKeys, Hash128[] artifacts)
        {
            ProduceArtifacts(artifactKeys, artifacts);
            bool hasFailedArtifacts = false;
            foreach (var artifact in artifacts)
            {
                if (!artifact.isValid)
                    hasFailedArtifacts = true;
            }

            if (hasFailedArtifacts)
            {
                // ProduceArtifact can fail if the assets have changed while importing or since last refresh.
                // Try at least once to get into a correct state.
                AssetDatabase.Refresh();

                ProduceArtifacts(artifactKeys, artifacts);
            }

            foreach (var artifact in artifacts)
            {
                if (!artifact.isValid)
                    return false;
            }

            return true;
        }


        internal static bool ProduceArtifactsRefreshIfNecessary(NativeArray<GUID> guids, Type assetImportType, NativeArray<Hash128> artifacts)
        {
            ProduceArtifacts(guids, assetImportType, artifacts);

            bool hasFailedArtifacts = false;
            foreach (var artifact in artifacts)
            {
                if (!artifact.isValid)
                    hasFailedArtifacts = true;
            }

            if (hasFailedArtifacts)
            {
                // ProduceArtifact can fail if the assets have changed while importing or since last refresh.
                // Try at least once to get into a correct state.
                AssetDatabase.Refresh();

                ProduceArtifacts(guids, assetImportType, artifacts);
            }

            foreach (var artifact in artifacts)
            {
                if (!artifact.isValid)
                    return false;
            }

            return true;
        }

        internal static bool ProduceArtifactsRefreshIfNecessary(NativeArray<GUID> guids, Type assetImportType, NativeList<Hash128> artifacts)
        {
            artifacts.ResizeUninitialized(guids.Length);
            return ProduceArtifactsRefreshIfNecessary(guids, assetImportType, artifacts.AsArray());
        }

        internal static void LookupArtifacts(NativeArray<GUID> guids, Type assetImportType, NativeList<Hash128> artifacts)
        {
            artifacts.ResizeUninitialized(guids.Length);
            //@TODO: use batch API when it lands in trunk
            for (int i = 0; i != guids.Length; i++)
                artifacts[i] = AssetDatabaseExperimental.LookupArtifact(new ArtifactKey(guids[i], assetImportType)).value;
        }


        internal static bool AssetExists(GUID guid)
        {
            return AssetDatabase.GetAssetDependencyHash(guid) != default;
        }

        internal static ulong GetArtifactDependencyVersion()
        {
            return AssetDatabase.GlobalArtifactDependencyVersion;
        }

        internal static ulong GetArtifactProcessedVersion()
        {
            return AssetDatabase.GlobalArtifactProcessedVersion;
        }
    }
}
#endif
