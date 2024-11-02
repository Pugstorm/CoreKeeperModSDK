using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.Entities.Baking
{
#if !USE_NATIVE_STREAM
    /// <summary>
    /// An unmanaged, resizable list per thread.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    internal struct UnsafeDependencyStream<T>        : IDisposable, IEnumerable<T>
                                                        where T : unmanaged, IEquatable<T>
    {
        private UnsafeList<UnsafeList<T>> listPerThread;

        /// <summary>
        /// Initializes and returns an instance of UnsafeDependencyStream.
        /// </summary>
        /// <param name="allocator">The allocator to use.</param>
        public UnsafeDependencyStream(AllocatorManager.AllocatorHandle allocator)
        {
#if UNITY_2022_2_14F1_OR_NEWER
            int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
            int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
            listPerThread = new UnsafeList<UnsafeList<T>>(maxThreadCount, allocator);
            for (int index = 0; index < maxThreadCount; ++index)
            {
                listPerThread.Add(new UnsafeList<T>(0, allocator));
            }
        }

        /// <summary>
        /// Adds an item to the list associated with the threadIndex
        /// </summary>
        /// <param name="element">Element to add to the list.</param>
        /// <param name="threadIndex">Thread index provided by NativeSetThreadIndex</param>
        public void Add(T element, int threadIndex)
        {
            ref UnsafeList<T> threadList = ref listPerThread.ElementAt(threadIndex);
            threadList.Add(element);
        }

        /// <summary>
        /// Releases all resources (memory).
        /// </summary>
        public void Dispose()
        {
            // Release

#if UNITY_2022_2_14F1_OR_NEWER
            int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
            int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
            for (int index = 0; index < maxThreadCount; ++index)
            {
                listPerThread[index].Dispose();
            }
            listPerThread.Dispose();
        }

        public void BeginWriting()
        {

        }

        public JobHandle EndWriting(JobHandle dependency)
        {
            return dependency;
        }

        public JobHandle CopyTo(UnsafeParallelHashSet<T> outputHashSet, JobHandle dependency)
        {
            var resizeHashSetJob = new ResizeHashSetJob()
            {
                changedComponentsPerThread = listPerThread,
                outputChangedComponents = outputHashSet
            };

            var resizeHashSetJobHandle = resizeHashSetJob.Schedule(dependency);
            var ComposeHashSetJob = new ComposeHashSetJob()
            {
                changedComponentsPerThread = listPerThread,
                outputChangedComponentsWriter = outputHashSet.AsParallelWriter()
            };

            return ComposeHashSetJob.Schedule(listPerThread.Length, 2, resizeHashSetJobHandle);
        }

        [BurstCompile]
        struct ResizeHashSetJob : IJob
        {
            [ReadOnly]
            public UnsafeList<UnsafeList<T>> changedComponentsPerThread;

            public UnsafeParallelHashSet<T> outputChangedComponents;

            public void Execute()
            {
#if UNITY_2022_2_14F1_OR_NEWER
                int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
                int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
                int count = 0;
                for (int index = 0; index < maxThreadCount; ++index)
                    count += changedComponentsPerThread[index].Length;

                if (outputChangedComponents.Capacity < count)
                {
                    outputChangedComponents.Capacity = count;
                }
            }
        }

        [BurstCompile]
        struct ComposeHashSetJob : IJobParallelFor
        {
            [ReadOnly]
            public UnsafeList<UnsafeList<T>> changedComponentsPerThread;

            public UnsafeParallelHashSet<T>.ParallelWriter outputChangedComponentsWriter;

            public void Execute(int i)
            {
                var threadList = changedComponentsPerThread[i];
                for (int index = 0; index < threadList.Length; ++index)
                {
                    outputChangedComponentsWriter.Add(threadList[index]);
                }
            }
        }

        public struct Enumerator : IEnumerator<T>, IEnumerator, IDisposable
        {
            private UnsafeDependencyStream<T> m_Internal;
            private int m_GlobalIndex;
            private int m_InternalIndex;

            public Enumerator(ref UnsafeDependencyStream<T> array)
            {
                this.m_Internal = array;
                this.m_GlobalIndex = 0;
                this.m_InternalIndex = -1;
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                while (m_GlobalIndex < m_Internal.listPerThread.Length)
                {
                    ++m_InternalIndex;
                    if (m_InternalIndex < m_Internal.listPerThread[m_GlobalIndex].Length)
                    {
                        return true;
                    }

                    // Move to the next list
                    m_InternalIndex = -1;
                    ++m_GlobalIndex;
                }
                return false;
            }

            public void Reset()
            {
                this.m_GlobalIndex = 0;
                this.m_InternalIndex = -1;
            }

            public T Current => this.m_Internal.listPerThread[this.m_GlobalIndex][m_InternalIndex];

            object IEnumerator.Current => (object) this.Current;
        }

        public UnsafeDependencyStream<T>.Enumerator GetEnumerator() => new UnsafeDependencyStream<T>.Enumerator(ref this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => (IEnumerator<T>) new UnsafeDependencyStream<T>.Enumerator(ref this);

        IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();
    }
#else
    /// <summary>
    /// This wrapper is to handle the stream writers in the main thread and then shared them across different types of jobs
    /// It also handle the stream readers to copy the data to a HashSet
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    internal struct UnsafeDependencyStream<T>        :  IDisposable
                                                        where T : unmanaged, IEquatable<T>
    {
        private UnsafeStream stream;
        private UnsafeList<UnsafeStream.Writer> writers;

        /// <summary>
        /// Creates an UnsafeStream where bufferCount is the maximum number of threads that can run jobs at once
        /// </summary>
        public UnsafeDependencyStream(AllocatorManager.AllocatorHandle allocator)
        {
#if UNITY_2022_2_14F1_OR_NEWER
            int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
            int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
            stream = new UnsafeStream(maxThreadCount, Allocator.TempJob);
            writers = new UnsafeList<UnsafeStream.Writer>(maxThreadCount, Allocator.TempJob);
        }

        /// <summary>
        /// Creates stream.ForEachCount number of writers and it calls BeginForEachIndex on each of them
        /// </summary>
        public void BeginWriting()
        {
            for (int index = 0; index < stream.ForEachCount; ++index)
            {
                writers.Add(stream.AsWriter());
                ref var writer = ref writers.ElementAt(index);
                writer.BeginForEachIndex(index);
            }
        }

        /// <summary>
        /// Calls EndForEachIndex for all the writers
        /// </summary>
        public void EndWriting()
        {
            for (int index = 0; index < writers.Length; ++index)
            {
                ref var writer = ref writers.ElementAt(index);
                writer.EndForEachIndex();
            }
        }

        /// <summary>
        /// Calls EndForEachIndex for all the writers in a job
        /// </summary>
        /// <param name="inputDeps">A job handle. The newly scheduled job will depend upon this handle.</param>
        /// <returns>The handle of a new job that will call EndForEachIndex for each writer.</returns>
        public JobHandle EndWriting(JobHandle inputDeps)
        {
            var endWritingJob = new EndWritingJob()
            {
                stream = this,
            };
            return endWritingJob.Schedule(inputDeps);
        }

        /// <summary>
        /// Adds an item to the writer associated with the threadIndex
        /// </summary>
        /// <param name="element">Element to add to the stream.</param>
        /// <param name="threadIndex">Thread index provided by NativeSetThreadIndex</param>
        public void Add(T element, int threadIndex)
        {
            //ref UnsafeList<T> threadList = ref listPerThread.ElementAt(threadIndex);
            //threadList.Add(element);
            ref var writer = ref writers.ElementAt(threadIndex);
            writer.Write(element);
        }

        /// <summary>
        /// Releases all resources (memory).
        /// </summary>
        public void Dispose()
        {
            // Release
            stream.Dispose();
            writers.Dispose();
        }

        /// <summary>
        /// Copies on a Job the content of the stream into the UnsafeParallelHashSet provided
        /// </summary>
        /// <param name="outputHashSet">UnsafeParallelHashSet where the content is going to be added.</param>
        /// <param name="inputDeps">A job handle. The newly scheduled job will depend upon this handle.</param>
        /// <returns>The handle of a new job that will perform the copy.</returns>
        public JobHandle CopyTo(UnsafeParallelHashSet<T> outputHashSet, JobHandle inputDeps)
        {
            var resizeHashSetJob = new ResizeHashSetJob()
            {
                stream = this,
                outputHashSet = outputHashSet
            };

            UnsafeList<UnsafeStream.Reader> readers = new UnsafeList<UnsafeStream.Reader>(stream.ForEachCount, Allocator.TempJob);
            for (int index = 0; index < stream.ForEachCount; ++index)
            {
                readers.Add(stream.AsReader());
            }

            var resizeHashSetJobHandle = resizeHashSetJob.Schedule(inputDeps);
            var ComposeHashSetJob = new ComposeHashSetJob()
            {
                readers = readers,
                outputHashSet = outputHashSet.AsParallelWriter()
            };

            var composeHashSetJobHandle = ComposeHashSetJob.Schedule(stream.ForEachCount, 2, resizeHashSetJobHandle);
            readers.Dispose(composeHashSetJobHandle);
            return composeHashSetJobHandle;
        }

        /// <summary>
        /// Job to call EndForEachIndex on all the writers
        /// </summary>
        [BurstCompile]
        struct EndWritingJob : IJob
        {
            public UnsafeDependencyStream<T> stream;

            public void Execute()
            {
                stream.EndWriting();
            }
        }

        /// <summary>
        /// Job to resize the UnsafeParallelHashSet so it doesn't run out of memory when inserting the values
        /// </summary>
        [BurstCompile]
        struct ResizeHashSetJob : IJob
        {
            [ReadOnly]
            public UnsafeDependencyStream<T> stream;

            public UnsafeParallelHashSet<T> outputHashSet;

            public void Execute()
            {
                int count = stream.stream.Count();
                if (outputHashSet.Capacity < count)
                {
                    outputHashSet.Capacity = count;
                }
            }
        }

        /// <summary>
        /// Job to insert all the values inside the UnsafeParallelHashSet.
        /// </summary>
        [BurstCompile]
        struct ComposeHashSetJob : IJobParallelFor
        {
            [ReadOnly]
            public UnsafeList<UnsafeStream.Reader> readers;

            public UnsafeParallelHashSet<T>.ParallelWriter outputHashSet;

            public void Execute(int i)
            {
                ref var reader = ref readers.ElementAt(i);
                int count = reader.BeginForEachIndex(i);

                for (int index = 0; index < count; ++index)
                {
                    outputHashSet.Add(reader.Read<T>());
                }

                readers[i].EndForEachIndex();
            }
        }
    }
#endif
}
