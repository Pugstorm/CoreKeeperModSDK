using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.Burst;
using Unity.Collections;
using MethodBody = Mono.Cecil.Cil.MethodBody;

namespace Unity.Jobs.CodeGen
{
    static class TypeReferenceExtensions
    {
        public static TypeDefinition CheckedResolve(this TypeReference typeReference)
        {
            return typeReference.Resolve() ?? throw new ResolutionException(typeReference);
        }
    }

    static class MethodReferenceExtensions
    {
        /// <summary>
        /// Generates a closed/specialized MethodReference for the given method and types[]
        /// e.g.
        /// struct Foo { T Bar<T>(T val) { return default(T); }
        ///
        /// In this case, if one would like a reference to "Foo::int Bar(int val)" this method will construct such a method
        /// reference when provided the open "T Bar(T val)" method reference and the TypeReferences to the types you'd like
        /// specified as generic arguments (in this case a TypeReference to "int" would be passed in).
        /// </summary>
        /// <param name="method"></param>
        /// <param name="types"></param>
        /// <returns></returns>
        public static MethodReference MakeGenericInstanceMethod(this MethodReference method,
            params TypeReference[] types)
        {
            var result = new GenericInstanceMethod(method);
            foreach (var type in types)
                result.GenericArguments.Add(type);
            return result;
        }
    }
}
