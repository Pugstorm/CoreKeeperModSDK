using System;
using System.Reflection;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Scripting;

namespace Unity.Entities
{
    /// <summary>
    /// Variants of EntityQuery methods that support managed components
    /// </summary>
    public static class EntityQueryExtensionsForComponentArray
    {
        /// <summary>
        /// Gather values of a component from all entities that match a query into a managed array.
        /// </summary>
        /// <param name="query">The query whose entities should have their <typeparamref name="T"/> values gathered.</param>
        /// <typeparam name="T">The managed component type to gather</typeparam>
        /// <returns>A managed array of <typeparamref name="T"/> values for all entities that match the query.</returns>
        public unsafe static T[] ToComponentArray<T>(this EntityQuery query) where T: class
        {
            var entities = query.ToEntityArray(Allocator.Temp);
            int entityCount = entities.Length;
            var arr = new T[entities.Length];
            var eda = *(query._GetImpl()->_Access);
            var componentType = ComponentType.ReadOnly<T>();
            for (int i = 0; i < entityCount; ++i)
            {
                arr[i] = eda.GetComponentObject<T>(entities[i], componentType);
            }
            return arr;
        }
    }
}
