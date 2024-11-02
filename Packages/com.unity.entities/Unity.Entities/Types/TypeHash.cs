using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    /// <summary>
    /// Produces a stable type hash for a Type based on its layout in memory and how that memory should be interpreted.
    /// </summary>
    /// <remarks>
    /// You can rename field members and this hash isn't affected. However, if a field's type changes,
    /// this hash changes, because the interpretation of the underlying memory will have contextually changed.
    ///
    /// The purpose of the stable type hash is to provide a version linking serialized component data to its runtime
    /// representation.
    /// 
    /// As such, the type hash has a few requirements:
    ///    - **R0:** TypeHashes apply only to types that Unity.Entities serializes. The internals of a UnityEngine.Object reference
    ///      contained in a component mustn't have an effect on the type hash. You can safely change the internals of the
    ///      UnityEngine.Object reference because they're serialized outside of Unity.Entities.
    ///    - **R1:** Types with the same data layout but are different types should have different hashes.
    ///    - **R2:** If a type's data layout changes, so should the type hash. This includes:
    ///      - A nested field's data layout changes (for example, a new member added)
    ///      - FieldOffsets, explicit size, or pack alignment are changed
    ///      - Different types of the same width swap places (for example if a uint swaps with an int)
    ///      - **Note:** Unity can't detect if fields of the same type swap (for example, mInt1 swaps with mInt2) This is a semantic
    ///        difference which you should increase your component [TypeVersion(1)] attribute. You shouldn't
    ///        try to hash field names to handle this, because you can't control all field names.
    ///        Also, field names have no effect serialization and doesn't affect hashes.
    ///    - **R3:** You should version the hash in case the semantics of a type change, but the data layout is unchanged
    ///    - **R4:** DOTS Runtime relies on hashes generated from the Editor (used in serialized data) and hashes generated
    ///      during compilation. These hashes must match. This rule exists because of the following:
    ///      - Tiny swapa out assemblies for 'tiny' versions, which means you should avoid any hashing using the AssemblyName
    ///        or handle it specially for the known swapped assemblies.
    ///        - This means you should avoid Type.AssemblyQualifiedName in hashes, but as well, closed-form
    ///          generic types include the assembly qualified name for GenericArguments in Type.FullName which
    ///          causes issues. For example, `typeof(ComponentWithGeneric.GenericField).FullName ==
    ///          Unity.Entities.ComponentWithGeneric.GenericField`1[[System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]`
    ///      - System.Reflection and Mono.Cecil provide different Type names (they use a different format).
    ///        Generating hashes from System.Reflection to match hashes using Mono.Cecil must account for this difference.
    /// </remarks>
    public class TypeHash
    {
        // http://www.isthe.com/chongo/src/fnv/hash_64a.c
        // with basis and prime:
        const ulong kFNV1A64OffsetBasis = 14695981039346656037;
        const ulong kFNV1A64Prime = 1099511628211;

        /// <summary>
        /// Generates a FNV1A64 hash.
        /// </summary>
        /// <param name="text">Text to hash.</param>
        /// <returns>Hash of input string.</returns>
        public static ulong FNV1A64(string text)
        {
            ulong result = kFNV1A64OffsetBasis;
            foreach (var c in text)
            {
                result = kFNV1A64Prime * (result ^ (byte)(c & 255));
                result = kFNV1A64Prime * (result ^ (byte)(c >> 8));
            }
            return result;
        }

        /// <summary>
        /// Generates a FNV1A64 hash.
        /// </summary>
        /// <param name="text">Text to hash.</param>
        /// <typeparam name="T">Unmanaged IUTF8 type.</typeparam>
        /// <returns>Hash of input string.</returns>
        public static ulong FNV1A64<T>(T text)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            ulong result = kFNV1A64OffsetBasis;
            for(int i = 0; i <text.Length; ++i)
            {
                var c = text[i];
                result = kFNV1A64Prime * (result ^ (byte)(c & 255));
                result = kFNV1A64Prime * (result ^ (byte)(c >> 8));
            }
            return result;
        }

        /// <summary>
        /// Generates a FNV1A64 hash.
        /// </summary>
        /// <param name="val">Value to hash.</param>
        /// <returns>Hash of input.</returns>
        public static ulong FNV1A64(int val)
        {
            ulong result = kFNV1A64OffsetBasis;
            unchecked
            {
                result = (((ulong)(val & 0x000000FF) >>  0) ^ result) * kFNV1A64Prime;
                result = (((ulong)(val & 0x0000FF00) >>  8) ^ result) * kFNV1A64Prime;
                result = (((ulong)(val & 0x00FF0000) >> 16) ^ result) * kFNV1A64Prime;
                result = (((ulong)(val & 0xFF000000) >> 24) ^ result) * kFNV1A64Prime;
            }

            return result;
        }

        /// <summary>
        /// Combines a FNV1A64 hash with a value.
        /// </summary>
        /// <param name="hash">Input Hash.</param>
        /// <param name="value">Value to add to the hash.</param>
        /// <returns>A combined FNV1A64 hash.</returns>
        public static ulong CombineFNV1A64(ulong hash, ulong value)
        {
            hash ^= value;
            hash *= kFNV1A64Prime;

            return hash;
        }

#if UNITY_2022_3_11F1_OR_NEWER
        private static ulong HashType(Type type, Dictionary<Type, ulong> cache)
        {
            if (cache.TryGetValue(type, out ulong hash))
                return hash;

            hash = HashTypeName(type);
            // If we shouldn't walk the type's fields just return the type name hash.
            // UnityEngine objects have their own serialization mechanism so exclude hashing their internals
            if (type.IsArray || type.IsPointer || type.IsPrimitive || type.IsEnum || (TypeManager.UnityEngineObjectType?.IsAssignableFrom(type) == true))
            {
                cache.Add(type, hash);
                return hash;
            }

            if (type.ContainsGenericParameters)
                throw new ArgumentException($"'{type}' contains open generic parameters. Generic types must have all generic parameters specified to closed types when calculating stable type hashes");

            // Only non-pod and non-unityengine types could possibly have a version attribute
            hash = CombineFNV1A64(hash, HashVersionAttribute(type));
            
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            for(ulong fieldIndex = 0;  fieldIndex < (ulong)fields.Length; fieldIndex++)
            {
                var field = fields[fieldIndex];
                // statics have no effect on data layout
                if (field.IsStatic)
                    continue;

                var fieldType = field.FieldType;
                ulong fieldTypeHash = 0;

                // Managed components can have circular definitions, so when looking at managed fields only hash
                // the name and field index, but do not cache the result -- we still want the hash for managed fields
                // to be calculated from more than just the name since we do handle managed component serialization
                // where field layout is important to identify via the hash alone
                if (fieldType.IsClass)
                    fieldTypeHash = HashTypeName(fieldType);
                else if(!cache.TryGetValue(fieldType, out fieldTypeHash))
                {
                    fieldTypeHash = HashType(fieldType, cache);
                }

                fieldTypeHash = CombineFNV1A64(fieldTypeHash, fieldIndex);

                var offset = field.GetCustomAttribute<FieldOffsetAttribute>();
                if (offset != null)
                    fieldTypeHash = CombineFNV1A64(fieldTypeHash, (ulong)offset.Value);

                hash = CombineFNV1A64(hash, fieldTypeHash);
            }

            // TODO: Enable this. Currently IL2CPP gives totally inconsistent results to Mono.
            /*
            if (type.StructLayoutAttribute != null && !type.StructLayoutAttribute.IsDefaultAttribute())
            {
                var explicitSize = type.StructLayoutAttribute.Size;
                if (explicitSize > 0)
                    hash = CombineFNV1A64(hash, (ulong)explicitSize);

                // Todo: Enable this. We cannot support Pack at the moment since a type's Packing will
                // change based on its field's explicit packing which will fail for Tiny mscorlib
                // as it's not in sync with dotnet
                // var packingSize = type.StructLayoutAttribute.Pack;
                // if (packingSize > 0)
                //     hash = CombineFNV1A64(hash, (ulong)packingSize);
            }
            */

            cache.Add(type, hash);
            return hash;
        }

        private static ulong HashVersionAttribute(Type type)
        {
            int version = 0;

            var versionAttribute = type.GetCustomAttribute<TypeManager.TypeVersionAttribute>(true);
            if (versionAttribute != null)
            {
                version = versionAttribute.TypeVersion;
            }

            return FNV1A64(version);
        }
#else
        private static readonly Type[] WorkaroundTypes = new Type[] { typeof(System.Guid) };

        private static ulong HashType(Type type, Dictionary<Type, ulong> cache)
        {
            var hash = HashTypeName(type);

            // UnityEngine objects have their own serialization mechanism so exclude hashing the type's
            // internals and just hash its name+assemblyname (not fully qualified)
            if (TypeManager.UnityEngineObjectType?.IsAssignableFrom(type) == true)
            {
                return CombineFNV1A64(hash, FNV1A64(type.Assembly.GetName().Name));
            }

            if (type.IsGenericParameter || type.IsArray || type.IsPointer || type.IsPrimitive || type.IsEnum || WorkaroundTypes.Contains(type))
                return hash;

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (!field.IsStatic) // statics have no effect on data layout
                {
                    var fieldType = field.FieldType;

                    if (!cache.TryGetValue(fieldType, out ulong fieldTypeHash))
                    {
                        // Classes can have cyclical type definitions so to prevent a potential stackoverflow
                        // we make all future occurence of fieldType resolve to the hash of its field type name
                        cache.Add(fieldType, HashTypeName(fieldType));
                        fieldTypeHash = HashType(fieldType, cache);
                        cache[fieldType] = fieldTypeHash;
                    }

                    var fieldOffsetAttrs = field.GetCustomAttributes(typeof(FieldOffsetAttribute));
                    if (fieldOffsetAttrs.Any())
                    {
                        var offset = ((FieldOffsetAttribute)fieldOffsetAttrs.First()).Value;
                        hash = CombineFNV1A64(hash, (ulong)offset);
                    }

                    hash = CombineFNV1A64(hash, fieldTypeHash);
                }
            }

            // TODO: Enable this. Currently IL2CPP gives totally inconsistent results to Mono.
            /*
            if (type.StructLayoutAttribute != null && !type.StructLayoutAttribute.IsDefaultAttribute())
            {
                var explicitSize = type.StructLayoutAttribute.Size;
                if (explicitSize > 0)
                    hash = CombineFNV1A64(hash, (ulong)explicitSize);
                // Todo: Enable this. We cannot support Pack at the moment since a type's Packing will
                // change based on its field's explicit packing which will fail for Tiny mscorlib
                // as it's not in sync with dotnet
                // var packingSize = type.StructLayoutAttribute.Pack;
                // if (packingSize > 0)
                //     hash = CombineFNV1A64(hash, (ulong)packingSize);
            }
            */

            return hash;
        }
        private static ulong HashVersionAttribute(Type type, IEnumerable<CustomAttributeData> customAttributes = null)
        {
            int version = 0;

            customAttributes = customAttributes ?? type.CustomAttributes;
            if (customAttributes.Any())
            {
                var versionAttribute = customAttributes.FirstOrDefault(ca => ca.Constructor.DeclaringType == typeof(TypeManager.TypeVersionAttribute));
                if (versionAttribute != null)
                {
                    version = (int)versionAttribute.ConstructorArguments
                        .First(arg => arg.ArgumentType.Name == "Int32")
                        .Value;
                }
            }

            return FNV1A64(version);
        }
#endif


        private static ulong HashNamespace(Type type)
        {
            var hash = kFNV1A64OffsetBasis;

            // System.Reflection and Cecil don't report namespaces the same way so do an alternative:
            // Find the namespace of an un-nested parent type, then hash each of the nested children names
            if (type.IsNested)
            {
                hash = CombineFNV1A64(hash, HashNamespace(type.DeclaringType));
                hash = CombineFNV1A64(hash, FNV1A64(type.DeclaringType.Name));
            }
            else if (!string.IsNullOrEmpty(type.Namespace))
                hash = CombineFNV1A64(hash, FNV1A64(type.Namespace));

            return hash;
        }

        private static ulong HashTypeName(Type type)
        {
            ulong hash = HashNamespace(type);
            hash = CombineFNV1A64(hash, FNV1A64(type.Name));
#if UNITY_2022_3_11F1_OR_NEWER
            hash = CombineFNV1A64(hash, FNV1A64(type.Assembly.GetName().Name));
#endif
            foreach (var ga in type.GenericTypeArguments)
            {
                Assert.IsTrue(!ga.IsGenericParameter);
                hash = CombineFNV1A64(hash, HashTypeName(ga));
            }

            return hash;
        }


        /// <summary>
        /// Calculates a stable type hash for the input type.
        /// </summary>
        /// <param name="type">Type to hash.</param>
        /// <param name="customAttributes">Custom attributes for the provided type (if any).</param>
        /// <param name="hashCache">Cache for Types and their hashes. Used for quicker lookups when hashing.</param>
        /// <returns>StableTypeHash for the input type.</returns>
        public static ulong CalculateStableTypeHash(Type type, IEnumerable<CustomAttributeData> customAttributes = null, Dictionary<Type, ulong> hashCache = null)
        {
            // This API should not be called anymore but we can't deprecate it in a minor release so we forward to a new one
#if UNITY_2022_3_11F1_OR_NEWER
            if (hashCache == null)
                hashCache = new Dictionary<Type, ulong>();

            return CalculateStableTypeHash(type, hashCache);
#else
            ulong versionHash = HashVersionAttribute(type, customAttributes);

            if (hashCache == null)
                hashCache = new Dictionary<Type, ulong>();

            ulong typeHash = HashType(type, hashCache);


            return CombineFNV1A64(versionHash, typeHash);
#endif
        }

#if UNITY_2022_3_11F1_OR_NEWER
        /// <summary>
        /// Calculates a stable type hash for the input type.
        /// </summary>
        /// <param name="type">Type to hash.</param>
        /// <param name="customAttributes">Custom attributes for the provided type (if any).</param>
        /// <param name="hashCache">Cache for Types and their hashes. Used for quicker lookups when hashing.</param>
        /// <returns>StableTypeHash for the input type.</returns>
        public static ulong CalculateStableTypeHash(Type type, Dictionary<Type, ulong> hashCache)
        {
            Assert.IsNotNull(hashCache, "The cache used for calculating type hashes must not be null");
            ulong hash = HashType(type, hashCache);
            return hash;
        }
#endif

        /// <summary>
        /// Calculates a MemoryOrdering for the input type.
        /// </summary>
        /// <param name="type">Type to inspect.</param>
        /// <param name="hasCustomMemoryOrder">Out param; set to true if the memory order has been explicitly overriden for the input type.</param>
        /// <param name="hashCache">Cache for Types and their hashes. Used for quicker lookups when hashing.</param>
        /// <returns>MemoryOrdering for the input type.</returns>
        public static ulong CalculateMemoryOrdering(Type type, out bool hasCustomMemoryOrder, Dictionary<Type, ulong> hashCache = null)
        {
            hasCustomMemoryOrder = false;

            if (type == null || type.FullName == "Unity.Entities.Entity")
            {
                return 0;
            }

            var customAttributes = type.CustomAttributes;
            if (customAttributes.Any())
            {
                var forcedMemoryOrderAttribute = customAttributes.FirstOrDefault(ca => ca.Constructor.DeclaringType == typeof(TypeManager.ForcedMemoryOrderingAttribute));
                if (forcedMemoryOrderAttribute != null)
                {
                    hasCustomMemoryOrder = true;
                    ulong memoryOrder = (ulong)forcedMemoryOrderAttribute.ConstructorArguments
                        .First(arg => arg.ArgumentType.Name == "UInt64" || arg.ArgumentType.Name == "ulong")
                        .Value;

                    return memoryOrder;
                }
            }

            return CalculateStableTypeHash(type, customAttributes, hashCache);
        }
    }
}
