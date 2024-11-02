using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEditor;

namespace Unity.Entities.UI
{
    /// <summary>
    /// Maintains a database of all the inspector-related types and allows creation of new instances of inspectors.
    /// </summary>
    static partial class InspectorRegistry
    {
        static class Cache
        {
            [Flags]
            public enum RegistrationStatus
            {
                Registered = 0, /* Unused at the moment */
                MissingDefaultConstructor = 1,
                GenericArgumentsDoNotMatchInspectedType = 1 << 1,
                UnsupportedUserDefinedGenericInspector = 1 << 2,
                UnsupportedPartiallyResolvedGenericInspector = 1 << 3,
                UnsupportedGenericInspectorForNonGenericType = 1 << 4,
                UnsupportedGenericArrayInspector = 1 << 5,
            }

            class RegistrationInfo
            {
                readonly Dictionary<RegistrationStatus, List<Type>> s_UnregisteredTypesPerStatus =
                    new Dictionary<RegistrationStatus, List<Type>>();

                readonly Dictionary<Type, RegistrationStatus> s_StatusPerType =
                    new Dictionary<Type, RegistrationStatus>();

                public void CacheInvalidInspectorType(Type type, RegistrationStatus status)
                {
                    if (s_StatusPerType.TryGetValue(type, out var s))
                        status |= s;
                    s_StatusPerType[type] = status;

                    var data = s_UnregisteredTypesPerStatus;
                    if (!data.TryGetValue(status, out var list))
                    {
                        data[status] = list = new List<Type>();
                    }

                    list.Add(type);
                }

                public RegistrationStatus GetStatus(Type t)
                    => s_StatusPerType.TryGetValue(t, out var status) ? status : RegistrationStatus.Registered;
            }

            public static readonly Dictionary<Type, List<Type>> s_InspectorsPerType;
            public static readonly Dictionary<Type, Type[]> s_GenericArgumentsPerType;
            public static readonly Dictionary<Type, Type[]> s_RootGenericArgumentsPerType;
            static readonly RegistrationInfo s_RegistrationInfo;

            static Cache()
            {
                s_InspectorsPerType = new Dictionary<Type, List<Type>>();
                s_GenericArgumentsPerType = new Dictionary<Type, Type[]>();
                s_RootGenericArgumentsPerType = new Dictionary<Type, Type[]>();
                s_RegistrationInfo = new RegistrationInfo();
                RegisterCustomInspectors(s_RegistrationInfo);
            }

            public static Type[] GetGenericArguments(Type type)
            {
                if (!type.IsGenericType)
                    return Array.Empty<Type>();

                if (!s_GenericArgumentsPerType.TryGetValue(type, out var array))
                    s_GenericArgumentsPerType[type] = array = type.GetGenericTypeDefinition().GetGenericArguments();
                return array;
            }

            static void RegisterCustomInspectors(RegistrationInfo info)
            {
                foreach (var type in TypeCache.GetTypesDerivedFrom(typeof(IInspector<>)))
                {
                    RegisterInspectorType(s_InspectorsPerType, typeof(IInspector<>), type, info);
                }
            }

            static void RegisterInspectorType(IDictionary<Type, List<Type>> typeMap, Type interfaceType,
                Type inspectorType,
                RegistrationInfo info)
            {
                if (!ValidateCustomInspectorRegistration(inspectorType, info))
                    return;

                var inspectorInterface = inspectorType.GetInterface(interfaceType.FullName);
                if (null == inspectorInterface || inspectorType.IsAbstract)
                    return;

                var genericArguments = inspectorInterface.GetGenericArguments();
                var targetType = genericArguments[0];

                // Generic inspector for generic type
                if (inspectorType.ContainsGenericParameters && targetType.IsGenericType)
                    targetType = targetType.GetGenericTypeDefinition();

                if (null == inspectorType.GetConstructor(Array.Empty<Type>()))
                {
                    info.CacheInvalidInspectorType(inspectorType, RegistrationStatus.MissingDefaultConstructor);
                    return;
                }

                if (!typeMap.TryGetValue(targetType, out var list))
                    typeMap[targetType] = list = new List<Type>();

                list.Add(inspectorType);
            }

            static bool ValidateCustomInspectorRegistration(Type type, RegistrationInfo info)
            {
                if (type.IsAbstract)
                    return false;

                if (!type.IsGenericType)
                    return true;

                var inspectedType = type.GetRootType().GetGenericArguments()[0];
                if (inspectedType.IsArray)
                {
                    info.CacheInvalidInspectorType(type, RegistrationStatus.UnsupportedGenericArrayInspector);
                    return false;
                }

                if (!inspectedType.IsGenericType)
                {
                    info.CacheInvalidInspectorType(type,
                        RegistrationStatus.UnsupportedGenericInspectorForNonGenericType);
                    return false;
                }

                if (null == type.GetInterface(nameof(IExperimentalInspector)))
                {
                    info.CacheInvalidInspectorType(type, RegistrationStatus.UnsupportedUserDefinedGenericInspector);
                    return false;
                }

                var rootArguments = inspectedType.GetGenericArguments();
                var arguments = GetGenericArguments(type);

                if (arguments.Length > rootArguments.Length)
                {
                    info.CacheInvalidInspectorType(type, RegistrationStatus.GenericArgumentsDoNotMatchInspectedType);
                    return false;
                }

                var set = new HashSet<Type>(rootArguments);
                foreach (var argument in arguments)
                {
                    set.Remove(argument);
                }

                if (set.Count == 0)
                    return true;

                info.CacheInvalidInspectorType(type, RegistrationStatus.UnsupportedPartiallyResolvedGenericInspector);
                return false;
            }

            internal static RegistrationStatus GetRegistrationStatusForInspectorType(Type t)
            {
                return s_RegistrationInfo.GetStatus(t);
            }
        }
    }
}
