#if UNITY_DOTSRUNTIME
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Mono.Cecil;
using Unity.Cecil.Awesome;

namespace Unity.Entities.CodeGen
{
    public class TypeHash
    {
        // http://www.isthe.com/chongo/src/fnv/hash_64a.c
        // with basis and prime:
        const ulong kFNV1A64OffsetBasis = 14695981039346656037;
        const ulong kFNV1A64Prime = 1099511628211;

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

        public static ulong CombineFNV1A64(ulong hash, params ulong[] values)
        {
            foreach (var value in values)
            {
                hash ^= value;
                hash *= kFNV1A64Prime;
            }

            return hash;
        }

        // Todo: Remove this. DOTS Runtime currently doesn't conform to these system types so don't inspect their fields
        static readonly string[] WorkaroundTypeNames = new string[] { "System.Guid" };

        private static ulong HashType(TypeReference typeRef, Dictionary<TypeReference, ulong> cache)
        {
            var hash = HashTypeName(typeRef);
            var typeResolver = TypeResolver.For(typeRef);
            var typeDef = typeRef.Resolve();

            // UnityEngine objects have their own serialization mechanism so exclude hashing the type's
            // internals and just hash its name which is stable and important to how Entities will serialize
            if (typeRef.IsArray || typeRef.IsGenericParameter || typeRef.IsPointer || typeDef.IsPrimitive || typeDef.IsEnum || typeDef.IsUnityEngineObject() || WorkaroundTypeNames.Contains(typeRef.FullName))
                return hash;

            foreach (var field in typeDef.Fields)
            {
                if (!field.IsStatic) // statics have no effect on data layout
                {
                    var fieldTypeRef = typeResolver.ResolveFieldType(field);

                    // Classes can have cyclical type definitions so prevent recursion if we've seen the type already
                    if (!cache.TryGetValue(fieldTypeRef, out ulong fieldTypeHash))
                    {
                        // Classes can have cyclical type definitions so to prevent a potential stackoverflow
                        // we make all future occurence of fieldType resolve to the hash of its field type name
                        cache.Add(fieldTypeRef, HashTypeName(fieldTypeRef));
                        fieldTypeHash = HashType(fieldTypeRef, cache);
                        cache[fieldTypeRef] =  fieldTypeHash;
                    }

                    if (field.HasLayoutInfo)
                    {
                        var offset = field.Offset;
                        hash = CombineFNV1A64(hash, (ulong)offset);
                    }
                    hash = CombineFNV1A64(hash, fieldTypeHash);
                }
            }

            // TODO: Enable this. Currently IL2CPP gives totally inconsistent results to Mono.
            /*
            if (typeDef.HasLayoutInfo)
            {
                var explicitSize = typeDef.ClassSize;
                if (explicitSize > 0)
                    hash = CombineFNV1A64(hash, (ulong)explicitSize);

                // Todo: Enable this. We cannot support Pack at the moment since a type's Packing will
                // change based on its field's explicit packing which will fail for Tiny mscorlib
                // as it's not in sync with dotnet
                //var packingSize = typeDef.PackingSize;
                //if (packingSize > 0)
                //    hash = CombineFNV1A64(hash, (ulong)packingSize);
            }
            */

            return hash;
        }

        public static ulong HashVersionAttribute(TypeReference typeRef)
        {
            int version = 0;
            var typeDef = typeRef.Resolve();
            if (typeDef.CustomAttributes.Count > 0)
            {
                var versionAttribute = typeDef.CustomAttributes.FirstOrDefault(ca => ca.Constructor.DeclaringType.Name == nameof(TypeManager.TypeVersionAttribute));
                if (versionAttribute != null)
                {
                    version = (int)versionAttribute.ConstructorArguments
                        .First(arg => arg.Type.MetadataType == MetadataType.Int32)
                        .Value;
                }
            }

            return FNV1A64(version);
        }

        private static ulong HashNamespace(TypeReference typeRef)
        {
            var hash = kFNV1A64OffsetBasis;

            // System.Reflection and Cecil don't report namespaces the same way so do an alternative:
            // Find the namespace of an un-nested parent type, then hash each of the nested children names
            if (typeRef.IsNested)
            {
                hash = CombineFNV1A64(hash, HashNamespace(typeRef.DeclaringType));
                hash = CombineFNV1A64(hash, FNV1A64(typeRef.DeclaringType.Name));
            }
            else if (!string.IsNullOrEmpty(typeRef.Namespace))
                hash = CombineFNV1A64(hash, FNV1A64(typeRef.Namespace));

            return hash;
        }

        private static ulong HashTypeName(TypeReference typeRef)
        {
            ulong hash = HashNamespace(typeRef);
            hash = CombineFNV1A64(hash, FNV1A64(typeRef.Name));
            if (typeRef.IsGenericInstance)
            {
                var gi = (GenericInstanceType)typeRef;
                foreach (var ga in gi.GenericArguments)
                {
                    if (ga.IsGenericParameter)
                        throw new ArgumentException($"Found GenericParameter in {typeRef.FullName}. This is not supported. Please correct your type to fully qualify its GenericArguments");
                    hash = CombineFNV1A64(hash, HashTypeName(ga));
                }
            }

            return hash;
        }

        struct TypeReferenceComparer : IEqualityComparer<TypeReference>
        {
            public bool Equals(TypeReference x, TypeReference y)
            {
                return x.FullName.Equals(y.FullName);
            }

            public int GetHashCode(TypeReference obj)
            {
                return obj.FullName.GetHashCode();
            }
        }

        // Our StableTypeHashes purpose is to provide a version linking serialized component data to its runtime representation
        // As such the type hash has a few requirements:
        // R0 - TypeHashes apply to types serialized by Unity.Entities. The internals of a UnityEngine.Object reference
        //      contained in a component must not have an effect on the type hash as the internals of the
        //      UnityEngine.Object reference are fine to change safely since they are serialized outside of Unity.Entities
        // R1 - Types with the same data layout, but are still different types, should have different hashes
        // R2 - If a type's data layout changes, so should the type hash. This includes:
        //      - Nested field's data layout changes (e.g. new member added)
        //      - FieldOffsets, explicit size, or pack alignment are changed
        //      - Different types of the same width swap places (e.g. uint to int)
        //      - NOTE: we cannot detect if fields of the same type swap (e.g. mInt1 to mInt2) This would be a semantic
        //        difference which the user should increase their component [TypeVersion(1)] attribute. We explicitly do
        //        not want to try to handle this case by hashing field names, as users do not control all field names,
        //        and field names have no effect on how we serialize and should not effect our hashes.
        // R3 - The hash should be user versioned should the semantics of a type change, but the data layout is unchanged
        // R4 - DOTS Runtime will rely on hashes generated from the editor (used in serialized data) and hashes generated
        //      during compilation. These hashes must match. This rule exists due to the non-obvious gotchas:
        //      - DOTS Runtime will swap out assemblies for 'tiny' versions, this means any hashing using the AssemblyName
        //        should be avoided or handled specially for the known swapped assemblies.
        //        - Of course this means Type.AssemblyQualifiedName should be avoided in hashes, but as well, closed-form
        //          generic types will include the assembly qualified name for GenericArguments in Type.FullName which will
        //          cause issues. e.g. typeof(ComponentWithGeneric.GenericField).FullName ==
        //          Unity.Entities.ComponentWithGeneric.GenericField`1[[System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]
        //      - System.Reflection and Mono.Cecil will provide different Type names (they use a different format).
        //        Generating hashes from System.Reflection and to match hashes using Mono.Cecil must account for this difference
        public static ulong CalculateStableTypeHash(TypeReference typeRef)
        {
            ulong versionHash = HashVersionAttribute(typeRef);
            ulong typeHash = HashType(typeRef, new Dictionary<TypeReference, ulong>(new TypeReferenceComparer()));

            return CombineFNV1A64(versionHash, typeHash);
        }

        public static ulong CalculateMemoryOrdering(TypeReference typeRef)
        {
            if (typeRef == null || typeRef.IsEntityType())
            {
                return 0;
            }

            var typeDef = typeRef.Resolve();
            if (typeDef.CustomAttributes.Count > 0)
            {
                var forcedMemoryOrderAttribute = typeDef.CustomAttributes.FirstOrDefault(ca => ca.Constructor.DeclaringType.Name == nameof(TypeManager.ForcedMemoryOrderingAttribute));
                if (forcedMemoryOrderAttribute != null)
                {
                    ulong memoryOrder = (ulong)forcedMemoryOrderAttribute.ConstructorArguments
                        .First(arg => arg.Type.MetadataType == MetadataType.UInt64)
                        .Value;

                    return memoryOrder;
                }
            }

            return CalculateStableTypeHash(typeRef);
        }
    }
}
#endif
