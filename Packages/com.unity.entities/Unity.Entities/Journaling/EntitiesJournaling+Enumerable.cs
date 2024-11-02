#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;

namespace Unity.Entities
{
    partial class EntitiesJournaling
    {
        /// <summary>
        /// Get the records matching a record index.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="name">The record index.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithRecordIndex(this IEnumerable<RecordView> records, ulong index) =>
            records.Where(r => r.Index == index);

        /// <summary>
        /// Get all records matching a record type.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="name">The record type.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithRecordType(this IEnumerable<RecordView> records, RecordType type) =>
            records.Where(r => r.RecordType == type);

        /// <summary>
        /// Get all records matching a frame index.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="name">The frame index.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithFrameIndex(this IEnumerable<RecordView> records, int index) =>
            records.Where(r => r.FrameIndex == index);

        /// <summary>
        /// Get all records matching a world name.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="name">The world name.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithWorld(this IEnumerable<RecordView> records, string name) =>
            records.Where(r => r.World.Name.Contains(name));

        /// <summary>
        /// Get all records matching a world sequence number.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="name">The world sequence number.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithWorld(this IEnumerable<RecordView> records, ulong sequenceNumber) =>
            records.Where(r => r.World.SequenceNumber == sequenceNumber);

        /// <summary>
        /// Get all records matching an existing world.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="name">The world.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithWorld(this IEnumerable<RecordView> records, World world) =>
            records.Where(r => r.World.Reference == world);

        /// <summary>
        /// Get all records matching a system type name.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="name">The system type name.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithSystem(this IEnumerable<RecordView> records, string name) =>
            records.WithExecutingSystem(name).Concat(records.WithOriginSystem(name));

        /// <summary>
        /// Get all records matching a system handle untyped.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="name">The system handle untyped.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithSystem(this IEnumerable<RecordView> records, SystemHandle handle) =>
            records.WithExecutingSystem(handle).Concat(records.WithOriginSystem(handle));

        /// <summary>
        /// Get all records matching an executing system type name.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="name">The system type name.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithExecutingSystem(this IEnumerable<RecordView> records, string name) =>
            records.Where(r => r.ExecutingSystem.Name.Contains(name));

        /// <summary>
        /// Get all records matching an executing system handle untyped.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="name">The system handle untyped.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithExecutingSystem(this IEnumerable<RecordView> records, SystemHandle handle) =>
            records.Where(r => r.ExecutingSystem.Handle == handle);

        /// <summary>
        /// Get all records matching an origin system type name.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="name">The system type name.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithOriginSystem(this IEnumerable<RecordView> records, string name) =>
            records.Where(r => r.OriginSystem.Name.Contains(name));

        /// <summary>
        /// Get all records matching an origin system handle untyped.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="name">The system handle untyped.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithOriginSystem(this IEnumerable<RecordView> records, SystemHandle handle) =>
            records.Where(r => r.OriginSystem.Handle == handle);

        /// <summary>
        /// Get all records matching a component type name.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="name">The component type name.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithComponentType(this IEnumerable<RecordView> records, string name) =>
            records.Where(r => r.ComponentTypes.Any(t => t.ToString().Contains(name)));

        /// <summary>
        /// Get all records matching a component type.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="name">The component type.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithComponentType(this IEnumerable<RecordView> records, ComponentType componentType) =>
            records.Where(r => r.ComponentTypes.Any(t => t.TypeIndex == componentType.TypeIndex));

        /// <summary>
        /// Get all records matching a component type index.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="typeIndex">The component type index.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithComponentType(this IEnumerable<RecordView> records, TypeIndex typeIndex) =>
            records.Where(r => r.ComponentTypes.Any(t => t.TypeIndex == typeIndex));

        /// <summary>
        /// Get all records matching an entity index.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="index">The entity index.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithEntity(this IEnumerable<RecordView> records, int index) =>
            records.Where(r => r.Entities.Any(e => e.Index == index));

        /// <summary>
        /// Get all records matching an entity index and version.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="index">The entity index.</param>
        /// <param name="version">The entity version.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithEntity(this IEnumerable<RecordView> records, int index, int version) =>
            records.Where(r => r.Entities.Any(e => e.Index == index && e.Version == version));

        /// <summary>
        /// Get all records matching an entity.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="entity">The entity.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithEntity(this IEnumerable<RecordView> records, Entity entity) =>
            records.Where(r => r.Entities.Any(e => e.Index == entity.Index && e.Version == entity.Version));

        /// <summary>
        /// Get all records matching an existing entity name.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
        /// </remarks>
        /// <param name="name">The entity name.</param>
        /// <returns>IEnumerable of <see cref="RecordView"/>.</returns>
        [ExcludeFromBurstCompatTesting("LINQ")]
        public static IEnumerable<RecordView> WithEntity(this IEnumerable<RecordView> records, string name) =>
            records.Where(r => r.Entities.Any(e => e.ToString().Contains(name)));
    }
}
#endif
