using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// The <see cref="EntityMapSparse{T}"/> can be used to store data mapped by entity in a sparse way.
    /// </summary>
    /// <remarks>
    /// This structure stores values indexed by <see cref="Entity.Index"/>. It offers efficient read/write at the cost of memory.
    /// 
    /// This structure is best used if:
    ///     - the size of <typeparamref name="T"/> is smaller than ~8 bytes
    ///     - the data must exist on MANY entity in a world
    ///
    /// otherwise; consider using <seealso cref="EntityMapDense{T}"/>
    /// 
    /// This data structure has a fixed memory overhead of sizeof(T) + 4 bytes per entity.
    /// </remarks>
    /// <typeparam name="T">The data type to store per entity.</typeparam>
    [NativeContainer]
    unsafe struct EntityMapSparse<T> : IEntityMap<T> where T : unmanaged
    {
        /// <summary>
        /// The allocator used to construct this instance.
        /// </summary>
        readonly Allocator m_Allocator;

        /// <summary>
        /// The internal unsafe implementation.
        /// </summary>
        [NativeDisableUnsafePtrRestriction] UnsafeEntityMapSparse<T>* m_EntityMapSparseData;

        /// <summary>
        /// Returns the upper bound of the sparse array.
        /// </summary>
        public int Capacity => m_EntityMapSparseData->Capacity;

        /// <summary>
        /// Returns the number of entries in the storage.
        /// </summary>
        public int Count => m_EntityMapSparseData->Count;

        /// <summary>
        /// Initializes a new instance of <see cref="EntityMapSparse{T}"/>.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity to allocate.</param>
        /// <param name="allocator">The allocator type.</param>
        public EntityMapSparse(int initialCapacity, Allocator allocator)
        {
            m_Allocator = allocator;
            var handle = (AllocatorManager.AllocatorHandle) allocator;
            m_EntityMapSparseData = AllocatorManager.Allocate<UnsafeEntityMapSparse<T>>(handle);
            *m_EntityMapSparseData = new UnsafeEntityMapSparse<T>(initialCapacity, allocator);
        }

        /// <summary>
        /// Disposes this instance.
        /// </summary>
        public void Dispose()
        {
            if (m_EntityMapSparseData == null)
                throw new Exception("UnsafeList has yet to be created or has been destroyed!");

            m_EntityMapSparseData->Dispose();
            AllocatorManager.Free(m_Allocator, m_EntityMapSparseData);
            m_EntityMapSparseData = null;
        }

        /// <summary>
        /// Gets or sets the data for the specified <see cref="Entity"/>.
        /// </summary>
        /// <param name="entity">The entity to get or set data for.</param>
        public T this[Entity entity]
        {
            get => m_EntityMapSparseData->GetValue(entity);
            set => m_EntityMapSparseData->SetValue(entity, value);
        }
        
        /// <summary>
        /// Clears the storage for re-use.
        /// </summary>
        public void Clear()
            => m_EntityMapSparseData->Clear();

        /// <summary>
        /// Resizes to sparse data set to the given capacity.
        /// </summary>
        /// <param name="capacity">The capacity to set.</param>
        public void Resize(int capacity)
            => m_EntityMapSparseData->Resize(capacity);

        /// <summary>
        /// Returns <see langword="true"/> if the specified entity exists in the storage.
        /// </summary>
        /// <param name="entity">The entity to check existence for.</param>
        /// <returns><see langword="true"/> if the entity exists in the storage; <see langword="false"/> otherwise.</returns>
        public bool Exists(Entity entity)
            => m_EntityMapSparseData->Exists(entity);

        /// <summary>
        /// Removes the data for the specified entity.
        /// </summary>
        /// <param name="entity">The entity to remove data for.</param>
        public void Remove(Entity entity)
            => m_EntityMapSparseData->Remove(entity);
    }
    
    /// <summary>
    /// The internal storage for the <see cref="EntityMapSparse{T}"/>.
    /// </summary>
    struct UnsafeEntityMapSparse<T> : IDisposable where T : unmanaged
    {
        /// <summary>
        /// Collection of values indexed by <see cref="Entity.Index"/>.
        /// </summary>
        UnsafeList<T> m_ValueByEntity;
        
        /// <summary>
        /// Collection of version indexed by <see cref="Entity.Index"/>. This is used to track destroyed entities.
        /// </summary>
        UnsafeList<int> m_VersionByEntity;

        /// <summary>
        /// The actual number of entries in the storage.
        /// </summary>
        int m_Count;
        
        /// <summary>
        /// Returns the upper bound of the sparse array.
        /// </summary>
        public int Capacity => m_ValueByEntity.Capacity;

        /// <summary>
        /// Returns the number of entries in the storage.
        /// </summary>
        public int Count => m_Count;
        
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
        /// Initializes a new instance of <see cref="UnsafeEntityMapSparse{T}"/>.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity to allocate.</param>
        /// <param name="allocator">The allocator type.</param>
        public UnsafeEntityMapSparse(int initialCapacity, Allocator allocator)
        {
            m_ValueByEntity = new UnsafeList<T>(initialCapacity, allocator);
            m_VersionByEntity = new UnsafeList<int>(initialCapacity, allocator);
            m_Count = 0;
        }

        /// <summary>
        /// Disposes this instance.
        /// </summary>
        public void Dispose()
        {
            m_ValueByEntity.Dispose();
            m_VersionByEntity.Dispose();
        }

        /// <summary>
        /// Clears the storage for re-use.
        /// </summary>
        public void Clear()
        {
            m_ValueByEntity.Clear();
            m_VersionByEntity.Clear();
            m_Count = 0;
        }

        /// <summary>
        /// Resizes to sparse data set to the given capacity.
        /// </summary>
        /// <param name="capacity">The capacity to set.</param>
        public void Resize(int capacity)
        {
            m_ValueByEntity.Resize(capacity, NativeArrayOptions.ClearMemory);
            m_VersionByEntity.Resize(capacity, NativeArrayOptions.ClearMemory);
        }

        /// <summary>
        /// Returns <see langword="true"/> if the specified entity exists in the storage.
        /// </summary>
        /// <param name="entity">The entity to check existence for.</param>
        /// <returns><see langword="true"/> if the entity exists in the storage; <see langword="false"/> otherwise.</returns>
        public bool Exists(Entity entity)
        {
            if (m_VersionByEntity.Length <= entity.Index)
                return false;

            return m_VersionByEntity[entity.Index] == entity.Version;
        }

        /// <summary>
        /// Removes the data for the specified entity.
        /// </summary>
        /// <param name="entity">The entity to remove data for.</param>
        public void Remove(Entity entity)
        {
            if (m_VersionByEntity.Length <= entity.Index)
                return;

            if (m_VersionByEntity[entity.Index] != 0)
                m_Count--;

            m_VersionByEntity[entity.Index] = 0;
        }
        
        /// <summary>
        /// Gets the value for the specified entity.
        /// </summary>
        /// <param name="entity">The entity to get the data for.</param>
        /// <returns>The data for the entity.</returns>
        public T GetValue(Entity entity)
        {
            return m_ValueByEntity[entity.Index];
        }

        /// <summary>
        /// Gets the value for the specified entity index.
        /// </summary>
        /// <param name="index">The entity index.</param>
        /// <returns>The value for the given index.</returns>
        internal T GetValueUnchecked(int index)
        {
            return m_ValueByEntity[index];
        }

        /// <summary>
        /// Sets the value for the specified entity.
        /// </summary>
        /// <param name="entity">The entity to set the data for.</param>
        /// <param name="value">The data to set.</param>
        public void SetValue(Entity entity, T value)
        {
            if (m_ValueByEntity.Length <= entity.Index)
                Resize(entity.Index + 1);

            if (m_VersionByEntity[entity.Index] == 0)
                m_Count++;

            m_ValueByEntity[entity.Index] = value;
            m_VersionByEntity[entity.Index] = entity.Version;
        }

        public Enumerator GetEnumerator()
            => new Enumerator(m_ValueByEntity, m_VersionByEntity);
        
        /// <summary>
        /// An enumerator which will iterate key-value pairs in the map.
        /// </summary>
        public struct Enumerator : IEnumerator<EntityWithValue<T>>
        {
            UnsafeList<T> m_ValueByEntity;
            UnsafeList<int> m_VersionByEntity;

            int m_Index;

            public Enumerator(UnsafeList<T> valueByEntity, UnsafeList<int> versionByEntity)
            {
                m_ValueByEntity = valueByEntity;
                m_VersionByEntity = versionByEntity;
                m_Index = -1;
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                while (++m_Index < m_VersionByEntity.Length && m_VersionByEntity[m_Index] == 0)
                {
                }

                return m_Index < m_ValueByEntity.Length;
            }

            public void Reset()
            {
                m_Index = -1;
            }

            public EntityWithValue<T> Current => new EntityWithValue<T> {Entity = new Entity {Index = m_Index, Version = m_VersionByEntity[m_Index]}, Value = m_ValueByEntity[m_Index]};

            object IEnumerator.Current => Current;
        }
    }
}