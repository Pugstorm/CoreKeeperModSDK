using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Entities
{
    /// <summary>
    /// A block of unmanaged memory containing the components for entities sharing the same
    /// <see cref="Archetype"/>.
    /// </summary>
    [DebuggerTypeProxy(typeof(ArchetypeChunkDebugView))]
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public unsafe struct ArchetypeChunk : IEquatable<ArchetypeChunk>
    {
        [FieldOffset(0)]
        [NativeDisableUnsafePtrRestriction] internal ChunkIndex m_Chunk;
        [FieldOffset(8)]
        [NativeDisableUnsafePtrRestriction] internal EntityComponentStore* m_EntityComponentStore;

        /// <summary>
        /// Returns the number of entities in the chunk.
        /// </summary>
        public readonly int Count => m_Chunk.Count;

        /// <summary>
        /// The number of entities currently stored in the chunk.
        /// </summary>
        [Obsolete("This property is always equal to Count, and will be removed in a future release.")]
        public readonly int ChunkEntityCount => m_Chunk.Count;

        /// <summary>
        /// The number of entities that can fit in this chunk.
        /// </summary>
        /// <remarks>The capacity of a chunk depends on the size of the components making up the
        /// <see cref="Archetype"/> of the entities stored in the chunk.</remarks>
        public readonly int Capacity => m_EntityComponentStore->GetArchetype(m_Chunk)->ChunkCapacity;
        /// <summary>
        /// Whether this chunk is exactly full.
        /// </summary>
        public readonly bool Full => Count == Capacity;

        internal ArchetypeChunk(ChunkIndex chunk, EntityComponentStore* entityComponentStore)
        {
            m_Chunk = chunk;
            m_EntityComponentStore = entityComponentStore;
        }

        /// <summary>
        /// Two ArchetypeChunk instances are equal if they reference the same block of chunk and entity component store memory.
        /// </summary>
        /// <param name="lhs">An ArchetypeChunk</param>
        /// <param name="rhs">Another ArchetypeChunk</param>
        /// <returns>True, if both ArchetypeChunk instances reference the same memory, or both contain null memory
        /// references.</returns>
        public static bool operator==(ArchetypeChunk lhs, ArchetypeChunk rhs)
        {
            return lhs.m_Chunk == rhs.m_Chunk && lhs.m_EntityComponentStore == rhs.m_EntityComponentStore;
        }

        /// <summary>
        /// Two ArchetypeChunk instances are only equal if they reference the same block of chunk and entity component store memory.
        /// </summary>
        /// <param name="lhs">An ArchetypeChunk</param>
        /// <param name="rhs">Another ArchetypeChunk</param>
        /// <returns>True, if the ArchetypeChunk instances reference different blocks of memory.</returns>
        public static bool operator!=(ArchetypeChunk lhs, ArchetypeChunk rhs)
        {
            return lhs.m_Chunk != rhs.m_Chunk || lhs.m_EntityComponentStore != rhs.m_EntityComponentStore;
        }

        /// <summary>
        /// Two ArchetypeChunk instances are equal if they reference the same block of chunk memory.
        /// </summary>
        /// <param name="compare">An object</param>
        /// <returns>True if <paramref name="compare"/> is an `ArchetypeChunk` instance that references the same memory,
        /// or both contain null memory references; otherwise false.</returns>
        public override bool Equals(object compare)
        {
            return this == (ArchetypeChunk)compare;
        }

        /// <summary>
        /// Computes a hashcode to support hash-based collections.
        /// </summary>
        /// <returns>The computed hash.</returns>
        public readonly override int GetHashCode() => m_Chunk;

        /// <summary>
        /// The archetype of the entities stored in this chunk.
        /// </summary>
        /// <remarks>All entities in a chunk must have the same <see cref="Archetype"/>.</remarks>
        public readonly EntityArchetype Archetype =>
            new EntityArchetype()
            {
                Archetype = m_EntityComponentStore->GetArchetype(m_Chunk)
            };

        /// <summary>
        /// SequenceNumber is a unique number for each chunk, across all worlds.
        /// </summary>
        public ulong SequenceNumber => m_Chunk.SequenceNumber;

        /// <summary>
        /// A special "null" ArchetypeChunk that you can use to test whether ArchetypeChunk instances are valid.
        /// </summary>
        /// <remarks>An ArchetypeChunk struct that refers to a chunk of memory that has been freed will be equal to
        /// this "null" ArchetypeChunk instance.</remarks>
        public static ArchetypeChunk Null => new ArchetypeChunk();

        /// <summary>
        /// Two ArchetypeChunk instances are equal if they reference the same block of chunk and entity component store memory.
        /// </summary>
        /// <param name="archetypeChunk">Another ArchetypeChunk instance</param>
        /// <returns>True, if both ArchetypeChunk instances reference the same memory or both contain null memory
        /// references.</returns>
        public bool Equals(ArchetypeChunk archetypeChunk)
        {
            return m_Chunk == archetypeChunk.m_Chunk && m_EntityComponentStore == archetypeChunk.m_EntityComponentStore;
        }

        /// <summary>
        /// The number of shared components in the archetype associated with this chunk.
        /// </summary>
        /// <returns>The shared component count.</returns>
        public readonly int NumSharedComponents()
        {
            return m_EntityComponentStore->GetArchetype(m_Chunk)->NumSharedComponents;
        }

        /// <summary>
        /// Reports whether this ArchetypeChunk instance is invalid.
        /// </summary>
        /// <returns>True, if no <see cref="Archetype"/> is associated with the this ArchetypeChunk
        /// instance.</returns>
        public readonly bool Invalid()
        {
            return m_EntityComponentStore->GetArchetype(m_Chunk) == null;
        }

        /// <summary>
        /// Provides a native array interface to entity instances stored in this chunk.
        /// </summary>
        /// <remarks>The native array returned by this method references existing data, not a copy.</remarks>
        /// <param name="entityTypeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetEntityTypeHandle"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.</param>
        /// <returns>A native array containing the entities in the chunk.</returns>
        public readonly NativeArray<Entity> GetNativeArray(EntityTypeHandle entityTypeHandle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(entityTypeHandle.m_Safety);
#endif
            var archetype = m_EntityComponentStore->GetArchetype(m_Chunk);
            var buffer = m_Chunk.Buffer;
            var length = Count;
            var startOffset = archetype->Offsets[0];
            var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Entity>(buffer + startOffset, length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, entityTypeHandle.m_Safety);
#endif
            return result;
        }

        /// <summary>
        /// Reports whether the data in any of IComponentData components in the chunk, of the type identified by
        /// <paramref name="typeHandle"/>, could have changed since the specified version.
        /// </summary>
        /// <remarks>
        /// When you access a component in a chunk with write privileges, the ECS framework updates the change version
        /// of that component type to the current <see cref="EntityManager.GlobalSystemVersion"/> value. Since every
        /// system stores the global system version in its <see cref="ComponentSystemBase.LastSystemVersion"/> field
        /// when it updates, you can compare these two versions with this function in order to determine whether
        /// the data of components in this chunk could have changed since the last time that system ran.
        ///
        /// Note that for efficiency, the change version applies to whole chunks not individual entities. The change
        /// version is updated even when another job or system that has declared write access to a component does
        /// not actually change the component value.</remarks>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetComponentTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.
        /// </param>
        /// <param name="version">The version to compare. In a system, this parameter should be set to the
        /// current <see cref="ComponentSystemBase.LastSystemVersion"/> at the time the job is run or
        /// scheduled.</param>
        /// <typeparam name="T">The component type.</typeparam>
        /// <returns>True, if the version number stored in the chunk for this component is more recent than the version
        /// passed to the <paramref name="version"/> parameter.</returns>
        public readonly bool DidChange<T>(ref ComponentTypeHandle<T> typeHandle, uint version) where T : IComponentData
        {
            return ChangeVersionUtility.DidChange(GetChangeVersion(ref typeHandle), version);
        }
        /// <summary>
        /// Obsolete. Use <see cref="DidChange{T}(ref Unity.Entities.ComponentTypeHandle{T},uint)"/> instead.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetComponentTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.
        /// </param>
        /// <param name="version">The version to compare. In a system, this parameter should be set to the
        /// current <see cref="ComponentSystemBase.LastSystemVersion"/> at the time the job is run or
        /// scheduled.</param>
        /// <typeparam name="T">The component type.</typeparam>
        /// <returns>True, if the version number stored in the chunk for this component is more recent than the version
        /// passed to the <paramref name="version"/> parameter.</returns>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly bool DidChange<T>(ComponentTypeHandle<T> typeHandle, uint version) where T : IComponentData
        {
            return ChangeVersionUtility.DidChange(GetChangeVersion(ref typeHandle), version);
        }

        /// <summary>
        /// Reports whether the data in any of IComponentData components in the chunk, of the type identified by
        /// <paramref name="typeHandle"/>, could have changed since the specified version.
        /// </summary>
        /// <remarks>
        /// When you access a component in a chunk with write privileges, the ECS framework updates the change version
        /// of that component type to the current <see cref="EntityManager.GlobalSystemVersion"/> value. Since every
        /// system stores the global system version in its <see cref="ComponentSystemBase.LastSystemVersion"/> field
        /// when it updates, you can compare these two versions with this function in order to determine whether
        /// the data of components in this chunk could have changed since the last time that system ran.
        ///
        /// Note that for efficiency, the change version applies to whole chunks not individual entities. The change
        /// version is updated even when another job or system that has declared write access to a component does
        /// not actually change the component value.</remarks>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetDynamicComponentTypeHandle"/>. Pass the object to a job
        /// using a public field you define as part of the job struct.
        /// </param>
        /// <param name="version">The version to compare. In a system, this parameter should be set to the
        /// current <see cref="ComponentSystemBase.LastSystemVersion"/> at the time the job is run or
        /// scheduled.</param>
        /// <returns>True, if the version number stored in the chunk for this component is more recent than the version
        /// passed to the <paramref name="version"/> parameter.</returns>
        public readonly bool DidChange(ref DynamicComponentTypeHandle typeHandle, uint version)
        {
            return ChangeVersionUtility.DidChange(GetChangeVersion(ref typeHandle), version);
        }
        /// <summary>
        /// Obsolete. Use <see cref="DidChange(ref Unity.Entities.DynamicComponentTypeHandle,uint)"/> instead.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetDynamicComponentTypeHandle"/>. Pass the object to a job
        /// using a public field you define as part of the job struct.
        /// </param>
        /// <param name="version">The version to compare. In a system, this parameter should be set to the
        /// current <see cref="ComponentSystemBase.LastSystemVersion"/> at the time the job is run or
        /// scheduled.</param>
        /// <returns>True, if the version number stored in the chunk for this component is more recent than the version
        /// passed to the <paramref name="version"/> parameter.</returns>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly bool DidChange(DynamicComponentTypeHandle typeHandle, uint version)
        {
            return ChangeVersionUtility.DidChange(GetChangeVersion(ref typeHandle), version);
        }

        /// <summary>
        /// Reports whether any of the data in dynamic buffer components in the chunk, of the type identified by
        /// <paramref name="bufferTypeHandle"/>, could have changed since the specified version.
        /// </summary>
        /// <remarks>
        /// When you access a component in a chunk with write privileges, the ECS framework updates the change version
        /// of that component type to the current <see cref="EntityManager.GlobalSystemVersion"/> value. Since every
        /// system stores the global system version in its <see cref="ComponentSystemBase.LastSystemVersion"/> field
        /// when it updates, you can compare these two versions with this function in order to determine whether
        /// the data of components in this chunk could have changed since the last time that system ran.
        ///
        /// Note that for efficiency, the change version applies to whole chunks not individual entities. The change
        /// version is updated even when another job or system that has declared write access to a component does
        /// not actually change the component value.</remarks>
        /// <param name="bufferTypeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetBufferTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.</param>
        /// <param name="version">The version to compare. In a system, this parameter should be set to the
        /// current <see cref="ComponentSystemBase.LastSystemVersion"/> at the time the job is run or
        /// scheduled.</param>
        /// <typeparam name="T">The data type of the elements in the dynamic buffer.</typeparam>
        /// <returns>True, if the version number stored in the chunk for this component is more recent than the version
        /// passed to the <paramref name="version"/> parameter.</returns>
        public readonly bool DidChange<T>(ref BufferTypeHandle<T> bufferTypeHandle, uint version) where T : unmanaged, IBufferElementData
        {
            return ChangeVersionUtility.DidChange(GetChangeVersion(ref bufferTypeHandle), version);
        }
        /// <summary>
        /// Obsolete. Use <see cref="DidChange{T}(ref Unity.Entities.BufferTypeHandle{T},uint)"/> instead.
        /// </summary>
        /// <param name="bufferTypeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetBufferTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.</param>
        /// <param name="version">The version to compare. In a system, this parameter should be set to the
        /// current <see cref="ComponentSystemBase.LastSystemVersion"/> at the time the job is run or
        /// scheduled.</param>
        /// <typeparam name="T">The data type of the elements in the dynamic buffer.</typeparam>
        /// <returns>True, if the version number stored in the chunk for this component is more recent than the version
        /// passed to the <paramref name="version"/> parameter.</returns>
        [Obsolete("The bufferTypeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly bool DidChange<T>(BufferTypeHandle<T> bufferTypeHandle, uint version) where T : unmanaged, IBufferElementData
        {
            return ChangeVersionUtility.DidChange(GetChangeVersion(ref bufferTypeHandle), version);
        }

        /// <summary>
        /// Reports whether the value of shared components associated with the chunk, of the type identified by
        /// <paramref name="chunkSharedComponentData"/>, could have changed since the specified version.
        /// </summary>
        /// <remarks>
        /// Shared components behave differently than other types of components in terms of change versioning because
        /// changing the value of a shared component can move an entity to a different chunk. If the change results
        /// in an entity moving to a different chunk, then only the order version is updated (for both the original and
        /// the receiving chunk). If you change the shared component value for all entities in a chunk at once, the
        /// change version for that chunk is updated. The order version is unaffected.
        ///
        /// Note that for efficiency, the change version applies to whole chunks not individual entities. The change
        /// version is updated even when another job or system that has declared write access to a component does
        /// not actually change the component value.</remarks>
        /// <typeparam name="T">The data type of the shared component.</typeparam>
        /// <param name="chunkSharedComponentData">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetSharedComponentTypeHandle{T}"/>. Pass the object to a job
        /// using a public field you define as part of the job struct.</param>
        /// <param name="version">The version to compare. In a system, this parameter should be set to the
        /// current <see cref="ComponentSystemBase.LastSystemVersion"/> at the time the job is run or
        /// scheduled.</param>
        /// <returns>True, if the version number stored in the chunk for this component is more recent than the version
        /// passed to the <paramref name="version"/></returns>
        public readonly bool DidChange<T>(SharedComponentTypeHandle<T> chunkSharedComponentData, uint version) where T : struct, ISharedComponentData
        {
            return ChangeVersionUtility.DidChange(GetChangeVersion(chunkSharedComponentData), version);
        }

        /// <summary>
        /// Reports whether the value of shared components associated with the chunk, of the type identified by
        /// <paramref name="typeHandle"/>, could have changed since the specified version.
        /// </summary>
        /// <remarks>
        /// Shared components behave differently than other types of components in terms of change versioning because
        /// changing the value of a shared component can move an entity to a different chunk. If the change results
        /// in an entity moving to a different chunk, then only the order version is updated (for both the original and
        /// the receiving chunk). If you change the shared component value for all entities in a chunk at once, the
        /// change version for that chunk is updated. The order version is unaffected.
        ///
        /// Note that for efficiency, the change version applies to whole chunks not individual entities. The change
        /// version is updated even when another job or system that has declared write access to a component does
        /// not actually change the component value.</remarks>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetDynamicSharedComponentTypeHandle"/>.</param>
        /// <param name="version">The version to compare. In a system, this parameter should be set to the
        /// current <see cref="ComponentSystemBase.LastSystemVersion"/>.</param>
        /// <returns>True, if the version number stored in the chunk for this component is more recent than the version
        /// passed to the <paramref name="version"/></returns>
        public readonly bool DidChange(ref DynamicSharedComponentTypeHandle typeHandle, uint version)
        {
            return ChangeVersionUtility.DidChange(GetChangeVersion(ref typeHandle), version);
        }
        /// <summary>
        /// Obsolete. Use <see cref="DidChange(ref DynamicSharedComponentTypeHandle,uint)"/>
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetDynamicSharedComponentTypeHandle"/>.</param>
        /// <param name="version">The version to compare. In a system, this parameter should be set to the
        /// current <see cref="ComponentSystemBase.LastSystemVersion"/>.</param>
        /// <returns>True, if the version number stored in the chunk for this component is more recent than the version
        /// passed to the <paramref name="version"/></returns>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly bool DidChange(DynamicSharedComponentTypeHandle typeHandle, uint version)
        {
            return ChangeVersionUtility.DidChange(GetChangeVersion(ref typeHandle), version);
        }

        /// <summary>
        /// Gets the change version number assigned to the specified type of component in this chunk.
        /// </summary>
        /// <remarks>
        /// Every time a system accesses components in a chunk, the system updates the change version of any
        /// component types to which it has write access with the current
        /// <see cref="ComponentSystemBase.GlobalSystemVersion"/>. (A system updates the version whether or not you
        /// actually write any component data -- always specify read-only access when possible.)
        ///
        /// You can use the change version to filter out entities that have not changed since the last time a system ran.
        /// Implement change filtering using one of the following:
        ///
        /// - [Entities.ForEach.WithChangeFilter(ComponentType)](xref:Unity.Entities.SystemBase.Entities)
        /// - <see cref="EntityQuery.AddChangedVersionFilter(ComponentType)"/>
        /// - <see cref="ArchetypeChunk.DidChange{T}(ref ComponentTypeHandle{T}, uint)"/> in an <see cref="IJobChunk"/> job.
        ///
        /// Note that change versions are stored at the chunk level. Thus when you use change filtering, the query system
        /// excludes or includes whole chunks not individual entities.
        /// </remarks>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetComponentTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.</param>
        /// <typeparam name="T">The data type of component T.</typeparam>
        /// <returns>The current version number of the specified component, which is the version set the last time a system
        /// accessed a component of that type in this chunk with write privileges. Returns 0 if the chunk does not contain
        /// a component of the specified type.</returns>
        public readonly uint GetChangeVersion<T>(ref ComponentTypeHandle<T> typeHandle)
            where T : IComponentData
        {
            var archetype = m_EntityComponentStore->GetArchetype(m_Chunk);
            if (Hint.Unlikely(typeHandle.m_LookupCache.Archetype != archetype))
            {
                typeHandle.m_LookupCache.Update(archetype, typeHandle.m_TypeIndex);
            }
            var typeIndexInArchetype = typeHandle.m_LookupCache.IndexInArchetype;
            if (Hint.Unlikely(typeIndexInArchetype == -1)) return 0;

            return Archetype.Archetype->Chunks.GetChangeVersion(typeIndexInArchetype, m_Chunk.ListIndex);
        }
        /// <summary>
        /// Obsolete. Use <see cref="GetChangeVersion{T}(ref Unity.Entities.ComponentTypeHandle{T})"/> instead.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetComponentTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.</param>
        /// <typeparam name="T">The data type of component T.</typeparam>
        /// <returns>The current version number of the specified component, which is the version set the last time a system
        /// accessed a component of that type in this chunk with write privileges. Returns 0 if the chunk does not contain
        /// a component of the specified type.</returns>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly uint GetChangeVersion<T>(ComponentTypeHandle<T> typeHandle)
            where T : IComponentData
        {
            return GetChangeVersion(ref typeHandle);
        }

        /// <summary>
        /// Gets the change version number assigned to the specified type of component in this chunk.
        /// </summary>
        /// <remarks>
        /// Every time a system accesses components in a chunk, the system updates the change version of any
        /// component types to which it has write access with the current
        /// <see cref="ComponentSystemBase.GlobalSystemVersion"/>. (A system updates the version whether or not you
        /// actually write any component data -- always specify read-only access when possible.)
        ///
        /// You can use the change version to filter out entities that have not changed since the last time a system ran.
        /// Implement change filtering using one of the following:
        ///
        /// - [Entities.ForEach.WithChangeFilter(ComponentType)](xref:Unity.Entities.SystemBase.Entities)
        /// - <see cref="EntityQuery.AddChangedVersionFilter(ComponentType)"/>
        /// - <see cref="ArchetypeChunk.DidChange{T}(ref ComponentTypeHandle{T}, uint)"/> in an <see cref="IJobChunk"/> job.
        ///
        /// Note that change versions are stored at the chunk level. Thus when you use change filtering, the query system
        /// excludes or includes whole chunks not individual entities.
        /// </remarks>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetDynamicComponentTypeHandle"/>. Pass the object to a job using
        /// a public field you define as part of the job struct.</param>
        /// <returns>The current version number of the specified component, which is the version set the last time a system
        /// accessed a component of that type in this chunk with write privileges. Returns 0 if the chunk does not contain
        /// a component of the specified type.</returns>
        public readonly uint GetChangeVersion(ref DynamicComponentTypeHandle typeHandle)
        {
            ChunkDataUtility.GetIndexInTypeArray(m_EntityComponentStore->GetArchetype(m_Chunk), typeHandle.m_TypeIndex, ref typeHandle.m_TypeLookupCache);
            int typeIndexInArchetype = typeHandle.m_TypeLookupCache;
            if (Hint.Unlikely(typeIndexInArchetype == -1)) return 0;

            return Archetype.Archetype->Chunks.GetChangeVersion(typeIndexInArchetype, m_Chunk.ListIndex);
        }
        /// <summary>
        /// Obsolete. Use <see cref="GetChangeVersion(ref Unity.Entities.DynamicComponentTypeHandle)"/> instead.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetDynamicComponentTypeHandle"/>. Pass the object to a job using
        /// a public field you define as part of the job struct.</param>
        /// <returns>The current version number of the specified component, which is the version set the last time a system
        /// accessed a component of that type in this chunk with write privileges. Returns 0 if the chunk does not contain
        /// a component of the specified type.</returns>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly uint GetChangeVersion(DynamicComponentTypeHandle typeHandle)
        {
            return GetChangeVersion(ref typeHandle);
        }

        /// <summary>
        /// Gets the change version number assigned to the specified type of dynamic buffer component in this chunk.
        /// </summary>
        /// <remarks>
        /// Every time a system accesses components in a chunk, the system updates the change version of any
        /// component types to which it has write access with the current
        /// <see cref="ComponentSystemBase.GlobalSystemVersion"/>. (A system updates the version whether or not you
        /// actually write any component data -- always specify read-only access when possible.)
        ///
        /// You can use the change version to filter out entities that have not changed since the last time a system ran.
        /// Implement change filtering using one of the following:
        ///
        /// - [Entities.ForEach.WithChangeFilter(ComponentType)](xref:Unity.Entities.SystemBase.Entities)
        /// - <see cref="EntityQuery.AddChangedVersionFilter(ComponentType)"/>
        /// - <see cref="ArchetypeChunk.DidChange{T}(ref ComponentTypeHandle{T}, uint)"/> in an <see cref="IJobChunk"/> job.
        ///
        /// Note that change versions are stored at the chunk level. Thus if you use change filtering, the query system
        /// excludes or includes whole chunks not individual entities.
        /// </remarks>
        /// <param name="bufferTypeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetBufferTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.</param>
        /// <typeparam name="T">The data type of component T.</typeparam>
        /// <returns>The current version number of the specified dynamic buffer type, which is the version set the last time a system
        /// accessed a buffer component of that type in this chunk with write privileges. Returns 0 if the chunk does not contain
        /// a buffer component of the specified type.</returns>
        public readonly uint GetChangeVersion<T>(ref BufferTypeHandle<T> bufferTypeHandle)
            where T : unmanaged, IBufferElementData
        {
            var archetype = m_EntityComponentStore->GetArchetype(m_Chunk);
            if (Hint.Unlikely(bufferTypeHandle.m_LookupCache.Archetype != archetype))
            {
                bufferTypeHandle.m_LookupCache.Update(archetype, bufferTypeHandle.m_TypeIndex);
            }
            var typeIndexInArchetype = bufferTypeHandle.m_LookupCache.IndexInArchetype;
            if (Hint.Unlikely(typeIndexInArchetype == -1)) return 0;

            return Archetype.Archetype->Chunks.GetChangeVersion(typeIndexInArchetype, m_Chunk.ListIndex);
        }
        /// <summary>
        /// Obsolete. Use <see cref="GetChangeVersion{T}(ref Unity.Entities.BufferTypeHandle{T})"/> instead.
        /// </summary>
        /// <param name="bufferTypeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetBufferTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.</param>
        /// <typeparam name="T">The data type of component T.</typeparam>
        /// <returns>The current version number of the specified dynamic buffer type, which is the version set the last time a system
        /// accessed a buffer component of that type in this chunk with write privileges. Returns 0 if the chunk does not contain
        /// a buffer component of the specified type.</returns>
        [Obsolete("The bufferTypeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly uint GetChangeVersion<T>(BufferTypeHandle<T> bufferTypeHandle)
            where T : unmanaged, IBufferElementData
        {
            return GetChangeVersion(ref bufferTypeHandle);
        }

        /// <summary>
        /// Gets the change version number assigned to the specified type of shared component in this chunk.
        /// </summary>
        /// <remarks>
        /// Shared components behave differently than other types of components in terms of change versioning because
        /// changing the value of a shared component can move an entity to a different chunk. If the change results
        /// in an entity moving to a different chunk, then only the order version is updated (for both the original and
        /// the receiving chunk). If you change the shared component value for all entities in a chunk at once,
        /// the entities remain in their current chunk. The change version for that chunk is updated and the order
        /// version is unaffected.
        /// </remarks>
        /// <param name="chunkSharedComponentData">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetSharedComponentTypeHandle{T}"/>. Pass the object to a job
        /// using a public field you define as part of the job struct.</param>
        /// <typeparam name="T">The data type of shared component T.</typeparam>
        /// <returns>The current version number of the specified shared component, which is the version set the last time a system
        /// accessed a component of that type in this chunk with write privileges. Returns 0 if the chunk does not contain
        /// a shared component of the specified type.</returns>
        public readonly uint GetChangeVersion<T>(SharedComponentTypeHandle<T> chunkSharedComponentData)
            where T : struct, ISharedComponentData
        {
            var typeIndexInArchetype = ChunkDataUtility.GetIndexInTypeArray(m_EntityComponentStore->GetArchetype(m_Chunk), chunkSharedComponentData.m_TypeIndex);
            if (Hint.Unlikely(typeIndexInArchetype == -1)) return 0;

            return Archetype.Archetype->Chunks.GetChangeVersion(typeIndexInArchetype, m_Chunk.ListIndex);
        }

        /// <summary>
        /// Gets the change version number assigned to the specified type of shared component in this chunk.
        /// </summary>
        /// <remarks>
        /// Shared components behave differently than other types of components in terms of change versioning because
        /// changing the value of a shared component can move an entity to a different chunk. If the change results
        /// in an entity moving to a different chunk, then only the order version is updated (for both the original and
        /// the receiving chunk). If you change the shared component value for all entities in a chunk at once,
        /// the entities remain in their current chunk. The change version for that chunk is updated and the order
        /// version is unaffected.
        /// </remarks>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetDynamicSharedComponentTypeHandle"/>.</param>
        /// <returns>The current version number of the specified shared component, which is the version set the last time a system
        /// accessed a component of that type in this chunk with write privileges. Returns 0 if the chunk does not contain
        /// a shared component of the specified type.</returns>
        public readonly uint GetChangeVersion(ref DynamicSharedComponentTypeHandle typeHandle)
        {
            ChunkDataUtility.GetIndexInTypeArray(m_EntityComponentStore->GetArchetype(m_Chunk), typeHandle.m_TypeIndex,
                ref typeHandle.m_cachedTypeIndexinArchetype);
            int typeIndexInArchetype = typeHandle.m_cachedTypeIndexinArchetype;
            if (Hint.Unlikely(typeIndexInArchetype == -1)) return 0;

            return Archetype.Archetype->Chunks.GetChangeVersion(typeIndexInArchetype, m_Chunk.ListIndex);
        }
        /// <summary>
        /// Obsolete. Use <see cref="GetChangeVersion(ref DynamicSharedComponentTypeHandle)"/> instead.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetDynamicSharedComponentTypeHandle"/>.</param>
        /// <returns>The current version number of the specified shared component, which is the version set the last time a system
        /// accessed a component of that type in this chunk with write privileges. Returns 0 if the chunk does not contain
        /// a shared component of the specified type.</returns>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly uint GetChangeVersion(DynamicSharedComponentTypeHandle typeHandle)
        {
            return GetChangeVersion(ref typeHandle);
        }

        /// <summary>
        /// Reports whether a structural change has occured in this chunk since the specified version.
        /// </summary>
        /// <remarks>
        /// Typically, you set the <paramref name="version"/> parameter to the
        /// <see cref="ComponentSystemBase.LastSystemVersion"/> of a system to detect whether the order version
        /// has changed since the last time that system ran.
        /// </remarks>
        /// <param name="version">The version number to compare. </param>
        /// <returns>True, if the order version number has changed since the specified version.</returns>
        public readonly bool DidOrderChange(uint version)
        {
            return ChangeVersionUtility.DidChange(GetOrderVersion(), version);
        }

        /// <summary>
        /// Gets the order version number assigned to this chunk.
        /// </summary>
        /// <remarks>
        /// Every time you perform a structural change affecting a chunk, the ECS framework updates the order
        /// version of the chunk to the current <see cref="ComponentSystemBase.GlobalSystemVersion"/> value.
        /// Structural changes include adding and removing entities, adding or removing the component of an
        /// entity, and changing the value of a shared component (except when you change the value for all entities
        /// in a chunk at the same time).
        /// </remarks>
        /// <returns>The current order version of this chunk.</returns>
        public readonly uint GetOrderVersion()
        {
            return Archetype.Archetype->Chunks.GetOrderVersion(m_Chunk.ListIndex);
        }

        /// <summary>
        /// Checks whether a given <see cref="IComponentData"/> is enabled on the specified <see cref="Entity"/>. For the purposes
        /// of EntityQuery matching, an entity with a disabled component will behave as if it does not have that component.
        /// </summary>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <typeparam name="T">The component type whose enabled status should be checked. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</typeparam>
        /// <param name="typeHandle">A type handle for the component type that will be queried.</param>
        /// <param name="entityIndexInChunk">The index within this chunk of the entity whose component should be checked.</param>
        /// <returns>True if the specified component is enabled, or false if it is disabled. Also returns false if the <typeparamref name="T"/>
        /// is not present in this chunk.</returns>
        /// <seealso cref="SetComponentEnabled{T}(ref ComponentTypeHandle{T},int,bool)"/>
        public readonly bool IsComponentEnabled<T>(ref ComponentTypeHandle<T> typeHandle, int entityIndexInChunk) where T :
#if UNITY_DISABLE_MANAGED_COMPONENTS
            struct,
#endif
            IComponentData, IEnableableComponent
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            , new()
#endif
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(typeHandle.m_Safety);
#endif
            var archetype = m_EntityComponentStore->GetArchetype(m_Chunk);
            if (Hint.Unlikely(typeHandle.m_LookupCache.Archetype != archetype))
            {
                typeHandle.m_LookupCache.Update(archetype, typeHandle.m_TypeIndex);
            }
            var typeIndexInArchetype = typeHandle.m_LookupCache.IndexInArchetype;
            if (Hint.Unlikely(typeIndexInArchetype == -1))
                return false;

            return m_EntityComponentStore->IsComponentEnabled(m_Chunk, entityIndexInChunk, typeIndexInArchetype);
        }
        /// <summary>
        /// Obsolete. Use <see cref="IsComponentEnabled{T}(ref Unity.Entities.ComponentTypeHandle{T},int)"/> instead.
        /// </summary>
        /// <typeparam name="T">The component type whose enabled status should be checked. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</typeparam>
        /// <param name="typeHandle">A type handle for the component type that will be queried.</param>
        /// <param name="entityIndexInChunk">The index within this chunk of the entity whose component should be checked.</param>
        /// <returns>True if the specified component is enabled, or false if it is disabled. Also returns false if the <typeparamref name="T"/>
        /// is not present in this chunk.</returns>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly bool IsComponentEnabled<T>(ComponentTypeHandle<T> typeHandle, int entityIndexInChunk) where T :
#if UNITY_DISABLE_MANAGED_COMPONENTS
            struct,
#endif
            IComponentData, IEnableableComponent
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            , new()
#endif
        {
            return IsComponentEnabled(ref typeHandle, entityIndexInChunk);
        }

        /// <summary>
        /// Checks whether a given <see cref="IBufferElementData"/> is enabled on the specified <see cref="Entity"/>. For the purposes
        /// of EntityQuery matching, an entity with a disabled component will behave as if it does not have that component.
        /// </summary>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <typeparam name="T">The buffer component type whose enabled status should be checked. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</typeparam>
        /// <param name="bufferTypeHandle">A type handle for the component type that will be queried.</param>
        /// <param name="entityIndexInChunk">The index within this chunk of the entity whose component should be checked.</param>
        /// <returns>True if the specified buffer component is enabled, or false if it is disabled. Also returns false if the <typeparamref name="T"/>
        /// is not present in this chunk.</returns>
        /// <seealso cref="SetComponentEnabled{T}(ref BufferTypeHandle{T},int,bool)"/>
        public readonly bool IsComponentEnabled<T>(ref BufferTypeHandle<T> bufferTypeHandle, int entityIndexInChunk) where T : unmanaged, IBufferElementData, IEnableableComponent
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(bufferTypeHandle.m_Safety0);
#endif
            var archetype = m_EntityComponentStore->GetArchetype(m_Chunk);
            if (Hint.Unlikely(bufferTypeHandle.m_LookupCache.Archetype != archetype))
            {
                bufferTypeHandle.m_LookupCache.Update(archetype, bufferTypeHandle.m_TypeIndex);
            }
            var typeIndexInArchetype = bufferTypeHandle.m_LookupCache.IndexInArchetype;
            if (Hint.Unlikely(typeIndexInArchetype == -1))
                return false;

            return m_EntityComponentStore->IsComponentEnabled(m_Chunk, entityIndexInChunk, typeIndexInArchetype);
        }
        /// <summary>
        /// Obsolete. Use <see cref="IsComponentEnabled{T}(ref Unity.Entities.BufferTypeHandle{T},int)"/> instead.
        /// </summary>
        /// <typeparam name="T">The buffer component type whose enabled status should be checked. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</typeparam>
        /// <param name="bufferTypeHandle">A type handle for the component type that will be queried.</param>
        /// <param name="entityIndexInChunk">The index within this chunk of the entity whose component should be checked.</param>
        /// <returns>True if the specified buffer component is enabled, or false if it is disabled. Also returns false if the <typeparamref name="T"/>
        /// is not present in this chunk.</returns>
        [Obsolete("The bufferTypeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly bool IsComponentEnabled<T>(BufferTypeHandle<T> bufferTypeHandle, int entityIndexInChunk) where T : unmanaged, IBufferElementData, IEnableableComponent
        {
            return IsComponentEnabled(ref bufferTypeHandle, entityIndexInChunk);
        }

        /// <summary>
        /// Checks whether a given <see cref="IBufferElementData"/> is enabled on the specified <see cref="Entity"/>. For the purposes
        /// of EntityQuery matching, an entity with a disabled component will behave as if it does not have that component.
        /// </summary>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <param name="typeHandle">A type handle for the component type that will be queried.</param>
        /// <param name="entityIndexInChunk">The index within this chunk of the entity whose component should be checked.</param>
        /// <returns>True if the specified buffer component is enabled, or false if it is disabled. Also returns false if the <typeparamref name="T"/>
        /// is not present in this chunk.</returns>
        /// <seealso cref="SetComponentEnabled{T}(ref BufferTypeHandle{T},int,bool)"/>
        public readonly bool IsComponentEnabled(ref DynamicComponentTypeHandle typeHandle, int entityIndexInChunk)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(typeHandle.m_Safety0);
#endif
            ChunkDataUtility.GetIndexInTypeArray(m_EntityComponentStore->GetArchetype(m_Chunk), typeHandle.m_TypeIndex, ref typeHandle.m_TypeLookupCache);
            var typeIndexInArchetype = typeHandle.m_TypeLookupCache;
            if (Hint.Unlikely(typeIndexInArchetype == -1))
                return false;

            return m_EntityComponentStore->IsComponentEnabled(m_Chunk, entityIndexInChunk, typeIndexInArchetype);
        }

        /// <summary>
        /// Gets a copy of all the Enableable bits for the specified type handle.
        /// </summary>
        /// <param name="handle">A type handle for the component type whose enabled bits you want to query.</param>
        /// <returns>A <see cref="v128"/> is returned containing a copy of the bitarray.</returns>
        public readonly unsafe v128 GetEnableableBits(ref DynamicComponentTypeHandle handle)
        {
            var archetype = m_EntityComponentStore->GetArchetype(m_Chunk);
            ChunkDataUtility.GetIndexInTypeArray(archetype, handle.m_TypeIndex, ref handle.m_TypeLookupCache);

            if (handle.m_TypeLookupCache == -1)
                return default;
            int indexInArchetype = handle.m_TypeLookupCache;
            int memoryOrderIndexInArchetype = archetype->TypeIndexInArchetypeToMemoryOrderIndex[indexInArchetype];
            return handle.m_TypeLookupCache == -1 ?
                new v128() :
                *archetype->Chunks.GetComponentEnabledMaskArrayForTypeInChunk(memoryOrderIndexInArchetype, m_Chunk.ListIndex);
        }

        /// <summary>
        /// Provides a ComponentEnabledMask to the component enabled bits in this chunk.
        /// </summary>
        /// <typeparam name="T">The component type</typeparam>
        /// <param name="typeHandle">Type handle for the component type <typeparamref name="T"/>.</param>
        /// <returns>An <see cref="EnabledMask"/> instance for component <typeparamref name="T"/> in this chunk.</returns>
        public readonly EnabledMask GetEnabledMask<T>(ref ComponentTypeHandle<T> typeHandle)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(typeHandle.m_Safety);
#endif
            var archetype = m_EntityComponentStore->GetArchetype(m_Chunk);
            if (Hint.Unlikely(typeHandle.m_LookupCache.Archetype != archetype))
            {
                typeHandle.m_LookupCache.Update(archetype, typeHandle.m_TypeIndex);
            }
            // In case the chunk does not contains the component type (or the internal TypeIndex lookup fails to find a
            // match), the LookupCache.Update will invalidate the IndexInArchetype.
            // In such a case, we return an empty EnabledMask.
            if (Hint.Unlikely(typeHandle.m_LookupCache.IndexInArchetype == -1))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return new EnabledMask(new SafeBitRef(null, 0, typeHandle.m_Safety), null);
#else
                return new EnabledMask(SafeBitRef.Null, null);
#endif
            }
            int* ptrChunkDisabledCount = default;
            var ptr = (typeHandle.IsReadOnly)
                ? ChunkDataUtility.GetEnabledRefRO(m_Chunk, Archetype.Archetype, typeHandle.m_LookupCache.IndexInArchetype).Ptr
                : ChunkDataUtility.GetEnabledRefRW(m_Chunk, Archetype.Archetype, typeHandle.m_LookupCache.IndexInArchetype,
                    typeHandle.GlobalSystemVersion, out ptrChunkDisabledCount).Ptr;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var result = new EnabledMask(new SafeBitRef(ptr, 0, typeHandle.m_Safety), ptrChunkDisabledCount);
#else
            var result = new EnabledMask(new SafeBitRef(ptr, 0), ptrChunkDisabledCount);
#endif
            return result;
        }

        private readonly short GetRequiredTypeIndexInArchetype<T>(ref ComponentTypeHandle<T> typeHandle, Archetype* archetype)
            where T:
#if UNITY_DISABLE_MANAGED_COMPONENTS
            struct,
#endif
            IComponentData
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            , new()
#endif
        {
            if (Hint.Unlikely(typeHandle.m_LookupCache.Archetype != archetype))
            {
                typeHandle.m_LookupCache.Update(archetype, typeHandle.m_TypeIndex);
            }
            var typeIndexInArchetype = typeHandle.m_LookupCache.IndexInArchetype;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(typeIndexInArchetype == -1))
            {
                var typeName = typeHandle.m_TypeIndex.ToFixedString();
                throw new ArgumentException($"The target chunk does not have data for Component type {typeName}");
            }
#endif
            return typeIndexInArchetype;
        }

        private readonly short GetRequiredTypeIndexInArchetype<T>(ref BufferTypeHandle<T> bufferTypeHandle, Archetype* archetype)
            where T : unmanaged, IBufferElementData
        {
            if (Hint.Unlikely(bufferTypeHandle.m_LookupCache.Archetype != archetype))
            {
                bufferTypeHandle.m_LookupCache.Update(archetype, bufferTypeHandle.m_TypeIndex);
            }

            var typeIndexInArchetype = bufferTypeHandle.m_LookupCache.IndexInArchetype;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(typeIndexInArchetype == -1))
            {
                var typeName = bufferTypeHandle.m_TypeIndex.ToFixedString();
                throw new ArgumentException($"The target chunk does not have data for Component type {typeName}");
            }
#endif
            return typeIndexInArchetype;
        }

        private readonly short GetRequiredTypeIndexInArchetype(ref DynamicComponentTypeHandle typeHandle, Archetype* archetype)
        {
            ChunkDataUtility.GetIndexInTypeArray(archetype, typeHandle.m_TypeIndex, ref typeHandle.m_TypeLookupCache);
            var typeIndexInArchetype = typeHandle.m_TypeLookupCache;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(typeIndexInArchetype == -1))
            {
                var typeName = typeHandle.m_TypeIndex.ToFixedString();
                throw new ArgumentException($"The target chunk does not have data for Component type {typeName}");
            }
#endif
            return typeIndexInArchetype;
        }

        private readonly void SetComponentEnabledForAllInChunk(Archetype* archetype, short typeIndexInArchetype, bool value)
        {
            var bits = ChunkDataUtility.GetEnabledRefRW(m_Chunk, archetype,
                typeIndexInArchetype, m_EntityComponentStore->GlobalSystemVersion, out var ptrChunkDisabledCount);
            bits.SetBits(0, value, Count);
            *ptrChunkDisabledCount = value ? 0 : Count;
        }

        /// <summary>
        /// Enable or disable a <see cref="IComponentData"/> on all entities in this chunk.
        /// </summary>
        /// <remarks>
        /// This is more efficient than calling <see cref="SetComponentEnabled"/> on each entity individually, but
        /// has the same net result.
        /// This operation does not cause a structural change, or affect the value of the components. For the purposes
        /// of EntityQuery matching, an entity with a disabled component will behave as if it does not have that component.
        /// </remarks>
        /// <exception cref="ArgumentException">The target component type <typeparamref name="T"/> is not present in the this chunk.</exception>
        /// <typeparam name="T">The component type to enable or disable. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</typeparam>
        /// <param name="typeHandle">A type handle for the component type that will be enabled or disabled.</param>
        /// <param name="value">True if the specified component should be enabled, or false if it should be disabled.</param>
        /// <exception cref="ArgumentException"></exception>
        public readonly void SetComponentEnabledForAll<T>(ref ComponentTypeHandle<T> typeHandle, bool value) where T :
#if UNITY_DISABLE_MANAGED_COMPONENTS
            struct,
#endif
            IComponentData, IEnableableComponent
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            , new()
#endif
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(typeHandle.m_Safety);
#endif
            var archetype = Archetype.Archetype;
            var typeIndexInArchetype = GetRequiredTypeIndexInArchetype(ref typeHandle, archetype);
            SetComponentEnabledForAllInChunk(archetype, typeIndexInArchetype, value);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(m_EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecordSetComponentEnabled(ref typeHandle, value);
#endif
        }

        /// <summary>
        /// Enable or disable an <see cref="IBufferElementData"/> on all entities in this chunk.
        /// </summary>
        /// <remarks>
        /// This is more efficient than calling <see cref="SetComponentEnabled"/> on each entity individually, but
        /// has the same net result.
        /// This operation does not cause a structural change, or affect the value of the components. For the purposes
        /// of EntityQuery matching, an entity with a disabled component will behave as if it does not have that component.
        /// </remarks>
        /// <exception cref="ArgumentException">The target component type <typeparamref name="T"/> is not present in the this chunk.</exception>
        /// <typeparam name="T">The component type to enable or disable. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</typeparam>
        /// <param name="bufferTypeHandle">A type handle for the buffer component type that will be enabled or disabled.</param>
        /// <param name="value">True if the specified component should be enabled, or false if it should be disabled.</param>
        /// <exception cref="ArgumentException"></exception>
        public readonly void SetComponentEnabledForAll<T>(ref BufferTypeHandle<T> bufferTypeHandle, bool value) where T : unmanaged, IBufferElementData, IEnableableComponent
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(bufferTypeHandle.m_Safety0);
#endif
            var archetype = Archetype.Archetype;
            var typeIndexInArchetype = GetRequiredTypeIndexInArchetype(ref bufferTypeHandle, archetype);
            SetComponentEnabledForAllInChunk(archetype, typeIndexInArchetype, value);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(m_EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecordSetComponentEnabled(ref bufferTypeHandle, value);
#endif
        }

        /// <summary>
        /// Enable or disable a component on all entities in this chunk.
        /// </summary>
        /// <remarks>
        /// This is more efficient than calling <see cref="SetComponentEnabled"/> on each entity individually, but
        /// has the same net result.
        /// This operation does not cause a structural change, or affect the value of the components. For the purposes
        /// of EntityQuery matching, an entity with a disabled component will behave as if it does not have that component.
        /// </remarks>
        /// <exception cref="ArgumentException">The target component type is not present in the this chunk.</exception>
        /// <param name="typeHandle">A type handle for the component type that will be enabled or disabled. This component must implement the
        /// <see cref="IEnableableComponent"/> interface.</param>
        /// <param name="value">True if the specified component should be enabled, or false if it should be disabled.</param>
        /// <exception cref="ArgumentException">Thrown if this chunk does not have the target component type.</exception>
        /// <exception cref="ArgumentException">Thrown if the component type does not implement <see cref="IEnableableComponent"/>.</exception>
        public readonly void SetComponentEnabledForAll(ref DynamicComponentTypeHandle typeHandle, bool value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(typeHandle.m_Safety0);
#endif
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(!TypeManager.IsEnableableType(typeHandle.m_TypeIndex)))
            {
                var typeName = typeHandle.m_TypeIndex.ToFixedString();
                throw new ArgumentException($"Component type {typeName} must implement {nameof(IEnableableComponent)}");
            }
#endif
            var archetype = Archetype.Archetype;
            short typeIndexInArchetype = GetRequiredTypeIndexInArchetype(ref typeHandle, archetype);
            SetComponentEnabledForAllInChunk(archetype, typeIndexInArchetype, value);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(m_EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecordSetComponentEnabled(ref typeHandle, value);
#endif
        }

        /// <summary>
        /// Enable or disable a <see cref="IComponentData"/> on the specified <see cref="Entity"/>. This operation does
        /// not cause a structural change, or affect the value of the component. For the purposes
        /// of EntityQuery matching, an entity with a disabled component will behave as if it does not have that component.
        /// </summary>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <exception cref="ArgumentException">The target component type <typeparamref name="T"/> is not present in the this chunk.</exception>
        /// <typeparam name="T">The component type to enable or disable. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</typeparam>
        /// <param name="typeHandle">A type handle for the component type that will be enabled or disabled.</param>
        /// <param name="entityIndexInChunk">The index within this chunk of the entity whose component should be checked.</param>
        /// <param name="value">True if the specified component should be enabled, or false if it should be disabled.</param>
        /// <seealso cref="IsComponentEnabled{T}(ref Unity.Entities.ComponentTypeHandle{T},int)"/>
        public readonly void SetComponentEnabled<T>(ref ComponentTypeHandle<T> typeHandle, int entityIndexInChunk, bool value) where T:
#if UNITY_DISABLE_MANAGED_COMPONENTS
            struct,
#endif
            IComponentData, IEnableableComponent
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            , new()
#endif
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(typeHandle.m_Safety);
#endif
            var archetype = Archetype.Archetype;
            short typeIndexInArchetype = GetRequiredTypeIndexInArchetype(ref typeHandle, archetype);
            m_EntityComponentStore->SetComponentEnabled(m_Chunk, entityIndexInChunk, typeIndexInArchetype, value);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(m_EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecordSetComponentEnabled(ref typeHandle, value);
#endif
        }
        /// <summary>
        /// Obsolete. Use <see cref="SetComponentEnabled{T}(ref Unity.Entities.ComponentTypeHandle{T},int,bool)"/> instead.
        /// </summary>
        /// <typeparam name="T">The component type to enable or disable. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</typeparam>
        /// <param name="typeHandle">A type handle for the component type that will be enabled or disabled.</param>
        /// <param name="entityIndexInChunk">The index within this chunk of the entity whose component should be checked.</param>
        /// <param name="value">True if the specified component should be enabled, or false if it should be disabled.</param>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly void SetComponentEnabled<T>(ComponentTypeHandle<T> typeHandle, int entityIndexInChunk, bool value) where T:
#if UNITY_DISABLE_MANAGED_COMPONENTS
            struct,
#endif
            IComponentData, IEnableableComponent
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            , new()
#endif
        {
            SetComponentEnabled(ref typeHandle, entityIndexInChunk, value);
        }

        /// <summary>
        /// Enable or disable a <see cref="IBufferElementData"/> on the specified <see cref="Entity"/>. This operation does
        /// not cause a structural change, or affect the value of the component. For the purposes
        /// of EntityQuery matching, an entity with a disabled component will behave as if it does not have that component.
        /// </summary>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <exception cref="ArgumentException">The target component type <typeparamref name="T"/> is not present in the this chunk.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="bufferTypeHandle"/> was created as read-only (in-Editor only, if the Jobs Debugger is enabled.</exception>
        /// <typeparam name="T">The buffer component type to enable or disable. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</typeparam>
        /// <param name="bufferTypeHandle">A type handle for the buffer component type that will be enabled or disabled.</param>
        /// <param name="entityIndexInChunk">The index within this chunk of the entity whose buffer component should be checked.</param>
        /// <param name="value">True if the specified buffer component should be enabled, or false if it should be disabled.</param>
        /// <seealso cref="IsComponentEnabled{T}(ref BufferTypeHandle{T},int)"/>
        public readonly void SetComponentEnabled<T>(ref BufferTypeHandle<T> bufferTypeHandle, int entityIndexInChunk, bool value) where T : unmanaged, IBufferElementData, IEnableableComponent
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(bufferTypeHandle.m_Safety0);
#endif
            var archetype = Archetype.Archetype;
            short typeIndexInArchetype = GetRequiredTypeIndexInArchetype(ref bufferTypeHandle, archetype);
            m_EntityComponentStore->SetComponentEnabled(m_Chunk, entityIndexInChunk, typeIndexInArchetype, value);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(m_EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecordSetComponentEnabled(ref bufferTypeHandle, value);
#endif
        }
        /// <summary>
        /// Obsolete. Use <see cref="SetComponentEnabled{T}(ref BufferTypeHandle{T},int,bool)"/> instead.
        /// </summary>
        /// <typeparam name="T">The buffer component type to enable or disable. This type must implement the
        /// <see cref="IEnableableComponent"/> interface.</typeparam>
        /// <param name="bufferTypeHandle">A type handle for the buffer component type that will be enabled or disabled.</param>
        /// <param name="entityIndexInChunk">The index within this chunk of the entity whose buffer component should be checked.</param>
        /// <param name="value">True if the specified buffer component should be enabled, or false if it should be disabled.</param>
        [Obsolete("The bufferTypeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly void SetComponentEnabled<T>(BufferTypeHandle<T> bufferTypeHandle, int entityIndexInChunk, bool value) where T : unmanaged, IBufferElementData, IEnableableComponent
        {
            SetComponentEnabled(ref bufferTypeHandle, entityIndexInChunk, value);
        }

        /// <summary>
        /// Enable or disable a <see cref="IComponentData"/> on the specified <see cref="Entity"/>. This operation does
        /// not cause a structural change, or affect the value of the component. For the purposes
        /// of EntityQuery matching, an entity with a disabled component will behave as if it does not have that component.
        /// </summary>
        /// <exception cref="ArgumentException">The <see cref="Entity"/> does not exist.</exception>
        /// <exception cref="ArgumentException">The target component type is not present in the this chunk.</exception>
        /// <exception cref="ArgumentException">The target component type does not implement <see cref="IEnableableComponent"/>.</exception>
        /// <param name="typeHandle">A type handle for the component type that will be enabled or disabled.</param>
        /// <param name="entityIndexInChunk">The index within this chunk of the entity whose component should be checked.</param>
        /// <param name="value">True if the specified component should be enabled, or false if it should be disabled.</param>
        public readonly void SetComponentEnabled(ref DynamicComponentTypeHandle typeHandle, int entityIndexInChunk, bool value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(typeHandle.m_Safety0);
#endif
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(!TypeManager.IsEnableableType(typeHandle.m_TypeIndex)))
            {
                var typeName = typeHandle.m_TypeIndex.ToFixedString();
                throw new ArgumentException($"Component type {typeName} must implement {nameof(IEnableableComponent)}");
            }
#endif
            var archetype = Archetype.Archetype;
            short typeIndexInArchetype = GetRequiredTypeIndexInArchetype(ref typeHandle, archetype);
            m_EntityComponentStore->SetComponentEnabled(m_Chunk, entityIndexInChunk, typeIndexInArchetype, value);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(m_EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecordSetComponentEnabled(ref typeHandle, value);
#endif
        }

        /// <summary>
        /// Gets the value of a chunk component.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetComponentTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.</param>
        /// <typeparam name="T">The data type of the chunk component.</typeparam>
        /// <returns>A copy of the chunk component.</returns>
        /// <seealso cref="SetChunkComponentData{T}(ref Unity.Entities.ComponentTypeHandle{T},T)"/>
        public readonly T GetChunkComponentData<T>(ref ComponentTypeHandle<T> typeHandle)
            where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(typeHandle.m_Safety);
#endif
            // TODO(DOTS-5748): use type handle's LookupCache here
            var metaChunkEntity = m_Chunk.MetaChunkEntity;
            m_EntityComponentStore->AssertEntityHasComponent(metaChunkEntity, typeHandle.m_TypeIndex);
            var ptr = m_EntityComponentStore->GetComponentDataWithTypeRO(metaChunkEntity, typeHandle.m_TypeIndex);
            T value;
            UnsafeUtility.CopyPtrToStructure(ptr, out value);
            return value;
        }
        /// <summary>
        /// Obsolete. Use <see cref="GetChunkComponentData{T}(ref Unity.Entities.ComponentTypeHandle{T})"/> instead.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetComponentTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.</param>
        /// <typeparam name="T">The data type of the chunk component.</typeparam>
        /// <returns>A copy of the chunk component.</returns>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly T GetChunkComponentData<T>(ComponentTypeHandle<T> typeHandle)
            where T : struct
        {
            return GetChunkComponentData(ref typeHandle);
        }

        /// <summary>
        /// Sets the value of a chunk component.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetComponentTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.</param>
        /// <typeparam name="T">The data type of the chunk component.</typeparam>
        /// <param name="value">A struct of type T containing the new values for the chunk component.</param>
        /// <seealso cref="GetChunkComponentData{T}(ref Unity.Entities.ComponentTypeHandle{T})"/>
        public readonly void SetChunkComponentData<T>(ref ComponentTypeHandle<T> typeHandle, T value)
            where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(typeHandle.m_Safety);
#endif
            // TODO(DOTS-5748): use type handle's LookupCache here
            var metaChunkEntity = m_Chunk.MetaChunkEntity;
            m_EntityComponentStore->AssertEntityHasComponent(metaChunkEntity, typeHandle.m_TypeIndex);
            var ptr = m_EntityComponentStore->GetComponentDataWithTypeRW(metaChunkEntity, typeHandle.m_TypeIndex, typeHandle.GlobalSystemVersion);
            UnsafeUtility.CopyStructureToPtr(ref value, ptr);
        }
        /// <summary>
        /// Obsolete. Use <see cref="SetChunkComponentData{T}(ref Unity.Entities.ComponentTypeHandle{T},T)"/> instead.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetComponentTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.</param>
        /// <typeparam name="T">The data type of the chunk component.</typeparam>
        /// <param name="value">A struct of type T containing the new values for the chunk component.</param>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly void SetChunkComponentData<T>(ComponentTypeHandle<T> typeHandle, T value)
            where T : struct
        {
            SetChunkComponentData(ref typeHandle, value);
        }

        /// <summary>
        /// Gets the index into the array of unique values for the specified shared component.
        /// </summary>
        /// <remarks>
        /// Because shared components can contain managed types, you can only access the value index of a shared component
        /// inside a job, not the value itself. The index value indexes the array returned by
        /// <see cref="EntityManager.GetAllUniqueSharedComponentsManaged{T}(System.Collections.Generic.List{T})"/>. If desired, you can create a native
        /// array that mirrors your unique value list, but which contains only unmanaged, blittable data and pass that
        /// into an <see cref="IJobChunk"/> job. The unique value list and a specific index is only valid until a
        /// structural change occurs.
        /// </remarks>
        /// <param name="chunkSharedComponentData">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetSharedComponentTypeHandle{T}"/>. Pass the object to a job
        /// using a public field you define as part of the job struct.</param>
        /// <typeparam name="T">The data type of the shared component.</typeparam>
        /// <returns>The index value, or -1 if the chunk does not contain a shared component of the specified type.</returns>
        public readonly int GetSharedComponentIndex<T>(SharedComponentTypeHandle<T> chunkSharedComponentData)
            where T : struct, ISharedComponentData
        {
            var archetype = m_EntityComponentStore->GetArchetype(m_Chunk);
            var typeIndexInArchetype = ChunkDataUtility.GetIndexInTypeArray(archetype, chunkSharedComponentData.m_TypeIndex);
            if (Hint.Unlikely(typeIndexInArchetype == -1)) return -1;

            var chunkSharedComponentIndex = typeIndexInArchetype - archetype->FirstSharedComponent;
            var sharedComponentIndex = archetype->Chunks.GetSharedComponentValue(chunkSharedComponentIndex, m_Chunk.ListIndex);
            return sharedComponentIndex;
        }

        /// <summary>
        /// Gets the index into the array of unique values for the specified shared component.
        /// </summary>
        /// <remarks>
        /// Because shared components can contain managed types, you can only access the value index of a shared component
        /// inside a job, not the value itself. The index value indexes the array returned by
        /// <see cref="EntityManager.GetAllUniqueSharedComponentsManaged{T}(System.Collections.Generic.List{T})"/>. If desired, you can create a native
        /// array that mirrors your unique value list, but which contains only unmanaged, blittable data and pass that
        /// into an <see cref="IJobChunk"/> job. The unique value list and a specific index is only valid until a
        /// structural change occurs.
        /// </remarks>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetDynamicSharedComponentTypeHandle"/>.</param>
        /// <returns>The index value, or -1 if the chunk does not contain a shared component of the specified type.</returns>
        public readonly int GetSharedComponentIndex(ref DynamicSharedComponentTypeHandle typeHandle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(typeHandle.m_Safety);
#endif
            var archetype = m_EntityComponentStore->GetArchetype(m_Chunk);
            ChunkDataUtility.GetIndexInTypeArray(archetype, typeHandle.m_TypeIndex,
                ref typeHandle.m_cachedTypeIndexinArchetype);
            var typeIndexInArchetype = typeHandle.m_cachedTypeIndexinArchetype;
            if (Hint.Unlikely(typeIndexInArchetype == -1)) return -1;

            var chunkSharedComponentIndex = typeIndexInArchetype - archetype->FirstSharedComponent;
            var sharedComponentIndex = archetype->Chunks.GetSharedComponentValue(chunkSharedComponentIndex, m_Chunk.ListIndex);
            return sharedComponentIndex;
        }
        /// <summary>
        /// Obsolete. Use <see cref="GetSharedComponentIndex(ref DynamicSharedComponentTypeHandle)"/> instead.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetDynamicSharedComponentTypeHandle"/>.</param>
        /// <returns>The index value, or -1 if the chunk does not contain a shared component of the specified type.</returns>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly int GetSharedComponentIndex(DynamicSharedComponentTypeHandle typeHandle)
        {
            return GetSharedComponentIndex(ref typeHandle);
        }

        /// <summary> Obsolete. Use <see cref="GetSharedComponentManaged{T}"/> instead.</summary>
        /// <param name="chunkSharedComponentData">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetSharedComponentTypeHandle{T}"/>.</param>
        /// <param name="entityManager">An EntityManager instance.</param>
        /// <typeparam name="T">The data type of the shared component.</typeparam>
        /// <returns>The shared component value.</returns>
        [Obsolete("Use GetSharedComponentManaged (UnityUpgradable) -> GetSharedComponentManaged<T>(*)", true)]
        public T GetSharedComponentData<T>(SharedComponentTypeHandle<T> chunkSharedComponentData, EntityManager entityManager)
            where T : struct, ISharedComponentData { return default; }

        /// <summary>
        /// Gets the current value of a managed shared component.
        /// </summary>
        /// <remarks>You can't call this method inside a job.</remarks>
        /// <param name="chunkSharedComponentData">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetSharedComponentTypeHandle{T}"/>.</param>
        /// <param name="entityManager">An EntityManager instance.</param>
        /// <typeparam name="T">The data type of the shared component.</typeparam>
        /// <returns>The shared component value.</returns>
        public readonly T GetSharedComponentManaged<T>(SharedComponentTypeHandle<T> chunkSharedComponentData, EntityManager entityManager)
            where T : struct, ISharedComponentData
        {
            return entityManager.GetSharedComponentManaged<T>(GetSharedComponentIndex(chunkSharedComponentData));
        }

        /// <summary> Obsolete. Use <see cref="GetSharedComponent{T}(SharedComponentTypeHandle{T})"/> instead.</summary>
        /// <param name="chunkSharedComponentData">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetSharedComponentTypeHandle{T}"/>.</param>
        /// <typeparam name="T">The data type of the shared component.</typeparam>
        /// <returns>The shared component value.</returns>
        [Obsolete("Use GetSharedComponent (UnityUpgradable) -> GetSharedComponent<T>(*)", true)]
        public T GetSharedComponentDataUnmanaged<T>(SharedComponentTypeHandle<T> chunkSharedComponentData)
            where T : unmanaged, ISharedComponentData
        {
            return default;
        }

        /// <summary>
        /// Gets the current value of an unmanaged shared component.
        /// </summary>
        /// <param name="chunkSharedComponentData">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetSharedComponentTypeHandle{T}"/>.</param>
        /// <param name="entityManager">The <see cref="EntityManager"/> through which the shared component value should be retrieved.</param>
        /// <typeparam name="T">The data type of the shared component.</typeparam>
        /// <returns>The shared component value.</returns>
        public readonly T GetSharedComponent<T>(SharedComponentTypeHandle<T> chunkSharedComponentData, EntityManager entityManager)
            where T : unmanaged, ISharedComponentData
        {
            return entityManager.GetSharedComponent<T>(GetSharedComponentIndex(chunkSharedComponentData));
        }
        /// <summary>
        /// Gets the current value of an unmanaged shared component.
        /// </summary>
        /// <param name="chunkSharedComponentData">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetSharedComponentTypeHandle{T}"/>.</param>
        /// <typeparam name="T">The data type of the shared component.</typeparam>
        /// <returns>The shared component value.</returns>
        public readonly T GetSharedComponent<T>(SharedComponentTypeHandle<T> chunkSharedComponentData)
            where T : unmanaged, ISharedComponentData
        {
            var sharedComponentIndex = GetSharedComponentIndex(chunkSharedComponentData);
            var typeIndex = TypeManager.GetTypeIndex<T>();
            T data = default(T);
            m_EntityComponentStore->GetSharedComponentData_Unmanaged(sharedComponentIndex, typeIndex, UnsafeUtility.AddressOf(ref data));
            return data;
        }

        /// <summary>
        /// Provides an unsafe interface to shared components stored in this chunk.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetDynamicSharedComponentTypeHandle"/>.</param>
        /// <returns>A pointer to the shared component value.</returns>
        public readonly void* GetDynamicSharedComponentDataAddress(ref DynamicSharedComponentTypeHandle typeHandle)
        {
            var sharedComponentIndex = GetSharedComponentIndex(ref typeHandle);
            var typeIndex = typeHandle.m_TypeIndex;

            return m_EntityComponentStore->GetSharedComponentDataAddr_Unmanaged(sharedComponentIndex, typeIndex);
        }
        /// <summary>
        /// Obsolete. Use <see cref="GetDynamicSharedComponentDataAddress(ref Unity.Entities.DynamicSharedComponentTypeHandle)"/> instead.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetDynamicSharedComponentTypeHandle"/>.</param>
        /// <returns>A pointer to the shared component value.</returns>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly void* GetDynamicSharedComponentDataAddress(DynamicSharedComponentTypeHandle typeHandle)
        {
            return GetDynamicSharedComponentDataAddress(ref typeHandle);
        }

        /// <summary>
        /// Gets the current value of a shared component.
        /// </summary>
        /// <remarks>You cannot call this function inside a job.</remarks>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetDynamicSharedComponentTypeHandle"/>.</param>
        /// <param name="entityManager">An EntityManager instance.</param>
        /// <returns>The shared component value.</returns>
        public readonly object GetSharedComponentDataBoxed(ref DynamicSharedComponentTypeHandle typeHandle, EntityManager entityManager)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(typeHandle.m_Safety);
#endif
            return entityManager.GetSharedComponentDataBoxed(
                GetSharedComponentIndex(ref typeHandle),
                typeHandle.m_TypeIndex);
        }
        /// <summary>
        /// Obsolete. Use <see cref="GetSharedComponentDataBoxed(ref Unity.Entities.DynamicSharedComponentTypeHandle,Unity.Entities.EntityManager)"/> instead.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetDynamicSharedComponentTypeHandle"/>.</param>
        /// <param name="entityManager">An EntityManager instance.</param>
        /// <returns>The shared component value.</returns>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly object GetSharedComponentDataBoxed(DynamicSharedComponentTypeHandle typeHandle, EntityManager entityManager)
        {
            return GetSharedComponentDataBoxed(ref typeHandle, entityManager);
        }

        /// <summary>
        /// Reports whether this chunk contains the specified component type.
        /// </summary>
        /// <remarks>When an <see cref="EntityQuery"/> includes optional components (using
        /// <see cref="EntityQueryBuilder.WithAny{T}()"/>), some chunks returned by the query may contain such components and some
        /// may not. Use this function to determine whether or not the current chunk contains one of these optional
        /// component types.</remarks>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetComponentTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.
        /// </param>
        /// <typeparam name="T">The data type of the component.</typeparam>
        /// <returns>True, if this chunk contains an array of the specified component type.</returns>
        public readonly bool Has<T>(ref ComponentTypeHandle<T> typeHandle)
            where T :
#if UNITY_DISABLE_MANAGED_COMPONENTS
            struct,
#endif
            IComponentData
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            , new()
#endif
        {
            var archetype = m_EntityComponentStore->GetArchetype(m_Chunk);
            if (Hint.Unlikely(typeHandle.m_LookupCache.Archetype != archetype))
            {
                typeHandle.m_LookupCache.Update(archetype, typeHandle.m_TypeIndex);
            }
            var typeIndexInArchetype = typeHandle.m_LookupCache.IndexInArchetype;
            return (typeIndexInArchetype != -1);
        }
        /// <summary>
        /// Obsolete. Use <see cref="Has{T}(ref Unity.Entities.ComponentTypeHandle{T})"/> instead.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetComponentTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.
        /// </param>
        /// <typeparam name="T">The data type of the component.</typeparam>
        /// <returns>True, if this chunk contains an array of the specified component type.</returns>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly bool Has<T>(ComponentTypeHandle<T> typeHandle)
            where T :
#if UNITY_DISABLE_MANAGED_COMPONENTS
        struct,
#endif
            IComponentData
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            , new()
#endif
        {
            return Has(ref typeHandle);
        }

        /// <summary>
        /// Reports whether this chunk contains the specified component type.
        /// </summary>
        /// <remarks>When an <see cref="EntityQuery"/> includes optional components (using
        /// <see cref="EntityQueryBuilder.WithAny{T}()"/>), some chunks returned by the query may contain such components and some
        /// may not. Use this function to determine whether or not the current chunk contains one of these optional
        /// component types. </remarks>
        /// <typeparam name="T">The data type of the component.</typeparam>
        /// <returns>True, if this chunk contains an array of the specified component type.</returns>
        public readonly bool Has<T>()
        {
            var typeIndexInArchetype = ChunkDataUtility.GetIndexInTypeArray(m_EntityComponentStore->GetArchetype(m_Chunk), TypeManager.GetTypeIndex<T>());
            return (typeIndexInArchetype != -1);
        }

        /// <summary>
        /// Reports whether this chunk contains the specified component type.
        /// </summary>
        /// <param name="typeHandle">Type handle for the component type to query.</param>
        /// <returns>True, if this chunk contains an array of the specified component type.</returns>
        public readonly bool Has(ref DynamicComponentTypeHandle typeHandle)
        {
            ChunkDataUtility.GetIndexInTypeArray(m_EntityComponentStore->GetArchetype(m_Chunk), typeHandle.m_TypeIndex, ref typeHandle.m_TypeLookupCache);
            return (typeHandle.m_TypeLookupCache != -1);
        }
        /// <summary>
        /// Obsolete. Use <see cref="Has(ref Unity.Entities.DynamicComponentTypeHandle)"/> instead.
        /// </summary>
        /// <param name="typeHandle">Type handle for the component type to query.</param>
        /// <returns>True, if this chunk contains an array of the specified component type.</returns>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly bool Has(DynamicComponentTypeHandle typeHandle)
        {
            return Has(ref typeHandle);
        }

        /// <summary>
        /// Reports whether this chunk contains a chunk component of the specified component type.
        /// </summary>
        /// <remarks>When an <see cref="EntityQuery"/> includes optional components used as chunk
        /// components (with <see cref="EntityQueryBuilder.WithAny{T}()"/>), some chunks returned by the query may have these chunk
        /// components and some may not. Use this function to determine whether or not the current chunk contains one of
        /// these optional component types as a chunk component.</remarks>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetComponentTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.
        /// </param>
        /// <typeparam name="T">The data type of the chunk component.</typeparam>
        /// <returns>True, if this chunk contains a chunk component of the specified type.</returns>
        /// <seealso cref="Has{T}(ref Unity.Entities.ComponentTypeHandle{T})"/>
        public readonly bool HasChunkComponent<T>(ref ComponentTypeHandle<T> typeHandle)
            where T : unmanaged, IComponentData
        {
            var metaChunkArchetype = m_EntityComponentStore->GetArchetype(m_Chunk)->MetaChunkArchetype;
            if (Hint.Unlikely(metaChunkArchetype == null))
                return false;
            // TODO(DOTS-5748): use type handle's LookupCache here
            var typeIndexInArchetype = ChunkDataUtility.GetIndexInTypeArray(metaChunkArchetype, typeHandle.m_TypeIndex);
            return (typeIndexInArchetype != -1);
        }
        /// <summary>
        /// Obsolete. Use <see cref="HasChunkComponent{T}(ref Unity.Entities.ComponentTypeHandle{T})"/> instead.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetComponentTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.
        /// </param>
        /// <typeparam name="T">The data type of the chunk component.</typeparam>
        /// <returns>True, if this chunk contains a chunk component of the specified type.</returns>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly bool HasChunkComponent<T>(ComponentTypeHandle<T> typeHandle)
            where T : unmanaged, IComponentData
        {
            return HasChunkComponent(ref typeHandle);
        }

        /// <summary>
        /// Reports whether this chunk contains a chunk component of the specified component type.
        /// </summary>
        /// <remarks>When an <see cref="EntityQuery"/> includes optional components used as chunk
        /// components (with <see cref="EntityQueryBuilder.WithAny{T}()"/>), some chunks returned by the query may have these chunk
        /// components and some may not. Use this function to determine whether or not the current chunk contains one of
        /// these optional component types as a chunk component.</remarks>
        /// <typeparam name="T">The data type of the chunk component.</typeparam>
        /// <returns>True, if this chunk contains a chunk component of the specified type.</returns>
        public readonly bool HasChunkComponent<T>()
            where T : unmanaged, IComponentData
        {
            var metaChunkArchetype = m_EntityComponentStore->GetArchetype(m_Chunk)->MetaChunkArchetype;
            if (Hint.Unlikely(metaChunkArchetype == null))
                return false;
            var typeIndexInArchetype = ChunkDataUtility.GetIndexInTypeArray(metaChunkArchetype, TypeManager.GetTypeIndex<T>());
            return (typeIndexInArchetype != -1);
        }

        /// <summary>
        /// Reports whether this chunk contains a shared component of the specified component type.
        /// </summary>
        /// <remarks>When an <see cref="EntityQuery"/> includes optional components used as shared
        /// components (with <see cref="EntityQueryBuilder.WithAny{T}()"/>), some chunks returned by the query may have these shared
        /// components and some may not. Use this function to determine whether or not the current chunk contains one of
        /// these optional component types as a shared component.</remarks>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetSharedComponentTypeHandle{T}"/>. Pass the object to a job
        /// using a public field you define as part of the job struct.
        /// </param>
        /// <typeparam name="T">The data type of the shared component.</typeparam>
        /// <returns>True, if this chunk contains a shared component of the specified type.</returns>
        public readonly bool Has<T>(SharedComponentTypeHandle<T> typeHandle)
            where T : struct, ISharedComponentData
        {
            var typeIndexInArchetype = ChunkDataUtility.GetIndexInTypeArray(m_EntityComponentStore->GetArchetype(m_Chunk), typeHandle.m_TypeIndex);
            return (typeIndexInArchetype != -1);
        }

        /// <summary>
        /// Reports whether this chunk contains a shared component of the specified component type.
        /// </summary>
        /// <remarks>When an <see cref="EntityQuery"/> includes optional components used as shared
        /// components (with <see cref="EntityQueryBuilder.WithAny{T}()"/>), some chunks returned by the query may have these shared
        /// components and some may not. Use this function to determine whether or not the current chunk contains one of
        /// these optional component types as a shared component.</remarks>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetDynamicSharedComponentTypeHandle"/>.</param>
        /// <returns>True, if this chunk contains a shared component of the specified type.</returns>
        public readonly bool Has(ref DynamicSharedComponentTypeHandle typeHandle)
        {
            ChunkDataUtility.GetIndexInTypeArray(m_EntityComponentStore->GetArchetype(m_Chunk), typeHandle.m_TypeIndex,
                ref typeHandle.m_cachedTypeIndexinArchetype);
            return (typeHandle.m_cachedTypeIndexinArchetype != -1);
        }
        /// <summary>
        /// Obsolete. Use <see cref="Has(ref DynamicSharedComponentTypeHandle)"/> instead.
        /// </summary>
        /// <remarks>When an <see cref="EntityQuery"/> includes optional components used as shared
        /// components (with <see cref="EntityQueryBuilder.WithAny{T}()"/>), some chunks returned by the query may have these shared
        /// components and some may not. Use this function to determine whether or not the current chunk contains one of
        /// these optional component types as a shared component.</remarks>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetDynamicSharedComponentTypeHandle"/>.</param>
        /// <returns>True, if this chunk contains a shared component of the specified type.</returns>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly bool Has(DynamicSharedComponentTypeHandle typeHandle)
        {
            return Has(ref typeHandle);
        }

        /// <summary>
        /// Reports whether this chunk contains a dynamic buffer containing the specified component type.
        /// </summary>
        /// <remarks>When an <see cref="EntityQuery"/> includes optional dynamic buffer types
        /// (with <see cref="EntityQueryBuilder.WithAny{T}()"/>), some chunks returned by the query may have these dynamic buffers
        /// components and some may not. Use this function to determine whether or not the current chunk contains one of
        /// these optional dynamic buffers.</remarks>
        /// <param name="bufferTypeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetBufferTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.</param>
        /// <typeparam name="T">The data type of the component stored in the dynamic buffer.</typeparam>
        /// <returns>True, if this chunk contains an array of the dynamic buffers containing the specified component type.</returns>
        public readonly bool Has<T>(ref BufferTypeHandle<T> bufferTypeHandle)
            where T : unmanaged, IBufferElementData
        {
            var archetype = m_EntityComponentStore->GetArchetype(m_Chunk);
            if (Hint.Unlikely(bufferTypeHandle.m_LookupCache.Archetype != archetype))
            {
                bufferTypeHandle.m_LookupCache.Update(archetype, bufferTypeHandle.m_TypeIndex);
            }
            var typeIndexInArchetype = bufferTypeHandle.m_LookupCache.IndexInArchetype;
            return (typeIndexInArchetype != -1);
        }
        /// <summary>
        /// Obsolete. Use <see cref="Has{T}(ref Unity.Entities.BufferTypeHandle{T})"/> instead.
        /// </summary>
        /// <param name="bufferTypeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetBufferTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.</param>
        /// <typeparam name="T">The data type of the component stored in the dynamic buffer.</typeparam>
        /// <returns>True, if this chunk contains an array of the dynamic buffers containing the specified component type.</returns>
        [Obsolete("The bufferTypeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly bool Has<T>(BufferTypeHandle<T> bufferTypeHandle)
            where T : unmanaged, IBufferElementData
        {
            return Has(ref bufferTypeHandle);
        }

        /// <summary>
        /// Provides a native array interface to components stored in this chunk.
        /// </summary>
        /// <remarks>The native array returned by this method references existing data, not a copy.</remarks>
        /// <remarks>For raw unsafe access to a chunk's component data, see <see cref="GetComponentDataPtrRO{T}(ref ComponentTypeHandle{T})"/>
        /// and <see cref="GetComponentDataPtrRW{T}(ref ComponentTypeHandle{T})"/>.</remarks>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetComponentTypeHandle{T}(bool)"/>. Pass the object to a job
        /// using a public field you define as part of the job struct.</param>
        /// <typeparam name="T">The data type of the component.</typeparam>
        /// <exception cref="ArgumentException">If you call this function on a "tag" component type (which is an empty
        /// component with no fields).</exception>
        /// <returns>A native array containing the components in the chunk.</returns>
        public readonly NativeArray<T> GetNativeArray<T>(ref ComponentTypeHandle<T> typeHandle)
            where T : unmanaged, IComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(typeHandle.m_Safety);
#endif
            var archetype = m_EntityComponentStore->GetArchetype(m_Chunk);
            byte* ptr = (typeHandle.IsReadOnly)
                ? ChunkDataUtility.GetOptionalComponentDataWithTypeRO(m_Chunk, archetype, 0, typeHandle.m_TypeIndex,
                    ref typeHandle.m_LookupCache)
                : ChunkDataUtility.GetOptionalComponentDataWithTypeRW(m_Chunk, archetype, 0, typeHandle.m_TypeIndex,
                    typeHandle.GlobalSystemVersion, ref typeHandle.m_LookupCache);
            if (Hint.Unlikely(ptr == null))
            {
                var emptyResult =
                    NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(null, 0, 0);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref emptyResult, typeHandle.m_Safety);
#endif
                return emptyResult;
            }
            var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, Count, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, typeHandle.m_Safety);
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(m_EntityComponentStore->m_RecordToJournal != 0) && !typeHandle.IsReadOnly)
                JournalAddRecordGetComponentDataRW(ref typeHandle, ptr, typeHandle.m_SizeInChunk * Count);
#endif

            return result;
        }
        /// <summary>
        /// Obsolete. Use <see cref="GetNativeArray{T}(ref ComponentTypeHandle{T})"/> instead.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetComponentTypeHandle{T}(bool)"/>. Pass the object to a job
        /// using a public field you define as part of the job struct.</param>
        /// <typeparam name="T">The data type of the component.</typeparam>
        /// <returns>A native array containing the components in the chunk.</returns>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly NativeArray<T> GetNativeArray<T>(ComponentTypeHandle<T> typeHandle)
            where T : unmanaged, IComponentData
        {
            return GetNativeArray(ref typeHandle);
        }


        /// <summary>
        /// Provides an unsafe read-only interface to array of Entities stored in this chunk.
        /// </summary>
        /// <param name="entityTypeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetEntityTypeHandle"/>. Pass the object to a job
        /// using a public field you define as part of the job struct.</param>
        /// <returns>A pointer to the component data stored in the chunk.</returns>
        public readonly Entity* GetEntityDataPtrRO(EntityTypeHandle entityTypeHandle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(entityTypeHandle.m_Safety);
#endif
            var archetype = m_EntityComponentStore->GetArchetype(m_Chunk);
            var buffer = m_Chunk.Buffer;
            var startOffset = archetype->Offsets[0];
            var result = buffer + startOffset;

            return (Entity*)result;
        }

        /// <summary>
        /// Provides an unsafe read-only interface to components stored in this chunk.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetComponentTypeHandle{T}(bool)"/>. Pass the object to a job
        /// using a public field you define as part of the job struct.</param>
        /// <typeparam name="T">The data type of the component.</typeparam>
        /// <exception cref="ArgumentException">If you call this function on a "tag" component type (which is an empty
        /// component with no fields).</exception>
        /// <returns>A pointer to the component data stored in the chunk. Returns null if the chunk's archetype does not include
        /// component type <typeparamref name="T"/>.</returns>
        public readonly T* GetComponentDataPtrRO<T>(ref ComponentTypeHandle<T> typeHandle)
            where T : unmanaged, IComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(typeHandle.m_Safety);
#endif

            // This updates the type handle's cache as a side effect, which will tell us if the archetype has the component
            // or not.
            void* ptr = ChunkDataUtility.GetOptionalComponentDataWithTypeRO(m_Chunk, m_EntityComponentStore->GetArchetype(m_Chunk), 0,
                typeHandle.m_TypeIndex, ref typeHandle.m_LookupCache);
            return (T*)ptr;
        }

        /// <summary>
        /// Provides an unsafe read/write interface to components stored in this chunk.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetComponentTypeHandle{T}(bool)"/>. Pass the object to a job
        /// using a public field you define as part of the job struct.</param>
        /// <typeparam name="T">The data type of the component.</typeparam>
        /// <exception cref="ArgumentException">If you call this function on a "tag" component type (which is an empty
        /// component with no fields).</exception>
        /// <exception cref="InvalidOperationException">If the provided type handle is read-only.</exception>
        /// <returns>A pointer to the component data stored in the chunk. Returns null if the chunk's archetype does not include
        /// component type <typeparamref name="T"/>.</returns>
        public readonly T* GetComponentDataPtrRW<T>(ref ComponentTypeHandle<T> typeHandle)
            where T : unmanaged, IComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(typeHandle.m_Safety);
#endif
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(typeHandle.IsReadOnly))
                throw new InvalidOperationException(
                    "Provided ComponentTypeHandle is read-only; can't get a read/write pointer to component data");
#endif

            byte* ptr = ChunkDataUtility.GetOptionalComponentDataWithTypeRW(m_Chunk, m_EntityComponentStore->GetArchetype(m_Chunk),
                0, typeHandle.m_TypeIndex,
                typeHandle.GlobalSystemVersion, ref typeHandle.m_LookupCache);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(m_EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecordGetComponentDataRW(ref typeHandle, ptr,
                    typeHandle.m_LookupCache.ComponentSizeOf * Count);
#endif

            return (T*)ptr;
        }

        /// <summary>
        /// Provides an unsafe read-only interface to components stored in this chunk. This variant assumes that the
        /// component is present in the chunk; use <see cref="GetComponentDataPtrRO{T}"/> in cases where the caller
        /// can't guarantee this.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetComponentTypeHandle{T}(bool)"/>. Pass the object to a job
        /// using a public field you define as part of the job struct.</param>
        /// <typeparam name="T">The data type of the component.</typeparam>
        /// <exception cref="ArgumentException">If you call this function on a "tag" component type (which is an empty
        /// component with no fields).</exception>
        /// <returns>A pointer to the component data stored in the chunk. Results are undefined if the chunk's archetype
        /// does not include component type <typeparamref name="T"/>.</returns>
        public readonly void* GetRequiredComponentDataPtrRO<T>(ref ComponentTypeHandle<T> typeHandle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(typeHandle.m_Safety);
#endif

            byte* ptr = ChunkDataUtility.GetComponentDataWithTypeRO(m_Chunk, m_EntityComponentStore->GetArchetype(m_Chunk), 0,
                typeHandle.m_TypeIndex, ref typeHandle.m_LookupCache);
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            // Must check this after computing the pointer, to make sure the cache is up to date
            if (Hint.Unlikely(typeHandle.m_LookupCache.IndexInArchetype == -1))
            {
                var typeName = typeHandle.m_TypeIndex.ToFixedString();
                throw new ArgumentException($"Required component {typeName} not found in archetype.");
            }
#endif
            return ptr;
        }

        /// <summary>
        /// Provides an unsafe read/write interface to components stored in this chunk. This variant assumes that the
        /// component is present in the chunk; use <see cref="GetComponentDataPtrRW{T}"/> in cases where the caller
        /// can't guarantee this.
        /// </summary>
        /// <param name="typeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetComponentTypeHandle{T}(bool)"/>. Pass the object to a job
        /// using a public field you define as part of the job struct.</param>
        /// <typeparam name="T">The data type of the component.</typeparam>
        /// <exception cref="ArgumentException">If you call this function on a "tag" component type (which is an empty
        /// component with no fields).</exception>
        /// <exception cref="InvalidOperationException">If the provided type handle is read-only.</exception>
        /// <returns>A pointer to the component data stored in the chunk. Returns null if the chunk's archetype does not include
        /// component type <typeparamref name="T"/>.</returns>
        public readonly void* GetRequiredComponentDataPtrRW<T>(ref ComponentTypeHandle<T> typeHandle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(typeHandle.m_Safety);
#endif
            byte* ptr = ChunkDataUtility.GetComponentDataWithTypeRW(m_Chunk, m_EntityComponentStore->GetArchetype(m_Chunk),
                0, typeHandle.m_TypeIndex,
                typeHandle.GlobalSystemVersion, ref typeHandle.m_LookupCache);
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(typeHandle.IsReadOnly))
                throw new InvalidOperationException(
                    "Provided ComponentTypeHandle is read-only; can't get a read/write pointer to component data");
            // Must check this after computing the pointer, to make sure the cache is up to date
            if (Hint.Unlikely(typeHandle.m_LookupCache.IndexInArchetype == -1))
            {
                var typeName = typeHandle.m_TypeIndex.ToFixedString();
                throw new ArgumentException($"Required component {typeName} not found in archetype.");
            }
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(m_EntityComponentStore->m_RecordToJournal != 0))
                JournalAddRecordGetComponentDataRW(ref typeHandle, ptr,
                    typeHandle.m_LookupCache.ComponentSizeOf * Count);
#endif

            return ptr;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckZeroSizedGetDynamicComponentDataArrayReinterpret(in DynamicComponentTypeHandle typeHandle)
        {
            if (Hint.Unlikely(typeHandle.IsZeroSized))
            {
                var typeName = typeHandle.m_TypeIndex.ToFixedString();
                throw new ArgumentException($"ArchetypeChunk.GetDynamicComponentDataArrayReinterpret<{typeName}> cannot be called on zero-sized IComponentData");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckComponentSizeMatches(in DynamicComponentTypeHandle typeHandle, int typeSize, int expectedTypeSize)
        {
            if (Hint.Unlikely(typeSize != expectedTypeSize))
            {
                var typeName = typeHandle.m_TypeIndex.ToFixedString();
                throw new InvalidOperationException($"Dynamic chunk component type {typeName} (size = {typeSize}) size does not equal {expectedTypeSize}. Component size must match with expectedTypeSize.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckCannotBeAliasedDueToSizeConstraints<T>(in DynamicComponentTypeHandle typeHandle, int outTypeSize, int outLength, int byteLen, int length)
        {
            if (Hint.Unlikely(outTypeSize * outLength != byteLen))
            {
                var typeName = typeHandle.m_TypeIndex.ToFixedString();
                throw new InvalidOperationException($"Dynamic chunk component type {TypeManager.GetType(typeHandle.m_TypeIndex)} (array length {length}) and {typeof(T)} cannot be aliased due to size constraints. The size of the types and lengths involved must line up.");
            }
        }

        /// <summary>
        /// Construct a NativeArray view of a chunk's component data.
        /// </summary>
        /// <param name="typeHandle">Type handle for the target component type</param>
        /// <param name="expectedTypeSize">The expected size (in bytes) of the target component type. It is an error to
        /// pass a size that does not match the target type's actual size.</param>
        /// <typeparam name="T">The target component type</typeparam>
        /// <returns>A NativeArray which aliases the chunk's component value array for type <typeparamref name="T"/>.
        /// The array does not own this data, and does not need to be disposed when it goes out of scope.</returns>
        /// <exception cref="ArgumentException">Thrown if <typeparamref name="T"/> is an <see cref="IBufferElementData"/>. Use <see cref="ArchetypeChunk.GetBufferAccessor{T}"/> instead.</exception>
        /// <exception cref="InvalidOperationException">Thrown if <paramref name="expectedTypeSize"/> does not match the actual size of <typeparamref name="T"/>,
        /// or if the data may not be safely aliased due to size constraints.</exception>
        public readonly NativeArray<T> GetDynamicComponentDataArrayReinterpret<T>(ref DynamicComponentTypeHandle typeHandle, int expectedTypeSize)
            where T : struct
        {
            CheckZeroSizedGetDynamicComponentDataArrayReinterpret(typeHandle);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(typeHandle.m_Safety0);
#endif
            var archetype = m_EntityComponentStore->GetArchetype(m_Chunk);
            ChunkDataUtility.GetIndexInTypeArray(archetype, typeHandle.m_TypeIndex, ref typeHandle.m_TypeLookupCache);
            var typeIndexInArchetype = typeHandle.m_TypeLookupCache;
            if (Hint.Unlikely(typeIndexInArchetype == -1))
            {
                var emptyResult =
                    NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(null, 0, 0);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref emptyResult, typeHandle.m_Safety0);
#endif
                return emptyResult;
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(archetype->Types[typeIndexInArchetype].IsBuffer))
            {
                var typeName = typeHandle.m_TypeIndex.ToFixedString();
                throw new ArgumentException($"ArchetypeChunk.GetDynamicComponentDataArrayReinterpret cannot be called for IBufferElementData {typeName}");
            }
#endif

            var typeSize = archetype->SizeOfs[typeIndexInArchetype];
            var length = Count;
            var byteLen = length * typeSize;
            var outTypeSize = UnsafeUtility.SizeOf<T>();
            var outLength = byteLen / outTypeSize;

            CheckComponentSizeMatches(typeHandle, typeSize, expectedTypeSize);
            CheckCannotBeAliasedDueToSizeConstraints<T>(typeHandle, outTypeSize, outLength, byteLen, length);

            byte* ptr = (typeHandle.IsReadOnly)
                ? ChunkDataUtility.GetComponentDataRO(m_Chunk, archetype, 0, typeIndexInArchetype)
                : ChunkDataUtility.GetComponentDataRW(m_Chunk, archetype, 0, typeIndexInArchetype, typeHandle.GlobalSystemVersion);
            var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, outLength, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, typeHandle.m_Safety0);
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(m_EntityComponentStore->m_RecordToJournal != 0) && !typeHandle.IsReadOnly)
                JournalAddRecordGetComponentDataRW(ref typeHandle, ptr, outTypeSize * outLength);
#endif

            return result;
        }

        //Method used by EntityBehaviour in motion
        internal readonly void* GetDynamicComponentDataPtr(ref DynamicComponentTypeHandle typeHandle)
        {
            CheckZeroSizedGetDynamicComponentDataArrayReinterpret(typeHandle);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(typeHandle.m_Safety0);
#endif
            var archetype = m_EntityComponentStore->GetArchetype(m_Chunk);
            ChunkDataUtility.GetIndexInTypeArray(archetype, typeHandle.m_TypeIndex, ref typeHandle.m_TypeLookupCache);
            var typeIndexInArchetype = typeHandle.m_TypeLookupCache;
            if (Hint.Unlikely(typeIndexInArchetype == -1))
            {
                return null;
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(archetype->Types[typeIndexInArchetype].IsBuffer))
            {
                var typeName = typeHandle.m_TypeIndex.ToFixedString();
                throw new ArgumentException($"ArchetypeChunk.GetDynamicComponentDataArrayReinterpret cannot be called for IBufferElementData {typeName}");
            }
#endif
            byte* ptr = (typeHandle.IsReadOnly)
                ? ChunkDataUtility.GetComponentDataRO(m_Chunk, archetype, 0, typeIndexInArchetype)
                : ChunkDataUtility.GetComponentDataRW(m_Chunk, archetype, 0, typeIndexInArchetype, typeHandle.GlobalSystemVersion);

            return ptr;
        }

        /// <summary>
        /// Obsolete. Use <see cref="GetDynamicComponentDataArrayReinterpret{T}(ref Unity.Entities.DynamicComponentTypeHandle,int)"/> instead.
        /// </summary>
        /// <param name="typeHandle">Type handle for the target component type</param>
        /// <param name="expectedTypeSize">The expected size (in bytes) of the target component type. It is an error to
        /// pass a size that does not match the target type's actual size.</param>
        /// <typeparam name="T">The target component type</typeparam>
        /// <returns>A NativeArray which aliases the chunk's component value array for type <typeparamref name="T"/>.
        /// The array does not own this data, and does not need to be disposed when it goes out of scope.</returns>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly NativeArray<T> GetDynamicComponentDataArrayReinterpret<T>(DynamicComponentTypeHandle typeHandle, int expectedTypeSize)
            where T : struct
        {
            return GetDynamicComponentDataArrayReinterpret<T>(ref typeHandle, expectedTypeSize);
        }

        /// <summary>
        /// Provides access to a chunk's array of component values for a specific managed component type.
        /// </summary>
        /// <param name="typeHandle">The type handle for the target component type.</param>
        /// <param name="manager">The EntityManager which owns this chunk.</param>
        /// <typeparam name="T">The target component type</typeparam>
        /// <returns>An interface to this chunk's component values for type <typeparamref name="T"/></returns>
        public readonly ManagedComponentAccessor<T> GetManagedComponentAccessor<T>(ref ComponentTypeHandle<T> typeHandle, EntityManager manager)
            where T : class
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(typeHandle.m_Safety);
#endif
            var archetype = m_EntityComponentStore->GetArchetype(m_Chunk);
            byte* ptr = ChunkDataUtility.GetOptionalComponentDataWithTypeRW(m_Chunk, archetype, 0,
                typeHandle.m_TypeIndex,
                typeHandle.GlobalSystemVersion, ref typeHandle.m_LookupCache);
            NativeArray<int> indexArray;
            if (Hint.Unlikely(ptr == null))
            {
                indexArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<int>(null, 0, 0);
            }
            else
            {
                var length = Count;
                indexArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<int>(ptr, length, Allocator.None);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
                if (Hint.Unlikely(m_EntityComponentStore->m_RecordToJournal != 0))
                    JournalAddRecordGetComponentObjectRW(ref typeHandle);
#endif
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref indexArray, typeHandle.m_Safety);
#endif

            return new ManagedComponentAccessor<T>(indexArray, manager);
        }
        /// <summary>
        /// Obsolete. Use <see cref="GetManagedComponentAccessor{T}(ref Unity.Entities.ComponentTypeHandle{T},Unity.Entities.EntityManager)"/> instead.
        /// </summary>
        /// <param name="typeHandle">The type handle for the target component type.</param>
        /// <param name="manager">The EntityManager which owns this chunk.</param>
        /// <typeparam name="T">The target component type</typeparam>
        /// <returns>An interface to this chunk's component values for type <typeparamref name="T"/></returns>
        [Obsolete("The typeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly ManagedComponentAccessor<T> GetManagedComponentAccessor<T>(ComponentTypeHandle<T> typeHandle, EntityManager manager)
            where T : class
        {
            return GetManagedComponentAccessor(ref typeHandle, manager);
        }

        /// <summary>
        /// Provides access to a chunk's array of component values for a specific buffer component type.
        /// </summary>
        /// <param name="bufferTypeHandle">The type handle for the target component type.</param>
        /// <typeparam name="T">The target component type, which must inherit <see cref="IBufferElementData"/>.</typeparam>
        /// <returns>An interface to this chunk's component values for type <typeparamref name="T"/></returns>
        public readonly BufferAccessor<T> GetBufferAccessor<T>(ref BufferTypeHandle<T> bufferTypeHandle)
            where T : unmanaged, IBufferElementData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(bufferTypeHandle.m_Safety0);
#endif
            var archetype = m_EntityComponentStore->GetArchetype(m_Chunk);
            var typeIndex = bufferTypeHandle.m_TypeIndex;
            if (Hint.Unlikely(bufferTypeHandle.m_LookupCache.Archetype != archetype))
            {
                bufferTypeHandle.m_LookupCache.Update(m_EntityComponentStore->GetArchetype(m_Chunk), typeIndex);
            }

            byte* ptr = (bufferTypeHandle.IsReadOnly)
                ? ChunkDataUtility.GetOptionalComponentDataWithTypeRO(m_Chunk, archetype, 0, typeIndex,
                    ref bufferTypeHandle.m_LookupCache)
                : ChunkDataUtility.GetOptionalComponentDataWithTypeRW(m_Chunk, archetype, 0, typeIndex,
                    bufferTypeHandle.GlobalSystemVersion, ref bufferTypeHandle.m_LookupCache);
            if (Hint.Unlikely(ptr == null))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return new BufferAccessor<T>(null, 0, 0, true, bufferTypeHandle.m_Safety0, bufferTypeHandle.m_Safety1, 0);
#else
                return new BufferAccessor<T>(null, 0, 0, 0);
#endif
            }
            int typeIndexInArchetype = bufferTypeHandle.m_LookupCache.IndexInArchetype;
            int internalCapacity = archetype->BufferCapacities[typeIndexInArchetype];
            var length = Count;
            int stride = bufferTypeHandle.m_LookupCache.ComponentSizeOf;

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(m_EntityComponentStore->m_RecordToJournal != 0) && !bufferTypeHandle.IsReadOnly)
                JournalAddRecordGetBufferRW(ref bufferTypeHandle);
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new BufferAccessor<T>(ptr, length, stride, bufferTypeHandle.IsReadOnly, bufferTypeHandle.m_Safety0, bufferTypeHandle.m_Safety1, internalCapacity);
#else
            return new BufferAccessor<T>(ptr, length, stride, internalCapacity);
#endif
        }
        /// <summary>
        /// Obsolete. Use <see cref="GetBufferAccessor{T}(ref Unity.Entities.BufferTypeHandle{T})"/> instead.
        /// </summary>
        /// <param name="bufferTypeHandle">The type handle for the target component type.</param>
        /// <typeparam name="T">The target component type, which must inherit <see cref="IBufferElementData"/>.</typeparam>
        /// <returns>An interface to this chunk's component values for type <typeparamref name="T"/></returns>
        [Obsolete("The bufferTypeHandle argument should now be passed by ref. (RemovedAfter Entities 1.0)", false)]
        public readonly BufferAccessor<T> GetBufferAccessor<T>(BufferTypeHandle<T> bufferTypeHandle)
            where T : unmanaged, IBufferElementData
        {
            return GetBufferAccessor(ref bufferTypeHandle);
        }

        /// <summary>
        /// Give unsafe access to the buffers with type <paramref name="chunkBufferTypeHandle"/> in the chunk.
        /// </summary>
        /// <param name="chunkBufferTypeHandle">An object containing type and job safety information. To create this
        /// object, call <see cref="ComponentSystemBase.GetBufferTypeHandle{T}"/>. Pass the object to a job using a
        /// public field you define as part of the job struct.</param>
        /// <returns>An interface to this chunk's component values for the target buffer component type.</returns>
        public readonly LowLevel.Unsafe.UnsafeUntypedBufferAccessor GetUntypedBufferAccessor(ref DynamicComponentTypeHandle chunkBufferTypeHandle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(chunkBufferTypeHandle.m_Safety0);
#endif
            var archetype = m_EntityComponentStore->GetArchetype(m_Chunk);
            short typeIndexInArchetype = chunkBufferTypeHandle.m_TypeLookupCache;
            ChunkDataUtility.GetIndexInTypeArray(archetype, chunkBufferTypeHandle.m_TypeIndex, ref typeIndexInArchetype);
            chunkBufferTypeHandle.m_TypeLookupCache = (short)typeIndexInArchetype;
            if (Hint.Unlikely(typeIndexInArchetype == -1))
            {
                return default(LowLevel.Unsafe.UnsafeUntypedBufferAccessor);
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Hint.Unlikely(!archetype->Types[typeIndexInArchetype].IsBuffer))
                throw new ArgumentException($"ArchetypeChunk.GetUntypedBufferAccessor must be called only for IBufferElementData types");
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            //Expect the safety to be set and valid
            AtomicSafetyHandle.CheckReadAndThrow(chunkBufferTypeHandle.m_Safety1);
#endif
            int internalCapacity = archetype->BufferCapacities[typeIndexInArchetype];
            var typeInfo = TypeManager.GetTypeInfo(chunkBufferTypeHandle.m_TypeIndex);
            byte* ptr = (chunkBufferTypeHandle.IsReadOnly)
                ? ChunkDataUtility.GetComponentDataRO(m_Chunk, archetype, 0, typeIndexInArchetype)
                : ChunkDataUtility.GetComponentDataRW(m_Chunk, archetype, 0, typeIndexInArchetype, chunkBufferTypeHandle.GlobalSystemVersion);

            var length = Count;
            int stride = archetype->SizeOfs[typeIndexInArchetype];
            int elementSize = typeInfo.ElementSize;
            int elementAlign = typeInfo.AlignmentInBytes;

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (Hint.Unlikely(m_EntityComponentStore->m_RecordToJournal != 0) && !chunkBufferTypeHandle.IsReadOnly)
                JournalAddRecordGetBufferRW(ref chunkBufferTypeHandle);
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new LowLevel.Unsafe.UnsafeUntypedBufferAccessor(ptr, length, stride, elementSize, elementAlign, internalCapacity,
                chunkBufferTypeHandle.IsReadOnly, chunkBufferTypeHandle.m_Safety0, chunkBufferTypeHandle.m_Safety1);
#else
            return new LowLevel.Unsafe.UnsafeUntypedBufferAccessor(ptr, length, stride, elementSize, elementAlign, internalCapacity);
#endif
        }

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
        [MethodImpl(MethodImplOptions.NoInlining)]
        readonly void JournalAddRecord(EntitiesJournaling.RecordType recordType, TypeIndex typeIndex, uint globalSystemVersion, void* data = null, int dataLength = 0)
        {
            fixed (ArchetypeChunk* archetypeChunk = &this)
            {
                EntitiesJournaling.AddRecord(
                    recordType: recordType,
                    entityComponentStore: archetypeChunk->m_EntityComponentStore,
                    globalSystemVersion: globalSystemVersion,
                    chunks: archetypeChunk,
                    chunkCount: 1,
                    types: &typeIndex,
                    typeCount: 1,
                    data: data,
                    dataLength: dataLength);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        readonly void JournalAddRecordGetComponentDataRW(ref DynamicComponentTypeHandle typeHandle, void* data, int dataLength) =>
            JournalAddRecord(EntitiesJournaling.RecordType.GetComponentDataRW, typeHandle.m_TypeIndex, typeHandle.m_GlobalSystemVersion, data, dataLength);

        internal readonly void JournalAddRecordGetComponentDataRW<T>(ref ComponentTypeHandle<T> typeHandle, void* data, int dataLength) =>
            JournalAddRecord(EntitiesJournaling.RecordType.GetComponentDataRW, typeHandle.m_TypeIndex, typeHandle.m_GlobalSystemVersion, data, dataLength);

        readonly void JournalAddRecordGetComponentObjectRW<T>(ref ComponentTypeHandle<T> typeHandle) where T : class =>
            JournalAddRecord(EntitiesJournaling.RecordType.GetComponentObjectRW, typeHandle.m_TypeIndex, typeHandle.m_GlobalSystemVersion);

        readonly void JournalAddRecordGetBufferRW<T>(ref BufferTypeHandle<T> typeHandle) where T : unmanaged, IBufferElementData =>
            JournalAddRecord(EntitiesJournaling.RecordType.GetBufferRW, typeHandle.m_TypeIndex, typeHandle.m_GlobalSystemVersion);

        [MethodImpl(MethodImplOptions.NoInlining)]
        readonly void JournalAddRecordGetBufferRW(ref DynamicComponentTypeHandle typeHandle) =>
            JournalAddRecord(EntitiesJournaling.RecordType.GetBufferRW, typeHandle.m_TypeIndex, typeHandle.m_GlobalSystemVersion);

        [MethodImpl(MethodImplOptions.NoInlining)]
        readonly void JournalAddRecordSetComponentEnabled(ref DynamicComponentTypeHandle typeHandle, bool value) =>
            JournalAddRecord(value ? EntitiesJournaling.RecordType.EnableComponent : EntitiesJournaling.RecordType.DisableComponent, typeHandle.m_TypeIndex, typeHandle.m_GlobalSystemVersion);

        readonly void JournalAddRecordSetComponentEnabled<T>(ref ComponentTypeHandle<T> typeHandle, bool value) =>
            JournalAddRecord(value ? EntitiesJournaling.RecordType.EnableComponent : EntitiesJournaling.RecordType.DisableComponent, typeHandle.m_TypeIndex, typeHandle.m_GlobalSystemVersion);

        readonly void JournalAddRecordSetComponentEnabled<T>(ref BufferTypeHandle<T> typeHandle, bool value) where T : unmanaged, IBufferElementData  =>
            JournalAddRecord(value ? EntitiesJournaling.RecordType.EnableComponent : EntitiesJournaling.RecordType.DisableComponent, typeHandle.m_TypeIndex, typeHandle.m_GlobalSystemVersion);
#endif
    }

    /// <summary>
    /// Wrapper around the header data for a specific chunk
    /// </summary>
    [ChunkSerializable]
    public struct ChunkHeader : ICleanupComponentData
    {
        /// <summary>
        /// Summary of the current chunk.
        /// </summary>
        public ArchetypeChunk ArchetypeChunk;

        /// <summary>
        /// Constructs a ChunkHeader representing an empty chunk.
        /// </summary>
        public static ChunkHeader Null => new();
    }

    /// <summary>
    /// Interface to a chunk's array of component values for a buffer component type
    /// </summary>
    /// <typeparam name="T">Buffer component type.</typeparam>
    [NativeContainer]
    public unsafe struct BufferAccessor<T>
        where T : unmanaged, IBufferElementData
    {
        [NativeDisableUnsafePtrRestriction]
        private byte* m_BasePointer;
        private int m_Length;
        private int m_Stride;
        private int m_InternalCapacity;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private byte m_IsReadOnly;
#endif

        /// <summary>
        /// The number of buffer elements
        /// </summary>
        public int Length => m_Length;


#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety0;
        private AtomicSafetyHandle m_ArrayInvalidationSafety;

        /// <summary>
        /// Reports whether this type handle was created in read-only mode.
        /// </summary>
        internal bool IsReadOnly => m_IsReadOnly == 1;

#pragma warning disable 0414 // assigned but its value is never used
        private int m_SafetyReadOnlyCount;
        private int m_SafetyReadWriteCount;
#pragma warning restore 0414

#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal BufferAccessor(byte* basePointer, int length, int stride, bool readOnly, AtomicSafetyHandle safety, AtomicSafetyHandle arrayInvalidationSafety, int internalCapacity)
        {
            m_BasePointer = basePointer;
            m_Length = length;
            m_Stride = stride;
            m_Safety0 = safety;
            m_ArrayInvalidationSafety = arrayInvalidationSafety;
            m_IsReadOnly = readOnly ? (byte)1u : (byte)0u;
            m_SafetyReadOnlyCount = readOnly ? 2 : 0;
            m_SafetyReadWriteCount = readOnly ? 0 : 2;
            m_InternalCapacity = internalCapacity;
        }

#else
        internal BufferAccessor(byte* basePointer, int length, int stride, int internalCapacity)
        {
            m_BasePointer = basePointer;
            m_Length = length;
            m_Stride = stride;
            m_InternalCapacity = internalCapacity;
        }

#endif

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private void AssertIndexInRange(int index)
        {
            if (Hint.Unlikely(index < 0 || index >= Length))
                throw new InvalidOperationException($"index {index} out of range in LowLevelBufferAccessor of length {Length}");
        }

        /// <summary>
        /// Look up the <see cref="DynamicBuffer{T}"/> value at a specific array index within the chunk.
        /// </summary>
        /// <param name="index">The index of the entity within the chunk whose value for <typeparamref name="T"/> should be returned.</param>
        /// <returns>The <see cref="DynamicBuffer{T}"/> value at a specific array index within the chunk</returns>
        public DynamicBuffer<T> this[int index]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety0);
#endif
                AssertIndexInRange(index);
                BufferHeader* hdr = (BufferHeader*)(m_BasePointer + index * m_Stride);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return new DynamicBuffer<T>(hdr, m_Safety0, m_ArrayInvalidationSafety, IsReadOnly, false, 0, m_InternalCapacity);
#else
                return new DynamicBuffer<T>(hdr, m_InternalCapacity);
#endif
            }
        }

        /// <summary>
        /// Returns the formatted FixedString "BufferAccessor[type_name_here]".
        /// </summary>
        /// <returns>Returns the formatted FixedString "BufferAccessor[type_name_here]".</returns>
        public FixedString512Bytes ToFixedString()
        {
            var fs = new FixedString128Bytes((FixedString32Bytes) "BufferAccessor[");
            fs.Append(TypeManager.GetTypeNameFixed(TypeManager.GetTypeIndex<T>()));
            fs.Append(']');
            return fs;
        }
    }

    namespace LowLevel.Unsafe
    {
        /// <summary>
        /// Allow untyped access to buffers data in a chunk. The use of untyped accessor is in general
        /// not recommended and should be exploited only in very specific use case scenario.
        /// </summary>
        public unsafe struct UnsafeUntypedBufferAccessor
        {
            [NativeDisableUnsafePtrRestriction]
            private byte* m_Pointer;
            private int m_InternalCapacity;
            private int m_Stride;
            private int m_Length;
            private int m_ElementSize;
            private int m_ElementAlign;

    #if ENABLE_UNITY_COLLECTIONS_CHECKS
            private AtomicSafetyHandle m_Safety0;
            private AtomicSafetyHandle m_ArrayInvalidationSafety;
    #pragma warning disable 0414 // assigned but its value is never used
            private int m_SafetyReadOnlyCount;
            private int m_SafetyReadWriteCount;
    #pragma warning restore 0414

    #endif

            /// <summary>
            /// The number of buffers in the chunk.
            /// </summary>
            public int Length => m_Length;
            /// <summary>
            /// The size (in bytes) of a single buffer element.
            /// </summary>
            public int ElementSize => m_ElementSize;

    #if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal UnsafeUntypedBufferAccessor(byte* basePointer, int length, int stride, int elementSize, int elementAlign,
                int internalCapacity, bool readOnly, AtomicSafetyHandle safety0, AtomicSafetyHandle arrayInvalidationSafety)
            {
                m_Pointer = basePointer;
                m_InternalCapacity = internalCapacity;
                m_ElementSize = elementSize;
                m_ElementAlign = elementAlign;
                m_Stride = stride;
                m_Length = length;
                m_Safety0 = safety0;
                m_ArrayInvalidationSafety = arrayInvalidationSafety;
                m_SafetyReadOnlyCount = readOnly ? 2 : 0;
                m_SafetyReadWriteCount = readOnly ? 0 : 2;
            }
    #else
            internal UnsafeUntypedBufferAccessor(byte* basePointer, int length, int stride, int elementSize, int elementAlign, int internalCapacity)
            {
                m_Pointer = basePointer;
                m_InternalCapacity = internalCapacity;
                m_ElementSize = elementSize;
                m_ElementAlign = elementAlign;
                m_Stride = stride;
                m_Length = length;
            }
    #endif
            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void CheckReadAccess()
            {
    #if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety0);
                AtomicSafetyHandle.CheckReadAndThrow(m_ArrayInvalidationSafety);
    #endif
            }
            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void CheckWriteAccess()
            {
    #if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety0);
                AtomicSafetyHandle.CheckWriteAndThrow(m_ArrayInvalidationSafety);
    #endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
            private void AssertIndexInRange(int index)
            {
                if (Hint.Unlikely(index < 0 || index >= m_Length))
                    throw new InvalidOperationException($"index {index} out of range in LowLevelBufferAccessor of length {Length}");
            }
            /// <summary>
            /// The unsafe pointer and length for the buffer at the given <paramref name="index"/> in the chunk
            /// </summary>
            /// <param name="index">The array index of the buffer to query</param>
            /// <param name="length">The buffer's length will be written here</param>
            /// <returns>The base pointer for the buffer at array index <paramref name="index"/></returns>
            /// <exception cref="InvalidOperationException">Thrown if <paramref name="index"/> is out of range</exception>
            public void* GetUnsafePtrAndLength(int index, out int length)
            {
                CheckWriteAccess();
                AssertIndexInRange(index);
                var hdr = (BufferHeader*)(m_Pointer + index * m_Stride);
                length = hdr->Length;
                return BufferHeader.GetElementPointer(hdr);
            }
            /// <summary>
            /// The read-only unsafe pointer and length for the buffer at the given <paramref name="index"/> in the chunk
            /// </summary>
            /// <param name="index">The array index of the buffer to query</param>
            /// <param name="length">The buffer's length will be written here</param>
            /// <returns>The base pointer for the buffer at array index <paramref name="index"/></returns>
            /// <exception cref="InvalidOperationException">Thrown if <paramref name="index"/> is out of range</exception>
            public void* GetUnsafeReadOnlyPtrAndLength(int index, out int length)
            {
                CheckReadAccess();
                AssertIndexInRange(index);
                var hdr = (BufferHeader*)(m_Pointer + index * m_Stride);
                length = hdr->Length;
                return BufferHeader.GetElementPointer(hdr);
            }
            /// <summary>
            /// Gets the unsafe pointer to buffer elements at the given <paramref name="index"/> in the chunk
            /// </summary>
            /// <param name="index">The array index of the buffer to query</param>
            /// <returns>The base pointer for the buffer at array index <paramref name="index"/></returns>
            /// <exception cref="InvalidOperationException">Thrown if <paramref name="index"/> is out of range</exception>
            public void* GetUnsafePtr(int index)
            {
                CheckWriteAccess();
                AssertIndexInRange(index);
                var hdr = (BufferHeader*)(m_Pointer + index * m_Stride);
                return BufferHeader.GetElementPointer(hdr);
            }

            /// <summary>
            /// Gets the read-only unsafe pointer to buffer elements at the given <paramref name="index"/> in the chunk
            /// </summary>
            /// <param name="index">The array index of the buffer to query</param>
            /// <returns>The base pointer for the buffer at array index <paramref name="index"/></returns>
            /// <exception cref="InvalidOperationException">Thrown if <paramref name="index"/> is out of range</exception>
            public void* GetUnsafeReadOnlyPtr(int index)
            {
                CheckReadAccess();
                AssertIndexInRange(index);
                var hdr = (BufferHeader*)(m_Pointer + index * m_Stride);
                return BufferHeader.GetElementPointer(hdr);
            }

            /// <summary>
            /// Gets the current size of the buffer at the given <paramref name="index"/> in the chunk
            /// </summary>
            /// <param name="index">The array index of the buffer to query</param>
            /// <returns>The length the buffer at array index <paramref name="index"/></returns>
            /// <exception cref="InvalidOperationException">Thrown if <paramref name="index"/> is out of range</exception>
            public int GetBufferLength(int index)
            {
                CheckReadAccess();
    #if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if(index >= m_Length)
                    throw new InvalidOperationException($"out of bound exception. Cannot get buffer length at index {index}");
    #endif
                var hdr = (BufferHeader*)(m_Pointer + index * m_Stride);
                return hdr->Length;
            }
            /// <summary>
            /// Gets the current capacity of the buffer at the given <paramref name="index"/> in the chunk
            /// </summary>
            /// <param name="index">The array index of the buffer to query</param>
            /// <returns>The capacity for the buffer at array index <paramref name="index"/></returns>
            /// <exception cref="InvalidOperationException">Thrown if <paramref name="index"/> is out of range</exception>
            public int GetBufferCapacity(int index)
            {
                CheckReadAccess();
    #if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if(index >= m_Length)
                    throw new InvalidOperationException($"out of bound exception. Cannot get buffer length at index {index}");
    #endif
                var hdr = (BufferHeader*)(m_Pointer + index * m_Stride);
                return hdr->Capacity;
            }
            /// <summary>
            /// Increases the buffer capacity and length of the buffer associated to the entity at the given
            /// <paramref name="index"/> in the chunk
            /// </summary>
            /// <remarks>If <paramref name="length"/> is less than the current
            /// length of the buffer at index <paramref name="index"/>, the length of the buffer is reduced while the
            /// capacity remains unchanged.</remarks>
            /// <param name="index">The array index of the buffer to query</param>
            /// <param name="length">the new length of the buffer</param>
            /// <exception cref="InvalidOperationException">Thrown if <paramref name="index"/> is out of range</exception>
            public void ResizeUninitialized(int index, int length)
            {
    #if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety0);
                AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_ArrayInvalidationSafety);
    #endif
                AssertIndexInRange(index);

                var headerPtr = (BufferHeader*)(m_Pointer + index * m_Stride);
                BufferHeader.EnsureCapacity(headerPtr, length, m_ElementSize , m_ElementAlign, BufferHeader.TrashMode.RetainOldData, false, 0);
                headerPtr->Length = length;
            }
        }
    }

    internal unsafe struct ArchetypeChunkArray
    {
        /// <summary>
        /// Helper function to compute the total number of entities in an array of chunks
        /// </summary>
        /// <param name="chunks">The array of chunks</param>
        /// <returns>This count ignores enableable components; it's just the raw chunk count for all chunks in the array.</returns>
        internal static int TotalEntityCountInChunksIgnoreFiltering(NativeArray<ArchetypeChunk> chunks)
        {
            int entityCount = 0;
            for (var i = 0; i < chunks.Length; i++)
            {
                entityCount += chunks[i].Count;
            }

            return entityCount;
        }
    }

    /// <summary>
    /// A handle to a specific component type, used to access an <see cref="ArchetypeChunk"/>'s component data in a job.
    /// </summary>
    /// <remarks>
    /// Passing a type handle to a job automatically registers the job as a reader
    /// or writer of that type, which allows the DOTS safety system to detect potential race conditions between concurrent
    /// jobs which access the same component type.
    ///
    /// To create a ComponentTypeHandle, use <see cref="ComponentSystemBase.GetComponentTypeHandle"/>. While type handles
    /// can be created just in time before they're used, it is more efficient to create them once during system creation,
    /// cache them in a private field on the system, and incrementally update them with
    /// <see cref="ComponentTypeHandle{T}.Update"/> just before use.
    ///
    /// If the component type is not known at compile time, use <seealso cref="DynamicComponentTypeHandle"/>.
    /// </remarks>
    /// <typeparam name="T">The component type</typeparam>
    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public struct ComponentTypeHandle<T>
    {
        internal LookupCache m_LookupCache;
        internal readonly TypeIndex m_TypeIndex;
        internal readonly int m_SizeInChunk;
        internal uint m_GlobalSystemVersion;
        internal readonly byte m_IsReadOnly;
        internal readonly byte m_IsZeroSized;
        /// <summary>The global system version for which this handle is valid.</summary>
        /// <remarks>Attempting to use this type handle with a different
        /// system version indicates that the handle is no longer valid; use the <see cref="Update(Unity.Entities.SystemBase)"/>
        /// method to incrementally update the version just before use.
        /// </remarks>
        public readonly uint GlobalSystemVersion => m_GlobalSystemVersion;

        /// <summary>
        /// Reports whether this type handle was created in read-only mode.
        /// </summary>
        public readonly bool IsReadOnly => m_IsReadOnly == 1;

        /// <summary>
        /// Reports whether this type will consume chunk space when used in an archetype.
        /// </summary>
        internal readonly bool IsZeroSized => m_IsZeroSized == 1;

#pragma warning disable 0414
        private readonly int m_Length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private readonly int m_MinIndex;
        private readonly int m_MaxIndex;
        internal AtomicSafetyHandle m_Safety;
#endif
#pragma warning restore 0414

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal ComponentTypeHandle(AtomicSafetyHandle safety, bool isReadOnly, uint globalSystemVersion)
#else
        internal ComponentTypeHandle(bool isReadOnly, uint globalSystemVersion)
#endif
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            var typeInfo = TypeManager.GetTypeInfo(typeIndex);

            m_Length = 1;
            m_TypeIndex = typeIndex;
            m_SizeInChunk = typeInfo.SizeInChunk;
            m_IsZeroSized = typeInfo.IsZeroSized ? (byte)1u : (byte)0u;
            m_GlobalSystemVersion = globalSystemVersion;
            m_IsReadOnly = isReadOnly ? (byte)1u : (byte)0u;
            m_LookupCache = new LookupCache();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_MinIndex = 0;
            m_MaxIndex = 0;
            m_Safety = safety;
#endif
        }

        /// <summary>
        /// When a ComponentTypeHandle is cached by a system across multiple system updates, calling this function
        /// inside the system's OnUpdate() method performs the minimal incremental updates necessary to make the
        /// type handle safe to use.
        /// </summary>
        /// <param name="state">The SystemState of the system on which this type handle is cached.</param>
        public unsafe void Update(ref SystemState state)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = state.m_DependencyManager->Safety.GetSafetyHandleForComponentTypeHandle(m_TypeIndex, IsReadOnly);
#endif
            m_GlobalSystemVersion = state.GlobalSystemVersion;
        }

        /// <summary>
        /// When a ComponentTypeHandle is cached by a system across multiple system updates, calling this function
        /// inside the system's OnUpdate() method performs the minimal incremental updates necessary to make the
        /// type handle safe to use.
        /// </summary>
        /// <param name="system">The system on which this type handle is cached.</param>
        public unsafe void Update(SystemBase system)
        {
            Update(ref *system.m_StatePtr);
        }

        /// <summary>
        /// Returns the formatted FixedString "ComponentTypeHandle[type_name_here]".
        /// </summary>
        /// <returns>Returns the formatted FixedString "ComponentTypeHandle[type_name_here]".</returns>
        public FixedString512Bytes ToFixedString()
        {
            var fs = new FixedString128Bytes((FixedString32Bytes) "ComponentTypeHandle[");
            fs.Append(TypeManager.GetTypeNameFixed(m_TypeIndex));
            fs.Append(']');
            return fs;
        }
    }

    /// <summary>
    /// A handle to a specific component type, used to access an <see cref="ArchetypeChunk"/>'s component data in a job.
    /// </summary>
    /// <remarks>
    /// Passing a type handle to a job automatically registers the job as a reader or writer of that type, which allows
    /// the DOTS safety system to detect potential race conditions between concurrent jobs which access the same component type.
    ///
    /// To create a DynamicComponentTypeHandle, use <see cref="ComponentSystemBase.GetDynamicComponentTypeHandle"/>. While type handles
    /// can be created just in time before they're used, it is more efficient to create them once during system creation,
    /// cache them in a private field on the system, and incrementally update them with
    /// <see cref="DynamicComponentTypeHandle.Update"/> just before use.
    ///
    /// If the component type is known at compile time, use <seealso cref="ComponentTypeHandle{T}"/>.
    /// </remarks>
    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public struct DynamicComponentTypeHandle
    {
        // NOTE: increasing the size of this struct will cause stack overflow issues in the Entities Graphics package
        // BurstCompatibleTypeArray type.
        internal readonly TypeIndex m_TypeIndex;
        internal  uint m_GlobalSystemVersion;
        internal readonly byte m_IsReadOnly;
        internal readonly byte m_IsZeroSized;
        internal short m_TypeLookupCache;

        /// <summary>The global system version for which this handle is valid.</summary>
        /// <remarks>Attempting to use this type handle with a different
        /// system version indicates that the handle is no longer valid; use the <see cref="Update(Unity.Entities.SystemBase)"/>
        /// method to incrementally update the version just before use.
        /// </remarks>
        public readonly uint GlobalSystemVersion => m_GlobalSystemVersion;
        /// <summary>
        /// Reports whether this type handle was created in read-only mode.
        /// </summary>
        public readonly bool IsReadOnly => m_IsReadOnly == 1;

        /// <summary>
        /// Reports whether this type will consume chunk space when used in an archetype.
        /// </summary>
        internal readonly bool IsZeroSized => m_IsZeroSized == 1;

#pragma warning disable 0414
        private readonly int m_Length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private readonly int m_MinIndex;
        private readonly int m_MaxIndex;
        internal  AtomicSafetyHandle m_Safety0;
        internal  AtomicSafetyHandle m_Safety1;
        internal readonly int m_SafetyReadOnlyCount;
        internal readonly int m_SafetyReadWriteCount;
#endif
#pragma warning restore 0414

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal DynamicComponentTypeHandle(ComponentType componentType, AtomicSafetyHandle safety0, AtomicSafetyHandle safety1, uint globalSystemVersion)
#else
        internal DynamicComponentTypeHandle(ComponentType componentType, uint globalSystemVersion)
#endif
        {
            m_Length = 1;
            m_TypeIndex = componentType.TypeIndex;
            m_IsZeroSized = TypeManager.GetTypeInfo(m_TypeIndex).IsZeroSized ? (byte) 1u : (byte) 0u;
            m_GlobalSystemVersion = globalSystemVersion;
            m_IsReadOnly = componentType.AccessModeType == ComponentType.AccessMode.ReadOnly ? (byte)1u : (byte)0u;
            m_TypeLookupCache = 0;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_MinIndex = 0;
            m_MaxIndex = 0;
            m_Safety0 = safety0;
            m_Safety1 = safety1;
            int numHandles = componentType.IsBuffer ? 2 : 1;
            m_SafetyReadOnlyCount = m_IsReadOnly == 1 ? numHandles : 0;
            m_SafetyReadWriteCount = m_IsReadOnly == 1 ? 0: numHandles;
#endif
        }

        /// <summary>
        /// When a DynamicComponentTypeHandle is cached by a system across multiple system updates, calling this function
        /// inside the system's OnUpdate() method performs the minimal incremental updates necessary to make the
        /// type handle safe to use.
        /// </summary>
        /// <param name="system">The system on which this type handle is cached.</param>
        public unsafe void Update(SystemBase system)
        {
            Update(ref *system.m_StatePtr);
        }

        /// <summary>
        /// When a DynamicComponentTypeHandle is cached by a system across multiple system updates, calling this function
        /// inside the system's OnUpdate() method performs the minimal incremental updates necessary to make the
        /// type handle safe to use.
        /// </summary>
        /// <param name="state">The SystemState of the system on which this type handle is cached.</param>
        public unsafe void Update(ref SystemState state)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety0 = state.m_DependencyManager->Safety.GetSafetyHandleForDynamicComponentTypeHandle(m_TypeIndex, IsReadOnly);
            int numHandles = IsReadOnly ? m_SafetyReadOnlyCount : m_SafetyReadWriteCount;
            if (numHandles > 1)
                m_Safety1 = state.m_DependencyManager->Safety.GetBufferHandleForBufferTypeHandle(m_TypeIndex);
#endif
            m_GlobalSystemVersion = state.GlobalSystemVersion;
        }

        /// <summary>
        /// Returns the formatted FixedString "DynamicComponentTypeHandle[type_name_here]".
        /// </summary>
        /// <returns>Returns the formatted FixedString "DynamicComponentTypeHandle[type_name_here]".</returns>
        public FixedString512Bytes ToFixedString()
        {
            var fs = new FixedString128Bytes((FixedString32Bytes) "DynamicComponentTypeHandle[");
            fs.Append(TypeManager.GetTypeNameFixed(m_TypeIndex));
            fs.Append(']');
            return fs;
        }

        /// <summary>
        /// API that allows user-code to fetch chunk component data as read-only (to avoid bumping change versions when unnecessary).
        /// </summary>
        /// <remarks>Note: This API is one-way. Making a RO DCTH RW is invalid due to job safety.</remarks>
        /// <returns>A RO version of this RW DCTH.</returns>
        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="InvalidOperationException">If the DCTH is already read-only.</exception>
        public DynamicComponentTypeHandle CopyToReadOnly()
        {
            if (IsReadOnly)
                throw new InvalidOperationException("Already RO!");

            var componentType = ComponentType.ReadOnly(m_TypeIndex);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new DynamicComponentTypeHandle(componentType, m_Safety0, m_Safety1, GlobalSystemVersion);
#else
            return new DynamicComponentTypeHandle(componentType, GlobalSystemVersion);
#endif
        }
    }

    /// <summary>
    /// A handle to a specific buffer component type, used to access an <see cref="ArchetypeChunk"/>'s component data in a job.
    /// </summary>
    /// <remarks>
    /// Passing a type handle to a job automatically registers the job as a reader or writer of that type, which allows
    /// the DOTS safety system to detect potential race conditions between concurrent jobs which access the same component type.
    ///
    /// To create a BufferTypeHandle, use <see cref="ComponentSystemBase.GetBufferTypeHandle{T}"/>. While type handles
    /// can be created just in time before they're used, it is more efficient to create them once during system creation,
    /// cache them in a private field on the system, and incrementally update them with
    /// <see cref="BufferTypeHandle{T}.Update"/> just before use.
    ///
    /// If the component type is not known at compile time, use <seealso cref="DynamicComponentTypeHandle"/>.
    /// </remarks>
    /// <typeparam name="T">The buffer element type</typeparam>
    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public struct BufferTypeHandle<T>
        where T : unmanaged, IBufferElementData
    {
        internal LookupCache m_LookupCache;
        internal readonly TypeIndex m_TypeIndex;
        internal uint               m_GlobalSystemVersion;
        internal readonly byte      m_IsReadOnly;

        /// <summary>The global system version for which this handle is valid.</summary>
        /// <remarks>Attempting to use this type handle with a different
        /// system version indicates that the handle is no longer valid; use the <see cref="Update(Unity.Entities.SystemBase)"/>
        /// method to incrementally update the version just before use.
        /// </remarks>
        public uint GlobalSystemVersion => m_GlobalSystemVersion;
        /// <summary>
        /// Reports whether this type handle was created in read-only mode.
        /// </summary>
        public bool IsReadOnly => m_IsReadOnly == 1;

#pragma warning disable 0414
        private readonly int m_Length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private readonly int m_MinIndex;
        private readonly int m_MaxIndex;

        internal AtomicSafetyHandle m_Safety0;
        internal AtomicSafetyHandle m_Safety1;
        internal int m_SafetyReadOnlyCount;
        internal int m_SafetyReadWriteCount;
#endif
#pragma warning restore 0414

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal BufferTypeHandle(AtomicSafetyHandle safety, AtomicSafetyHandle arrayInvalidationSafety, bool isReadOnly, uint globalSystemVersion)
#else
        internal BufferTypeHandle(bool isReadOnly, uint globalSystemVersion)
#endif
        {
            m_LookupCache = new LookupCache();
            m_Length = 1;
            m_TypeIndex = TypeManager.GetTypeIndex<T>();
            m_GlobalSystemVersion = globalSystemVersion;
            m_IsReadOnly = isReadOnly ? (byte) 1u : (byte) 0u;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_MinIndex = 0;
            m_MaxIndex = 0;
            m_Safety0 = safety;
            m_Safety1 = arrayInvalidationSafety;
            m_SafetyReadOnlyCount = isReadOnly ? 2 : 0;
            m_SafetyReadWriteCount = isReadOnly ? 0 : 2;
#endif
        }

        /// <summary>
        /// When a BufferTypeHandle is cached by a system across multiple system updates, calling this function
        /// inside the system's OnUpdate() method performs the minimal incremental updates necessary to make the
        /// type handle safe to use.
        /// </summary>
        /// <param name="system">The system on which this type handle is cached.</param>
        public unsafe void Update(SystemBase system)
        {
            Update(ref *system.m_StatePtr);
        }

        /// <summary>
        /// When a BufferTypeHandle is cached by a system across multiple system updates, calling this function
        /// inside the system's OnUpdate() method performs the minimal incremental updates necessary to make the
        /// type handle safe to use.
        /// </summary>
        /// <param name="state">The SystemState of the system on which this type handle is cached.</param>
        unsafe public void Update(ref SystemState state)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety0 = state.m_DependencyManager->Safety.GetSafetyHandleForBufferTypeHandle(m_TypeIndex, IsReadOnly);
            m_Safety1 = state.m_DependencyManager->Safety.GetBufferHandleForBufferTypeHandle(m_TypeIndex);
#endif
            m_GlobalSystemVersion = state.m_EntityComponentStore->GlobalSystemVersion;
        }

        /// <summary>
        /// Returns the formatted FixedString "BufferTypeHandle[type_name_here]".
        /// </summary>
        /// <returns>Returns the formatted FixedString "BufferTypeHandle[type_name_here]".</returns>
        public FixedString512Bytes ToFixedString()
        {
            var fs = new FixedString128Bytes((FixedString32Bytes) "BufferTypeHandle[");
            fs.Append(TypeManager.GetTypeNameFixed(m_TypeIndex));
            fs.Append(']');
            return fs;
        }
    }

    /// <summary>
    /// A handle to a specific shared component type, used to access an <see cref="ArchetypeChunk"/>'s component data in a job.
    /// </summary>
    /// <remarks>
    /// Passing a type handle to a job automatically registers the job as a reader or writer of that type, which allows
    /// the DOTS safety system to detect potential race conditions between concurrent jobs which access the same component type.
    ///
    /// To create a SharedComponentTypeHandle, use <see cref="ComponentSystemBase.GetSharedComponentTypeHandle{T}"/>. While type handles
    /// can be created just in time before they're used, it is more efficient to create them once during system creation,
    /// cache them in a private field on the system, and incrementally update them with
    /// <see cref="SharedComponentTypeHandle{T}.Update"/> just before use.
    ///
    /// If the component type is not known at compile time, use <seealso cref="DynamicSharedComponentTypeHandle"/>.
    /// </remarks>
    /// <typeparam name="T">Shared component type.</typeparam>
    [NativeContainer]
    [NativeContainerIsReadOnly]
    public struct SharedComponentTypeHandle<T>
        where T : struct, ISharedComponentData
    {
        internal readonly TypeIndex m_TypeIndex;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
#endif
#pragma warning restore 0414

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal SharedComponentTypeHandle(AtomicSafetyHandle safety)
#else
        internal unsafe SharedComponentTypeHandle(bool unused)
#endif
        {
            m_TypeIndex = TypeManager.GetTypeIndex<T>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = safety;
#endif
        }

        /// <summary>
        /// When a SharedComponentTypeHandle is cached by a system across multiple system updates, calling this function
        /// inside the system's OnUpdate() method performs the minimal incremental updates necessary to make the
        /// type handle safe to use.
        /// </summary>
        /// <param name="system">The system on which this type handle is cached.</param>
        public unsafe void Update(SystemBase system)
        {
            Update(ref *system.m_StatePtr);
        }

        /// <summary>
        /// When a SharedComponentTypeHandle is cached by a system across multiple system updates, calling this function
        /// inside the system's OnUpdate() method performs the minimal incremental updates necessary to make the
        /// type handle safe to use.
        /// </summary>
        /// <param name="state">The SystemState of the system on which this type handle is cached.</param>
        public unsafe void Update(ref SystemState state)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = state.m_DependencyManager->Safety.GetSafetyHandleForSharedComponentTypeHandle(m_TypeIndex);
#endif
        }

        /// <summary>
        /// Returns the formatted FixedString "SharedComponentTypeHandle[type_name_here]".
        /// </summary>
        /// <returns>Returns the formatted FixedString "SharedComponentTypeHandle[type_name_here]".</returns>
        public FixedString512Bytes ToFixedString()
        {
            var fs = new FixedString128Bytes((FixedString32Bytes) "SharedComponentTypeHandle[");
            fs.Append(TypeManager.GetTypeNameFixed(m_TypeIndex));
            fs.Append(']');
            return fs;
        }
    }

    /// <summary>
    /// A handle to a specific shared component type, used to access an <see cref="ArchetypeChunk"/>'s component data in a job.
    /// </summary>
    /// <remarks>
    /// Passing a type handle to a job automatically registers the job as a reader or writer of that type, which allows
    /// the DOTS safety system to detect potential race conditions between concurrent jobs which access the same component type.
    ///
    /// To create a DynamicSharedComponentTypeHandle, use <see cref="ComponentSystemBase.GetDynamicSharedComponentTypeHandle"/>. While type handles
    /// can be created just in time before they're used, it is more efficient to create them once during system creation,
    /// cache them in a private field on the system, and incrementally update them with
    /// <see cref="DynamicSharedComponentTypeHandle.Update"/> just before use.
    ///
    /// If the component type is known at compile time, use <seealso cref="SharedComponentTypeHandle{T}"/>.
    /// </remarks>
    [NativeContainer]
    [NativeContainerIsReadOnly]
    public struct DynamicSharedComponentTypeHandle
    {
        internal readonly TypeIndex m_TypeIndex;
        internal short m_cachedTypeIndexinArchetype;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
#endif
#pragma warning restore 0414

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal DynamicSharedComponentTypeHandle(ComponentType componentType, AtomicSafetyHandle safety)
#else
        internal unsafe DynamicSharedComponentTypeHandle(ComponentType componentType)
#endif
        {
            m_TypeIndex = componentType.TypeIndex;
            m_cachedTypeIndexinArchetype = -1;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = safety;
#endif
        }

        /// <summary>
        /// When a DynamicSharedComponentTypeHandle is cached by a system across multiple system updates, calling this function
        /// inside the system's OnUpdate() method performs the minimal incremental updates necessary to make the
        /// type handle safe to use.
        /// </summary>
        /// <param name="system">The system on which this type handle is cached.</param>
        public unsafe void Update(SystemBase system)
        {
            Update(ref *system.m_StatePtr);
        }

        /// <summary>
        /// When a DynamicSharedComponentTypeHandle is cached by a system across multiple system updates, calling this function
        /// inside the system's OnUpdate() method performs the minimal incremental updates necessary to make the
        /// type handle safe to use.
        /// </summary>
        /// <param name="state">The SystemState of the system on which this type handle is cached.</param>
        public unsafe void Update(ref SystemState state)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = state.m_DependencyManager->Safety.GetSafetyHandleForSharedComponentTypeHandle(m_TypeIndex);
#endif
        }

        /// <summary>
        /// Returns the formatted FixedString "DynamicSharedComponentTypeHandle[type_name_here]".
        /// </summary>
        /// <returns>Returns the formatted FixedString "DynamicSharedComponentTypeHandle[type_name_here]".</returns>
        public FixedString512Bytes ToFixedString()
        {
            var fs = new FixedString128Bytes((FixedString32Bytes) "DynamicSharedComponentTypeHandle[");
            fs.Append(TypeManager.GetTypeNameFixed(m_TypeIndex));
            fs.Append(']');
            return fs;
        }
    }

    /// <summary>
    /// A handle to the <see cref="Entity"/> component type, used to access an <see cref="ArchetypeChunk"/>'s entities in a job.
    /// </summary>
    /// <remarks>
    /// Passing a type handle to a job automatically registers the job as a reader or writer of that type, which allows
    /// the DOTS safety system to detect potential race conditions between concurrent jobs which access the same component type.
    ///
    /// To create a EntityTypeHandle, use <see cref="ComponentSystemBase.GetEntityTypeHandle"/>. While type handles
    /// can be created just in time before they're used, it is more efficient to create them once during system creation,
    /// cache them in a private field on the system, and incrementally update them with
    /// <see cref="EntityTypeHandle.Update"/> just before use.
    /// </remarks>
    [NativeContainer]
    [NativeContainerIsReadOnly]
    public struct EntityTypeHandle
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal EntityTypeHandle(AtomicSafetyHandle safety)
#else
        internal unsafe EntityTypeHandle(bool unused)
#endif
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = safety;
#endif
        }

        /// <summary>
        /// When a EntityTypeHandle is cached by a system across multiple system updates, calling this function
        /// inside the system's OnUpdate() method performs the minimal incremental updates necessary to make the
        /// type handle safe to use.
        /// </summary>
        /// <param name="system">The system on which this type handle is cached.</param>
        public unsafe void Update(SystemBase system)
        {
            Update(ref *system.m_StatePtr);
        }

        /// <summary>
        /// When a EntityTypeHandle is cached by a system across multiple system updates, calling this function
        /// inside the system's OnUpdate() method performs the minimal incremental updates necessary to make the
        /// type handle safe to use.
        /// </summary>
        /// <param name="state">The SystemState of the system on which this type handle is cached.</param>
        public unsafe void Update(ref SystemState state)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = state.m_DependencyManager->Safety.GetSafetyHandleForEntityTypeHandle();
#endif
        }


        /// <summary>
        /// Returns "EntityTypeHandle".
        /// </summary>
        /// <returns>Returns "EntityTypeHandle".</returns>
        public FixedString128Bytes ToFixedString() => "EntityTypeHandle";
    }

    /// <summary>
    /// Interface to a chunk's array of component values for managed component type <typeparamref name="T"/>
    /// </summary>
    /// <typeparam name="T">The target component type</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ManagedComponentAccessor<T>
        where T : class
    {
        NativeArray<int> m_IndexArray;
        EntityComponentStore* m_EntityComponentStore;
        ManagedComponentStore m_ManagedComponentStore;

        unsafe internal ManagedComponentAccessor(NativeArray<int> indexArray, EntityManager entityManager)
        {
            var access = entityManager.GetCheckedEntityDataAccess();
            var ecs = access->EntityComponentStore;
            var mcs = access->ManagedComponentStore;

            m_IndexArray = indexArray;
            m_EntityComponentStore = ecs;
            m_ManagedComponentStore = mcs;
        }

        /// <summary>
        /// Access an element by index
        /// </summary>
        /// <param name="index">The array index of the buffer to query</param>
        /// <returns>The value of component <typeparamref name="T"/> at array index <paramref name="index"/></returns>
        /// <exception cref="InvalidOperationException">Thrown if <paramref name="index"/> is out of range</exception>
        public unsafe T this[int index]
        {
            get
            {
                // Direct access to the m_ManagedComponentData for fast iteration
                // we can not cache m_ManagedComponentData directly since it can be reallocated
                return (T)m_ManagedComponentStore.m_ManagedComponentData[m_IndexArray[index]];
            }

            set
            {
                var iManagedComponent = m_IndexArray[index];
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (Hint.Unlikely(value != null && typeof(T) != value.GetType()))
                    throw new ArgumentException($"Assigning component value is of type: {value.GetType()} but the expected component type is: {typeof(T)}");

#endif
                m_ManagedComponentStore.UpdateManagedComponentValue(&iManagedComponent, value, ref *m_EntityComponentStore);
                m_IndexArray[index] = iManagedComponent;
            }
        }

        /// <summary>
        /// The number of elements in this accessor
        /// </summary>
        public int Length => m_IndexArray.Length;

        /// <summary>
        /// Returns the formatted string "ManagedComponentAccessor[type_name_here]".
        /// </summary>
        /// <returns>Returns the formatted string "ManagedComponentAccessor[type_name_here]".</returns>
        public override string ToString()
        {
            return $"ManagedComponentAccessor[{typeof(T).FullName}]";
        }
    }
}
