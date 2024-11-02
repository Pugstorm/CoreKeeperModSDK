using System;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// A partial clone of the <see cref="EntityNameStorage"/> which stores case invariant versions.
    /// </summary>
    [GenerateTestsForBurstCompatibility]
    unsafe struct EntityNameStorageLowerInvariant : IDisposable
    {
        struct Data
        {
            public int Entries;
        }

        readonly Allocator m_Allocator;
        
        [NativeDisableUnsafePtrRestriction] UnsafeList<byte>* m_Buffer;
        [NativeDisableUnsafePtrRestriction] Data* m_Data;
        
        public EntityNameStorageLowerInvariant(int initialCapacity, Allocator allocator)
        {
            m_Allocator = allocator;
            m_Buffer = UnsafeList<byte>.Create(initialCapacity, allocator);
            m_Data = (Data*) UnsafeUtility.Malloc(UnsafeUtility.SizeOf<Data>(), UnsafeUtility.AlignOf<Data>(), allocator);
            m_Data->Entries = 0;
        }

        public void Dispose()
        {
            UnsafeUtility.Free(m_Data, m_Allocator);
            m_Data = null;
            UnsafeList<byte>.Destroy(m_Buffer);
            m_Buffer = null;
        }
                
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new [] { typeof(FixedString64Bytes) })]
        public void GetFixedString<T>(int index, ref T temp)
            where T : IUTF8Bytes, INativeList<byte>
        {
            // Synchronize names from the 'EntityNameStorage' and store them in a case invariant way.
            UpdateFromEntityNameStorage();
            
            // Do the standard string unpack.
            Assert.IsTrue(index < EntityNameStorage.s_State.Data.entries);
            var e = EntityNameStorage.s_State.Data.entry[index];
            Assert.IsTrue(e.length <= EntityNameStorage.kEntityNameMaxLengthBytes);
            temp.Length = math.min(e.length, temp.Capacity);
            UnsafeUtility.MemCpy(temp.GetUnsafePtr(), m_Buffer->Ptr + e.offset, temp.Length);
        }

        public void UpdateFromEntityNameStorage()
        {
            if (m_Data->Entries != EntityNameStorage.Entries)
            {
                var fromLength = m_Data->Entries;
                var toLength = EntityNameStorage.Entries;
                        
                m_Data->Entries = toLength;

                if (toLength <= fromLength) 
                    return;
                
                var fromEntry = EntityNameStorage.s_State.Data.entry[fromLength];
                var toEntry = EntityNameStorage.s_State.Data.entry[toLength - 1];

                var start = fromEntry.offset;
                var length = toEntry.offset + toEntry.length - fromEntry.offset;

                UnsafeUtility.MemCpy(m_Buffer->Ptr + start, EntityNameStorage.s_State.Data.buffer.Ptr + start, length);
                
                // At this point we have an array of 'bytes'. we need to enumerate each 'character' to apply lower case.
                for (var i = fromLength; i < toLength; i++)
                {
                    var entry = EntityNameStorage.s_State.Data.entry[i];
                    FixedStringUtility.Utf8ToLower(m_Buffer->Ptr + entry.offset, entry.length);
                }
            }
        }
    }
}