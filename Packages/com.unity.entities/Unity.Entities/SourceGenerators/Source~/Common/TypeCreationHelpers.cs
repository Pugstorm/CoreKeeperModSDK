using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Unity.Entities.SourceGen.Common
{
    public static class TypeCreationHelpers
    {
        /// <summary>
        /// Line to replace with on generated source.
        /// </summary>
        public static string GeneratedLineTriviaToGeneratedSource => "// __generatedline__";

        public static SourceText FixUpLineDirectivesAndOutputSource(
            string generatedSourceFilePath,
            string generatedSyntax)
        {
            // Output as source
            var sourceTextForNewClass = SourceText.From(generatedSyntax, Encoding.UTF8)
                .WithInitialLineDirectiveToGeneratedSource(generatedSourceFilePath)
                    .WithIgnoreUnassignedVariableWarning();

            // Add line directives for lines with `GeneratedLineTriviaToGeneratedSource` or #line
            var textChanges = new List<TextChange>();
            foreach (var line in sourceTextForNewClass.Lines)
            {
                var lineText = line.ToString();
                if (lineText.Contains(GeneratedLineTriviaToGeneratedSource))
                {
                    textChanges.Add(new TextChange(line.Span,
                        lineText.Replace(GeneratedLineTriviaToGeneratedSource, $"#line {line.LineNumber + 2} \"{generatedSourceFilePath}\"")));
                }
                else if (lineText.Contains("#line") && lineText.TrimStart().IndexOf("#line", StringComparison.Ordinal) != 0)
                {
                    var indexOfLineDirective = lineText.IndexOf("#line", StringComparison.Ordinal);
                    textChanges.Add(new TextChange(line.Span,
                        lineText.Substring(0, indexOfLineDirective) + Environment.NewLine +
                        lineText.Substring(indexOfLineDirective)));
                }
            }
            return sourceTextForNewClass.WithChanges(textChanges);
        }

        public static SourceText GenerateSourceTextForRootNodes(
            string generatedSourceFilePath,
            BaseTypeDeclarationSyntax originalSyntax,
            string generatedSyntax,
            CancellationToken cancellationToken)
        {
            var syntaxTreeSourceBuilder = new StringWriter(new StringBuilder());
            var baseDeclaration = GetBaseDeclaration(originalSyntax);
            var usings = originalSyntax.SyntaxTree.GetCompilationUnitRoot(cancellationToken).Usings;

            foreach (var @using in usings)
                if (@using.ContainsDirectives)
                {
                    int numberOfNotClosedIfDirectives = 0;
                    foreach (var token in @using.ChildTokens())
                    foreach (var trivia in token.LeadingTrivia)
                        if (trivia.IsDirective)
                        {
                            if (trivia.IsKind(SyntaxKind.IfDirectiveTrivia))
                                numberOfNotClosedIfDirectives++;
                            else if (trivia.IsKind(SyntaxKind.EndIfDirectiveTrivia))
                                numberOfNotClosedIfDirectives--;
                        }
                    baseDeclaration.end.Insert(0, Environment.NewLine+"#endif", numberOfNotClosedIfDirectives);
                }

            syntaxTreeSourceBuilder.WriteLine(usings.ToFullString());
            syntaxTreeSourceBuilder.Write(baseDeclaration.start);
            syntaxTreeSourceBuilder.WriteLine(generatedSyntax);
            syntaxTreeSourceBuilder.Write(baseDeclaration.end.ToString());
            syntaxTreeSourceBuilder.Flush();

            // Output as source
            var sourceTextForNewClass = SourceText.From(syntaxTreeSourceBuilder.ToString(), Encoding.UTF8)
                .WithInitialLineDirectiveToGeneratedSource(generatedSourceFilePath)
                    .WithIgnoreUnassignedVariableWarning();

            // Add line directives for lines with `GeneratedLineTriviaToGeneratedSource` or #line
            var textChanges = new List<TextChange>();
            foreach (var line in sourceTextForNewClass.Lines)
            {
                var lineText = line.ToString();
                if (lineText.Contains(GeneratedLineTriviaToGeneratedSource))
                {
                    textChanges.Add(new TextChange(line.Span,
                        lineText.Replace(GeneratedLineTriviaToGeneratedSource, $"#line {line.LineNumber + 2} \"{generatedSourceFilePath}\"")));
                }
                else if (lineText.Contains("#line") && lineText.TrimStart().IndexOf("#line", StringComparison.Ordinal) != 0)
                {
                    var indexOfLineDirective = lineText.IndexOf("#line", StringComparison.Ordinal);
                    textChanges.Add(new TextChange(line.Span,
                        lineText.Substring(0, indexOfLineDirective - 1) + Environment.NewLine +
                        lineText.Substring(indexOfLineDirective)));
                }
            }

            return sourceTextForNewClass.WithChanges(textChanges);

            static (string start, StringBuilder end) GetBaseDeclaration(BaseTypeDeclarationSyntax typeSyntax)
            {
                var builderStart = "";
                var builderEnd = new StringBuilder();
                var curliesToClose = 0;
                var parentSyntax = typeSyntax.Parent as MemberDeclarationSyntax;
                while (parentSyntax != null && (
                           parentSyntax.IsKind(SyntaxKind.ClassDeclaration) ||
                           parentSyntax.IsKind(SyntaxKind.StructDeclaration) ||
                           parentSyntax.IsKind(SyntaxKind.RecordDeclaration) ||
                           parentSyntax.IsKind(SyntaxKind.NamespaceDeclaration)))
                {
                    switch (parentSyntax)
                    {
                        case TypeDeclarationSyntax parentTypeSyntax:
                            var keyword = parentTypeSyntax.Keyword.ValueText; // e.g. class/struct/record
                            var typeName = parentTypeSyntax.Identifier.ToString() + parentTypeSyntax.TypeParameterList; // e.g. Outer/Generic<T>
                            var constraint = parentTypeSyntax.ConstraintClauses.ToString(); // e.g. where T: new()
                            builderStart = $"partial {keyword} {typeName} {constraint} {{" + Environment.NewLine + builderStart;
                            break;
                        case NamespaceDeclarationSyntax parentNameSpaceSyntax:
                            builderStart = $"namespace {parentNameSpaceSyntax.Name} {{{Environment.NewLine}{parentNameSpaceSyntax.Usings}" + builderStart;
                            break;
                    }

                    curliesToClose++;
                    parentSyntax = parentSyntax.Parent as MemberDeclarationSyntax;
                }

                builderEnd.AppendLine();
                builderEnd.Append('}', curliesToClose);

                return (builderStart, builderEnd);
            }
        }
    }
}
