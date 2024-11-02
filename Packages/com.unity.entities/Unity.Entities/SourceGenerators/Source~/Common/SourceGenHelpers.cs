using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Unity.Entities.SourceGen.Common
{
    public static class SourceGenHelpers
    {
        public static void LogError(this GeneratorExecutionContext context, string errorCode, string title, string errorMessage, Location location, string description = "")
        {
            if (errorCode.Contains("ICE"))
                errorMessage = "This error indicates a bug in the DOTS source generators. We'd appreciate a bug report (Help -> Report a Bug...). Thanks! " +
                    $"Error message: '{errorMessage}'";

            SourceOutputHelpers.LogInfoToSourceGenLog($"{DiagnosticSeverity.Error}: {errorCode}, {title}, {errorMessage}");
            var rule = new DiagnosticDescriptor(errorCode, title, errorMessage, "Source Generator", DiagnosticSeverity.Error, true, description);
            context.ReportDiagnostic(Diagnostic.Create(rule, location));
        }

        public static bool TryParseQualifiedEnumValue<TEnum>(string value, out TEnum result) where TEnum : struct
        {
            var unqualifiedEnumValue = value.Split('.').Last();
            return Enum.TryParse(unqualifiedEnumValue, out result) && Enum.IsDefined(typeof(TEnum), result);
        }

        public static SourceText WithInitialLineDirectiveToGeneratedSource(this SourceText sourceText, string generatedSourceFilePath)
        {
            var firstLine = sourceText.Lines.FirstOrDefault();
            return sourceText.WithChanges(new TextChange(firstLine.Span, $"#line 1 \"{generatedSourceFilePath}\"" + Environment.NewLine + firstLine));
        }

        public static SourceText WithIgnoreUnassignedVariableWarning(this SourceText sourceText)
        {
            var firstLine = sourceText.Lines.FirstOrDefault();
            return sourceText.WithChanges(new TextChange(firstLine.Span, $"#pragma warning disable 0219" + Environment.NewLine + firstLine));
        }

        // Stable version of String.GetHashCode
        public static int GetStableHashCode(string str)
        {
            unchecked
            {
                var hash1 = 5381;
                var hash2 = hash1;

                for(var i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i+1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i+1];
                }

                return hash1 + (hash2*1566083941);
            }
        }

        /// <summary>
        /// Returns true if running as part of csc.exe, otherwise we are likely running in the IDE.
        /// Skipping Source Generation in the IDE can be a considerable performance win as source
        /// generators can be run multiple times per keystroke. If the user doesn't rely on generated types
        /// consider skipping your Generator's Execute method when this returns false
        /// </summary>
        public static readonly bool IsBuildTime = Assembly.GetEntryAssembly() != null;

        public static bool ShouldRun(Compilation compilation, CancellationToken cancellationToken)
        {
            // Throw if we are cancelled
            cancellationToken.ThrowIfCancellationRequested();

            // Don't run if we don't reference Entities (or are Entities) or if we are CodeGen.Tests (which need to run generators manually)
            return (compilation.Assembly.Name == "Unity.Entities" ||
                    compilation.ReferencedAssemblyNames.Any(n => n.Name == "Unity.Entities")) &&
                   !compilation.Assembly.Name.Contains("CodeGen.Tests");
        }
    }
}
