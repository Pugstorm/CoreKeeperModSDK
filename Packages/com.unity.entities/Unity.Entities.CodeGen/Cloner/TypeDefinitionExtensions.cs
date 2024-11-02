using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Unity.Entities.CodeGen.Cloner
{
    public static class TypeDefinitionExtensions
    {
        public static (List<MethodDefinition> Original, List<(MethodDefinition Definition, string Source)> Rewritten)
            GetOriginalAndRewrittenMethods(this TypeDefinition typeDefinition)
        {
            var original = new List<MethodDefinition>();
            var rewritten = new List<(MethodDefinition, string)>();

            foreach (MethodDefinition method in typeDefinition.Methods)
            {
                var patchedAttribute =
                    method.CustomAttributes.FirstOrDefault(attr => attr.AttributeType.Name == nameof(DOTSCompilerPatchedMethodAttribute));

                if (patchedAttribute != null)
                {
                    rewritten.Add((method, patchedAttribute.ConstructorArguments.First().Value.ToString()));
                }
                else
                {
                    original.Add(method);
                }
            }

            return (original, rewritten);
        }

        static string GetPropertyName(PropertyDefinition propertyDefinition) => $"{propertyDefinition.DeclaringType.ToString().Replace('/', '.')}.{propertyDefinition.Name}";
        public static (Dictionary<string, PropertyDefinition> OriginalLookup, List<(PropertyDefinition Definition, string Source)> Rewritten)
            GetOriginalAndRewrittenProperties(this TypeDefinition typeDefinition)
        {
            var originalLookup = new Dictionary<string, PropertyDefinition>();
            var rewritten = new List<(PropertyDefinition, string)>();

            foreach (var property in typeDefinition.Properties)
            {
                var patchedAttribute =
                    property.CustomAttributes.FirstOrDefault(attr => attr.AttributeType.Name == nameof(DOTSCompilerPatchedPropertyAttribute));

                if (patchedAttribute != null)
                {
                    rewritten.Add((property, patchedAttribute.ConstructorArguments.First().Value.ToString()));
                }
                else
                {
                    originalLookup[GetPropertyName(property)] = property;
                }
            }

            return (originalLookup, rewritten);
        }

        public static void UpdateOriginalMethod(
            this TypeDefinition typeDef,
            Dictionary<string, MethodDefinition> originalMethodIdsToDefinitions,
            (MethodDefinition Definition, string ConstructorArgument) rewrittenMethodIn)
        {
            var originalMethod = originalMethodIdsToDefinitions[rewrittenMethodIn.ConstructorArgument];
            var rewrittenMethod = rewrittenMethodIn.Definition;

            // If we are using a display class in a method but not in the source, we need to remove the display class usage
            // (otherwise Mono will get confused when trying to debug multiple sequence points that point to the same source file location)
            var displayClassesUsedInOriginal = GetAllDisplayClassUsagesInMethod(originalMethod);
            var displayClassesUsedInRewritten = GetAllDisplayClassUsagesInMethod(rewrittenMethod);

            foreach (var originalDisplayClass in displayClassesUsedInOriginal)
            {
                if (!displayClassesUsedInRewritten.Contains(originalDisplayClass) && originalMethod.DeclaringType.NestedTypes.Contains(originalDisplayClass))
                {
                    originalMethod.DeclaringType.NestedTypes.Remove(originalDisplayClass);

                    var methodsToRemove = new List<MethodDefinition>(typeDef.Methods.Count);
                    foreach (var displayMethodCandidate in typeDef.Methods)
                        if (displayMethodCandidate.Parameters.FirstOrDefault()?.ParameterType.GetElementType() == originalDisplayClass)
                            methodsToRemove.Add(displayMethodCandidate);
                    foreach (var methodToRemove in methodsToRemove)
                        typeDef.Methods.Remove(methodToRemove);
                }
            }

            originalMethod.Body = rewrittenMethod.Body;
            typeDef.Methods.Remove(rewrittenMethod);

            var sequencePoints = originalMethod.DebugInformation.SequencePoints;
            sequencePoints.Clear();

            foreach (var sp in rewrittenMethod.DebugInformation.SequencePoints)
                sequencePoints.Add(sp);

            originalMethod.DebugInformation.Scope = rewrittenMethod.DebugInformation.Scope;

            if (rewrittenMethod.HasGenericParameters && originalMethod.HasGenericParameters)
            {
                originalMethod.GenericParameters.Clear();
                foreach (var genericParam in rewrittenMethod.GenericParameters)
                {
                    originalMethod.GenericParameters.Add(genericParam);
                }
            }
        }

        // Check both field and variable usages for display classes
        static HashSet<TypeDefinition> GetAllDisplayClassUsagesInMethod(MethodDefinition method)
        {
            var displayClassesUsedInMethod = new HashSet<TypeDefinition>();

            foreach (var local in method.Body.Variables.Where(local => local.VariableType.IsDisplayClassCandidate()))
                displayClassesUsedInMethod.Add(local.VariableType.Resolve());

            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.IsLoadField() || instruction.IsLoadStaticField())
                {
                    var fieldOperand = (FieldReference)instruction.Operand;
                    if (fieldOperand.FieldType.IsDisplayClassCandidate())
                        displayClassesUsedInMethod.Add(fieldOperand.FieldType.Resolve());
                }
                else if (instruction.OpCode == OpCodes.Newobj)
                {
                    var methodOperand = (MethodReference)instruction.Operand;
                    if (methodOperand.DeclaringType.IsDisplayClassCandidate())
                        displayClassesUsedInMethod.Add(method.DeclaringType.Resolve());
                }
            }

            return displayClassesUsedInMethod;
        }

    }
}
