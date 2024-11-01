#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    partial class EntitiesJournaling
    {
        /// <summary>
        /// Try to retrieve record data as a <see cref="SystemView"/>.
        /// </summary>
        /// <remarks>
        /// Record type must be <see cref="RecordType.SystemAdded"/> or <see cref="RecordType.SystemRemoved"/>.
        /// </remarks>
        /// <param name="record">The record view.</param>
        /// <param name="systemView">The system view.</param>
        /// <returns><see langword="true"/> if successful, <see langword="false"/> otherwise.</returns>
        public static unsafe bool TryGetRecordDataAsSystemView(RecordView record, out SystemView systemView)
        {
            if ((record.RecordType != RecordType.SystemAdded && record.RecordType != RecordType.SystemRemoved) ||
                record.DataPtr == null || record.DataLength != UnsafeUtility.SizeOf<SystemHandle>())
            {
                systemView = default;
                return false;
            }

            systemView = new SystemView((SystemHandle*)record.DataPtr);
            return true;
        }

        /// <summary>
        /// Try to retrieve record data as component data array boxed.
        /// </summary>
        /// <remarks>
        /// Record type must be either <see cref="RecordType.GetComponentDataRW"/>,
        /// <see cref="RecordType.SetComponentData"/> or <see cref="RecordType.SetSharedComponentData"/>.
        /// </remarks>
        /// <param name="record">The record view.</param>
        /// <param name="componentDataArray">The component data array boxed.</param>
        /// <returns><see langword="true"/> if successful, <see langword="false"/> otherwise.</returns>
        [ExcludeFromBurstCompatTesting("Returns managed object")]
        public static unsafe bool TryGetRecordDataAsComponentDataArrayBoxed(RecordView record, out Array componentDataArray)
        {
            if ((record.RecordType != RecordType.GetComponentDataRW &&
                record.RecordType != RecordType.SetComponentData &&
                record.RecordType != RecordType.SetSharedComponentData) ||
                record.ComponentTypes.Length != 1 ||
                record.DataPtr == null || record.DataLength == 0)
            {
                componentDataArray = null;
                return false;
            }

            if (s_RecordDataMap.TryGetValue(record, out var data))
            {
                componentDataArray = (Array)data;
                return true;
            }

            var typeIndex = record.ComponentTypes[0].TypeIndex;
            if (IsManagedComponent(typeIndex))
            {
                componentDataArray = null;
                return false;
            }

            var type = TypeManager.GetType(typeIndex);
            if (type == null)
            {
                componentDataArray = null;
                return false;
            }

            var typeInfo = TypeManager.GetTypeInfo(typeIndex);
            if (record.DataLength % typeInfo.TypeSize != 0)
            {
                componentDataArray = null;
                return false;
            }

            componentDataArray = Array.CreateInstance(type, record.DataLength / typeInfo.TypeSize);
            if (componentDataArray == null)
                return false;

            var handle = GCHandle.Alloc(componentDataArray, GCHandleType.Pinned);
            var addr = handle.AddrOfPinnedObject();
            UnsafeUtility.MemCpy(addr.ToPointer(), record.DataPtr, record.DataLength);
            handle.Free();

            s_RecordDataMap.Add(record, componentDataArray);
            return true;
        }

        /// <summary>
        /// Non-blocking utility methods to retrieve records.
        /// </summary>
        public static class Records
        {
            /// <summary>
            /// Get all records currently in buffer.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("Returns managed array")]
            public static RecordView[] All => TryGetRecords(Ordering.Descending).ToArray();

            /// <summary>
            /// Get a number records starting from index.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="index">The start index.</param>
            /// <param name="count">The count of records.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] Range(int index, int count) => TryGetRecords(Ordering.Descending).Skip(index).Take(count).ToArray();

            /// <summary>
            /// Get the record matching a record index.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="name">The record index.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithRecordIndex(ulong index) => TryGetRecords(Ordering.Descending).WithRecordIndex(index).ToArray();

            /// <summary>
            /// Get all records matching a record type.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="name">The record type.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithRecordType(RecordType type) => TryGetRecords(Ordering.Descending).WithRecordType(type).ToArray();

            /// <summary>
            /// Get all records matching a frame index.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="name">The frame index.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithFrameIndex(int index) => TryGetRecords(Ordering.Descending).WithFrameIndex(index).ToArray();

            /// <summary>
            /// Get all records matching a world name.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="name">The world name.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithWorld(string name) => TryGetRecords(Ordering.Descending).WithWorld(name).ToArray();

            /// <summary>
            /// Get all records matching a world sequence number.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="name">The world sequence number.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithWorld(ulong sequenceNumber) => TryGetRecords(Ordering.Descending).WithWorld(sequenceNumber).ToArray();

            /// <summary>
            /// Get all records matching an existing world.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="name">The world.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithWorld(World world) => TryGetRecords(Ordering.Descending).WithWorld(world).ToArray();

            /// <summary>
            /// Get all records matching a system type name.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="name">The system type name.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithSystem(string name) => TryGetRecords(Ordering.Descending).WithSystem(name).ToArray();

            /// <summary>
            /// Get all records matching a system handle untyped.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="name">The system handle untyped.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithSystem(SystemHandle handle) => TryGetRecords(Ordering.Descending).WithSystem(handle).ToArray();

            /// <summary>
            /// Get all records matching an executing system type name.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="name">The system type name.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithExecutingSystem(string name) => TryGetRecords(Ordering.Descending).WithExecutingSystem(name).ToArray();

            /// <summary>
            /// Get all records matching an executing system handle untyped.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="name">The system handle untyped.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithExecutingSystem(SystemHandle handle) => TryGetRecords(Ordering.Descending).WithExecutingSystem(handle).ToArray();

            /// <summary>
            /// Get all records matching an origin system type name.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="name">The system type name.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithOriginSystem(string name) => TryGetRecords(Ordering.Descending).WithOriginSystem(name).ToArray();

            /// <summary>
            /// Get all records matching an origin system handle untyped.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="name">The system handle untyped.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithOriginSystem(SystemHandle handle) => TryGetRecords(Ordering.Descending).WithOriginSystem(handle).ToArray();

            /// <summary>
            /// Get all records matching a component type name.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="name">The component type name.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithComponentType(string name) => TryGetRecords(Ordering.Descending).WithComponentType(name).ToArray();

            /// <summary>
            /// Get all records matching a component type.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="typeIndex">The component type.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithComponentType(ComponentType componentType) => TryGetRecords(Ordering.Descending).WithComponentType(componentType).ToArray();

            /// <summary>
            /// Get all records matching a component type index.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="typeIndex">The component type index.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithComponentType(TypeIndex typeIndex) => TryGetRecords(Ordering.Descending).WithComponentType(typeIndex).ToArray();

            /// <summary>
            /// Get all records matching an entity index.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="index">The entity index.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithEntity(int index) => TryGetRecords(Ordering.Descending).WithEntity(index).ToArray();

            /// <summary>
            /// Get all records matching an entity index and version.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="index">The entity index.</param>
            /// <param name="version">The entity version.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithEntity(int index, int version) => TryGetRecords(Ordering.Descending).WithEntity(index, version).ToArray();

            /// <summary>
            /// Get all records matching an entity.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="entity">The entity.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithEntity(Entity entity) => TryGetRecords(Ordering.Descending).WithEntity(entity).ToArray();

            /// <summary>
            /// Get all records matching an existing entity name.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="InvalidOperationException"/> if records are currently locked for write.
            /// </remarks>
            /// <param name="name">The entity name.</param>
            /// <returns>Array of <see cref="RecordView"/>.</returns>
            [ExcludeFromBurstCompatTesting("LINQ")]
            public static RecordView[] WithEntity(string name) => TryGetRecords(Ordering.Descending).WithEntity(name).ToArray();
        }

        static bool IsManagedComponent(TypeIndex typeIndex)
        {
            return TypeManager.IsSharedComponentType(typeIndex) ? TypeManager.IsManagedSharedComponent(typeIndex) : TypeManager.IsManagedComponent(typeIndex);
        }
    }
}
#endif
