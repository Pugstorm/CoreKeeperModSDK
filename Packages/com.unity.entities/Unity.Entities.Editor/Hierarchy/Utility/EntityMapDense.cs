using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// The <see cref="EntityMapDense{T}"/> can be used to store data mapped by entity in a dense way.
    /// </summary>
    /// <remarks>
    /// This structure uses a sparse array mapping to an internal dense array. This uses a fixed size overhead to save on per-entity storage costs.
    ///
    /// This structure is best used if:
    ///     - the size of <typeparamref name="T"/> is larger than ~8 bytes
    ///     - the data must exist on FEW entity in a world
    ///
    /// otherwise; consider using <seealso cref="EntityMapSparse{T}"/>
    ///
    /// This data structure has a fixed memory overhead of 8 bytes per entity and will expand as data is added.
    /// </remarks>
    /// <typeparam name="T">The data type to store per entity.</typeparam>
    unsafe struct EntityMapDense<T> : IEntityMap<T> where T : unmanaged
    {
        /// <summary>
        /// The allocator used to construct this instance.
        /// </summary>
        readonly Allocator m_Allocator;

        /// <summary>
        /// The internal unsafe implementation.
        /// </summary>
        [NativeDisableUnsafePtrRestriction] UnsafeEntityMapDense<T>* m_EntityMapDenseData;

        /// <summary>
        /// Returns the upper bound of the sparse array.
        /// </summary>
        public int Capacity => m_EntityMapDenseData->Capacity;

        /// <summary>
        /// Returns the number of entries in the storage.
        /// </summary>
        public int Count => m_EntityMapDenseData->Count;

        /// <summary>
        /// Returns the total number of unique entries, not including shared instances.
        /// </summary>
        public int CountNonSharedDefault => m_EntityMapDenseData->CountNonSharedDefault;

        /// <summary>
        /// Initializes a new instance of <see cref="EntityMapSparse{T}"/>.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity to allocate.</param>
        /// <param name="allocator">The allocator type.</param>
        public EntityMapDense(int initialCapacity, Allocator allocator)
        {
            m_Allocator = allocator;
            var handle = (AllocatorManager.AllocatorHandle) allocator;
            m_EntityMapDenseData = AllocatorManager.Allocate<UnsafeEntityMapDense<T>>(handle);
            *m_EntityMapDenseData = new UnsafeEntityMapDense<T>(initialCapacity, allocator);
        }

        /// <summary>
        /// Disposes this instance.
        /// </summary>
        public void Dispose()
        {
            if (m_EntityMapDenseData == null)
                throw new Exception("UnsafeList has yet to be created or has been destroyed!");

            m_EntityMapDenseData->Dispose();
            AllocatorManager.Free(m_Allocator, m_EntityMapDenseData);
            m_EntityMapDenseData = null;
        }

        /// <summary>
        /// Gets the data for the specified <see cref="Entity"/>.
        /// </summary>
        /// <param name="entity">The entity to get or set data for.</param>
        public T this[Entity entity]
        {
            get => m_EntityMapDenseData->GetValue(entity);
            // The setter has been intentionally omitted. Use Add or Update instead.
        }

        /// <inheritdoc cref="IEntityMap{T}.Add"/>
        public void Add(Entity entity, T value)
        {
            if (m_EntityMapDenseData->Exists(entity))
                throw new InvalidOperationException("The key already exists in the map.");

            m_EntityMapDenseData->SetValue(entity, value);
        }

        /// <inheritdoc cref="IEntityMap{T}.TryAdd"/>
        public bool TryAdd(Entity entity, T value)
        {
            if (m_EntityMapDenseData->Exists(entity))
                return false;

            m_EntityMapDenseData->SetValue(entity, value);
            return true;
        }

        /// <inheritdoc cref="IEntityMap{T}.Update"/>
        public void Update(Entity entity, T value)
        {
            if (!m_EntityMapDenseData->Exists(entity))
                throw new InvalidOperationException("The key does not exist in the map.");

            m_EntityMapDenseData->SetValue(entity, value);
        }

        /// <inheritdoc cref="IEntityMap{T}.TryUpdate"/>
        public bool TryUpdate(Entity entity, T value)
        {
            if (!m_EntityMapDenseData->Exists(entity))
                return false;

            m_EntityMapDenseData->SetValue(entity, value);
            return true;
        }

        /// <summary>
        /// Sets the shared default value.
        /// </summary>
        /// <remarks>
        /// If any entities are assigned this value they will consume no additional memory. Only one default can be set.
        ///
        /// NOTE: If this value is changed all entities which are assigned this value are also updated.
        /// </remarks>
        /// <param name="value">The shared default value to set.</param>
        public void SetSharedDefaultValue(T value)
            => m_EntityMapDenseData->SetSharedDefaultValue(value);

        /// <summary>
        /// Gets the shared default value.
        /// </summary>
        public T GetSharedDefaultValue()
            => m_EntityMapDenseData->GetSharedDefaultValue();

        internal void GetDefaultEntities(Entity* entities)
            => m_EntityMapDenseData->GetDefaultEntities(entities);

        /// <summary>
        /// Clears the storage for re-use.
        /// </summary>
        public void Clear()
            => m_EntityMapDenseData->Clear();

        /// <summary>
        /// Resizes to sparse data set to the given capacity.
        /// </summary>
        /// <param name="capacity">The capacity to set.</param>
        public void Resize(int capacity)
            => m_EntityMapDenseData->Resize(capacity);

        /// <summary>
        /// Returns <see langword="true"/> if the specified entity exists in the storage.
        /// </summary>
        /// <param name="entity">The entity to check existence for.</param>
        /// <returns><see langword="true"/> if the entity exists in the storage; <see langword="false"/> otherwise.</returns>
        public bool Exists(Entity entity)
            => m_EntityMapDenseData->Exists(entity);

        /// <summary>
        /// Removes the data for the specified entity.
        /// </summary>
        /// <param name="entity">The entity to remove data for.</param>
        public void Remove(Entity entity)
            => m_EntityMapDenseData->Remove(entity);

        /// <summary>
        /// Registers an entity to the dense set and assigns it the default value. This method performs no validation for free list checking. Use with caution.
        /// </summary>
        /// <param name="entity">The entity to assign the default value to.</param>
        public void SetValueDefaultUnchecked(Entity entity)
            => m_EntityMapDenseData->SetValueDefaultUnchecked(entity);

        public Enumerator GetEnumerator()
            => new Enumerator(m_EntityMapDenseData->GetEnumerator());

        public NonDefaultEntityEnumerator GetNonDefaultEntityEnumerator()
            => new NonDefaultEntityEnumerator(m_EntityMapDenseData->GetNonDefaultEntityEnumerator());

        public struct Enumerator : IEnumerator<EntityWithValue<T>>
        {
            UnsafeEntityMapDense<T>.Enumerator m_Enumerator;

            public Enumerator(UnsafeEntityMapDense<T>.Enumerator enumerator)
            {
                m_Enumerator = enumerator;
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
                => m_Enumerator.MoveNext();

            public void Reset()
                => m_Enumerator.Reset();

            public EntityWithValue<T> Current
                => m_Enumerator.Current;

            object IEnumerator.Current
                => Current;
        }

        public struct NonDefaultEntityEnumerator : IEnumerator<EntityWithValue<T>>
        {
            UnsafeEntityMapDense<T>.NonDefaultEntityEnumerator m_Enumerator;

            public NonDefaultEntityEnumerator(UnsafeEntityMapDense<T>.NonDefaultEntityEnumerator enumerator)
            {
                m_Enumerator = enumerator;
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
                => m_Enumerator.MoveNext();

            public void Reset()
                => m_Enumerator.Reset();

            public EntityWithValue<T> Current
                => m_Enumerator.Current;

            object IEnumerator.Current
                => Current;
        }
    }

    /// <summary>
    /// The internal storage for the <see cref="EntityMapDense{T}"/>.
    /// </summary>
    unsafe struct UnsafeEntityMapDense<T> : IDisposable where T : unmanaged
    {
#if ENTITY_STORE_V1
        /// <summary>
        /// Sparse indexing in to the dense data set.
        /// </summary>
        UnsafeEntityMapSparse<int> m_IndexByEntity;
#else
        UnsafeHashMap<Entity, int> m_IndexByEntity;
#endif

        /// <summary>
        /// A set of free indices in the dense data set.
        /// </summary>
        UnsafeList<int> m_FreeIndex;

        /// <summary>
        /// The densely packed data array.
        /// </summary>
        UnsafeList<T> m_DataByIndex;

        /// <summary>
        /// Returns the upper bound of the sparse array.
        /// </summary>
        public int Capacity => m_IndexByEntity.Capacity;

        /// <summary>
        /// Returns the number of entries in the storage.
        /// </summary>
        public int Count => m_IndexByEntity.Count;

        /// <summary>
        /// Returns the total number of unique entries, not including shared instances.
        /// </summary>
        /// <returns></returns>
        public int CountNonSharedDefault => m_DataByIndex.Length - m_FreeIndex.Length - 1;

        /// <summary>
        /// Gets or sets the data for the specified <see cref="Entity"/>.
        /// </summary>
        /// <param name="entity">The entity to get or set data for.</param>
        public T this[Entity entity]
        {
            get => GetValue(entity);
            set => SetValue(entity, value);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="UnsafeEntityMapDense{T}"/>.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity to allocate.</param>
        /// <param name="allocator">The allocator type.</param>
        public UnsafeEntityMapDense(int initialCapacity, Allocator allocator)
        {
            m_IndexByEntity = new(initialCapacity, allocator);
            m_FreeIndex = new UnsafeList<int>(initialCapacity, allocator);
            m_DataByIndex = new UnsafeList<T>(initialCapacity, allocator) {default};
        }

        /// <summary>
        /// Disposes this instance.
        /// </summary>
        public void Dispose()
        {
            m_IndexByEntity.Dispose();
            m_FreeIndex.Dispose();
            m_DataByIndex.Dispose();
        }

        /// <summary>
        /// Sets the shared default value.
        /// </summary>
        /// <remarks>
        /// If any entities are assigned this value they will consume no additional memory. Only one default can be set.
        ///
        /// NOTE: If this value is changed all entities which are assigned this value are also updated.
        /// </remarks>
        /// <param name="value">The shared default value to set.</param>
        public void SetSharedDefaultValue(T value)
        {
            m_DataByIndex[0] = value;
        }

        /// <summary>
        /// Gets the shared default value.
        /// </summary>
        public T GetSharedDefaultValue()
        {
            return m_DataByIndex[0];
        }

        /// <summary>
        /// Gets all entities which are assigned to the shared default value.
        /// </summary>
        /// <param name="entities"></param>
        internal void GetDefaultEntities(Entity* entities)
        {
            var index = 0;

            foreach (var entityValuePair in m_IndexByEntity)
            {
                if (entityValuePair.Value == 0)
#if ENTITY_STORE_V1
                    entities[index++] = entityValuePair.Entity;
#else
                    entities[index++] = entityValuePair.Key;
#endif
            }
        }

        /// <summary>
        /// Clears the storage for re-use.
        /// </summary>
        public void Clear()
        {
            m_IndexByEntity.Clear();
            m_FreeIndex.Clear();
            m_DataByIndex.Length = 1;
        }

        /// <summary>
        /// Resizes to sparse data set to the given capacity.
        /// </summary>
        /// <param name="capacity">The capacity to set.</param>
        public void Resize(int capacity)
        {
#if ENTITY_STORE_V1
            m_IndexByEntity.Resize(capacity);
#endif
        }

        /// <summary>
        /// Returns true if the given entity exists in the sparse data set.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public bool Exists(Entity entity)
        {
#if ENTITY_STORE_V1
            return m_IndexByEntity.Exists(entity);
#else
            return m_IndexByEntity.ContainsKey(entity);
#endif
        }

        /// <summary>
        /// Removes the data for the specified entity.
        /// </summary>
        /// <param name="entity">The entity to remove data for.</param>
        public void Remove(Entity entity)
        {
#if ENTITY_STORE_V1
            if (m_IndexByEntity.Capacity <= entity.Index)
                return;
#endif

            if (m_IndexByEntity[entity] != 0)
                m_FreeIndex.Add(m_IndexByEntity[entity]);

            m_IndexByEntity.Remove(entity);
        }

        /// <summary>
        /// Gets the value for the specified entity.
        /// </summary>
        /// <param name="entity">The entity to get the data for.</param>
        /// <returns>The data for the entity.</returns>
        public T GetValue(Entity entity)
        {
            return m_DataByIndex[m_IndexByEntity[entity]];
        }

        /// <summary>
        /// Sets the value for the specified entity.
        /// </summary>
        /// <param name="entity">The entity to set the data for.</param>
        /// <param name="value">The data to set.</param>
        public void SetValue(Entity entity, T value)
        {
            if (Capacity <= entity.Index)
            {
                Resize(entity.Index + 1);
            }

            if (Exists(entity)) // This entity already exists in the sparse set.
            {
                if (m_IndexByEntity[entity] == 0) // This entity is currently pointing to the shared 0 index.
                {
                    if (!IsSharedDefault(value)) // It now has it's own unique value. Allocate and assign it an entry in the dense set.
                    {
                        SetValueForDenseIndex(GetNextIndex(), entity, value);
                    }
                }
                else // This entity is currently pointing to it's own unique value.
                {
                    if (IsSharedDefault(value)) // It it's now being assigned back to the shared default.
                    {
                        m_FreeIndex.Add(m_IndexByEntity[entity]);
                        m_IndexByEntity[entity] = 0;
                    }
                    else // otherwise; just update the value.
                    {
                        m_DataByIndex[m_IndexByEntity[entity]] = value;
                    }
                }
            }
            else // This entity does not exist in the sparse set yet.
            {
                if (IsSharedDefault(value)) // We are assigning as a shared default.
                {
                    m_IndexByEntity[entity] = 0;
                }
                else // We are assigning a unique entry.
                {
                    SetValueForDenseIndex(GetNextIndex(), entity, value);
                }
            }
        }

        /// <summary>
        /// Registers an entity to the dense set and assigns it the default value. This method performs no validation for free list checking. Use with caution.
        /// </summary>
        /// <param name="entity">The entity to assign the default value to.</param>
        public void SetValueDefaultUnchecked(Entity entity)
            => m_IndexByEntity[entity] = 0;

        /// <summary>
        /// Returns true if the given value matches the shared default.
        /// </summary>
        /// <param name="value">The value to compare.</param>
        /// <returns>True if the values match.</returns>
        bool IsSharedDefault(T value)
            => UnsafeUtility.MemCmp(&value, m_DataByIndex.Ptr, UnsafeUtility.SizeOf<T>()) == 0;

        /// <summary>
        /// Gets the next index, either from the free list or the next available data slot.
        /// </summary>
        /// <returns>The newly generated index.</returns>
        int GetNextIndex()
        {
            var index = 0;

            if (m_FreeIndex.Length > 0)
            {
                index = m_FreeIndex[m_FreeIndex.Length - 1];
                m_FreeIndex.RemoveAt(m_FreeIndex.Length - 1);
            }
            else
            {
                index = m_DataByIndex.Length;
            }

            return index;
        }

        /// <summary>
        /// Assigns the data to the given dense index.
        /// </summary>
        /// <param name="index">The dense index.</param>
        /// <param name="entity">The entity the data is assigned to.</param>
        /// <param name="value">The data to set.</param>
        void SetValueForDenseIndex(int index, Entity entity, T value)
        {
            if (index >= m_DataByIndex.Length)
                m_DataByIndex.Resize(index + 1, NativeArrayOptions.UninitializedMemory);

            m_IndexByEntity[entity] = index;
            m_DataByIndex[index] = value;
        }

        public Enumerator GetEnumerator()
            => new Enumerator(m_IndexByEntity.GetEnumerator(), m_DataByIndex);

        public NonDefaultEntityEnumerator GetNonDefaultEntityEnumerator()
            => new NonDefaultEntityEnumerator(m_IndexByEntity.GetEnumerator(), m_DataByIndex);

        /// <summary>
        /// An enumerator which will iterate all key-value pairs in the map.
        /// </summary>
        public struct Enumerator : IEnumerator<EntityWithValue<T>>
        {
            UnsafeList<T> m_Data;

#if ENTITY_STORE_V1
            UnsafeEntityMapSparse<int>.Enumerator m_Enumerator;

            public Enumerator(UnsafeEntityMapSparse<int>.Enumerator enumerator, UnsafeList<T> data)
            {
                m_Enumerator = enumerator;
                m_Data = data;
            }
#else
            UnsafeHashMap<Entity, int>.Enumerator m_Enumerator;

            public Enumerator(UnsafeHashMap<Entity, int>.Enumerator enumerator, UnsafeList<T> data)
            {
                m_Enumerator = enumerator;
                m_Data = data;
            }
#endif

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                return m_Enumerator.MoveNext();
            }

            public void Reset()
            {
                m_Enumerator.Reset();
            }

            public EntityWithValue<T> Current
            {
                get
                {
                    var entityWithIndex = m_Enumerator.Current;

                    return new EntityWithValue<T>
                    {
#if ENTITY_STORE_V1
                        Entity = entityWithIndex.Entity,
#else
                        Entity = entityWithIndex.Key,
#endif
                        Value = m_Data[entityWithIndex.Value]
                    };
                }
            }

            object IEnumerator.Current => Current;
        }

        /// <summary>
        /// An enumerator which will iterate all non-default key-value pairs in the map.
        /// </summary>
        public struct NonDefaultEntityEnumerator : IEnumerator<EntityWithValue<T>>
        {
            UnsafeList<T> m_DataByIndex;

#if ENTITY_STORE_V1
            UnsafeEntityMapSparse<int>.Enumerator m_Enumerator;

            public NonDefaultEntityEnumerator(UnsafeEntityMapSparse<int>.Enumerator enumerator, UnsafeList<T> dataByIndex)
            {
                m_Enumerator = enumerator;
                m_DataByIndex = dataByIndex;
            }
#else
            UnsafeHashMap<Entity, int>.Enumerator m_Enumerator;

            public NonDefaultEntityEnumerator(UnsafeHashMap<Entity, int>.Enumerator enumerator, UnsafeList<T> dataByIndex)
            {
                m_Enumerator = enumerator;
                m_DataByIndex = dataByIndex;
            }
#endif

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                do
                {
                    if (!m_Enumerator.MoveNext())
                    {
                        return false;
                    }
                }
                while (m_Enumerator.Current.Value == 0);

                return true;
            }

            public void Reset()
            {
                m_Enumerator.Reset();
            }

            public EntityWithValue<T> Current
            {
                get
                {
                    var entityWithIndex = m_Enumerator.Current;

                    return new EntityWithValue<T>
                    {
#if ENTITY_STORE_V1
                        Entity = entityWithIndex.Entity,
#else
                        Entity = entityWithIndex.Key,
#endif
                        Value = m_DataByIndex[entityWithIndex.Value]
                    };
                }
            }

            object IEnumerator.Current => Current;
        }
    }
}
