using JetBrains.Annotations;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    /// <summary>
    /// Makes an element that can generate a UI hierarchy for a given object. When generating the UI hierarchy for
    /// the target and its properties, inspector types deriving from <see cref="PropertyInspector{TValue}"/> and
    /// <see cref="PropertyInspector{TValue, TAttribute}"/> will be considered.
    /// </summary>
#if UNITY_2023_3_OR_NEWER
    [UxmlElement]
#endif
    internal sealed partial class PropertyElement : BindingContextElement
    {
        /// <summary>
        /// Creates a new instance of <see cref="PropertyElement"/> with the provided target value.
        /// </summary>
        /// <param name="value">The target.</param>
        /// <typeparam name="TValue">The target type.</typeparam>
        /// <returns>The new instance.</returns>
        public static PropertyElement MakeWithValue<TValue>(TValue value)
        {
            var element = new PropertyElement();
            element.SetTarget(value);
            return element;
        }
        
#if !UNITY_2023_3_OR_NEWER
        /// <summary>
        ///   <para>Instantiates a <see cref="PropertyElement"/> using the data read from a UXML file.</para>
        /// </summary>
        [UsedImplicitly]
        class PropertyElementFactory : UxmlFactory<PropertyElement, PropertyElementTraits>
        {
        }

        /// <summary>
        ///   <para>Defines UxmlTraits for the <see cref="PropertyElement"/>.</para>
        /// </summary>
        class PropertyElementTraits : UxmlTraits
        {
        }
#endif
    }
}
