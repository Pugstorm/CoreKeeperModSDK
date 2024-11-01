using Unity.Properties;

namespace Unity.Entities.UI
{
    partial class InspectorVisitor
        : IPropertyBagVisitor
    {
        void IPropertyBagVisitor.Visit<TContainer>(
            IPropertyBag<TContainer> properties,
            ref TContainer container)
        {
            foreach (var property in properties.GetProperties(ref container))
            {
                property.Accept(this, ref container);
            }
        }
    }
}
