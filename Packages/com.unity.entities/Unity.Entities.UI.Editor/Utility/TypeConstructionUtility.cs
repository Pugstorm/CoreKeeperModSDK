using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;

namespace Unity.Entities.UI
{
    /// <summary>
    /// Utility class around <see cref="System.Type"/>.
    /// </summary>
    static class TypeConstructionUtility
    {
        /// <summary>
        /// Returns a list of all the constructable types from the <see cref="TType"/> type.
        /// </summary>
        /// <typeparam name="TType">The type to query.</typeparam>
        /// <returns>A list of all the constructable types from the <see cref="TType"/> type.</returns>
        public static IEnumerable<Type> GetAllConstructableTypes<TType>()
        {
            return GetConstructableTypes<TType>();
        }

        /// <summary>
        /// Adds all the constructable types from the <see cref="TType"/> type to the given list.
        /// </summary>
        /// <param name="result">List to contain the results.</param>
        /// <typeparam name="TType">The type to query.</typeparam>
        public static void GetAllConstructableTypes<TType>(List<Type> result)
        {
            result.AddRange(GetConstructableTypes<TType>());
        }

        /// <summary>
        /// Returns a list of all the constructable types from the provided type.
        /// </summary>
        /// /// <param name="type">The type to query.</param>
        /// <returns>A list of all the constructable types from the provided type.</returns>
        public static IEnumerable<Type> GetAllConstructableTypes(Type type)
        {
            return GetConstructableTypes(type);
        }

        /// <summary>
        /// Adds all the constructable types from the provided type to the given list.
        /// </summary>
        /// <param name="type">The type to query.</param>
        /// <param name="result">List to contain the results.</param>
        public static void GetAllConstructableTypes(Type type, List<Type> result)
        {
            result.AddRange(GetConstructableTypes(type));
        }

        /// <summary>
        /// Returns <see langword="true"/> if type <see cref="T"/> is constructable from any of its derived types.
        /// </summary>
        /// <remarks>
        /// Constructable is defined as either having a default or implicit constructor or having a registered construction method.
        /// </remarks>
        /// <typeparam name="T">The type to query.</typeparam>
        /// <returns><see langword="true"/> if type <see cref="T"/> is constructable from any of its derived types.</returns>
        public static bool CanBeConstructedFromDerivedType<T>()
        {
            return GetConstructableTypes<T>().Any(type => type != typeof(T));
        }

        static IEnumerable<Type> GetConstructableTypes<TType>()
        {
            if (TypeUtility.CanBeInstantiated<TType>())
            {
                yield return typeof(TType);
            }

            foreach (var type in UnityEditor.TypeCache.GetTypesDerivedFrom<TType>().Where(TypeUtility.CanBeInstantiated))
            {
                yield return type;
            }
        }

        static IEnumerable<Type> GetConstructableTypes(Type type)
        {
            if (TypeUtility.CanBeInstantiated(type))
            {
                yield return type;
            }

            foreach (var t in UnityEditor.TypeCache.GetTypesDerivedFrom(type).Where(TypeUtility.CanBeInstantiated))
            {
                yield return t;
            }
        }
    }
}
