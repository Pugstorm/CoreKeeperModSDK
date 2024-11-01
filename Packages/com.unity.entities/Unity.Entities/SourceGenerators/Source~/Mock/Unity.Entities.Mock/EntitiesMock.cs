using System.Collections;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Burst.Intrinsics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities.CodeGeneratedJobForEach;
using Unity.Jobs;

namespace Unity.Entities
{
    public struct SystemHandle{}

    public class EntitiesMock { }

    public struct Entity : IQueryTypeParameter { }

    public struct EntityQueryBuilder
    {
        public EntityQueryBuilder(Allocator allocator)
        {
        }

        public void Dispose()
        {
        }

        public void Reset()
        {
        }

        public EntityQueryBuilder WithAll<T>() => this;
        public EntityQueryBuilder WithAllRW<T>() => this;
        public EntityQueryBuilder WithAny<T>() => this;
        public EntityQueryBuilder WithNone<T>() => this;
        public EntityQueryBuilder WithDisabled<T>() => this;
        public EntityQueryBuilder WithDisabledRW<T>() => this;
        public EntityQueryBuilder WithAbsent<T>() => this;
        public EntityQueryBuilder WithAspect<T>() => this;
        public EntityQueryBuilder WithOptions(EntityQueryOptions options) => this;
        public EntityQueryBuilder AddAdditionalQuery() => this;
        public EntityQuery Build(ref SystemState systemState) => default;
    }

    unsafe public struct BlobBuilder : IDisposable
    {
        public BlobBuilder(Allocator allocator, int chunkSize = 65536) {}
        public BlobBuilderArray<T> Construct<T>(ref BlobArray<T> blobArray, params T[] data) where T : struct => default;
        public ref T ConstructRoot<T>() where T : struct => throw new NotImplementedException();
        public ref T SetPointer<T>(ref BlobPtr<T> ptr, ref T obj) where T : struct => throw new NotImplementedException();
        public BlobBuilderArray<T> Allocate<T>(ref BlobArray<T> ptr, int length) where T : struct => default;
        public void Dispose() {}
        public BlobAssetReference<T> CreateBlobAssetReference<T>(Allocator allocator) where T : unmanaged => default;
    }

    public unsafe struct BlobAssetReference<T> where T : unmanaged
    {
        public ref T Value => throw new NotImplementedException();
    }

    public class MayOnlyLiveInBlobStorageAttribute : Attribute {}

    public unsafe ref struct BlobBuilderArray<T> where T : struct
    {
        public ref T this[int index] => throw new NotImplementedException();
    }
    [MayOnlyLiveInBlobStorage] public unsafe struct BlobArray<T> where T : struct {}
    [MayOnlyLiveInBlobStorage] public unsafe struct BlobPtr<T> where T : struct {}

    public interface IJobChunk
    {
        void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask);
    }

    public interface IJobEntity{}

    public sealed class WithAllAttribute : Attribute
    {
        public WithAllAttribute(params Type[] types){}
    }
    public sealed class WithDisabledAttribute : Attribute
    {
        public WithDisabledAttribute(params Type[] types){}
    }
    public sealed class WithChangeFilterAttribute : Attribute
    {
        public WithChangeFilterAttribute(params Type[] types){}
    }

    public static class IJobEntityExtensions
    {
        public static JobHandle Schedule<T>(this T jobData, JobHandle dependsOn)
            where T : unmanaged, IJobEntity  => default;
        public static JobHandle ScheduleByRef<T>(this ref T jobData, JobHandle dependsOn)
            where T : unmanaged, IJobEntity  => default;
        public static JobHandle Schedule<T>(this T jobData, EntityQuery query, JobHandle dependsOn)
            where T : unmanaged, IJobEntity  => default;
        public static JobHandle ScheduleByRef<T>(this ref T jobData, EntityQuery query, JobHandle dependsOn)
            where T : unmanaged, IJobEntity  => default;
        public static void Schedule<T>(this T jobData)
            where T : unmanaged, IJobEntity  {}
        public static void ScheduleByRef<T>(this ref T jobData)
            where T : unmanaged, IJobEntity  {}
        public static void Schedule<T>(this T jobData, EntityQuery query)
            where T : unmanaged, IJobEntity  {}
        public static void ScheduleByRef<T>(this ref T jobData, EntityQuery query)
            where T : unmanaged, IJobEntity  {}
        public static JobHandle ScheduleParallel<T>(this T jobData, JobHandle dependsOn)
            where T : unmanaged, IJobEntity  => default;
        public static JobHandle ScheduleParallelByRef<T>(this ref T jobData, JobHandle dependsOn)
            where T : unmanaged, IJobEntity  => default;
        public static JobHandle ScheduleParallel<T>(this T jobData, EntityQuery query, JobHandle dependsOn)
            where T : unmanaged, IJobEntity  => default;
        public static JobHandle ScheduleParallelByRef<T>(this ref T jobData, EntityQuery query, JobHandle dependsOn)
            where T : unmanaged, IJobEntity  => default;
        public static void ScheduleParallel<T>(this T jobData)
            where T : unmanaged, IJobEntity  {}
        public static void ScheduleParallelByRef<T>(this ref T jobData)
            where T : unmanaged, IJobEntity  {}
        public static void ScheduleParallel<T>(this T jobData, EntityQuery query)
            where T : unmanaged, IJobEntity  {}
        public static void ScheduleParallelByRef<T>(this ref T jobData, EntityQuery query)
            where T : unmanaged, IJobEntity {}
        public static void Run<T>(this T jobData)
            where T : struct, IJobEntity  {}
        public static void RunByRef<T>(this ref T jobData)
            where T : struct, IJobEntity  {}
        public static void Run<T>(this T jobData, EntityQuery query)
            where T : struct, IJobEntity  {}
        public static void RunByRef<T>(this ref T jobData, EntityQuery query)
            where T : struct, IJobEntity  {}
    }

    public readonly struct RefRW<T> : IQueryTypeParameter where T : struct, IComponentData
    {
        public unsafe RefRW(NativeArray<T> componentDatas, int index){}
        public unsafe ref readonly T ValueRO => throw new NotImplementedException();
        public unsafe ref T ValueRW => throw new NotImplementedException();
        public unsafe bool IsValid => default;
        public static RefRW<T> Optional(NativeArray<T> componentDataNativeArray, int index) => default;
    }

    public readonly struct RefRO<T> : IQueryTypeParameter where T : struct, IComponentData
    {
        public unsafe RefRO(NativeArray<T> componentDatas, int index){}
        public unsafe ref readonly T ValueRO => throw new NotImplementedException();
        public unsafe bool IsValid => default;
    }

    public readonly struct SafeBitRef
    {

    }
    public readonly struct EnabledRefRW<T> : IQueryTypeParameter where T : struct, IEnableableComponent
    {
        public unsafe EnabledRefRW(SafeBitRef ptr, int* ptrChunkDisable) { }
    }

    public readonly struct EnabledRefRO<T> : IQueryTypeParameter where T : struct, IEnableableComponent
    {
        public unsafe EnabledRefRO(SafeBitRef ptr) { }
    }

    public unsafe partial struct EntityCommandBuffer : IDisposable
    {
        public void Dispose() => throw new NotImplementedException();

        unsafe public struct ParallelWriter { }

        public ParallelWriter AsParallelWriter() => default;

        public void Playback(EntityManager mgr) { }
        [SupportedInEntitiesForEach]
        public void AddComponent<T>(Entity e) where T : unmanaged, IComponentData { }
        [SupportedInEntitiesForEach]
        public void RemoveComponentForEntityQuery<T>(EntityQuery entityQuery) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal class SupportedInEntitiesForEach : Attribute
    {
    }

    public static unsafe class EntityCommandBufferManagedComponentExtensions
    {
        [SupportedInEntitiesForEach]
        public static void AddComponent<T>(this EntityCommandBuffer ecb, Entity e, T component) where T : class { }
        [SupportedInEntitiesForEach]
        public static void AddComponent<T>(this EntityCommandBuffer ecb, Entity e) where T : class { }
    }

    public struct ComponentTypeHandle<T>
    {
        public unsafe void Update(ref SystemState state) {}
        public unsafe void Update(SystemBase system) {}
    }

    public struct SharedComponentTypeHandle<T>
    {
        public unsafe void Update(ref SystemState state) {}
        public unsafe void Update(SystemBase system) {}
    }

    public struct BufferTypeHandle<T>
    {
        public unsafe void Update(ref SystemState state) {}
        public unsafe void Update(SystemBase system) {}
    }

    public struct EnabledMask
    {
        public unsafe EnabledRefRW<T> GetEnabledRefRW<T>(int index)
            where T : unmanaged, IComponentData, IEnableableComponent => default;
        public EnabledRefRO<T> GetEnabledRefRO<T>(int index)
            where T : unmanaged, IComponentData, IEnableableComponent => default;

        public unsafe EnabledRefRW<T> GetOptionalEnabledRefRW<T>(int index)
            where T : unmanaged, IComponentData, IEnableableComponent => default;

        public EnabledRefRO<T> GetOptionalEnabledRefRO<T>(int index)
            where T : unmanaged, IComponentData, IEnableableComponent => default;
    }
    public unsafe struct ArchetypeChunk : IEquatable<ArchetypeChunk>
    {
        public int ChunkEntityCount => default;
        public int Count => default;
        public bool Equals(ArchetypeChunk other) => true;
        public override bool Equals(object obj) => true;
        public override int GetHashCode() => default;
        public NativeArray<Entity> GetNativeArray(EntityTypeHandle chunkComponentTypeHandle) => default;
        public NativeArray<T> GetNativeArray<T>(ref ComponentTypeHandle<T> chunkComponentTypeHandle) where T : unmanaged, IComponentData => default;
        public readonly BufferAccessor<T> GetBufferAccessor<T>(ref BufferTypeHandle<T> bufferTypeHandle)
            where T : unmanaged, IBufferElementData => default;
        public EnabledMask GetEnabledMask<T>(ref ComponentTypeHandle<T> chunkComponentTypeHandle)
            where T : unmanaged, IComponentData, IEnableableComponent
            => default;

        public T GetSharedComponent<T>(SharedComponentTypeHandle<T> aspect2EcsTestSharedCompScAc) => default;
    }

    public unsafe struct BufferAccessor<T>
        where T : unmanaged, IBufferElementData
    {
        public DynamicBuffer<T> this[int index] => default;
    }

    public enum ScheduleGranularity
    {
        Chunk = 0,
        Entity = 1,
    }

    public struct EntityQueryMask
    {
        public bool MatchesIgnoreFilter(Entity entity) => false;
    }

    public unsafe struct ComponentLookup<T> where T : struct, IComponentData
    {
        public void Update(ref SystemState systemState){}

        public RefRW<T> GetRefRW(Entity entity) => default;
        public RefRO<T> GetRefRO(Entity entity) => default;

        public RefRO<T> GetRefROOptional(Entity entity) => default;
        public RefRW<T> GetRefRWOptional(Entity entity) => default;

        public EnabledRefRW<T2> GetEnabledRefRW<T2>(Entity entity)
            where T2 : unmanaged, IComponentData, IEnableableComponent => default;
        public EnabledRefRW<T2> GetEnabledRefRWOptional<T2>(Entity entity)
            where T2 : unmanaged, IComponentData, IEnableableComponent => default;

        public EnabledRefRO<T2> GetEnabledRefRO<T2>(Entity entity)
            where T2 : unmanaged, IComponentData, IEnableableComponent => default;
        public EnabledRefRO<T2> GetEnabledRefROOptional<T2>(Entity entity)
            where T2 : unmanaged, IComponentData, IEnableableComponent => default;

        public T this[Entity e] => default;
        public bool HasComponent(Entity entity) => true;
    }

    public unsafe struct BufferLookup<T> where T : unmanaged, IBufferElementData
    {
        public void Update(ref SystemState systemState){}
        public DynamicBuffer<T> this[Entity e] => default;
        public bool HasBuffer(Entity entity) => default;
    }

    public unsafe struct EntityStorageInfoLookup
    {
        public void Update(SystemBase system) { }
        public void Update(ref SystemState systemState) { }
        public bool Exists(Entity entity) => default;

        public EntityStorageInfo this[Entity entity] => default;
    }

    public struct EntityStorageInfo
    {
        public ArchetypeChunk Chunk;
        public int IndexInChunk;
    }

    public abstract unsafe partial class ComponentSystemBase
    {
        protected internal virtual void OnCreateForCompiler() {}
        protected internal EntityQuery GetEntityQuery(params ComponentType[] componentTypes) => default;
        protected EntityQuery GetEntityQuery(NativeArray<ComponentType> componentTypes) => default;
        protected internal EntityQuery GetEntityQuery(params EntityQueryDesc[] queryDesc) => default;
        public T GetSingleton<T>() where T : unmanaged, IComponentData => default;
        protected JobHandle Dependency { get; set; }
        protected ComponentTypeHandle<T> GetComponentTypeHandle<T>(bool isReadOnly) => default;
        public ComponentLookup<T> GetComponentLookup<T>(bool isReadOnly = false)
            where T : unmanaged, IComponentData => default;
        public BufferLookup<T> GetBufferLookup<T>(bool isReadOnly = false)
            where T : unmanaged, IBufferElementData => default;
        public EntityManager EntityManager => throw default;
    }

    public abstract unsafe class SystemBase : ComponentSystemBase
    {
        protected void CompleteDependency() { }
        protected abstract void OnUpdate();
        protected internal ref SystemState CheckedStateRef => throw new Exception();
        protected new JobHandle Dependency { get; set; }
        protected internal ForEachLambdaJobDescription Entities => new ForEachLambdaJobDescription();
        protected internal LambdaSingleJobDescription Job => new LambdaSingleJobDescription();

        protected internal T GetComponent<T>(Entity entity) where T : unmanaged, IComponentData => default;
        protected internal void SetComponent<T>(Entity entity, T component) where T : unmanaged, IComponentData{}
        protected internal bool HasComponent<T>(Entity entity) where T : unmanaged, IComponentData => true;

        public DynamicBuffer<T> GetBuffer<T>(Entity entity, bool isReadOnly = false) where T : unmanaged, IBufferElementData => default;
        protected internal bool HasBuffer<T>(Entity entity) where T : struct, IBufferElementData => true;

        public new ComponentLookup<T> GetComponentLookup<T>(bool isReadOnly = false)
            where T : unmanaged, IComponentData => default;

        public new BufferLookup<T> GetBufferLookup<T>(bool isReadOnly = false)
            where T : unmanaged, IBufferElementData => default;

        public bool Exists(Entity entity) => true;
        public ref readonly TimeData Time => throw default;
        public World World => default;
    }

    public class World
    {
        public T GetOrCreateSystemManaged<T>() where T : ComponentSystemBase => default;
    }

    public abstract unsafe partial class EntityCommandBufferSystem : SystemBase
    {
        public EntityCommandBuffer CreateCommandBuffer() => default;
        public void AddJobHandleForProducer(JobHandle producerJob)
        {
        }
    }

    public interface ISystem
    {
        void OnUpdate(ref SystemState state) {}
        void OnCreate(ref SystemState state) {}
        void OnDestroy(ref SystemState state) {}
    }

    public interface IComponentData : IQueryTypeParameter { }

    public interface ISharedComponentData {}
    public interface IEnableableComponent { }

    public delegate void ForEachDelegate<T1,T2>(ref T1 t1, ref T2 t2);

    public interface IBufferElementData { }

    public struct DynamicBuffer<T> : IQueryTypeParameter where T : unmanaged
    {
        public T this[int i]
        {
            get
            {
                return default;
            }
            set
            {
                value = default;
            }
        }
    }

    public struct SystemState
    {
        public EntityQuery GetEntityQuery(ComponentType a) => default;
        public EntityQuery GetEntityQuery(params EntityQueryDesc[] queryDesc) => default;
        public ComponentTypeHandle<T> GetComponentTypeHandle<T>(bool isReadOnly = false) where T : struct, IComponentData => default;
        public BufferTypeHandle<T> GetBufferTypeHandle<T>(bool isReadOnly = false)
            where T : unmanaged, IBufferElementData => default;
        public ComponentLookup<T> GetComponentLookup<T>(bool isReadOnly = false)
            where T : struct, IComponentData => default;
        public BufferLookup<T> GetBufferLookup<T>(bool isReadOnly = false) where T : unmanaged, IBufferElementData =>
            default;
        public void CompleteDependency(){}
        public EntityTypeHandle GetEntityTypeHandle() => default;
        public EntityManager EntityManager => default;
        public EntityStorageInfoLookup GetEntityStorageInfoLookup() => default;
        public JobHandle Dependency { get; set; }
        public SharedComponentTypeHandle<T> GetSharedComponentTypeHandle<T>() => default;
        public Allocator WorldUpdateAllocator => default;
    }

    public struct ComponentType
    {
        public enum AccessMode
        {
            ReadWrite,
            ReadOnly,
            Exclude
        }
        public static unsafe ComponentType[] Combine(params ComponentType[][] componentTypes) => default;
        public static ComponentType ReadWrite<T>() => default;
        public static ComponentType ReadOnly<T>() => default;
        public ComponentType(Type type, AccessMode accessModeType = AccessMode.ReadWrite)
        {
        }
    }

    public struct EntityQuery
    {
        public Entity GetSingletonEntity() => default;
        public EntityQueryMask GetEntityQueryMask() => default;
        public EntityQueryMask GetEntityQueryMask(EntityQuery query) => default;
        public RefRW<T> GetSingletonRW<T>() where T : unmanaged, IComponentData => throw new Exception();
        public T GetSingleton<T>() where T : unmanaged, IComponentData => default;
        public void SetChangedVersionFilter(ComponentType componentType) {}
        public void SetChangedVersionFilter(ComponentType[] componentType) {}
        public void SetSharedComponentFilter<T>(T sharedComponent) where T : unmanaged, ISharedComponentData
        {
        }

        public void ResetFilter() {}
        public bool IsEmptyIgnoreFilter => default;
        public NativeArray<int> CalculateBaseEntityIndexArrayAsync(AllocatorHandle allocator, JobHandle additionalInputDep, out JobHandle outJobHandle) => default;
    }

    public static class EntityQueryExtentions {
        public static T GetSingleton<T>(this EntityQuery query) where T : class, IComponentData => default;
    }

    public struct SystemAPIQueryBuilder
    {
        public SystemAPIQueryBuilder WithAll<T1>() => default;
        public SystemAPIQueryBuilder WithAny<T1>() => default;
        public SystemAPIQueryBuilder WithNone<T1>() => default;
        public SystemAPIQueryBuilder WithDisabled<T1>() => default;
        public SystemAPIQueryBuilder WithAbsent<T1>() => default;
        public SystemAPIQueryBuilder WithOptions(EntityQueryOptions options) => default;
        public EntityQuery Build() => default;
    }

    public static class SystemAPI
    {
        public static ComponentTypeHandle<T> GetComponentTypeHandle<T>(bool isReadOnly = false) where T : unmanaged, IComponentData => default;
        public static BufferTypeHandle<T> GetBufferTypeHandle<T>(bool isReadOnly = false) where T : unmanaged, IBufferElementData => default;

        // Query builder
        public static SystemAPIQueryBuilder QueryBuilder() => default;

        // Query
        public static QueryEnumerable<T> Query<T>()
            where T : struct, IQueryTypeParameter
            => default;

        public static QueryEnumerable<global::System.ValueTuple<T1, T2>> Query<T1, T2>()
            where T1 : struct, IQueryTypeParameter
            where T2 : struct, IQueryTypeParameter
            => default;

        public static QueryEnumerable<global::System.ValueTuple<T1, T2, T3>> Query<T1, T2, T3>()
            where T1 : struct, IQueryTypeParameter
            where T2 : struct, IQueryTypeParameter
            where T3 : struct, IQueryTypeParameter
            => default;

        // Time
        public static ref readonly TimeData Time => throw new Exception();

        // Components
        public static ComponentLookup<T> GetComponentLookup<T>(bool isReadOnly = false) where T : unmanaged, IComponentData => default;
        public static T GetComponent<T>(Entity entity) where T : struct, IComponentData => default;
        public static void SetComponent<T>(Entity entity, T component) where T : struct, IComponentData {}
        public static bool HasComponent<T>(Entity entity) where T : struct, IComponentData => default;
        public static RefRO<T> GetComponentRO<T>(Entity entity) where T : unmanaged, IComponentData => default;
        public static RefRW<T> GetComponentRW<T>(Entity entity) where T : unmanaged, IComponentData => default;

        // Buffer
        public static BufferLookup<T> GetBufferLookup<T>(bool isReadOnly = false) where T : unmanaged, IBufferElementData => default;
        public static bool HasBuffer<T>(Entity entity) where T : unmanaged, IBufferElementData => default;
        public static DynamicBuffer<T> GetBuffer<T>(Entity entity) where T : unmanaged, IBufferElementData => default;

        // StorageInfo
        public static EntityStorageInfoLookup GetEntityStorageInfoLookup() => default;
        public static bool Exists(Entity entity) => default;

        // Singletons
        public static RefRW<T> GetSingletonRW<T>() where T : unmanaged, IComponentData => throw new Exception();
        public static T GetSingleton<T>() where T : unmanaged, IComponentData => throw new Exception();

        // Aspects
        public static T GetAspect<T>(Entity entity) where T : struct, IAspect => default;

        public static class ManagedAPI
        {
            public static T GetComponent<T>(Entity entity) where T : class => default;
            public static T GetSingleton<T>() where T : class => throw new Exception();
        }

        public static Entity GetSingletonEntity<T>() where T : unmanaged => throw new Exception();
        public static bool HasSingleton<T>() where T : unmanaged => throw new Exception();
        public static bool IsComponentEnabled<T>(Entity entity) where T : unmanaged, IComponentData, IEnableableComponent => throw new Exception();
        public static void SetComponentEnabled<T>(Entity entity, bool value) where T : unmanaged, IComponentData, IEnableableComponent => throw new Exception();
    }

    public struct QueryEnumerable<T> : IEnumerable<T> where T : struct
    {
        public QueryEnumerable<T> WithAll<TComponent>() => throw ThrowCodeGenException();
        public QueryEnumerable<T> WithAny<TComponent>() => throw ThrowCodeGenException();
        public QueryEnumerable<T> WithNone<TComponent>() => throw ThrowCodeGenException();
        public QueryEnumerable<T> WithDisabled<TComponent>() => throw ThrowCodeGenException();
        public QueryEnumerable<T> WithAbsent<TComponent>() => throw ThrowCodeGenException();
        public QueryEnumerable<T> WithSharedComponentFilter<T1>(T1 sharedComp) => throw ThrowCodeGenException();
        public QueryEnumerable<T> WithSharedComponentFilter<T1, T2>(T1 sharedComp1, T2 sharedComp2) => throw ThrowCodeGenException();
        public QueryEnumerable<T> WithChangeFilter<T1>() => throw ThrowCodeGenException();
        public QueryEnumerable<T> WithChangeFilter<T1, T2>() => throw ThrowCodeGenException();
        public QueryEnumerable<T> WithFilter(NativeArray<Entity> entities) => throw ThrowCodeGenException();
        public QueryEnumerable<T> WithOptions(EntityQueryOptions options) => throw ThrowCodeGenException();

        public T Current => throw ThrowCodeGenException();
        public bool MoveNext() => false;
        public void Reset() {}
        public void Dispose() {}
        public IEnumerator<T> GetEnumerator() => throw ThrowCodeGenException();
        IEnumerator IEnumerable.GetEnumerator() => throw ThrowCodeGenException();

        static InvalidOperationException ThrowCodeGenException() => throw new InvalidOperationException();

        public QueryEnumerableWithEntity<T> WithEntityAccess() => default;
    }

    public readonly struct QueryEnumerableWithEntity<T1> : IEnumerable<(T1, Entity)>
    {
        public QueryEnumerableWithEntity(T1 item1, Entity entity){}

        public IEnumerator<(T1, Entity)> GetEnumerator() => default;
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Deconstruct(out T1 item1, out Entity entity) => throw new NotImplementedException();
    }

    public interface IQueryTypeParameter {}
    public interface IAspect : IQueryTypeParameter {}

    public interface IAspectCreate<T> : IQueryTypeParameter where T : IAspect
    {
        T CreateAspect(Entity entity, ref SystemState system);
        void AddComponentRequirementsTo(ref UnsafeList<ComponentType> all);
        void CompleteDependencyBeforeRO(ref SystemState state);
        void CompleteDependencyBeforeRW(ref SystemState state);
    }

    public interface ISystemCompilerGenerated
    {
        void OnCreateForCompiler(ref SystemState state);
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class DOTSCompilerPatchedMethodAttribute : Attribute
    {
        public DOTSCompilerPatchedMethodAttribute(string targetMethodName) { }
    }

    public class EntityIndexInChunk : Attribute {}
    public class EntityIndexInQuery : Attribute {}
    public class ChunkIndexInQuery : Attribute {}
    public class OptionalAttribute : Attribute{}
    public class DisableGenerationAttribute : Attribute{}

    [Flags]
    public enum EntityQueryOptions
    {
        Default = 0,
        IncludePrefab = 1,
        IncludeDisabled = 2,
        FilterWriteGroup = 4,
        IgnoreEnabledBits = 8,
        IncludeSystems = 16,
        IncludeDisabledEntities
    }

    public class EntityQueryDesc : IEquatable<EntityQueryDesc>
    {
        public ComponentType[] Any = Array.Empty<ComponentType>();
        public ComponentType[] None = Array.Empty<ComponentType>();
        public ComponentType[] All = Array.Empty<ComponentType>();
        public ComponentType[] Disabled = Array.Empty<ComponentType>();
        public ComponentType[] Absent = Array.Empty<ComponentType>();
        public EntityQueryOptions Options = EntityQueryOptions.Default;

        public bool Equals(EntityQueryDesc other) => true;
        public override bool Equals(object obj) => true;
        public override int GetHashCode() => default;
    }

    public static class JobChunkExtensions
    {
        public static unsafe void RunByRef<T>(this ref T jobData, EntityQuery query)
            where T : struct, IJobChunk {}
        public static unsafe JobHandle ScheduleByRef<T>(this ref T jobData, EntityQuery query, JobHandle dependsOn)
            where T : struct, IJobChunk => default;
        public static unsafe JobHandle ScheduleParallelByRef<T>(this ref T jobData, EntityQuery query, JobHandle dependsOn)
            where T : struct, IJobChunk => default;
    }

    public struct EntityTypeHandle
    {
        public void Update(SystemBase system) {}
        public void Update(ref SystemState state) {}
    }

    public struct EntityManager
    {
        public T GetComponentObject<T>(Entity entity) => default;

        private class StructuralChangeMethodAttribute : Attribute { }

        public int EntityOrderVersion => throw default;

        [StructuralChangeMethod]
        public bool RemoveComponent<T>(Entity entity) => throw default;

        [StructuralChangeMethod]
        public Entity CreateEntity() => throw default;

        [StructuralChangeMethod]
        public Entity CreateEntity(params ComponentType[] types) => default;

        public void DestroyEntity(EntityQuery entityQuery)
        {
        }

        public EntityQuery CreateEntityQuery(params ComponentType[] requiredComponents) => default;
        public void CompleteDependencyBeforeRW<T>(){}
        public void CompleteDependencyBeforeRO<T>(){}

        public T GetAspect<T>(Entity entity) where T : struct, IAspect => default;

        public void AddComponent<T>(EntityQuery query) {}
        public void AddComponentData<T>(Entity entity, T componentData) where T : class, IComponentData {}
    }

    namespace Serialization
    {
        public struct UntypedWeakReferenceId {}
    }
}

namespace Unity.Collections
{
    public struct NativeList<T>
    {
        public NativeList(int initialCapacity, Allocator allocator)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    public sealed class ReadOnlyAttribute : Attribute
    {
    }

    public struct NativeArray<T> : IDisposable, IEnumerable<T>, IEnumerable, IEquatable<NativeArray<T>>
    {
        public unsafe ReadOnly AsReadOnly() => default;
        public void Dispose(){}
        public IEnumerator<T> GetEnumerator() => default;
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public bool Equals(NativeArray<T> other) => default;
        public int Length => default;

        public unsafe T this[int index] => default;

        public struct ReadOnly : IEnumerable<T>, IEnumerable
        {
            public int Length => default;
            public unsafe T this[int index] => default;
            public IEnumerator<T> GetEnumerator() => throw new NotImplementedException();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}

namespace Unity.Collections.LowLevel.Unsafe
{
    public unsafe struct UnsafeList<T> : IEnumerable<T>, IDisposable
        where T : unmanaged
    {
        public UnsafeList(int initialCapacity, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
        {}

        public IEnumerator<T> GetEnumerator() => default!;

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void AddRange(UnsafeList<T> list)
        {
        }

        public void Add(in T value)
        {
        }

        public void Dispose()
        {
        }
    }
}

namespace Unity.Jobs
{
    public struct JobHandle : IEquatable<JobHandle>
    {
        public bool Equals(JobHandle other) => default;

        public override bool Equals(object obj) => obj is JobHandle other && Equals(other);

        public override int GetHashCode() => throw new NotImplementedException();
    }

    namespace LowLevel.Unsafe
    {
        public static class JobsUtility
        {
            public static bool JobCompilerEnabled { get; set; }
        }

        public sealed class JobProducerTypeAttribute : Attribute
        {
            public JobProducerTypeAttribute(Type producerType) { }
        }
    }

    public interface IJob{}

    public static class IJobExtensions
    {
        public static JobHandle Schedule<T>(this T job, JobHandle dependency) where T : struct, IJob => default;
    }
}

namespace Unity.Mathematics
{
    public static partial class math
    {
        public static int countbits(ulong x) => default;
        public static int min(int x, int y) { return x < y ? x : y; }
    }
}

namespace UnityEngine
{
    public class Object{}
    public class Component:Object{}
    public class Transform : Component{}
}

namespace Unity.Core
{
    public readonly struct TimeData
    {
        public readonly double ElapsedTime;
        public readonly float DeltaTime;
    }
}
