using System;
using System.Linq;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace Unity.Entities.CodeGen
{
    public class FoundErrorInUserCodeException : Exception
    {
        public DiagnosticMessage[] DiagnosticMessages { get; }

        public FoundErrorInUserCodeException(DiagnosticMessage[] diagnosticMessages)
        {
            DiagnosticMessages = diagnosticMessages;
        }

        public  override string ToString() => DiagnosticMessages.Select(dm => dm.MessageData).SeparateByComma();
    }
}
