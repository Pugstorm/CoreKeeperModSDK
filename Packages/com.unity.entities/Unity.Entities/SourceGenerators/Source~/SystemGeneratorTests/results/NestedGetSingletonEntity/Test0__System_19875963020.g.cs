#pragma warning disable 0219
#line 1 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;
[global::System.Runtime.CompilerServices.CompilerGenerated]
public unsafe partial struct NestedGetSingletonEntity : global::Unity.Entities.ISystemCompilerGenerated
{
    [global::Unity.Entities.DOTSCompilerPatchedMethod("OnUpdate_T0_ref_Unity.Entities.SystemState&")]
    void __OnUpdate_6D4E9467(ref SystemState state)
    {
        #line 10 "/0/Test0.cs"
        var entityQuery = new EntityQuery();
        #line 11 "/0/Test0.cs"
        var foo = global::Unity.Entities.Internal.InternalCompilerInterface.GetComponentAfterCompletingDependency<global::Unity.Entities.Tests.EcsTestData>(ref __TypeHandle.__Unity_Entities_Tests_EcsTestData_RO_ComponentLookup, ref state, entityQuery.GetSingletonEntity());
#line hidden
    }

    
    TypeHandle __TypeHandle;
    struct TypeHandle
    {
        [global::Unity.Collections.ReadOnly] public global::Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestData> __Unity_Entities_Tests_EcsTestData_RO_ComponentLookup;
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void __AssignHandles(ref global::Unity.Entities.SystemState state)
        {
            __Unity_Entities_Tests_EcsTestData_RO_ComponentLookup = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestData>(true);
        }
        
    }
    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    void __AssignQueries(ref global::Unity.Entities.SystemState state)
    {
        var entityQueryBuilder = new global::Unity.Entities.EntityQueryBuilder(global::Unity.Collections.Allocator.Temp);
        entityQueryBuilder.Dispose();
    }
    
    public void OnCreateForCompiler(ref SystemState state)
    {
        __AssignQueries(ref state);
        __TypeHandle.__AssignHandles(ref state);
    }
}
