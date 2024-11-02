using System;
using System.Collections.Generic;
using Unity.Properties;

namespace Unity.Entities
{
    interface ITypedVisit<TValue>
    {
        void Visit<TContainer>(Property<TContainer, TValue> property, ref TContainer container, ref TValue value);
    }

    unsafe class ManagedObjectRemap :
        IPropertyBagVisitor,
        IPropertyVisitor,
        ITypedVisit<Entity>
    {
        /// <summary>
        /// Set used to track already visited references, in order to avoid infinite recursion.
        /// </summary>
        HashSet<object> m_References;

        // Standard Remap Info
        EntityRemapUtility.EntityRemapInfo* m_Info;

        // Prefab Remap Info
        Entity* m_PrefabSrc;
        Entity* m_PrefabDst;
        int m_PrefabCount;

        IPropertyBag GetPropertyBag(object obj)
        {
            if (null == obj)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            var type = obj.GetType();
            ref readonly var typeInfo = ref TypeManager.GetTypeInfo(TypeManager.GetTypeIndex(type));

            if (typeInfo.Category == TypeManager.TypeCategory.UnityEngineObject)
            {
                throw new ArgumentException("Cannot remap hybrid components", nameof(obj));
            }

            var properties = PropertyBag.GetPropertyBag(type);

            if (null == properties)
            {
                throw new MissingPropertyBagException(type);
            }

            return properties;
        }

        /// <summary>
        /// Remaps all entity references within the given object using the specified <see cref="EntityRemapUtility.EntityRemapInfo"/>.
        /// </summary>
        /// <param name="obj">The object to remap references for.</param>
        /// <param name="entityRemapInfo">The entity remap information.</param>
        /// <exception cref="ArgumentNullException">The given object was null.</exception>
        /// <exception cref="MissingPropertyBagException">The given object has no property bag associated with it.</exception>
        public void RemapEntityReferences(ref object obj, EntityRemapUtility.EntityRemapInfo* entityRemapInfo)
        {
            m_Info = entityRemapInfo;
            m_PrefabSrc = null;
            m_PrefabDst = null;
            m_PrefabCount = 0;

            m_References?.Clear();
            GetPropertyBag(obj).Accept(this, ref obj);
        }

        /// <summary>
        /// Remaps all entity references within the given object using the specified <see cref="EntityRemapUtility.EntityRemapInfo"/>.
        /// </summary>
        /// <param name="obj">The object to remap references for.</param>
        /// <param name="remapSrc">Array of entities that should be remapped.</param>
        /// <param name="remapDst">Array of entities that each entry in the remapSrc array should be remapped to.</param>
        /// <param name="remapInfoCount">Length of the entity arrays.</param>
        /// <exception cref="ArgumentNullException">The given object was null.</exception>
        /// <exception cref="MissingPropertyBagException">The given object has no property bag associated with it.</exception>
        public void RemapEntityReferencesForPrefab(ref object obj, Entity* remapSrc, Entity* remapDst, int remapInfoCount)
        {
            m_Info = null;
            m_PrefabSrc = remapSrc;
            m_PrefabDst = remapDst;
            m_PrefabCount = remapInfoCount;

            m_References?.Clear();
            GetPropertyBag(obj).Accept(this, ref obj);
        }

        /// <summary>
        /// Invoked by Unity.Properties for each container type (i.e. struct or class).
        /// </summary>
        /// <remarks>
        /// We do not explicitly override collection visitation. Instead it will simply fall through to this call and enumerate all elements.
        /// </remarks>
        /// <param name="properties">The property bag being visited.</param>
        /// <param name="container">The container being visited.</param>
        /// <typeparam name="TContainer">The container type.</typeparam>
        void IPropertyBagVisitor.Visit<TContainer>(IPropertyBag<TContainer> properties, ref TContainer container)
        {
            foreach (var property in properties.GetProperties(ref container))
                ((IPropertyAccept<TContainer>)property).Accept(this, ref container);
        }

        /// <summary>
        /// Invoked by Unity.Properties for any non-collection property.
        /// </summary>
        /// <param name="property">The property being visited.</param>
        /// <param name="container">The source container.</param>
        /// <typeparam name="TContainer">The container type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        void IPropertyVisitor.Visit<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container)
        {
            var value = property.GetValue(ref container);

            if (TypeTraits<TValue>.CanBeNull && null == value)
                return;

            if (!TypeTraits<TValue>.IsValueType && typeof(string) != typeof(TValue))
            {
                if (m_References == null)
                    m_References = new HashSet<object>();

                if (!m_References.Add(value))
                    return;
            }

            if (value is UnityEngine.Object)
                return;

            if (this is ITypedVisit<TValue> typed)
            {
                typed.Visit(property, ref container, ref value);
                property.SetValue(ref container, value);
                return;
            }

            PropertyContainer.Accept(this, ref value);

            if (!property.IsReadOnly && TypeTraits<TValue>.IsValueType)
                property.SetValue(ref container, value);
        }

        /// <summary>
        /// Invoked for each <see cref="Entity"/> member encountered.
        /// </summary>
        /// <param name="property">The property being visited.</param>
        /// <param name="container">The source container.</param>
        /// <param name="value">The entity value.</param>
        /// <typeparam name="TContainer">The container type.</typeparam>
        /// <returns>The status of the adapter visit.</returns>
        void ITypedVisit<Entity>.Visit<TContainer>(Property<TContainer, Entity> property, ref TContainer container, ref Entity value)
        {
            value = null != m_Info
                ? EntityRemapUtility.RemapEntity(m_Info, value)
                : EntityRemapUtility.RemapEntityForPrefab(m_PrefabSrc, m_PrefabDst, m_PrefabCount, value);
        }

        public void ClearGCRefs()
        {
            m_References?.Clear();
        }
    }
}
