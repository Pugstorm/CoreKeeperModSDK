using Unity.Properties;

namespace Unity.Entities.Editor
{
    class EntityInspectorBuilderVisitor : IPropertyVisitor, IListPropertyVisitor
    {
        readonly EntityInspectorContext m_Context;

        public ComponentElementBase Result { get; private set; }

        public EntityInspectorBuilderVisitor(EntityInspectorContext context)
        {
            m_Context = context;
        }

        void IPropertyVisitor.Visit<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container)
        {
            if (property is not IComponentProperty componentProperty)
                return;

            var value = property.GetValue(ref container);

            switch (componentProperty.Type)
            {
                case ComponentPropertyType.Tag:
                {
                    Result = new TagElement<TValue>(componentProperty, m_Context);
                    break;
                }
                case ComponentPropertyType.Buffer:
                {
                    if (PropertyBag.TryGetPropertyBagForValue(ref value, out var valuePropertyBag)
                        && valuePropertyBag is IListPropertyAccept<TValue> accept)
                    {
                        // Revisit as a list
                        accept.Accept(this, property, ref container, ref value);
                    }

                    break;
                }
                default:
                {
                    Result = new ComponentElement<TValue>(componentProperty, m_Context, ref value);
                    break;
                }
            }
        }

        void IListPropertyVisitor.Visit<TContainer, TList, TElement>(Property<TContainer, TList> property, ref TContainer container, ref TList list)
        {
            if (property is not IComponentProperty { Type: ComponentPropertyType.Buffer } componentProperty)
                return;

            var buffer = new InspectedBuffer<TList, TElement> { Value = list };
            var element = new BufferElement<TList, TElement>(componentProperty, m_Context, ref buffer);
            Result = element;
        }
    }
}
