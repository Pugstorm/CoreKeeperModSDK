using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Content;
using Unity.Entities.Serialization;

namespace Unity.Entities.Tests.Content
{
    public class RuntimeContentCatalogTests
    {
        // The initial data we seed the catalog with
        BlobAssetReference<RuntimeContentCatalogData> CatalogDataInitial;

        // data used for tests involving appending data to the end of the catalog without merging anything
        BlobAssetReference<RuntimeContentCatalogData> CatalogDataAppend;

        List<ContentObjectLocation> ObjectLocationsInitial;
        List<ContentObjectLocation> ObjectLocationsAppend;

        List<ContentFileLocation> FileLocationsInitial;
        List<ContentFileLocation> FileLocationsAppend;

        List<ContentArchiveLocation> ArchiveLocationsInitial;
        List<ContentArchiveLocation> ArchiveLocationsAppend;

        List<int[]> DependencyMappingsInitial;
        List<int[]> DependencyMappingsAppend;

        [OneTimeSetUp]
        public void SetUpBlobArrays()
        {
            CreateInitialCatalogData();
            CreateAppendCatalogData();
        }

        [OneTimeTearDown]
        public void DisposeBlobArrays()
        {
            CatalogDataInitial.Dispose();
            CatalogDataAppend.Dispose();
        }

        string fileNameTransformFunc(string fileId) => fileId.ToString();
        string archivePathTransformFunc(string archiveId) => $"testPrefix/{archiveId}";

        static Hash128 GetFileIdListHashCode(UnsafeList<ContentFileId> list)
        {
            var ss = new xxHash3.StreamingState(false);
            foreach (var d in list)
                ss.Update(d);
            return new Hash128(ss.DigestHash128());
        }

        private ContentArchiveLocation CreateContentArchiveLocation(string hashString)
        {
            return new ContentArchiveLocation
            {
                ArchiveId = new ContentArchiveId
                {
                    Value = new Hash128(hashString)
                }
            };
        }

        private ContentFileLocation CreateFileLocation(string hashString, int archiveIndex, int dependencyIndex)
        {
            return new ContentFileLocation
            {
                FileId = new ContentFileId
                {
                    Value = new Hash128(hashString)
                },
                ArchiveIndex = archiveIndex,
                DependencyIndex = dependencyIndex,

            };
        }

        private ContentObjectLocation CreateObjectLocation(string hashString, int fileIndex, long localIdInFile)
        {
            return new ContentObjectLocation
            {
                ObjectId = new UnsafeUntypedWeakReferenceId()
                {
                    GlobalId = new RuntimeGlobalObjectId { AssetGUID = new Hash128(hashString) }
                },
                FileIndex = fileIndex,
                LocalIdentifierInFile = localIdInFile
            };
        }

        private void CreateInitialCatalogData()
        {
            ArchiveLocationsInitial = new List<ContentArchiveLocation>();
            FileLocationsInitial = new List<ContentFileLocation>();
            ObjectLocationsInitial = new List<ContentObjectLocation>();
            DependencyMappingsInitial = new List<int[]>();

            var initialBuilder = new BlobBuilder(Allocator.Temp);
            ref RuntimeContentCatalogData initialCatalogData = ref initialBuilder.ConstructRoot<RuntimeContentCatalogData>();

            int initialNumArchives = 3;
            BlobBuilderArray<ContentArchiveLocation> initialArchiveBuilder = initialBuilder.Allocate(ref initialCatalogData.Archives, initialNumArchives);
            for (int i = 0; i < initialNumArchives; i++)
            {
                var archiveLocation = CreateContentArchiveLocation((i).ToString().PadRight(32, '0'));
                initialArchiveBuilder[i] = archiveLocation;
                ArchiveLocationsInitial.Add(archiveLocation);
            }

            int initialNumFiles = 6;
            BlobBuilderArray<ContentFileLocation> initialFileBuilder = initialBuilder.Allocate(ref initialCatalogData.Files, initialNumFiles);
            initialFileBuilder[0] = CreateFileLocation("0".PadRight(32, '0'), 0, 0);
            initialFileBuilder[1] = CreateFileLocation("1".PadRight(32, '0'), 0, 0);
            initialFileBuilder[2] = CreateFileLocation("2".PadRight(32, '0'), 1, 1);
            initialFileBuilder[3] = CreateFileLocation("3".PadRight(32, '0'), 1, 1);
            initialFileBuilder[4] = CreateFileLocation("4".PadRight(32, '0'), 2, 2);
            initialFileBuilder[5] = CreateFileLocation("5".PadRight(32, '0'), 2, 3);

            for (int i = 0; i < initialNumFiles; i++)
                FileLocationsInitial.Add(initialFileBuilder[i]);

            int initialNumObjects = 5;
            BlobBuilderArray<ContentObjectLocation> initialObjectBuilder = initialBuilder.Allocate(ref initialCatalogData.Objects, initialNumObjects);
            for (int i = 0; i < initialNumObjects; i++)
            {
                var objLocation = CreateObjectLocation((i).ToString().PadRight(32, '0'), i, i);
                initialObjectBuilder[i] = objLocation;
                ObjectLocationsInitial.Add(objLocation);
            }

            DependencyMappingsInitial.Add(new[] { 2, 3 });
            DependencyMappingsInitial.Add(new[] { 4, 5 });
            DependencyMappingsInitial.Add(new[] { 4 });
            DependencyMappingsInitial.Add(new[] { 5 });

            BlobBuilderArray<BlobArray<int>> initialDependencyBuilder = initialBuilder.Allocate(ref initialCatalogData.Dependencies, DependencyMappingsInitial.Count);
            for (int i = 0; i < DependencyMappingsInitial.Count; i++)
            {
                var deps = initialBuilder.Allocate(ref initialDependencyBuilder[i], DependencyMappingsInitial[i].Length);
                for (int d = 0; d < DependencyMappingsInitial[i].Length; d++)
                    deps[d] = DependencyMappingsInitial[i][d];
            }

            CatalogDataInitial = initialBuilder.CreateBlobAssetReference<RuntimeContentCatalogData>(Allocator.Persistent);
            initialBuilder.Dispose();
        }

        private void CreateAppendCatalogData()
        {
            ArchiveLocationsAppend = new List<ContentArchiveLocation>();
            FileLocationsAppend = new List<ContentFileLocation>();
            ObjectLocationsAppend = new List<ContentObjectLocation>();
            DependencyMappingsAppend = new List<int[]>();
            var appendBuilder = new BlobBuilder(Allocator.Temp);
            ref RuntimeContentCatalogData appendCatalogData = ref appendBuilder.ConstructRoot<RuntimeContentCatalogData>();

            int numArchives = 3;
            BlobBuilderArray<ContentArchiveLocation> archiveBuilder = appendBuilder.Allocate(ref appendCatalogData.Archives, numArchives);
            for (int i = 0; i < numArchives; i++)
            {
                var archiveLocation = CreateContentArchiveLocation((i + ArchiveLocationsInitial.Count).ToString().PadRight(32, '0'));
                archiveBuilder[i] = archiveLocation;
                ArchiveLocationsAppend.Add(archiveLocation);
            }

            int numFiles = 6;
            BlobBuilderArray<ContentFileLocation> fileBuilder = appendBuilder.Allocate(ref appendCatalogData.Files, numFiles);
            fileBuilder[0] = CreateFileLocation("6".PadRight(32, '0'), 0, 0);
            fileBuilder[1] = CreateFileLocation("7".PadRight(32, '0'), 0, 0);
            fileBuilder[2] = CreateFileLocation("8".PadRight(32, '0'), 1, 0);
            fileBuilder[3] = CreateFileLocation("9".PadRight(32, '0'), 1, 1);
            fileBuilder[4] = CreateFileLocation("A".PadRight(32, '0'), 2, 2);
            fileBuilder[5] = CreateFileLocation("B".PadRight(32, '0'), 2, 3);

            for (int i = 0; i < numFiles; i++)
                FileLocationsAppend.Add(fileBuilder[i]);

            int numObjects = 5;
            BlobBuilderArray<ContentObjectLocation> objectBuilder = appendBuilder.Allocate(ref appendCatalogData.Objects, numObjects);
            for (int i = 0; i < numObjects; i++)
            {
                var objLocation = CreateObjectLocation(((i + ObjectLocationsInitial.Count)).ToString().PadRight(32, '0'), i, i);
                objectBuilder[i] = objLocation;
                ObjectLocationsAppend.Add(objLocation);
            }


            int numDependencies = 4;
            DependencyMappingsAppend.Add(new[] { 3 });
            DependencyMappingsAppend.Add(new[] { 4, 5 });
            DependencyMappingsAppend.Add(new[] { 4 });
            DependencyMappingsAppend.Add(new[] { 5 });

            BlobBuilderArray<BlobArray<int>> dependencyBuilder = appendBuilder.Allocate(ref appendCatalogData.Dependencies, numDependencies);
            for (int i = 0; i < DependencyMappingsAppend.Count; i++)
            {
                var deps = appendBuilder.Allocate(ref dependencyBuilder[i], DependencyMappingsAppend[i].Length);
                for (int d = 0; d < DependencyMappingsAppend[i].Length; d++)
                    deps[d] = DependencyMappingsAppend[i][d];
            }

            CatalogDataAppend = appendBuilder.CreateBlobAssetReference<RuntimeContentCatalogData>(Allocator.Persistent);
            appendBuilder.Dispose();
        }

        [Test]
        public void RuntimeContentCatalog_AddCatalogData_WorksAsExpectedInSimpleCase()
        {
            RuntimeContentCatalog catalog = new RuntimeContentCatalog();
            try
            {
                catalog.LoadCatalogData(ref CatalogDataInitial.Value, archivePathTransformFunc, fileNameTransformFunc);
                for (int i = 0; i < ObjectLocationsInitial.Count; i++)
                {
                    var expectedObjectLocation = ObjectLocationsInitial[i];
                    Assert.IsTrue(catalog.ObjectLocations.ContainsKey(new UntypedWeakReferenceId(expectedObjectLocation.ObjectId.GlobalId, expectedObjectLocation.ObjectId.GenerationType)), "Every ObjectId passed into AddCatalogData should be present in catalog.ObjectLocations.");
                    var storedObjectLocation = catalog.ObjectLocations[new UntypedWeakReferenceId(expectedObjectLocation.ObjectId.GlobalId, expectedObjectLocation.ObjectId.GenerationType)];
                    var expectedFileId = FileLocationsInitial[expectedObjectLocation.FileIndex].FileId;
                    Assert.AreEqual(expectedFileId, storedObjectLocation.FileId, "The FileId stored should match the correct file.");
                    Assert.AreEqual(expectedObjectLocation.LocalIdentifierInFile, storedObjectLocation.LocalIdentifierInFile, "The LocalIdentifierInFile for the stored ObjectLocation should match that of the ObjectLocation passed in.");
                }

                //Check that FileLocations are correctly appended
                for (int i = 0; i < FileLocationsInitial.Count; i++)

                {
                    var expectedFileLocation = FileLocationsInitial[i];
                    Assert.IsTrue(catalog.FileLocations.ContainsKey(expectedFileLocation.FileId), "AddCatalogData and MergeCatalogData should lead to the same FileIds being added to ObjectLocations");
                    var storedFileLocation = catalog.FileLocations[expectedFileLocation.FileId];
                    var expectedArchiveId = ArchiveLocationsInitial[expectedFileLocation.ArchiveIndex].ArchiveId;
                    var expectedPathString = fileNameTransformFunc(expectedFileLocation.FileId.ToString());

                    Assert.AreEqual(expectedArchiveId, storedFileLocation.ArchiveId, "AddCatalogData should correctly associate the passed in ArchiveIndex with the correct ArchiveId");
                    Assert.AreEqual(expectedFileLocation.DependencyIndex, storedFileLocation.DependencyIndex, "The passed in DependencyIndex should match the DependencyIndex set in the end");
                    Assert.AreEqual(expectedPathString, catalog.GetStringValue(storedFileLocation.PathIndex), "The path of the FileLocation being added should be equal to the FilePrefix followed by the FileId of the file being added.");
                }


                //Check that ArchiveLocations are correctly appended
                for (int i = 0; i < ArchiveLocationsInitial.Count; i++)
                {
                    var expectedArchiveLocation = ArchiveLocationsInitial[i];
                    Assert.IsTrue(catalog.ArchiveLocations.ContainsKey(expectedArchiveLocation.ArchiveId), "AddCatalogData and MergeCatalogData should lead to the same ArchiveIds being added to ArchiveLocations");
                    var storedArchiveLocation = catalog.ArchiveLocations[expectedArchiveLocation.ArchiveId];
                    var expectedPathString = archivePathTransformFunc(expectedArchiveLocation.ArchiveId.ToString());
                    Assert.AreEqual(expectedPathString, catalog.GetStringValue(storedArchiveLocation.PathIndex), "The path of the ArchiveLocation being added should be equal to the DataDirectory followed by the ArchiveId of the Archive being added.");
                }

                //Check that FileDependencies are correctly appended and are in the correct order
                for (int i = 0; i < DependencyMappingsInitial.Count; i++)
                {
                    for (int d = 0; d < DependencyMappingsInitial[i].Length; d++)
                    {
                        int fileIndex = DependencyMappingsInitial[i][d];
                        Assert.AreEqual(FileLocationsInitial[fileIndex].FileId, catalog.FileDependencies[i][d], "FileIds are either not properly assigned within DependencyMapping, or are assigned in the wrong order.");
                    }
                }
            }
            finally
            {
                catalog.Dispose();
            }
        }

        [Test]
        public void RuntimeContentCatalog_AddFileDependencies_WorksInStrictAppendCase()
        {
            RuntimeContentCatalog catalog = new RuntimeContentCatalog();
            try
            {
                catalog.AddFileDependencies(ref CatalogDataInitial.Value, catalog.FileDependencies.Length);
                Assert.AreEqual(DependencyMappingsInitial.Count, catalog.FileDependencies.Length, "Following append, the number of dependency mappings should go up by the amount of dependencies within DependencyMappingsInitial");
                for (int i = 0; i < DependencyMappingsInitial.Count; i++)
                {
                    var expectedList = DependencyMappingsInitial[i];
                    var storedList = catalog.FileDependencies[i];
                    Assert.AreEqual(expectedList.Length, storedList.Length, "The length of the dependency list stored should be the same as the length of the dependency list passed in. ");
                    for (int j = 0; j < expectedList.Length; j++)
                        Assert.AreEqual(FileLocationsInitial[expectedList[j]].FileId, storedList[j], "The file indices passed in should be correctly converted to the correct FileIds following the append.");
                }
            }
            finally
            {
                catalog.Dispose();
            }
        }

        [Test]
        public void RuntimeContentCatalog_AddArchiveLocations_WorksInStrictAppendCase()
        {
            RuntimeContentCatalog catalog = new RuntimeContentCatalog();
            try
            {
                catalog.AddArchiveLocations(ref CatalogDataInitial.Value, archivePathTransformFunc);
                Assert.AreEqual(ArchiveLocationsInitial.Count, catalog.ArchiveLocations.Count, "Following append, the number of dependency mappings should go up by the number of Archives within ArchiveLocationsInitial.");
                for (int i = 0; i < ArchiveLocationsInitial.Count; i++)
                {
                    var expectedLoc = ArchiveLocationsInitial[i];
                    Assert.IsTrue(catalog.ArchiveLocations.ContainsKey(expectedLoc.ArchiveId), "Every ArchiveLocation passed into AddArchiveLocations should be included in ArchiveLocations following the append.");
                    var storedLoc = catalog.ArchiveLocations[expectedLoc.ArchiveId];
                    var expectedPath = archivePathTransformFunc(expectedLoc.ArchiveId.ToString());
                    Assert.AreEqual(expectedPath, catalog.GetStringValue(storedLoc.PathIndex), "The path associated with the ArchiveLocation should be equal to the directory passed into AddArchiveLocations followed by the ArchiveId.");
                }
            }
            finally
            {
                catalog.Dispose();
            }
        }

        [Test]
        public void RuntimeContentCatalog_AddFileLocations_WorksInStrictAppendCase()
        {
            RuntimeContentCatalog catalog = new RuntimeContentCatalog();
            try
            {
                catalog.AddFileLocations(ref CatalogDataInitial.Value, catalog.FileDependencies.Length, fileNameTransformFunc);
                Assert.AreEqual(FileLocationsInitial.Count, catalog.FileLocations.Count, "The number of FileLocations should be equal to the number added following append.");
                for (int i = 0; i < FileLocationsInitial.Count; i++)
                {
                    var expectedLocation = FileLocationsInitial[i];
                    Assert.IsTrue(catalog.FileLocations.ContainsKey(expectedLocation.FileId), "All FileLocations in catalogData should be added to FileLocations");
                    var storedLocation = catalog.FileLocations[expectedLocation.FileId];
                    var expectedArchive = ArchiveLocationsInitial[expectedLocation.ArchiveIndex];
                    Assert.AreEqual(expectedArchive.ArchiveId, storedLocation.ArchiveId, "The Archive associated with each FileLocation passed in should not change following append.");
                    Assert.AreEqual(expectedLocation.DependencyIndex, storedLocation.DependencyIndex, "The dependency index associated with each FileLocation passed in should not change following append.");
                }
            }
            finally
            {
                catalog.Dispose();
            }
        }

        [Test]
        public void RuntimeContentCatalog_AddObjectLocations_WorksInStrictAppendCase()
        {
            RuntimeContentCatalog catalog = new RuntimeContentCatalog();
            try
            {
                catalog.AddObjectLocations(ref CatalogDataInitial.Value);
                Assert.AreEqual(ObjectLocationsInitial.Count, catalog.ObjectLocations.Count, "The number of ObjectLocations should be equal to the number of ObjectLocations passed in following append.");
                for (int i = 0; i < ObjectLocationsInitial.Count; i++)
                {
                    var expectedObject = ObjectLocationsInitial[i];
                    Assert.IsTrue(catalog.ObjectLocations.ContainsKey(new UntypedWeakReferenceId(expectedObject.ObjectId.GlobalId, expectedObject.ObjectId.GenerationType)), "Every ObjectLocation passed into AddObjectLocations should be contained within ObjectLocations");
                    var storedObject = catalog.ObjectLocations[new UntypedWeakReferenceId(expectedObject.ObjectId.GlobalId, expectedObject.ObjectId.GenerationType)];
                    var expectedFile = FileLocationsInitial[expectedObject.FileIndex];
                    Assert.AreEqual(expectedFile.FileId, storedObject.FileId, "Stored object does not have the correct FileId ");
                    Assert.AreEqual(expectedObject.LocalIdentifierInFile, storedObject.LocalIdentifierInFile, "Stored object does not have the correct localIdentifierInFile");
                }
            }
            finally
            {
                catalog.Dispose();
            }
        }



        [Test]
        public void RuntimeContentCatalog_TryGetObjectLocation_SucceedsOnExistingId()
        {
            RuntimeContentCatalog catalog = new RuntimeContentCatalog();
            try
            {
                catalog.LoadCatalogData(ref CatalogDataInitial.Value, archivePathTransformFunc, fileNameTransformFunc);

                ContentFileId actualId;
                long actualIdentifier;

                ContentFileId expectedFileId = FileLocationsInitial[ObjectLocationsInitial[0].FileIndex].FileId;
                long expectedIdentifier = ObjectLocationsInitial[0].LocalIdentifierInFile;

                bool result = catalog.TryGetObjectLocation(new UntypedWeakReferenceId(ObjectLocationsInitial[0].ObjectId.GlobalId, ObjectLocationsInitial[0].ObjectId.GenerationType), out actualId, out actualIdentifier);
                Assert.IsTrue(result, "TryGetObjectLocation should return true when queried with an ObjectLocation that exists.");
                Assert.AreEqual(expectedFileId, actualId, "FileId retrieved from TryGetObjectLocation did not match the expected FileId.");
                Assert.AreEqual(expectedIdentifier, actualIdentifier, "LocalIdentifierInFile retrieved from TryGetObjectLocation did not match the expected LocalIdentifierInFile");
            }
            finally
            {
                catalog.Dispose();
            }
        }

        [Test]
        public void RuntimeContentCatalog_TryGetObjectLocation_FailsOnInvalidId()
        {
            RuntimeContentCatalog catalog = new RuntimeContentCatalog();
            long actualIdentifier;
            try
            {
                catalog.LoadCatalogData(ref CatalogDataInitial.Value, archivePathTransformFunc, fileNameTransformFunc);

                bool result = catalog.TryGetObjectLocation(new UntypedWeakReferenceId(ObjectLocationsAppend[0].ObjectId.GlobalId, ObjectLocationsAppend[0].ObjectId.GenerationType), out var actualId, out actualIdentifier);
                Assert.IsFalse(result, "TryGetObjectLocation should return  false when queried with an ObjectLocation that doesnt not exist in the RuntimeContentCatalog.");
                Assert.AreEqual((ContentFileId) default, actualId, "FileId should be default in the case of a query for an ObjectLocation that doesn't exist");
                Assert.AreEqual((long) default, actualIdentifier, "LocalIdentifierInFile should be default in the case of a query for an ObjectLocation that doesn't exist");
            }
            finally
            {
                catalog.Dispose();
            }
        }

        [Test]
        public void RuntimeContentCatalog_TryGetFileLocationString_SucceedsOnExistingId()
        {
            RuntimeContentCatalog catalog = new RuntimeContentCatalog();
            try
            {
                catalog.LoadCatalogData(ref CatalogDataInitial.Value, archivePathTransformFunc, fileNameTransformFunc);

                string expectedFilePath = fileNameTransformFunc(FileLocationsInitial[0].FileId.ToString());
                int expectedDependencyIndex = FileLocationsInitial[0].DependencyIndex;
                int[] expectedDependencyIndices = DependencyMappingsInitial[expectedDependencyIndex];
                ContentArchiveId expectedArchiveId = ArchiveLocationsInitial[FileLocationsInitial[0].ArchiveIndex].ArchiveId;

                bool result = catalog.TryGetFileLocation(FileLocationsInitial[0].FileId, out string actualFilePath, out UnsafeList<ContentFileId> actualDependencies,
                    out ContentArchiveId actualArchiveId, out int actualDependencyIndex);

                Assert.IsTrue(result, "TryGetFileLocation should return true when queried with a FileLocation that exists in the catalog. ");
                Assert.AreEqual(expectedFilePath, actualFilePath, "The file path returned should be equal to the file prefix + the hash of the FileLocation's FileId");
                Assert.AreEqual(expectedDependencyIndex, actualDependencyIndex, "The value of the stored dependency index should be equal to the expected DependencyIndex");
                Assert.AreEqual(expectedArchiveId, actualArchiveId, "The value of the stored ArchiveId should match the ArchiveId passed in.");
                for (int i = 0; i < expectedDependencyIndices.Length; i++)
                {
                    ContentFileId expectedFileId = FileLocationsInitial[expectedDependencyIndices[i]].FileId;
                    Assert.AreEqual(expectedFileId, actualDependencies[i], $"File dependencies should be correctly retrieved from the call to TryGetFileLocation, failure occured at index {i}");
                }
            }
            finally
            {
                catalog.Dispose();
            }
        }

        [Test]
        public void RuntimeContentCatalog_TryGetFileLocationString_FailsOnInvalidId()
        {
            RuntimeContentCatalog catalog = new RuntimeContentCatalog();
            try
            {
                catalog.LoadCatalogData(ref CatalogDataInitial.Value, archivePathTransformFunc, fileNameTransformFunc);
                bool result = catalog.TryGetFileLocation(FileLocationsAppend[0].FileId, out string actualFilePath, out UnsafeList<ContentFileId> actualDependencies,
                    out ContentArchiveId actualArchiveId, out int actualDependencyIndex);
                Assert.IsFalse(result, "TryGetFileLocation should return false when queried with a FileLocation that does not exist within the catalog.");
                Assert.AreEqual((string) default, actualFilePath, "the file path should be set to default if the retrieval fails.");
                Assert.IsFalse(actualDependencies.IsCreated, "The dependency list should not be created if the retrieval fails.");
                Assert.AreEqual((ContentArchiveId) default, actualArchiveId, "The archiveId should be set to default if the retrieval fails. ");
                Assert.AreEqual(-1, actualDependencyIndex, "The dependencyIndex returned should be set to -1 if the retrieval fails.");
            }
            finally
            {
                catalog.Dispose();
            }
        }

        [Test]
        public void RuntimeContentCatalog_TryGetFileLocationHandle_SucceedsOnExistingId()
        {
            RuntimeContentCatalog catalog = new RuntimeContentCatalog();
            try
            {
                catalog.LoadCatalogData(ref CatalogDataInitial.Value, archivePathTransformFunc, fileNameTransformFunc);

                string expectedFilePath = fileNameTransformFunc(FileLocationsInitial[0].FileId.ToString());
                int expectedDependencyIndex = FileLocationsInitial[0].DependencyIndex;
                int[] expectedDependencyIndices = DependencyMappingsInitial[expectedDependencyIndex];
                ContentArchiveId expectedArchiveId = ArchiveLocationsInitial[FileLocationsInitial[0].ArchiveIndex].ArchiveId;

                bool result = catalog.TryGetFileLocation(FileLocationsInitial[0].FileId, out int actualFilePathIndex, out UnsafeList<ContentFileId> actualDependencies,
                    out ContentArchiveId actualArchiveId, out int actualDependencyIndex);

                Assert.IsTrue(result, "TryGetFileLocation should return true when queried with a FileLocation that exists in the catalog. ");
                Assert.AreEqual(expectedFilePath, catalog.GetStringValue(actualFilePathIndex), "The target of the file path returned should be equal to the file prefix + the hash of the FileLocation's FileId");
                Assert.AreEqual(expectedDependencyIndex, actualDependencyIndex, "The value of the stored dependency index should be equal to the expected DependencyIndex");
                Assert.AreEqual(expectedArchiveId, actualArchiveId, "The value of the stored ArchiveId should match the ArchiveId passed in.");
                for (int i = 0; i < expectedDependencyIndices.Length; i++)
                {
                    ContentFileId expectedFileId = FileLocationsInitial[expectedDependencyIndices[i]].FileId;
                    Assert.AreEqual(expectedFileId, actualDependencies[i], $"File dependencies should be correctly retrieved from the call to TryGetFileLocation, failure occured at index {i}");
                }

            }
            finally
            {
                catalog.Dispose();
            }
        }

        [Test]
        public void RuntimeContentCatalog_TryGetFileLocationHandle_FailsOnInvalidId()
        {
            RuntimeContentCatalog catalog = new RuntimeContentCatalog();
            try
            {
                catalog.LoadCatalogData(ref CatalogDataInitial.Value, archivePathTransformFunc, fileNameTransformFunc);
                bool result = catalog.TryGetFileLocation(FileLocationsAppend[0].FileId, out int actualFilePathIndex, out UnsafeList<ContentFileId> actualDependencies,
                    out ContentArchiveId actualArchiveId, out int actualDependencyIndex);
                Assert.IsFalse(result, "TryGetFileLocation should return false when queried with a FileLocation that does not exist within the catalog.");
                Assert.AreEqual(-1, actualFilePathIndex, "the file path should be set to default if the retrieval fails.");
                Assert.IsFalse(actualDependencies.IsCreated, "The dependency list should not be created if the retrieval fails.");
                Assert.AreEqual((ContentArchiveId) default, actualArchiveId, "The archiveId should be set to default if the retrieval fails. ");
                Assert.AreEqual(-1, actualDependencyIndex, "The dependencyIndex returned should be set to -1 if the retrieval fails.");
            }
            finally
            {
                catalog.Dispose();
            }
        }

        [Test]
        public void RuntimeContentCatalog_TryGetArchiveLocationString_SucceedsOnExistingId()
        {
            RuntimeContentCatalog catalog = new RuntimeContentCatalog();
            try
            {
                catalog.LoadCatalogData(ref CatalogDataInitial.Value, archivePathTransformFunc, fileNameTransformFunc);

                string expectedPath = archivePathTransformFunc(ArchiveLocationsInitial[0].ArchiveId.ToString());

                bool result = catalog.TryGetArchiveLocation(ArchiveLocationsInitial[0].ArchiveId, out string actualPath);
                Assert.IsTrue(result, "TryGetArchiveLocation should return true when queried with a FileLocation that exists in the catalog. ");
                Assert.AreEqual(expectedPath, actualPath, "The archive path returned should be equal to the data directory + the hash of the ArchiveLocation's ArchiveId ");
            }
            finally
            {
                catalog.Dispose();
            }
        }

        [Test]
        public void RuntimeContentCatalog_TryGetArchiveLocationString_FailsOnInvalidId()
        {
            RuntimeContentCatalog catalog = new RuntimeContentCatalog();
            try
            {
                catalog.LoadCatalogData(ref CatalogDataInitial.Value, archivePathTransformFunc, fileNameTransformFunc);
                bool result = catalog.TryGetArchiveLocation(ArchiveLocationsAppend[2].ArchiveId, out string actualPath);
                Assert.IsFalse(result, "TryGetArchiveLocation should return false when queried with a ArchiveId that does not exist within the catalog.");
                Assert.AreEqual((string) default, actualPath, "The archive path should be set to default if the retrieval fails. ");
            }
            finally
            {
                catalog.Dispose();
            }
        }

        [Test]
        public void RuntimeContentCatalog_TryGetArchiveLocationHandle_SucceedsOnExistingId()
        {
            RuntimeContentCatalog catalog = new RuntimeContentCatalog();
            try
            {
                catalog.LoadCatalogData(ref CatalogDataInitial.Value, archivePathTransformFunc, fileNameTransformFunc);

                string expectedPath = archivePathTransformFunc(ArchiveLocationsInitial[0].ArchiveId.ToString());

                bool result = catalog.TryGetArchiveLocation(ArchiveLocationsInitial[0].ArchiveId, out int actualPathIndex);
                Assert.IsTrue(result, "TryGetArchiveLocation should return true when queried with a FileLocation that exists in the catalog. ");
                Assert.AreEqual(expectedPath, catalog.GetStringValue(actualPathIndex), "The archive path returned should be equal to the data directory + the hash of the ArchiveLocation's ArchiveId ");
            }
            finally
            {
                catalog.Dispose();
            }
        }

        [Test]
        public void RuntimeContentCatalog_TryGetArchiveLocationHandle_FailsOnInvalidId()
        {
            RuntimeContentCatalog catalog = new RuntimeContentCatalog();
            try
            {
                catalog.LoadCatalogData(ref CatalogDataInitial.Value, archivePathTransformFunc, fileNameTransformFunc);
                bool result = catalog.TryGetArchiveLocation(ArchiveLocationsAppend[2].ArchiveId, out int actualPathIndex);
                Assert.IsFalse(result, "TryGetArchiveLocation should return false when queried with a ArchiveId that does not exist within the catalog.");
                Assert.AreEqual(-1, actualPathIndex, "The archive path should be set to default if the retrieval fails. ");
            }
            finally
            {
                catalog.Dispose();
            }
        }
    }
}
