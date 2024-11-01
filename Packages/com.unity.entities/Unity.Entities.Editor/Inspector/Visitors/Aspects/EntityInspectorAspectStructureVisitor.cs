using Unity.Properties;

namespace Unity.Entities.Editor
{
    class EntityInspectorAspectStructureVisitor : PropertyVisitor
    {
        public EntityInspectorAspectStructure InspectorAspectStructure { get; }

        public EntityInspectorAspectStructureVisitor()
        {
            InspectorAspectStructure = new EntityInspectorAspectStructure();
        }

        public void Reset()
        {
            InspectorAspectStructure.Reset();
        }

        protected override void VisitProperty<TContainer, TValue>(Property<TContainer, TValue> property,
            ref TContainer container, ref TValue value)
        {
            if (property is IEntityAspectsCollectionContainerProperty)
            {
                InspectorAspectStructure.Aspects.Add(property.Name);
            }
        }
    }
}
