using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Unity.Entities
{
    public unsafe partial struct EntityManager
    {
        // ----------------------------------------------------------------------------------------------------------
        // PUBLIC
        // ----------------------------------------------------------------------------------------------------------

        /// <summary>
        /// An optional <see cref="JobHandle"/> that corresponds to the job currently using an ExclusiveEntityTransaction.
        /// This job is completed when this EntityManager's <see cref="World"/> is destroyed.
        /// </summary>
        public JobHandle ExclusiveEntityTransactionDependency
        {
            get
            {
                // Note this can't use read/write checking
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
#endif
                return m_EntityDataAccess->DependencyManager->ExclusiveTransactionDependency;
            }
            set
            {
                // Note this can't use read/write checking
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
#endif
                m_EntityDataAccess->DependencyManager->ExclusiveTransactionDependency = value;
            }
        }

        /// <summary>
        /// Check whether or not a new exclusive entity transaction can begin.
        /// </summary>
        /// <returns><see langword="true"/> if a new exclusive transaction can begin, <see langword="false"/> otherwise.</returns>
        public bool CanBeginExclusiveEntityTransaction()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (IsInExclusiveTransaction)
                return false;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
#endif

            var access = GetUncheckedEntityDataAccess();
            if (access->DependencyManager->IsInTransaction)
                return false;

            return true;
        }

        /// <summary>
        /// Begins an exclusive entity transaction, which allows you to make structural changes inside a Job.
        /// </summary>
        /// <remarks>
        /// <see cref="ExclusiveEntityTransaction"/> allows you to create and destroy entities from a job. The purpose is
        /// to enable procedural generation scenarios where instantiation on big scale must happen on jobs. As the
        /// name implies it is exclusive to any other access to the EntityManager.
        ///
        /// An exclusive entity transaction should be used on a manually created <see cref="World"/> that acts as a
        /// staging area to construct and setup entities.
        ///
        /// After the job has completed you can end the transaction and use
        /// <see cref="MoveEntitiesFrom(EntityManager)"/> to move the entities to an active <see cref="World"/>.
        /// </remarks>
        /// <returns>A transaction object that provides an functions for making structural changes.</returns>
        public ExclusiveEntityTransaction BeginExclusiveEntityTransaction()
        {
            var access = GetCheckedEntityDataAccess();

        #if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (IsInExclusiveTransaction)
                throw new InvalidOperationException("An exclusive transaction is already in process.");
            if (access->DependencyManager->IsInTransaction)
                throw new InvalidOperationException("An exclusive transaction is already in process.");
        #endif

            access->DependencyManager->BeginExclusiveTransaction();

            var copy = this;

        #if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            copy.m_IsInExclusiveTransaction = 1;
        #endif
            return new ExclusiveEntityTransaction(copy);
        }

        /// <summary>
        /// Ends an active <see cref="ExclusiveEntityTransaction"/> and returns ownership of the
        /// <see cref="EntityManager"/> to the main thread.
        /// </summary>
        /// <seealso cref="ExclusiveEntityTransaction"/>
        /// <seealso cref="BeginExclusiveEntityTransaction()"/>
        public void EndExclusiveEntityTransaction()
        {
        #if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (m_IsInExclusiveTransaction == 1)
                throw new InvalidOperationException("Transactions can only be ended from the main thread");
        #endif

        #if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
        #endif

            m_EntityDataAccess->DependencyManager->PreEndExclusiveTransaction();
        #if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
        #endif
            m_EntityDataAccess->DependencyManager->EndExclusiveTransaction();
        }

        // ----------------------------------------------------------------------------------------------------------
        // INTERNAL
        // ----------------------------------------------------------------------------------------------------------

#if ENTITY_STORE_V1
        internal void AllocateConsecutiveEntitiesForLoading(int count)
        {
            EntityComponentStore* s = GetCheckedEntityDataAccess()->EntityComponentStore;

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (s->CountEntities() != 0)
                throw new ArgumentException("loading into non-empty entity manager is not supported");
#endif
            s->AllocateConsecutiveEntitiesForLoading(count);
        }
#endif

        [ExcludeFromBurstCompatTesting("Accesses managed component store")]
        internal void AddSharedComponentManaged<T>(NativeArray<ArchetypeChunk> chunks, T componentData)
            where T : struct, ISharedComponentData
        {
#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
            {
                m_EntityDataAccess->StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.AddComponent, in m_EntityDataAccess->m_WorldUnmanaged);
                m_EntityDataAccess->StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.SetSharedComponent, in m_EntityDataAccess->m_WorldUnmanaged);
            }
#endif

            var componentType = ComponentType.ReadWrite<T>();
            var archetypeChanges = m_EntityDataAccess->BeginStructuralChanges();
            int sharedComponentIndex = m_EntityDataAccess->InsertSharedComponent(componentData);
            m_EntityDataAccess->AddSharedComponentDataDuringStructuralChange(chunks, sharedComponentIndex, componentType);
            m_EntityDataAccess->EndStructuralChanges(ref archetypeChanges);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
            {
                m_EntityDataAccess->StructuralChangesRecorder.End(); // SetSharedComponent
                m_EntityDataAccess->StructuralChangesRecorder.End(); // AddComponent
            }
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(m_EntityDataAccess->EntityComponentStore->m_RecordToJournal != 0))
            {
                var typeIndex = componentType.TypeIndex;
                m_EntityDataAccess->JournalAddRecord_AddComponent(default, in chunks, &typeIndex, 1);
                m_EntityDataAccess->JournalAddRecord_SetSharedComponentManaged(default, in chunks, typeIndex);
            }
#endif
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleSharedComponentData) })]
        internal void AddSharedComponent<T>(NativeArray<ArchetypeChunk> chunks, T componentData)
            where T : unmanaged, ISharedComponentData
        {
#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
            {
                m_EntityDataAccess->StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.AddComponent, in m_EntityDataAccess->m_WorldUnmanaged);
                m_EntityDataAccess->StructuralChangesRecorder.Begin(StructuralChangesProfiler.StructuralChangeType.SetSharedComponent, in m_EntityDataAccess->m_WorldUnmanaged);
            }
#endif

            var componentType = ComponentType.ReadWrite<T>();
            var archetypeChanges = m_EntityDataAccess->BeginStructuralChanges();
            int sharedComponentIndex = m_EntityDataAccess->InsertSharedComponent_Unmanaged(componentData);
            m_EntityDataAccess->AddSharedComponentDataDuringStructuralChange(chunks, sharedComponentIndex, componentType);
            m_EntityDataAccess->EndStructuralChanges(ref archetypeChanges);

#if ENABLE_PROFILER
            if (StructuralChangesProfiler.Enabled)
            {
                m_EntityDataAccess->StructuralChangesRecorder.End(); // SetSharedComponent
                m_EntityDataAccess->StructuralChangesRecorder.End(); // AddComponent
            }
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Burst.CompilerServices.Hint.Unlikely(m_EntityDataAccess->EntityComponentStore->m_RecordToJournal != 0))
            {
                var typeIndex = componentType.TypeIndex;
                m_EntityDataAccess->JournalAddRecord_AddComponent(default, in chunks, &typeIndex, 1);
                m_EntityDataAccess->JournalAddRecord_SetSharedComponent(default, in chunks, typeIndex, &componentData, TypeManager.GetTypeInfo(typeIndex).TypeSize);
            }
#endif
        }
    }
}
