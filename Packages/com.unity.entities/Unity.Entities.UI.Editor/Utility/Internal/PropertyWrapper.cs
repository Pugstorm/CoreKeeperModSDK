using Unity.Properties;

namespace Unity.Entities.UI
{
    interface IPropertyWrapper
    {

    }

    struct PropertyWrapper<T> : IPropertyWrapper
    {
        class PropertyBag : ContainerPropertyBag<PropertyWrapper<T>>, IPropertyWrapper
        {
            Property Property { get; } = new Property();

            public PropertyBag()
                => AddProperty(Property);
        }

        class Property : Property<PropertyWrapper<T>, T>, IPropertyWrapper
        {
            public override string Name => nameof(Value);
            public override bool IsReadOnly => false;
            public override T GetValue(ref PropertyWrapper<T> container) => container.Value;
            public override void SetValue(ref PropertyWrapper<T> container, T value) => container.Value = value;
        }

        public T Value;

        public PropertyWrapper(T value)
        {
            if (!Properties.PropertyBag.Exists<PropertyWrapper<T>>())
            {
                Properties.PropertyBag.Register(new PropertyBag());
            }

            Value = value;
        }
    }
}
