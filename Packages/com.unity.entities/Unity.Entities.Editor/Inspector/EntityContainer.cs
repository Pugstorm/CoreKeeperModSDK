using System;
using System.Collections.Generic;
using Unity.Entities.Editor;
using Unity.Properties;

namespace Unity.Entities
{
    [Flags]
    enum ComponentPropertyType
    {
        None = 0,
        Component = 1 << 1,
        SharedComponent = 1 << 2,
        ChunkComponent = 1 << 3,
        CompanionComponent = 1 << 4,
        [Obsolete("Use ComponentPropertyType.CompanionComponent instead.", error: false)]
        HybridComponent = CompanionComponent,
        Tag = 1 << 5,
        Buffer = 1 << 6,
        All = Component | SharedComponent | ChunkComponent | CompanionComponent | Tag | Buffer
    }

    interface IComponentProperty : IProperty<EntityContainer>
    {
        TypeIndex TypeIndex { get; }
        ComponentPropertyType Type { get; }
    }

    readonly struct EntityContainer
    {
        static EntityContainer()
        {
            PropertyBag.Register(new EntityContainerPropertyBag());
        }

        public EntityManager EntityManager => m_World.EntityManager;
        readonly World m_World;
        public readonly Entity Entity;
        public readonly bool IsReadOnly;

        public int GetComponentCount()
        {
            return Exists() ? EntityManager.GetComponentCount(Entity) : 0;
        }

        public EntityContainer(EntityManager entityManager, Entity entity, bool readOnly = true)
        {
            m_World = entityManager.World;
            Entity = entity;
            IsReadOnly = readOnly;
        }

        internal bool Exists()
        {
            if (m_World is not { IsCreated: true } || Entity == Entity.Null)
                return false;

            return EntityManager.SafeExists(Entity);
        }
    }

    class EntityContainerPropertyBag : PropertyBag<EntityContainer>, INamedProperties<EntityContainer>, IIndexedProperties<EntityContainer>
    {
        class ComponentPropertyConstructor : ITypeVisitor
        {
            public TypeIndex TypeIndex { private get; set; }
            public bool IsReadOnly { private get; set; }

            public IComponentProperty Property { get; private set; }

            void ITypeVisitor.Visit<TComponent>()
            {
                IComponentProperty CreateInstance(Type propertyType)
                    => (IComponentProperty)Activator.CreateInstance(propertyType.MakeGenericType(typeof(TComponent)), TypeIndex, IsReadOnly);

                var type = typeof(TComponent);
                if (typeof(IComponentData).IsAssignableFrom(type))
                {
                    if (TypeManager.IsChunkComponent(TypeIndex))
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                        Property = CreateInstance(type.IsValueType
                        ? typeof(StructChunkComponentProperty<>)
                        : typeof(ClassChunkComponentProperty<>));
#else
                        Property = CreateInstance(typeof(StructChunkComponentProperty<>));
#endif
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                    else if (TypeManager.IsManagedComponent(TypeIndex))
                        Property = CreateInstance(typeof(ClassComponentProperty<>));
#endif
                    else
                        Property = CreateInstance(typeof(StructComponentProperty<>));
                }
                else if (typeof(ISharedComponentData).IsAssignableFrom(type))
                    Property = CreateInstance(typeof(SharedComponentProperty<>));
                else if (typeof(IBufferElementData).IsAssignableFrom(type))
                    Property = CreateInstance(typeof(DynamicBufferProperty<>));
                else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                    Property = CreateInstance(typeof(ManagedComponentProperty<>));
                else
                    throw new InvalidOperationException();
            }
        }

        abstract class ComponentProperty<TComponent> : Property<EntityContainer, TComponent>, IComponentProperty
        {
            public override string Name { get; } = SanitizedPropertyName(TypeUtility.GetTypeDisplayName(typeof(TComponent)));
            public override bool IsReadOnly { get; }
            public TypeIndex TypeIndex { get; }
            public abstract ComponentPropertyType Type { get; }

            public ComponentProperty(TypeIndex typeIndex, bool isReadOnly)
            {
                TypeIndex = typeIndex;
                IsReadOnly = isReadOnly;
            }

            public override TComponent GetValue(ref EntityContainer container)
            {
                return IsZeroSize ? default : DoGetValue(ref container);
            }

            public override void SetValue(ref EntityContainer container, TComponent value)
            {
                if (IsReadOnly) throw new NotSupportedException("Property is ReadOnly");

                if (IsZeroSize)
                    return;

                DoSetValue(ref container, value);
            }

            protected abstract TComponent DoGetValue(ref EntityContainer container);
            protected abstract void DoSetValue(ref EntityContainer container, TComponent value);
            protected abstract bool IsZeroSize { get; }
        }

        class SharedComponentProperty<TComponent> : ComponentProperty<TComponent>
            where TComponent : struct, ISharedComponentData
        {
            protected override bool IsZeroSize { get; } = false;
            public override ComponentPropertyType Type { get; } = ComponentPropertyType.SharedComponent;

            public SharedComponentProperty(TypeIndex typeIndex, bool isReadOnly) : base(typeIndex, isReadOnly)
            {
            }

            protected override TComponent DoGetValue(ref EntityContainer container)
            {
                var entityManager = container.EntityManager;
                return entityManager.GetSharedComponentManaged<TComponent>(container.Entity);
            }

            protected override void DoSetValue(ref EntityContainer container, TComponent value)
            {
                var entityManager = container.EntityManager;
                entityManager.SetSharedComponentManaged(container.Entity, value);
            }
        }

        class StructComponentProperty<TComponent> : ComponentProperty<TComponent>
            where TComponent : unmanaged, IComponentData
        {
            protected override bool IsZeroSize { get; } = TypeManager.IsZeroSized(TypeManager.GetTypeIndex<TComponent>());
            public override ComponentPropertyType Type => IsZeroSize ? ComponentPropertyType.Tag : ComponentPropertyType.Component;

            public StructComponentProperty(TypeIndex typeIndex, bool isReadOnly) : base(typeIndex, isReadOnly)
            {
            }

            protected override TComponent DoGetValue(ref EntityContainer container)
            {
                var entityManager = container.EntityManager;
                return entityManager.GetComponentData<TComponent>(container.Entity);
            }

            protected override void DoSetValue(ref EntityContainer container, TComponent value)
            {
                var entityManager = container.EntityManager;
                entityManager.SetComponentData(container.Entity, value);
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        class ClassComponentProperty<TComponent> : ComponentProperty<TComponent>
            where TComponent : class, IComponentData, new()
        {
            protected override bool IsZeroSize { get; } = TypeManager.IsZeroSized(TypeManager.GetTypeIndex<TComponent>());
            public override ComponentPropertyType Type => IsZeroSize ? ComponentPropertyType.Tag : ComponentPropertyType.Component;

            public ClassComponentProperty(TypeIndex typeIndex, bool isReadOnly) : base(typeIndex, isReadOnly)
            {
            }

            protected override TComponent DoGetValue(ref EntityContainer container)
            {
                var entityManager = container.EntityManager;
                return entityManager.GetComponentData<TComponent>(container.Entity);
            }

            protected override void DoSetValue(ref EntityContainer container, TComponent value)
            {
                var entityManager = container.EntityManager;
                entityManager.SetComponentData(container.Entity, value);
            }
        }
#endif

        class ManagedComponentProperty<TComponent> : ComponentProperty<TComponent>
            where TComponent : UnityEngine.Object
        {
            protected override bool IsZeroSize { get; } = TypeManager.IsZeroSized(TypeManager.GetTypeIndex<TComponent>());
            public override ComponentPropertyType Type => IsZeroSize ? ComponentPropertyType.Tag : ComponentPropertyType.CompanionComponent;

            public ManagedComponentProperty(TypeIndex typeIndex, bool isReadOnly) : base(typeIndex, isReadOnly)
            {
            }

            protected override TComponent DoGetValue(ref EntityContainer container)
            {
                var entityManager = container.EntityManager;
                return entityManager.GetComponentObject<TComponent>(container.Entity);
            }

            protected override void DoSetValue(ref EntityContainer container, TComponent value)
            {
                var entityManager = container.EntityManager;
                entityManager.SetComponentObject(container.Entity, typeof(TComponent), value);
            }
        }

        class StructChunkComponentProperty<TComponent> : ComponentProperty<TComponent>
            where TComponent : unmanaged, IComponentData
        {
            protected override bool IsZeroSize { get; } = TypeManager.IsZeroSized(TypeManager.GetTypeIndex<TComponent>());
            public override ComponentPropertyType Type => IsZeroSize ? ComponentPropertyType.Tag : ComponentPropertyType.ChunkComponent;

            public StructChunkComponentProperty(TypeIndex typeIndex, bool isReadOnly) : base(typeIndex, isReadOnly)
            {
            }

            protected override TComponent DoGetValue(ref EntityContainer container)
            {
                var entityManager = container.EntityManager;
                return entityManager.GetChunkComponentData<TComponent>(container.Entity);
            }

            protected override void DoSetValue(ref EntityContainer container, TComponent value)
            {
                var entityManager = container.EntityManager;
                entityManager.SetChunkComponentData(entityManager.GetChunk(container.Entity), value);
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        class ClassChunkComponentProperty<TComponent> : ComponentProperty<TComponent>
            where TComponent : class, IComponentData, new()
        {
            protected override bool IsZeroSize { get; } = TypeManager.IsZeroSized(TypeManager.GetTypeIndex<TComponent>());
            public override ComponentPropertyType Type => IsZeroSize ? ComponentPropertyType.Tag : ComponentPropertyType.ChunkComponent;

            public ClassChunkComponentProperty(TypeIndex typeIndex, bool isReadOnly) : base(typeIndex, isReadOnly)
            {
            }

            protected override TComponent DoGetValue(ref EntityContainer container)
            {
                var entityManager = container.EntityManager;
                return entityManager.GetChunkComponentData<TComponent>(container.Entity);
            }

            protected override void DoSetValue(ref EntityContainer container, TComponent value)
            {
                var entityManager = container.EntityManager;
                entityManager.SetChunkComponentData(entityManager.GetChunk(container.Entity), value);
            }
        }
#endif

        class DynamicBufferProperty<TElement> : ComponentProperty<DynamicBufferContainer<TElement>>
            where TElement : unmanaged, IBufferElementData
        {
            public override string Name => SanitizedPropertyName(Properties.TypeUtility.GetTypeDisplayName(typeof(TElement)));
            protected override bool IsZeroSize { get; } = TypeManager.IsZeroSized(TypeManager.GetTypeIndex<TElement>());
            public override ComponentPropertyType Type { get; } = ComponentPropertyType.Buffer;

            public DynamicBufferProperty(TypeIndex typeIndex, bool isReadOnly) : base(typeIndex, isReadOnly)
            {
            }

            protected override DynamicBufferContainer<TElement> DoGetValue(ref EntityContainer container)
            {
                return new DynamicBufferContainer<TElement>(container, TypeIndex, IsReadOnly);
            }

            protected override void DoSetValue(ref EntityContainer container, DynamicBufferContainer<TElement> value)
            {
                // Nothing to do here, the container already proxies the data.
            }
        }

        readonly Dictionary<TypeIndex, IComponentProperty> m_ReadOnlyPropertyCache = new Dictionary<TypeIndex, IComponentProperty>();
        readonly Dictionary<TypeIndex, IComponentProperty> m_ReadWritePropertyCache = new Dictionary<TypeIndex, IComponentProperty>();

        readonly ComponentPropertyConstructor m_ComponentPropertyConstructor = new ComponentPropertyConstructor();

        public override PropertyCollection<EntityContainer> GetProperties()
        {
            return PropertyCollection<EntityContainer>.Empty;
        }

        public override PropertyCollection<EntityContainer> GetProperties(ref EntityContainer container)
        {
            return new PropertyCollection<EntityContainer>(EnumerateProperties(container));
        }

        IEnumerable<IProperty<EntityContainer>> EnumerateProperties(EntityContainer container)
        {
            if (!container.Exists())
                yield break;

            var entityManager = container.EntityManager;
            var count = container.GetComponentCount();
            for (var i = 0; i < count; i++)
            {
                var typeIndex = entityManager.GetComponentTypeIndex(container.Entity, i);
                var property = GetOrCreatePropertyForType(typeIndex, container.IsReadOnly);
                yield return property;
            }
        }

        bool INamedProperties<EntityContainer>.TryGetProperty(ref EntityContainer container, string name, out IProperty<EntityContainer> property)
        {
            if (!container.Exists())
            {
                property = null;
                return false;
            }

            foreach (var p in EnumerateProperties(container))
            {
                if (p.Name != name) continue;
                property = p;
                return true;
            }

            property = null;
            return false;
        }

        bool IIndexedProperties<EntityContainer>.TryGetProperty(ref EntityContainer container, int index, out IProperty<EntityContainer> property)
        {
            if (!container.Exists())
            {
                property = null;
                return false;
            }

            var entityManager = container.EntityManager;
            property = GetOrCreatePropertyForType(entityManager.GetComponentTypeIndex(container.Entity, index), container.IsReadOnly);
            return true;
        }

        IComponentProperty GetOrCreatePropertyForType(TypeIndex typeIndex, bool isReadOnly)
        {
            var cache = isReadOnly ? m_ReadOnlyPropertyCache : m_ReadWritePropertyCache;

            if (cache.TryGetValue(typeIndex, out var property))
                return property;

            m_ComponentPropertyConstructor.TypeIndex = typeIndex;
            m_ComponentPropertyConstructor.IsReadOnly = isReadOnly;
            PropertyBag.GetPropertyBag(TypeManager.GetType(typeIndex)).Accept(m_ComponentPropertyConstructor);
            cache.Add(typeIndex, m_ComponentPropertyConstructor.Property);
            return m_ComponentPropertyConstructor.Property;
        }

        static string SanitizedPropertyName(string typeDisplayName)
        {
            return typeDisplayName.Replace(".", "_");
        }
    }
}
