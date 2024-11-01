using System.CodeDom.Compiler;
using System.Linq;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.JobEntityGenerator;

internal static class JobEntityIndentedTextWriterExtensions
{
    internal static void WriteInternalCompilerStruct(this IndentedTextWriter writer, bool requiresEntityManager)
    {
        writer.WriteLine("/// <summary> Internal structure used by the compiler</summary>");
        writer.WriteLine("public struct InternalCompiler");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        writer.WriteLine("[global::System.Diagnostics.Conditional(\"ENABLE_UNITY_COLLECTIONS_CHECKS\")]");
        writer.WriteLine("// scheduleType 0:Run, 1:Schedule, 2:ScheduleParallel");
        writer.WriteLine("public static void CheckForErrors(int scheduleType)");
        writer.WriteLine("{");
        writer.Indent++;
        if (requiresEntityManager)
            writer.WriteLine("if(scheduleType == 2) throw new global::System.InvalidOperationException(\"Tried to ScheduleParallel a job with a managed execute signature. Please use .Run or .Schedule instead.\");");
        writer.Indent--;
        writer.WriteLine("}");
        writer.Indent--;
        writer.WriteLine("}");
    }

    internal static void WriteScheduleAndRunMethods(this IndentedTextWriter writer)
    {
        writer.WriteLine(@"global::Unity.Jobs.JobHandle __ThrowCodeGenException() => throw new global::System.Exception(""This method should have been replaced by source gen."");");
        writer.WriteLine();
        writer.WriteLine(@"// Emitted to disambiguate scheduling method invocations");
        writer.WriteLine(@"public void Run() => __ThrowCodeGenException();");
        writer.WriteLine(@"public void RunByRef() => __ThrowCodeGenException();");
        writer.WriteLine(@"public void Run(global::Unity.Entities.EntityQuery query) => __ThrowCodeGenException();");
        writer.WriteLine(@"public void RunByRef(global::Unity.Entities.EntityQuery query) => __ThrowCodeGenException();");
        writer.WriteLine(@"public global::Unity.Jobs.JobHandle Schedule(global::Unity.Jobs.JobHandle dependsOn) => __ThrowCodeGenException();");
        writer.WriteLine(@"public global::Unity.Jobs.JobHandle ScheduleByRef(global::Unity.Jobs.JobHandle dependsOn) => __ThrowCodeGenException();");
        writer.WriteLine(@"public global::Unity.Jobs.JobHandle Schedule(global::Unity.Entities.EntityQuery query, global::Unity.Jobs.JobHandle dependsOn) => __ThrowCodeGenException();");
        writer.WriteLine(@"public global::Unity.Jobs.JobHandle ScheduleByRef(global::Unity.Entities.EntityQuery query, global::Unity.Jobs.JobHandle dependsOn) => __ThrowCodeGenException();");
        writer.WriteLine(@"public void Schedule() => __ThrowCodeGenException();");
        writer.WriteLine(@"public void ScheduleByRef() => __ThrowCodeGenException();");
        writer.WriteLine(@"public void Schedule(global::Unity.Entities.EntityQuery query) => __ThrowCodeGenException();");
        writer.WriteLine(@"public void ScheduleByRef(global::Unity.Entities.EntityQuery query) => __ThrowCodeGenException();");
        writer.WriteLine(@"public global::Unity.Jobs.JobHandle ScheduleParallel(global::Unity.Jobs.JobHandle dependsOn) => __ThrowCodeGenException();");
        writer.WriteLine(@"public global::Unity.Jobs.JobHandle ScheduleParallelByRef(global::Unity.Jobs.JobHandle dependsOn) => __ThrowCodeGenException();");
        writer.WriteLine(@"public global::Unity.Jobs.JobHandle ScheduleParallel(global::Unity.Entities.EntityQuery query, global::Unity.Jobs.JobHandle dependsOn) => __ThrowCodeGenException();");
        writer.WriteLine(@"public global::Unity.Jobs.JobHandle ScheduleParallelByRef(global::Unity.Entities.EntityQuery query, global::Unity.Jobs.JobHandle dependsOn) => __ThrowCodeGenException();");
        writer.WriteLine(@"public global::Unity.Jobs.JobHandle ScheduleParallel(global::Unity.Entities.EntityQuery query, global::Unity.Jobs.JobHandle dependsOn, global::Unity.Collections.NativeArray<int> chunkBaseEntityIndices) => __ThrowCodeGenException();");
        writer.WriteLine(@"public global::Unity.Jobs.JobHandle ScheduleParallelByRef(global::Unity.Entities.EntityQuery query, global::Unity.Jobs.JobHandle dependsOn, global::Unity.Collections.NativeArray<int> chunkBaseEntityIndices) => __ThrowCodeGenException();");
        writer.WriteLine(@"public void ScheduleParallel() => __ThrowCodeGenException();");
        writer.WriteLine(@"public void ScheduleParallelByRef() => __ThrowCodeGenException();");
        writer.WriteLine(@"public void ScheduleParallel(global::Unity.Entities.EntityQuery query) => __ThrowCodeGenException();");
        writer.WriteLine(@"public void ScheduleParallelByRef(global::Unity.Entities.EntityQuery query) => __ThrowCodeGenException();");
    }

    internal static void WriteExecuteMethod(this IndentedTextWriter writer,
        bool inheritsFromBeginEndChunk,
        bool hasEnableableComponent,
        JobEntityParam[] userExecuteMethodParameters)
    {
        writer.WriteLine("[global::System.Runtime.CompilerServices.CompilerGenerated]");
        writer.WriteLine("public void Execute(in global::Unity.Entities.ArchetypeChunk chunk, int chunkIndexInQuery, bool useEnabledMask, in global::Unity.Burst.Intrinsics.v128 chunkEnabledMask)");
        writer.WriteLine("{");
        writer.Indent++;

        if (inheritsFromBeginEndChunk)
        {
            writer.WriteLine("var shouldExecuteChunk = OnChunkBegin(in chunk, chunkIndexInQuery, useEnabledMask, in chunkEnabledMask);");
            writer.WriteLine("if (shouldExecuteChunk)");
            writer.WriteLine("{");
            writer.Indent++;
        }

        foreach (var param in userExecuteMethodParameters)
        {
            if (!string.IsNullOrEmpty(param.VariableDeclarationAtStartOfExecuteMethod))
                writer.WriteLine(param.VariableDeclarationAtStartOfExecuteMethod);
        }

        writer.WriteLine("int chunkEntityCount = chunk.Count;");
        writer.WriteLine("int matchingEntityCount = 0;");
        writer.WriteLine();

        if (hasEnableableComponent)
        {
            writer.WriteLine("if (!useEnabledMask)");
            writer.WriteLine("{");
            writer.Indent++;
        }

        writer.WriteLine("for(int entityIndexInChunk = 0; entityIndexInChunk < chunkEntityCount; ++entityIndexInChunk)");
        writer.WriteLine("{");
        writer.Indent++;

        foreach (var param in userExecuteMethodParameters)
        {
            if (param.RequiresExecuteMethodArgumentSetup)
                writer.WriteLine(param.ExecuteMethodArgumentSetup);
        }

        writer.WriteLine($"Execute({userExecuteMethodParameters.Select(param => param.ExecuteMethodArgumentValue).SeparateByComma()});");
        writer.WriteLine("matchingEntityCount++;");
        writer.Indent--;
        writer.WriteLine("}");

        if (!hasEnableableComponent)
            return;

        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine("else");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine(
            "int edgeCount = global::Unity.Mathematics.math.countbits(chunkEnabledMask.ULong0 ^ (chunkEnabledMask.ULong0 << 1)) " +
            "+ global::Unity.Mathematics.math.countbits(chunkEnabledMask.ULong1 ^ (chunkEnabledMask.ULong1 << 1)) - 1;");
        writer.WriteLine("bool useRanges = edgeCount <= 4;");
        writer.WriteLine("if (useRanges)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("int entityIndexInChunk = 0;");
        writer.WriteLine("int chunkEndIndex = 0;");
        writer.WriteLine();
        writer.WriteLine("while (global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeTryGetNextEnabledBitRange(chunkEnabledMask, chunkEndIndex, out entityIndexInChunk, out chunkEndIndex))");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("while (entityIndexInChunk < chunkEndIndex)");
        writer.WriteLine("{");
        writer.Indent++;
        foreach (var param in userExecuteMethodParameters)
        {
            if (param.RequiresExecuteMethodArgumentSetup)
                writer.WriteLine(param.ExecuteMethodArgumentSetup);
        }
        writer.WriteLine($"Execute({userExecuteMethodParameters.Select(param => param.ExecuteMethodArgumentValue).SeparateByComma()});");
        writer.WriteLine("entityIndexInChunk++;");
        writer.WriteLine("matchingEntityCount++;");
        writer.Indent--;
        writer.WriteLine("}");
        writer.Indent--;
        writer.WriteLine("}");
        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine("else");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("ulong mask64 = chunkEnabledMask.ULong0;");
        writer.WriteLine("int count = global::Unity.Mathematics.math.min(64, chunkEntityCount);");
        writer.WriteLine("for (int entityIndexInChunk = 0; entityIndexInChunk < count; ++entityIndexInChunk)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("if ((mask64 & 1) != 0)");
        writer.WriteLine("{");
        writer.Indent++;
        foreach (var param in userExecuteMethodParameters)
        {
            if (param.RequiresExecuteMethodArgumentSetup)
                writer.WriteLine(param.ExecuteMethodArgumentSetup);
        }
        writer.WriteLine($"Execute({userExecuteMethodParameters.Select(param => param.ExecuteMethodArgumentValue).SeparateByComma()});");
        writer.WriteLine("matchingEntityCount++;");
        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine("mask64 >>= 1;");
        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine("mask64 = chunkEnabledMask.ULong1;");
        writer.WriteLine("for (int entityIndexInChunk = 64; entityIndexInChunk < chunkEntityCount; ++entityIndexInChunk)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("if ((mask64 & 1) != 0)");
        writer.WriteLine("{");
        writer.Indent++;
        foreach (var param in userExecuteMethodParameters)
        {
            if (param.RequiresExecuteMethodArgumentSetup)
                writer.WriteLine(param.ExecuteMethodArgumentSetup);
        }
        writer.WriteLine($"Execute({userExecuteMethodParameters.Select(param => param.ExecuteMethodArgumentValue).SeparateByComma()});");
        writer.WriteLine("matchingEntityCount++;");
        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine("mask64 >>= 1;");
        writer.Indent--;
        writer.WriteLine("}");
        writer.Indent--;
        writer.WriteLine("}");
        writer.Indent--;
        writer.WriteLine("}");
        writer.Indent--;
        writer.WriteLine("}");
    }
}
