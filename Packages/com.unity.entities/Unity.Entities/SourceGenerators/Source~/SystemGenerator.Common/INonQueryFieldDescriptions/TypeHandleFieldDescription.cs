using System;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.Common;

public readonly struct TypeHandleFieldDescription : IEquatable<TypeHandleFieldDescription>, IMemberDescription
{
    public enum TypeHandleSource
    {
        None,
        Aspect,
        Component,
        SharedComponent,
        BufferElement
    }
    ITypeSymbol TypeSymbol { get; }
    bool IsReadOnly { get; }

    TypeHandleSource Source { get; } // I'm sure we know the type at the call site,
    // lets either split this into four classes
    // or see if we can find a way to
    // not have a bazillion INonQueryFieldDescription
    public string GeneratedFieldName { get; }

    public void AppendMemberDeclaration(IndentedTextWriter w, bool forcePublic = false)
    {
        switch (Source)
        {
            case TypeHandleSource.Aspect:
                if (IsReadOnly)
                    w.Write("[global::Unity.Collections.ReadOnly] ");
                if (forcePublic)
                    w.Write("public ");
                w.Write($"{TypeSymbol.ToFullName()}.TypeHandle {GeneratedFieldName};");
                break;
            case TypeHandleSource.BufferElement:
                if (IsReadOnly)
                    w.Write("[global::Unity.Collections.ReadOnly] ");
                if (forcePublic)
                    w.Write("public ");
                w.Write($"Unity.Entities.BufferTypeHandle<{TypeSymbol.ToFullName()}> {GeneratedFieldName};");
                break;
            case TypeHandleSource.Component:
                if (IsReadOnly)
                    w.Write("[global::Unity.Collections.ReadOnly] ");
                if (forcePublic)
                    w.Write("public ");
                w.Write($"Unity.Entities.ComponentTypeHandle<{TypeSymbol.ToFullName()}> {GeneratedFieldName};");
                break;
            default:
                if (IsReadOnly)
                    w.Write("[global::Unity.Collections.ReadOnly] ");
                if (forcePublic)
                    w.Write("public ");
                w.Write($"Unity.Entities.SharedComponentTypeHandle<{TypeSymbol.ToFullName()}> {GeneratedFieldName};");
                break;
        }
        w.WriteLine();
    }

    public string GetMemberAssignment() => Source switch
    {
        TypeHandleSource.Aspect => $"{GeneratedFieldName} = new {TypeSymbol.ToFullName()}.TypeHandle(ref state);",
        TypeHandleSource.BufferElement => $"{GeneratedFieldName} = state.GetBufferTypeHandle<{TypeSymbol.ToFullName()}>({(IsReadOnly ? "true" : "false")});",
        TypeHandleSource.Component =>
            TypeSymbol.IsReferenceType
                ? $"{GeneratedFieldName} = state.EntityManager.GetComponentTypeHandle<{TypeSymbol.ToFullName()}>({(IsReadOnly ? "true" : "false")});"
                : $"{GeneratedFieldName} = state.GetComponentTypeHandle<{TypeSymbol.ToFullName()}>({(IsReadOnly ? "true" : "false")});",
        _ => $"{GeneratedFieldName} = state.GetSharedComponentTypeHandle<{TypeSymbol.ToFullName()}>();"
    };

    public TypeHandleFieldDescription(ITypeSymbol typeSymbol, bool isReadOnly, TypeHandleSource forcedTypeHandleSource)
    {
        TypeSymbol = typeSymbol;
        IsReadOnly = isReadOnly;

        var typeParameterSymbol = typeSymbol as ITypeParameterSymbol;
        var isSpecifiedTypeSymbol = typeParameterSymbol == null;

        Debug.Assert(isSpecifiedTypeSymbol || (!isSpecifiedTypeSymbol && forcedTypeHandleSource != TypeHandleSource.None), "SG-DBG: Unspecified types require a forced type handle source");

        var typeSymbolValidIdentifier = TypeSymbol.ToValidIdentifier();
        if (isSpecifiedTypeSymbol)
        {
            if (typeSymbol.IsAspect())
            {
                GeneratedFieldName = $"__{typeSymbolValidIdentifier}_{(IsReadOnly ? "RO" : "RW")}_AspectTypeHandle";
                Source = TypeHandleSource.Aspect;
            }
            else if (typeSymbol.InheritsFromInterface("Unity.Entities.IBufferElementData"))
            {
                GeneratedFieldName = $"__{typeSymbolValidIdentifier}_{(IsReadOnly ? "RO" : "RW")}_BufferTypeHandle";
                Source = TypeHandleSource.BufferElement;
            }
            else if (typeSymbol.IsSharedComponent())
            {
                GeneratedFieldName = $"__{typeSymbolValidIdentifier}_SharedComponentTypeHandle";
                Source = TypeHandleSource.SharedComponent;
            }
            else
            {
                GeneratedFieldName = $"__{typeSymbolValidIdentifier}_{(IsReadOnly ? "RO" : "RW")}_ComponentTypeHandle";
                Source = TypeHandleSource.Component;
            }
        }
        else
        {
            var constraintTypes = typeParameterSymbol.ConstraintTypes;
            switch (forcedTypeHandleSource)
            {
                case TypeHandleSource.Aspect:
                    Debug.Assert(constraintTypes.Any(t => t.ToFullName() == "global::Unity.Entities.IAspect" || t.IsAspect()), "SG-DBG: Specified aspect types must be aspects");
                    GeneratedFieldName = $"__{typeSymbolValidIdentifier}_{(IsReadOnly ? "RO" : "RW")}_AspectTypeHandle";
                    Source = TypeHandleSource.Aspect;
                    break;
                case TypeHandleSource.BufferElement:
                    Debug.Assert(constraintTypes.Any(t => t.ToFullName() == "global::Unity.Entities.IBufferElementData" || t.InheritsFromInterface("Unity.Entities.IBufferElementData")), "SG-DBG: Specified buffer element types must be buffer element types");
                    GeneratedFieldName = $"__{typeSymbolValidIdentifier}_{(IsReadOnly ? "RO" : "RW")}_BufferTypeHandle";
                    Source = TypeHandleSource.BufferElement;
                    break;
                case TypeHandleSource.SharedComponent:
                    Debug.Assert(constraintTypes.Any(t => t.ToFullName() == "global::Unity.Entities.ISharedComponentData" || t.IsSharedComponent()), "SG-DBG: Specified shared component types must be shared component types");
                    GeneratedFieldName = $"__{typeSymbolValidIdentifier}_SharedComponentTypeHandle";
                    Source = TypeHandleSource.SharedComponent;
                    break;
                default:
                    Debug.Assert(constraintTypes.Any(t => t.ToFullName() == "global::Unity.Entities.IComponentData" || t.IsComponent()), "SG-DBG: Specified component types must be component types");
                    GeneratedFieldName = $"__{typeSymbolValidIdentifier}_{(IsReadOnly ? "RO" : "RW")}_ComponentTypeHandle";
                    Source = TypeHandleSource.Component;
                    break;
            }
        }
    }

    public bool Equals(TypeHandleFieldDescription other) =>
        SymbolEqualityComparer.Default.Equals(TypeSymbol, other.TypeSymbol) && IsReadOnly == other.IsReadOnly && Source == other.Source;

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode =  TypeSymbol != null ? SymbolEqualityComparer.Default.GetHashCode(TypeSymbol) : 0;
            hashCode = (hashCode * 397) ^ IsReadOnly.GetHashCode();
            hashCode = (hashCode * 397) ^ (int)Source;
            return hashCode;
        }
    }
}
