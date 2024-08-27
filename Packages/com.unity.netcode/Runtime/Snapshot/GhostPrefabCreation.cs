#if UNITY_EDITOR && !NETCODE_NDEBUG
#define NETCODE_DEBUG
#endif

using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.NetCode.LowLevel.Unsafe;

/// <summary>
/// Specify for which type of world the entity should be converted to. Based on the conversion setting, some components
/// may be removed (stripped) from the prefab at conversion or runtime.
/// </summary>
public enum NetcodeConversionTarget
{
    /// <summary>
    /// convert for both client and server worlds.
    /// </summary>
    ClientAndServer = 0,
    /// <summary>
    /// convert only for client worlds.
    /// </summary>
    Server = 1,
    /// <summary>
    /// convert only for server worlds.
    /// </summary>
    Client = 2
}

namespace Unity.NetCode
{
    /// <summary>
    /// <para>Stores the `Supported Ghost Mode` by a ghost at authoring time.</para>
    /// <para>- <b>Interpolated</b>: <inheritdoc cref="Interpolated"/></para>
    /// <para>- <b>Predicted</b>: <inheritdoc cref="Predicted"/></para>
    /// <para>- <b>All</b>: <inheritdoc cref="Predicted"/></para>
    /// </summary>
    public enum GhostModeMask
    {
        /// <summary>
        /// Interpolated Ghosts are lightweight, as they perform no simulation on the client.
        /// Instead, their values are interpolated (via <see cref="SmoothingAction"/> rules) from the latest few processed snapshots.
        /// From a timeline POV: Interpolated ghosts are behind the server.
        /// </summary>
        Interpolated = 1,
        /// <summary>
        /// <para>Predicted Ghosts are predicted by the clients. I.e. Their <see cref="Simulate"/> component is enabled during the
        /// execution of the <see cref="PredictedSimulationSystemGroup"/>, and Systems in the <see cref="PredictedSimulationSystemGroup"/>
        /// will execute on their entities. They'll also have the <see cref="PredictedGhost"/>.</para>
        /// <para>This prediction is both expensive and non-authoritative, however, it does allow predicted Ghosts to interact with physics more accurately,
        /// and it does align their timeline with the current client.</para>
        /// <para>Miss-predictions are handled by <see cref="GhostPredictionSmoothing"/> (example: <see cref="DefaultTranslationSmoothingAction"/>).
        /// From a timeline POV: Interpolated ghosts are behind the current client, and the server.</para>
        /// </summary>
        /// <example>
        /// In a sports game, the ball will likely be a Predicted Ghost, allowing players to predict collisions with it.
        /// See the `PredictionSwitching` sample for an example of this, using multiple balls.
        /// </example>
        Predicted = 2,
        /// <summary>
        /// Supports both modes, and thus can be changed at runtime (called "runtime prediction switching" via <see cref="GhostPredictionSwitchingQueues"/>).
        /// Disables mode-specific optimizations (via <see cref="GhostSendType"/>).
        /// </summary>
        All = 3,
    }

    /// <summary>
    /// The Current Ghost Mode of a Ghost, on any given client. Denotes replication and prediction rules.
    /// <inheritdoc cref="GhostModeMask"/>
    /// </summary>
    public enum GhostMode
    {
        /// <summary><inheritdoc cref="GhostModeMask.Interpolated"/></summary>
        Interpolated,
        /// <summary><inheritdoc cref="GhostModeMask.Predicted"/></summary>
        Predicted,
        /// <summary>
        /// The ghost will be <see cref="Predicted"/> by the Ghost Owner (set via <see cref="GhostOwner"/>)
        /// and <see cref="Interpolated"/> by every other client.
        /// </summary>
        OwnerPredicted
    }

    /// <summary>
    /// Specify if the ghost replication should be optimized for frequent (dynamic) or for infrequent (static) data changes.
    ///  <para><inheritdoc cref="Dynamic"/></para>
    ///  <para><inheritdoc cref="Static"/></para>
    /// </summary>
    public enum GhostOptimizationMode
    {
        /// <summary>
        /// `Dynamic` optimization mode is the default mode.
        /// Use when you expect the Ghost to change often (i.e. every frame).
        /// It will optimize the ghost (via layered delta-compression) to have a small snapshot size per snapshot.
        /// No change-checking is performed, although delta-compression is applied aggressively.
        /// </summary>
        Dynamic,
        /// <summary>
        /// <para>`Static` optimization mode is designed for ghosts that will change infrequently (i.e. rarely, or not change at all).</para>
        /// <para>In this mode the ghost is replicated to the client only when its state changes, which can bring good bandwidth savings,
        /// but it comes at the cost of additional cpu cycles to perform change-checking.
        /// As such, static optimization should be avoided for entities that frequently change their state, since it will actually
        /// increase both bandwidth (because of the extra protocol bits necessary) and cpu cost.</para>
        /// </summary>
        Static,
    }

    /// <summary>
    /// Helper methods and structs used to configure and create ghost prefabs
    /// </summary>
    public static class GhostPrefabCreation
    {
        /// <summary>
        /// Configuration used to create a ghost prefab.
        /// </summary>
        public struct Config
        {
            /// <summary>
            /// The name of the ghost. When creating prefabs from code this is used to create a unique ghost type.
            /// </summary>
            public FixedString64Bytes Name;
            /// <summary>
            /// Higher importance means the ghost will be sent more frequently if there is not enough bandwidth to send everything.
            /// </summary>
            public int Importance;
            /// <summary>
            /// The ghost modes this prefab can be instantiated as. If for example set the Interpolated it is not possible to use this prefab for prediction.
            /// </summary>
            public GhostModeMask SupportedGhostModes;
            /// <summary>
            /// The default mode  for this ghost. This controls what the prefab will be instantiated as when the client spawns it, but the default can be overridden and the mode can be changed at runtime.
            /// </summary>
            public GhostMode DefaultGhostMode;
            /// <summary>
            /// Dynamic optimization mode uses multiple baselines to make sure data is always small. Static optimization mode will compress slightly less when there are changes, but it will have zero cost when there are no changes.
            /// </summary>
            public GhostOptimizationMode OptimizationMode;
            /// <summary>
            /// Enable pre-serialization for this ghost. Pre-serialization makes it possible to share part of the serialization cpu cost between connections, but it also has that cost when the ghost is not sent.
            /// </summary>
            public bool UsePreSerialization;
            /// <summary>
            /// Optional, custom deterministic function that retrieve all no-backing and serializable component types for this ghost. By serializable,
            /// we means components that either have ghost fields (fields with a <see cref="GhostFieldAttribute"/> attribute)
            /// or a <see cref="GhostComponentAttribute"/>.
            /// </summary>
            /// <returns></returns>
            public PortableFunctionPointer<GhostPrefabCustomSerializer.CollectComponentDelegate> CollectComponentFunc;
        }
        /// <summary>
        /// Identifier for a specific component type on a specific child of a ghost prefab.
        /// </summary>
        public struct Component : IEquatable<Component>
        {
            /// <summary>
            /// The type of the component.
            /// </summary>
            public ComponentType ComponentType;
            /// <summary>
            /// The child entity that has the component. 0 is the root.
            /// </summary>
            public int ChildIndex;
            /// <summary>
            /// Compare two Component. Component are equals if the type and entity index are the same.
            /// </summary>
            /// <param name="other"></param>
            /// <returns></returns>
            public bool Equals(Component other)
            {
                return ComponentType == other.ComponentType && ChildIndex == other.ChildIndex;
            }
            /// <summary>
            /// Calculate a unique hash for the component based on type and index.
            /// </summary>
            /// <returns></returns>
            public override int GetHashCode()
            {
                return (ComponentType.GetHashCode() * 397) ^ ChildIndex.GetHashCode();
            }
        }
        /// <summary>
        /// Identifier for a type of modifier, the types can be combined using "or" and serves as a mask.
        /// </summary>
        [Flags]
        public enum ComponentOverrideType
        {
            /// <summary>
            /// No overrides present.
            /// </summary>
            None = 0,
            /// <summary>
            /// Overrides the type of prefab the component should be present.
            /// </summary>
            PrefabType = 1,
            /// <summary>
            /// Overrides to which type of client the componet is replicated
            /// </summary>
            SendMask = 2,
            // Deprecated SendToChild = 4,
            /// <summary>
            /// Specify the component <see cref="GhostComponentVariationAttribute">variant</see> to use for serialization.
            /// </summary>
            Variant = 8
        }

        /// <summary>
        /// A modifier for a specific component on a specific child entity. Only the override types specified by the OverrideType are applied, the others are ignored.
        /// </summary>
        public struct ComponentOverride
        {
            /// <summary>
            /// The property we are overriding.
            /// </summary>
            public ComponentOverrideType OverrideType;
            /// <summary>
            /// The prefab type to use if the OverrideType is PrefabType.
            /// </summary>
            public GhostPrefabType PrefabType;
            /// <summary>
            /// The new send mask to use for the component.
            /// </summary>
            public GhostSendType SendMask;
            /// <summary>
            /// The hash of the variant to use for the component. Setting to 0 means force using the default.
            /// </summary>
            public ulong Variant;
        }
        struct ComponentHashComparer : System.Collections.Generic.IComparer<ComponentType>
        {
            public int Compare(ComponentType x, ComponentType y)
            {
                var hashX = TypeManager.GetTypeInfo(x.TypeIndex).StableTypeHash;
                var hashY = TypeManager.GetTypeInfo(y.TypeIndex).StableTypeHash;

                if (hashX < hashY)
                    return -1;
                if (hashX > hashY)
                    return 1;
                return 0;
            }
        }
        internal unsafe struct SHA1
        {
            private void UpdateABCDE(int i, ref uint a, ref uint b, ref uint c, ref uint d, ref uint e, uint f, uint k)
            {
                var tmp = ((a << 5) | (a >> 27)) + e + f + k + words[i];
                e = d;
                d = c;
                c = (b << 30) | (b >> 2);
                b = a;
                a = tmp;
            }

            private void UpdateHash()
            {
                for (int i = 16; i < 80; ++i)
                {
                    words[i] = (words[i - 3] ^ words[i - 8] ^ words[i - 14] ^ words[i - 16]);
                    words[i] = (words[i] << 1) | (words[i] >> 31);
                }

                var a = h0;
                var b = h1;
                var c = h2;
                var d = h3;
                var e = h4;

                for (int i = 0; i < 20; ++i)
                {
                    var f = (b & c) | ((~b) & d);
                    var k = 0x5a827999u;
                    UpdateABCDE(i, ref a, ref b, ref c, ref d, ref e, f, k);
                }
                for (int i = 20; i < 40; ++i)
                {
                    var f = b ^ c ^ d;
                    var k = 0x6ed9eba1u;
                    UpdateABCDE(i, ref a, ref b, ref c, ref d, ref e, f, k);
                }
                for (int i = 40; i < 60; ++i)
                {
                    var f = (b & c) | (b & d) | (c & d);
                    var k = 0x8f1bbcdcu;
                    UpdateABCDE(i, ref a, ref b, ref c, ref d, ref e, f, k);
                }
                for (int i = 60; i < 80; ++i)
                {
                    var f = b ^ c ^ d;
                    var k = 0xca62c1d6u;
                    UpdateABCDE(i, ref a, ref b, ref c, ref d, ref e, f, k);
                }
                h0 += a;
                h1 += b;
                h2 += c;
                h3 += d;
                h4 += e;
            }

            public SHA1(in FixedString128Bytes str)
            {
                h0 = 0x67452301u;
                h1 = 0xefcdab89u;
                h2 = 0x98badcfeu;
                h3 = 0x10325476u;
                h4 = 0xc3d2e1f0u;
                var bitLen = str.Length << 3;
                var numFullChunks = bitLen >> 9;
                byte* ptr = str.GetUnsafePtr();
                for (int chunk = 0; chunk < numFullChunks; ++chunk)
                {
                    for (int i = 0; i < 16; ++i)
                    {
                        words[i] = (uint)((ptr[0] << 24) | (ptr[1] << 16) | (ptr[2] << 8) | ptr[3]);
                        ptr += 4;
                    }
                    UpdateHash();
                }
                var remainingBits = (bitLen & 0x1ff);
                var remainingBytes = (remainingBits >> 3);
                var fullWords = (remainingBytes >> 2);
                for (int i = 0; i < fullWords; ++i)
                {
                    words[i] = (uint)((ptr[0] << 24) | (ptr[1] << 16) | (ptr[2] << 8) | ptr[3]);
                    ptr += 4;
                }
                var fullBytes = remainingBytes & 3;
                switch (fullBytes)
                {
                    case 3:
                        words[fullWords] = (uint)((ptr[0] << 24) | (ptr[1] << 16) | (ptr[2] << 8) | 0x80u);
                        ptr += 3;
                        break;
                    case 2:
                        words[fullWords] = (uint)((ptr[0] << 24) | (ptr[1] << 16) | (0x80u << 8));
                        ptr += 2;
                        break;
                    case 1:
                        words[fullWords] = (uint)((ptr[0] << 24) | (0x80u << 16));
                        ptr += 1;
                        break;
                    case 0:
                        words[fullWords] = (uint)((0x80u << 24));
                        break;
                }
                ++fullWords;
                if (remainingBits >= 448)
                {
                    // Needs two chunks, one for the remaining bits and one for size
                    for (int i = fullWords; i < 16; ++i)
                        words[i] = 0;
                    UpdateHash();
                    for (int i = 0; i < 15; ++i)
                        words[i] = 0;
                    words[15] = (uint)bitLen;
                    UpdateHash();
                }
                else
                {
                    for (int i = fullWords; i < 15; ++i)
                        words[i] = 0;
                    words[15] = (uint)bitLen;
                    UpdateHash();
                }
            }

            public GhostType ToGhostType()
            {
                // Construct a guid, store it in the GhostType
                return new GhostType
                {
                    guid0 = h0,
                    guid1 = (h1 & (~0xf000u)) | 0x5000u, // Set version to 5
                    guid2 = (h2 & (0x3fffffffu)) | 0x80000000u, // Set upper bits to 1 and 0
                    guid3 = h3
                };
            }

            private fixed uint words[80];
            private uint h0;
            private uint h1;
            private uint h2;
            private uint h3;
            private uint h4;
        }
        /// <summary>
        /// Helper method to build a blob asset for a ghost prefab, should not be called directly.
        /// </summary>
        /// <param name="ghostConfig">Configuration used when creating ghost prefabs.</param>
        /// <param name="entityManager">Used to validate which components exists on <paramref name="rootEntity"/></param>
        /// <param name="rootEntity">Components existing on this entity, like <see cref="GhostOwner"/> is used to configure the result.</param>
        /// <param name="linkedEntities">List of all linked entities to the <paramref name="rootEntity"/></param>
        /// <param name="allComponents">List of all component types.</param>
        /// <param name="componentCounts">List of number of components on each index.</param>
        /// <param name="target"><see cref="NetcodeConversionTarget"/></param>
        /// <param name="prefabTypes">List of different types of <see cref="GhostPrefabType"/> to created.</param>
        /// <param name="sendMasksOverride">List of send masks.</param>
        /// <param name="sendToChildOverride">List of child overrides.</param>
        /// <param name="variants">Variant hashes for all types.</param>
        /// <returns><see cref="BlobAssetReference{T}"/> for <see cref="GhostPrefabBlobMetaData"/></returns>
        internal static BlobAssetReference<GhostPrefabBlobMetaData> CreateBlobAsset(
            Config ghostConfig, EntityManager entityManager, Entity rootEntity, NativeArray<Entity> linkedEntities,
            NativeList<ComponentType> allComponents, NativeArray<int> componentCounts,
            NetcodeConversionTarget target, NativeArray<GhostPrefabType> prefabTypes,
            NativeArray<int> sendMasksOverride, NativeArray<ulong> variants)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GhostPrefabBlobMetaData>();

            // Store importance, supported modes, default mode and name in the meta data blob asset
            root.Importance = ghostConfig.Importance;
            root.SupportedModes = GhostPrefabBlobMetaData.GhostMode.Both;
            root.DefaultMode = GhostPrefabBlobMetaData.GhostMode.Interpolated;
            if (ghostConfig.SupportedGhostModes == GhostModeMask.Interpolated)
                root.SupportedModes = GhostPrefabBlobMetaData.GhostMode.Interpolated;
            else if (ghostConfig.SupportedGhostModes == GhostModeMask.Predicted)
            {
                root.SupportedModes = GhostPrefabBlobMetaData.GhostMode.Predicted;
                root.DefaultMode = GhostPrefabBlobMetaData.GhostMode.Predicted;
            }
            else if (ghostConfig.DefaultGhostMode == GhostMode.OwnerPredicted)
            {
                if (!entityManager.HasComponent<GhostOwner>(rootEntity))
                    throw new InvalidOperationException("OwnerPrediction mode can only be used on prefabs which have a GhostOwner");
                root.DefaultMode = GhostPrefabBlobMetaData.GhostMode.Both;
            }
            else if (ghostConfig.DefaultGhostMode == GhostMode.Predicted)
            {
                root.DefaultMode = GhostPrefabBlobMetaData.GhostMode.Predicted;
            }
            root.StaticOptimization = (ghostConfig.OptimizationMode == GhostOptimizationMode.Static);
            builder.AllocateString(ref root.Name, ref ghostConfig.Name);

            var serverComponents = new NativeList<ulong>(allComponents.Length, Allocator.Temp);
            var serverVariants = new NativeList<ulong>(allComponents.Length, Allocator.Temp);
            var serverSendMasks = new NativeList<int>(allComponents.Length, Allocator.Temp);
            var removeOnServer = new NativeList<GhostPrefabBlobMetaData.ComponentReference>(allComponents.Length, Allocator.Temp);
            var removeOnClient = new NativeList<GhostPrefabBlobMetaData.ComponentReference>(allComponents.Length, Allocator.Temp);
            var disableOnPredicted = new NativeList<GhostPrefabBlobMetaData.ComponentReference>(allComponents.Length, Allocator.Temp);
            var disableOnInterpolated = new NativeList<GhostPrefabBlobMetaData.ComponentReference>(allComponents.Length, Allocator.Temp);

            // Snapshot data buffers should be removed from the server, and shared ghost type from the client
            removeOnServer.Add(new GhostPrefabBlobMetaData.ComponentReference(0,TypeManager.GetTypeInfo(ComponentType.ReadWrite<SnapshotData>().TypeIndex).StableTypeHash));
            removeOnServer.Add(new GhostPrefabBlobMetaData.ComponentReference(0,TypeManager.GetTypeInfo(ComponentType.ReadWrite<SnapshotDataBuffer>().TypeIndex).StableTypeHash));
            if(entityManager.HasComponent<SnapshotDynamicDataBuffer>(rootEntity))
                removeOnServer.Add(new GhostPrefabBlobMetaData.ComponentReference(0, TypeManager.GetTypeInfo(ComponentType.ReadWrite<SnapshotDynamicDataBuffer>().TypeIndex).StableTypeHash));

            // Remove predicted spawn request component from server in the client+server case, as the prefab asset needs to have it in this case but not in server world
            if (target == NetcodeConversionTarget.ClientAndServer && (ghostConfig.SupportedGhostModes & GhostModeMask.Predicted) == GhostModeMask.Predicted)
                removeOnServer.Add(new GhostPrefabBlobMetaData.ComponentReference(0, TypeManager.GetTypeInfo(ComponentType.ReadWrite<PredictedGhostSpawnRequest>().TypeIndex).StableTypeHash));

            // If both interpolated and predicted clients are supported the interpolated client needs to disable the prediction component
            // If the ghost is interpolated only the prediction component can be removed on clients
            if (ghostConfig.SupportedGhostModes == GhostModeMask.All)
                disableOnInterpolated.Add(new GhostPrefabBlobMetaData.ComponentReference(0, TypeManager.GetTypeInfo(ComponentType.ReadWrite<PredictedGhost>().TypeIndex).StableTypeHash));
            else if (ghostConfig.SupportedGhostModes == GhostModeMask.Interpolated)
                removeOnClient.Add(new GhostPrefabBlobMetaData.ComponentReference(0,TypeManager.GetTypeInfo(ComponentType.ReadWrite<PredictedGhost>().TypeIndex).StableTypeHash));

            var compIdx = 0;
            var blobNumServerComponentsPerEntity = builder.Allocate(ref root.NumServerComponentsPerEntity, linkedEntities.Length);
            for (int k = 0; k < linkedEntities.Length; ++k)
            {
                int prevCount = serverComponents.Length;
                var numComponents = componentCounts[k];
                for (int i=0;i<numComponents;++i, ++compIdx)
                {
                    var comp = allComponents[compIdx];
                    var prefabType = prefabTypes[compIdx];
                    var hash = TypeManager.GetTypeInfo(comp.TypeIndex).StableTypeHash;
                    if (prefabType == GhostPrefabType.All)
                    {
                        serverComponents.Add(hash);
                        serverSendMasks.Add(sendMasksOverride[compIdx]);
                        serverVariants.Add(variants[compIdx]);
                        continue;
                    }

                    bool isCommandData = typeof(ICommandData).IsAssignableFrom(comp.GetManagedType());
                    if (isCommandData)
                    {
                        //report warning for some configuration that imply stripping the component from some variants
                        if ((prefabType & GhostPrefabType.Server) == 0)
                            UnityEngine.Debug.LogWarning($"{ghostConfig.Name}: ICommandData {comp} is configured to be present only on the clients. Will be removed from server ghost prefab");
                        if ((prefabType & GhostPrefabType.Client) == 0)
                            UnityEngine.Debug.LogWarning($"{ghostConfig.Name}: ICommandData {comp} is configured to be present only on the server. Will be removed from from the client ghost prefab");
                        else if (prefabType == GhostPrefabType.InterpolatedClient)
                            UnityEngine.Debug.LogWarning($"{ghostConfig.Name}: ICommandData {comp} is configured to be present only on interpolated ghost. Will be removed from the server and predicted ghost prefab");
                        //Check the disabled components for potential and reportor warning for some cases
                        if (ghostConfig.SupportedGhostModes == GhostModeMask.All)
                        {
                            if ((prefabType & GhostPrefabType.InterpolatedClient) != 0 && (prefabType & GhostPrefabType.PredictedClient) == 0)
                                UnityEngine.Debug.LogWarning($"{ghostConfig.Name}: ICommandData {comp} is configured to be present only on interpolated ghost. Will be disabled on predicted ghost after spawning");
                        }
                    }
                    if ((prefabType & GhostPrefabType.Server) == 0)
                        removeOnServer.Add(new GhostPrefabBlobMetaData.ComponentReference(k,hash));
                    else
                    {
                        serverComponents.Add(hash);
                        serverSendMasks.Add(sendMasksOverride[compIdx]);
                        serverVariants.Add(variants[compIdx]);
                    }

                    // If something is not used on the client, remove it. Make sure to include things that is interpolated only if ghost
                    // is predicted only and the other way around
                    if ((prefabType & GhostPrefabType.Client) == 0)
                        removeOnClient.Add(new GhostPrefabBlobMetaData.ComponentReference(k,hash));
                    else if (ghostConfig.SupportedGhostModes == GhostModeMask.Interpolated && (prefabType & GhostPrefabType.InterpolatedClient) == 0)
                        removeOnClient.Add(new GhostPrefabBlobMetaData.ComponentReference(k,hash));
                    else if (ghostConfig.SupportedGhostModes == GhostModeMask.Predicted && (prefabType & GhostPrefabType.PredictedClient) == 0)
                        removeOnClient.Add(new GhostPrefabBlobMetaData.ComponentReference(k,hash));

                    // If the prefab only supports a single mode on the client there is no need to enable / disable, if is handled by the
                    // previous loop removing components on the client instead
                    if (ghostConfig.SupportedGhostModes == GhostModeMask.All)
                    {
                        // Components available on predicted but not interpolated should be disabled on interpolated clients
                        if ((prefabType & GhostPrefabType.InterpolatedClient) == 0 && (prefabType & GhostPrefabType.PredictedClient) != 0)
                            disableOnInterpolated.Add(new GhostPrefabBlobMetaData.ComponentReference(k,hash));
                        if ((prefabType & GhostPrefabType.InterpolatedClient) != 0 && (prefabType & GhostPrefabType.PredictedClient) == 0)
                            disableOnPredicted.Add(new GhostPrefabBlobMetaData.ComponentReference(k,hash));
                    }
                }
                blobNumServerComponentsPerEntity[k] = serverComponents.Length - prevCount;
            }
            var blobServerComponents = builder.Allocate(ref root.ServerComponentList, serverComponents.Length);
            for (int i = 0; i < serverComponents.Length; ++i)
            {
                blobServerComponents[i].StableHash = serverComponents[i];
                blobServerComponents[i].Variant = serverVariants[i];
                blobServerComponents[i].SendMaskOverride = serverSendMasks[i];
            }

            // A pre-spawned instance can be created in ClientServer even if the prefab is not, so anything which should
            // be usable on the server needs to know what to remove from the server version
            if (target != NetcodeConversionTarget.Client)
            {
                // Client only data never needs information about the server
                var blobRemoveOnServer = builder.Allocate(ref root.RemoveOnServer, removeOnServer.Length);
                for (int i = 0; i < removeOnServer.Length; ++i)
                    blobRemoveOnServer[i] = removeOnServer[i];
            }
            else
                builder.Allocate(ref root.RemoveOnServer, 0);
            if (target != NetcodeConversionTarget.Server)
            {
                var blobRemoveOnClient = builder.Allocate(ref root.RemoveOnClient, removeOnClient.Length);
                for (int i = 0; i < removeOnClient.Length; ++i)
                    blobRemoveOnClient[i] = removeOnClient[i];
            }
            else
                builder.Allocate(ref root.RemoveOnClient, 0);

            if (target != NetcodeConversionTarget.Server)
            {
                // The data for interpolated / predicted diff is required unless this is server-only
                var blobDisableOnPredicted = builder.Allocate(ref root.DisableOnPredictedClient, disableOnPredicted.Length);
                for (int i = 0; i < disableOnPredicted.Length; ++i)
                    blobDisableOnPredicted[i] = disableOnPredicted[i];
                var blobDisableOnInterpolated = builder.Allocate(ref root.DisableOnInterpolatedClient, disableOnInterpolated.Length);
                for (int i = 0; i < disableOnInterpolated.Length; ++i)
                    blobDisableOnInterpolated[i] = disableOnInterpolated[i];
            }
            else
            {
                builder.Allocate(ref root.DisableOnPredictedClient, 0);
                builder.Allocate(ref root.DisableOnInterpolatedClient, 0);
            }

            return builder.CreateBlobAssetReference<GhostPrefabBlobMetaData>(Allocator.Persistent);
        }

        /// <summary>
        /// Strip components which are not used, and add the ones which should always be present on a ghost prefab
        /// </summary>
        /// <param name="ghostConfig">Configuration used when creating ghost prefabs.</param>
        /// <param name="entityManager">Used to validate which components exists on <paramref name="rootEntity"/></param>
        /// <param name="rootEntity">Components existing on this entity, like <see cref="GhostOwner"/> is used to configure the result.</param>
        /// <param name="ghostType">Component storing the guid of the prefab the ghost was created from.</param>
        /// <param name="linkedEntities">List of all linked entities to the <paramref name="rootEntity"/></param>
        /// <param name="allComponents">List of all component types.</param>
        /// <param name="componentCounts">List of number of components on each index.</param>
        /// <param name="target"><see cref="NetcodeConversionTarget"/></param>
        /// <param name="prefabTypes">List of different types of <see cref="GhostPrefabType"/> to created.</param>
        public static void FinalizePrefabComponents(Config ghostConfig, EntityManager entityManager,
            Entity rootEntity, GhostType ghostType, NativeArray<Entity> linkedEntities,
            NativeList<ComponentType> allComponents, NativeArray<int> componentCounts,
            NetcodeConversionTarget target, NativeArray<GhostPrefabType> prefabTypes)
        {
            var entities = new NativeArray<Entity>(allComponents.Length, Allocator.Temp);
            int compIdx = 0;
            for (int k = 0; k < linkedEntities.Length; ++k)
            {
                var numComponents = componentCounts[k];
                var ent = linkedEntities[k];
                for (int i = 0; i < numComponents; ++i, ++compIdx)
                    entities[compIdx] = ent;
            }

            //Keep track of any component that should be removed on the client. Used later
            //to check if we need to add a DynamicSnapshotData component to the client ghost.
            //This simply the logic a little since this depend on the current serialization variant
            //chosen for the component
            var removedFromClient = new NativeArray<bool>(allComponents.Length, Allocator.Temp);

            if (target == NetcodeConversionTarget.Server)
            {
                // If converting server-only data we can remove all components which are not used on the server
                for (int i=0;i< allComponents.Length;++i)
                {
                    var comp = allComponents[i];
                    var prefabType = prefabTypes[i];
                    if((prefabType & GhostPrefabType.Server) == 0)
                    {
                        entityManager.RemoveComponent(entities[i], comp);
                        if(typeof(ICommandData).IsAssignableFrom(comp.GetManagedType()))
                            UnityEngine.Debug.LogWarning($"{ghostConfig.Name}: ICommandData {comp} is configured to be present only on client ghosts. Will be removed from from the server target");
                    }
                }
            }
            else if (target == NetcodeConversionTarget.Client)
            {
                // If converting client-only data we can remove all components which are not used on the client
                // If the ghost is interpolated only we can also remove all componens which are not used on interpolated clients,
                // and if it is predicted only we can remove everything which is not used on predicted clients
                for (int i=0;i< allComponents.Length;++i)
                {
                    var comp = allComponents[i];
                    var prefabType = prefabTypes[i];
                    if (prefabType == GhostPrefabType.All)
                        continue;
                    if(typeof(ICommandData).IsAssignableFrom(comp.GetManagedType()))
                    {
                        if ((prefabType & GhostPrefabType.Client) == 0)
                            UnityEngine.Debug.LogWarning($"{ghostConfig.Name}: ICommandData {comp} is configured to be present only on the server. Will be removed from from the client target");
                        else if (ghostConfig.SupportedGhostModes == GhostModeMask.Predicted && (prefabType & GhostPrefabType.PredictedClient) == 0)
                            UnityEngine.Debug.LogWarning($"{ghostConfig.Name}: ICommandData {comp} is configured to be present only on interpolated ghost. Will be removed from the client target");
                    }

                    if ((prefabType & GhostPrefabType.Client) == 0)
                    {
                        entityManager.RemoveComponent(entities[i], comp);
                        removedFromClient[i] = true;
                    }
                    else if (ghostConfig.SupportedGhostModes == GhostModeMask.Interpolated && (prefabType & GhostPrefabType.InterpolatedClient) == 0)
                    {
                        entityManager.RemoveComponent(entities[i], comp);
                        removedFromClient[i] = true;
                    }
                    else if (ghostConfig.SupportedGhostModes == GhostModeMask.Predicted && (prefabType & GhostPrefabType.PredictedClient) == 0)
                    {
                        entityManager.RemoveComponent(entities[i], comp);
                        removedFromClient[i] = true;
                    }
                }
            }
            // Even if converting for client and server we can remove components which are only for predicted clients when
            // the ghost is always interpolated, or components which are only for interpolated clients if the ghost is always
            // predicted
            else if (ghostConfig.SupportedGhostModes == GhostModeMask.Interpolated)
            {
                for (int i=0;i< allComponents.Length;++i)
                {
                    var comp = allComponents[i];
                    var prefabType = prefabTypes[i];
                    if ((prefabType & (GhostPrefabType.InterpolatedClient | GhostPrefabType.Server)) == 0)
                    {
                        entityManager.RemoveComponent(entities[i], comp);
                        removedFromClient[i] = true;
                    }
                }
            }
            else if (ghostConfig.SupportedGhostModes == GhostModeMask.Predicted)
            {
                for (int i=0;i< allComponents.Length;++i)
                {
                    var comp = allComponents[i];
                    var prefabType = prefabTypes[i];
                    if ((prefabType & (GhostPrefabType.PredictedClient | GhostPrefabType.Server)) == 0)
                    {
                        entityManager.RemoveComponent(entities[i], comp);
                        removedFromClient[i] = true;
                        if(typeof(ICommandData).IsAssignableFrom(comp.GetManagedType()))
                            UnityEngine.Debug.LogWarning($"{ghostConfig.Name}: ICommandData {comp} is configured to be present only on interpolated ghost. Will be removed from the client and server target");
                    }
                }
            }
            else
            {
                for (int i=0;i< allComponents.Length;++i)
                {
                    var comp = allComponents[i];
                    var prefabType = prefabTypes[i];
                    if (prefabType == 0)
                    {
                        entityManager.RemoveComponent(entities[i], comp);
                        removedFromClient[i] = true;
                    }
                }
            }

            entityManager.AddComponentData(rootEntity, ghostType);

            // FIXME: maybe stripping should be individual systems running before this to make sure it can be changed in a way that always triggers a reconvert - and to avoid reflection

            // we must add a shared ghost type to make sure different ghost types with the same archetype end up in different chunks. The problem
            // rely on the fact that ghosts with the same archetype may have different serialization rules. And the majority of the works (done on a per-chunk basis)
            // assumes that the chunks contains ghosts of the same type (in term or serialization).
            entityManager.AddSharedComponent(rootEntity, new GhostTypePartition {SharedValue = ghostType});

            // All types have the ghost components
            entityManager.AddComponentData(rootEntity, new GhostInstance());
            // No need to add the predicted ghost component for interpolated only ghosts if the data is only used by the client
            if (target != NetcodeConversionTarget.Client || ghostConfig.SupportedGhostModes != GhostModeMask.Interpolated)
                entityManager.AddComponentData(rootEntity, new PredictedGhost());
            if (ghostConfig.UsePreSerialization)
                entityManager.AddComponentData(rootEntity, default(PreSerializedGhost));

            var hasBuffers = false;
            //Check if the entity has any buffers left and SnapshotDynamicData buffer to for client. Must be stripped on server
            if (target != NetcodeConversionTarget.Server)
            {
                //if the prefab does not support at least one client mode (is server only) then there is no reason to add the dynamic buffer snapshot
                //Need to conside the variant serialization, that is why is using the removedFromClient
                for (int i = 0; i < allComponents.Length && !hasBuffers; ++i)
                    hasBuffers |= (allComponents[i].IsBuffer && !removedFromClient[i]) && (prefabTypes[i] & GhostPrefabType.Client) != 0;
                // Converting to client or client and server, if client and server this should be stripped from servers at runtime
                entityManager.AddComponentData(rootEntity, new SnapshotData());
                entityManager.AddBuffer<SnapshotDataBuffer>(rootEntity);
                if(hasBuffers)
                    entityManager.AddBuffer<SnapshotDynamicDataBuffer>(rootEntity);
            }

        }

        // This function will get all the components that are not baking types
        private static NativeArray<ComponentType> GetNotBakingComponentTypes(EntityManager entityManager, Entity entity, ComponentType linkedEntityGroupComponentType)
        {
            var components = entityManager.GetComponentTypes(entity);
            NativeList<ComponentType> relevantComponents = new NativeList<ComponentType>(components.Length, Allocator.Temp);

            // Remove all the baking components
            for (int index = 0; index < components.Length; ++index)
            {
                if ((components[index].TypeIndex & (TypeManager.BakingOnlyTypeFlag | TypeManager.TemporaryBakingTypeFlag)) == 0
                    && (components[index] != linkedEntityGroupComponentType))
                {
                    // We need to ignore this type as it is bake only
                    relevantComponents.Add(components[index]);
                }
            }
            return relevantComponents.AsArray();
        }

        /// <summary>
        /// Helper method to build a list of all component types on all children of a ghost prefab, should not be called directly.
        /// </summary>
        /// <param name="entityManager">Used to add components data on ghost children.</param>
        /// <param name="linkedEntities">Linked entities, 0 is the root followed by its children. Each will be marked with <see cref="GhostChildEntity"/></param>
        /// <param name="allComponents">Populated with root and child components.</param>
        /// <param name="componentCounts">Populated with each ghost's number of components.</param>
        public static void CollectAllComponents(EntityManager entityManager, NativeArray<Entity> linkedEntities, out NativeList<ComponentType> allComponents, out NativeArray<int> componentCounts)
        {
            var linkedEntityGroupComponentType = ComponentType.ReadWrite<LinkedEntityGroup>();

            var rootComponents = GetNotBakingComponentTypes(entityManager, linkedEntities[0], linkedEntityGroupComponentType);
            rootComponents.Sort(default(ComponentHashComparer));
            // Collects all hierarchy components
            allComponents = new NativeList<ComponentType>(rootComponents.Length*linkedEntities.Length, Allocator.Temp);
            componentCounts = new NativeArray<int>(linkedEntities.Length, Allocator.Temp);
            allComponents.AddRange(rootComponents);
            componentCounts[0] = rootComponents.Length;

            // Mark all child entities as ghost children, entity 0 is the root and should not have the GhostChildEntity
            for (int i = 1; i < linkedEntities.Length; ++i)
            {
                entityManager.AddComponentData(linkedEntities[i], default(GhostChildEntity));
                var childComponents = GetNotBakingComponentTypes(entityManager, linkedEntities[i], linkedEntityGroupComponentType);
                childComponents.Sort(default(ComponentHashComparer));
                allComponents.AddRange(childComponents);
                componentCounts[i] = childComponents.Length;
            }
        }

        /// <summary>
        /// Converts an entity to a ghost prefab, and registers it with the collection.
        /// This method will add the `Prefab` and `LinkedEntityGroup` components if they do not already exist.
        /// It will also add all component required for a prefab to be used as a ghost, and register it with the `GhostCollectionSystem`.
        /// The blob asset (which is created as part of making it a ghost prefab) is owned (and will be freed by) the `GhostCollectionSystem`.
        /// Thus, the calling code should not free the blob asset.
        /// The prefabs must be created exactly the same way on both the client and the server, and they must contain all components.
        /// Use component overrides if you want to have some components server or client only.
        /// </summary>
        /// <remarks>
        /// Note that - when using this in a System `OnCreate` method - you must ensure your system is created after the `DefaultVariantSystemGroup`,
        /// as we must register serialization strategies before you access them.
        /// </remarks>
        /// <param name="entityManager">Used to add components data on ghost children.</param>
        /// <param name="prefab">Entity prefab to be converted.</param>
        /// <param name="config">Configuration used to create a ghost prefab.</param>
        /// <param name="overrides">Override types for specific components.</param>
        public static void ConvertToGhostPrefab(EntityManager entityManager, Entity prefab,
            Config config,
            NativeParallelHashMap<Component, ComponentOverride> overrides = default)
        {
            // Make sure there is a valid overrides map to make the rest of this function easier
            if (!overrides.IsCreated)
                overrides = new NativeParallelHashMap<Component, ComponentOverride>(1, Allocator.Temp);

#if !DOTS_DISABLE_DEBUG_NAMES
            entityManager.GetName(prefab, out var name);
            if(name.IsEmpty)
                entityManager.SetName(prefab, config.Name);
#endif

            //the prefab tag must be added also to the child entities
            if (!entityManager.HasComponent<LinkedEntityGroup>(prefab))
            {
                var buffer = entityManager.AddBuffer<LinkedEntityGroup>(prefab);
                buffer.Add(prefab);
            }
            var linkedEntityBuffer = entityManager.GetBuffer<LinkedEntityGroup>(prefab);
            var linkedEntitiesArray = new NativeArray<Entity>(linkedEntityBuffer.Length, Allocator.Temp);
            for (int i = 0; i < linkedEntityBuffer.Length; ++i)
                linkedEntitiesArray[i] = linkedEntityBuffer[i].Value;
            //added here as second pass to avoid invalidating the buffer safety handle
            for (int i = 0; i < linkedEntitiesArray.Length; ++i)
                entityManager.AddComponent<Prefab>(linkedEntitiesArray[i]);

            var allComponents = default(NativeList<ComponentType>);
            var componentCounts = default(NativeArray<int>);
            if (!config.CollectComponentFunc.Ptr.IsCreated)
            {
                CollectAllComponents(entityManager, linkedEntitiesArray, out allComponents, out componentCounts);
            }
            else
            {
                allComponents = new NativeList<ComponentType>(256, Allocator.Temp);
                componentCounts = new NativeArray<int>(linkedEntitiesArray.Length, Allocator.Temp);
                config.CollectComponentFunc.Ptr.Invoke(GhostComponentSerializer.IntPtrCast(ref allComponents), GhostComponentSerializer.IntPtrCast(ref componentCounts));
            }

            var prefabTypes = new NativeArray<GhostPrefabType>(allComponents.Length, Allocator.Temp);
            var sendMasksOverride = new NativeArray<int>(allComponents.Length, Allocator.Temp);
            var variants = new NativeArray<ulong>(allComponents.Length, Allocator.Temp);

            // TODO - Consider changing the API to pass this as an arg.
            using var collectionDataQuery = entityManager.CreateEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<GhostComponentSerializerCollectionData>());
            var collectionData = collectionDataQuery.GetSingleton<GhostComponentSerializerCollectionData>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            {
                entityManager.GetName(prefab, out var prefabName);
                collectionData.ThrowIfCollectionNotFinalized($"ConvertToGhostPrefab on prefab '{prefab.ToFixedString()} ({prefabName})'");
            }
#endif

            int childIndex = 0;
            int childStart = 0;
            for (int i = 0; i < allComponents.Length; ++i)
            {
                while (i - childStart == componentCounts[childIndex])
                {
                    ++childIndex;
                    childStart = i;
                }
                var hasOverrides = overrides.TryGetValue(new Component{ComponentType = allComponents[i], ChildIndex = childIndex}, out var compOverride);
                ulong variant = 0;
                if (hasOverrides && (compOverride.OverrideType & ComponentOverrideType.Variant) != 0)
                    variant = compOverride.Variant;

                var variantType = collectionData.GetCurrentSerializationStrategyForComponent(allComponents[i], variant, childIndex == 0);
                prefabTypes[i] = variantType.PrefabType;
                sendMasksOverride[i] = -1;
                variants[i] = variantType.Hash;
                if (hasOverrides)
                {
                    if ((compOverride.OverrideType & ComponentOverrideType.PrefabType) != 0)
                        prefabTypes[i] = compOverride.PrefabType;
                    if ((compOverride.OverrideType & ComponentOverrideType.SendMask) != 0)
                        sendMasksOverride[i] = (int)compOverride.SendMask;
                }
            }

            NetcodeConversionTarget target = (entityManager.World.IsServer()) ? NetcodeConversionTarget.Server : NetcodeConversionTarget.Client;
            // Calculate a uuid v5 using the guid of this .cs file as namespace and the prefab name as name. See rfc 4122 for more info on uuid5
            // TODO: should probably be the raw bytes from the namespace guid + name
            var uuid5 = new SHA1($"f17641b8-279a-94b1-1b84-487e72d49ab5{config.Name}");
            // I need an unique identifier and should not clash with any loaded prefab, use uuid5 with a namespace + ghost name
            var ghostType = uuid5.ToGhostType();

            //This should be present only for prefabs. FinalizePrefabComponents is also called for not prefab entities so it should not
            //be added there.
            if(target != NetcodeConversionTarget.Server && config.SupportedGhostModes != GhostModeMask.Interpolated)
                entityManager.AddComponent<PredictedGhostSpawnRequest>(prefab);

            FinalizePrefabComponents(config, entityManager, prefab, ghostType, linkedEntitiesArray,
                        allComponents, componentCounts, target, prefabTypes);

            using var codePrefabQuery = entityManager.CreateEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<CodeGhostPrefab>());
            if (!codePrefabQuery.TryGetSingletonEntity<CodeGhostPrefab>(out var codePrefabSingleton))
                codePrefabSingleton = entityManager.CreateSingletonBuffer<CodeGhostPrefab>();
            var codePrefabs = entityManager.GetBuffer<CodeGhostPrefab>(codePrefabSingleton);

#if NETCODE_DEBUG
            for (int i = 0; i < codePrefabs.Length; ++i)
            {
                if (entityManager.GetComponentData<GhostType>(codePrefabs[i].entity) == ghostType)
                {
                    throw new InvalidOperationException("Duplicate ghost prefab found, all ghost prefabs must have a unique name");
                }
            }
            #endif

            var blobAsset = CreateBlobAsset(config, entityManager, prefab, linkedEntitiesArray,
                allComponents, componentCounts, target, prefabTypes, sendMasksOverride, variants);
            codePrefabs.Add(new CodeGhostPrefab{entity = prefab, blob = blobAsset});
            entityManager.AddComponentData(prefab, new GhostPrefabMetaData
            {
                Value = blobAsset
            });
        }
		
		public interface IEntityManagerWrapper
		{
			public void Dispose();
			public bool HasComponent<T>(Entity entity);
			public void AddComponent<T>(Entity entity) where T : unmanaged;
			public void AddComponent<T>(Entity entity, T componentData) where T : unmanaged, IComponentData;
			public void RemoveComponent<T>(Entity entity) where T : unmanaged, IComponentData;
			public void RemoveComponent(Entity entity, ComponentType type);
			public void AddSharedComponent<T>(Entity entity, T component) where T : unmanaged, ISharedComponentData;
			public void SetComponentEnabled<T>(Entity entity, bool enabled) where T : unmanaged, IComponentData, IEnableableComponent;
			public DynamicBuffer<T> GetBuffer<T>(Entity entity) where T : unmanaged, IBufferElementData;
			public void AddBuffer<T>(Entity entity) where T : unmanaged, IBufferElementData;
			public void Playback(EntityManager entityManager);
			public NativeArray<ComponentType> GetComponentTypes(Entity entity, Allocator allocator = Allocator.Temp);
		}
		
        internal static BlobAssetReference<GhostPrefabBlobMetaData> CreateBlobAsset(
            Config ghostConfig, IEntityManagerWrapper entityManager, Entity rootEntity, NativeArray<Entity> linkedEntities,
            NativeList<ComponentType> allComponents, NativeArray<int> componentCounts,
            NetcodeConversionTarget target, NativeArray<GhostPrefabType> prefabTypes,
            NativeArray<int> sendMasksOverride, NativeArray<ulong> variants)
        {
			// As of 2024-03-29, almost all ghost prefabs result in a blob size of less than 2KB, with only a handful
			// reaching 4KB. Reducing the chunk size avoids a 100MB memory spike from Allocator.Temp during conversion.
            var builder = new BlobBuilder(Allocator.Temp, chunkSize: 2048);
            ref var root = ref builder.ConstructRoot<GhostPrefabBlobMetaData>();

            // Store importance, supported modes, default mode and name in the meta data blob asset
            root.Importance = ghostConfig.Importance;
            root.SupportedModes = GhostPrefabBlobMetaData.GhostMode.Both;
            root.DefaultMode = GhostPrefabBlobMetaData.GhostMode.Interpolated;
            if (ghostConfig.SupportedGhostModes == GhostModeMask.Interpolated)
                root.SupportedModes = GhostPrefabBlobMetaData.GhostMode.Interpolated;
            else if (ghostConfig.SupportedGhostModes == GhostModeMask.Predicted)
            {
                root.SupportedModes = GhostPrefabBlobMetaData.GhostMode.Predicted;
                root.DefaultMode = GhostPrefabBlobMetaData.GhostMode.Predicted;
            }
            else if (ghostConfig.DefaultGhostMode == GhostMode.OwnerPredicted)
            {
                if (!entityManager.HasComponent<GhostOwner>(rootEntity))
                    throw new InvalidOperationException("OwnerPrediction mode can only be used on prefabs which have a GhostOwner");
                root.DefaultMode = GhostPrefabBlobMetaData.GhostMode.Both;
            }
            else if (ghostConfig.DefaultGhostMode == GhostMode.Predicted)
            {
                root.DefaultMode = GhostPrefabBlobMetaData.GhostMode.Predicted;
            }
            root.StaticOptimization = (ghostConfig.OptimizationMode == GhostOptimizationMode.Static);
            builder.AllocateString(ref root.Name, ref ghostConfig.Name);

            var serverComponents = new NativeList<ulong>(allComponents.Length, Allocator.Temp);
            var serverVariants = new NativeList<ulong>(allComponents.Length, Allocator.Temp);
            var serverSendMasks = new NativeList<int>(allComponents.Length, Allocator.Temp);
            var removeOnServer = new NativeList<GhostPrefabBlobMetaData.ComponentReference>(allComponents.Length, Allocator.Temp);
            var removeOnClient = new NativeList<GhostPrefabBlobMetaData.ComponentReference>(allComponents.Length, Allocator.Temp);
            var disableOnPredicted = new NativeList<GhostPrefabBlobMetaData.ComponentReference>(allComponents.Length, Allocator.Temp);
            var disableOnInterpolated = new NativeList<GhostPrefabBlobMetaData.ComponentReference>(allComponents.Length, Allocator.Temp);

            // Snapshot data buffers should be removed from the server, and shared ghost type from the client
            removeOnServer.Add(new GhostPrefabBlobMetaData.ComponentReference(0,TypeManager.GetTypeInfo(ComponentType.ReadWrite<SnapshotData>().TypeIndex).StableTypeHash));
            removeOnServer.Add(new GhostPrefabBlobMetaData.ComponentReference(0,TypeManager.GetTypeInfo(ComponentType.ReadWrite<SnapshotDataBuffer>().TypeIndex).StableTypeHash));
            if(entityManager.HasComponent<SnapshotDynamicDataBuffer>(rootEntity))
                removeOnServer.Add(new GhostPrefabBlobMetaData.ComponentReference(0, TypeManager.GetTypeInfo(ComponentType.ReadWrite<SnapshotDynamicDataBuffer>().TypeIndex).StableTypeHash));

            // Remove predicted spawn request component from server in the client+server case, as the prefab asset needs to have it in this case but not in server world
            if (target == NetcodeConversionTarget.ClientAndServer && (ghostConfig.SupportedGhostModes & GhostModeMask.Predicted) == GhostModeMask.Predicted)
                removeOnServer.Add(new GhostPrefabBlobMetaData.ComponentReference(0, TypeManager.GetTypeInfo(ComponentType.ReadWrite<PredictedGhostSpawnRequest>().TypeIndex).StableTypeHash));

            // If both interpolated and predicted clients are supported the interpolated client needs to disable the prediction component
            // If the ghost is interpolated only the prediction component can be removed on clients
            if (ghostConfig.SupportedGhostModes == GhostModeMask.All)
                disableOnInterpolated.Add(new GhostPrefabBlobMetaData.ComponentReference(0, TypeManager.GetTypeInfo(ComponentType.ReadWrite<PredictedGhost>().TypeIndex).StableTypeHash));
            else if (ghostConfig.SupportedGhostModes == GhostModeMask.Interpolated)
                removeOnClient.Add(new GhostPrefabBlobMetaData.ComponentReference(0,TypeManager.GetTypeInfo(ComponentType.ReadWrite<PredictedGhost>().TypeIndex).StableTypeHash));

            var compIdx = 0;
            var blobNumServerComponentsPerEntity = builder.Allocate(ref root.NumServerComponentsPerEntity, linkedEntities.Length);
            for (int k = 0; k < linkedEntities.Length; ++k)
            {
                int prevCount = serverComponents.Length;
                var numComponents = componentCounts[k];
                for (int i=0;i<numComponents;++i, ++compIdx)
                {
                    var comp = allComponents[compIdx];
                    var prefabType = prefabTypes[compIdx];
                    var hash = TypeManager.GetTypeInfo(comp.TypeIndex).StableTypeHash;
                    if (prefabType == GhostPrefabType.All)
                    {
                        serverComponents.Add(hash);
                        serverSendMasks.Add(sendMasksOverride[compIdx]);
                        serverVariants.Add(variants[compIdx]);
                        continue;
                    }

                    bool isCommandData = typeof(ICommandData).IsAssignableFrom(comp.GetManagedType());
                    if (isCommandData)
                    {
                        //report warning for some configuration that imply stripping the component from some variants
                        if ((prefabType & GhostPrefabType.Server) == 0)
                            UnityEngine.Debug.LogWarning($"{ghostConfig.Name}: ICommandData {comp} is configured to be present only on the clients. Will be removed from server ghost prefab");
                        if ((prefabType & GhostPrefabType.Client) == 0)
                            UnityEngine.Debug.LogWarning($"{ghostConfig.Name}: ICommandData {comp} is configured to be present only on the server. Will be removed from from the client ghost prefab");
                        else if (prefabType == GhostPrefabType.InterpolatedClient)
                            UnityEngine.Debug.LogWarning($"{ghostConfig.Name}: ICommandData {comp} is configured to be present only on interpolated ghost. Will be removed from the server and predicted ghost prefab");
                        //Check the disabled components for potential and reportor warning for some cases
                        if (ghostConfig.SupportedGhostModes == GhostModeMask.All)
                        {
                            if ((prefabType & GhostPrefabType.InterpolatedClient) != 0 && (prefabType & GhostPrefabType.PredictedClient) == 0)
                                UnityEngine.Debug.LogWarning($"{ghostConfig.Name}: ICommandData {comp} is configured to be present only on interpolated ghost. Will be disabled on predicted ghost after spawning");
                        }
                    }
                    if ((prefabType & GhostPrefabType.Server) == 0)
                        removeOnServer.Add(new GhostPrefabBlobMetaData.ComponentReference(k,hash));
                    else
                    {
                        serverComponents.Add(hash);
                        serverSendMasks.Add(sendMasksOverride[compIdx]);
                        serverVariants.Add(variants[compIdx]);
                    }

                    // If something is not used on the client, remove it. Make sure to include things that is interpolated only if ghost
                    // is predicted only and the other way around
                    if ((prefabType & GhostPrefabType.Client) == 0)
                        removeOnClient.Add(new GhostPrefabBlobMetaData.ComponentReference(k,hash));
                    else if (ghostConfig.SupportedGhostModes == GhostModeMask.Interpolated && (prefabType & GhostPrefabType.InterpolatedClient) == 0)
                        removeOnClient.Add(new GhostPrefabBlobMetaData.ComponentReference(k,hash));
                    else if (ghostConfig.SupportedGhostModes == GhostModeMask.Predicted && (prefabType & GhostPrefabType.PredictedClient) == 0)
                        removeOnClient.Add(new GhostPrefabBlobMetaData.ComponentReference(k,hash));

                    // If the prefab only supports a single mode on the client there is no need to enable / disable, if is handled by the
                    // previous loop removing components on the client instead
                    if (ghostConfig.SupportedGhostModes == GhostModeMask.All)
                    {
                        // Components available on predicted but not interpolated should be disabled on interpolated clients
                        if ((prefabType & GhostPrefabType.InterpolatedClient) == 0 && (prefabType & GhostPrefabType.PredictedClient) != 0)
                            disableOnInterpolated.Add(new GhostPrefabBlobMetaData.ComponentReference(k,hash));
                        if ((prefabType & GhostPrefabType.InterpolatedClient) != 0 && (prefabType & GhostPrefabType.PredictedClient) == 0)
                            disableOnPredicted.Add(new GhostPrefabBlobMetaData.ComponentReference(k,hash));
                    }
                }
                blobNumServerComponentsPerEntity[k] = serverComponents.Length - prevCount;
            }
            var blobServerComponents = builder.Allocate(ref root.ServerComponentList, serverComponents.Length);
            for (int i = 0; i < serverComponents.Length; ++i)
            {
                blobServerComponents[i].StableHash = serverComponents[i];
                blobServerComponents[i].Variant = serverVariants[i];
                blobServerComponents[i].SendMaskOverride = serverSendMasks[i];
            }

            // A pre-spawned instance can be created in ClientServer even if the prefab is not, so anything which should
            // be usable on the server needs to know what to remove from the server version
            if (target != NetcodeConversionTarget.Client)
            {
                // Client only data never needs information about the server
                var blobRemoveOnServer = builder.Allocate(ref root.RemoveOnServer, removeOnServer.Length);
                for (int i = 0; i < removeOnServer.Length; ++i)
                    blobRemoveOnServer[i] = removeOnServer[i];
            }
            else
                builder.Allocate(ref root.RemoveOnServer, 0);
            if (target != NetcodeConversionTarget.Server)
            {
                var blobRemoveOnClient = builder.Allocate(ref root.RemoveOnClient, removeOnClient.Length);
                for (int i = 0; i < removeOnClient.Length; ++i)
                    blobRemoveOnClient[i] = removeOnClient[i];
            }
            else
                builder.Allocate(ref root.RemoveOnClient, 0);

            if (target != NetcodeConversionTarget.Server)
            {
                // The data for interpolated / predicted diff is required unless this is server-only
                var blobDisableOnPredicted = builder.Allocate(ref root.DisableOnPredictedClient, disableOnPredicted.Length);
                for (int i = 0; i < disableOnPredicted.Length; ++i)
                    blobDisableOnPredicted[i] = disableOnPredicted[i];
                var blobDisableOnInterpolated = builder.Allocate(ref root.DisableOnInterpolatedClient, disableOnInterpolated.Length);
                for (int i = 0; i < disableOnInterpolated.Length; ++i)
                    blobDisableOnInterpolated[i] = disableOnInterpolated[i];
            }
            else
            {
                builder.Allocate(ref root.DisableOnPredictedClient, 0);
                builder.Allocate(ref root.DisableOnInterpolatedClient, 0);
            }

            return builder.CreateBlobAssetReference<GhostPrefabBlobMetaData>(Allocator.Persistent);
        }
		
		public static void FinalizePrefabComponents(Config ghostConfig, IEntityManagerWrapper entityManager,
			Entity rootEntity, GhostType ghostType, NativeArray<Entity> linkedEntities,
			NativeList<ComponentType> allComponents, NativeArray<int> componentCounts,
			NetcodeConversionTarget target, NativeArray<GhostPrefabType> prefabTypes)
		{
			var entities = new NativeArray<Entity>(allComponents.Length, Allocator.Temp);
			int compIdx = 0;
			for (int k = 0; k < linkedEntities.Length; ++k)
			{
				var numComponents = componentCounts[k];
				var ent = linkedEntities[k];
				for (int i = 0; i < numComponents; ++i, ++compIdx)
					entities[compIdx] = ent;
			}

			//Keep track of any component that should be removed on the client. Used later
			//to check if we need to add a DynamicSnapshotData component to the client ghost.
			//This simply the logic a little since this depend on the current serialization variant
			//chosen for the component
			var removedFromClient = new NativeArray<bool>(allComponents.Length, Allocator.Temp);

			if (target == NetcodeConversionTarget.Server)
			{
				// If converting server-only data we can remove all components which are not used on the server
				for (int i=0;i< allComponents.Length;++i)
				{
					var comp = allComponents[i];
					var prefabType = prefabTypes[i];
					if((prefabType & GhostPrefabType.Server) == 0)
					{
						entityManager.RemoveComponent(entities[i], comp);
						if(typeof(ICommandData).IsAssignableFrom(comp.GetManagedType()))
							UnityEngine.Debug.LogWarning($"{ghostConfig.Name}: ICommandData {comp} is configured to be present only on client ghosts. Will be removed from from the server target");
					}
				}
			}
			else if (target == NetcodeConversionTarget.Client)
			{
				// If converting client-only data we can remove all components which are not used on the client
				// If the ghost is interpolated only we can also remove all componens which are not used on interpolated clients,
				// and if it is predicted only we can remove everything which is not used on predicted clients
				for (int i=0;i< allComponents.Length;++i)
				{
					var comp = allComponents[i];
					var prefabType = prefabTypes[i];
					if (prefabType == GhostPrefabType.All)
						continue;
					if(typeof(ICommandData).IsAssignableFrom(comp.GetManagedType()))
					{
						if ((prefabType & GhostPrefabType.Client) == 0)
							UnityEngine.Debug.LogWarning($"{ghostConfig.Name}: ICommandData {comp} is configured to be present only on the server. Will be removed from from the client target");
						else if (ghostConfig.SupportedGhostModes == GhostModeMask.Predicted && (prefabType & GhostPrefabType.PredictedClient) == 0)
							UnityEngine.Debug.LogWarning($"{ghostConfig.Name}: ICommandData {comp} is configured to be present only on interpolated ghost. Will be removed from the client target");
					}

					if ((prefabType & GhostPrefabType.Client) == 0)
					{
						entityManager.RemoveComponent(entities[i], comp);
						removedFromClient[i] = true;
					}
					else if (ghostConfig.SupportedGhostModes == GhostModeMask.Interpolated && (prefabType & GhostPrefabType.InterpolatedClient) == 0)
					{
						entityManager.RemoveComponent(entities[i], comp);
						removedFromClient[i] = true;
					}
					else if (ghostConfig.SupportedGhostModes == GhostModeMask.Predicted && (prefabType & GhostPrefabType.PredictedClient) == 0)
					{
						entityManager.RemoveComponent(entities[i], comp);
						removedFromClient[i] = true;
					}
				}
			}
			// Even if converting for client and server we can remove components which are only for predicted clients when
			// the ghost is always interpolated, or components which are only for interpolated clients if the ghost is always
			// predicted
			else if (ghostConfig.SupportedGhostModes == GhostModeMask.Interpolated)
			{
				for (int i=0;i< allComponents.Length;++i)
				{
					var comp = allComponents[i];
					var prefabType = prefabTypes[i];
					if ((prefabType & (GhostPrefabType.InterpolatedClient | GhostPrefabType.Server)) == 0)
					{
						entityManager.RemoveComponent(entities[i], comp);
						removedFromClient[i] = true;
					}
				}
			}
			else if (ghostConfig.SupportedGhostModes == GhostModeMask.Predicted)
			{
				for (int i=0;i< allComponents.Length;++i)
				{
					var comp = allComponents[i];
					var prefabType = prefabTypes[i];
					if ((prefabType & (GhostPrefabType.PredictedClient | GhostPrefabType.Server)) == 0)
					{
						entityManager.RemoveComponent(entities[i], comp);
						removedFromClient[i] = true;
						if(typeof(ICommandData).IsAssignableFrom(comp.GetManagedType()))
							UnityEngine.Debug.LogWarning($"{ghostConfig.Name}: ICommandData {comp} is configured to be present only on interpolated ghost. Will be removed from the client and server target");
					}
				}
			}
			else
			{
				for (int i=0;i< allComponents.Length;++i)
				{
					var comp = allComponents[i];
					var prefabType = prefabTypes[i];
					if (prefabType == 0)
					{
						entityManager.RemoveComponent(entities[i], comp);
						removedFromClient[i] = true;
					}
				}
			}

			entityManager.AddComponent(rootEntity, ghostType);

			// FIXME: maybe stripping should be individual systems running before this to make sure it can be changed in a way that always triggers a reconvert - and to avoid reflection

			// we must add a shared ghost type to make sure different ghost types with the same archetype end up in different chunks. The problem
			// rely on the fact that ghosts with the same archetype may have different serialization rules. And the majority of the works (done on a per-chunk basis)
			// assumes that the chunks contains ghosts of the same type (in term or serialization).
			entityManager.AddSharedComponent(rootEntity, new GhostTypePartition {SharedValue = ghostType});

			// All types have the ghost components
			entityManager.AddComponent(rootEntity, new GhostInstance());
			// No need to add the predicted ghost component for interpolated only ghosts if the data is only used by the client
			if (target != NetcodeConversionTarget.Client || ghostConfig.SupportedGhostModes != GhostModeMask.Interpolated)
				entityManager.AddComponent(rootEntity, new PredictedGhost());
			if (ghostConfig.UsePreSerialization)
				entityManager.AddComponent(rootEntity, default(PreSerializedGhost));

			var hasBuffers = false;
			//Check if the entity has any buffers left and SnapshotDynamicData buffer to for client. Must be stripped on server
			if (target != NetcodeConversionTarget.Server)
			{
				//if the prefab does not support at least one client mode (is server only) then there is no reason to add the dynamic buffer snapshot
				//Need to conside the variant serialization, that is why is using the removedFromClient
				for (int i = 0; i < allComponents.Length && !hasBuffers; ++i)
					hasBuffers |= (allComponents[i].IsBuffer && !removedFromClient[i]) && (prefabTypes[i] & GhostPrefabType.Client) != 0;
				// Converting to client or client and server, if client and server this should be stripped from servers at runtime
				entityManager.AddComponent(rootEntity, new SnapshotData());
				entityManager.AddBuffer<SnapshotDataBuffer>(rootEntity);
				if(hasBuffers)
					entityManager.AddBuffer<SnapshotDynamicDataBuffer>(rootEntity);
			}
		}

        // This function will get all the components that are not baking types
        private static NativeArray<ComponentType> GetNotBakingComponentTypes(IEntityManagerWrapper entityManager, Entity entity, ComponentType linkedEntityGroupComponentType)
        {
            var components = entityManager.GetComponentTypes(entity);
            NativeList<ComponentType> relevantComponents = new NativeList<ComponentType>(components.Length, Allocator.Temp);

            // Remove all the baking components
            for (int index = 0; index < components.Length; ++index)
            {
                if ((components[index].TypeIndex & (TypeManager.BakingOnlyTypeFlag | TypeManager.TemporaryBakingTypeFlag)) == 0
                    && (components[index] != linkedEntityGroupComponentType))
                {
                    // We need to ignore this type as it is bake only
                    relevantComponents.Add(components[index]);
                }
            }
            return relevantComponents.AsArray();
        }

        /// <summary>
        /// Helper method to build a list of all component types on all children of a ghost prefab, should not be called directly.
        /// </summary>
        /// <param name="entityManager">Used to add components data on ghost children.</param>
        /// <param name="linkedEntities">Linked entities, 0 is the root followed by its children. Each will be marked with <see cref="GhostChildEntity"/></param>
        /// <param name="allComponents">Populated with root and child components.</param>
        /// <param name="componentCounts">Populated with each ghost's number of components.</param>
        public static void CollectAllComponents(IEntityManagerWrapper entityManager, NativeArray<Entity> linkedEntities, out NativeList<ComponentType> allComponents, out NativeArray<int> componentCounts)
        {
            var linkedEntityGroupComponentType = ComponentType.ReadWrite<LinkedEntityGroup>();

            var rootComponents = GetNotBakingComponentTypes(entityManager, linkedEntities[0], linkedEntityGroupComponentType);
            rootComponents.Sort(default(ComponentHashComparer));
            // Collects all hierarchy components
            allComponents = new NativeList<ComponentType>(rootComponents.Length*linkedEntities.Length, Allocator.Temp);
            componentCounts = new NativeArray<int>(linkedEntities.Length, Allocator.Temp);
            allComponents.AddRange(rootComponents);
            componentCounts[0] = rootComponents.Length;

            // Mark all child entities as ghost children, entity 0 is the root and should not have the GhostChildEntity
            for (int i = 1; i < linkedEntities.Length; ++i)
            {
                entityManager.AddComponent(linkedEntities[i], default(GhostChildEntity));
                var childComponents = GetNotBakingComponentTypes(entityManager, linkedEntities[i], linkedEntityGroupComponentType);
                childComponents.Sort(default(ComponentHashComparer));
                allComponents.AddRange(childComponents);
                componentCounts[i] = childComponents.Length;
            }
        }
		
        public static void ConvertToGhostPrefab(IEntityManagerWrapper entityManager, Entity prefab, GhostType ghostType,
            NetcodeConversionTarget target, Config config, GhostComponentSerializerCollectionData collectionData, BlobAssetStore blobAssetStore,
            NativeParallelHashMap<Component, ComponentOverride> overrides = default)
        {
            // Make sure there is a valid overrides map to make the rest of this function easier
            if (!overrides.IsCreated)
                overrides = new NativeParallelHashMap<Component, ComponentOverride>(1, Allocator.Temp);

			UnityEngine.Debug.Assert(entityManager.HasComponent<Prefab>(prefab));
			UnityEngine.Debug.Assert(entityManager.HasComponent<LinkedEntityGroup>(prefab));
			
            var linkedEntityBuffer = entityManager.GetBuffer<LinkedEntityGroup>(prefab);
            var linkedEntitiesArray = new NativeArray<Entity>(linkedEntityBuffer.Length, Allocator.Temp);
            for (int i = 0; i < linkedEntityBuffer.Length; ++i)
                linkedEntitiesArray[i] = linkedEntityBuffer[i].Value;
            
            var allComponents = default(NativeList<ComponentType>);
            var componentCounts = default(NativeArray<int>);
            if (!config.CollectComponentFunc.Ptr.IsCreated)
            {
                CollectAllComponents(entityManager, linkedEntitiesArray, out allComponents, out componentCounts);
            }
            else
            {
                allComponents = new NativeList<ComponentType>(256, Allocator.Temp);
                componentCounts = new NativeArray<int>(linkedEntitiesArray.Length, Allocator.Temp);
                config.CollectComponentFunc.Ptr.Invoke(GhostComponentSerializer.IntPtrCast(ref allComponents), GhostComponentSerializer.IntPtrCast(ref componentCounts));
            }

            var prefabTypes = new NativeArray<GhostPrefabType>(allComponents.Length, Allocator.Temp);
            var sendMasksOverride = new NativeArray<int>(allComponents.Length, Allocator.Temp);
            var variants = new NativeArray<ulong>(allComponents.Length, Allocator.Temp);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            {
                collectionData.ThrowIfCollectionNotFinalized($"ConvertToGhostPrefab on prefab '{prefab.ToFixedString()}'");
            }
#endif

            int childIndex = 0;
            int childStart = 0;
            for (int i = 0; i < allComponents.Length; ++i)
            {
                while (i - childStart == componentCounts[childIndex])
                {
                    ++childIndex;
                    childStart = i;
                }
                var hasOverrides = overrides.TryGetValue(new Component{ComponentType = allComponents[i], ChildIndex = childIndex}, out var compOverride);
                ulong variant = 0;
                if (hasOverrides && (compOverride.OverrideType & ComponentOverrideType.Variant) != 0)
                    variant = compOverride.Variant;

                var variantType = collectionData.GetCurrentSerializationStrategyForComponent(allComponents[i], variant, true);
                prefabTypes[i] = variantType.PrefabType;
                sendMasksOverride[i] = -1;
                variants[i] = variantType.Hash;
                if (hasOverrides)
                {
                    if ((compOverride.OverrideType & ComponentOverrideType.PrefabType) != 0)
                        prefabTypes[i] = compOverride.PrefabType;
                    if ((compOverride.OverrideType & ComponentOverrideType.SendMask) != 0)
                        sendMasksOverride[i] = (int)compOverride.SendMask;
                }
            }

			if (target == NetcodeConversionTarget.ClientAndServer)
				entityManager.AddComponent<GhostPrefabRuntimeStrip>(prefab);

            //This should be present only for prefabs. FinalizePrefabComponents is also called for not prefab entities so it should not
            //be added there.
            if(target != NetcodeConversionTarget.Server && config.SupportedGhostModes != GhostModeMask.Interpolated)
                entityManager.AddComponent<PredictedGhostSpawnRequest>(prefab);

            FinalizePrefabComponents(config, entityManager, prefab, ghostType, linkedEntitiesArray,
                        allComponents, componentCounts, target, prefabTypes);

            var blobAsset = CreateBlobAsset(config, entityManager, prefab, linkedEntitiesArray,
                allComponents, componentCounts, target, prefabTypes, sendMasksOverride, variants);

			blobAssetStore.TryAdd(ref blobAsset);
			
            entityManager.AddComponent(prefab, new GhostPrefabMetaData
            {
                Value = blobAsset
            });
        }
    }
}
