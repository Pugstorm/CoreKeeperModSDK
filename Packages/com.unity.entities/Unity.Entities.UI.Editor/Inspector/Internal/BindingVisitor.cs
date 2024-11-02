using Unity.Properties;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    class BindingVisitor : PathVisitor
    {
        public VisualElement Element;
        public BindingContextElement Root;

        protected override void VisitPath<TContainer, TValue>(Property<TContainer, TValue> property,
            ref TContainer container, ref TValue value)
        {
            BindingUtilities.Bind(Element, ref value, Path, Root);
        }
    }
}
