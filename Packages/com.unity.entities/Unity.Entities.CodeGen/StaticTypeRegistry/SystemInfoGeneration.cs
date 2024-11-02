#if UNITY_DOTSRUNTIME
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;
using TypeGenInfoList = System.Collections.Generic.List<Unity.Entities.CodeGen.StaticTypeRegistryPostProcessor.TypeGenInfo>;
using SystemList = System.Collections.Generic.List<Mono.Cecil.TypeDefinition>;
using Unity.Cecil.Awesome;
using static Unity.Entities.CodeGen.MonoExtensions;

namespace Unity.Entities.CodeGen
{
    internal partial class StaticTypeRegistryPostProcessor : EntitiesILPostProcessor
    {
        public List<int> GetSystemTypeFlagsList(List<TypeReference> systems)
        {
            var inGroup = systems.Select(s =>
            {
                var flags = 0;
                var resolvedSystemType = s.Resolve();
                if (resolvedSystemType
                    .IsChildTypeOf(AssemblyDefinition.MainModule.ImportReference(typeof(ComponentSystemGroup))
                        .Resolve()))
                    flags |= TypeManager.SystemTypeInfo.kIsSystemGroupFlag;
                var unused1 = false;
                var unused2 = false;
                if (s.IsManagedType(ref unused1, ref unused2))
                    flags |= TypeManager.SystemTypeInfo.kIsSystemManagedFlag;

                if (s.TypeImplements(typeof(ISystemStartStop)))
                    flags |= TypeManager.SystemTypeInfo.kIsSystemISystemStartStopFlag;
                return flags;
            }).ToList();
            return inGroup;
        }

        static WorldSystemFilterFlags GetChildDefaultFilterFlag(TypeDefinition typeDef)
        {
            var flags =  WorldSystemFilterFlags.Default;
            var filterFlagsAttribute = typeDef.CustomAttributes.FirstOrDefault(ca => ca.AttributeType.Name == nameof(WorldSystemFilterAttribute) && ca.ConstructorArguments.Count >= 2);
            if(filterFlagsAttribute != null)
            {
                // override the default value if flags are provided
                flags = (WorldSystemFilterFlags) filterFlagsAttribute.ConstructorArguments[1].Value;
            }
            else if (typeDef.BaseType != null) // Traverse the hierarchy to fetch a flags from an ancestor if we can't find one on this type
                flags = (WorldSystemFilterFlags) GetChildDefaultFilterFlag(typeDef.BaseType.Resolve());
            return flags;
        }
        static WorldSystemFilterFlags GetParentGroupDefaultFilterFlags(TypeDefinition typeDef)
        {
            var baseTypeDef = typeDef;
            List<CustomAttribute> groupAttributes = new List<CustomAttribute>();
            while (baseTypeDef != null)
            {
                groupAttributes.Add(baseTypeDef.CustomAttributes.Where(ca => ca.AttributeType.Name == nameof(UpdateInGroupAttribute) && ca.ConstructorArguments.Count == 1));
                baseTypeDef = baseTypeDef.BaseType?.Resolve();
            }
            if (groupAttributes.Count == 0)
            {
                // Fallback default
                return WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation;
            }

            WorldSystemFilterFlags systemFlags = default;
            foreach (var uig in groupAttributes)
            {
                var groupTypeDef = ((TypeReference)uig.ConstructorArguments[0].Value).Resolve();
                var groupFlags = GetChildDefaultFilterFlag(groupTypeDef);
                if ((groupFlags & WorldSystemFilterFlags.Default) != 0)
                {
                    groupFlags &= ~WorldSystemFilterFlags.Default;
                    groupFlags |= GetParentGroupDefaultFilterFlags(groupTypeDef);
                }
                systemFlags |= groupFlags;
            }
            return systemFlags;
        }

        int GetFilterFlag(TypeDefinition typeDef, bool isBase = false)
        {
            // If no flags are given we assume the default world
            var flags =  WorldSystemFilterFlags.Default;
            var filterFlagsAttribute = typeDef.CustomAttributes.FirstOrDefault(ca => ca.AttributeType.Name == nameof(WorldSystemFilterAttribute) && ca.ConstructorArguments.Count >= 1);
            if(filterFlagsAttribute != null)
            {
                // override the default value if flags are provided
                flags = (WorldSystemFilterFlags) filterFlagsAttribute.ConstructorArguments[0].Value;
            }
            else if (typeDef.BaseType != null) // Traverse the hierarchy to fetch a flags from an ancestor if we can't find one on this type
                flags = (WorldSystemFilterFlags) GetFilterFlag(typeDef.BaseType.Resolve(), true);

            if (!isBase && (flags & WorldSystemFilterFlags.Default) != 0)
            {
                flags &= ~WorldSystemFilterFlags.Default;
                flags |= GetParentGroupDefaultFilterFlags(typeDef);
            }

            if(typeDef.HasAttribute("UnityEngine.ExecuteAlways"))
                flags |= WorldSystemFilterFlags.Editor;

            return (int) flags;
        }

        public List<int> GetSystemFilterFlagList(List<TypeReference> systems)
        {
            var flags = systems.Select(s => GetFilterFlag(s.Resolve())).ToList();
            return flags;
        }

        public static List<string> GetSystemNames(List<TypeReference> systems)
        {
            return systems.Select(s => s.FullName).ToList();
        }

        public MethodDefinition InjectGetSystemAttributes(List<TypeReference> systems)
        {
            var getSystemAttributesFn = new MethodDefinition(
                "GetSystemAttributes",
                MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig,
                AssemblyDefinition.MainModule.ImportReference(typeof(Attribute).MakeArrayType()));

            getSystemAttributesFn.Parameters.Add(
                new ParameterDefinition("systemType",
                    ParameterAttributes.None,
                    AssemblyDefinition.MainModule.ImportReference(typeof(Type))));

            getSystemAttributesFn.Body.InitLocals = true;
            getSystemAttributesFn.Body.SimplifyMacros();

            var bc = getSystemAttributesFn.Body.Instructions;
            foreach (var sysRef in systems)
            {
                var sysDef = sysRef.Resolve();
                bc.Add(Instruction.Create(OpCodes.Ldarg_0));
                bc.Add(Instruction.Create(OpCodes.Ldtoken, AssemblyDefinition.MainModule.ImportReference(sysRef)));
                bc.Add(Instruction.Create(OpCodes.Call, m_GetTypeFromHandleFnRef));
                // Stack: argtype Type
                bc.Add(Instruction.Create(OpCodes.Ceq));
                // Stack: bool
                int branchToNext = bc.Count;
                bc.Add(Instruction.Create(OpCodes.Nop));    // will be: Brfalse_S nextTestCase

                var attrList = sysDef.CustomAttributes;

                // If ther whole assembly is disabled then add the disableautocreation attribute
                // if the type isn't already tagged for being disabled (we don't want to add it twice)
                var disableAutoCreationAttr = sysRef.Module.Assembly.CustomAttributes.FirstOrDefault(ca => ca.AttributeType.Name == nameof(DisableAutoCreationAttribute));
                if (disableAutoCreationAttr != null && attrList.FirstOrDefault(a=>a.AttributeType.Name == nameof(DisableAutoCreationAttribute)) == null)
                    attrList.Add(disableAutoCreationAttr);

                int arrayLen = attrList.Count;
                bc.Add(Instruction.Create(OpCodes.Ldc_I4, arrayLen));
                // Stack: arrayLen
                bc.Add(Instruction.Create(OpCodes.Newarr, AssemblyDefinition.MainModule.ImportReference(typeof(Attribute))));
                // Stack: array[]

                for (int i = 0; i < attrList.Count; ++i)
                {
                    var attr = attrList[i];

                    // The stelem.ref will gobble up the array ref we need to return, so dupe it.
                    bc.Add(Instruction.Create(OpCodes.Dup));
                    bc.Add(Instruction.Create(OpCodes.Ldc_I4, i));       // the index we will write
                                                                         // Stack: array[] array[] array-index

                    // CustomAttributes are usually injected into the ctor of the type being decorated, however for our purposes we want to construct
                    // an Attribute[] with all the custom initialization the decorated type would have. As such we do two passes: construct the attribute using
                    // the constructor call writer (including constant arguments), and then after construction, initialize fields using field CustomAttributeNamedArguments.
                    // Since both calls require generating IL based on the actual types used, we take a narrow approach of only supporting the subset of types we know we
                    // need to handle (Type, and bool) currently.
                    foreach (var ca in attr.ConstructorArguments)
                    {
                        InjectLoadFromCustomArgument(bc, ca);
                    }

                    // Construct the attribute; push it on the list.
                    var ctor = AssemblyDefinition.MainModule.ImportReference(attr.Constructor);
                    bc.Add(Instruction.Create(OpCodes.Newobj, ctor));

                    foreach (var field in attr.Fields)
                    {
                        // custom attributes can set fields in the constructor so scan the fields for arguments
                        if (field.Argument.Value != null)
                        {
                            // Copy the element on the stack (the attribute) so we can store into it's fields
                            bc.Add(Instruction.Create(OpCodes.Dup));
                            InjectLoadFromCustomArgument(bc, field.Argument);

                            var attributeTypeDef = attr.AttributeType.Resolve();
                            var attributeField = attributeTypeDef.Fields.Single(f => f.Name == field.Name);
                            bc.Add(Instruction.Create(OpCodes.Stfld, AssemblyDefinition.MainModule.ImportReference(attributeField)));
                        }
                    }

                    // Stack: array[] array[] array-index value(object)
                    bc.Add(Instruction.Create(OpCodes.Stelem_Ref));
                    // Stack: array[]
                }

                // Stack: array[]
                bc.Add(Instruction.Create(OpCodes.Ret));

                // Put a no-op to start the next test.
                var nextTest = Instruction.Create(OpCodes.Nop);
                bc.Add(nextTest);

                // And go back and patch the IL to jump to the next test no-op just created.
                bc[branchToNext] = Instruction.Create(OpCodes.Brfalse_S, nextTest);
            }
            bc.Add(Instruction.Create(OpCodes.Ldstr, "FATAL: GetSystemAttributes asked to create an unknown Type."));
            var arguementExceptionCtor = AssemblyDefinition.MainModule.ImportReference(typeof(ArgumentException)).Resolve().GetConstructors()
                .Single(c => c.Parameters.Count == 1 && c.Parameters[0].ParameterType.MetadataType == MetadataType.String);
            bc.Add(Instruction.Create(OpCodes.Newobj, AssemblyDefinition.MainModule.ImportReference(arguementExceptionCtor)));
            bc.Add(Instruction.Create(OpCodes.Throw));

            getSystemAttributesFn.Body.OptimizeMacros();

            return getSystemAttributesFn;
        }

        void InjectLoadFromCustomArgument(Mono.Collections.Generic.Collection<Instruction> instructions, CustomAttributeArgument customArgument)
        {
            var caType = customArgument.Type;
            var caValue = customArgument.Value;

            /*
                https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/attributes#positional-and-named-parameters
                The types of positional and named parameters for an attribute class are limited to the attribute parameter types, which are:
                    - One of the following types: bool, byte, char, double, float, int, long, sbyte, short, string, uint, ulong, ushort.
                    - The type object.
                    - The type System.Type.
                    - An enum type, provided it has public accessibility and the types in which it is nested (if any) also have public accessibility (Attribute specification).
                    - Single-dimensional arrays of the above types.
                    - A constructor argument or public field which does not have one of these types, cannot be used as a positional or named parameter in an attribute specification.
            */

            // NOTE: Any odd looking double casts (such as "(int)(uint)") is the result of C#'s type system and cecil's lack of proper overloads for its
            // Instruction.Create type safety system which will complain which will promote uint to ulongs as that is the best type for the container given cecil only provides an 'int' overload
            // but a ulong isn't correct for the operand we are passing. Bleh, it's ungly and misleading but now at least correct and safe.
            switch (caType.MetadataType)
            {
                case MetadataType.Boolean:
                    instructions.Add(Instruction.Create(OpCodes.Ldc_I4, (bool) caValue ? 1 : 0)); break;
                case MetadataType.Byte:
                    instructions.Add(Instruction.Create(OpCodes.Ldc_I4, (byte) caValue)); break;
                case MetadataType.Char:
                    instructions.Add(Instruction.Create(OpCodes.Ldc_I4, (char) caValue)); break;
                case MetadataType.Double:
                    instructions.Add(Instruction.Create(OpCodes.Ldc_R8, (double) caValue)); break;
                case MetadataType.Single:
                    instructions.Add(Instruction.Create(OpCodes.Ldc_R4, (float) caValue)); break;
                case MetadataType.UInt16:
                    instructions.Add(Instruction.Create(OpCodes.Ldc_I4, (int)(ushort) caValue)); break;
                case MetadataType.Int16:
                    instructions.Add(Instruction.Create(OpCodes.Ldc_I4, (int)(short) caValue)); break;
                case MetadataType.UInt32:
                    instructions.Add(Instruction.Create(OpCodes.Ldc_I4, (int)(uint) caValue)); break;
                case MetadataType.Int32:
                    instructions.Add(Instruction.Create(OpCodes.Ldc_I4, (int) caValue)); break;
                case MetadataType.UInt64:
                    instructions.Add(Instruction.Create(OpCodes.Ldc_I8, (long)(ulong) caValue)); break;
                case MetadataType.Int64:
                    instructions.Add(Instruction.Create(OpCodes.Ldc_I8, (long) caValue)); break;
                case MetadataType.SByte:
                    instructions.Add(Instruction.Create(OpCodes.Ldc_I4, (sbyte) caValue)); break;
                case MetadataType.String:
                    instructions.Add(Instruction.Create(OpCodes.Ldstr, (string) caValue)); break;
                case MetadataType.Class:
                {
                    if (caValue is TypeReference)
                    {
                        instructions.Add(Instruction.Create(OpCodes.Ldtoken, AssemblyDefinition.MainModule.ImportReference((TypeReference)caValue)));
                        instructions.Add(Instruction.Create(OpCodes.Call, m_GetTypeFromHandleFnRef));
                    }
                    break;
                }
                case MetadataType.Array:
                    throw new NotImplementedException("Single-array attribute support needs to be implemented");
                case MetadataType.ValueType:
                {
                    if (caType.IsEnum())
                    {
                        var td = caType.Resolve();
                        var enumTypeRef = td.GetEnumUnderlyingType();
                        if (enumTypeRef.MetadataType == MetadataType.UInt16)
                            instructions.Add(Instruction.Create(OpCodes.Ldc_I4, (int)(ushort) caValue));
                        else if (enumTypeRef.MetadataType == MetadataType.Int16)
                            instructions.Add(Instruction.Create(OpCodes.Ldc_I4, (int)(short) caValue));
                        else if (enumTypeRef.MetadataType == MetadataType.UInt32)
                            instructions.Add(Instruction.Create(OpCodes.Ldc_I4, (int)(uint) caValue));
                        else if (enumTypeRef.MetadataType == MetadataType.Int32)
                            instructions.Add(Instruction.Create(OpCodes.Ldc_I4, (int) caValue));
                        else if (enumTypeRef.MetadataType == MetadataType.UInt64)
                            instructions.Add(Instruction.Create(OpCodes.Ldc_I8, (long)(ulong) caValue));
                        else if(enumTypeRef.MetadataType == MetadataType.Int64)
                            instructions.Add(Instruction.Create(OpCodes.Ldc_I8, (long) caValue));
                    }
                    break;
                }
                default:
                    throw new ArgumentException($"Invalid custom argument type for {caType.FullName}");
            }
        }

        public MethodDefinition InjectCreateSystem(List<TypeReference> systems)
        {
            var createSystemsFunction = new MethodDefinition(
                "CreateSystem",
                MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig,
                AssemblyDefinition.MainModule.ImportReference(typeof(object)));

            createSystemsFunction.Parameters.Add(
                new ParameterDefinition("systemType",
                    ParameterAttributes.None,
                    AssemblyDefinition.MainModule.ImportReference(typeof(Type))));

            createSystemsFunction.Body.InitLocals = true;
            var bc = createSystemsFunction.Body.Instructions;

            foreach (var sysRef in systems)
            {
                if (sysRef.IsValueType())
                    continue;

                var sysDef = sysRef.Resolve();
                var constructor = AssemblyDefinition.MainModule.ImportReference(sysDef.GetConstructors()
                    .Single(param => !param.HasParameters && !param.IsStatic));

                bc.Add(Instruction.Create(OpCodes.Ldarg_0));
                bc.Add(Instruction.Create(OpCodes.Ldtoken, AssemblyDefinition.MainModule.ImportReference(sysRef)));
                bc.Add(Instruction.Create(OpCodes.Call, m_GetTypeFromHandleFnRef));
                bc.Add(Instruction.Create(OpCodes.Ceq));
                int branchToNext = bc.Count;
                bc.Add(Instruction.Create(OpCodes.Nop));    // will be: Brfalse_S nextTestCase
                bc.Add(Instruction.Create(OpCodes.Newobj, constructor));
                bc.Add(Instruction.Create(OpCodes.Ret));

                var nextTest = Instruction.Create(OpCodes.Nop);
                bc.Add(nextTest);

                bc[branchToNext] = Instruction.Create(OpCodes.Brfalse_S, nextTest);
            }

            bc.Add(Instruction.Create(OpCodes.Ldstr, "FATAL: CreateSystem asked to create an unknown type. Only subclasses of ComponentSystemBase can be constructed."));
            var argumentExceptionCtor = AssemblyDefinition.MainModule.ImportReference(typeof(ArgumentException)).Resolve().GetConstructors()
                .Single(c => c.Parameters.Count == 1 && c.Parameters[0].ParameterType.MetadataType == MetadataType.String);
            bc.Add(Instruction.Create(OpCodes.Newobj, AssemblyDefinition.MainModule.ImportReference(argumentExceptionCtor)));
            bc.Add(Instruction.Create(OpCodes.Throw));

            return createSystemsFunction;
        }
    }
}
#endif
