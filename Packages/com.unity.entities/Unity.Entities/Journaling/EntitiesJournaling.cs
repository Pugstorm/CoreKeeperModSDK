#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    /// <summary>
    /// Entities journaling provides detailed information about past ECS events.
    /// </summary>
    public static unsafe partial class EntitiesJournaling
    {
        static Dictionary<ulong, WeakReference<World>> s_WorldWeakRefMap;
        static Dictionary<ulong, string> s_WorldNameMap;
        static Dictionary<SystemHandle, SystemTypeIndex> s_SystemTypeMap;
        static Dictionary<RecordView, object> s_RecordDataMap;

        sealed class SharedInit { internal static readonly SharedStatic<bool> Ref = SharedStatic<bool>.GetOrCreate<SharedInit>(); }
        sealed class SharedEnabled { internal static readonly SharedStatic<bool> Ref = SharedStatic<bool>.GetOrCreate<SharedEnabled>(); }
        sealed class SharedEntityTypeIndex { internal static readonly SharedStatic<TypeIndex> Ref = SharedStatic<TypeIndex>.GetOrCreate<SharedEntityTypeIndex>(); }
        sealed class SharedState { internal static readonly SharedStatic<JournalingState> Ref = SharedStatic<JournalingState>.GetOrCreate<SharedState>(); }

        static ref bool s_Initialized => ref SharedInit.Ref.Data;
        static ref bool s_Enabled => ref SharedEnabled.Ref.Data;
        static ref TypeIndex s_EntityTypeIndex => ref SharedEntityTypeIndex.Ref.Data;
        static ref JournalingState s_State => ref SharedState.Ref.Data;

        public enum JournalingOperationType
        {
            StartRecording,
            StopRecording,
            ClearResults
        }

        public static event Action<JournalingOperationType> s_JournalingOperationExecuted;

        /// <summary>
        /// Whether or not entities journaling events are recorded.
        /// </summary>
        /// <remarks>
        /// Setting this to <see langword="false"/> does not clear or deallocate the journaling state.
        /// </remarks>
        [ExcludeFromBurstCompatTesting("Managed collections")]
        public static bool Enabled
        {
            get => s_Enabled;
            set
            {
                // In case we are trying to enable journaling, but default
                // world was not initialized yet, initialize journaling now.
                if (value)
                    Internal_Initialize();

                // Do not allow to change value if not initialized
                if (!s_Initialized)
                    return;

                // If value is same, do nothing
                if (s_Enabled == value)
                    return;

                s_Enabled = value;
                UpdateState();

                // Reset record session caches
                s_RecordDataMap.Clear();
                s_State.ClearSystemVersionBuffers();

                s_JournalingOperationExecuted?.Invoke(value ? JournalingOperationType.StartRecording : JournalingOperationType.StopRecording);
            }
        }

        /// <summary>
        /// The number of records in the buffer.
        /// </summary>
        public static int RecordCount => s_State.RecordCount;

        /// <summary>
        /// The last record index that was added to the buffer.
        /// </summary>
        public static ulong RecordIndex => s_State.RecordIndex;

        /// <summary>
        /// Current used bytes.
        /// </summary>
        public static ulong UsedBytes => s_State.UsedBytes;

        /// <summary>
        /// Current allocated bytes.
        /// </summary>
        public static ulong AllocatedBytes => s_State.AllocatedBytes;

        /// <summary>
        /// Retrieve records currently in buffer.
        /// </summary>
        /// <remarks>
        /// <b>Call will be blocking if records are currently locked for write.</b>
        /// <para>For this reason, it is not recommended to call this in a debugger watch window, otherwise a deadlock might occur.</para>
        /// </remarks>
        /// <returns>Array of record view.</returns>
        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
        public static RecordViewArray GetRecords(Ordering ordering) => s_State.GetRecords(ordering, blocking: true);

        /// <summary>
        /// Try to retrieve records currently in buffer.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <returns>Array of record view.</returns>
        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
        public static RecordViewArray TryGetRecords(Ordering ordering) => s_State.GetRecords(ordering, blocking: false);

        /// <summary>
        /// Clear all the records.
        /// </summary>
        [ExcludeFromBurstCompatTesting("Managed collections")]
        public static void Clear()
        {
            if (!s_Initialized)
                return;

            s_RecordDataMap.Clear();
            s_State.Clear();

            s_JournalingOperationExecuted?.Invoke(JournalingOperationType.ClearResults);
        }

        [ExcludeFromBurstCompatTesting("Managed collections")]
        internal static void Initialize()
        {
            Internal_Initialize();
            Enabled = Preferences.Enabled;
        }

        [ExcludeFromBurstCompatTesting("Managed collections")]
        internal static void Shutdown()
        {
            Internal_Shutdown();
        }

        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void AddRecord(RecordType recordType, ulong worldSequenceNumber, in SystemHandle executingSystem, Entity* entities, int entityCount, in SystemHandle originSystem = default, TypeIndex* types = null, int typeCount = 0, void* data = null, int dataLength = 0)
        {
            if (!s_Initialized)
                return;

            if (entities == null && entityCount > 0)
                entityCount = 0;
            if (types == null && typeCount > 0)
                typeCount = 0;
            if (data == null && dataLength > 0)
                dataLength = 0;

            // Skip Entity type index
            if (typeCount > 0 && types != null && types[0] == s_EntityTypeIndex)
            {
                types++;
                typeCount--;
            }

            s_State.PushBack(recordType, worldSequenceNumber, in executingSystem, in originSystem, entities, entityCount, types, typeCount, data, dataLength);
        }

        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void AddRecord(RecordType recordType, ulong worldSequenceNumber, in SystemHandle executingSystem, ArchetypeChunk* chunks, int chunkCount, in SystemHandle originSystem = default, TypeIndex* types = null, int typeCount = 0, void* data = null, int dataLength = 0)
        {
            if (!s_Initialized)
                return;

            if (chunks == null && chunkCount > 0)
                chunkCount = 0;
            if (types == null && typeCount > 0)
                typeCount = 0;
            if (data == null && dataLength > 0)
                dataLength = 0;

            // Skip Entity type index
            if (typeCount > 0 && types != null && types[0] == s_EntityTypeIndex)
            {
                types++;
                typeCount--;
            }

            s_State.PushBack(recordType, worldSequenceNumber, in executingSystem, in originSystem, chunks, chunkCount, types, typeCount, data, dataLength);
        }

        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void AddRecord(RecordType recordType, ulong worldSequenceNumber, in SystemHandle executingSystem, Archetype* archetype, ChunkIndex chunk, in SystemHandle originSystem = default, TypeIndex* types = null, int typeCount = 0, void* data = null, int dataLength = 0)
        {
            if (!s_Initialized)
                return;

            if (types == null && typeCount > 0)
                typeCount = 0;
            if (data == null && dataLength > 0)
                dataLength = 0;

            // Skip Entity type index
            if (typeCount > 0 && types != null && types[0] == s_EntityTypeIndex)
            {
                types++;
                typeCount--;
            }

            s_State.PushBack(recordType, worldSequenceNumber, in executingSystem, in originSystem, archetype, chunk, types, typeCount, data, dataLength);
        }

        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void AddRecord(RecordType recordType, EntityComponentStore* entityComponentStore, uint globalSystemVersion, Entity* entities, int entityCount, in SystemHandle originSystem = default, TypeIndex* types = null, int typeCount = 0, void* data = null, int dataLength = 0)
        {
            if (!s_Initialized)
                return;

            if (entities == null && entityCount > 0)
                entityCount = 0;
            if (types == null && typeCount > 0)
                typeCount = 0;
            if (data == null && dataLength > 0)
                dataLength = 0;

            // Skip Entity type index
            if (typeCount > 0 && types != null && types[0] == s_EntityTypeIndex)
            {
                types++;
                typeCount--;
            }

            s_State.PushBack(recordType, entityComponentStore, globalSystemVersion, in originSystem, entities, entityCount, types, typeCount, data, dataLength);
        }

        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void AddRecord(RecordType recordType, EntityComponentStore* entityComponentStore, uint globalSystemVersion, ArchetypeChunk* chunks, int chunkCount, in SystemHandle originSystem = default, TypeIndex* types = null, int typeCount = 0, void* data = null, int dataLength = 0)
        {
            if (!s_Initialized)
                return;

            if (chunks == null && chunkCount > 0)
                chunkCount = 0;
            if (types == null && typeCount > 0)
                typeCount = 0;
            if (data == null && dataLength > 0)
                dataLength = 0;

            // Skip Entity type index
            if (typeCount > 0 && types != null && types[0] == s_EntityTypeIndex)
            {
                types++;
                typeCount--;
            }

            s_State.PushBack(recordType, entityComponentStore, globalSystemVersion, in originSystem, chunks, chunkCount, types, typeCount, data, dataLength);
        }

        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void AddRecord(RecordType recordType, EntityComponentStore* entityComponentStore, uint globalSystemVersion, Archetype* archetype, ChunkIndex chunk, in SystemHandle originSystem = default, TypeIndex* types = null, int typeCount = 0, void* data = null, int dataLength = 0)
        {
            if (!s_Initialized)
                return;

            if (types == null && typeCount > 0)
                typeCount = 0;
            if (data == null && dataLength > 0)
                dataLength = 0;

            // Skip Entity type index
            if (typeCount > 0 && types != null && types[0] == s_EntityTypeIndex)
            {
                types++;
                typeCount--;
            }

            s_State.PushBack(recordType, entityComponentStore, globalSystemVersion, in originSystem, archetype, chunk, types, typeCount, data, dataLength);
        }

        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void AddSystemVersionHandle(EntityComponentStore* store, uint version, in SystemHandle handle)
        {
            if (!s_Initialized)
                return;

            s_State.PushBack(store, version, in handle);
        }

        internal static void OnWorldCreated(World world)
        {
            if (!s_Initialized)
                return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (world == null)
                throw new ArgumentNullException(nameof(world));
#endif

            s_WorldWeakRefMap.TryAdd(world.SequenceNumber, new WeakReference<World>(world));
            s_WorldNameMap.TryAdd(world.SequenceNumber, world.Name);
        }

        internal static void OnSystemCreated(SystemTypeIndex systemType, in SystemHandle systemHandle)
        {
            if (!s_Initialized)
                return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (!TypeManager.IsSystemTypeIndex(systemType))
                throw new ArgumentNullException(nameof(systemType));
            if (systemHandle == default)
                throw new ArgumentException(nameof(systemHandle));
#endif

            s_SystemTypeMap.TryAdd(systemHandle, systemType);
        }

        static void Internal_Initialize()
        {
            if (s_Initialized)
                return;

            s_WorldWeakRefMap = new Dictionary<ulong, WeakReference<World>>();
            s_WorldNameMap = new Dictionary<ulong, string>();
            s_SystemTypeMap = new Dictionary<SystemHandle, SystemTypeIndex>();
            s_RecordDataMap = new Dictionary<RecordView, object>();
            s_EntityTypeIndex = TypeManager.GetTypeIndex<Entity>();
            s_State = new JournalingState(Preferences.TotalMemoryMB * 1024 * 1024);
            s_Initialized = true;

            UpdateState();
        }

        static void Internal_Shutdown()
        {
            if (!s_Initialized)
                return;

            s_State.Dispose();
            s_RecordDataMap = null;
            s_SystemTypeMap = null;
            s_WorldNameMap = null;
            s_WorldWeakRefMap = null;
            s_Enabled = false;
            s_Initialized = false;
        }

        static void UpdateState()
        {
            // Update worlds and systems
            for (var i = 0; i < World.All.Count; ++i)
            {
                var world = World.All[i];
                if (world == null || !world.IsCreated)
                    continue;

                // Add world
                s_WorldWeakRefMap.TryAdd(world.SequenceNumber, new WeakReference<World>(world));
                s_WorldNameMap.TryAdd(world.SequenceNumber, world.Name);

                // Add systems
                using (var systems = world.Unmanaged.GetAllUnmanagedSystems(Allocator.Temp))
                {
                    foreach (var system in systems)
                    {
                        var systemType = world.Unmanaged.ResolveSystemState(system)->m_SystemTypeIndex;
                        if (TypeManager.IsSystemTypeIndex(systemType))
                            s_SystemTypeMap.TryAdd(system, systemType);
                    }
                }

                // Sync enabled state
                var access = world.EntityManager.GetCheckedEntityDataAccessExclusive();
                if (access != null)
                {
                    var store = access->EntityComponentStore;
                    if (store != null)
                        store->m_RecordToJournal = (byte)(Enabled ? 1 : 0);
                }
            }
        }

        static World GetWorld(ulong worldSeqNumber)
        {
            return s_WorldWeakRefMap.TryGetValue(worldSeqNumber, out var worldWeakRef) && worldWeakRef.TryGetTarget(out var world) && world.IsCreated ? world : null;
        }

        static string GetWorldName(ulong worldSeqNumber)
        {
            return s_WorldNameMap.TryGetValue(worldSeqNumber, out var name) ? name : string.Empty;
        }

        [ExcludeFromBurstCompatTesting("uses managed Dictionary")]
        static SystemTypeIndex GetSystemType(SystemHandle handle)
        {
            return s_SystemTypeMap.TryGetValue(handle, out var type) ? type : SystemTypeIndex.Null;
        }

        static string GetSystemName(SystemHandle handle)
        {
            return s_SystemTypeMap.TryGetValue(handle, out var type) ? TypeManager.GetSystemName(type).ToString() : string.Empty;
        }

        static Entity GetEntity(int index, int version, ulong worldSeqNumber)
        {
            var world = GetWorld(worldSeqNumber);
            if (world == null || !world.IsCreated)
                return Entity.Null;

            var entity = new Entity { Index = index, Version = version };
            return world.EntityManager.Exists(entity) ? entity : Entity.Null;
        }

        static object GetRecordData(RecordView record)
        {
            switch (record.RecordType)
            {
                case RecordType.SystemAdded:
                case RecordType.SystemRemoved:
                    return TryGetRecordDataAsSystemView(record, out var systemView) ? systemView : null;

                case RecordType.SetComponentData:
                case RecordType.GetComponentDataRW:
                case RecordType.SetSharedComponentData:
                    return TryGetRecordDataAsComponentDataArrayBoxed(record, out var componentDataArray) ? componentDataArray : null;

                default:
                    return null;
            }
        }

        static T* Allocate<T>(AllocatorManager.AllocatorHandle allocator, NativeArrayOptions options) where T : unmanaged
        {
            var ptr = AllocatorManager.Allocate<T>(allocator, 1);
            if (options == NativeArrayOptions.ClearMemory && ptr != null)
                UnsafeUtility.MemClear(ptr, UnsafeUtility.SizeOf<T>());
            return ptr;
        }
    }
}
#endif
