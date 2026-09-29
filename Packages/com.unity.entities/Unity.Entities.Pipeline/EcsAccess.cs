using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Unity.Entities.Pipeline
{
    // Public EntityManager APIs provide change tracking and the same setters used by the inspector.
    internal abstract class EcsAccess
    {
        static readonly Dictionary<Type, EcsAccess> Cache = new Dictionary<Type, EcsAccess>();

        internal static EcsAccess For(Type type)
        {
            if (Cache.TryGetValue(type, out var access)) return access;
            Type definition;
            if (typeof(IBufferElementData).IsAssignableFrom(type)) definition = typeof(BufferAccess<>);
            else if (typeof(ISharedComponentData).IsAssignableFrom(type)) definition = typeof(SharedAccess<>);
            else if (typeof(IComponentData).IsAssignableFrom(type))
            {
                if (type.IsValueType) definition = typeof(ComponentAccess<>);
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                else definition = typeof(ManagedAccess<>);
#else
                else throw new NotSupportedException("Managed components are disabled in this build.");
#endif
            }
            else throw new NotSupportedException($"Unsupported component category: {type.FullName}.");
            try { access = (EcsAccess)Activator.CreateInstance(definition.MakeGenericType(type)); }
            catch (ArgumentException error) { throw new NotSupportedException($"Cannot construct an accessor for {type.FullName}.", error); }
            Cache.Add(type, access);
            return access;
        }

        internal abstract object Read(EntityManager manager, Entity entity, bool chunk);
        internal abstract void Write(EntityManager manager, Entity entity, bool chunk, object value);
        internal abstract void Add(EntityManager manager, Entity entity, bool chunk, object value);
        internal virtual void Filter(EntityQuery query, object value) => throw new ArgumentException("A shared filter requires ISharedComponentData.");
        internal virtual int Length(EntityManager manager, Entity entity) => throw new ArgumentException("A buffer type is required.");
        internal virtual object Element(EntityManager manager, Entity entity, int index) => throw new ArgumentException("A buffer type is required.");
        internal virtual void EditBuffer(EntityManager manager, Entity entity, EcsOperation operation) => throw new ArgumentException("A buffer type is required.");

        sealed class ComponentAccess<T> : EcsAccess where T : unmanaged, IComponentData
        {
            internal override object Read(EntityManager manager, Entity entity, bool chunk) =>
                TypeManager.IsZeroSized(TypeManager.GetTypeIndex<T>()) ? default(T) :
                chunk ? manager.GetChunkComponentData<T>(entity) : manager.GetComponentData<T>(entity);
            internal override void Write(EntityManager manager, Entity entity, bool chunk, object value)
            {
                if (TypeManager.IsZeroSized(TypeManager.GetTypeIndex<T>())) return;
                if (chunk) manager.SetChunkComponentData(manager.GetChunk(entity), (T)value);
                else manager.SetComponentData(entity, (T)value);
            }
            internal override void Add(EntityManager manager, Entity entity, bool chunk, object value)
            {
                if (chunk) manager.AddChunkComponentData<T>(entity);
                else manager.AddComponent<T>(entity);
                if (value != null) Write(manager, entity, chunk, value);
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        sealed class ManagedAccess<T> : EcsAccess where T : class, IComponentData, new()
        {
            internal override object Read(EntityManager manager, Entity entity, bool chunk) =>
                chunk ? manager.GetChunkComponentData<T>(entity) : manager.GetComponentData<T>(entity);
            internal override void Write(EntityManager manager, Entity entity, bool chunk, object value)
            {
                if (chunk) manager.SetChunkComponentData(manager.GetChunk(entity), (T)value);
                else manager.SetComponentData(entity, (T)value);
            }
            internal override void Add(EntityManager manager, Entity entity, bool chunk, object value)
            {
                if (chunk) manager.AddChunkComponentData<T>(entity);
                else manager.AddComponent<T>(entity);
                Write(manager, entity, chunk, value ?? new T());
            }
        }
#endif

        sealed class SharedAccess<T> : EcsAccess where T : struct, ISharedComponentData
        {
            internal override object Read(EntityManager manager, Entity entity, bool chunk) => manager.GetSharedComponentManaged<T>(entity);
            internal override void Write(EntityManager manager, Entity entity, bool chunk, object value) => manager.SetSharedComponentManaged(entity, (T)value);
            internal override void Add(EntityManager manager, Entity entity, bool chunk, object value) => manager.AddSharedComponentManaged(entity, value == null ? default : (T)value);
            internal override void Filter(EntityQuery query, object value) => query.AddSharedComponentFilterManaged((T)value);
        }

        sealed class BufferAccess<T> : EcsAccess where T : unmanaged, IBufferElementData
        {
            internal override object Read(EntityManager manager, Entity entity, bool chunk) => throw new ArgumentException("Read buffer elements through a range.");
            internal override void Write(EntityManager manager, Entity entity, bool chunk, object value) => throw new ArgumentException("Use a buffer operation to write a buffer.");
            internal override void Add(EntityManager manager, Entity entity, bool chunk, object value)
            {
                var buffer = manager.AddBuffer<T>(entity);
                if (value is T[] elements)
                    foreach (var element in elements) buffer.Add(element);
            }
            internal override int Length(EntityManager manager, Entity entity) => manager.GetBuffer<T>(entity, true).Length;
            internal override object Element(EntityManager manager, Entity entity, int index) => manager.GetBuffer<T>(entity, true)[index];

            internal override void EditBuffer(EntityManager manager, Entity entity, EcsOperation operation)
            {
                // Validate/convert before obtaining writable buffer access.
                var length = Length(manager, entity);
                T[] values = null;
                if (operation.Op == "bufferReplace" || operation.Op == "bufferInsert")
                {
                    if (operation.Elements == null) throw new ArgumentException("elements is required.");
                    values = new T[operation.Elements.Count];
                    for (var i = 0; i < values.Length; i++) values[i] = (T)EcsValueCodec.Decode(operation.Elements[i], typeof(T));
                }
                object edited = null;
                switch (operation.Op)
                {
                    case "bufferSet":
                        if (operation.Index < 0 || operation.Index >= length) throw new ArgumentOutOfRangeException("index");
                        edited = EcsValueCodec.Patch(Element(manager, entity, operation.Index), typeof(T), operation.Fields);
                        break;
                    case "bufferResize":
                        if (operation.Length < 0) throw new ArgumentOutOfRangeException("length");
                        break;
                    case "bufferInsert":
                        if (operation.Index < 0 || operation.Index > length) throw new ArgumentOutOfRangeException("index");
                        checked { _ = length + values.Length; }
                        break;
                    case "bufferRemove":
                        if (operation.Index < 0 || operation.Count < 0 || operation.Index > length - operation.Count)
                            throw new ArgumentException("Buffer removal range is out of bounds.");
                        break;
                    case "bufferReplace": break;
                    default: throw new ArgumentException($"Unknown buffer operation '{operation.Op}'.");
                }
                var buffer = manager.GetBuffer<T>(entity);
                switch (operation.Op)
                {
                    case "bufferSet": buffer[operation.Index] = (T)edited; break;
                    case "bufferResize":
                        buffer.ResizeUninitialized(operation.Length);
                        for (var i = length; i < buffer.Length; i++) buffer[i] = default;
                        break;
                    case "bufferReplace": buffer.CopyFrom(values); break;
                    case "bufferInsert":
                        for (var i = 0; i < values.Length; i++) buffer.Insert(operation.Index + i, values[i]);
                        break;
                    case "bufferRemove": buffer.RemoveRange(operation.Index, operation.Count); break;
                }
            }
        }
    }
}
