using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Content
{
    internal class DefaultContentLocationService : ContentLocationService
    {
        UnsafeHashMap<FixedString512Bytes, UnsafeList<RemoteContentId>> contentSets;
        UnsafeHashMap<RemoteContentId, RemoteContentLocation> contentLocations;

        public override int LocationCount => contentLocations.Count;

        public DefaultContentLocationService(string name, int priority, string catalogPath, Func<string, string> remotePathFunc)
        {
            Priority = priority;
            Name = name;
            if (BlobAssetReference<RemoteContentCatalogData>.TryRead(catalogPath, 1, out var catalog))
            {
                int count = catalog.Value.RemoteContentLocations.Length;
                contentLocations = new UnsafeHashMap<RemoteContentId, RemoteContentLocation>(count, Allocator.Persistent);
                for (int ti = 0; ti < count; ti++)
                {
                    var data = catalog.Value.RemoteContentLocations[ti];
                    if (remotePathFunc != null)
                        data.location.Path = remotePathFunc(data.location.Path.ToString());
                    contentLocations.Add(data.identifier, data.location);
                }

                contentSets = new UnsafeHashMap<FixedString512Bytes, UnsafeList<RemoteContentId>>(catalog.Value.ContentSets.Length, Allocator.Persistent);
                for (int i = 0; i < catalog.Value.ContentSets.Length; i++)
                {
                    var ids = new UnsafeList<RemoteContentId>(catalog.Value.ContentSets[i].Ids.Length, Allocator.Persistent);
                    ref var blobIds = ref catalog.Value.ContentSets[i].Ids;
                    for (int j = 0; j < blobIds.Length; j++)
                        ids.Add(catalog.Value.RemoteContentLocations[blobIds[j]].identifier);
                    contentSets.Add(catalog.Value.ContentSets[i].Name, ids);
                }
                catalog.Dispose();
            }
        }

        internal DefaultContentLocationService(string name, int priority, int count, Func<int, (RemoteContentId, RemoteContentLocation)> indexer, Func<string, string> remotePathFunc, Func<string, IEnumerable<string>> contentSetFunc)
        {
            Priority = priority;
            Name = name;
            var newContentSets = new Dictionary<string, List<RemoteContentId>>();
            contentLocations = new UnsafeHashMap<RemoteContentId, RemoteContentLocation>(count, Allocator.Persistent);
            for (int ti = 0; ti < count; ti++)
            {
                var loc = indexer(ti);
                if (remotePathFunc != null)
                    loc.Item2.Path = remotePathFunc(loc.Item2.Path.ToString());
                contentLocations.Add(loc.Item1, loc.Item2);
                if (contentSetFunc != null)
                {
                    var cs = contentSetFunc(loc.Item1.Name.ToString());
                    foreach (var s in cs)
                    {
                        if (!newContentSets.TryGetValue(s, out var set))
                            newContentSets.Add(s, set = new List<RemoteContentId>());
                        set.Add(loc.Item1);
                    }
                }
            }
            contentSets = new UnsafeHashMap<FixedString512Bytes, UnsafeList<RemoteContentId>>(newContentSets.Count, Allocator.Persistent);
            foreach (var k in newContentSets)
            {
                var ids = new UnsafeList<RemoteContentId>(k.Value.Count, Allocator.Persistent);
                foreach (var i in k.Value)
                    ids.Add(i);
                contentSets.Add(k.Key, ids);
            }
        }

        unsafe public override bool TryGetLocationSet(in FixedString512Bytes setName, out RemoteContentId* idPtr, out int count)
        {
            if (contentSets.TryGetValue(setName, out var ids))
            {
                idPtr = ids.Ptr;
                count = ids.Length;
                return true;
            }
            idPtr = default;
            count = default;
            return false;
        }

        public override void Dispose()
        {
            if (contentLocations.IsCreated)
                contentLocations.Dispose();
            if (contentSets.IsCreated)
            {
                foreach (var cs in contentSets)
                    cs.Value.Dispose();
                contentSets.Dispose();
            }
        }

        public override LocationStatus GetLocationStatus(RemoteContentId id)
        {
            if (!contentLocations.TryGetValue(id, out var loc))
                return new LocationStatus { Location = default, State = ResolvingState.None };

            return new LocationStatus { Location = loc, State = ResolvingState.Complete };
        }

        public override LocationStatus ResolveLocation(RemoteContentId id)
        {
            return GetLocationStatus(id);
        }


        public override bool GetResolvedContentIds(ref UnsafeList<RemoteContentId> ids)
        {
            var e = contentLocations.GetEnumerator();
            while (e.MoveNext())
                ids.Add(e.Current.Key);
            return true;
        }


        public override bool GetResolvedRemoteContentLocations(ref NativeHashSet<RemoteContentLocation> locs)
        {
            var e = contentLocations.GetEnumerator();
            while (e.MoveNext())
                locs.Add(e.Current.Value);
            return true;
        }
    }
}
