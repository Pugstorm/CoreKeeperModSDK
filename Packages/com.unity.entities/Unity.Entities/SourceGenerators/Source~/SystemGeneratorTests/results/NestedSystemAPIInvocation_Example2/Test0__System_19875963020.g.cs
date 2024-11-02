#pragma warning disable 0219
#line 1 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;
[global::System.Runtime.CompilerServices.CompilerGenerated]
public unsafe partial struct NestedSystemAPIInvocation_Example2 : global::Unity.Entities.ISystemCompilerGenerated
{
    [global::Unity.Entities.DOTSCompilerPatchedMethod("OnUpdate_T0_ref_Unity.Entities.SystemState&")]
    void __OnUpdate_6D4E9467(ref SystemState state)
    {
        #line 10 "/0/Test0.cs"
        var foo = state.EntityManager.GetComponentObject<EcsTestManagedComponent>(__query_1641826531_0.GetSingletonEntity());
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
