using System.Runtime.InteropServices;
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
        /// The number prefab that has been loaded into the <see cref="GhostCollectionPrefab"/> collection.
        /// Use to determine which ghosts types the server can stream to the clients.
        /// <para>
        /// The server report to the client the list of loaded prefabs (with their see <see cref="GhostType"/> guid)
        /// as part of the snapshot protocol.
        /// The list is dynamic; new prefabs can be added/loaded at runtime on the server, and the ones will be reported to the client.
        /// </para>
        /// <para>
        /// Clients reports to the server the number of loaded prefab as part of the command protocol.
        /// When the client receive a ghost snapshot, the ghost prefab list is processed and the <see cref="GhostCollectionPrefab"/> collection
        /// is updated with any new ghost types not present in the collection.
        /// <para>
        /// The client is not required to have all prefab type in the <see cref="GhostCollectionPrefab"/> to be loaded into the world. They can
        /// be loaded/added dynamically to the world (i.e when streaming a sub-scene), and the <see cref="GhostCollectionPrefab.Loading"/> state
        /// should be used in that case to inform the <see cref="GhostCollection"/> that the specified prefabs are getting loaded into the world.
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
    internal struct GhostCollectionPrefabSerializer : IBufferElementData
    {
        /// <summary>
        /// The stable type hash of the component buffer. Used to retrieve the component type from the <see cref="Entities.TypeManager"/>.
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
        /// The total size in bytes of the serialized component data.
        /// </summary>
        public int SnapshotSize;
        /// <summary>
        /// The number of bits used by change mask bitarray.
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
        public bool StaticOptimization;
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
    internal struct GhostCollectionComponentIndex : IBufferElementData
    {
        /// <summary>Index of ghost entity the rule applies to.</summary>
        public int EntityIndex;
        /// <summary>Index in the GhostComponentCollection, used to retrieve the component type from the DynamicTypeHandle.</summary>
        public int ComponentIndex;
        /// <summary>Index in the GhostComponentSerializer.State collection, used to get the type of serializer to use.</summary>
        public int SerializerIndex;
        /// <summary>Current send mask for that component, used to not send/receive components in some configuration.</summary>
        public GhostComponentSerializer.SendMask SendMask;
#if UNITY_EDITOR || NETCODE_DEBUG
        public int PredictionErrorBaseIndex;
        #endif
    }

}
