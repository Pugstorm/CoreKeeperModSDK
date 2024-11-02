using System;
using System.Linq;
using System.Reflection;
using Unity.Collections;

namespace Unity.Entities
{
    public static partial class TypeManager
    {
        static Type[] s_AspectTypes;

        internal static void InitializeAspects(bool ignoreTestAspects = true)
        {
            s_AspectTypes = ignoreTestAspects
                ? GetTypesDerivedFrom(typeof(IAspect)).Where(t => t.Namespace != null
                                                                    && !t.Namespace.Contains("test", StringComparison.InvariantCultureIgnoreCase)
                                                                    && t.GetCustomAttribute<DisableGenerationAttribute>() == null).ToArray()
                : GetTypesDerivedFrom(typeof(IAspect)).Where(t => t.Namespace != null
                                                                    && t.GetCustomAttribute<DisableGenerationAttribute>() == null).ToArray();
        }

        static void ShutdownAspects()
        {
            s_AspectTypes = null;
        }

        /// <summary>
        /// Retrieve all managed <see cref="Type"/> that derives from <see cref="IAspect"/>.
        /// </summary>
        /// <returns>An array of all managed <see cref="Type"/> that derives from <see cref="IAspect"/>.</returns>
        [ExcludeFromBurstCompatTesting("Returns managed types")]
        internal static Type[] GetAllAspectTypes()
        {
            return s_AspectTypes;
        }

        /// <summary>
        /// Retrieve managed <see cref="Type"/> that derives from <see cref="IAspect"/> from its type index.
        /// </summary>
        /// <param name="typeIndex">The type index of the aspect type.</param>
        /// <returns>The managed <see cref="Type"/> that derives from <see cref="IAspect"/>.</returns>
        [ExcludeFromBurstCompatTesting("Returns managed type")]
        internal static Type GetAspectType(int typeIndex)
        {
            if (typeIndex < 0 || typeIndex >= s_AspectTypes.Length)
                return null;

            return s_AspectTypes[typeIndex];
        }

        /// <summary>
        /// Retrieve the type index of the managed <see cref="Type"/> that derives from <see cref="IAspect"/>.
        /// </summary>
        /// <param name="type">The managed <see cref="Type"/> that derives from <see cref="IAspect"/>.</param>
        /// <returns>The type index of the managed <see cref="Type"/> that derives from <see cref="IAspect"/>.</returns>
        [ExcludeFromBurstCompatTesting("Takes managed type")]
        internal static int GetAspectTypeIndex(Type type)
        {
            return type != null ? Array.IndexOf(s_AspectTypes, type) : 0;
        }

        /// <summary>
        /// Retrieve the type index of the managed <see cref="Type"/> that derives from <see cref="IAspect"/>.
        /// </summary>
        /// <typeparam name="T">The managed <see cref="Type"/> that derives from <see cref="IAspect"/>.</typeparam>
        /// <returns>The type index of the managed <see cref="Type"/> that derives from <see cref="IAspect"/>.</returns>
        [ExcludeFromBurstCompatTesting("Takes managed type")]
        internal static int GetAspectTypeIndex<T>() where T : struct, IAspect
        {
            return GetAspectTypeIndex(typeof(T));
        }
    }
}
