using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Entities.Serialization;
using Unity.Mathematics;

namespace Unity.Entities.Content
{
    /// <summary>
    /// Utility class for creating and printing catalog data.
    /// </summary>
    internal static class RuntimeContentCatalogDataUtility
    {
        /// <summary>
        /// Print data in the serialized format.
        /// </summary>
        /// <param name="data">The catalog data to print.</param>
        /// <param name="printFunc">Method to print each line.</param>
        public static void Print(ref RuntimeContentCatalogData data, Action<string> printFunc)
        {
            for (int i = 0; i < data.Archives.Length; i++)
                printFunc($"Archives[{i}] = ArchiveId:{data.Archives[i].ArchiveId}");
            for (int i = 0; i < data.Files.Length; i++)
                printFunc($"Files[{i}] = FileId:{data.Files[i].FileId}, ArchiveIndex:{data.Files[i].ArchiveIndex}, DependencyIndex:{data.Files[i].DependencyIndex}");
            for (int i = 0; i < data.Objects.Length; i++)
                printFunc($"Objects[{i}] = ObjectId:{data.Objects[i].ObjectId}, FileIndex:{data.Objects[i].FileIndex}, LocalIdentifierInFile:{data.Objects[i].LocalIdentifierInFile}");
            for (int i = 0; i < data.Scenes.Length; i++)
                printFunc($"Scenes[{i}] = SceneId:{data.Scenes[i].SceneId}, FileIndex:{data.Scenes[i].FileIndex}, SceneName:{data.Scenes[i].SceneName}");
            for (int i = 0; i < data.Dependencies.Length; i++)
            {
                var sb = new StringBuilder();
                for (int d = 0; d < data.Dependencies[i].Length; d++)
                    sb.Append($"{data.Dependencies[i][d]},");
                printFunc($"Dependencies[{i}] = {sb}");
            }
        }

        //Given a set of files, find or create the index of the matching set in the map
        static int GetDependencyIndex(IEnumerable<ContentFileId> files, ref Dictionary<uint2, int> depMap, ref List<List<ContentFileId>> depList)
        {
            var ss = new xxHash3.StreamingState(true);
            foreach (var d in files)
                ss.Update(d);
            var hash = ss.DigestHash64();
            if (!depMap.TryGetValue(hash, out var index))
            {
                depMap.Add(hash, index = depList.Count);
                depList.Add(new List<ContentFileId>(files));
            }
            return index;
        }

        /// <summary>
        /// Create runtime catalog data.
        /// </summary>
        /// <param name="dataSource">The source catalog data.</param>
        /// <param name="blobBuilder">The builder to use when creating the data.</param>
        /// <param name="blobDataRoot">The root object to create data into.</param>
        /// <param name="idRemapFunc">Functor to remap <seealso cref="ContentRuntimeId"/>.</param>
        public static void Create(IRuntimeCatalogDataSource dataSource, BlobBuilder blobBuilder, ref RuntimeContentCatalogData blobDataRoot, Func<UntypedWeakReferenceId, UntypedWeakReferenceId> idRemapFunc)
        {
            var archiveEnum = dataSource.GetArchiveIds();
            var archiveCount = archiveEnum.Count();
            var archives = blobBuilder.Allocate(ref blobDataRoot.Archives, archiveCount);
            var allFiles = new List<(ContentFileId, int)>();
            var fileIndexMap = new Dictionary<ContentFileId, int>();
            var allObjects = new List<(UntypedWeakReferenceId, long, int)>();
            var allScenes = new List<(UntypedWeakReferenceId, string, int)>();
            int archiveIndex = 0;
            foreach (var a in archiveEnum)
            {
                archives[archiveIndex] = new ContentArchiveLocation() { ArchiveId = a };
                var fileEnum = dataSource.GetFileIds(a);
                foreach (var f in fileEnum)
                {
                    var fileIndex = allFiles.Count;
                    fileIndexMap.Add(f, fileIndex);
                    allFiles.Add((f, archiveIndex));
                    var objEnum = dataSource.GetObjects(f);
                    foreach (var o in objEnum)
                        allObjects.Add((o.Item1, o.Item2, fileIndex));
                    var sceneEnum = dataSource.GetScenes(f);
                    foreach (var s in sceneEnum)
                        allScenes.Add((s.Item1, s.Item2, fileIndex));
                }
                archiveIndex++;
            }
            var dependencyMap = new Dictionary<uint2, int>();
            var allDependencies = new List<List<ContentFileId>>();
            var files = blobBuilder.Allocate(ref blobDataRoot.Files, allFiles.Count);
            for (int i = 0; i < allFiles.Count; i++)
            {
                var fi = allFiles[i];
                files[i] = new ContentFileLocation()
                {
                    FileId = fi.Item1,
                    ArchiveIndex = fi.Item2,
                    DependencyIndex = GetDependencyIndex(dataSource.GetDependencies(fi.Item1), ref dependencyMap, ref allDependencies)
                };
            }
            var objects = blobBuilder.Allocate(ref blobDataRoot.Objects, allObjects.Count);
            for (int i = 0; i < allObjects.Count; i++)
            {
                var oi = allObjects[i];
                objects[i] = new ContentObjectLocation() { ObjectId = new UnsafeUntypedWeakReferenceId(idRemapFunc(oi.Item1)), LocalIdentifierInFile = oi.Item2, FileIndex = oi.Item3 };
            }

            var scenes = blobBuilder.Allocate(ref blobDataRoot.Scenes, allScenes.Count);
            for (int i = 0; i < allScenes.Count; i++)
            {
                var oi = allScenes[i];
                scenes[i] = new ContentSceneLocation() { SceneId = new UnsafeUntypedWeakReferenceId(idRemapFunc(oi.Item1)), SceneName = oi.Item2, FileIndex = oi.Item3 };
            }

            var dependencies = blobBuilder.Allocate(ref blobDataRoot.Dependencies, allDependencies.Count);
            for (int i = 0; i < allDependencies.Count; i++)
            {
                var deps = blobBuilder.Allocate(ref dependencies[i], allDependencies[i].Count);
                for (int d = 0; d < allDependencies[i].Count; d++)
                    deps[d] = fileIndexMap[allDependencies[i][d]];
            }
        }
    }
}
