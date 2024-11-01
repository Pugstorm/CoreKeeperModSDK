#pragma warning disable 0219
#line 1 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;
[global::System.Runtime.CompilerServices.CompilerGenerated]
public unsafe partial struct PartialMethodSystem : global::Unity.Entities.ISystemCompilerGenerated
{
    [global::Unity.Entities.DOTSCompilerPatchedMethod("CustomOnUpdate_T0_ref_Unity.Entities.SystemState&")]
    void __CustomOnUpdate_5D2F01C4(ref SystemState state)
    {
        #line 10 "/0/Test0.cs"
        var tickSingleton2 = __query_1641826531_0.GetSingleton<EcsTestData>();
#line hidden
    }

    
    TypeHandle __TypeHandle;
    global::Unity.Entities.EntityQuery __query_1641826531_0;
    struct TypeHandle
    {
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void __AssignHandles(ref global::Unity.Entities.SystemState state)
        {
        }
        
    }
    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    void __AssignQueries(ref global::Unity.Entities.SystemState state)
    {
        var entityQueryBuilder = new global::Unity.Entities.EntityQueryBuilder(global::Unity.Collections.Allocator.Temp);
        __query_1641826531_0 = 
            entityQueryBuilder
                .WithAll<global::Unity.Entities.Tests.EcsTestData>()
                .WithOptions(global::Unity.Entities.EntityQueryOptions.IncludeSystems)
                .Build(ref state);
        entityQueryBuilder.Reset();
        entityQueryBuilder.Dispose();
    }
    
    public void OnCreateForCompiler(ref SystemState state)
    {
        __AssignQueries(ref state);
        __TypeHandle.__AssignHandles(ref state);
    }
}
