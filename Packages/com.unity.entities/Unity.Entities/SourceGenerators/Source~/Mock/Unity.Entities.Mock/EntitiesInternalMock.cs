using System.Runtime.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Unity.Entities.Internal;

public static partial class InternalCompilerInterface
{
    public interface IAspectLookup<T> where T : IAspect
    {
        public void Update(ref SystemState state);
        public T this[Entity entity] { get; }
    }

    public static EntityStorageInfoLookup GetEntityStorageInfoLookup(
        ref EntityStorageInfoLookup entityStorageInfoLookup, ref SystemState state)
        => default;

    public static bool DoesEntityExist(ref EntityStorageInfoLookup entityStorageInfoLookup, ref SystemState state,
        Entity entity)
        => default;

    public static EntityTypeHandle GetEntityTypeHandle(ref EntityTypeHandle entityTypeHandle, ref SystemState state)
        => default;

    public static ComponentLookup<T> GetComponentLookup<T>(ref ComponentLookup<T> componentLookup,
        ref SystemState state) where T : unmanaged, IComponentData
        => default;

    public static BufferLookup<T> GetBufferLookup<T>(ref BufferLookup<T> bufferLookup, ref SystemState state)
        where T : unmanaged, IBufferElementData
        => default;

    public static RefRO<T> GetComponentROAfterCompletingDependency<T>(ref ComponentLookup<T> componentLookup, ref SystemState state,
        Entity entity) where T : unmanaged, IComponentData
        => default;

    public static RefRW<T> GetComponentRWAfterCompletingDependency<T>(ref ComponentLookup<T> componentLookup, ref SystemState state,
        Entity entity) where T : unmanaged, IComponentData
        => default;

    public static T GetComponentAfterCompletingDependency<T>(ref ComponentLookup<T> componentLookup, ref SystemState state, Entity entity)
        where T : unmanaged, IComponentData
        => default;

    public static RefRW<T> GetComponentRWAfterCompletingDependency<T>(ref ComponentLookup<T> componentLookup, ref SystemState state,
        SystemHandle systemHandle) where T : unmanaged, IComponentData
        => default;

    public static T GetComponentAfterCompletingDependency<T>(ref ComponentLookup<T> componentLookup, ref SystemState state,
        SystemHandle systemHandle) where T : unmanaged, IComponentData
        => default;
    public static void SetComponentAfterCompletingDependency<T>(ref ComponentLookup<T> componentLookup, ref SystemState state, T component,
        Entity entity) where T : unmanaged, IComponentData
    {
    }

    public static void SetComponentAfterCompletingDependency<T>(ref ComponentLookup<T> componentLookup, ref SystemState state, T component,
        SystemHandle systemHandle) where T : unmanaged, IComponentData
    {
    }

    public static bool HasComponentAfterCompletingDependency<T>(ref ComponentLookup<T> componentLookup, ref SystemState state, Entity entity)
        where T : unmanaged, IComponentData
        => default;

    public static bool HasComponentAfterCompletingDependency<T>(ref ComponentLookup<T> componentLookup, ref SystemState state,
        SystemHandle systemHandle) where T : unmanaged, IComponentData
        => default;

    public static bool IsComponentEnabledAfterCompletingDependency<T>(ref ComponentLookup<T> componentLookup, ref SystemState state,
        Entity entity) where T : unmanaged, IComponentData
        => default;

    public static bool IsComponentEnabledAfterCompletingDependency<T>(ref ComponentLookup<T> componentLookup, ref SystemState state,
        SystemHandle systemHandle) where T : unmanaged, IComponentData
        => default;

    public static void SetComponentEnabledAfterCompletingDependency<T>(ref ComponentLookup<T> componentLookup, ref SystemState state,
        Entity entity, bool value) where T : unmanaged, IComponentData
    {
    }

    public static void SetComponentEnabledAfterCompletingDependency<T>(ref ComponentLookup<T> componentLookup, ref SystemState state,
        SystemHandle systemHandle, bool value) where T : unmanaged, IComponentData
    {
    }

    public static DynamicBuffer<T> GetBufferAfterCompletingDependency<T>(ref BufferLookup<T> bufferLookup, ref SystemState state,
        Entity entity) where T : unmanaged, IBufferElementData
        => default;

    public static bool HasBufferAfterCompletingDependency<T>(ref BufferLookup<T> bufferLookup, ref SystemState state, Entity entity)
        where T : unmanaged, IBufferElementData
        => default;

    public static bool IsBufferEnabledAfterCompletingDependency<T>(ref BufferLookup<T> bufferLookup, ref SystemState state, Entity entity)
        where T : unmanaged, IBufferElementData
        => default;

    public static void SetBufferEnabledAfterCompletingDependency<T>(ref BufferLookup<T> bufferLookup, ref SystemState state, bool value,
        Entity entity) where T : unmanaged, IBufferElementData
    {
    }

    public static T GetAspectAfterCompletingDependency<TLookup, T>(ref TLookup aspectLookup, ref SystemState state, bool isAspectReadOnly,
        Entity entity)
        where TLookup : struct, IAspectLookup<T>
        where T : struct, IAspect, IAspectCreate<T>
        => default;

    public static ComponentTypeHandle<T> GetComponentTypeHandle<T>(ref ComponentTypeHandle<T> componentTypeHandle,
        ref SystemState state) where T : unmanaged, IComponentData
        => default;

    public static BufferTypeHandle<T> GetBufferTypeHandle<T>(ref BufferTypeHandle<T> bufferTypeHandle,
        ref SystemState state) where T : unmanaged, IBufferElementData
        => default;

    public static SharedComponentTypeHandle<T> GetSharedComponentTypeHandle<T>(
        ref SharedComponentTypeHandle<T> sharedComponentTypeHandle, ref SystemState state)
        where T : struct, ISharedComponentData
        => default;

    public static void MergeWith(ref UnsafeList<ComponentType> componentTypes, ref UnsafeList<ComponentType> addThese)
    {
    }

    public static class EntityQueryInterface
    {
        public static bool HasComponentsRequiredForExecuteMethodToRun(
            ref EntityQuery userDefinedQuery,
            ref Span<ComponentType> componentsUsedInExecuteMethod)
            => true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe RefRW<T> GetRefRW<T>(IntPtr ptr, int index) where T : unmanaged, IComponentData => default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe RefRO<T> GetRefRO<T>(IntPtr ptr, int index) where T : unmanaged, IComponentData => default;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe IntPtr UnsafeGetChunkNativeArrayReadOnlyIntPtrWithoutChecks<T>(in ArchetypeChunk chunk, ref ComponentTypeHandle<T> typeHandle) where T : unmanaged, IComponentData => default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe IntPtr UnsafeGetChunkNativeArrayIntPtrWithoutChecks<T>(in ArchetypeChunk chunk, ref ComponentTypeHandle<T> typeHandle) where T : unmanaged, IComponentData => default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UncheckedRefRO<T> UnsafeGetUncheckedRefRO<T>(IntPtr ptr, int index) where T : unmanaged, IComponentData => default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UncheckedRefRW<T> UnsafeGetUncheckedRefRW<T>(IntPtr ptr, int index) where T : unmanaged, IComponentData => default;

    public readonly struct UncheckedRefRO<T> : IComponentData where T : unmanaged, IComponentData
    {
        public T ValueRO => default;
    }

    public readonly struct UncheckedRefRW<T> where T : unmanaged, IComponentData
    {
        public T ValueRW => default;
        public T ValueRO => default;
    }

    public static JobRunWithoutJobSystemDelegate BurstCompile(JobRunWithoutJobSystemDelegate d) => default;
    public static JobChunkRunWithoutJobSystemDelegate BurstCompile(JobChunkRunWithoutJobSystemDelegate d) => default;
    public static JobChunkRunWithoutJobSystemDelegateLimitEntities BurstCompile(JobChunkRunWithoutJobSystemDelegateLimitEntities d) => default;
    public delegate void JobChunkRunWithoutJobSystemDelegate(ref EntityQuery query, IntPtr jobPtr);
    public delegate void JobRunWithoutJobSystemDelegate(IntPtr jobPtr);
    public delegate void JobChunkRunWithoutJobSystemDelegateLimitEntities(ref EntityQuery query, IntPtr limitToEntityArrayPtr, int limitToEntityArrayLength, IntPtr jobPtr);
    public static unsafe ref T UnsafeAsRef<T>(IntPtr value) where T : struct => throw new Exception();
    public static unsafe IntPtr AddressOf<T>(ref T value) where T : struct => throw new Exception();
    public static unsafe T GetComponentData<T>(EntityManager manager, Entity entity, int typeIndex, out T originalComponent) where T : struct, IComponentData => throw new Exception();
    public static unsafe void WriteComponentData<T>(EntityManager manager, Entity entity, int typeIndex, ref T lambdaComponent, ref T originalComponent) where T : struct, IComponentData {}
    public static unsafe IntPtr UnsafeGetChunkNativeArrayReadOnlyIntPtr<T>(ArchetypeChunk chunk, ref ComponentTypeHandle<T> typeHandle) where T : struct, IComponentData => default;
    public static unsafe IntPtr UnsafeGetChunkNativeArrayIntPtr<T>(ArchetypeChunk chunk, ref ComponentTypeHandle<T> typeHandle) where T : struct, IComponentData => default;
    public static unsafe IntPtr UnsafeGetChunkEntityArrayIntPtr(ArchetypeChunk chunk, EntityTypeHandle typeHandle) => default;
    public static unsafe IntPtr UnsafeGetEntityArrayIntPtr(NativeArray<Entity> array) => default;
    public static unsafe T UnsafeGetCopyOfNativeArrayPtrElement<T>(IntPtr nativeArrayPtr, int elementIndex) where T : unmanaged => *((T*) nativeArrayPtr + elementIndex);
    public static unsafe ref T UnsafeGetRefToNativeArrayPtrElement<T>(IntPtr nativeArrayPtr, int elementIndex) where T : unmanaged => throw new Exception();
    public static unsafe IntPtr UnsafeGetSystemStateIntPtr(ref SystemState state) => default;
    public static unsafe ref SystemState UnsafeGetSystemStateRef(IntPtr statePtr) => ref *(SystemState*) statePtr;
    public static bool UnsafeTryGetNextEnabledBitRange(v128 mask, int firstIndexToCheck, out int nextRangeBegin,
        out int nextRangeEnd)
    {
        nextRangeBegin = default;
        nextRangeEnd = default;
        return default;
    }

    public static class JobChunkInterface
    {
        public static void RunWithoutJobsInternal<T>(ref T jobData, ref EntityQuery query) where T : struct, IJobChunk {}
        public static void RunWithoutJobs<T>(ref T jobData, EntityQuery query) where T : struct, IJobChunk {}
        public static void RunByRefWithoutJobs<T>(ref T jobData, EntityQuery query) where T : struct, IJobChunk {}
        public static JobHandle Schedule<T>(T jobData, EntityQuery query, JobHandle dependsOn) where T : struct, IJobChunk => default;
        public static JobHandle ScheduleParallel<T>(T jobData, EntityQuery query, JobHandle dependsOn) where T : struct, IJobChunk => default;
        public static JobHandle ScheduleParallel<T>(T jobData, EntityQuery query, JobHandle dependsOn, NativeArray<int> chunkBaseEntityIndices) where T : struct, IJobChunk => default;
        public static JobHandle Schedule<T>(T jobData, EntityQuery query, NativeArray<Entity> limitToEntityArray, JobHandle dependsOn) where T : struct, IJobChunk => default;
#pragma warning disable 618
        public static JobHandle ScheduleParallel<T>(T jobData, EntityQuery query, ScheduleGranularity scheduleGranularity, NativeArray<Entity> limitToEntityArray, JobHandle dependsOn) where T : struct, IJobChunk => default;
#pragma warning restore 618
        public static void RunWithoutJobs<T>(ref T jobData, EntityQuery query, NativeArray<Entity> limitToEntityArray) where T : struct, IJobChunk {}
        public static void RunByRefWithoutJobs<T>(ref T jobData, EntityQuery query, NativeArray<Entity> limitToEntityArray) where T : struct, IJobChunk {}
        public static unsafe void RunWithoutJobsInternal<T>(ref T jobData, ref EntityQuery query, IntPtr limitToEntityArrayPtr, int limitToEntityArrayLength) where T : struct, IJobChunk {}
        public static unsafe void RunWithoutJobsInternal<T>(ref T jobData, ref EntityQuery query, Entity* limitToEntityArray, int limitToEntityArrayLength) where T : struct, IJobChunk {}
    }

    public static void UnsafeRunJobChunk<T>(ref T jobData, EntityQuery query,
        JobChunkRunWithoutJobSystemDelegate functionPointer) where T : struct, IJobChunk {}

    public static void CombineComponentType(ref UnsafeList<ComponentType> all, ComponentType readOnly) {}
}

public unsafe struct InternalEntityQueryEnumerator : IDisposable
{
    // hot state
    public int         IndexInChunk;
    public int         EntityCount;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public InternalEntityQueryEnumerator(EntityQuery query)
    {
        EntityCount = 1;
        IndexInChunk = 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Dispose() {}

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNextHotLoop() => false;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool MoveNextColdLoop(out ArchetypeChunk chunk) => true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNextEntityRange(out bool movedToNewChunk, out ArchetypeChunk chunk, out int entityStartIndex, out int entityEndIndex)
    {
        movedToNewChunk = false;
        chunk = default;
        entityStartIndex = -1;
        entityEndIndex = -1;
        return false;
    }
}
