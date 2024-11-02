using System;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.SceneManagement;

namespace Unity.Scenes.Editor
{
    [ScriptedImporter(44, "sceneMetaData")]
    [InitializeOnLoad]
    class GameObjectSceneMetaDataImporter : ScriptedImporter
    {
        [Serializable]
        internal struct GameObjectSceneMetaData
        {
            public BlobString SceneName;
            public BlobArray<Hash128> SubSceneGUIDs;
        }

        static readonly int CurrentFileFormatVersion = 3;
        static Type GameObjectSceneMetaDataImporterType = null;
        const string k_Extension = "scenemeta";

        static GameObjectSceneMetaDataImporter()
        {
            GameObjectSceneMetaDataImporterType = typeof(GameObjectSceneMetaDataImporter);
        }

        static bool GetMetaDataArtifactPath(Hash128 artifactHash, out string metaDataPath)
        {
            metaDataPath = default;
            if (!AssetDatabaseCompatibility.GetArtifactPaths(artifactHash, out string[] paths))
                return false;

            try
            {
                metaDataPath = paths.First(o => o.EndsWith(k_Extension, StringComparison.Ordinal));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }

            return true;
        }

        static bool GetGameObjectSceneMetaData(Hash128 sceneGUID, bool async, out BlobAssetReference<GameObjectSceneMetaData> sceneMetaDataRef)
        {
            sceneMetaDataRef = default;

            if (!sceneGUID.IsValid)
                return false;

            var importMode = async ? ImportMode.Asynchronous : ImportMode.Synchronous;
            var hash = AssetDatabaseCompatibility.GetArtifactHash(sceneGUID, GameObjectSceneMetaDataImporterType, importMode);
            if (!hash.isValid)
                return false;

            if (!GetMetaDataArtifactPath(hash, out var metaPath))
            {
                var scenePath = AssetDatabaseCompatibility.GuidToPath(sceneGUID);
                throw new InvalidOperationException($"Failed to get artifact paths for scene {scenePath} - {sceneGUID}");
            }

            if (!BlobAssetReference<GameObjectSceneMetaData>.TryRead(metaPath, CurrentFileFormatVersion, out sceneMetaDataRef))
                throw new InvalidOperationException($"Unable to read {metaPath}");

            return true;
        }


        internal static Hash128[] GetSubScenes(GUID guid)
        {
            if(!GetGameObjectSceneMetaData(guid, false, out var sceneMetaDataRef))
            {
                return new Hash128[0];
            }

            var guids = sceneMetaDataRef.Value.SubSceneGUIDs.ToArray();
            sceneMetaDataRef.Dispose();
            return guids;
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            ctx.DependsOnCustomDependency("SceneMetaDataFileFormatVersion");
            EditorEntityScenes.DependOnSceneGameObjects(AssetDatabaseCompatibility.PathToGUID(ctx.assetPath), ctx);

            var scene = EditorSceneManager.OpenScene(ctx.assetPath, OpenSceneMode.Additive);
            try
            {
                var metaPath = ctx.GetOutputArtifactFilePath(k_Extension);
                var subScenes = SubScene.AllSubScenes;
                var sceneGuids = subScenes.Where(x => x.SceneGUID.IsValid).Select(x => x.SceneGUID)
                    .Distinct()
                    .ToArray();

                var builder = new BlobBuilder(Allocator.Temp);
                ref var metaData = ref builder.ConstructRoot<GameObjectSceneMetaData>();

                builder.AllocateString(ref metaData.SceneName, scene.name);
                builder.Construct(ref metaData.SubSceneGUIDs, sceneGuids);
                BlobAssetReference<GameObjectSceneMetaData>.Write(builder, metaPath, CurrentFileFormatVersion);
                builder.Dispose();
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
