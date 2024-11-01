using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Properties;

namespace Unity.Entities
{
    /// <summary>
    /// Unity.Properties visitor used to deep compare two object instances. This is an internal class.
    /// </summary>
    /// <remarks>
    /// An instance of this class can be re-used for multiple clone operations.
    /// </remarks>
    class ManagedObjectEqual :
        IPropertyBagVisitor,
        IListPropertyBagVisitor,
        ISetPropertyBagVisitor,
        IDictionaryPropertyBagVisitor,
        IPropertyVisitor
    {
        /// <summary>
        /// Map used to track of references within the same object.
        /// </summary>
        Dictionary<object, object> m_References;

        /// <summary>
        /// This root lhs object to compare.
        /// </summary>
        object m_LhsObject;

        /// <summary>
        /// This root rhs object to compare.
        /// </summary>
        object m_RhsObject;

        /// <summary>
        /// The current destination container on the stack. This is pushed and popped as we traverse the tree.
        /// </summary>
        object m_Stack;

        /// <summary>
        /// Returns true if the two objects are equal.
        /// </summary>
        /// <remarks>
        /// This value starts as true and will only be set false during visitation.
        /// </remarks>
        bool m_Equals;

        /// <summary>
        /// Determines whether two object instances are equal using a deep comparison.
        /// </summary>
        /// <param name="lhs">The first object to compare.</param>
        /// <param name="rhs">The second object to compare.</param>
        /// <returns><see langword="true"/> if the objects are considered equal; otherwise, <see langword="false"/>.</returns>
        public bool CompareEqual(object lhs, object rhs)
        {
            if (lhs == null) return rhs == null;
            if (rhs == null) return false;

            var type = lhs.GetType();

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return lhs.Equals(rhs);

            var properties = PropertyBag.GetPropertyBag(type);

            if (null == properties)
                throw new MissingPropertyBagException(type);

            m_References?.Clear();
            m_LhsObject = lhs;
            m_RhsObject = rhs;
            m_Equals = true;

            m_Stack = rhs;
            properties.Accept(this, ref lhs);
            return m_Equals;
        }

        /// <summary>
        /// Invoked by Unity.Properties for each container type (i.e. struct or class).
        /// </summary>
        /// <param name="properties">The property bag being visited.</param>
        /// <param name="srcContainer">The source container being visited.</param>
        /// <typeparam name="TContainer">The container type.</typeparam>
        void IPropertyBagVisitor.Visit<TContainer>(IPropertyBag<TContainer> properties, ref TContainer srcContainer)
        {
            foreach (var property in properties.GetProperties(ref srcContainer))
            {
                if (!m_Equals) return;
                ((IPropertyAccept<TContainer>)property).Accept(this, ref srcContainer);
            }
        }

        /// <summary>
        /// Invoked by Unity.Properties for each list container type (i.e. Array or IList type)
        /// </summary>
        /// <remarks>
        /// This will be called for built in array types.
        /// </remarks>
        /// <param name="properties">The property bag being visited.</param>
        /// <param name="srcContainer">The source list being visited.</param>
        /// <typeparam name="TList">The list type.</typeparam>
        /// <typeparam name="TElement">The element type.</typeparam>
        void IListPropertyBagVisitor.Visit<TList, TElement>(IListPropertyBag<TList, TElement> properties, ref TList srcContainer)
        {
            var dstContainer = (TList)m_Stack;

            if (srcContainer.Count != dstContainer.Count)
            {
                m_Equals = false;
                return;
            }

            for (var i = 0; i < srcContainer.Count; i++)
            {
                if (CompareEquality(srcContainer[i], dstContainer[i])) continue;
                m_Equals = false;
                return;
            }
        }

        /// <summary>
        /// Invoked by Unity.Properties for each set container type. (i.e. ISet type)
        /// </summary>
        /// <param name="properties">The property bag being visited.</param>
        /// <param name="srcContainer">The source set being visited.</param>
        /// <typeparam name="TSet">The set type.</typeparam>
        /// <typeparam name="TElement">The element type.</typeparam>
        void ISetPropertyBagVisitor.Visit<TSet, TElement>(ISetPropertyBag<TSet, TElement> properties, ref TSet srcContainer)
        {
            var dstContainer = (TSet)m_Stack;

            if (srcContainer.Count != dstContainer.Count)
            {
                m_Equals = false;
                return;
            }

            using (var srcContainerEnumerator = srcContainer.GetEnumerator())
            using (var dstContainerEnumerator = dstContainer.GetEnumerator())
            {
                for (;;)
                {
                    var srcNext = srcContainerEnumerator.MoveNext();
                    var dstNext = dstContainerEnumerator.MoveNext();

                    if (srcNext != dstNext)
                    {
                        m_Equals = false;
                        return;
                    }

                    if (!srcNext) break;

                    if (CompareEquality(srcContainerEnumerator.Current, dstContainerEnumerator.Current)) continue;
                    m_Equals = false;
                    return;
                }
            }
        }

        /// <summary>
        /// Invoked by Unity.Properties for each dictionary container type. (i.e. IDictionary{TKey, TValue} type)
        /// </summary>
        /// <param name="properties">The property bag being visited.</param>
        /// <param name="srcContainer">The source dictionary being visited</param>
        /// <typeparam name="TDictionary">The dictionary type.</typeparam>
        /// <typeparam name="TKey">The key type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        void IDictionaryPropertyBagVisitor.Visit<TDictionary, TKey, TValue>(IDictionaryPropertyBag<TDictionary, TKey, TValue> properties, ref TDictionary srcContainer)
        {
            var dstContainer = (TDictionary)m_Stack;

            if (srcContainer.Count != dstContainer.Count)
            {
                m_Equals = false;
                return;
            }

            using (var srcContainerEnumerator = srcContainer.GetEnumerator())
            using (var dstContainerEnumerator = dstContainer.GetEnumerator())
            {
                for (;;)
                {
                    var srcNext = srcContainerEnumerator.MoveNext();
                    var dstNext = dstContainerEnumerator.MoveNext();

                    if (srcNext != dstNext)
                    {
                        m_Equals = false;
                        return;
                    }

                    if (!srcNext) break;

                    var keysAreEqual = CompareEquality(srcContainerEnumerator.Current.Key, dstContainerEnumerator.Current.Key);
                    var valuesAreEqual = CompareEquality(srcContainerEnumerator.Current.Key, dstContainerEnumerator.Current.Key);

                    if (keysAreEqual && valuesAreEqual) continue;
                    m_Equals = false;
                    return;
                }
            }
        }

        /// <summary>
        /// Invoked by Unity.Properties for any non-collection property.
        /// </summary>
        /// <param name="property">The property being visited.</param>
        /// <param name="srcContainer">The source container.</param>
        /// <typeparam name="TContainer">The container type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        void IPropertyVisitor.Visit<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer srcContainer)
        {
            // Unbox the current destination container being written to.
            var dstContainer = (TContainer)m_Stack;

            // It's important that we get the existing instance of the dstValue, even though we are cloning. This lets us handle init-only fields etc.
            var srcValue = property.GetValue(ref srcContainer);
            var dstValue = property.GetValue(ref dstContainer);

            // Copy the srcValue --> dstValue.
            if (!CompareEquality(dstValue, srcValue)) m_Equals = false;
        }

        /// <summary>
        /// Compares the given values for equality.
        /// </summary>
        /// <param name="lhs">The left hand side to test.</param>
        /// <param name="rhs">The right hand side to test.</param>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <returns><see langword="true"/> if the values are equals, otherwise; <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool CompareEquality<TValue>(TValue lhs, TValue rhs)
        {
            if (!TypeTraits<TValue>.IsContainer)
            {
                return EqualityComparer<TValue>.Default.Equals(lhs, rhs);
            }

            if (TypeTraits<TValue>.CanBeNull)
            {
                if (null == lhs) return null == rhs;
                if (null == rhs) return false;
            }

            if (!TypeTraits<TValue>.IsValueType)
            {
                if (ReferenceEquals(lhs, rhs))
                    return true;

                var type = lhs.GetType();

                // UnityEngine references can be copied as-is.
                if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                {
                    return EqualityComparer<TValue>.Default.Equals(lhs, rhs);
                }

                // Boxed value types can be compared as-is using the default comparer (with boxing).
                if (!TypeTraits.IsContainer(type))
                {
                    return EqualityComparer<TValue>.Default.Equals(lhs, rhs);
                }

                var isReferenceType = !(type.IsValueType || type == typeof(string));

                if (isReferenceType)
                {
                    // If this is a reference within the same object. We need to compare against the reference within the other object.
                    // This is to support things like circular references/graph structures.
                    // The root object is not added to the map so we need to explicitly check for it.
                    if (m_LhsObject == (object)lhs)
                        return m_RhsObject.Equals(rhs);

                    // Otherwise let's check the map if it exists.
                    if (null != m_References && m_References.TryGetValue(lhs, out var existingReference))
                        return existingReference.Equals(rhs);

                    // Retain a mapping of references within this object. This is needed to support things like circular references.
                    if (null == m_References) m_References = new Dictionary<object, object>();
                    m_References.Add(lhs, rhs);
                }
            }

            // We now have a properly constructed instance of the dstValue, nest in and copy all members.
            // Unity.Properties will automatically handle collection type from this point.
            var dstContainer = m_Stack;

            m_Stack = rhs;
            PropertyContainer.TryAccept(this, ref lhs, out _);
            m_Stack = dstContainer;

            return m_Equals;
        }
    }
}
