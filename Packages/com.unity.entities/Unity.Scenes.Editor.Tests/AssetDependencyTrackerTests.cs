using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Tests;
using Unity.Scenes.Editor.Tests;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Hash128 = UnityEngine.Hash128;

namespace Unity.Scenes.Tests
{
    public class AssetDependencyTrackerTests : ECSTestsFixture
    {
        TestWithTempAssets _Temp;

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
            _Temp.TearDown();
        }

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _Temp.SetUp();
        }

        [Test]
        public void TestMissingAsset()
        {
            using(var test = new AssetDependencyTracker<int>(typeof(TestImporter), "stuff"))
            using(var list = new NativeList<AssetDependencyTracker<int>.Completed>(0, Allocator.Persistent))
            {
                var guid = GUID.Generate();
                test.Add(guid, 1, true);

                test.GetCompleted(list);

                AssertDontExist(list, guid, 1);

                AssetDatabase.Refresh();
                test.GetCompleted(list);
                Assert.AreEqual(0, list.Length);
            }
        }


        // One asset registered with multiple guid & user data
        [Test]
        public void MultiTargetCompletion()
        {
            using(var test = new AssetDependencyTracker<int>(typeof(TestImporter), "stuff"))
            using(var list = new NativeList<AssetDependencyTracker<int>.Completed>(0, Allocator.Persistent))
            {
                var path = _Temp.GetNextPath();
                var guid = WriteFileAndRefresh(path, "boing");

                test.Add(guid, 0, true);
                test.Add(guid, 1, true);
                test.Add(guid, 2, false);

                // Remove this one again
                // (Ensure that we dont get events about user data we already removed)
                test.Remove(guid, 1);

                test.GetCompleted(list);

                Assert.AreEqual(1, test.TotalAssets);
                Assert.AreEqual(0, test.InProgressAssets);

                Assert.AreEqual(2, list.Length);
                Assert.AreEqual(guid, list[0].Asset);
                Assert.AreEqual(guid, list[1].Asset);

                Assert.AreEqual(2, list[0].UserKey);
                Assert.AreEqual(0, list[1].UserKey);

                Assert.AreEqual(list[1].ArtifactID, list[0].ArtifactID);
                Assert.AreNotEqual(default(Hash128), list[0].ArtifactID);
            }
        }

        [Test]
        public void ImbalancedRegistrationThrows()
        {
            using (var test = new AssetDependencyTracker<int>(typeof(TestImporter), "stuff"))
            {
                var guid = GUID.Generate();
                test.Add(guid, 0, true);

                Assert.Throws<ArgumentException>(() =>
                {
                    test.Add(guid, 0, true);
                });
                Assert.Throws<ArgumentException>(() =>
                {
                    test.Remove(guid, 8);
                });
                Assert.Throws<ArgumentException>(() =>
                {
                    test.Remove(default, 0);
                });
            }
        }

        [Test]
        public void RemoveTwiceThrows()
        {
            using (var test = new AssetDependencyTracker<int>(typeof(TestImporter), "stuff"))
            {
                var guid = GUID.Generate();
                test.Add(guid, 0, true);
                test.Remove(guid, 0);

                Assert.Throws<ArgumentException>(() =>
                {
                    test.Remove(guid, 0);
                });
            }
        }

        GUID WriteFileAndRefresh(string path, string contents)
        {
            File.WriteAllText(path, contents);
            AssetDatabase.Refresh();
            return new GUID(AssetDatabase.AssetPathToGUID(path));
        }

        [Test]
        public void TestSyncAssetModifiedDuringImport()
        {
            var path = _Temp.GetNextPath();
            var guid = WriteFileAndRefresh(path, "Boing");

            using (var test = new AssetDependencyTracker<int>(typeof(TestImporter), "stuff"))
            using (var list = new NativeList<AssetDependencyTracker<int>.Completed>(0, Allocator.Persistent))
            {
                File.WriteAllText(path, "Boings");
                test.Add(guid, 1, false);
                test.GetCompleted(list);

                AssertOne(list, guid, 1, "Boings");
            }
        }

        [UnityTest]
        public IEnumerator AsyncDependencyChangeIsDetected_AndRetriggersOnError_WithFileChange()
        {
            return AsyncDependencyChangeIsDetected_AndRetriggersOnError_Impl(true);
        }

        [UnityTest]
        [Ignore("The current approach to detecting failures does not detect cases where only the file modification date changes.")]
        public IEnumerator AsyncDependencyChangeIsDetected_AndRetriggersOnError_WithoutFileChange()
        {
            return AsyncDependencyChangeIsDetected_AndRetriggersOnError_Impl(false);
        }

        public IEnumerator AsyncDependencyChangeIsDetected_AndRetriggersOnError_Impl(bool modifyFile)
        {
            // This test is specifically designed to exercise the case where a source asset dependency is changed during import.
            // This can happen in entity scene imports. Regular asset imports just detect this and retrigger the import.
            // However, the special code path that we are taking for on demand importing circumvents this error handling
            // and means that we need to do it ourselves in the AssetDependencyTracker.
            var path = _Temp.GetNextPath();
            var dependencyPath = _Temp.GetNextPath();
            WriteFileAndRefresh(dependencyPath, "test data");

            using (var tracker = new AssetDependencyTracker<int>(typeof(TestImporterWithSourceDependency), "TestImporterWithSourceDependency"))
            using (var list = new NativeList<AssetDependencyTracker<int>.Completed>(0, Allocator.Persistent))
            {
                // The results of importing an asset are cached by content and importer, so we have to actually use a
                // different content every time to get the importer to run. This is crucial for this test as we are
                // relying on the importer to stall and give us time to modify some files.
                var dependencyGuid = AssetDatabase.GUIDFromAssetPath(dependencyPath).ToString();
                var guid = WriteFileAndRefresh(path, dependencyGuid + " " + DateTime.UtcNow.Ticks);

                // This triggers the import. The test importer declares a source dependency and then immediately stalls
                // for a few seconds.
                tracker.Add(guid, 1, true);
                tracker.AddCompleted(list);
                Thread.Sleep(2000);

                // While the importer is running, we are going to touch the source dependency file. This will confuse
                // the asset database and make the import fail, which is required for our test.
                var fullDependencyPath = Path.GetFullPath(dependencyPath);
                if (modifyFile)
                    File.WriteAllText(fullDependencyPath, "test data 2");
                else
                    File.SetLastWriteTime(fullDependencyPath, DateTime.Now);

                // The changed source asset should trigger a reimport of the asset that depends on it to handle the
                // failed import. If it does not, then we'll eventually time out.
                var sw = Stopwatch.StartNew();
                while (list.IsEmpty)
                {
                    if (sw.Elapsed.TotalSeconds > 50)
                        throw new Exception("Timed out waiting for import to complete");
                    yield return null;
                    tracker.AddCompleted(list);
                }

                AssertOne(list, guid, 1, dependencyGuid);
            }
        }

        [UnityTest]
        public IEnumerator AsyncChangeIsDetected()
        {
            var path = _Temp.GetNextPath();

            using (var test = new AssetDependencyTracker<int>(typeof(TestImporter), "stuff"))
            using (var list = new NativeList<AssetDependencyTracker<int>.Completed>(0, Allocator.Persistent))
            {
                var guid = WriteFileAndRefresh(path, "a");

                test.Add(guid, 1, true);

                // There is no guarantee on when the change will be detected we have to just wait for it. We should eventually get exactly one change.
                test.GetCompleted(list);
                while (list.IsEmpty)
                {
                    yield return null;
                    test.AddCompleted(list);
                }

                AssertOne(list, guid, 1, "a");
                var hash0 = list[0].ArtifactID;

                WriteFileAndRefresh(path, "b");
                test.GetCompleted(list);
                while (list.IsEmpty)
                {
                    yield return null;
                    test.AddCompleted(list);
                }

                AssertOne(list, guid, 1, "b");
                var hash1 = list[0].ArtifactID;

                Assert.AreNotEqual(hash0, hash1);
            }
        }

        [UnityTest]
        public IEnumerator AsyncRemoveIsDetected()
        {
            var path = _Temp.GetNextPath();

            using (var test = new AssetDependencyTracker<int>(typeof(TestImporter), "stuff"))
            using (var list = new NativeList<AssetDependencyTracker<int>.Completed>(0, Allocator.Persistent))
            {
                var guid = WriteFileAndRefresh(path, "Boings");
                test.Add(guid, 1, true);

                test.GetCompleted(list);
                while (list.IsEmpty)
                {
                    yield return null;
                    test.AddCompleted(list);
                }
                AssertOne(list, guid, 1, "Boings");

                File.Delete(path);
                AssetDatabase.Refresh();
                test.GetCompleted(list);

                AssertDontExist(list, guid, 1);
            }
        }

        static void AssertDontExist(NativeList<AssetDependencyTracker<int>.Completed> list, GUID guid, int userData)
        {
            Assert.AreEqual(1, list.Length, "Completed List must have 1 element");
            Assert.AreEqual(guid, list[0].Asset, "Received GUID doesn't match");
            Assert.AreEqual(userData, list[0].UserKey, "UserData doesn't match");
            Assert.AreEqual(default(Hash128), list[0].ArtifactID, "Artifact should not exist");
        }

        static void AssertOne(NativeList<AssetDependencyTracker<int>.Completed> list, GUID guid, int userData, string content)
        {
            Assert.AreEqual(1, list.Length, "Completed List must have 1 element");
            Assert.AreEqual(guid, list[0].Asset, "Received GUID doesn't match");
            Assert.AreEqual(userData, list[0].UserKey, "UserData doesn't match");
            Assert.AreNotEqual(default(Hash128), list[0].ArtifactID, "Artifact should exist");
            AssetDatabaseCompatibility.GetArtifactPaths(list[0].ArtifactID, out var paths);

            Assert.AreEqual(paths.Length, 1);
            var path = paths.First(p => p.EndsWith("output", StringComparison.Ordinal));
            Assert.AreEqual(content, File.ReadAllText(path));
        }
    }
}
