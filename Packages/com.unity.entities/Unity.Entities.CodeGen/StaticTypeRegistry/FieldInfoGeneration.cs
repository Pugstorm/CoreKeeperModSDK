#if UNITY_DOTSRUNTIME
using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.Cecil.Awesome.Comparers;
using Unity.Cecil.Awesome;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;
using TypeGenInfoList = System.Collections.Generic.List<Unity.Entities.CodeGen.StaticTypeRegistryPostProcessor.TypeGenInfo>;
using SystemList = System.Collections.Generic.List<Mono.Cecil.TypeDefinition>;

namespace Unity.Entities.CodeGen
{
    internal partial class StaticTypeRegistryPostProcessor : EntitiesILPostProcessor
    {
        // Unique list of field types and names
        HashSet<TypeReference> m_FieldTypes = new HashSet<TypeReference>(new Cecil.Awesome.Comparers.TypeReferenceEqualityComparer());
        HashSet<string> m_FieldNames = new HashSet<string>();

        // List of field info used for injecting FieldInfo information in a TypeRegistry
        List<FieldGenInfo> m_FieldGenInfos = new List<FieldGenInfo>();
        // Mapping for TypeReference to FieldInfo in the m_FieldGenInfos list
        Dictionary<TypeReference, FieldInfoLookUp> m_FieldInfoMap = new Dictionary<TypeReference, FieldInfoLookUp>(new Unity.Cecil.Awesome.Comparers.TypeReferenceEqualityComparer());

        struct FieldInfoLookUp
        {
            public int Index;
            public int Count;
        }

        internal struct FieldGenInfo
        {
            public int Offset;
            public TypeReference FieldType;
            public string FieldName;
            public int FieldTypeIndex;
            public int FieldNameIndex;
        }

        class FieldInfoData
        {
#pragma warning disable 0649
            public TypeReference BaseType;
            public string FieldPath;
            public TypeReference FieldType;
            public int FieldOffset;
#pragma warning restore 0649
        }

        class FieldInfoDataComparer : IEqualityComparer<FieldInfoData>
        {
            public bool Equals(FieldInfoData lhs, FieldInfoData rhs)
            {
                return lhs.BaseType.FullName.Equals(rhs.BaseType.FullName) && lhs.FieldPath.Equals(rhs.FieldPath);
            }

            public int GetHashCode(FieldInfoData obj)
            {
                return obj.BaseType.GetHashCode() * 347 ^ obj.FieldPath.GetHashCode();
            }
        }

        void GenerateFieldInfoForRegisteredComponents()
        {
            var componentsToGen = AssemblyDefinition.CustomAttributes
                .Where(ca => ca.AttributeType.Name == nameof(GenerateComponentFieldInfoAttribute))
                .Select(ca => ca.ConstructorArguments.First().Value as TypeReference)
                .Distinct();

            foreach (var component in componentsToGen)
            {
                var componentDef = component.Resolve();
                // UnityEngineObject category is returned if the component is not an ECS component type
                if (componentDef == null || FindTypeCategoryForType(componentDef) == TypeManager.TypeCategory.UnityEngineObject)
                    throw new ArgumentException($"Type '{component.FullName}' was registered for [GenerateComponentFieldInfo], however only value type ECS components are allowed. Please remove this type or change the type to be a value type.");

                GenerateFieldInfos(component);
            }
        }

        FieldInfoLookUp GenerateFieldInfos(TypeReference typeRef)
        {
            var lookup = new FieldInfoLookUp()
            {
                Index = m_FieldGenInfos.Count(),
                Count = 0
            };

            if (!m_FieldInfoMap.ContainsKey(typeRef))
            {
                var resolver = TypeResolver.For(typeRef);
                var typeDef = typeRef.Resolve();

                // Push the component into the fieldInfoTypeList
                m_FieldTypes.Add(typeRef);
                foreach (var field in typeDef.Fields)
                {
                    if (field.IsStatic)
                        continue;

                    var fieldType = resolver.ResolveFieldType(field);
                    var sanitizedFieldName = SanitizeFieldName(field.Name);

                    var fieldInfo = new FieldGenInfo
                    {
                        Offset = TypeUtils.GetFieldOffset(field.Name, typeRef, ArchBits),
                        FieldName = sanitizedFieldName,
                        FieldType = fieldType
                    };

                    m_FieldGenInfos.Add(fieldInfo);
                    m_FieldTypes.Add(fieldType);
                    m_FieldNames.Add(sanitizedFieldName);
                    lookup.Count++;
                }
                m_FieldInfoMap.Add(typeRef, lookup);

                // Now that we have added info for the top-level, recurse until all nested fields have fieldInfo
                foreach (var field in typeDef.Fields)
                {
                    if (field.IsStatic)
                        continue;

                    var fieldType = resolver.ResolveFieldType(field);
                    GenerateFieldInfos(fieldType);
                }
            }

            return lookup;
        }

        // It's pretty common for a component to have members that are the same type as other components, so collapse duplicate fieldinfo
        // so we refer to only a single name and field type that needs to be emitted
        void ReduceFieldInfos()
        {
            var fieldTypes = m_FieldTypes.ToList();
            var fieldNames = m_FieldNames.ToList();
            var trComparer = new TypeReferenceEqualityComparer();
            for (int i = 0; i < m_FieldGenInfos.Count; ++i)
            {
                var fieldInfo = m_FieldGenInfos[i];

                fieldInfo.FieldNameIndex = fieldNames.IndexOf(fieldInfo.FieldName);
                fieldInfo.FieldTypeIndex = fieldTypes.FindIndex(ft => trComparer.Equals(ft, fieldInfo.FieldType));

                m_FieldGenInfos[i] = fieldInfo;
            }
        }

        string SanitizeFieldName(string fieldName)
        {
            int indexLt = fieldName.IndexOf('<');
            int indexGt = fieldName.IndexOf('>');
            if (indexLt < 0 || indexGt < 0)
                return fieldName;

            return fieldName.Substring(indexLt + 1, indexGt - 1);
        }

        protected override bool PostProcessUnmanagedImpl(TypeDefinition[] unmanagedComponentSystemTypes)
        {
            return false;
        }
    }
}
#endif
