using System;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace Unity.Jobs.CodeGen
{
    static class InternalCompilerError
    {
        public static DiagnosticMessage DCICE300(TypeReference producerReference, TypeReference jobStructType, Exception ex)
        {
            return UserError.MakeError(nameof(DCICE300), $"Unexpected error while generating automatic registration for job provider {producerReference.FullName} via job struct {jobStructType.FullName}. Please report this error.\nException: {ex.Message}");
        }
    }

    static class UserError
    {
        public static DiagnosticMessage DC3001(TypeReference type)
        {
            return MakeError(nameof(DC3001), $"{type.FullName}: [RegisterGenericJobType] requires an instance of a generic value type");
        }

        static DiagnosticMessage MakeInternal(DiagnosticType type, string errorCode, string messageData)
        {
            var result = new DiagnosticMessage {Column = 0, Line = 0, DiagnosticType = type, File = ""};

            if (errorCode.Contains("ICE"))
            {
                messageData = messageData + " Seeing this error indicates a bug in the DOTS Job code-generators. We'd appreciate a bug report (About->Report a Bug...). Thnx! <3";
            }

            var errorType = type == DiagnosticType.Error ? "error" : "warning";
            messageData = $"{errorType} {errorCode}: {messageData}";

            result.MessageData = messageData;

            return result;
        }

        public static DiagnosticMessage MakeError(string errorCode, string messageData)
        {
            return MakeInternal(DiagnosticType.Error, errorCode, messageData);
        }
    }
}
