using System;
using System.Collections.Generic;

namespace Unity.Entities.SourceGen.SystemGenerator.SystemAPI.Query;

static class NestedStruct
{
    public readonly struct Field
    {
        public readonly string Declaration;
        public readonly string AssignmentInNestedStructConstructor;
        public readonly string Name;
        public readonly bool DependsOnEntityManagerField;

        public Field(
            string declaration,
            string name,
            string assignmentInNestedStructConstructor = null,
            bool dependsOnEntityManagerField = false)
        {
            Declaration = declaration;
            AssignmentInNestedStructConstructor = assignmentInNestedStructConstructor;
            Name = name;
            DependsOnEntityManagerField = dependsOnEntityManagerField;
        }
    }

    public readonly struct ArgumentInReturnedType
    {
        public readonly string Setup;
        public readonly string Value;

        public bool RequiresSetup => !string.IsNullOrEmpty(Setup);
        public bool IsEmpty => string.IsNullOrEmpty(Value);

        public ArgumentInReturnedType(string value, string setup = null)
        {
            Setup = setup;
            Value = value;
        }
    }

    internal static IEnumerable<(Field Field, ArgumentInReturnedType ArgumentInReturnedTupleDuringIndexAccess)>
        ResolvedChunk(IReadOnlyCollection<ReturnedTupleElementDuringEnumeration> elements, bool provideEntityAccess, bool performCollectionChecks)
    {
        foreach (var arg in elements)
        {
            string fieldName;
            string fieldDeclaration;
            string elementInReturnedTuple;

            switch (arg.Type)
            {
                case QueryType.Aspect:
                    fieldName = $"{arg.Name}_ResolvedChunk";
                    fieldDeclaration = $"public {arg.TypeSymbolFullName}.ResolvedChunk {fieldName};";
                    elementInReturnedTuple = $"{fieldName}[index]";
                    yield return
                    (
                        new Field(fieldDeclaration, fieldName),
                        new ArgumentInReturnedType(elementInReturnedTuple)
                    );
                    break;
                case QueryType.TagComponent:
                    fieldName = "";
                    fieldDeclaration = "";
                    elementInReturnedTuple = $"default({arg.TypeSymbolFullName})";
                    yield return
                    (
                        new Field(fieldDeclaration, fieldName),
                        new ArgumentInReturnedType(elementInReturnedTuple)
                    );
                    break;
                case QueryType.ValueTypeComponent:
                    fieldName = $"{arg.Name}_IntPtr";
                    fieldDeclaration = $"public global::System.IntPtr {fieldName};";
                    elementInReturnedTuple = $"Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetCopyOfNativeArrayPtrElement<{arg.TypeSymbolFullName}>({fieldName}, index)";
                    yield return
                    (
                        new Field(fieldDeclaration, fieldName),
                        new ArgumentInReturnedType(elementInReturnedTuple)
                    );
                    break;
                case QueryType.RefRW:
                    string typeHandleName = $"{arg.Name}_TypeHandle";

                    if (performCollectionChecks)
                    {
                        fieldName = typeHandleName;
                        fieldDeclaration = $"public Unity.Entities.ComponentTypeHandle<{arg.TypeArgumentFullName}> {fieldName};";

                        yield return
                        (
                            new Field(fieldDeclaration, fieldName),
                            new ArgumentInReturnedType(default)
                        );
                    }

                    fieldName = $"{arg.Name}_IntPtr";
                    fieldDeclaration = $"public global::System.IntPtr {fieldName};";
                    elementInReturnedTuple =
                        performCollectionChecks
                            ? $"Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetUncheckedRefRW<{arg.TypeArgumentFullName}>({fieldName}, index, ref {typeHandleName})"
                            : $"Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetUncheckedRefRW<{arg.TypeArgumentFullName}>({fieldName}, index)" ;

                    yield return
                    (
                        new Field(fieldDeclaration, fieldName),
                        new ArgumentInReturnedType(elementInReturnedTuple)
                    );
                    break;
                case QueryType.EnabledRefRW:
                    fieldName = $"{arg.Name}_EnabledMask";
                    fieldDeclaration = $"public Unity.Entities.EnabledMask {fieldName};";
                    elementInReturnedTuple = $"{fieldName}.GetEnabledRefRW<{arg.TypeArgumentFullName}>(index)";
                    yield return
                    (
                        new Field(fieldDeclaration, fieldName),
                        new ArgumentInReturnedType(elementInReturnedTuple)
                    );
                    break;
                case QueryType.RefRO:
                    string typeHandleName_= $"{arg.Name}_TypeHandle";

                    if (performCollectionChecks)
                    {
                        fieldName = typeHandleName_;
                        fieldDeclaration = $"[Unity.Collections.ReadOnly] public Unity.Entities.ComponentTypeHandle<{arg.TypeArgumentFullName}> {fieldName};";

                        yield return
                        (
                            new Field(fieldDeclaration, fieldName),
                            new ArgumentInReturnedType(default)
                        );
                    }

                    fieldName = $"{arg.Name}_IntPtr";
                    fieldDeclaration = $"public global::System.IntPtr {fieldName};";
                    elementInReturnedTuple =
                        performCollectionChecks
                            ? $"Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetUncheckedRefRO<{arg.TypeArgumentFullName}>({fieldName}, index, ref {typeHandleName_})"
                            : $"Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetUncheckedRefRO<{arg.TypeArgumentFullName}>({fieldName}, index)";

                    yield return
                    (
                        new Field(fieldDeclaration, fieldName),
                        new ArgumentInReturnedType(elementInReturnedTuple)
                    );
                    break;
                case QueryType.EnabledRefRO:
                    fieldName = $"{arg.Name}_EnabledMask";
                    fieldDeclaration = $"public Unity.Entities.EnabledMask {fieldName};";
                    elementInReturnedTuple = $"{fieldName}.GetEnabledRefRO<{arg.TypeArgumentFullName}>(index)";
                    yield return
                    (
                        new Field(fieldDeclaration, fieldName),
                        new ArgumentInReturnedType(elementInReturnedTuple)
                    );
                    break;
                case QueryType.UnmanagedSharedComponent:
                case QueryType.ManagedSharedComponent:
                    fieldName = $"{arg.Name}";
                    fieldDeclaration = $"public {arg.TypeSymbolFullName} {fieldName};";
                    elementInReturnedTuple = fieldName;
                    yield return
                    (
                        new Field(fieldDeclaration, fieldName),
                        new ArgumentInReturnedType(elementInReturnedTuple)
                    );
                    break;
                case QueryType.DynamicBuffer:
                    fieldName = $"{arg.Name}_BufferAccessor";
                    fieldDeclaration = $"public Unity.Entities.BufferAccessor<{arg.TypeArgumentFullName}> {fieldName};";
                    elementInReturnedTuple = $"{fieldName}[index]";
                    yield return
                    (
                        new Field(fieldDeclaration, fieldName),
                        new ArgumentInReturnedType(elementInReturnedTuple)
                    );
                    break;
                case QueryType.UnityEngineComponent:
                    fieldName = $"{arg.Name}_ManagedComponentAccessor";
                    fieldDeclaration = $"public Unity.Entities.ManagedComponentAccessor<{arg.TypeArgumentFullName}> {fieldName};";
                    elementInReturnedTuple = $"new Unity.Entities.SystemAPI.ManagedAPI.UnityEngineComponent<{arg.TypeArgumentFullName}>({fieldName}[index])";
                    yield return
                    (
                        new Field(fieldDeclaration, fieldName),
                        new ArgumentInReturnedType(elementInReturnedTuple)
                    );
                    break;
                case QueryType.ManagedComponent:
                    fieldName = $"{arg.Name}_ManagedComponentAccessor";
                    fieldDeclaration = $"public Unity.Entities.ManagedComponentAccessor<{arg.TypeSymbolFullName}> {fieldName};";
                    elementInReturnedTuple = $"{fieldName}[index]";
                    yield return
                    (
                        new Field(fieldDeclaration, fieldName),
                        new ArgumentInReturnedType(elementInReturnedTuple)
                    );
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        if (provideEntityAccess)
            yield return
            (
                new Field("public global::System.IntPtr Entity_IntPtr;", "Entity_IntPtr"),
                new ArgumentInReturnedType("Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetCopyOfNativeArrayPtrElement<Unity.Entities.Entity>(Entity_IntPtr, index)")
            );
    }

    public static IEnumerable<(Field Field, ArgumentInReturnedType ArgumentWhenInitializingResolvedChunk)>
        TypeHandle(IReadOnlyCollection<ReturnedTupleElementDuringEnumeration> elements, bool provideEntityAccess,
            bool performsCollectionChecks)
    {
        foreach (var arg in elements)
        {
            string fieldName;
            string fieldDeclaration;
            string fieldAssignment;
            string resolvedChunkInitializerArgument;
            string initializerArgumentSetUp = default;

            Field field;

            switch (arg.Type)
            {
                case QueryType.Aspect:
                    fieldName = $"{arg.Name}_AspectTypeHandle";
                    fieldDeclaration = $"{arg.TypeSymbolFullName}.TypeHandle {fieldName};";
                    fieldAssignment = $"{fieldName} = new {arg.TypeSymbolFullName}.TypeHandle(ref systemState);";
                    resolvedChunkInitializerArgument = $"{fieldName}.Resolve(archetypeChunk);";

                    yield return
                    (
                        new Field(
                            fieldDeclaration,
                            fieldName,
                            fieldAssignment),
                        new ArgumentInReturnedType(resolvedChunkInitializerArgument)
                    );
                    break;
                case QueryType.TagComponent:
                    fieldName = "";
                    fieldDeclaration = "";
                    fieldAssignment = "";
                    resolvedChunkInitializerArgument = "";

                    yield return
                    (
                        new Field(
                            fieldDeclaration,
                            fieldName,
                            fieldAssignment),
                        new ArgumentInReturnedType(resolvedChunkInitializerArgument)
                    );
                    break;
                case QueryType.ValueTypeComponent:
                    fieldName = $"{arg.Name}_ComponentTypeHandle_RO";
                    fieldDeclaration = $"[Unity.Collections.ReadOnly] Unity.Entities.ComponentTypeHandle<{arg.TypeSymbolFullName}> {fieldName};";
                    fieldAssignment = $"{fieldName} = systemState.GetComponentTypeHandle<{arg.TypeSymbolFullName}>(isReadOnly: true);";
                    resolvedChunkInitializerArgument = $"Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetChunkNativeArrayReadOnlyIntPtrWithoutChecks<{arg.TypeSymbolFullName}>(archetypeChunk, ref {fieldName});";

                    yield return
                    (
                        new Field(
                            fieldDeclaration,
                            fieldName,
                            fieldAssignment),
                        new ArgumentInReturnedType(resolvedChunkInitializerArgument)
                    );
                    break;
                case QueryType.RefRW:
                    fieldName = $"{arg.Name}_ComponentTypeHandle_RW";
                    fieldDeclaration = $"Unity.Entities.ComponentTypeHandle<{arg.TypeArgumentFullName}> {fieldName};";
                    fieldAssignment = $"{fieldName} = systemState.GetComponentTypeHandle<{arg.TypeArgumentFullName}>(isReadOnly: false);";

                    field = new Field(fieldDeclaration, fieldName, fieldAssignment);
                    if (performsCollectionChecks)
                    {
                        resolvedChunkInitializerArgument = $"{fieldName};";
                        yield return
                        (
                            field,
                            new ArgumentInReturnedType(resolvedChunkInitializerArgument)
                        );
                    }

                    resolvedChunkInitializerArgument =
                        $"Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetChunkNativeArrayIntPtrWithoutChecks<{arg.TypeArgumentFullName}>(archetypeChunk, ref {fieldName});";

                    yield return
                    (
                        field,
                        new ArgumentInReturnedType(resolvedChunkInitializerArgument)
                    );
                    break;
                case QueryType.EnabledRefRW:
                    fieldName = $"{arg.Name}_ComponentTypeHandle_RW";
                    fieldDeclaration = $"Unity.Entities.ComponentTypeHandle<{arg.TypeArgumentFullName}> {fieldName};";
                    fieldAssignment = $"{fieldName} = systemState.GetComponentTypeHandle<{arg.TypeArgumentFullName}>(isReadOnly: false);";
                    resolvedChunkInitializerArgument = $"archetypeChunk.GetEnabledMask(ref {fieldName});";

                    yield return
                    (
                        new Field(
                            fieldDeclaration,
                            fieldName,
                            fieldAssignment),
                        new ArgumentInReturnedType(resolvedChunkInitializerArgument)
                    );
                    break;
                case QueryType.RefRO:
                    fieldName = $"{arg.Name}_ComponentTypeHandle_RO";
                    fieldDeclaration = $"[Unity.Collections.ReadOnly] Unity.Entities.ComponentTypeHandle<{arg.TypeArgumentFullName}> {fieldName};";
                    fieldAssignment = $"{fieldName} = systemState.GetComponentTypeHandle<{arg.TypeArgumentFullName}>(isReadOnly: true);";

                    field = new Field(fieldDeclaration, fieldName, fieldAssignment);

                    if (performsCollectionChecks)
                    {
                        resolvedChunkInitializerArgument = $"{fieldName};";
                        yield return
                        (
                            field,
                            new ArgumentInReturnedType(resolvedChunkInitializerArgument)
                        );
                    }

                    resolvedChunkInitializerArgument =
                        $"Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetChunkNativeArrayReadOnlyIntPtrWithoutChecks<{arg.TypeArgumentFullName}>(archetypeChunk, ref {fieldName});";

                    yield return
                    (
                        field,
                        new ArgumentInReturnedType(resolvedChunkInitializerArgument)
                    );
                    break;
                case QueryType.EnabledRefRO:
                    fieldName = $"{arg.Name}_ComponentTypeHandle_RO";
                    fieldDeclaration = $"[Unity.Collections.ReadOnly] Unity.Entities.ComponentTypeHandle<{arg.TypeArgumentFullName}> {fieldName};";
                    fieldAssignment = $"{fieldName} = systemState.GetComponentTypeHandle<{arg.TypeArgumentFullName}>(isReadOnly: true);";
                    resolvedChunkInitializerArgument = $"archetypeChunk.GetEnabledMask(ref {fieldName});";

                    yield return
                    (
                        new Field(
                            fieldDeclaration,
                            fieldName,
                            fieldAssignment),
                        new ArgumentInReturnedType(resolvedChunkInitializerArgument)
                    );
                    break;
                case QueryType.UnmanagedSharedComponent:
                    fieldName = $"{arg.Name}_SharedComponentTypeHandle_RO";
                    fieldDeclaration = $"[Unity.Collections.ReadOnly] Unity.Entities.SharedComponentTypeHandle<{arg.TypeSymbolFullName}> {fieldName};";
                    fieldAssignment = $"{fieldName} = systemState.GetSharedComponentTypeHandle<{arg.TypeSymbolFullName}>();";
                    resolvedChunkInitializerArgument = $"{arg.Name};";
                    initializerArgumentSetUp = $"var {arg.Name} = archetypeChunk.GetSharedComponent({fieldName}, _entityManager);";

                    yield return
                    (
                        new Field(
                            fieldDeclaration,
                            fieldName,
                            fieldAssignment,
                            dependsOnEntityManagerField: true),
                        new ArgumentInReturnedType(resolvedChunkInitializerArgument, initializerArgumentSetUp)
                    );
                    break;
                case QueryType.ManagedSharedComponent:
                    fieldName = $"{arg.Name}_SharedComponentTypeHandle_RO";
                    fieldDeclaration = $"[Unity.Collections.ReadOnly] Unity.Entities.SharedComponentTypeHandle<{arg.TypeSymbolFullName}> {fieldName};";
                    fieldAssignment = $"{fieldName} = systemState.GetSharedComponentTypeHandle<{arg.TypeSymbolFullName}>();";
                    resolvedChunkInitializerArgument = $"{arg.Name};";
                    initializerArgumentSetUp = $"var {arg.Name} = archetypeChunk.GetSharedComponentManaged({fieldName}, _entityManager);";

                    yield return
                    (
                        new Field(
                            fieldDeclaration,
                            fieldName,
                            fieldAssignment,
                            dependsOnEntityManagerField: true),
                        new ArgumentInReturnedType(resolvedChunkInitializerArgument, initializerArgumentSetUp)
                    );
                    break;
                case QueryType.UnityEngineComponent:
                    fieldName = $"{arg.Name}_ManagedComponentTypeHandle_RO";
                    fieldDeclaration = $"[Unity.Collections.ReadOnly] Unity.Entities.ComponentTypeHandle<{arg.TypeArgumentFullName}> {fieldName};";
                    fieldAssignment = $"{fieldName} = systemState.EntityManager.GetComponentTypeHandle<{arg.TypeArgumentFullName}>(isReadOnly: false);";
                    resolvedChunkInitializerArgument = $"archetypeChunk.GetManagedComponentAccessor(ref {fieldName}, _entityManager);";

                    yield return
                    (
                        new Field(
                            fieldDeclaration,
                            fieldName,
                            fieldAssignment,
                            dependsOnEntityManagerField: true),
                        new ArgumentInReturnedType(resolvedChunkInitializerArgument)
                    );
                    break;
                case QueryType.ManagedComponent:
                    fieldName = $"{arg.Name}_ManagedComponentTypeHandle_RO";
                    fieldDeclaration = $"[Unity.Collections.ReadOnly] Unity.Entities.ComponentTypeHandle<{arg.TypeSymbolFullName}> {fieldName};";
                    fieldAssignment = $"{fieldName} = systemState.EntityManager.GetComponentTypeHandle<{arg.TypeSymbolFullName}>(isReadOnly: false);";
                    resolvedChunkInitializerArgument = $"archetypeChunk.GetManagedComponentAccessor(ref {fieldName}, _entityManager);";

                    yield return
                    (
                        new Field(
                            fieldDeclaration,
                            fieldName,
                            fieldAssignment,
                            dependsOnEntityManagerField: true),
                        new ArgumentInReturnedType(resolvedChunkInitializerArgument)
                    );
                    break;
                case QueryType.DynamicBuffer:
                    fieldName = $"{arg.Name}_BufferTypeHandle_RW";
                    fieldDeclaration = $"Unity.Entities.BufferTypeHandle<{arg.TypeArgumentFullName}> {fieldName};";
                    fieldAssignment = $"{fieldName} = systemState.GetBufferTypeHandle<{arg.TypeArgumentFullName}>(isReadOnly: false);";
                    resolvedChunkInitializerArgument = $"archetypeChunk.GetBufferAccessor(ref {fieldName});";

                    yield return
                    (
                        new Field(
                            fieldDeclaration,
                            fieldName,
                            fieldAssignment),
                        new ArgumentInReturnedType(resolvedChunkInitializerArgument)
                    );
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        if (provideEntityAccess)
        {
            var entityField =
                new Field(
                    "Unity.Entities.EntityTypeHandle Entity_TypeHandle;",
                    "Entity_TypeHandle",
                    "Entity_TypeHandle = systemState.GetEntityTypeHandle();");
            yield return (entityField, new ArgumentInReturnedType("Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetChunkEntityArrayIntPtr(archetypeChunk, Entity_TypeHandle);"));
        }
    }

    internal static IEnumerable<string> InitializeResolvedChunkInstanceInTypeHandle(
        IEnumerable<(Field ResolvedChunkField, ArgumentInReturnedType Argument)> pairedFields)
    {
        foreach (var pair in pairedFields)
        {
            if (string.IsNullOrEmpty(pair.ResolvedChunkField.Name))
                continue;

            if (pair.Argument.RequiresSetup)
                yield return pair.Argument.Setup;

            yield return $"resolvedChunk.{pair.ResolvedChunkField.Name} = {pair.Argument.Value}";
        }
    }
}
