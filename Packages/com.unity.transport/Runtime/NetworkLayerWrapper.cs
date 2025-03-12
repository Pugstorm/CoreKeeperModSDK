using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using BurstRuntime = Unity.Burst.BurstRuntime;

namespace Unity.Networking.Transport
{
    internal unsafe struct NetworkLayerWrapper : IDisposable
    {
        private void* m_RawLayerData;
        private long m_TypeHash;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private int m_RawLayerDataSize;
#endif

        private ManagedCallWrapper m_Dispose_FPtr;
        private ManagedCallWrapper m_ScheduleReceive_FPtr;
        private ManagedCallWrapper m_ScheduleSend_FPtr;

        static public NetworkLayerWrapper Create<T>(ref T layer) where T : unmanaged, INetworkLayer
        {
            var wrapper = new NetworkLayerWrapper
            {
                m_RawLayerData = UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), Allocator.Persistent),
                m_TypeHash = BurstRuntime.GetHashCode64<T>(),
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_RawLayerDataSize = UnsafeUtility.SizeOf<T>(),
#endif
                m_Dispose_FPtr = new ManagedCallWrapper(&DisposeWrapper<T>),
                m_ScheduleReceive_FPtr = new ManagedCallWrapper(&ScheduleReceiveWrapper<T>),
                m_ScheduleSend_FPtr = new ManagedCallWrapper(&ScheduleSendWrapper<T>),
            };

            UnsafeUtility.CopyStructureToPtr(ref layer, wrapper.m_RawLayerData);

            return wrapper;
        }

        public bool IsType<T>() where T : unmanaged, INetworkLayer
        {
            return m_TypeHash == BurstRuntime.GetHashCode64<T>();
        }

        public unsafe ref T CastRef<T>() where T : unmanaged, INetworkLayer
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!IsType<T>())
                throw new InvalidCastException();

            if (m_RawLayerDataSize != UnsafeUtility.SizeOf<T>())
                throw new InvalidOperationException();
#endif
            return ref UnsafeUtility.AsRef<T>(m_RawLayerData);
        }

        // Dispose wrapper
        public void Dispose()
        {
            var arguments = new DisposeArguments { LayerPtr = m_RawLayerData };
            m_Dispose_FPtr.Invoke(ref arguments);

            UnsafeUtility.Free(m_RawLayerData, Allocator.Persistent);
        }

        private struct DisposeArguments
        {
            public void* LayerPtr;
        }

        private static void DisposeWrapper<T>(void* argumentsPtr, int size) where T : unmanaged, INetworkLayer
        {
            ref var arguments = ref ManagedCallWrapper.ArgumentsFromPtr<DisposeArguments>(argumentsPtr, size);
            UnsafeUtility.AsRef<T>(arguments.LayerPtr).Dispose();
        }

        // ScheduleReceive wrapper
        public JobHandle ScheduleReceive(ref ReceiveJobArguments jobArguments, JobHandle dependency)
        {
            var arguments = new ScheduleReceiveArguments
            {
                LayerPtr = m_RawLayerData,
                JobArguments = jobArguments,
                Dependency = dependency,
            };
            m_ScheduleReceive_FPtr.Invoke(ref arguments);
            return arguments.Return;
        }

        private struct ScheduleReceiveArguments
        {
            public void* LayerPtr;
            public ReceiveJobArguments JobArguments;
            public JobHandle Dependency;
            public JobHandle Return;
        }

        private static void ScheduleReceiveWrapper<T>(void* argumentsPtr, int size) where T : unmanaged, INetworkLayer
        {
            ref var arguments = ref ManagedCallWrapper.ArgumentsFromPtr<ScheduleReceiveArguments>(argumentsPtr, size);
            arguments.Return = UnsafeUtility.AsRef<T>(arguments.LayerPtr).ScheduleReceive(ref arguments.JobArguments, arguments.Dependency);
        }

        // ScheduleSend wrapper
        public JobHandle ScheduleSend(ref SendJobArguments jobArguments, JobHandle dependency)
        {
            var arguments = new ScheduleSendArguments
            {
                LayerPtr = m_RawLayerData,
                JobArguments = jobArguments,
                Dependency = dependency,
            };
            m_ScheduleSend_FPtr.Invoke(ref arguments);
            return arguments.Return;
        }

        private struct ScheduleSendArguments
        {
            public void* LayerPtr;
            public SendJobArguments JobArguments;
            public JobHandle Dependency;
            public JobHandle Return;
        }

        private static void ScheduleSendWrapper<T>(void* argumentsPtr, int size) where T : unmanaged, INetworkLayer
        {
            ref var arguments = ref ManagedCallWrapper.ArgumentsFromPtr<ScheduleSendArguments>(argumentsPtr, size);
            arguments.Return = UnsafeUtility.AsRef<T>(arguments.LayerPtr).ScheduleSend(ref arguments.JobArguments, arguments.Dependency);
        }
    }
}
