// Uncomment to get debug logs
//#if DEBUG
//#define ASPECT_DEBUG
//#endif

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;
namespace Unity.Entities.SourceGen.Aspect
{
    [Generator]
    public class AspectGenerator : ISourceGenerator, IDiagnosticFrame
    {
        static readonly string s_GeneratorName = "Aspect";
        Dictionary<string, AspectDefinition> m_cache = null;
        Dictionary<string, AspectDefinition> AspectCache => m_cache ??= new Dictionary<string, AspectDefinition>();

        /// <summary>
        /// Register our syntax receiver
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new AspectReceiver(context.CancellationToken));
        }

        /// <summary>
        /// <param name="semanticModel"></param>
        /// <param name="node"></param>
        /// <param name="aspectField"></param>
        /// </summary>
        /// <returns></returns>
        bool TryParseField(IFieldSymbol field, AspectDefinition aspectDefinition, out AspectField aspectField)
        {
            DebugTrace.WriteLine($"Try Parsing Field {aspectDefinition.Name}.{field.Name} at {field.Locations.Select(x => x.ToString()).SeparateByNewLine()}");

            aspectField = default;
            var fieldTypeName = field.Type.ToFullName();

            // RefRW<T>
            if (fieldTypeName.StartsWith(AspectStrings.k_RefRWFullName))
                return TryParseFieldRef(aspectDefinition, field, out aspectField, isRO: false);

            // RefRO<T>
            if (fieldTypeName.StartsWith(AspectStrings.k_RefROFullName))
                return TryParseFieldRef(aspectDefinition, field, out aspectField, isRO: true);

            // DynamicBuffer<T>
            if (fieldTypeName.StartsWith(AspectStrings.k_DynamicBufferFullName))
                return TryParseFieldDynamicBuffer(aspectDefinition, field, out aspectField);

            // EnabledRefRW
            if (fieldTypeName.StartsWith(AspectStrings.k_EnabledRefRWFullName))
                return TryParseFieldEnabledRef(aspectDefinition, field, out aspectField, isRO: false);

            // EnabledRefRO
            if (fieldTypeName.StartsWith(AspectStrings.k_EnabledRefROFullName))
                return TryParseFieldEnabledRef(aspectDefinition, field, out aspectField, isRO: true);

            // Entity
            if (fieldTypeName.StartsWith(AspectStrings.k_EntityFullName))
                return TryParseFieldEntity(aspectDefinition, field, out aspectField);

            // Shared Component
            if (TryParseFieldSharedComponent(aspectDefinition, field, out aspectField))
                return true;

            return TryParseFieldAspect(aspectDefinition, field, out aspectField);
        }

        bool TryParseFieldRef(AspectDefinition aspectDefinition, IFieldSymbol fieldSymbol, out AspectField aspectField, bool isRO)
        {
            aspectField = default;
            if (!(fieldSymbol.Type is INamedTypeSymbol typeSymbol)) return false;
            if (typeSymbol.TypeArguments.FirstOrDefault() is INamedTypeSymbol genericNameTypeSymbol)
            {
                if (fieldSymbol.HasAttribute("Unity.Collections.ReadOnlyAttribute"))
                {
                    AspectErrors.SGA0011(fieldSymbol.Locations.FirstOrDefault());
                    aspectField = default;
                    return false;
                }
                var fieldName = fieldSymbol.Name;
                var isOptional = fieldSymbol.HasAttribute("Unity.Entities.OptionalAttribute");
                AspectField field;
                if (isRO)
                {
                    field = new AspectField
                    {
                        AspectDefinition = aspectDefinition,
                        FieldName = fieldName,
                        TypeName = genericNameTypeSymbol.ToFullName(),
                        IsReadOnly = true,
                        IsOptional = isOptional,
                        IsZeroSize = genericNameTypeSymbol.IsZeroSizedComponent(),
                        Symbol = fieldSymbol
                    };
                }
                else
                {
                    field = new AspectField
                    {
                        AspectDefinition = aspectDefinition,
                        FieldName = fieldName,
                        TypeName = genericNameTypeSymbol.ToFullName(),
                        IsReadOnly = false,
                        IsOptional = isOptional,
                        IsZeroSize = genericNameTypeSymbol.IsZeroSizedComponent(),
                        Symbol = fieldSymbol
                    };
                }

                if(!aspectDefinition.PrimitivesRouter.AddRef(field, out var conflictingField))
                    AspectErrors.SGA0001(field.Symbol.Locations.FirstOrDefault(), field.TypeName, conflictingField?.Symbol?.Locations.FirstOrDefault());

                if (!field.IsZeroSize)
                {
                    aspectDefinition.FieldsNeedConstruction.Add(field);
                }
                else
                    aspectDefinition.FieldsRequiringDefaultConstruction.Add(field);
                aspectField = field;
                return true;
            }
            aspectField = default;
            return false;
        }

        bool TryParseFieldDynamicBuffer(AspectDefinition aspectDefinition, IFieldSymbol fieldSymbol, out AspectField aspectField)
        {
            aspectField = default;
            if (!(fieldSymbol.Type is INamedTypeSymbol typeSymbol)) return false;
            if (typeSymbol.TypeArguments.FirstOrDefault() is INamedTypeSymbol genericNameTypeSymbol)
            {
                var fieldName = fieldSymbol.Name;
                var field = new AspectField
                {
                    AspectDefinition = aspectDefinition,
                    FieldName = fieldName,
                    TypeName = genericNameTypeSymbol.ToFullName(),
                    IsReadOnly = fieldSymbol.HasAttribute("Unity.Collections.ReadOnlyAttribute"),
                    IsOptional = fieldSymbol.HasAttribute("Unity.Entities.OptionalAttribute"),
                    Symbol = fieldSymbol
                };

                if(!aspectDefinition.PrimitivesRouter.AddDynamicBuffer(field, out var conflictingField))
                    AspectErrors.SGA0001(field.Symbol.Locations.FirstOrDefault(), field.TypeName, conflictingField?.Symbol?.Locations.FirstOrDefault());

                aspectDefinition.FieldsNeedConstruction.Add(field);
                aspectField = field;
                return true;
            }
            aspectField = default;
            return false;
        }

        bool TryParseFieldEntity(AspectDefinition aspectDefinition, IFieldSymbol fieldSymbol, out AspectField aspectField)
        {
            // Entity-in-Aspect
            if (aspectDefinition.HasEntityField)
                AspectErrors.SGA0006(fieldSymbol.Locations.FirstOrDefault());
            else
            {
                var field = new AspectField
                {
                    AspectDefinition = aspectDefinition,
                    FieldName = fieldSymbol.Name,
                    Symbol = fieldSymbol,
                    TypeName = "global::Unity.Entities.Entity",
                    IsReadOnly = true
                };
                if(!aspectDefinition.PrimitivesRouter.AddEntity(field, out var conflictingField))
                    AspectErrors.SGA0001(field.Symbol.Locations.FirstOrDefault(), field.TypeName, conflictingField?.Symbol?.Locations.FirstOrDefault());
                aspectField = field;

                return true;
            }
            aspectField = default;
            return false;
        }

        bool TryParseFieldAspect(AspectDefinition aspectDefinition, IFieldSymbol fieldSymbol, out AspectField aspectField)
        {
            var type = fieldSymbol.Type.GetSymbolType();
            if (type != null && type.Interfaces.Any(i => i.ToFullName() == AspectStrings.k_AspectInterfaceFullName))
            {
                // Nested Aspect Field
                var symbolName = fieldSymbol.Type.ToFullName();
                var fieldName = fieldSymbol.Name;
                var field = new AspectField
                {
                    AspectDefinition = aspectDefinition,
                    FieldName = fieldName,
                    TypeName = symbolName,
                    IsReadOnly = fieldSymbol.HasAttribute("Unity.Collections.ReadOnlyAttribute"),
                    IsOptional = fieldSymbol.HasAttribute("Unity.Entities.OptionalAttribute"),
                    IsNestedAspect = true,
                    Symbol = fieldSymbol
                };

                aspectDefinition.AspectFields.Add(field);
                aspectField = field;
                return true;
            }
            aspectField = default;
            return false;
        }

        bool TryParseFieldEnabledRef(AspectDefinition aspectDefinition, IFieldSymbol fieldSymbol,
            out AspectField aspectField, bool isRO)
        {
            aspectField = default;
            if (!(fieldSymbol.Type is INamedTypeSymbol typeSymbol)) return false;
            if (typeSymbol.TypeArguments.FirstOrDefault() is INamedTypeSymbol genericNameTypeSymbol)
            {
                var fieldName = fieldSymbol.Name;
                if (fieldSymbol.HasAttribute("Unity.Collections.ReadOnlyAttribute"))
                {
                    AspectErrors.SGA0011(fieldSymbol.Locations.FirstOrDefault());
                    aspectField = default;
                    return false;
                }
                AspectField field;
                if (isRO)
                {
                    field = new AspectField
                    {
                        AspectDefinition = aspectDefinition,
                        FieldName = fieldName,
                        TypeName = genericNameTypeSymbol.ToFullName(),
                        IsReadOnly = true,
                        IsOptional = fieldSymbol.HasAttribute("Unity.Entities.OptionalAttribute"),
                        IsZeroSize = genericNameTypeSymbol.IsZeroSizedComponent(),
                        Symbol = fieldSymbol
                    };
                }
                else
                {
                    field = new AspectField
                    {
                        AspectDefinition = aspectDefinition,
                        FieldName = fieldName,
                        TypeName = genericNameTypeSymbol.ToFullName(),
                        IsReadOnly = false,
                        IsOptional = fieldSymbol.HasAttribute("Unity.Entities.OptionalAttribute"),
                        IsZeroSize = genericNameTypeSymbol.IsZeroSizedComponent(),
                        Symbol = fieldSymbol
                    };
                }

                // Require a ComponentLookup/ComponentTypeHandle for Aspect.EntityLookup
                if(!aspectDefinition.PrimitivesRouter.AddEnabledRef(field, out var conflictingField))
                    AspectErrors.SGA0001(field.Symbol.Locations.FirstOrDefault(), field.TypeName, conflictingField?.Symbol?.Locations.FirstOrDefault());


                // Require the field to be initialized from the constructor parameter
                aspectDefinition.FieldsNeedConstruction.Add(field);

                aspectField = field;
                return true;
            }
            aspectField = default;
            return false;
        }

        bool TryParseFieldSharedComponent(AspectDefinition aspectDefinition, IFieldSymbol fieldSymbol, out AspectField aspectField)
        {
            var type = fieldSymbol.Type.GetSymbolType();
            if (type != null && type.Interfaces.Any(i => i.ToFullName() == "global::Unity.Entities.ISharedComponentData"))
            {
                // Shared Component Aspect Field
                var symbolName = fieldSymbol.Type.ToFullName();
                var fieldName = fieldSymbol.Name;
                var field = new AspectField
                {
                    AspectDefinition = aspectDefinition,
                    FieldName = fieldName,
                    TypeName = symbolName,
                    IsReadOnly = true,
                    IsOptional = fieldSymbol.HasAttribute("Unity.Entities.OptionalAttribute"),
                    Symbol = fieldSymbol
                };

                if (!aspectDefinition.PrimitivesRouter.AddSharedComponent(field, out var conflictingField))
                    AspectErrors.SGA0001(field.Symbol.Locations.FirstOrDefault(), field.TypeName, conflictingField?.Symbol?.Locations.FirstOrDefault());

                aspectDefinition.FieldsNeedConstruction.Add(field);
                aspectField = field;
                return true;
            }
            aspectField = default;
            return false;
        }

        /// <summary>
        /// Generate all aspects found
        /// </summary>
        /// <param name="context"></param>
        public void Execute(GeneratorExecutionContext context)
        {
            if (!SourceGenHelpers.ShouldRun(context.Compilation, context.CancellationToken))
                return;

            SourceOutputHelpers.Setup(context.ParseOptions, context.AdditionalFiles);

            // Flush aspect cache
            m_cache = null;

            // Scope a DiagnosticLogger for the entirety of the scope of execution.
            using (Service<IDiagnosticLogger>.Scoped(new DiagnosticLogger(context)))
            // Scope a diagnostic frame, for the entirety of the scope of execution, that provide context on the current aspect being generated
            using (Service<IDiagnosticFrame>.Scoped(this))
            {
                // Keep dictionary of seen semantic models so we can re-use symbol caching
                var treeToSemanticModel = new Dictionary<SyntaxTree, SemanticModel>();
                SemanticModel GetSemanticModel(SyntaxNode syntaxNode)
                {
                    if (treeToSemanticModel.ContainsKey(syntaxNode.SyntaxTree))
                        return treeToSemanticModel[syntaxNode.SyntaxTree];

                    var model = context.Compilation.GetSemanticModel(syntaxNode.SyntaxTree);
                    treeToSemanticModel[syntaxNode.SyntaxTree] = model;
                    return model;
                }

                // Go through aspect candidates and resolve them to the same type (multiple aspect partial declarations can map to a single definition)
                var fullNameToAspectDefinition = new Dictionary<string, AspectDefinition>();
                var aspectReceiver = (AspectReceiver)context.SyntaxReceiver;

                foreach (var aspectCandidate in aspectReceiver._AspectCandidates)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();

                    var aspectDeclarationSymbol = GetSemanticModel(aspectCandidate).GetDeclaredSymbol(aspectCandidate);
                    // skip if not an aspect.
                    if (!aspectDeclarationSymbol.IsAspect()) continue;

                    var aspectFullName = aspectDeclarationSymbol.ToFullName();
                    if (!fullNameToAspectDefinition.ContainsKey(aspectFullName))
                        fullNameToAspectDefinition[aspectFullName] = new AspectDefinition(aspectDeclarationSymbol);
                    fullNameToAspectDefinition[aspectFullName].SourceSyntaxNodes.Add(aspectCandidate);
                }

                // Group by syntax tree so we can emit new source per original source file (for inspection of generated code that matches source)
                var aspectsGroupedBySyntaxTree = fullNameToAspectDefinition.Values.GroupBy(kvp => kvp.SourceSyntaxNodes.First().SyntaxTree);

                foreach (var aspectTreeGrouping in aspectsGroupedBySyntaxTree)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();

                    var syntaxTree = aspectTreeGrouping.Key;
                    var syntaxTreeSourceBuilder = new System.IO.StringWriter(new StringBuilder());

                    // Gather all 'using' statement from all source nodes and output
                    foreach (var @using in GetAllUsingsInSyntaxTree(aspectTreeGrouping))
                        syntaxTreeSourceBuilder.WriteLine(@using);

                    foreach (var aspectDef in aspectTreeGrouping)
                    {
                        try
                        {
                            // Check for Disable Generation
                            if (aspectDef.SyntaxHasAttributeCandidate(AspectStrings.k_EntityPackageNamespace, AspectStrings.k_DisableGeneration))
                                continue;

                            DebugTrace.WriteLine(
                                $"Parsing {aspectDef.Name} at {aspectDef.SourceSyntaxNodes.Select(x => x.GetLocation().ToString()).SeparateByNewLine()}");

                            bool valid = true;

                            // Report Aspect errors (try to report as many as possible(
                            foreach (var node in aspectDef.SourceSyntaxNodes)
                            {
                                // Aspects must be readonly otherwise they don't work well with foreach enumerators
                                // (can't have setters on the foreach value, unless it is marked read-only)
                                if (!node.HasTokenOfKind(SyntaxKind.ReadOnlyKeyword))
                                {
                                    AspectErrors.SGA0005(node.GetLocation());
                                    valid = false;
                                }

                                if(node.TypeParameterList != null)
                                {
                                    AspectErrors.SGA0009(node.GetLocation());
                                    valid = false;
                                }

                                if (node.Parent != null)
                                {
                                    if(node.Parent is TypeDeclarationSyntax typeDec
                                       && typeDec.Modifiers.All(x => !x.IsKind(SyntaxKind.PartialKeyword)))
                                    {
                                        valid = false;
                                    }
                                }

                                var correctOrAbsent = VerifyIAspectCreateImplementationCorrectOrAbsent(aspectDef, node);

                                if (!correctOrAbsent.Success)
                                {
                                    AspectErrors.SGA0002(node.GetLocation(), correctOrAbsent.ErrorMessage);
                                    valid = false;
                                }
                            }

                            // gather all fields
                            var syntaxNode = aspectDef.SourceSyntaxNodes.FirstOrDefault();
                            var semanticModel = context.Compilation.GetSemanticModel(syntaxNode.SyntaxTree);
                            ITypeSymbol aspectTypeSymbol = semanticModel.GetDeclaredSymbol(syntaxNode);
                            if (aspectTypeSymbol != null)
                            {
                                aspectDef.Symbol = aspectTypeSymbol;
                                foreach (var field in aspectTypeSymbol.GetMembers().OfType<IFieldSymbol>())
                                {
                                    if (!TryParseField(field, aspectDef, out var aspectField))
                                    {
                                        if (!field.IsConst)
                                            AspectErrors.SGA0007(field.Locations.FirstOrDefault());
                                    }
                                }
                            }

                            // Resolve additional required primitives by the aspect excluding its nested aspect.
                            aspectDef.PrimitivesRouter.ResolveDependencies();

                            // Dealias the required primitives for each nested aspect and merge them into this aspect
                            SolveAliasing(aspectDef);

                            // If there are no field that participates to the query, the aspect in invalid, raise SGA0004
                            if (!aspectDef.PrimitivesRouter.HasAnyQueryComponents && aspectDef.AspectFields.Count == 0)
                                AspectErrors.SGA0004(aspectDef.SourceSyntaxNodes.First().GetLocation());

                            if (!valid)
                                continue;

                            DebugTrace.WriteLine($"Aspect {aspectDef.Name} at {aspectDef.Symbol.Locations.FirstOrDefault()}");
                            DebugTrace.WriteLine(Printer.PrintToString(aspectDef.PrimitivesRouter));

                            syntaxTreeSourceBuilder.Write(AspectSyntaxFactory.GenerateAspectSource(aspectDef));
                        }
                        catch (Exception exception)
                        {
                            if (exception is OperationCanceledException)
                                throw;

                            // Log the exception as "SGA0000"
                            Location loc = default;
                            if (aspectDef != null)
                            {
                                if (aspectDef.SourceSyntaxNodes != null && aspectDef.SourceSyntaxNodes.Any())
                                {
                                    if (aspectDef.SourceSyntaxNodes != null && aspectDef.SourceSyntaxNodes.Any())
                                    {
                                        loc = aspectDef.SourceSyntaxNodes.First().GetLocation();
                                    }
                                }

                                AspectErrors.SGAICE0000(aspectDef.Name, loc, exception);
                            }
                        }
                    }

                    syntaxTreeSourceBuilder.Flush();

                    var code = syntaxTreeSourceBuilder.ToString();

                    SourceOutputHelpers.OutputSourceToFile(
                        syntaxTree.GetGeneratedSourceFilePath(context.Compilation.Assembly.Name, s_GeneratorName).FullFilePath,
                        () => code);

                    var generatedSourceHint = syntaxTree.GetGeneratedSourceFileName(s_GeneratorName);
                    context.AddSource(generatedSourceHint, code);

                    syntaxTreeSourceBuilder.Dispose();
                }
            }
        }


        /// <summary>
        /// Route all fields that alias the same component type
        /// to the same dots primitive such as ComponentLookup<T>
        /// and ComponentTypeHandle<T>
        /// </summary>
        /// <param name="aspect"></param>
        /// <remarks>
        /// Aliasing is caused by nested aspects having the same
        /// component type as one of the host's component type
        /// In this example, MyComponent is aliased in instances of AspectB since the component is present in both AspectB and AspectA.
        /// However, MyComponent is not aliased in instances of AspectB.
        /// ex:
        ///  public readonly struct AspectA : IAspect<AspectA>
        ///  {
        ///      readonly RefRO<MyComponent> m_MyComponent;
        ///  }
        ///  public readonly struct AspectB : IAspect<AspectB>
        ///  {
        ///      readonly AspectA m_AspectA;
        ///      readonly RefRO<MyComponent> m_MyComponent;
        ///  }
        /// </remarks>
        void SolveAliasing(AspectDefinition aspect)
        {
            if (aspect.AspectFields.Count == 0) return;
            var nestedAspects = new List<NestedAspectDefinition>();
            foreach (var aspectField in aspect.AspectFields)
            {
                // Parse each nested aspect's fields
                AspectDefinition nestedAspect = ParseNestedFields(aspectField);
                if (nestedAspect != null)
                    nestedAspects.Add(new NestedAspectDefinition { AspectField = aspectField, Definition = nestedAspect });
            }
            aspect.NestedAspects = nestedAspects.ToArray();
        }

        AspectDefinition ParseNestedFields(AspectField field)
        {
            if (SymbolEqualityComparer.Default.Equals(field.Symbol.Type, field.AspectDefinition.Symbol))
            {
                // try to nest itself. the compiler will report that as an error
                return null;
            }
            DebugTrace.WriteLineAndIndentIncrease($"Parse Fields of nested aspect '{field.Symbol.Name}'");

            AspectDefinition nestedAspect = ParseNestedAspectFields(field.Symbol.Type);
            nestedAspect.Parent = field.AspectDefinition;
            nestedAspect.PrimitivesRouter.ResolveDependencies();

            DebugTrace.IndentDecrease();
            DebugTrace.WriteLineAndIndentIncrease($"Merge in AccessRouter of nested aspect '{field.Symbol.Name}'");

            // Merge all the required Decay Primitives and their dependent fields from the parsed nested aspect
            field.AspectDefinition.PrimitivesRouter.Merge(nestedAspect.PrimitivesRouter, field.BindOverride);

            DebugTrace.IndentDecrease();
            return nestedAspect;
        }

        /// <summary>
        /// Parse all fields from an aspect
        /// All nested aspect fields will be parsed and resulting AccessRouter and
        /// their dependencies will be merged into the root aspect
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        AspectDefinition ParseNestedAspectFields(ITypeSymbol symbol)
        {
            var name = symbol.ToFullName();
            if (AspectCache.TryGetValue(name, out var aspect))
                return aspect;

            AspectDefinition nestedAspect = new AspectDefinition(symbol);
            var nestedAspects = new List<NestedAspectDefinition>();
            foreach (var m in symbol.GetMembers())
            {
                switch (m)
                {
                    case IFieldSymbol fs:
                        if (TryParseField(fs, nestedAspect, out var aspectField))
                            if (aspectField.IsNestedAspect)
                                nestedAspects.Add(new NestedAspectDefinition { AspectField = aspectField, Definition = ParseNestedFields(aspectField) });
                        break;
                }
            }
            nestedAspect.NestedAspects = nestedAspects.ToArray();
            AspectCache.Add(name, nestedAspect);

            return nestedAspect;
        }



        static (bool Success, string ErrorMessage)
            VerifyIAspectCreateImplementationCorrectOrAbsent(AspectDefinition aspect, StructDeclarationSyntax node)
        {
            foreach (var childNode in node.BaseList.Types)
            {
                var aspectCreateCandidate = childNode.Type switch
                {
                    GenericNameSyntax name => name,
                    QualifiedNameSyntax {Right: GenericNameSyntax name} => name,
                    _ => null
                };

                if (aspectCreateCandidate == null)
                    continue;
                if (aspectCreateCandidate.Identifier.ValueText != AspectStrings.k_AspectCreateInterface)
                    continue;

                var aspectTypeName = aspectCreateCandidate.TypeArgumentList.Arguments[0] switch
                {
                    SimpleNameSyntax name => name.Identifier.ValueText,
                    QualifiedNameSyntax { Right: { } name } => name.Identifier.ValueText,
                    _ => null
                };

                aspect.IsIAspectCreateCorrectlyImplementedByUser = node.Identifier.ValueText == aspectTypeName;

                if (aspect.IsIAspectCreateCorrectlyImplementedByUser)
                    return (Success: true, string.Empty);

                return (Success: false,
                    ErrorMessage:
                    $"You have implemented `IAspectCreate<{aspectTypeName}>` on `{node.Identifier.ValueText}`. This is incorrect. Please implement `IAspectCreate<{node.Identifier.ValueText}>` instead. " +
                    $"If you do not implement `IAspectCreate<{node.Identifier.ValueText}>`, a default implementation will be generated for you automatically.");
            }
            return (Success: true, string.Empty);
        }


        static SortedSet<string> GetAllUsingsInSyntaxTree(IGrouping<SyntaxTree, AspectDefinition> aspectTreeGrouping)
        {
            var usings = new SortedSet<string>();
            foreach (var aspect in aspectTreeGrouping)
            {
                foreach (var node in aspect.SourceSyntaxNodes)
                {
                    var root = node.SyntaxTree.GetRoot();
                    foreach (var childNode in root.ChildNodes().OfType<UsingDirectiveSyntax>())
                    {
                        var @using = childNode.WithoutTrivia();
                        usings.Add(@using.ToString());
                    }
                }
            }

            return usings;
        }

        /// <summary>
        /// Diagnostic frame brief information
        /// </summary>
        /// <returns></returns>
        string IDiagnosticFrame.GetBrief()
        {
            return null;
            // TODO: fix this so that it doesn't rely on fields stored in AspectGenerator (generators are not allowed to store state)
            /*
            if (m_CurrentAspectDefinition == null) return "";
            var sb = new StringBuilder();
            sb.Append($"Aspect: \"{m_CurrentAspectDefinition.Name}\"");
            return sb.ToString();
            */
        }

        /// <summary>
        /// Diagnostic frame complete information
        /// </summary>
        /// <returns></returns>
        string IDiagnosticFrame.GetMessage()
        {
            return null;

            // TODO: fix this so that it doesn't rely on fields stored in AspectGenerator (generators are not allowed to store state)
            /*
            if (m_CurrentAspectDefinition == null) return "AspectGenerator";
            var sb = new StringBuilder();
            sb.AppendLine($"Aspect: \"{m_CurrentAspectDefinition.Name}\"");
            if (m_CurrentAspectDefinition.SourceSyntaxNodes.Count == 1)
            {
                sb.AppendLine($"Location: {m_CurrentAspectDefinition.SourceSyntaxNodes.Single().GetLocation()}");
            }
            else
            {
                sb.AppendLine($"Locations:");
                sb.Append(m_CurrentAspectDefinition.SourceSyntaxNodes.Select(x => x.LocationString()).SeparateByNewLine());
            }
            return sb.ToString();
            */
        }
    }

    public static class DebugTrace
    {
        private static string ms_Indent = "";
        [System.Diagnostics.Conditional("ASPECT_DEBUG")]
        public static void WriteLineAndIndentIncrease(string line)
        {
            WriteLine(line);
            ms_Indent += "  ";
        }
        [System.Diagnostics.Conditional("ASPECT_DEBUG")]
        public static void IndentDecrease()
        {
            ms_Indent = ms_Indent.Substring(0, ms_Indent.Length - 2);
        }
        [System.Diagnostics.Conditional("ASPECT_DEBUG")]
        public static void WriteLine(string line) => Console.WriteLine(ms_Indent + line);

        [System.Diagnostics.Conditional("ASPECT_DEBUG")]
        public static void Write(string text) => Console.Write(text);

        [System.Diagnostics.Conditional("ASPECT_DEBUG")]
        public static void WriteLine() => Console.WriteLine();

    }
}
