using System;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// Common interface for the per-entity data storage.
    /// </summary>
    /// <typeparam name="T">The data type stored.</typeparam>
    interface IEntityMap<T> : IDisposable where T : unmanaged
    {
        /// <summary>
        /// Returns the upper bound for the array.
        /// </summary>
        int Capacity { get; }

        /// <summary>
        /// Returns the number of entries in the storage.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Gets the data for the specified <see cref="Entity"/>.
        /// </summary>
        /// <param name="entity">The entity to get data for.</param>
        T this[Entity entity] { get; }

        /// <summary>
        /// Clears the storage for re-use.
        /// </summary>
        void Clear();

        /// <summary>
        /// Resizes to sparse data set to the given capacity.
        /// </summary>
        /// <param name="capacity">The capacity to set.</param>
        void Resize(int capacity);

        /// <summary>
        /// Returns <see langword="true"/> if the specified entity exists in the storage.
        /// </summary>
        /// <param name="entity">The entity to check existence for.</param>
        /// <returns><see langword="true"/> if the entity exists in the storage; <see langword="false"/> otherwise.</returns>
        bool Exists(Entity entity);

        /// <summary>
        /// Removes the data for the specified entity.
        /// </summary>
        /// <param name="entity">The entity to remove data for.</param>
        void Remove(Entity entity);
    }

    /// <summary>
    /// Represents a key-value pair with the key being an entity.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    struct EntityWithValue<TValue>
    {
        public Entity Entity;
        public TValue Value;
    }
}
