using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.Pool;

namespace Unity.Entities.UI
{
    /// <summary>
    /// Maintains a database of all the inspector-related types and allows creation of new instances of inspectors.
    /// </summary>
    static partial class InspectorRegistry
    {
        /// <summary>
        /// Creates a new instance of a <see cref="Inspector{TValue}"/> that can act as a root inspector.
        /// </summary>
        /// <param name="constraints">Constraints that filter the candidate inspector types.</param>
        /// <typeparam name="TValue">The type of the value</typeparam>
        /// <returns>The inspector instance or null</returns>
        static IInspector<TValue> GetInspector<TValue>(params IInspectorConstraint[] constraints)
        {
            return GetInspectorWithConstraints<TValue>(InspectorConstraint.Combine(InspectorConstraint.AssignableTo<IRootInspector>(), constraints));
        }

        /// <summary>
        /// Creates a new instance of a <see cref="PropertyInspector{TValue,TAttribute}"/> that can act as a property drawer
        /// for a given field.
        /// </summary>
        /// <param name="property"></param>
        /// <param name="constraints">Constraints that filter the candidate property drawer types.</param>
        /// <typeparam name="TValue">The type of the value</typeparam>
        /// <returns>The property drawer instance or null</returns>
        static IInspector<TValue> GetPropertyInspector<TValue>(IProperty property, params IInspectorConstraint[] constraints)
        {
            foreach (var attribute in property.GetAttributes<UnityEngine.PropertyAttribute>() ??
                                            Array.Empty<UnityEngine.PropertyAttribute>())
            {
                var drawer = typeof(IPropertyDrawer<>).MakeGenericType(attribute.GetType());
                var inspector = GetPropertyInspector<TValue>(InspectorConstraint.Combine(InspectorConstraint.AssignableTo(drawer), constraints));
                if (null != inspector)
                {
                    return inspector;
                }
            }

            return GetInspectorWithConstraints<TValue>(InspectorConstraint.Combine(InspectorConstraint.AssignableTo<IPropertyDrawer>(), constraints));
        }

        /// <summary>
        /// Creates a new instance of a <see cref="PropertyInspector{TValue,TAttribute}"/> that can act as a property drawer
        /// for a given field.
        /// </summary>
        /// <param name="property"></param>
        /// <param name="constraints">Constraints that filter the candidate property drawer types.</param>
        /// <typeparam name="TValue">The type of the value</typeparam>
        /// <returns>The property drawer instance or null</returns>
        static IInspector<TValue> GetAttributeInspector<TValue>(IProperty property, params IInspectorConstraint[] constraints)
        {
            foreach (var attribute in property.GetAttributes<UnityEngine.PropertyAttribute>() ??
                                      Array.Empty<UnityEngine.PropertyAttribute>())
            {
                var drawer = typeof(IPropertyDrawer<>).MakeGenericType(attribute.GetType());
                var inspector = GetInspectorWithConstraints<TValue>(InspectorConstraint.Combine(InspectorConstraint.AssignableTo(drawer), constraints));
                if (null != inspector)
                {
                    return inspector;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a new instance of a <see cref="PropertyInspector{TValue,TAttribute}"/> that can act as a property drawer
        /// for a given field.
        /// </summary>
        /// <param name="constraints">Constraints that filter the candidate property drawer types.</param>
        /// <typeparam name="TValue">The type of the value</typeparam>
        /// <typeparam name="TAttribute">The attribute type the value was tagged with</typeparam>
        /// <returns>The property drawer instance or null</returns>
        static PropertyInspector<TValue> GetPropertyInspector<TValue>(params IInspectorConstraint[] constraints)
        {
            return (PropertyInspector<TValue>) GetInspectorWithConstraints<TValue>(InspectorConstraint.Combine(InspectorConstraint.AssignableTo<IPropertyDrawer>(), constraints));
        }

        /// <summary>
        /// Creates an inspector instance that satisfy the constraints.
        /// </summary>
        /// <param name="constraints">Constraints that filter the candidate property drawer types.</param>
        /// <typeparam name="TValue">The type of the value</typeparam>
        /// <returns>An inspector instance of null</returns>
        static InspectorBase<TValue> GetInspectorWithConstraints<TValue>(params IInspectorConstraint[] constraints)
        {
            var valueType = typeof(TValue);
            var genericArguments = valueType.IsGenericType ? valueType.GetGenericArguments() : Array.Empty<Type>();
            var candidates = ListPool<Type>.Get();
            try
            {
                foreach (var type in GetInspectorTypes<TValue>(constraints))
                {
                    var t = type;
                    if (type.IsGenericType)
                    {
                        if (!OrderGenericArguments(genericArguments, type, out var types))
                            continue;

                        if (type.IsGenericTypeDefinition)
                            t = type.MakeGenericType(types);
                    }

                    candidates.Add(t);
                }

                if (candidates.Count == 0)
                    return null;

                Type bestType = null;
                var parameters = int.MaxValue;

                foreach (var type in candidates)
                {
                    if (!type.IsGenericType)
                    {
                        bestType = type;
                        break;
                    }

                    if (null == type.GetInterface(nameof(IExperimentalInspector)))
                        continue;

                    if (type.GetRootType() != typeof(InspectorBase<TValue>))
                        continue;

                    var rootCount = 0;
                    foreach (var argument in GetRootGenericArguments(type)[0].GetGenericArguments())
                    {
                        if (argument.IsGenericParameter)
                            ++rootCount;
                    }

                    var argumentCount = 0;
                    foreach (var argument in Cache.GetGenericArguments(type))
                    {
                        if (argument.IsGenericParameter)
                            ++argumentCount;
                    }

                    if (rootCount != genericArguments.Length)
                        continue;

                    if (argumentCount >= parameters)
                        continue;

                    parameters = argumentCount;
                    bestType = type;
                }

                return null != bestType
                    ? (InspectorBase<TValue>) Activator.CreateInstance(bestType)
                    : null;
            }
            finally
            {
                ListPool<Type>.Release(candidates);
            }
        }

        /// <summary>
        /// Returns all the inspector candidate types that satisfy the constraints.
        /// </summary>
        /// <param name="constraints">Constraints that filter the candidate property drawer types.</param>
        /// <typeparam name="TValue">The type of the value</typeparam>
        /// <returns>The candidate inspector types</returns>
        internal static IEnumerable<Type> GetInspectorTypes<TValue>(params IInspectorConstraint[] constraints)
        {
            var valueType = typeof(TValue);
            var enumerator = GetInspectorTypes(Cache.s_InspectorsPerType, valueType, constraints);
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }

            if (!valueType.IsGenericType)
                yield break;

            enumerator = GetInspectorTypes(Cache.s_InspectorsPerType, valueType.GetGenericTypeDefinition(), constraints);
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }
        }

        static SatisfiesConstraintsEnumerator GetInspectorTypes(Dictionary<Type, List<Type>> lookup, Type type, params IInspectorConstraint[] constraints)
        {
            lookup.TryGetValue(type, out var inspectors);
            return new SatisfiesConstraintsEnumerator(inspectors, constraints);
        }

        static Type[] GetRootGenericArguments(Type type)
        {
            if (!type.IsGenericType)
                return Array.Empty<Type>();

            if (!Cache.s_RootGenericArgumentsPerType.TryGetValue(type, out var array))
                Cache.s_RootGenericArgumentsPerType[type] =
                    array = type.GetGenericTypeDefinition().GetRootType().GetGenericArguments();
            return array;
        }

        static bool OrderGenericArguments(Type[] instanceArguments, Type inspector, out Type[] types)
        {
            var inspectorArguments = inspector.GetGenericArguments();
            var result = new Type[inspectorArguments.Length];

            var root = inspector.GetRootType();
            var rootArguments = root.GetGenericArguments()[0].GetGenericArguments();

            for (var i = 0; i < inspectorArguments.Length; ++i)
            {
                var index = Array.IndexOf(rootArguments, inspectorArguments[i]);
                if (index < 0)
                {
                    types = Array.Empty<Type>();
                    return false;
                }

                result[i] = instanceArguments[index];
            }

            types = result;
            return true;
        }
    }
}
