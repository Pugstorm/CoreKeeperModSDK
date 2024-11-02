using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.LambdaJobs;

public struct JobStructWriter : IMemberWriter
{
    public LambdaJobDescription LambdaJobDescription;

    public void WriteTo(IndentedTextWriter writer)
    {
        writer.WriteLine(TypeCreationHelpers.GeneratedLineTriviaToGeneratedSource);

        // Generate Burst-related attributes
        if (LambdaJobDescription.Burst.IsEnabled)
        {
            writer.WriteLine("[global::Unity.Burst.NoAlias]");
            WriteBurstCompileAttribute(writer);
        }

        // Generate job struct
        writer.Write(LambdaJobDescription.NeedsUnsafe ? "unsafe " : string.Empty);
        writer.Write($"struct {LambdaJobDescription.JobStructName}");

        // Implement IJob or IJobChunk
        bool implementsIJobOrIJobChunk = false;
        if (LambdaJobDescription.LambdaJobKind == LambdaJobKind.Job)
        {
            writer.Write(" : global::Unity.Jobs.IJob");
            implementsIJobOrIJobChunk = true;
        }
        else if (!LambdaJobDescription.WithStructuralChanges)
        {
            writer.Write(" : global::Unity.Entities.IJobChunk");
            implementsIJobOrIJobChunk = true;
        }

        // Implement IJobBase if needed
        if (implementsIJobOrIJobChunk && LambdaJobDescription.IsForDOTSRuntime)
            writer.Write(", global::Unity.Jobs.IJobBase");

        writer.WriteLine();
        writer.WriteLine("{");
        writer.Indent++;

        if (LambdaJobDescription.IsForDOTSRuntime)
        {
            // Implement IJobBase methods
            writer.WriteLine(
                "public void PrepareJobAtExecuteTimeFn_Gen(int jobIndex, global::System.IntPtr localNodes)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("throw new global::System.NotImplementedException();");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
            writer.WriteLine("public void CleanupJobAfterExecuteTimeFn_Gen()");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("throw new global::System.NotImplementedException();");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
            writer.WriteLine("public void CleanupJobFn_Gen()");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("throw new global::System.NotImplementedException();");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
            writer.WriteLine(
                "public global::Unity.Jobs.LowLevel.Unsafe.JobsUtility.ManagedJobForEachDelegate GetExecuteMethod_Gen()");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("throw new global::System.NotImplementedException();");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
            writer.WriteLine("public int GetUnmanagedJobSize_Gen()");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("throw new global::System.NotImplementedException();");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
            writer.WriteLine(
                "public global::Unity.Jobs.LowLevel.Unsafe.JobsUtility.ManagedJobMarshalDelegate GetMarshalToBurstMethod_Gen()");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("throw new global::System.NotImplementedException();");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
            writer.WriteLine(
                "public global::Unity.Jobs.LowLevel.Unsafe.JobsUtility.ManagedJobMarshalDelegate GetMarshalFromBurstMethod_Gen()");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("throw new global::System.NotImplementedException();");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
            writer.WriteLine("public int IsBursted_Gen()");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("throw new global::System.NotImplementedException();");
            writer.Indent--;
            writer.WriteLine("}");

            writer.WriteLine();
            if (LambdaJobDescription.SafetyChecksEnabled)
            {
                writer.WriteLine(
                    "public int PrepareJobAtPreScheduleTimeFn_Gen(ref global::Unity.Development.JobsDebugger.DependencyValidator data, ref global::Unity.Jobs.JobHandle dependsOn, global::System.IntPtr deferredSafety)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("throw new global::System.NotImplementedException();");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine();
                writer.WriteLine(
                    "public void PrepareJobAtPostScheduleTimeFn_Gen(ref global::Unity.Development.JobsDebugger.DependencyValidator data, ref global::Unity.Jobs.JobHandle scheduledJob)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("throw new global::System.NotImplementedException();");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine();
                writer.WriteLine(
                    "public void PatchMinMax_Gen(global::Unity.Jobs.LowLevel.Unsafe.JobsUtility.MinMax param)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("throw new global::System.NotImplementedException();");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine();
                writer.WriteLine("public int GetSafetyFieldCount()");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("throw new global::System.NotImplementedException();");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine();
            }

            if (LambdaJobDescription.SafetyChecksEnabled || LambdaJobDescription.DOTSRuntimeProfilerEnabled)
            {
                writer.WriteLine("public int GetJobNameIndex_Gen()");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("throw new global::System.NotImplementedException();");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine();
            }
        }

        if (LambdaJobDescription.NeedsEntityInQueryIndex)
        {
            writer.WriteLine("[global::Unity.Collections.ReadOnly]");
            writer.WriteLine("public global::Unity.Collections.NativeArray<int> __ChunkBaseEntityIndices;");
        }

        if (LambdaJobDescription.NeedsTimeData)
            writer.WriteLine("public global::Unity.Core.TimeData __Time;");

        foreach (var variable in LambdaJobDescription.VariablesCaptured)
        {
            foreach (var attr in variable.Attributes) writer.WriteLine($"[{attr}]");
            writer.WriteLine(
                $"public {variable.Symbol.GetSymbolType().ToFullName()} {variable.VariableFieldName};");
        }

        if (!LambdaJobDescription.WithStructuralChanges)
        {
            foreach (var p in LambdaJobDescription.LambdaParameters)
                writer.WriteLine(p.FieldInGeneratedJobChunkType());
        }

        foreach (var field in LambdaJobDescription.AdditionalFields)
            writer.WriteLine(field.ToFieldDeclaration().ToString());

        if (LambdaJobDescription.ProfilerEnabled && !LambdaJobDescription.IsForDOTSRuntime)
        {
            writer.Write(
                "public static readonly global::Unity.Profiling.ProfilerMarker s_ProfilerMarker = new global::Unity.Profiling.ProfilerMarker(");

            if (LambdaJobDescription.Schedule.Mode == ScheduleMode.Run)
            {
                if (LambdaJobDescription.Burst.IsEnabled)
                    writer.Write(
                        "new global::Unity.Profiling.ProfilerCategory(\"Burst\", global::Unity.Profiling.ProfilerCategoryColor.BurstJobs), ");

                writer.Write("\"");
                writer.Write(LambdaJobDescription.Name);
                writer.Write("\");");
                writer.WriteLine();
            }
            else
            {
                writer.Write("\"");
                writer.Write(LambdaJobDescription.Name);
                writer.Write(LambdaJobDescription.Schedule.Mode == ScheduleMode.Schedule ? ".Schedule" : ".ScheduleParallel");
                writer.Write("\");");
                writer.WriteLine();
            }
        }

        writer.WriteLine();
        // Generate original lambda body
        if (LambdaJobDescription.Burst.IsEnabled)
            writer.WriteLine(
                "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");

        writer.Write("void OriginalLambdaBody(");

        bool hasPrevParam = false;
        foreach (var p in LambdaJobDescription.LambdaParameters)
        {
            var methodParameter = p.LambdaBodyMethodParameter(LambdaJobDescription.Burst.IsEnabled);
            if (methodParameter != null)
            {
                if (hasPrevParam)
                    writer.Write(", ");

                writer.Write(methodParameter);
                hasPrevParam = true;
            }
        }
        writer.Write(")");
        writer.WriteLine();
        writer.Write(LambdaJobDescription.RewrittenLambdaBody.WithoutPreprocessorTrivia());
        writer.WriteLine();

        writer.WriteLine($"{TypeCreationHelpers.GeneratedLineTriviaToGeneratedSource}");
        WriteExecuteMethod(writer);

        if (LambdaJobDescription.DisposeOnJobCompletionVariables.Count > 0)
        {
            if (LambdaJobDescription.Schedule.Mode == ScheduleMode.Run)
            {
                writer.WriteLine("public void DisposeOnCompletion()");
                writer.WriteLine("{");
                writer.Indent++;
                foreach (var v in LambdaJobDescription.DisposeOnJobCompletionVariables)
                foreach (var name in v.NamesOfAllDisposableMembersIncludingOurselves())
                    writer.WriteLine($"{name}.Dispose();");

                writer.Indent--;
                writer.WriteLine("}");
            }
            else
            {
                writer.WriteLine(
                    "public global::Unity.Jobs.JobHandle DisposeOnCompletion(global::Unity.Jobs.JobHandle jobHandle)");
                writer.WriteLine("{");
                writer.Indent++;
                foreach (var v in LambdaJobDescription.DisposeOnJobCompletionVariables)
                foreach (var name in v.NamesOfAllDisposableMembersIncludingOurselves())
                    writer.WriteLine($"jobHandle = {name}.Dispose(jobHandle);");

                writer.WriteLine("return jobHandle;");
                writer.Indent--;
                writer.WriteLine("}");
            }
        }

        if (LambdaJobDescription.Schedule.Mode == ScheduleMode.Run && !LambdaJobDescription.WithStructuralChanges)
        {
            if (LambdaJobDescription.LambdaJobKind == LambdaJobKind.Entities)
            {
                WriteBurstCompileAttribute(writer);

                writer.WriteLine(
                    $"public static void RunWithoutJobSystem(ref global::Unity.Entities.EntityQuery query, global::System.IntPtr jobPtr)");
                writer.WriteLine("{");
                writer.Indent++;
                if (LambdaJobDescription.IsForDOTSRuntime)
                    writer.WriteLine("global::Unity.Runtime.TempMemoryScope.EnterScope();");
                writer.WriteLine("try");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"ref var jobData = ref global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeAsRef<{LambdaJobDescription.JobStructName}>(jobPtr);");
                writer.WriteLine("global::Unity.Entities.Internal.InternalCompilerInterface.JobChunkInterface.RunWithoutJobsInternal(ref jobData, ref query);");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("finally");
                writer.WriteLine("{");
                writer.Indent++;
                if (LambdaJobDescription.IsForDOTSRuntime)
                    writer.WriteLine("global::Unity.Runtime.TempMemoryScope.ExitScope();");
                writer.Indent--;
                writer.WriteLine("}");
                writer.Indent--;
                writer.WriteLine("}");
            }
            else
            {
                WriteBurstCompileAttribute(writer);
                writer.WriteLine($"public static void RunWithoutJobSystem(global::System.IntPtr jobPtr)");
                writer.WriteLine("{");
                writer.Indent++;
                if (LambdaJobDescription.IsForDOTSRuntime)
                    writer.WriteLine("global::Unity.Runtime.TempMemoryScope.EnterScope();");
                writer.WriteLine("try");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"ref var jobData = ref global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeAsRef<{LambdaJobDescription.JobStructName}>(jobPtr);");
                writer.WriteLine("jobData.Execute();");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("finally");
                writer.WriteLine("{");
                writer.Indent++;
                if (LambdaJobDescription.IsForDOTSRuntime)
                    writer.WriteLine("global::Unity.Runtime.TempMemoryScope.ExitScope();");
                writer.Indent--;
                writer.WriteLine("}");
                writer.Indent--;
                writer.WriteLine("}");
            }
        }

        writer.Indent--;
        writer.WriteLine("}");
    }

    void WriteExecuteMethod(IndentedTextWriter writer)
    {
        if (LambdaJobDescription.LambdaJobKind == LambdaJobKind.Job)
        {
            writer.WriteLine("public void Execute()");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine();
            WritePerformLambda(writer);
            writer.Indent--;
            writer.WriteLine("}");
        }

        else if (LambdaJobDescription.WithStructuralChanges)
        {
            writer.WriteLine("public void RunWithStructuralChange(global::Unity.Entities.EntityQuery query)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"{TypeCreationHelpers.GeneratedLineTriviaToGeneratedSource}");
            writer.WriteLine($"var mask = query.GetEntityQueryMask();");
            writer.WriteLine(
                $"global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeCreateGatherEntitiesResult(ref query, out var gatherEntitiesResult);");

            foreach (var p in LambdaJobDescription.LambdaParameters)
            {
                var structuralChangesGetTypeIndex = p.StructuralChanges_GetTypeIndex();
                if (structuralChangesGetTypeIndex != null)
                    writer.WriteLine(structuralChangesGetTypeIndex);
            }

            writer.WriteLine("try");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("int entityCount = gatherEntitiesResult.EntityCount;");
            writer.WriteLine("for (int entityIndex = 0; entityIndex != entityCount; entityIndex++)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine(
                "var entity = global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeGetEntityFromGatheredEntities(ref gatherEntitiesResult, entityIndex);");
            writer.WriteLine("if (mask.MatchesIgnoreFilter(entity))");
            writer.WriteLine("{");
            writer.Indent++;
            foreach (var p in LambdaJobDescription.LambdaParameters)
            {
                var structuralChangesReadLambdaParam = p.StructuralChanges_ReadLambdaParam();
                if (structuralChangesReadLambdaParam != null)
                    writer.WriteLine(structuralChangesReadLambdaParam);
            }

            foreach (var f in LambdaJobDescription.AdditionalFields)
                writer.WriteLine($"{f.FieldName}.Update(__this);");

            WritePerformLambda(writer);

            foreach (var p in LambdaJobDescription.LambdaParameters)
            {
                var structuralChangesWriteBackLambdaParam = p.StructuralChanges_WriteBackLambdaParam();
                if (structuralChangesWriteBackLambdaParam != null)
                    writer.WriteLine(structuralChangesWriteBackLambdaParam);
            }

            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("finally");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine(
                "global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeReleaseGatheredEntities(ref query, ref gatherEntitiesResult);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }
        else
        {
            writer.WriteLine("[global::System.Runtime.CompilerServices.CompilerGenerated]");
            writer.WriteLine(
                "public void Execute(in global::Unity.Entities.ArchetypeChunk chunk, int batchIndex, bool useEnabledMask, in global::Unity.Burst.Intrinsics.v128 chunkEnabledMask)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"{TypeCreationHelpers.GeneratedLineTriviaToGeneratedSource}");
            foreach (var param in LambdaJobDescription.LambdaParameters)
            {
                var nativeArrayOrAccessor = param.GetNativeArrayOrAccessor();
                if (nativeArrayOrAccessor != null)
                    writer.WriteLine(nativeArrayOrAccessor);
            }

            writer.WriteLine("int chunkEntityCount = chunk.Count;");
            if (LambdaJobDescription.NeedsEntityInQueryIndex)
                writer.WriteLine("int matchingEntityCount = 0;");
            writer.WriteLine("if (!useEnabledMask)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("for(var entityIndex = 0; entityIndex < chunkEntityCount; ++entityIndex)");
            writer.WriteLine("{");
            writer.Indent++;

            if (LambdaJobDescription.NeedsEntityInQueryIndex)
                writer.WriteLine(
                    "var entityInQueryIndex = __ChunkBaseEntityIndices[batchIndex] + matchingEntityCount++;");

            WritePerformLambda(writer);
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("else");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine(
                "int edgeCount = global::Unity.Mathematics.math.countbits(chunkEnabledMask.ULong0 ^ (chunkEnabledMask.ULong0 << 1))" +
                " + global::Unity.Mathematics.math.countbits(chunkEnabledMask.ULong1 ^ (chunkEnabledMask.ULong1 << 1)) - 1;");
            writer.WriteLine("bool useRanges = edgeCount <= 4;");
            writer.WriteLine("if (useRanges)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("int entityIndex = 0;");
            writer.WriteLine("int batchEndIndex = 0;");
            writer.WriteLine(
                "while (global::Unity.Entities.Internal.InternalCompilerInterface.UnsafeTryGetNextEnabledBitRange(chunkEnabledMask, batchEndIndex, out entityIndex, out batchEndIndex))");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("while (entityIndex < batchEndIndex)");
            writer.WriteLine("{");
            writer.Indent++;
            if (LambdaJobDescription.NeedsEntityInQueryIndex)
                writer.WriteLine(
                    "var entityInQueryIndex = __ChunkBaseEntityIndices[batchIndex] + matchingEntityCount++;");
            WritePerformLambda(writer);
            writer.WriteLine("entityIndex++;");
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
            writer.WriteLine("for (var entityIndex = 0; entityIndex < count; ++entityIndex)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if ((mask64 & 1) != 0)");
            writer.WriteLine("{");
            writer.Indent++;
            if (LambdaJobDescription.NeedsEntityInQueryIndex)
                writer.WriteLine(
                    "var entityInQueryIndex = __ChunkBaseEntityIndices[batchIndex] + matchingEntityCount++;");
            WritePerformLambda(writer);
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("mask64 >>= 1;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("mask64 = chunkEnabledMask.ULong1;");
            writer.WriteLine("for (var entityIndex = 64; entityIndex < chunkEntityCount; ++entityIndex)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if ((mask64 & 1) != 0)");
            writer.WriteLine("{");
            writer.Indent++;
            if (LambdaJobDescription.NeedsEntityInQueryIndex)
                writer.WriteLine(
                    "var entityInQueryIndex = __ChunkBaseEntityIndices[batchIndex] + matchingEntityCount++;");
            WritePerformLambda(writer);
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
    void WriteBurstCompileAttribute(IndentedTextWriter textWriter)
    {
        if (!LambdaJobDescription.Burst.IsEnabled)
            return;

        var parameters = new List<string>(capacity: 4);
        if (LambdaJobDescription.Burst.Settings.BurstFloatMode != null)
            parameters.Add($"FloatMode=global::Unity.Burst.FloatMode.{LambdaJobDescription.Burst.Settings.BurstFloatMode}");
        if (LambdaJobDescription.Burst.Settings.BurstFloatPrecision != null)
            parameters.Add($"FloatPrecision=global::Unity.Burst.FloatPrecision.{LambdaJobDescription.Burst.Settings.BurstFloatPrecision}");
        if (LambdaJobDescription.Burst.Settings.SynchronousCompilation != null)
            parameters.Add($"CompileSynchronously={LambdaJobDescription.Burst.Settings.SynchronousCompilation.ToString().ToLower()}");

        if (parameters.Count == 0)
            textWriter.WriteLine("[global::Unity.Burst.BurstCompile]");
        else
        {
            textWriter.WriteLine("[global::Unity.Burst.BurstCompile(");
            for (var index = 0; index < parameters.Count; index++)
            {
                var p = parameters[index];
                textWriter.Write(p);
                if (index < parameters.Count - 1)
                    textWriter.Write(", ");
            }
            textWriter.WriteLine(")]");
        }
    }

    void WritePerformLambda(IndentedTextWriter writer)
    {
        foreach (var p in LambdaJobDescription.LambdaParameters)
        {
            var lambdaBodyParameterSetup = p.LambdaBodyParameterSetup();
            if (lambdaBodyParameterSetup != null)
                writer.WriteLine(lambdaBodyParameterSetup);
        }
        writer.Write("OriginalLambdaBody(");
        writer.Indent++;

        bool hasPrevParam = false;
        if (LambdaJobDescription.WithStructuralChanges)
        {
            foreach (var p in LambdaJobDescription.LambdaParameters)
            {
                var methodParameter = p.StructuralChanges_LambdaBodyParameter();
                if (methodParameter != null)
                {
                    if (hasPrevParam)
                        writer.Write(", ");

                    writer.Write(methodParameter);
                    hasPrevParam = true;
                }
            }
        }
        else
        {
            foreach (var p in LambdaJobDescription.LambdaParameters)
            {
                var methodParameter = p.LambdaBodyParameter();
                if (methodParameter != null)
                {
                    if (hasPrevParam)
                        writer.Write(", ");

                    writer.Write(methodParameter);
                    hasPrevParam = true;
                }
            }
        }
        writer.Write(");");
        writer.WriteLine();
        writer.Indent--;
    }
}
