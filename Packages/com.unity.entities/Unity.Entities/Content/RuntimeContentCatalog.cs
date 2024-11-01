using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Serialization;

namespace Unity.Entities.Content
{
    /// <summary>
    /// Runtime catalog.  The class uses data loaded from RuntimeContentCatalogData but expands it into a format that is better suited for runtime access.
    /// Paths are precomputed, dependency arrays are created, and index lookups are resolved into ids.
    /// </summary>
    internal struct RuntimeContentCatalog : IDisposable
    {
        internal struct ObjectLocation
        {
            public ContentFileId FileId;
            public long LocalIdentifierInFile;
        }

        internal struct SceneLocation
        {
            public ContentFileId FileId;
            public int PathIndex;
        }

        internal struct FileLocation
        {
            public ContentArchiveId ArchiveId;
            public int DependencyIndex;
            public int PathIndex;
        }

        internal struct ArchiveLocation
        {
            public int PathIndex;
        }

        internal UnsafeHashMap<UntypedWeakReferenceId, SceneLocation> SceneLocations;
        internal UnsafeHashMap<UntypedWeakReferenceId, ObjectLocation> ObjectLocations;
        internal UnsafeHashMap<ContentFileId, FileLocation> FileLocations;
        internal UnsafeHashMap<ContentArchiveId, ArchiveLocation> ArchiveLocations;
        internal UnsafeList<UnsafeList<ContentFileId>> FileDependencies;
        internal ManagedStringTable ManagedStrings;
        /// <summary>
        /// Initialize internal data storage.
        /// </summary>
        /// <param name="objectCapacity">The initial capacity for objects.</param>
        /// <param name="fileCapacity">The initial capacity for files.</param>
        /// <param name="archiveCapacity">The initial capacity for archives.</param>
        /// <param name="dependencyCapacity">The initial capacity for file dependencies.</param>
        /// <param name="sceneCapacity">The initial capacity for scenes.</param>
        public void Initialize(int objectCapacity = 0, int fileCapacity = 0, int archiveCapacity = 0, int dependencyCapacity = 0, int sceneCapacity = 0)
        {
            ManagedStrings = new ManagedStringTable(objectCapacity + fileCapacity + archiveCapacity + sceneCapacity);
            SceneLocations = new UnsafeHashMap<UntypedWeakReferenceId, SceneLocation>(sceneCapacity, Allocator.Persistent);
            ObjectLocations = new UnsafeHashMap<UntypedWeakReferenceId, ObjectLocation>(objectCapacity, Allocator.Persistent);
            FileLocations = new UnsafeHashMap<ContentFileId, FileLocation>(fileCapacity, Allocator.Persistent);
            ArchiveLocations = new UnsafeHashMap<ContentArchiveId, ArchiveLocation>(archiveCapacity, Allocator.Persistent);
            FileDependencies = new UnsafeList<UnsafeList<ContentFileId>>(dependencyCapacity, Allocator.Persistent);
        }

        /// <summary>
        /// The number of file dependency sets
        /// </summary>
        public int FileDependencySetCount => FileDependencies.Length;

        /// <summary>
        /// Get the entire list of archive ids.
        /// </summary>
        /// <param name="alloc">Allocator to use for created NativeArray.</param>
        /// <returns>The set of archive ids.  The caller is responsible for disposing the returned array.</returns>
        public NativeArray<ContentArchiveId> GetArchiveIds(AllocatorManager.AllocatorHandle alloc) => ArchiveLocations.GetKeyArray(alloc);

        /// <summary>
        /// Get the entire list of object ids.
        /// </summary>
        /// <param name="alloc">Allocator to use for created NativeArray.</param>
        /// <returns>The set of object ids.  The caller is responsible for disposing the returned array.</returns>
        public NativeArray<UntypedWeakReferenceId> GetObjectIds(AllocatorManager.AllocatorHandle alloc) => ObjectLocations.GetKeyArray(alloc);
        /// <summary>
        /// Get the entire list of object ids.
        /// </summary>
        /// <param name="alloc">Allocator to use for created NativeArray.</param>
        /// <returns>The set of object ids.  The caller is responsible for disposing the returned array.</returns>
        public NativeArray<UntypedWeakReferenceId> GetSceneIds(AllocatorManager.AllocatorHandle alloc) => SceneLocations.GetKeyArray(alloc);

        /// <summary>
        /// Get the entire list of file ids.
        /// </summary>
        /// <param name="alloc">Allocator to use for created NativeArray.</param>
        /// <returns>The set of file ids.  The caller is responsible for disposing the returned array.</returns>
        public NativeArray<ContentFileId> GetFileIds(AllocatorManager.AllocatorHandle alloc) => FileLocations.GetKeyArray(alloc);

        /// <summary>
        /// The number of scenes.
        /// </summary>
        public int GetSceneLocationCount() => SceneLocations.Count;

        /// <summary>
        /// The number of objects.
        /// </summary>
        public int GetObjectLocationCount() => ObjectLocations.Count;

        /// <summary>
        /// The number of serialized files.
        /// </summary>
        public int GetFileLocationCount() => FileLocations.Count;

        /// <summary>
        /// The number of archives.
        /// </summary>
        public int GetArchiveLocationCount() => ArchiveLocations.Count;

        /// <summary>
        /// Loads the Catalog data from a given directory. If reset is false and there is existing catalog data, this
        /// will merge the existing RuntimeContentCatalog with the catalog stored at catalogPath.
        /// </summary>
        /// <param name="catalogPath"> The path at which the catalog data that will be loaded is stored. </param>
        /// <param name="archivePathTransformFunc">Functor to transform archive paths.</param>
        /// <param name="mountedFileNameTransformFunc">Functor to transform filenames.</param>
        /// <returns> Returns true if the load was successful, false if it was unsuccessful. </returns>
        public bool LoadCatalogData(string catalogPath, Func<string, string> archivePathTransformFunc, Func<string, string> mountedFileNameTransformFunc)
        {
            if (!string.IsNullOrEmpty(catalogPath) && BlobAssetReference<RuntimeContentCatalogData>.TryRead(catalogPath, 1, out var catalogData))
            {
                LoadCatalogData(ref catalogData.Value, archivePathTransformFunc, mountedFileNameTransformFunc);
                catalogData.Dispose();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Adds catalog data.  Any existing catalog data is preserved.
        /// </summary>
        /// <param name="catalogData">The catalog data.</param>
        /// <param name="archivePathTransformFunc">Functor to transform archive paths.</param>
        /// <param name="mountedFileNameTransformFunc">Functor to transform filenames.</param>
        public void LoadCatalogData(ref RuntimeContentCatalogData catalogData, Func<string, string> archivePathTransformFunc, Func<string, string> mountedFileNameTransformFunc)
        {
            int dependencyOffset = FileDependencies.Length;
            AddFileDependencies(ref catalogData, dependencyOffset);
            AddArchiveLocations(ref catalogData, archivePathTransformFunc);
            AddFileLocations(ref catalogData, dependencyOffset, mountedFileNameTransformFunc);
            AddObjectLocations(ref catalogData);
            AddSceneLocations(ref catalogData);
        }

        /// <summary>
        /// True if the data has been created
        /// </summary>
        public bool IsCreated => ObjectLocations.IsCreated;

        /// <summary>
        /// True if there is no data loaded into the catalog.
        /// </summary>
        public bool IsEmpty => ObjectLocations.IsEmpty && SceneLocations.IsEmpty;

        /// <summary>
        /// Retrieves the stored string value.
        /// </summary>
        /// <param name="index">Index of the stored string value;</param>
        /// <returns>The string value at the index.</returns>
        public string GetStringValue(int index) => ManagedStrings[index];

        /// <summary>
        /// Releases resources.
        /// </summary>
        public void Dispose()
        {
            if (ObjectLocations.IsCreated)
                ObjectLocations.Dispose();

            if(SceneLocations.IsCreated)
                SceneLocations.Dispose();

            if (FileLocations.IsCreated)
                FileLocations.Dispose();

            if (ArchiveLocations.IsCreated)
                ArchiveLocations.Dispose();

            if (FileDependencies.IsCreated)
            {
                for (int i = 0; i < FileDependencies.Length; i++)
                    FileDependencies[i].Dispose();
                FileDependencies.Dispose();
            }
            if(ManagedStrings.IsCreated)
                ManagedStrings.Dispose();
        }

        internal void AddFileDependencies(ref RuntimeContentCatalogData catalogData, int dependencyOffset)
        {
            if (!FileDependencies.IsCreated)
                FileDependencies = new UnsafeList<UnsafeList<ContentFileId>>(catalogData.Dependencies.Length, Allocator.Persistent);

            FileDependencies.Resize(FileDependencies.Length + catalogData.Dependencies.Length);

            for (int depIndex = 0; depIndex < catalogData.Dependencies.Length; depIndex++)
            {
                ref BlobArray<int> deps = ref catalogData.Dependencies[depIndex];
                var depsCount = catalogData.Dependencies[depIndex].Length;
                var dpArray = new UnsafeList<ContentFileId>(depsCount, Allocator.Persistent);
                dpArray.Resize(depsCount);
                for (int i = 0; i < depsCount; i++)
                    dpArray[i] = catalogData.Files[deps[i]].FileId;

                FileDependencies[dependencyOffset + depIndex] = dpArray;
            }
        }

        internal void AddArchiveLocations(ref RuntimeContentCatalogData catalogData, Func<string, string> pathTransformFunc)
        {
            if (!ManagedStrings.IsCreated)
                ManagedStrings = new ManagedStringTable(64);

            if (!ArchiveLocations.IsCreated)
                ArchiveLocations = new UnsafeHashMap<ContentArchiveId, ArchiveLocation>(catalogData.Archives.Length, Allocator.Persistent);
            else
                ArchiveLocations.Capacity = ArchiveLocations.Count + catalogData.Archives.Length;

            for (int archiveIndex = 0; archiveIndex < catalogData.Archives.Length; archiveIndex++)
            {
                var archiveId = catalogData.Archives[archiveIndex].ArchiveId;
                ArchiveLocations.TryAdd(archiveId, new ArchiveLocation { PathIndex = ManagedStrings.Add(pathTransformFunc(archiveId.ToString())) });
            }
        }

        internal void AddFileLocations(ref RuntimeContentCatalogData catalogData, int dependencyOffset, Func<string, string> mountedFileNameTransformFunc)
        {
            if (!ManagedStrings.IsCreated)
                ManagedStrings = new ManagedStringTable(64);

            if (!FileLocations.IsCreated)
                FileLocations = new UnsafeHashMap<ContentFileId, FileLocation>(catalogData.Files.Length, Allocator.Persistent);
            else
                FileLocations.Capacity = FileLocations.Count + catalogData.Files.Length;

            for (int fileIndex = 0; fileIndex < catalogData.Files.Length; fileIndex++)
            {
                var file = catalogData.Files[fileIndex];
                FileLocations.TryAdd(file.FileId, new FileLocation
                {
                    ArchiveId = catalogData.Archives[file.ArchiveIndex].ArchiveId,
                    DependencyIndex = file.DependencyIndex + dependencyOffset,
                    PathIndex = ManagedStrings.Add(mountedFileNameTransformFunc(file.FileId.ToString()))
                });
            }
        }

        internal void AddSceneLocations(ref RuntimeContentCatalogData catalogData)
        {
            if (!ManagedStrings.IsCreated)
                ManagedStrings = new ManagedStringTable(64);

            if (!SceneLocations.IsCreated)
                SceneLocations = new UnsafeHashMap<UntypedWeakReferenceId, SceneLocation>(catalogData.Scenes.Length, Allocator.Persistent);
            else
                SceneLocations.Capacity = SceneLocations.Count + catalogData.Scenes.Length;

            for (int sceneIndex = 0; sceneIndex < catalogData.Scenes.Length; sceneIndex++)
            {
                var sceneData = catalogData.Scenes[sceneIndex];
                var fileId = catalogData.Files[sceneData.FileIndex].FileId;
                SceneLocations.TryAdd(new UntypedWeakReferenceId { GlobalId = sceneData.SceneId.GlobalId, GenerationType = sceneData.SceneId.GenerationType }, new SceneLocation { FileId = fileId, PathIndex = ManagedStrings.Add(sceneData.SceneName.ToString())});
            }
        }

        unsafe internal void AddObjectLocations(ref RuntimeContentCatalogData catalogData)
        {
            if (!ObjectLocations.IsCreated)
                ObjectLocations = new UnsafeHashMap<UntypedWeakReferenceId, ObjectLocation>(catalogData.Objects.Length, Allocator.Persistent);
            else
                ObjectLocations.Capacity = ObjectLocations.Count + catalogData.Objects.Length;

            var filePtr = (ContentFileLocation*)catalogData.Files.GetUnsafePtr();
            var objPtr = (ContentObjectLocation*)catalogData.Objects.GetUnsafePtr();
            var count = catalogData.Objects.Length;
            for (int objIndex = 0; objIndex < count; objIndex++)
            {
                var objData = objPtr[objIndex];
                ObjectLocations.TryAdd(
                    new UntypedWeakReferenceId { GlobalId = objData.ObjectId.GlobalId, GenerationType = objData.ObjectId.GenerationType },
                    new ObjectLocation
                {
                    FileId = filePtr[objData.FileIndex].FileId,
                    LocalIdentifierInFile = objData.LocalIdentifierInFile
                });
            }
        }

        /// <summary>
        /// Get information about a specific object to load.
        /// </summary>
        /// <param name="objectId">The runtime id of the object.  If using an UntypedWeakReferenceId, this would be the RuntimeId property.</param>
        /// <param name="fileId">The ContentArchive file that this object is in.</param>
        /// <param name="localIdentifierInFile">The local id in the ContentArchive file.</param>
        /// <returns>True if the object information has been found, false otherwise.</returns>
        public bool TryGetObjectLocation(UntypedWeakReferenceId objectId, out ContentFileId fileId, out long localIdentifierInFile)
        {
            if (ObjectLocations.TryGetValue(objectId, out var loc))
            {
                fileId = loc.FileId;
                localIdentifierInFile = loc.LocalIdentifierInFile;
                return true;
            }

            fileId = default;
            localIdentifierInFile = default;
            return false;
        }


        /// <summary>
        /// Retrieves location information about a scene.
        /// </summary>
        /// <param name="sceneId">The scene id.</param>
        /// <param name="fileId">The file id of the scene.</param>
        /// <param name="sceneName">The name of the scene.</param>
        /// <returns>True if the location data is found, otherwise false.</returns>
        public bool TryGetSceneLocation(UntypedWeakReferenceId sceneId, out ContentFileId fileId, out string sceneName)
        {
            if (SceneLocations.TryGetValue(sceneId, out var loc))
            {
                fileId = loc.FileId;
                sceneName = GetStringValue(loc.PathIndex);
                return true;
            }
             
            fileId = default;
            sceneName = default;
            return false;
        }

        /// <summary>
        /// Get the ContentArchive file information for loading.
        /// </summary>
        /// <param name="fileId">The id of the ContentArchiveFile.</param>
        /// <param name="filePath">The path of the file within its archive.</param>
        /// <param name="fileDependencies">The set of file dependencies for this file. These must be loaded before this file is loaded.</param>
        /// <param name="archiveId">The archive that this file is contained in.</param>
        /// <returns>True if the file information has been found, false otherwise.</returns>
        public bool TryGetFileLocation(ContentFileId fileId, out string filePath, out UnsafeList<ContentFileId> fileDependencies, out ContentArchiveId archiveId, out int dependencyIndex)
        {
            if (TryGetFileLocation(fileId, out int filePathIndex, out fileDependencies, out archiveId, out dependencyIndex))
            {
                filePath = GetStringValue(filePathIndex);
                return true;
            }
            filePath = default;
            return false;
        }

        /// <summary>
        /// Get the ContentArchive file information for loading.
        /// </summary>
        /// <param name="fileId">The id of the ContentArchiveFile.</param>
        /// <param name="filePathHandle">The path of the file within its archive as a handle.</param>
        /// <param name="fileDependencies">The set of file dependencies for this file. These must be loaded before this file is loaded.</param>
        /// <param name="archiveId">The archive that this file is contained in.</param>
        /// <returns>True if the file information has been found, false otherwise.</returns>
        public bool TryGetFileLocation(ContentFileId fileId, out int filePathIndex, out UnsafeList<ContentFileId> fileDependencies, out ContentArchiveId archiveId, out int dependencyIndex)
        {
            if (FileLocations.TryGetValue(fileId, out var loc))
            {
                filePathIndex = loc.PathIndex;
                archiveId = loc.ArchiveId;
                dependencyIndex = loc.DependencyIndex;
                fileDependencies = FileDependencies[loc.DependencyIndex];
                return true;
            }
            filePathIndex = -1;
            fileDependencies = default;
            archiveId = default;
            dependencyIndex = -1;
            return false;
        }

        /// <summary>
        /// Get the loading information for and archive.
        /// </summary>
        /// <param name="archiveId">The archive id.</param>
        /// <param name="archivePath">The path to mount the archive.</param>
        /// <returns>True if the archive information has been found, false otherwise.</returns>
        public bool TryGetArchiveLocation(ContentArchiveId archiveId, out string archivePath)
        {
            if (ArchiveLocations.TryGetValue(archiveId, out var loc))
            {
                archivePath = GetStringValue(loc.PathIndex);
                return true;
            }
            archivePath = default;
            return false;
        }
        /// <summary>
        /// Get the loading information for an archive.
        /// </summary>
        /// <param name="archiveId">The archive id.</param>
        /// <param name="archivePathHandle">The path to mount the archive as a handle.</param>
        /// <returns>True if the archive information has been found, false otherwise.</returns>
        public bool TryGetArchiveLocation(ContentArchiveId archiveId, out int archivePathIndex)
        {
            if (ArchiveLocations.TryGetValue(archiveId, out var loc))
            {
                archivePathIndex = loc.PathIndex;
                return true;
            }
            archivePathIndex = -1;
            return false;
        }

        /// <summary>
        /// Print the contents of the catalog
        /// </summary>
        /// <param name="printFunc">Function to handle the actual printing.</param>
        public void Print(Action<string> printFunc)
        {
            try
            {
                using (var archives = GetArchiveIds(Allocator.Temp))
                {
                    for (int i = 0; i < archives.Length; i++)
                    {
                        var a = archives[i];
                        if (!TryGetArchiveLocation(a, out string archivePath))
                            printFunc($"Unable to find location for archive id {a}.");
                        else
                            printFunc($"Archives[{i}] = {a}, path = {archivePath}");
                    }
                }

                using (var files = GetFileIds(Allocator.Temp))
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        var f = files[i];
                        if (!TryGetFileLocation(f, out string filePath, out var deps, out var archiveId, out var dependencyIndex))
                        {
                            printFunc($"Unable to find location for file id {f}");
                        }
                        else
                        {
                            printFunc($"Files[{i}] = {f}, ArchiveId = {archiveId}, path = {filePath}, dependencyIndex = {dependencyIndex}");
                        }
                    }
                }

                using (var objects = GetObjectIds(Allocator.Temp))
                {
                    for (int i = 0; i < objects.Length; i++)
                    {
                        var o = objects[i];
                        if (!TryGetObjectLocation(o, out var fileId, out var lfid))
                            printFunc($"Unable to find location for object id {o}");
                        else
                            printFunc($"Objects[{i}] = {o}, FileId = {fileId}, IdInFile = {lfid}");
                    }
                }

                for (int i = 0; i < FileDependencies.Length; i++)
                {
                    printFunc($"Dependencies[{i}]:");
                    StringBuilder builder = new StringBuilder(64 * FileDependencies[i].Length);
                    builder.Append("ContentFileIds: [");
                    for (int j = 0; j < FileDependencies[i].Length; j++)
                        builder.Append(FileDependencies[i][j].Value + ", ");
                    builder.Remove(builder.Length - 2, 2);
                    builder.Append("]");
                    printFunc(builder.ToString());
                }
            }
            catch (Exception e)
            {
                printFunc(e.ToString());
            }
        }



    }

    struct ManagedStringTable : IDisposable
    {
        GCHandle ValuesHandle;
        public ManagedStringTable(int capacity)
        {
            ValuesHandle = GCHandle.Alloc(new System.Collections.Generic.List<string>(capacity));
        }

        public bool IsCreated => ValuesHandle.IsAllocated;

        public int Add(string s)
        {
            var vals = ValuesHandle.Target as System.Collections.Generic.List<string>;
            vals.Add(s);
            return vals.Count - 1;
        }

        public void Dispose()
        {
            ValuesHandle.Free();
        }

        public string this[int i]
        {
            get
            {
                return (ValuesHandle.Target as System.Collections.Generic.List<string>)[i];
            }
        }
    }

}
