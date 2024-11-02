using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.LambdaJobs;

interface IParamDescription
{
    public ParameterSyntax Syntax { get; }
    public ITypeSymbol TypeSymbol { get; }
    public string Name { get; }
    public bool IsByRef();
}

/// <summary>
/// A parameter that requires :
///     * A field of type IParamDescription.TypeSymbol to be inserted in the System class
///     * logic to be inserted in the execute method
///
/// The generator will create a unique field name composed from the field type and read-only state.
///
/// </summary>
interface IParamRequireUpdate : IParamDescription
{
    /// <summary>
    /// If the parameter is read-only.
    /// Used during the creation of a unique field name
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// The field name to generate
    /// Will be assigned by the execute method generator so it is a unique field name
    /// </summary>
    string FieldName { get; set; }

    /// <summary>
    /// Format the code that will be included in the execute method
    /// </summary>
    /// <param name="description"></param>
    /// <returns></returns>
    string FormatUpdateInvocation(LambdaJobDescription description);
}

interface IManagedComponentParamDescription : IParamDescription
{
}

interface ISharedComponentParamDescription : IParamDescription
{
}

interface IComponentParamDescription : IParamDescription
{
}

interface IDynamicBufferParamDescription : IParamDescription
{
}

public abstract class LambdaParamDescription
{
    public ParameterSyntax Syntax { get; }
    public ITypeSymbol TypeSymbol { get; }
    public ITypeSymbol EntityQueryTypeSymbol { get; protected set; }
    public string Name => Syntax != null ? Syntax.Identifier.ToString() : string.Empty;

    // E.g. writing Entities.ForEach((ref MyComponent a, ref MyComponent b) => {}), where MyComponent is an IComponentData type, is not allowed.
    // On the other hand, we allow two int parameters, e.g. when writing Entities.ForEach((Entity entity, int entityInQueryIndex, int nativeThreadIndex) => {}).
    internal virtual bool AllowDuplicateTypes => false;
    internal virtual bool IsSourceGeneratedParam => false;
    public bool IsByRef() => Syntax.GetModifierString() == "ref";
    internal virtual bool QueryTypeIsReadOnly() => false;
    internal virtual bool IsQueryableType => true;
    internal virtual string FieldAssignmentInGeneratedJobChunkType() => null;
    internal virtual string FieldInGeneratedJobChunkType() => null;
    internal virtual string GetNativeArrayOrAccessor() => null;

    internal LambdaParamDescription(ParameterSyntax syntax, ITypeSymbol typeSymbol, string lambdaJobName)
    {
        Syntax = syntax;
        TypeSymbol = typeSymbol;
        EntityQueryTypeSymbol = typeSymbol;
    }

    internal static LambdaParamDescription From(LambdaJobDescription description, ParameterSyntax param, string lambdaJobName)
    {
        var context = description.SystemDescription;

        var typeSymbol = context.SemanticModel.GetTypeInfo(param.Type).Type;

        if (typeSymbol.Is("Unity.Entities.EntityCommandBuffer.ParallelWriter"))
        {
            LambdaJobsErrors.DC0081(context, param.Identifier.ValueText, param.GetLocation());
            return null;
        }

        if (typeSymbol.InheritsFromInterface("Unity.Entities.ISharedComponentData"))
            return new LambdaParamDescription_SharedComponent(param, typeSymbol, lambdaJobName);
        if (typeSymbol.IsDynamicBuffer())
            return new LambdaParamDescription_DynamicBuffer(param, typeSymbol, lambdaJobName);
        if (typeSymbol.Is("Unity.Entities.Entity"))
            return new LambdaParamDescription_Entity(param, typeSymbol, lambdaJobName);

        if (typeSymbol.IsInt())
        {
            switch (param.Identifier.ValueText)
            {
                case "entityInQueryIndex":
                    return new LambdaParamDescription_EntityInQueryIndex(param, typeSymbol, lambdaJobName);
                case "nativeThreadIndex":
                    return new LambdaParamDescription_NativeThreadIndex(param, typeSymbol, lambdaJobName);
                default:
                    LambdaJobsErrors.DC0014(context, param.GetLocation(), param.Identifier.ValueText, new[] {"entityInQueryIndex", "nativeThreadIndex"});
                    return null;
            }
        }

        if (typeSymbol.IsValueType)
        {
            if (typeSymbol.Is("Unity.Entities.EntityCommandBuffer"))
            {
                return new LambdaParamDescription_EntityCommandBuffer(param, typeSymbol, lambdaJobName);
            }

            if (typeSymbol.InheritsFromInterface("Unity.Entities.IComponentData"))
            {
                // TODO: we can probably loosen this restriction with source generators (and allow generic IComponentData within reason), needs tests and validation
                if (typeSymbol is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.TypeArguments.Any())
                {
                    LambdaJobsErrors.DC0050(context, param.GetLocation(), typeSymbol.Name, description.LambdaJobKind);
                    return null;
                }

                if (typeSymbol.IsZeroSizedComponent())
                    return new LambdaParamDescription_TagComponent(param, typeSymbol, lambdaJobName);
                return new LambdaParamDescription_Component(param, typeSymbol, lambdaJobName);
            }

            if (typeSymbol.IsAspect())
            {
                foreach (var modifier in param.Modifiers)
                {
                    if (modifier.IsKind(SyntaxKind.InKeyword) || modifier.IsKind(SyntaxKind.RefKeyword))
                    {
                        LambdaJobsErrors.DC0082(context, param.GetLocation(), param.Identifier.ValueText);
                        return null;
                    }
                }

                return new LambdaParamDescription_Aspect(param, typeSymbol, lambdaJobName);
            }

            if (typeSymbol.InheritsFromInterface("Unity.Entities.IBufferElementData"))
                LambdaJobsErrors.DC0033(context, param.GetLocation(), param.Identifier.ValueText, typeSymbol.Name);
            else
            {
                if (typeSymbol is ITypeParameterSymbol)
                    LambdaJobsErrors.DC0050(context, param.GetLocation(), typeSymbol.Name, description.LambdaJobKind);
                else
                    LambdaJobsErrors.DC0021(context, param.GetLocation(), param.Identifier.ValueText, typeSymbol.Name);
            }
            return null;
        }

        if (typeSymbol.InheritsFromInterface("Unity.Entities.IComponentData") || typeSymbol.InheritsFromType("UnityEngine.Object"))
        {
            // TODO: we can probably loosen this restriction with source generators (and allow generic IComponentData within reason), needs tests and validation
            if (typeSymbol is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.TypeArguments.Any())
            {
                LambdaJobsErrors.DC0050(context, param.GetLocation(), typeSymbol.Name, description.LambdaJobKind);
                return null;
            }
            return new LambdaParamDescription_ManagedComponent(param, typeSymbol, lambdaJobName);
        }

        LambdaJobsErrors.DC0005(context, param.GetLocation(), param.Identifier.ToString(), typeSymbol.ToDisplayString(),  description.LambdaJobKind);
        return null;
    }

    internal virtual string LambdaBodyMethodParameter(bool usesBurst) => null;
    internal virtual string LambdaBodyParameterSetup() => null;
    internal virtual string LambdaBodyParameter() => null;
    internal virtual string StructuralChanges_ReadLambdaParam() => null;
    internal virtual string StructuralChanges_WriteBackLambdaParam() => null;
    internal virtual string StructuralChanges_LambdaBodyParameter() => null;
    internal virtual string StructuralChanges_GetTypeIndex() => null;
}

public class LambdaParamDescription_BatchIndex : LambdaParamDescription
{
    // Notice that we pass `batchIndex` instead of `entityInQueryIndex` as the sort key in our replacement code for performance reasons:
    // It is much faster to sort e.g. 10 batches than to sort 100,000 entities.
    internal override string LambdaBodyParameter() => "batchIndex";
    internal override string LambdaBodyMethodParameter(bool _) => "int __sortKey";
    internal override bool IsSourceGeneratedParam => true;

    public LambdaParamDescription_BatchIndex() : base(default, default, string.Empty)
    {
    }
    internal override bool IsQueryableType => false;
}

public class LambdaParamDescription_EntityCommandBuffer : LambdaParamDescription
{
    public const string GeneratedParallelWriterFieldNameInJobChunkType = "__ecbParallelWriter";
    public const string GeneratedEcbFieldNameInJobChunkType = "__entityCommandBuffer";
    public const string TemporaryJobEntityCommandBufferVariableName = "__tempJobEcb";

    public LambdaParamDescription_EntityCommandBuffer(ParameterSyntax parameterSyntax, ITypeSymbol typeSymbol, string lambdaJobName)
        : base(parameterSyntax, typeSymbol, lambdaJobName)
    {
    }

    public string GeneratedEcbFieldNameInSystemBaseType { get; set; }
    public (bool IsImmediate, SystemGenerator.LambdaJobs.ScheduleMode ScheduleMode, ITypeSymbol SystemType) Playback { get; set; }

    internal override string FieldAssignmentInGeneratedJobChunkType()
    {
        switch (Playback.ScheduleMode)
        {
            case ScheduleMode.ScheduleParallel:
                return $@"{GeneratedParallelWriterFieldNameInJobChunkType} = {GeneratedEcbFieldNameInSystemBaseType}.CreateCommandBuffer().AsParallelWriter()";
            case ScheduleMode.Schedule:
                return $@"{GeneratedEcbFieldNameInJobChunkType} = {GeneratedEcbFieldNameInSystemBaseType}.CreateCommandBuffer()";
            default:
                return
                    Playback.SystemType == null
                        ? $@"{GeneratedEcbFieldNameInJobChunkType} = {TemporaryJobEntityCommandBufferVariableName}"
                        : $@"{GeneratedEcbFieldNameInJobChunkType} = {GeneratedEcbFieldNameInSystemBaseType}.CreateCommandBuffer()";
        }
    }

    internal override string FieldInGeneratedJobChunkType() =>
        Playback.ScheduleMode == ScheduleMode.ScheduleParallel
            ? $"public global::Unity.Entities.EntityCommandBuffer.ParallelWriter {GeneratedParallelWriterFieldNameInJobChunkType};"
            : $"public global::Unity.Entities.EntityCommandBuffer {GeneratedEcbFieldNameInJobChunkType};";

    internal override bool IsQueryableType => false;
    internal override bool QueryTypeIsReadOnly() => true;
}


class LambdaParamDescription_Component : LambdaParamDescription, IComponentParamDescription, IParamRequireUpdate
{
    internal LambdaParamDescription_Component(ParameterSyntax syntax, ITypeSymbol typeSymbol, string lambdaJobName) : base(syntax, typeSymbol, lambdaJobName) {}
    internal override string FieldInGeneratedJobChunkType() => $@"{(Syntax.IsReadOnly() ? "[global::Unity.Collections.ReadOnly] " : "")}public global::Unity.Entities.ComponentTypeHandle<{TypeSymbol.ToFullName()}> __{Syntax.Identifier}TypeHandle;";
    internal override bool QueryTypeIsReadOnly() => Syntax.IsReadOnly();
    internal override string LambdaBodyMethodParameter(bool usesBurst) => $@"{"[Unity.Burst.NoAlias] ".EmitIfTrue(usesBurst)}{Syntax.GetModifierString()} {TypeSymbol.ToFullName()} {Syntax.Identifier}";
    internal override string GetNativeArrayOrAccessor()
    {
        return QueryTypeIsReadOnly()
            ? $@"var {Syntax.Identifier}ArrayPtr = global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetChunkNativeArrayReadOnlyIntPtr<{TypeSymbol.ToFullName()}>(chunk, ref __{Syntax.Identifier}TypeHandle);"
            : $@"var {Syntax.Identifier}ArrayPtr = global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetChunkNativeArrayIntPtr<{TypeSymbol.ToFullName()}>(chunk, ref __{Syntax.Identifier}TypeHandle);";
    }
    internal override string LambdaBodyParameter()
    {
        return Syntax.GetModifierString() switch
        {
            "ref" => $@"ref global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<{TypeSymbol.ToFullName()}>({Syntax.Identifier}ArrayPtr, entityIndex)",
            "in" => $@"in global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<{TypeSymbol.ToFullName()}>({Syntax.Identifier}ArrayPtr, entityIndex)",
            _ => $@"global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetCopyOfNativeArrayPtrElement<{TypeSymbol.ToFullName()}>({Syntax.Identifier}ArrayPtr, entityIndex)"
        };
    }

    internal override string FieldAssignmentInGeneratedJobChunkType() =>
        $@"__{Syntax.Identifier}TypeHandle = __TypeHandle.{FieldName}";

    internal override string StructuralChanges_GetTypeIndex() =>
        $@"var {Syntax.Identifier}TypeIndex = global::Unity.Entities.TypeManager.GetTypeIndex<{TypeSymbol.ToFullName()}>();";
    internal override string StructuralChanges_ReadLambdaParam() =>
        $@"var {Syntax.Identifier} = global::Unity.Entities.Internal.InternalCompilerInterface.GetComponentData<{TypeSymbol.ToFullName()}>(__this.EntityManager, entity, {Syntax.Identifier}TypeIndex, out var original{Syntax.Identifier});";
    internal override string StructuralChanges_WriteBackLambdaParam() =>
        Syntax.IsReadOnly() ? null : $@"global::Unity.Entities.Internal.InternalCompilerInterface.WriteComponentData<{TypeSymbol.ToFullName()}>(__this.EntityManager, entity, {Syntax.Identifier}TypeIndex, ref {Syntax.Identifier}, ref original{Syntax.Identifier});";
    internal override string StructuralChanges_LambdaBodyParameter() => $@"{Syntax.GetModifierString()} {Syntax.Identifier}";

    bool IParamRequireUpdate.IsReadOnly => QueryTypeIsReadOnly();
    public string FieldName { get; set; }
    string IParamRequireUpdate.FormatUpdateInvocation(LambdaJobDescription description)
    {
        return $@"__TypeHandle.{FieldName}.Update(ref this.CheckedStateRef);";
    }
}

class LambdaParamDescription_TagComponent : LambdaParamDescription, IComponentParamDescription
{
    internal LambdaParamDescription_TagComponent(ParameterSyntax syntax, ITypeSymbol typeSymbol, string lambdaJobName) : base(syntax, typeSymbol, lambdaJobName) {}
    internal override bool QueryTypeIsReadOnly() => true;
    internal override string LambdaBodyMethodParameter(bool usesBurst) =>
        $@"{"[Unity.Burst.NoAlias] ".EmitIfTrue(usesBurst)}{Syntax.GetModifierString()} {TypeSymbol.ToFullName()} {Syntax.Identifier}";
    internal override string LambdaBodyParameterSetup() => $@"{TypeSymbol.ToFullName()} {Syntax.Identifier} = default;";
    internal override string LambdaBodyParameter() => $"{Syntax.GetModifierString()} {Syntax.Identifier}";
    internal override string StructuralChanges_LambdaBodyParameter() => $@"{Syntax.GetModifierString()} {Syntax.Identifier}";
}

class LambdaParamDescription_ManagedComponent : LambdaParamDescription, IManagedComponentParamDescription
{
    internal LambdaParamDescription_ManagedComponent(ParameterSyntax syntax, ITypeSymbol typeSymbol, string lambdaJobName) : base(syntax, typeSymbol, lambdaJobName) {}
    internal override string FieldInGeneratedJobChunkType() => $@"public global::Unity.Entities.ComponentTypeHandle<{TypeSymbol.ToFullName()}> __{Syntax.Identifier}TypeHandle;";
    internal override bool QueryTypeIsReadOnly() => Syntax.IsReadOnly();
    internal override string LambdaBodyMethodParameter(bool usesBurst) => $@"{TypeSymbol.ToFullName()} {Syntax.Identifier}";
    internal override string GetNativeArrayOrAccessor() =>
        $@"var {Syntax.Identifier}Accessor = chunk.GetManagedComponentAccessor(ref __{Syntax.Identifier}TypeHandle, __this.EntityManager);";
    internal override string LambdaBodyParameter() => $@"{Syntax.Identifier}Accessor[entityIndex]";
    internal override string FieldAssignmentInGeneratedJobChunkType() =>
        $@"__{Syntax.Identifier}TypeHandle = this.EntityManager.GetComponentTypeHandle<{TypeSymbol.ToFullName()}>({(Syntax.IsReadOnly() ? "true" : "false")})";
    internal override string StructuralChanges_ReadLambdaParam() =>
        $@"var {Syntax.Identifier} = __this.EntityManager.GetComponentObject<{TypeSymbol.ToFullName()}>(entity);";
    internal override string StructuralChanges_LambdaBodyParameter() => $@"{Syntax.Identifier}";
}

class LambdaParamDescription_SharedComponent : LambdaParamDescription, ISharedComponentParamDescription
{
    internal LambdaParamDescription_SharedComponent(ParameterSyntax syntax, ITypeSymbol typeSymbol, string lambdaJobName) : base(syntax, typeSymbol, lambdaJobName) {}
    internal override string FieldInGeneratedJobChunkType() =>
        $@"{(Syntax.IsReadOnly() ? "[Unity.Collections.ReadOnly] " : "")}public global::Unity.Entities.SharedComponentTypeHandle<{TypeSymbol.ToFullName()}> __{Syntax.Identifier}TypeHandle;";
    internal override bool QueryTypeIsReadOnly() => Syntax.IsReadOnly();
    internal override string LambdaBodyMethodParameter(bool usesBurst) => $@"{Syntax.GetModifierString()} {TypeSymbol.ToFullName()} {Syntax.Identifier}";
    internal override string GetNativeArrayOrAccessor() =>
        $@"var __{Syntax.Identifier}Data = chunk.GetSharedComponentManaged(__{Syntax.Identifier}TypeHandle, __this.EntityManager);";
    internal override string LambdaBodyParameter() => $"__{Syntax.Identifier}Data";
    internal override string FieldAssignmentInGeneratedJobChunkType() => $@"__{Syntax.Identifier}TypeHandle = GetSharedComponentTypeHandle<{TypeSymbol.ToFullName()}>()";

    internal override string StructuralChanges_ReadLambdaParam() =>
        $@"var {Syntax.Identifier} = __this.EntityManager.GetSharedComponentManaged<{TypeSymbol.ToFullName()}>(entity);";
    internal override string StructuralChanges_LambdaBodyParameter() => $@"{Syntax.GetModifierString()} {Syntax.Identifier}";
}

class LambdaParamDescription_DynamicBuffer : LambdaParamDescription, IDynamicBufferParamDescription, IParamRequireUpdate
{
    ITypeSymbol _bufferGenericArgumentType;
    internal LambdaParamDescription_DynamicBuffer(ParameterSyntax syntax, ITypeSymbol typeSymbol, string lambdaJobName) : base(syntax, typeSymbol, lambdaJobName)
    {
        var namedTypeSymbol = (INamedTypeSymbol)typeSymbol;
        _bufferGenericArgumentType = namedTypeSymbol.TypeArguments.First();
        EntityQueryTypeSymbol = _bufferGenericArgumentType;
    }
    internal override string FieldInGeneratedJobChunkType() =>
        $@"public BufferTypeHandle<{_bufferGenericArgumentType}> __{Syntax.Identifier}TypeHandle;";
    internal override bool QueryTypeIsReadOnly() => Syntax.IsReadOnly();
    internal override string LambdaBodyMethodParameter(bool usesBurst) =>
        $@"DynamicBuffer<{_bufferGenericArgumentType}> {Syntax.Identifier}";
    internal override string GetNativeArrayOrAccessor() =>
        $@"var {Syntax.Identifier}Accessor = chunk.GetBufferAccessor(ref __{Syntax.Identifier}TypeHandle);";
    internal override string LambdaBodyParameter() => $@"{Syntax.Identifier}Accessor[entityIndex]";

    internal override string FieldAssignmentInGeneratedJobChunkType() =>
        $@"__{Syntax.Identifier}TypeHandle = __TypeHandle.{FieldName}";

    bool IParamRequireUpdate.IsReadOnly => QueryTypeIsReadOnly();
    public string FieldName { get; set; }
    string IParamRequireUpdate.FormatUpdateInvocation(LambdaJobDescription description)
    {
        return $@"__TypeHandle.{FieldName}.Update(ref this.CheckedStateRef);";
    }

    internal override string StructuralChanges_ReadLambdaParam() =>
        $@"var {Syntax.Identifier} = __this.EntityManager.GetBuffer<{_bufferGenericArgumentType}>(entity);";
    internal override string StructuralChanges_LambdaBodyParameter() => $@"{Syntax.Identifier}";
}

class LambdaParamDescription_Entity : LambdaParamDescription, IParamRequireUpdate
{
    internal LambdaParamDescription_Entity(ParameterSyntax syntax, ITypeSymbol typeSymbol, string lambdaJobName) : base(syntax, typeSymbol, lambdaJobName) {}
    internal override string FieldInGeneratedJobChunkType() => $@"[global::Unity.Collections.ReadOnly] public global::Unity.Entities.EntityTypeHandle __{Syntax.Identifier}TypeHandle;";
    internal override string LambdaBodyMethodParameter(bool usesBurst) => $@"global::Unity.Entities.Entity {Syntax.Identifier}";
    internal override string LambdaBodyParameter() =>
        $@"global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetCopyOfNativeArrayPtrElement<global::Unity.Entities.Entity>(__{Syntax.Identifier}ArrayPtr, entityIndex)";
    internal override string GetNativeArrayOrAccessor() =>
        $@"var __{Syntax.Identifier}ArrayPtr = global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetChunkEntityArrayIntPtr(chunk, __{Syntax.Identifier}TypeHandle);";

    internal override string FieldAssignmentInGeneratedJobChunkType()
    {
        return $@"__{Syntax.Identifier}TypeHandle = __TypeHandle.{FieldName}";
    }

    internal override string StructuralChanges_LambdaBodyParameter() => $@"entity";

    internal override bool IsQueryableType => false;

    public bool IsReadOnly => true;
    public string FieldName { get; set; }

    public string FormatUpdateInvocation(LambdaJobDescription description) => $@"__TypeHandle.{FieldName}.Update(ref this.CheckedStateRef);";


}

class LambdaParamDescription_EntityInQueryIndex : LambdaParamDescription
{
    internal LambdaParamDescription_EntityInQueryIndex(ParameterSyntax syntax, ITypeSymbol typeSymbol, string lambdaJobName) : base(syntax, typeSymbol, lambdaJobName) { }
    internal override string LambdaBodyMethodParameter(bool usesBurst) => $@"int {Syntax.Identifier}";
    internal override string LambdaBodyParameter() => @"entityInQueryIndex";
    internal override string StructuralChanges_LambdaBodyParameter() => @"entityIndex";
    internal override bool AllowDuplicateTypes => true;
    internal override bool IsQueryableType => false;
}

class LambdaParamDescription_NativeThreadIndex : LambdaParamDescription
{
    internal LambdaParamDescription_NativeThreadIndex(ParameterSyntax syntax, ITypeSymbol typeSymbol, string lambdaJobName) : base(syntax, typeSymbol, lambdaJobName) { }
    internal override string FieldInGeneratedJobChunkType() =>
        @"[global::Unity.Collections.LowLevel.Unsafe.NativeSetThreadIndexAttribute] internal int __NativeThreadIndex;";
    internal override string LambdaBodyMethodParameter(bool usesBurst) => $@"int {Syntax.Identifier}";
    internal override string LambdaBodyParameter() => $@"__NativeThreadIndex";
    internal override string FieldAssignmentInGeneratedJobChunkType() => $@"__NativeThreadIndex = 0";
    internal override string StructuralChanges_LambdaBodyParameter() => @"__NativeThreadIndex";
    internal override bool AllowDuplicateTypes => true;
    internal override bool IsQueryableType => false;
}


class LambdaParamDescription_Aspect : LambdaParamDescription, IParamRequireUpdate
{
    internal LambdaParamDescription_Aspect(ParameterSyntax syntax, ITypeSymbol typeSymbol, string lambdaJobName) : base(syntax, typeSymbol, lambdaJobName) { }

    internal override string FieldInGeneratedJobChunkType() => $@"{(Syntax.IsReadOnly() ? "[Unity.Collections.ReadOnly] " : "")}public {TypeSymbol.ToFullName()}.TypeHandle __{Syntax.Identifier}TypeHandle;";
    internal override bool QueryTypeIsReadOnly() => Syntax.IsReadOnly();
    internal override string LambdaBodyMethodParameter(bool usesBurst) => $@"{TypeSymbol.ToFullName()} {Syntax.Identifier}";
    internal override string GetNativeArrayOrAccessor()
    {
        return $@"var {Syntax.Identifier}ArrayPtr = __{Syntax.Identifier}TypeHandle.Resolve(chunk);";
    }
    internal override string LambdaBodyParameter()
    {
        return $@"{Syntax.Identifier}ArrayPtr[entityIndex]";
    }

    internal override string FieldAssignmentInGeneratedJobChunkType() =>
        $@"__{Syntax.Identifier}TypeHandle = __TypeHandle.{FieldName}";

    internal override string StructuralChanges_ReadLambdaParam()
    {
        return $@"var {Syntax.Identifier} = __this.GetAspect<{TypeSymbol.ToFullName()}>(entity);";
    }

    internal override string StructuralChanges_WriteBackLambdaParam() => "";

    internal override string StructuralChanges_LambdaBodyParameter() => $@"{Syntax.GetModifierString()} {Syntax.Identifier}";

    public string FieldName { get; set; }
    bool IParamRequireUpdate.IsReadOnly => QueryTypeIsReadOnly();
    string IParamRequireUpdate.FormatUpdateInvocation(LambdaJobDescription description)
        => $@"__TypeHandle.{FieldName}.Update(ref this.CheckedStateRef);";
}
