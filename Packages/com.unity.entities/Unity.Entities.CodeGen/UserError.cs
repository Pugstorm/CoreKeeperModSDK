using System;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace Unity.Entities.CodeGen
{
    static class UserError
    {
        static DiagnosticMessage MakeInternal(DiagnosticType type, string errorCode, string messageData, MethodDefinition method, Instruction instruction)
        {
            var result = new DiagnosticMessage {Column = 0, Line = 0, DiagnosticType = type, File = ""};

            var seq = instruction != null ? CecilHelpers.FindBestSequencePointFor(method, instruction) : null;

            if (errorCode.Contains("ICE"))
            {
                messageData = messageData + " Seeing this error indicates a bug in the DOTS source generators. We'd appreciate a bug report (About->Report a Bug...). Thnx! <3";
            }

            var errorType = type == DiagnosticType.Error ? "error" : "warning";
            messageData = $"{errorType} {errorCode}: {messageData}";
            if (seq != null)
            {
                result.File = seq.Document.Url;
                result.Column = seq.StartColumn;
                result.Line = seq.StartLine;
                var shortenedFilePath = seq.Document.Url.Replace($"{Environment.CurrentDirectory}{Path.DirectorySeparatorChar}", "");
                result.MessageData = $"{shortenedFilePath}({seq.StartLine},{seq.StartColumn}): {messageData}";
            }
            else
            {
                result.MessageData = messageData;
            }

            return result;
        }

        public static DiagnosticMessage MakeWarning(string errorCode, string messageData, MethodDefinition method, Instruction instruction)
        {
            return MakeInternal(DiagnosticType.Warning, errorCode, messageData, method, instruction);
        }

        public static DiagnosticMessage DC3002(TypeReference type)
        {
            return MakeInternal(DiagnosticType.Error, nameof(DC3002),
                $"{type.FullName}: [RegisterGenericJobType] requires an instance of a generic value type", null, null);
        }
    }
}
