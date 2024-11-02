#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Entities
{
    // TODO: Support IBufferElementData

    /// <summary>
    /// Builds a static registry of bindings between a runtime field with an authoring field. Multiple different runtime
    /// fields can be associated with a same authoring field.
    /// Only primitive types of int, bool, and float, in addition to Unity.Mathematics variants of these primitives
    /// (e.g. int2, float4) will be added to the BindingRegistry. Other types will be silently ignored.
    /// </summary>
    [InitializeOnLoad]
    public static class BindingRegistry
    {
        /// <summary>
        /// Properties of a runtime field.
        /// </summary>
        public struct RuntimeFieldProperties
        {
            public int FieldOffset;
            public int FieldSize;
            public Type FieldType;
        }

        /// <summary>
        /// Binds a runtime field property to an authoring field name
        /// </summary>
        public struct ReverseBinding
        {
            public string AuthoringFieldName;
            public TypeIndex ComponentTypeIndex;
            public RuntimeFieldProperties FieldProperties;
        }

        /// <summary>
        /// Maps a runtime (ComponentData) field type and name to an authoring (MonoBehaviour) field type and name.
        /// </summary>
        /// <remarks>
        /// An authoring field can map to several runtime fields. But a runtime field only reads from one authoring field.
        /// </remarks>
        static Dictionary<(Type, string), (Type, string)> s_RuntimeToAuthoringFieldMap =
            new Dictionary<(Type, string), (Type, string)>();

        /// <summary>
        /// Keeps a set of the names of all the fields that are bound for a runtime type.
        /// </summary>
        /// <remarks>
        /// A runtime type can be partially bound.
        /// </remarks>
        static Dictionary<Type, HashSet<string>> s_RuntimeFieldNames =
            new Dictionary<Type, HashSet<string>>();

        /// <summary>
        /// Field offset and size associated with the type and name of a runtime ComponentData.
        /// </summary>
        static Dictionary<(Type, string), RuntimeFieldProperties> s_RuntimeFieldProperties =
            new Dictionary<(Type, string), RuntimeFieldProperties>();

        /// <summary>
        /// Maps an association of runtime property fields with their authoring field name to an Authoring Type to be able to support live properties in the Editor
        /// </summary>
        internal static Dictionary<Type, List<ReverseBinding>> s_AuthoringToRuntimeBinding = new Dictionary<Type, List<ReverseBinding>>();

        static BindingRegistry() =>
            Initialize();

        static void Initialize()
        {
            // Function is called at DomainReload

            // Make sure the TypeManager is initialized
            TypeManager.Initialize();

            // Clear reverse binding mapping to clean old fields
            s_AuthoringToRuntimeBinding.Clear();

            // Gather all registered runtime fields
            var registeredFields = TypeCache.GetFieldsWithAttribute<RegisterBindingAttribute>();
            foreach (var field in registeredFields)
            {

                // We need to do that because an authoring field can be bound to several runtime fields.
                var attrs = field.GetCustomAttributes<RegisterBindingAttribute>();
                foreach (var attr in attrs)
                {
                    var authoringField = field.Name;
                    if (!string.IsNullOrEmpty(attr.AuthoringField))
                        authoringField += $".{attr.AuthoringField}";

                    if (!BindingRegistryUtility.TryGetBindingPaths(field.DeclaringType, authoringField, out var authoringPaths))
                        continue;

                    if (field.IsPrivate && !field.GetCustomAttributes<SerializeField>().Any())
                        continue;

                    //fragile: assumption that the only generated support types are bool/int/float 2/3/4
                    if (!BindingRegistryUtility.TryGetBindingPaths(attr.ComponentType, attr.ComponentField, out var runtimePaths))
                        continue;

                    if (attr.Generated)
                    {
                        authoringField += attr.ComponentField.Substring(attr.ComponentField.IndexOf('.'));
                        Register(attr.ComponentType, runtimePaths[0], field.DeclaringType, authoringField);
                    }
                    // Mapping an authoring field with sub components to a runtime field with sub components.
                    else
                    {
                        var numberOfBindings = math.min(authoringPaths.Length, runtimePaths.Length);
                        for (int i = 0; i < numberOfBindings; ++i)
                        {
                            Register(
                                attr.ComponentType, runtimePaths[i],
                                field.DeclaringType, authoringPaths[i]);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Register binding of a runtime field with an authoring field
        /// </summary>
        /// <param name="runtimeComponent">Type of the runtime component. Must implement IComponentData.</param>
        /// <param name="runtimeField">Name of the runtime field.</param>
        /// <param name="authoringComponent">Type of the authoring component. Must derive from UnityEngine.Component.</param>
        /// <param name="authoringField">Name of the authoring field.</param>
        /// <exception cref="InvalidOperationException">Thrown if registering the same runtime field more than once.</exception>
        public static void Register(Type runtimeComponent, string runtimeField, Type authoringComponent,
            string authoringField)
        {
            Assert.IsTrue(typeof(Component).IsAssignableFrom(authoringComponent));
            Assert.IsTrue(typeof(IComponentData).IsAssignableFrom(runtimeComponent));

            var typeIndex = TypeManager.GetTypeIndex(runtimeComponent);
            Assert.IsTrue(typeIndex != 1);

            (Type, string) runtimeKey = (runtimeComponent, runtimeField);
            (Type, string) authoringValue = (authoringComponent, authoringField);

            // TODO: Validation checks on authoringComponent type and authoringField
            // - Check if field is public, serialized and not decorated with a NotKeyable attribute
            // - Are validation checks even necessary? If these are not animatable field there's no way they could be put in the
            //   rig definition. However we risk having users declare stuff that just isn't valid and they would not know about it.
            // - Would be great to have an API such as AnimationUtility.GetAnimatableBindings(ComponentType) without the
            //   need to instantiate a GameObject. We don't care about the path, only animatable fields of Component type.
            //   Having this would minimize the amount of reflection we'd have to do in C# altogether for validation.

            bool extractedProperties = ExtractFieldProperties(runtimeKey, out var fieldProps);
            if (!extractedProperties) // Should we keep continuing?
                Debug.LogError($"No compatible binding for {authoringComponent}. ('{runtimeComponent}'.'{runtimeField}' => {authoringField}");

            if (!s_RuntimeFieldProperties.ContainsKey(runtimeKey))
            {
                if (extractedProperties)
                    s_RuntimeFieldProperties.Add(runtimeKey, fieldProps);
            }

            var isValueType = runtimeKey.Item1.IsValueType;
            if (!isValueType)
                Debug.LogWarning(
                    $"Field [{runtimeKey.Item2}] part of runtime component type [{runtimeKey.Item1}] could not be found.");
            else
            {
                if (!s_RuntimeToAuthoringFieldMap.TryGetValue(runtimeKey, out var registeredAuthoring))
                {
                    s_RuntimeToAuthoringFieldMap.Add(runtimeKey, authoringValue);
                }
                else
                {
                    if (registeredAuthoring != authoringValue)
                        throw new InvalidOperationException(
                            $"Field [{runtimeKey.Item2}] part of runtime component type [{runtimeKey.Item1}] is already bound to field [{registeredAuthoring.Item2}] of authoring component type [{registeredAuthoring.Item1}] ");
                }

                if (s_RuntimeFieldNames.TryGetValue(runtimeComponent, out var fieldNames))
                    fieldNames.Add(runtimeField);
                else
                    s_RuntimeFieldNames[runtimeComponent] = new HashSet<string>() {runtimeField};
            }

            if (!s_AuthoringToRuntimeBinding.TryGetValue(authoringComponent, out var lookup))
            {
                if (extractedProperties)
                    s_AuthoringToRuntimeBinding[authoringComponent] = lookup = new List<ReverseBinding>();
            }

            if (!extractedProperties)
                return;

            if (authoringComponent == typeof(Transform) || !lookup.Any(x => x.AuthoringFieldName == authoringField))
            {
                lookup.Add(new ReverseBinding() {
                    AuthoringFieldName = authoringField,
                    ComponentTypeIndex = typeIndex,
                    FieldProperties = fieldProps
                });
            }
        }

        /// <summary>
        /// Checks if a runtime type has any authoring bindings.
        /// </summary>
        /// <param name="componentType">Type of the runtime component. Must implement IComponentData.</param>
        /// <returns>Returns true if component type is registered. False otherwise.</returns>
        public static bool HasBindings(Type componentType)
        {
            if (!typeof(IComponentData).IsAssignableFrom(componentType))
                return false;

            return s_RuntimeFieldNames.ContainsKey(componentType);
        }

        /// <summary>
        /// Return authoring binding for a given runtime type and field name.
        /// </summary>
        /// <param name="componentType">Type of the runtime component. Must implement IComponentData.</param>
        /// <param name="fieldName">Field name.</param>
        /// <returns>Returns a runtime type and a field name.</returns>
        public static (Type Type, string FieldName) GetBinding(Type type, string fieldName)
        {
            Assert.IsTrue(typeof(IComponentData).IsAssignableFrom(type));
            if (s_RuntimeToAuthoringFieldMap.TryGetValue((type, fieldName), out var authoringValue))
                return authoringValue;

            return (null, string.Empty);
        }

        /// <summary>
        /// Returns all associated runtime bindings for a given authoring type.
        /// </summary>
        /// <param name="authoringType">Type of the authoring component. Must derive from UnityEngine.Component.</param>
        /// <returns>Returns a list of ReverseBinding.</returns>
        public static List<ReverseBinding> GetReverseBindings(Type authoringType)
        {
            Assert.IsTrue(typeof(Component).IsAssignableFrom(authoringType));

            if (s_AuthoringToRuntimeBinding.TryGetValue(authoringType, out var reverseBindings))
                return reverseBindings;
            return null;
        }

        /// <summary>
        /// Gets all registered fields of a runtime type.
        /// </summary>
        /// <param name="type">Type of the runtime component. Must implement IComponentData.</param>
        /// <returns>Returns a HashSet of field names.</returns>
        public static HashSet<string> GetFields(Type type)
        {
            Assert.IsTrue(typeof(IComponentData).IsAssignableFrom(type));

            s_RuntimeFieldNames.TryGetValue(type, out var fieldSet);

            return fieldSet;
        }

        /// <summary>
        /// Gets the runtime field size in bytes.
        /// </summary>
        /// <param name="type">Type of the runtime component. Must implement IComponentData.</param>
        /// <param name="fieldName">Runtime field.</param>
        /// <returns>Returns the number of bytes of the runtime field.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the runtime field was not registered.</exception>
        public static int GetFieldSize(Type type, string fieldName)
        {
            Assert.IsTrue(typeof(IComponentData).IsAssignableFrom(type));
            (Type, string) key = (type, fieldName);
            if (s_RuntimeFieldProperties.TryGetValue(key, out var fieldData))
                return fieldData.FieldSize;

            throw new InvalidOperationException(
                $"Field [{fieldName}] from component type [{type}] has not been registered.");
        }

        /// <summary>
        /// Gets the runtime field offset in bytes
        /// </summary>
        /// <param name="type">Type of the runtime component. Must implement IComponentData.</param>
        /// <param name="fieldName">Runtime field.</param>
        /// <returns>Returns the offset in bytes of the runtime field.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the runtime field was not registered.</exception>
        public static int GetFieldOffset(Type type, string fieldName)
        {
            Assert.IsTrue(typeof(IComponentData).IsAssignableFrom(type));
            (Type, string) key = (type, fieldName);
            if (s_RuntimeFieldProperties.TryGetValue(key, out var fieldData))
                return fieldData.FieldOffset;

            throw new InvalidOperationException(
                $"Field [{fieldName}] from component type [{type}] has not been registered.");
        }

        static bool ExtractFieldProperties((Type, string) field, out RuntimeFieldProperties data)
        {
            data = default;

            Type type = field.Item1;

            if (!type.IsValueType)
                return false;

            foreach (var name in field.Item2.Split('.'))
            {
                var fieldInfo = type.GetField(name, BindingRegistryUtility.FieldFlags);
                if (fieldInfo == null)
                    return false;

                data.FieldOffset += UnsafeUtility.GetFieldOffset(fieldInfo);
                type = fieldInfo.FieldType;
            }

            data.FieldSize = UnsafeUtility.SizeOf(type);
            data.FieldType = type;

            return true;
        }
    }

    /// <summary>
    /// Utility class for BindingRegistry.
    /// </summary>
    public static class BindingRegistryUtility
    {
        internal const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static readonly string[] k_XYZWComponents4D = { "x", "y", "z", "w" };
        static readonly string[] k_XYZWComponents3D = k_XYZWComponents4D.Take(3).ToArray();
        static readonly string[] k_XYZWComponents2D = k_XYZWComponents4D.Take(2).ToArray();

        static readonly string[] k_RGBAComponents = { "r", "g", "b", "a" };

        static readonly string[] k_QuaternionComponents = { "value.x", "value.y", "value.z", "value.w" };

        static readonly HashSet<Type> k_SupportedUnmanagedTypes = new HashSet<Type>
        {
            typeof(int),
            typeof(float),
            typeof(bool)
        };

        static readonly Dictionary<Type, string[]> k_SupportedVectorTypes = new Dictionary<Type, string[]>()
        {
            {typeof(float2), k_XYZWComponents2D},
            {typeof(int2), k_XYZWComponents2D},
            {typeof(bool2), k_XYZWComponents2D},
            {typeof(Vector2), k_XYZWComponents2D},
            {typeof(Vector2Int), k_XYZWComponents2D},
            {typeof(float3), k_XYZWComponents3D},
            {typeof(int3), k_XYZWComponents3D},
            {typeof(bool3), k_XYZWComponents3D},
            {typeof(Vector3), k_XYZWComponents3D},
            {typeof(Vector3Int), k_XYZWComponents3D},
            {typeof(float4), k_XYZWComponents4D},
            {typeof(int4), k_XYZWComponents4D},
            {typeof(bool4), k_XYZWComponents4D},
            {typeof(Vector4), k_XYZWComponents4D},
            {typeof(quaternion), k_QuaternionComponents},
            {typeof(Quaternion), k_XYZWComponents4D},
            {typeof(Color), k_RGBAComponents}
        };

        /// <summary>
        /// Retrieves individual property paths from a given component type.
        /// For vector type fields like float4 or int4, this will retrieve all sub components.
        /// </summary>
        /// <param name="type">Type of authoring or runtime component.</param>
        /// <param name="fieldName">Field name.</param>
        /// <param name="resultingPaths">Array of supported fields for <paramref name="fieldName"/>.</param>
        /// <returns>Returns true if specified field property is supported by the BindingRegistry. False otherwise.</returns>
        public static bool TryGetBindingPaths(Type type, string fieldName, out string[] resultingPaths)
        {
            var field = ExtractFieldInfoFromPath(type, fieldName);
            if (field == null)
            {
                resultingPaths = null;
                return false;
            }

            if (field.FieldType.IsEnum)
            {
                resultingPaths = new[] { fieldName };
                return true;
            }

            if (k_SupportedUnmanagedTypes.Contains(field.FieldType))
            {
                resultingPaths = new[] { fieldName };
                return true;
            }

            if (k_SupportedVectorTypes.TryGetValue(field.FieldType, out var components))
            {
                resultingPaths = components.Select(component => $"{fieldName}.{component}").ToArray();
                return true;
            }

            resultingPaths = null;
            return false;
        }

        static FieldInfo ExtractFieldInfoFromPath(Type type, string path)
        {
            FieldInfo fieldInfo = null;

            foreach (var fieldName in path.Split ("."))
            {
                // Parse fields and inherited fields.
                var typeIter = type;
                while (typeIter != null)
                {
                    fieldInfo = typeIter.GetField(fieldName, FieldFlags);
                    if (fieldInfo != null)
                        break;
                    typeIter = typeIter.BaseType;
                }

                if (fieldInfo == null)
                    break;

                type = fieldInfo.FieldType;
            }

            return fieldInfo;
        }
    }
}

#endif
