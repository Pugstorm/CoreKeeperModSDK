using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;

namespace Unity.Entities.CodeGen.Cloner
{
    class Cloner : EntitiesILPostProcessor
    {
        protected override bool PostProcessImpl(TypeDefinition[] componentSystemTypes)
        {
            var madeChange = false;
            foreach (var typeDef in componentSystemTypes)
            {
                var methods = typeDef.GetOriginalAndRewrittenMethods();
                var properties = typeDef.GetOriginalAndRewrittenProperties();

                if (!methods.Rewritten.Any() && !properties.Rewritten.Any())
                {
                    continue;
                }

                foreach (var rewrittenProperty in properties.Rewritten)
                {
                    if (!properties.OriginalLookup.ContainsKey(rewrittenProperty.Source))
                    {
                        throw new InvalidOperationException(
                            $"Method Cloner ILPP: Cannot find property {rewrittenProperty.Source} in {typeDef.FullName}. " +
                            $"Property candidates are {string.Join(", ", properties.OriginalLookup.Keys)}");
                    }

                    var originalProperty = properties.OriginalLookup[rewrittenProperty.Source];
                    if (rewrittenProperty.Definition.GetMethod != null)
                        methods.Rewritten.Add((rewrittenProperty.Definition.GetMethod, GetMethodNameAndParamsAsString(originalProperty.GetMethod)));
                    if (rewrittenProperty.Definition.SetMethod != null)
                        methods.Rewritten.Add((rewrittenProperty.Definition.SetMethod, GetMethodNameAndParamsAsString(originalProperty.SetMethod)));
                    typeDef.Properties.Remove((((PropertyDefinition Definition, string ConstructorArgument))rewrittenProperty).Definition);
                    madeChange = true;
                }

                var originalMethodIdsToDefinitions = methods.Original.ToDictionary(GetMethodNameAndParamsAsString, method => method);

                foreach (var rewrittenMethod in methods.Rewritten)
                {
                    if (!originalMethodIdsToDefinitions.ContainsKey(rewrittenMethod.Source))
                    {
                        throw new InvalidOperationException(
                            $"Method Cloner ILPP: Cannot find method {rewrittenMethod.Source} in {typeDef.FullName}. " +
                            $"Method candidates are {string.Join(", ", originalMethodIdsToDefinitions.Keys)}");
                    }

                    typeDef.UpdateOriginalMethod(originalMethodIdsToDefinitions, rewrittenMethod);
                    madeChange = true;
                }
            }
            return madeChange;
        }

        protected override bool PostProcessUnmanagedImpl(TypeDefinition[] unmanagedComponentSystemTypes)
        {
            return false;
        }

        static string GetMethodNameAndParamsAsString(MethodReference method)
        {
            var strBuilder = new StringBuilder();
            strBuilder.Append(method.Name);
            strBuilder.Append($"_T{method.GenericParameters.Count}");

            foreach (var parameter in method.Parameters)
            {
                if (parameter.ParameterType.IsByReference)
                {
                    if (parameter.IsIn)
                        strBuilder.Append($"_in");
                    else if (parameter.IsOut)
                        strBuilder.Append($"_out");
                    else
                        strBuilder.Append($"_ref");
                }

                strBuilder.Append($"_{parameter.ParameterType}");
            }

            return strBuilder.ToString();
        }
    }
}
