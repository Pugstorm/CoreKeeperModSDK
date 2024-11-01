using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.LambdaJobs;

public struct ExecuteMethodWriter : IMemberWriter
{
    const string TemporaryJobEntityCommandBufferVariableName = "__tempJobEcb";

    public LambdaJobDescription LambdaJobDescription;

    IEnumerable<string> GetJobStructFieldAssignments()
    {
        foreach (var capturedVariable in LambdaJobDescription.VariablesCaptured)
            yield return $@"{capturedVariable.VariableFieldName} = {capturedVariable.OriginalVariableName}";

        if (!LambdaJobDescription.WithStructuralChanges)
            foreach (var param in LambdaJobDescription.LambdaParameters)
            {
                var assignment = param.FieldAssignmentInGeneratedJobChunkType();
                if (assignment != null)
                    yield return $"{assignment}";
            }

        foreach (var field in LambdaJobDescription.AdditionalFields)
            yield return field.JobStructAssign();

        if (LambdaJobDescription.NeedsTimeData)
            yield return "__Time = this.CheckedStateRef.WorldUnmanaged.Time";
    }

    public void WriteTo(IndentedTextWriter writer)
    {
        if (LambdaJobDescription.NeedsUnsafe)
            writer.Write("unsafe ");

        writer.Write(LambdaJobDescription.Schedule.DependencyArgument != null ? "Unity.Jobs.JobHandle " : "void ");
        writer.Write(LambdaJobDescription.ExecuteInSystemMethodName);
        writer.Write("(");
        writer.Write(GetExecuteMethodParams().SeparateByComma());
        writer.WriteLine(")");
        writer.WriteLine("{");
        writer.Indent++;

        foreach (var p in LambdaJobDescription.LambdaParameters)
        {
            if (p is IParamRequireUpdate requireUpdate)
                writer.WriteLine($"{requireUpdate.FormatUpdateInvocation(LambdaJobDescription)}");
        }

        if (!LambdaJobDescription.WithStructuralChanges)
        {
            foreach (var fieldDescription in LambdaJobDescription.AdditionalFields)
                writer.WriteLine($"{fieldDescription.FormatUpdateInvocation()};");
        }

        if (LambdaJobDescription.EntityCommandBufferParameter is { Playback: { IsImmediate: true } })
            writer.WriteLine($"global::Unity.Entities.EntityCommandBuffer {TemporaryJobEntityCommandBufferVariableName} = new global::Unity.Entities.EntityCommandBuffer(this.World.UpdateAllocator.ToAllocator);");

        writer.WriteLine($"var __job = new {LambdaJobDescription.JobStructName}");
        writer.WriteLine("{");
        writer.Indent++;

        var assignments = GetJobStructFieldAssignments().ToArray();
        for (var index = 0; index < assignments.Length; index++)
        {
            var assignment = assignments[index];
            writer.Write(assignment);
            if (index < assignments.Length - 1)
            {
                writer.Write(",");
                writer.WriteLine();
            }
        }
        writer.Indent--;
        writer.WriteLine();
        writer.WriteLine("};");

        foreach (var arg in LambdaJobDescription.WithSharedComponentFilterArgumentSyntaxes)
            writer.WriteLine($@"{LambdaJobDescription.EntityQueryFieldName}.SetSharedComponentFilter({arg});");

        if (LambdaJobDescription.Schedule.DependencyArgument != null)
            writer.WriteLine("Unity.Jobs.JobHandle __jobHandle;");

        writer.WriteLine(CalculateChunkBaseEntityIndices().SeparateByNewLine());

        var profilerEnabled = LambdaJobDescription.ProfilerEnabled && !LambdaJobDescription.IsForDOTSRuntime;
        if (profilerEnabled)
        {
            writer.WriteLine($"using ({LambdaJobDescription.JobStructName}.s_ProfilerMarker.Auto())");
            writer.WriteLine("{");
            writer.Indent++;
        }

        WriteScheduleInvocation(writer);

        if (profilerEnabled)
        {
            writer.Indent--;
            writer.WriteLine("}");
        }

        if (LambdaJobDescription.WithSharedComponentFilterArgumentSyntaxes.Count > 0)
            writer.WriteLine( $@"{LambdaJobDescription.EntityQueryFieldName}.ResetFilter();");

        if (LambdaJobDescription.DisposeOnJobCompletionVariables.Count > 0)
        {
            if (LambdaJobDescription.Schedule.Mode == ScheduleMode.Run)
                writer.WriteLine("__job.DisposeOnCompletion();");
            else if (LambdaJobDescription.Schedule.DependencyArgument != null)
                writer.WriteLine(@"__jobHandle = __job.DisposeOnCompletion(__jobHandle);");
            else
                writer.WriteLine(@"this.CheckedStateRef.Dependency = __job.DisposeOnCompletion(this.CheckedStateRef.Dependency);");
        }
        bool addJobHandleForProducer = LambdaJobDescription.EntityCommandBufferParameter is {Playback: {IsImmediate: false}}
                                       && LambdaJobDescription.EntityCommandBufferParameter.Playback.ScheduleMode != ScheduleMode.Run;
        if (addJobHandleForProducer)
            writer.WriteLine($"{LambdaJobDescription.EntityCommandBufferParameter.GeneratedEcbFieldNameInSystemBaseType}.AddJobHandleForProducer(Dependency);");

        var createTempEcb = CreateTemporaryEcb();
        if (createTempEcb != null)
        {
            writer.WriteLine($"{TemporaryJobEntityCommandBufferVariableName}.Playback(EntityManager);");
            writer.WriteLine($"{TemporaryJobEntityCommandBufferVariableName}.Dispose();");
        }

        if (LambdaJobDescription.Schedule.Mode == ScheduleMode.Run)
        {
            foreach (var v in LambdaJobDescription.VariablesCaptured)
            {
                if (v is {IsThis: false, IsWritable: true})
                    writer.WriteLine( $@"{v.OriginalVariableName} = __job.{v.VariableFieldName};");
            }
        }

        if (LambdaJobDescription.Schedule.DependencyArgument != null)
            writer.WriteLine("return __jobHandle;");

        writer.Indent--;
        writer.WriteLine("}");
    }

    void WriteScheduleInvocation(IndentedTextWriter writer)
    {
        if (LambdaJobDescription.LambdaJobKind == LambdaJobKind.Entities)
            ScheduleEntitiesInvocation(writer);
        else
            ScheduleJobInvocation(writer);
    }

    void ScheduleEntitiesInvocation(IndentedTextWriter writer)
    {
        switch (LambdaJobDescription.Schedule.Mode)
        {
            case ScheduleMode.Run:
            {
                if (LambdaJobDescription.WithStructuralChanges)
                {
                    writer.WriteLine($"if(!{LambdaJobDescription.EntityQueryFieldName}.IsEmptyIgnoreFilter)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine("this.CheckedStateRef.CompleteDependency();");
                    writer.WriteLine($"__job.RunWithStructuralChange({LambdaJobDescription.EntityQueryFieldName});");
                    writer.Indent--;
                    writer.WriteLine("}");
                }
                else {
                    writer.WriteLine($"if(!{LambdaJobDescription.EntityQueryFieldName}.IsEmptyIgnoreFilter)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine($"this.CheckedStateRef.CompleteDependency();");
                    writer.WriteLine($"var __jobPtr = global::Unity.Entities.Internal.InternalCompilerInterface.AddressOf(ref __job);");
                    writer.WriteLine($"{LambdaJobDescription.JobStructName}.RunWithoutJobSystem(ref {LambdaJobDescription.EntityQueryFieldName}, __jobPtr);");
                    writer.Indent--;
                    writer.WriteLine("}");
                }
                break;
            }

            case ScheduleMode.Schedule:
            {
                writer.WriteLine(LambdaJobDescription.Schedule.DependencyArgument != null
                    ? $@"__jobHandle = global::Unity.Entities.Internal.InternalCompilerInterface.JobChunkInterface.Schedule(__job, {LambdaJobDescription.EntityQueryFieldName}{(LambdaJobDescription.WithScheduleGranularityArgumentSyntaxes.Count > 0 ? ", default" : "")}, __inputDependency);"
                    : $@"this.CheckedStateRef.Dependency = global::Unity.Entities.Internal.InternalCompilerInterface.JobChunkInterface.Schedule(__job, {LambdaJobDescription.EntityQueryFieldName}{(LambdaJobDescription.WithScheduleGranularityArgumentSyntaxes.Count > 0 ? ", default" : "")}, this.CheckedStateRef.Dependency);");
                break;
            }

            case ScheduleMode.ScheduleParallel:
            {
                writer.WriteLine(LambdaJobDescription.Schedule.DependencyArgument != null
                    ? $@"__jobHandle = global::Unity.Entities.Internal.InternalCompilerInterface.JobChunkInterface.ScheduleParallel(__job, {LambdaJobDescription.EntityQueryFieldName}{(LambdaJobDescription.WithScheduleGranularityArgumentSyntaxes.Count > 0 ? $", {LambdaJobDescription.WithScheduleGranularityArgumentSyntaxes.ElementAt(0)}" : "")}{(LambdaJobDescription.WithScheduleGranularityArgumentSyntaxes.Count > 0 ? ", default" : "")}, __inputDependency{(LambdaJobDescription.NeedsEntityInQueryIndex && LambdaJobDescription.Schedule.Mode == ScheduleMode.ScheduleParallel ? $", {LambdaJobDescription.ChunkBaseEntityIndexFieldName}" : "")});"
                    : $@"this.CheckedStateRef.Dependency = global::Unity.Entities.Internal.InternalCompilerInterface.JobChunkInterface.ScheduleParallel(__job, {LambdaJobDescription.EntityQueryFieldName}{(LambdaJobDescription.WithScheduleGranularityArgumentSyntaxes.Count > 0 ? $", {LambdaJobDescription.WithScheduleGranularityArgumentSyntaxes.ElementAt(0)}" : "")}{(LambdaJobDescription.WithScheduleGranularityArgumentSyntaxes.Count > 0 ? ", default" : "")}, this.CheckedStateRef.Dependency{(LambdaJobDescription.NeedsEntityInQueryIndex && LambdaJobDescription.Schedule.Mode == ScheduleMode.ScheduleParallel ? $", {LambdaJobDescription.ChunkBaseEntityIndexFieldName}" : "")});");
                break;
            }
            default:
                throw new InvalidOperationException("Can't create ScheduleJobInvocation for invalid lambda description");
        }
    }

    void ScheduleJobInvocation(IndentedTextWriter writer)
    {
        switch (LambdaJobDescription.Schedule.Mode)
        {
            case ScheduleMode.Run:
            {
                writer.WriteLine("this.CheckedStateRef.CompleteDependency();");
                writer.WriteLine($"var __jobPtr = global::Unity.Entities.Internal.InternalCompilerInterface.AddressOf(ref __job);");
                writer.WriteLine($"{LambdaJobDescription.JobStructName}.RunWithoutJobSystem(__jobPtr);");

                break;
            }

            case ScheduleMode.Schedule:
            {
                writer.WriteLine(LambdaJobDescription.Schedule.DependencyArgument != null
                    ? "__jobHandle = global::Unity.Jobs.IJobExtensions.Schedule(__job, __inputDependency);"
                    : "this.CheckedStateRef.Dependency = global::Unity.Jobs.IJobExtensions.Schedule(__job, this.CheckedStateRef.Dependency);");
                break;
            }
        }
    }

    IEnumerable<string> CalculateChunkBaseEntityIndices()
    {
        if (!LambdaJobDescription.NeedsEntityInQueryIndex)
            yield return string.Empty;

        else if (LambdaJobDescription.Schedule.Mode == ScheduleMode.Run)
            yield return $"__job.__ChunkBaseEntityIndices = {LambdaJobDescription.EntityQueryFieldName}.CalculateBaseEntityIndexArray(this.CheckedStateRef.WorldUpdateAllocator);";

        else if (LambdaJobDescription.Schedule.DependencyArgument != null)
        {
            yield return
                @$"global::Unity.Collections.NativeArray<int> {LambdaJobDescription.ChunkBaseEntityIndexFieldName} = {LambdaJobDescription.EntityQueryFieldName}.CalculateBaseEntityIndexArrayAsync(this.CheckedStateRef.WorldUpdateAllocator, __inputDependency, out __inputDependency);";
            yield return $"__job.__ChunkBaseEntityIndices = {LambdaJobDescription.ChunkBaseEntityIndexFieldName};";
        }
        else
        {
            yield return "global::Unity.Jobs.JobHandle outHandle;";
            yield return $"global::Unity.Collections.NativeArray<int> {LambdaJobDescription.ChunkBaseEntityIndexFieldName} = {LambdaJobDescription.EntityQueryFieldName}.CalculateBaseEntityIndexArrayAsync(this.CheckedStateRef.WorldUpdateAllocator, Dependency, out outHandle);";
            yield return $"__job.__ChunkBaseEntityIndices = {LambdaJobDescription.ChunkBaseEntityIndexFieldName};";
            yield return "Dependency = outHandle;";
        }
    }

    IEnumerable<string> GetExecuteMethodParams()
    {
        var distinctParams = new HashSet<string>();
        foreach (var v in LambdaJobDescription.VariablesCaptured)
        {
            if (!v.IsThis)
            {
                if (LambdaJobDescription.Schedule.Mode == ScheduleMode.Run && v.IsWritable)
                    distinctParams.Add($"ref {v.Type.ToFullName()} {v.Symbol.Name}");
                else
                    distinctParams.Add($"{v.Type.ToFullName()} {v.Symbol.Name}");
            }
        }

        if (LambdaJobDescription.Schedule.DependencyArgument != null)
            distinctParams.Add("Unity.Jobs.JobHandle __inputDependency");

        if (LambdaJobDescription.WithFilterEntityArray != null)
            distinctParams.Add($"global::Unity.Entities.Entity* {LambdaJobDescription.WithFilterEntityArray}");

        foreach (var argument in LambdaJobDescription.AdditionalVariablesCapturedForScheduling)
            distinctParams.Add($@"{argument.Symbol.GetSymbolType().ToFullName()} {argument.Name}");

        return distinctParams;
    }

    string CreateTemporaryEcb()
    {
        if (LambdaJobDescription.EntityCommandBufferParameter == null)
            return null;
        return
            LambdaJobDescription.EntityCommandBufferParameter.Playback.IsImmediate
                ? $"global::Unity.Entities.EntityCommandBuffer {TemporaryJobEntityCommandBufferVariableName} = new global::Unity.Entities.EntityCommandBuffer(this.World.UpdateAllocator.ToAllocator);"
                : null;
    }
}
