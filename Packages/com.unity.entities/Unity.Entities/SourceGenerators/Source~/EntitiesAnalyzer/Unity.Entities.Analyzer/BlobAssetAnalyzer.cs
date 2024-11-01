using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class BlobAssetAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                EntitiesDiagnostics.k_Ea0001Descriptor,
                EntitiesDiagnostics.k_Ea0002Descriptor,
                EntitiesDiagnostics.k_Ea0003Descriptor,
                EntitiesDiagnostics.k_Ea0009Descriptor);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(compilationCtx =>
            {
                var myCompileData = new CompileData(new ConcurrentDictionary<string, byte>(), new ConcurrentDictionary<string, byte>());
                compilationCtx.RegisterOperationAction(op => AnalyzeInvocation(op, myCompileData), OperationKind.Invocation);
                compilationCtx.RegisterOperationAction(op => AnalyzeVariableDeclarator(op, myCompileData),OperationKind.VariableDeclarator);
                compilationCtx.RegisterSymbolAction(op => AnalyzeParameter(op, myCompileData), SymbolKind.Parameter);
            });
        }

        void AnalyzeParameter(SymbolAnalysisContext context, CompileData compileData)
        {
            var parameterSymbol = context.Symbol as IParameterSymbol;
            if (parameterSymbol?.RefKind == RefKind.Ref)
                return;

            if (IsTypeRestrictedToBlobAssetStorage(parameterSymbol?.Type, compileData.nonBlobRestrictedTypes))
            {
                var typeName = parameterSymbol?.Type.ToFullName();
                var methodName = parameterSymbol.ContainingSymbol.ToString();
                var location = context.Symbol.Locations.First();
                context.ReportDiagnostic(Diagnostic.Create(EntitiesDiagnostics.k_Ea0009Descriptor, location, typeName, methodName));
            }
        }

        readonly struct CompileData
        {
            public readonly ConcurrentDictionary<string, byte> nonBlobRestrictedTypes;
            public readonly ConcurrentDictionary<string, byte> blobWithRefType;
            public CompileData(ConcurrentDictionary<string, byte> nonBlobRestrictedTypes, ConcurrentDictionary<string, byte> blobWithRefType)
            {
                this.nonBlobRestrictedTypes = nonBlobRestrictedTypes;
                this.blobWithRefType = blobWithRefType;
            }
        }

        static void AnalyzeVariableDeclarator(OperationAnalysisContext context, CompileData compileData)
        {
            var declarator = (IVariableDeclaratorOperation)context.Operation;
            var localSymbol = declarator.Symbol;
            if (localSymbol.RefKind != RefKind.Ref && IsTypeRestrictedToBlobAssetStorage(localSymbol.Type, compileData.nonBlobRestrictedTypes))
            {
                var fieldName = declarator.Initializer.Value.Syntax.ToString();
                var statement = context.Operation.Syntax.AncestorsAndSelf().OfType<StatementSyntax>().First();
                var location = statement.GetLocation();
                context.ReportDiagnostic(declarator.Initializer.Value.Kind == OperationKind.ObjectCreation
                    ? Diagnostic.Create(EntitiesDiagnostics.k_Ea0002Descriptor, location, fieldName)
                    : Diagnostic.Create(
                        EntitiesDiagnostics.k_Ea0001Descriptor, location, fieldName, declarator.GetVarTypeName(), declarator.Symbol.Name));
            }
        }

        static void AnalyzeInvocation(OperationAnalysisContext context, CompileData compileData)
        {
            var invocationOperation = (IInvocationOperation)context.Operation;
            var targetMethod = invocationOperation.TargetMethod;


            if (targetMethod.ContainingType.IsUnmanagedType
                && targetMethod.TypeArguments.Length == 1
                && targetMethod.Name == "ConstructRoot"
                && targetMethod.ContainingType.ToFullName() == "global::Unity.Entities.BlobBuilder")
            {
                var blobAssetType = targetMethod.TypeArguments[0];
                if (blobAssetType.TypeKind == TypeKind.TypeParameter || blobAssetType is INamedTypeSymbol { IsGenericType: true }) // Unresolved T
                    return;
                if (ContainBlobRefType(blobAssetType, out var fieldDescription, out var errorDescription, compileData.blobWithRefType))
                {
                    var blobAssetTypeFullName = blobAssetType.ToSimpleName();
                    context.ReportDiagnostic(Diagnostic.Create(EntitiesDiagnostics.k_Ea0003Descriptor, invocationOperation.Syntax.GetLocation(),
                        blobAssetTypeFullName, $"{blobAssetTypeFullName}{fieldDescription}", errorDescription));
                }
            }
        }

        // Checks if a type
        static bool IsTypeRestrictedToBlobAssetStorage(ITypeSymbol  type, ConcurrentDictionary<string,byte> nonRestrictedTypes)
        {
            if (type.TypeKind == TypeKind.TypeParameter)
                return false;
            if (type is IPointerTypeSymbol)
                return false;
            if (type is IArrayTypeSymbol)
                return false;
            if (type is INamedTypeSymbol namedType)
            {
                if (namedType.SpecialType != SpecialType.None) // IsPrimitive
                    return false;

                var fullName = type.ToSimpleName();
                if (nonRestrictedTypes.ContainsKey(fullName))
                    return false;

                var containingNamespace = type.ContainingNamespace.ToString();
                if (containingNamespace == "UnityEngine" ||
                    containingNamespace == "UnityEditor" ||
                    containingNamespace == "System"      ||
                    containingNamespace == "System.Private.CoreLib")
                    return false;

                if (namedType.IsUnboundGenericType)
                {
                    nonRestrictedTypes.TryAdd(fullName, default);
                    return false;
                }

                if (namedType.IsValueType)
                {
                    if (namedType.GetAttributes().Any(attribute => attribute.AttributeClass.ToFullName() == "global::Unity.Entities.MayOnlyLiveInBlobStorageAttribute"))
                        return true;

                    foreach (var symbol in namedType.GetMembers())
                    {
                        if (symbol.IsStatic)
                            continue;
                        if (symbol is IFieldSymbol field && IsTypeRestrictedToBlobAssetStorage(field.Type, nonRestrictedTypes))
                            return true;
                    }
                }

                nonRestrictedTypes.TryAdd(fullName, default);
            }
            return false;
        }

        // Checks if a known blob contains any reference types
        static bool ContainBlobRefType(ITypeSymbol type, out string firstFieldDescription, out string firstErrorDescription, ConcurrentDictionary<string,byte> blobWithRefType)
        {
            if (type.IsReferenceType)
            {
                firstErrorDescription = "is a reference.  Only non-reference types are allowed in Blobs.";
                firstFieldDescription = null;
                return true;
            }

            if (type is IPointerTypeSymbol)
            {
                firstErrorDescription = "is a pointer.  Only non-reference types are allowed in Blobs.";
                firstFieldDescription = null;
                return true;
            }

            if (type.SpecialType != SpecialType.None) // IsPrimitive
            {
                firstErrorDescription = null;
                firstFieldDescription = null;
                return false;
            }

            var typeFullName = type.ToFullName();
            if (blobWithRefType.ContainsKey(typeFullName))
            {
                firstFieldDescription = null;
                firstErrorDescription = null;
                return false;
            }
            blobWithRefType.TryAdd(typeFullName, default);

            if (type is INamedTypeSymbol { TypeArguments: { Length: 1 } } namedTypeSymbol && namedTypeSymbol.ContainingNamespace.ToString() == "Unity.Entities")
            {
                var isBlobArray = type.Name == "BlobArray";
                if (isBlobArray || type.Name == "BlobPtr")
                {
                    if (ContainBlobRefType(namedTypeSymbol.TypeArguments[0], out firstFieldDescription, out firstErrorDescription, blobWithRefType))
                    {
                        firstFieldDescription = (isBlobArray ? "[]" : ".Value") + firstFieldDescription;
                        return true;
                    }
                }
            }

            if (typeFullName == "global::Unity.Entities.Serialization.UntypedWeakReferenceId")
            {
                firstErrorDescription = "is an UntypedWeakReferenceId. Weak asset references are not yet supported in Blobs.";
                firstFieldDescription = null;
                return true;
            }

            foreach (var field in type.GetMembers())
            {
                if (field is IFieldSymbol fieldSymbol && !field.IsStatic)
                {
                    if (ContainBlobRefType(fieldSymbol.Type, out firstFieldDescription, out firstErrorDescription, blobWithRefType))
                    {
                        firstFieldDescription = $".{field.Name}{firstFieldDescription}";
                        return true;
                    }
                }
            }

            firstFieldDescription = null;
            firstErrorDescription = null;
            return false;
        }
    }
}
