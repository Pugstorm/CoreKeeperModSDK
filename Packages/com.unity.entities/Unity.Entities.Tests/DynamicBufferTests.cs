using System;
using System.Diagnostics;
using NUnit.Framework;
using Unity.Jobs;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using static Unity.Burst.CompilerServices.Aliasing;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

namespace Unity.Entities.Tests
{
    public partial class DynamicBufferTests : ECSTestsFixture
    {
        [DebuggerDisplay("Value: {Value}")]
        struct DynamicBufferElement : IBufferElementData
        {
            public DynamicBufferElement(int value)
            {
                Value = value;
            }

            public int Value;
        }

        [Test]
        public void CopyFromDynamicBuffer([Values(0, 1, 2, 3, 64)] int srcBufferLength)
        {
            var srcEntity = m_Manager.CreateEntity(typeof(DynamicBufferElement));
            var dstEntity = m_Manager.CreateEntity(typeof(DynamicBufferElement));
            var src = m_Manager.GetBuffer<DynamicBufferElement>(srcEntity);
            var dst = m_Manager.GetBuffer<DynamicBufferElement>(dstEntity);

            src.EnsureCapacity(srcBufferLength);
            for (var i = 0; i < srcBufferLength; ++i)
            {
                src.Add(new DynamicBufferElement() {Value = i});
            }

            dst.EnsureCapacity(2);

            for (var i = 0; i < 2; ++i)
            {
                dst.Add(new DynamicBufferElement() {Value = 0});
            }

            Assert.DoesNotThrow(() => dst.CopyFrom(src));

            Assert.AreEqual(src.Length, dst.Length);

            for (var i = 0; i < src.Length; ++i)
            {
                Assert.AreEqual(i, src[i].Value);
                Assert.AreEqual(src[i].Value, dst[i].Value);
            }
        }

        [Test]
        public void CopyFromArray([Values(0, 1, 2, 3, 64)] int srcBufferLength)
        {
            var dstEntity = m_Manager.CreateEntity(typeof(DynamicBufferElement));
            var src = new DynamicBufferElement[srcBufferLength];
            var dst = m_Manager.GetBuffer<DynamicBufferElement>(dstEntity);

            for (var i = 0; i < srcBufferLength; ++i)
            {
                src[i] = new DynamicBufferElement() {Value = i};
            }

            dst.EnsureCapacity(2);

            for (var i = 0; i < 2; ++i)
            {
                dst.Add(new DynamicBufferElement() {Value = 0});
            }

            Assert.DoesNotThrow(() => dst.CopyFrom(src));

            Assert.AreEqual(src.Length, dst.Length);

            for (var i = 0; i < src.Length; ++i)
            {
                Assert.AreEqual(i, src[i].Value);
                Assert.AreEqual(src[i].Value, dst[i].Value);
            }
        }

        [Test]
        public void CopyFromDynamicBufferToEmptyDestination()
        {
            var srcEntity = m_Manager.CreateEntity(typeof(DynamicBufferElement));
            var dstEntity = m_Manager.CreateEntity(typeof(DynamicBufferElement));
            var src = m_Manager.GetBuffer<DynamicBufferElement>(srcEntity);
            var dst = m_Manager.GetBuffer<DynamicBufferElement>(dstEntity);

            src.EnsureCapacity(64);
            for (var i = 0; i < 64; ++i)
            {
                src.Add(new DynamicBufferElement() {Value = i});
            }

            Assert.DoesNotThrow(() => dst.CopyFrom(src));

            Assert.AreEqual(src.Length, dst.Length);

            for (var i = 0; i < src.Length; ++i)
            {
                Assert.AreEqual(i, src[i].Value);
                Assert.AreEqual(src[i].Value, dst[i].Value);
            }
        }

        [Test]
        public void CopyFromNullSource()
        {
            var dstEntity = m_Manager.CreateEntity(typeof(DynamicBufferElement));
            var dst = m_Manager.GetBuffer<DynamicBufferElement>(dstEntity);

            Assert.Throws<ArgumentNullException>(() => dst.CopyFrom(null));
        }

        [Test]
        public void SetCapacity()
        {
            var dstEntity = m_Manager.CreateEntity(typeof(DynamicBufferElement));
            var dst = m_Manager.GetBuffer<DynamicBufferElement>(dstEntity);
            dst.Add(new DynamicBufferElement(){Value = 0});
            dst.Add(new DynamicBufferElement(){Value = 1});
            dst.Capacity = 100;
            Assert.AreEqual(100, dst.Capacity);
            Assert.AreEqual(dst[0], new DynamicBufferElement(){Value = 0});
            Assert.AreEqual(dst[1], new DynamicBufferElement(){Value = 1});
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires dynamic buffer safety checks")]
        public void SetCapacitySmallerThanLengthThrows()
        {
            var dstEntity = m_Manager.CreateEntity(typeof(DynamicBufferElement));
            var dst = m_Manager.GetBuffer<DynamicBufferElement>(dstEntity);
            dst.Add(new DynamicBufferElement(){Value = 0});
            dst.Add(new DynamicBufferElement(){Value = 1});
            Assert.Throws<InvalidOperationException>(() => dst.Capacity = 1);
        }

        [Test]
        public void SetCapacitySmallerActuallyShrinksBuffer()
        {
            var dstEntity = m_Manager.CreateEntity(typeof(DynamicBufferElement));
            var dst = m_Manager.GetBuffer<DynamicBufferElement>(dstEntity);
            dst.Capacity = 1000;
            Assert.AreEqual(1000, dst.Capacity);
            dst.Capacity = 100;
            Assert.AreEqual(100, dst.Capacity);
        }

        [Test]
        public void SetCapacityZeroWorks()
        {
            var dstEntity = m_Manager.CreateEntity(typeof(DynamicBufferElement));
            var dst = m_Manager.GetBuffer<DynamicBufferElement>(dstEntity);
            dst.Capacity = 0;
            Assert.AreEqual(0, dst.Capacity);
            dst.Capacity = 100;
            Assert.AreEqual(100, dst.Capacity);
            dst.Capacity = 0;
            Assert.AreEqual(0, dst.Capacity);
        }

        [Test]
        public void DynamicBufferResize_Clears()
        {
            var dstEntity = m_Manager.CreateEntity(typeof(DynamicBufferElement));
            var buf = m_Manager.GetBuffer<DynamicBufferElement>(dstEntity);
            buf.Add(new DynamicBufferElement(3));
            buf.Add(new DynamicBufferElement(5));
            buf.Resize(10, NativeArrayOptions.ClearMemory);

            Assert.AreEqual(3, buf[0].Value);
            Assert.AreEqual(5, buf[1].Value);
            Assert.AreEqual(10, buf.Length);
            for(int i = 2;i != 10;i++)
                Assert.AreEqual(0, buf[i].Value);
        }

        [Test]
        public unsafe void DynamicBuffer_GetUnsafePtr_ReadOnlyAndReadWriteAreEqual()
        {
            var ent = m_Manager.CreateEntity(typeof(DynamicBufferElement));
            var buf = m_Manager.GetBuffer<DynamicBufferElement>(ent);
            Assert.AreEqual((UIntPtr)buf.GetUnsafePtr(), (UIntPtr)buf.GetUnsafeReadOnlyPtr());
        }

        struct DynamicBufferContainerJob : IJob
        {
            public DynamicBuffer<EcsTestContainerElement> buffer;

            public void Execute() { }
        }

        [Test]
        public void DynamicBuffer_ElementWithContainer_Works()
        {
            var entity = m_Manager.CreateEntity();
            var element = new EcsTestContainerElement();
            element.Create();
            var bufferA = m_Manager.AddBuffer<EcsTestContainerElement>(entity);
            bufferA.Add(element);

            var bufferB = m_Manager.GetBuffer<EcsTestContainerElement>(entity);

            Assert.AreEqual(bufferB[0], element);
            Assert.AreEqual(bufferB[0].data[1], element.data[1]);

            element.Destroy();
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on jobs debugger")]
        public void DynamicBuffer_ElementWithContainerInJob_Throws()
        {
            var job = new DynamicBufferContainerJob();
            var entity = m_Manager.CreateEntity();
            var element = new EcsTestContainerElement();
            var buffer = m_Manager.AddBuffer<EcsTestContainerElement>(entity);
            buffer.Add(element);

            job.buffer = buffer;

            var e = Assert.Throws<InvalidOperationException>(() => job.Schedule());
            Assert.IsTrue(e.Message.Contains("Nested native containers are illegal in jobs"));
        }

        struct BufferJob_ReadOnly : IJobChunk
        {
            [ReadOnly]
            public BufferTypeHandle<EcsIntElement> BufferTypeRO;

            public NativeArray<int> IntArray;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // This job is not written to support queries with enableable component types.
                Assert.IsFalse(useEnabledMask);

                var buffer = chunk.GetBufferAccessor(ref BufferTypeRO)[0];
                IntArray[0] += buffer.Length;
            }
        }

        struct BufferJob_ReadWrite : IJobChunk
        {
            public BufferTypeHandle<EcsIntElement> BufferTypeRW;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // This job is not written to support queries with enableable component types.
                Assert.IsFalse(useEnabledMask);

                var buffer = chunk.GetBufferAccessor(ref BufferTypeRW)[0];
                buffer.Add(10);
            }
        }

        struct BufferJob_ReadOnly_FromEntity : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle EntityTypeRO;

            [ReadOnly]
            public BufferLookup<EcsIntElement> BufferLookupRo;

            public NativeArray<int> IntArray;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // This job is not written to support queries with enableable component types.
                Assert.IsFalse(useEnabledMask);

                var entity = chunk.GetNativeArray(EntityTypeRO)[0];
                var buffer = BufferLookupRo[entity];
                IntArray[0] += buffer.Length;
            }
        }

        struct BufferJob_ReadWrite_FromEntity : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle EntityTypeRO;

            public BufferLookup<EcsIntElement> BufferLookupRw;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // This job is not written to support queries with enableable component types.
                Assert.IsFalse(useEnabledMask);

                var entity = chunk.GetNativeArray(EntityTypeRO)[0];
                var buffer = BufferLookupRw[entity];
                buffer.Add(10);
            }
        }

       [Test]
       public void ReadOnlyBufferDoesNotBumpVersionNumber()
       {
           m_ManagerDebug.SetGlobalSystemVersion(10);
           var entity = m_Manager.CreateEntity(typeof(EcsIntElement));

           var queryRO = m_Manager.CreateEntityQuery(ComponentType.ReadOnly<EcsIntElement>());

           var queryRW = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsIntElement>());
           queryRW.SetChangedVersionFilter(typeof(EcsIntElement));
           queryRW.SetChangedFilterRequiredVersion(10);

           new BufferJob_ReadOnly
           {
               BufferTypeRO = m_Manager.GetBufferTypeHandle<EcsIntElement>(true),
               IntArray = CollectionHelper.CreateNativeArray<int>(1, World.UpdateAllocator.ToAllocator)
           }.Run(queryRO);

           // Should not process any chunks due to version filtering
           new BufferJob_ReadWrite
           {
               BufferTypeRW = m_Manager.GetBufferTypeHandle<EcsIntElement>(false)
           }.Run(queryRW);

           var buffer = m_Manager.GetBuffer<EcsIntElement>(entity);
           Assert.AreEqual(0, buffer.Length);
       }

       [Test]
       public void ReadOnlyBufferDoesNotBumpVersionNumber_BufferLookup()
       {
           m_ManagerDebug.SetGlobalSystemVersion(10);
           var entity = m_Manager.CreateEntity(typeof(EcsIntElement));

           var queryRO = m_Manager.CreateEntityQuery(ComponentType.ReadOnly<EcsIntElement>());

           var queryRW = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsIntElement>());
           queryRW.SetChangedVersionFilter(typeof(EcsIntElement));
           queryRW.SetChangedFilterRequiredVersion(10);

           new BufferJob_ReadOnly_FromEntity
           {
               EntityTypeRO = m_Manager.GetEntityTypeHandle(),
               BufferLookupRo = m_Manager.GetBufferLookup<EcsIntElement>(true),
               IntArray = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(1, ref World.UpdateAllocator)
           }.Run(queryRO);

           // Should not process any chunks due to version filtering
           new BufferJob_ReadWrite_FromEntity
           {
               EntityTypeRO = m_Manager.GetEntityTypeHandle(),
               BufferLookupRw = m_Manager.GetBufferLookup<EcsIntElement>(false),
           }.Run(queryRW);

           var buffer = m_Manager.GetBuffer<EcsIntElement>(entity);
           Assert.AreEqual(0, buffer.Length);
       }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void WritingToReadOnlyBufferTriggersSafetySystem()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsIntElement));
            var queryRO = m_Manager.CreateEntityQuery(ComponentType.ReadOnly<EcsIntElement>());

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));
            new BufferJob_ReadWrite{BufferTypeRW = m_Manager.GetBufferTypeHandle<EcsIntElement>(true)}.Run(queryRO);
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void WritingToReadOnlyBufferTriggersSafetySystem_BufferLookup()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsIntElement));
            var queryRO = m_Manager.CreateEntityQuery(ComponentType.ReadOnly<EcsIntElement>());

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));
            new BufferJob_ReadWrite_FromEntity{EntityTypeRO = m_Manager.GetEntityTypeHandle(), BufferLookupRw = m_Manager.GetBufferLookup<EcsIntElement>(true)}.Run(queryRO);
        }

        public partial class DynamicBufferTestsSystem : SystemBase
        {
            private struct DynamicBufferData1 : IBufferElementData
            {
                public int A;

                public DynamicBufferData1(int a)
                {
                    A = a;
                }
            }
            private struct DynamicBufferData2 : IBufferElementData
            {
                public float A;

                public DynamicBufferData2(float a)
                {
                    A = a;
                }
            }
            private struct DynamicBufferData3 : IBufferElementData
            {
                public double A;

                public DynamicBufferData3(double a)
                {
                    A = a;
                }
            }

            protected override void OnUpdate()
            {
                Entities
                .ForEach((ref EcsTestData d1, ref EcsTestData2 d2, ref EcsTestData3 d3) =>
                {
                    ExpectNotAliased(in d1, in d2);
                    ExpectNotAliased(in d1, in d3);
                    ExpectNotAliased(in d2, in d3);
                })
                .WithoutBurst() // See "DOTS-3029"
                // .WithBurst(synchronousCompilation: true)
                .Run();

                Entities
                .ForEach((in DynamicBuffer<DynamicBufferData1> d1, in DynamicBuffer<DynamicBufferData2> d2, in DynamicBuffer<DynamicBufferData3> d3) =>
                {
                    unsafe
                    {
                        // They obviously alias with themselves.
                        ExpectAliased(d1.GetUnsafePtr(), d1.GetUnsafePtr());
                        ExpectAliased(d2.GetUnsafePtr(), d2.GetUnsafePtr());
                        ExpectAliased(d3.GetUnsafePtr(), d3.GetUnsafePtr());

                        // They do not alias with each other though because they are
                        ExpectNotAliased(d1.GetUnsafePtr(), d2.GetUnsafePtr());
                        ExpectNotAliased(d1.GetUnsafePtr(), d3.GetUnsafePtr());
                        ExpectNotAliased(d2.GetUnsafePtr(), d3.GetUnsafePtr());

                        // Check that it does indeed alias with a copy of itself.
                        var copyBuffer = d1;
                        ExpectAliased(copyBuffer.GetUnsafePtr(), d1.GetUnsafePtr());
                    }
                })
                .WithoutBurst() // See "DOTS-3029"
                // .WithBurst(synchronousCompilation: true)
                .Run();
            }
        }

        // Does not work with AOT burst builds as this test relies on generating Burst compiler warnings/errors
        [Test, Ignore("DOTS-3029")]
        public void DynamicBufferAliasing()
        {
            World.GetOrCreateSystemManaged<DynamicBufferTestsSystem>().Update();
        }

        // @TODO: when 2019.1 support is dropped this can be shared with the collections tests:
        // until then the package validation will fail otherwise when collections is not marked testable
        // since we can not have shared test code between packages in 2019.1
        static class GCAllocRecorderForDynamicBuffer
        {
            static UnityEngine.Profiling.Recorder AllocRecorder;

            static GCAllocRecorderForDynamicBuffer()
            {
                AllocRecorder = UnityEngine.Profiling.Recorder.Get("GC.Alloc.DynamicBuffer");
            }

            static int CountGCAllocs(Action action)
            {
                AllocRecorder.FilterToCurrentThread();
                AllocRecorder.enabled = false;
                AllocRecorder.enabled = true;

                action();

                AllocRecorder.enabled = false;
                return AllocRecorder.sampleBlockCount;
            }

            // NOTE: action is called twice to warmup any GC allocs that can happen due to static constructors etc.
            public static void ValidateNoGCAllocs(Action action)
            {
                CountGCAllocs(action);

                var count = CountGCAllocs(action);
                if (count != 0)
                    throw new AssertionException($"Expected 0 GC allocations but there were {count}");
            }
        }

        [Test]
        public void DynamicBufferForEach()
        {
            var dstEntity = m_Manager.CreateEntity(typeof(DynamicBufferElement));
            var buf = m_Manager.GetBuffer<DynamicBufferElement>(dstEntity);
            buf.Add(new DynamicBufferElement(3));
            buf.Add(new DynamicBufferElement(5));

            int count = 0, sum = 0;
            GCAllocRecorderForDynamicBuffer.ValidateNoGCAllocs(() =>
            {
                count = 0;
                sum = 0;
                foreach (var value in buf)
                {
                    sum += value.Value;
                    count++;
                }
            });
            Assert.AreEqual(2, count);
            Assert.AreEqual(8, sum);
        }
    }
}
