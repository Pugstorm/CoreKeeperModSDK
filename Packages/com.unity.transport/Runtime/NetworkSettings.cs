using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport.Logging;
using BurstRuntime = Unity.Burst.BurstRuntime;

namespace Unity.Networking.Transport
{
    /// <summary>
    /// An aggregate of the different parameter structures that describe a network configuration.
    /// </summary>
    public struct NetworkSettings : IDisposable
    {
        private struct ParameterSlice
        {
            public int Offset;
            public int Size;
        }

        private const int k_MapInitialCapacity = 8;

        private NativeParallelHashMap<long, ParameterSlice> m_ParameterOffsets;
        private NativeList<byte> m_Parameters;
        private byte m_Initialized;
        private byte m_ReadOnly;

        /// <summary>If the settings have been created (e.g. not disposed).</summary>
        /// <value>True if created, false otherwise.</value>
        public bool IsCreated => m_Initialized == 0 || m_Parameters.IsCreated;

        private bool EnsureInitializedOrError()
        {
            if (m_Initialized == 0)
            {
                m_Initialized = 1;
                m_Parameters = new NativeList<byte>(Allocator.Temp);
                m_ParameterOffsets = new NativeParallelHashMap<long, ParameterSlice>(k_MapInitialCapacity, Allocator.Temp);
            }

            if (!m_Parameters.IsCreated)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new ObjectDisposedException($"The {nameof(NetworkSettings)} has been deallocated, it is not allowed to access it.");
#else
                DebugLog.LogError($"The {nameof(NetworkSettings)} has been deallocated, it is not allowed to access it.");
                return false;
#endif
            }
            return true;
        }

        /// <summary>
        /// Creates a new settings object using the provided allocator. If no allocator is
        /// provided, <c>Allocator.Temp</c> will be used.
        /// </summary>
        /// <param name="allocator">Allocator to use to create the object.</param>
        public NetworkSettings(Allocator allocator)
        {
            m_Initialized = 1;
            m_ReadOnly = 0;
            m_Parameters = new NativeList<byte>(allocator);
            m_ParameterOffsets = new NativeParallelHashMap<long, ParameterSlice>(k_MapInitialCapacity, allocator);
        }

        internal NetworkSettings(NetworkSettings from, Allocator allocator)
        {
            m_Initialized = 1;
            m_ReadOnly = 0;

            if (from.m_Initialized == 0)
            {
                m_Parameters = new NativeList<byte>(allocator);
                m_ParameterOffsets = new NativeParallelHashMap<long, ParameterSlice>(k_MapInitialCapacity, allocator);
                return;
            }

            m_Parameters = new NativeList<byte>(from.m_Parameters.Length, allocator);
            m_Parameters.AddRangeNoResize(from.m_Parameters);

            var keys = from.m_ParameterOffsets.GetKeyArray(Allocator.Temp);
            m_ParameterOffsets = new NativeParallelHashMap<long, ParameterSlice>(keys.Length, allocator);
            for (int i = 0; i < keys.Length; i++)
                m_ParameterOffsets.Add(keys[i], from.m_ParameterOffsets[keys[i]]);
        }

        public void Dispose()
        {
            // Disposed state is m_Initialized == 1 && m_Parameters.IsCreated == false
            m_Initialized = 1;

            if (m_Parameters.IsCreated)
            {
                m_Parameters.Dispose();
                m_ParameterOffsets.Dispose();
            }
        }

        /// <summary>Get a read-only copy of the settings.</summary>
        /// <returns>A read-only copy of the settings.</returns>
        public NetworkSettings AsReadOnly()
        {
            var settings = this;
            settings.m_ReadOnly = 1;
            return settings;
        }

        /// <summary>
        /// Add a new parameter structure to the list. Only one instance of any parameter type is
        /// stored at a time. Adding a new instance of an existing parameter structure will
        /// overwrite the one that is already stored.
        /// </summary>
        /// <typeparam name="T">Type of <see cref="INetworkParameter"/> to add.</typeparam>
        /// <param name="parameter">Parameter structure to add.</param>
        /// <exception cref="ArgumentException">
        /// If the parameters contain invalid values. Note that this exception is only thrown when
        /// collections checks are enabled (i.e. in the editor). Otherwise an error is logged and
        /// the parameters are added anyway.
        /// </exception>
        public unsafe void AddRawParameterStruct<T>(ref T parameter) where T : unmanaged, INetworkParameter
        {
            if (!EnsureInitializedOrError())
                return;

            if (m_ReadOnly != 0)
            {
                DebugLog.LogError("NetworkSettings structure is read-only, modifications are not allowed.");
                return;
            }

            ValidateParameterOrError(ref parameter);

            var typeHash = BurstRuntime.GetHashCode64<T>();
            var parameterSlice = new ParameterSlice
            {
                Offset = m_Parameters.Length,
                Size = UnsafeUtility.SizeOf<T>(),
            };

            if (m_ParameterOffsets.TryAdd(typeHash, parameterSlice))
            {
                m_Parameters.Resize(m_Parameters.Length + parameterSlice.Size, NativeArrayOptions.UninitializedMemory);
            }
            else
            {
                parameterSlice = m_ParameterOffsets[typeHash];
            }

            var valuePtr = (T*)((byte*)m_Parameters.GetUnsafePtr<byte>() + parameterSlice.Offset);
            *valuePtr = parameter;
        }

        /// <summary>Try to get the parameter structure for the specified type.</summary>
        /// <typeparam name="T">Type of the parameter structure to get.</typeparam>
        /// <param name="parameter">Stored parameter structure (if true is returned).</param>
        /// <returns>True if the parameter structure is in the settings.</returns>
        public unsafe bool TryGet<T>(out T parameter) where T : unmanaged, INetworkParameter
        {
            parameter = default;

            if (!EnsureInitializedOrError())
                return false;

            var typeHash = BurstRuntime.GetHashCode64<T>();

            if (m_ParameterOffsets.TryGetValue(typeHash, out var parameterSlice))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (UnsafeUtility.SizeOf<T>() != parameterSlice.Size)
                    throw new ArgumentException($"The size of the parameter type {typeof(T)} ({UnsafeUtility.SizeOf<T>()}) is different to the stored size ({parameterSlice.Size})");

                if (m_Parameters.Length < parameterSlice.Offset + parameterSlice.Size)
                    throw new OverflowException($"The registered parameter slice is out of bounds of the parameters buffer");
#endif
                parameter = *(T*)((byte*)m_Parameters.GetUnsafeReadOnlyPtr<byte>() + parameterSlice.Offset);
                return true;
            }

            return false;
        }

        internal static void ValidateParameterOrError<T>(ref T parameter) where T : INetworkParameter
        {
            if (!parameter.Validate())
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new ArgumentException($"The provided network parameter ({parameter.GetType().Name}) is not valid");
#else
                DebugLog.ErrorParameterIsNotValid(parameter.GetType().Name);
#endif
            }
        }
    }
}
