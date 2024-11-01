#pragma warning disable 0219
#line 1 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
using Unity.Entities;
[global::System.Runtime.CompilerServices.CompilerGenerated]
partial class EntitiesForEachDynamicBuffer
{
    [global::Unity.Entities.DOTSCompilerPatchedMethod("OnUpdate_T0")]
    void __OnUpdate_450AADF4()
    {
        #line 10 "/0/Test0.cs"

        EntitiesForEachDynamicBuffer_7418F297_LambdaJob_0_Execute();
    }

    #line 16 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
    [global::Unity.Burst.NoAlias]
    [global::Unity.Burst.BurstCompile]
    struct EntitiesForEachDynamicBuffer_7418F297_LambdaJob_0_Job : global::Unity.Entities.IJobChunk
    {
        public BufferTypeHandle<BufferData> __bufTypeHandle;
        
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        void OriginalLambdaBody(DynamicBuffer<BufferData> buf)
        { }
        #line 26 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        public void Execute(in global::Unity.Entities.ArchetypeChunk chunk, int batchIndex, bool useEnabledMask, in global::Unity.Burst.Intrinsics.v128 chunkEnabledMask)
        {
            #line 30 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
            var bufAccessor = chunk.GetBufferAccessor(ref __bufTypeHandle);
            int chunkEntityCount = chunk.Count;
            if (!useEnabledMask)
            {
                for(var entityIndex = 0; entityIndex < chunkEntityCount; ++entityIndex)
                {
                    OriginalLambdaBody(bufAccessor[entityIndex]);
                }
            }
            else
            {
                int edgeCount = global::Unity.Mathematics.math.countbits(chunkEnabledMask.ULong0 ^ (chunkEnabledMask.ULong0 << 1)) + global::Unity.Mathematics.math.countbits(chunkEnabledMask.ULong1 ^ (chunkEnabledMask.ULong1 << 1)) - 1;
                bool useRanges = edgeCount <= 4;
                if (useRanges)
                {
                    int entityIndex = 0;
                    int batchEndIndex = 0;
                    while (global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeTryGetNextEnabledBitRange(chunkEnabledMask, batchEndIndex, out entityIndex, out batchEndIndex))
                    {
                        while (entityIndex < batchEndIndex)
                        {
                            OriginalLambdaBody(bufAccessor[entityIndex]);
                            entityIndex++;
                        }
                    }
                }
                else
                {
                    ulong mask64 = chunkEnabledMask.ULong0;
                    int count = global::Unity.Mathematics.math.min(64, chunkEntityCount);
                    for (var entityIndex = 0; entityIndex < count; ++entityIndex)
                    {
                        if ((mask64 & 1) != 0)
                        {
                            OriginalLambdaBody(bufAccessor[entityIndex]);
                        }
                        mask64 >>= 1;
                    }
                    mask64 = chunkEnabledMask.ULong1;
                    for (var entityIndex = 64; entityIndex < chunkEntityCount; ++entityIndex)
                    {
                        if ((mask64 & 1) != 0)
                        {
                            OriginalLambdaBody(bufAccessor[entityIndex]);
                        }
                        mask64 >>= 1;
                    }
                }
            }
        }
        [global::Unity.Burst.BurstCompile]
        public static void RunWithoutJobSystem(ref global::Unity.Entities.EntityQuery query, global::System.IntPtr jobPtr)
        {
            try
            {
                ref var jobData = ref global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeAsRef<EntitiesForEachDynamicBuffer_7418F297_LambdaJob_0_Job>(jobPtr);
                global::Unity.Entities.Internal.InternalCompilerInterface.JobChunkInterface.RunWithoutJobsInternal(ref jobData, ref query);
            }
            finally
            {
            }
        }
    }
    void EntitiesForEachDynamicBuffer_7418F297_LambdaJob_0_Execute()
    {
        __TypeHandle.__BufferData_RW_BufferTypeHandle.Update(ref this.CheckedStateRef);
        var __job = new EntitiesForEachDynamicBuffer_7418F297_LambdaJob_0_Job
        {
            __bufTypeHandle = __TypeHandle.__BufferData_RW_BufferTypeHandle
        };
        
        if(!__query_1641826531_0.IsEmptyIgnoreFilter)
        {
            this.CheckedStateRef.CompleteDependency();
            var __jobPtr = global::Unity.Entities.Internal.InternalCompilerInterface.AddressOf(ref __job);
            EntitiesForEachDynamicBuffer_7418F297_LambdaJob_0_Job.RunWithoutJobSystem(ref __query_1641826531_0, __jobPtr);
        }
    }
    
    TypeHandle __TypeHandle;
    global::Unity.Entities.EntityQuery __query_1641826531_0;
    struct TypeHandle
    {
        public Unity.Entities.BufferTypeHandle<global::BufferData> __BufferData_RW_BufferTypeHandle;
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void __AssignHandles(ref global::Unity.Entities.SystemState state)
        {
            __BufferData_RW_BufferTypeHandle = state.GetBufferTypeHandle<global::BufferData>(false);
        }
        
    }
    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    void __AssignQueries(ref global::Unity.Entities.SystemState state)
    {
        var entityQueryBuilder = new global::Unity.Entities.EntityQueryBuilder(global::Unity.Collections.Allocator.Temp);
        __query_1641826531_0 = 
            entityQueryBuilder
                .WithAllRW<global::BufferData>()
                .Build(ref state);
        entityQueryBuilder.Reset();
        entityQueryBuilder.Dispose();
    }
    
    protected override void OnCreateForCompiler()
    {
        base.OnCreateForCompiler();
        __AssignQueries(ref this.CheckedStateRef);
        __TypeHandle.__AssignHandles(ref this.CheckedStateRef);
    }
}
