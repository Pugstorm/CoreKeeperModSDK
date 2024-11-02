using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe.NotBurstCompatible;
using Unity.Entities;
using System;

namespace Unity.Scenes
{
    struct LiveConversionSceneMsg : IDisposable
    {
        public NativeArray<Hash128> LoadedScenes;
        public NativeArray<Hash128> RemovedScenes;

        public void Dispose()
        {
            LoadedScenes.Dispose();
            RemovedScenes.Dispose();
        }

        public byte[] ToMsg()
        {
            var buffer = new UnsafeAppendBuffer(0, 16, Allocator.TempJob);
            Serialize(ref buffer);
            var bytes = buffer.ToBytesNBC();
            buffer.Dispose();
            return bytes;
        }

        unsafe public static LiveConversionSceneMsg FromMsg(byte[] buffer, AllocatorManager.AllocatorHandle allocator)
        {
            fixed(byte* ptr = buffer)
            {
                var reader = new UnsafeAppendBuffer.Reader(ptr, buffer.Length);
                LiveConversionSceneMsg msg = default;
                msg.Deserialize(ref reader, allocator);
                return msg;
            }
        }

        void Serialize(ref UnsafeAppendBuffer buffer)
        {
            buffer.Add(LoadedScenes);
            buffer.Add(RemovedScenes);
        }

        void Deserialize(ref UnsafeAppendBuffer.Reader buffer, AllocatorManager.AllocatorHandle allocator)
        {
            buffer.ReadNext(out LoadedScenes, allocator);
            buffer.ReadNext(out RemovedScenes, allocator);
        }
    }

    class LiveConversionSceneChangeTracker
    {
        private EntityQuery _LoadedScenesQuery;
        private EntityQuery _UnloadedScenesQuery;
        private NativeList<SceneReference>  m_PreviousScenes;
        private NativeList<LiveConversionPatcher.LiveConvertedSceneCleanup> m_PreviousRemovedScenes;

        public LiveConversionSceneChangeTracker(EntityManager manager)
        {
            _LoadedScenesQuery = manager.CreateEntityQuery(typeof(SceneReference));
            _UnloadedScenesQuery = manager.CreateEntityQuery(ComponentType.Exclude<SceneReference>(), ComponentType.ReadOnly<LiveConversionPatcher.LiveConvertedSceneCleanup>());
            m_PreviousScenes = new NativeList<SceneReference>(Allocator.Persistent);
            m_PreviousRemovedScenes = new NativeList<LiveConversionPatcher.LiveConvertedSceneCleanup>(Allocator.Persistent);
        }

        public void Dispose()
        {
            _LoadedScenesQuery.Dispose();
            _UnloadedScenesQuery.Dispose();
            m_PreviousScenes.Dispose();
            m_PreviousRemovedScenes.Dispose();
        }

        public void Reset()
        {
            m_PreviousScenes.Clear();
            m_PreviousRemovedScenes.Clear();
        }

        public bool GetSceneMessage(out LiveConversionSceneMsg msg)
        {
            msg = default;

            if (_LoadedScenesQuery.CalculateChunkCount() == 0 && _UnloadedScenesQuery.CalculateChunkCount() == 0
                && m_PreviousScenes.Length == 0 && m_PreviousRemovedScenes.Length == 0)
                return false;

            var loadedScenes = _LoadedScenesQuery.ToComponentDataArray<SceneReference>(Allocator.TempJob);
            var removedScenes = _UnloadedScenesQuery.ToComponentDataArray<LiveConversionPatcher.LiveConvertedSceneCleanup>(Allocator.TempJob);
            var newRemovedScenes = SubtractArrays(removedScenes, m_PreviousRemovedScenes.AsArray());

            var noNewUnloads = newRemovedScenes.Length == 0;
            var sameLoadState = loadedScenes.ArraysEqual(m_PreviousScenes.AsArray());
            if (noNewUnloads && sameLoadState)
            {
                loadedScenes.Dispose();
                removedScenes.Dispose();
                newRemovedScenes.Dispose();
                return false;
            }

            msg.LoadedScenes = loadedScenes.Reinterpret<Hash128>();
            msg.RemovedScenes = newRemovedScenes.Reinterpret<Hash128>();

            m_PreviousScenes.Clear();
            m_PreviousRemovedScenes.Clear();
            m_PreviousScenes.AddRange(loadedScenes);
            m_PreviousRemovedScenes.AddRange(removedScenes);
            removedScenes.Dispose();

            return true;
        }

        internal static NativeArray<T> SubtractArrays<T>(NativeArray<T> minuend, NativeArray<T> subtrahend) where T : unmanaged, IEquatable<T>
        {
            if (minuend.Length == 0 || minuend.ArraysEqual(subtrahend))
                return new NativeArray<T>(0, Allocator.Persistent);

            if (subtrahend.Length == 0)
                return new NativeArray<T>(minuend, Allocator.Persistent);

            var result = new NativeList<T>(Allocator.Temp);
            result.AddRange(minuend);

            foreach (var sub in subtrahend)
            {
                var i = result.IndexOf(sub);
                if (i != -1)
                    result.RemoveAtSwapBack(i);
            }
            var asArray = new NativeArray<T>(result.AsArray(), Allocator.Persistent);
            result.Dispose();
            return asArray;
        }
    }
}
