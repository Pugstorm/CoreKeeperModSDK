using System;
using System.Collections.Generic;
using Unity.Properties;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// The <see cref="EntityAspectsCollectionContainer"/> is used to expose all valid <see cref="IAspect"/> objects as properties for a given <see cref="Entity"/>.
    /// </summary>
    readonly struct EntityAspectsCollectionContainer
    {
        static EntityAspectsCollectionContainer()
        {
            PropertyBag.Register(new EntityAspectsCollectionContainerPropertyBag());
        }

        public readonly World World;
        public readonly Entity Entity;
        public readonly bool IsReadOnly;

        public EntityAspectsCollectionContainer(World world, Entity entity, bool readOnly = true)
        {
            World = world;
            Entity = entity;
            IsReadOnly = readOnly;
        }

        internal bool Exists()
        {
            if (World == null || !World.IsCreated)
                return false;

            return Entity.Index >= 0
#if ENTITY_STORE_V1
                && (uint) Entity.Index < (uint) World.EntityManager.EntityCapacity
#endif
                && World.EntityManager.Exists(Entity);
        }
    }

    /// <summary>
    /// The <see cref="EntityAspectsCollectionContainerPropertyBag"/> is used to expose <see cref="IAspect"/> types as properties for a given <see cref="EntityAspectsCollectionContainer"/> object.
    /// </summary>
    class EntityAspectsCollectionContainerPropertyBag : PropertyBag<EntityAspectsCollectionContainer>, INamedProperties<EntityAspectsCollectionContainer>, IIndexedProperties<EntityAspectsCollectionContainer>
    {
        public override PropertyCollection<EntityAspectsCollectionContainer> GetProperties()
            => PropertyCollection<EntityAspectsCollectionContainer>.Empty;

        public override PropertyCollection<EntityAspectsCollectionContainer> GetProperties(ref EntityAspectsCollectionContainer collectionContainer)
            => new PropertyCollection<EntityAspectsCollectionContainer>(EnumerateProperties(collectionContainer));

        static IEnumerable<IProperty<EntityAspectsCollectionContainer>> EnumerateProperties(EntityAspectsCollectionContainer collectionContainer)
        {
            var aspectTypes = AspectTypeInfoManager.GetAspectTypesFromEntity(collectionContainer.World, collectionContainer.Entity);

            foreach (var aspectType in aspectTypes)
            {
                var containerType = typeof(EntityAspectsCollectionContainerProperty<>).MakeGenericType(aspectType.GetManagedType());
                yield return (IProperty<EntityAspectsCollectionContainer>)Activator.CreateInstance(containerType);
            }
        }

        public bool TryGetProperty(ref EntityAspectsCollectionContainer collectionContainer, string name, out IProperty<EntityAspectsCollectionContainer> property)
        {
            foreach (var p in EnumerateProperties(collectionContainer))
            {
                if (p.Name == name)
                {
                    property = p;
                    return true;
                }
            }

            property = default;
            return false;
        }

        public bool TryGetProperty(ref EntityAspectsCollectionContainer collectionContainer, int index, out IProperty<EntityAspectsCollectionContainer> property)
            => throw new NotImplementedException();
    }

    interface IEntityAspectsCollectionContainerProperty
    {
    }

    /// <summary>
    /// The <see cref="EntityAspectsCollectionContainerProperty{TAspect}"/> is used to expose aspects for a given <see cref="EntityAspectsCollectionContainer"/>.
    /// </summary>
    /// <remarks>
    /// Aspects are returned as safe <see cref="EntityAspectContainer{TAspect}"/> objects.
    /// </remarks>
    /// <typeparam name="TAspect">The underlying aspect.</typeparam>
    class EntityAspectsCollectionContainerProperty<TAspect> : Property<EntityAspectsCollectionContainer, EntityAspectContainer<TAspect>>, IEntityAspectsCollectionContainerProperty
        where TAspect : struct, IAspect, IAspectCreate<TAspect>
    {
        public override string Name { get; } = Properties.TypeUtility.GetTypeDisplayName(typeof(TAspect)).Replace(".", "_");
        public override bool IsReadOnly { get; } = false;

        public override EntityAspectContainer<TAspect> GetValue(ref EntityAspectsCollectionContainer collectionContainer)
            => new EntityAspectContainer<TAspect>(collectionContainer.World, collectionContainer.Entity);

        public override void SetValue(ref EntityAspectsCollectionContainer collectionContainer, EntityAspectContainer<TAspect> value)
        {
        }
    }
}
