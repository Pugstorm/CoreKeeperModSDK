using System;
using System.Collections.Generic;
using System.IO;
using Unity.Entities.Content;
using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core.Compression;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.IO.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using System.Linq;

namespace Unity.Scenes
{
    struct AsyncLoadSceneData
    {
        public EntityManager EntityManager;
        public int SceneSize;
        public int CompressedSceneSize;
        public string ScenePath;
        public Codec Codec;
        public bool BlockUntilFullyLoaded;
        public BlobAssetReference<DotsSerialization.BlobHeader> BlobHeader;
        public BlobAssetOwner BlobHeaderOwner;
        public Entity SceneSectionEntity;
        public UntypedWeakReferenceId UnityObjectRefId;
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        public PostLoadCommandBuffer PostLoadCommandBuffer;
#endif
        internal int ExternalEntitiesRefRange;
        internal int SceneSectionIndex;
    }

    unsafe class AsyncLoadSceneOperation
    {
        public enum LoadingStatus
        {
            Completed,
            NotStarted,
            WaitingForUnityObjectReferencesLoad,
            WaitingForResourcesLoad,
            WaitingForEntitiesLoad,
            WaitingForSceneDeserialization
        }

        public override string ToString()
        {
            return $"AsyncLoadSceneJob({_ScenePath})";
        }

        struct FreeJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public void* Ptr;
            public SerializeUtility.WorldDeserializationStatus DeserializationStatus;
            public ReadHandle ReadHandle;
            public UnsafeList<ReadCommand> ReadCommands;
            public bool FreeChunks;

            public void Execute()
            {
                Memory.Unmanaged.Free(Ptr, Allocator.Persistent);

                if (FreeChunks)
                {
                    int length = DeserializationStatus.MegaChunkInfoList.Length;
                    for (int i = 0; i < length; ++i)
                    {
                        var chunks = DeserializationStatus.MegaChunkInfoList[i];
                        EntityComponentStore.FreeContiguousChunks(chunks.MegaChunkIndex, chunks.MegaChunkSize);
                    }
                }

                DeserializationStatus.Dispose();
                if (ReadHandle.IsValid())
                    ReadHandle.Dispose();
                if (ReadCommands.IsCreated)
                    ReadCommands.Dispose();
            }
        }

        public void Dispose()
        {
            if (_LoadingStatus == LoadingStatus.WaitingForResourcesLoad || _LoadingStatus == LoadingStatus.WaitingForEntitiesLoad)
            {
                var freeJob = new FreeJob { Ptr = _FileContent, DeserializationStatus = _DeserializationStatus, ReadCommands = _ReadCommands, ReadHandle = _ReadHandle };
                freeJob.FreeChunks = true;
                freeJob.Schedule(_ReadHandle.JobHandle);
            }

            if (_UnityObjectRefId.IsValid)
                RuntimeContentManager.ReleaseObjectAsync(_UnityObjectRefId);

#if !UNITY_DISABLE_MANAGED_COMPONENTS
            _Data.PostLoadCommandBuffer?.Dispose();
#endif
            _Data.BlobHeaderOwner.Release();
            _DeserializationResultArray.Dispose(_EntityManager.ExclusiveEntityTransactionDependency);
        }

        struct AsyncLoadSceneJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public byte*                        FileContent;            // Only to use when deserializing from memory
            public long                         FileLength;

            public GCHandle                     LoadingOperationHandle;
            [DeallocateOnJobCompletion]
            public NativeArray<int> UnityObjectRefs;

            public ExclusiveEntityTransaction   Transaction;
            public NativeArray<SerializeUtility.WorldDeserializationResult> DeserializationResult;

            static readonly ProfilerMarker k_ProfileDeserializeWorld = new ProfilerMarker("AsyncLoadSceneJob.DeserializeWorld");
            [NativeDisableUnsafePtrRestriction]
            public SerializeUtility.WorldDeserializationStatus DeserializationStatus;
            public BlobAssetReference<DotsSerialization.BlobHeader> BlobHeader;
            public Entity SceneSectionEntity;
            public int SceneSectionIndex;
            public int ExternalEntitiesRefRange;

            public void Execute()
            {
                var loadingOperation = (AsyncLoadSceneOperation)LoadingOperationHandle.Target;
                LoadingOperationHandle.Free();

                try
                {
                    SerializeUtility.WorldDeserializationResult deserializationResult;
                    // Deserializing from memory loaded file
                    if (FileContent != null)
                    {
                        using (var reader = new MemoryBinaryReader(FileContent, FileLength))
                        {
                            k_ProfileDeserializeWorld.Begin();
                            SerializeUtility.DeserializeWorldInternal(Transaction, reader, out deserializationResult, ExternalEntitiesRefRange, SceneSectionIndex, UnityObjectRefs);
                            k_ProfileDeserializeWorld.End();
                        }
                    }
                    else
                    {
                        k_ProfileDeserializeWorld.Begin();
                        var dotsReader = DotsSerialization.CreateReader(ref BlobHeader.Value);
                        SerializeUtility.EndDeserializeWorld(Transaction, dotsReader, ref DeserializationStatus, out deserializationResult, ExternalEntitiesRefRange, SceneSectionIndex, UnityObjectRefs);
                        k_ProfileDeserializeWorld.End();
                    }
                    Transaction.EntityManager.AddSharedComponentManaged(Transaction.EntityManager.UniversalQueryWithSystems, new SceneTag { SceneEntity = SceneSectionEntity });
                    DeserializationResult[0] = deserializationResult;
                }
                catch (Exception exc)
                {
                    loadingOperation._LoadingFailure = exc.Message;
                    loadingOperation._LoadingException = exc;
                }
            }
        }

        AsyncLoadSceneData      _Data;
        string                  _ScenePath => _Data.ScenePath;
        int                     _SceneSize => _Data.SceneSize;
        ref EntityManager           _EntityManager => ref _Data.EntityManager;
        bool                    _BlockUntilFullyLoaded => _Data.BlockUntilFullyLoaded;

        UntypedWeakReferenceId _UnityObjectRefId;
#if UNITY_EDITOR
        RuntimeContentManager.InstanceHandle _UnityObjectRefsHandle;
#endif

        LoadingStatus           _LoadingStatus;
        string                  _LoadingFailure;
        Exception               _LoadingException;

        private byte*            _FileContent;
        ReadHandle               _ReadHandle;
        UnsafeList<ReadCommand>  _ReadCommands;

        private double _StartTime;
        private SerializeUtility.WorldDeserializationStatus _DeserializationStatus;
        private NativeArray<SerializeUtility.WorldDeserializationResult> _DeserializationResultArray;
        public SerializeUtility.WorldDeserializationResult DeserializationResult => _DeserializationResultArray[0];


        public AsyncLoadSceneOperation(AsyncLoadSceneData asyncLoadSceneData)
        {
            _Data = asyncLoadSceneData;
            _UnityObjectRefId = asyncLoadSceneData.UnityObjectRefId;
            _LoadingStatus = LoadingStatus.NotStarted;
            _DeserializationResultArray = new NativeArray<SerializeUtility.WorldDeserializationResult>(1, Allocator.Persistent);
        }

        public bool IsCompleted
        {
            get
            {
                return _LoadingStatus == LoadingStatus.Completed;
            }
        }

        public string ErrorStatus
        {
            get
            {
                if (_LoadingStatus == LoadingStatus.Completed)
                    return _LoadingFailure;
                else
                    return null;
            }
        }

        public Exception Exception => _LoadingException;

        public UntypedWeakReferenceId StealReferencedUnityObjects()
        {
            var tmp = _UnityObjectRefId;
            _UnityObjectRefId = default;
            return tmp;
        }

        private void UpdateBlocking()
        {
            if (_LoadingStatus == LoadingStatus.Completed)
                return;
            if (_SceneSize == 0)
                return;

            try
            {
                Assert.IsFalse(string.IsNullOrEmpty(_ScenePath));
                _StartTime = Time.realtimeSinceStartup;

                if (_Data.BlobHeader.IsCreated)
                {
                    var dotsReader = DotsSerialization.CreateReader(ref _Data.BlobHeader.Value);
                    _ReadHandle = SerializeUtility.BeginDeserializeWorld(_ScenePath, dotsReader, out _DeserializationStatus, out _ReadCommands);
                }
                else
                {
                    throw new InvalidOperationException("BlobHeader must be valid");
                }

                if (_UnityObjectRefId.IsValid)
                {
#if UNITY_EDITOR
                    _UnityObjectRefsHandle = RuntimeContentManager.LoadInstanceAsync(_UnityObjectRefId);
                    RuntimeContentManager.WaitForInstanceCompletion(_UnityObjectRefsHandle);
#else
                    RuntimeContentManager.LoadObjectAsync(_UnityObjectRefId);
                    RuntimeContentManager.WaitForObjectCompletion(_UnityObjectRefId);
#endif
                }

                ScheduleSceneRead();
                _EntityManager.EndExclusiveEntityTransaction();
                PostProcessScene();
            }
            catch (Exception e)
            {
                _LoadingFailure = e.Message;
                _LoadingException = Exception;
            }
            _LoadingStatus = LoadingStatus.Completed;
        }

        private void UpdateAsync()
        {
            //@TODO: Try to overlap Resources load and entities scene load
            if (_LoadingStatus == LoadingStatus.NotStarted)
            {
                if (_SceneSize == 0)
                    return;

                try
                {
                    Assert.IsFalse(string.IsNullOrEmpty(_ScenePath));
                    _StartTime = Time.realtimeSinceStartup;

                    // Load the file into memory if it's a compressed one, then decompress via a job and deserialize from memory
                    if (_Data.Codec != Codec.None)
                    {
                        _FileContent = (byte*) Memory.Unmanaged.Allocate(_SceneSize, 16, Allocator.Persistent);
                        _ReadCommands = new UnsafeList<ReadCommand>(1, Allocator.Persistent);
                        _ReadCommands.Add(new ReadCommand
                        {
                            Buffer = _FileContent,
                            Offset = 0,
                            Size = _SceneSize
                        });
#if ENABLE_PROFILER
                        // When AsyncReadManagerMetrics are available, mark up the file read for more informative IO metrics.
                        // Metrics can be retrieved by AsyncReadManagerMetrics.GetMetrics
                        _ReadHandle = AsyncReadManager.Read(_ScenePath, _ReadCommands.Ptr, 1, subsystem: AssetLoadingSubsystem.EntitiesScene);
#else
                        _ReadHandle = AsyncReadManager.Read(_ScenePath, _ReadCommands.Ptr, 1);
#endif
                    }

                    // Asynchronous deserialization from file, the BeginDeserializeWorld call will schedule the reads, the End call will perform the deserialization itself
                    else
                    {
                        if (_Data.BlobHeader.IsCreated)
                        {
                            var dotsReader = DotsSerialization.CreateReader(ref _Data.BlobHeader.Value);
                            _ReadHandle = SerializeUtility.BeginDeserializeWorld(_ScenePath, dotsReader, out _DeserializationStatus, out _ReadCommands);
                        }
                        else
                        {
                            throw new InvalidOperationException("BlobHeader must be valid");
                        }
                    }

                    if (_UnityObjectRefId.IsValid)
                    {
#if UNITY_EDITOR
                        _UnityObjectRefsHandle = RuntimeContentManager.LoadInstanceAsync(_UnityObjectRefId);
#else
                        RuntimeContentManager.LoadObjectAsync(_UnityObjectRefId);
#endif
                        _LoadingStatus = LoadingStatus.WaitingForUnityObjectReferencesLoad;
                    }
                    else
                    {
                        _LoadingStatus = LoadingStatus.WaitingForEntitiesLoad;
                    }
                }
                catch (Exception e)
                {
                    _LoadingFailure = e.Message;
                    _LoadingException = e;
                    _LoadingStatus = LoadingStatus.Completed;
                }
            }

            // Once async asset bundle load is done, we can read the asset
            if (_LoadingStatus == LoadingStatus.WaitingForUnityObjectReferencesLoad)
            {
#if UNITY_EDITOR
                if (RuntimeContentManager.GetInstanceLoadingStatus(_UnityObjectRefsHandle) == ObjectLoadingStatus.Completed)
#else
                if (RuntimeContentManager.GetObjectLoadingStatus(_UnityObjectRefId) == ObjectLoadingStatus.Completed)
#endif
                    _LoadingStatus = LoadingStatus.WaitingForEntitiesLoad;
            }

            if (_LoadingStatus == LoadingStatus.WaitingForEntitiesLoad)
            {
                try
                {
                    _LoadingStatus = LoadingStatus.WaitingForSceneDeserialization;
                    ScheduleSceneRead();
                    if (_BlockUntilFullyLoaded)
                    {
                        _EntityManager.ExclusiveEntityTransactionDependency.Complete();
                    }
                }
                catch (Exception e)
                {
                    _LoadingFailure = e.Message;
                    _LoadingException = e;
                    _LoadingStatus = LoadingStatus.Completed;
                }
            }

            // Complete Loading status
            if (_LoadingStatus == LoadingStatus.WaitingForSceneDeserialization)
            {
                if (_EntityManager.ExclusiveEntityTransactionDependency.IsCompleted)
                {
                    _EntityManager.EndExclusiveEntityTransaction();
                    PostProcessScene();
                    _LoadingStatus = LoadingStatus.Completed;
                    var currentTime = Time.realtimeSinceStartup;
                    var totalTime = currentTime - _StartTime;

                    System.Console.WriteLine($"Streamed scene with {totalTime * 1000,4:f0}ms latency from {_ScenePath}");
                }
            }
        }

        public void Update()
        {
            if (_BlockUntilFullyLoaded)
            {
                UpdateBlocking();
            }
            else
            {
                UpdateAsync();
            }
        }

        void ScheduleSceneRead()
        {
            var transaction = _EntityManager.BeginExclusiveEntityTransaction();
            UnityEngine.Object[] objectReferences = null;
#if UNITY_EDITOR
            if (_UnityObjectRefsHandle.IsValid)
            {
                SerializeUtilityHybrid.DeserializeObjectReferences(RuntimeContentManager.GetInstanceValue<ReferencedUnityObjects>(_UnityObjectRefsHandle), out objectReferences);
                RuntimeContentManager.ReleaseInstancesAsync(_UnityObjectRefsHandle);
            }
#else
            if(_UnityObjectRefId.IsValid)
                SerializeUtilityHybrid.DeserializeObjectReferences(RuntimeContentManager.GetObjectValue<ReferencedUnityObjects>(_UnityObjectRefId), out objectReferences);
#endif
            NativeArray<int> unityObjectRefs = default;
            if (objectReferences != null && objectReferences.Length > 0)
            {
                unityObjectRefs = new NativeArray<int>(objectReferences.Length, Allocator.Persistent);
                for (int i = 0; i < unityObjectRefs.Length; i++)
                {
                    unityObjectRefs[i] = objectReferences[i].GetInstanceID();
                }
            }
            else
            {
                unityObjectRefs = new NativeArray<int>(0, Allocator.Persistent);
            }

            var loadJob = new AsyncLoadSceneJob
            {
                Transaction = transaction,
                LoadingOperationHandle = GCHandle.Alloc(this),
                UnityObjectRefs = unityObjectRefs,
                DeserializationStatus = _DeserializationStatus,
                BlobHeader = _Data.BlobHeader,
                FileContent = _FileContent,
                FileLength = _SceneSize,
                DeserializationResult = _DeserializationResultArray,
                SceneSectionEntity = _Data.SceneSectionEntity,
                SceneSectionIndex = _Data.SceneSectionIndex,
                ExternalEntitiesRefRange = _Data.ExternalEntitiesRefRange,
            };

            var loadJobHandle = loadJob.Schedule(JobHandle.CombineDependencies(
                _EntityManager.ExclusiveEntityTransactionDependency,
                _ReadHandle.JobHandle));
            _EntityManager.ExclusiveEntityTransactionDependency = loadJobHandle;
            _DeserializationStatus = default; // _DeserializationStatus is disposed by AsyncLoadSceneJob
            var freeJob = new FreeJob { Ptr = _FileContent, ReadCommands = _ReadCommands, ReadHandle = _ReadHandle };
            freeJob.Schedule(loadJobHandle);

            _FileContent = null;
            _ReadCommands = default;
            _ReadHandle = default;
        }

        static readonly ProfilerMarker s_PostProcessScene = new ProfilerMarker(nameof(PostProcessScene));
        void PostProcessScene()
        {
            using var marker = s_PostProcessScene.Auto();

#if !UNITY_DISABLE_MANAGED_COMPONENTS
            if (_Data.PostLoadCommandBuffer != null)
            {
                _Data.PostLoadCommandBuffer.CommandBuffer.Playback(_EntityManager);
                _Data.PostLoadCommandBuffer.Dispose();
                _Data.PostLoadCommandBuffer = null;
            }
#endif
            SceneSectionStreamingSystem.AddStreamingWorldSystems(_EntityManager.World);
            var group = _EntityManager.World.GetOrCreateSystemManaged<ProcessAfterLoadGroup>();
            group.Update();
            _EntityManager.CompleteAllTrackedJobs();
            _EntityManager.World.DestroyAllSystemsAndLogException(out bool errorsWhileDestroyingSystems);
            using var missingSceneTag = _EntityManager.CreateEntityQuery(ComponentType.Exclude<SceneTag>());
            if (!missingSceneTag.IsEmptyIgnoreFilter)
                _EntityManager.AddSharedComponentManaged(missingSceneTag, new SceneTag { SceneEntity = _Data.SceneSectionEntity });
        }
    }
}
