using System;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.NetCode
{
    /// <summary>
    /// The RpcCollection is the set of all available RPCs. It is created by the RpcSystem.
    /// It is used to register RPCs and to get queues for sending RPCs. In most cases you
    /// do not need to use it directly, the generated code will use it to setup the RPC
    /// components.
    /// </summary>
    public struct RpcCollection : IComponentData
    {
        internal struct RpcData : IComparable<RpcData>
        {
            public ulong TypeHash;
            public PortableFunctionPointer<RpcExecutor.ExecuteDelegate> Execute;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            public ComponentType RpcType;
#endif
            public int CompareTo(RpcData other)
            {
                if (TypeHash < other.TypeHash)
                    return -1;
                if (TypeHash > other.TypeHash)
                    return 1;
                return 0;
            }
        }
        /// <summary>
        /// Treat the set of assemblies loaded on the client / server as dynamic or different. This is only required if
        /// assemblies containing ghost component serializers or RPC serializers are removed when building standalone.
        /// This property is read in OnUpdate, so it must be set before then. Defaults to false, which saves 6 bytes per header,
        /// and allows RPC version errors to trigger immediately upon connecting to the server (rather than needing to wait for
        /// an invalid RPC to be received).
        /// </summary>
        public bool DynamicAssemblyList
        {
            get { return m_DynamicAssemblyList.Value == 1; }
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (m_IsFinal == 1)
                    throw new InvalidOperationException("DynamicAssemblyList must be set before the RpcSystem.OnUpdate is called!");
#endif
                m_DynamicAssemblyList.Value = value ? (byte)1u : (byte)0u;
            }
        }

        /// <summary>
        /// The RPC "common header" format is 9 bytes:
        /// - Message Type: byte
        /// - LocalTime: int
        /// - RemoteTime: int
        ///
        /// And then, for each RPC, the header is:
        /// - RpcHash: [short|long] (based on DynamicAssemblyList)
        /// - Size: ushort
        /// - Payload : x bytes
        ///
        /// So for a single message we have:
        /// - 9 (common header) + 4 => 13 bytes (no DynamicAssemblyList)
        /// - 9 (common header) + 10 => 19 bytes (with DynamicAssemblyList)
        /// </summary>
        /// <param name="dynamicAssemblyList">Whether or not your project is using <see cref="DynamicAssemblyList"/>.</param>
        /// <returns>If <see cref="DynamicAssemblyList"/>, 19 bytes, otherwise 13 bytes.</returns>
        public static int GetRpcHeaderLength(bool dynamicAssemblyList) => k_RpcCommonHeaderLengthBytes + GetInnerRpcMessageHeaderLength(dynamicAssemblyList);

        /// <inheritdoc cref="GetRpcHeaderLength"/>>
        internal const int k_RpcCommonHeaderLengthBytes = 9;

        /// <summary>
        /// If <see cref="DynamicAssemblyList"/>, 10 bytes, otherwise 4 bytes.
        /// </summary>
        /// <param name="dynamicAssemblyList">Whether or not your project is using <see cref="DynamicAssemblyList"/>.</param>
        /// <returns>If <see cref="DynamicAssemblyList"/>, 10 bytes, otherwise 4 bytes.</returns>
        internal static int GetInnerRpcMessageHeaderLength(bool dynamicAssemblyList) => dynamicAssemblyList ? 10 : 4;

        /// <summary>
        /// Register a new RPC type which can be sent over the network. This must be called before
        /// any connections are established.
        /// </summary>
        /// <typeparam name="TActionSerializer">A struct of type IRpcCommandSerializer.</typeparam>
        /// <typeparam name="TActionRequest">A struct of type IComponent.</typeparam>
        public void RegisterRpc<TActionSerializer, TActionRequest>()
            where TActionRequest : struct, IComponentData
            where TActionSerializer : struct, IRpcCommandSerializer<TActionRequest>
        {
            RegisterRpc(ComponentType.ReadWrite<TActionRequest>(), default(TActionSerializer).CompileExecute());
        }
        /// <summary>
        /// Register a new RPC type which can be sent over the network. This must be called before
        /// any connections are established.
        /// </summary>
        /// <param name="type">Type to register.</param>
        /// <param name="exec">Callback for RPC to execute.</param>
        public void RegisterRpc(ComponentType type, PortableFunctionPointer<RpcExecutor.ExecuteDelegate> exec)
        {
            if (m_IsFinal == 1)
                throw new InvalidOperationException("Cannot register new RPCs after the RpcSystem has started running");

            if (!exec.Ptr.IsCreated)
            {
                throw new InvalidOperationException($"Cannot register RPC for type {type.GetManagedType()}: Ptr property is not created (null)" +
                                                    "Check CompileExecute() and verify you are initializing the PortableFunctionPointer with a valid static function delegate, decorated with [BurstCompile(DisableDirectCall = true)] attribute");
            }

            var hash = TypeManager.GetTypeInfo(type.TypeIndex).StableTypeHash;
            if (hash == 0)
                throw new InvalidOperationException(String.Format("Unexpected 0 hash for type {0}", type.GetManagedType()));
            if (m_RpcTypeHashToIndex.TryGetValue(hash, out var index))
            {
                var rpcData = m_RpcData[index];
                if (rpcData.TypeHash != 0)
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if (rpcData.RpcType == type)
                        throw new InvalidOperationException($"Registering RPC {type.ToFixedString()} multiple times is not allowed! Existing: {rpcData.RpcType.ToFixedString()}!");
                    throw new InvalidOperationException($"StableTypeHash collision between types {type.ToFixedString()} and {rpcData.RpcType.ToFixedString()} while registering RPC!");
#else
                    throw new InvalidOperationException($"Hash collision or multiple registrations for {type.ToFixedString()} while registering RPC! Existing: {rpcData.TypeHash}!");
#endif
                }

                rpcData.TypeHash = hash;
                rpcData.Execute = exec;
                m_RpcData[index] = rpcData;
            }
            else
            {
                m_RpcTypeHashToIndex.Add(hash, m_RpcData.Length);
                m_RpcData.Add(new RpcData
                {
                    TypeHash = hash,
                    Execute = exec,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    RpcType = type
#endif
                });
            }
        }

        /// <summary>
        /// Get an RpcQueue which can be used to send RPCs.
        /// </summary>
        /// <typeparam name="TActionSerializer">Struct of type <see cref="IRpcCommandSerializer{TActionRequest}"/></typeparam>
        /// <typeparam name="TActionRequest">Struct of type <see cref="IComponentData"/></typeparam>
        /// <returns><see cref="RpcQueue{TActionSerializer,TActionRequest}"/> to be used to send RPCs.</returns>
        public RpcQueue<TActionSerializer, TActionRequest> GetRpcQueue<TActionSerializer, TActionRequest>()
            where TActionRequest : struct, IComponentData
            where TActionSerializer : struct, IRpcCommandSerializer<TActionRequest>
        {
            var hash = TypeManager.GetTypeInfo(TypeManager.GetTypeIndex<TActionRequest>()).StableTypeHash;
            if (hash == 0)
                throw new InvalidOperationException(String.Format("Unexpected 0 hash for type {0}", typeof(TActionRequest)));
            int index;
            if (!m_RpcTypeHashToIndex.TryGetValue(hash, out index))
            {
                if (m_IsFinal == 1)
                    throw new InvalidOperationException("Cannot register new RPCs after the RpcSystem has started running");
                index = m_RpcData.Length;
                m_RpcTypeHashToIndex.Add(hash, index);
                m_RpcData.Add(new RpcData
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    RpcType = ComponentType.ReadWrite<TActionRequest>()
#endif
                });
            }
            return new RpcQueue<TActionSerializer, TActionRequest>
            {
                rpcType = hash,
                rpcTypeHashToIndex = m_RpcTypeHashToIndex,
                dynamicAssemblyList = m_DynamicAssemblyList
            };
        }
        /// <summary>
        /// Internal method to calculate the hash of all types when sending version. When calling this you
        /// must have write access to the singleton since it changes internal state.
        /// </summary>
        internal ulong CalculateVersionHash()
        {
            if (m_RpcData.Length >= ushort.MaxValue)
                throw new InvalidOperationException(String.Format("RpcSystem does not support more than {0} RPCs", ushort.MaxValue));
            for (int i = 0; i < m_RpcData.Length; ++i)
            {
                if (m_RpcData[i].TypeHash == 0)
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    throw new InvalidOperationException(String.Format("Missing RPC registration for {0} which is used to send data", m_RpcData[i].RpcType.GetManagedType()));
#else
                    throw new InvalidOperationException("Missing RPC registration for RPC which is used to send");
#endif
            }
            m_RpcData.Sort();
            m_RpcTypeHashToIndex.Clear();
            for (int i = 0; i < m_RpcData.Length; ++i)
            {
                m_RpcTypeHashToIndex.Add(m_RpcData[i].TypeHash, i);

#if ENABLE_UNITY_RPC_REGISTRATION_LOGGING
#if UNITY_DOTS_DEBUG
                UnityEngine.Debug.Log(String.Format("NetCode RPC Method hash 0x{0:X} index {1} type {2}", m_RpcData[i].TypeHash, i, m_RpcData[i].RpcType));
#else
                UnityEngine.Debug.Log(String.Format("NetCode RPC Method hash {0} index {1}", m_RpcData[i].TypeHash, i));
#endif
#endif
            }

            ulong hash = m_RpcData[0].TypeHash;
            for (int i = 0; i < m_RpcData.Length; ++i)
                hash = TypeHash.CombineFNV1A64(hash, m_RpcData[i].TypeHash);
            m_IsFinal = 1;
            return (m_DynamicAssemblyList.Value == 1) ? 0 : hash;
        }
        internal static unsafe void SendProtocolVersion(DynamicBuffer<OutgoingRpcDataStreamBuffer> buffer, NetworkProtocolVersion version)
        {
            bool dynamicAssemblyList = (version.RpcCollectionVersion == 0);
            int msgHeaderLen = GetInnerRpcMessageHeaderLength(dynamicAssemblyList);
            DataStreamWriter writer = new DataStreamWriter(UnsafeUtility.SizeOf<NetworkProtocolVersion>() + msgHeaderLen + 1, Allocator.Temp);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (buffer.Length != 0)
                throw new InvalidOperationException("Protocol version must be the very first RPC sent");
#endif
            if (dynamicAssemblyList)
                writer.WriteULong(0);
            else
                writer.WriteUShort(ushort.MaxValue);
            var lenWriter = writer;
            writer.WriteUShort((ushort)0);
            writer.WriteInt(version.NetCodeVersion);
            writer.WriteInt(version.GameVersion);
            if (dynamicAssemblyList)
            {
                writer.WriteULong(0);
                writer.WriteULong(0);
            }
            else
            {
                writer.WriteULong(version.RpcCollectionVersion);
                writer.WriteULong(version.ComponentCollectionVersion);
            }
            lenWriter.WriteUShort((ushort)(writer.Length - msgHeaderLen - 1));
            var prevLen = buffer.Length;
            buffer.ResizeUninitialized(buffer.Length + writer.Length);
            byte* ptr = (byte*) buffer.GetUnsafePtr();
            ptr += prevLen;
            UnsafeUtility.MemCpy(ptr, writer.AsNativeArray().GetUnsafeReadOnlyPtr(), writer.Length);
        }

        internal NativeList<RpcData> Rpcs => m_RpcData;

        internal NativeList<RpcData> m_RpcData;
        internal NativeParallelHashMap<ulong, int> m_RpcTypeHashToIndex;
        internal NativeReference<byte> m_DynamicAssemblyList;

        internal byte m_IsFinal;
    }
}
