using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Content
{

    /// <summary>
    /// This class handles the overall process of delivering remote content to the client device.
    /// <seealso cref="ContentLocationService"/>s are used to resolve <seealso cref="RemoteContentId"/> into <seealso cref="RemoteContentLocation"/>s.
    /// The <seealso cref="ContentDownloadService"/> is used to download the data described by the <seealso cref="RemoteContentLocation"/>s.
    /// </summary>
    public class ContentDeliveryService : IDisposable
    {
        /// <summary>
        /// The state of the content delivery process.
        /// </summary>
        public enum DeliveryState
        {
            /// <summary>
            /// Content has not begun the dlivery process.
            /// </summary>
            None,
            /// <summary>
            /// The location is being resolved.
            /// </summary>
            ResolvingLocation,
            /// <summary>
            /// The location has been resolved.
            /// </summary>
            LocationResolved,
            /// <summary>
            /// The content is downloading.
            /// </summary>
            DownloadingContent,
            /// <summary>
            /// The content has sucessfully downloaded and is in the cache.
            /// </summary>
            ContentDownloaded,
            /// <summary>
            /// The delivery was cancelled.
            /// </summary>
            Cancelled,
            /// <summary>
            /// The delivery failed.
            /// </summary>
            Failed
        }

        /// <summary>
        /// The status of the content delivery process.
        /// </summary>
        public struct DeliveryStatus
        {
            /// <summary>
            /// The delivery state, which is determined from both the location resolving state and the download state.
            /// </summary>
            public DeliveryState State
            {
                get
                {
                    if (DownloadStatus.DownloadState == ContentDownloadService.State.Failed)
                        return DeliveryState.Failed;
                    if (DownloadStatus.DownloadState == ContentDownloadService.State.Cancelled)
                        return DeliveryState.Cancelled;
                    if (DownloadStatus.DownloadState == ContentDownloadService.State.Complete)
                        return DeliveryState.ContentDownloaded;
                    if (DownloadStatus.DownloadState == ContentDownloadService.State.Downloading)
                        return DeliveryState.DownloadingContent;
                    if (LocationStatus.State == ContentLocationService.ResolvingState.Failed)
                        return DeliveryState.Failed;
                    if (LocationStatus.State == ContentLocationService.ResolvingState.Complete)
                        return DeliveryState.LocationResolved;
                    if (LocationStatus.State == ContentLocationService.ResolvingState.Resolving)
                        return DeliveryState.ResolvingLocation;
                    return DeliveryState.None;
                }
            }

            /// <summary>
            /// The id of the remote content.  This is generally the hash of the relative path of the content file within the streaming assest folder.
            /// </summary>
            public RemoteContentId ContentId;

            /// <summary>
            /// The status of the location resolving process.
            /// </summary>
            public ContentLocationService.LocationStatus LocationStatus;

            /// <summary>
            /// The status of the downloading process.
            /// </summary>
            public ContentDownloadService.DownloadStatus DownloadStatus;
        }


        struct ContentSet : IDisposable
        {
            public UnsafeList<RemoteContentId> remoteIds;

            public void Dispose()
            {
                remoteIds.Dispose();
            }
        }
        SortedList<int, ContentLocationService> locationServices;
        SortedList<int, ContentDownloadService>  downloadServices;
        UnsafeHashMap<RemoteContentId, ContentSet> contentSets;
        UnsafeList<RemoteContentId> activeDownloads;
        UnsafeHashMap<RemoteContentId, DeliveryStatus> downloadStates;

        /// <summary>
        /// Enumeration of location services.
        /// </summary>
        public IEnumerable<ContentLocationService> LocationServices => locationServices.Values;

        /// <summary>
        /// Enumeration of download services.
        /// </summary>
        public IEnumerable<ContentDownloadService> DownloadServices => downloadServices.Values;

        struct DescendingOrderComparer : IComparer<int>
        {
            public int Compare(int x, int y) => y - x;
        }

        /// <summary>
        /// Construct a new delivery service.  In order to be functional, at least 1 locations service and 1 download service must be added.
        /// </summary>
        public ContentDeliveryService()
        {
            downloadStates = new UnsafeHashMap<RemoteContentId, DeliveryStatus>(8, Allocator.Persistent);
            activeDownloads = new UnsafeList<RemoteContentId>(8, Allocator.Persistent);
            contentSets = new UnsafeHashMap<RemoteContentId, ContentSet>(4, Allocator.Persistent);
            locationServices = new SortedList<int, ContentLocationService>(1, new DescendingOrderComparer());
            downloadServices = new SortedList<int, ContentDownloadService>(1, new DescendingOrderComparer());
        }

        /// <summary>
        /// Remaps the orignal content path to the path of the content in the local cache.
        /// </summary>
        /// <param name="originalPath">The original content path, relative to the streaming assets folder.</param>
        /// <returns>The remapped path in the local cache.  If the content is not already in the cache, the original path is returned.</returns>
        public string RemapContentPath(string originalPath)
        {
            var id = new RemoteContentId(originalPath);
            var status = ProcessDownload(id);
            ContentDeliveryGlobalState.LogFunc?.Invoke($"RemapContentPath id [{id.Name},{id.Hash}] with relative path {originalPath}, new path ={status.DownloadStatus.LocalPath}");

            if (status.State != DeliveryState.ContentDownloaded)
                return originalPath;
            return status.DownloadStatus.LocalPath.ToString();
        }

        /// <summary>
        /// Adds a download service.  The priority of the service will be used to set the order.
        /// If there is another service with the same priority, the priority of the service added will be increased until it can be added.
        /// When added, OnAddedToDeliveryService will be called on the service.
        /// If there is a service with the same name, the existing service will be replaced with the passed in service.
        /// </summary>
        /// <param name="service">The download service.</param>
        public void AddDownloadService(ContentDownloadService service)
        {
            foreach (var ds in downloadServices)
            {
                if (ds.Value.Name == service.Name)
                {
                    downloadServices.Remove(ds.Key);
                    break;
                }
            }

            while (!downloadServices.TryAdd(service.Priority, service))
                service.Priority++;
            service.OnAddedToDeliveryService(this);
            ContentDeliveryGlobalState.LogFunc?.Invoke($"Added download service {service.Name}");
        }

        ContentDownloadService GetDownloadServiceForLocation(in RemoteContentLocation location)
        {
            foreach (var dlSvc in downloadServices)
                if (dlSvc.Value.CanDownload(location))
                    return dlSvc.Value;
            return null;
        }

        /// <summary>
        /// Adds a location service. The priority of the service will be used to set the order.
        /// If there is another service with the same priority, the priority of the service added will be increased until it can be added.  When added, OnAddedToDeliveryService will be called on the service.
        /// </summary>
        /// <param name="service">The location service.</param>
        public void AddLocationService(ContentLocationService service)
        {
            foreach (var ds in locationServices)
            {
                if (ds.Value.Name == service.Name)
                {
                    locationServices.Remove(ds.Key);
                    ds.Value.Dispose();
                    break; 
                }
            }

            while (!locationServices.TryAdd(service.Priority, service))
                service.Priority++;
            service.OnAddedToDeliveryService(this);
            ContentDeliveryGlobalState.LogFunc?.Invoke($"Added location service {service.Name}");
        }

        /// <summary>
        /// Computes the size of content.
        /// </summary>
        /// <param name="entryCount">The total number of resolved locations in the service.</param>
        /// <param name="totalBytes">The total number of bytes of the data.</param>
        /// <param name="cachedBytes">The total number of bytes already cached by the download service.</param>
        /// <param name="uncachedBytes">The total number of bytes not cached by the download service.</param>
        /// <returns>Returns true if successful.</returns>
        public bool AccumulateContentSize(ref int entryCount, ref long totalBytes, ref long cachedBytes, ref long uncachedBytes)
        {
            var locations = new NativeHashSet<RemoteContentLocation>(32, Allocator.Temp);
            foreach (var svc in locationServices.Values)
                svc.GetResolvedRemoteContentLocations(ref locations);

            AccumulateContentSize(locations, ref entryCount, ref totalBytes, ref cachedBytes, ref uncachedBytes);
            locations.Dispose();
            return true;
        }

        /// <summary>
        /// Computes the size of the content set specified.
        /// </summary>
        /// <param name="setName">The content set to compute the size of.</param>
        /// <param name="entryCount">The total number of resolved locations in the service.</param>
        /// <param name="totalBytes">The total number of bytes of the data.</param>
        /// <param name="cachedBytes">The total number of bytes already cached by the download service.</param>
        /// <param name="uncachedBytes">The total number of bytes not cached by the download service.</param>
        unsafe public void AccumulateContentSize(in FixedString512Bytes setName, ref int entryCount, ref long totalBytes, ref long cachedBytes, ref long uncachedBytes)
        {
            var locations = new NativeHashSet<RemoteContentLocation>(32, Allocator.Temp);
            foreach (var svc in locationServices.Values)
            {
                if (svc.TryGetLocationSet(setName, out var idPtr, out var count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var id = idPtr[i];
                        locations.Add(svc.ResolveLocation(id).Location);
                    }
                }
            }
            AccumulateContentSize(locations, ref entryCount, ref totalBytes, ref cachedBytes, ref uncachedBytes);
            locations.Dispose();
        }

        void AccumulateContentSize(NativeHashSet<RemoteContentLocation> locations, ref int entryCount, ref long totalBytes, ref long cachedBytes, ref long uncachedBytes)
        {
            foreach (var l in locations)
            {
                entryCount++;
                totalBytes += l.Size;
                var status = GetDownloadServiceForLocation(l).GetDownloadStatus(in l);
                if (status.DownloadState == ContentDownloadService.State.Complete)
                    cachedBytes += l.Size;
                else
                    uncachedBytes += l.Size;
            }
        }

        /// <summary>
        /// Get the download statistics for a specified remote content id.
        /// </summary>
        /// <param name="id">The remote content id.</param>
        /// <param name="totalBytes">The total size of the content.</param>
        /// <param name="downloadedBytes">The number of bytes already downloaded.</param>
        /// <returns>True if successful, false if the id has not been resolved or is not known to the system.</returns>
        public bool AccumulateDownloadStats(in RemoteContentId id, ref long totalBytes, ref long downloadedBytes)
        {
            if (downloadStates.TryGetValue(id, out var status))
            {
                if (status.LocationStatus.State != ContentLocationService.ResolvingState.Complete)
                    return false;
                GetDownloadServiceForLocation(status.LocationStatus.Location).GetDownloadProgress(status.LocationStatus.Location, ref totalBytes, ref downloadedBytes);
                return true;
            }
            else if (contentSets.TryGetValue(id, out var set))
            {
                for (int i = 0; i < set.remoteIds.Length; i++)
                    AccumulateDownloadStats(set.remoteIds[i], ref totalBytes, ref downloadedBytes);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clears any data from the cache directory that is not referenced by a location service.
        /// </summary>
        public void CleanCache()
        {
            try
            {
                var locs = new NativeHashSet<RemoteContentLocation>(32, Allocator.Temp);
                foreach (var l in locationServices)
                    l.Value.GetResolvedRemoteContentLocations(ref locs);
                var validPaths = new HashSet<string>();
                foreach (var loc in locs)
                {
                    if (GetDownloadServiceForLocation(loc).GetLocalCacheFilePath(loc, out var cachePath))
                        validPaths.Add(cachePath);
                }
                locs.Dispose();
                int deletedFileCount = 0;
                foreach (var dlSvc in downloadServices)
                {
                    foreach (var file in Directory.GetFiles(dlSvc.Value.CacheRoot, "*.*", SearchOption.AllDirectories))
                    {
                        if (!validPaths.Contains(file))
                        {
                            ContentDeliveryGlobalState.LogFunc?.Invoke($"Deleting file {file} from cache");
                            File.Delete(file);
                            var dir = Path.GetDirectoryName(file);
                            if (Directory.GetFiles(dir).Length == 0)
                                Directory.Delete(dir, true);
                            deletedFileCount++;
                        }
                    }
                }
                ContentDeliveryGlobalState.LogFunc?.Invoke($"Cleaned cache, {deletedFileCount} files deleted.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Gets the delivery status of a remote id.  This version of this method should be used when retrieving the id of a set of downloads.
        /// </summary>
        /// <param name="id">The id of the delivery request.</param>
        /// <param name="results">The results of the status inquiry.  If the id refers to a set of requests, this array will contain multiple entries.</param>
        public void GetDeliveryStatus(in RemoteContentId id, ref NativeList<DeliveryStatus> results)
        {
            if (downloadStates.TryGetValue(id, out var status))
            {
                results.Add(status);
            }
            else if (contentSets.TryGetValue(id, out var set))
            {
                for (int i = 0; i < set.remoteIds.Length; i++)
                    GetDeliveryStatus(set.remoteIds[i], ref results);
            }
        }

        ContentLocationService.LocationStatus GetLocationStatus(in RemoteContentId id)
        {
            foreach (var ls in locationServices)
            {
                var s = ls.Value.GetLocationStatus(id);
                if (s.State != ContentLocationService.ResolvingState.None)
                    return s;
            }
            return new ContentLocationService.LocationStatus { State = ContentLocationService.ResolvingState.Failed };
        }

        ContentLocationService.LocationStatus ResolveLocation(in RemoteContentId id)
        {
            foreach (var ls in locationServices)
            {
                var s = ls.Value.ResolveLocation(id);
                if (s.State != ContentLocationService.ResolvingState.None)
                    return s;
            }
            return new ContentLocationService.LocationStatus { State = ContentLocationService.ResolvingState.Failed };
        }

        /// <summary>
        /// Cancels all active downloads.  This also marks all completed downloads as cancelled as well.  This will force any previously completed downloads to restart.
        /// </summary>
        public void CancelAllDeliveries()
        {
            foreach(var d in downloadStates)
                CancelDelivery(d.Key);

            foreach (var cs in contentSets)
                cs.Value.Dispose();
            contentSets.Clear();
        }

        /// <summary>
        /// Gets the delivery status of a remote id.  If the id refers to a single delivery, the status returned will be for that id.
        /// If it refers to a set of deliveries, the status will be aggregated to the lowest value of the set.
        /// </summary>
        /// <param name="id">The remote content id.</param>
        /// <returns>The status of the delivery.</returns>
        public DeliveryStatus GetDeliveryStatus(in RemoteContentId id)
        {
            if (id.IsValid)
            { 
                if (downloadStates.TryGetValue(id, out var status))
                {
                    if (status.State != DeliveryState.ContentDownloaded)
                    {
                        if (status.LocationStatus.State < ContentLocationService.ResolvingState.Complete)
                            status.LocationStatus = GetLocationStatus(id);
                        if (status.LocationStatus.State == ContentLocationService.ResolvingState.Complete)
                            status.DownloadStatus = GetDownloadServiceForLocation(status.LocationStatus.Location).GetDownloadStatus(status.LocationStatus.Location);
                        downloadStates[id] = status;
                    }
                    return status;
                }
                else if (contentSets.TryGetValue(id, out var set))
                {
                    var aggregateStatus = new DeliveryStatus { ContentId = id };
                    aggregateStatus.LocationStatus.State = ContentLocationService.ResolvingState.Failed;
                    aggregateStatus.DownloadStatus.DownloadState = ContentDownloadService.State.Failed;
                    for (int i = 0; i < set.remoteIds.Length; i++)
                    {
                        var s = GetDeliveryStatus(set.remoteIds[i]);
                        aggregateStatus.DownloadStatus.BytesDownloaded += s.DownloadStatus.BytesDownloaded;
                        if (s.DownloadStatus.DownloadState < aggregateStatus.DownloadStatus.DownloadState)
                            aggregateStatus.DownloadStatus.DownloadState = s.DownloadStatus.DownloadState;
                        aggregateStatus.LocationStatus.Location.Size += s.LocationStatus.Location.Size;
                        if (s.LocationStatus.State < aggregateStatus.LocationStatus.State)
                            aggregateStatus.LocationStatus.State = s.LocationStatus.State;
                    }
                    return aggregateStatus;
                }
            }
            return new DeliveryStatus { ContentId = id };
        }

        /// <summary>
        /// Starts the process of delivering content based on its location.
        /// </summary>
        /// <param name="url">The url of the remote content.</param>
        /// <param name="hash">The hash of the remote content.  If not set, the cache will not be used.</param>
        /// <param name="size">The expected size of the remote content.  If not set, accurate download progress is not available.</param>
        /// <param name="crc">The crc of the content.  If not set, the crc is not checked.</param>
        /// <returns>The remote content id for the content that is downloaded.  This id can be used to check the status of the download.</returns>
        public RemoteContentId DeliverContent(string url, Hash128 hash, long size, uint crc = 0)
        {
            return DeliverContent(new RemoteContentLocation { Path = url, Hash = hash, Size = size, Crc = crc });
        }

        /// <summary>
        /// Starts the process of delivering content based on its location.
        /// </summary>
        /// <param name="loc">The location of the content.</param>
        /// <returns>The generated remote content id for the location.  This can be used to track the progress.</returns>
        unsafe public RemoteContentId DeliverContent(in RemoteContentLocation loc)
        {
            var dlSvc = GetDownloadServiceForLocation(loc);
            var id = new RemoteContentId(loc.Path);
            if (!loc.Hash.IsValid && downloadStates.ContainsKey(id))
            {
                downloadStates.Remove(id);
                var i = activeDownloads.IndexOf(id);
                if (i >= 0)
                    activeDownloads.RemoveAtSwapBack(i);
                dlSvc.CancelDownload(loc);
            }
            if (!downloadStates.ContainsKey(id))
            {
                var status = new DeliveryStatus { ContentId = id };
                status.LocationStatus = new ContentLocationService.LocationStatus { Location = loc, State = ContentLocationService.ResolvingState.Complete };
                status.DownloadStatus = dlSvc.DownloadContent(loc);
                downloadStates[id] = status;
                activeDownloads.Add(id);
            }
            return id;
        }

        /// <summary>
        /// Cancel an in progress download. This will remove the download state and attempt to cancel the download if it is still inprogress or queued.
        /// If the download has already completed, the cached data will not be removed.
        /// </summary>
        /// <param name="id">The remote content id to cancel.</param>
        /// <returns>False if the id is not known. True otherwise.</returns>
        public bool CancelDelivery(in RemoteContentId id)
        {
            ContentDeliveryGlobalState.LogFunc?.Invoke($"Cancelling content delivery for {id}.");

            var status = GetDeliveryStatus(id);
            if (status.State == DeliveryState.None)
                return false;
            var i = activeDownloads.IndexOf(id);
            if (i >= 0)
                activeDownloads.RemoveAtSwapBack(i);
            if (status.State >= DeliveryState.LocationResolved)// && status.State < DeliveryState.ContentDownloaded)
                GetDownloadServiceForLocation(status.LocationStatus.Location).CancelDownload(status.LocationStatus.Location);
            status.DownloadStatus.DownloadState = ContentDownloadService.State.Cancelled;
            downloadStates[id] = status;
            return true;
        }

        /// <summary>
        /// Starts the delivery process for content identified by the remote content id.
        /// </summary>
        /// <param name="id">The remote content id for the content.</param>
        public void DeliverContent(in RemoteContentId id)
        {
            ContentDeliveryGlobalState.LogFunc?.Invoke($"Delivering content for {id}.");

            if (downloadStates.TryGetValue(id, out var status))
            {
                if(status.State == DeliveryState.Failed || status.State == DeliveryState.Cancelled)
                    downloadStates.Remove(id);
            }
            if (!downloadStates.ContainsKey(id))
            {
                ProcessDownload(id);
                activeDownloads.Add(id);
            }
        }

        /// <summary>
        /// Delivers a named content set.
        /// </summary>
        /// <param name="setName">The name of the content set.  These are defined during the publishing process.</param>
        /// <returns>The remote id for the content set.  This can be used to check the download status.</returns>
        unsafe public RemoteContentId DeliverContent(in FixedString512Bytes setName)
        {
            ContentDeliveryGlobalState.LogFunc?.Invoke($"Delivering content set for {setName}.");
            foreach (var ls in locationServices)
            {
                if (ls.Value.TryGetLocationSet(setName, out var idPtr, out var count))
                    return DeliverContent(idPtr, count);
            }
            return default;
        }

        /// <summary>
        /// Downloads multiple files as a single set.
        /// </summary>
        /// <param name="remoteIds">A pointer to the array of remote content ids.</param>
        /// <param name="length">The number of remote content ids.</param>
        /// <returns>The remote id for the content set.  This can be used to check the download status.</returns>
        unsafe public RemoteContentId DeliverContent(RemoteContentId* remoteIds, int length)
        {
            var id = GetRemoteContentIdentifier(remoteIds, length);
            if (!contentSets.ContainsKey(id))
            {
                var ids = new UnsafeList<RemoteContentId>(length, Allocator.Persistent);
                ids.AddRangeNoResize(remoteIds, length);
                for (int i = 0; i < length; i++)
                    DeliverContent(remoteIds[i]);
                var cs = new ContentSet { remoteIds = ids };
                contentSets.Add(id, cs);
            }
            return id;
        }
        /// <summary>
        /// Starts the delivery process of a set of contents.
        /// </summary>
        /// <param name="remoteIds">The set of remote ids to deliver.</param>
        /// <returns>A generated remote content id that can be used to track the progress of the delivery.</returns>
        unsafe public RemoteContentId DeliverContent(in UnsafeList<RemoteContentId> remoteIds) => DeliverContent(remoteIds.Ptr, remoteIds.Length);

        unsafe private RemoteContentId GetRemoteContentIdentifier(RemoteContentId* ids, int length)
        {
            var validIds = new List<RemoteContentId>(length);
            for (int i = 0; i < length; i++)
            {
                var id = ids[i];
                if (id.IsValid)
                    validIds.Add(id);
            }
            if (validIds.Count == 0)
                return default;
            var hash = UnityEngine.Hash128.Compute(validIds);
            return new RemoteContentId(hash.ToString(), hash);
        }

        DeliveryStatus ProcessDownload(in RemoteContentId id)
        {
            var status = GetDeliveryStatus(id);
            if (status.State == DeliveryState.None)
            {
                status.LocationStatus = ResolveLocation(id);
                downloadStates[id] = status;
            }
            if (status.State == DeliveryState.LocationResolved)
            {
                status.DownloadStatus = GetDownloadServiceForLocation(status.LocationStatus.Location).DownloadContent(status.LocationStatus.Location);
                downloadStates[id] = status;
            }
            return status;
        }

        /// <summary>
        /// Process active content deliveries.
        /// </summary>
        public void Process()
        {
            foreach (var ls in locationServices)
                ls.Value.Process();
            foreach(var ds in downloadServices)
                ds.Value.Process();
            for(int i = activeDownloads.Length - 1; i >= 0; --i)
            {
                var id = activeDownloads[i];
                var status = ProcessDownload(id);
                if (status.State >= DeliveryState.ContentDownloaded)
                    activeDownloads.RemoveAtSwapBack(i);
            }
        }

        /// <summary>
        /// Free internal resources.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (locationServices != null)
                {
                    foreach (var ls in locationServices)
                        ls.Value.Dispose();
                    locationServices = null;
                }
                if (downloadServices != null)
                {
                    foreach (var ds in downloadServices)
                        ds.Value.Dispose();
                    downloadServices = null;
                }
                if (contentSets.IsCreated)
                {
                    foreach (var cs in contentSets)
                        cs.Value.Dispose();
                    contentSets.Dispose();
                }
                if (downloadStates.IsCreated)
                    downloadStates.Dispose();
                if(activeDownloads.IsCreated)
                    activeDownloads.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
