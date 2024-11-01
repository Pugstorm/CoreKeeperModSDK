using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System;
using Unity.Burst.Intrinsics;
using Unity.Jobs;

// ******* COPY AND PASTE WARNING *************
// NOTE: Duplicate tests (with only type differences)
// - BufferElementDataTests.cs and CleanupBufferElementDataTests.cs
// - Any change to this file should be reflected in the other file.
// Changes between two files:
// - s/BufferElementDataTests/CleanupBufferElementDataTests/
// - s/EcsIntElement/EcsIntCleanupElement/g
// - s/IBufferElementData/ICleanupBufferElementData/g
// ******* COPY AND PASTE WARNING *************

#pragma warning disable 0649
#pragma warning disable 0219 // assigned but its value is never used

namespace Unity.Entities.Tests
{
    class CleanupBufferElementDataTests : ECSTestsFixture
    {
        [InternalBufferCapacity(1024 * 1024)]
        public struct OverSizedCapacity : ICleanupBufferElementData
        {
            public int Value;
        }

        [Test]
        public void BufferTypeClassificationWorks()
        {
            var t  = TypeManager.GetTypeInfo<EcsIntCleanupElement>();
            Assert.AreEqual(TypeManager.TypeCategory.BufferData, t.Category);
            Assert.AreEqual(8, t.BufferCapacity);
        }

        [Test]
        public void BufferComponentTypeCreationWorks()
        {
            var bt = ComponentType.ReadWrite<EcsIntCleanupElement>();
            var typeInfo = TypeManager.GetTypeInfo(bt.TypeIndex);
            Assert.AreEqual(ComponentType.AccessMode.ReadWrite, bt.AccessModeType);
            Assert.AreEqual(8, typeInfo.BufferCapacity);
        }

        [Test]
        public void CreateEntityWithIntThrows()
        {
            Assert.Throws<System.ArgumentException>(() => { m_Manager.CreateEntity(typeof(int));});
        }

        [Test]
        public void AddComponentWithIntThrows()
        {
            var entity = m_Manager.CreateEntity();
            Assert.Throws<System.ArgumentException>(() => { m_Manager.AddComponent(entity, ComponentType.ReadWrite<int>()); });
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires data validation checks")]
        // Invalid because chunk size is too small to hold a single entity
        public void CreateEntityWithInvalidInternalCapacity()
        {
            var arrayType = ComponentType.ReadWrite<OverSizedCapacity>();
            Assert.Throws<ArgumentException>(() => m_Manager.CreateEntity(arrayType));
        }

        [Test]
        public void HasComponent()
        {
            var arrayType = ComponentType.ReadWrite<EcsIntCleanupElement>();
            var entity = m_Manager.CreateEntity(arrayType);
            Assert.IsTrue(m_Manager.HasComponent(entity, arrayType));
        }

        [Test]
        public void InitialCapacityWorks()
        {
            var arrayType = ComponentType.ReadWrite<EcsIntCleanupElement>();
            var entity = m_Manager.CreateEntity(arrayType);
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
            Assert.AreEqual(8, buffer.Capacity);
        }

        [Test]
        public void InitialCapacityWorks2()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
            Assert.AreEqual(8, buffer.Capacity);
        }

        [Test]
        public void AddWorks()
        {
            var arrayType = ComponentType.ReadWrite<EcsIntCleanupElement>();
            var entity = m_Manager.CreateEntity(arrayType);
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
            for (int i = 0; i < 189; ++i)
                buffer.Add(i);

            Assert.AreEqual(189, buffer.Length);
            for (int i = 0; i < 189; ++i)
            {
                Assert.AreEqual(i, buffer[i].Value);
            }
        }

        [Test]
        public void InsertWorks()
        {
            var arrayType = ComponentType.ReadWrite<EcsIntCleanupElement>();
            var entity = m_Manager.CreateEntity(arrayType);
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
            // Insert at end
            for (int i = 0; i < 189; ++i)
                buffer.Insert(i, i);

            Assert.AreEqual(189, buffer.Length);
            for (int i = 0; i < 189; ++i)
            {
                Assert.AreEqual(i, buffer[i].Value);
            }

            buffer.Clear();

            // Insert at beginning
            for (int i = 0; i < 189; ++i)
                buffer.Insert(0, i);

            Assert.AreEqual(189, buffer.Length);
            for (int i = 0; i < 189; ++i)
            {
                Assert.AreEqual(188 - i, buffer[i].Value);
            }

            buffer.Clear();

            // Insert in middle
            for (int i = 0; i < 189; ++i)
                buffer.Insert(i / 2, i);

            Assert.AreEqual(189, buffer.Length);
            for (int i = 0; i < 189; ++i)
            {
                int expectedValue = i < 94 ? i * 2 + 1 : (188 - i) * 2;
                Assert.AreEqual(expectedValue, buffer[i].Value);
            }
        }

        [Test]
        public void AddRangeWorks()
        {
            var arrayType = ComponentType.ReadWrite<EcsIntCleanupElement>();
            var entity = m_Manager.CreateEntity(arrayType);
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
            for (int i = 0; i < 7; ++i)
                buffer.Add(i);

            Assert.AreEqual(7, buffer.Length);

            var blah = new NativeArray<EcsIntCleanupElement>(1024, Allocator.Temp);

            for (int i = 0; i < blah.Length; ++i)
            {
                blah[i] = i;
            }

            buffer.AddRange(blah);
            blah.Dispose();

            Assert.AreEqual(1024 + 7, buffer.Length);

            for (int i = 0; i < 7; ++i)
                Assert.AreEqual(i, buffer[i].Value);
            for (int i = 0; i < 1024; ++i)
                Assert.AreEqual(i, buffer[7 + i].Value);
        }

        [Test]
        public void RemoveAtWorks()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
            for (int i = 0; i < 9; ++i)
                buffer.Add(i);

            buffer.RemoveAt(7);

            CheckBufferContents(buffer, new int[] { 0, 1, 2, 3, 4, 5, 6, 8 });
        }

        private static void CheckBufferContents(DynamicBuffer<EcsIntCleanupElement> buffer, int[] refs)
        {
            Assert.AreEqual(refs.Length, buffer.Length);

            for (int i = 0; i < refs.Length; ++i)
            {
                Assert.AreEqual(refs[i], buffer[i].Value);
            }
        }

        [Test]
        public void RemoveAtWorksFromStart()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
            for (int i = 0; i < 9; ++i)
                buffer.Add(i);

            buffer.RemoveAt(0);

            CheckBufferContents(buffer, new int[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        }

        [Test]
        public void RemoveAtWorksFromEnd()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
            for (int i = 0; i < 9; ++i)
                buffer.Add(i);

            buffer.RemoveAt(8);
            buffer.RemoveAt(7);

            CheckBufferContents(buffer, new int[] { 0, 1, 2, 3, 4, 5, 6 });
        }

        [Test]
        public void RemoveRangeWorksFromEnd()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
            for (int i = 0; i < 9; ++i)
                buffer.Add(i);

            buffer.RemoveRange(5, 4);

            CheckBufferContents(buffer, new int[] { 0, 1, 2, 3, 4 });
        }

        [Test]
        public void InitialCapacityWorksWithAddComponment()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddComponent(entity, ComponentType.ReadWrite<EcsIntCleanupElement>());
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
            Assert.AreEqual(8, buffer.Capacity);
        }

        [Test]
        public void RemoveComponent()
        {
            var arrayType = ComponentType.ReadWrite<EcsIntCleanupElement>();
            var entity = m_Manager.CreateEntity(arrayType);
            Assert.IsTrue(m_Manager.HasComponent(entity, arrayType));
            m_Manager.RemoveComponent(entity, arrayType);
            Assert.IsFalse(m_Manager.HasComponent(entity, arrayType));
        }

        [Test]
        public void MutateBufferData()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddBuffer<EcsIntCleanupElement>(entity);

            var array = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
            Assert.AreEqual(0, array.Length);

            using (var array2 = new NativeArray<EcsIntCleanupElement>(6, Allocator.Temp))
            {
                array.CopyFrom(array2);

                Assert.AreEqual(6, array.Length);

                array[3] = 5;
                Assert.AreEqual(5, array[3].Value);
                Assert.AreNotEqual(5, array2[3].Value); // no aliasing
            }
        }

        [Test]
        public void BufferComponentGroupChunkIteration()
        {
            /*var entity64 =*/
            m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            /*var entity10 =*/
            m_Manager.CreateEntity(typeof(EcsIntCleanupElement));

            var group = m_Manager.CreateEntityQuery(typeof(EcsIntCleanupElement));

            var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            var bufferTypeHandle = m_Manager.GetBufferTypeHandle<EcsIntCleanupElement>(false);
            var buffers = chunks[0].GetBufferAccessor(ref bufferTypeHandle);

            Assert.AreEqual(2, buffers.Length);
            Assert.AreEqual(0, buffers[0].Length);
            Assert.AreEqual(8, buffers[0].Capacity);
            Assert.AreEqual(0, buffers[1].Length);
            Assert.AreEqual(8, buffers[1].Capacity);

            buffers[0].Add(12);
            buffers[0].Add(13);

            Assert.AreEqual(2, buffers[0].Length);
            Assert.AreEqual(12, buffers[0][0].Value);
            Assert.AreEqual(13, buffers[0][1].Value);

            Assert.AreEqual(0, buffers[1].Length);
        }

        [Test]
        public void BufferLookup_Works()
        {
            var entityInt = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            m_Manager.GetBuffer<EcsIntCleanupElement>(entityInt).CopyFrom(new EcsIntCleanupElement[] { 1, 2, 3 });

            var intLookup = EmptySystem.GetBufferLookup<EcsIntCleanupElement>();
            Assert.IsTrue(intLookup.HasBuffer(entityInt));
            Assert.IsFalse(intLookup.HasBuffer(new Entity()));

            Assert.AreEqual(2, intLookup[entityInt][1].Value);
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void OutOfBoundsAccessThrows()
        {
            var entityInt = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var intArray = m_Manager.GetBuffer<EcsIntCleanupElement>(entityInt);
            intArray.Add(12);
            m_Manager.DestroyEntity(entityInt);

            Assert.Throws<ObjectDisposedException>(() =>
            {
                intArray.Add(123);
            });
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void UseAfterStructuralChangeThrows()
        {
            var entityInt = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var intArray = m_Manager.GetBuffer<EcsIntCleanupElement>(entityInt);
            m_Manager.DestroyEntity(entityInt);

            Assert.Throws<ObjectDisposedException>(() =>
            {
                intArray.Add(123);
            });
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void UseAfterStructuralChangeThrows2()
        {
            var entityInt = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBufferLookup<EcsIntCleanupElement>();
            var array = buffer[entityInt];
            m_Manager.DestroyEntity(entityInt);

            Assert.Throws<ObjectDisposedException>(() =>
            {
                array.Add(123);
            });
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void UseAfterStructuralChangeThrows3()
        {
            var entityInt = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entityInt);
            buffer.CopyFrom(new EcsIntCleanupElement[] { 1, 2, 3 });
            m_Manager.AddComponentData(entityInt, new EcsTestData() { value = 20 });
            Assert.Throws<ObjectDisposedException>(() => {
                buffer.Add(4);
            });
        }

        [Test]
        [TestRequiresCollectionChecks("Read/write safety relies on Atomic Safety Handles")]
        public void WritingReadOnlyThrows()
        {
            var entityInt = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBufferLookup<EcsIntCleanupElement>(true);
            var array = buffer[entityInt];
            Assert.Throws<InvalidOperationException>(() =>
            {
                array.Add(123);
            });
        }

        [Test]
        public void ReinterpretWorks()
        {
            var entityInt = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var intBuffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entityInt);
            var floatBuffer = intBuffer.Reinterpret<float>();

            intBuffer.Add(0x3f800000);
            floatBuffer.Add(-1.0f);

            Assert.AreEqual(2, intBuffer.Length);
            Assert.AreEqual(2, floatBuffer.Length);

            Assert.AreEqual(0x3f800000, intBuffer[0].Value);
            Assert.AreEqual(1.0f, floatBuffer[0]);
            Assert.AreEqual(0xbf800000u, (uint)intBuffer[1].Value);
            Assert.AreEqual(-1.0f, floatBuffer[1]);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires data validation checks")]
        public void ReinterpretWrongSizeThrows()
        {
            var entityInt = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entityInt);
            Assert.Throws<InvalidOperationException>(() =>
            {
                buffer.Reinterpret<ushort>();
            });
        }

        [Test]
        public void TrimExcessWorks()
        {
            var entityInt = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var intBuffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entityInt);

            Assert.AreEqual(0, intBuffer.Length);
            Assert.AreEqual(8, intBuffer.Capacity);

            intBuffer.CopyFrom(new EcsIntCleanupElement[] { 0, 1, 2, 3 });

            intBuffer.TrimExcess();

            Assert.AreEqual(4, intBuffer.Length);
            Assert.AreEqual(8, intBuffer.Capacity);

            for (int i = 4; i < 10; ++i)
            {
                intBuffer.Add(i);
            }

            Assert.AreEqual(10, intBuffer.Length);
            Assert.AreEqual(16, intBuffer.Capacity);

            intBuffer.TrimExcess();

            Assert.AreEqual(10, intBuffer.Length);
            Assert.AreEqual(10, intBuffer.Capacity);

            for (int i = 0; i < 10; ++i)
            {
                Assert.AreEqual(i, intBuffer[i].Value);
            }
        }

        [Test]
        public void BufferSurvivesArchetypeChange()
        {
            var entityInt = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entityInt);
            buffer.CopyFrom(new EcsIntCleanupElement[] { 1, 2, 3 });

            m_Manager.AddComponentData(entityInt, new EcsTestData() { value = 20 });

            CheckBufferContents(m_Manager.GetBuffer<EcsIntCleanupElement>(entityInt), new int[] { 1, 2, 3 });
        }

        internal struct ElementWithoutCapacity : ICleanupBufferElementData
        {
            public float Value;
        }

        [Test]
        public void NoCapacitySpecifiedWorks()
        {
            var original = m_Manager.CreateEntity(typeof(ElementWithoutCapacity));
            var buffer = m_Manager.GetBuffer<ElementWithoutCapacity>(original);
            Assert.AreEqual(buffer.Capacity, 32);
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void ArrayInvalidationWorks()
        {
            var original = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(original);
            buffer.Add(1);
            var array = buffer.AsNativeArray();
            Assert.AreEqual(1, array[0].Value);
            Assert.AreEqual(1, array.Length);
            buffer.Add(2);
            Assert.Throws<ObjectDisposedException>(() =>
            {
                int value = array[0].Value;
            });
            Assert.Throws<ObjectDisposedException>(() =>
            {
                array[0] = 5;
            });
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void ArrayInvalidationHappensForAllInstances()
        {
            var e0 = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var e1 = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));

            var b0 = m_Manager.GetBuffer<EcsIntCleanupElement>(e0);
            var b1 = m_Manager.GetBuffer<EcsIntCleanupElement>(e1);

            b0.Add(1);
            b1.Add(1);

            var a0 = b0.AsNativeArray();
            var a1 = b1.AsNativeArray();

            b0.Add(1);

            Assert.Throws<ObjectDisposedException>(() =>
            {
                int value = a0[0].Value;
            });

            Assert.Throws<ObjectDisposedException>(() =>
            {
                int value = a1[0].Value;
            });
        }

        [Test]
        public void ArraysAreNotInvalidateByWrites()
        {
            var original = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(original);
            buffer.Add(1);
            var array = buffer.AsNativeArray();
            Assert.AreEqual(1, array[0].Value);
            Assert.AreEqual(1, array.Length);
            buffer[0] = 2;
            Assert.AreEqual(2, array[0].Value);
        }

        struct ArrayConsumingJob : IJob
        {
            public NativeArray<EcsIntCleanupElement> Array;

            public void Execute()
            {
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on job safety system")]
        public void BufferInvalidationNotPossibleWhenArraysAreGivenToJobs()
        {
            var original = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(original);
            buffer.Add(1);
            var handle = new ArrayConsumingJob {Array = buffer.AsNativeArray()}.Schedule();
            Assert.Throws<InvalidOperationException>(() => buffer.Add(2));
            Assert.Throws<InvalidOperationException>(() => m_Manager.DestroyEntity(original));
            handle.Complete();
        }

        struct WriteJob : IJobChunk
        {
            public BufferTypeHandle<EcsIntCleanupElement> Int;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // This job is not written to support queries with enableable component types.
                Assert.IsFalse(useEnabledMask);

                var intValue = chunk.GetBufferAccessor(ref Int)[0];

                Assert.AreEqual(intValue.Length, 1);

                var intValueArray = intValue.AsNativeArray();

                Assert.AreEqual(5, intValue[0].Value);
                Assert.AreEqual(5, intValueArray[0].Value);

                intValueArray[0] = 6;

                Assert.AreEqual(intValueArray.Length, 1);
                Assert.AreEqual(6, intValue[0].Value);
            }
        }

        [Test]
        public void ReadWriteDynamicBuffer()
        {
            var original = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(original);
            buffer.Add(5);

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<EcsIntCleanupElement>()
                .Build(EmptySystem);
            var job = new WriteJob
            {
                //@TODO: Throw exception when read only flag is not accurately passed to job for buffers...
                Int = EmptySystem.GetBufferTypeHandle<EcsIntCleanupElement>()
            };

            job.ScheduleParallel(query, default).Complete();
        }

        struct ReadOnlyJob : IJobChunk
        {
            [ReadOnly]
            public BufferTypeHandle<EcsIntCleanupElement> Int;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // This job is not written to support queries with enableable component types.
                Assert.IsFalse(useEnabledMask);

                var intValue = chunk.GetBufferAccessor(ref Int)[0];

                // Reading buffer
                Assert.AreEqual(intValue.Length, 1);
                Assert.AreEqual(5, intValue[0].Value);

                // Reading casted native array
                var intValueArray = intValue.AsNativeArray();
                Assert.AreEqual(intValueArray.Length, 1);
                Assert.AreEqual(5, intValueArray[0].Value);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // Can't write to buffer...
                Assert.Throws<InvalidOperationException>(() => { intValue[0] = 5; });
                Assert.Throws<InvalidOperationException>(() => { intValueArray[0] = 5; });
#endif
            }
        }

        public void ReadOnlyDynamicBufferImpl(bool readOnlyType)
        {
            var original = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(original);
            buffer.Add(5);

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<EcsIntCleanupElement>()
                .Build(EmptySystem);
            var job = new ReadOnlyJob
            {
                Int = EmptySystem.GetBufferTypeHandle<EcsIntCleanupElement>(readOnlyType)
            };

            job.ScheduleParallel(query, default).Complete();
        }

        [Test]
        public void ReadOnlyDynamicBufferReadOnly()
        {
            ReadOnlyDynamicBufferImpl(true);
        }

        [Test]
        public void ReadOnlyDynamicBufferWritable()
        {
            ReadOnlyDynamicBufferImpl(false);
        }

        struct BufferConsumingJob : IJob
        {
            public DynamicBuffer<EcsIntCleanupElement> Buffer;

            public void Execute()
            {
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on job safety system")]
        public void BufferInvalidationNotPossibleWhenBuffersAreGivenToJobs()
        {
            var original = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(original);
            buffer.Add(1);
            var handle = new BufferConsumingJob {Buffer = buffer}.Schedule();
            Assert.Throws<InvalidOperationException>(() => buffer.Add(2));
            Assert.Throws<InvalidOperationException>(() => m_Manager.DestroyEntity(original));
            handle.Complete();
        }

        struct ReadOnlyNativeArrayJob : IJob
        {
            [ReadOnly]
            public NativeArray<EcsIntCleanupElement> IntArray;

            public void Execute()
            {
                var array = IntArray;

                // Reading casted native array
                Assert.AreEqual(array.Length, 1);
                Assert.AreEqual(5, array[0].Value);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // Can't write to buffer...
                Assert.Throws<InvalidOperationException>(() => { array[0] = 5; });
                Assert.Throws<InvalidOperationException>(() => { array[0] = 5; });
#endif
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on job safety system")]
        public void NativeArrayInJobReadOnly()
        {
            var original = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(original);
            buffer.Add(5);

            var job = new ReadOnlyNativeArrayJob
            {
                IntArray = buffer.AsNativeArray()
            };
            var jobHandle = job.Schedule();

            Assert.Throws<InvalidOperationException>(() => { buffer.Add(5); });
            Assert.Throws<InvalidOperationException>(() => { buffer[0] = 6; });
            Assert.Throws<InvalidOperationException>(() => { job.IntArray[0] = 6; });
            Assert.Throws<InvalidOperationException>(() => { job.IntArray[0] = 6; });

            Assert.AreEqual(5, buffer[0].Value);
            Assert.AreEqual(5, job.IntArray[0].Value);

            jobHandle.Complete();
        }

        [Test]
        public void DynamicBuffer_Default_IsCreated_IsFalse()
        {
            DynamicBuffer<EcsIntCleanupElement> buffer = default;
            Assert.False(buffer.IsCreated);
        }

        [Test]
        public void DynamicBuffer_FromEntity_IsCreated_IsTrue()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
            Assert.IsTrue(buffer.IsCreated);
        }

        [Test]
        [Explicit("Takes a long time (order of seconds).")]
        public void DynamicBuffer_AllocateBufferWithLongSize_DoesNotThrow()
        {
            if (IntPtr.Size == 4)
                Assert.Ignore("Test makes no sense on 32bit");

            var entity = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
            int capacity = (int)(((long)int.MaxValue + 1) / UnsafeUtility.SizeOf<EcsIntCleanupElement>() + 1); //536870913
            Assert.DoesNotThrow(() => buffer.ResizeUninitialized(capacity));
            Assert.AreEqual(capacity, buffer.Length);
        }

        [Test]
        [Explicit("Takes a long time (order of seconds).")]
        public void DynamicBuffer_Insert_BufferHasLongSize_DoesNotThrow()
        {
            if (IntPtr.Size == 4)
                Assert.Ignore("Test makes no sense on 32bit");

            var entity = m_Manager.CreateEntity(typeof(EcsIntCleanupElement));
            var buffer = m_Manager.GetBuffer<EcsIntCleanupElement>(entity);
            int capacity = (int)(((long)int.MaxValue + 1) / UnsafeUtility.SizeOf<EcsIntCleanupElement>() + 1); //536870913
            buffer.ResizeUninitialized(capacity);

            Assert.DoesNotThrow(() => buffer.Insert(0, new EcsIntCleanupElement { Value = 99 }));
            Assert.AreEqual(capacity + 1, buffer.Length);
        }

        [Test]
        [Explicit("Takes a long time (order of seconds).")]
        public void DynamicBuffer_AddRange_NewBufferHasLongSize_DoesNotThrow()
        {
            if (IntPtr.Size == 4)
                Assert.Ignore("Test makes no sense on 32bit");

            var entity = m_Manager.CreateEntity(typeof(EcsIntElement));
            var buffer = m_Manager.GetBuffer<EcsIntElement>(entity);
            int capacity = (int)(((long)int.MaxValue + 1) / UnsafeUtility.SizeOf<EcsIntElement>() + 1); //536870913
            buffer.ResizeUninitialized(capacity);

            NativeArray<EcsIntElement> array = new NativeArray<EcsIntElement>(10, Allocator.Temp);
            Assert.DoesNotThrow(() => buffer.AddRange(array));
            Assert.AreEqual(capacity + 10, buffer.Length);
        }

        [Test]
        [Explicit("Takes a long time (order of seconds).")]
        public void DynamicBuffer_RemoveRange_MovedBufferHasLongSize_DoesNotThrow()
        {
            if (IntPtr.Size == 4)
                Assert.Ignore("Test makes no sense on 32bit");

            var entity = m_Manager.CreateEntity(typeof(EcsIntElement));
            var buffer = m_Manager.GetBuffer<EcsIntElement>(entity);
            int capacity = (int)(((long)int.MaxValue + 1) / UnsafeUtility.SizeOf<EcsIntElement>() + 2);
            buffer.ResizeUninitialized(capacity);

            Assert.AreEqual(536870914, buffer.Length);
            Assert.DoesNotThrow(() => buffer.RemoveRange(0, 1));
            Assert.AreEqual(536870913, buffer.Length);
        }

        [Test]
        [Explicit("Takes a long time (order of seconds).")]
        public void DynamicBuffer_Add_NewBufferHasLongSize_DoesNotThrow()
        {
            if (IntPtr.Size == 4)
                Assert.Ignore("Test makes no sense on 32bit");

            var arrayType = ComponentType.ReadWrite<EcsIntElement>();
            var entity = m_Manager.CreateEntity(typeof(EcsIntElement));
            var buffer = m_Manager.GetBuffer<EcsIntElement>(entity);
            int capacity = (int)(((long)int.MaxValue + 1) / UnsafeUtility.SizeOf<EcsIntElement>() + 1); //536870913

            buffer.ResizeUninitialized(capacity);

            Assert.DoesNotThrow(() => buffer.Add(1));
        }

        [Test]
        [Explicit("Takes a long time (order of seconds).")]
        public void DynamicBuffer_TrimExcess_NewBufferHasLongSize_DoesNotThrow()
        {
            if (IntPtr.Size == 4)
                Assert.Ignore("Test makes no sense on 32bit");

            var arrayType = ComponentType.ReadWrite<EcsIntElement>();
            var entity = m_Manager.CreateEntity(typeof(EcsIntElement));
            var buffer = m_Manager.GetBuffer<EcsIntElement>(entity);
            int capacity = (int)(((long)int.MaxValue + 1) / UnsafeUtility.SizeOf<EcsIntElement>() + 1); //536870913

            buffer.ResizeUninitialized(capacity);
            // cause the capacity to double
            buffer.Add(1);

            Assert.DoesNotThrow(() => buffer.TrimExcess());
            Assert.AreEqual(capacity + 1, buffer.Length);
        }

        [Test]
        public void DynamicBuffer_Reserve_IncreasesCapacity()
        {
            var arrayType = ComponentType.ReadWrite<EcsIntElement>();
            var entity = m_Manager.CreateEntity(typeof(EcsIntElement));
            var buffer = m_Manager.GetBuffer<EcsIntElement>(entity);

            buffer.EnsureCapacity(100);

            Assert.AreEqual(100, buffer.Capacity);
            Assert.AreEqual(0, buffer.Length);
        }
    }
}

#pragma warning restore 0649
#pragma warning restore 0219 // assigned but its value is never used
