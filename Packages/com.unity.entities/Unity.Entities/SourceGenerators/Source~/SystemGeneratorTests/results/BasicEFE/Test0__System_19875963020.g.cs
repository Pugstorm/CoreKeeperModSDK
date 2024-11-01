#pragma warning disable 0219
#line 1 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;
[global::System.Runtime.CompilerServices.CompilerGenerated]
public partial class BasicEFESystem
{
    [global::Unity.Entities.DOTSCompilerPatchedMethod("OnUpdate_T0")]
    void __OnUpdate_450AADF4()
    {
        #line 15 "/0/Test0.cs"

        BasicEFESystem_6F759C06_LambdaJob_0_Execute();
        #line 22 "/0/Test0.cs"

        BasicEFESystem_6F759C06_LambdaJob_1_Execute();
    }

    #line 21 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
    [global::Unity.Burst.NoAlias]
    [global::Unity.Burst.BurstCompile(
    FloatMode=global::Unity.Burst.FloatMode.Deterministic, FloatPrecision=global::Unity.Burst.FloatPrecision.Low, CompileSynchronously=true)]
    struct BasicEFESystem_6F759C06_LambdaJob_0_Job : global::Unity.Entities.IJobChunk
    {
        public global::Unity.Entities.ComponentTypeHandle<global::TestData> __testDataTypeHandle;
        
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        void OriginalLambdaBody([Unity.Burst.NoAlias] ref global::TestData testData)
        {
#line 17 "/0/Test0.cs"
testData.value++;
            }
        #line 35 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        public void Execute(in global::Unity.Entities.ArchetypeChunk chunk, int batchIndex, bool useEnabledMask, in global::Unity.Burst.Intrinsics.v128 chunkEnabledMask)
        {
            #line 39 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
            var testDataArrayPtr = global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetChunkNativeArrayIntPtr<global::TestData>(chunk, ref __testDataTypeHandle);
            int chunkEntityCount = chunk.Count;
            if (!useEnabledMask)
            {
                for(var entityIndex = 0; entityIndex < chunkEntityCount; ++entityIndex)
                {
                    OriginalLambdaBody(ref global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<global::TestData>(testDataArrayPtr, entityIndex));
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
                            OriginalLambdaBody(ref global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<global::TestData>(testDataArrayPtr, entityIndex));
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
                            OriginalLambdaBody(ref global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<global::TestData>(testDataArrayPtr, entityIndex));
                        }
                        mask64 >>= 1;
                    }
                    mask64 = chunkEnabledMask.ULong1;
                    for (var entityIndex = 64; entityIndex < chunkEntityCount; ++entityIndex)
                    {
                        if ((mask64 & 1) != 0)
                        {
                            OriginalLambdaBody(ref global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<global::TestData>(testDataArrayPtr, entityIndex));
                        }
                        mask64 >>= 1;
                    }
                }
            }
        }
    }
    void BasicEFESystem_6F759C06_LambdaJob_0_Execute()
    {
        __TypeHandle.__TestData_RW_ComponentTypeHandle.Update(ref this.CheckedStateRef);
        var __job = new BasicEFESystem_6F759C06_LambdaJob_0_Job
        {
            __testDataTypeHandle = __TypeHandle.__TestData_RW_ComponentTypeHandle
        };
        
        this.CheckedStateRef.Dependency = global::Unity.Entities.Internal.InternalCompilerInterface.JobChunkInterface.ScheduleParallel(__job, __query_1641826536_0, this.CheckedStateRef.Dependency);
    }
    #line 101 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
    [global::Unity.Burst.NoAlias]
    [global::Unity.Burst.BurstCompile]
    struct BasicEFESystem_6F759C06_LambdaJob_1_Job : global::Unity.Entities.IJobChunk
    {
        public global::Unity.Entities.ComponentTypeHandle<global::TestData> __testDataTypeHandle;
        
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        void OriginalLambdaBody([Unity.Burst.NoAlias] ref global::TestData testData)
        {
#line 24 "/0/Test0.cs"
testData.value++;
            }
        #line 114 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        public void Execute(in global::Unity.Entities.ArchetypeChunk chunk, int batchIndex, bool useEnabledMask, in global::Unity.Burst.Intrinsics.v128 chunkEnabledMask)
        {
            #line 118 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
            var testDataArrayPtr = global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetChunkNativeArrayIntPtr<global::TestData>(chunk, ref __testDataTypeHandle);
            int chunkEntityCount = chunk.Count;
            if (!useEnabledMask)
            {
                for(var entityIndex = 0; entityIndex < chunkEntityCount; ++entityIndex)
                {
                    OriginalLambdaBody(ref global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<global::TestData>(testDataArrayPtr, entityIndex));
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
                            OriginalLambdaBody(ref global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<global::TestData>(testDataArrayPtr, entityIndex));
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
                            OriginalLambdaBody(ref global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<global::TestData>(testDataArrayPtr, entityIndex));
                        }
                        mask64 >>= 1;
                    }
                    mask64 = chunkEnabledMask.ULong1;
                    for (var entityIndex = 64; entityIndex < chunkEntityCount; ++entityIndex)
                    {
                        if ((mask64 & 1) != 0)
                        {
                            OriginalLambdaBody(ref global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<global::TestData>(testDataArrayPtr, entityIndex));
                        }
                        mask64 >>= 1;
                    }
                }
            }
        }
    }
    void BasicEFESystem_6F759C06_LambdaJob_1_Execute()
    {
        __TypeHandle.__TestData_RW_ComponentTypeHandle.Update(ref this.CheckedStateRef);
        var __job = new BasicEFESystem_6F759C06_LambdaJob_1_Job
        {
            __testDataTypeHandle = __TypeHandle.__TestData_RW_ComponentTypeHandle
        };
        
        this.CheckedStateRef.Dependency = global::Unity.Entities.Internal.InternalCompilerInterface.JobChunkInterface.ScheduleParallel(__job, __query_1641826536_0, this.CheckedStateRef.Dependency);
    }
    
    TypeHandle __TypeHandle;
    global::Unity.Entities.EntityQuery __query_1641826536_0;
    struct TypeHandle
    {
        public Unity.Entities.ComponentTypeHandle<global::TestData> __TestData_RW_ComponentTypeHandle;
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void __AssignHandles(ref global::Unity.Entities.SystemState state)
        {
            __TestData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<global::TestData>(false);
        }
        
    }
    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    void __AssignQueries(ref global::Unity.Entities.SystemState state)
    {
        var entityQueryBuilder = new global::Unity.Entities.EntityQueryBuilder(global::Unity.Collections.Allocator.Temp);
        __query_1641826536_0 = 
            entityQueryBuilder
                .WithAllRW<global::TestData>()
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
