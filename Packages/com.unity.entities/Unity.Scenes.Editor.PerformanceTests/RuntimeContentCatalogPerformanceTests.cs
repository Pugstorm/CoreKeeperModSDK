#if !UNITY_DISABLE_MANAGED_COMPONENTS
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using UnityEditor;

namespace Unity.Entities.Tests.Content
{
    public class RuntimeContentCatalogPerformanceTests
    {
        static void CreateCatalog(int objCount, int fileCount, int archiveCount, int depCount, string outputPath)
        {
            var r = new Random(1234);
            var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref var blob = ref blobBuilder.ConstructRoot<RuntimeContentCatalogData>();
            var archives = blobBuilder.Allocate(ref blob.Archives, archiveCount);
            archives[0] = new ContentArchiveLocation();
            var archIndexMap = new Dictionary<ContentArchiveId, int>();
            for (int i = 1; i < archiveCount; i++)
            {
                var id = new ContentArchiveId { Value = new Hash128((uint)i, r.NextUInt(), r.NextUInt(), r.NextUInt()) };
                archives[i] = new ContentArchiveLocation() { ArchiveId = id };
                archIndexMap[id] = i;
            }

            var files = blobBuilder.Allocate(ref blob.Files, fileCount);
            files[0] = new ContentFileLocation();
            var fileIndexMap = new Dictionary<ContentFileId, int>();
            for (int i = 1; i < fileCount; i++)
            {
                var id = new ContentFileId { Value = new Hash128(r.NextUInt(), (uint)i, r.NextUInt(), r.NextUInt()) };
                files[i] = new ContentFileLocation() { FileId = id, ArchiveIndex = (ushort)( i % archiveCount ), DependencyIndex = (ushort)(i % depCount) };
                fileIndexMap[id] = i;
            }

            var dependencies = blobBuilder.Allocate(ref blob.Dependencies, depCount);
            for (int i = 0; i < depCount; i++)
            {
                var deps = blobBuilder.Allocate(ref dependencies[i], 5);
                for (int d = 0; d < 5; d++)
                    deps[d] = (ushort)r.NextInt(0, files.Length);
            }


            var objs = blobBuilder.Allocate(ref blob.Objects, objCount);
            for (int i = 0; i < objCount; i++)
            {
                var id = new UntypedWeakReferenceId(new RuntimeGlobalObjectId() { AssetGUID = new Hash128(r.NextUInt(), r.NextUInt(), (uint)i, r.NextUInt()), SceneObjectIdentifier0 = (uint)i, }, WeakReferenceGenerationType.UnityObject );
                objs[i] = new ContentObjectLocation() { ObjectId = new UnsafeUntypedWeakReferenceId { GenerationType = id.GenerationType, GlobalId = id.GlobalId }, FileIndex = (ushort)(i%fileCount), LocalIdentifierInFile = i };
            }

            var aref = blobBuilder.CreateBlobAssetReference<RuntimeContentCatalogData>(Allocator.Temp);
            BlobAssetReference<RuntimeContentCatalogData>.Write(blobBuilder, outputPath, 1);
            blobBuilder.Dispose();
            aref.Dispose();
        }

        [Test, Performance]
        public void LoadCatalogDataRaw(
            [Values(10000, 100000)]int objCount,
            [Values(1000, 65000)] int fileCount,
            [Values(1000, 65000)] int archiveCount,
            [Values(5, 50)] int depCount)
        {
            var path = $"Temp/catalog-{objCount}-{fileCount}-{archiveCount}-{depCount}-{objCount + fileCount + archiveCount}.bin";
            CreateCatalog(objCount, fileCount, archiveCount, depCount, path);
            Measure.Method(() =>
            {
                BlobAssetReference<RuntimeContentCatalogData>.TryRead(path, 1, out var catalog);
            })
            .WarmupCount(1)
            .MeasurementCount(3)
            .Run();
            File.Delete(path);
        }

        [Test, Performance]
        public void AddFullCatalogData(
            [Values(10000, 100000)]int objCount,
            [Values(1000, 65000)]int fileCount,
            [Values(1000, 65000)]int archiveCount,
            [Values(5, 50)]int depCount)
        {
            var path = $"Temp/catalog-{objCount}-{fileCount}-{archiveCount}-{depCount}-{objCount + fileCount + archiveCount}.bin";
            CreateCatalog(objCount, fileCount, archiveCount, depCount, path);
            
            BlobAssetReference<RuntimeContentCatalogData>.TryRead(path, 1, out var data);
            var dataDirectory = Path.GetDirectoryName(path).Replace('\\', '/') + "/";
            Measure.Method(() =>
                {
                    using (var catalog = new RuntimeContentCatalog())
                        catalog.LoadCatalogData(ref data.Value, p => $"{dataDirectory}{p}", i => $"carchive:/a:/{i}");
                })
                .WarmupCount(1)
                .MeasurementCount(3)
                .Run();
            File.Delete(path);
            data.Dispose();
        }

        [Test, Performance]
        public void AddObjectData([Values(10000, 100000)]int objCount)
        {
            var fileCount = 1;
            var archiveCount = 1;
            var depCount = 1;
            var path = $"Temp/catalog-{objCount}-{fileCount}-{archiveCount}-{depCount}-{objCount + fileCount + archiveCount}.bin";
            CreateCatalog(objCount, fileCount, archiveCount, depCount, path);

            BlobAssetReference<RuntimeContentCatalogData>.TryRead(path, 1, out var data);
            Measure.Method(() =>
            {
                using (var catalog = new RuntimeContentCatalog())
                    catalog.AddObjectLocations(ref data.Value);
            })
                .WarmupCount(1)
                .MeasurementCount(3)
                .Run();
            File.Delete(path);
            data.Dispose();
        }

        [Test, Performance]
        public void TryGetArchiveLocation([Values(1000, 10000, 65000)]int archiveCount)
        {
            var path = $"Temp/catalog-TryGetArchiveLocation.bin";
            CreateCatalog(1000, 1000, archiveCount, 1, path);
            var catalog = new RuntimeContentCatalog();
            catalog.LoadCatalogData(path, p => $"{Path.GetDirectoryName(path)}{p}", i => $"carchive:/a:/{i}");
            var ids = catalog.GetArchiveIds(Allocator.Temp);
            var r = new Random(1234);
            Measure.Method(() =>
            {
                for (int i = 0; i < 100000; i++)
                    catalog.TryGetArchiveLocation(ids[r.NextInt(0, ids.Length)], out string archivePath);
            })
            .WarmupCount(3)
            .MeasurementCount(10)
            .Run();
            File.Delete(path);
            ids.Dispose();
        }

        [Test, Performance]
        public void TryGetFileLocation([Values(1000, 10000, 65000)]int fileCount, [Values(10, 100, 1000)]int depCount)
        {
            var path = $"Temp/catalog-TryGetArchiveLocation.bin";
            CreateCatalog(1000, fileCount, 1000, depCount, path);
            var catalog = new RuntimeContentCatalog();
            catalog.LoadCatalogData(path, p => $"{Path.GetDirectoryName(path)}{p}", i => $"carchive:/a:/{i}");
            var ids = catalog.GetFileIds(Allocator.Temp);
            var r = new Random(1234);
            Measure.Method(() =>
            {
                for (int i = 0; i < 100000; i++)
                    catalog.TryGetFileLocation(ids[r.NextInt(0, ids.Length)], out string filePath, out var deps, out var archiveId, out var _);
            })
            .WarmupCount(3)
            .MeasurementCount(10)
            .Run();
            File.Delete(path);
            ids.Dispose();
        }

        [Test, Performance]
        public void TryGetObjectLocation([Values(1000, 10000, 100000, 1000000)]int objectCount)
        {
            var path = $"Temp/catalog-TryGetArchiveLocation.bin";
            CreateCatalog(objectCount, 1000, 1000, 1, path);
            var catalog = new RuntimeContentCatalog();
            catalog.LoadCatalogData(path, p => $"{Path.GetDirectoryName(path)}{p}", i => $"carchive:/a:/{i}");
            var ids = catalog.GetObjectIds(Allocator.Temp);
            var r = new Random(1234);
            Measure.Method(() =>
            {
                for (int i = 0; i < 100000; i++)
                    catalog.TryGetObjectLocation(ids[r.NextInt(0, ids.Length)], out var fileId, out var LocalId);
            })
            .WarmupCount(3)
            .MeasurementCount(10)
            .Run();
            File.Delete(path);
            ids.Dispose();
        }
    }
}
#endif
