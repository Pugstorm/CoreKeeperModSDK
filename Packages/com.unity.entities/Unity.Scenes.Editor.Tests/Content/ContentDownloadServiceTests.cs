using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Entities.Content;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Entities.Editor.Tests
{
    //will run until cancelled
    class NonCompletingDownloadOperation : ContentDownloadService.DownloadOperation
    {
        protected override void StartDownload(string remotePath, string localTmpPath) { }
        protected override bool ProcessDownload(ref long downloadedBytes, ref string error) => false;
        protected override void CancelDownload() { }
    }


    static class ContentDownloadServiceExtensions
    {
        public static bool AllDownloadsComplete(this ContentDownloadService service, IEnumerable<RemoteContentLocation> locs)
        {
            foreach (var l in locs)
                if (service.GetDownloadStatus(l).DownloadState < ContentDownloadService.State.Complete)
                    return false;
            return true;
        }
    }

    [TestFixture]
    class ContentDownloadServiceTests
    {
        string cachePath;
        string remotePath;
        List<RemoteContentLocation> locations;

        static RemoteContentLocation CreateTestFile(string folder, int length)
        {
            var path = Path.Combine(folder, Path.GetRandomFileName());
            var buffer = new byte[length];
            for (int i = 0; i < length; i++)
                buffer[i] = (byte)i;

            using (var fs = File.OpenWrite(path))
            {
                fs.Write(buffer, 0, buffer.Length); 
            }
            return new RemoteContentLocation{ Hash = UnityEngine.Hash128.Compute(buffer), Path = $"file://{path}", Size = length, Crc = 0 };
        }

        ContentDownloadService CreateService(Func<ContentDownloadService.DownloadOperation> opFunc)
        {
            if (Directory.Exists(cachePath))
                Directory.Delete(cachePath, true);
            Directory.CreateDirectory(cachePath);
            return new ContentDownloadService("default", cachePath, 1, 5, opFunc);
        }

        [OneTimeSetUp]
        public void Setup()
        {
            cachePath = Path.Combine(Application.temporaryCachePath, "AddressablesIntegrationTests.ContentCache");
            remotePath = Path.Combine(Application.temporaryCachePath, "AddressablesIntegrationTests.RemoteContent");
            if(Directory.Exists(cachePath))
                Directory.Delete(cachePath, true);
            if(Directory.Exists(remotePath))
                Directory.Delete(remotePath, true);
            Directory.CreateDirectory(cachePath);
            Directory.CreateDirectory(remotePath);
            locations = new List<RemoteContentLocation>();
            for (int i = 0; i < 25; i++)
                locations.Add(CreateTestFile(remotePath, (i+1) * 1024));
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            if (Directory.Exists(cachePath))
                Directory.Delete(cachePath, true);
            if (Directory.Exists(remotePath))
                Directory.Delete(remotePath, true);
        }

        [Test]
        public void ComputeCachePath_ReturnsExpectedPathForLocation()
        {
            using (var service = CreateService(null))
            {
                foreach (var loc in locations)
                {
                    var hashStr = loc.Hash.ToString();
                    var cachedPath = service.ComputeCachePath(loc);
                    Assert.AreEqual(Path.Combine(cachePath, $"{hashStr[0]}{hashStr[1]}", hashStr), cachedPath);
                }
            }
        }

        static void AssertFilesMatch(string fileA, string fileB, long expectedSize)
        {
            if (fileA.StartsWith("file://")) fileA = fileA.Substring("file://".Length);
            if (fileB.StartsWith("file://")) fileB = fileB.Substring("file://".Length);
            Assert.IsTrue(File.Exists(fileA), $"File {fileA} does not exist.");
            Assert.IsTrue(File.Exists(fileB), $"File {fileB} does not exist.");

            using (var fsA = File.OpenRead(fileA))
            {
                Assert.AreEqual(expectedSize, fsA.Length);
                using (var fsB = File.OpenRead(fileB))
                {
                    Assert.AreEqual(expectedSize, fsB.Length);
                    for (var i = 0; i < expectedSize; i++)
                        Assert.AreEqual(fsA.ReadByte(), fsB.ReadByte());
                }
            }
        }

        [Test]
        public void WhenMaxDownloadsExceeded_AdditionalDownloadsQueue()
        {
            using (var service = CreateService(() => new NonCompletingDownloadOperation()))
            {
                for (int i = 0; i < locations.Count; i++)
                {
                    var status = service.DownloadContent(locations[i]);
                    if (i < 5)
                        Assert.AreEqual(ContentDownloadService.State.Downloading, status.DownloadState);
                    else
                        Assert.AreEqual(ContentDownloadService.State.Queued, status.DownloadState);
                }
            }
        }

        [UnityTest]
        public IEnumerator DownloadContent_FileMatches_Remote()
        {
            using (var service = CreateService(null))
            {
                foreach (var loc in locations)
                    service.DownloadContent(loc);

                while (!service.AllDownloadsComplete(locations))
                {
                    service.Process();
                    yield return null;
                }

                foreach (var loc in locations)
                {
                    var status = service.GetDownloadStatus(loc);
                    Assert.AreEqual(ContentDownloadService.State.Complete, status.DownloadState);
                    AssertFilesMatch(status.LocalPath.ToString(), loc.Path.ToString(), loc.Size);
                }
            }
        }

        [UnityTest]
        public IEnumerator UnendingDownloadOperations_CanBeCancelled()
        {
            using (var svc = CreateService(() => new NonCompletingDownloadOperation()))
            {
                foreach (var loc in locations)
                    svc.DownloadContent(loc);
                for (int i = 0; i < 10; i++)
                {
                    svc.Process();
                    foreach (var loc in locations)
                        Assert.IsTrue(svc.GetDownloadStatus(loc).DownloadState >= ContentDownloadService.State.Queued);

                    yield return null;
                }
                foreach (var loc in locations)
                    svc.CancelDownload(loc);
                svc.Process();
                foreach (var loc in locations)
                    Assert.AreEqual(ContentDownloadService.State.Cancelled, svc.GetDownloadStatus(loc).DownloadState);
            }
        }

    }
}
