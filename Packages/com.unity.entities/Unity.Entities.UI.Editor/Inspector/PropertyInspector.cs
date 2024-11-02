namespace Unity.Entities.UI
{
    /// <summary>
    /// Base class for defining a custom inspector for field values of type <see cref="TValue"/> when it is tagged with an
    /// attribute of type <see cref="TAttribute"/>.
    /// </summary>
    /// <typeparam name="TValue">The type of the field value to inspect.</typeparam>
    internal abstract class PropertyInspector<TValue> : InspectorBase<TValue>, IPropertyDrawer
    {
    }

    /// <summary>
    /// Base class for defining a custom inspector for field values of type <see cref="TValue"/> when it is tagged with an
    /// attribute of type <see cref="TAttribute"/>.
    /// </summary>
    /// <typeparam name="TValue">The type of the field value to inspect.</typeparam>
    /// <typeparam name="TAttribute">The property drawer type. </typeparam>
    internal abstract class PropertyInspector<TValue, TAttribute> : InspectorBase<TValue>, IPropertyDrawer<TAttribute>
        where TAttribute : UnityEngine.PropertyAttribute
    {
        /// <summary>
        /// Returns the <see cref="UnityEngine.PropertyAttribute"/> of the field.
        /// </summary>
        protected TAttribute DrawerAttribute => GetAttribute<TAttribute>();
    }
}
