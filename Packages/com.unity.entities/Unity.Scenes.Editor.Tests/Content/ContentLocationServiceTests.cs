using NUnit.Framework;
using Unity.Entities.Content;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

namespace Unity.Entities.Editor.Tests
{
    
    [TestFixture]
    class ContentLocationServiceTests
    {
        DefaultContentLocationService CreateService(int locCount)
        {
            return new DefaultContentLocationService(null, 0, locCount,
                i => (new RemoteContentId($"id{i}"), new RemoteContentLocation { Path = $"id{i}", Size = 10, Hash = new Hash128((uint)i, 0, 0, 0) }),
                i => i,
                i => new string[] { "all" });
        }

        void AssertState(DefaultContentLocationService ls, string id, ContentLocationService.ResolvingState state)
        {
            var status = ls.GetLocationStatus(new RemoteContentId(id));
            Assert.AreEqual(state, status.State);
        }

        [Test]
        public void GetLocationStatus_WhenCalledWithoutEntries_ReturnsStatusNone()
        {
            using (var ls = CreateService(0))
                AssertState(ls, "id0", ContentLocationService.ResolvingState.None);
        }

        [Test]
        public void GetLocationStatus_WhenCalledWithValidId_ReturnsStatusComplete()
        {
            using (var ls = CreateService(1))
                AssertState(ls, "id0", ContentLocationService.ResolvingState.Complete);
        }

        [Test]
        public void GetLocationStatus_WhenCalledWithInvalidId_ReturnsStatusNone()
        {
            using (var ls = CreateService(1))
                AssertState(ls, "invalid", ContentLocationService.ResolvingState.None);
        }
        [Test]
        public unsafe void GetContentSet_LocationStatus()
        {
            using (var ls = CreateService(10))
            {
                Assert.IsTrue(ls.TryGetLocationSet("all", out var ptr, out var count));
                Assert.AreEqual(10, count);
            }
        }
        [Test]
        public void LocationCount_IsExpectedValue()
        {
            using (var ls = CreateService(10))
                Assert.AreEqual(10, ls.LocationCount);
        }
        [Test]
        public void ResolvedLocations_MatchesExpectedLocations()
        {
            using (var ls = CreateService(10))
            {
                var locs = new NativeHashSet<RemoteContentLocation>(10, Allocator.Temp);
                Assert.IsTrue(ls.GetResolvedRemoteContentLocations(ref locs));
                Assert.AreEqual(10, locs.Count);
                var ids = new UnsafeList<RemoteContentId>(10, Allocator.Temp);
                Assert.IsTrue(ls.GetResolvedContentIds(ref ids));
                for (int i = 0; i < ids.Length; i++)
                {
                    var s = ls.ResolveLocation(ids[i]);
                    Assert.AreEqual(ContentLocationService.ResolvingState.Complete, s.State);
                    Assert.IsTrue(locs.Contains(s.Location));
                }
                ids.Dispose();
                locs.Dispose();
            }
        }

    }
}
