using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.Common;

public readonly struct QueriesAndHandles
{
    public readonly HashSet<IMemberDescription> TypeHandleStructNestedFields;
    public readonly Dictionary<IQueryFieldDescription, string> QueryFieldsToFieldNames;
    public readonly int UniqueId;

    QueriesAndHandles(HashSet<IMemberDescription> typeHandleStructNestedFields, Dictionary<IQueryFieldDescription, string> queryFieldsToFieldNames, SyntaxNode owningType)
    {
        TypeHandleStructNestedFields = typeHandleStructNestedFields;
        QueryFieldsToFieldNames = queryFieldsToFieldNames;

        var systemLineSpan = owningType.GetLocation().GetLineSpan();
        UniqueId = (systemLineSpan.Span.Start.Line + SourceGenHelpers.GetStableHashCode(Path.GetFileName(systemLineSpan.Path))) & 0x7fffffff;
    }

    public static QueriesAndHandles Create(TypeDeclarationSyntax owningType)
        => new(new HashSet<IMemberDescription>(),new Dictionary<IQueryFieldDescription, string>(), owningType);

    public string GetOrCreateQueryField(IQueryFieldDescription queryFieldDescription, string generatedName = null)
    {
        if (QueryFieldsToFieldNames.TryGetValue(queryFieldDescription, out string matchingFieldName))
            return matchingFieldName;
        generatedName ??= $"__query_{UniqueId}_{QueryFieldsToFieldNames.Count}";
        QueryFieldsToFieldNames.Add(queryFieldDescription, generatedName);
        return generatedName;
    }

    // We cannot call `GetOrCreateTypeHandleField(ITypeSymbol typeSymbol, bool isReadOnly)` when creating type handle
    // fields for source-generated types, because there aren't any type symbols available yet.
    public string GetOrCreateSourceGeneratedIfeTypeHandleField(string containerTypeFullName)
    {
        var description = new IfeTypeHandleFieldDescription(containerTypeFullName);
        TypeHandleStructNestedFields.Add(description);

        return description.GeneratedFieldName;
    }

    public string GetOrCreateAspectLookup(ITypeSymbol entityTypeLookup, bool isReadOnly)
    {
        var entityTypeLookupField = new AspectLookupFieldDescription(entityTypeLookup, isReadOnly);
        TypeHandleStructNestedFields.Add(entityTypeLookupField);

        return entityTypeLookupField.GeneratedFieldName;
    }

    public string GetOrCreateEntityTypeHandleField()
    {
        var entityTypeHandleFieldDescription = new EntityTypeHandleFieldDescription();
        TypeHandleStructNestedFields.Add(entityTypeHandleFieldDescription);

        return entityTypeHandleFieldDescription.GeneratedFieldName;
    }

    public string GetOrCreateTypeHandleField(ITypeSymbol typeSymbol, bool isReadOnly, TypeHandleFieldDescription.TypeHandleSource forcedTypeHandleSource = TypeHandleFieldDescription.TypeHandleSource.None)
    {
        var typeHandleFieldDescription = new TypeHandleFieldDescription(typeSymbol, isReadOnly, forcedTypeHandleSource);
        TypeHandleStructNestedFields.Add(typeHandleFieldDescription);

        return typeHandleFieldDescription.GeneratedFieldName;
    }

    public string GetOrCreateComponentLookupField(ITypeSymbol typeSymbol, bool isReadOnly)
    {
        var lookupField = new ComponentLookupFieldDescription(typeSymbol, isReadOnly);
        TypeHandleStructNestedFields.Add(lookupField);

        return lookupField.GeneratedFieldName;
    }
    public string GetOrCreateJobEntityHandle(ITypeSymbol typeSymbol, bool assignDefaultQuery)
    {
        var lookupField = new JobEntityQueryAndHandleDescription(typeSymbol, assignDefaultQuery);
        TypeHandleStructNestedFields.Add(lookupField);
        return lookupField.GeneratedFieldName;
    }

    public string GetOrCreateBufferLookupField(ITypeSymbol typeSymbol, bool isReadOnly)
    {
        var bufferLookupField = new BufferLookupFieldDescription(typeSymbol, isReadOnly);
        TypeHandleStructNestedFields.Add(bufferLookupField);

        return bufferLookupField.GeneratedFieldName;
    }

    public string GetOrCreateEntityStorageInfoLookupField()
    {
        var storageLookupField = new EntityStorageInfoLookupFieldDescription();
        TypeHandleStructNestedFields.Add(storageLookupField);

        return storageLookupField.GeneratedFieldName;
    }

    public static void WriteTypeHandleStructAndAssignQueriesMethod(IndentedTextWriter writer, params QueriesAndHandles[] handleDescriptions)
    {
        if (handleDescriptions.Length == 0)
            throw new InvalidOperationException("Didn't pass in any handle Descriptions");

        writer.WriteLine();
        writer.WriteLine("TypeHandle __TypeHandle;");

        var allQueryFieldsToGenerate = handleDescriptions.SelectMany(d => d.QueryFieldsToFieldNames).ToArray();
        foreach (var kvp in allQueryFieldsToGenerate)
            writer.WriteLine(kvp.Key.GetFieldDeclaration(kvp.Value));

        writer.WriteLine("struct TypeHandle");
        writer.WriteLine("{");
        writer.Indent++;

        // This handles the aliasing of lookups and typehandles
        var nonQueryFields = handleDescriptions[0].TypeHandleStructNestedFields;
        for (var i = 1; i < handleDescriptions.Length; i++)
        {
            var desc = handleDescriptions[i];
            foreach (var nonQueryField in desc.TypeHandleStructNestedFields)
                nonQueryFields.Add(nonQueryField);
        }

        foreach (var memberDescription in nonQueryFields)
            memberDescription.AppendMemberDeclaration(writer, forcePublic: true);

        writer.WriteLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        writer.WriteLine("public void __AssignHandles(ref global::Unity.Entities.SystemState state)");
        writer.WriteLine("{");
        writer.Indent++;
        foreach (var nonQueryField in nonQueryFields)
            writer.WriteLine(nonQueryField.GetMemberAssignment());
        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine();
        writer.Indent--;
        writer.WriteLine("}");

        writer.WriteLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        writer.WriteLine("void __AssignQueries(ref global::Unity.Entities.SystemState state)");
        writer.WriteLine("{");
        writer.Indent++;

        writer.WriteLine("var entityQueryBuilder = new global::Unity.Entities.EntityQueryBuilder(global::Unity.Collections.Allocator.Temp);");
        foreach (var kvp in allQueryFieldsToGenerate)
            kvp.Key.WriteEntityQueryFieldAssignment(writer, kvp.Value);

        writer.WriteLine("entityQueryBuilder.Dispose();");

        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine();
    }

    // Expects the first handle description to be the one where the TypeHandle is generated.
    // By the point this is called all HandleDescriptions should have been filled out!
    public static void WriteInternalCompilerQueryAndHandleDataStruct(
        IndentedTextWriter writer,
        bool shouldHaveUpdate,
        bool fieldsArePublic,
        Action<IndentedTextWriter> writeAdditionalSyntaxInInternalCompilerQueryAndHandleData,
        params QueriesAndHandles[] handlesAndQueries)
    {
        if (handlesAndQueries.Length == 0)
            throw new InvalidOperationException("Didn't pass in any handle Descriptions");

        writer.WriteLine("/// <summary> Used internally by the compiler, we won't promise this exists in the future </summary>");
        writer.WriteLine("public struct InternalCompilerQueryAndHandleData");
        writer.WriteLine("{");
        writer.Indent++;
        if (fieldsArePublic)
            writer.Write("public ");
        writer.WriteLine("TypeHandle __TypeHandle;");

        var allQueryFieldsToGenerate = handlesAndQueries.SelectMany(d => d.QueryFieldsToFieldNames).ToArray();
        foreach (var kvp in allQueryFieldsToGenerate)
            writer.WriteLine(kvp.Key.GetFieldDeclaration(kvp.Value, fieldsArePublic));

        if (fieldsArePublic)
            writer.Write("public ");
        writer.WriteLine("struct TypeHandle");
        writer.WriteLine("{");
        writer.Indent++;

        // This handles the aliasing of lookups and typehandles
        var nonQueryFields = handlesAndQueries[0].TypeHandleStructNestedFields;
        for (var i = 1; i < handlesAndQueries.Length; i++)
        {
            var desc = handlesAndQueries[i];
            foreach (var nonQueryField in desc.TypeHandleStructNestedFields)
                nonQueryFields.Add(nonQueryField);
        }
        foreach (var nonQueryField in nonQueryFields)
            nonQueryField.AppendMemberDeclaration(writer, fieldsArePublic);

        writer.WriteLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        writer.WriteLine("public void __AssignHandles(ref global::Unity.Entities.SystemState state)");
        writer.WriteLine("{");
        writer.Indent++;
        foreach (var nonQueryField in nonQueryFields)
            writer.WriteLine(nonQueryField.GetMemberAssignment());
        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine();

        if (shouldHaveUpdate)
        {
            writer.WriteLine("public void Update(ref global::Unity.Entities.SystemState state)");
            writer.WriteLine("{");
            writer.Indent++;
            foreach (var nonQueryField in nonQueryFields)
                writer.WriteLine($"{nonQueryField.GeneratedFieldName}.Update(ref state);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
        }

        writer.Indent--;
        writer.WriteLine("}");

        writer.WriteLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        writer.WriteLine("void __AssignQueries(ref global::Unity.Entities.SystemState state)");
        writer.WriteLine("{");
        writer.Indent++;

        writer.WriteLine("var entityQueryBuilder = new global::Unity.Entities.EntityQueryBuilder(global::Unity.Collections.Allocator.Temp);");
        foreach (var kvp in allQueryFieldsToGenerate)
            kvp.Key.WriteEntityQueryFieldAssignment(writer, kvp.Value);

        writer.WriteLine("entityQueryBuilder.Dispose();");

        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine();

        writeAdditionalSyntaxInInternalCompilerQueryAndHandleData?.Invoke(writer);

        writer.Indent--;
        writer.WriteLine("}");
    }
}
