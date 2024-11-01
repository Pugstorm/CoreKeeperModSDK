using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// The <see cref="HierarchyNodeMap{T}"/> can be used to store data per hierarchy node.
    /// </summary>
    /// <remarks>
    /// !!IMPORTANT!! This has an 8 byte memory overhead for EACH entity in the world. Use with caution.
    /// 
    /// This structure uses an internal entity lookup to access entity nodes in O(1). All other node types use hash map access.
    /// 
    /// This structure has a fixed memory overhead of 8 bytes per entity plus any node data stored.
    /// </remarks>
    /// <typeparam name="T">The hierarchy node data type.</typeparam>
    [GenerateTestsForBurstCompatibility]
    unsafe struct HierarchyNodeMap<T> : IDisposable where T : unmanaged
    {
        /// <summary>
        /// The internal unmanaged data.
        /// </summary>
        struct HierarchyNodeMapData
        {
            public int ValueByHandleCount;
        }

        /// <summary>
        /// The allocator used to construct this instance.
        /// </summary>
        readonly Allocator m_Allocator;

        /// <summary>
        /// Instance data for this structure.
        /// </summary>
        [NativeDisableUnsafePtrRestriction] HierarchyNodeMapData* m_HierarchyNodeMapData;

        /// <summary>
        /// Storage for <see cref="Entity"/> based nodes. This is a linear lookup indexed by <see cref="Entity.Index"/>.
        /// </summary>
        EntityMapDense<T> m_ValueByEntity;

        /// <summary>
        /// Storage for all other node types. This is a hash based lookup based on the <see cref="HierarchyNodeHandle"/>.
        /// </summary>
        UnsafeParallelHashMap<HierarchyNodeHandle, T> m_ValueByHandle;

        /// <summary>
        /// Returns the internal entity data storage.
        /// </summary>
        internal EntityMapDense<T> ValueByEntity => m_ValueByEntity;

        /// <summary>
        /// Returns the internal entity data storage.
        /// </summary>
        internal UnsafeParallelHashMap<HierarchyNodeHandle, T> ValueByHandle => m_ValueByHandle;

        /// <summary>
        /// Returns the number of hashed handle nodes.
        /// </summary>
        internal int ValueByHandleCount => m_HierarchyNodeMapData->ValueByHandleCount;

        /// <summary>
        /// Gets the data for the specified handle.
        /// </summary>
        /// <param name="handle">The handle to get data for.</param>
        public T this[HierarchyNodeHandle handle]
        {
            get
            {
                switch (handle.Kind)
                {
                    case NodeKind.Entity:
                        return m_ValueByEntity[handle.ToEntity()];
                    default:
                        return m_ValueByHandle[handle];
                }
            }
            // The setter has been intentionally omitted. Use Add or Update instead.
        }

        /// <summary>
        /// Adds the data for the specified handle.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void Add(HierarchyNodeHandle handle, T value)
        {
            switch (handle.Kind)
            {
                case NodeKind.Entity:
                {
                    m_ValueByEntity.Add(handle.ToEntity(), value);
                }
                break;

                default:
                {
                    if (!UnsafeParallelHashMapBase<HierarchyNodeHandle, T>.TryAdd(m_ValueByHandle.m_Buffer, handle, value, false, m_Allocator))
                        throw new InvalidOperationException("The key already exists in the map.");

                    m_HierarchyNodeMapData->ValueByHandleCount++;
                }
                break;
            }
        }

        /// <summary>
        /// Adds the data for the specified handle if it does not exist.
        /// </summary>
        /// <returns><see langword="true"/> if the handle value pair was added, <see langword="false"/> otherwise.</returns>
        public bool TryAdd(HierarchyNodeHandle handle, T value)
        {
            switch (handle.Kind)
            {
                case NodeKind.Entity:
                {
                    return m_ValueByEntity.TryAdd(handle.ToEntity(), value);
                }

                default:
                {
                    if (!UnsafeParallelHashMapBase<HierarchyNodeHandle, T>.TryAdd(m_ValueByHandle.m_Buffer, handle, value, false, m_Allocator))
                        return false;

                    m_HierarchyNodeMapData->ValueByHandleCount++;
                    return true;
                }
            }
        }

        /// <summary>
        /// Updates the data for the specified handle.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void Update(HierarchyNodeHandle handle, T value)
        {
            switch (handle.Kind)
            {
                case NodeKind.Entity:
                {
                    m_ValueByEntity.Update(handle.ToEntity(), value);
                }
                break;

                default:
                {
                    if (!UnsafeParallelHashMapBase<HierarchyNodeHandle, T>.TryGetFirstValueAtomic(m_ValueByHandle.m_Buffer, handle, out _, out var iterator))
                        throw new InvalidOperationException("The key does not exist in the map.");

                    UnsafeParallelHashMapBase<HierarchyNodeHandle, T>.SetValue(m_ValueByHandle.m_Buffer, ref iterator, ref value);
                }
                break;
            }
        }

        /// <summary>
        /// Updates the data for the specified handle if it exists.
        /// </summary>
        /// <returns><see langword="true"/> if the handle value pair was updated, <see langword="false"/> otherwise.</returns>
        public bool TryUpdate(HierarchyNodeHandle handle, T value)
        {
            switch (handle.Kind)
            {
                case NodeKind.Entity:
                {
                    return m_ValueByEntity.TryUpdate(handle.ToEntity(), value);
                }

                default:
                {
                    if (!UnsafeParallelHashMapBase<HierarchyNodeHandle, T>.TryGetFirstValueAtomic(m_ValueByHandle.m_Buffer, handle, out _, out var iterator))
                        return false;

                    UnsafeParallelHashMapBase<HierarchyNodeHandle, T>.SetValue(m_ValueByHandle.m_Buffer, ref iterator, ref value);
                    return true;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance <see cref="HierarchyNodeMap{T}"/>.
        /// </summary>
        /// <param name="allocator">The allocator to use.</param>
        public HierarchyNodeMap(Allocator allocator)
        {
            m_Allocator = allocator;
            m_ValueByEntity = new EntityMapDense<T>(16, allocator);
            m_ValueByHandle = new UnsafeParallelHashMap<HierarchyNodeHandle, T>(16, allocator) {{HierarchyNodeHandle.Root, default}};
            m_HierarchyNodeMapData = (HierarchyNodeMapData*) Memory.Unmanaged.Allocate(UnsafeUtility.SizeOf<HierarchyNodeMapData>(), UnsafeUtility.AlignOf<HierarchyNodeMapData>(), allocator);
            m_HierarchyNodeMapData->ValueByHandleCount = 1;
        }

        /// <summary>
        /// Disposes this instance.
        /// </summary>
        public void Dispose()
        {
            m_ValueByEntity.Dispose();
            m_ValueByHandle.Dispose();
            Memory.Unmanaged.Free(m_HierarchyNodeMapData, m_Allocator);
            m_HierarchyNodeMapData = null;
        }

        /// <summary>
        /// Returns the number of valid nodes in the storage.
        /// </summary>
        /// <returns>The number of valid nodes.</returns>
        public int Count()
        {
            return m_ValueByEntity.Count + m_HierarchyNodeMapData->ValueByHandleCount;
        }

        /// <summary>
        /// Clears all data from the storage.
        /// </summary>
        public void Clear()
        {
            m_ValueByEntity.Clear();
            m_ValueByHandle.Clear();
            m_ValueByHandle.Add(HierarchyNodeHandle.Root, default);
            m_HierarchyNodeMapData->ValueByHandleCount = 1;
        }

        /// <summary>
        /// Returns <see langword="true"/> if the given handle exists in the storage.
        /// </summary>
        /// <param name="handle">The handle to check existence for.</param>
        /// <returns><see langword="true"/> if the given handle exists; <see langword="false"/> otherwise.</returns>
        public bool Exists(HierarchyNodeHandle handle)
        {
            switch (handle.Kind)
            {
                case NodeKind.Entity:
                {
                    return m_ValueByEntity.Exists(handle.ToEntity());
                }

                default:
                {
                    return m_ValueByHandle.ContainsKey(handle);
                }
            }
        }

        /// <summary>
        /// Removes the specified handle from the storage.
        /// </summary>
        /// <param name="handle">The handle to remove.</param>
        public void Remove(HierarchyNodeHandle handle)
        {
            switch (handle.Kind)
            {
                case NodeKind.Entity:
                {
                    m_ValueByEntity.Remove(handle.ToEntity());
                }
                break;

                default:
                {
                    if (m_ValueByHandle.Remove(handle))
                        m_HierarchyNodeMapData->ValueByHandleCount--;
                }
                break;
            }
        }

        /// <summary>
        /// Resizes to sparse entity data set to the given capacity.
        /// </summary>
        /// <param name="capacity">The capacity to set.</param>
        public void ResizeEntityCapacity(int capacity)
            => m_ValueByEntity.Resize(capacity);

        public void SetSharedDefault(T value)
            => m_ValueByEntity.SetSharedDefaultValue(value);
    }
}
