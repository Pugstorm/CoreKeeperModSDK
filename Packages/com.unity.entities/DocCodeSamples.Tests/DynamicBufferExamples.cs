using Unity.Burst.Intrinsics;
using UnityEngine;
using Unity.Collections;

namespace Doc.CodeSamples.Tests
{
    #region add-in-job

    using Unity.Entities;
    using Unity.Jobs;

    [RequireMatchingQueriesForUpdate]
    public partial class CreateEntitiesWithBuffers : SystemBase
    {
        // A command buffer system executes command buffers in its own OnUpdate
        public EntityCommandBufferSystem CommandBufferSystem;

        protected override void OnCreate()
        {
            // Get the command buffer system
            CommandBufferSystem
                = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            // The command buffer to record commands,
            // which are executed by the command buffer system later in the frame
            EntityCommandBuffer.ParallelWriter commandBuffer
                = CommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
            //The DataToSpawn component tells us how many entities with buffers to create
            Entities.ForEach((Entity spawnEntity, int entityInQueryIndex, in DataToSpawn data) =>
            {
                for (int e = 0; e < data.EntityCount; e++)
                {
                    //Create a new entity for the command buffer
                    Entity newEntity = commandBuffer.CreateEntity(entityInQueryIndex);

                    //Create the dynamic buffer and add it to the new entity
                    DynamicBuffer<MyBufferElement> buffer =
                        commandBuffer.AddBuffer<MyBufferElement>(entityInQueryIndex, newEntity);

                    //Reinterpret to plain int buffer
                    DynamicBuffer<int> intBuffer = buffer.Reinterpret<int>();

                    //Optionally, populate the dynamic buffer
                    for (int j = 0; j < data.ElementCount; j++)
                    {
                        intBuffer.Add(j);
                    }
                }

                //Destroy the DataToSpawn entity since it has done its job
                commandBuffer.DestroyEntity(entityInQueryIndex, spawnEntity);
            }).ScheduleParallel();

            CommandBufferSystem.AddJobHandleForProducer(this.Dependency);
        }
    }
    #endregion

    #region declare-element

    public struct IntBufferElement : IBufferElementData
    {
        public int Value;
    }

    #endregion

    #region declare-element-full

    // InternalBufferCapacity specifies how many elements a buffer can have before
    // the buffer storage is moved outside the chunk.
    [InternalBufferCapacity(8)]
    public struct MyBufferElement : IBufferElementData
    {
        // Actual value each buffer element will store.
        public int Value;

        // The following implicit conversions are optional, but can be convenient.
        public static implicit operator int(MyBufferElement e)
        {
            return e.Value;
        }

        public static implicit operator MyBufferElement(int e)
        {
            return new MyBufferElement { Value = e };
        }
    }

    #endregion

    public struct DataToSpawn : IComponentData
    {
        public int EntityCount;
        public int ElementCount;
    }

    public partial class AddBufferSnippets : SystemBase
    {
        protected override void OnCreate()
        {
            var entity = EntityManager.CreateEntity();

            #region add-with-manager

            EntityManager.AddBuffer<MyBufferElement>(entity);

            #endregion

            #region add-with-archetype

            Entity e = EntityManager.CreateEntity(typeof(MyBufferElement));

            #endregion

            #region reinterpret-snippet

            DynamicBuffer<int> intBuffer
                = EntityManager.GetBuffer<MyBufferElement>(entity).Reinterpret<int>();

            #endregion

            #region access-manager

            DynamicBuffer<MyBufferElement> dynamicBuffer
                = EntityManager.GetBuffer<MyBufferElement>(entity);

            #endregion

            #region lookup-snippet

            BufferLookup<MyBufferElement> lookup = GetBufferLookup<MyBufferElement>();
            var buffer = lookup[entity];
            buffer.Add(17);
            buffer.RemoveAt(0);

            #endregion

            #region invalidation

            var entity1 = EntityManager.CreateEntity();
            var entity2 = EntityManager.CreateEntity();

            DynamicBuffer<MyBufferElement> buffer1
                = EntityManager.AddBuffer<MyBufferElement>(entity1);
            // This line causes a structural change and invalidates
            // the previously acquired dynamic buffer
            DynamicBuffer<MyBufferElement> buffer2
                = EntityManager.AddBuffer<MyBufferElement>(entity1);
            // This line will cause an error:
            buffer1.Add(17);

            #endregion
        }

        protected override void OnUpdate()
        {
            throw new System.NotImplementedException();
        }
    }

    #region access-in-ijobentity-system
    public partial struct AccessDynamicBufferFromJobSystem : ISystem
    {
        private BufferLookup<ExampleBufferComponent> _bufferLookup;

        public void OnCreate(ref SystemState state)
        {
            _bufferLookup = state.GetBufferLookup<ExampleBufferComponent>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _bufferLookup.Update(ref state);
            var exampleBufferAccessJob = new AccessDynamicBufferJob { BufferLookup = _bufferLookup };
            exampleBufferAccessJob.ScheduleParallel();
        }
    }
    #endregion

    #region access-in-ijobentity-job
    public partial struct AccessDynamicBufferJob : IJobEntity
    {
        [ReadOnly] public BufferLookup<ExampleBufferComponent> BufferLookup;
        public void Execute()
        {
            // ...
        }
    }
    #endregion

    #region access-buffer-system

    [RequireMatchingQueriesForUpdate]
    public partial class DynamicBufferSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var sum = 0;

            Entities.ForEach((DynamicBuffer<MyBufferElement> buffer) =>
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    sum += buffer[i].Value;
                }
            }).Run();

            Debug.Log("Sum of all buffers: " + sum);
        }
    }

    #endregion

    #region access-ijfe

    [RequireMatchingQueriesForUpdate]
    public partial class DynamicBufferForEachSystem : SystemBase
    {
        private EntityQuery query;

        //Sums the intermediate results into the final total
        public struct SumResult : IJob
        {
            [DeallocateOnJobCompletion] public NativeArray<int> sums;

            public void Execute()
            {
                int sum = 0;
                for (int i = 0; i < sums.Length; i++)
                {
                    sum += sums[i];
                }

                //Note: Debug.Log is not burst-compatible
                Debug.Log("Sum of all buffers: " + sum);
            }
        }

        //Schedules the two jobs with a dependency between them
        protected override void OnUpdate()
        {
            //Create a native array to hold the intermediate sums
            int entitiesInQuery = query.CalculateEntityCount();
            NativeArray<int> intermediateSums
                = new NativeArray<int>(entitiesInQuery, Allocator.TempJob);

            //Schedule the first job to add all the buffer elements
            Entities
                .WithStoreEntityQueryInField(ref query)
                .ForEach((int entityInQueryIndex, Entity entity, in DynamicBuffer<MyBufferElement> buffer) => {
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        intermediateSums[entityInQueryIndex] += buffer[i].Value;
                    }
                })
                .ScheduleParallel();

            // Must use a NativeArray to get data out of a Job.WithCode when scheduled to run on a background thread
            NativeArray<int> sum = new NativeArray<int>(1, Allocator.TempJob);

            //Schedule the second job, which depends on the first
            Job
                .WithDisposeOnCompletion(intermediateSums)
                .WithCode(() =>
                {
                    for (int i = 0; i < intermediateSums.Length; i++)
                    {
                        sum[0] += intermediateSums[i];
                    }
                    //if we do not specify dependencies, the job depends on the Dependency property
                }).Schedule();
            // Likewise, if we don't return a JobHandle, the system adds the job to its Dependency property

            //sum[0] will contain the result after all the jobs have finished
            this.CompleteDependency(); // Wait for the results now

            Debug.Log(sum[0]);
            sum.Dispose();
        }
    }

    #endregion

    #region access-buffers-in-chunk
    public partial class ExampleSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<ExampleBufferComponent>()
                .Build(EntityManager);
            NativeArray<ArchetypeChunk> chunks = query.ToArchetypeChunkArray(Allocator.Temp);
            for (int i = 0; i < chunks.Length; i++)
            {
                UpdateChunk(chunks[i]);
            }
        }

        private void UpdateChunk(ArchetypeChunk chunk)
        {
            // Get a BufferTypeHandle representing dynamic buffer type ExampleBufferComponent from SystemBase.
            BufferTypeHandle<ExampleBufferComponent> myElementHandle = GetBufferTypeHandle<ExampleBufferComponent>();
            // Get a BufferAccessor from the chunk.
            BufferAccessor<ExampleBufferComponent> buffers = chunk.GetBufferAccessor(ref myElementHandle);
            // Iterate through all ExampleBufferComponent buffers of each entity in the chunk.
            for (int i = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; i++)
            {
                DynamicBuffer<ExampleBufferComponent> buffer = buffers[i];
                // Iterate through all elements of the buffer.
                for (int j = 0; j < buffer.Length; j++)
                {
                    // ...
                }
            }
        }
    }
    #endregion

    #region access-chunk-job

    [RequireMatchingQueriesForUpdate]
    public partial class DynamicBufferJobSystem : SystemBase
    {
        private EntityQuery query;

        protected override void OnCreate()
        {
            //Create a query to find all entities with a dynamic buffer
            // containing MyBufferElement
            query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<MyBufferElement>()
                .Build(this);
        }

        public struct BuffersInChunks : IJobChunk
        {
            //The data type and safety object
            public BufferTypeHandle<MyBufferElement> BufferTypeHandle;

            //An array to hold the output, intermediate sums
            public NativeArray<int> sums;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                //A buffer accessor is a list of all the buffers in the chunk
                BufferAccessor<MyBufferElement> buffers
                    = chunk.GetBufferAccessor(ref BufferTypeHandle);

                ChunkEntityEnumerator enumerator =
                    new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var e))
                {
                    //An individual dynamic buffer for a specific entity
                    DynamicBuffer<MyBufferElement> buffer = buffers[e];
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        sums[e] += buffer[i].Value;
                    }
                }
            }
        }

        //Sums the intermediate results into the final total
        public struct SumResult : IJob
        {
            [DeallocateOnJobCompletion] public NativeArray<int> sums;
            public NativeArray<int> result;
            public void Execute()
            {
                for (int i = 0; i < sums.Length; i++)
                {
                    result[0] += sums[i];
                }
            }
        }

        protected override void OnUpdate()
        {
            //Create a native array to hold the intermediate sums
            int chunksInQuery = query.CalculateChunkCount();
            NativeArray<int> intermediateSums
                = new NativeArray<int>(chunksInQuery, Allocator.TempJob);

            //Schedule the first job to add all the buffer elements
            BuffersInChunks bufferJob = new BuffersInChunks();
            bufferJob.BufferTypeHandle = GetBufferTypeHandle<MyBufferElement>();
            bufferJob.sums = intermediateSums;
            this.Dependency = bufferJob.ScheduleParallel(query, this.Dependency);

            //Schedule the second job, which depends on the first
            SumResult finalSumJob = new SumResult();
            finalSumJob.sums = intermediateSums;
            NativeArray<int> finalSum = new NativeArray<int>(1, Allocator.Temp);
            finalSumJob.result = finalSum;
            this.Dependency = finalSumJob.Schedule(this.Dependency);

            this.CompleteDependency();
            Debug.Log("Sum of all buffers: " + finalSum[0]);
        }
    }

    #endregion

    #region dynamicbuffer.class

    [InternalBufferCapacity(8)]
    public struct FloatBufferElement : IBufferElementData
    {
        // Actual value each buffer element will store.
        public float Value;

        // The following implicit conversions are optional, but can be convenient.
        public static implicit operator float(FloatBufferElement e)
        {
            return e.Value;
        }

        public static implicit operator FloatBufferElement(float e)
        {
            return new FloatBufferElement { Value = e };
        }
    }

    public partial class DynamicBufferExample : SystemBase
    {
        protected override void OnUpdate()
        {
            float sum = 0;

            Entities.ForEach((DynamicBuffer<FloatBufferElement> buffer) =>
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    sum += buffer[i].Value;
                }
            }).Run();

            Debug.Log("Sum of all buffers: " + sum);
        }
    }

    #endregion

    class DynamicBufferSnippets
    {
        private void ShowAdd()
        {
            DynamicBuffer<int> buffer = new DynamicBuffer<int>();
            DynamicBuffer<int> secondBuffer = new DynamicBuffer<int>();

            #region dynamicbuffer.add

            buffer.Add(5);

            #endregion

            #region dynamicbuffer.addrange

            int[] source = { 1, 2, 3, 4, 5 };
            NativeArray<int> newElements = new NativeArray<int>(source, Allocator.Persistent);
            buffer.AddRange(newElements);

            #endregion

            #region dynamicbuffer.asnativearray

            int[] intArray = { 1, 2, 3, 4, 5 };
            NativeArray<int>.Copy(intArray, buffer.AsNativeArray());

            #endregion

            #region dynamicbuffer.capacity

            #endregion

            #region dynamicbuffer.clear

            buffer.Clear();

            #endregion

            #region dynamicbuffer.copyfrom.dynamicbuffer

            buffer.CopyFrom(secondBuffer);

            #endregion

            #region dynamicbuffer.copyfrom.nativearray

            int[] sourceArray = { 1, 2, 3, 4, 5 };
            NativeArray<int> nativeArray = new NativeArray<int>(source, Allocator.Persistent);
            buffer.CopyFrom(nativeArray);

            #endregion

            #region dynamicbuffer.copyfrom.nativeslice

            NativeSlice<int> nativeSlice = new NativeSlice<int>(nativeArray, 1, 3);
            buffer.CopyFrom(nativeSlice);

            #endregion

            #region dynamicbuffer.copyfrom.array

            int[] integerArray = { 1, 2, 3, 4, 5 };
            buffer.CopyFrom(integerArray);

            #endregion

            #region dynamicbuffer.getenumerator

            foreach (var element in buffer)
            {
                //Use element...
            }

            #endregion

            #region dynamicbuffer.getunsafeptr

            #endregion

            int insertionIndex = 2;

            #region dynamicbuffer.insert

            if (insertionIndex < buffer.Length)
                buffer.Insert(insertionIndex, 6);

            #endregion

            #region dynamicbuffer.iscreated

            #endregion

            #region dynamicbuffer.length

            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = i * i;
            }

            #endregion

            #region dynamicbuffer.removeat

            if (insertionIndex < buffer.Length)
                buffer.RemoveAt(insertionIndex);

            #endregion

            int start = 1;

            #region dynamicbuffer.removerange

            buffer.RemoveRange(start, 5);

            #endregion

            #region dynamicbuffer.reserve

            buffer.EnsureCapacity(buffer.Capacity + 10);

            #endregion

            #region dynamicbuffer.resizeuninitialized

            buffer.ResizeUninitialized(buffer.Length + 10);

            #endregion

            #region dynamicbuffer.indexoperator

            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = i * i;
            }

            #endregion

            #region dynamicbuffer.tonativearray

            NativeArray<int> copy = buffer.ToNativeArray(Allocator.Persistent);

            #endregion

            #region dynamicbuffer.trimexcess

            if (buffer.Capacity > buffer.Length)
                buffer.TrimExcess();

            #endregion
        }
    }

    [RequireMatchingQueriesForUpdate]
    public partial class ReinterpretExample : SystemBase
    {
        protected override void OnUpdate()
        {
            #region dynamicbuffer.reinterpret

            Entities.ForEach((DynamicBuffer<FloatBufferElement> buffer) =>
            {
                DynamicBuffer<float> floatBuffer = buffer.Reinterpret<float>();
                for (int i = 0; i < floatBuffer.Length; i++)
                {
                    floatBuffer[i] = i * 1.2f;
                }
            }).ScheduleParallel();

            #endregion
        }
    }

    /// Entity command buffer example code
    public class DynamicBufferECB
    {
        [InternalBufferCapacity(16)]
        public struct MyElement : IBufferElementData
        {
            public int Value;
        }

        #region dynamicbuffer.ecb

        private void Example(Entity e, Entity otherEntity)
        {
            EntityCommandBuffer ecb = new(Allocator.TempJob);

            // Record a command to remove the MyElement dynamic buffer from an entity.
            ecb.RemoveComponent<MyElement>(e);

            // Record a command to add a MyElement dynamic buffer to an existing entity.
            // This doesn't fail if the target entity already contains the buffer component.
            // The data of the returned DynamicBuffer is stored in the EntityCommandBuffer, 
            // so changes to the returned buffer are also recorded changes. 
            DynamicBuffer<MyElement> myBuff = ecb.AddBuffer<MyElement>(e);

            // After playback, the entity will have a MyElement buffer with 
            // Length 20 and these recorded values.
            myBuff.Length = 20;
            myBuff[0] = new MyElement { Value = 5 };
            myBuff[3] = new MyElement { Value = -9 };

            // SetBuffer is like AddBuffer, but safety checks will throw an exception at playback if 
            // the entity doesn't already have a MyElement buffer. 
            DynamicBuffer<MyElement> otherBuf = ecb.SetBuffer<MyElement>(otherEntity);

            // Records a MyElement value to append to the buffer. Throws an exception at 
            // playback if the entity doesn't already have a MyElement buffer. 
            // ecb.AddComponent<MyElement>(otherEntity) is a safe way to ensure a buffer 
            // exists before appending to it.
            ecb.AppendToBuffer(otherEntity, new MyElement { Value = 12 });
        }

        #endregion

    }

    
}
