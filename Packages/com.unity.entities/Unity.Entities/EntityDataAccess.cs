using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine.Profiling;

namespace Unity.Entities
{
    readonly struct ManagedEntityDataAccess
    {
        volatile static ManagedEntityDataAccess[] s_Instances = { default, default, default, default };

        // AllocHandle and FreeHandle must be called from the main thread
        public static int AllocHandle(ManagedEntityDataAccess instance)
        {
            var count = s_Instances.Length;
            for (int i = 0; i < count; ++i)
            {
                if (s_Instances[i].World == null)
                {
                    s_Instances[i] = instance;
                    return i;
                }
            }

            var newInstances = new ManagedEntityDataAccess[count * 2];
            for (int i = 0; i < count; ++i)
                newInstances[i] = s_Instances[i];
            newInstances[count] = instance;

            s_Instances = newInstances;
            return count;
        }

        public static void FreeHandle(int i)
        {
            s_Instances[i] = default;
        }

        // This is thread safe even if s_Instances is being resized concurrently since both the old and new versions
        // will have the same value at s_Instances[handle] and the reference is updated atomically
        public static ref ManagedEntityDataAccess GetInstance(int handle)
        {
            return ref s_Instances[handle];
        }

        public static ManagedEntityDataAccess Debugger_GetInstance(int handle)
        {
            if (handle >= 0 && handle < s_Instances.Length)
                return s_Instances[handle];
            else
                return default;
        }

        public ManagedEntityDataAccess(World world, ManagedComponentStore store)
        {
            World = world;
            ManagedComponentStore = store;
        }

        readonly public World                 World;
        readonly public ManagedComponentStore ManagedComponentStore;
    }

    [StructLayout(LayoutKind.Sequential)]
    [GenerateTestsForBurstCompatibility]
    [BurstCompile]
    unsafe struct EntityDataAccess : IDisposable
    {
        private delegate void PlaybackManagedDelegate(IntPtr self);

        // approximate count of entities past which it is faster
        // to form batches when iterating through them all
        internal const int FASTER_TO_BATCH_THRESHOLD = 10;
        internal const int MANAGED_REFERENCES_DEFAULT = 8;

        private static readonly SharedStatic<IntPtr> s_ManagedPlaybackTrampoline =
            SharedStatic<IntPtr>.GetOrCreate<PlaybackManagedDelegate>();

        private static object s_DelegateGCPrevention;

        [ExcludeFromBurstCompatTesting("Managed data access")]
        internal ref ManagedEntityDataAccess ManagedEntityDataAccess => ref ManagedEntityDataAccess.GetInstance(m_ManagedAccessHandle);

        [ExcludeFromBurstCompatTesting("Managed component store")]
        internal ManagedComponentStore ManagedComponentStore => ManagedEntityDataAccess.ManagedComponentStore;

        // These pointer attributes freak debuggers out because they take
        // copies of the root struct when inspecting, and it generally
        // misbehaves from there.

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal EntityComponentStore* EntityComponentStore
        {
            // This is always safe as the EntityDataAccess is always unsafe heap allocated.
            get
            {
                fixed (EntityComponentStore* ptr = &m_EntityComponentStore)
                {
                    return ptr;
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal EntityQueryManager* EntityQueryManager
        {
            // This is always safe as the EntityDataAccess is always unsafe heap allocated.
            get
            {
                fixed (EntityQueryManager* ptr = &m_EntityQueryManager)
                {
                    return ptr;
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal ComponentDependencyManager* DependencyManager
        {
            // This is always safe as the EntityDataAccess is always unsafe heap allocated.
            get
            {
                fixed (ComponentDependencyManager* ptr = &m_DependencyManager)
                {
                    return ptr;
                }
            }
        }

        [NativeDisableUnsafePtrRestriction] private EntityComponentStore m_EntityComponentStore;
        [NativeDisableUnsafePtrRestriction] private EntityQueryManager m_EntityQueryManager;
        [NativeDisableUnsafePtrRestriction] private ComponentDependencyManager m_DependencyManager;
        [NativeDisableUnsafePtrRestriction] private UnsafeList<int> m_ManagedReferenceIndexList;
        [NativeDisableUnsafePtrRestriction] public EntityQuery m_UniversalQuery; // matches all components
        [NativeDisableUnsafePtrRestriction] public EntityQuery m_UniversalQueryWithChunks;
        [NativeDisableUnsafePtrRestriction] public EntityQuery m_UniversalQueryWithSystems;
        [NativeDisableUnsafePtrRestriction] public EntityQuery m_UniversalQueryWithChunksAndSystems;
        [NativeDisableUnsafePtrRestriction] public EntityQuery m_EntityGuidQuery;
        [NativeDisableUnsafePtrRestriction] public WorldUnmanaged m_WorldUnmanaged;

#if UNITY_EDITOR
        public int m_CachedEntityGUIDToEntityIndexVersion;
        UntypedUnsafeParallelHashMap m_CachedEntityGUIDToEntityIndex;

        [GenerateTestsForBurstCompatibility(CompileTarget = GenerateTestsForBurstCompatibilityAttribute.BurstCompatibleCompileTarget.Editor)]
        public ref UnsafeParallelMultiHashMap<int, Entity> CachedEntityGUIDToEntityIndex
        {
            get
            {
                fixed (void* ptr = &m_CachedEntityGUIDToEntityIndex)
                {
                    return ref UnsafeUtility.AsRef<UnsafeParallelMultiHashMap<int, Entity>>(ptr);
                }
            }
        }

        internal const int kBuiltinEntityQueryCount = 6;
#else
        internal const int kBuiltinEntityQueryCount = 5;
#endif

        private int m_ManagedAccessHandle;

        EntityArchetype m_EntityAndSimulateOnlyArchetype;
        private UntypedUnsafeParallelHashMap m_AliveEntityQueries;

        internal ref UnsafeParallelHashMap<ulong, byte> AliveEntityQueries
        {
            get
            {
                fixed (void* ptr = &m_AliveEntityQueries)
                {
                    return ref UnsafeUtility.AsRef<UnsafeParallelHashMap<ulong, byte>>(ptr);
                }
            }
        }

        internal bool IsInExclusiveTransaction => m_DependencyManager.IsInTransaction;

#if ENABLE_PROFILER
        internal ref StructuralChangesProfiler.Recorder StructuralChangesRecorder => ref m_EntityComponentStore.StructuralChangesRecorder;
#endif

        [ExcludeFromBurstCompatTesting("Takes managed World")]
        public static void Initialize(EntityDataAccess* self, World world)
        {

            self->m_ManagedReferenceIndexList = new UnsafeList<int>(MANAGED_REFERENCES_DEFAULT, Allocator.Persistent);

            self->m_EntityAndSimulateOnlyArchetype = default;
            self->m_ManagedAccessHandle = -1;

            self->AliveEntityQueries = new UnsafeParallelHashMap<ulong, byte>(32, Allocator.Persistent);
#if UNITY_EDITOR
            self->CachedEntityGUIDToEntityIndex = new UnsafeParallelMultiHashMap<int, Entity>(32, Allocator.Persistent);
#endif
            self->m_DependencyManager.OnCreate(world.Unmanaged);
            Entities.EntityComponentStore.Create(&self->m_EntityComponentStore, world.SequenceNumber);
            Unity.Entities.EntityQueryManager.Create(&self->m_EntityQueryManager, &self->m_DependencyManager);

            self->m_WorldUnmanaged = world.Unmanaged;

            var managedGuts = new ManagedEntityDataAccess(world, new ManagedComponentStore(self->EntityComponentStore));
            self->m_ManagedAccessHandle = ManagedEntityDataAccess.AllocHandle(managedGuts);
            self->m_EntityComponentStore.m_DebugOnlyManagedAccess = self->m_ManagedAccessHandle;

            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludeSystems);
            self->m_UniversalQueryWithSystems = self->m_EntityQueryManager.CreateEntityQuery(self, builder);

            builder.Reset();
            builder.WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab);
            self->m_UniversalQuery = self->m_EntityQueryManager.CreateEntityQuery(self, builder);

            builder.Reset();
            builder.WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab |
                                EntityQueryOptions.IncludeMetaChunks);
            self->m_UniversalQueryWithChunks = self->m_EntityQueryManager.CreateEntityQuery(self, builder);

            builder.Reset();
            builder.WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab |
                                EntityQueryOptions.IncludeMetaChunks | EntityQueryOptions.IncludeSystems);
            self->m_UniversalQueryWithChunksAndSystems = self->m_EntityQueryManager.CreateEntityQuery(self, builder);

#if UNITY_EDITOR
            builder.Reset();
            builder.WithAllRW<EntityGuid>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab);
            self->m_EntityGuidQuery = self->m_EntityQueryManager.CreateEntityQuery(self, builder);
#endif

            builder.Dispose();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            self->m_UniversalQuery._GetImpl()->_DisallowDisposing = 1;
            self->m_UniversalQueryWithChunksAndSystems._GetImpl()->_DisallowDisposing = 1;
            self->m_UniversalQueryWithSystems._GetImpl()->_DisallowDisposing = 1;
            self->m_UniversalQueryWithChunks._GetImpl()->_DisallowDisposing = 1;
#if UNITY_EDITOR
            self->m_EntityGuidQuery._GetImpl()->_DisallowDisposing = 1;
#endif
#endif

            if (s_DelegateGCPrevention == null)
            {
                var trampoline = new PlaybackManagedDelegate(PlaybackManagedDelegateInMonoWithWrappedExceptions);
                s_DelegateGCPrevention = trampoline; // Need to hold on to this
                s_ManagedPlaybackTrampoline.Data = Marshal.GetFunctionPointerForDelegate(trampoline);
            }
        }

        [ExcludeFromBurstCompatTesting("calls managed playback")]
        [MonoPInvokeCallback(typeof(PlaybackManagedDelegate))]
        private static void PlaybackManagedDelegateInMonoWithWrappedExceptions(IntPtr target)
        {
            try
            {
                ((EntityDataAccess*) target.ToPointer())->PlaybackManagedChanges();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        [ExcludeFromBurstCompatTesting("Disposes managed lists")]
        public void Dispose()
        {
            m_ManagedReferenceIndexList.Dispose();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            m_UniversalQueryWithSystems._GetImpl()->_DisallowDisposing = 0;
            m_UniversalQueryWithChunks._GetImpl()->_DisallowDisposing = 0;
            m_UniversalQuery._GetImpl()->_DisallowDisposing = 0;
            m_UniversalQueryWithChunksAndSystems._GetImpl()->_DisallowDisposing = 0;
#if UNITY_EDITOR
            m_EntityGuidQuery._GetImpl()->_DisallowDisposing = 0;
#endif
#endif
            m_UniversalQueryWithSystems.Dispose();
            m_UniversalQueryWithChunks.Dispose();
            m_UniversalQuery.Dispose();
            m_UniversalQueryWithChunksAndSystems.Dispose();
            m_UniversalQueryWithSystems = default;
            m_UniversalQueryWithChunks = default;
            m_UniversalQuery = default;
            m_UniversalQueryWithChunksAndSystems = default;
#if UNITY_EDITOR
            m_EntityGuidQuery.Dispose();
            m_EntityGuidQuery = default;
#endif

            // Disposing EntityQueryImpl accesses EntityComponentStore, so they must be disposed first
            var keys = AliveEntityQueries.GetKeyArray(Allocator.Temp);
            for (var i = 0; i < keys.Length; i++)
            {
                var queryPtr = (EntityQueryImpl*)keys[i];
                queryPtr->Dispose();
                EntityQueryImpl.Free(queryPtr);
            }
            AliveEntityQueries.Dispose();
            AliveEntityQueries = default;

            m_DependencyManager.Dispose();
            Entities.EntityComponentStore.Destroy(EntityComponentStore);
            Entities.EntityQueryManager.Destroy(EntityQueryManager);

            ManagedEntityDataAccess.ManagedComponentStore.Dispose();
            ManagedEntityDataAccess.FreeHandle(m_ManagedAccessHandle);
            m_ManagedAccessHandle = -1;

#if UNITY_EDITOR
            CachedEntityGUIDToEntityIndex.Dispose();
            CachedEntityGUIDToEntityIndex = default;
#endif
        }

        public bool Exists(Entity entity)
        {
            return EntityComponentStore->Exists(entity);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private void CheckIsStructuralChange()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (DependencyManager->ForEachStructuralChange.Depth != 0)
                throw new InvalidOperationException("Structural changes are not allowed while iterating over entities. Please use EntityCommandBuffer instead.");
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private void CheckIsAdditiveStructuralChange(UnsafePtrList<Archetype> archetypes)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            for(var i = 0; i < archetypes.Length; ++i)
                CheckIsAdditiveStructuralChange(archetypes[i]);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private void CheckIsAdditiveStructuralChange(Archetype* archetype)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (!DependencyManager->ForEachStructuralChange.IsAdditiveStructuralChangePossible(archetype))
                //@TODO: Better error message
                throw new InvalidOperationException("Structural changes are not allowed while iterating over entities. Please use EntityCommandBuffer instead.");
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        internal void AssertMainThread()
        {
            if (IsInExclusiveTransaction)
                throw new InvalidOperationException("Must be called from the main thread");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        internal void AssertQueryIsValid(EntityQuery query)
        {
            if (!IsQueryValid(query))
                throw new ArgumentException("query is invalid or disposed");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        internal void AssertQueryHasNoEnableableComponents(EntityQuery query)
        {
            if (query._GetImpl()->_QueryData->HasEnableableComponents != 0)
                throw new ArgumentException("EntityQuery objects with types that implement IEnableableComponent are not currently supported by this operation.");
        }

        public bool IsQueryValid(EntityQuery query)
        {
            if (!AliveEntityQueries.ContainsKey((ulong) (IntPtr) query.__impl))
                return false;

            // Also check that the sequence number matches in case the same pointer was given back by malloc.
            return query.__seqno == query.__impl->_SeqNo;
        }


        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckMoreThan1024Components(int count)
        {
            if (count + 1 > 1024)
                throw new ArgumentException($"Archetypes can't hold more than 1024 components");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private static void AssertCountsMatch(int count, int outputCount)
        {
            if (count != outputCount)
                throw new System.ArgumentException(
                    $"srcEntities.Length ({count}) and outputEntities.Length (({outputCount})) must be the same.");
        }

        public void BeforeStructuralChange()
        {
            // This is not an end user error. If there are any managed changes at this point, it indicates there is some
            // (previous) EntityManager change that is not properly playing back the managed changes that were buffered
            // afterward. That needs to be found and fixed.
            CheckIsStructuralChange();
            EntityComponentStore->AssertNoQueuedManagedDeferredCommands();

            if (!m_DependencyManager.IsInTransaction)
                m_DependencyManager.CompleteAllJobsAndInvalidateArrays();
        }

        public EntityComponentStore.ArchetypeChanges BeginStructuralChanges()
        {
            if (!IsInExclusiveTransaction)
                BeforeStructuralChange();
            m_ManagedReferenceIndexList.Clear();
            return EntityComponentStore->BeginArchetypeChangeTracking();
        }

        public void PrepareForAdditiveStructuralChanges()
        {
            CompleteForAdditiveStructuralChanges();
        }

        public void PrepareForAdditiveStructuralChanges(Archetype* archetype)
        {
            CheckIsAdditiveStructuralChange(archetype);
            CompleteForAdditiveStructuralChanges();
        }

        public void PrepareForInstantiateAdditiveStructuralChanges(NativeArray<Entity> entities)
        {
            foreach (var entity in entities)
                CheckIsAdditiveStructuralChange(EntityComponentStore->GetArchetype(entity)->InstantiateArchetype);

            CompleteForAdditiveStructuralChanges();
        }

        public void PrepareForCopyAdditiveStructuralChanges(NativeArray<Entity> entities)
        {
            foreach (var entity in entities)
                CheckIsAdditiveStructuralChange(EntityComponentStore->GetArchetype(entity)->CopyArchetype);

            CompleteForAdditiveStructuralChanges();
        }

        private void CompleteForAdditiveStructuralChanges()
        {
            if (IsInExclusiveTransaction)
                return;

            // This is not an end user error. If there are any managed changes at this point, it indicates there is some
            // (previous) EntityManager change that is not properly playing back the managed changes that were buffered
            // afterward. That needs to be found and fixed.

            EntityComponentStore->AssertNoQueuedManagedDeferredCommands();

            if (!m_DependencyManager.IsInTransaction)
                DependencyManager->CompleteAllJobsAndCheckDeallocateAndThrow();
        }

        public EntityComponentStore.ArchetypeChanges BeginAdditiveStructuralChanges()
        {
            m_ManagedReferenceIndexList.Clear();
            return EntityComponentStore->BeginArchetypeChangeTracking();
        }

        public void CheckIsAdditiveArchetypeStructuralChangePossible(Archetype* archetype)
        {
            if (!IsInExclusiveTransaction)
                CheckIsAdditiveStructuralChange(archetype);
        }

        public void EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes)
        {
            if (EntityComponentStore->ManagedChangesTracker.CommandBuffer.Length > 0)
                PlaybackManagedChanges();

            if (!m_ManagedReferenceIndexList.IsEmpty)
                ResetManagedReferences();

            if (EntityComponentStore->m_Archetypes.Length - changes.StartIndex != 0)
                EntityComponentStore->EndArchetypeChangeTracking(changes, EntityQueryManager);

            if (EntityComponentStore->m_ChunkListChangesTracker.ArchetypeTrackingHead != null)
                EntityComponentStore->InvalidateChunkListCacheForChangedArchetypes();
        }

        public void ResetManagedReferences()
        {
            fixed (EntityDataAccess* mgr = &this)
            {
                ECBInterop.RemoveManagedReferences(mgr, m_ManagedReferenceIndexList.Ptr,
                    m_ManagedReferenceIndexList.Length);
            }

            m_ManagedReferenceIndexList.Clear();
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="entities">The NativeArray of entities to destroy</param>
        public void DestroyEntityDuringStructuralChange(NativeArray<Entity> entities,
            in SystemHandle originSystem = default)
        {
            if (entities.Length == 0)
                return;

            EntityComponentStore->AssertValidEntities((Entity*) entities.GetUnsafePtr(), entities.Length);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_DestroyEntity(in originSystem, in entities);
#endif

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.DestroyEntity, in m_WorldUnmanaged);
#endif

            StructuralChange.DestroyEntity(EntityComponentStore, (Entity*) entities.GetUnsafePtr(), entities.Length);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="queryImpl"></param>
        public void DestroyEntitiesInQueryDuringStructuralChange(EntityQueryImpl* queryImpl,
            in SystemHandle originSystem = default)
        {
#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.DestroyEntity, in m_WorldUnmanaged);
#endif
            queryImpl->SyncFilterTypes();
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
            {
                // TODO: Allocating and populating this filtered chunk array is redundant work, but that's what the journaling interface requires.
                using var chunks = queryImpl->ToArchetypeChunkArray(Allocator.TempJob);
                if (Hint.Likely(chunks.Length > 0))
                {
                    JournalAddRecord_DestroyEntity(in originSystem, in chunks);
                }
            }
#endif
            var linkedEntityGroupTypeHandle = GetBufferTypeHandle<LinkedEntityGroup>(true);
            StructuralChange.DestroyChunksQuery(EntityComponentStore, queryImpl, ref linkedEntityGroupTypeHandle);
#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="entities"></param>
        /// <param name="count"></param>
        internal void DestroyEntityInternalDuringStructuralChange(Entity* entities, int count,
            in SystemHandle originSystem = default)
        {
            if (count == 0)
                return;
            EntityComponentStore->AssertValidEntities(entities, count);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_DestroyEntity(in originSystem, entities, count);
#endif

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.DestroyEntity, in m_WorldUnmanaged);
#endif

            StructuralChange.DestroyEntity(EntityComponentStore, entities, count);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif
        }

        internal EntityArchetype CreateArchetype(ComponentType* types, int count, bool addSimulateComponentIfMissing)
        {
            Assert.IsTrue(types != null || count == 0);
            EntityComponentStore->AssertCanCreateArchetype(types, count);

            ComponentTypeInArchetype* typesInArchetype = stackalloc ComponentTypeInArchetype[count + 2];

            var cachedComponentCount = FillSortedArchetypeArray(typesInArchetype, types, count, addSimulateComponentIfMissing);

            return CreateArchetype_Sorted(typesInArchetype, cachedComponentCount);
        }

        internal EntityArchetype CreateArchetype_Sorted(ComponentTypeInArchetype* typesInArchetype, int cachedComponentCount)
        {
            // Lookup existing archetype (cheap)
            EntityArchetype entityArchetype;
            entityArchetype.Archetype = EntityComponentStore->GetExistingArchetype(typesInArchetype, cachedComponentCount);
            if (entityArchetype.Archetype != null)
                return entityArchetype;

            // Creating an archetype invalidates all iterators / jobs etc
            // because it affects the live iteration linked lists...

            entityArchetype.Archetype = EntityComponentStore->GetOrCreateArchetype(typesInArchetype, cachedComponentCount);

            return entityArchetype;
        }

        internal static int FillSortedArchetypeArray(ComponentTypeInArchetype* dst, ComponentType* requiredComponents, int count, bool addSimulateComponentIfMissing)
        {
            CheckMoreThan1024Components(count);
            dst[0] = new ComponentTypeInArchetype(ComponentType.ReadWrite<Entity>());
            bool hasSimulate = false;
            for (var i = 0; i < count; ++i)
            {
                hasSimulate |= (requiredComponents[i] == ComponentType.ReadWrite<Simulate>());
                SortingUtilities.InsertSorted(dst, i + 1, requiredComponents[i]);
            }
            if (!hasSimulate && addSimulateComponentIfMissing)
            {
                SortingUtilities.InsertSorted(dst, count + 1, ComponentType.ReadWrite<Simulate>());
                return count + 2;
            }
            return count + 1;
        }

        /// <summary>
        /// Creates an entity with no components.
        /// </summary>
        /// <remarks>
        /// Creates the entity in the first available chunk with the archetype having no components.
        ///
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before creating the entity and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <returns>The Entity object that you can use to access the entity.</returns>
        public Entity CreateEntity(in SystemHandle originSystem = default)
        {
            Entity entity;

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.CreateEntity, in m_WorldUnmanaged);
#endif

            StructuralChange.CreateEntity(EntityComponentStore, GetEntityAndSimulateArchetype().Archetype, &entity, 1);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_CreateEntity(in originSystem, &entity, 1);
#endif

            return entity;
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="archetype"></param>
        /// <exception cref="ArgumentException">Thrown if the archetype is null.</exception>
        /// <returns></returns>
        public Entity CreateEntityDuringStructuralChange(EntityArchetype archetype, in SystemHandle originSystem = default)
        {
            Entity entity;
            CreateEntityDuringStructuralChange(archetype, &entity, 1, in originSystem);
            return entity;
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="archetype"></param>
        /// <param name="outEntities"></param>
        /// <param name="count"></param>
        /// <exception cref="ArgumentException">Thrown if the archetype is null.</exception>
        internal void CreateEntityDuringStructuralChange(EntityArchetype archetype, Entity* outEntities, int count,
            in SystemHandle originSystem = default)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (count < 0)
                throw new ArgumentOutOfRangeException("count must be non-negative");
#endif
            Unity.Entities.EntityComponentStore.AssertValidArchetype(EntityComponentStore, archetype);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.CreateEntity, in m_WorldUnmanaged);
#endif

            StructuralChange.CreateEntity(EntityComponentStore, archetype.Archetype, outEntities, count);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_CreateEntity(in originSystem, outEntities, count, (TypeIndex*)archetype.Types, archetype.TypesCount);
#endif
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="archetype"></param>
        /// <param name="entities"></param>
        public void CreateEntityDuringStructuralChange(EntityArchetype archetype, NativeArray<Entity> entities, in SystemHandle originSystem = default)
        {
            CreateEntityDuringStructuralChange(archetype, (Entity*) entities.GetUnsafePtr(), entities.Length, in originSystem);
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="componentType"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Thrown if an entity does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown if the component type being added is Entity type.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the component increases the shared component count of the entity's archetype to more than the maximum allowed.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the component causes the size of the archetype to exceed the size of a chunk.</exception>
        public bool AddComponentDuringStructuralChange(Entity entity, ComponentType componentType,
            in SystemHandle originSystem = default)
        {
            if (HasComponent(entity, componentType))
                return false;

            EntityComponentStore->AssertCanAddComponent(entity, componentType);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_AddComponent(in originSystem, &entity, 1, &componentType.TypeIndex, 1);
#endif

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.AddComponent, in m_WorldUnmanaged);
#endif

            var result = StructuralChange.AddComponentEntity(EntityComponentStore, &entity, componentType.TypeIndex);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif
            return result;
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="typeSet"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Thrown if an entity does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown if the component type being added is Entity type.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the component increases the shared component count of the entity's archetype to more than the maximum allowed.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the component causes the size of the archetype to exceed the size of a chunk.</exception>
        public void AddMultipleComponentsDuringStructuralChange(Entity entity, in ComponentTypeSet typeSet,
            in SystemHandle originSystem = default)
        {
            EntityComponentStore->AssertCanAddComponents(entity, typeSet);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_AddComponent(in originSystem, &entity, 1, typeSet.UnsafeTypesPtrRO, typeSet.Length);
#endif

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.AddComponent, in m_WorldUnmanaged);
#endif

            StructuralChange.AddComponentsEntity(EntityComponentStore, &entity, typeSet);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="queryImpl">The query to apply this operation to</param>
        /// <param name="componentType">The component type being added to the entities.</param>
        /// <exception cref="InvalidOperationException">Thrown if an entity in the query does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown if the component type being added is Entity type.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the component increases the shared component count of the entity's archetype to more than the maximum allowed.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the component causes the size of the archetype to exceed the size of a chunk.</exception>
        public void AddComponentToQueryDuringStructuralChange(EntityQueryImpl* queryImpl, ComponentType componentType,
            in SystemHandle originSystem = default)
        {
            if (queryImpl->IsEmptyIgnoreFilter)
                return;
            EntityComponentStore->AssertCanAddComponent(queryImpl, componentType);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.AddComponent, in m_WorldUnmanaged);
#endif

            queryImpl->SyncFilterTypes();
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
            {
                // TODO: Allocating and populating this filtered chunk array is redundant work, but that's what the journaling interface requires.
                using var chunks = queryImpl->ToArchetypeChunkArray(Allocator.TempJob);
                if (Hint.Likely(chunks.Length > 0))
                    JournalAddRecord_AddComponent(in originSystem, in chunks, &componentType.TypeIndex, 1);
            }
#endif
            StructuralChange.AddComponentQuery(EntityComponentStore, queryImpl, componentType.TypeIndex);
#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="queryImpl">The query to apply this operation to</param>
        /// <param name="typeSet">The component types being added to the entities.</param>
        /// <exception cref="InvalidOperationException">Thrown if an entity in the query does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown if one of the component types being added is Entity type.</exception>
        /// <exception cref="InvalidOperationException">Thrown if one of the components increases the shared component count of the entity's archetype to more than the maximum allowed.</exception>
        /// <exception cref="InvalidOperationException">Thrown if one of the components causes the size of the archetype to exceed the size of a chunk.</exception>
        internal void AddComponentsToQueryDuringStructuralChange(EntityQueryImpl* queryImpl, in ComponentTypeSet typeSet,
            in SystemHandle originSystem = default)
        {
            if (queryImpl->IsEmptyIgnoreFilter || typeSet.Length == 0)
                return;
            EntityComponentStore->AssertCanAddComponents(queryImpl->_QueryData->MatchingArchetypes, typeSet);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.AddComponent, in m_WorldUnmanaged);
#endif

            queryImpl->SyncFilterTypes();
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
            {
                // TODO: Allocating and populating this filtered chunk array is redundant work, but that's what the journaling interface requires.
                using var chunks = queryImpl->ToArchetypeChunkArray(Allocator.TempJob);
                JournalAddRecord_AddComponent(in originSystem, in chunks, typeSet.UnsafeTypesPtrRO, typeSet.Length);
            }
#endif
            StructuralChange.AddComponentsQuery(EntityComponentStore, queryImpl, typeSet);
#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="entities">The set of entities.</param>
        /// <param name="componentType">The component type added to the entities.</param>
        /// <exception cref="InvalidOperationException">Thrown if an entity in the native array does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown if a component type being added is Entity type.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the component increases the shared component count of the entity's archetype to more than the maximum allowed.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the component causes the size of the archetype to exceed the size of a chunk.</exception>
        public void AddComponentDuringStructuralChange(NativeArray<Entity> entities, ComponentType componentType,
            in SystemHandle originSystem = default)
        {
            if (entities.Length == 0)
                return;

            EntityComponentStore->AssertCanAddComponent(entities, componentType);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_AddComponent(in originSystem, in entities, &componentType.TypeIndex, 1);
#endif

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.AddComponent, in m_WorldUnmanaged);
#endif

            if (entities.Length > FASTER_TO_BATCH_THRESHOLD &&
                EntityComponentStore->CreateEntityBatchList(entities, componentType.IsSharedComponent ? 1 : 0,
                    Allocator.Temp, out var entityBatchList))
            {
                StructuralChange.AddComponentEntitiesBatch(EntityComponentStore,
                    (UnsafeList<EntityBatchInChunk>*) NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(
                        ref entityBatchList), componentType.TypeIndex);
            }
            else
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    StructuralChange.AddComponentEntity(EntityComponentStore, &entity, componentType.TypeIndex);
                }
            }

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="entities">The set of entities.</param>
        /// <param name="componentTypeSet">The component types added to the entities.</param>
        /// <exception cref="InvalidOperationException">Thrown if an entity in the native array does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown if a component type being added is Entity type.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the component increases the shared component count of the entity's archetype to more than the maximum allowed.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the component causes the size of the archetype to exceed the size of a chunk.</exception>
        public void AddMultipleComponentsDuringStructuralChange(NativeArray<Entity> entities,
            in ComponentTypeSet componentTypeSet, in SystemHandle originSystem = default)
        {
            if (entities.Length == 0 || componentTypeSet.Length == 0)
                return;
            EntityComponentStore->AssertCanAddComponents(entities, componentTypeSet);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_AddComponent(in originSystem, in entities, componentTypeSet.UnsafeTypesPtrRO, componentTypeSet.Length);
#endif

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.AddComponent, in m_WorldUnmanaged);
#endif

            if (componentTypeSet.ChunkComponentCount == 0 && entities.Length > FASTER_TO_BATCH_THRESHOLD &&
                EntityComponentStore->CreateEntityBatchList(entities, componentTypeSet.m_masks.SharedComponents,
                    Allocator.Temp, out var entityBatchList))
            {
                StructuralChange.AddComponentsEntitiesBatch(EntityComponentStore,
                    (UnsafeList<EntityBatchInChunk>*) NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(
                        ref entityBatchList), componentTypeSet);
            }
            else
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    StructuralChange.AddComponentsEntity(EntityComponentStore, &entity, componentTypeSet);
                }
            }

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="entityArray"></param>
        /// <param name="componentTypeSet"></param>
        /// <exception cref="InvalidOperationException">Thrown if a componentType is Entity type.</exception>
        public void RemoveMultipleComponentsDuringStructuralChange(NativeArray<Entity> entities,
            in ComponentTypeSet componentTypeSet, in SystemHandle originSystem = default)
        {
            if (entities.Length == 0 || componentTypeSet.Length == 0)
                return;
            EntityComponentStore->AssertCanRemoveComponents(componentTypeSet);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_RemoveComponent(in originSystem, in entities, componentTypeSet.UnsafeTypesPtrRO, componentTypeSet.Length);
#endif

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.RemoveComponent, in m_WorldUnmanaged);
#endif

            if (entities.Length > FASTER_TO_BATCH_THRESHOLD &&
                EntityComponentStore->CreateEntityBatchList(entities, 0, Allocator.Temp, out var entityBatchList))
            {
                StructuralChange.RemoveComponentsEntitiesBatch(EntityComponentStore,
                    (UnsafeList<EntityBatchInChunk>*) NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(
                        ref entityBatchList), componentTypeSet);
            }
            else
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    StructuralChange.RemoveComponentsEntity(EntityComponentStore, &entity, componentTypeSet);
                }
            }

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="componentType"></param>
        /// <returns></returns>
        public bool RemoveComponentDuringStructuralChange(Entity entity, ComponentType componentType,
            in SystemHandle originSystem = default)
        {
            EntityComponentStore->AssertCanRemoveComponent(componentType);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_RemoveComponent(in originSystem, &entity, 1, &componentType.TypeIndex, 1);
#endif

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.RemoveComponent, in m_WorldUnmanaged);
#endif

            var result = StructuralChange.RemoveComponentEntity(EntityComponentStore, &entity, componentType.TypeIndex);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif

            return result;
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="componentTypeSet"></param>
        public void RemoveComponentDuringStructuralChange(Entity entity, in ComponentTypeSet componentTypeSet,
            in SystemHandle originSystem = default)
        {
            EntityComponentStore->AssertCanRemoveComponents(componentTypeSet);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_RemoveComponent(in originSystem, &entity, 1, componentTypeSet.UnsafeTypesPtrRO, componentTypeSet.Length);
#endif

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.RemoveComponent, in m_WorldUnmanaged);
#endif

            StructuralChange.RemoveComponentsEntity(EntityComponentStore, &entity, componentTypeSet);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="queryImpl"></param>
        /// <param name="componentType"></param>
        /// <exception cref="InvalidOperationException">Thrown if the componentType is Entity type.</exception>
        public void RemoveComponentFromQueryDuringStructuralChange(EntityQueryImpl* queryImpl, ComponentType componentType,
            in SystemHandle originSystem = default)
        {
            if (queryImpl->IsEmptyIgnoreFilter)
                return;
            EntityComponentStore->AssertCanRemoveComponent(componentType);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.RemoveComponent, in m_WorldUnmanaged);
#endif
            queryImpl->SyncFilterTypes();
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
            {
                // TODO: Allocating and populating this filtered chunk array is redundant work, but that's what the journaling interface requires.
                using var chunks = queryImpl->ToArchetypeChunkArray(Allocator.TempJob);
                JournalAddRecord_RemoveComponent(in originSystem, in chunks, &componentType.TypeIndex, 1);
            }
#endif
            StructuralChange.RemoveComponentQuery(EntityComponentStore, queryImpl, componentType.TypeIndex);
#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="queryImpl"></param>
        /// <param name="componentTypeSet"></param>
        internal void RemoveMultipleComponentsFromQueryDuringStructuralChange(EntityQueryImpl* queryImpl, in ComponentTypeSet componentTypeSet,
            in SystemHandle originSystem = default)
        {
            if (queryImpl->IsEmptyIgnoreFilter || componentTypeSet.Length == 0)
                return;
            EntityComponentStore->AssertCanRemoveComponents(componentTypeSet);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.RemoveComponent, in m_WorldUnmanaged);
#endif
            queryImpl->SyncFilterTypes();
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
            {
                // TODO: Allocating and populating this filtered chunk array is redundant work, but that's what the journaling interface requires.
                using var chunks = queryImpl->ToArchetypeChunkArray(Allocator.TempJob);
                JournalAddRecord_RemoveComponent(in originSystem, in chunks, componentTypeSet.UnsafeTypesPtrRO,
                    componentTypeSet.Length);
            }
#endif
            StructuralChange.RemoveComponentsQuery(EntityComponentStore, queryImpl, componentTypeSet);
#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="chunks"></param>
        /// <param name="componentType"></param>
        internal void RemoveComponentDuringStructuralChange(NativeArray<ArchetypeChunk> chunks,
            ComponentType componentType, in SystemHandle originSystem = default)
        {
            if (chunks.Length == 0)
                return;

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_RemoveComponent(in originSystem, in chunks, &componentType.TypeIndex, 1);
#endif

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.RemoveComponent, in m_WorldUnmanaged);
#endif

            StructuralChange.RemoveComponentChunks(EntityComponentStore,
                (ArchetypeChunk*) NativeArrayUnsafeUtility.GetUnsafePtr(chunks), chunks.Length,
                componentType.TypeIndex);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="entityArray"></param>
        /// <param name="componentType"></param>
        /// <exception cref="ArgumentException">Thrown if componentType is Entity type.</exception>
        public void RemoveComponentDuringStructuralChange(NativeArray<Entity> entities, ComponentType componentType,
            in SystemHandle originSystem = default)
        {
            if (entities.Length == 0)
                return;

            EntityComponentStore->AssertCanRemoveComponent(componentType);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_RemoveComponent(in originSystem, in entities, &componentType.TypeIndex, 1);
#endif

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.RemoveComponent, in m_WorldUnmanaged);
#endif

            if (entities.Length > FASTER_TO_BATCH_THRESHOLD &&
                EntityComponentStore->CreateEntityBatchList(entities, 0, Allocator.Temp, out var entityBatchList))
            {
                StructuralChange.RemoveComponentEntitiesBatch(EntityComponentStore,
                    (UnsafeList<EntityBatchInChunk>*) NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(
                        ref entityBatchList), componentType.TypeIndex);
            }
            else
            {
                for (var i = 0; i < entities.Length; ++i)
                {
                    var entity = entities[i];
                    StructuralChange.RemoveComponentEntity(EntityComponentStore, &entity, componentType.TypeIndex);
                }
            }

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif
        }

        public bool HasComponent(Entity entity, ComponentType type)
        {
            return EntityComponentStore->HasComponent(entity, type);
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] {typeof(BurstCompatibleComponentData)})]
        public T GetComponentData<T>(Entity entity) where T : unmanaged, IComponentData
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();

            EntityComponentStore->AssertEntityHasComponent(entity, typeIndex);
            EntityComponentStore->AssertNotZeroSizedComponent(typeIndex);

            if (!IsInExclusiveTransaction)
                DependencyManager->CompleteWriteDependency(typeIndex);

            var ptr = EntityComponentStore->GetComponentDataWithTypeRO(entity, typeIndex);

            T value;
            UnsafeUtility.CopyPtrToStructure(ptr, out value);
            return value;
        }

        // This complements the non-RW version above, completing dependencies. It must return byte* because we end up
        // inserting into a RefRW in EntityManager.
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public byte* GetComponentDataRW_AsBytePointer(Entity entity, TypeIndex typeIndex)
        {
            EntityComponentStore->AssertEntityHasComponent(entity, typeIndex);
            EntityComponentStore->AssertNotZeroSizedComponent(typeIndex);
            EntityComponentStore->AssertComponentIsUnmanaged(typeIndex);

            if (!IsInExclusiveTransaction)
                DependencyManager->CompleteReadAndWriteDependency(typeIndex);

            return EntityComponentStore->GetComponentDataWithTypeRW(entity, typeIndex, EntityComponentStore->GlobalSystemVersion);
        }

        // Does not complete dependencies - used for low level component data memory access
        public void* GetComponentDataRawRW(Entity entity, TypeIndex typeIndex)
        {
            return EntityComponentStore->GetComponentDataRawRW(entity, typeIndex);
        }

        internal void* GetComponentDataRawRWEntityHasComponent(Entity entity, TypeIndex typeIndex)
        {
            return EntityComponentStore->GetComponentDataRawRWEntityHasComponent(entity, typeIndex);
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] {typeof(BurstCompatibleComponentData)})]
        public void SetComponentData<T>(Entity entity, T componentData, in SystemHandle originSystem = default)
            where T : unmanaged, IComponentData
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();

            EntityComponentStore->AssertEntityHasComponent(entity, typeIndex);
            EntityComponentStore->AssertNotZeroSizedComponent(typeIndex);

            if (!IsInExclusiveTransaction)
                DependencyManager->CompleteReadAndWriteDependency(typeIndex);

            var ptr = EntityComponentStore->GetComponentDataWithTypeRW(entity, typeIndex,
                EntityComponentStore->GlobalSystemVersion);
            UnsafeUtility.CopyStructureToPtr(ref componentData, ptr);
        }

        public void SetComponentDataRaw(Entity entity, TypeIndex typeIndex, void* data, int size,
            in SystemHandle originSystem = default)
        {
            EntityComponentStore->AssertEntityHasComponent(entity, typeIndex);
            EntityComponentStore->SetComponentDataRawEntityHasComponent(entity, typeIndex, data, size);
        }

        public bool IsComponentEnabled(Entity entity, TypeIndex typeIndex)
        {
            EntityComponentStore->AssertEntityHasComponent(entity, typeIndex);
            Unity.Entities.EntityComponentStore.AssertComponentEnableable(typeIndex);

            return EntityComponentStore->IsComponentEnabled(entity, typeIndex);
        }

        public bool IsComponentEnabled(Entity entity, TypeIndex typeIndex, ref LookupCache typeLookupCache)
        {
            EntityComponentStore->AssertEntityHasComponent(entity, typeIndex, ref typeLookupCache);
            Unity.Entities.EntityComponentStore.AssertComponentEnableable(typeIndex);

            return EntityComponentStore->IsComponentEnabled(entity, typeIndex, ref typeLookupCache);
        }

        public void SetComponentEnabled(Entity entity, TypeIndex typeIndex, bool value)
        {
            EntityComponentStore->AssertEntityHasComponent(entity, typeIndex);
            Unity.Entities.EntityComponentStore.AssertComponentEnableable(typeIndex);

            EntityComponentStore->SetComponentEnabled(entity, typeIndex, value);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_SetComponentEnabled(default, &entity, 1, &typeIndex, 1, value);
#endif
        }

        public void SetComponentEnabled(Entity entity, TypeIndex typeIndex, bool value, ref LookupCache typeLookupCache)
        {
            EntityComponentStore->AssertEntityHasComponent(entity, typeIndex, ref typeLookupCache);
            Unity.Entities.EntityComponentStore.AssertComponentEnableable(typeIndex);

            EntityComponentStore->SetComponentEnabled(entity, typeIndex, value, ref typeLookupCache);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_SetComponentEnabled(default, &entity, 1, &typeIndex, 1, value);
#endif
        }

        public bool IsEnabled(Entity entity)
        {
            return !HasComponent(entity, ComponentType.ReadWrite<Disabled>());
        }

        public void SetEnabled(Entity entity, bool value)
        {
            if (IsEnabled(entity) == value)
                return;

            var disabledType = ComponentType.ReadWrite<Disabled>();
            if (HasComponent(entity, ComponentType.ReadWrite<LinkedEntityGroup>()))
            {
                var typeIndex = TypeManager.GetTypeIndex<LinkedEntityGroup>();
                //@TODO DOTS-5412: We can't use WorldUpdate.Allocator here because DynamicBuffer.ToNativeArray() uses new NativeArray() instead of CollectionHelper.CreateNativeArray()
                var linkedEntities = GetBuffer<LinkedEntityGroup>(entity
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    , DependencyManager->Safety.GetSafetyHandle(typeIndex, false),
                    DependencyManager->Safety.GetBufferSafetyHandle(typeIndex)
#endif
                ).Reinterpret<Entity>()
                    .ToNativeArray(Allocator.TempJob);
                {
                    if (value)
                        RemoveComponentDuringStructuralChange(linkedEntities, disabledType);
                    else
                        AddComponentDuringStructuralChange(linkedEntities, disabledType);
                }
                linkedEntities.Dispose();
            }
            else
            {
                if (!value)
                    AddComponentDuringStructuralChange(entity, disabledType);
                else
                    RemoveComponentDuringStructuralChange(entity, disabledType);
            }
        }

        public void AddComponentForLinkedEntityGroup(Entity entity, EntityQueryMask mask, TypeIndex typeIndex, void* data, int componentSize)
        {
            if (!HasComponent(entity, ComponentType.ReadWrite<LinkedEntityGroup>()))
                return;

            var linkedTypeIndex = TypeManager.GetTypeIndex<LinkedEntityGroup>();
            using var linkedEntities = GetBuffer<LinkedEntityGroup>(entity
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    , DependencyManager->Safety.GetSafetyHandle(linkedTypeIndex, false),
                    DependencyManager->Safety.GetBufferSafetyHandle(linkedTypeIndex)
#endif
                ).Reinterpret<Entity>()
                .ToNativeArray(m_WorldUnmanaged.UpdateAllocator.ToAllocator);

            // Filter the linked entities based on the mask
            foreach (var e in linkedEntities)
            {
                if (mask.MatchesIgnoreFilter(e))
                {
                    AddComponentDuringStructuralChange(e, ComponentType.FromTypeIndex(typeIndex));
                    if (componentSize > 0)
                    {
                        SetComponentDataRaw(e, typeIndex, data, componentSize);
                    }
                }
            }
        }

        public void SetComponentForLinkedEntityGroup(Entity entity, EntityQueryMask mask, TypeIndex typeIndex, void* data, int componentSize)
        {
            if (!HasComponent(entity, ComponentType.ReadWrite<LinkedEntityGroup>()))
                return;

            EntityComponentStore->AssertNotZeroSizedComponent(typeIndex);

            var linkedTypeIndex = TypeManager.GetTypeIndex<LinkedEntityGroup>();
            using var linkedEntities = GetBuffer<LinkedEntityGroup>(entity
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    , DependencyManager->Safety.GetSafetyHandle(linkedTypeIndex, false),
                    DependencyManager->Safety.GetBufferSafetyHandle(linkedTypeIndex)
#endif
                ).Reinterpret<Entity>()
                .ToNativeArray(m_WorldUnmanaged.UpdateAllocator.ToAllocator);

            // Filter the linked entities based on the mask
            foreach (var e in linkedEntities)
            {
                if (mask.MatchesIgnoreFilter(e))
                {
                    EntityComponentStore->AssertEntityHasComponent(e, typeIndex);

                    SetComponentDataRaw(e, typeIndex, data, componentSize);
                }
            }
        }

        public void ReplaceComponentForLinkedEntityGroup(Entity entity, TypeIndex typeIndex, void* data, int componentSize)
        {
            if (!HasComponent(entity, ComponentType.ReadWrite<LinkedEntityGroup>()))
                return;

            EntityComponentStore->AssertNotZeroSizedComponent(typeIndex);

            var linkedTypeIndex = TypeManager.GetTypeIndex<LinkedEntityGroup>();
            using var linkedEntities = GetBuffer<LinkedEntityGroup>(entity
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    , DependencyManager->Safety.GetSafetyHandle(linkedTypeIndex, false),
                    DependencyManager->Safety.GetBufferSafetyHandle(linkedTypeIndex)
#endif
                ).Reinterpret<Entity>()
                .ToNativeArray(m_WorldUnmanaged.UpdateAllocator.ToAllocator);

            // Filter the linked entities based on the mask
            foreach (var e in linkedEntities)
            {
                if (EntityComponentStore->HasComponent(e, typeIndex))
                {
                    SetComponentDataRaw(e, typeIndex, data, componentSize);
                }
            }
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="componentData"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public bool AddSharedComponentDataDuringStructuralChange_Managed<T>(
            Entity entity,
            T componentData,
            in SystemHandle originSystem = default)
            where T : struct, ISharedComponentData
        {
            Assert.IsTrue(TypeManager.IsManagedSharedComponent(TypeManager.GetTypeIndex<T>()));

            //TODO: optimization: set value when component is added, not afterwards
            var added = AddComponentDuringStructuralChange(entity, ComponentType.ReadWrite<T>(), in originSystem);
            SetSharedComponentData_Managed(entity, componentData, in originSystem);
            return added;
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="entities"></param>
        /// <param name="componentData"></param>
        /// <param name="originSystem"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void AddSharedComponentDataDuringStructuralChange_Managed<T>(
            NativeArray<Entity> entities,
            T componentData,
            in SystemHandle originSystem = default)
            where T : struct, ISharedComponentData
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            Assert.IsTrue(TypeManager.IsManagedSharedComponent(typeIndex));

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
            {
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.AddComponent, in m_WorldUnmanaged);
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.SetSharedComponent, in m_WorldUnmanaged);
            }
#endif

            var componentType = ComponentType.FromTypeIndex(typeIndex);
            var newSharedComponentDataIndex = InsertSharedComponent(componentData);
            StructuralChange.AddSharedComponentDataIndexWithBurst(EntityComponentStore, (Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length, componentType,
                newSharedComponentDataIndex);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
            {
                StructuralChangesRecorder.End(); // SetSharedComponent
                StructuralChangesRecorder.End(); // AddComponent
            }
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
            {
                JournalAddRecord_AddComponent(in originSystem, in entities, &typeIndex, 1);
                JournalAddRecord_SetSharedComponentManaged(in originSystem, in entities, typeIndex);
            }
#endif
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="chunks"></param>
        /// <param name="sharedComponentIndex"></param>
        /// <param name="componentType"></param>
        internal void AddSharedComponentDataDuringStructuralChange(
            NativeArray<ArchetypeChunk> chunks,
            int sharedComponentIndex,
            ComponentType componentType,
            in SystemHandle originSystem = default)
        {
            Assert.IsTrue(componentType.IsSharedComponent);
            if (chunks.Length == 0)
                return;

            StructuralChange.AddSharedComponentChunks(EntityComponentStore,
                (ArchetypeChunk*) NativeArrayUnsafeUtility.GetUnsafePtr(chunks), chunks.Length, componentType.TypeIndex,
                sharedComponentIndex);
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="chunks"></param>
        /// <param name="sharedComponentIndex"></param>
        /// <param name="componentType"></param>
        internal void AddSharedComponentDataToQueryDuringStructuralChange(
            EntityQueryImpl* queryImpl,
            int sharedComponentIndex,
            ComponentType componentType,
            in SystemHandle originSystem = default)
        {
            Assert.IsTrue(componentType.IsSharedComponent);
            Assert.IsTrue(TypeManager.IsManagedSharedComponent(componentType.TypeIndex));
            if (queryImpl->IsEmptyIgnoreFilter)
                return;
            EntityComponentStore->AssertCanAddComponent(queryImpl, componentType);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
            {
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.AddComponent, in m_WorldUnmanaged);
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.SetSharedComponent, in m_WorldUnmanaged);
            }
#endif
            queryImpl->SyncFilterTypes();
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
            {
                // TODO: Allocating and populating this filtered chunk array is redundant work, but that's what the journaling interface requires.
                using var chunks = queryImpl->ToArchetypeChunkArray(Allocator.TempJob);
                if (Hint.Likely(chunks.Length > 0))
                {
                    var typeIndex = componentType.TypeIndex;
                    JournalAddRecord_AddComponent(default, in chunks, &typeIndex, 1);
                    JournalAddRecord_SetSharedComponentManaged(default, in chunks, typeIndex);
                }
            }
#endif
            StructuralChange.AddSharedComponentQuery(EntityComponentStore, queryImpl, componentType.TypeIndex, sharedComponentIndex);
#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
            {
                StructuralChangesRecorder.End(); // SetSharedComponent
                StructuralChangesRecorder.End(); // AddComponent
            }
#endif
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="queryImpl"></param>
        /// <param name="sharedComponentIndex"></param>
        /// <param name="componentType"></param>
        /// <param name="componentValue"></param>
        internal void AddSharedComponentDataToQueryDuringStructuralChange_Unmanaged(
            EntityQueryImpl* queryImpl,
            int sharedComponentIndex,
            ComponentType componentType,
            void* componentValue,
            in SystemHandle originSystem = default)
        {
            Assert.IsTrue(componentType.IsSharedComponent);
            if (queryImpl->IsEmptyIgnoreFilter)
                return;
            EntityComponentStore->AssertCanAddComponent(queryImpl, componentType);
#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
            {
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.AddComponent, in m_WorldUnmanaged);
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.SetSharedComponent, in m_WorldUnmanaged);
            }
#endif
            queryImpl->SyncFilterTypes();
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
            {
                var typeIndex = componentType.TypeIndex;
                // TODO: Allocating and populating this filtered chunk array is redundant work, but that's what the journaling interface requires.
                using var chunks = queryImpl->ToArchetypeChunkArray(Allocator.TempJob);
                if (Hint.Likely(chunks.Length > 0))
                {
                    JournalAddRecord_AddComponent(default, in chunks, &typeIndex, 1);
                    JournalAddRecord_SetSharedComponent(default, in chunks, typeIndex, componentValue,
                        TypeManager.GetTypeInfo(typeIndex).TypeSize);
                }
            }
#endif
            StructuralChange.AddSharedComponentQuery(EntityComponentStore, queryImpl, componentType.TypeIndex, sharedComponentIndex);
#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
            {
                StructuralChangesRecorder.End(); // SetSharedComponent
                StructuralChangesRecorder.End(); // AddComponent
            }
#endif
        }

        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void AddSharedComponentDataBoxedDefaultMustBeNullDuringStructuralChange(NativeArray<Entity> entities,
            TypeIndex typeIndex, int hashCode, object componentData, in SystemHandle originSystem = default)
        {
            if (entities.Length == 0)
                return;

            //TODO: optimization: set value when component is added, not afterwards
            AddComponentDuringStructuralChange(entities, ComponentType.FromTypeIndex(typeIndex), in originSystem);
            SetSharedComponentDataBoxedDefaultMustBeNullDuringStructuralChange(entities, typeIndex, hashCode,
                componentData, in originSystem);
        }

        [GenerateTestsForBurstCompatibility]
        public void AddSharedComponentDataAddrDefaultMustBeNullDuringStructuralChange(NativeArray<Entity> entities, TypeIndex typeIndex, int hashCode, void* componentDataAddr)
        {
            //TODO: optimization: set value when component is added, not afterwards
            AddComponentDuringStructuralChange(entities, ComponentType.FromTypeIndex(typeIndex));
            SetSharedComponentDataAddrDefaultMustBeNullDuringStructuralChange(entities, typeIndex, hashCode, componentDataAddr);
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="typeIndex"></param>
        /// <param name="hashCode"></param>
        /// <param name="componentData"></param>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public bool AddSharedComponentDataBoxedDefaultMustBeNullDuringStructuralChange(Entity entity, TypeIndex typeIndex,
            int hashCode, object componentData, in SystemHandle originSystem = default)
        {
            //TODO: optimize this (no need to move the entity to a new chunk twice)
            var added = AddComponentDuringStructuralChange(entity, ComponentType.FromTypeIndex(typeIndex),
                originSystem);
            SetSharedComponentDataBoxedDefaultMustBeNullDuringStructuralChange(entity, typeIndex, hashCode,
                componentData, in originSystem);

            return added;
        }

        [GenerateTestsForBurstCompatibility]
        // if componentData is null: consider we are adding the default value
        // if defaultComponentData is null: consider we are inserting non default value
        public bool AddSharedComponentDataDuringStructuralChange_Unmanaged(Entity entity, ComponentType componentType, void* componentData, void* defaultComponentData)
        {
            Assert.IsFalse(TypeManager.IsManagedSharedComponent(componentType.TypeIndex));

            var added = AddComponentDuringStructuralChange(entity, componentType);
            SetSharedComponentData_Unmanaged(entity, componentType.TypeIndex, componentData, defaultComponentData);
            return added;
        }

        [GenerateTestsForBurstCompatibility]
        // if componentData is null: consider we are adding the default value
        // if defaultComponentData is null: consider we are inserting non default value
        public void AddSharedComponentDataDuringStructuralChange_Unmanaged(NativeArray<Entity> entities,
            ComponentType componentType, void* componentData, void* defaultComponentData, in SystemHandle originSystem = default)
        {
            Assert.IsFalse(TypeManager.IsManagedSharedComponent(componentType.TypeIndex));

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
            {
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.AddComponent, in m_WorldUnmanaged);
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.SetSharedComponent, in m_WorldUnmanaged);
            }
#endif

            var newSharedComponentDataIndex = InsertSharedComponent_Unmanaged(componentType.TypeIndex, 0, componentData, defaultComponentData);
            StructuralChange.AddSharedComponentDataIndexWithBurst(EntityComponentStore, (Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length, componentType,
                newSharedComponentDataIndex);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
            {
                StructuralChangesRecorder.End(); // SetSharedComponent
                StructuralChangesRecorder.End(); // AddComponent
            }
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
            {
                var typeIndex = componentType.TypeIndex;
                JournalAddRecord_AddComponent(in originSystem, in entities, &typeIndex, 1);
                JournalAddRecord_SetSharedComponent(in originSystem, in entities, typeIndex, componentData, TypeManager.GetTypeInfo(typeIndex).TypeSize);
            }
#endif
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        public void GetAllUniqueSharedComponents_Unmanaged<T>(out UnsafeList<T> sharedComponentValues, AllocatorManager.AllocatorHandle allocator) where T : unmanaged, ISharedComponentData
        {
            Assert.IsFalse(TypeManager.IsManagedSharedComponent(TypeManager.GetTypeIndex<T>()));

            EntityComponentStore->GetAllUniqueSharedComponents_Unmanaged<T>(out sharedComponentValues, allocator);
        }

        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public object GetSharedComponentDataNonDefaultBoxed(int shareComponentIndex)
        {
            if (Entities.EntityComponentStore.IsUnmanagedSharedComponentIndex(shareComponentIndex))
            {
                var typeIndex = Entities.EntityComponentStore.GetComponentTypeFromSharedComponentIndex(shareComponentIndex);
                return EntityComponentStore->GetSharedComponentDataObject_Unmanaged(shareComponentIndex, typeIndex);
            }
            else
            {
                return ManagedComponentStore.GetSharedComponentDataNonDefaultBoxed(shareComponentIndex);
            }
        }

        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public object GetSharedComponentDataBoxed(int shareComponentIndex, TypeIndex typeIndex)
        {
            if (Entities.EntityComponentStore.IsUnmanagedSharedComponentIndex(shareComponentIndex))
            {
                return EntityComponentStore->GetSharedComponentDataObject_Unmanaged(shareComponentIndex, typeIndex);
            }
            else
            {
                return ManagedComponentStore.GetSharedComponentDataBoxed(shareComponentIndex, typeIndex);
            }
        }

        public JobHandle GetCreatedAndDestroyedEntitiesAsync(NativeList<int> state, NativeList<Entity> createdEntities,
            NativeList<Entity> destroyedEntities)
        {
            var jobHandle = Entities.EntityComponentStore.GetCreatedAndDestroyedEntities(EntityComponentStore, state,
                createdEntities, destroyedEntities, true);
            DependencyManager->AddDependency(null, 0, null, 0, jobHandle);

            return jobHandle;
        }

        public void GetCreatedAndDestroyedEntities(NativeList<int> state, NativeList<Entity> createdEntities,
            NativeList<Entity> destroyedEntities)
        {
            Entities.EntityComponentStore.GetCreatedAndDestroyedEntities(EntityComponentStore, state, createdEntities,
                destroyedEntities, false);
        }

        [ExcludeFromBurstCompatTesting("Potentially accesses managed component store")]
        public T GetSharedComponentData<T>(Entity entity) where T : struct, ISharedComponentData
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            EntityComponentStore->AssertEntityHasComponent(entity, typeIndex);

            var sharedComponentIndex = EntityComponentStore->GetSharedComponentDataIndex(entity, typeIndex);
            return GetSharedComponentData<T>(sharedComponentIndex);
        }

        [ExcludeFromBurstCompatTesting("Potentially accesses managed component store")]
        public void SetSharedComponentData_Managed<T>(
            Entity entity,
            T componentData,
            in SystemHandle originSystem = default)
            where T : struct, ISharedComponentData
        {
            Assert.IsTrue(TypeManager.IsManagedSharedComponent(TypeManager.GetTypeIndex<T>()));

            var typeIndex = TypeManager.GetTypeIndex<T>();
            EntityComponentStore->AssertEntityHasComponent(entity, typeIndex);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.SetSharedComponent, in m_WorldUnmanaged);
#endif

            var componentType = ComponentType.FromTypeIndex(typeIndex);
            var newSharedComponentDataIndex = InsertSharedComponent(componentData);
            EntityComponentStore->SetSharedComponentDataIndex(entity, componentType, newSharedComponentDataIndex);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_SetSharedComponentManaged(in originSystem, &entity, 1, typeIndex);
#endif
        }

        [ExcludeFromBurstCompatTesting("Potentially accesses managed component store")]
        public void SetSharedComponentData_Managed<T>(
            NativeArray<Entity> entities,
            T componentData,
            in SystemHandle originSystem = default)
            where T : struct, ISharedComponentData
        {
            Assert.IsTrue(TypeManager.IsManagedSharedComponent(TypeManager.GetTypeIndex<T>()));

            var typeIndex = TypeManager.GetTypeIndex<T>();
            var componentType = ComponentType.FromTypeIndex(typeIndex);
            EntityComponentStore->AssertEntityHasComponent(entities, componentType);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.SetSharedComponent, in m_WorldUnmanaged);
#endif

            var newSharedComponentDataIndex = InsertSharedComponent(componentData);
            StructuralChange.SetSharedComponentDataIndexWithBurst(EntityComponentStore,
                (Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length, componentType,
                newSharedComponentDataIndex);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_SetSharedComponentManaged(in originSystem, in entities, typeIndex);
#endif
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="typeIndex"></param>
        /// <param name="hashCode"></param>
        /// <param name="componentData"></param>
        [ExcludeFromBurstCompatTesting("Potentially accesses managed component store")]
        public void SetSharedComponentDataBoxedDefaultMustBeNullDuringStructuralChange(Entity entity, TypeIndex typeIndex,
            int hashCode, object componentData, in SystemHandle originSystem = default)
        {
            EntityComponentStore->AssertEntityHasComponent(entity, typeIndex);

            var newSharedComponentDataIndex = 0;
            var isManagedSharedComponent = TypeManager.IsManagedSharedComponent(typeIndex);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.SetSharedComponent, in m_WorldUnmanaged);
#endif

            if (componentData != null) // null means default
            {
                if (isManagedSharedComponent)
                {
                    newSharedComponentDataIndex = ManagedComponentStore.InsertSharedComponentAssumeNonDefault(typeIndex, hashCode, componentData);
                }
                else
                {
                    var componentDataAddr = (byte*)UnsafeUtility.PinGCObjectAndGetAddress(componentData, out var gcHandle) + TypeManager.ObjectOffset;
                    newSharedComponentDataIndex = EntityComponentStore->InsertSharedComponent_Unmanaged(typeIndex, hashCode, componentDataAddr, null);
                    UnsafeUtility.ReleaseGCObject(gcHandle);
                }
            }
            var componentType = ComponentType.FromTypeIndex(typeIndex);
            SetSharedComponentDataIndexWithBurst(EntityComponentStore, entity, componentType, newSharedComponentDataIndex);

            m_ManagedReferenceIndexList.Add(newSharedComponentDataIndex);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif
        }

        [BurstCompile]
        private static void SetSharedComponentDataIndexWithBurst(EntityComponentStore* ecs, in Entity entity,
            in ComponentType componentType, int newSharedComponentDataIndex)
        {
            ecs->SetSharedComponentDataIndex(entity, componentType, newSharedComponentDataIndex);
        }

        [ExcludeFromBurstCompatTesting("Potentially accesses managed component store")]
        public void SetSharedComponentDataBoxedDefaultMustBeNullDuringStructuralChange(NativeArray<Entity> entities,
            TypeIndex typeIndex, int hashCode, object componentData, in SystemHandle originSystem = default)
        {
            if (entities.Length == 0)
                return;

            var type = ComponentType.FromTypeIndex(typeIndex);
            EntityComponentStore->AssertEntityHasComponent(entities, type);
            Assert.IsTrue(TypeManager.IsManagedSharedComponent(typeIndex));

            var newSharedComponentDataIndex = 0;
            if (componentData != null) // null means default
            {
                newSharedComponentDataIndex =
                    ManagedComponentStore.InsertSharedComponentAssumeNonDefault(typeIndex, hashCode, componentData);
            }

            for (int i = 0; i < entities.Length; i++)
            {
                SetSharedComponentDataIndexWithBurst(EntityComponentStore, entities[i], type, newSharedComponentDataIndex);
            }

            m_ManagedReferenceIndexList.Add(newSharedComponentDataIndex);
        }

        /// <summary>
        /// Sets an unmanaged shared component on an entity.
        /// </summary>
        /// <remarks>NOTE: This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).</remarks>
        /// <param name="entity">The target entity.</param>
        /// <param name="typeIndex">The type of the component.</param>
        /// <param name="hashCode">The new hash code of the component.</param>
        /// <param name="componentDataAddr">The unmanaged component data pointer.</param>
        [GenerateTestsForBurstCompatibility]
        public void SetSharedComponentDataAddrDefaultMustBeNullDuringStructuralChange(
            Entity entity,
            TypeIndex typeIndex,
            int hashCode,
            void* componentDataAddr)
        {
            UnityEngine.Assertions.Assert.IsTrue(
                TypeManager.IsSharedComponentType(typeIndex) &&
                !TypeManager.IsManagedSharedComponent(typeIndex));
            var type = ComponentType.FromTypeIndex(typeIndex);
            EntityComponentStore->AssertEntityHasComponent(entity, type);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.SetSharedComponent, in m_WorldUnmanaged);
#endif

            var newSharedComponentDataIndex = 0;
            if (componentDataAddr != null) // null means default
            {
                newSharedComponentDataIndex = EntityComponentStore->InsertSharedComponent_Unmanaged(typeIndex, hashCode, componentDataAddr, null);
            }

            SetSharedComponentDataIndexWithBurst(EntityComponentStore, entity, type, newSharedComponentDataIndex);


            m_ManagedReferenceIndexList.Add(newSharedComponentDataIndex);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif
        }

        [GenerateTestsForBurstCompatibility]
        public void SetSharedComponentDataAddrDefaultMustBeNullDuringStructuralChange(
            NativeArray<Entity> entities,
            TypeIndex typeIndex,
            int hashCode,
            void* componentDataAddr,
            in SystemHandle originSystem = default)
        {
            UnityEngine.Assertions.Assert.IsFalse(TypeManager.IsManagedSharedComponent(typeIndex));
            var type = ComponentType.FromTypeIndex(typeIndex);
            EntityComponentStore->AssertEntityHasComponent(entities, type);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.SetSharedComponent, in m_WorldUnmanaged);
#endif

            var newSharedComponentDataIndex = 0;
            if (componentDataAddr != null) // null means default
            {
                newSharedComponentDataIndex = EntityComponentStore->InsertSharedComponent_Unmanaged(typeIndex, hashCode, componentDataAddr, null);
            }

            for (int i = 0; i < entities.Length; i++)
            {
                SetSharedComponentDataIndexWithBurst(EntityComponentStore, entities[i], type, newSharedComponentDataIndex);
            }

            m_ManagedReferenceIndexList.Add(newSharedComponentDataIndex);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif
        }

        [ExcludeFromBurstCompatTesting("Takes managed list")]
        public void GetAllUniqueSharedComponents<T>(List<T> sharedComponentValues) where T : struct, ISharedComponentData
        {
            var ti = TypeManager.GetTypeIndex<T>();
            if (TypeManager.IsManagedSharedComponent(ti))
            {
                ManagedComponentStore.GetAllUniqueSharedComponents_Managed(sharedComponentValues);
            }
            else
            {
                var defaultValue = default(T);
                EntityComponentStore->GetAllUniqueSharedComponents_Unmanaged(ti, UnsafeUtility.AddressOf(ref defaultValue), out var unmanagedSharedComponentValues, out _, Allocator.Temp);
                for (int i = 0; i < unmanagedSharedComponentValues.Length; i++)
                {
                    var el = UnsafeUtility.ReadArrayElement<T>(unmanagedSharedComponentValues.Ptr, i);
                    sharedComponentValues.Add(el);
                }
            }
        }

        [ExcludeFromBurstCompatTesting("Takes managed list")]
        public void GetAllUniqueSharedComponents<T>(List<T> sharedComponentValues, List<int> sharedComponentIndices) where T : struct, ISharedComponentData
        {
            var ti = TypeManager.GetTypeIndex<T>();
            if (TypeManager.IsManagedSharedComponent(ti))
            {
                ManagedComponentStore.GetAllUniqueSharedComponents_Managed(sharedComponentValues, sharedComponentIndices);
            }
            else
            {
                var defaultValue = default(T);
                EntityComponentStore->GetAllUniqueSharedComponents_Unmanaged(
                    ti,
                    UnsafeUtility.AddressOf(ref defaultValue),
                    out var unmanagedSharedComponentValues,
                    out var unmanagedSharedComponentIndices,
                    Allocator.Temp);
                for (int i = 0; i < unmanagedSharedComponentValues.Length; i++)
                {
                    var el = UnsafeUtility.ReadArrayElement<T>(unmanagedSharedComponentValues.Ptr, i);
                    sharedComponentValues.Add(el);
                    sharedComponentIndices.Add(unmanagedSharedComponentIndices[i]);
                }
            }
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="newData"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [ExcludeFromBurstCompatTesting("Potentially accesses managed component store")]
        public int InsertSharedComponent<T>(T newData) where T : struct, ISharedComponentData
        {
            var ti = TypeManager.GetTypeIndex<T>();
            int index;
            if (TypeManager.IsManagedSharedComponent(ti))
            {
                index = ManagedComponentStore.InsertSharedComponent_Managed(newData);
            }
            else
            {
                var defaultData = default(T);
                index = EntityComponentStore->InsertSharedComponent_Unmanaged(ti,
                    0,
                    UnsafeUtility.AddressOf(ref newData),
                    UnsafeUtility.AddressOf(ref defaultData));
            }
            m_ManagedReferenceIndexList.Add(index);
            return index;
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="newData"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        public int InsertSharedComponent_Unmanaged<T>(T newData) where T : unmanaged, ISharedComponentData
        {
            var ti = TypeManager.GetTypeIndex<T>();
            int index;
            Assert.IsFalse(TypeManager.IsManagedSharedComponent(ti));

            var defaultData = default(T);
            index = EntityComponentStore->InsertSharedComponent_Unmanaged(ti,
                0,
                UnsafeUtility.AddressOf(ref newData),
                UnsafeUtility.AddressOf(ref defaultData));

            m_ManagedReferenceIndexList.Add(index);
            return index;
        }

        [ExcludeFromBurstCompatTesting("Potentially accesses managed component store")]
        public int GetSharedComponentVersion<T>(T sharedData) where T : struct
        {
            var ti = TypeManager.GetTypeIndex<T>();
            if (TypeManager.IsManagedSharedComponent(ti))
            {
                return ManagedComponentStore.GetSharedComponentVersion_Managed(sharedData);
            }
            else
            {
                var defaultData = default(T);
                return EntityComponentStore->GetSharedComponentVersion_Unmanaged(ti, UnsafeUtility.AddressOf(ref sharedData), UnsafeUtility.AddressOf(ref defaultData));
            }
        }

        [ExcludeFromBurstCompatTesting("Potentially accesses managed component store")]
        public T GetSharedComponentData<T>(int sharedComponentIndex) where T : struct
        {
            if (Entities.EntityComponentStore.IsUnmanagedSharedComponentIndex(sharedComponentIndex))
            {
                T res = default(T);
                if (sharedComponentIndex != 0)
                {
                    EntityComponentStore->GetSharedComponentData_Unmanaged(sharedComponentIndex, TypeManager.GetTypeIndex<T>(), UnsafeUtility.AddressOf(ref res));
                }
                return res;
            }
            else
            {
                return ManagedComponentStore.GetSharedComponentData_Managed<T>(sharedComponentIndex);
            }
        }

        [ExcludeFromBurstCompatTesting("Potentially accesses managed component store")]
        public void AddSharedComponentReference(int sharedComponentIndex, int numRefs = 1)
        {
            if (Unity.Entities.EntityComponentStore.IsUnmanagedSharedComponentIndex(sharedComponentIndex))
            {
                EntityComponentStore->AddSharedComponentReference_Unmanaged(sharedComponentIndex, numRefs);
            }
            else
            {
                ManagedComponentStore.AddSharedComponentReference_Managed(sharedComponentIndex, numRefs);
            }
        }

        [ExcludeFromBurstCompatTesting("Potentially accesses managed component store")]
        public void RemoveSharedComponentReference(int sharedComponentIndex, int numRefs = 1)
        {
            if (Entities.EntityComponentStore.IsUnmanagedSharedComponentIndex(sharedComponentIndex))
            {
                EntityComponentStore->RemoveSharedComponentReference_Unmanaged(sharedComponentIndex, numRefs);
            }
            else
            {
                ManagedComponentStore.RemoveSharedComponentReference_Managed(sharedComponentIndex, numRefs);
            }
        }
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public int GetSharedComponentCount()
        {
            return ManagedComponentStore.GetSharedComponentCount() + EntityComponentStore->GetUnmanagedSharedComponentCount();
        }
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public NativeParallelHashMap<int, int> MoveAllSharedComponents(EntityComponentStore* srcEntityComponentStore, ManagedComponentStore srcManagedComponents, AllocatorManager.AllocatorHandle allocator)
        {
            var managedsharedComponentCount = srcManagedComponents.GetSharedComponentCount();
            var remap = new NativeParallelHashMap<int, int>(srcEntityComponentStore->GetUnmanagedSharedComponentCount() + managedsharedComponentCount, allocator);
            ManagedComponentStore.MoveAllSharedComponents_Managed(srcManagedComponents, ref remap, managedsharedComponentCount);
            EntityComponentStore->MoveAllSharedComponents_Unmanaged(srcEntityComponentStore, ref remap);
            return remap;
        }

        [BurstCompile]
        static void BuildSharedComponentMapForMoving(ref NativeHashMap<int, int> hashmap, ref NativeArray<ArchetypeChunk> chunks)
        {
            var map = hashmap;
            // Build a map of all shared component values that will be moved with the appropriate refcount
            for (int i = 0; i < chunks.Length; ++i)
            {
                var chunk = chunks[i].m_Chunk;
                var archetype = chunks[i].Archetype.Archetype;
                var sharedComponentValues = archetype->Chunks.GetSharedComponentValues(chunk.ListIndex);

                for (int sharedComponentValueIndex = 0; sharedComponentValueIndex < archetype->NumSharedComponents; ++sharedComponentValueIndex)
                {
                    var sharedComponentIndex = sharedComponentValues[sharedComponentValueIndex];
                    if (map.TryGetValue(sharedComponentIndex, out var refCounter))
                    {
                        map[sharedComponentIndex] = ++refCounter;
                    }
                    else
                    {
                        map.Add(sharedComponentIndex, 1);
                    }
                }
            }
        }

        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public NativeHashMap<int, int> MoveSharedComponents(EntityComponentStore* srcEntityComponentStore, ManagedComponentStore srcManagedComponents, NativeArray<ArchetypeChunk> chunks, AllocatorManager.AllocatorHandle allocator)
        {
            var map = new NativeHashMap<int, int>(64, allocator);
            BuildSharedComponentMapForMoving(ref map, ref chunks);

            // Move all shared components that are being referenced
            var kvps = map.GetKeyValueArrays(Allocator.Temp);
            for (int i = 0; i < kvps.Length; i++)
            {
                var sharedComponentIndex = kvps.Keys[i];
                int dstIndex;

                // * remove refcount based on refcount table
                // * -1 because CloneSharedComponentNonDefault above adds 1 refcount
                int srcRefCount = kvps.Values[i];
                if (Entities.EntityComponentStore.IsUnmanagedSharedComponentIndex(sharedComponentIndex))
                {
                    dstIndex = EntityComponentStore->CloneSharedComponentNonDefault(srcEntityComponentStore, sharedComponentIndex);
                    EntityComponentStore->AddSharedComponentReference_Unmanaged(dstIndex, srcRefCount - 1);
                    srcEntityComponentStore->RemoveSharedComponentReference_Unmanaged(sharedComponentIndex, srcRefCount);
                    EntityComponentStore->IncrementSharedComponentVersion_Unmanaged(dstIndex);
                }
                else
                {
                    dstIndex = ManagedComponentStore.CloneSharedComponentNonDefault(srcManagedComponents, sharedComponentIndex);
                    ManagedComponentStore.AddSharedComponentReference_Managed(dstIndex, srcRefCount - 1);
                    srcManagedComponents.RemoveSharedComponentReference_Managed(sharedComponentIndex, srcRefCount);
                    ManagedComponentStore.IncrementSharedComponentVersion_Managed(dstIndex);
                }

                map[sharedComponentIndex] = dstIndex;
            }

            kvps.Dispose();
            return map;
        }

        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public int InsertSharedComponentAssumeNonDefault(TypeIndex typeIndex, int hashCode, object sharedComponent)
        {
            if (TypeManager.IsManagedSharedComponent(typeIndex))
            {
                return ManagedComponentStore.InsertSharedComponentAssumeNonDefault(typeIndex, hashCode, sharedComponent);
            }
            else
            {
                /*
                 * this is actually used in hybrid to read unmanaged shared components, but it is NOT called in dotsrt
                 */
                var sharedComponentAddr = (byte*)UnsafeUtility.PinGCObjectAndGetAddress(sharedComponent, out var gcHandle) + TypeManager.ObjectOffset;
                var index = EntityComponentStore->InsertSharedComponent_Unmanaged(typeIndex, hashCode, sharedComponentAddr, null);
                UnsafeUtility.ReleaseGCObject(gcHandle);
                return index;
            }
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="typeIndex"></param>
        /// <param name="hashCode"></param>
        /// <param name="newData"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        [GenerateTestsForBurstCompatibility]
        public int InsertSharedComponent_Unmanaged(TypeIndex typeIndex, int hashCode, void* newData, void* defaultValue)
        {
            var sharedComponentIndex = EntityComponentStore->InsertSharedComponent_Unmanaged(typeIndex, hashCode, newData, defaultValue);
            if (sharedComponentIndex != 0)
                m_ManagedReferenceIndexList.Add(sharedComponentIndex);
            return sharedComponentIndex;
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="typeIndex"></param>
        /// <param name="hashCode"></param>
        /// <param name="newData"></param>
        /// <returns></returns>
        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public int InsertSharedComponent_Managed(TypeIndex typeIndex, int hashCode, object newData)
        {
            var sharedComponentIndex =
                ManagedComponentStore.InsertSharedComponentAssumeNonDefault(typeIndex, hashCode, newData);
            if (sharedComponentIndex != 0)
                m_ManagedReferenceIndexList.Add(sharedComponentIndex);
            return sharedComponentIndex;
        }

        [GenerateTestsForBurstCompatibility]
        public void SetSharedComponentData_Unmanaged(
            Entity entity,
            TypeIndex typeIndex,
            void* componentData,
            void* componentDataDefaultValue,
            in SystemHandle originSystem = default)
        {
            var componentType = ComponentType.FromTypeIndex(typeIndex);
            UnityEngine.Assertions.Assert.IsFalse(TypeManager.IsManagedSharedComponent(typeIndex));
            EntityComponentStore->AssertEntityHasComponent(entity, typeIndex);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.SetSharedComponent, in m_WorldUnmanaged);
#endif

            var newSharedComponentDataIndex = InsertSharedComponent_Unmanaged(typeIndex, 0, componentData, componentDataDefaultValue);
            EntityComponentStore->SetSharedComponentDataIndex(entity, componentType, newSharedComponentDataIndex);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_SetSharedComponent(in originSystem, &entity, 1, componentType.TypeIndex, componentData, TypeManager.GetTypeInfo(componentType.TypeIndex).TypeSize);
#endif
        }

        [GenerateTestsForBurstCompatibility]
        public void SetSharedComponentData_Unmanaged(
            NativeArray<Entity> entities,
            TypeIndex typeIndex,
            void* componentData,
            void* componentDataDefaultValue,
            in SystemHandle originSystem = default)
        {
            var componentType = ComponentType.FromTypeIndex(typeIndex);
            UnityEngine.Assertions.Assert.IsFalse(TypeManager.IsManagedSharedComponent(typeIndex));
            EntityComponentStore->AssertEntityHasComponent(entities, componentType);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.SetSharedComponent, in m_WorldUnmanaged);
#endif

            var newSharedComponentDataIndex = InsertSharedComponent_Unmanaged(typeIndex, 0, componentData, componentDataDefaultValue);
            StructuralChange.SetSharedComponentDataIndexWithBurst(EntityComponentStore, (Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length, componentType,
                newSharedComponentDataIndex);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_SetSharedComponent(in originSystem, in entities, componentType.TypeIndex, componentData, TypeManager.GetTypeInfo(componentType.TypeIndex).TypeSize);
#endif
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="queryImpl"></param>
        /// <param name="sharedComponentIndex"></param>
        /// <param name="componentType"></param>
        /// <param name="originSystem"></param>
        public void SetSharedComponentDataOnQueryDuringStructuralChange(
            EntityQueryImpl* queryImpl,
            int sharedComponentIndex,
            ComponentType componentType,
            in SystemHandle originSystem = default)
        {
            Assert.IsTrue(TypeManager.IsManagedSharedComponent(componentType.TypeIndex));
            EntityComponentStore->AssertNonEmptyArchetypesHaveComponent(queryImpl->_QueryData->MatchingArchetypes, componentType);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.SetSharedComponent, in m_WorldUnmanaged);
#endif
            queryImpl->SyncFilterTypes();
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
            {
                // TODO: Allocating and populating this filtered chunk array is redundant work, but that's what the journaling interface requires.
                using var chunks = queryImpl->ToArchetypeChunkArray(Allocator.TempJob);
                if (Hint.Likely(chunks.Length > 0))
                {
                    JournalAddRecord_SetSharedComponentManaged(default, in chunks, componentType.TypeIndex);
                }
            }
#endif
            StructuralChange.SetSharedComponentDataIndexWithBurst(EntityComponentStore,
                queryImpl, componentType, sharedComponentIndex);
#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif

        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="queryImpl"></param>
        /// <param name="sharedComponentIndex"></param>
        /// <param name="componentType"></param>
        /// <param name="componentData"></param>
        /// <param name="originSystem"></param>
        [GenerateTestsForBurstCompatibility]
        public void SetSharedComponentDataOnQueryDuringStructuralChange_Unmanaged(
            EntityQueryImpl* queryImpl,
            int sharedComponentIndex,
            ComponentType componentType,
            void* componentData,
            in SystemHandle originSystem = default)
        {
            Assert.IsTrue(componentType.IsSharedComponent);
            Assert.IsFalse(TypeManager.IsManagedSharedComponent(componentType.TypeIndex));
            EntityComponentStore->AssertNonEmptyArchetypesHaveComponent(queryImpl->_QueryData->MatchingArchetypes, componentType);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.SetSharedComponent, in m_WorldUnmanaged);
#endif
            queryImpl->SyncFilterTypes();
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
            {
                // TODO: Allocating and populating this filtered chunk array is redundant work, but that's what the journaling interface requires.
                using var chunks = queryImpl->ToArchetypeChunkArray(Allocator.TempJob);
                if (Hint.Likely(chunks.Length > 0))
                {
                    JournalAddRecord_SetSharedComponent(in originSystem, in chunks, componentType.TypeIndex,
                        componentData, TypeManager.GetTypeInfo(componentType.TypeIndex).TypeSize);
                }
            }
#endif
            StructuralChange.SetSharedComponentDataIndexWithBurst(EntityComponentStore,
                queryImpl, componentType, sharedComponentIndex);
#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif

        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        public T GetSharedComponentData_Unmanaged<T>(Entity entity) where T : unmanaged, ISharedComponentData
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            UnityEngine.Assertions.Assert.IsFalse(TypeManager.IsManagedSharedComponent(typeIndex));
            EntityComponentStore->AssertEntityHasComponent(entity, typeIndex);

            var sharedComponentIndex = EntityComponentStore->GetSharedComponentDataIndex(entity, typeIndex);
            return GetSharedComponentData_Unmanaged<T>(sharedComponentIndex);
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        public T GetSharedComponentData_Unmanaged<T>(int sharedComponentIndex) where T : unmanaged
        {
            Assert.IsTrue(Entities.EntityComponentStore.IsUnmanagedSharedComponentIndex(sharedComponentIndex));

            var res = default(T);
            if (sharedComponentIndex != 0)
            {
                EntityComponentStore->GetSharedComponentData_Unmanaged(sharedComponentIndex, TypeManager.GetTypeIndex<T>(), UnsafeUtility.AddressOf(ref res));
            }

            return res;
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        public int GetSharedComponentVersion_Unmanaged<T>(T sharedData) where T : unmanaged
        {
            var ti = TypeManager.GetTypeIndex<T>();
            Assert.IsFalse(TypeManager.IsManagedSharedComponent(ti));
            var res = default(T);
            return EntityComponentStore->GetSharedComponentVersion_Unmanaged(ti, UnsafeUtility.AddressOf(ref sharedData), UnsafeUtility.AddressOf(ref res));
        }

        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public bool AllSharedComponentReferencesAreFromChunks(EntityComponentStore* entityComponentStore)
        {
            var refCounts = new UnsafeParallelHashMap<int, int>(64, Allocator.Temp);
            for (var i = 0; i < entityComponentStore->m_Archetypes.Length; ++i)
            {
                var archetype = entityComponentStore->m_Archetypes.Ptr[i];
                var chunkCount = archetype->Chunks.Count;
                for (int j = 0; j < archetype->NumSharedComponents; ++j)
                {
                    var values = archetype->Chunks.GetSharedComponentValueArrayForType(j);
                    for (var ci = 0; ci < chunkCount; ++ci)
                    {
                        var sharedComponentIndex = values[ci];
                        if (refCounts.TryGetValue(sharedComponentIndex, out var rc))
                        {
                            refCounts[sharedComponentIndex] = rc + 1;
                        }
                        else
                        {
                            refCounts.Add(sharedComponentIndex, 1);
                        }
                    }
                }
            }

            if (ManagedComponentStore.AllSharedComponentReferencesAreFromChunks(refCounts) == false)
                return false;

            return EntityComponentStore->AllSharedComponentReferencesAreFromChunks(refCounts);
        }

        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void CopySharedComponents(EntityDataAccess* srcEntityDataAccess, int* sharedComponentIndices, int sharedComponentIndicesCount)
        {
            var srcEntityComponentStore = srcEntityDataAccess->EntityComponentStore;
            var dstEntityComponentStore = EntityComponentStore;
            var srcManagedComponents = srcEntityDataAccess->ManagedComponentStore;
            var dstManagedComponents = ManagedComponentStore;

            for (var i = 0; i != sharedComponentIndicesCount; i++)
            {
                var sharedComponentIndex = sharedComponentIndices[i];
                if (sharedComponentIndex == 0)
                    continue;

                int dstIndex;
                if (Entities.EntityComponentStore.IsUnmanagedSharedComponentIndex(sharedComponentIndex))
                {
                    dstIndex = dstEntityComponentStore->CloneSharedComponentNonDefault(srcEntityComponentStore, sharedComponentIndex);
                }
                else
                {
                    dstIndex = dstManagedComponents.CloneSharedComponentNonDefault(srcManagedComponents, sharedComponentIndex);
                }

                sharedComponentIndices[i] = dstIndex;
            }
        }

        public EntityStorageInfo GetStorageInfo(Entity entity)
        {
            EntityComponentStore->AssertEntitiesExist(&entity, 1);

            var entityInChunk = EntityComponentStore->GetEntityInChunk(entity);
            return new EntityStorageInfo
            {
                Chunk = new ArchetypeChunk(entityInChunk.Chunk, EntityComponentStore),
                IndexInChunk = entityInChunk.IndexInChunk
            };
        }

        private void PlaybackManagedChangesMono()
        {
            ManagedComponentStore.Playback(ref EntityComponentStore->ManagedChangesTracker);
        }

        [BurstDiscard]
        private void PlaybackManagedDirectly(ref bool didTheThing)
        {
            didTheThing = true;
            PlaybackManagedChangesMono();
        }

        internal void PlaybackManagedChanges()
        {
            if (EntityComponentStore->ManagedChangesTracker.Empty)
                return;

            bool monoDidIt = false;
            PlaybackManagedDirectly(ref monoDidIt);
            if (monoDidIt)
                return;

            fixed (void* self = &this)
            {
                new FunctionPointer<PlaybackManagedDelegate>(s_ManagedPlaybackTrampoline.Data).Invoke((IntPtr)self);
            }
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] {typeof(BurstCompatibleBufferElement)},
            CompileTarget = GenerateTestsForBurstCompatibilityAttribute.BurstCompatibleCompileTarget.Editor)]
        public DynamicBuffer<T> GetBuffer<T>(Entity entity, AtomicSafetyHandle safety,
            AtomicSafetyHandle arrayInvalidationSafety, bool isReadOnly = false) where T : unmanaged, IBufferElementData
#else
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] {typeof(BurstCompatibleBufferElement)})]
        public DynamicBuffer<T> GetBuffer<T>(Entity entity, bool isReadOnly = false) where T : unmanaged, IBufferElementData
#endif
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            EntityComponentStore->AssertEntityHasComponent(entity, typeIndex);
            if (!TypeManager.IsBuffer(typeIndex))
            {
                var typeName = typeIndex.ToFixedString();

                // It would be difficult to hit this error since T is constrained to IBufferElementData
                throw new ArgumentException(
                    $"GetBuffer<{typeName}> may not be IComponentData or ISharedComponentData; currently {TypeManager.GetTypeInfo<T>().Category}");
            }
#endif

            if (!IsInExclusiveTransaction)
            {
                if (isReadOnly)
                    DependencyManager->CompleteWriteDependency(typeIndex);
                else
                    DependencyManager->CompleteReadAndWriteDependency(typeIndex);
            }

            BufferHeader* header;
            if (isReadOnly)
            {
                header = (BufferHeader*) EntityComponentStore->GetComponentDataWithTypeRO(entity, typeIndex);
            }
            else
            {
                header = (BufferHeader*) EntityComponentStore->GetComponentDataWithTypeRW(entity, typeIndex,
                    EntityComponentStore->GlobalSystemVersion);
            }

            int internalCapacity = TypeManager.GetTypeInfo(typeIndex).BufferCapacity;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var useMemoryInit = EntityComponentStore->useMemoryInitPattern != 0;
            byte memoryInitPattern = EntityComponentStore->memoryInitPattern;
            return new DynamicBuffer<T>(header, safety, arrayInvalidationSafety, isReadOnly, useMemoryInit,
                memoryInitPattern, internalCapacity);
#else
            return new DynamicBuffer<T>(header, internalCapacity);
#endif
        }

        public void SetBufferRaw(Entity entity, TypeIndex componentTypeIndex, BufferHeader* tempBuffer, int sizeInChunk,
            in SystemHandle originSystem = default)
        {
            if (!IsInExclusiveTransaction)
                DependencyManager->CompleteReadAndWriteDependency(componentTypeIndex);

            EntityComponentStore->AssertEntityHasComponent(entity, componentTypeIndex);

            EntityComponentStore->SetBufferRaw(entity, componentTypeIndex, tempBuffer, sizeInChunk);
        }

        public EntityArchetype GetEntityAndSimulateArchetype()
        {
            if (!m_EntityAndSimulateOnlyArchetype.Valid)
                m_EntityAndSimulateOnlyArchetype = CreateArchetype((ComponentType*) null, 0, true);

            return m_EntityAndSimulateOnlyArchetype;
        }

        /// <summary>
        /// This function must be wrapped in BeginStructuralChanges() and EndStructuralChanges(ref EntityComponentStore.ArchetypeChanges changes).
        /// </summary>
        /// <param name="srcEntity"></param>
        /// <param name="outputEntities"></param>
        /// <param name="count"></param>
        internal void InstantiateInternalDuringStructuralChange(Entity srcEntity, Entity* outputEntities, int count,
            in SystemHandle originSystem = default)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (count < 0)
                throw new ArgumentOutOfRangeException("count must be non-negative");
#endif
            EntityComponentStore->AssertEntitiesExist(&srcEntity, 1);
            EntityComponentStore->AssertCanInstantiateEntities(srcEntity, outputEntities, count);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.CreateEntity, in m_WorldUnmanaged);
#endif

            StructuralChange.InstantiateEntity(EntityComponentStore, &srcEntity, outputEntities, count);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_CreateEntity(in originSystem, outputEntities, count);
#endif
        }

        internal void InstantiateInternalDuringStructuralChange(Entity* srcEntities, Entity* outputEntities, int count,
            int outputCount, bool removePrefab, in SystemHandle originSystem = default)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (count < 0)
                throw new ArgumentOutOfRangeException("count must be non-negative");
#endif
            AssertCountsMatch(count, outputCount);
            EntityComponentStore->AssertEntitiesExist(srcEntities, count);
            EntityComponentStore->AssertCanInstantiateEntities(srcEntities, count, removePrefab);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.CreateEntity, in m_WorldUnmanaged);
#endif

            StructuralChange.InstantiateEntities(EntityComponentStore, srcEntities, outputEntities, count, removePrefab);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
                StructuralChangesRecorder.End();
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecord_CreateEntity(in originSystem, outputEntities, outputCount);
#endif
        }

        public void SwapComponents(ArchetypeChunk leftChunk, int leftIndex, ArchetypeChunk rightChunk, int rightIndex)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (leftIndex < 0 || leftIndex >= leftChunk.Count)
                throw new ArgumentOutOfRangeException(
                    $"leftIndex {leftIndex} is out of range for chunk count {leftChunk.Count}");
            if (rightIndex < 0 || rightIndex >= rightChunk.Count)
                throw new ArgumentOutOfRangeException(
                    $"rightIndex {rightIndex} is out of range for chunk count {rightChunk.Count}");
#endif
            if (!IsInExclusiveTransaction)
                BeforeStructuralChange();

            var globalSystemVersion = EntityComponentStore->GlobalSystemVersion;

            ChunkDataUtility.SwapComponents(leftChunk.Archetype.Archetype, leftChunk.m_Chunk, leftIndex, rightChunk.Archetype.Archetype, rightChunk.m_Chunk, rightIndex, 1,
                globalSystemVersion, globalSystemVersion);
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] {typeof(BurstCompatibleBufferElement)})]
        public BufferTypeHandle<T> GetBufferTypeHandle<T>(bool isReadOnly)
            where T : unmanaged, IBufferElementData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var typeIndex = TypeManager.GetTypeIndex<T>();
            return new BufferTypeHandle<T>(
                DependencyManager->Safety.GetSafetyHandleForBufferTypeHandle(typeIndex, isReadOnly),
                DependencyManager->Safety.GetBufferHandleForBufferTypeHandle(typeIndex),
                isReadOnly, EntityComponentStore->GlobalSystemVersion);
#else
            return new BufferTypeHandle<T>(isReadOnly, EntityComponentStore->GlobalSystemVersion);
#endif
        }

        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public void GetAllUniqueSharedComponents<T>(List<T> sharedComponentValues, List<int> sharedComponentIndices, List<int> sharedComponentVersions)
            where T : struct, ISharedComponentData
        {
            ManagedComponentStore.GetAllUniqueSharedComponents_Managed(sharedComponentValues, sharedComponentIndices, sharedComponentVersions);
        }

        public bool HasBlobReferences(int sharedComponentIndex)
        {
            return TypeManager.GetTypeInfo(
                    Entities.EntityComponentStore.GetComponentTypeFromSharedComponentIndex(sharedComponentIndex))
                .HasBlobAssetRefs;
        }

        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        public NativeArray<int> MoveSharedComponents(ManagedComponentStore srcManagedComponents,
            NativeArray<ArchetypeChunk> chunks, AllocatorManager.AllocatorHandle allocator)
        {
            return ManagedComponentStore.MoveSharedComponents_Managed(srcManagedComponents, chunks, allocator);
        }

        internal Entity GetEntityByEntityIndex(int index)
        {
            return EntityComponentStore->GetEntityByEntityIndex(index);
        }

        internal int GetNameIndexByEntityIndex(int index)
        {
            int nameIndex = 0;
#if !DOTS_DISABLE_DEBUG_NAMES
            var entityName = EntityComponentStore->GetEntityNameByEntityIndex(index);
            nameIndex = entityName.Index;
#endif
            return nameIndex;
        }

        /// <summary>
        /// Gets the name assigned to an entity.
        /// </summary>
        /// <remarks>For performance, entity names only exist when running in the Unity Editor.</remarks>
        /// <param name="entity">The Entity object of the entity of interest.</param>
        /// <returns>The entity name.</returns>
        [ExcludeFromBurstCompatTesting("Returns managed string")]
        public string GetName(Entity entity)
        {
            return EntityComponentStore->GetName(entity);
        }

        public void GetName(Entity entity, out FixedString64Bytes name)
        {
            EntityComponentStore->GetName(entity, out name);
        }

        /// <summary>
        /// Sets the name of an entity.
        /// </summary>
        /// <remarks>For performance, entity names only exist when running in the Unity Editor.</remarks>
        /// <param name="entity">The Entity object of the entity to name.</param>
        /// <param name="name">The name to assign.</param>
        public void SetName(Entity entity, in FixedString64Bytes name)
        {
            EntityComponentStore->SetName(entity, in name);
        }

        /// <summary>
        /// Waits for all tracked jobs to complete.
        /// </summary>
        /// <remarks>Calling <see cref="CompleteAllTrackedJobs"/> blocks the main thread until all currently running tracked Jobs finish.</remarks>
        /// <remarks>Tracked JobHandles for this world include every systems' resulting JobHandle directly after their OnUpdate</remarks>
        public void CompleteAllTrackedJobs()
        {
            DependencyManager->CompleteAllJobsAndInvalidateArrays();
        }

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_CreateEntity(in SystemHandle originSystem, Entity* entities, int entityCount, TypeIndex* types = null, int typeCount = 0)
        {
            EntitiesJournaling.AddRecord(
                recordType: EntitiesJournaling.RecordType.CreateEntity,
                worldSequenceNumber: m_WorldUnmanaged.SequenceNumber,
                executingSystem: m_WorldUnmanaged.ExecutingSystem,
                originSystem: in originSystem,
                entities: entities,
                entityCount: entityCount,
                types: types,
                typeCount: typeCount);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_CreateEntity(in SystemHandle originSystem, in NativeArray<Entity> entities, TypeIndex* types = null, int typeCount = 0) =>
            JournalAddRecord_CreateEntity(in originSystem, (Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length, types, typeCount);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_CreateEntity(in SystemHandle originSystem, ArchetypeChunk* chunks, int chunkCount, TypeIndex* types = null, int typeCount = 0)
        {
            EntitiesJournaling.AddRecord(
                recordType: EntitiesJournaling.RecordType.CreateEntity,
                worldSequenceNumber: m_WorldUnmanaged.SequenceNumber,
                executingSystem: m_WorldUnmanaged.ExecutingSystem,
                originSystem: in originSystem,
                chunks: chunks,
                chunkCount: chunkCount,
                types: types,
                typeCount: typeCount);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_CreateEntity(in SystemHandle originSystem, in NativeArray<ArchetypeChunk> chunks, TypeIndex* types = null, int typeCount = 0) =>
            JournalAddRecord_CreateEntity(in originSystem, (ArchetypeChunk*)chunks.GetUnsafeReadOnlyPtr(), chunks.Length, types, typeCount);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_DestroyEntity(in SystemHandle originSystem, Entity* entities, int entityCount)
        {
            EntitiesJournaling.AddRecord(
                recordType: EntitiesJournaling.RecordType.DestroyEntity,
                worldSequenceNumber: m_WorldUnmanaged.SequenceNumber,
                executingSystem: m_WorldUnmanaged.ExecutingSystem,
                originSystem: in originSystem,
                entities: entities,
                entityCount: entityCount);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_DestroyEntity(in SystemHandle originSystem, in NativeArray<Entity> entities) =>
            JournalAddRecord_DestroyEntity(in originSystem, (Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_DestroyEntity(in SystemHandle originSystem, ArchetypeChunk* chunks, int chunkCount)
        {
            EntitiesJournaling.AddRecord(
                recordType: EntitiesJournaling.RecordType.DestroyEntity,
                worldSequenceNumber: m_WorldUnmanaged.SequenceNumber,
                executingSystem: m_WorldUnmanaged.ExecutingSystem,
                originSystem: in originSystem,
                chunks: chunks,
                chunkCount: chunkCount);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_DestroyEntity(in SystemHandle originSystem, in NativeArray<ArchetypeChunk> chunks) =>
            JournalAddRecord_DestroyEntity(in originSystem, (ArchetypeChunk*)chunks.GetUnsafeReadOnlyPtr(), chunks.Length);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_AddComponent(in SystemHandle originSystem, Entity* entities, int entityCount, TypeIndex* types, int typeCount)
        {
            EntitiesJournaling.AddRecord(
                recordType: EntitiesJournaling.RecordType.AddComponent,
                worldSequenceNumber: m_WorldUnmanaged.SequenceNumber,
                executingSystem: m_WorldUnmanaged.ExecutingSystem,
                originSystem: in originSystem,
                entities: entities,
                entityCount: entityCount,
                types: types,
                typeCount: typeCount);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_AddComponent(in SystemHandle originSystem, in NativeArray<Entity> entities, TypeIndex* types, int typeCount) =>
            JournalAddRecord_AddComponent(in originSystem, (Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length, types, typeCount);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_AddComponent(in SystemHandle originSystem, ArchetypeChunk* chunks, int chunkCount, TypeIndex* types, int typeCount)
        {
            EntitiesJournaling.AddRecord(
                recordType: EntitiesJournaling.RecordType.AddComponent,
                worldSequenceNumber: m_WorldUnmanaged.SequenceNumber,
                executingSystem: m_WorldUnmanaged.ExecutingSystem,
                originSystem: in originSystem,
                chunks: chunks,
                chunkCount: chunkCount,
                types: types,
                typeCount: typeCount);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_AddComponent(in SystemHandle originSystem, in NativeArray<ArchetypeChunk> chunks, TypeIndex* types, int typeCount) =>
            JournalAddRecord_AddComponent(in originSystem, (ArchetypeChunk*)chunks.GetUnsafeReadOnlyPtr(), chunks.Length, types, typeCount);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_RemoveComponent(in SystemHandle originSystem, Entity* entities, int entityCount, TypeIndex* types, int typeCount)
        {
            EntitiesJournaling.AddRecord(
                recordType: EntitiesJournaling.RecordType.RemoveComponent,
                worldSequenceNumber: m_WorldUnmanaged.SequenceNumber,
                executingSystem: m_WorldUnmanaged.ExecutingSystem,
                originSystem: in originSystem,
                entities: entities,
                entityCount: entityCount,
                types: types,
                typeCount: typeCount);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_RemoveComponent(in SystemHandle originSystem, in NativeArray<Entity> entities, TypeIndex* types, int typeCount) =>
            JournalAddRecord_RemoveComponent(in originSystem, (Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length, types, typeCount);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_RemoveComponent(in SystemHandle originSystem, ArchetypeChunk* chunks, int chunkCount, TypeIndex* types, int typeCount)
        {
            EntitiesJournaling.AddRecord(
                recordType: EntitiesJournaling.RecordType.RemoveComponent,
                worldSequenceNumber: m_WorldUnmanaged.SequenceNumber,
                executingSystem: m_WorldUnmanaged.ExecutingSystem,
                originSystem: in originSystem,
                chunks: chunks,
                chunkCount: chunkCount,
                types: types,
                typeCount: typeCount);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_RemoveComponent(in SystemHandle originSystem, in NativeArray<ArchetypeChunk> chunks, TypeIndex* types, int typeCount) =>
            JournalAddRecord_RemoveComponent(in originSystem, (ArchetypeChunk*)chunks.GetUnsafeReadOnlyPtr(), chunks.Length, types, typeCount);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_SetComponentEnabled(in SystemHandle originSystem, Entity* entities, int entityCount, TypeIndex* types, int typeCount, bool value)
        {
            EntitiesJournaling.AddRecord(
                recordType: value ? EntitiesJournaling.RecordType.EnableComponent : EntitiesJournaling.RecordType.DisableComponent,
                worldSequenceNumber: m_WorldUnmanaged.SequenceNumber,
                executingSystem: m_WorldUnmanaged.ExecutingSystem,
                originSystem: in originSystem,
                entities: entities,
                entityCount: entityCount,
                types: types,
                typeCount: typeCount);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_SetComponentEnabled(in SystemHandle originSystem, in NativeArray<Entity> entities, TypeIndex* types, int typeCount, bool value) =>
            JournalAddRecord_SetComponentEnabled(in originSystem, (Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length, types, typeCount, value);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_SetComponentEnabled(in SystemHandle originSystem, ArchetypeChunk* chunks, int chunkCount, TypeIndex* types, int typeCount, bool value)
        {
            EntitiesJournaling.AddRecord(
                recordType: value ? EntitiesJournaling.RecordType.EnableComponent : EntitiesJournaling.RecordType.DisableComponent,
                worldSequenceNumber: m_WorldUnmanaged.SequenceNumber,
                executingSystem: m_WorldUnmanaged.ExecutingSystem,
                originSystem: in originSystem,
                chunks: chunks,
                chunkCount: chunkCount,
                types: types,
                typeCount: typeCount);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_SetComponentEnabled(in SystemHandle originSystem, in NativeArray<ArchetypeChunk> chunks, TypeIndex* types, int typeCount, bool value) =>
            JournalAddRecord_SetComponentEnabled(in originSystem, (ArchetypeChunk*)chunks.GetUnsafeReadOnlyPtr(), chunks.Length, types, typeCount, value);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_SetSharedComponent(in SystemHandle originSystem, Entity* entities, int entityCount, TypeIndex typeIndex, void* data, int dataLength)
        {
            EntitiesJournaling.AddRecord(
                recordType: EntitiesJournaling.RecordType.SetSharedComponentData,
                worldSequenceNumber: m_WorldUnmanaged.SequenceNumber,
                executingSystem: m_WorldUnmanaged.ExecutingSystem,
                originSystem: in originSystem,
                entities: entities,
                entityCount: entityCount,
                types: &typeIndex,
                typeCount: 1,
                data: data,
                dataLength: dataLength);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_SetSharedComponent(in SystemHandle originSystem, in NativeArray<Entity> entities, TypeIndex typeIndex, void* data, int dataLength) =>
            JournalAddRecord_SetSharedComponent(in originSystem, (Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length, typeIndex, data, dataLength);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_SetSharedComponent(in SystemHandle originSystem, ArchetypeChunk* chunks, int chunkCount, TypeIndex typeIndex, void* data, int dataLength)
        {
            EntitiesJournaling.AddRecord(
                recordType: EntitiesJournaling.RecordType.SetSharedComponentData,
                worldSequenceNumber: m_WorldUnmanaged.SequenceNumber,
                executingSystem: m_WorldUnmanaged.ExecutingSystem,
                originSystem: in originSystem,
                chunks: chunks,
                chunkCount: chunkCount,
                types: &typeIndex,
                typeCount: 1,
                data: data,
                dataLength: dataLength);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_SetSharedComponent(in SystemHandle originSystem, in NativeArray<ArchetypeChunk> chunks, TypeIndex typeIndex, void* data, int dataLength) =>
            JournalAddRecord_SetSharedComponent(in originSystem, (ArchetypeChunk*)chunks.GetUnsafeReadOnlyPtr(), chunks.Length, typeIndex, data, dataLength);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_SetSharedComponentManaged(in SystemHandle originSystem, Entity* entities, int entityCount, TypeIndex typeIndex)
        {
            EntitiesJournaling.AddRecord(
                recordType: EntitiesJournaling.RecordType.SetSharedComponentData,
                worldSequenceNumber: m_WorldUnmanaged.SequenceNumber,
                executingSystem: m_WorldUnmanaged.ExecutingSystem,
                originSystem: in originSystem,
                entities: entities,
                entityCount: entityCount,
                types: &typeIndex,
                typeCount: 1);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_SetSharedComponentManaged(in SystemHandle originSystem, in NativeArray<Entity> entities, TypeIndex typeIndex) =>
            JournalAddRecord_SetSharedComponentManaged(in originSystem, (Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length, typeIndex);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_SetSharedComponentManaged(in SystemHandle originSystem, ArchetypeChunk* chunks, int chunkCount, TypeIndex typeIndex)
        {
            EntitiesJournaling.AddRecord(
                recordType: EntitiesJournaling.RecordType.SetSharedComponentData,
                worldSequenceNumber: m_WorldUnmanaged.SequenceNumber,
                executingSystem: m_WorldUnmanaged.ExecutingSystem,
                originSystem: in originSystem,
                chunks: chunks,
                chunkCount: chunkCount,
                types: &typeIndex,
                typeCount: 1);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void JournalAddRecord_SetSharedComponentManaged(in SystemHandle originSystem, in NativeArray<ArchetypeChunk> chunks, TypeIndex typeIndex) =>
            JournalAddRecord_SetSharedComponentManaged(in originSystem, (ArchetypeChunk*)chunks.GetUnsafeReadOnlyPtr(), chunks.Length, typeIndex);
#endif
    }

    static unsafe partial class EntityDataAccessManagedComponentExtensions
    {
        internal static int* GetManagedComponentIndex(ref this EntityDataAccess dataAccess, Entity entity, TypeIndex typeIndex)
        {
            dataAccess.EntityComponentStore->AssertEntityHasComponent(entity, typeIndex);

            if (!dataAccess.IsInExclusiveTransaction)
                dataAccess.DependencyManager->CompleteReadAndWriteDependency(typeIndex);

            return (int*)dataAccess.EntityComponentStore->GetComponentDataWithTypeRW(entity, typeIndex, dataAccess.EntityComponentStore->GlobalSystemVersion);
        }

        public static T GetComponentData<T>(ref this EntityDataAccess dataAccess, Entity entity, ManagedComponentStore managedComponentStore) where T : class, IComponentData, new()
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            var index = *dataAccess.GetManagedComponentIndex(entity, typeIndex);
            return (T)managedComponentStore.GetManagedComponent(index);
        }

        public static T GetComponentObject<T>(ref this EntityDataAccess dataAccess, Entity entity, ComponentType componentType)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (!componentType.IsManagedComponent)
                throw new System.ArgumentException($"GetComponentObject must be called with a managed component type.");
#endif
            var index = *dataAccess.GetManagedComponentIndex(entity, componentType.TypeIndex);
            return (T)dataAccess.ManagedComponentStore.GetManagedComponent(index);
        }

        public static void SetComponentObject(ref this EntityDataAccess dataAccess, Entity entity, ComponentType componentType, object componentObject, in SystemHandle originSystem = default)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (!componentType.IsManagedComponent)
                throw new System.ArgumentException($"SetComponentObject must be called with a managed component type.");
            if (componentObject != null && componentObject.GetType() != TypeManager.GetType(componentType.TypeIndex))
                throw new System.ArgumentException($"SetComponentObject {componentObject.GetType()} doesn't match the specified component type: {TypeManager.GetType(componentType.TypeIndex)}");
#endif
            var ptr = dataAccess.GetManagedComponentIndex(entity, componentType.TypeIndex);
            dataAccess.ManagedComponentStore.UpdateManagedComponentValue(ptr, componentObject, ref *dataAccess.EntityComponentStore);
        }

        public static void MoveComponentObjectDuringStructuralChange(ref this EntityDataAccess dataAccess, Entity srcEntity,
            Entity dstEntity, ComponentType componentType, in SystemHandle originSystem = default)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(!componentType.IsManagedComponent))
                throw new ArgumentException($"{componentType} must be a managed component type.");
#endif
            if (srcEntity == dstEntity)
                return;
            // Get the src pointer first, so we fail if it's not present before adding T to dst
            int* srcPtr = dataAccess.GetManagedComponentIndex(srcEntity, componentType.TypeIndex);
            // We need to add T to dst before getting the pointer to its value index
            if (!dataAccess.HasComponent(dstEntity, componentType))
                dataAccess.AddComponentDuringStructuralChange(dstEntity, componentType, originSystem);

            // If dst's T value is non-null (value index != 0), and it's a different object than src, and it's IDisposable,
            // we'd need to dispose the old value.
            int* dstPtr = dataAccess.GetManagedComponentIndex(dstEntity, componentType.TypeIndex);
            if (*dstPtr != 0)
            {
                object srcValue = dataAccess.ManagedComponentStore.GetManagedComponent(*srcPtr);
                object dstValue = dataAccess.ManagedComponentStore.GetManagedComponent(*dstPtr);
                if (!ReferenceEquals(srcValue, dstValue))
                {
                    // If srcValue and dstValue are references to different objects, we need to dispose dstValue. This can
                    // use the same code path as SetComponentObject<T>(dstEntity, srcValue).
                    dataAccess.ManagedComponentStore.UpdateManagedComponentValue(dstPtr, null,
                        ref *dataAccess.EntityComponentStore);
                }
                else if (*srcPtr != *dstPtr)
                {
                    // src and dst refer to the same object with different managed component indices. We need to
                    // release *dstPtr back into the pool of unused value indices, but not dispose of the value object.
                    dataAccess.ManagedComponentStore.SetManagedComponentValue(*dstPtr, null);
                    dataAccess.EntityComponentStore->FreeManagedComponentIndex(*dstPtr);
                }
            }
            *dstPtr = *srcPtr;
            // Force the src entity's managed component index to zero before removing the component, to ensure that
            // removing doesn't attempt to dispose the value.
            *srcPtr = 0;
            dataAccess.RemoveComponentDuringStructuralChange(srcEntity, componentType, originSystem);
        }
    }
}
