using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine.Networking;

namespace Unity.Entities.Content
{
    /// <summary>
    /// Class responsible for managing active content downloads.
    /// </summary>
    public class ContentDownloadService : IDisposable
    {
        /// <summary>
        /// The state of a content download.
        /// </summary>
        public enum State
        {
            /// <summary>
            /// The download has not been requested.
            /// </summary>
            None,
            /// <summary>
            /// The download is in the queue to be processed.
            /// </summary>
            Queued,
            /// <summary>
            /// The download is in progress.
            /// </summary>
            Downloading,
            /// <summary>
            /// The download is completed and is successful.
            /// </summary>
            Complete,
            /// <summary>
            /// The download was cancelled.
            /// </summary>
            Cancelled,
            /// <summary>
            /// The download failed.
            /// </summary>
            Failed
        }

        /// <summary>
        /// The status of a content download.  This contains the state, progress, and the final local path of a successful download.
        /// </summary>
        public struct DownloadStatus
        {
            /// <summary>
            /// The state of the download process.
            /// </summary>
            public State DownloadState;
            /// <summary>
            /// The current number of bytes downloaded.
            /// </summary>
            public long BytesDownloaded;
            /// <summary>
            /// When complete, the local path of the file that was downloaded.
            /// </summary>
            public FixedString512Bytes LocalPath;
        }

        /// <summary>
        /// Abstraction for the individual download operations.  These can be subclassed for custom implementations of downloading.
        /// </summary>
        public abstract class DownloadOperation
        {
            string tempDownloadPath;
            string finalDownloadPath;
            /// <summary>
            /// True if the operation has been cancelled.
            /// </summary>
            public bool IsCancelled { get; private set; }
            /// <summary>
            /// The location of the operation.
            /// </summary>
            public RemoteContentLocation Location { get; private set; }
            /// <summary>
            /// True if the operation has been started.  This will be false if the operation is queued.
            /// </summary>
            public bool IsStarted { get; private set; }

            /// <summary>
            /// Initialize the download operation with the information needed to start.
            /// </summary>
            /// <param name="loc">The content location.</param>
            /// <param name="tmpPath">The temporary path to download to.</param>
            /// <param name="finalPath">The final path to copy to once the download completes.</param>
            public void Init(RemoteContentLocation loc, string tmpPath, string finalPath)
            {
                Location = loc;
                tempDownloadPath = tmpPath;
                finalDownloadPath = finalPath;
            }

            /// <summary>
            /// Process the state of the operation.
            /// </summary>
            /// <param name="status">The status of the operation.  This may be modified if the status changes.</param>
            /// <param name="downloadedBytes">The total number of bytes downloaded of the content.</param>
            /// <returns>True if the operation is complete, false if more processing is required.</returns>
            public bool Process(ref DownloadStatus status, ref long downloadedBytes)
            {
                if (IsCancelled)
                {
                    status.DownloadState = State.Cancelled;
                    return true;
                }
                string error = null;
                if (ProcessDownload(ref downloadedBytes, ref error))
                {
                    if (File.Exists(finalDownloadPath))
                        File.Delete(finalDownloadPath);
                    if (string.IsNullOrEmpty(error))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(finalDownloadPath));
                        File.Move(tempDownloadPath, finalDownloadPath);
                        status.BytesDownloaded = downloadedBytes;
                        status.LocalPath = finalDownloadPath;
                        status.DownloadState = State.Complete;
                    }
                    else
                    {
                        if (File.Exists(tempDownloadPath))
                            File.Delete(tempDownloadPath);
                        status.DownloadState = State.Failed;
                    }
                    return true;
                }
                return false;
            }
            /// <summary>
            /// Called when the operation can begin downloading it data.
            /// </summary>
            /// <param name="remotePath">The remote path of the data.  This is typically a url.</param>
            /// <param name="localTmpPath">The local path to download the data to.  This file will be moved to the final path once completed.</param>
            protected abstract void StartDownload(string remotePath, string localTmpPath);

            /// <summary>
            /// Called to process the download operation.
            /// </summary>
            /// <param name="downloadedBytes">The total number of bytes downloaded.</param>
            /// <param name="error">This will be set to the error string if the operation fails.</param>
            /// <returns>True if processing is completed, false otherwise.</returns>
            protected abstract bool ProcessDownload(ref long downloadedBytes, ref string error);

            /// <summary>
            /// Called when the download is cancelled.
            /// </summary>
            protected abstract void CancelDownload();

            /// <summary>
            /// Starts the download operation.
            /// </summary>
            /// <param name="status">The status will be updated when Start is called.</param>
            public void Start(ref DownloadStatus status)
            {
                if (IsCancelled)
                {
                    status.DownloadState = State.Cancelled;
                }
                else
                {
                    status.DownloadState = State.Downloading;
                    StartDownload(Location.Path.ToString(), tempDownloadPath);
                    IsStarted = true;
                }
            }

            /// <summary>
            /// Cancel the download if possible.
            /// </summary>
            public void Cancel()
            {
                IsCancelled = true;
                CancelDownload();
            }
        }

        LinkedList<DownloadOperation> activeDownloads;
        Dictionary<RemoteContentLocation, DownloadStatus> downloadStates;
        int maxActive = 5;
        Func<DownloadOperation> createOpFunc;
        long activeDownloadedBytes = 0;
        long completeDownloadedBytes = 0;


        /// <summary>
        /// The root directory of the local cache.
        /// </summary>
        public string CacheRoot { get; protected set; }
        /// <summary>
        /// The download service name. Each service name must be unique.
        /// </summary>
        public string Name {get; protected set;}
        /// <summary>
        /// The priority of the service. Higher values will place it at the front of the service list.
        /// </summary>
        public int Priority { get; set; }
        /// <summary>
        /// Total bytes downloaded.
        /// </summary>
        /// <remarks>
        /// If content is loaded from the cache, this value isn't affected. This value is reset when <see cref="ClearDownloadProgress"/> is called.
        /// </remarks>
        public long TotalDownloadedBytes => activeDownloadedBytes + completeDownloadedBytes;
        /// <summary>
        /// Total bytes processed.
        /// </summary>
        /// <remarks>
        /// This value contains the total size of content even if it has already been cached. This value is reset when <see cref="ClearDownloadProgress"/> is called.
        /// </remarks>
        public long TotalBytes { get; private set; }

        /// <summary>
        /// Construct a download service.
        /// </summary>
        /// <param name="name">The name of the service. Each name must be unique.</param>
        /// <param name="cacheDir">The root directory of the local cache.</param>
        /// <param name="priority">The priority of the service. Higher values are placed at the front of the service list.</param>
        /// <param name="maxActiveDownloads">The maximum allowed concurrent downloads. When there are more requests than can be run concurrently, they are queued until some of the active operations complete.</param>
        /// <param name="createDownloadOpFunc">Allows for specifying a custom type of DownloadOperation. By default, this will use UnityWebRequest.</param>
        public ContentDownloadService(string name, string cacheDir, int priority = 1, int maxActiveDownloads = 5, Func<DownloadOperation> createDownloadOpFunc = null)
        {
            Name = name;
            Priority = priority;
            maxActive = maxActiveDownloads;
            CacheRoot = cacheDir;
            activeDownloads = new LinkedList<DownloadOperation>();
            downloadStates = new Dictionary<RemoteContentLocation, DownloadStatus>();
            createOpFunc = createDownloadOpFunc == null ? () => new DownloadOperationUnityWebRequest() : createDownloadOpFunc;
            Directory.CreateDirectory(cacheDir);
        }

        /// <summary>
        /// Release up internal resources
        /// </summary>
        public void Dispose() { }

        /// <summary>
        /// Called when added to the content delivery service.
        /// </summary>
        /// <param name="contentDeliveryService">The content delivery service this service is being added to.</param>
        public void OnAddedToDeliveryService(ContentDeliveryService contentDeliveryService){}

        /// <summary>
        /// Called when a download needs to be cancelled.  This is not guaranteed to cancel the operation.
        /// </summary>
        /// <param name="loc">The location to cancel.</param>
        public void CancelDownload(RemoteContentLocation loc)
        {
            if (downloadStates.TryGetValue(loc, out DownloadStatus status))
            {
                status.DownloadState = State.Cancelled;
                downloadStates[loc] = status;
            }
            
            var node = activeDownloads.First;
            while (node != null)
            {
                var job = node.Value;
                var next = node.Next;
                if (job.Location.Equals(loc))
                {
                    job.Cancel();
                    activeDownloads.Remove(node);
                }
                node = next;
            }
        }

        /// <summary>
        /// Gets the download status for a specific location.  If the content is cached, this will return a completed status even if the content was not explicitly requested.
        /// </summary>
        /// <param name="loc">The location of the content.</param>
        /// <returns>The current status of the content delivery.</returns>
        public DownloadStatus GetDownloadStatus(in RemoteContentLocation loc)
        {
            if (!downloadStates.TryGetValue(loc, out var status))
            {
                if (GetLocalCacheFilePath(loc, out var cachePath))
                {
                    status = new DownloadStatus { BytesDownloaded = loc.Size, LocalPath = cachePath, DownloadState = State.Complete };
                    downloadStates[loc] = status;
                }
            }
            return status;
        }

        /// <summary>
        /// Used to determine which download service to use to download content.  Each service is checked in order until a servce returns true.
        /// </summary>
        /// <param name="location">The location of the content.</param>
        /// <returns>True if the service can download this specific location.</returns>
        public bool CanDownload(RemoteContentLocation location)
        {
            return location.Type == RemoteContentLocation.LocationType.RemoteURL;
        }

        /// <summary>
        /// Computes the local path of the content in the cache.
        /// </summary>
        /// <param name="loc">The content location.</param>
        /// <returns>The local path of content.  This does not imply that the content actually exists in the cache.  Use File.Exists or <seealso cref="GetDownloadStatus"/> to determine if the content is cached.</returns>
        public string ComputeCachePath(RemoteContentLocation loc)
        {
            if (!loc.Hash.IsValid)
                return String.Empty;
            var hash128 = loc.Hash.ToString();
            var sb = new StringBuilder(CacheRoot, CacheRoot.Length + hash128.Length + 4);
            sb.Append(Path.DirectorySeparatorChar);
            sb.Append(hash128[0]);
            sb.Append(hash128[1]);
            sb.Append(Path.DirectorySeparatorChar);
            sb.Append(hash128);
            return sb.ToString();
        }

        /// <summary>
        /// Gets the local cache file path for a location and checks to see if it exists.
        /// </summary>
        /// <param name="loc">The content location.</param>
        /// <param name="path">The local cache path.  This will be set regardless if the cached file exists.</param>
        /// <returns>True if the cached file exists, otherwise false.</returns>
        public bool GetLocalCacheFilePath(RemoteContentLocation loc, out string path)
        {
            return File.Exists(path = ComputeCachePath(loc));
        }

        /// <summary>
        /// Resets the download statistics.
        /// </summary>
        public void ClearDownloadProgress()
        {
            TotalBytes = activeDownloadedBytes = completeDownloadedBytes = 0;
        }

        /// <summary>
        /// Gets the downlaod progress for a specific location.
        /// </summary>
        /// <param name="loc">The content location.</param>
        /// <param name="contentSize">The total bytes of the content.</param>
        /// <param name="downloadedBytes">The number of bytes downloaded so far.</param>
        public void GetDownloadProgress(RemoteContentLocation loc, ref long contentSize, ref long downloadedBytes)
        {
            contentSize += loc.Size;
            if (downloadStates.TryGetValue(loc, out var s))
                downloadedBytes += s.BytesDownloaded;
        }

        /// <summary>
        /// Starts the process of downloading content.
        /// </summary>
        /// <param name="loc">The content location.</param>
        /// <returns>The status of the download operation.  If the content is cached, this will return a complete status.</returns>
        unsafe public DownloadStatus DownloadContent(in RemoteContentLocation loc)
        {
            if (downloadStates.TryGetValue(loc, out var status))
            {
                if(status.DownloadState == State.Cancelled || status.DownloadState == State.Failed)
                    downloadStates.Remove(loc);
            }
            if (!downloadStates.TryGetValue(loc, out var s))
            {
                TotalBytes += loc.Size;
                if (!loc.Hash.IsValid)
                {
                    var op = createOpFunc();
                    var path = Path.Combine(UnityEngine.Application.persistentDataPath, Guid.NewGuid().ToString());
                    op.Init(loc, $"{path}.tmpdownload", path);
                    activeDownloads.AddLast(op);
                    s.DownloadState = State.Queued;
                }
                else
                {
                    if (GetLocalCacheFilePath(loc, out var cachePath))
                    {
                        File.SetLastAccessTime(cachePath, DateTime.Now);
                        s.DownloadState = State.Complete;
                        s.BytesDownloaded = loc.Size;
                        s.LocalPath = cachePath;
                    }
                    else
                    {
                        var op = createOpFunc();
                        op.Init(loc, $"{cachePath}.tmpdownload", cachePath);
                        activeDownloads.AddLast(op);
                        s.DownloadState = State.Queued;
                    }
                }
                downloadStates[loc] = s;
                Process();
            }
            return downloadStates[loc];
        }

        /// <summary>
        /// Processes active downloads and updates status.
        /// </summary>
        public void Process()
        {
            activeDownloadedBytes = 0;
            int activeCount = 0;
            var node = activeDownloads.First;
            while (node != null)
            {
                var job = node.Value;
                var next = node.Next;
                var status = downloadStates[job.Location];
                if (!job.IsStarted)
                {
                    if (activeCount < maxActive)
                    {
                        job.Start(ref status);
                        activeCount++;
                    }
                }

                if (job.IsStarted)
                {
                    long downloadedBytes = 0;
                    if (job.Process(ref status, ref downloadedBytes))
                    {
                        activeDownloads.Remove(node);
                        completeDownloadedBytes += downloadedBytes;
                    }
                    else
                    {
                        activeCount++;
                        activeDownloadedBytes += downloadedBytes;
                    }
                }
                downloadStates[job.Location] = status;
                node = next;
            }
        }
    }

    //default implementation that uses unitywebrequest for downloading
    internal class DownloadOperationUnityWebRequest : ContentDownloadService.DownloadOperation
    {
        string targetPath;
        UnityWebRequestAsyncOperation operation;

        protected override void StartDownload(string remotePath, string localTmpPath)
        {
            targetPath = localTmpPath;
            var dlHandler = new DownloadHandlerFile(localTmpPath, false);
            dlHandler.removeFileOnAbort = true;
            var req = new UnityWebRequest(remotePath, UnityWebRequest.kHttpVerbGET, dlHandler, null);
            operation = req.SendWebRequest();
        }


        protected override bool ProcessDownload(ref long downloadedBytes, ref string error)
        {
            downloadedBytes = (long)operation.webRequest.downloadedBytes;
            if (operation.isDone)
            {
                error = operation.webRequest.error;
                operation.webRequest.Dispose();
                return true;
            }
            return false;
        }

        protected override void CancelDownload()
        {
            if (operation != null && !operation.webRequest.isDone)
            {
                operation.webRequest.Abort();
                operation.webRequest.Dispose();
                if (File.Exists(targetPath))
                    File.Delete(targetPath);
            }
        }
    }

    //implementation that uses HttpClient, mainly to validate API
    internal class DownloadOperationHttpClient : ContentDownloadService.DownloadOperation
    {
        HttpClient httpClient;
        Task<Stream> downloadTask;
        Task writeTask;
        FileStream writeStream;
        string targetPath;
        public DownloadOperationHttpClient(HttpClient client)
        {
            httpClient = client;
        }

        protected override void StartDownload(string remotePath, string localTmpPath)
        {
            targetPath = localTmpPath;
            downloadTask = httpClient.GetStreamAsync(remotePath);
        }

        protected override void CancelDownload()
        {
        }

        protected override bool ProcessDownload(ref long downloadedBytes, ref string error)
        {
            if (downloadTask.IsCompleted)
            {
                if (writeStream == null)
                {
                    writeStream = File.OpenWrite(targetPath);
                    writeTask = downloadTask.Result.CopyToAsync(writeStream);
                }
                if (writeTask.IsCompleted)
                {
                    downloadedBytes = writeStream.Length;
                    writeStream.Dispose();
                    downloadTask.Result.Dispose();
                    return true;
                }
            }
            return false;
        }
    }
}
