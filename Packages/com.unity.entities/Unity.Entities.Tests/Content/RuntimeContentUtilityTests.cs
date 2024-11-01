using System;
using System.Threading;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using Unity.Jobs;

namespace Unity.Entities.Tests.Content
{
    public class RuntimeContentUtilityTests
    {
        struct ProducerConsumerSharedData : IDisposable
        {
            public int totalVal;
            public int iterationCount;
            public int currentCount;
            public int expectedCount;
            public MultiProducerSingleBulkConsumerQueue<int> queue;
            public ObjectValueCache status;
            public ProducerConsumerSharedData(int queueCount, int objValCount, int expectedTotalVals)
            {
                totalVal = iterationCount = currentCount = 0;
                expectedCount = expectedTotalVals;
                queue = new MultiProducerSingleBulkConsumerQueue<int>(8);
                status = new ObjectValueCache(objValCount);
            }

            public void Dispose()
            {
                queue.Dispose();
                status.Dispose();
            }
        }

        static readonly SharedStatic<ProducerConsumerSharedData> sharedResults = SharedStatic<ProducerConsumerSharedData>.GetOrCreate<RuntimeContentUtilityTests>();

        unsafe struct ProduceValuesJob : IJob
        {
            public UntypedWeakReferenceId id;
            public int count;
            public void Execute()
            {
                sharedResults.Data.status.SetObjectStatus(id, ObjectLoadingStatus.Loading, default);
                for (int i = 0; i < count; i++)
                {
                    Thread.Sleep(3);
                    sharedResults.Data.queue.Produce(i);
                    sharedResults.Data.status.SetObjectStatus(id, ObjectLoadingStatus.Loading, default);
                }
                sharedResults.Data.status.SetObjectStatus(id, ObjectLoadingStatus.Completed, default);
            }
        }

        unsafe struct ConsumeValuesJob : IJob
        {
            public void Execute()
            {
                while (sharedResults.Data.currentCount < sharedResults.Data.expectedCount)
                {
                    if (sharedResults.Data.queue.ConsumeAll(out var vals, Allocator.Temp))
                    {
                        for (int i = 0; i < vals.Length; i++)
                            sharedResults.Data.totalVal += vals[i];
                        sharedResults.Data.currentCount += vals.Length;
                        vals.Dispose();
                    }
                    sharedResults.Data.iterationCount++;
                    Thread.Sleep(3);
                }
            }
        }

        unsafe struct GetStatusJob : IJob
        {
            public UntypedWeakReferenceId id;
            public void Execute()
            {
                while (sharedResults.Data.status.GetLoadingStatus(id) < ObjectLoadingStatus.Completed)
                {
                    Thread.Sleep(1);
                }
            }
        }

        [Test]
        public void MultiProducerSingleBulkConsumerQueueTests([Values(4, 8)] int producerCount, [Values(1024, 1024 * 8)] int valueCount)
        {
            sharedResults.Data = new ProducerConsumerSharedData(8, 8, valueCount);
            var statusJobs = new NativeArray<JobHandle>(producerCount, Allocator.Persistent);
            var producerJobs = new NativeArray<JobHandle>(producerCount, Allocator.Persistent);

            for (int i = 0; i < producerCount; i++)
            {
                var id = new UntypedWeakReferenceId { GlobalId = new RuntimeGlobalObjectId { AssetGUID = new Hash128((uint)i, 0, 0, 0) } };

                producerJobs[i] = new ProduceValuesJob() { count = valueCount / producerCount, id = id }.Schedule();
                statusJobs[i] = new GetStatusJob { id = id }.Schedule();
#if !UNITY_SINGLETHREADED_JOBS
                JobHandle.ScheduleBatchedJobs();
#endif
            }

            var handle = new ConsumeValuesJob().Schedule();
            JobHandle.CompleteAll(producerJobs);
            JobHandle.CompleteAll(statusJobs);
            handle.Complete();

            Assert.AreEqual(valueCount, sharedResults.Data.currentCount);
            sharedResults.Data.Dispose();
            statusJobs.Dispose();
            producerJobs.Dispose();
        }
    }
}
