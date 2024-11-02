using System;
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
    // For each system that contains syntax nodes marked as candidates for potential patching, we create a `SystemSyntaxWalker` instance
    // to visit its methods and/or properties that contain these candidates. As the walker traverses through the method/property, it appends
    // tokens/trivia as needed to the `_currentMethodAndPropertyWriter`. Whenever it encounters a node that has been marked for potential patching
    // by either the `SystemAPIContext` module or the `IFE` module, it relinquishes write control to either the `SystemApiWalker` or the
    // `IFESyntaxWalker` respectively. The `SystemApiWalker`/`IFESyntaxWalker` then patches the node if necessary, before ceding write control back
    // the `SystemSyntaxWalker`. The `SystemSyntaxWalker` then continues traversing the method/property until it reaches the end.
    public class SystemSyntaxWalker : CSharpSyntaxWalker
    {
        private readonly SystemDescription _systemDescription;
        private readonly IndentedTextWriter _currentMethodAndPropertyWriter;
        private readonly StringWriter _currentInnerWriter;

        private string _newMemberName;
        private SyntaxToken _memberIdentifierToken;
        private bool _currentMemberRequiresSourceGen;
        private readonly HashSet<StatementSyntax> _statementsRequiringLineDirectives;
        private readonly HashSet<StatementSyntax> _statementsRequiringHiddenDirectives;

        public SystemSyntaxWalker(SystemDescription systemDescription)
            : base(SyntaxWalkerDepth.Trivia)
        {
            _currentInnerWriter = new StringWriter();
            _currentMethodAndPropertyWriter = new IndentedTextWriter(_currentInnerWriter);
            _systemDescription = systemDescription;
            (_statementsRequiringLineDirectives, _statementsRequiringHiddenDirectives) =
                _systemDescription.GetStatementsRequiringLineDirectivesAndHiddenDirectives();
        }

        public (bool MethodRequiresSourceGen, string SourceGeneratedMethod)
            VisitMethodDeclarationInSystem(MethodDeclarationSyntax methodDeclarationSyntax)
        {
            var originalMethodSymbol = _systemDescription.SemanticModel.GetDeclaredSymbol(methodDeclarationSyntax);
            var targetMethodNameAndSignature = originalMethodSymbol.GetMethodAndParamsAsString(_systemDescription);
            var stableHashCode = SourceGenHelpers.GetStableHashCode($"_{targetMethodNameAndSignature}") & 0x7fffffff;

            _newMemberName = $@"__{methodDeclarationSyntax.Identifier.ValueText}_{stableHashCode:X}";

            // The identifier token for the current method name
            _memberIdentifierToken = methodDeclarationSyntax.Identifier;

            // _currentMemberRequiresSourceGen will remain `false` if it turns out that none of the candidate nodes flagged for potential patching are valid candidates
            _currentMemberRequiresSourceGen = false;

            // StringWriter.Flush() unfortunately doesn't clear the buffer correctly: https://stackoverflow.com/a/13706647
            // Since IndentedTextWriter.Flush() calls stringWriter.Flush(), we cannot use it either.
            _currentInnerWriter.GetStringBuilder().Clear();

            // Append the `DOTSCompilerPatchedMethod` attribute to the method
            _currentMethodAndPropertyWriter.WriteLine($"[global::Unity.Entities.DOTSCompilerPatchedMethod(\"{targetMethodNameAndSignature}\")]");

            // Begin depth-first traversal of the method
            VisitMethodDeclaration(methodDeclarationSyntax);

            return (_currentMemberRequiresSourceGen, _currentMethodAndPropertyWriter.InnerWriter.ToString());
        }

        public (bool PropertyRequiresSourceGen, string SourceGeneratedProperty)
            VisitPropertyDeclarationInSystem(PropertyDeclarationSyntax propertyDeclarationSyntax)
        {
            var originalPropertySymbol = _systemDescription.SemanticModel.GetDeclaredSymbol(propertyDeclarationSyntax);
            var targetPropertyNameAndSignature = originalPropertySymbol.OriginalDefinition.ToString();
            var stableHashCode = SourceGenHelpers.GetStableHashCode($"_{targetPropertyNameAndSignature}") & 0x7fffffff;

            _newMemberName = $@"__{propertyDeclarationSyntax.Identifier.ValueText}_{stableHashCode:X}";

            // The identifier token for the current property name
            _memberIdentifierToken = propertyDeclarationSyntax.Identifier;

            // _currentMemberRequiresSourceGen will remain `false` if it turns out that none of the candidate nodes flagged for potential patching are valid candidates
            _currentMemberRequiresSourceGen = false;

            // StringWriter.Flush() unfortunately doesn't clear the buffer correctly: https://stackoverflow.com/a/13706647
            // Since IndentedTextWriter.Flush() calls stringWriter.Flush(), we cannot use it either.
            _currentInnerWriter.GetStringBuilder().Clear();

            // Append the `DOTSCompilerPatchedProperty` attribute to the property
            _currentMethodAndPropertyWriter.WriteLine($"[global::Unity.Entities.DOTSCompilerPatchedProperty(\"{targetPropertyNameAndSignature}\")]");

            // Begin depth-first traversal of the property
            VisitPropertyDeclaration(propertyDeclarationSyntax);

            return (_currentMemberRequiresSourceGen, _currentMethodAndPropertyWriter.InnerWriter.ToString());
        }

        public override void Visit(SyntaxNode node)
        {
            if (_statementsRequiringLineDirectives.Contains(node))
            {
                foreach (var leadingTrivia in node.GetLeadingTrivia())
                    if (leadingTrivia.IsKind(SyntaxKind.WhitespaceTrivia))
                        // Ensure proper indentation
                        VisitTrivia(leadingTrivia);

                // Append line directive
                _currentMethodAndPropertyWriter.WriteLine($"{GetLineDirective(_systemDescription.OriginalFilePath)}");
            }
            base.Visit(node);

            // Write a `#line hidden` directive after the last statement in the method/property
            // to ensure that no further generated source receives additions sequence points.
            if (_statementsRequiringHiddenDirectives.Contains(node))
                _currentMethodAndPropertyWriter.WriteLine("#line hidden");

            string GetLineDirective(string originalFilePath)
                => string.IsNullOrEmpty(originalFilePath) ? "" : $"#line {node.GetLineNumber() + 1} \"{originalFilePath}\"";
        }

        public override void VisitExplicitInterfaceSpecifier(ExplicitInterfaceSpecifierSyntax node)
        {
        }

        public override void VisitAttributeList(AttributeListSyntax node)
        {
        }

        public override void VisitToken(SyntaxToken token)
        {
            switch (token.Kind())
            {
                case SyntaxKind.OverrideKeyword:
                case SyntaxKind.PublicKeyword:
                case SyntaxKind.ProtectedKeyword:
                case SyntaxKind.PartialKeyword:
                case SyntaxKind.VirtualKeyword:
                case SyntaxKind.AbstractKeyword:
                {
                    // Do not append the token, only its leading trivia (e.g. preserving whitespace trivia is necessary
                    // for ensuring neat indentation)
                    VisitLeadingTrivia(token);
                    break;
                }
                default:
                {
                    VisitLeadingTrivia(token);

                    // If the current token is the identifier token of the method/property name, append the new method/property name instead
                    _currentMethodAndPropertyWriter.Write(token == _memberIdentifierToken ? _newMemberName : token.ToString());
                    VisitTrailingTrivia(token);
                    break;
                }
            }
        }
        public override void VisitTrivia(SyntaxTrivia trivia)
        {
            var triviaKind = trivia.Kind();

            if (triviaKind == SyntaxKind.EndOfLineTrivia)
                _currentMethodAndPropertyWriter.WriteLine();

            else if (triviaKind != SyntaxKind.DisabledTextTrivia &&
                triviaKind != SyntaxKind.PreprocessingMessageTrivia &&
                triviaKind != SyntaxKind.IfDirectiveTrivia &&
                triviaKind != SyntaxKind.ElifDirectiveTrivia &&
                triviaKind != SyntaxKind.ElseDirectiveTrivia &&
                triviaKind != SyntaxKind.EndIfDirectiveTrivia &&
                triviaKind != SyntaxKind.RegionDirectiveTrivia &&
                triviaKind != SyntaxKind.EndRegionDirectiveTrivia &&
                triviaKind != SyntaxKind.DefineDirectiveTrivia &&
                triviaKind != SyntaxKind.UndefDirectiveTrivia &&
                triviaKind != SyntaxKind.ErrorDirectiveTrivia &&
                triviaKind != SyntaxKind.WarningDirectiveTrivia &&
                triviaKind != SyntaxKind.PragmaWarningDirectiveTrivia &&
                triviaKind != SyntaxKind.PragmaChecksumDirectiveTrivia &&
                triviaKind != SyntaxKind.ReferenceDirectiveTrivia &&
                triviaKind != SyntaxKind.BadDirectiveTrivia &&
                triviaKind != SyntaxKind.SingleLineCommentTrivia &&
                triviaKind != SyntaxKind.MultiLineCommentTrivia)
            {
                if (!trivia.HasStructure)
                    _currentMethodAndPropertyWriter.Write(trivia.ToString());
            }
        }
        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            // If the current node is flagged for potential patching
            if (_systemDescription.CandidateNodes.TryGetValue(node, out CandidateSyntax candidateSyntax))
            {
                // Cede write control to the appropriate syntax walker by calling `walker.TryWriteSyntax()`.
                // If the method returns true, then the controlling walker has written the syntax for the current node.
                // If false, then it is the responsibility of the current walker to write the syntax for the current node.
                var success = _systemDescription.SyntaxWalkers[candidateSyntax.GetOwningModule()].TryWriteSyntax(_currentMethodAndPropertyWriter, candidateSyntax);
                _currentMemberRequiresSourceGen |= success;

                if (!success)
                    base.VisitInvocationExpression(node);
            }
            else
                base.VisitInvocationExpression(node);
        }

        public override void VisitGenericName(GenericNameSyntax node)
        {
            if (_systemDescription.CandidateNodes.TryGetValue(node, out CandidateSyntax candidateSyntax) && candidateSyntax.Type == CandidateType.Ife)
            {
                // Cede write control to the `IfeSyntaxWalker`. If it returns `true`, then it has written the syntax for the current node.
                var success = _systemDescription.SyntaxWalkers[Module.Ife].TryWriteSyntax(_currentMethodAndPropertyWriter, candidateSyntax);
                _currentMemberRequiresSourceGen |= success;

                // If the `IfeSyntaxWalker` did not write the syntax for the current node,
                // then it is the responsibility of the current walker to write it.
                if (!success)
                    base.VisitGenericName(node);
            }
            else
                base.VisitGenericName(node);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            // If the current node is flagged for potential patching
            if (_systemDescription.CandidateNodes.TryGetValue(node, out CandidateSyntax candidateSyntax) && candidateSyntax.Type <= CandidateType.MaxSystemAPI)
            {
                // Cede write control to the `SystemApiWalker`. If it returns `true`, then it has written the syntax for the current node.
                var success = _systemDescription.SyntaxWalkers[Module.SystemApiContext].TryWriteSyntax(_currentMethodAndPropertyWriter, candidateSyntax);
                _currentMemberRequiresSourceGen |= success;

                // If the `SystemApiWalker` did not write the syntax for the current node,
                // then it is the responsibility of the current walker to write it.
                if (!success)
                    base.VisitMemberAccessExpression(node);
            }
            else
                base.VisitMemberAccessExpression(node);
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            // If the current node is flagged for potential patching
            if (_systemDescription.CandidateNodes.TryGetValue(node, out CandidateSyntax candidateSyntax) && candidateSyntax.Type <= CandidateType.MaxSystemAPI)
            {
                // Cede write control to the `SystemApiWalker`. If it returns `true`, then it has written the syntax for the current node.
                var success = _systemDescription.SyntaxWalkers[Module.SystemApiContext].TryWriteSyntax(_currentMethodAndPropertyWriter, candidateSyntax);
                _currentMemberRequiresSourceGen |= success;

                // If the `SystemApiWalker` did not write the syntax for the current node,
                // then it is the responsibility of the current walker to write it.
                if (!success)
                    base.VisitIdentifierName(node);
            }
            else
                base.VisitIdentifierName(node);
        }
    }
}
