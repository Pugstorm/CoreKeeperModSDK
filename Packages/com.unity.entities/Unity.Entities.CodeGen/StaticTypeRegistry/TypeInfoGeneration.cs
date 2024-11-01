#if UNITY_DOTSRUNTIME
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;
using static Unity.Entities.CodeGen.ILHelper;
using static Unity.Entities.TypeManager;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeGenInfoList = System.Collections.Generic.List<Unity.Entities.CodeGen.StaticTypeRegistryPostProcessor.TypeGenInfo>;
using SystemList = System.Collections.Generic.List<Mono.Cecil.TypeDefinition>;
using Unity.Cecil.Awesome;
using static Unity.Entities.CodeGen.TypeReferenceExtensions;
using Unity.Collections.LowLevel.Unsafe;
using TypeAttributes = Mono.Cecil.TypeAttributes;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using TypeInfo = Unity.Entities.TypeManager.TypeInfo;

namespace Unity.Entities.CodeGen
{
    internal partial class StaticTypeRegistryPostProcessor : EntitiesILPostProcessor
    {
        internal struct TypeGenInfo
        {
            public TypeReference TypeReference;
            public TypeCategory TypeCategory;
            public List<int> EntityOffsets;
            public int EntityOffsetIndex;
            public List<int> BlobAssetRefOffsets;
            public int BlobAssetRefOffsetIndex;
            public List<int> WeakAssetRefOffsets;
            public int WeakAssetRefOffsetIndex;
            public HashSet<TypeReference> WriteGroupTypes;
            public int WriteGroupsIndex;
            public int FieldInfoCount;
            public int FieldInfoIndex;
            public int TypeIndex;
            public bool IsManaged;
            public TypeUtils.AlignAndSize AlignAndSize;
            public int BufferCapacity;
            public int MaxChunkCapacity;
            public int ElementSize;
            public int SizeInChunk;
            public int Alignment;
            public ulong StableHash;
            public ulong MemoryOrdering;
            public bool MightHaveEntityReferences;
            public bool MightHaveBlobAssetReferences;
        }

        int m_TotalTypeCount;
        int m_TotalEntityOffsetCount;
        int m_TotalBlobAssetRefOffsetCount;
        int m_TotalWeakAssetRefOffsetCount;
        int m_TotalWriteGroupCount;

        internal FieldReference GenerateConstantData(TypeDefinition constantStorageTypeDef, byte[] data)
        {
            const string kConstantDataFieldNamePrefix = "ConstantData";
            int constantDataCount = constantStorageTypeDef.NestedTypes.Count(t => t.Name.StartsWith(kConstantDataFieldNamePrefix, StringComparison.Ordinal));

            var constantDataTypeDef = new TypeDefinition(constantStorageTypeDef.Namespace, $"{kConstantDataFieldNamePrefix}{constantDataCount}", TypeAttributes.Class | TypeAttributes.NestedPublic | TypeAttributes.Sealed | TypeAttributes.AnsiClass, AssemblyDefinition.MainModule.ImportReference(typeof(ValueType)));
            constantStorageTypeDef.NestedTypes.Add(constantDataTypeDef);
            constantDataTypeDef.IsExplicitLayout = true;
            constantDataTypeDef.PackingSize = 1;
            constantDataTypeDef.ClassSize = data.Length;

            var constantDataFieldDef = new FieldDefinition($"Value{constantDataCount}", FieldAttributes.Static | FieldAttributes.InitOnly | FieldAttributes.HasFieldRVA, constantDataTypeDef);
            constantDataFieldDef.InitialValue = data;
            constantStorageTypeDef.Fields.Add(constantDataFieldDef);

            return constantDataFieldDef;
        }

        /// <summary>
        /// Populates the registry's BlobAssetReferenceOffsets int array.
        /// Offsets are laid out contiguously in memory such that the memory layout for Types A (2 entities), B (3 entities), C (0 entities) D (2 entities) is as such: aabbbdd
        /// </summary>
        internal void GenerateBlobAssetReferenceArray(ILProcessor il, in TypeGenInfoList typeGenInfoList, FieldReference fieldRef, bool isStaticField)
        {
            var intRef = AssemblyDefinition.MainModule.ImportReference(typeof(int));
            PushNewArray(il, intRef, m_TotalBlobAssetRefOffsetCount);

            int blobOffsetIndex = 0;
            foreach (var typeGenInfo in typeGenInfoList)
            {
                foreach (var offset in typeGenInfo.BlobAssetRefOffsets)
                {
                    PushNewArrayElement(il, blobOffsetIndex++);
                    EmitLoadConstant(il, offset);
                    il.Emit(OpCodes.Stelem_Any, intRef);
                }
            }

            StoreTopOfStackToField(il, fieldRef, isStaticField);
        }

        /// <summary>
        /// Populates the registry's entityOffset int array.
        /// Offsets are laid out contiguously in memory such that the memory layout for Types A (2 entites), B (3 entities), C (0 entities) D (2 entities) is as such: aabbbdd
        /// </summary>
        internal void GenerateEntityOffsetInfoArray(ILProcessor il, in TypeGenInfoList typeGenInfoList, FieldReference fieldRef, bool isStaticField)
        {
            PushNewArray(il, AssemblyDefinition.MainModule.ImportReference(typeof(int)), m_TotalEntityOffsetCount);

            int entityOffsetIndex = 0;
            foreach (var typeGenInfo in typeGenInfoList)
            {
                foreach (var offset in typeGenInfo.EntityOffsets)
                {
                    PushNewArrayElement(il, entityOffsetIndex++);
                    EmitLoadConstant(il, offset);
                    il.Emit(OpCodes.Stelem_Any, AssemblyDefinition.MainModule.ImportReference(typeof(int)));
                }
            }

            StoreTopOfStackToField(il, fieldRef, isStaticField);
        }

        /// <summary>
        ///  Populates the registry's System.Type array for all types in typeIndex order.
        /// </summary>
        internal void GenerateTypeArray(ILProcessor il, List<TypeReference> typeReferences, FieldReference fieldRef, bool isStaticField)
        {
            PushNewArray(il, m_SystemTypeRef, typeReferences.Count);

            for (int typeIndex = 0; typeIndex < typeReferences.Count; ++typeIndex)
            {
                TypeReference typeRef = EnsureImported(typeReferences[typeIndex]);

                PushNewArrayElement(il, typeIndex);
                il.Emit(OpCodes.Ldtoken, typeRef); // Push our meta-type onto the stack as it will be our arg to System.Type.GetTypeFromHandle
                il.Emit(OpCodes.Call, m_GetTypeFromHandleFnRef); // Call System.Type.GetTypeFromHandle with the above stack arg. Return value pushed on the stack
                il.Emit(OpCodes.Stelem_Ref);
            }

            StoreTopOfStackToField(il, fieldRef, isStaticField);
        }

        /// <summary>
        ///  Populates the registry's SystemTypeSizes array for all System types in typeIndex order.
        ///  Currently sets 0 for size of managed systems.
        /// </summary>
        internal void GenerateSystemTypeSizeArray(ILProcessor il, List<TypeReference> typeReferences, FieldReference fieldRef, bool isStaticField, int archbits)
        {
            PushNewArray(il, m_SystemIntRef, typeReferences.Count);

            for (int typeIndex = 0; typeIndex < typeReferences.Count; ++typeIndex)
            {
                TypeReference typeRef = EnsureImported(typeReferences[typeIndex]);
                var size = TypeUtils.AlignAndSizeOfType(typeRef, archbits).size;

                PushNewArrayElement(il, typeIndex);
                il.Emit(OpCodes.Ldc_I4, size); // Push our size onto the stack
                il.Emit(OpCodes.Stelem_I4);
            }

            StoreTopOfStackToField(il, fieldRef, isStaticField);
        }

        /// <summary>
        ///  Populates the registry's SystemTypeHashes array for all System types in typeIndex order.
        /// </summary>
        internal void GenerateSystemTypeHashArray(ILProcessor il, List<TypeReference> typeReferences, FieldReference fieldRef, bool isStaticField)
        {
            PushNewArray(il, m_SystemLongRef, typeReferences.Count);

            for (int typeIndex = 0; typeIndex < typeReferences.Count; ++typeIndex)
            {
                TypeReference typeRef = EnsureImported(typeReferences[typeIndex]);

                PushNewArrayElement(il, typeIndex);
                il.Emit(OpCodes.Call, m_BurstRuntimeGetHashCode64Ref.MakeGenericInstanceMethod(typeRef)); // Call BurstRuntime.GetHashCode64 with the above stack arg. Return value pushed on the stack
                il.Emit(OpCodes.Stelem_I8);
            }

            StoreTopOfStackToField(il, fieldRef, isStaticField);
        }

        /// <summary>
        /// Populates the registry's writeGroup int array.
        /// WriteGroup TypeIndices are laid out contiguously in memory such that the memory layout for Types A (2 writegroup elements),
        /// B (3 writegroup elements), C (0 writegroup elements) D (2 writegroup elements) is as such: aabbbdd
        /// </summary>
        internal void GenerateWriteGroupArray(ILProcessor il, TypeGenInfoList typeGenInfoList, FieldReference fieldRef, bool isStaticField)
        {
            PushNewArray(il, m_SystemTypeRef, m_TotalWriteGroupCount);

            int writeGroupIndex = 0;
            foreach (var typeGenInfo in typeGenInfoList)
            {
                foreach (var wgType in typeGenInfo.WriteGroupTypes)
                {
                    PushNewArrayElement(il, writeGroupIndex++);
                    il.Emit(OpCodes.Ldtoken, AssemblyDefinition.MainModule.ImportReference(wgType)); // Push our meta-type onto the stack as it will be our arg to System.Type.GetTypeFromHandle
                    il.Emit(OpCodes.Call, m_GetTypeFromHandleFnRef); // Call System.Type.GetTypeFromHandle with the above stack arg. Return value pushed on the stack
                    il.Emit(OpCodes.Stelem_Ref);
                }
            }

            StoreTopOfStackToField(il, fieldRef, isStaticField);
        }

        /// <summary>
        /// Populates the registry's TypeInfo array in typeIndex order.
        /// </summary>
        internal unsafe byte[] GenerateTypeInfoBlobArray(TypeGenInfoList typeGenInfoList)
        {
            var typeInfoSize = sizeof(TypeInfo);
            var blob = new byte[typeInfoSize * typeGenInfoList.Count];
            if (typeGenInfoList.Count == 0)
                return blob;

            fixed (byte* pData = &blob[0])
            {
                for (int i = 0; i < typeGenInfoList.Count; ++i)
                {
                    var typeGenInfo = typeGenInfoList[i];
                    var typeInfo = new TypeInfo(
                        typeGenInfo.TypeIndex,
                        typeGenInfo.TypeCategory,
                        typeGenInfo.EntityOffsetIndex == -1 ? -1 : typeGenInfo.EntityOffsets.Count,
                        typeGenInfo.EntityOffsetIndex,
                        typeGenInfo.MemoryOrdering,
                        typeGenInfo.StableHash,
                        typeGenInfo.BufferCapacity,
                        typeGenInfo.SizeInChunk,
                        typeGenInfo.ElementSize,
                        typeGenInfo.Alignment,
                        typeGenInfo.MaxChunkCapacity,
                        typeGenInfo.WriteGroupTypes.Count,
                        typeGenInfo.WriteGroupsIndex,
                        typeGenInfo.MightHaveBlobAssetReferences,
                        typeGenInfo.BlobAssetRefOffsets.Count,
                        typeGenInfo.BlobAssetRefOffsetIndex,
                        0,
                        0,
                        typeGenInfo.AlignAndSize.size
                    );
                    *((TypeInfo*)pData + i) = typeInfo;
                }
            }

            return blob;
        }

        /// <summary>
        /// Populates the registry's TypeInfo array in typeIndex order.
        /// </summary>
        internal void GenerateTypeInfoArray(ILProcessor il, TypeGenInfoList typeGenInfoList, FieldReference fieldRef, bool isStaticField)
        {
            PushNewArray(il, m_TypeInfoRef, typeGenInfoList.Count);

            for (int i = 0; i < typeGenInfoList.Count; ++i)
            {
                var typeGenInfo = typeGenInfoList[i];

                if (i != (typeGenInfo.TypeIndex & ClearFlagsMask))
                    throw new ArgumentException("The typeGenInfo list is not in the correct order. This is a bug.");

                PushNewArrayElement(il, i);

                // Push constructor arguments on to the stack
                EmitLoadConstant(il, typeGenInfo.TypeIndex);
                EmitLoadConstant(il, (int)typeGenInfo.TypeCategory);
                EmitLoadConstant(il, typeGenInfo.EntityOffsetIndex == -1 ? -1 : typeGenInfo.EntityOffsets.Count);
                EmitLoadConstant(il, typeGenInfo.EntityOffsetIndex);
                EmitLoadConstant(il, typeGenInfo.MemoryOrdering);
                EmitLoadConstant(il, typeGenInfo.StableHash);
                EmitLoadConstant(il, typeGenInfo.BufferCapacity);
                EmitLoadConstant(il, typeGenInfo.SizeInChunk);
                EmitLoadConstant(il, typeGenInfo.ElementSize);
                EmitLoadConstant(il, typeGenInfo.Alignment);
                EmitLoadConstant(il, typeGenInfo.MaxChunkCapacity);
                EmitLoadConstant(il, typeGenInfo.WriteGroupTypes.Count);
                EmitLoadConstant(il, typeGenInfo.WriteGroupsIndex);
                EmitLoadConstant(il, typeGenInfo.BlobAssetRefOffsets.Count);
                EmitLoadConstant(il, typeGenInfo.BlobAssetRefOffsetIndex);
                EmitLoadConstant(il, typeGenInfo.WeakAssetRefOffsets.Count);
                EmitLoadConstant(il, typeGenInfo.WeakAssetRefOffsetIndex);
                EmitLoadConstant(il, typeGenInfo.AlignAndSize.size);

                il.Emit(OpCodes.Newobj, m_TypeInfoConstructorRef);

                il.Emit(OpCodes.Stelem_Any, m_TypeInfoRef);
            }

            StoreTopOfStackToField(il, fieldRef, isStaticField);
        }

        internal void GenerateFieldInfoArray(ILProcessor il, List<FieldGenInfo> fieldInfoList, FieldReference fieldRef, bool isStaticField)
        {
            PushNewArray(il, m_FieldInfoRef, fieldInfoList.Count);

            for (int i = 0; i < fieldInfoList.Count; ++i)
            {
                var fieldInfo = fieldInfoList[i];

                PushNewArrayElement(il, i);

                // Push constructor arguments on to the stack
                EmitLoadConstant(il, fieldInfo.Offset);
                EmitLoadConstant(il, fieldInfo.FieldTypeIndex);
                EmitLoadConstant(il, fieldInfo.FieldNameIndex);

                il.Emit(OpCodes.Newobj, m_FieldInfoConstructorRef);

                il.Emit(OpCodes.Stelem_Any, m_FieldInfoRef);
            }

            StoreTopOfStackToField(il, fieldRef, isStaticField);
        }

        internal void GenerateFieldInfoLookupArray(ILProcessor il, FieldReference fieldRef, bool isStaticField)
        {
            PushNewArray(il, m_FieldInfoLookupRef, m_FieldTypes.Count);

            int typeIndex = 0;
            foreach (var fieldType in m_FieldTypes)
            {
                var lookup = m_FieldInfoMap[fieldType];

                PushNewArrayElement(il, typeIndex);

                // Push constructor arguments on to the stack
                EmitLoadConstant(il, typeIndex);
                EmitLoadConstant(il, lookup.Index);
                EmitLoadConstant(il, lookup.Count);

                il.Emit(OpCodes.Newobj, m_FieldInfoLookupConstructorRef);

                il.Emit(OpCodes.Stelem_Any, m_FieldInfoLookupRef);
                typeIndex++;
            }

            StoreTopOfStackToField(il, fieldRef, isStaticField);
        }

        internal MethodDefinition InjectConstructComponentFunction(TypeGenInfoList typeGenInfoList)
        {
            var createComponentFn = new MethodDefinition(
                "ConstructComponentFromBuffer",
                MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig,
                AssemblyDefinition.MainModule.ImportReference(typeof(object)));
            GeneratedRegistryDef.Methods.Add(createComponentFn);

            var srcPtrArg =
                new ParameterDefinition("buffer",
                    Mono.Cecil.ParameterAttributes.None,
                    AssemblyDefinition.MainModule.ImportReference(typeof(void*)));
            createComponentFn.Parameters.Add(srcPtrArg);

            var typeIndexNoFlagsArg = new ParameterDefinition("typeIndexNoFlags",
                Mono.Cecil.ParameterAttributes.None,
                AssemblyDefinition.MainModule.ImportReference(typeof(int)));
            createComponentFn.Parameters.Add(typeIndexNoFlagsArg);

            createComponentFn.Body.InitLocals = true;
            var il = createComponentFn.Body.GetILProcessor();

            il.Emit(OpCodes.Ldarg, srcPtrArg);
            var opBeforeSwitch = il.Create(OpCodes.Ldarg, typeIndexNoFlagsArg);
            var listOfBranches = new List<Instruction>();
            il.Append(opBeforeSwitch);

            foreach (var typeInfo in typeGenInfoList)
            {
                if (typeInfo.TypeReference == null)
                    continue;

                var componentRef = AssemblyDefinition.MainModule.ImportReference(typeInfo.TypeReference);

                var firstOp = Instruction.Create(OpCodes.Ldobj, componentRef);
                listOfBranches.Add(firstOp);
                il.Append(firstOp);
                il.Emit(OpCodes.Box, componentRef);
                il.Emit(OpCodes.Ret);
            }

            // Reverse order of what would appear in the executable
            var defaultCaseOp = Instruction.Create(OpCodes.Ldstr, "FATAL: Tried to construct a type that is unknown to the StaticTypeRegistry");
            il.InsertAfter(opBeforeSwitch, il.Create(OpCodes.Br, defaultCaseOp));
            il.InsertAfter(opBeforeSwitch, il.Create(OpCodes.Switch, listOfBranches.ToArray()));

            il.Append(defaultCaseOp);
            var notSupportedExConstructor = AssemblyDefinition.MainModule.ImportReference(typeof(NotSupportedException)).Resolve().GetConstructors()
                .Single(c => c.Parameters.Count == 1 && c.Parameters[0].ParameterType.MetadataType == MetadataType.String);
            il.Emit(OpCodes.Newobj, AssemblyDefinition.MainModule.ImportReference(notSupportedExConstructor));
            il.Emit(OpCodes.Throw);
            return createComponentFn;
        }

        (MethodReference, MethodReference, MethodReference) InjectEqualityFunctions(TypeGenInfoList typeGenInfoList)
        {
            // Declares: static public bool Equals(object lhs, object rhs, int typeIndex)
            // This function is required to allow users to query for equality when a Generic <T> param isn't available but the 'int' typeIndex is
            var boxedEqualsFn = new MethodDefinition("Equals", MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig, AssemblyDefinition.MainModule.ImportReference(typeof(bool)));
            boxedEqualsFn.Parameters.Add(new ParameterDefinition("lhs", Mono.Cecil.ParameterAttributes.None, AssemblyDefinition.MainModule.ImportReference(typeof(object))));
            boxedEqualsFn.Parameters.Add(new ParameterDefinition("rhs", Mono.Cecil.ParameterAttributes.None, AssemblyDefinition.MainModule.ImportReference(typeof(object))));
            boxedEqualsFn.Parameters.Add(new ParameterDefinition("typeIndex", Mono.Cecil.ParameterAttributes.None, AssemblyDefinition.MainModule.ImportReference(typeof(int))));
            GeneratedRegistryDef.Methods.Add(boxedEqualsFn);

            // Declares: static public bool Equals(object lhs, void* rhs, int typeIndex)
            // This function is required to allow users to query for equality when a Generic <T> param isn't available but the 'int' typeIndex is
            var boxedEqualsPtrFn = new MethodDefinition("Equals", MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig, AssemblyDefinition.MainModule.ImportReference(typeof(bool)));
            boxedEqualsPtrFn.Parameters.Add(new ParameterDefinition("lhs", Mono.Cecil.ParameterAttributes.None, AssemblyDefinition.MainModule.ImportReference(typeof(object))));
            boxedEqualsPtrFn.Parameters.Add(new ParameterDefinition("rhs", Mono.Cecil.ParameterAttributes.None, AssemblyDefinition.MainModule.ImportReference(typeof(void*))));
            boxedEqualsPtrFn.Parameters.Add(new ParameterDefinition("typeIndex", Mono.Cecil.ParameterAttributes.None, AssemblyDefinition.MainModule.ImportReference(typeof(int))));
            GeneratedRegistryDef.Methods.Add(boxedEqualsPtrFn);

            // Declares: static public int GetHashCode(object val, int typeIndex)
            // This function is required to allow users to query for equality when a Generic <T> param isn't available but the 'int' typeIndex is
            var boxedGetHashCodeFn = new MethodDefinition("BoxedGetHashCode", MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig, AssemblyDefinition.MainModule.ImportReference(typeof(int)));
            boxedGetHashCodeFn.Parameters.Add(new ParameterDefinition("val", Mono.Cecil.ParameterAttributes.None, AssemblyDefinition.MainModule.ImportReference(typeof(object))));
            boxedGetHashCodeFn.Parameters.Add(new ParameterDefinition("typeIndex", Mono.Cecil.ParameterAttributes.None, AssemblyDefinition.MainModule.ImportReference(typeof(int))));
            GeneratedRegistryDef.Methods.Add(boxedGetHashCodeFn);

            // List of instructions in an array where index == typeIndex
            var boxedEqJumpTable = new List<Instruction>[typeGenInfoList.Count];
            var boxedPtrEqJumpTable = new List<Instruction>[typeGenInfoList.Count];
            var boxedHashJumpTable = new List<Instruction>[typeGenInfoList.Count];

            for (int i = 0; i < typeGenInfoList.Count; ++i)
            {
                var typeGenInfo = typeGenInfoList[i];
                var thisTypeRef = AssemblyDefinition.MainModule.ImportReference(typeGenInfo.TypeReference);
                var typeRef = AssemblyDefinition.MainModule.ImportReference(typeGenInfo.TypeReference);

                {
                    // Equals function for operating on (object lhs, object rhs, int typeIndex) where the type isn't known by the user
                    {
                        var eqIL = boxedEqualsFn.Body.GetILProcessor();

                        boxedEqJumpTable[i] = new List<Instruction>();
                        var instructionList = boxedEqJumpTable[i];

                        if (!typeGenInfo.IsManaged)
                        {
                            instructionList.Add(eqIL.Create(OpCodes.Ldarg_0));
                            instructionList.Add(eqIL.Create(OpCodes.Unbox, thisTypeRef));
                            instructionList.Add(eqIL.Create(OpCodes.Ldarg_1));
                            instructionList.Add(eqIL.Create(OpCodes.Unbox, thisTypeRef));
                            instructionList.Add(eqIL.Create(OpCodes.Ldc_I8, (long)typeGenInfo.AlignAndSize.size));
                            instructionList.Add(eqIL.Create(OpCodes.Call, m_MemCmpFnRef));
                            instructionList.Add(eqIL.Create(OpCodes.Ldc_I4_0));
                            instructionList.Add(eqIL.Create(OpCodes.Ceq));
                        }
                        else
                        {
                            if (!typeRef.IsValueType())
                            {
                                var equalsFn = GenerateEqualsFunction(GeneratedRegistryDef, typeGenInfo);
                                instructionList.Add(eqIL.Create(OpCodes.Ldarg_0));
                                instructionList.Add(eqIL.Create(OpCodes.Castclass, thisTypeRef));
                                instructionList.Add(eqIL.Create(OpCodes.Ldarg_1));
                                instructionList.Add(eqIL.Create(OpCodes.Castclass, thisTypeRef));
                                instructionList.Add(eqIL.Create(OpCodes.Call, equalsFn));
                            }
                            else
                            {
                                var userImpl = GetTypesEqualsMethodReference(typeGenInfo.TypeReference)?.Resolve();
                                if (userImpl == null)
                                    throw new ArgumentException($"Component type '{typeRef.FullName}' contains managed references. It must implement IEquatable<>");

                                var loc0 = new VariableDefinition(thisTypeRef);
                                boxedEqualsFn.Body.Variables.Add(loc0);

                                instructionList.Add(eqIL.Create(OpCodes.Ldarg_0));
                                instructionList.Add(eqIL.Create(OpCodes.Unbox_Any, thisTypeRef));
                                instructionList.Add(eqIL.Create(OpCodes.Stloc, loc0));
                                instructionList.Add(eqIL.Create(OpCodes.Ldloca, loc0));
                                instructionList.Add(eqIL.Create(OpCodes.Ldarg_1));
                                instructionList.Add(eqIL.Create(OpCodes.Unbox_Any, thisTypeRef));
                                instructionList.Add(eqIL.Create(OpCodes.Call, AssemblyDefinition.MainModule.ImportReference(userImpl)));
                            }
                        }

                        instructionList.Add(eqIL.Create(OpCodes.Ret));
                    }

                    // Equals function for operating on (object lhs, void* rhs, int typeIndex) where the type isn't known by the user
                    {
                        var eqIL = boxedEqualsPtrFn.Body.GetILProcessor();

                        boxedPtrEqJumpTable[i] = new List<Instruction>();
                        var instructionList = boxedPtrEqJumpTable[i];


                        if (!typeGenInfo.IsManaged)
                        {
                            instructionList.Add(eqIL.Create(OpCodes.Ldarg_0));
                            instructionList.Add(eqIL.Create(OpCodes.Unbox, thisTypeRef));
                            instructionList.Add(eqIL.Create(OpCodes.Ldarg_1));
                            instructionList.Add(eqIL.Create(OpCodes.Ldc_I8, (long)typeGenInfo.AlignAndSize.size));
                            instructionList.Add(eqIL.Create(OpCodes.Call, m_MemCmpFnRef));
                            instructionList.Add(eqIL.Create(OpCodes.Ldc_I4_0));
                            instructionList.Add(eqIL.Create(OpCodes.Ceq));
                            instructionList.Add(eqIL.Create(OpCodes.Ret));
                        }
                        else
                        {
                            instructionList.Add(eqIL.Create(OpCodes.Ldstr, "Equals(object, void*) is not supported for managed types in DOTSRuntime"));
                            var notSupportedExConstructor = AssemblyDefinition.MainModule.ImportReference(typeof(NotSupportedException)).Resolve().GetConstructors()
                                .Single(c => c.Parameters.Count == 1 && c.Parameters[0].ParameterType.MetadataType == MetadataType.String);
                            instructionList.Add(eqIL.Create(OpCodes.Newobj, AssemblyDefinition.MainModule.ImportReference(notSupportedExConstructor)));
                            instructionList.Add(eqIL.Create(OpCodes.Throw));
                        }
                    }
                }

                // Store new Hash fn to Hash member
                {
                    // Hash function for operating on (object val, int typeIndex) where the type isn't known by the user
                    {
                        var hashIL = boxedGetHashCodeFn.Body.GetILProcessor();
                        boxedHashJumpTable[i] = new List<Instruction>();
                        var instructionList = boxedHashJumpTable[i];

                        if (!typeGenInfo.IsManaged)
                        {
                            instructionList.Add(hashIL.Create(OpCodes.Ldarg_0));
                            instructionList.Add(hashIL.Create(OpCodes.Unbox, thisTypeRef));
                            instructionList.Add(hashIL.Create(OpCodes.Ldc_I4, typeGenInfo.AlignAndSize.size));
                            instructionList.Add(hashIL.Create(OpCodes.Ldc_I4_0));
                            instructionList.Add(hashIL.Create(OpCodes.Call, m_Hash32FnRef));
                        }
                        else
                        {
                            if (!typeRef.IsValueType())
                            {
                                var hashFn = GenerateHashFunction(GeneratedRegistryDef, typeGenInfo.TypeReference);
                                instructionList.Add(hashIL.Create(OpCodes.Ldarg_0));
                                instructionList.Add(hashIL.Create(OpCodes.Castclass, thisTypeRef));
                                instructionList.Add(hashIL.Create(OpCodes.Call, hashFn));
                            }
                            else
                            {
                                var userImpl = GetTypesGetHashCodeMethodReference(typeRef)?.Resolve();
                                if (userImpl == null)
                                    throw new ArgumentException($"Component type '{typeRef.FullName}' contains managed references. It must implement IEquatable<>");

                                var loc0 = new VariableDefinition(thisTypeRef);
                                boxedGetHashCodeFn.Body.Variables.Add(loc0);

                                instructionList.Add(hashIL.Create(OpCodes.Ldarg_0));
                                instructionList.Add(hashIL.Create(OpCodes.Unbox_Any, thisTypeRef));
                                instructionList.Add(hashIL.Create(OpCodes.Stloc, loc0));
                                instructionList.Add(hashIL.Create(OpCodes.Ldloca, loc0));
                                instructionList.Add(hashIL.Create(OpCodes.Constrained, thisTypeRef));
                                instructionList.Add(hashIL.Create(OpCodes.Callvirt, AssemblyDefinition.MainModule.ImportReference(userImpl)));
                            }
                        }
                        instructionList.Add(hashIL.Create(OpCodes.Ret));
                    }
                }
            }

            // We now have a list of instructions for each type on how to invoke the correct Equals/Hash call.
            // Now generate the void* Equals and Hash functions by making a jump table to those instructions

            // object Equals
            {
                var eqIL = boxedEqualsFn.Body.GetILProcessor();
                List<Instruction> jumps = new List<Instruction>(boxedEqJumpTable.Length);
                Instruction loadTypeIndex = eqIL.Create(OpCodes.Ldarg_2);
                Instruction defaultCaseOp = Instruction.Create(OpCodes.Ldstr, "FATAL: Tried to call TypeManager.Equals() for a component type unknown to the static TypeRegistry");
                eqIL.Append(loadTypeIndex); // Load typeIndex

                foreach (var instructionList in boxedEqJumpTable)
                {
                    if (instructionList == null)
                    {
                        jumps.Add(defaultCaseOp);
                        continue;
                    }

                    // Add starting instruction to our jump table so we know which Equals IL block to execute
                    jumps.Add(instructionList[0]);

                    foreach (var instruction in instructionList)
                    {
                        eqIL.Append(instruction);
                    }
                }

                // default case
                eqIL.Append(defaultCaseOp);
                var notSupportedExConstructor = AssemblyDefinition.MainModule.ImportReference(typeof(NotSupportedException)).Resolve().GetConstructors()
                    .Single(c => c.Parameters.Count == 1 && c.Parameters[0].ParameterType.MetadataType == MetadataType.String);
                eqIL.Emit(OpCodes.Newobj, AssemblyDefinition.MainModule.ImportReference(notSupportedExConstructor));
                eqIL.Emit(OpCodes.Throw);

                // Since we are using InsertAfter these instructions are appended in reverse order to how they will appear
                eqIL.InsertAfter(loadTypeIndex, eqIL.Create(OpCodes.Br, defaultCaseOp));
                eqIL.InsertAfter(loadTypeIndex, eqIL.Create(OpCodes.Switch, jumps.ToArray()));
            }

            // object, void* Equals
            {
                var eqIL = boxedEqualsPtrFn.Body.GetILProcessor();
                List<Instruction> jumps = new List<Instruction>(boxedPtrEqJumpTable.Length);
                Instruction loadTypeIndex = eqIL.Create(OpCodes.Ldarg_2);
                Instruction defaultCaseOp = Instruction.Create(OpCodes.Ldstr, "FATAL: Tried to call TypeManager.Equals() for a component type unknown to the static TypeRegistry");
                eqIL.Append(loadTypeIndex); // Load typeIndex

                foreach (var instructionList in boxedPtrEqJumpTable)
                {
                    if (instructionList == null)
                    {
                        jumps.Add(defaultCaseOp);
                        continue;
                    }

                    // Add starting instruction to our jump table so we know which Equals IL block to execute
                    jumps.Add(instructionList[0]);

                    foreach (var instruction in instructionList)
                    {
                        eqIL.Append(instruction);
                    }
                }

                // default case
                eqIL.Append(defaultCaseOp);
                var notSupportedExConstructor = AssemblyDefinition.MainModule.ImportReference(typeof(NotSupportedException)).Resolve().GetConstructors()
                    .Single(c => c.Parameters.Count == 1 && c.Parameters[0].ParameterType.MetadataType == MetadataType.String);
                eqIL.Emit(OpCodes.Newobj, AssemblyDefinition.MainModule.ImportReference(notSupportedExConstructor));
                eqIL.Emit(OpCodes.Throw);

                // Since we are using InsertAfter these instructions are appended in reverse order to how they will appear
                eqIL.InsertAfter(loadTypeIndex, eqIL.Create(OpCodes.Br, defaultCaseOp));
                eqIL.InsertAfter(loadTypeIndex, eqIL.Create(OpCodes.Switch, jumps.ToArray()));
            }

            // object Hash
            {
                var hashIL = boxedGetHashCodeFn.Body.GetILProcessor();
                List<Instruction> jumps = new List<Instruction>(boxedHashJumpTable.Length);
                Instruction loadTypeIndex = hashIL.Create(OpCodes.Ldarg_1);
                Instruction defaultCaseOp = Instruction.Create(OpCodes.Ldstr, "FATAL: Tried to call TypeManager.GetHashCode() for a component type unknown to the static TypeRegistry");
                hashIL.Append(loadTypeIndex); // Load typeIndex

                foreach (var instructionList in boxedHashJumpTable)
                {
                    if (instructionList == null)
                    {
                        jumps.Add(defaultCaseOp);
                        continue;
                    }

                    // Add starting instruction to our jump table so we know which Equals IL block to execute
                    jumps.Add(instructionList[0]);

                    foreach (var instruction in instructionList)
                    {
                        hashIL.Append(instruction);
                    }
                }

                // default case
                hashIL.Append(defaultCaseOp);
                var notSupportedExConstructor = AssemblyDefinition.MainModule.ImportReference(typeof(NotSupportedException)).Resolve().GetConstructors()
                    .Single(c => c.Parameters.Count == 1 && c.Parameters[0].ParameterType.MetadataType == MetadataType.String);
                hashIL.Emit(OpCodes.Newobj, AssemblyDefinition.MainModule.ImportReference(notSupportedExConstructor));
                hashIL.Emit(OpCodes.Throw);

                // Since we are using InsertAfter these instructions are appended in reverse order to how they will appear in generated code
                hashIL.InsertAfter(loadTypeIndex, hashIL.Create(OpCodes.Br, defaultCaseOp));
                hashIL.InsertAfter(loadTypeIndex, hashIL.Create(OpCodes.Switch, jumps.ToArray()));
            }

            return (boxedEqualsFn, boxedEqualsPtrFn, boxedGetHashCodeFn);
        }

        private static MethodReference GetTypesEqualsMethodReference(TypeReference typeRef)
        {
            return GetTypesEqualsMethodReference(typeRef.Resolve());
        }

        private static MethodReference GetTypesEqualsMethodReference(TypeDefinition typeDef)
        {
            return typeDef.Methods.FirstOrDefault(
                m => m.Name == "Equals"
                && m.Parameters.Count == 1
                && m.Parameters[0].ParameterType == typeDef);
        }

        private static MethodReference GetTypesGetHashCodeMethodReference(TypeReference typeRef)
        {
            return GetTypesGetHashCodeMethodReference(typeRef.Resolve());
        }

        private static MethodReference GetTypesGetHashCodeMethodReference(TypeDefinition typeDef)
        {
            // This code is kind of weak. We actually want to confirm this function is overriding System.Object.GetHashCode however
            // as far as I can tell, cecil is not detecting legitimate overrides so we resort to this.
            return typeDef.Methods.FirstOrDefault(
                m => m.Name == "GetHashCode" && m.Parameters.Count == 0);
        }

        private TypeReference EnsureImported(TypeReference type)
        {
            var module = AssemblyDefinition.MainModule;
            if (type.IsGenericInstance)
            {
                var importedType = new GenericInstanceType(module.ImportReference(type.Resolve()));
                var genericType = type as GenericInstanceType;
                foreach (var ga in genericType.GenericArguments)
                    importedType.GenericArguments.Add(ga.IsGenericParameter ? ga : module.ImportReference(ga));
                return module.ImportReference(importedType);
            }
            return module.ImportReference(type);
        }

        internal MethodReference GenerateEqualsFunction(TypeDefinition registryDef, TypeGenInfo typeGenInfo)
        {
            var typeRef = AssemblyDefinition.MainModule.ImportReference(typeGenInfo.TypeReference);
            var equalsFn = new MethodDefinition("DoEquals", MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig, AssemblyDefinition.MainModule.ImportReference(typeof(bool)));
            var arg0 = new ParameterDefinition("i0", Mono.Cecil.ParameterAttributes.None, typeRef.IsValueType() ? new ByReferenceType(typeRef) : typeRef);
            var arg1 = new ParameterDefinition("i1", Mono.Cecil.ParameterAttributes.None, typeRef.IsValueType() ? new ByReferenceType(typeRef) : typeRef);
            equalsFn.Parameters.Add(arg0);
            equalsFn.Parameters.Add(arg1);
            registryDef.Methods.Add(equalsFn);

            var il = equalsFn.Body.GetILProcessor();

            var userImpl = GetTypesEqualsMethodReference(typeGenInfo.TypeReference)?.Resolve();
            if (userImpl != null)
            {
                if (typeRef.IsValueType())
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldobj, typeRef);
                    il.Emit(OpCodes.Call, AssemblyDefinition.MainModule.ImportReference(userImpl));
                }
                else
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Callvirt, AssemblyDefinition.MainModule.ImportReference(userImpl));
                    // Ret is called outside of this block
                }
            }
            else
            {
                GenerateEqualsFunctionRecurse(il, arg0, arg1, typeGenInfo);
            }

            il.Emit(OpCodes.Ret);

            return equalsFn;
        }

        internal void GenerateEqualsFunctionRecurse(ILProcessor il, ParameterDefinition arg0, ParameterDefinition arg1, TypeGenInfo typeGenInfo)
        {
            int typeSize = typeGenInfo.AlignAndSize.size;

            // Raw memcmp of the two types
            // May need to do something more clever if this doesn't pan out for all types
            il.Emit(OpCodes.Ldarg, arg0);
            il.Emit(OpCodes.Ldarg, arg1);
            il.Emit(OpCodes.Ldc_I8, (long)typeSize);
            il.Emit(OpCodes.Call, m_MemCmpFnRef);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ceq);
        }

        internal MethodReference GenerateHashFunction(TypeDefinition registryDef, TypeReference typeRef)
        {
            // http://www.isthe.com/chongo/tech/comp/fnv/index.html#FNV-1a
            const int FNV1a_32_OFFSET = unchecked((int)0x811C9DC5);

            var importedTypeRef = AssemblyDefinition.MainModule.ImportReference(typeRef);
            var hashFn = new MethodDefinition("DoHash", MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig, AssemblyDefinition.MainModule.ImportReference(typeof(int)));
            var arg0 = new ParameterDefinition("val", Mono.Cecil.ParameterAttributes.None, typeRef.IsValueType() ? new ByReferenceType(importedTypeRef) : importedTypeRef);
            hashFn.Parameters.Add(arg0);
            registryDef.Methods.Add(hashFn);

            var il = hashFn.Body.GetILProcessor();

            MethodDefinition userImpl = GetTypesGetHashCodeMethodReference(importedTypeRef)?.Resolve();
            if (userImpl != null)
            {
                il.Emit(OpCodes.Ldarg_0);
                if (typeRef.IsValueType())
                {
                    // avoid boxing if we know the type is a value type
                    il.Emit(OpCodes.Constrained, importedTypeRef);
                }
                il.Emit(OpCodes.Callvirt, AssemblyDefinition.MainModule.ImportReference(userImpl));
            }
            else
            {
                List<Instruction> fieldLoadChain = new List<Instruction>();
                List<Instruction> hashInstructions = new List<Instruction>();

                GenerateHashFunctionRecurse(il, hashInstructions, fieldLoadChain, arg0, importedTypeRef);
                if (hashInstructions.Count == 0)
                {
                    // If the type doesn't contain any value types to hash we want to return 0 as the hash
                    il.Emit(OpCodes.Ldc_I4_0);
                }
                else
                {
                    EmitLoadConstant(il, FNV1a_32_OFFSET); // Initial Hash value

                    foreach (var instruction in hashInstructions)
                    {
                        il.Append(instruction);
                    }
                }
            }

            il.Emit(OpCodes.Ret);

            return hashFn;
        }

        internal void GenerateHashFunctionRecurse(ILProcessor il, List<Instruction> hashInstructions, List<Instruction> fieldLoadChain, ParameterDefinition val, TypeReference typeRef)
        {
            // http://www.isthe.com/chongo/tech/comp/fnv/index.html#FNV-1a
            const int FNV1a_32_PRIME = 16777619;
            var typeResolver = TypeResolver.For(typeRef);
            var typeDef = typeRef.Resolve();
            foreach (var f in typeDef.Fields)
            {
                if (!f.IsStatic)
                {
                    var field = typeResolver.Resolve(f);
                    var fieldTypeRef = typeResolver.Resolve(field.FieldType);
                    var fieldTypeDef = fieldTypeRef.Resolve();
                    fieldTypeDef.MakeTypeInternal();

                    // https://cecilifier.appspot.com/ outputs what you would expect here, that there is
                    // a bit in the attributes for 'Fixed.'. Specifically:
                    //     FieldAttributes.Fixed
                    // Haven't been able to find the actual numeric value. Until then, use this approach:
                    bool isFixed = fieldTypeDef.ClassSize != -1 && fieldTypeDef.Name.Contains(">e__FixedBuffer");
                    if (isFixed || fieldTypeRef.IsPrimitive || fieldTypeRef.IsPointer || fieldTypeDef.IsEnum)
                    {
                        /*
                         Equivalent to:
                            hash *= FNV1a_32_PRIME;
                            hash ^= value;
                        */
                        hashInstructions.Add(il.Create(OpCodes.Ldc_I4, FNV1a_32_PRIME));
                        hashInstructions.Add(il.Create(OpCodes.Mul));


                        hashInstructions.Add(il.Create(OpCodes.Ldarg, val));
                        // Since we need to find the offset to nested members we need to chain field loads
                        hashInstructions.AddRange(fieldLoadChain);

                        if (isFixed)
                        {
                            hashInstructions.Add(il.Create(OpCodes.Ldflda, AssemblyDefinition.MainModule.ImportReference(field)));
                            hashInstructions.Add(il.Create(OpCodes.Ldind_I4));
                        }
                        else
                        {
                            if (fieldTypeRef.IsPointer && ArchBits == 64)
                            {
                                // Xor top and bottom of pointer
                                //
                                // Bottom 32 Bits
                                hashInstructions.Add(il.Create(OpCodes.Ldfld, AssemblyDefinition.MainModule.ImportReference(field)));
                                hashInstructions.Add(il.Create(OpCodes.Conv_I8)); // do I need this if we know the ptr is 64-bit
                                hashInstructions.Add(il.Create(OpCodes.Ldc_I4_M1)); // 0x00000000FFFFFFFF
                                hashInstructions.Add(il.Create(OpCodes.Conv_I8));

                                hashInstructions.Add(il.Create(OpCodes.And));

                                // Top 32 bits
                                hashInstructions.Add(il.Create(OpCodes.Ldarg, val));
                                hashInstructions.AddRange(fieldLoadChain);
                                hashInstructions.Add(il.Create(OpCodes.Ldfld, AssemblyDefinition.MainModule.ImportReference(field)));
                                hashInstructions.Add(il.Create(OpCodes.Conv_I8)); // do I need this if we know the ptr is 64-bit
                                hashInstructions.Add(il.Create(OpCodes.Ldc_I4, 32));
                                hashInstructions.Add(il.Create(OpCodes.Shr_Un));
                                hashInstructions.Add(il.Create(OpCodes.Ldc_I4_M1)); // 0x00000000FFFFFFFF
                                hashInstructions.Add(il.Create(OpCodes.Conv_I8));
                                hashInstructions.Add(il.Create(OpCodes.And));

                                hashInstructions.Add(il.Create(OpCodes.Xor));
                            }
                            else
                            {
                                hashInstructions.Add(il.Create(OpCodes.Ldfld, AssemblyDefinition.MainModule.ImportReference(field)));
                            }
                        }

                        // Subtle behavior. Aside from pointer types, we only load the first 4 bytes of the field.
                        // Makes hashing fast and simple, at the cost of more hash collisions.
                        hashInstructions.Add(il.Create(OpCodes.Conv_I4));
                        hashInstructions.Add(il.Create(OpCodes.Xor));
                    }
                    else if (fieldTypeRef.IsValueType())
                    {
                        // Workaround: We shouldn't need to special case for System.Guid however accessing the private members of types in mscorlib
                        // is problematic as eventhough we may elevate the field permissions in a new mscorlib assembly, Windows may load the assembly from the
                        // Global Assembly Cache regardless resulting in us throwing FieldAccessExceptions
                        if (fieldTypeRef.FullName == "System.Guid")
                        {
                            /*
                             Equivalent to:
                                hash *= FNV1a_32_PRIME;
                                hash ^= value;
                            */
                            hashInstructions.Add(il.Create(OpCodes.Ldc_I4, FNV1a_32_PRIME));
                            hashInstructions.Add(il.Create(OpCodes.Mul));

                            hashInstructions.Add(il.Create(OpCodes.Ldarg, val));
                            // Since we need to find the offset to nested members we need to chain field loads
                            hashInstructions.AddRange(fieldLoadChain);

                            hashInstructions.Add(il.Create(OpCodes.Ldflda, AssemblyDefinition.MainModule.ImportReference(field)));
                            hashInstructions.Add(il.Create(OpCodes.Call, AssemblyDefinition.MainModule.ImportReference(m_SystemGuidHashFn)));
                            hashInstructions.Add(il.Create(OpCodes.Xor));
                        }
                        else
                        {
                            fieldLoadChain.Add(Instruction.Create(OpCodes.Ldfld, AssemblyDefinition.MainModule.ImportReference(field)));
                            GenerateHashFunctionRecurse(il, hashInstructions, fieldLoadChain, val, fieldTypeRef);
                            fieldLoadChain.RemoveAt(fieldLoadChain.Count - 1);
                        }
                    }
                }
            }
        }

        void CalculateMemoryOrderingAndStableHash(TypeReference typeRef, out ulong memoryOrder, out ulong stableHash)
        {
            if (typeRef == null)
            {
                memoryOrder = 0;
                stableHash = 0;
                return;
            }

            stableHash = typeRef.CalculateStableTypeHash();
            memoryOrder = stableHash; // They are equivalent unless overridden below

            var typeDef = typeRef.Resolve();
            if (typeDef.CustomAttributes.Count > 0)
            {
                var forcedMemoryOrderAttribute = typeDef.CustomAttributes.FirstOrDefault(ca => ca.Constructor.DeclaringType.Name == nameof(ForcedMemoryOrderingAttribute));
                if (forcedMemoryOrderAttribute != null)
                {
                    memoryOrder = (ulong)forcedMemoryOrderAttribute.ConstructorArguments
                        .First(arg => arg.Type.MetadataType == MetadataType.UInt64)
                        .Value;
                }
            }
        }

        bool HasNativeContainer(TypeReference type)
        {
            if (type.IsPrimitive)
                return false;
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                if (elementType != null)
                    return HasNativeContainer(elementType);
                return false;
            }

            var typeDef = type.Resolve();
            if (typeDef.HasAttribute(typeof(NativeContainerAttribute).FullName))
                return true;

            // The incoming `type` should be a full generic instance.  This genericResolver
            // will help us resolve the generic parameters of any of its fields
            var genericResolver = TypeResolver.For(type);

            foreach (var typeField in typeDef.Fields)
            {
                // 1) enums which infinitely recurse because the values in the enum are of the same enum type
                // 2) statics which infinitely recurse themselves (Such as vector3.zero.zero.zero.zero)
                if (typeField.IsStatic)
                    continue;

                var genericResolvedFieldType = genericResolver.ResolveFieldType(typeField);
                if (genericResolvedFieldType.IsGenericParameter)
                    continue;

                // only recurse into things that are straight values; no pointers or byref values
                if (genericResolvedFieldType.IsValueType() && !genericResolvedFieldType.IsPrimitive)
                {
                    // make sure we iterate the FieldType with all generic params resolved
                    if (HasNativeContainer(genericResolvedFieldType))
                        return true;
                }
            }

            return false;
        }

        // True when a component is valid to using in world serialization. A component IsSerializable when it is valid to blit
        // the data across storage media. Thus components containing pointers have an IsSerializable of false as the component
        // is blittable but no longer valid upon deserialization.
        private (bool isSerializable, bool hasChunkSerializableAttribute) IsTypeValidForSerialization(TypeReference type)
        {
            var result = (false, false);
            if (m_ChunkSerializableCache.TryGetValue(type, out result))
                return result;

            var typeDef = type.Resolve();
            if (typeDef.HasAttribute(typeof(ChunkSerializableAttribute).FullName))
            {
                result = (true, true);
                m_ChunkSerializableCache.Add(type, result);
                return result;
            }

            var genericResolver = TypeResolver.For(type);
            foreach (var typeField in typeDef.Fields)
            {
                if (typeField.IsStatic)
                    continue;

                var fieldType = genericResolver.ResolveFieldType(typeField);
                if (fieldType.IsGenericParameter)
                    continue;

                if (fieldType.IsPointer || (fieldType == m_SystemIntPtrRef || fieldType == m_SystemUIntPtrRef))
                {
                    m_ChunkSerializableCache.Add(type, result);
                    return result;
                }
                else if (fieldType.IsValueType() && !fieldType.IsPrimitive && !fieldType.IsEnum())
                {
                    if (!IsTypeValidForSerialization(fieldType).isSerializable)
                    {
                        m_ChunkSerializableCache.Add(type, result);
                        return result;
                    }
                }
            }

            result = (true, false);
            m_ChunkSerializableCache.Add(type, result);
            return result;
        }

        // A component type is "chunk serializable" if it meets the following rules:
        // - It is decorated with [ChunkSerializable] attribute (this is an override for all other rules)
        // - The type is blittable AND
        // - The type does not contain any pointer types (including IntPtr) AND
        // - If the type is a shared component, it does not contain an entity reference
        private bool IsComponentChunkSerializable(TypeReference type, TypeCategory category, bool hasEntityReference)
        {
            var isSerializable = IsTypeValidForSerialization(type);

            // Shared Components are expected to be handled specially when serializing and are not required to be blittable.
            // They cannot contain an entity reference today as they are not patched however for unmanaged components
            // we should be able to correct this behaviour DOTS-7613
            if (!isSerializable.hasChunkSerializableAttribute && category == TypeCategory.ISharedComponentData && hasEntityReference)
            {
                isSerializable.isSerializable = false;
            }

            return isSerializable.isSerializable;
        }

        internal unsafe TypeGenInfo CreateTypeGenInfo(TypeReference typeRef, TypeCategory typeCategory)
        {
            // We will be referencing this type in generated functions to make it's type internal if it's private
            var typeDef = typeRef.Resolve();
            typeDef.MakeTypeInternal();

            TypeUtils.AlignAndSize alignAndSize = new TypeUtils.AlignAndSize();
            List<int> entityOffsets = new List<int>();
            List<int> blobAssetRefOffsets = new List<int>();
            List<int> weakAssetRefOffsets = new List<int>();
            bool mightHaveEntityRefs = false;
            bool mightHaveBlobRefs = false;
            bool hasNativeContainer = HasNativeContainer(typeRef);
            var isIRefCounted = typeDef.Interfaces.Any(i => i.InterfaceType.Name.Contains("IRefCounted"));
            var isIEquatable = typeDef.Interfaces.Any(i => i.InterfaceType.Name.Contains("IEquatable"));
            bool isManaged = typeDef != null &&
                             (typeRef.IsManagedType(
                                  ref mightHaveEntityRefs,
                                  ref mightHaveBlobRefs));

            if (!isManaged)
            {
                entityOffsets = TypeUtils.GetEntityFieldOffsets(typeRef, ArchBits);
                if (entityOffsets.Count > 0)
                    mightHaveEntityRefs = true;
                blobAssetRefOffsets = TypeUtils.GetFieldOffsetsOf("Unity.Entities.BlobAssetReferenceData", typeRef, ArchBits);
                if (blobAssetRefOffsets.Count > 0)
                    mightHaveBlobRefs = true;
                weakAssetRefOffsets = TypeUtils.GetFieldOffsetsOf("Unity.Entities.Serialization.UntypedWeakReferenceId", typeRef, ArchBits);
                alignAndSize = TypeUtils.AlignAndSizeOfType(typeRef, ArchBits);
            }
            else if (isManaged && IsNetDots
                // Todo: ISharedComponents are currently commonly managed as this was the only mechanism for storing managed
                // data in ECS. Until Unmanaged Shared Component are available we allow managed shared components in NET_DOTS
                // DOTSR-1865
                && typeCategory != TypeCategory.ISharedComponentData)
            {
                throw new ArgumentException($"Found a managed component '{typeRef.FullName}'. Managed components are not supported when building for the Tiny configuration. Change the type to be a struct or build with the NetStandard 2.1 configuration.");
            }

            int typeIndex = m_TotalTypeCount++;
            bool isCleanupBufferElement = typeDef.Interfaces.Select(i => i.InterfaceType.Name).Contains(nameof(ICleanupBufferElementData));
            bool isCleanupSharedComponent = typeDef.Interfaces.Select(i => i.InterfaceType.Name).Contains(nameof(ICleanupSharedComponentData));
            bool isCleanupComponent = typeDef.Interfaces.Select(i => i.InterfaceType.Name).Contains(nameof(ICleanupComponentData)) || isCleanupSharedComponent || isCleanupBufferElement;

            bool isEnableable = typeDef.Interfaces.Any(i => i.InterfaceType.Name.Contains(nameof(IEnableableComponent)));

            // if it's a managed component and it's not already obviously enableable, look for IEnableableComponent on
            // its base classes.
            if (isManaged && !isEnableable)
            {
                var mytype = typeDef.BaseType?.Resolve();
                while (mytype != null)
                {
                    if (mytype.Interfaces.Any(i => i.InterfaceType.Name.Contains(nameof(IEnableableComponent))))
                    {
                        isEnableable = true;
                        break;
                    }

                    mytype = mytype.BaseType?.Resolve();
                }
            }

            //check typeOverride attributes
            if (typeDef.CustomAttributes.Count > 0)
            {
                var overrideAttribute = typeDef.CustomAttributes.FirstOrDefault(ca =>
                    ca.Constructor.DeclaringType.Name == nameof(TypeOverridesAttribute));

                var hasOverrideAttribute = overrideAttribute != null;

                if (hasOverrideAttribute && (bool)overrideAttribute.ConstructorArguments[0].Value)
                    mightHaveEntityRefs = false;
                if (hasOverrideAttribute && (bool)overrideAttribute.ConstructorArguments[1].Value)
                    mightHaveBlobRefs = false;
            }

            bool isChunkSerializable = IsComponentChunkSerializable(typeRef, typeCategory, mightHaveEntityRefs);

            if (alignAndSize.empty || typeCategory == TypeCategory.ISharedComponentData)
                typeIndex |= ZeroSizeInChunkTypeFlag;

            if (typeCategory == TypeCategory.ISharedComponentData)
                typeIndex |= SharedComponentTypeFlag;

            if (isCleanupComponent)
                typeIndex |= CleanupComponentTypeFlag;

            if (isCleanupSharedComponent)
                typeIndex |= CleanupSharedComponentTypeFlag;

            if (typeCategory == TypeCategory.BufferData)
                typeIndex |= BufferComponentTypeFlag;

            if (!mightHaveEntityRefs)
                typeIndex |= HasNoEntityReferencesFlag;

            if (hasNativeContainer)
                typeIndex |= HasNativeContainerFlag;

            if (isManaged)
                typeIndex |= ManagedComponentTypeFlag;

            if (isEnableable)
                typeIndex |= EnableableComponentFlag;

            if (isIRefCounted)
                typeIndex |= IRefCountedComponentFlag;

            if (isIEquatable)
                typeIndex |= IEquatableTypeFlag;

            if (!isChunkSerializable)
                typeIndex |= IsNotChunkSerializableTypeFlag;

            if (typeCategory == TypeCategory.ISharedComponentData && isManaged && !isIEquatable)
                throw new ArgumentException($"Type '{typeRef.FullName}' is a ISharedComponentData and has managed references, you must implement IEquatable<T>");

            CalculateMemoryOrderingAndStableHash(typeRef, out ulong memoryOrdering, out ulong stableHash);

            // Determine if there is a special buffer capacity set for the type
            int bufferCapacity = -1;
            if (typeCategory == TypeCategory.BufferData && typeDef.CustomAttributes.Count > 0)
            {
                var forcedCapacityAttribute = typeDef.CustomAttributes.FirstOrDefault(ca => ca.Constructor.DeclaringType.Name == "InternalBufferCapacityAttribute");
                if (forcedCapacityAttribute != null)
                {
                    bufferCapacity = (int)forcedCapacityAttribute.ConstructorArguments
                        .First(arg => arg.Type.MetadataType == MetadataType.Int32)
                        .Value;
                }
            }

            // Determine max chunk capacity, if specified
            int maxChunkCapacity = 128;
            if (typeDef.CustomAttributes.Count > 0)
            {
                var maxChunkCapacityAttribute = typeDef.CustomAttributes.FirstOrDefault(ca => ca.Constructor.DeclaringType.Name == nameof(MaximumChunkCapacityAttribute));
                if (maxChunkCapacityAttribute != null)
                {
                    var chunkCapacityFromAttribute = (int)maxChunkCapacityAttribute.ConstructorArguments
                        .First(arg => arg.Type.MetadataType == MetadataType.Int32)
                        .Value;
                    if (chunkCapacityFromAttribute < maxChunkCapacity)
                        maxChunkCapacity = chunkCapacityFromAttribute;
                }
            }

            int elementSize = 0;
            int sizeInChunk = 0;
            int alignment = 0;
            if (typeCategory != TypeCategory.ISharedComponentData && !isManaged)
            {
                elementSize = alignAndSize.empty ? 0 : alignAndSize.size;
                sizeInChunk = elementSize;
                // This is the native type alignment, which may be useful eventually but for now
                // alignment for TypeInfo is purely referring to how the type aligns in chunk memory
                //alignment = alignAndSize.align;
                alignment = CalculateAlignmentInChunk(alignAndSize.size);
            }

            if (typeCategory != TypeCategory.ISharedComponentData && isManaged)
            {
                // Managed components are stored as an integer index inside the chunk
                sizeInChunk = alignment = 4;
            }

            if (typeCategory == TypeCategory.BufferData)
            {
                if (elementSize == 0)
                    throw new ArgumentException($"Component '{typeRef.FullName}' is used as a buffer data, but has size == 0.");

                // If we haven't overridden the bufferSize via an attribute
                if (bufferCapacity == -1)
                {
                    bufferCapacity = DefaultBufferCapacityNumerator / elementSize;
                }

                int bufferHeaderSize = sizeof(BufferHeader);
                sizeInChunk = (bufferCapacity * elementSize) + bufferHeaderSize;
            }

            var fieldInfoLookUp = new FieldInfoLookUp { Count = -1, Index = -1 };
            if (IsToolConfig)
                fieldInfoLookUp = GenerateFieldInfos(typeRef);

            var typeGenInfo = new TypeGenInfo()
            {
                TypeReference = typeRef,
                TypeIndex = typeIndex,
                TypeCategory = typeCategory,
                EntityOffsets = entityOffsets,
                EntityOffsetIndex = m_TotalEntityOffsetCount,
                BlobAssetRefOffsets = blobAssetRefOffsets,
                BlobAssetRefOffsetIndex = m_TotalBlobAssetRefOffsetCount,
                WeakAssetRefOffsets = weakAssetRefOffsets,
                WeakAssetRefOffsetIndex = m_TotalWeakAssetRefOffsetCount,
                WriteGroupTypes = new HashSet<TypeReference>(),
                WriteGroupsIndex = 0,
                IsManaged = isManaged,
                AlignAndSize = alignAndSize,
                BufferCapacity = bufferCapacity,
                MaxChunkCapacity = maxChunkCapacity,
                ElementSize = elementSize,
                SizeInChunk = sizeInChunk,
                Alignment = alignment,
                StableHash = stableHash,
                MemoryOrdering = memoryOrdering,
                FieldInfoCount = fieldInfoLookUp.Count,
                FieldInfoIndex = fieldInfoLookUp.Index,
                MightHaveEntityReferences = mightHaveEntityRefs,
                MightHaveBlobAssetReferences = mightHaveBlobRefs
            };

            m_TotalEntityOffsetCount += entityOffsets.Count;
            m_TotalBlobAssetRefOffsetCount += blobAssetRefOffsets.Count;
            m_TotalWeakAssetRefOffsetCount += weakAssetRefOffsets.Count;

            return typeGenInfo;
        }

        internal void PopulateWriteGroups(TypeGenInfoList typeGenInfoList)
        {
            var writeGroupMap = new Dictionary<TypeReference, HashSet<TypeReference>>();

            foreach (var typeGenInfo in typeGenInfoList)
            {
                var typeRef = typeGenInfo.TypeReference;
                var typeDef = typeRef.Resolve();
                foreach (var attribute in typeDef.CustomAttributes.Where(a => a.AttributeType.Name == nameof(WriteGroupAttribute) && a.AttributeType.Namespace == "Unity.Entities"))
                {
                    if (!writeGroupMap.ContainsKey(typeRef))
                    {
                        var targetList = new HashSet<TypeReference>();
                        writeGroupMap.Add(typeRef, targetList);
                    }
                    var targetType = attribute.ConstructorArguments[0].Value as TypeReference;
                    writeGroupMap[typeRef].Add(targetType);
                }
            }

            m_TotalWriteGroupCount = 0;
            for (int i = 0; i < typeGenInfoList.Count; ++i)
            {
                var typeGenInfo = typeGenInfoList[i];

                if (writeGroupMap.TryGetValue(typeGenInfo.TypeReference, out var writeGroups))
                {
                    typeGenInfo.WriteGroupTypes = writeGroups;
                    typeGenInfo.WriteGroupsIndex = m_TotalWriteGroupCount;
                    typeGenInfoList[i] = typeGenInfo;
                    m_TotalWriteGroupCount += writeGroups.Count;
                }
            }
        }
    }
}
#endif
