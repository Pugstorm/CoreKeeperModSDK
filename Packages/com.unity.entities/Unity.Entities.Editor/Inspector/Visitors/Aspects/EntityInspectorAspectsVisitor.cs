using System;
using Unity.Properties;

namespace Unity.Entities.Editor
{
    class EntityInspectorAspectsVisitor : PropertyVisitor
    {
        readonly EntityInspectorContext m_Context;
        public AspectElementBase Result { get; private set; }

        public EntityInspectorAspectsVisitor(EntityInspectorContext context)
        {
            m_Context = context;
        }

        protected override void VisitProperty<TContainer, TValue>(Property<TContainer, TValue> property,
            ref TContainer container, ref TValue value)
        {
            if (property is not IEntityAspectsCollectionContainerProperty)
                return;

            Result = new AspectElement<TValue>(property, m_Context, ref value);
        }
    }
}
