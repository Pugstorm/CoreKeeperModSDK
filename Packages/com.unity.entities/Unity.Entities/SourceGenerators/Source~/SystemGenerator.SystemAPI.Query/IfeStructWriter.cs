using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.SystemAPI.Query;

struct IfeStructWriter : IMemberWriter
{
    public IfeType IfeType { get; set; }
    public IReadOnlyList<SharedComponentFilterInfo> SharedComponentFilterInfos { get; set; }

    void GenerateCompleteDependenciesMethod(IndentedTextWriter writer)
    {
        writer.WriteLine("public static void CompleteDependencies(ref SystemState state)");
        writer.WriteLine("{");
        writer.Indent++;

        foreach (var element in IfeType.ReturnedTupleElementsDuringEnumeration)
        {
            writer.WriteLine(element.Type switch
            {
                QueryType.ManagedComponent =>
                    $"state.EntityManager.CompleteDependencyBeforeRW<{element.TypeSymbolFullName}>();",
                QueryType.UnityEngineComponent =>
                    $"state.EntityManager.CompleteDependencyBeforeRW<{element.TypeArgumentFullName}>();",
                QueryType.RefRW =>
                    $"state.EntityManager.CompleteDependencyBeforeRW<{element.TypeArgumentFullName}>();",
                QueryType.RefRO =>
                    $"state.EntityManager.CompleteDependencyBeforeRO<{element.TypeArgumentFullName}>();",
                QueryType.UnmanagedSharedComponent =>
                    $"state.EntityManager.CompleteDependencyBeforeRO<{element.TypeSymbolFullName}>();",
                QueryType.ManagedSharedComponent =>
                    $"state.EntityManager.CompleteDependencyBeforeRW<{element.TypeSymbolFullName}>();",
                QueryType.Aspect => $"default({element.TypeSymbolFullName}).CompleteDependencyBeforeRW(ref state);",
                QueryType.DynamicBuffer =>
                    $"state.EntityManager.CompleteDependencyBeforeRW<{element.TypeArgumentFullName}>();",
                QueryType.ValueTypeComponent =>
                    $"state.EntityManager.CompleteDependencyBeforeRO<{element.TypeSymbolFullName}>();",
                QueryType.EnabledRefRW =>
                    $"state.EntityManager.CompleteDependencyBeforeRW<{element.TypeArgumentFullName}>();",
                QueryType.EnabledRefRO =>
                    $"state.EntityManager.CompleteDependencyBeforeRO<{element.TypeArgumentFullName}>();",
                QueryType.TagComponent => "",
                _ => throw new ArgumentOutOfRangeException()
            });
        }

        writer.Indent--;
        writer.WriteLine("}");
    }

    void GenerateEnumerator(
        IndentedTextWriter writer,
        string ifeTypeName,
        string queryResultTypeName)
    {
        if (IfeType.UseBurst)
        {
            writer.WriteLine("[global::Unity.Burst.NoAlias]");
            writer.WriteLine($"[{IfeType.BurstCompileAttribute}]");
        }

        writer.WriteLine($"public struct Enumerator : global::System.Collections.Generic.IEnumerator<{queryResultTypeName}>");
        writer.WriteLine("{");
        writer.Indent++;

        // Create EntityQuery field if there are SharedComponentFilterTypes, because we need to invoke query.ResetFilter() at the end of the enumeration.
        if (SharedComponentFilterInfos.Count > 0)
            writer.WriteLine("global::Unity.Entities.EntityQuery _entityQuery;");

        writer.WriteLine("global::Unity.Entities.Internal.InternalEntityQueryEnumerator _entityQueryEnumerator;");
        writer.WriteLine("TypeHandle _typeHandle;");
        writer.WriteLine("ResolvedChunk _resolvedChunk;");
        writer.WriteLine();
        writer.WriteLine("int _currentEntityIndex;");
        writer.WriteLine("int _endEntityIndex;");
        writer.WriteLine();

        // Enumerator constructor should have SharedComponent parameters if .WithSharedComponentFilter() is used,
        // because we need to invoke query.SetSharedComponentFilter(sharedComp).
        writer.Write("public Enumerator(global::Unity.Entities.EntityQuery entityQuery, TypeHandle typeHandle, ref Unity.Entities.SystemState state");

        for (var index = 0; index < SharedComponentFilterInfos.Count; index++)
        {
            var info = SharedComponentFilterInfos[index];

            writer.Write(", ");
            writer.Write(info.TypeSymbol.ToFullName());
            writer.Write(" sharedComponent" + index);
        }

        writer.Write(")");
        writer.WriteLine();
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("if (!entityQuery.IsEmptyIgnoreFilter)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine($"{ifeTypeName}.CompleteDependencies(ref state);");
        writer.WriteLine("typeHandle.Update(ref state);");
        writer.WriteLine();

        for (var index = 0; index < SharedComponentFilterInfos.Count; index++)
        {
            var sharedComponentFilterInfo = SharedComponentFilterInfos[0];
            if (sharedComponentFilterInfo.IsManaged)
                writer.WriteLine("entityQuery.SetSharedComponentFilterManaged(sharedComponent" + index + ");");
            else
                writer.WriteLine("entityQuery.SetSharedComponentFilter(sharedComponent" + index + ");");
        }

        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine();

        if (SharedComponentFilterInfos.Count > 0)
            writer.WriteLine("_entityQuery = entityQuery;");

        writer.WriteLine("_entityQueryEnumerator = new global::Unity.Entities.Internal.InternalEntityQueryEnumerator(entityQuery);");
        writer.WriteLine();
        writer.WriteLine("_currentEntityIndex = -1;");
        writer.WriteLine("_endEntityIndex = -1;");
        writer.WriteLine();
        writer.WriteLine("_typeHandle = typeHandle;");
        writer.WriteLine("_resolvedChunk = default;");
        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine("public void Dispose() => _entityQueryEnumerator.Dispose();");
        writer.WriteLine();
        writer.WriteLine(
            "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        writer.WriteLine("public bool MoveNext()");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("_currentEntityIndex++;");
        writer.WriteLine();
        writer.WriteLine(IfeType.UseBurst
            ? "if (global::Unity.Burst.CompilerServices.Hint.Unlikely(_currentEntityIndex >= _endEntityIndex))"
            : "if (_currentEntityIndex >= _endEntityIndex)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine(IfeType.UseBurst
            ? "if (global::Unity.Burst.CompilerServices.Hint.Likely(_entityQueryEnumerator.MoveNextEntityRange(out bool movedToNewChunk, out global::Unity.Entities.ArchetypeChunk chunk, out int entityStartIndex, out int entityEndIndex)))"
            : "if (_entityQueryEnumerator.MoveNextEntityRange(out bool movedToNewChunk, out global::Unity.Entities.ArchetypeChunk chunk, out int entityStartIndex, out int entityEndIndex))");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("if (movedToNewChunk)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("_resolvedChunk = _typeHandle.Resolve(chunk);");
        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine("_currentEntityIndex = entityStartIndex;");
        writer.WriteLine("_endEntityIndex = entityEndIndex;");
        writer.WriteLine("return true;");
        writer.Indent--;
        writer.WriteLine("}");

        // We have reached the end of the enumeration. Reset filter if necessary.
        if (SharedComponentFilterInfos.Count > 0)
            writer.WriteLine("_entityQuery.ResetFilter();");

        writer.WriteLine("return false;");
        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine("return true;");
        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine($"public {queryResultTypeName} Current => _resolvedChunk.Get(_currentEntityIndex);");
        writer.WriteLine();
        writer.WriteLine("public Enumerator GetEnumerator() => this;");
        writer.WriteLine("public void Reset() => throw new global::System.NotImplementedException();");
        writer.WriteLine("object global::System.Collections.IEnumerator.Current => throw new global::System.NotImplementedException();");
        writer.Indent--;
        writer.WriteLine("}");
    }

    public void WriteTo(IndentedTextWriter writer)
    {
        var resolvedChunk =
            NestedStruct.ResolvedChunk(IfeType.ReturnedTupleElementsDuringEnumeration,
                IfeType.MustReturnEntityDuringIteration, IfeType.PerformsCollectionChecks).ToArray();
        var typeHandle =
            NestedStruct.TypeHandle(IfeType.ReturnedTupleElementsDuringEnumeration,
                IfeType.MustReturnEntityDuringIteration, IfeType.PerformsCollectionChecks).ToArray();

        (NestedStruct.Field ResolvedChunkField, NestedStruct.ArgumentInReturnedType TypeHandleArgument)[]
            pairedFields =
                resolvedChunk.Zip(typeHandle, (e1, e2) =>
                        (ResolvedChunkField: e1.Field, TypeHandleField: e2.ArgumentWhenInitializingResolvedChunk))
                    .ToArray();

        var resultType =
            IfeType.ResultType(queryResultConstructorArgs:
                resolvedChunk
                    .Where(f => !f.ArgumentInReturnedTupleDuringIndexAccess.IsEmpty)
                    .Select(f => f.ArgumentInReturnedTupleDuringIndexAccess.Value));

        writer.WriteLine(TypeCreationHelpers.GeneratedLineTriviaToGeneratedSource);
        if (IfeType.UseBurst)
        {
            writer.WriteLine("[global::Unity.Burst.NoAlias]");
            writer.WriteLine($"[{IfeType.BurstCompileAttribute}]");
        }

        writer.WriteLine($"readonly struct {IfeType.TypeName}");
        writer.WriteLine("{");
        writer.Indent++;
        if (IfeType.UseBurst)
        {
            writer.WriteLine("[global::Unity.Burst.NoAlias]");
            writer.WriteLine($"[{IfeType.BurstCompileAttribute}]");
        }

        writer.WriteLine($"public struct ResolvedChunk");
        writer.WriteLine("{");
        writer.Indent++;

        foreach (var field in resolvedChunk)
            writer.WriteLine(field.Field.Declaration);

        writer.WriteLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        writer.WriteLine($"public {resultType.FullName} Get(int index) => {resultType.Creation};");
        writer.Indent--;
        writer.WriteLine("}");
        if (IfeType.UseBurst)
        {
            writer.WriteLine("[global::Unity.Burst.NoAlias]");
            writer.WriteLine($"[{IfeType.BurstCompileAttribute}]");
        }

        writer.WriteLine("public struct TypeHandle");
        writer.WriteLine("{");
        writer.Indent++;
        bool needsEntManagerField = typeHandle.Any(f => f.Field.DependsOnEntityManagerField);
        if (needsEntManagerField)
            writer.WriteLine("public global::Unity.Entities.EntityManager _entityManager;");

        var distinctTypeHandleContent = new HashSet<string>();
        foreach (var t in typeHandle)
        {
            if (distinctTypeHandleContent.Add(t.Field.Declaration))
                writer.WriteLine(t.Field.Declaration);
        }

        writer.WriteLine("public TypeHandle(ref global::Unity.Entities.SystemState systemState)");
        writer.WriteLine("{");
        writer.Indent++;
        if (needsEntManagerField)
            writer.WriteLine("_entityManager = systemState.EntityManager;");

        distinctTypeHandleContent.Clear();
        foreach (var t in typeHandle)
        {
            if (distinctTypeHandleContent.Add(t.Field.AssignmentInNestedStructConstructor))
                writer.WriteLine(t.Field.AssignmentInNestedStructConstructor);
        }

        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine("public void Update(ref global::Unity.Entities.SystemState systemState)");
        writer.WriteLine("{");
        writer.Indent++;

        distinctTypeHandleContent.Clear();
        foreach (var kvp in typeHandle)
        {
            if (!string.IsNullOrEmpty(kvp.Field.Name) && distinctTypeHandleContent.Add(kvp.Field.Name))
                writer.WriteLine($"{kvp.Field.Name}.Update(ref systemState);");
        }

        writer.Indent--;
        writer.WriteLine("}");

        writer.WriteLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        writer.WriteLine("public ResolvedChunk Resolve(global::Unity.Entities.ArchetypeChunk archetypeChunk)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("var resolvedChunk = new ResolvedChunk();");
        foreach (var s in NestedStruct.InitializeResolvedChunkInstanceInTypeHandle(pairedFields))
            writer.WriteLine(s);

        writer.WriteLine("return resolvedChunk;");
        writer.Indent--;
        writer.WriteLine("}");
        writer.Indent--;
        writer.WriteLine("}");

        writer.WriteLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        writer.Write("public static Enumerator Query(global::Unity.Entities.EntityQuery entityQuery, TypeHandle typeHandle, ref Unity.Entities.SystemState state");

        for (var index = 0; index < SharedComponentFilterInfos.Count; index++)
        {
            var info = SharedComponentFilterInfos[index];

            writer.Write(", ");
            writer.Write(info.TypeSymbol.ToFullName());
            writer.Write(" sharedComponent" + index);
        }
        writer.Write(")");
        writer.Write(" => new Enumerator(entityQuery, typeHandle, ref state");

        for (var index = 0; index < SharedComponentFilterInfos.Count; index++)
        {
            writer.Write(", ");
            writer.Write(" sharedComponent" + index);
        }

        writer.Write(");");
        writer.WriteLine();

        GenerateEnumerator(writer, IfeType.TypeName, resultType.FullName);
        GenerateCompleteDependenciesMethod(writer);

        writer.Indent--;
        writer.WriteLine("}");
    }
}
