#pragma warning disable 0219
#line 1 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;
using static Unity.Entities.SystemAPI;
[global::System.Runtime.CompilerServices.CompilerGenerated]
public partial struct RotationSpeedSystemForEachISystem : global::Unity.Entities.ISystemCompilerGenerated
{
    [global::Unity.Entities.DOTSCompilerPatchedMethod("OnUpdate_T0_ref_Unity.Entities.SystemState&")]
    void __OnUpdate_6D4E9467(ref SystemState state)
    {
        #line 16 "/0/Test0.cs"
        Entity entity = default;
        #line 17 "/0/Test0.cs"
        var testAspectRO = global::Unity.Entities.Internal.InternalCompilerInterface.GetAspectAfterCompletingDependency<global::Unity.Entities.Tests.EcsTestAspect.Lookup, global::Unity.Entities.Tests.EcsTestAspect>(ref __TypeHandle.__Unity_Entities_Tests_EcsTestAspect_RW_AspectLookup, ref state, false, entity);
#line hidden
    }

    
    TypeHandle __TypeHandle;
    struct TypeHandle
    {
        public global::Unity.Entities.Tests.EcsTestAspect.Lookup __Unity_Entities_Tests_EcsTestAspect_RW_AspectLookup;
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void __AssignHandles(ref global::Unity.Entities.SystemState state)
        {
            __Unity_Entities_Tests_EcsTestAspect_RW_AspectLookup = new global::Unity.Entities.Tests.EcsTestAspect.Lookup(ref state);
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
