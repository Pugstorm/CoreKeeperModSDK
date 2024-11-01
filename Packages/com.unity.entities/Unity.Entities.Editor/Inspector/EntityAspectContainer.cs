using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Properties;

namespace Unity.Entities.Editor
{
    readonly struct EntityAspectContainer<TAspect>
        where TAspect : struct, IAspect, IAspectCreate<TAspect>
    {
        static EntityAspectContainer()
        {
            PropertyBag.Register(new EntityAspectPropertyBag<TAspect>());
        }

        public readonly World World;
        public readonly Entity Entity;

        internal EntityAspectContainer(World world, Entity entity)
        {
            World = world;
            Entity = entity;
        }

        public bool Exists()
        {
            if (World == null || !World.IsCreated)
                return false;

            return Entity.Index >= 0
#if ENTITY_STORE_V1
                && (uint) Entity.Index < (uint) World.EntityManager.EntityCapacity
#endif
                && World.EntityManager.Exists(Entity);
        }

        public EntityAspectAccess<TAspect> GetAccess()
        {
            if (!Exists())
                throw new InvalidOperationException("Attempt to access invalid EntityAspect");

            return new EntityAspectAccess<TAspect>(World.EntityManager, Entity);
        }
    }

    readonly struct EntityAspectAccess<TAspect> : IDisposable
        where TAspect : struct, IAspect, IAspectCreate<TAspect>
    {
        readonly Entity m_Entity;
        readonly EntityManager m_EntityManager;

        internal EntityAspectAccess(EntityManager entityManager, Entity entity)
        {
            m_Entity = entity;
            m_EntityManager = entityManager;
        }

        public TAspect GetAspect() => m_EntityManager.GetAspect<TAspect>(m_Entity);

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// The <see cref="EntityAspectPropertyBag{TAspect}"/> exposes all properties of an underlying <see cref="TAspect"/> in a safe way.
    /// </summary>
    /// <typeparam name="TAspect">The aspect which the <see cref="EntityAspectContainer{TAspect}"/> wraps.</typeparam>
    class EntityAspectPropertyBag<TAspect> : PropertyBag<EntityAspectContainer<TAspect>>, INamedProperties<EntityAspectContainer<TAspect>>
        where TAspect : struct, IAspect, IAspectCreate<TAspect>
    {
        static readonly MethodInfo k_GetPropertyMethodInfo = typeof(EntityAspectPropertyBag<TAspect>).GetMethod(nameof(GetProperty), BindingFlags.NonPublic | BindingFlags.Static);

        static EntityAspectProperty<TAspect, TValue> GetProperty<TValue>(IProperty property)
            => new EntityAspectProperty<TAspect, TValue>(property.Name, property.IsReadOnly);

        public override PropertyCollection<EntityAspectContainer<TAspect>> GetProperties()
            => PropertyCollection<EntityAspectContainer<TAspect>>.Empty;

        public override PropertyCollection<EntityAspectContainer<TAspect>> GetProperties(ref EntityAspectContainer<TAspect> container)
            => new PropertyCollection<EntityAspectContainer<TAspect>>(EnumerateProperties(container));

        IEnumerable<IProperty<EntityAspectContainer<TAspect>>> EnumerateProperties(EntityAspectContainer<TAspect> container)
        {
            var properties = PropertyBag.GetPropertyBag<TAspect>();

            if (container.Exists())
            {
                // ReSharper disable once ConvertToUsingDeclaration
                using (var access = container.GetAccess())
                {
                    var aspect = access.GetAspect();

                    foreach (var property in properties.GetProperties(ref aspect))
                    {
                        yield return k_GetPropertyMethodInfo.MakeGenericMethod(property.DeclaredValueType()).Invoke(this, new object[] { property }) as IProperty<EntityAspectContainer<TAspect>>;
                    }
                }
            }
        }

        public bool TryGetProperty(ref EntityAspectContainer<TAspect> container, string name, out IProperty<EntityAspectContainer<TAspect>> property)
        {
            var properties = PropertyBag.GetPropertyBag<TAspect>();

            if (container.Exists())
            {
                // ReSharper disable once ConvertToUsingDeclaration
                using (var access = container.GetAccess())
                {
                    var aspect = access.GetAspect();

                    if (properties is INamedProperties<TAspect> nameable && nameable.TryGetProperty(ref aspect, name, out var p))
                    {
                        property = k_GetPropertyMethodInfo.MakeGenericMethod(p.DeclaredValueType()).Invoke(this, new object[] { p }) as IProperty<EntityAspectContainer<TAspect>>;
                        return true;
                    }
                }
            }

            property = null;
            return false;
        }
    }

    /// <summary>
    /// The <see cref="EntityAspectProperty{TAspect, TValue}"/> is used to safely access individual properties of a aspect.
    /// </summary>
    /// <typeparam name="TAspect">The aspect which this property wraps.</typeparam>
    /// <typeparam name="TValue">The declared value type for this property.</typeparam>
    class EntityAspectProperty<TAspect, TValue> : Property<EntityAspectContainer<TAspect>, TValue>
        where TAspect : struct, IAspect, IAspectCreate<TAspect>
    {
        public override string Name { get; }
        public override bool IsReadOnly { get; }

        public EntityAspectProperty(string name, bool isReadOnly)
        {
            Name = name;
            IsReadOnly = isReadOnly;
        }

        public override TValue GetValue(ref EntityAspectContainer<TAspect> container)
        {
            var properties = PropertyBag.GetPropertyBag<TAspect>();

            // ReSharper disable once ConvertToUsingDeclaration
            using (var access = container.GetAccess())
            {
                var aspect = access.GetAspect();

                // Try to use fast name access
                if (properties is INamedProperties<TAspect> nameable && nameable.TryGetProperty(ref aspect, Name, out var p))
                {
                    if (!(p is Property<TAspect, TValue> typed))
                    {
                        throw new InvalidOperationException("Failed to forward aspect property.");
                    }

                    return typed.GetValue(ref aspect);
                }

                // Otherwise we perform a linear scan of all properties.
                foreach (var property in properties.GetProperties(ref aspect))
                {
                    if (string.Equals(property.Name, Name))
                    {
                        if (!(property is Property<TAspect, TValue> typed))
                        {
                            throw new InvalidOperationException("Failed to forward aspect property.");
                        }

                        return typed.GetValue(ref aspect);
                    }
                }
            }

            throw new InvalidOperationException("Failed to forward aspect property.");
        }

        public override void SetValue(ref EntityAspectContainer<TAspect> container, TValue value)
        {
            var properties = PropertyBag.GetPropertyBag<TAspect>();

            // ReSharper disable once ConvertToUsingDeclaration
            using (var access = container.GetAccess())
            {
                var aspect = access.GetAspect();

                // Try to use fast name access
                if (properties is INamedProperties<TAspect> nameable && nameable.TryGetProperty(ref aspect, Name, out var p))
                {
                    if (!(p is Property<TAspect, TValue> typed))
                    {
                        throw new InvalidOperationException("Failed to forward aspect property.");
                    }

                    typed.SetValue(ref aspect, value);
                    return;
                }

                // Otherwise we perform a linear scan of all properties.
                foreach (var property in properties.GetProperties(ref aspect))
                {
                    if (string.Equals(property.Name, Name))
                    {
                        if (!(property is Property<TAspect, TValue> typed))
                        {
                            throw new InvalidOperationException("Failed to forward aspect property.");
                        }

                        typed.SetValue(ref aspect, value);
                        return;
                    }
                }
            }

            throw new InvalidOperationException("Failed to forward aspect property.");
        }
    }
}
