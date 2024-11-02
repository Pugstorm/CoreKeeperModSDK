using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.Common
{
    public static class PartialSystemTypeGenerator
    {
        static (int NumClosingBracesRequired, int NumNotClosedIfDirectives)
            WriteOpeningSyntaxForGeneratedPart_AndReturnClosingSyntax(
                IndentedTextWriter writer,
                SyntaxNode originalType)
        {
            static (Stack<(string Value, bool AddIndentAfter)> OpeningSyntaxes, int NumClosingBracesRequired) GetBaseDeclaration(SyntaxNode typeSyntax)
            {
                var opening = new Stack<(string Value, bool AddIndentAfter)>();
                var numBracesToClose = 0;

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
                            opening.Push(("{", AddIndentAfter: true));
                            opening.Push(($"partial {keyword} {typeName} {constraint}", AddIndentAfter: false));
                            break;
                        case NamespaceDeclarationSyntax parentNameSpaceSyntax:
                            foreach (var usingDir in parentNameSpaceSyntax.Usings)
                                opening.Push(($"{usingDir}", AddIndentAfter: false));
                            opening.Push(("{", AddIndentAfter: true));
                            opening.Push(($"namespace {parentNameSpaceSyntax.Name}", AddIndentAfter: false));
                            break;
                    }

                    numBracesToClose++;
                    parentSyntax = parentSyntax.Parent as MemberDeclarationSyntax;
                }
                return (opening, numBracesToClose);
            }

            var baseDeclaration = GetBaseDeclaration(originalType);
            var usings = originalType.SyntaxTree.GetCompilationUnitRoot().Usings;

            int numNotClosedIfDirectives = 0;
            foreach (var @using in usings)
                if (@using.ContainsDirectives)
                {
                    foreach (var token in @using.ChildTokens())
                    foreach (var trivia in token.LeadingTrivia)
                        if (trivia.IsDirective)
                        {
                            writer.WriteLine(trivia.ToString());
                            if (trivia.IsKind(SyntaxKind.IfDirectiveTrivia))
                                numNotClosedIfDirectives++;
                            else if (trivia.IsKind(SyntaxKind.EndIfDirectiveTrivia))
                                numNotClosedIfDirectives--;
                        }
                }

            foreach (var usingDirectiveSyntax in usings)
                writer.WriteLine(usingDirectiveSyntax.ToString());

            foreach (var opening in baseDeclaration.OpeningSyntaxes)
            {
                writer.WriteLine(opening.Value);
                if (opening.AddIndentAfter)
                    writer.Indent++;
            }

            return (baseDeclaration.NumClosingBracesRequired, numNotClosedIfDirectives);
        }

        public static (SyntaxTreeInfo SyntaxTreeInfo, TypeDeclarationSyntax OriginalSystem, string GeneratedSyntaxTreeContainingGeneratedPartialSystem)
            Generate(SystemDescription[] allDescriptionsForTheSameSystem)
        {
            var description = allDescriptionsForTheSameSystem.First();

            using var sw = new StringWriter();
            using var indentedTextWriter = new IndentedTextWriter(sw);

            var result = WriteOpeningSyntaxForGeneratedPart_AndReturnClosingSyntax(indentedTextWriter, description.SystemTypeSyntax);
            AppendBeginSystemType(indentedTextWriter, description.SystemTypeSyntax);

            foreach (var desc in allDescriptionsForTheSameSystem)
            {
                var walker = new SystemSyntaxWalker(desc);

                foreach (var kvp in desc.CandidateNodesGroupedByMethodOrProperty)
                {
                    switch (kvp.Key)
                    {
                        case MethodDeclarationSyntax methodDeclarationSyntax:
                        {
                            var visit = walker.VisitMethodDeclarationInSystem(methodDeclarationSyntax);
                            if (visit.MethodRequiresSourceGen)
                                indentedTextWriter.WriteLine(visit.SourceGeneratedMethod);
                            break;
                        }
                        case PropertyDeclarationSyntax propertyDeclarationSyntax:
                        {
                            var visit = walker.VisitPropertyDeclarationInSystem(propertyDeclarationSyntax);
                            if (visit.PropertyRequiresSourceGen)
                                indentedTextWriter.WriteLine(visit.SourceGeneratedProperty);
                            break;
                        }
                    }
                }
            }

            AddMiscellaneousMembers(indentedTextWriter, allDescriptionsForTheSameSystem);
            AddEntityCommandBufferSystemFields(indentedTextWriter, allDescriptionsForTheSameSystem);
            AddOnCreateForCompilerWithFields(indentedTextWriter, allDescriptionsForTheSameSystem, description.SystemType == SystemType.ISystem);

            for (int i = 0; i < result.NumNotClosedIfDirectives; i++)
                indentedTextWriter.WriteLine("#endif");

            indentedTextWriter.Indent--;
            indentedTextWriter.WriteLine("}");

            for (int i = 0; i < result.NumClosingBracesRequired; i++)
            {
                indentedTextWriter.Indent--;
                indentedTextWriter.WriteLine("}");
            }

            return (description.SyntaxTreeInfo, description.SystemTypeSyntax, indentedTextWriter.InnerWriter.ToString());
        }

        static void AppendBeginSystemType(IndentedTextWriter writer, TypeDeclarationSyntax originalSyntax)
        {
            writer.WriteLine("[global::System.Runtime.CompilerServices.CompilerGenerated]");
            writer.Write(originalSyntax.Modifiers.ToString());
            switch (originalSyntax)
            {
                case StructDeclarationSyntax _:
                    writer.WriteLine(" struct {0}{1} : global::Unity.Entities.ISystemCompilerGenerated", originalSyntax.Identifier.ToString(), originalSyntax.TypeParameterList?.ToString());
                    break;
                default:
                    writer.WriteLine(" class {0}{1}", originalSyntax.Identifier.ToString(), originalSyntax.TypeParameterList?.ToString());
                    break;
            }
            writer.WriteLine("{");
            writer.Indent++;
        }

        private static void AddMiscellaneousMembers(
            IndentedTextWriter writer,
            SystemDescription[] descriptions)
        {
            foreach (var desc in descriptions)
            foreach (var mem in desc.NewMiscellaneousMembers)
                mem.WriteTo(writer);
        }

        private static void AddEntityCommandBufferSystemFields(IndentedTextWriter writer, SystemDescription[] descriptions)
        {
            var distinctEcbFields = new Dictionary<string, string>();
            foreach (var desc in descriptions)
            {
                foreach (var kvp in desc.FullEcbSystemTypeNamesToGeneratedFieldNames)
                {
                    if (distinctEcbFields.ContainsKey(kvp.Key))
                        continue;
                    distinctEcbFields.Add(kvp.Key, kvp.Value);
                    writer.WriteLine($"{kvp.Key} {kvp.Value};");
                }
            }
        }
        private static void AddOnCreateForCompilerWithFields(IndentedTextWriter writer, SystemDescription[] descriptions, bool isISystem)
        {
            // Only needs to create OnCreateForCompiler in SystemBase if members are present
            if (!descriptions.Any(d =>
                    d.QueriesAndHandles.TypeHandleStructNestedFields.Count > 0 ||
                    d.QueriesAndHandles.QueryFieldsToFieldNames.Count > 0 ||
                    d.AdditionalStatementsInOnCreateForCompilerMethod.Count > 0))
            {
                if (isISystem)
                    writer.WriteLine("public void OnCreateForCompiler(ref SystemState state){}");
                return;
            }

            QueriesAndHandles.WriteTypeHandleStructAndAssignQueriesMethod(writer, descriptions.Select(d => d.QueriesAndHandles).ToArray());

            var additionalStatementsInOnCreate = new HashSet<string>();
            foreach (var description in descriptions)
            foreach (var statement in description.AdditionalStatementsInOnCreateForCompilerMethod)
                additionalStatementsInOnCreate.Add(statement);

            if (isISystem)
            {
                writer.WriteLine("public void OnCreateForCompiler(ref SystemState state)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("__AssignQueries(ref state);");
                writer.WriteLine("__TypeHandle.__AssignHandles(ref state);");
                foreach (var statement in additionalStatementsInOnCreate)
                    writer.WriteLine(statement);
                writer.Indent--;
                writer.WriteLine("}");
            }
            else
            {
                writer.WriteLine("protected override void OnCreateForCompiler()");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("base.OnCreateForCompiler();");
                writer.WriteLine("__AssignQueries(ref this.CheckedStateRef);");
                writer.WriteLine("__TypeHandle.__AssignHandles(ref this.CheckedStateRef);");
                foreach (var statement in additionalStatementsInOnCreate)
                    writer.WriteLine(statement);
                writer.Indent--;
                writer.WriteLine("}");
            }
        }
    }
}
