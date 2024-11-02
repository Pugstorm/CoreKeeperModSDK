using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.Common
{
    public static class SymbolExtensions
    {
        static SymbolDisplayFormat QualifiedFormat { get; } =
            new(
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                miscellaneousOptions:
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        static SymbolDisplayFormat QualifiedFormatWithoutGlobalPrefix { get; } =
            new(
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                miscellaneousOptions:
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        public static bool Is(this ITypeSymbol symbol, string fullyQualifiedName, bool checkBaseType = true)
        {
            fullyQualifiedName = PrependGlobalIfMissing(fullyQualifiedName);

            if (symbol is null)
                return false;

            if (symbol.ToDisplayString(QualifiedFormat) == fullyQualifiedName)
                return true;

            return checkBaseType && symbol.BaseType.Is(fullyQualifiedName);
        }

        public static bool IsInt(this ITypeSymbol symbol) => symbol.SpecialType == SpecialType.System_Int32;

        public static bool IsDynamicBuffer(this ITypeSymbol symbol) =>
            symbol.Name == "DynamicBuffer" && symbol.ContainingNamespace.ToDisplayString(QualifiedFormat) == "global::Unity.Entities";

        public static bool IsSharedComponent(this ITypeSymbol symbol) => symbol.InheritsFromInterface("Unity.Entities.ISharedComponentData");

        public static bool IsComponent(this ITypeSymbol symbol) => symbol.InheritsFromInterface("Unity.Entities.IComponentData");

        public static bool IsZeroSizedComponent(this ITypeSymbol symbol, HashSet<ITypeSymbol> seenSymbols = null)
        {
// TODO: This was recently fixed (https://github.com/dotnet/roslyn-analyzers/issues/5804), remove pragmas after we update .net
#pragma warning disable RS1024
            seenSymbols ??= new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default) { symbol };
#pragma warning restore RS1024

            foreach (var field in symbol.GetMembers().OfType<IFieldSymbol>())
            {
                switch (symbol.SpecialType)
                {
                    case SpecialType.System_Void:
                        continue;
                    case SpecialType.None:
                        if (field.IsStatic || field.IsConst)
                            continue;

                        if (field.Type.TypeKind == TypeKind.Struct)
                        {
                            // Handle cycles in type (otherwise we will stack overflow)
                            if (!seenSymbols.Add(field.Type))
                                continue;

                            if (IsZeroSizedComponent(field.Type))
                                continue;
                        }
                        return false;
                    default:
                        return false;
                }
            }
            return true;
        }

        public static bool IsEnableableComponent(this ITypeSymbol symbol) => symbol.InheritsFromInterface("Unity.Entities.IEnableableComponent");

        public static string ToFullName(this ISymbol symbol) => symbol.ToDisplayString(QualifiedFormat);

        static string ToFullNameIL(this ITypeSymbol symbol)
        {
            var initialTypeArgument = symbol is INamedTypeSymbol namedTypeSymbol
                ? string.Join(",", namedTypeSymbol.TypeArguments.Select(t => t.ToFullNameIL()))
                : string.Empty;

            var typeArgumentBuilder = new StringBuilder(initialTypeArgument);
            var metaDataName = symbol switch
            {
                IArrayTypeSymbol array => $"{array.ElementType.ToFullNameIL()}[{(array.Rank == 1 ? string.Empty : string.Join(",", Enumerable.Range(0, array.Rank).Select(_=>"0...")))}]",
                IFunctionPointerTypeSymbol fp => $"method {fp.Signature.ReturnType.ToFullNameIL()} *({string.Join(",", fp.Signature.Parameters.Select(p => p.Type.ToFullNameIL()))})",
                IPointerTypeSymbol pointerTypeSymbol => $"{pointerTypeSymbol.PointedAtType.ToFullNameIL()}*",
                _ => symbol.MetadataName
            };
            var nameBuilder = new StringBuilder(metaDataName);

            // Walk up containing types
            for (var containingSymbol = symbol.ContainingSymbol; containingSymbol is not null; containingSymbol = containingSymbol.ContainingSymbol)
            {
                switch (containingSymbol)
                {
                    case INamedTypeSymbol containingType when symbol is not ITypeParameterSymbol:
                        nameBuilder.Insert(0, containingSymbol.MetadataName+'/');
                        if (containingType.TypeArguments.Length > 0)
                        {
                            var typeArgsFromContainingType = string.Join(",", containingType.TypeArguments.Select(t => t.ToFullNameIL()));
                            if (typeArgumentBuilder.Length == 0)
                                typeArgumentBuilder.Append(typeArgsFromContainingType);
                            else
                                typeArgumentBuilder.Insert(0, typeArgsFromContainingType+',');
                        }
                        break;
                    case INamespaceSymbol namespaceSymbol when symbol is not ITypeParameterSymbol:
                        if (!namespaceSymbol.IsGlobalNamespace)
                            nameBuilder.Insert(0, namespaceSymbol.MetadataName+".");
                        break;
                }
            }

            // Append TypeArguments at the end
            if (typeArgumentBuilder.Length > 0)
            {
                nameBuilder.Append('<');
                nameBuilder.Append(typeArgumentBuilder);
                nameBuilder.Append('>');
            }

            return nameBuilder.ToString();
        }

        public static string ToSimpleName(this ITypeSymbol symbol) => symbol.ToDisplayString(QualifiedFormatWithoutGlobalPrefix);

        public static string ToValidIdentifier(this ITypeSymbol symbol)
        {
            var validIdentifier = symbol.ToDisplayString(QualifiedFormatWithoutGlobalPrefix).Replace('.', '_');
            if (symbol is INamedTypeSymbol { IsGenericType: true })
                validIdentifier = validIdentifier.Replace('<', '_').Replace('>', '_');
            return validIdentifier;
        }

        public static bool ImplementsInterface(this ISymbol symbol, string interfaceName)
        {
            interfaceName = PrependGlobalIfMissing(interfaceName);

            return symbol is ITypeSymbol typeSymbol
                   && typeSymbol.AllInterfaces.Any(i => i.ToFullName() == interfaceName || i.InheritsFromInterface(interfaceName));
        }

        public static ITypeSymbol GetSymbolType(this ISymbol symbol)
        {
            return symbol switch
            {
                ILocalSymbol localSymbol => localSymbol.Type,
                IParameterSymbol parameterSymbol => parameterSymbol.Type,
                INamedTypeSymbol namedTypeSymbol => namedTypeSymbol,
                IMethodSymbol methodSymbol => methodSymbol.ContainingType,
                IPropertySymbol propertySymbol => propertySymbol.ContainingType,
                _ => throw new InvalidOperationException($"Unknown typeSymbol type {symbol.GetType()}")
            };
        }

        public static bool InheritsFromInterface(this ITypeSymbol symbol, string interfaceName, bool checkBaseType = true)
        {
            if (symbol is null)
                return false;

            interfaceName = PrependGlobalIfMissing(interfaceName);

            foreach (var @interface in symbol.Interfaces)
            {
                if (@interface.ToDisplayString(QualifiedFormat) == interfaceName)
                    return true;

                if (checkBaseType)
                {
                    foreach (var baseInterface in @interface.AllInterfaces)
                    {
                        if (baseInterface.ToDisplayString(QualifiedFormat) == interfaceName)
                            return true;
                        if (baseInterface.InheritsFromInterface(interfaceName))
                            return true;
                    }
                }
            }

            if (checkBaseType && symbol.BaseType != null)
                if (symbol.BaseType.InheritsFromInterface(interfaceName))
                    return true;

            return false;
        }

        public static bool InheritsFromType(this ITypeSymbol symbol, string typeName, bool checkBaseType = true)
        {
            typeName = PrependGlobalIfMissing(typeName);

            if (symbol is null)
                return false;

            if (symbol.ToDisplayString(QualifiedFormat) == typeName)
                return true;

            if (checkBaseType && symbol.BaseType != null)
                if (symbol.BaseType.InheritsFromType(typeName))
                    return true;

            return false;
        }

        public static bool HasAttribute(this ISymbol typeSymbol, string fullyQualifiedAttributeName)
        {
            fullyQualifiedAttributeName = PrependGlobalIfMissing(fullyQualifiedAttributeName);
            return typeSymbol.GetAttributes().Any(attribute => attribute.AttributeClass.ToFullName() == fullyQualifiedAttributeName);
        }

        public static bool HasAttributeOrFieldWithAttribute(this ITypeSymbol typeSymbol, string fullyQualifiedAttributeName)
        {
            fullyQualifiedAttributeName = PrependGlobalIfMissing(fullyQualifiedAttributeName);

            return typeSymbol.HasAttribute(fullyQualifiedAttributeName) ||
                   typeSymbol.GetMembers().OfType<IFieldSymbol>().Any(f => !f.IsStatic && f.Type.HasAttributeOrFieldWithAttribute(fullyQualifiedAttributeName));
        }

        public static string GetMethodAndParamsAsString<TDiagnostic>(this IMethodSymbol methodSymbol, TDiagnostic diagnosticReporter) where TDiagnostic : ISourceGeneratorDiagnosable
        {
            var strBuilder = new StringBuilder();

            strBuilder.Append(methodSymbol.Name);
            strBuilder.Append($"_T{methodSymbol.TypeParameters.Length}");
            foreach (var param in methodSymbol.Parameters)
            {
                if (param.RefKind == RefKind.In && !methodSymbol.IsOverride)
                    strBuilder.Append("_in");
                else if (param.RefKind == RefKind.Out)
                    strBuilder.Append("_out");
                else if (param.RefKind == RefKind.Ref)
                    strBuilder.Append("_ref");

                var paramILName = param.Type.ToFullNameIL();
                if (!string.IsNullOrEmpty(paramILName))
                    strBuilder.Append($"_{paramILName}");
                else
                {
                    diagnosticReporter.LogError("SGIL", "ILPP Failure", $"Failed to get IL name for parameter {param.Name}", param.Locations.FirstOrDefault() ?? Location.None);
                    return "";
                }

                if (param.RefKind != RefKind.None)
                    strBuilder.Append('&');
                if (methodSymbol.IsOverride && param.RefKind == RefKind.In)
                    strBuilder.Append(" modreq(System.Runtime.InteropServices.InAttribute)");
            }
            return strBuilder.ToString();
        }

        public static bool IsAspect(this ITypeSymbol typeSymbol) => typeSymbol.InheritsFromInterface("Unity.Entities.IAspect");

        static string PrependGlobalIfMissing(this string typeOrNamespaceName) =>
            !typeOrNamespaceName.StartsWith("global::") ? $"global::{typeOrNamespaceName}" : typeOrNamespaceName;
    }
}
