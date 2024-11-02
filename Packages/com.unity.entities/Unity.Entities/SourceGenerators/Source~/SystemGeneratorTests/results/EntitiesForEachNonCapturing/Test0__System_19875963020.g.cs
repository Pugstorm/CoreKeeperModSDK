#pragma warning disable 0219
#line 1 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
[global::System.Runtime.CompilerServices.CompilerGenerated]
partial class EntitiesForEachNonCapturing
{
    [global::Unity.Entities.DOTSCompilerPatchedMethod("OnUpdate_T0")]
    void __OnUpdate_450AADF4()
    {
        #line 14 "/0/Test0.cs"

        EntitiesForEachNonCapturing_4E2AFFBE_LambdaJob_0_Execute();
    }

    #line 18 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
    [global::Unity.Burst.NoAlias]
    [global::Unity.Burst.BurstCompile]
    struct EntitiesForEachNonCapturing_4E2AFFBE_LambdaJob_0_Job : global::Unity.Entities.IJobChunk
    {
        public global::Unity.Entities.ComponentTypeHandle<global::Translation> __translationTypeHandle;
        
        
        
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        void OriginalLambdaBody([Unity.Burst.NoAlias] ref global::Translation translation, [Unity.Burst.NoAlias] ref global::TagComponent1 tag1, [Unity.Burst.NoAlias] in global::TagComponent2 tag2)
        { 
#line 14 "/0/Test0.cs"
translation.Value += 5; }
        #line 31 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        public void Execute(in global::Unity.Entities.ArchetypeChunk chunk, int batchIndex, bool useEnabledMask, in global::Unity.Burst.Intrinsics.v128 chunkEnabledMask)
        {
            #line 35 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
            var translationArrayPtr = global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetChunkNativeArrayIntPtr<global::Translation>(chunk, ref __translationTypeHandle);
            int chunkEntityCount = chunk.Count;
            if (!useEnabledMask)
            {
                for(var entityIndex = 0; entityIndex < chunkEntityCount; ++entityIndex)
                {
                    global::TagComponent1 tag1 = default;
                    global::TagComponent2 tag2 = default;
                    OriginalLambdaBody(ref global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<global::Translation>(translationArrayPtr, entityIndex), ref tag1, in tag2);
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
                            global::TagComponent1 tag1 = default;
                            global::TagComponent2 tag2 = default;
                            OriginalLambdaBody(ref global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<global::Translation>(translationArrayPtr, entityIndex), ref tag1, in tag2);
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
                            global::TagComponent1 tag1 = default;
                            global::TagComponent2 tag2 = default;
                            OriginalLambdaBody(ref global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<global::Translation>(translationArrayPtr, entityIndex), ref tag1, in tag2);
                        }
                        mask64 >>= 1;
                    }
                    mask64 = chunkEnabledMask.ULong1;
                    for (var entityIndex = 64; entityIndex < chunkEntityCount; ++entityIndex)
                    {
                        if ((mask64 & 1) != 0)
                        {
                            global::TagComponent1 tag1 = default;
                            global::TagComponent2 tag2 = default;
                            OriginalLambdaBody(ref global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<global::Translation>(translationArrayPtr, entityIndex), ref tag1, in tag2);
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
                ref var jobData = ref global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeAsRef<EntitiesForEachNonCapturing_4E2AFFBE_LambdaJob_0_Job>(jobPtr);
                global::Unity.Entities.Internal.InternalCompilerInterface.JobChunkInterface.RunWithoutJobsInternal(ref jobData, ref query);
            }
            finally
            {
            }
        }
    }
    void EntitiesForEachNonCapturing_4E2AFFBE_LambdaJob_0_Execute()
    {
        __TypeHandle.__Translation_RW_ComponentTypeHandle.Update(ref this.CheckedStateRef);
        var __job = new EntitiesForEachNonCapturing_4E2AFFBE_LambdaJob_0_Job
        {
            __translationTypeHandle = __TypeHandle.__Translation_RW_ComponentTypeHandle
        };
        
        if(!__query_1641826535_0.IsEmptyIgnoreFilter)
        {
            this.CheckedStateRef.CompleteDependency();
            var __jobPtr = global::Unity.Entities.Internal.InternalCompilerInterface.AddressOf(ref __job);
            EntitiesForEachNonCapturing_4E2AFFBE_LambdaJob_0_Job.RunWithoutJobSystem(ref __query_1641826535_0, __jobPtr);
        }
    }
    
    TypeHandle __TypeHandle;
    global::Unity.Entities.EntityQuery __query_1641826535_0;
    struct TypeHandle
    {
        public Unity.Entities.ComponentTypeHandle<global::Translation> __Translation_RW_ComponentTypeHandle;
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void __AssignHandles(ref global::Unity.Entities.SystemState state)
        {
            __Translation_RW_ComponentTypeHandle = state.GetComponentTypeHandle<global::Translation>(false);
        }
        
    }
    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    void __AssignQueries(ref global::Unity.Entities.SystemState state)
    {
        var entityQueryBuilder = new global::Unity.Entities.EntityQueryBuilder(global::Unity.Collections.Allocator.Temp);
        __query_1641826535_0 = 
            entityQueryBuilder
                .WithAll<global::TagComponent1>()
                .WithAll<global::TagComponent2>()
                .WithAllRW<global::Translation>()
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
