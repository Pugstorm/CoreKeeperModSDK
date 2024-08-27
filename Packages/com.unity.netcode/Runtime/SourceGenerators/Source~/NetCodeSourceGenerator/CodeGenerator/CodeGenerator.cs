using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Unity.NetCode.Roslyn;

namespace Unity.NetCode.Generators
{
    //This class must not contains state and must be immutable. All necessary data must come from arguments and context
    internal static class CodeGenerator
    {
        public const string RpcSerializer = "NetCode.RpcCommandSerializer.cs";
        public const string CommandSerializer = "NetCode.CommandDataSerializer.cs";
        public const string ComponentSerializer = "NetCode.GhostComponentSerializer.cs";
        public const string RegistrationSystem = "NetCode.GhostComponentSerializerRegistrationSystem.cs";
        public const string MetaDataRegistrationSystem = "NetCode.GhostComponentMetaDataRegistrationSystem.cs";
        public const string InputSynchronization = "NetCode.InputSynchronization.cs";

        //Namespace generation can be a little tricky.
        //Given the current generated NS: AssemblyName.Generated
        //I assumed the following rules:
        // 1) if the type has no namespace => nothing to consider, is global NS
        // 2) if the type ns namespace has as common ancestor AssemblyName => use type NS
        // 3) if the type ns namespace doesn't have a common prefix with AssemblyName => use type NS
        // 4) if the type ns namespace and AssemblyName has some common prefix => prepend global::
        internal static string GetValidNamespaceForType(string generatedNs, string ns)
        {
            //if it is 0 is still part of the ancestor
            if(generatedNs.IndexOf(ns, StringComparison.Ordinal) <= 0)
                return ns;

            //need to use global to avoid confusion
            return "global::" + ns;
        }

        /// <summary>
        ///     True if we can generate this EXACT type via a known Template.
        ///     If false, we will check its children, and see if we can generate any of those.
        /// </summary>
        /// <remarks>
        /// A type failing this check will NOT prevent it from being serialized.
        /// I.e. This is ONLY to check whether or not we have a template for this EXACT type.
        /// </remarks>
        static bool TryGetTypeTemplate(TypeInformation typeInfo, Context context, out TypeTemplate template)
        {
            template = default;

            var description = typeInfo.Description;
            if (!context.registry.Templates.TryGetValue(description, out template))
            {
                if (description.Attribute.subtype == 0)
                    return false;

                bool foundSubType = false;
                foreach (var myType in context.registry.Templates)
                {
                    if (description.Attribute.subtype == myType.Key.Attribute.subtype)
                    {
                        if (description.Key == myType.Key.Key)
                        {
                            foundSubType = true;
                            break;
                        }
                        context.diagnostic.LogError($"'{context.generatorName}' defines a field '{typeInfo.FieldName}' with GhostField configuration '{description}' with a subtype, but subType '{description.Attribute.subtype}' is registered to a different type ('{myType.Key.TypeFullName}'). Thus, ignoring this field. Did you mean to use a different subType?",
                            typeInfo.Location);
                        return false;
                    }
                }
                if (!foundSubType)
                {
                    context.diagnostic.LogError($"'{context.generatorName}' defines a field '{typeInfo.FieldName}' with GhostField configuration '{description}' with a subtype, but this subType has not been registered. Known subTypes are {context.registry.FormatAllKnownSubTypes()}. Please register your SubType Template in the `UserDefinedTemplates` `TypeRegistry` via an `.additionalfile` (see docs).",
                        typeInfo.Location);
                    return false;
                }
                return false;
            }

            if (template.SupportsQuantization && description.Attribute.quantization < 0)
            {
                context.diagnostic.LogError($"'{context.generatorName}' defines a field '{typeInfo.FieldName}' with GhostField configuration '{description}' which requires a quantization value to be specified, but it has not been. Thus, ignoring the field. To fix, add a quantization value to the GhostField attribute constructor.",
                    typeInfo.Location);
                template = default;
                return false;
            }

            if (!template.SupportsQuantization && description.Attribute.quantization > 0)
            {
                context.diagnostic.LogError($"'{context.generatorName}' defines a field '{typeInfo.FieldName}' with GhostField configuration '{description}' that does not support quantization, but has a quantization value specified. Thus, ignoring the field. To fix, remove the quantization value from the GhostField attribute constructor.",
                    typeInfo.Location);
                template = default;
                return false;
            }

            // TODO: subtype + composite doesn't work atm, we don't pass the subtype=x info down
            // when processing the nested types, so default variant will be used, and given template in the variant
            // will be ignored. Also you might have a normal template and set the composite=true by mistake, but
            // we can't detect this atm
            if (template.Composite && description.Attribute.subtype > 0)
            {
                context.diagnostic.LogError($"'{context.generatorName}' defines a field '{typeInfo.FieldName}' with GhostField configuration '{description}' using an invalid configuration: Subtyped types cannot also be defined as composite, as it is assumed your Template given is the one in use for the whole type. I.e. If you'd like to implement change-bit composition yourself on this type, modify the template directly (at '{template.TemplatePath}').");
                return false;
            }

            context.diagnostic.LogDebug($"'{context.generatorName}' found Template for field '{typeInfo.FieldName}' with GhostField configuration '{description}': '{template}'.");
            return true;
        }

        public static void GenerateRegistrationSystem(Context context)
        {
            //There is nothing to generate in that case. Skip creating an empty system
            if(context.generatedTypes.Count == 0 && context.serializationStrategies.Count == 0)
                return;

            using (new Profiler.Auto("GenerateRegistrationSystem"))
            {
                //Generate the ghost registration
                var registrationSystemCodeGen = context.codeGenCache.GetTemplate(CodeGenerator.RegistrationSystem);
                registrationSystemCodeGen = registrationSystemCodeGen.Clone();
                var replacements = new Dictionary<string, string>(16);

                foreach (var t in context.generatedTypes)
                {
                    replacements["GHOST_NAME"] = t;
                    registrationSystemCodeGen.GenerateFragment("GHOST_COMPONENT_LIST", replacements);
                }

                int selfIndex = 0;
                foreach (var ss in context.serializationStrategies)
                {
                    var typeInfo = ss.TypeInfo;

                    if (typeInfo == null)
                        throw new InvalidOperationException("Must define TypeInfo when using `serializationStrategies.Add`!");

                    if(ss.Hash == "0")
                        context.diagnostic.LogError($"Setting invalid hash on variantType {ss.VariantTypeName} to {ss.Hash}!");

                    var displayName = ss.DisplayName ?? ss.VariantTypeName;
                    displayName = SmartTruncateDisplayName(displayName);

                    var isDefaultSerializer = string.IsNullOrWhiteSpace(ss.VariantTypeName) || ss.VariantTypeName == ss.ComponentTypeName;

                    replacements["VARIANT_TYPE"] = ss.VariantTypeName;
                    replacements["GHOST_COMPONENT_TYPE"] = ss.ComponentTypeName;
                    replacements["GHOST_VARIANT_DISPLAY_NAME"] = displayName;
                    replacements["GHOST_VARIANT_HASH"] = ss.Hash;
                    replacements["SELF_INDEX"] = selfIndex++.ToString();
                    replacements["VARIANT_IS_SERIALIZED"] = ss.IsSerialized ? "1" : "0";
                    replacements["GHOST_IS_DEFAULT_SERIALIZER"] = isDefaultSerializer ? "1" : "0";
                    replacements["GHOST_SEND_CHILD_ENTITY"] = typeInfo.GhostAttribute != null && typeInfo.GhostAttribute.SendDataForChildEntity ? "1" : "0";
                    replacements["TYPE_IS_INPUT_COMPONENT"] = typeInfo.ComponentType == ComponentType.Input ? "1" : "0";
                    replacements["TYPE_IS_INPUT_BUFFER"] = typeInfo.ComponentType == ComponentType.CommandData ? "1" : "0";
                    replacements["TYPE_IS_TEST_VARIANT"] = typeInfo.IsTestVariant ? "1" : "0";
                    replacements["TYPE_HAS_DONT_SUPPORT_PREFAB_OVERRIDES_ATTRIBUTE"] = typeInfo.HasDontSupportPrefabOverridesAttribute ? "1" : "0";
                    replacements["GHOST_PREFAB_TYPE"] = ss.GhostAttribute != null ? $"GhostPrefabType.{ss.GhostAttribute.PrefabType.ToString().Replace(",", "|GhostPrefabType.")}" : "GhostPrefabType.All";

                    if (typeInfo.GhostAttribute != null)
                    {
                        if ((typeInfo.GhostAttribute.PrefabType & GhostPrefabType.Client) == GhostPrefabType.InterpolatedClient)
                            replacements["GHOST_SEND_MASK"] = "GhostSendType.OnlyInterpolatedClients";
                        else if ((typeInfo.GhostAttribute.PrefabType & GhostPrefabType.Client) == GhostPrefabType.PredictedClient)
                            replacements["GHOST_SEND_MASK"] = "GhostSendType.OnlyPredictedClients";
                        else if (typeInfo.GhostAttribute.PrefabType == GhostPrefabType.Server)
                            replacements["GHOST_SEND_MASK"] = "GhostSendType.DontSend";
                        else if (typeInfo.GhostAttribute.SendTypeOptimization == GhostSendType.OnlyInterpolatedClients)
                            replacements["GHOST_SEND_MASK"] = "GhostSendType.OnlyInterpolatedClients";
                        else if (typeInfo.GhostAttribute.SendTypeOptimization == GhostSendType.OnlyPredictedClients)
                            replacements["GHOST_SEND_MASK"] = "GhostSendType.OnlyPredictedClients";
                        else if (typeInfo.GhostAttribute.SendTypeOptimization == GhostSendType.AllClients)
                            replacements["GHOST_SEND_MASK"] = "GhostSendType.AllClients";
                        else
                            replacements["GHOST_SEND_MASK"] = "GhostSendType.DontSend";
                    }
                    else
                    {
                        replacements["GHOST_SEND_MASK"] = "GhostSendType.AllClients";
                    }

                    registrationSystemCodeGen.GenerateFragment("GHOST_SERIALIZATION_STRATEGY_LIST", replacements);

                    if (typeInfo.ComponentType == ComponentType.Input && !String.IsNullOrEmpty(ss.InputBufferComponentTypeName))
                    {
                        replacements["GHOST_INPUT_BUFFER_COMPONENT_TYPE"] = ss.InputBufferComponentTypeName;

                        registrationSystemCodeGen.GenerateFragment("GHOST_INPUT_COMPONENT_LIST", replacements);
                    }
                }

                replacements.Clear();
                replacements["GHOST_USING"] = context.generatedNs;
                registrationSystemCodeGen.GenerateFragment("GHOST_USING_STATEMENT", replacements);

                replacements.Clear();
                replacements.Add("GHOST_NAMESPACE", context.generatedNs);
                registrationSystemCodeGen.GenerateFile("GhostComponentSerializerCollection.cs", string.Empty, replacements, context.batch);
            }
        }

        /// <summary>Long display names like "Some.Very.Long.Namespace.WithAMassiveStructNameAtTheEnd" will be truncated from the back.
        /// E.g. Removing "Some", then "Very" etc. It must fit into the FixedString capacity, otherwise we'll get runtime exceptions during Registration.</summary>
        static string SmartTruncateDisplayName(string displayName)
        {
            int indexOf = 0;
            const int fixedString64BytesCapacity = 61;
            while (displayName.Length - indexOf > fixedString64BytesCapacity && indexOf < displayName.Length)
            {
                int newIndexOf = displayName.IndexOf('.', indexOf);
                if (newIndexOf < 0) newIndexOf = displayName.IndexOf(',', indexOf);

                // We may have to just truncate in the middle of a word.
                if (newIndexOf < 0 || newIndexOf >= displayName.Length - 1)
                    indexOf = Math.Max(0, displayName.Length - fixedString64BytesCapacity);
                else indexOf = newIndexOf + 1;
            }
            return displayName.Substring(indexOf, displayName.Length - indexOf);
        }

        public static void GenerateGhost(Context context, TypeInformation typeTree)
        {
            using(new Profiler.Auto("CodeGen"))
            {
                var generator = new ComponentSerializer(context, typeTree);
                GenerateType(context, typeTree, generator, typeTree.TypeFullName, 0);
                generator.GenerateSerializer(context, typeTree);
            }
        }

        public static void GenerateCommand(Context context, TypeInformation typeInfo, CommandSerializer.Type commandType)
        {
            void BuildGenerator(Context ctx, TypeInformation typeInfo, CommandSerializer parentGenerator)
            {
                if (!typeInfo.IsValid)
                    return;

                var fieldGen = new CommandSerializer(context, parentGenerator.CommandType, typeInfo);
                if (TryGetTypeTemplate(typeInfo, context, out var template))
                {
                    if (!template.SupportCommand)
                        return;

                    fieldGen = new CommandSerializer(context, parentGenerator.CommandType, typeInfo, template);
                    if (!template.Composite)
                    {
                        fieldGen.GenerateFields(ctx, typeInfo.Parent);
                        fieldGen.AppendTarget(parentGenerator);
                        return;
                    }
                }
                foreach (var field in typeInfo.GhostFields)
                    BuildGenerator(ctx, field, fieldGen);
                fieldGen.AppendTarget(parentGenerator);
            }

            using(new Profiler.Auto("CodeGen"))
            {
                var serializeGenerator = new CommandSerializer(context, commandType);
                BuildGenerator(context, typeInfo, serializeGenerator);
                serializeGenerator.GenerateSerializer(context, typeInfo);
                if (commandType == Generators.CommandSerializer.Type.Input)
                {
                    // The input component needs to be registered as an empty type variant so that the
                    // ghost component attributes placed on it can be parsed during ghost conversion
                    var inputGhostAttributes = ComponentFactory.TryGetGhostComponent(typeInfo.Symbol);
                    if (inputGhostAttributes == null)
                        inputGhostAttributes = new GhostComponentAttribute();
                    var variantHash = Helpers.ComputeVariantHash(typeInfo.Symbol, typeInfo.Symbol);
                    context.serializationStrategies.Add(new CodeGenerator.Context.SerializationStrategyCodeGen
                    {
                        TypeInfo = typeInfo,
                        VariantTypeName = typeInfo.TypeFullName.Replace('+', '.'),
                        ComponentTypeName = typeInfo.TypeFullName.Replace('+', '.'),
                        Hash = variantHash.ToString(),
                        GhostAttribute = inputGhostAttributes
                    });

                    TypeInformation bufferTypeTree;
                    ITypeSymbol bufferSymbol;
                    string bufferName;
                    using (new Profiler.Auto("GenerateInputBufferType"))
                    {
                        if (!GenerateInputBufferType(context, typeInfo, out bufferTypeTree,
                                out bufferSymbol, out bufferName))
                            return;
                    }

                    var tmp = context.serializationStrategies[context.serializationStrategies.Count-1];
                    tmp.InputBufferComponentTypeName = bufferSymbol.ToDisplayString();
                    context.serializationStrategies[context.serializationStrategies.Count-1] = tmp;

                    using (new Profiler.Auto("GenerateInputCommandData"))
                    {
                        serializeGenerator = new CommandSerializer(context, Generators.CommandSerializer.Type.Command);
                        BuildGenerator(context, bufferTypeTree, serializeGenerator);
                        serializeGenerator.GenerateSerializer(context, bufferTypeTree);
                    }

                    using (new Profiler.Auto("GenerateInputBufferGhostComponent"))
                    {
                        // Check if the input type has any GhostField attributes, needs to first
                        // lookup the symbol from the candidates list and get the field members from there
                        bool hasGhostFields = false;
                        foreach (var member in typeInfo.Symbol.GetMembers())
                        {
                            foreach (var attribute in member.GetAttributes())
                            {
                                if (attribute.AttributeClass != null &&
                                    attribute.AttributeClass.Name is "GhostFieldAttribute" or "GhostField")
                                    hasGhostFields = true;
                            }
                        }


                        // Parse the generated input buffer as a component so it will be included in snapshot replication.
                        // This only needs to be done if the input struct has ghost fields inside as the generated input
                        // buffer should then be replicated to remote players.
                        if (hasGhostFields) // Ignore GhostEnabledBit here as inputs cannot have them.
                        {
                            GenerateInputBufferGhostComponent(context, typeInfo, bufferName, bufferSymbol);
                        }
                        else
                        {
                            // We must add the serialization strategy even if there are no ghost fields, as empty variants
                            // still save the ghost component attributes.
                            var bufferVariantHash = Helpers.ComputeVariantHash(bufferTypeTree.Symbol, bufferTypeTree.Symbol);
                            context.diagnostic.LogDebug($"Adding SerializationStrategy for input buffer {bufferTypeTree.TypeFullName}, which doesn't have any GhostFields, as we still need to store the GhostComponentAttribute data.");
                            context.serializationStrategies.Add(new CodeGenerator.Context.SerializationStrategyCodeGen
                            {
                                TypeInfo = typeInfo,
                                IsSerialized = false,
                                VariantTypeName = bufferTypeTree.TypeFullName.Replace('+', '.'),
                                ComponentTypeName = bufferTypeTree.TypeFullName.Replace('+', '.'),
                                Hash = bufferVariantHash.ToString(),
                                GhostAttribute = inputGhostAttributes
                            });
                        }
                    }
                }
            }
        }

        #region Internal for Code Generation

        private static bool GenerateInputBufferType(Context context, TypeInformation typeTree, out TypeInformation bufferTypeTree, out ITypeSymbol bufferSymbol, out string bufferName)
        {
            // TODO - Code gen should handle throwing an exception for a zero-sized buffer with [GhostEnabledBit].

            // Add the generated code for the command type symbol to the compilation for further processing
			// first lookup from the metadata cache. If it is present there, we are done.
            var bufferType = context.executionContext.Compilation.GetTypeByMetadataName("Unity.NetCode.InputBufferData`1");
            var inputType = typeTree.Symbol;
            if (bufferType == null)
            {
				//Search in current compilation unit. This is slow path but only happen for the NetCode assembly itself (where we don't have any IInputComponentData, so fine).
                var inputBufferType = context.executionContext.Compilation.GetSymbolsWithName("InputBufferData", SymbolFilter.Type).First() as INamedTypeSymbol;
                bufferSymbol = inputBufferType.Construct(inputType);
            }
            else
            {
                bufferSymbol = bufferType.Construct(inputType);
            }
            if (bufferSymbol == null)
            {
                context.diagnostic.LogError($"Failed to construct input buffer symbol InputBufferData<{typeTree.TypeFullName}>!");
                bufferTypeTree = null;
                bufferName = null;
                return false;
            }
            // FieldTypeName includes the namespace, strip that away when generating the buffer type name
            bufferName = $"{typeTree.FieldTypeName}InputBufferData";
            if (typeTree.Namespace.Length != 0 && typeTree.FieldTypeName.Length > typeTree.Namespace.Length)
                bufferName = $"{typeTree.FieldTypeName.Substring(typeTree.Namespace.Length + 1)}InputBufferData";
            // If the type is nested inside another class/type the parent name will be included in the type name separated by an underscore
            bufferName = bufferName.Replace('.', '_');

            var typeBuilder = new TypeInformationBuilder(context.diagnostic, context.executionContext, TypeInformationBuilder.SerializationMode.Commands);
            // Parse input generated code as command data
            context.ResetState();
            context.generatorName = bufferName;
            bufferTypeTree = typeBuilder.BuildTypeInformation(bufferSymbol, null);
            if (bufferTypeTree == null)
            {
                context.diagnostic.LogError($"Failed to generate type information for symbol ${bufferSymbol.ToDisplayString()}!");
                return false;
            }
            context.types.Add(bufferTypeTree);
            context.diagnostic.LogDebug($"Generating input buffer command data for ${bufferTypeTree.TypeFullName}!");
            return true;
        }

        private static void GenerateInputBufferGhostComponent(Context context, TypeInformation inputTypeTree, string bufferName, ITypeSymbol bufferSymbol)
        {
            // Add to generatedType list so it is included in the serializer registration system
            context.generatedTypes.Add(bufferName);

            var ghostFieldOverride = new GhostField();
            // Type information needs to be rebuilt and this time interpreting the type as a component instead of command
            var typeBuilder = new TypeInformationBuilder(context.diagnostic, context.executionContext, TypeInformationBuilder.SerializationMode.Component);
            context.ResetState();
            var bufferTypeTree = typeBuilder.BuildTypeInformation(bufferSymbol, null, ghostFieldOverride);
            if (bufferTypeTree == null)
            {
                context.diagnostic.LogError($"Failed to generate type information for symbol ${bufferSymbol.ToDisplayString()}!");
                return;
            }
            // Set ghost component attribute from values set on the input component source, or defaults
            // if not present, except for the OwnerSendType which can only be SendToNonOwner since it's
            // a dynamic buffer
            var inputGhostAttributes = ComponentFactory.TryGetGhostComponent(inputTypeTree.Symbol);
            if (inputGhostAttributes != null)
            {
                bufferTypeTree.GhostAttribute = new GhostComponentAttribute
                {
                    PrefabType = inputGhostAttributes.PrefabType,
                    SendDataForChildEntity = inputGhostAttributes.SendDataForChildEntity,
                    SendTypeOptimization = inputGhostAttributes.SendTypeOptimization,
                    OwnerSendType = SendToOwnerType.SendToNonOwner
                };
            }
            else
                bufferTypeTree.GhostAttribute = new GhostComponentAttribute { OwnerSendType = SendToOwnerType.SendToNonOwner };

            var variantHash = Helpers.ComputeVariantHash(bufferTypeTree.Symbol, bufferTypeTree.Symbol);
            context.serializationStrategies.Add(new CodeGenerator.Context.SerializationStrategyCodeGen
            {
                TypeInfo = bufferTypeTree,
                VariantTypeName = bufferTypeTree.TypeFullName.Replace('+', '.'),
                ComponentTypeName = bufferTypeTree.TypeFullName.Replace('+', '.'),
                Hash = variantHash.ToString(),
                GhostAttribute = bufferTypeTree.GhostAttribute,
                IsSerialized = true,
            });

            context.types.Add(bufferTypeTree);
            context.diagnostic.LogDebug($"Generating ghost for input buffer {bufferTypeTree.TypeFullName}!");
            GenerateGhost(context, bufferTypeTree);
        }

        private static void GenerateType(Context context, TypeInformation type,
            ComponentSerializer parentContainer, string fullFieldName, int fieldIndex)
        {
            context.executionContext.CancellationToken.ThrowIfCancellationRequested();
            if (TryGetTypeTemplate(type, context, out var template))
            {
                var generator = new ComponentSerializer(context, type, template);
                if (generator.Composite)
                {
                    // Find and apply the composite overrides, then skip those fragments when processing the composite fields
                    var overrides = generator.GenerateCompositeOverrides(context, type.Parent);
                    if (overrides != null)
                        generator.AppendTarget(parentContainer);
                    var fieldIt = 0;
                    //Verify the assumptions: all generator must be primitive types
                    if (generator.TypeInformation.GhostFields.Count > 0)
                    {
                        var areAllPrimitive = type.GhostFields.TrueForAll(f => f.Kind == GenTypeKind.Primitive);
                        var field = type.GhostFields[0];
                        if (!areAllPrimitive)
                        {
                            context.diagnostic.LogError(
                                $"Can't generate a composite serializer for {type.Description}. The struct fields must be all primitive types but are {field.TypeFullName}!",
                                type.Location);
                            return;
                        }
                        var areAllSameType = type.GhostFields.TrueForAll(f => f.FieldTypeName == field.FieldTypeName);
                        if (!areAllSameType)
                        {
                            context.diagnostic.LogError($"Can't generate a composite serializer for {type.Description}. The struct fields must be all of the same type.!. " +
                                                        $"Check the template assignment in your UserDefinedTemplate class implementation. " +
                                                        $"Composite templates should be used only for generating types that has all the same fields (i.e float3)", type.Location);
                            return;
                        }
                    }
                    foreach (var childGhostField in generator.TypeInformation.GhostFields)
                    {
                        //Composite templates forcibly aggregate the change masks. You can't override this behaviour with the
                        //GhostField.Composite flags (at least the way it designed today).
                        //Given also how they currently work, only basic fields types in practice can be supported. This is very limiting.
                        //So, for now we restrict ourself to support only template here that generate always 1 bit change mask.
                        //TODO: For these reasons, removing the concept of composite (from the template) make sense in my opinion.
                        if (!TryGetTypeTemplate(type, context, out var fieldTemplate))
                        {
                            context.diagnostic.LogError(
                                $"Inside type '{context.generatorName}', we could not find the exact template for field '{type.FieldName}' with configuration '{type.Description}', which means that netcode cannot serialize this type (with this configuration), as it does not know how. " +
                                $"To rectify, either a) define your own template for this type (and configuration), b) resolve any other code-gen errors, or c) modify your GhostField(...) configuration (Quantization, SubType, SmoothingAction etc) to use a known, already existing template. Known templates are {context.registry.FormatAllKnownTypes()}. All known subTypes are {context.registry.FormatAllKnownSubTypes()}!",
                                type.Location);
                            context.diagnostic.LogError(
                                $"Unable to generate serializer for GhostField '{type.TypeFullName}.{childGhostField.TypeFullName}.{childGhostField.TypeFullName}' (description: {childGhostField.Description}) while building the composite!",
                                type.Location);
                        }
                        var g = new ComponentSerializer(context, childGhostField, fieldTemplate);
                        g.GenerateFields(context, childGhostField.Parent, overrides);

                        g.GenerateMasks(context, true, fieldIt);
                        g.AppendTarget(parentContainer);
                        ++fieldIt;
                    }
                    //We need to increment both the total current and total changemask bit counter if the
                    //parent class does not aggregate field.
                    if (!parentContainer.TypeInformation.Attribute.aggregateChangeMask)
                    {
                        parentContainer.m_TargetGenerator.AppendFragment("GHOST_AGGREGATE_WRITE", parentContainer.m_TargetGenerator, "GHOST_WRITE_COMBINED");
                        parentContainer.m_TargetGenerator.Fragments["__GHOST_AGGREGATE_WRITE__"].Content = "";
                        ++context.changeMaskBitCount;
                        ++context.curChangeMaskBits;
                    }
                    return;
                }
                generator.GenerateFields(context, type.Parent);
                generator.GenerateMasks(context, type.Attribute.aggregateChangeMask, fieldIndex);
                generator.AppendTarget(parentContainer);
                if (!parentContainer.TypeInformation.Attribute.aggregateChangeMask)
                {
                    parentContainer.m_TargetGenerator.AppendFragment("GHOST_AGGREGATE_WRITE", parentContainer.m_TargetGenerator, "GHOST_WRITE_COMBINED");
                    parentContainer.m_TargetGenerator.Fragments["__GHOST_AGGREGATE_WRITE__"].Content = "";
                    ++context.changeMaskBitCount;
                    ++context.curChangeMaskBits;
                }
                return;
            }
            // If it's a primitive type and we still have not found a template to use, we can't go any deeper and it's an error
            var isErrorBecausePrimitive = type.Kind == GenTypeKind.Primitive;
            var isErrorBecauseMustFindSubType = type.Description.Attribute.subtype != 0;
            if (isErrorBecausePrimitive || isErrorBecauseMustFindSubType)
            {
                context.diagnostic.LogError($"Inside type '{context.generatorName}', we could not find the exact template for field '{type.FieldName}' with configuration '{type.Description}', which means that netcode cannot serialize this type (with this configuration), as it does not know how. " +
                                            $"To rectify, either a) define your own template for this type (and configuration), b) resolve any other code-gen errors, or c) modify your GhostField(...) configuration (Quantization, SubType, SmoothingAction etc) to use a known, already existing template. Known templates are {context.registry.FormatAllKnownTypes()}. All known subTypes are {context.registry.FormatAllKnownSubTypes()}!", type.Location);
                return;
            }

            if (type.GhostFields.Count == 0 && !type.ShouldSerializeEnabledBit)
            {
                context.diagnostic.LogError($"Couldn't find the TypeDescriptor for GhostField '{context.generatorName}.{type.FieldName}' the type {type.Description} when processing {fullFieldName}! Types must have either valid [GhostField] attributes, or a [GhostEnabledBit] (on an IEnableableComponent).", type.Location);
                return;
            }

            //Make a temporary container that is used to copy the current generated code.
            var temp = new ComponentSerializer(context, type);
            for (var index = 0; index < type.GhostFields.Count; index++)
            {
                var field = type.GhostFields[index];
                GenerateType(context, field, temp, $"{field.DeclaringTypeFullName}.{field.FieldName}", index);
            }
            temp.AppendTarget(parentContainer);
            //increment the mask bits if the current aggregation scope is completed.
            if (type.Attribute.aggregateChangeMask && !parentContainer.TypeInformation.Attribute.aggregateChangeMask)
            {
                parentContainer.m_TargetGenerator.AppendFragment("GHOST_AGGREGATE_WRITE", parentContainer.m_TargetGenerator, "GHOST_WRITE_COMBINED");
                parentContainer.m_TargetGenerator.Fragments["__GHOST_AGGREGATE_WRITE__"].Content = "";
                ++context.curChangeMaskBits;
                ++context.changeMaskBitCount;
            }
        }
        #endregion

        public struct GeneratedFile
        {
            public string Namespace;
            public string GeneratedClassName;
            public string Code;
        }

        public interface ITemplateFileProvider
        {
            string GetTemplateData(string filename);
        }

        public class CodeGenCache
        {
            private Dictionary<string, GhostCodeGen> cache;
            private ITemplateFileProvider provider;
            private Context context;

            public CodeGenCache(ITemplateFileProvider templateFileProvider, Context context)
            {
                this.provider = templateFileProvider;
                this.context = context;
                this.cache = new Dictionary<string, GhostCodeGen>(128);
            }

            public GhostCodeGen GetTemplate(string templatePath)
            {
                if (!cache.TryGetValue(templatePath, out var codeGen))
                {
                    var templateData = provider.GetTemplateData(templatePath);
                    codeGen = new GhostCodeGen(templatePath, templateData, context);
                    cache.Add(templatePath, codeGen);
                }
                return codeGen;
            }

            public GhostCodeGen GetTemplateWithOverride(string templatePath, string templateOverride)
            {
                var key = templatePath + templateOverride;
                if (!cache.TryGetValue(key, out var codeGen))
                {
                    var templateData = provider.GetTemplateData(templatePath);
                    codeGen = new GhostCodeGen(templatePath, templateData, context);
                    if (!string.IsNullOrEmpty(templateOverride))
                    {
                        var overrideTemplateData = provider.GetTemplateData(templateOverride);
                        codeGen.AddTemplateOverrides(templateOverride, overrideTemplateData);
                    }
                    cache.Add(key, codeGen);
                }
                return codeGen;
            }
        }

        //Contains all the state for the current serialization. Generators must be stateless and immutable, only the
        //Context should contains mutable data
        public class Context
        {
            internal GeneratorExecutionContext executionContext;
            public readonly string generatedNs;
            public readonly TypeRegistry registry;
            public readonly IDiagnosticReporter diagnostic;
            public readonly CodeGenCache codeGenCache;
            public readonly List<GeneratedFile> batch;
            public readonly List<TypeInformation> types;
            public readonly HashSet<string> imports;
            public readonly HashSet<string> generatedTypes;
            public struct SerializationStrategyCodeGen
            {
                public TypeInformation TypeInfo;
                public string DisplayName;
                public string ComponentTypeName;
                public string VariantTypeName;
                public string Hash;
                public bool IsSerialized;
                public GhostComponentAttribute GhostAttribute;
                public string InputBufferComponentTypeName;

            }
            public readonly List<SerializationStrategyCodeGen> serializationStrategies;

            //Follow the Rolsyn convention for inner classes (so Namespace.ClassName[+DeclaringClass]+Class
            public string variantTypeFullName;
            public ulong variantHash;
            public string generatorName;
            //Total number of changeMaskBits bits
            public int changeMaskBitCount;
            //The current used mask bits
            public int curChangeMaskBits;
            public ulong ghostFieldHash;
            //public CurrentFieldState FieldState;

            public void ResetState()
            {
                changeMaskBitCount = 0;
                curChangeMaskBits = 0;
                ghostFieldHash = 0;
                variantTypeFullName = null;
                variantHash = 0;
                imports.Clear();
                imports.Add("Unity.Entities");
                imports.Add("Unity.Collections");
                imports.Add("Unity.NetCode");
                imports.Add("Unity.Transforms");
                imports.Add("Unity.Mathematics");
            }

            string GenerateNamespaceFromAssemblyName(string assemblyName)
            {
                return Regex.Replace(assemblyName, @"[^\w\.]", "_", RegexOptions.Singleline) + ".Generated";
            }

            public Context(TypeRegistry typeRegistry, ITemplateFileProvider templateFileProvider,
                IDiagnosticReporter reporter, GeneratorExecutionContext context, string assemblyName)
            {
                executionContext = context;
                types = new List<TypeInformation>(16);
                serializationStrategies = new List<SerializationStrategyCodeGen>(32);
                codeGenCache = new CodeGenCache(templateFileProvider, this);
                batch = new List<GeneratedFile>(256);
                imports = new HashSet<string>();
                generatedTypes = new HashSet<string>();
                diagnostic = reporter;
                generatedNs = GenerateNamespaceFromAssemblyName(assemblyName);
                registry = typeRegistry;
                ResetState();
            }
        }
    }
}
