using System;
using System.IO;
using Unity.Collections;
using Unity.Entities.Serialization;
using UnityEngine;

namespace Unity.Entities.Content
{
    /// <summary>Contains methods for the content update process.</summary>
    public static class ContentDeliveryGlobalState
    {
        /// <summary>
        /// Action to log content update progress.
        /// </summary>
        public static Action<string> LogFunc = null;

        /// <summary>
        /// The name of the content set for the catalogs.
        /// </summary>
        public const string kCatalogLocations = "catalogs";

        /// <summary>
        /// States of content update process.
        /// </summary>
        public enum ContentUpdateState
        {
            /// <summary>
            /// Content update process has not started.
            /// </summary>
            None,
            /// <summary>
            /// Downloading uncached catalog info - this happens each time the application is started to check for updated catalogs.
            /// </summary>
            DownloadingCatalogInfo,
            /// <summary>
            /// Downloading catalogs.  
            /// </summary>
            DownloadingCatalogs,
            /// <summary>
            /// Downloading initial content sets.
            /// </summary>
            DownloadingContentSet,
            /// <summary>
            /// The content update failed and there is no data in the cache.
            /// </summary>
            NoContentAvailable,
            /// <summary>
            /// Content is ready.  It may be local, updated from remote, or from the cache.  
            /// </summary>
            ContentReady,
            /// <summary>
            /// The content will be loaded from the streaming assets folder and no attempt to update will be made.
            /// </summary>
            UsingContentFromStreamingAssets,
            /// <summary>
            /// The content has successfully been updated from the remote content server.
            /// </summary>
            ContentUpdatedFromRemote,
            /// <summary>
            /// The content update failed, content will be loaded from the local cache if possible.
            /// </summary>
            UsingContentFromCache
        }

        /// <summary>
        /// Functor used to remap content into the local cache.
        /// </summary>
        public static Func<string, string> PathRemapFunc;

        /// <summary>
        /// The current state of the content update process.
        /// </summary>
        public static ContentUpdateState CurrentContentUpdateState => contentUpdateState;

        static Action<ContentUpdateState> OnContentReady;
        static ContentUpdateState contentUpdateState = ContentUpdateState.None;
        static ContentUpdateContext contentUpdateContext;
        static ContentDeliveryService contentDeliveryService;

        internal static void Cleanup()
        {
            contentDeliveryService?.Dispose();
        }

        /// <summary>
        /// Register for content update state changes.
        /// </summary>
        /// <param name="updateStateFunc">The action that will be called for each content update state change.</param>
        public static void RegisterForContentUpdateCompletion(Action<ContentUpdateState> updateStateFunc)
        {
            if (updateStateFunc != null)
            {
                if (contentUpdateState >= ContentUpdateState.ContentReady)
                    updateStateFunc(contentUpdateState);
                else
                    OnContentReady += updateStateFunc;
            }
        }

        /// <summary>
        /// Initialize the content delivery system.
        /// </summary>
        /// <param name="remoteUrlRoot">The remote url root in the form of "https://hostname.com/pathToContent/".  If this parameter is not specified, content is assumed to be local.</param>
        /// <param name="cachePath">The full local path of the content cache.  This must be a directory that the application has read and write access to.</param>
        /// <param name="initialContentSet">The initial set of content to download.  The content sets are given names during the publish process and by default everything is added to the "all" set.</param>
        /// <param name="updateStateFunc">Callback action that will get called whenever the content update state changes.</param>
        public static void Initialize(string remoteUrlRoot, string cachePath, string initialContentSet, Action<ContentUpdateState> updateStateFunc)
        {
            if (string.IsNullOrEmpty(remoteUrlRoot))
            {
                contentUpdateState = ContentUpdateState.UsingContentFromStreamingAssets;
                PathRemapFunc = p => $"{Application.streamingAssetsPath}/{p}";
            }
            else
            {
                contentUpdateState = ContentUpdateState.None;
                contentUpdateContext = new ContentUpdateContext()
                {
                    remoteUrlRoot = remoteUrlRoot,
                    cachePath = cachePath,
                    initialContentSet = initialContentSet
                };

                contentDeliveryService = new ContentDeliveryService();
                PathRemapFunc = contentDeliveryService.RemapContentPath;
                contentDeliveryService.AddDownloadService(new ContentDownloadService("default", cachePath, 1, 5, null));
            }
            RegisterForContentUpdateCompletion(updateStateFunc);
        }

        internal static void Update()
        {
            if (contentDeliveryService == null)
                return;
            contentDeliveryService.Process();

            if (contentUpdateContext == null)
                return;

            ContentUpdateState state = contentUpdateState;
            if (contentUpdateContext.Update(contentDeliveryService, ref state))
            {
                //update context is not needed anymore
                contentUpdateContext = null;

                //no connection and no cached data, use local data in streaming assets
                if (state == ContentUpdateState.UsingContentFromStreamingAssets)
                {
                    contentDeliveryService.Dispose();
                    contentDeliveryService = null;
                    PathRemapFunc = p => $"{Application.streamingAssetsPath}/{p}";
                }
            }

            //if the state changes, save new state and invoke callbacks
            if (state != contentUpdateState)
            {
                contentUpdateState = state;
                if (OnContentReady != null)
                {
                    OnContentReady(contentUpdateState);
                    if (contentUpdateState >= ContentUpdateState.ContentReady)
                        OnContentReady = null;
                }
            }
        }

        internal static string CreateCatalogLocationPath(string streamingAssetsPath) => Path.Combine(streamingAssetsPath, $"{kCatalogLocations}.bin");

        internal unsafe static bool FileExists(string path)
        {
#if UNITY_EDITOR
            return System.IO.File.Exists(path);
#else
                IO.LowLevel.Unsafe.FileInfoResult result;
                var readHandle = IO.LowLevel.Unsafe.AsyncReadManager.GetFileInfo(path, &result);
                readHandle.JobHandle.Complete();
                return result.FileState == IO.LowLevel.Unsafe.FileState.Exists;
#endif
        }


        internal class ContentUpdateContext
        {
            public string remoteUrlRoot;
            public string initialContentSet;
            public string cachePath;
            RemoteContentId currentRemoteId;

            public bool Update(ContentDeliveryService contentDeliveryService, ref ContentUpdateState currentUpdateState)
            {
                if (currentUpdateState == ContentUpdateState.None)
                {
                    LogFunc?.Invoke($"Downloading remote catalog info from {remoteUrlRoot}{kCatalogLocations}.bin");
                    currentRemoteId = contentDeliveryService.DeliverContent($"{remoteUrlRoot}{kCatalogLocations}.bin", default, 0, 0);
                    currentUpdateState = ContentUpdateState.DownloadingCatalogInfo;
                }

                if (currentUpdateState == ContentUpdateState.DownloadingCatalogInfo)
                {
                    var status = contentDeliveryService.GetDeliveryStatus(currentRemoteId);
                    if (status.State == ContentDeliveryService.DeliveryState.ContentDownloaded)
                    {
                        LogFunc?.Invoke($"Remote catalog info loaded, creating location service from {status.DownloadStatus.LocalPath}");
                        var catalogInfoPath = Path.Combine(cachePath, $"{kCatalogLocations}.bin");
                        File.Copy(status.DownloadStatus.LocalPath.ToString(), catalogInfoPath, true);
                        File.Delete(status.DownloadStatus.LocalPath.ToString());
                        var catLocSvc = new DefaultContentLocationService(kCatalogLocations, 1, catalogInfoPath, p => $"{remoteUrlRoot}{p}");
                        contentDeliveryService.AddLocationService(catLocSvc);
                        LogFunc?.Invoke($"Downloading content set '{kCatalogLocations}'");
                        currentRemoteId = contentDeliveryService.DeliverContent(kCatalogLocations);
                        currentUpdateState = ContentUpdateState.DownloadingCatalogs;
                    }
                    else if (status.State == ContentDeliveryService.DeliveryState.Failed)
                    {
                        LogFunc?.Invoke($"Failed to load remote catalog info.");
                        var catalogInfoPath = Path.Combine(cachePath, $"{kCatalogLocations}.bin");
                        if (File.Exists(catalogInfoPath))
                        {
                            LogFunc?.Invoke($"Cached catalog info file found at '{catalogInfoPath}', attempting to use cached content.");
                            var catLocSvc = new DefaultContentLocationService(kCatalogLocations, 1, catalogInfoPath, p => $"{remoteUrlRoot}{p}");
                            contentDeliveryService.AddLocationService(catLocSvc);
                            LogFunc?.Invoke($"Downloading content set '{kCatalogLocations}'");
                            currentRemoteId = contentDeliveryService.DeliverContent(kCatalogLocations);
                            currentUpdateState = ContentUpdateState.DownloadingCatalogs;
                        }
                        else
                        {
                            currentUpdateState = AttemptToSetContentPathsToStreamingAssets();
                        }
                    }
                }

                if (currentUpdateState == ContentUpdateState.DownloadingCatalogs)
                {
                    var status = contentDeliveryService.GetDeliveryStatus(currentRemoteId);
                    if (status.State == ContentDeliveryService.DeliveryState.ContentDownloaded)
                    {
                        var deliveryStatus = new NativeList<ContentDeliveryService.DeliveryStatus>(3, Allocator.Temp);
                        contentDeliveryService.GetDeliveryStatus(currentRemoteId, ref deliveryStatus);
                        LogFunc?.Invoke($"Content set '{kCatalogLocations}' loaded, found {deliveryStatus.Length} catalog locations.");
                        for (int i = 0; i < deliveryStatus.Length; i++)
                        {
                            LogFunc?.Invoke($"Loading catalog '{deliveryStatus[i].ContentId.Name}' from path '{deliveryStatus[i].DownloadStatus.LocalPath}'");
                            var locSvc = new DefaultContentLocationService(deliveryStatus[i].ContentId.Name.ToString(), 2 + (deliveryStatus.Length - i), deliveryStatus[i].DownloadStatus.LocalPath.ToString(), p => $"{remoteUrlRoot}{p}");
                            contentDeliveryService.AddLocationService(locSvc);
                        }
                        deliveryStatus.Dispose();
                        if (!string.IsNullOrEmpty(initialContentSet))
                        {
                            LogFunc?.Invoke($"Downloading content set '{initialContentSet}'");
                            currentRemoteId = contentDeliveryService.DeliverContent(initialContentSet);
                            currentUpdateState = ContentUpdateState.DownloadingContentSet;
                        }
                        else
                        {
                            LogFunc?.Invoke("No initial content set specified");
                            currentRemoteId = default;
                            currentUpdateState = ContentUpdateState.ContentUpdatedFromRemote;
                        }
                    }
                    else if (status.State == ContentDeliveryService.DeliveryState.Failed)
                    {
                        currentUpdateState = AttemptToSetContentPathsToStreamingAssets();
                    }
                }
                if (currentUpdateState == ContentUpdateState.DownloadingContentSet)
                {
                    var status = contentDeliveryService.GetDeliveryStatus(currentRemoteId);
                    if (status.State == ContentDeliveryService.DeliveryState.ContentDownloaded)
                    {
                        var deliveryStatus = new NativeList<ContentDeliveryService.DeliveryStatus>(128, Allocator.Temp);
                        contentDeliveryService.GetDeliveryStatus(currentRemoteId, ref deliveryStatus);
                        LogFunc?.Invoke($"Content set '{initialContentSet}' loaded, found {deliveryStatus.Length} content locations.");
                        deliveryStatus.Dispose();
                        currentRemoteId = default;
                        currentUpdateState = ContentUpdateState.ContentUpdatedFromRemote;
                    }
                    else if (status.State == ContentDeliveryService.DeliveryState.Failed)
                    {
                        currentUpdateState = AttemptToSetContentPathsToStreamingAssets();
                    }
                }
                return (currentUpdateState >= ContentUpdateState.NoContentAvailable);
            }

            internal ContentUpdateState AttemptToSetContentPathsToStreamingAssets()
            {
                var catalogPathInStreamingAssets = $"{Application.streamingAssetsPath}/{RuntimeContentManager.RelativeCatalogPath}";
                if (FileExists(catalogPathInStreamingAssets))
                {
                    LogFunc?.Invoke($"No connection, but catalog found at {catalogPathInStreamingAssets}.  Attempting to use streaming assets for content.");
                    return ContentUpdateState.UsingContentFromStreamingAssets;
                }
                else
                {
                    LogFunc?.Invoke($"No connection, no cached data, no catalog found at {catalogPathInStreamingAssets}.  Content is not available.");
                    return ContentUpdateState.NoContentAvailable;
                }
            }
        }

    }
}
