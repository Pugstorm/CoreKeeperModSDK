// #define DEBUG_ASSET_DEPENDENCY_TRACKER
#if UNITY_EDITOR
using System;
using System.Diagnostics;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Experimental;
using Hash128 = UnityEngine.Hash128;

namespace Unity.Scenes
{
    //@TODO: This doesn't yet automatically handle triggering a refresh if a dependent asset was changed on disk but refresh hasn't been called yet.
    //       Fixing that requires 20.2 branch from asset pipeline team to land.

    internal class AssetDependencyTracker<T> : IDisposable where T : unmanaged, IEquatable<T>
    {
        public struct Completed
        {
            public GUID Asset;
            public T UserKey;

            /// <summary>
            /// ArtifactID might be default
            /// This means that the Asset doesn't currently exist.
            /// Thus can't be generated.
            /// </summary>
            public Hash128 ArtifactID;
        }

        struct ReportedValue
        {
            public Hash128 ReportedHash;
            public bool DidReport;

            public T UserKey;
            public bool Async;
        }

        struct RefCount
        {
            public int Async;
            public int Synchronous;

            public bool ChangRefCount(int refcount, bool async)
            {
                if (async)
                    Async += refcount;
                else
                    Synchronous += refcount;
                return Async != 0 || Synchronous != 0;
            }
        }

        private NativeParallelMultiHashMap<GUID, ReportedValue> _AllAssets;
        private NativeParallelHashMap<GUID, RefCount>               _AllAssetsRefCount;

        private NativeList<GUID> _InProgress;
        private NativeList<Hash128> _ArtifactCache;

        Type   _AssetImportType;
        bool   _RequestRefresh;

        // Tracks if any dependencies have changed anywhere in the project.
        // When they change we have to call ProduceAsset
        ulong  _GlobalArtifactDependencyVersion;
        // Tracks if any artifacts might have completed imported.
        // We need to lookup if something has changed based on this.
        ulong  _GlobalArtifactProcessedVersion;

        int    _ProgressID;
        bool   _UpdateProgressBar;
        int    _TotalProgressAssets;
        string _ProgressSummary;
        bool   _IsAssetWorker;

        public AssetDependencyTracker(Type importerType, string progressSummary)
        {
            _AssetImportType = importerType;
            _ProgressID = -1;
            _ProgressSummary = progressSummary;

             _AllAssets = new NativeParallelMultiHashMap<GUID, ReportedValue>(1024, Allocator.Persistent);
             _AllAssetsRefCount = new NativeParallelHashMap<GUID, RefCount>(1024, Allocator.Persistent);
             _InProgress = new NativeList<GUID>(1024, Allocator.Persistent);
            _ArtifactCache = new NativeList<Hash128>(1024, Allocator.Persistent);
            _IsAssetWorker = AssetDatabaseCompatibility.IsAssetImportWorkerProcess();
        }

        public void Dispose()
        {
            if (_ProgressID != -1)
                Progress.Remove(_ProgressID);

            _AllAssets.Dispose();
            _AllAssetsRefCount.Dispose();
            _InProgress.Dispose();
            _ArtifactCache.Dispose();
        }

        public void Add(GUID asset, T userKey, bool async)
        {
            if (GetIterator(asset, userKey, out var temp, out var temp2))
                throw new ArgumentException("Add must not be called with an asset & userKey that has already been Added.");

            LogDependencyTracker($"Add: {asset}");

            var value = new ReportedValue
                {ReportedHash = default, UserKey = userKey, Async = async, DidReport = false};
            _AllAssets.Add(asset, value);

            //@TODO: Don't trigger full _GlobalArtifactDependencyVersion if no new assets were added?
            var refcount = new RefCount();
            refcount.ChangRefCount(1, async);
            if (_AllAssetsRefCount.TryAdd(asset, refcount))
            {
                // Progress is tracked per asset (Not per asset / userKey combination)
                // So we only increase _TotalProgressAssets when we see a new asset
                _TotalProgressAssets += 1;
                _UpdateProgressBar = true;
            }
            else
            {
                refcount = _AllAssetsRefCount[asset];
                refcount.ChangRefCount(1, async);
                _AllAssetsRefCount[asset] = refcount;
            }

            // For now we just ask the asset pipeline to produce all assets when adding a new asset
            _GlobalArtifactDependencyVersion = 0;
        }

        public void Remove(GUID asset, T userKey)
        {
            LogDependencyTracker($"Remove: {asset}");

            if (GetIterator(asset, userKey, out var iterator, out var reportedValue))
            {
                _AllAssets.Remove(iterator);

                var refcount = _AllAssetsRefCount[asset];
                if (refcount.ChangRefCount(-1, reportedValue.Async))
                    _AllAssetsRefCount[asset] = refcount;
                else
                {
                    _AllAssetsRefCount.Remove(asset);

                    // Progress is tracked per asset (Not per asset / userKey combination)
                    // So we only decrease _TotalProgressAssets when we remove that asset completely
                    _TotalProgressAssets -= 1;
                    _UpdateProgressBar = true;
                }
            }
            else
            {
                throw new ArgumentException("Remove must be called with an asset & userKey, that has been Added.");
            }
        }

        public void RequestRefresh()
        {
            _RequestRefresh = true;
        }

        public bool GetCompleted(NativeList<Completed> completed)
        {
            completed.Clear();
            return AddCompleted(completed);
        }

        /// <summary>
        /// adds any completed imports to the completed list.
        /// </summary>
        /// <param name="completed"></param>
        /// <returns></returns>
        public bool AddCompleted(NativeList<Completed> completed)
        {
            if (_IsAssetWorker && _AllAssets.IsEmpty)
            {
                // This requires special codepaths declaring dependencies / changes in asset pipeline / tests to work correctly.
                // For now just disallow it for clarity.
                throw new System.ArgumentException("Importing dependent assets on an import workers is currently not supported");
            }

            // LogDependencyTracker("AssetDependencyTracker.GetCompleted");

            if (_RequestRefresh)
            {
                LogDependencyTracker("AssetDatabase.Refresh");

                AssetDatabase.Refresh();
                _RequestRefresh = false;
            }

            // Assets on disk have changed, we need to re-request artifacts for everything again.
            var globalArtifactDependencyVersion = AssetDatabaseCompatibility.GetArtifactDependencyVersion();

            if (_GlobalArtifactDependencyVersion != globalArtifactDependencyVersion)
            {
                _GlobalArtifactDependencyVersion = globalArtifactDependencyVersion;
                LogDependencyTracker($"Update refresh: {globalArtifactDependencyVersion}");

                using (var all = _AllAssetsRefCount.GetKeyArray(Allocator.TempJob))
                using (var allSync = new NativeList<GUID>(Allocator.TempJob))
                {
                    // Get all Synchronous import assets
                    foreach (var asset in _AllAssetsRefCount)
                    {
                        if (asset.Value.Synchronous != 0)
                            allSync.Add(asset.Key);
                    }

                    // Process any sync imported assets
                    if (allSync.Length != 0)
                    {
                        var hasFailedArtifacts = AssetDatabaseCompatibility.ProduceArtifactsRefreshIfNecessary(allSync.AsArray(), _AssetImportType, _ArtifactCache);

                        foreach (var artifact in _ArtifactCache)
                        {
                            LogDependencyTracker("Produce Sync: " + artifact);
                        }

                        if (hasFailedArtifacts)
                        {
                            LogDependencyTracker("Failed Sync artifacts");

                            for (int i = 0; i != allSync.Length; i++)
                            {
                                if (!_ArtifactCache[i].isValid)
                                    Debug.LogError(
                                        $"Asset {AssetDatabaseCompatibility.GuidToPath(allSync[i])} couldn't be imported. (Most likely the assets dependencies or the asset itself is being modified during import.)");
                            }
                        }
                    }

                    AssetDatabaseCompatibility.ProduceArtifactsAsync(all, _AssetImportType, _ArtifactCache);

                    _UpdateProgressBar = true;
                    _InProgress.Clear();
                    for (int i = 0; i != all.Length; i++)
                    {
                        var guid = all[i];
                        var artifact = _ArtifactCache[i];

                        if (artifact.isValid)
                        {
                            LogDependencyTracker($"AssetImport completed immediately: {guid}");
                            AddToCompletionList(guid, artifact, completed);
                        }
                        else
                        {
                            //@TODO: use artifact status code instead to reduce call overhead when it lands in 20.2
                            if (!AssetDatabaseCompatibility.AssetExists(guid))
                            {
                                LogDependencyTracker($"AssetImport completed because it doesn't exist: {guid}");
                                AddToCompletionList(guid, artifact, completed);
                            }
                            else
                            {
                                LogDependencyTracker($"AssetImport in progress: {guid}");
                                _InProgress.Add(all[i]);
                            }
                        }
                    }
                }
            }

            var globalArtifactProcessedVersion = AssetDatabaseCompatibility.GetArtifactProcessedVersion();
            if (_InProgress.Length != 0 && globalArtifactProcessedVersion != _GlobalArtifactProcessedVersion)
            {
                _GlobalArtifactProcessedVersion = globalArtifactProcessedVersion;

                LogDependencyTracker($"New artifacts: {globalArtifactProcessedVersion}");

                // Clean up any _InProgress assets that are no longer required
                for (int i = 0; i < _InProgress.Length;)
                {
                    if (!_AllAssets.ContainsKey(_InProgress[i]))
                    {
                        _UpdateProgressBar = true;
                        _InProgress.RemoveAtSwapBack(i);
                    }
                    else
                        i++;
                }

                AssetDatabaseCompatibility.LookupArtifacts(_InProgress.AsArray(), _AssetImportType, _ArtifactCache);

                for (int i = 0; i < _InProgress.Length;)
                {
                    var artifact = _ArtifactCache[i];
                    var guid = _InProgress[i];
                    if (artifact.isValid)
                    {
                        LogDependencyTracker($"Asset import completed: {_InProgress[i]}");

                        AddToCompletionList(guid, artifact, completed);
                        _UpdateProgressBar = true;

                        _InProgress.RemoveAtSwapBack(i);
                        _ArtifactCache.RemoveAtSwapBack(i);
                    }
                    else
                    {
                        if (AssetDatabaseExperimental.GetOnDemandArtifactProgress(new ArtifactKey(guid, _AssetImportType)).state == OnDemandState.Failed)
                        {
                            _RequestRefresh = true;
                        }

                        i++;
                    }
                }
            }

            if (_UpdateProgressBar)
            {
                _UpdateProgressBar = false;
                if (_InProgress.Length == 0)
                {
                    LogDependencyTracker("Progress Done");

                    if (_ProgressID != -1)
                    {
                        Progress.Finish(_ProgressID);
                        _ProgressID = -1;
                    }
                }
                else
                {
                    if (_ProgressID == -1 || !Progress.Exists(_ProgressID))
                        _ProgressID = Progress.Start(_ProgressSummary);

                    float progress = (_TotalProgressAssets - _InProgress.Length) / (float) _TotalProgressAssets;
                    string description = $"{_TotalProgressAssets - _InProgress.Length} out of {_TotalProgressAssets}";

                    LogDependencyTracker($"Progress update: " + description);

                    Progress.Report(_ProgressID, progress, description);
                }
            }

            return _InProgress.Length == 0;
        }

        public int TotalAssets
        {
            get { return _TotalProgressAssets; }
        }

        public int InProgressAssets
        {
            get { return _InProgress.Length; }
        }

        bool GetIterator(GUID asset, T userKey, out NativeParallelMultiHashMapIterator<GUID> iterator, out ReportedValue value)
        {
            if (_AllAssets.TryGetFirstValue(asset, out value, out iterator))
            {
                do
                {
                    if (value.UserKey.Equals(userKey))
                        return true;
                } while (_AllAssets.TryGetNextValue(out value, ref iterator));
            }

            value = default;
            return false;
        }

        void AddToCompletionList(GUID guid, Hash128 artifact, NativeList<Completed> completed)
        {
            if (!_AllAssets.TryGetFirstValue(guid, out var value, out var it))
                throw new InvalidOperationException();

            do
            {
                if (!value.DidReport || value.ReportedHash != artifact)
                {
                    completed.Add(new Completed {Asset = guid, UserKey = value.UserKey, ArtifactID = artifact});
                    value.ReportedHash = artifact;
                    value.DidReport = true;
                    _AllAssets.SetValue(value, it);
                }
            } while (_AllAssets.TryGetNextValue(out value, ref it));
        }

        [Conditional("DEBUG_ASSET_DEPENDENCY_TRACKER")]
        static void LogDependencyTracker(string status)
        {
            Debug.Log(status);
        }
    }
}

#endif
