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
        #line 21 "/0/Test0.cs"
        foreach (var (rotationSpeed, entity) in IFE_1641826537_0.Query(__query_1641826537_0, __TypeHandle.__IFE_1641826537_0_TypeHandle, ref state))
        #line 22 "/0/Test0.cs"
        {
            #line 23 "/0/Test0.cs"
            var rotation = global::Unity.Entities.Internal.InternalCompilerInterface.GetComponentAfterCompletingDependency<global::Unity.Entities.Tests.EcsTestData>(ref __TypeHandle.__Unity_Entities_Tests_EcsTestData_RO_ComponentLookup, ref state, entity);
#line hidden
        }
    }

    #line 24 "Temp/GeneratedCode/TestProject/Test0__System_19875963020.g.cs"
    readonly struct IFE_1641826537_0
    {
        public struct ResolvedChunk
        {
            public global::System.IntPtr item1_IntPtr;
            public global::System.IntPtr Entity_IntPtr;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public Unity.Entities.QueryEnumerableWithEntity<Unity.Entities.Internal.InternalCompilerInterface.UncheckedRefRO<global::RotationSpeed>> Get(int index) => new Unity.Entities.QueryEnumerableWithEntity<Unity.Entities.Internal.InternalCompilerInterface.UncheckedRefRO<global::RotationSpeed>>(Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetUncheckedRefRO<global::RotationSpeed>(item1_IntPtr, index),Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetCopyOfNativeArrayPtrElement<Unity.Entities.Entity>(Entity_IntPtr, index));
        }
        public struct TypeHandle
        {
            [Unity.Collections.ReadOnly] Unity.Entities.ComponentTypeHandle<global::RotationSpeed> item1_ComponentTypeHandle_RO;
            Unity.Entities.EntityTypeHandle Entity_TypeHandle;
            public TypeHandle(ref global::Unity.Entities.SystemState systemState)
            {
                item1_ComponentTypeHandle_RO = systemState.GetComponentTypeHandle<global::RotationSpeed>(isReadOnly: true);
                Entity_TypeHandle = systemState.GetEntityTypeHandle();
            }
            public void Update(ref global::Unity.Entities.SystemState systemState)
            {
                item1_ComponentTypeHandle_RO.Update(ref systemState);
                Entity_TypeHandle.Update(ref systemState);
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public ResolvedChunk Resolve(global::Unity.Entities.ArchetypeChunk archetypeChunk)
            {
                var resolvedChunk = new ResolvedChunk();
                resolvedChunk.item1_IntPtr = Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetChunkNativeArrayReadOnlyIntPtrWithoutChecks<global::RotationSpeed>(archetypeChunk, ref item1_ComponentTypeHandle_RO);
                resolvedChunk.Entity_IntPtr = Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetChunkEntityArrayIntPtr(archetypeChunk, Entity_TypeHandle);
                return resolvedChunk;
            }
        }
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static Enumerator Query(global::Unity.Entities.EntityQuery entityQuery, TypeHandle typeHandle, ref Unity.Entities.SystemState state) => new Enumerator(entityQuery, typeHandle, ref state);
        public struct Enumerator : global::System.Collections.Generic.IEnumerator<Unity.Entities.QueryEnumerableWithEntity<Unity.Entities.Internal.InternalCompilerInterface.UncheckedRefRO<global::RotationSpeed>>>
        {
            global::Unity.Entities.Internal.InternalEntityQueryEnumerator _entityQueryEnumerator;
            TypeHandle _typeHandle;
            ResolvedChunk _resolvedChunk;
            
            int _currentEntityIndex;
            int _endEntityIndex;
            
            public Enumerator(global::Unity.Entities.EntityQuery entityQuery, TypeHandle typeHandle, ref Unity.Entities.SystemState state)
            {
                if (!entityQuery.IsEmptyIgnoreFilter)
                {
                    IFE_1641826537_0.CompleteDependencies(ref state);
                    typeHandle.Update(ref state);
                    
                }
                
                _entityQueryEnumerator = new global::Unity.Entities.Internal.InternalEntityQueryEnumerator(entityQuery);
                
                _currentEntityIndex = -1;
                _endEntityIndex = -1;
                
                _typeHandle = typeHandle;
                _resolvedChunk = default;
            }
            
            public void Dispose() => _entityQueryEnumerator.Dispose();
            
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                _currentEntityIndex++;
                
                if (_currentEntityIndex >= _endEntityIndex)
                {
                    if (_entityQueryEnumerator.MoveNextEntityRange(out bool movedToNewChunk, out global::Unity.Entities.ArchetypeChunk chunk, out int entityStartIndex, out int entityEndIndex))
                    {
                        if (movedToNewChunk)
                        {
                            _resolvedChunk = _typeHandle.Resolve(chunk);
                        }
                        
                        _currentEntityIndex = entityStartIndex;
                        _endEntityIndex = entityEndIndex;
                        return true;
                    }
                    return false;
                }
                return true;
            }
            
            public Unity.Entities.QueryEnumerableWithEntity<Unity.Entities.Internal.InternalCompilerInterface.UncheckedRefRO<global::RotationSpeed>> Current => _resolvedChunk.Get(_currentEntityIndex);
            
            public Enumerator GetEnumerator() => this;
            public void Reset() => throw new global::System.NotImplementedException();
            object global::System.Collections.IEnumerator.Current => throw new global::System.NotImplementedException();
        }
        public static void CompleteDependencies(ref SystemState state)
        {
            state.EntityManager.CompleteDependencyBeforeRO<global::RotationSpeed>();
        }
    }
    
    TypeHandle __TypeHandle;
    global::Unity.Entities.EntityQuery __query_1641826537_0;
    struct TypeHandle
    {
        public IFE_1641826537_0.TypeHandle __IFE_1641826537_0_TypeHandle;
        [global::Unity.Collections.ReadOnly] public global::Unity.Entities.ComponentLookup<global::Unity.Entities.Tests.EcsTestData> __Unity_Entities_Tests_EcsTestData_RO_ComponentLookup;
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void __AssignHandles(ref global::Unity.Entities.SystemState state)
        {
            __IFE_1641826537_0_TypeHandle = new IFE_1641826537_0.TypeHandle(ref state);
            __Unity_Entities_Tests_EcsTestData_RO_ComponentLookup = state.GetComponentLookup<global::Unity.Entities.Tests.EcsTestData>(true);
        }
        
    }
    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    void __AssignQueries(ref global::Unity.Entities.SystemState state)
    {
        var entityQueryBuilder = new global::Unity.Entities.EntityQueryBuilder(global::Unity.Collections.Allocator.Temp);
        __query_1641826537_0 = 
            entityQueryBuilder
                .WithAll<global::RotationSpeed>()
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
