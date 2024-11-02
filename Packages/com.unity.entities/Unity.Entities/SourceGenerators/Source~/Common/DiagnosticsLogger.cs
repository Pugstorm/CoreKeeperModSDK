using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.Common
{
    public static class DiagnosticsLogger
    {
        public static void LogError<TDiagnosable>(this TDiagnosable diagnosable, string errorCode, string title, string errorMessage, Location location, string description = "") where TDiagnosable : ISourceGeneratorDiagnosable
        {
            if (errorCode.Contains("ICE"))
                errorMessage = "This error indicates a bug in the DOTS source generators. We'd appreciate a bug report (Help -> Report a Bug...). Thanks! " +
                    $"Error message: '{errorMessage}'";

            Log(diagnosable, DiagnosticSeverity.Error, errorCode, title, errorMessage, location, description);
        }

        public static void LogWarning<TDiagnosable>(this TDiagnosable diagnosable, string errorCode, string title, string errorMessage, Location location, string description = "") where TDiagnosable : ISourceGeneratorDiagnosable
            => Log(diagnosable, DiagnosticSeverity.Warning, errorCode, title, errorMessage, location, description);

        public static void LogInfo<TDiagnosable>(this TDiagnosable diagnosable, string errorCode, string title, string errorMessage, Location location, string description = "") where TDiagnosable : ISourceGeneratorDiagnosable
            => Log(diagnosable, DiagnosticSeverity.Info, errorCode, title, errorMessage, location, description);

        static void Log<TDiagnosable>(this TDiagnosable diagnosable, DiagnosticSeverity diagnosticSeverity, string errorCode, string title, string errorMessage, Location location, string description = "") where TDiagnosable : ISourceGeneratorDiagnosable
        {
            if (location.IsInMetadata)
                throw new InvalidOperationException(
                    "Errors thrown by source generators must point to locations in source code, NOT locations in metadata. " +
                    $"Please ensure that the `{errorCode}` error points to a valid location in source code.");

            SourceOutputHelpers.LogInfoToSourceGenLog($"{diagnosticSeverity}: {errorCode}, {title}, {errorMessage}");
            var rule = new DiagnosticDescriptor(errorCode, title, errorMessage, "Source Generator", diagnosticSeverity, true, description);
            diagnosable.SourceGenDiagnostics?.Add(Diagnostic.Create(rule, location));
        }
    }

    public interface ISourceGeneratorDiagnosable
    {
        public List<Diagnostic> SourceGenDiagnostics { get; }
    }
}
