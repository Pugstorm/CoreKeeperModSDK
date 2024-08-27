using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode.LowLevel.Unsafe;

namespace Unity.NetCode
{

    // TODO - Make internal if possible.
    /// <summary>
    /// <para>
    /// For internal use only. Stores individual "serialization strategies" (and meta-data) for all netcode-informed components,
    /// as well as all variants of these components (<see cref="GhostComponentVariationAttribute"/>).
    /// Thus, maps to the code-generated <see cref="GhostComponentSerializer"/> ("Default Serializers") as well as
    /// all user-created Variants (<see cref="GhostComponentVariationAttribute"/>).
    /// This type also stores instances of the <see cref="DontSerializeVariant"/>, <see cref="ClientOnlyVariant"/>, and <see cref="ServerOnlyVariant"/>.
    /// </para>
    /// <para>
    /// Note: Serializers are considered "optional". It is perfectly valid for a types "serialization strategy" to be: "Do nothing".
    /// An example of this is a component for which a variant has been declared (using the <see cref="GhostComponentVariationAttribute"/>)
    /// but for which serialization is not generated, i.e: the <see cref="GhostInstance"/> attribute is specified in
    /// the base component declaration, but not in a variant. We call these "Empty Variants".
    /// </para>
    /// </summary>
    /// <remarks>This type was renamed from "VariantType" for 1.0.</remarks>
    public struct ComponentTypeSerializationStrategy : IComparable<ComponentTypeSerializationStrategy>
    {
        /// <summary>Denotes why this strategy is the default (or not). Higher value = more important.</summary>
        /// <remarks>This is a flags enum, so there may be multiple reasons why a strategy is considered the default.</remarks>
        [Flags]
        public enum DefaultType : byte
        {
            /// <summary>This is not the default.</summary>
            NotDefault = 0,
            /// <summary>It's an editor test variant, so we should default to it if we literally don't have any other defaults.</summary>
            YesAsEditorDefault = 1 << 1,
            /// <summary>This is the default variant only because we could not find a suitable one.</summary>
            YesAsIsFallback = 1 << 2,
            /// <summary>Child entities default to <see cref="DontSerializeVariant"/>.</summary>
            YesAsIsChildDefaultingToDontSerializeVariant = 1 << 3,
            /// <summary>
            /// The default serializer should be used. Only applicable if we're serialized.
            /// On children: This only applies if the user has set flag <see cref="GhostComponentAttribute.SendDataForChildEntity"/> on the default serializer.
            /// </summary>
            YesAsIsDefaultSerializerAndDefaultIsUnchanged = 1 << 4,
            /// <summary>If the developer has created only 1 variant for a type, it becomes the default.</summary>
            YesAsOnlyOneVariantBecomesDefault = 1 << 5,
            /// <summary>This is the default variant selected by the user (via <see cref="DefaultVariantSystemBase"/>), and thus is higher priority than <see cref="YesAsIsDefaultSerializerAndDefaultIsUnchanged"/>.</summary>
            YesAsIsUserSpecifiedNewDefault = 1 << 6,
            /// <summary>This is a default variant because the user has marked it as such (via a ComponentOverride). Highest priority.</summary>
            YesViaUserSpecifiedNamedDefaultOrHash = 1 << 7,
        }

        /// <summary>Indexer into <see cref="GhostComponentSerializerCollectionData.SerializationStrategies"/> list.</summary>
        public short SelfIndex;
        /// <summary>Indexes into the <see cref="GhostComponentSerializerCollectionData.Serializers"/>.</summary>
        /// <remarks>Serializers are optional. Thus, 0 if this type does not serialize component data.</remarks>
        public short SerializerIndex;
        /// <summary>Component that this Variant is associated with.</summary>
        public ComponentType Component;
        /// <summary>Hash identifier for the strategy. Should be non-zero by the time it's used in <see cref="GhostComponentSerializerCollectionData.SelectSerializationStrategyForComponentWithHash"/>.</summary>
        public ulong Hash;
        /// <summary>
        /// The <see cref="GhostPrefabType"/> value set in <see cref="GhostInstance"/> present in the variant declaration.
        /// Some variants modify the serialization rules. Default is <see cref="GhostPrefabType.All"/>
        /// </summary>
        public GhostPrefabType PrefabType;
        ///<summary>Override which client type it will be sent to, if we're able to determine.</summary>
        public GhostSendType SendTypeOptimization;
        /// <summary><see cref="DefaultType"/></summary>
        public DefaultType DefaultRule;
        // TODO - Create a flag byte enum for all of these.
        /// <summary>
        /// True if this is the "default" serializer for this component type.
        /// I.e. The one generated from the component definition itself (see <see cref="GhostFieldAttribute"/> and <see cref="GhostComponentAttribute"/>).
        /// </summary>
        /// <remarks>Types like `Translation` don't have a default serializer as the type itself doesn't define any GhostFields, but they do have serialized variants.</remarks>
        public byte IsDefaultSerializer;
        /// <summary><inheritdoc cref="GhostComponentVariationAttribute.IsTestVariant"/></summary>
        /// <remarks>True if this is an editor test variant. Forces this variant to be considered a "default" which makes writing tests easier.</remarks>
        public byte IsTestVariant;
        /// <summary>True if the <see cref="GhostComponentAttribute.SendDataForChildEntity"/> flag is true on this variant (if it has one), or this type (if not).</summary>
        public byte SendForChildEntities;
        /// <summary>True if the code-generator determined that this is an input component (or a variant of one).</summary>
        public byte IsInputComponent;
        /// <summary>True if the code-generator determined that this is an input buffer.</summary>
        public byte IsInputBuffer;
        /// <summary>Does this component explicitly opt-out of overrides (regardless of variant count)?</summary>
        public byte HasDontSupportPrefabOverridesAttribute;

        /// <summary><see cref="IsInputComponent"/> and <see cref="IsInputBuffer"/>.</summary>
        internal byte IsInput => (byte) (IsInputComponent | IsInputBuffer);
        /// <summary>The type name, unless it has a Variant (in which case it'll use the Variant Display name... assuming that is not null).</summary>
        public FixedString64Bytes DisplayName;
        /// <summary>True if this variant serializes its data.</summary>
        /// <remarks>Note that this will also be true if the type has the attribute <see cref="GhostEnabledBitAttribute"/>.</remarks>
        public byte IsSerialized => (byte) (SerializerIndex >= 0 ? 1 : 0);
        /// <summary>True if this variant is the <see cref="DontSerializeVariant"/>.</summary>
        public bool IsDontSerializeVariant => Hash == GhostVariantsUtility.DontSerializeHash;
        /// <summary>True if this variant is the <see cref="ClientOnlyVariant"/>.</summary>
        public bool IsClientOnlyVariant => Hash == GhostVariantsUtility.ClientOnlyHash;

        /// <summary>
        /// Check if two VariantType are identical.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(ComponentTypeSerializationStrategy other)
        {
            if (IsSerialized != other.IsSerialized)
                return IsSerialized - other.IsSerialized;
            if (DefaultRule != other.DefaultRule)
                return (int)DefaultRule - (int)other.DefaultRule;
            if (Hash != other.Hash)
                return Hash < other.Hash ? -1 : 1;
            return 0;
        }

        /// <summary>
        /// Convert the instance to its string representation.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => ToFixedString().ToString();

        /// <summary>Logs a burst compatible debug string (if in burst), otherwise logs even more info.</summary>
        /// <returns>A debug string.</returns>
        public FixedString512Bytes ToFixedString()
        {
            var fs = new FixedString512Bytes((FixedString32Bytes) $"SS<");
            fs.Append(Component.GetDebugTypeName());
            fs.Append((FixedString128Bytes) $">[{DisplayName}, H:{Hash}, DR:{(int) DefaultRule}, SI:{SerializerIndex}, PT:{(int) PrefabType}, self:{SelfIndex}, child:{SendForChildEntities}]");
            return fs;
        }

        internal static FixedString32Bytes GetDefaultDisplayName(ComponentTypeSerializationStrategy.DefaultType defaultRule)
        {
            if ((defaultRule & ComponentTypeSerializationStrategy.DefaultType.YesViaUserSpecifiedNamedDefaultOrHash) != 0)
                return "Chosen";
            if ((defaultRule & ComponentTypeSerializationStrategy.DefaultType.YesAsIsUserSpecifiedNewDefault) != 0)
                return "User-Specified Default";
            if ((defaultRule & ComponentTypeSerializationStrategy.DefaultType.YesAsOnlyOneVariantBecomesDefault) != 0)
                return "Default as Only Variant";
            if ((defaultRule & ComponentTypeSerializationStrategy.DefaultType.YesAsIsDefaultSerializerAndDefaultIsUnchanged) != 0)
                return "Default Serializer";
            if ((defaultRule & ComponentTypeSerializationStrategy.DefaultType.YesAsIsFallback) != 0)
                return "Fallback";
            if ((defaultRule & ComponentTypeSerializationStrategy.DefaultType.YesAsEditorDefault) != 0)
                return "Editor-Only Default";
            return defaultRule == DefaultType.NotDefault ? "" : "Default";
        }
    }

    /// <summary>
    /// Parent group of all code-generated systems that registers the ghost component serializers to the <see cref="GhostCollection"/>,
    /// more specifically to the <see cref="GhostComponentSerializer.State"/> collection) at runtime.
    /// For internal use only, don't add systems to this group.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.BakingSystem,
        WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.BakingSystem)]
    [CreateBefore(typeof(DefaultVariantSystemGroup))]
    public partial class GhostComponentSerializerCollectionSystemGroup : ComponentSystemGroup
    {
        /// <summary>HashSets and HashTables have a fixed capacity.</summary>
        /// <remarks>Increase this if you have lots of variants. Hardcoded multiplier is due to DontSerializeVariants.</remarks>
        public static int CollectionDefaultCapacity = (int) (DynamicTypeList.MaxCapacity * 2.2);

        /// <summary>Hacky workaround for GetSingleton not working on frame 0 (not sure why, as creation order is correct).</summary>
        internal GhostComponentSerializerCollectionData ghostComponentSerializerCollectionDataCache { get; private set; }

        /// <summary>
        /// Used to store the default ghost component variation mapping during the world creation.
        /// </summary>
        internal GhostVariantRules DefaultVariantRules { get; private set; }

        struct NeverCreatedSingleton : IComponentData
        {}

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<NeverCreatedSingleton>();
            var worldNameShortened = new FixedString32Bytes();
            FixedStringMethods.CopyFromTruncated(ref worldNameShortened, World.Unmanaged.Name);
            ghostComponentSerializerCollectionDataCache = new GhostComponentSerializerCollectionData
            {
                WorldName = worldNameShortened,
                CollectionFinalized = new NativeReference<byte>(Allocator.Persistent),
                Serializers = new NativeList<GhostComponentSerializer.State>(CollectionDefaultCapacity, Allocator.Persistent),
                SerializationStrategies = new NativeList<ComponentTypeSerializationStrategy>(CollectionDefaultCapacity, Allocator.Persistent),
                SerializationStrategiesComponentTypeMap = new NativeParallelMultiHashMap<ComponentType, short>(CollectionDefaultCapacity, Allocator.Persistent),
                DefaultVariants = new NativeHashMap<ComponentType, DefaultVariantSystemBase.HashRule>(CollectionDefaultCapacity, Allocator.Persistent),
                InputComponentBufferMap = new NativeHashMap<ComponentType, ComponentType>(CollectionDefaultCapacity, Allocator.Persistent),
            };
            DefaultVariantRules = new GhostVariantRules(ghostComponentSerializerCollectionDataCache.DefaultVariants);
            //ATTENTION! this entity is destroyed in the BakingWorld, because in the first import this is what it does, it clean all the Entities in the world when you
            //open a scene.
            //For that reason, is the current world is a Baking word. this entity is "lazily" recreated by the GhostAuthoringBakingSystem if missing.
            EntityManager.CreateSingleton(ghostComponentSerializerCollectionDataCache);
        }

        protected override void OnDestroy()
        {
            ghostComponentSerializerCollectionDataCache.Dispose();
            ghostComponentSerializerCollectionDataCache = default;
            DefaultVariantRules = null;
            base.OnDestroy();
        }
    }

    /// <summary><see cref="GhostComponentSerializerCollectionSystemGroup"/>. Blittable. For internal use only.</summary>
    [StructLayout(LayoutKind.Sequential)]
    [BurstCompile]
    public struct GhostComponentSerializerCollectionData : IComponentData
    {
        internal NativeReference<byte> CollectionFinalized;

        /// <summary>
        /// All the Serializers. Allows us to serialize <see cref="ComponentType"/>'s to the snapshot.
        /// </summary>
        internal NativeList<GhostComponentSerializer.State> Serializers;
        /// <summary>
        /// Stores all known code-forced default variants.
        /// </summary>
        internal NativeHashMap<ComponentType, DefaultVariantSystemBase.HashRule> DefaultVariants;
        /// <summary>
        /// Every netcode-related ComponentType needs a "strategy" for serializing it. This stores all of them.
        /// </summary>
        internal NativeList<ComponentTypeSerializationStrategy> SerializationStrategies;
        /// <summary>
        /// Maps a given <see cref="ComponentType"/> to an entry in the <see cref="SerializationStrategies"/> collection.
        /// </summary>
        internal NativeParallelMultiHashMap<ComponentType, short> SerializationStrategiesComponentTypeMap;
        /// <summary>
        /// Map to look up the buffer type to use for an IInputComponentData type. Only used for baking purpose.
        /// </summary>
        internal NativeHashMap<ComponentType, ComponentType> InputComponentBufferMap;
        /// <summary>
        /// For debugging and exception strings.
        /// </summary>
        internal FixedString32Bytes WorldName;

        ulong HashGhostComponentSerializer(in GhostComponentSerializer.State comp)
        {
            //this will give us a good starting point
            var compHash = TypeManager.GetTypeInfo(comp.ComponentType.TypeIndex).StableTypeHash;
            if(compHash == 0)
                throw new InvalidOperationException($"'{WorldName}': Unexpected 0 hash for type {comp.ComponentType}!");
            compHash = TypeHash.CombineFNV1A64(compHash, comp.GhostFieldsHash);
            //ComponentSize might depend on #ifdef or other compilation/platform rules so it must be not included. we will leave the comment here
            //so it is clear why we don't consider this field
            //compHash = TypeHash.CombineFNV1A64(compHash, TypeHash.FNV1A64(comp.ComponentSize));
            compHash = TypeHash.CombineFNV1A64(compHash, TypeHash.FNV1A64(comp.SnapshotSize));
            compHash = TypeHash.CombineFNV1A64(compHash, TypeHash.FNV1A64(comp.ChangeMaskBits));
            compHash = TypeHash.CombineFNV1A64(compHash, TypeHash.FNV1A64((int)comp.SendToOwner));
            return compHash;
        }

        /// <summary>
        /// Used by code-generated systems to register SerializationStrategies.
        /// Internal use only.
        /// </summary>
        /// <param name="serializationStrategy"></param>
        public void AddSerializationStrategy(ref ComponentTypeSerializationStrategy serializationStrategy)
        {
            ThrowIfNotInRegistrationPhase("register a SerializationStrategy");

            // Validate that source-generator hashes don't collide.
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            ThrowIfNoHash(serializationStrategy.Hash, serializationStrategy.ToFixedString());
            if (serializationStrategy.DisplayName.IsEmpty)
            {
                UnityEngine.Debug.LogError($"{serializationStrategy.ToFixedString()} doesn't have a valid DisplayName! Ensure you set it, even if it's just to the ComponentType name.");
                serializationStrategy.DisplayName.CopyFromTruncated(serializationStrategy.Component.ToFixedString());
            }

            foreach (var existingSSIndex in SerializationStrategiesComponentTypeMap.GetValuesForKey(serializationStrategy.Component))
            {
                var existingSs = SerializationStrategies[existingSSIndex];
                if (existingSs.Hash == serializationStrategy.Hash || existingSs.DisplayName == serializationStrategy.DisplayName)
                {
                    UnityEngine.Debug.LogError($"{serializationStrategy.ToFixedString()} has the same Hash or DisplayName as already-added one (below)! Likely error in code-generation, must fix!\n{existingSs.ToFixedString()}!");
                }
            }
#endif

            AddSerializationStrategyInternal(ref serializationStrategy);
        }

        /// <summary>
        /// Internal method to register a SerializationStrategy. Hash collisions fine, as long as they are one of the "special" types (<see cref="DontSerializeVariant"/>, <see cref="ClientOnlyVariant"/>, <see cref="ServerOnlyVariant"/>).
        /// </summary>
        /// <remarks>
        /// Note that we may generate lots of <see cref="DontSerializeVariant"/>'s (2 per type), depending on different contexts.
        /// </remarks>
        private void AddSerializationStrategyInternal(ref ComponentTypeSerializationStrategy serializationStrategy)
        {
            serializationStrategy.SelfIndex = (short) SerializationStrategies.Length;
            SerializationStrategies.Add(serializationStrategy);
            SerializationStrategiesComponentTypeMap.Add(serializationStrategy.Component, serializationStrategy.SelfIndex);
        }

        /// <summary>
        /// Used by code-generated systems and meant for internal use only.
        /// Adds the generated ghost serializer to <see cref="GhostComponentSerializer.State"/> collection.
        /// </summary>
        /// <param name="state"></param>
        public void AddSerializer(GhostComponentSerializer.State state)
        {
            ThrowIfNotInRegistrationPhase("register a Serializer");

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            ThrowIfNoHash(state.VariantHash, $"'{WorldName}': AddSerializer for '{state.ComponentType}'.");
#endif

            // Map to SerializationStrategy:
            MapSerializerToStrategy(ref state, (short) Serializers.Length);
            state.SerializerHash = HashGhostComponentSerializer(state);
            Serializers.Add(state);
        }

        /// <summary>
        /// We have no idea how many code-generated types are left to be registered, so instead,
        /// we have a flag that is set when we know ALL of them have been created.
        /// If the user queries this collection BEFORE all queries have been created, then they used to get silent errors where GhostFields default to `DontSerializeVariant`.
        /// This throw highlights that user-error.
        /// </summary>
        /// <param name="context">The context of this call, to aid in error reporting.</param>
        /// <exception cref="InvalidOperationException">Throws if user-code queries too early.</exception>
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void ThrowIfNotInRegistrationPhase(in FixedString512Bytes context)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if(!CollectionFinalized.IsCreated)
                throw new InvalidOperationException($"'{WorldName}': Fatal error: Attempting to {context} but OnCreate has not yet been called! You must delay this registration call to after the creation of the `GhostComponentSerializerCollectionSystemGroup`.");
            if (CollectionFinalized.Value != 0)
                throw new InvalidOperationException($"'{WorldName}': Fatal error: Attempting to {context} but we've already finalized or queried this collection! You must ensure that, when called from `OnCreate`, your system uses attribute `[CreateBefore(typeof(DefaultVariantSystemGroup))]`.");
#endif
        }

        /// <summary>
        /// We have no idea how many code-generated types are left to be registered, so instead,
        /// we have a flag that is set when we know ALL of them have been created.
        /// If the user queries this collection BEFORE all queries have been created, then they'll get silent errors where GhostFields default to `DontSerializeVariant`.
        /// </summary>
        /// <param name="context">The context of this call, to aid in error reporting.</param>
        /// <exception cref="InvalidOperationException">Throws if user-code queries too early.</exception>
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void ThrowIfCollectionNotFinalized(in FixedString512Bytes context)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!CollectionFinalized.IsCreated || CollectionFinalized.Value == 0)
                throw new InvalidOperationException($"'{WorldName}': Fatal error: Attempting to {context} but we have not yet finalized this collection! You must delay your call until after the creation of the `DefaultVariantSystemGroup` (e.g. `[CreateAfter(typeof(DefaultVariantSystemGroup))]` on your system).");
#endif
       }

        /// <summary>
        /// Lookup a component type to use as a buffer for a given IInputComponentData.
        /// </summary>
        /// <param name="inputType"></param>
        /// <param name="bufferType"></param>
        /// <returns>True if the component has an assosiated buffer to use, false if it does not.</returns>
        [Obsolete("TryGetBufferForInputComponent has been deprecated. In order to find the buffer associated with an IInputComponentData please just use" +
                  "IInputBuffer<T> where T is the IInputComponentData type you are looking for.", false)]
        public bool TryGetBufferForInputComponent(ComponentType inputType, out ComponentType bufferType)
        {
            bufferType = default;
            return false;
        }

        /// <summary>
        /// Used by code-generated systems and meant for internal use only.
        /// Adds a mapping from an IInputComponentData to the buffer it should use.
        /// </summary>
        /// <param name="inputType"></param>
        /// <param name="bufferType"></param>
        public void AddInputComponent(ComponentType inputType, ComponentType bufferType)
        {
            InputComponentBufferMap.TryAdd(inputType, bufferType);
        }
        internal void MapSerializerToStrategy(ref GhostComponentSerializer.State state, short serializerIndex)
        {
            foreach (var ssIndex in SerializationStrategiesComponentTypeMap.GetValuesForKey(state.ComponentType))
            {
                ref var ss = ref SerializationStrategies.ElementAt(ssIndex);
                if (ss.Hash == state.VariantHash)
                {
                    state.SerializationStrategyIndex = ssIndex;
                    ss.SerializerIndex = serializerIndex;
                    return;
                }
            }

            throw new InvalidOperationException($"{WorldName}: No SerializationStrategy found for Serializer with Hash: {state.VariantHash}!");
        }

        /// <summary>
        /// Finds the <see cref="chosenVariant"/> for this <see cref="componentType"/> (from available variants via <see cref="GetAllAvailableSerializationStrategiesForType"/>).
        /// </summary>
        /// <param name="componentType">The type we're finding the SS for.</param>
        /// <param name="chosenVariantHash"> If set, denotes a specific variant should be used. 0 implies "use default".
        /// Note that, at runtime, we've already converted child variants to either specific serializers, or the `DontSerializeVariant`.
        /// Without this nuance, this code would break.</param>
        /// <param name="isRoot">True if the entity is a root entity, false if it's a child.
        /// This distinction is because child entities default to <see cref="DontSerializeVariant"/>.</param>
        [BurstCompile]
        internal ComponentTypeSerializationStrategy GetCurrentSerializationStrategyForComponent(ComponentType componentType, ulong chosenVariantHash, bool isRoot)
        {
            using var available = GetAllAvailableSerializationStrategiesForType(componentType, chosenVariantHash, isRoot);
            return SelectSerializationStrategyForComponentWithHash(componentType, chosenVariantHash, in available, isRoot);
        }

        /// <inheritdoc cref="GetCurrentSerializationStrategyForComponent"/>
        internal ComponentTypeSerializationStrategy SelectSerializationStrategyForComponentWithHash(ComponentType componentType, ulong chosenVariantHash, in NativeList<ComponentTypeSerializationStrategy> available, bool isRoot)
        {
            if (available.Length != 0)
            {
                if (chosenVariantHash == 0)
                {
                    // Find the best default ss:
                    var bestIndex = 0;
                    for (var i = 1; i < available.Length; i++)
                    {
                        var bestSs = available[bestIndex];
                        var availableSs = available[i];
                        if (availableSs.DefaultRule > bestSs.DefaultRule)
                        {
                            bestIndex = i;
                        }
                        else if (availableSs.DefaultRule == bestSs.DefaultRule)
                        {
                            if (availableSs.DefaultRule != ComponentTypeSerializationStrategy.DefaultType.NotDefault)
                            {
                                BurstCompatibleErrorWithAggregate(componentType, in available, $"Type `{componentType.ToFixedString()}` (isRoot: {isRoot} with chosenVariantHash '{chosenVariantHash}') has 2 or more default serialization strategies with the same `DefaultRule` ({(int) availableSs.DefaultRule})! Using the first.");
                            }
                        }
                    }

                    var finalVariant = available[bestIndex];
                    if (finalVariant.DefaultRule != ComponentTypeSerializationStrategy.DefaultType.NotDefault)
                    {
                        // The best default variant we've found isn't serialized on children anyway, so replace it with the DontSerializeVariant.
                        if (!finalVariant.IsDontSerializeVariant && !isRoot && finalVariant.SendForChildEntities == 0)
                        {
                            if (TryFindDontSerializeIndex(in available, out int dontSerializeIndex))
                                return available[dontSerializeIndex];
                            return ConstructDontSerializeVariant(in available, componentType, ComponentTypeSerializationStrategy.DefaultType.YesAsIsFallback, bestIndex, nameof(DontSerializeVariant));
                        }
                        return finalVariant;
                    }

                    // We failed, so get the safest fallback:
                    var fallback = GetSafestFallbackVariantUponError(available);
                    BurstCompatibleErrorWithAggregate(componentType, in available, $"Type `{componentType.ToFixedString()}` (isRoot: {isRoot} with chosenVariantHash '{chosenVariantHash}') has NO default serialization strategies! Calculating the safest fallback guess ('{fallback.ToFixedString()}').");
                    return fallback;
                }

                // Find the EXACT variant by hash.
                foreach (var variant in available)
                    if (variant.Hash == chosenVariantHash)
                        return variant;

                // Couldn't find any, so try to get the safest fallback:
                if (available.Length != 0)
                {
                    var fallback = GetSafestFallbackVariantUponError(available);
                    BurstCompatibleErrorWithAggregate(componentType, in available, $"Failed to find serialization strategy for `{componentType.ToFixedString()}` (isRoot: {isRoot}) with chosenVariantHash '{chosenVariantHash}'! There are {available.Length} serialization strategies available, so calculating the safest fallback guess ('{fallback.ToFixedString()}').");
                    return fallback;
                }
            }

            // Failed to find anything, so fallback:
            BurstCompatibleErrorWithAggregate(componentType, in available, $"Unable to find chosenVariantHash '{chosenVariantHash}' for `{componentType.ToFixedString()}` (isRoot: {isRoot}) as no serialization strategies available for type! Fallback is `DontSerializeVariant`.");
            TryFindDefaultSerializerIndex(in available, out var sourceVariantIndex);
            if (TryFindDontSerializeIndex(in available, out var dontSerializeIndexFallback))
                return available[dontSerializeIndexFallback];
            return ConstructDontSerializeVariant(in available, componentType, ComponentTypeSerializationStrategy.DefaultType.YesAsIsFallback, sourceVariantIndex, $"{nameof(DontSerializeVariant)} (Fallback)");
        }

        /// <summary>When we are unable to find the requested variant, this method finds the best fallback.</summary>
        static ComponentTypeSerializationStrategy GetSafestFallbackVariantUponError(in NativeList<ComponentTypeSerializationStrategy> available)
        {
            // Prefer to serialize all data on the ghost. Potentially wasteful, but "safest" as data will be replicated.
            for (var i = 0; i < available.Length; i++)
            {
                if (available[i].IsSerialized != 0 && available[i].IsDefaultSerializer != 0)
                    return available[i];
            }

            // Otherwise fallback to a serialized variant.
            for (var i = 0; i < available.Length; i++)
            {
                if (available[i].IsSerialized != 0)
                    return available[i];
            }

            // Otherwise fallback to the last in the list (most likely to be custom).
            return available[available.Length - 1];
        }

        /// <summary>
        /// <para><b>Finds all available variants for a given type, applying all variant rules at once.</b></para>
        /// <para>Since multiple variants can be present for any given component there are some important use cases that need to be
        /// handled.</para>
        /// <para> Note that, for <see cref="InputBufferData{T}"/>s, they'll return the variants available to their <see cref="IInputComponentData"/> authoring struct.</para>
        /// <para> Note that the number of default variants returned may not be 1 (it could be more or less).</para>
        /// </summary>
        /// <param name="componentType">Type to find the variant for.</param>
        /// <param name="chosenVariantHash"> If set, indicates that a variant has specifically been asked for (as an override). Zero implies find default.</param>
        /// <param name="isRoot">True if this component is on the root entity.</param>
        /// <returns>A list of all available variants for this `componentType`.</returns>
        [BurstCompile]
        public NativeList<ComponentTypeSerializationStrategy> GetAllAvailableSerializationStrategiesForType(ComponentType componentType, ulong chosenVariantHash, bool isRoot)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            ThrowIfCollectionNotFinalized($"attempting to GetAllAvailableSerializationStrategiesForType({componentType.ToFixedString()}, hash: {chosenVariantHash}, isRoot: {isRoot})");
#endif

            var availableVariants = new NativeList<ComponentTypeSerializationStrategy>(4, Allocator.Temp);
            var numCustomVariants = 0;
            var customVariantIndex = -1;
            var alreadyAddedDontSerializeVariant = false;
            var alreadyAddedClientOnlyVariant = false;
            var alreadyAddedServerOnlyVariant = false;

            // Code-gen: "Serialization Strategies" are generated and mapped here.
            // Any SS's that we CREATE are also added to this map, so it essentially acts like a dynamic cache.
            foreach (var strategyLookup in SerializationStrategiesComponentTypeMap.GetValuesForKey(componentType))
            {
                var strategy = SerializationStrategies[strategyLookup];
                strategy.DefaultRule = CalculateDefaultTypeForSerializer(componentType, isRoot, strategy.IsSerialized > 0, strategy.IsDefaultSerializer, strategy.IsInput, strategy.Hash, ref strategy.SendForChildEntities, chosenVariantHash);
                AddAndCount(ref strategy);
            }

            // `ClientOnlyVariant` special case:
            ComponentTypeSerializationStrategy.DefaultType defaultType;
            if (!alreadyAddedClientOnlyVariant && VariantIsUserSpecifiedDefaultRule(componentType, GhostVariantsUtility.ClientOnlyHash, isRoot, chosenVariantHash, out defaultType))
            {
                var clientOnlyVariant = new ComponentTypeSerializationStrategy
                {
                    Component = componentType,
                    DefaultRule = defaultType,
                    SerializerIndex = -1, // Client only so non-serialized.
                    SelfIndex = -1, // Hardcoded index lookup.
                    PrefabType = GhostPrefabType.Client,
                    Hash = GhostVariantsUtility.ClientOnlyHash,
                    DisplayName = GhostVariantsUtility.k_ClientOnlyVariant,
                };
                AddSerializationStrategyInternal(ref clientOnlyVariant);

                AddAndCount(ref clientOnlyVariant);
            }
            // `ServerOnlyVariant` special case:
            if (!alreadyAddedServerOnlyVariant && VariantIsUserSpecifiedDefaultRule(componentType, GhostVariantsUtility.ServerOnlyHash, isRoot, chosenVariantHash, out defaultType))
            {
                var serverOnlyVariant = new ComponentTypeSerializationStrategy
                {
                    Component = componentType,
                    DefaultRule = defaultType,
                    SerializerIndex = -1, // Server only so non-serialized.
                    SelfIndex = -1, // Hardcoded index lookup.
                    PrefabType = GhostPrefabType.Server,
                    Hash = GhostVariantsUtility.ServerOnlyHash,
                    DisplayName = GhostVariantsUtility.k_ServerOnlyVariant,
                };
                AddSerializationStrategyInternal(ref serverOnlyVariant);

                AddAndCount(ref serverOnlyVariant);
            }

            // `DontSerializeVariant` special case:
            if (!alreadyAddedDontSerializeVariant && !IsInput(availableVariants))
            {
                // We only want to add the `DontSerializeVariant` specifically asked for, or otherwise useful.
                if ((VariantIsUserSpecifiedDefaultRule(componentType, GhostVariantsUtility.DontSerializeHash, isRoot, chosenVariantHash, out _)) || !TryFindDontSerializeIndex(in availableVariants, out _))
                {
                    byte sendForChildEntities = 0;
                    var defaultTypeForDontSerializeVariant = CalculateDefaultTypeForSerializer(componentType, isRoot, false, 0, 0, GhostVariantsUtility.DontSerializeHash, ref sendForChildEntities, chosenVariantHash);
                    TryFindDefaultSerializerIndex(in availableVariants, out var sourceVariantIndex);
                    var dontSerializeVariant = ConstructDontSerializeVariant(availableVariants, componentType, defaultTypeForDontSerializeVariant, sourceVariantIndex, nameof(DontSerializeVariant));

                    AddAndCount(ref dontSerializeVariant);
                }
            }

            // If the type only has one custom variant, that is now the default:
            if (numCustomVariants == 1)
            {
                ref var customVariantFallback = ref availableVariants.ElementAt(customVariantIndex);
                customVariantFallback.DefaultRule |= ComponentTypeSerializationStrategy.DefaultType.YesAsOnlyOneVariantBecomesDefault;
            }

            // Finalize:
            availableVariants.Sort();

            return availableVariants;

            void AddAndCount(ref ComponentTypeSerializationStrategy variant)
            {
                if (IsUserCreatedVariant(variant.Hash, variant.IsDefaultSerializer))
                {
                    numCustomVariants++;
                    customVariantIndex = availableVariants.Length;
                }

                if (variant.IsTestVariant != 0)
                {
                    variant.DefaultRule |= ComponentTypeSerializationStrategy.DefaultType.YesAsEditorDefault;
                }

                // If the user picked this variant for this specific child, we know they want to serialize it.
                const ComponentTypeSerializationStrategy.DefaultType userPicked = ComponentTypeSerializationStrategy.DefaultType.YesViaUserSpecifiedNamedDefaultOrHash | ComponentTypeSerializationStrategy.DefaultType.YesAsIsUserSpecifiedNewDefault;
                var isUserSpecifiedVariant = (variant.DefaultRule & userPicked) != 0;
                if (isUserSpecifiedVariant && !isRoot) // Don't care if serialized or not, as that'll be handled later. This flag implies intent.
                {
                    variant.SendForChildEntities = 1;
                }

                availableVariants.Add(variant);
                alreadyAddedDontSerializeVariant |= variant.Hash == GhostVariantsUtility.DontSerializeHash;
                alreadyAddedClientOnlyVariant |= variant.Hash == GhostVariantsUtility.ClientOnlyHash;
                alreadyAddedServerOnlyVariant |= variant.Hash == GhostVariantsUtility.ServerOnlyHash;
            }

            static bool IsUserCreatedVariant(ulong variantTypeHash, byte isDefaultSerializer)
            {
                return isDefaultSerializer == 0 && variantTypeHash != GhostVariantsUtility.DontSerializeHash && variantTypeHash != GhostVariantsUtility.ClientOnlyHash;
            }
        }

        private static bool TryFindDefaultSerializerIndex(in NativeList<ComponentTypeSerializationStrategy> availableVariants, out int defaultSerializerIndex)
        {
            for (defaultSerializerIndex = 0; defaultSerializerIndex < availableVariants.Length; defaultSerializerIndex++)
            {
                if (availableVariants[defaultSerializerIndex].IsDefaultSerializer > 0)
                    return true;
            }
            defaultSerializerIndex = -1;
            return false;
        }

        private static bool TryFindDontSerializeIndex(in NativeList<ComponentTypeSerializationStrategy> availableVariants, out int dontSerializeIndex)
        {
            for (dontSerializeIndex = 0; dontSerializeIndex < availableVariants.Length; dontSerializeIndex++)
            {
                if (availableVariants[dontSerializeIndex].IsDontSerializeVariant)
                    return true;
            }
            dontSerializeIndex = -1;
            return false;
        }

        static bool IsInput(NativeList<ComponentTypeSerializationStrategy> availableVariants)
        {
            foreach (var ss in availableVariants)
                if(ss.IsInput != 0)
                    return true;
            return false;
        }

        ComponentTypeSerializationStrategy ConstructDontSerializeVariant(in NativeList<ComponentTypeSerializationStrategy> availableVariants, ComponentType componentType, ComponentTypeSerializationStrategy.DefaultType defaultRule, int sourceVariantIndex, string displayName)
        {
            var dontSerializeVariant = new ComponentTypeSerializationStrategy
            {
                Component = componentType,
                DefaultRule = default, // We set this AFTER adding it to the map, so repeated runs are not invalidated.
                SerializerIndex = -1,
                SelfIndex = -1,
                PrefabType = GhostPrefabType.All,
                Hash = GhostVariantsUtility.DontSerializeHash,
                DisplayName = displayName,
            };

            // Copy over variant data from the default serializer, as we should use the same settings for this variant.
            // Example use-case: User has Component `Foo` which is 'PrefabType.Server', and not serialized on children.
            // Child therefore use the `DontSerializeVariant`, but the `DontSerializeVariant` must inherit 'PrefabType.Server'.
            if(sourceVariantIndex >= 0)
            {
                var defaultSerializer = availableVariants[sourceVariantIndex];
                dontSerializeVariant.PrefabType = defaultSerializer.PrefabType;
                dontSerializeVariant.SendTypeOptimization = defaultSerializer.SendTypeOptimization;
                dontSerializeVariant.HasDontSupportPrefabOverridesAttribute = defaultSerializer.HasDontSupportPrefabOverridesAttribute;
            }

            AddSerializationStrategyInternal(ref dontSerializeVariant);
            dontSerializeVariant.DefaultRule = defaultRule;
            return dontSerializeVariant;
        }

        internal static bool AnyVariantsAreSerialized(in NativeList<ComponentTypeSerializationStrategy> availableVariants)
        {
            foreach (var x in availableVariants)
            {
                if (x.IsSerialized != 0)
                    return true;
            }

            return false;
        }

        void BurstCompatibleErrorWithAggregate(ComponentType componentType, in NativeList<ComponentTypeSerializationStrategy> availableVariants, FixedString4096Bytes error)
        {
            error.Append(WorldName);
            error.Append(' ');
            error.Append(componentType.ToFixedString());
            if (availableVariants.IsCreated)
            {
                error.Append((FixedString64Bytes) $", {availableVariants.Length} variants available: ");
                for (var i = 0; i < availableVariants.Length; i++)
                {
                    var availableVariant = availableVariants[i];
                    error.Append('\n');
                    error.Append(i);
                    error.Append(':');
                    error.Append(availableVariant.ToFixedString());
                }
            }
            UnityEngine.Debug.LogError(error);
        }

        /// <summary>
        /// Variants have nested "is default" rules. This method calculates them.
        /// </summary>
        ComponentTypeSerializationStrategy.DefaultType CalculateDefaultTypeForSerializer(ComponentType componentType, bool isRoot, bool isSerialized, byte isDefaultSerializer, byte isInput, ulong ssHash, ref byte sendForChildEntities, ulong chosenVariantHash)
        {
            if (VariantIsUserSpecifiedDefaultRule(componentType, ssHash, isRoot, chosenVariantHash, out var defaultType))
            {
                return defaultType;
            }

            // The user did NOT specify this as a default, so infer defaults from rules:
            if (isSerialized)
            {
                // Child entities default to DontSerializeVariant:
                // But that may have been changed via attribute, making them the default serializer:
                if (isRoot || isInput != 0 || sendForChildEntities != 0)
                    return isDefaultSerializer != 0 ? ComponentTypeSerializationStrategy.DefaultType.YesAsIsDefaultSerializerAndDefaultIsUnchanged : ComponentTypeSerializationStrategy.DefaultType.NotDefault;
            }
            else
            {
                // It's the DontSerializeVariant.
                if (ssHash == GhostVariantsUtility.DontSerializeHash)
                    return ComponentTypeSerializationStrategy.DefaultType.YesAsIsChildDefaultingToDontSerializeVariant;

                // It's the default, non-serialized variant, so use it as a last resort.
                if (isDefaultSerializer > 0)
                    return ComponentTypeSerializationStrategy.DefaultType.YesAsIsFallback;
            }
            return ComponentTypeSerializationStrategy.DefaultType.NotDefault;
        }

        bool VariantIsUserSpecifiedDefaultRule(ComponentType componentType, ulong variantTypeHash, bool isRoot, ulong chosenVariantHash, out ComponentTypeSerializationStrategy.DefaultType defaultType)
        {
            // The user requested this variant by name.
            if (variantTypeHash == chosenVariantHash)
            {
                defaultType = ComponentTypeSerializationStrategy.DefaultType.YesViaUserSpecifiedNamedDefaultOrHash;
                return true;
            }

            if (DefaultVariants.TryGetValue(componentType, out var existingRule))
            {
                var variantRule = (isRoot ? existingRule.VariantForParents : existingRule.VariantForChildren);
                if (variantRule != default)
                {
                    // The user DID SPECIFY a default, which invalidates all other defaults.
                    if (variantRule == variantTypeHash)
                    {
                        defaultType = ComponentTypeSerializationStrategy.DefaultType.YesAsIsUserSpecifiedNewDefault;
                        return true;
                    }
                }
            }

            defaultType = ComponentTypeSerializationStrategy.DefaultType.NotDefault;
            return false;
        }

        /// <summary>Validation that the SourceGenerators return valid hashes for "default serializers".</summary>
        /// <param name="hash">Hash to check.</param>
        /// <param name="context"></param>
        /// <exception cref="InvalidOperationException"></exception>
        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void ThrowIfNoHash(ulong hash, FixedString512Bytes context)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (hash == 0)
                throw new InvalidOperationException($"Cannot add variant for context '{context}' as hash is zero! Set hashes for all variants via `GhostVariantsUtility` and ensure you've rebuilt NetCode 'Source Generators'.");
#endif
        }

        /// <summary>Release the allocated resources used to store the ghost serializer strategies and mappings.</summary>
        public void Dispose()
        {
            CollectionFinalized.Dispose();
            Serializers.Dispose();
            SerializationStrategies.Dispose();
            DefaultVariants.Dispose();
            SerializationStrategiesComponentTypeMap.Dispose();
            InputComponentBufferMap.Dispose();
        }

        /// <summary>
        /// Validate that all the serialization strategies have a valid <see cref="ComponentTypeSerializationStrategy.SerializerIndex"/>
        /// and that all the <see cref="GhostComponentSerializer.State.SerializationStrategyIndex"/> have been set.
        /// </summary>
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void Validate()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            for (var i = 0; i < SerializationStrategies.Length; i++)
            {
                var serializationStrategy = SerializationStrategies[i];
                UnityEngine.Assertions.Assert.AreEqual(i, serializationStrategy.SelfIndex, "SerializationStrategies[i]");
                if (serializationStrategy.SerializerIndex >= 0)
                {
                    UnityEngine.Assertions.Assert.IsTrue(serializationStrategy.SerializerIndex < Serializers.Length, "SerializationStrategies > Serializer Index in Range");
                    UnityEngine.Assertions.Assert.AreEqual(i, Serializers[serializationStrategy.SerializerIndex].SerializationStrategyIndex, "SerializationStrategies > Serializer > SerializationStrategies backwards lookup!");
                }
            }
            foreach (var serializer in Serializers)
            {
                UnityEngine.Assertions.Assert.IsTrue(serializer.SerializationStrategyIndex >= 0 && serializer.SerializationStrategyIndex < SerializationStrategies.Length, "Serializer > SerializationStrategies Index in Range");
            }
#endif
        }
    }
}
