#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    [BurstCompile]
    partial class EntitiesJournaling
    {
        /// <summary>
        /// Utiliy methods for <see cref="RecordViewArray"/>.
        /// </summary>
        [BurstCompile]
        public unsafe static class RecordViewArrayUtility
        {
            /// <summary>
            /// Convert <see cref="RecordType.GetComponentDataRW"/> records to <see cref="RecordType.SetComponentData"/> records, when possible.
            /// </summary>
            /// <param name="records">The record view array to convert.</param>
            [BurstCompile]
            public static void ConvertGetRWsToSets(in RecordViewArray records)
            {
                var worldDataStore = new NativeList<NativeList<NativeHashMap<ComponentTypeView, IntPtr>>>(0, Allocator.Temp);

                // Process every record in descending order
                for (
                    int recordIter = records.Ordering == Ordering.Ascending ? records.Length - 1 : 0;
                    recordIter >= 0 && recordIter < records.Length;
                    recordIter = records.Ordering == Ordering.Ascending ? recordIter - 1 : recordIter + 1)
                {
                    var record = records[recordIter];

                    // Only process GetComponentDataRW records
                    if (record.RecordType != RecordType.GetComponentDataRW)
                        continue;

                    // Process every entity of this record
                    bool hasChanges = false;
                    for (var entityIter = 0; entityIter < record.Entities.Length; ++entityIter)
                    {
                        var entity = record.Entities[entityIter];

                        // Grow world data store if necessary
                        var worldIndex = (int)entity.WorldSequenceNumber;
                        if (worldDataStore.Length < worldIndex + 1)
                            worldDataStore.AddReplicate(default, worldIndex + 1 - worldDataStore.Length);

                        // Get the entity data store for this entity's world
                        var entityDataStore = worldDataStore[worldIndex];
                        if (!entityDataStore.IsCreated)
                        {
                            entityDataStore = new NativeList<NativeHashMap<ComponentTypeView, IntPtr>>(0, Allocator.Temp);
                            worldDataStore[worldIndex] = entityDataStore;
                        }

                        // Grow entity data store if necessary
                        var entityIndex = entity.Index;
                        if (entityDataStore.Length < entityIndex + 1)
                            entityDataStore.AddReplicate(default, entityIndex + 1 - entityDataStore.Length);

                        // Get the component data store for this entity
                        var componentDataStore = entityDataStore[entityIndex];
                        if (!componentDataStore.IsCreated)
                        {
                            componentDataStore = new NativeHashMap<ComponentTypeView, IntPtr>(0, Allocator.Temp);
                            entityDataStore[entityIndex] = componentDataStore;
                        }

                        // Process every component of that entity
                        for (var componentIter = 0; componentIter < record.ComponentTypes.Length; ++componentIter)
                        {
                            var component = record.ComponentTypes[componentIter];
                            var typeInfo = TypeManager.GetTypeInfo(component.TypeIndex);
                            var dataPtr = record.DataPtr + (entityIter * typeInfo.TypeSize);

                            // Get the component data for this component
                            if (!componentDataStore.TryGetValue(component, out var componentData))
                            {
                                // This component was never seen before, add it to component data store
                                var componentDataPtr = AllocatorManager.Allocate(Allocator.Temp, typeInfo.TypeSize, typeInfo.AlignmentInBytes);
                                if (componentDataPtr != null)
                                {
                                    UnsafeUtility.MemCpy(componentDataPtr, dataPtr, typeInfo.TypeSize);
                                    componentDataStore.Add(component, new IntPtr(componentDataPtr));
                                }
                            }
                            else
                            {
                                // This component already exist in the component data store, swap its value
                                var componentDataPtr = componentData.ToPointer();
                                if (componentDataPtr != null)
                                {
                                    UnsafeUtilityExtensions.MemSwap(dataPtr, componentDataPtr, typeInfo.TypeSize);
                                    hasChanges = true;
                                }
                            }
                        }
                    }

                    // If any component data was changed, mark the record as converted
                    if (hasChanges)
                    {
                        var header = new Header(record.Index, RecordType.SetComponentData, record.FrameIndex, record.World.SequenceNumber, record.ExecutingSystem.Handle, record.OriginSystem.Handle, record.Entities.Length, record.ComponentTypes.Length, record.DataLength);
                        UnsafeUtility.MemCpy(record.Ptr, &header, UnsafeUtility.SizeOf<Header>());
                    }
                }

                // Dispose everything we allocated
                foreach (var entityStore in worldDataStore)
                {
                    if (!entityStore.IsCreated)
                        continue;

                    foreach (var componentStore in entityStore)
                    {
                        if (!componentStore.IsCreated)
                            continue;

                        var values = componentStore.GetValueArray(Allocator.Temp);
                        foreach (var componentData in values)
                            AllocatorManager.Free(Allocator.Temp, componentData.ToPointer());
                        values.Dispose();

                        componentStore.Dispose();
                    }
                    entityStore.Dispose();
                }
                worldDataStore.Dispose();
            }
        }
    }
}
#endif
