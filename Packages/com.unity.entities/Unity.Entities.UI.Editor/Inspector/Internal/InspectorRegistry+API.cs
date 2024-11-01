using System.Collections.Generic;
using Unity.Properties;

namespace Unity.Entities.UI
{
    static partial class InspectorRegistry
    {
        public static IInspector GetInspector<TValue>(InspectorVisitor.InspectorContext inspectorContext,
            IProperty property,
            ref TValue value, PropertyPath path)
        {
            if (TypeTraits<TValue>.CanBeNull && EqualityComparer<TValue>.Default.Equals(value, default))
                return null;

            if (!TypeTraits<TValue>.IsContainer)
            {
                var inspector = InspectorRegistry.GetInspector<TValue>();
                if (null != inspector)
                {
                    inspector.Context = new InspectorContext<TValue>(
                        inspectorContext.Root,
                        path,
                        property,
                        property.GetAttributes()
                    );
                }

                return inspector;
            }

            using (var scoped = ScopedVisitor<CustomInspectorVisitor<TValue>>.Make())
            {
                var visitor = scoped.Visitor;
                visitor.Target = InspectorTarget.Inspector;
                visitor.PropertyPath = path;
                visitor.Root = inspectorContext.Root;
                visitor.Property = property;
                PropertyContainer.Accept(visitor, ref value);
                return visitor.Inspector;
            }
        }

        public static IInspector GetPropertyInspector<TValue>(InspectorVisitor.InspectorContext inspectorContext,
            IProperty property, ref TValue value, PropertyPath path)
        {
            if (TypeTraits<TValue>.CanBeNull && EqualityComparer<TValue>.Default.Equals(value, default))
                return null;

            if (!TypeTraits<TValue>.IsContainer)
            {
                var inspector = InspectorRegistry.GetPropertyInspector<TValue>(property);
                if (null != inspector)
                {
                    inspector.Context = new InspectorContext<TValue>(
                        inspectorContext.Root,
                        path,
                        property,
                        property.GetAttributes()
                    );
                }

                return inspector;
            }

            using (var scoped = ScopedVisitor<CustomInspectorVisitor<TValue>>.Make())
            {
                var visitor = scoped.Visitor;
                visitor.Target = InspectorTarget.PropertyInspector;
                visitor.PropertyPath = path;
                visitor.Root = inspectorContext.Root;
                visitor.Property = property;
                PropertyContainer.Accept(visitor, ref value);
                return visitor.Inspector;
            }
        }

        public static IInspector GetAttributeInspector<TValue>(InspectorVisitor.InspectorContext inspectorContext,
            IProperty property, ref TValue value, PropertyPath path)
        {
            if (TypeTraits<TValue>.CanBeNull && EqualityComparer<TValue>.Default.Equals(value, default))
                return null;

            if (!TypeTraits<TValue>.IsContainer)
            {
                var inspector = InspectorRegistry.GetAttributeInspector<TValue>(property);
                if (null != inspector)
                {
                    inspector.Context = new InspectorContext<TValue>(
                        inspectorContext.Root,
                        path,
                        property,
                        property.GetAttributes()
                    );
                }

                return inspector;
            }

            using (var scoped = ScopedVisitor<CustomInspectorVisitor<TValue>>.Make())
            {
                var visitor = scoped.Visitor;
                visitor.Target = InspectorTarget.AttributeInspector;
                visitor.PropertyPath = path;
                visitor.Root = inspectorContext.Root;
                visitor.Property = property;
                PropertyContainer.Accept(visitor, ref value);
                return visitor.Inspector;
            }
        }
    }
}
