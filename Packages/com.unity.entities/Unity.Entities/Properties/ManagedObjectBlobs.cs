using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Properties;

namespace Unity.Entities
{
    unsafe class ManagedObjectBlobs :
        IPropertyBagVisitor,
        IPropertyVisitor,
        ISetPropertyBagVisitor,
        IListPropertyBagVisitor,
        IDictionaryPropertyBagVisitor
    {
        /// <summary>
        /// Set used to track already visited references.
        /// </summary>
        HashSet<object> m_References;

        // Blob Asset Ptr Storage
        NativeList<BlobAssetPtr> m_BlobAssets;
        NativeParallelHashMap<BlobAssetPtr, int> m_BlobAssetMap;

        /// <summary>
        /// Gathers all blob asset references within the specified object.
        /// </summary>
        /// <param name="obj">The object to extract all blob asset references from.</param>
        /// <param name="blobAssets">The array where new blob asset references are added.</param>
        /// <param name="blobAssetMap">Mapping to track existing blob asset references encountered.</param>
        /// <exception cref="ArgumentNullException">The given object was null.</exception>
        /// <exception cref="MissingPropertyBagException">The given object has no property bag associated with it.</exception>
        public void GatherBlobAssetReferences(object obj, NativeList<BlobAssetPtr> blobAssets, NativeParallelHashMap<BlobAssetPtr, int> blobAssetMap)
        {
            if (null == obj)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            var type = obj.GetType();

            var properties = PropertyBag.GetPropertyBag(type);

            if (null == properties)
            {
                throw new MissingPropertyBagException(type);
            }

            m_References?.Clear();

            m_BlobAssets = blobAssets;
            m_BlobAssetMap = blobAssetMap;

            properties.Accept(this, ref obj);
        }

        /// <summary>
        /// Invoked by Unity.Properties for each container type (i.e. struct or class).
        /// </summary>
        /// <param name="properties">The property bag being visited.</param>
        /// <param name="container">The container being visited.</param>
        /// <typeparam name="TContainer">The container type.</typeparam>
        void IPropertyBagVisitor.Visit<TContainer>(IPropertyBag<TContainer> properties, ref TContainer container)
        {
            foreach (var property in properties.GetProperties(ref container))
                property.Accept(this, ref container);
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
            if (TypeTraits<TValue>.IsContainer)
            {
                VisitValue(property.GetValue(ref container));
            }
        }

        /// <summary>
        /// Invoked by Unity.Properties for each ISet based container type.
        /// </summary>
        /// <remarks>
        /// We specialize on well known types to avoid struct enumerator boxing.
        /// </remarks>
        void ISetPropertyBagVisitor.Visit<TSet, TElement>(ISetPropertyBag<TSet, TElement> properties, ref TSet container)
        {
            if (container is HashSet<TElement> hashSet)
            {
                foreach (var element in hashSet)
                {
                    VisitValue(element);
                }
            }
            else
            {
                foreach (var element in container)
                {
                    VisitValue(element);
                }
            }
        }

        /// <summary>
        /// Invoked by Unity.Properties for each IList based container type.
        /// </summary>
        /// <remarks>
        /// We specialize on well known types to avoid struct enumerator boxing.
        /// </remarks>
        void IListPropertyBagVisitor.Visit<TList, TElement>(IListPropertyBag<TList, TElement> properties, ref TList container)
        {
            if (container is List<TElement> list)
            {
                foreach (var element in list)
                {
                    VisitValue(element);
                }
            }
            else if (container is TElement[] array)
            {
                foreach (var element in array)
                {
                    VisitValue(element);
                }
            }
            else
            {
                foreach (var element in container)
                {
                    VisitValue(element);
                }
            }
        }

        /// <summary>
        /// Invoked by Unity.Properties for each IDictionary based container type.
        /// </summary>
        /// <remarks>
        /// We specialize on well known types to avoid struct enumerator boxing.
        /// </remarks>
        void IDictionaryPropertyBagVisitor.Visit<TDictionary, TKey, TValue>(IDictionaryPropertyBag<TDictionary, TKey, TValue> properties, ref TDictionary container)
        {
            if (container is Dictionary<TKey, TValue> dictionary)
            {
                foreach (var kvp in dictionary)
                {
                    VisitValue(kvp.Key);
                    VisitValue(kvp.Value);
                }
            }
            else
            {
                foreach (var kvp in container)
                {
                    VisitValue(kvp.Key);
                    VisitValue(kvp.Value);
                }
            }
        }

        void VisitValue<TValue>(TValue value)
        {
            if (typeof(TValue) == typeof(BlobAssetReferenceData))
            {
                var blobAssetReferenceData = UnsafeUtility.As<TValue, BlobAssetReferenceData>(ref value);

                if (null != blobAssetReferenceData.m_Ptr)
                {
                    var blobAssetPtr = new BlobAssetPtr(blobAssetReferenceData.Header);

                    if (!m_BlobAssetMap.TryGetValue(blobAssetPtr, out _))
                    {
                        var index = m_BlobAssets.Length;
                        m_BlobAssets.Add(blobAssetPtr);
                        m_BlobAssetMap.Add(blobAssetPtr, index);
                    }
                }

                return;
            }

            if (!TypeTraits<TValue>.IsContainer)
                return;

            if (TypeTraits<TValue>.CanBeNull)
            {
                if (null == value)
                    return;

                if (value is UnityEngine.Object)
                    return;
            }

            if (!TypeTraits<TValue>.IsValueType && typeof(string) != typeof(TValue))
            {
                if (m_References == null)
                    m_References = new HashSet<object>();

                if (!m_References.Add(value))
                    return;
            }

            PropertyContainer.TryAccept(this, ref value, out _);
        }
    }
}
