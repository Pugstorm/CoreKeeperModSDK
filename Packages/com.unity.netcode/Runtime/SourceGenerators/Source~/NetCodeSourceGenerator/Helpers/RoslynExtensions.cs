using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.NetCode.Generators;

namespace Unity.NetCode.Roslyn
{
    /// <summary>
    /// Some extension to provide more user friendly access to some type information in roslyn.
    /// TODO Should be nice to have a common effort to collect and share a common set of utilities that make sense
    /// </summary>
    internal static class Extensions
    {
        private static bool IsPrimitive(ITypeSymbol symbol)
        {
            switch (symbol.SpecialType)
            {
                case SpecialType.System_Boolean:
                    return true;
                case SpecialType.System_Char:
                    return true;
                case SpecialType.System_SByte:
                    return true;
                case SpecialType.System_Byte:
                    return true;
                case SpecialType.System_Int16:
                    return true;
                case SpecialType.System_UInt16:
                    return true;
                case SpecialType.System_Int32:
                    return true;
                case SpecialType.System_UInt32:
                    return true;
                case SpecialType.System_Int64:
                    return true;
                case SpecialType.System_UInt64:
                    return true;
                case SpecialType.System_Single:
                    return true;
                case SpecialType.System_Double:
                    return true;
                case SpecialType.System_IntPtr:
                    return true;
                case SpecialType.System_UIntPtr:
                    return true;
                default:
                    return false;
            }
        }

        public static string GetFieldTypeName(this ITypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                    return "bool";
                case SpecialType.System_SByte:
                    return "sbyte";
                case SpecialType.System_Byte:
                    return "byte";
                case SpecialType.System_Int16:
                    return "short";
                case SpecialType.System_UInt16:
                    return "ushort";
                case SpecialType.System_Int32:
                    return "int";
                case SpecialType.System_UInt32:
                    return "uint";
                case SpecialType.System_Int64:
                    return "long";
                case SpecialType.System_UInt64:
                    return "ulong";
                case SpecialType.System_Single:
                    return "float";
                case SpecialType.System_Double:
                    return "double";
                case SpecialType.System_IntPtr:
                    return "iptr";
                case SpecialType.System_UIntPtr:
                    return "uptr";
                default:
                    //TODO: need full type specifier??
                    return type.ToDisplayString(QualifiedTypeFormat);
            }
        }

        public static bool IsEnum(ITypeSymbol symbol)
        {
            return symbol.TypeKind == TypeKind.Enum ||
                   symbol.SpecialType == SpecialType.System_Enum;
        }

        public static string GetUnderlyingTypeName(ITypeSymbol enumType)
        {
            if (!IsEnum(enumType))
                return string.Empty;
            var underlyingType = ((INamedTypeSymbol) enumType).EnumUnderlyingType;
            return underlyingType?.ToDisplayString(QualifiedTypeFormatNoSpecial);
        }

        public static bool IsStruct(ITypeSymbol symbol)
        {
            return symbol.TypeKind == TypeKind.Struct || symbol.IsReferenceType &&
                symbol.TypeKind == TypeKind.Class;
        }

        public static bool IsBuffer(ITypeSymbol symbol)
        {
            return symbol.TypeKind == TypeKind.Struct &&
                   symbol.ImplementsInterface("Unity.Entities.IBufferElementData");
        }

        public static bool IsCommand(ITypeSymbol symbol)
        {
            return symbol.TypeKind == TypeKind.Struct &&
                   symbol.ImplementsInterface("Unity.NetCode.ICommandData");
        }

        public static bool IsRpc(ITypeSymbol symbol)
        {
            return symbol.TypeKind == TypeKind.Struct &&
                   symbol.ImplementsInterface("Unity.NetCode.IRpcCommand");
        }

        public static bool IsComponent(ITypeSymbol symbol)
        {
            return symbol.TypeKind == TypeKind.Struct &&
                   symbol.ImplementsInterface("Unity.Entities.IComponentData");
        }

        public static GenTypeKind GetTypeKind(ITypeSymbol symbol)
        {
            if (IsPrimitive(symbol))
                return GenTypeKind.Primitive;
            if (IsEnum(symbol))
                return GenTypeKind.Enum;
            if (IsStruct(symbol))
                return GenTypeKind.Struct;
            return GenTypeKind.Invalid;
        }


        public static IEnumerable<ComponentType> GetAllComponentType(ITypeSymbol symbol)
        {
            using (new Profiler.Auto("GetAllComponentType"))
            {
                ComponentType componentType;
                var allInterfaces = symbol.Interfaces;
                foreach (var intefaceSymbol in allInterfaces)
                {
                    if(TryGetComponentTypeFromInterface(intefaceSymbol, out componentType))
                        yield return componentType;
                }
                if (symbol.BaseType != null && TryGetComponentTypeFromInterface(symbol.BaseType, out componentType))
                    yield return componentType;
            }
        }

        public static ComponentType GetComponentType(ITypeSymbol symbol)
        {
            using (new Profiler.Auto("GetComponentType"))
            {
                ComponentType componentType;
                var allInterfaces = symbol.Interfaces;
                foreach (var intefaceSymbol in allInterfaces)
                {
                    if (TryGetComponentTypeFromInterface(intefaceSymbol, out componentType))
                        return componentType;
                }
                if (symbol.BaseType != null && TryGetComponentTypeFromInterface(symbol.BaseType, out componentType))
                    return componentType;
                return ComponentType.Unknown;
            }
        }

        private static bool TryGetComponentTypeFromInterface(INamedTypeSymbol interfaceSymbol, out ComponentType componentType)
        {
            var interfaceQualifiedName = interfaceSymbol.ToDisplayString(QualifiedTypeFormat);

            // Detecting the type here for interfaces inheriting ICommandData is important for
            // InputBufferData when parsing it as a component (type needs to be set to ComponentType.CommandData) or the
            // default ghost component parameters will not be set properly
            if (interfaceQualifiedName == "Unity.NetCode.ICommandData" ||
                interfaceSymbol.InheritsFromInterface("Unity.NetCode.ICommandData"))
            {
                componentType = ComponentType.CommandData;
                return true;
            }

            if (interfaceQualifiedName == "Unity.NetCode.IRpcCommand" ||
                interfaceSymbol.InheritsFromInterface("Unity.NetCode.IRpcCommand"))
            {
                componentType = ComponentType.Rpc;
                return true;
            }

            if (interfaceQualifiedName == "Unity.NetCode.IInputComponentData" ||
                interfaceSymbol.InheritsFromInterface("Unity.NetCode.IInputComponentData"))
            {
                componentType = ComponentType.Input;
                return true;
            }

            if (interfaceQualifiedName == "Unity.Entities.IComponentData" ||
                interfaceSymbol.InheritsFromInterface("Unity.Entities.IComponentData"))
            {
                componentType = ComponentType.Component;
                return true;
            }
            if (interfaceQualifiedName == "UnityEngine.Component" ||
                Roslyn.Extensions.InheritsFromBase(interfaceSymbol, "UnityEngine.Component"))
            {
                componentType = ComponentType.HybridComponent;
                return true;
            }

            if (interfaceQualifiedName == "Unity.Entities.IBufferElementData" ||
                interfaceSymbol.InheritsFromInterface("Unity.Entities.IBufferElementData"))
            {
                componentType = ComponentType.Buffer;
                return true;
            }
            componentType = ComponentType.Unknown;
            return false;
        }

        private static SymbolDisplayFormat QualifiedTypeFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                                  SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        private static SymbolDisplayFormat QualifiedTypeFormatNoSpecial = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);
        private static SymbolDisplayFormat NameOnlyFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);
        public static bool ImplementsInterface(this ITypeSymbol typeSymbol, string interfaceName)
        {
            using (new Profiler.Auto("ImplementsInterface"))
            {
                return typeSymbol.AllInterfaces.Any(i =>
                    i.ToDisplayString(QualifiedTypeFormat) == interfaceName || i.InheritsFromInterface(interfaceName));
            }
        }

        public static bool InheritsFromInterface(this ITypeSymbol symbol, string interfaceName, bool exact = false)
        {
            if (symbol is null)
                return false;

            foreach (var iface in symbol.Interfaces)
            {
                if (iface.ToDisplayString(QualifiedTypeFormat) == interfaceName)
                    return true;

                if (!exact)
                {
                    foreach (var baseInterface in iface.AllInterfaces)
                    {
                        if (baseInterface.ToDisplayString(QualifiedTypeFormat) == interfaceName)
                            return true;
                        if (baseInterface.InheritsFromInterface(interfaceName))
                            return true;
                    }
                }
            }

            if (!exact && symbol.BaseType != null)
                return symbol.BaseType.InheritsFromInterface(interfaceName);

            return false;
        }
        public static bool InheritsFromBase(this ITypeSymbol symbol, string baseName)
        {
            if (symbol is null)
                return false;

            while (symbol.BaseType != null)
            {
                symbol = symbol.BaseType;
                if (symbol.ToDisplayString(QualifiedTypeFormat) == baseName)
                    return true;
            }

            return false;
        }


        public static string GetFullTypeName(this ISymbol symbol)
        {
            var fullName = GetTypeNameWithDeclaringTypename(symbol);
            var ns = GetFullyQualifiedNamespace(symbol);
            if (string.IsNullOrEmpty(ns))
                return fullName;
            return string.Concat(ns, ".", fullName);
        }

        //return an ECMA compliant fully qualified name:
        // - The namespace.[XXX+]TypeName if the type is not generic
        // - The namespace.[XXX+]TypeName`N[[NameWithNamespaceAndContainingType, assembly],..]  if the type is generic
        public static string GetMetadataQualifiedName(ISymbol symbol)
        {
            var sb = new StringBuilder(symbol.MetadataName);
            var qualifiedNamespace = GetFullyQualifiedNamespace(symbol);
            if (((INamedTypeSymbol)symbol).IsGenericType)
            {
                sb.Append('[');
                foreach (var parameter in ((INamedTypeSymbol)symbol).TypeArguments)
                {
                    sb.Append($"[{parameter.ToDisplayString(QualifiedTypeFormat)}, {parameter.ContainingAssembly.ToDisplayString()}]");
                    sb.Append(',');
                }
                sb.Length -= 1;
                sb.Append(']');
            }
            while(symbol.ContainingType != null)
            {
                sb.Insert(0, $"{symbol.ContainingType.OriginalDefinition.ToDisplayString(NameOnlyFormat)}+");
                symbol = symbol.ContainingType;
            }
            if(!string.IsNullOrWhiteSpace(qualifiedNamespace))
                sb.Insert(0, $"{qualifiedNamespace}.");
            return sb.ToString();
        }

        //es: struct A { struct B { struct C} } } would return a string like A+B+C
        public static string GetTypeNameWithDeclaringTypename(ISymbol symbol)
        {
            var declaring = new List<string>(3);
            if (((INamedTypeSymbol)symbol).IsGenericType)
            {
                var n = new StringBuilder();
                n.Append(symbol.Name);
                n.Append('<');
                foreach (var parameter in ((INamedTypeSymbol)symbol).TypeArguments)
                {
                    n.Append(parameter.ToDisplayString(QualifiedTypeFormat));
                    n.Append(',');
                }
                n.Length -= 1;
                n.Append('>');
                declaring.Add(n.ToString());
            }
            else
                declaring.Add(symbol.Name);

            var p = symbol;
            while (p.ContainingType != null)
            {
                declaring.Add(p.ContainingType.ToDisplayString(NameOnlyFormat));
                p = p.ContainingType;
            }
            declaring.Reverse();
            return string.Join("+", declaring);
        }

        public static string GetFullyQualifiedNamespace(ISymbol symbol)
        {
            if (symbol.ContainingNamespace == null || symbol.ContainingNamespace.IsGlobalNamespace)
                return string.Empty;
            return symbol.ContainingNamespace.ToDisplayString();
        }

        /// <summary>
        /// This is the only trustful (i.e. proper) way to check for interfaces.
        /// This is also the reason why we can't rely on the SyntaxTreeVisitor for retrieving the candidates,
        /// and why we need to collect pretty much all the structs with at least one interface.
        /// </summary>
        public static AttributeData GetAttribute(ISymbol symbol, string attributeNamespace, string attributeName)
        {
            using (new Profiler.Auto("GetAttribute"))
            {
                return symbol.GetAttributes().FirstOrDefault(a =>
                    a.AttributeClass?.Name == attributeName &&
                    GetFullyQualifiedNamespace(a.AttributeClass) == attributeNamespace);
            }
        }
    }

    internal static class SyntaxExtensions
    {
        public static bool HasAttribute(this TypeDeclarationSyntax symbol, string attributeName)
        {
            return symbol.AttributeLists
                .SelectMany(list => list.Attributes.Select(a => a.Name.ToString()))
                .SingleOrDefault(a => a == attributeName) != null;
        }
        public static bool AnyAttribute(this TypeDeclarationSyntax symbol, string attributeName)
        {
            return symbol.AttributeLists
                .SelectMany(list => list.Attributes.Select(a => a.Name.ToString()))
                .SingleOrDefault(a => a == attributeName) != null;
        }
        public static string FullyQualifiedName(BaseTypeDeclarationSyntax declarationNode)
        {
            var identifiers = new List<string>(32) {declarationNode.Identifier.Text};
            SyntaxNode node = declarationNode;
            while (node.Parent != null)
            {
                if (node.Parent.IsKind(SyntaxKind.ClassDeclaration) || node.Parent.IsKind(SyntaxKind.StructDeclaration))
                    identifiers.Add((node.Parent as TypeDeclarationSyntax)?.Identifier.Text);
                else if (node.Parent.IsKind(SyntaxKind.NamespaceDeclaration))
                    identifiers.Add((node.Parent as NamespaceDeclarationSyntax)?.Name.ToString());

                node = node.Parent;
            }
            identifiers.Reverse();
            return string.Join(".", identifiers);
        }
    }
}
