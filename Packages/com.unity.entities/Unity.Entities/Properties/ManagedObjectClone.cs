using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Properties;

namespace Unity.Entities
{
    /// <summary>
    /// Unity.Properties visitor used to deep clone object instances. This is an internal class.
    /// </summary>
    /// <remarks>
    /// An instance of this class can be re-used for multiple clone operations.
    /// </remarks>
    class ManagedObjectClone :
        IPropertyBagVisitor,
        IListPropertyBagVisitor,
        ISetPropertyBagVisitor,
        IDictionaryPropertyBagVisitor,
        IPropertyVisitor
    {
        /// <summary>
        /// Map used to track and copy references within the same object.
        /// </summary>
        Dictionary<object, object> m_References;

        /// <summary>
        /// The root source. This is the object we are cloning from.
        /// </summary>
        object m_RootSource;

        /// <summary>
        /// The root destination. This is the object we are cloning to.
        /// </summary>
        object m_RootDestination;

        /// <summary>
        /// The current destination container being written to. This is pushed and popped as we traverse the tree.
        /// </summary>
        /// <remarks>
        /// At the end of visitation this will contain the final cloned object.
        /// </remarks>
        object m_Stack;

        /// <summary>
        /// Creates a new object that is a copy of the given instance.
        /// </summary>
        /// <param name="obj">The object to clone.</param>
        /// <returns>A new object that is a copy of the given instance.</returns>
        public object Clone(object obj)
        {
            switch (obj)
            {
                case null:
                    return null;
                case ICloneable cloneable:
                    return cloneable.Clone();
            }

            var type = obj.GetType();

            // UnityEngine references are always by reference.
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return obj;
            var instance = type.IsArray
                ? Array.CreateInstance(type.GetElementType(), obj is IList srcList ? srcList.Count : 0)
                : Activator.CreateInstance(type);

            // Visit the source container and write to the dst.
            var properties = PropertyBag.GetPropertyBag(type);

            if (null == properties)
                throw new MissingPropertyBagException(type);

            m_References?.Clear();

            m_RootSource = obj;
            m_RootDestination = instance;

            // Push the instance on the stack.
            m_Stack = instance;
            properties.Accept(this, ref obj);
            return m_Stack;
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
                ((IPropertyAccept<TContainer>)property).Accept(this, ref srcContainer);
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
            // Unbox the current destination container being written to.
            var dstContainer = (TList)m_Stack;

            if (typeof(TList).IsArray)
            {
                // We assume the list has been initialized correctly by a previous call to CloneValue.
                var index = 0;

                foreach (var element in srcContainer)
                {
                    var value = default(TElement);
                    CloneValue(ref value, element);
                    dstContainer[index++] = value;
                }
            }
            else
            {
                dstContainer.Clear();

                foreach (var element in srcContainer)
                {
                    var value = default(TElement);
                    CloneValue(ref value, element);
                    dstContainer.Add(value);
                }
            }

            // Re-box the container.
            m_Stack = dstContainer;
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
            // Unbox the current destination container being written to.
            var dstContainer = (TSet)m_Stack;

            dstContainer.Clear();

            foreach (var element in srcContainer)
            {
                var value = default(TElement);
                CloneValue(ref value, element);
                dstContainer.Add(value);
            }

            // Re-box the container.
            m_Stack = dstContainer;
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
            // Unbox the current destination container being written to.
            var dstContainer = (TDictionary)m_Stack;

            dstContainer.Clear();

            foreach (var kvp in srcContainer)
            {
                var key = default(TKey);
                var value = default(TValue);

                CloneValue(ref key, kvp.Key);
                CloneValue(ref value, kvp.Value);

                dstContainer.Add(key, value);
            }

            // Re-box the container.
            m_Stack = dstContainer;
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
            CloneValue(ref dstValue, srcValue);

            if (property.IsReadOnly)
                return;

            property.SetValue(ref dstContainer, dstValue);

            // Re-box the container.
            m_Stack = dstContainer;
        }

        /// <summary>
        /// Constructs and initializes the given instance of <paramref name="dstValue"/> based on the given <paramref name="srcValue"/>.
        /// </summary>
        /// <param name="dstValue">The destination value to initialize.</param>
        /// <param name="srcValue">The source value to initialize based on.</param>
        /// <typeparam name="TValue">The value type.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CloneValue<TValue>(ref TValue dstValue, TValue srcValue)
        {
            // Values types can be copied as-is.
            if (!TypeTraits<TValue>.IsContainer)
            {
                dstValue = srcValue;
                return;
            }

            if (TypeTraits<TValue>.CanBeNull && null == srcValue)
            {
                dstValue = default;
                return;
            }

            if (TypeTraits<TValue>.IsValueType)
            {
                dstValue = default;
            }
            else
            {
                var type = srcValue.GetType();

                // UnityEngine references can be copied as-is.
                if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                {
                    dstValue = srcValue;
                    return;
                }

                // Boxed value types can be copied as-is.
                if (!TypeTraits.IsContainer(type))
                {
                    dstValue = srcValue;
                    return;
                }

                var isReferenceType = !(type.IsValueType || type == typeof(string));

                if (isReferenceType)
                {
                    // If we have already encountered this object before. Use the already created instance.
                    // This is to support things like circular references/graph structures.
                    // The root object is not added to the map so we need to explicitly check for it.
                    if (m_RootSource == (object)srcValue)
                    {
                        dstValue = (TValue)m_RootDestination;
                        return;
                    }

                    // Otherwise let's check the map.
                    if (null != m_References && m_References.TryGetValue(srcValue, out var existingReference))
                    {
                        dstValue = (TValue)existingReference;
                        return;
                    }
                }

                // We are dealing with an arbitrary csharp object.
                // We may or may not need to construct a new instance depending on the default constructor of the container.
                // If we already have a use-able instance we will use that instead of creating a new one.
                // This is both for performance and to handle init-only fields.
                if (type.IsArray)
                {
                    var count = srcValue is IList srcList ? srcList.Count : 0;

                    if (null == dstValue || (dstValue as Array)?.Length != count)
                    {
                        dstValue = (TValue)(object)Array.CreateInstance(type.GetElementType(), count);
                    }
                }
                else
                {
                    if (null == dstValue || dstValue.GetType() != type)
                    {
                        dstValue = (TValue)Activator.CreateInstance(type);
                    }
                }

                // Retain a mapping of references within this object. This is needed to support things like circular references.
                if (isReferenceType)
                {
                    if (null == m_References) m_References = new Dictionary<object, object>();
                    m_References.Add(srcValue, dstValue);
                }
            }

            // We now have a properly constructed instance of the dstValue, nest in and copy all members.
            // Unity.Properties will automatically handle collection type from this point.
            var dstContainer = m_Stack;

            m_Stack = dstValue;
            PropertyContainer.TryAccept(this, ref srcValue, out _);
            dstValue = (TValue)m_Stack;

            m_Stack = dstContainer;
        }

        public void ClearGCRefs()
        {
            m_References?.Clear();
            m_Stack = null;
            m_RootSource = null;
            m_RootDestination = null;
        }
    }
}
