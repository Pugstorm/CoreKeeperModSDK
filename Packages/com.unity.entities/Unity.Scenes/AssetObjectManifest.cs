using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Hash128 = Unity.Entities.Hash128;
using Unity.Entities.Serialization;

namespace Unity.Scenes
{
    internal class AssetObjectManifest : ScriptableObject
    {
        public RuntimeGlobalObjectId[] GlobalObjectIds;
        public Object[]                Objects;
    }

    #if UNITY_EDITOR
    internal class AssetObjectManifestBuilder
    {
        public static unsafe void BuildManifest(GUID guid, AssetObjectManifest manifest)
        {
            var objects = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid.ToString()));
            BuildManifest(objects, manifest);
        }

        public static unsafe void BuildManifest(Object[] objects, AssetObjectManifest manifest)
        {
            manifest.Objects = objects;
            manifest.GlobalObjectIds = new RuntimeGlobalObjectId[objects.Length];
            var globalobjectIds = new GlobalObjectId[objects.Length];

            GlobalObjectId.GetGlobalObjectIdsSlow(objects, globalobjectIds);

            fixed(GlobalObjectId* src = globalobjectIds)
            fixed(RuntimeGlobalObjectId * dst = manifest.GlobalObjectIds)
            {
                UnsafeUtility.MemCpy(dst, src, UnsafeUtility.SizeOf<RuntimeGlobalObjectId>() * objects.Length);
            }
        }
    }
    #endif
}
