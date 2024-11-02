using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.Analyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EntitiesCodeFixProvider)), Shared]
    public class EntitiesCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
            CSharpCompilerDiagnostics.CS1654,
            EntitiesDiagnostics.ID_EA0001,
            EntitiesDiagnostics.ID_EA0007,
            EntitiesDiagnostics.ID_EA0008,
            EntitiesDiagnostics.ID_EA0009,
            EntitiesDiagnostics.ID_EA0010,
            EntitiesDiagnostics.ID_EA0016);
        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            foreach (var diagnostic in context.Diagnostics)
            {
                var node = root?.FindNode(diagnostic.Location.SourceSpan);

                switch (node)
                {
                    case LocalDeclarationStatementSyntax {Declaration:{} variableDeclaration} when diagnostic.Id == EntitiesDiagnostics.ID_EA0001:
                    context.RegisterCodeFix(
                        CodeAction.Create(title: "Use non-readonly reference",
                            createChangedDocument: c => MakeNonReadonlyReference(context.Document, variableDeclaration, c),
                            equivalenceKey: "NonReadonlyReference"),
                        diagnostic);
                    break;
                    case ParameterSyntax parameterSyntax:
                    {
                        if (diagnostic.Id == EntitiesDiagnostics.ID_EA0009)
                        {
                            context.RegisterCodeFix(
                                CodeAction.Create(title: "Use non-readonly reference",
                                    createChangedDocument: c =>
                                        MakeNonReadonlyReference(context.Document, parameterSyntax, c),
                                    equivalenceKey: "NonReadonlyReferenceParameter"),
                                diagnostic);
                        }
                        else if (diagnostic.Id == EntitiesDiagnostics.ID_EA0016)
                        {
                            context.RegisterCodeFix(
                                CodeAction.Create(title: "Add ref keyword",
                                    createChangedDocument: c => AddRef(context.Document, parameterSyntax, c),
                                    equivalenceKey: "AddRef"),
                                diagnostic);
                        }

                        break;
                    }
                    case TypeDeclarationSyntax typeDeclarationSyntax:
                    {
                        if (diagnostic.Id == EntitiesDiagnostics.ID_EA0007 || diagnostic.Id == EntitiesDiagnostics.ID_EA0008)
                        {
                            context.RegisterCodeFix(
                                CodeAction.Create(title: "Add partial keyword",
                                    createChangedDocument: c => AddPartial(context.Document, typeDeclarationSyntax, c),
                                    equivalenceKey: "AddPartial"),
                                diagnostic);
                        }
                        else if (diagnostic.Id == EntitiesDiagnostics.ID_EA0010)
                        {
                            context.RegisterCodeFix(
                                CodeAction.Create(title: "Add BurstCompile attribute",
                                    createChangedDocument: c => AddBurstCompilerAttribute(context.Document, typeDeclarationSyntax, c),
                                    equivalenceKey: "AddBurstCompilerAttribute"),
                                diagnostic);
                        }

                        break;
                    }
                    case ElementAccessExpressionSyntax elementAccessExpression when diagnostic.Id == CSharpCompilerDiagnostics.CS1654:
                    {
                        context.RegisterCodeFix(
                            CodeAction.Create(title: "Store element in a new variable",
                                createChangedDocument: c => AssignCurrentVariableToNewVariable_AndUseNewVariableInstead(context.Document, elementAccessExpression, c),
                                equivalenceKey: "StoreValueTypeElementInNewVariable"),
                            diagnostic);
                        break;
                    }
                }
                }
            }

        // To understand the implementation of this method, it helps to read through some sample code here:
        // https://sharplab.io/#v2:C4LghgzsA+ACBMBGAsAKDbAzAAigJwFcBjYbAEQE8A7MAWwEsiAhAgM1YFM8AeAFQD5saAN5ps47Fmy9swABb0IAbXpVSqgCYcAHgF0xE0agknsAcw6kAvIK2swBADbAAFLwCUAbgOncl7DbYAG5gjgQcAdh2Ds5uXj7YAL5oyeioUgjYAMLYRiYZACzYALIu7kLGhgkmrAD2eBxgRHLYLi4heFEARogANMFgnRpd8OWq2FQcAO6tlDQMzGycPKrA/P1zdIws7FzcrI61YGvuSgAMurmJ7tVVlb4mAPQAVNgA6hFTYGqytdgNUzw9GAEXkEUm2lIjlUoNqIFuvgRDxMHQm02GiEiGO892RD0mUwx50uVmwiBxeLxSIe43qWk6wD+DQgtUcQQiWQAyogAGwAVgKADpqYjcZTpH8OFQIAQGtgiPUGiRsJopcBjvRalRsLVWLI5ODpo4KCrpVwQRpsNDJv1gWiOBoIL9sAQIKCDbIgUF6GBsJAWUQfRbsFNgS0wRVxSZ6Fo1PRWPQuNgAAbDZOm/WGyFWmHCsUPZ6PEXiIkXSLk4uR8Uvd6fb6kRn/aZAkGZtHZ62w+H50VR3yogkYrE9Cl9lGDNGEkbD+CjsfiVHMpzWVqDnrE8vlADUq/RIw3pMQgrOrHiPfFlYktLw9OdzNZ7Oy3P5Qsv4jfEuwUplcoVeCV6hULG6rAJq2q6m2BLGqabp4MGnYQP0YIml0tTyPalqNq67qgl6Pp+hAAZBg6IZhh+EbJh06Yxmq8aJp04wRhCUK5pWhaVouHAys4kQuKWJJkturTDPAB5ksep5zqYqQmKkiRAA==
        static async Task<Document> AssignCurrentVariableToNewVariable_AndUseNewVariableInstead(
            Document document, ElementAccessExpressionSyntax elementAccessExpression, CancellationToken cancellationToken)
        {
            var currentIdentifierName = (IdentifierNameSyntax)elementAccessExpression.Expression;
            var currentIdentifier = currentIdentifierName.Identifier;
            var currentIdentifierText = currentIdentifier.ValueText;
            var currentBracketedArgumentList = elementAccessExpression.ArgumentList;

            // Declare a new variable and assign the current variable to it.
            var newIdentifier = SyntaxFactory.IdentifierName($"__new{currentIdentifierText}__").WithTriviaFrom(currentIdentifierName);
            var variableDeclaratorSyntax =
                SyntaxFactory.VariableDeclarator(
                    newIdentifier.Identifier.WithTrailingTrivia(SyntaxFactory.Space),
                    argumentList: null,
                    initializer: SyntaxFactory.EqualsValueClause(currentIdentifierName.WithLeadingTrivia(SyntaxFactory.Space)));

            // Insert the new variable declaration before the statement containing the current variable.
            var insertNewVariableDeclarationBefore = elementAccessExpression.AncestorOfKind<StatementSyntax>();

            // If the original code is: `db[0] = blah`, then the new variable declaration needs to use the trivia associated with `db` in order to preserve
            // the correct indentation.
            // If the original code is: `var result = (db1[0] = blah) + (db2[0] = blah);` then the new variable declaration needs to use the trivia associated with
            // `var` in order to preserve the correct indentation.
            // To retrieve the correct trivia, we simply need to look for the first `IdentifierNameSyntax` node in the statement containing the current variable,
            // and use its trivia.
            var variableDeclarationUseTriviaFrom = insertNewVariableDeclarationBefore.DescendantNodes().OfType<IdentifierNameSyntax>().First();
            var variableDeclarationStatement =
                SyntaxFactory.ParseStatement($"var {variableDeclaratorSyntax.ToString()};")
                    .WithLeadingTrivia(variableDeclarationUseTriviaFrom.Identifier.LeadingTrivia)
                    .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

            var newElementAccessNode = SyntaxFactory.ElementAccessExpression(newIdentifier, currentBracketedArgumentList).WithTriviaFrom(currentIdentifierName);

            var currentRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            // Because we are making multiple changes, and each change creates a new COPY of the document with the change applied, we need to track
            // the original nodes we want to modify across all new copies to ensure that our changes are always correctly applied. Each time we apply a change, we
            // must invoke `trackedRoot.GetCurrentNode(originalNodeToModify)` to identify the node in the new copy that corresponds to `originalNodeToModify`.
            var trackedRoot = currentRoot?.TrackNodes(insertNewVariableDeclarationBefore, elementAccessExpression);
            var trackedStatement = trackedRoot?.GetCurrentNode(insertNewVariableDeclarationBefore);
            trackedRoot = trackedRoot.InsertNodesBefore(trackedStatement, new SyntaxNode[]{variableDeclarationStatement});
            var trackedElementAccessNode = trackedRoot.GetCurrentNode(elementAccessExpression);
            trackedRoot = trackedRoot.ReplaceNode(trackedElementAccessNode, newElementAccessNode.WithTrailingTrivia(SyntaxFactory.Space));

            Debug.Assert(currentRoot != null, nameof(currentRoot) + " != null");

            return document.WithSyntaxRoot(trackedRoot);
        }

        static async Task<Document> AddPartial(Document document, TypeDeclarationSyntax typeDeclarationSyntax, CancellationToken cancellationToken)
        {
            var partialModifier = SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space);
            var modifiedSyntax = typeDeclarationSyntax.WithoutLeadingTrivia().AddModifiers(partialModifier).WithTriviaFrom(typeDeclarationSyntax);
            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            Debug.Assert(oldRoot != null, nameof(oldRoot) + " != null");
            var newRoot = oldRoot.ReplaceNode(typeDeclarationSyntax, modifiedSyntax);
            return document.WithSyntaxRoot(newRoot);
        }

        static async Task<Document> AddRef(Document document, ParameterSyntax typeDeclarationSyntax, CancellationToken cancellationToken)
        {
            var refModifier = SyntaxFactory.Token(SyntaxKind.RefKeyword).WithTrailingTrivia(SyntaxFactory.Space);
            var modifiedSyntax = typeDeclarationSyntax.WithoutLeadingTrivia().AddModifiers(refModifier).WithTriviaFrom(typeDeclarationSyntax);
            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            Debug.Assert(oldRoot != null, nameof(oldRoot) + " != null");
            var newRoot = oldRoot.ReplaceNode(typeDeclarationSyntax, modifiedSyntax);
            return document.WithSyntaxRoot(newRoot);
        }

        static async Task<Document> MakeNonReadonlyReference(Document document, ParameterSyntax parameterSyntax, CancellationToken cancellationToken)
        {
            var modifiedParam = parameterSyntax;
            modifiedParam = modifiedParam.WithoutLeadingTrivia();
            modifiedParam = modifiedParam.WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.RefKeyword).WithTrailingTrivia(SyntaxFactory.Space)));
            modifiedParam = modifiedParam.WithTriviaFrom(parameterSyntax);
            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = oldRoot.ReplaceNode(parameterSyntax, modifiedParam);
            return document.WithSyntaxRoot(newRoot);
        }

        static async Task<Document> MakeNonReadonlyReference(Document document, VariableDeclarationSyntax variableDeclaration, CancellationToken cancellationToken)
        {
            var modifiedVarDecl = variableDeclaration;
            if (modifiedVarDecl.Type is RefTypeSyntax overallRefType)
            {
                // fixes snippets with readonly keyword e.g. `ref readonly MyBlob readonlyBlob = ref _blobAssetReference.Value`
                modifiedVarDecl = modifiedVarDecl.WithType(overallRefType.WithReadOnlyKeyword(default).WithTriviaFrom(overallRefType));
            }
            else
            {
                // fixes snippets missing ref keywords e.g. `MyBlob readonlyBlob = _blobAssetReference.Value`
                var originalTypeWithSpace = modifiedVarDecl.Type.WithoutTrivia();
                var type = SyntaxFactory.RefType(originalTypeWithSpace).WithTriviaFrom(modifiedVarDecl.Type);
                type = type.WithRefKeyword(type.RefKeyword.WithTrailingTrivia(SyntaxFactory.Space));
                modifiedVarDecl = modifiedVarDecl.WithType(type);

                var modifiedVariables = modifiedVarDecl.Variables.ToArray();
                for (var index = 0; index < modifiedVariables.Length; index++)
                {
                    var modifiedValue = SyntaxFactory.RefExpression(modifiedVariables[index].Initializer.Value.WithoutTrivia().WithLeadingTrivia(SyntaxFactory.Space)).WithTriviaFrom(modifiedVariables[index].Initializer.Value);
                    modifiedVariables[index] = modifiedVariables[index].WithInitializer(modifiedVariables[index].Initializer.WithValue(modifiedValue));
                }
                modifiedVarDecl = modifiedVarDecl.WithVariables(SyntaxFactory.SeparatedList(modifiedVariables));
            }
            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = oldRoot.ReplaceNode(variableDeclaration, modifiedVarDecl);
            return document.WithSyntaxRoot(newRoot);
        }

        static async Task<Document> AddBurstCompilerAttribute(Document document,
            TypeDeclarationSyntax typeDeclarationSyntax, CancellationToken cancellationToken)
        {
            var burstCompilerAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("BurstCompile"));
            var burstCompilerAttributeList =
                SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList(new[] {burstCompilerAttribute}))
                    .NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.LineFeed)
                    .WithLeadingTrivia(typeDeclarationSyntax.GetLeadingTrivia());

            var modifiedSyntax = typeDeclarationSyntax.AddAttributeLists(burstCompilerAttributeList);

            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            Debug.Assert(oldRoot != null, nameof(oldRoot) + " != null");

            var newRoot = oldRoot.ReplaceNode(typeDeclarationSyntax, modifiedSyntax);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
