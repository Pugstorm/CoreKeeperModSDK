using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Assertions;
using Unity.Burst;

namespace Unity.Entities
{
    /// <summary>
    /// An interface for managed and unmanaged shared component types to inherit from. Whenever
    /// a IRefCounted shared component is added to a world, its Retain() method will be invoked. Similarly,
    /// when removed from a world, its Release() method will be invoked. This interface can be used to safely manage
    /// the lifetime of a shared component whose instance data is shared between multiple worlds.
    /// </summary>
    public interface IRefCounted
    {
        /// <summary>
        /// Delegate method used for invoking Retain() and Release() member functions from Burst compiled code.
        /// </summary>
        public delegate void RefCountDelegate(IntPtr _this);

        /// <summary>
        /// Called when a world has a new instance of a IRefCounted type added to it.
        /// </summary>
        void Retain();

        /// <summary>
        /// Called when a world has the last instance of a IRefCounted type removed from it.
        /// </summary>
        void Release();
    }

    internal unsafe class ManagedComponentStore
    {
        readonly EntityComponentStore* m_EntityComponentStore;
        readonly ManagedObjectClone m_ManagedObjectClone = new ManagedObjectClone();
        readonly ManagedObjectRemap m_ManagedObjectRemap = new ManagedObjectRemap();

        UnsafeParallelMultiHashMap<ulong, int> m_HashLookup = new UnsafeParallelMultiHashMap<ulong, int>(128, Allocator.Persistent);

        List<object> m_SharedComponentData = new List<object>();

        struct SharedComponentInfo
        {
            public int       RefCount;
            public TypeIndex ComponentType;
            public int       Version;
            public int       HashCode;
        }

        UnsafeList<SharedComponentInfo> m_SharedComponentInfo = new UnsafeList<SharedComponentInfo>(0, Allocator.Persistent);
        internal object[] m_ManagedComponentData = new object[64];

        public void SetManagedComponentCapacity(int newCapacity)
        {
            Assert.IsTrue(m_ManagedComponentData.Length < newCapacity);
            Array.Resize(ref m_ManagedComponentData, newCapacity);
        }

        public object GetManagedComponent(int index)
        {
            return m_ManagedComponentData[index];
        }

        public object Debugger_GetManagedComponent(int index)
        {
            if (index < 0 || index >= m_ManagedComponentData.Length)
                return null;
            return m_ManagedComponentData[index];
        }

        private SharedComponentInfo* SharedComponentInfoPtr
        {
            get { return m_SharedComponentInfo.Ptr; }
        }

        int m_FreeListIndex;

        internal delegate void InstantiateCompanionComponentDelegate(int* srcArray, int componentCount, Entity* dstEntities, int* dstComponentReferenceIndices, int* dstComponentLinkIds, int* dstArray, int instanceCount, ManagedComponentStore managedComponentStore);
        internal static InstantiateCompanionComponentDelegate InstantiateCompanionComponent;

        internal delegate void AssignCompanionComponentsToCompanionGameObjectsDelegate(EntityManager entityManager, NativeArray<Entity> entities);
        internal static AssignCompanionComponentsToCompanionGameObjectsDelegate AssignCompanionComponentsToCompanionGameObjects;

        private sealed class ManagedComponentStoreKeyContext
        {
        }

        private sealed class CompanionReferenceTypeIndexStatic
        {
            public static readonly SharedStatic<TypeIndex> Ref = SharedStatic<TypeIndex>.GetOrCreate<ManagedComponentStoreKeyContext, CompanionReferenceTypeIndexStatic>();
        }

        public static TypeIndex CompanionReferenceTypeIndex
        {
            get => CompanionReferenceTypeIndexStatic.Ref.Data;
            set => CompanionReferenceTypeIndexStatic.Ref.Data = value;
        }

        private sealed class CompanionLinkTypeIndexStatic
        {
            public static readonly SharedStatic<TypeIndex> Ref = SharedStatic<TypeIndex>.GetOrCreate<ManagedComponentStoreKeyContext, CompanionLinkTypeIndexStatic>();
        }

        public static TypeIndex CompanionLinkTypeIndex
        {
            get => CompanionLinkTypeIndexStatic.Ref.Data;
            set => CompanionLinkTypeIndexStatic.Ref.Data = value;
        }

        private sealed class CompanionLinkTransformTypeIndexStatic
        {
            public static readonly SharedStatic<TypeIndex> Ref = SharedStatic<TypeIndex>.GetOrCreate<ManagedComponentStoreKeyContext, CompanionLinkTransformTypeIndexStatic>();
        }

        public static TypeIndex CompanionLinkTransformTypeIndex
        {
            get => CompanionLinkTransformTypeIndexStatic.Ref.Data;
            set => CompanionLinkTransformTypeIndexStatic.Ref.Data = value;
        }

        public ManagedComponentStore(EntityComponentStore* entityComponentStore)
        {
            m_EntityComponentStore = entityComponentStore;
            ResetSharedComponentData();
        }

        public void Dispose()
        {
            for (var i = 1; i != m_SharedComponentData.Count; i++)
                (m_SharedComponentData[i] as IRefCounted)?.Release();

            for (var i = 0; i != m_ManagedComponentData.Length; i++)
                DisposeManagedComponentData(m_ManagedComponentData[i]);

            m_SharedComponentInfo.Dispose();
            m_SharedComponentData.Clear();
            m_SharedComponentData = null;
            m_HashLookup.Dispose();
        }

        void ResetSharedComponentData()
        {
            m_HashLookup.Clear();
            m_SharedComponentData.Clear();
            m_SharedComponentInfo.Clear();

            m_SharedComponentData.Add(null);
            m_SharedComponentInfo.Add(new SharedComponentInfo { RefCount = 1, ComponentType = TypeIndex.Null, Version = 1, HashCode = 0});
            m_FreeListIndex = -1;
        }

        public void GetAllUniqueSharedComponents_Managed<T>(List<T> sharedComponentValues)
            where T : struct, ISharedComponentData
        {
            sharedComponentValues.Add(default(T));
            int n = m_SharedComponentInfo.Length;
            var infos = SharedComponentInfoPtr;
            var targetType = TypeManager.GetTypeIndex<T>();
            for (var i = 1; i != n; i++)
            {
                if (infos[i].ComponentType == targetType)
                {
                    object data = m_SharedComponentData[i];
                    if (data != null)
                        sharedComponentValues.Add((T)data);
                }
            }
        }

        public void GetAllUniqueSharedComponents_Managed<T>(List<T> sharedComponentValues, List<int> sharedComponentIndices)
            where T : struct, ISharedComponentData
        {
            sharedComponentValues.Add(default(T));
            sharedComponentIndices.Add(0);
            int n = m_SharedComponentInfo.Length;
            var infos = SharedComponentInfoPtr;
            var targetType = TypeManager.GetTypeIndex<T>();
            for (var i = 1; i != n; i++)
            {
                if (infos[i].ComponentType == targetType)
                {
                    object data = m_SharedComponentData[i];
                    if (data != null)
                    {
                        sharedComponentValues.Add((T)data);
                        sharedComponentIndices.Add(i);
                    }
                }
            }
        }

        public void GetAllUniqueSharedComponents_Managed<T>(List<T> sharedComponentValues, List<int> sharedComponentIndices, List<int> sharedComponentVersions)
            where T : struct, ISharedComponentData
        {
            sharedComponentValues.Add(default(T));
            sharedComponentIndices.Add(0);
            sharedComponentVersions.Add(0);
            int n = m_SharedComponentInfo.Length;
            var infos = SharedComponentInfoPtr;
            var targetType = TypeManager.GetTypeIndex<T>();
            for (var i = 1; i != n; i++)
            {
                if (infos[i].ComponentType == targetType)
                {
                    object data = m_SharedComponentData[i];
                    if (data != null)
                    {
                        sharedComponentValues.Add((T)data);
                        sharedComponentIndices.Add(i);
                        sharedComponentVersions.Add(infos[i].Version);
                    }
                }
            }
        }

        public int GetSharedComponentCount()
        {
            return m_SharedComponentData.Count;
        }

        public int InsertSharedComponent_Managed<T>(T newData) where T : struct
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            var index = FindSharedComponentIndex(TypeManager.GetTypeIndex<T>(), newData);

            if (index == 0) return 0;

            if (index != -1)
            {
                SharedComponentInfoPtr[index].RefCount++;
                return index;
            }

            var hashcode = TypeManager.GetHashCode<T>(ref newData);

            object newDataObj = newData;

            (newDataObj as IRefCounted)?.Retain();
            return Add(typeIndex, hashcode, newDataObj);
        }

        private int FindSharedComponentIndex<T>(TypeIndex typeIndex, T newData) where T : struct
        {
            var defaultVal = default(T);
            if (TypeManager.Equals(ref defaultVal, ref newData))
                return 0;

            return FindNonDefaultSharedComponentIndex(typeIndex, TypeManager.GetHashCode(ref newData), ref newData);
        }

        private int FindNonDefaultSharedComponentIndex<T>(TypeIndex typeIndex, int hashCode, ref T newData) where T : struct
        {
            int itemIndex;
            NativeParallelMultiHashMapIterator<ulong> iter;

            if (!m_HashLookup.TryGetFirstValue(EntityComponentStore.GetSharedComponentHashKey(typeIndex, hashCode), out itemIndex, out iter))
                return -1;

            var infos = SharedComponentInfoPtr;
            do
            {
                var data = m_SharedComponentData[itemIndex];
                if (data != null && infos[itemIndex].ComponentType == typeIndex)
                {
                    var inst = (T) data;
                    if (TypeManager.Equals(ref inst, ref newData))
                        return itemIndex;
                }
            }
            while (m_HashLookup.TryGetNextValue(out itemIndex, ref iter));

            return -1;
        }

        private int FindNonDefaultSharedComponentIndex(TypeIndex typeIndex, int hashCode, object newData)
        {
            int itemIndex;
            NativeParallelMultiHashMapIterator<ulong> iter;

            if (!m_HashLookup.TryGetFirstValue(EntityComponentStore.GetSharedComponentHashKey(typeIndex, hashCode),
                    out itemIndex,
                    out iter))
                return -1;

            var infos = SharedComponentInfoPtr;
            do
            {
                var data = m_SharedComponentData[itemIndex];
                if (data != null && infos[itemIndex].ComponentType == typeIndex)
                {
                    if (TypeManager.Equals(data, newData, typeIndex))
                        return itemIndex;
                }
            }
            while (m_HashLookup.TryGetNextValue(out itemIndex, ref iter));

            return -1;
        }

        internal int CloneSharedComponentNonDefault(ManagedComponentStore srcManagedComponents, int srcSharedComponentIndex)
        {
            var srcInfos = srcManagedComponents.SharedComponentInfoPtr;
            var srcData = srcManagedComponents.m_SharedComponentData[srcSharedComponentIndex];
            var typeIndex = srcInfos[srcSharedComponentIndex].ComponentType;
            var hashCode = srcInfos[srcSharedComponentIndex].HashCode;

            return InsertSharedComponentAssumeNonDefault(typeIndex, hashCode, srcData);
        }

        internal int InsertSharedComponentAssumeNonDefault(TypeIndex typeIndex, int hashCode, object newData)
        {
            var index = FindNonDefaultSharedComponentIndex(typeIndex, hashCode, newData);

            if (-1 == index)
            {
                (newData as IRefCounted)?.Retain();
                index = Add(typeIndex, hashCode, newData);
            }
            else
            {
                SharedComponentInfoPtr[index].RefCount++;
            }

            return index;
        }

        internal int InsertSharedComponentAssumeNonDefaultMove(TypeIndex typeIndex, int hashCode, object newData)
        {
            var index = FindNonDefaultSharedComponentIndex(typeIndex, hashCode, newData);

            if (-1 == index)
                index = Add(typeIndex, hashCode, newData);
            else
                SharedComponentInfoPtr[index].RefCount++;

            return index;
        }

        private int Add(TypeIndex typeIndex, int hashCode, object newData)
        {
            m_EntityComponentStore->m_SharedComponentVersion++;
            var info = new SharedComponentInfo
            {
                RefCount = 1,
                Version = m_EntityComponentStore->m_SharedComponentVersion,
                ComponentType = typeIndex,
                HashCode = hashCode
            };

            if (m_FreeListIndex != -1)
            {
                var infos = SharedComponentInfoPtr;

                int index = m_FreeListIndex;
                m_FreeListIndex = infos[index].Version;

                Assert.IsTrue(m_SharedComponentData[index] == null);

                m_HashLookup.Add(EntityComponentStore.GetSharedComponentHashKey(typeIndex, hashCode), index);
                m_SharedComponentData[index] = newData;
                infos[index] = info;
                return index;
            }
            else
            {
                int index = m_SharedComponentData.Count;
                m_HashLookup.Add(EntityComponentStore.GetSharedComponentHashKey(typeIndex, hashCode), index);
                m_SharedComponentData.Add(newData);
                m_SharedComponentInfo.Add(info);
                return index;
            }
        }

        public void IncrementSharedComponentVersion_Managed(int index)
        {
            var version = ++m_EntityComponentStore->m_SharedComponentVersion;
            if (index == 0)
            {
                m_EntityComponentStore->m_SharedComponentGlobalVersion = version;
            }
            else
            {
                SharedComponentInfoPtr[index].Version = version;
            }
        }

        public int GetSharedComponentVersion_Managed<T>(T sharedData) where T : struct
        {
            var index = FindSharedComponentIndex(TypeManager.GetTypeIndex<T>(), sharedData);
            if (index <= 0)
            {
                return m_EntityComponentStore->m_SharedComponentGlobalVersion;
            }
            return SharedComponentInfoPtr[index].Version;
        }

        public T GetSharedComponentData_Managed<T>(int index) where T : struct
        {
            if (index == 0)
                return default(T);

            return (T)m_SharedComponentData[index];
        }

        public object GetSharedComponentDataBoxed(int index, TypeIndex typeIndex)
        {
            if (index == 0)
                return Activator.CreateInstance(TypeManager.GetType(typeIndex));
            return m_SharedComponentData[index];
        }

        public object GetSharedComponentDataNonDefaultBoxed(int index)
        {
            Assert.AreNotEqual(0, index);
            return m_SharedComponentData[index];
        }

        public void AddSharedComponentReference_Managed(int index, int numRefs = 1)
        {
            if (index == 0)
                return;
            Assert.IsTrue(numRefs >= 0);
            SharedComponentInfoPtr[index].RefCount += numRefs;
        }

        public void RemoveSharedComponentReference_Managed(int index, int numRefs = 1)
        {
            if (index == 0)
                return;

            var infos = SharedComponentInfoPtr;

            var newCount = infos[index].RefCount -= numRefs;
            Assert.IsTrue(newCount >= 0);

            if (newCount != 0)
                return;

            // Bump default version when a shared component is removed completely.
            // This ensures that when asking for a shared component that previously existed and now longer exists
            // It will always return a change value.
            IncrementSharedComponentVersion_Managed(0);

            var hashCode = infos[index].HashCode;

            object sharedComponent = m_SharedComponentData[index];
            Assert.IsFalse(ReferenceEquals(sharedComponent, null));
            (sharedComponent as IRefCounted)?.Release();

            m_SharedComponentData[index] = null;
            var typeIndex = infos[index].ComponentType;
            infos[index].ComponentType = TypeIndex.Null;
            infos[index].Version = m_FreeListIndex;
            m_FreeListIndex = index;

            int itemIndex;
            NativeParallelMultiHashMapIterator<ulong> iter;
            if (m_HashLookup.TryGetFirstValue(
                    EntityComponentStore.GetSharedComponentHashKey(typeIndex, hashCode),
                    out itemIndex,
                    out iter))
            {
                do
                {
                    if (itemIndex == index)
                    {
                        m_HashLookup.Remove(iter);
                        return;
                    }
                }
                while (m_HashLookup.TryGetNextValue(out itemIndex, ref iter));
            }

            #if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            throw new System.InvalidOperationException("shared component couldn't be removed due to internal state corruption");
            #endif
        }

        public void CheckInternalConsistency()
        {
            var infos = SharedComponentInfoPtr;

            int refcount = 0;
            for (int i = 0; i < m_SharedComponentData.Count; ++i)
            {
                if (m_SharedComponentData[i] != null)
                {
                    refcount++;

                    var hashCode = infos[i].HashCode;

                    bool found = false;
                    int itemIndex;
                    NativeParallelMultiHashMapIterator<ulong> iter;
                    if (m_HashLookup.TryGetFirstValue(
                            EntityComponentStore.GetSharedComponentHashKey(infos[i].ComponentType, hashCode),
                            out itemIndex,
                            out iter))
                    {
                        do
                        {
                            if (itemIndex == i)
                                found = true;
                        }
                        while (m_HashLookup.TryGetNextValue(out itemIndex, ref iter));
                    }

                    Assert.IsTrue(found);
                }
            }

            Assert.AreEqual(refcount, m_HashLookup.Count());
        }

        public bool IsEmpty()
        {
            var infos = SharedComponentInfoPtr;

            for (int i = 1; i < m_SharedComponentData.Count; ++i)
            {
                if (m_SharedComponentData[i] != null)
                    return false;

                if (infos[i].ComponentType != TypeIndex.Null)
                    return false;

                if (infos[i].RefCount != 0)
                    return false;
            }

            if (m_SharedComponentData[0] != null)
                return false;

            if (m_HashLookup.Count() != 0)
                return false;

            return true;
        }

        public bool AllSharedComponentReferencesAreFromChunks(UnsafeParallelHashMap<int, int> refCountMap)
        {
            var infos = SharedComponentInfoPtr;
            for (int i = 1; i < m_SharedComponentInfo.Length; i++)
            {
                if (refCountMap.TryGetValue(i, out var recordedRefCount))
                {
                    var infoRefCount = infos[i].RefCount;
                    if (recordedRefCount != infoRefCount)
                        return false;
                }
            }
            return true;
        }

        public void MoveAllSharedComponents_Managed(
            ManagedComponentStore srcManagedComponents,
            ref NativeParallelHashMap<int, int> remap,
            int numSharedComponents)
        {
            remap[0] = 0;

            var srcInfos = srcManagedComponents.SharedComponentInfoPtr;

            for (int srcIndex = 1; srcIndex < numSharedComponents; ++srcIndex)
            {
                var srcData = srcManagedComponents.m_SharedComponentData[srcIndex];
                if (srcData == null) continue;

                var typeIndex = srcInfos[srcIndex].ComponentType;
                var hashCode = srcInfos[srcIndex].HashCode;
                var dstIndex = InsertSharedComponentAssumeNonDefaultMove(typeIndex, hashCode, srcData);

                SharedComponentInfoPtr[dstIndex].RefCount += srcInfos[srcIndex].RefCount - 1;
                IncrementSharedComponentVersion_Managed(dstIndex);

                remap[srcIndex] = dstIndex;
            }

            srcManagedComponents.ResetSharedComponentData();

        }


        public NativeArray<int> MoveSharedComponents_Managed(
            ManagedComponentStore srcManagedComponents,
            NativeArray<ArchetypeChunk> chunks,
            AllocatorManager.AllocatorHandle allocator)
        {
            // Todo: When NativeArray supports custom allocators, remove these .ToAllocator callsites DOTS-7695
            var remap = new NativeArray<int>(srcManagedComponents.GetSharedComponentCount(), allocator.ToAllocator);
            var remapPtr = (int*) remap.GetUnsafePtr();
            // Build a map of all shared component values that will be moved
            // remap will have a refcount of how many chunks reference the shared component after this loop
            for (int i = 0; i < chunks.Length; ++i)
            {
                var chunk = chunks[i].m_Chunk;
                var archetype = chunks[i].Archetype.Archetype;
                var sharedComponentValues = archetype->Chunks.GetSharedComponentValues(chunk.ListIndex);
                for (int sharedComponentIndex = 0;
                    sharedComponentIndex < archetype->NumSharedComponents;
                    ++sharedComponentIndex)
                    remapPtr[sharedComponentValues[sharedComponentIndex]]++;
            }

            remap[0] = 0;

            // Move all shared components that are being referenced
            // remap will have a remap table of src SharedComponentDataIndex -> dst SharedComponentDataIndex
            var srcInfos = srcManagedComponents.SharedComponentInfoPtr;
            for (int srcIndex = 1; srcIndex < remap.Length; ++srcIndex)
            {
                if (remapPtr[srcIndex] == 0)
                    continue;

                var srcData = srcManagedComponents.m_SharedComponentData[srcIndex];
                var typeIndex = srcInfos[srcIndex].ComponentType;
                var hashCode = srcInfos[srcIndex].HashCode;

                var dstIndex = InsertSharedComponentAssumeNonDefault(typeIndex, hashCode, srcData);

                // * remove refcount based on refcount table
                // * -1 because InsertSharedComponentAssumeNonDefault above adds 1 refcount
                int srcRefCount = remapPtr[srcIndex];
                SharedComponentInfoPtr[dstIndex].RefCount += srcRefCount - 1;
                srcManagedComponents.RemoveSharedComponentReference_Managed(srcIndex, srcRefCount);
                IncrementSharedComponentVersion_Managed(dstIndex);

                remapPtr[srcIndex] = dstIndex;
            }

            return remap;
        }

        public void MoveManagedComponentsFromDifferentWorld(NativeArray<int> srcIndices, NativeArray<int> dstIndices, int count, ManagedComponentStore srcManagedComponentStore)
        {
            for (int i = 0; i < count; ++i)
            {
                int src = srcIndices[i];
                int dst = dstIndices[i];
                m_ManagedComponentData[dst] = srcManagedComponentStore.m_ManagedComponentData[src];
                srcManagedComponentStore.m_ManagedComponentData[src] = null;
            }
        }

        public void PrepareForDeserialize()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (!IsEmpty())
                throw new System.ArgumentException("SharedComponentManager must be empty when deserializing a scene");
#endif

            ResetSharedComponentData();
        }

        public void PatchEntities(Archetype* archetype, ChunkIndex chunk, int entityCount, EntityRemapUtility.EntityRemapInfo* remapping)
        {
            var firstManagedComponent = archetype->FirstManagedComponent;
            var numManagedComponents = archetype->NumManagedComponents;

            for (int i = 0; i < numManagedComponents; ++i)
            {
                int indexInArchetype = i + firstManagedComponent;
                if (!archetype->Types[indexInArchetype].HasEntityReferences)
                    continue;

                var a = (int*)ChunkDataUtility.GetComponentDataRO(chunk, archetype, 0, indexInArchetype);
                for (int ei = 0; ei < entityCount; ++ei)
                {
                    if (a[ei] != 0)
                    {
                        var obj = m_ManagedComponentData[a[ei]];
                        m_ManagedObjectRemap.RemapEntityReferences(ref obj, remapping);
                    }
                }
            }

            m_ManagedObjectRemap.ClearGCRefs();
        }

        void PatchEntitiesForPrefab(int* managedComponents, int numManagedComponents, int allocatedCount, int remappingCount, Entity* remapSrc, Entity* remapDst)
        {
            for (int i = 0; i < allocatedCount; ++i)
            {
                for (int c = 0; c < numManagedComponents; c++)
                {
                    var managedComponentIndex = managedComponents[c];
                    if (managedComponentIndex != 0)
                    {
                        var obj = m_ManagedComponentData[managedComponentIndex];
                        m_ManagedObjectRemap.RemapEntityReferencesForPrefab(ref obj, remapSrc, remapDst, remappingCount);
                    }
                }
                managedComponents += numManagedComponents;
                remapDst += remappingCount;
            }
            m_ManagedObjectRemap.ClearGCRefs();
        }

        public void Playback(ref ManagedDeferredCommands managedDeferredCommands)
        {
            var reader = new UnsafeAppendBuffer.Reader(ref managedDeferredCommands.CommandBuffer);
            while (!reader.EndOfBuffer)
            {
                var cmd = reader.ReadNext<int>();
                switch ((ManagedDeferredCommands.Command)cmd)
                {
                    case (ManagedDeferredCommands.Command.IncrementSharedComponentVersion):
                    {
                        var sharedIndex = reader.ReadNext<int>();
                            IncrementSharedComponentVersion_Managed(sharedIndex);
                    }
                    break;

                    case (ManagedDeferredCommands.Command.AddReference):
                    {
                        var index = reader.ReadNext<int>();
                        var numRefs = reader.ReadNext<int>();
                        AddSharedComponentReference_Managed(index, numRefs);
                    }
                    break;

                    case (ManagedDeferredCommands.Command.RemoveReference):
                    {
                        var index = reader.ReadNext<int>();
                        var numRefs = reader.ReadNext<int>();
                        RemoveSharedComponentReference_Managed(index, numRefs);
                    }
                    break;

                    case (ManagedDeferredCommands.Command.PatchManagedEntities):
                    {
                        var archetype = (Archetype*)reader.ReadNext<IntPtr>();
                        var chunk = reader.ReadNext<ChunkIndex>();
                        var entityCount = reader.ReadNext<int>();
                        var remapping = (EntityRemapUtility.EntityRemapInfo*)reader.ReadNext<IntPtr>();

                        PatchEntities(archetype, chunk, entityCount, remapping);
                    }
                    break;

                    case (ManagedDeferredCommands.Command.PatchManagedEntitiesForPrefabs):
                    {
                        var remapSrc = (byte*)reader.ReadNext<IntPtr>();
                        var allocatedCount = reader.ReadNext<int>();
                        var remappingCount = reader.ReadNext<int>();
                        var numManagedComponents = reader.ReadNext<int>();
                        var allocator = (Allocator)reader.ReadNext<int>();


                        var remapSrcSize = UnsafeUtility.SizeOf<Entity>() * remappingCount;
                        var remapDstSize = UnsafeUtility.SizeOf<Entity>() * remappingCount * allocatedCount;

                        var remapDst = remapSrc + remapSrcSize;
                        var managedComponents = remapDst + remapDstSize;

                        PatchEntitiesForPrefab((int*)managedComponents, numManagedComponents, allocatedCount, remappingCount, (Entity*)remapSrc, (Entity*)remapDst);
                        Memory.Unmanaged.Free(remapSrc, allocator);
                    }
                    break;

                    case (ManagedDeferredCommands.Command.CloneManagedComponents):
                    {
                        var srcArray = (int*)reader.ReadNextArray<int>(out var componentCount);
                        var instanceCount = reader.ReadNext<int>();
                        var dstArray = (int*)reader.ReadNextArray<int>(out _);
                        CloneManagedComponents(srcArray, componentCount, dstArray, instanceCount);
                    }
                    break;

                    case (ManagedDeferredCommands.Command.CloneCompanionComponents):
                    {
                        var srcArray = (int*)reader.ReadNextArray<int>(out var componentCount);
                        var entities = (Entity*)reader.ReadNextArray<Entity>(out var instanceCount);
                        var dstComponentReferenceIndices = (int*)reader.ReadNextArray<int>(out _);
                        var dstComponentLinkIds = (int*)reader.ReadNextArray<int>(out _);
                        var dstArray = (int*)reader.ReadNextArray<int>(out _);

                        if (InstantiateCompanionComponent != null)
                            InstantiateCompanionComponent(srcArray, componentCount, entities, dstComponentReferenceIndices, dstComponentLinkIds, dstArray, instanceCount, this);
                        else
                        {
                            // InstantiateHybridComponent was not injected just copy the reference to the object and dont clone it
                            for (int src = 0; src < componentCount; ++src)
                            {
                                object sourceComponent = m_ManagedComponentData[srcArray[src]];
                                for (int i = 0; i < instanceCount; ++i)
                                    m_ManagedComponentData[dstArray[i]] = sourceComponent;
                                dstArray += instanceCount;
                            }
                        }
                    }
                    break;

                    case (ManagedDeferredCommands.Command.FreeManagedComponents):
                    {
                        var count = reader.ReadNext<int>();
                        for (int i = 0; i < count; ++i)
                        {
                            var managedComponentIndex = reader.ReadNext<int>();
                            DisposeManagedComponentData(m_ManagedComponentData[managedComponentIndex]);
                            m_ManagedComponentData[managedComponentIndex] = null;
                        }
                    }
                    break;

                    case (ManagedDeferredCommands.Command.SetManagedComponentCapacity):
                    {
                        var capacity = reader.ReadNext<int>();
                        SetManagedComponentCapacity(capacity);
                    }
                    break;
                }
            }

            managedDeferredCommands.Reset();
        }

        private void CloneManagedComponents(int* srcArray, int componentCount, int* dstArray, int instanceCount)
        {
            for (int src = 0; src < componentCount; ++src)
            {
                object sourceComponent = m_ManagedComponentData[srcArray[src]];
                for (int i = 0; i < instanceCount; ++i)
                    m_ManagedComponentData[dstArray[i]] = m_ManagedObjectClone.Clone(sourceComponent);
                dstArray += instanceCount;
            }

            m_ManagedObjectClone.ClearGCRefs();
        }

        internal void SetManagedComponentValue(int index, object componentObject)
        {
            m_ManagedComponentData[index] = componentObject;
        }

        // Ensure there are at least "count" free managed component indices and
        // resize managed component array directly if needed
        public void ReserveManagedComponentIndicesDirect(int count, ref EntityComponentStore entityComponentStore)
        {
            int freeCount = entityComponentStore.ManagedComponentFreeCount;
            if (freeCount >= count)
                return;

            int newCapacity = entityComponentStore.GrowManagedComponentCapacity(count - freeCount);
            SetManagedComponentCapacity(newCapacity);
        }

        public void UpdateManagedComponentValue(int* index, object value, ref EntityComponentStore entityComponentStore)
        {
            entityComponentStore.AssertNoQueuedManagedDeferredCommands();
            var iManagedComponent = *index;

            if (iManagedComponent != 0 && !ReferenceEquals(value, m_ManagedComponentData[iManagedComponent]))
            {
                var oldObject = m_ManagedComponentData[iManagedComponent];
                (oldObject as IRefCounted)?.Release();
                (oldObject as IDisposable)?.Dispose();
            }

            if (value != null)
            {
                if (iManagedComponent == 0)
                {
                    ReserveManagedComponentIndicesDirect(1, ref entityComponentStore);
                    iManagedComponent = *index = entityComponentStore.AllocateManagedComponentIndex();
                }
            }
            else
            {
                if (iManagedComponent == 0)
                    return;
                *index = 0;
                entityComponentStore.FreeManagedComponentIndex(iManagedComponent);
            }
            m_ManagedComponentData[iManagedComponent] = value;
        }

        public void CloneManagedComponentsFromDifferentWorld(int* indices, int count, ManagedComponentStore srcManagedComponentStore, ref EntityComponentStore dstEntityComponentStore)
        {
            dstEntityComponentStore.AssertNoQueuedManagedDeferredCommands();
            ReserveManagedComponentIndicesDirect(count, ref dstEntityComponentStore);
            for (int i = 0; i < count; ++i)
            {
                var obj = srcManagedComponentStore.m_ManagedComponentData[indices[i]];
                var clone = m_ManagedObjectClone.Clone(obj);
                int dstIndex = dstEntityComponentStore.AllocateManagedComponentIndex();
                indices[i] = dstIndex;
                DisposeManagedComponentData(m_ManagedComponentData[dstIndex]);
                m_ManagedComponentData[dstIndex] = clone;
            }

            m_ManagedObjectClone.ClearGCRefs();
        }

        public void ResetManagedComponentStoreForDeserialization(int managedComponentCount, ref EntityComponentStore entityComponentStore)
        {
            managedComponentCount++; // also need space for 0 index (null)
            Assert.AreEqual(0, entityComponentStore.ManagedComponentIndexUsedCount);
            entityComponentStore.m_ManagedComponentFreeIndex.Length = 0;
            entityComponentStore.m_ManagedComponentIndex = managedComponentCount;
            if (managedComponentCount > entityComponentStore.m_ManagedComponentIndexCapacity)
            {
                entityComponentStore.m_ManagedComponentIndexCapacity = managedComponentCount;
                SetManagedComponentCapacity(managedComponentCount);
            }
        }

        public static void DisposeManagedComponentData(object obj)
        {
            if (obj is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
