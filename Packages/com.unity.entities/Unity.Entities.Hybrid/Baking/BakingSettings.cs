using System;
using System.Collections.Generic;
using Unity.Entities.Conversion;
using Unity.Entities.Hybrid;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using Unity.Entities.Build;
using UnityEditor.AssetImporters;
#endif
using UnityObject = UnityEngine.Object;

namespace Unity.Entities
{
    internal class BakingSettings
    {
        public Hash128                  SceneGUID;
        public BakingUtility.BakingFlags          BakingFlags;
        public WorldSystemFilterFlags FilterFlags = WorldSystemFilterFlags.BakingSystem;
        public List<Type>               ExtraSystems = new List<Type>();
        public List<Type>               Systems;
#if UNITY_EDITOR
        public IEntitiesPlayerSettings DotsSettings;
        public AssetImportContext AssetImportContext;
        public GameObject PrefabRoot;

        public BakingSystemFilterSettings BakingSystemFilterSettings
        {
            get
            {
                // Build Config might exist because this is a build via build configs, so using the old filter.
                // This is to keep compatibility for DOTS Runtime + Netcode until they can fix things.

                if (DotsSettings != null)
                    return DotsSettings.GetFilterSettings();

                return null;
            }
        }
#endif
        internal byte                     NamespaceID; // this must be internal

        public BlobAssetStore BlobAssetStore { get; protected internal set; }

        public BakingSettings() {}

        public BakingSettings(BakingUtility.BakingFlags bakingFlags, BlobAssetStore blobAssetStore)
        {
            BakingFlags = bakingFlags;
            BlobAssetStore = blobAssetStore;
        }
    }
}
