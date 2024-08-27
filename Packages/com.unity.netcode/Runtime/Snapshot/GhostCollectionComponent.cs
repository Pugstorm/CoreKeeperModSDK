using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.NetCode.LowLevel.Unsafe;

namespace Unity.NetCode
{
    /// <summary>
    /// A BlobAsset containing all the meta data required for ghosts.
    /// </summary>
    internal struct GhostPrefabBlobMetaData
    {
        public enum GhostMode
        {
            Interpolated = 1,
            Predicted = 2,
            Both = 3
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ComponentInfo
        {
            ///<summary>The Component StableTypeHash.</summary>
            public ulong StableHash;
            //<summary>Serializer variant to use. If 0, the default for that type is used.
            //Note: This also denotes if we should send to child.</summary>
            public ulong Variant;
            //<summary>The SendMask override for the component if different than -1.</summary>
            public int SendMaskOverride;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ComponentReference
        {
            public ComponentReference(int index, ulong hash)
            {
                EntityIndex = index;
                StableHash = hash;
            }
            ///<summary>The entity index in the linkedEntityGroup</summary>
            public int EntityIndex;
            ///<summary>The component stable hash.</summary>
            public ulong StableHash;
        }

        public int Importance;
        public GhostMode SupportedModes;
        public GhostMode DefaultMode;
        public bool StaticOptimization;
        public BlobString Name;
        ///<summary>Array of components for each child in the hierarchy.</summary>
        public BlobArray<ComponentInfo> ServerComponentList;
        public BlobArray<int> NumServerComponentsPerEntity;
        /// <summary>
        /// A list of (child index, components) pairs which should be removed from the prefab when using it on the server. The main use-case is to support ClientAndServer data.
        /// </summary>
        public BlobArray<ComponentReference> RemoveOnServer;
        /// <summary>
        /// A list of (child index, components) pairs which should be removed from the prefab when using it on the client. The main use-case is to support ClientAndServer data.
        /// </summary>
        public BlobArray<ComponentReference> RemoveOnClient;
        /// <summary>
        /// A list of (child index, components) pairs which should be disabled when the prefab is used to instantiate a predicted ghost. This is used so we can have a single client prefab.
        /// </summary>
        public BlobArray<ComponentReference> DisableOnPredictedClient;
        /// <summary>
        /// A list of (child index, components) pairs which should be disabled when the prefab is used to instantiate an interpolated ghost. This is used so we can have a single client prefab.
        /// </summary>
        public BlobArray<ComponentReference> DisableOnInterpolatedClient;
    }

    /// <summary>
    /// A component added to all ghost prefabs. It contains the meta-data required to use the prefab as a ghost.
    /// </summary>
    [DontSupportPrefabOverrides]
    [GhostComponent(SendDataForChildEntity = false)]
    internal struct GhostPrefabMetaData : IComponentData
    {
        public BlobAssetReference<GhostPrefabBlobMetaData> Value;
    }

    /// <summary>
    /// A component added to ghost prefabs which require runtime stripping of components before they can be used.
    /// The component is removed when the runtime stripping is performed.
    /// </summary>
    internal struct GhostPrefabRuntimeStrip : IComponentData
    {}

    /// <summary>
    /// A component used to identify the singleton which owns the ghost collection lists and data.
    /// The singleton contains buffers for GhostCollectionPrefab, GhostCollectionPrefabSerializer,
    /// GhostCollectionComponentIndex and GhostComponentSerializer.State
    /// </summary>
    public struct GhostCollection : IComponentData
    {
        /// <summary>
        /// The number of prefabs that have been loaded into the <see cref="GhostCollectionPrefab"/> collection.
        /// Use to determine which ghosts types the server can stream to the clients.
        /// <para>
        /// The server reports (to the client) the list of loaded prefabs (with their see <see cref="GhostTypeComponent"/> guid)
        /// as part of the snapshot protocol.
        /// The list is dynamic; new prefabs can be added/loaded at runtime (on the server), and the new ones will be reported to the client.
        /// </para>
        /// <para>
        /// Clients report (to the server) the number of loaded prefabs, as part of the command protocol.
        /// When the client receives a ghost snapshot, the ghost prefab list is processed, and the <see cref="GhostCollectionPrefab"/> collection
        /// is updated with any new ghost types not already present in the collection.
        /// <para>
        /// The client does not need to have loaded ALL prefab types in the <see cref="GhostCollectionPrefab"/> to initialize the world. I.e. They can
        /// be loaded/added dynamically into the world (i.e when streaming a sub-scene), and the <see cref="GhostCollectionPrefab.Loading"/> state
        /// should be used in that case (to inform the <see cref="GhostCollection"/> that the specified prefabs are currently being loaded into the world).
        /// </para>
        /// </para>
        /// </summary>
        public int NumLoadedPrefabs;
        #if UNITY_EDITOR || NETCODE_DEBUG
        /// <summary>
        /// Only for debug, the current length of the predicted error names list. Used by the <see cref="GhostPredictionDebugSystem"/>.
        /// </summary>
        internal int NumPredictionErrors;
        #endif
        /// <summary>
        /// Flag set when there is at least one <see cref="NetworkStreamConnection"/> that is game.
        /// </summary>
        public bool IsInGame;
    }

    /// <summary>
    /// A list of all prefabs which can be used for ghosts. This is populated with all ghost prefabs on the server
    /// and that list is sent for clients. Having a prefab in this list does not guarantee that there is a serializer
    /// for it yet.
    /// Added to the GhostCollection singleton entity.
    /// </summary>
    /// <remarks>
    /// The list is sorted by the value of the <see cref="GhostType"/> guid.
    /// </remarks>
    [InternalBufferCapacity(0)]
    public struct GhostCollectionPrefab : IBufferElementData
    {
        /// <summary>
        /// Ghost prefabs can be added dynamically to the ghost collection as soon as they are loaded from either a
        /// sub-scene, or created dynamically at runtime.
        /// This enum is used on the clients, to signal the ghost collection system that the <see cref="GhostCollectionPrefab"/>
        /// type is being loaded into the world
        /// </summary>
        public enum LoadingState
        {
            /// <summary>
            /// The default state. Prefab not loaded or present (i.e. the <see cref="GhostCollectionPrefab.GhostPrefab"/> reference is <see cref="Entity.Null"/>).
            /// </summary>
            NotLoading = 0,
            /// <summary>
            /// Denotes that the client has started loading the Entity Prefab (i.e the client is streaming the sub-scene content).
            /// The <see cref="GhostCollectionSystem"/> will start monitoring the state of the resource (see <see cref="GhostCollectionPrefab.GhostPrefab"/>).
            /// </summary>
            LoadingActive,
            /// <summary>
            /// The prefab is currently being loaded, but either a) the prefab entity does not exist or b) the prefab has been not processed yet.
            /// This state should only be set via the <see cref="GhostCollectionSystem"/>, and only when the <see cref="GhostCollectionPrefab.Loading"/> state
            /// is currently set to <see cref="LoadingActive"/>.
            /// </summary>
            LoadingNotActive
        }
        /// <inheritdoc cref="NetCode.GhostType"/>
        public GhostType GhostType;
        /// <summary>
        /// A reference to the prefab entity. The reference is initially equals to <see cref="Entity.Null"/> and assigned by
        /// the <see cref="GhostCollectionSystem"/> when prefabs are processed.
        /// </summary>
        public Entity GhostPrefab;
        /// <summary>
        /// Calculated at runtime by the <see cref="GhostCollectionSystem"/> and used to for consistency check. In particular,
        /// the hash to verify the ghost is serialized and deserialized in the same way.
        /// </summary>
        internal ulong Hash;
        /// <summary>
        /// Game code should set this to LoadingActive if the prefab is currently being loaded. The collection system
        /// will set it to LoadingNotActive every frame, so game code must reset it to LoadingActive every frame the
        /// prefab is still loading.
        /// </summary>
        public LoadingState Loading;
    }
    /// <summary>
    /// A list of all serializer data for the prefabs in GhostCollectionPrefab. This list can be shorter if not all
    /// serializers are created yet.
    /// Added to the GhostCollection singleton entity.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct GhostCollectionPrefabSerializer : IBufferElementData
    {
        /// <summary>
        /// The stable type hash of the prefab. Used to retrieve GhostCollectionPrefabSerializer instance. The hash is composed by
        /// the name and the hash of all the component serializers.
        /// </summary>
        public ulong TypeHash;
        /// <summary>
        /// The index of the first component serialization rule to use inside the <see cref="GhostCollectionComponentIndex"/>.
        /// </summary>
        public int FirstComponent;
        /// <summary>
        /// The total number of serialized components. Include both root and child entities.
        /// </summary>
        public int NumComponents;
        /// <summary>
        /// The total number of serialized components present only in the child entities.
        /// </summary>
        public int NumChildComponents;
        /// <summary>
        /// The total size in bytes of the entire ghost type, including space for enable bits and change masks.
        /// </summary>
        public int SnapshotSize;
        /// <summary>
        /// The number of bits used by change mask bitarray for this entire ghost type.
        /// </summary>
        public int ChangeMaskBits;
        /// <summary>
        /// Only set if the <see cref="GhostOwner"/> is present on the entity prefab,
        /// is the offset in bytes, from the beginning of the snapshot data, in which the network id of the of client
        /// owning the entity can be retrieved.
        /// <code>
        /// var ghostOwner = *(uint*)(snapshotDataPtr + PredictionOwnerOffset)
        /// </code>
        /// </summary>
        public int PredictionOwnerOffset;
        /// <summary>
        /// Flag stating if the ghost replication mode is set to owner predicted.
        /// </summary>
        public int OwnerPredicted;
        /// <summary>
        /// Set to 1 when the ghost contains components with different <see cref="GhostComponentSerializer.SendMask"/>.
        /// Based on the ghost replication mode (interpolated or predicted), some of these component should be not replicated,
        /// and the decision must be made by the <see cref="GhostSendSystem"/> at runtime, when entities are serialized.
        /// </summary>
        public byte PartialComponents;
        /// <summary>
        /// Set to 1 if the ghost has some components for which the <see cref="GhostComponentAttribute.OwnerSendType"/>
        /// is different than <see cref="SendToOwnerType.All"/>. When the flag is set, the <see cref="GhostSendSystem"/>
        /// will peform the necessary ghost owner checks.
        /// </summary>
        public byte PartialSendToOwner;
        /// <summary>
        /// True if the <see cref="GhostOptimizationMode"/> is set to <see cref="GhostOptimizationMode.Static"/> in the
        /// <see cref="GhostAuthoringComponent"/>.
        /// </summary>
        public byte StaticOptimization;
        /// <summary>
        /// Reflect the importance value set in the <see cref="GhostAuthoringComponent"/>. Is used as the base value for the
        /// scaled importance calculated at runtime.
        /// </summary>
        public int BaseImportance;
        /// <summary>
        /// Used by the <see cref="GhostSpawnClassificationSystem"/> to assign the type of <see cref="GhostSpawnBuffer.Type"/> to use for this ghost,
        /// if no other user-defined system has classified how the new ghost should be spawned.
        /// </summary>
        public GhostSpawnBuffer.Type FallbackPredictionMode;
        /// <summary>
        /// Flag that indicates if the ghost prefab contains a <see cref="GhostGroup"/> component and can be used as root
        /// of the group (see also <seealso cref="GhostChildEntity"/>).
        /// </summary>
        public int IsGhostGroup;
        /// <summary>
        /// The number of bits necessary to store the enabled state of all the enableable ghost components (that are flagged with <see cref="GhostEnabledBitAttribute"/>).
        /// </summary>
        public int EnableableBits;
        /// <summary>
        /// The size of the largest replicated <see cref="IBufferElementData"/> for this ghost. It is used to calculate the
        /// necessary <see cref="SnapshotDynamicDataBuffer"/> capacity to hold the replicated buffer data.
        /// </summary>
        public int MaxBufferSnapshotSize;
        /// <summary>
        /// The total number of replicated <see cref="IBufferElementData"/> for this ghost.
        /// </summary>
        public int NumBuffers;
        /// <summary>
        /// A profile marker used to track serialization performance.
        /// </summary>
        public Profiling.ProfilerMarker profilerMarker;
        /// <summary>
        /// A custom serializer function to serializer the chunk (only for server).
        /// </summary>
        public PortableFunctionPointer<GhostPrefabCustomSerializer.ChunkSerializerDelegate> CustomSerializer;
        /// <summary>
        /// The function pointer to invoke for pre-serializing the chunk (only for server).
        /// </summary>
        public PortableFunctionPointer<GhostPrefabCustomSerializer.ChunkPreserializeDelegate> CustomPreSerializer;
    }

    /// <summary>
    /// This list contains the set of uniques components which support serialization. Used to map the DynamicComponentTypeHandle
    /// to a concrete ComponentType in jobs.
    /// Added to the GhostCollection singleton entity.
    /// </summary>
    [InternalBufferCapacity(0)]
    internal struct GhostCollectionComponentType : IBufferElementData
    {
        /// <summary>
        /// The type of the component. Must be either a <see cref="IComponentData"/> or a <see cref="IBufferElementData"/>.
        /// </summary>
        public ComponentType Type;
        /// <summary>
        /// The index of the first serializer for this component type inside the <see cref="GhostComponentSerializer"/> collection
        /// </summary>
        public int FirstSerializer;
        /// <summary>
        /// The index of the last (included) serializer for this component type inside the <see cref="GhostComponentSerializer"/> collection
        /// </summary>
        public int LastSerializer;
    }

    /// <summary>
    /// This list contains the set of entity + component for all serialization rules in GhostCollectionPrefabSerializer.
    /// GhostCollectionPrefabSerializer contains a FirstComponent and NumComponents which identifies the set of components
    /// to use from this array.
    /// Added to the GhostCollection singleton entity.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct GhostCollectionComponentIndex : IBufferElementData
    {
        /// <summary>Index of ghost entity the rule applies to.</summary>
        public int EntityIndex;
        /// <summary>Index in the <see cref="GhostCollectionComponentIndex"/>, used to retrieve the component type from the DynamicTypeHandle.</summary>
        public int ComponentIndex;
        /// <summary>Index in the <see cref="GhostComponentSerializer.State"/> collection, used to get the type of serializer to use.</summary>
        public int SerializerIndex;
        /// <summary>The <see cref="TypeIndex"/> the component.</summary>
        public int TypeIndex;
        /// <summary>Size of the component.</summary>
        public int ComponentSize;
        /// <summary>Size of the component in the snapshot buffer.</summary>
        public int SnapshotSize;
        /// <summary>Current send mask for that component, used to not send/receive components in some configuration.</summary>
        public GhostSendType SendMask;
        /// <summary>Current owner mask for that component, used to not send/receive components in some configuration.</summary>
        public SendToOwnerType SendToOwner;
#if UNITY_EDITOR || NETCODE_DEBUG
        internal int PredictionErrorBaseIndex;
        #endif
    }

    /// <summary>
    /// Allow to associate for a given ghost prefab a custom made (hand written) serialization function.
    /// The method allow to serialize on per "archetype", allowing for better vectorization and optimisation in general.
    /// However, writing the serialization code is not trivial and require deep knowledge of the underlying
    /// <see cref="GhostChunkSerializer"/> implementation, data and wire format.
    /// </summary>
    public struct GhostPrefabCustomSerializer
    {
        /// <summary>
        /// Contains all the necessary data to perform the chunk serialization.
        /// </summary>
        public struct Context
        {
            /// <summary>
            /// The pointer to the buffer that contains the snapshot data. The size of the buffer is fixed
            /// by archetype, being the component set immutable after the prefab has been registered and pre-processed
            /// by the <see cref="GhostCollectionSystem"/>.
            /// </summary>
            [NoAlias]public IntPtr snapshotDataPtr;
            /// <summary>
            /// The pointer to the buffer that contains the dynamic buffer snapshot data. This is a
            /// variable size buffer.
            /// </summary>
            [NoAlias]public IntPtr snapshotDynamicDataPtr;
            /// <summary>
            /// The index inside the <see cref="GhostCollectionPrefabSerializer"/> buffer.
            /// </summary>
            public int ghostType;
            /// <summary>
            /// The offset from the start of the <see cref="snapshotDataPtr"/> from which the component data
            /// are stored. The offset depends on the number of component, their change masks and the presence or
            /// not of enable bits to replicate.
            /// </summary>
            public int snapshotOffset;
            /// <summary>
            /// The offsets in bytes from the beginning of the <see cref="snapshotDataPtr"/> buffer where the
            /// component change mask bits bits are stored.
            /// </summary>
            public int changeMaskOffset;
            /// <summary>
            /// The offsets in bytes from the beginning of the <see cref="snapshotDataPtr"/> buffer where the
            /// state of the component enable bits are stored.
            /// </summary>
            public int enablebBitsOffset;
            /// <summary>
            /// The offset from the beginning of the <see cref="snapshotDynamicDataPtr"/> from which the dynamic buffer
            /// data are going to be stored.
            /// </summary>
            public int dynamicDataOffset;
            /// <summary>
            /// The size (in bytes) of the snasphot data. Entitiy component data are stored strided by the snapshotSize
            /// and the snapshot buffer format is something like:
            /// |ent1       | ... |ent n|
            /// |c1, c2.. cn| ... |c1, c2.. cn|
            /// </summary>
            public int snapshotStride;
            /// <summary>
            /// The capacity (in bytes) of the dynamic snasphot data. This is pre-computed and it is used mostly for
            /// boundary checks.
            /// </summary>
            public int dynamicDataCapacity;
            /// <summary>
            /// The dynamic type handle of all the registered serializable component types currently in use.
            /// </summary>
            [NoAlias][ReadOnly] public IntPtr ghostChunkComponentTypesPtr;
            /// <summary>
            /// The <see cref="ghostChunkComponentTypesPtr"/> list length.
            /// </summary>
            public int ghostChunkComponentTypesPtrLen;
            /// <summary>
            /// A lookup used to retrieve the chunk information (chunk and indices) when serializing
            /// child components.
            /// </summary>
            [ReadOnly] public EntityStorageInfoLookup childEntityLookup;
            /// <summary>
            /// Type handled used to retrieve the <see cref="LinkedEntityGroup"/> buffer from the chunk.
            /// </summary>
            [ReadOnly] public BufferTypeHandle<LinkedEntityGroup> linkedEntityGroupTypeHandle;
            /// <summary>
            /// The <see cref="GhostSerializerState"/> data used to convert component data to snapshot data.
            /// </summary>
            public GhostSerializerState serializerState;
            /// <summary>
            /// The index of the first relevant entity in the chunk.
            /// </summary>
            public int startIndex;
            /// <summary>
            /// The index of the last relevant entity in the chunk. This should be used to iterate over
            /// the chunk entities. Don't use chunk.Count.
            /// </summary>
            public int endIndex;
            /// <summary>
            /// The connection <see cref="NetworkId"/>
            /// </summary>
            public int networkId;
            /// <summary>
            /// instruct the custom serializer to not copy the data to the snapsnot, because has been already
            /// pre-serialized
            /// </summary>
            public int hasPreserializedData;
            /// <summary>
            /// [Output] the buffer where to store the ghost data compressed size and start bit inside the
            /// temporary data stream.
            /// Stores 2 ints per component, per entity.
            /// [1st] Writer bit offset to the start of this components writes.
            /// [2nd] Num bits written for this component.
            /// </summary>
            [NoAlias]public IntPtr entityStartBit;
            /// <summary>
            /// The list of readonly <see cref="DynamicComponentTypeHandle"/> that must be used to retrieve the component
            /// data from the chunk
            /// </summary>
            [NoAlias]public IntPtr ghostChunkComponentTypes;
            /// <summary>
            /// The baselines to use to serialize the entities. It contains 4 baselines per entity:
            /// Index 0-2 the snapshot baseline, Index 3 the dynamic buffer baseline.
            /// </summary>
            [NoAlias]public IntPtr baselinePerEntityPtr;
            /// <summary>
            /// Contains a run-length encoded baseline indices to use for each entities runs. Can be used
            /// to determine if an entity is irrelevant.
            /// </summary>
            [NoAlias]public IntPtr sameBaselinePerEntityPtr;
            /// <summary>
            /// [Output] a buffer that store the total size of the dynamic buffer data for each entity in the chunk.
            /// </summary>
            [NoAlias]public IntPtr dynamicDataSizePerEntityPtr;
            /// <summary>
            /// a readonly buffer that contains all zero bytes  (up to 8kb)
            /// </summary>
            [NoAlias]public IntPtr zeroBaseline;
            /// <summary>
            /// the pointer of the <see cref="GhostInstance"/> data in the chunk.
            /// </summary>
            [NoAlias]public IntPtr ghostInstances;
        }

        /// <summary>
        /// The function pointer to invoke for serializing the chunk.
        /// </summary>
        public PortableFunctionPointer<ChunkSerializerDelegate> SerializeChunk;
        /// <summary>
        /// A custom serializer function to serializer the chunk (only for server)
        /// </summary>
        public PortableFunctionPointer<ChunkPreserializeDelegate> PreSerializeChunk;
        ///<summary>
        /// Delegate to specify a custom order for the serialised components.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CollectComponentDelegate(IntPtr componentTypes, IntPtr componentCount);
        ///<summary>
        /// Delegate for the custom chunk serializer.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ChunkSerializerDelegate(
            ref ArchetypeChunk chunk,
            in GhostCollectionPrefabSerializer typeData,
            in DynamicBuffer<GhostCollectionComponentIndex> componentIndices,
            ref Context context,
            ref DataStreamWriter tempWriter,
            in StreamCompressionModel compressionModel,
            ref int lastSerializedEntity);
        ///<summary>
        /// Delegate for the custom chunk pre-serialization function.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ChunkPreserializeDelegate(
            in ArchetypeChunk chunk,
            in GhostCollectionPrefabSerializer typeData,
            in DynamicBuffer<GhostCollectionComponentIndex> componentIndices,
            ref Context context);
    }

    /// <summary>
    /// Singleton component that holds the list of custom chunk serializers.
    /// </summary>
    public struct GhostCollectionCustomSerializers : IComponentData
    {
        /// <summary>
        /// Associate a <see cref="GhostPrefabCustomSerializer"/> for a specific prefab guid (or <see cref="GhostType"/>)
        /// The Hash128 can be derived from the <see cref="GhostType"/> via the explicit cast operator.
        /// </summary>
        public NativeHashMap<Hash128, GhostPrefabCustomSerializer> Serializers;
    }
}
