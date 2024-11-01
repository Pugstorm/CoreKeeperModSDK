#pragma warning disable 0219
#line 1 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;
[global::System.Runtime.CompilerServices.CompilerGenerated]
public unsafe partial struct PartialMethodSystem : global::Unity.Entities.ISystemCompilerGenerated
{
    [global::Unity.Entities.DOTSCompilerPatchedMethod("ToggleEnabled_T0_Unity.Entities.Entity_ref_Unity.Entities.SystemState&")]

    void __ToggleEnabled_5DBCC748(Entity entity, ref SystemState state)
    {
        #line 15 "/0/Test0.cs"
global::Unity.Entities.Internal.InternalCompilerInterface.SetComponentEnabledAfterCompletingDependency<global::Unity.Entities.Tests.EcsTestDataEnableable>(ref __TypeHandle.__Unity_Entities_Tests_EcsTestDataEnableable_RW_ComponentLookup, ref state, entity, !global::Unity.Entities.Internal.InternalCompilerInterface.IsComponentEnabledAfterCompletingDependency<global::Unity.Entities.Tests.EcsTestDataEnableable>(ref __TypeHandle.__Unity_Entities_Tests_EcsTestDataEnableable_RO_ComponentLookup, ref state, entity));
    }

    
    TypeHandle __TypeHandle;
    struct TypeHandle
    {
        public Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestDataEnableable> __Unity_Entities_Tests_EcsTestDataEnableable_RW_ComponentLookup;
        [global::Unity.Collections.ReadOnly] public Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestDataEnableable> __Unity_Entities_Tests_EcsTestDataEnableable_RO_ComponentLookup;
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void __AssignHandles(ref global::Unity.Entities.SystemState state)
        {
            __Unity_Entities_Tests_EcsTestDataEnableable_RW_ComponentLookup = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestDataEnableable>(false);
            __Unity_Entities_Tests_EcsTestDataEnableable_RO_ComponentLookup = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestDataEnableable>(true);
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
