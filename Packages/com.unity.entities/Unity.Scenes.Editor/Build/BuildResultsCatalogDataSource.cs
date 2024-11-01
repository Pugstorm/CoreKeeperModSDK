using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities.Serialization;
using UnityEditor;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEngine;

namespace Unity.Entities.Content
{
    /// <summary>
    /// Class to convert build output into a format that can be used to build the content catalog data.
    /// </summary>
    internal class BuildResultsCatalogDataSource : IRuntimeCatalogDataSource
    {
        IBundleBuildResults _results;
        IEnumerable<(UntypedWeakReferenceId, long)> _builtInObjects;
        Func<string, ContentFileId> _depIdFunc;

        Dictionary<string, List<(UntypedWeakReferenceId, long)>> _clusterToObjects = new Dictionary<string, List<(UntypedWeakReferenceId, long)>>();
        public BuildResultsCatalogDataSource(IBundleBuildResults results, IEnumerable<(UntypedWeakReferenceId, long)> builtInObjects, Func<Hash128, long, string, UntypedWeakReferenceId> objIdentifierToRuntimeId, Func<string, ContentFileId> pathToFileId, IClusterOutput buildLayout, HashSet<string> sceneGuids)
        {
            _results = results;
            _depIdFunc = pathToFileId;
            _builtInObjects = builtInObjects;
            foreach (var k in buildLayout.ObjectToCluster)
            {
                var id = objIdentifierToRuntimeId(k.Key.guid, k.Key.localIdentifierInFile, k.Key.filePath);
                if (!id.IsValid)
                    continue;

                if (!_clusterToObjects.TryGetValue(k.Value.ToString(), out var objs))
                    _clusterToObjects.Add(k.Value.ToString(), objs = new List<(UntypedWeakReferenceId, long)>());
                objs.Add((id, buildLayout.ObjectToLocalID[k.Key]));
            }
        }

        public IEnumerable<ContentArchiveId> GetArchiveIds()
        {
            return _results.WriteResults.Keys.Select(s => new ContentArchiveId { Value = new Hash128(s) }).Append(default);
        }

        public IEnumerable<ContentFileId> GetDependencies(ContentFileId fileId)
        {
            if (!fileId.IsValid)
                return new ContentFileId[0];

            return _results.WriteResults[fileId.ToString()].externalFileReferences.Select(s => _depIdFunc(s.filePath));
        }

        public IEnumerable<(UntypedWeakReferenceId, string)> GetScenes(ContentFileId fileId)
        {
            if (fileId.IsValid)
            {
                var guidStr = fileId.Value.ToString();
                var scenePath = AssetDatabase.GUIDToAssetPath(guidStr);
                if (!string.IsNullOrEmpty(scenePath))
                {
                    var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                    var id = UntypedWeakReferenceId.CreateFromObjectInstance(sceneAsset);
                    Resources.UnloadAsset(sceneAsset);
                    return new (UntypedWeakReferenceId, string)[] { (id, guidStr) };
                }
            }
            return new (UntypedWeakReferenceId, string)[0];
        }

        public IEnumerable<(UntypedWeakReferenceId, long)> GetObjects(ContentFileId fileId)
        {
            if (!fileId.IsValid)
                return _builtInObjects;
            if (_clusterToObjects.TryGetValue(fileId.Value.ToString(), out var res))
                return res;
            return new (UntypedWeakReferenceId, long)[0];
        }

        public IEnumerable<ContentFileId> GetFileIds(ContentArchiveId archiveId)
        {
            //for now there is just one file per archive
            return new ContentFileId[] { new ContentFileId { Value = archiveId.Value } };
        }
    }
}
