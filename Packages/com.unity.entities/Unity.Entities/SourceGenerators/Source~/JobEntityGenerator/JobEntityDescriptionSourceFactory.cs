using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.JobEntityGenerator;

public partial class JobEntityDescription
{
    public string Generate()
    {
        var hasEnableableComponent = true; // HasEnableableComponent();
        // Currently, it is never set to false.
        // It could be false in cases where the IJobEntity is constructing its own EntityQuery based on the provided parameters,
        // in which case it can statically determine at source-generation time whether the query contains any enableable components
        // (and thus whether it needs to generate the extra code to handle enabled bits correctly).
        // This work has not yet been implemented. (e.g. when enableablebits is turned of with static query generation.)
        // Check should be based on whether the query contains enableablebits, not if the parameters does (cause some chunks might still need to check if they are enabled)
        // Discussion: https://github.cds.internal.unity3d.com/unity/dots/pull/3217#discussion_r227389
        // Also do this for EntitiesSourceFactory.JobStructFor if that is still a thing.

        var inheritsFromBeginEndChunk = m_JobEntityTypeSymbol.InheritsFromInterface("Unity.Entities.IJobEntityChunkBeginEnd");

        using var stringWriter = new StringWriter();
        using var indentedTextWriter = new IndentedTextWriter(stringWriter);

        indentedTextWriter.WriteLine("[global::System.Runtime.CompilerServices.CompilerGenerated]");
        indentedTextWriter.WriteLine($"partial struct {TypeName} : global::Unity.Entities.IJobChunk");
        indentedTextWriter.WriteLine("{");
        indentedTextWriter.Indent++;

        indentedTextWriter.WriteLine("InternalCompilerQueryAndHandleData.TypeHandle __TypeHandle;");
        if (m_RequiresEntityManager)
            indentedTextWriter.WriteLine("public global::Unity.Entities.EntityManager __EntityManager;");
        if (m_HasEntityIndexInQuery)
            indentedTextWriter.WriteLine("[global::Unity.Collections.ReadOnly] public global::Unity.Collections.NativeArray<int> __ChunkBaseEntityIndices;");

        indentedTextWriter.WriteExecuteMethod(inheritsFromBeginEndChunk, hasEnableableComponent, _userExecuteMethodParams);

        if (inheritsFromBeginEndChunk)
        {
            indentedTextWriter.WriteLine("OnChunkEnd(in chunk, chunkIndexInQuery, useEnabledMask, in chunkEnabledMask, shouldExecuteChunk);");
            indentedTextWriter.Indent--;
            indentedTextWriter.WriteLine("}");
        }

        indentedTextWriter.WriteScheduleAndRunMethods();
        QueriesAndHandles.WriteInternalCompilerQueryAndHandleDataStruct(
            indentedTextWriter,
            shouldHaveUpdate: true,
            fieldsArePublic: true,
            writeAdditionalSyntaxInInternalCompilerQueryAndHandleData: WriteAdditionalSyntax,
            _queriesAndHandles);

        indentedTextWriter.WriteInternalCompilerStruct(m_RequiresEntityManager);

        indentedTextWriter.Indent--;
        indentedTextWriter.WriteLine("}");
        return stringWriter.ToString();
    }

    void WriteAdditionalSyntax(IndentedTextWriter writer)
    {
        const string jobExtensions = "global::Unity.Entities.JobChunkExtensions";
        const string internalJobExtensions = "global::Unity.Entities.Internal.InternalCompilerInterface.JobChunkInterface";

        // Generate all methods required for throwing a runtime exception if users schedule/run jobs with incompatible queries.
        if (m_CheckUserDefinedQueryForScheduling)
        {
            writer.WriteLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            writer.WriteLine($"public static int GetRequiredComponentTypeCount() => {m_ComponentTypesInExecuteMethod.Count}{m_AspectTypesInExecuteMethod.Select(a => $" + {a.TypeSymbol.ToFullName()}.GetRequiredComponentTypeCount()").SeparateBy("")};");

            writer.WriteLine();
            writer.WriteLine("public static void AddRequiredComponentTypes(ref global::System.Span<Unity.Entities.ComponentType> components)");
            writer.WriteLine("{");
            writer.Indent++;
            for (var index = 0; index < m_ComponentTypesInExecuteMethod.Count; index++)
                writer.WriteLine($"components[{index}] = {m_ComponentTypesInExecuteMethod[index].ToString()};");
            if (m_AspectTypesInExecuteMethod.Count > 0)
            {
                writer.WriteLine($"int startAddIndex = {m_ComponentTypesInExecuteMethod.Count};");

                for (var index = 0; index < m_AspectTypesInExecuteMethod.Count; index++)
                {
                    var aspect = m_AspectTypesInExecuteMethod[index];
                    var aspectFullName = aspect.TypeSymbol.ToFullName();

                    writer.WriteLine($"int aspect{index}ComponentTypeCount = {aspectFullName}.GetRequiredComponentTypeCount();");
                    writer.WriteLine($"global::System.Span<global::Unity.Entities.ComponentType> aspect{index}Components = stackalloc global::Unity.Entities.ComponentType[aspect{index}ComponentTypeCount];");
                    writer.WriteLine($"{aspectFullName}.AddRequiredComponentTypes(ref aspect{index}Components);");
                    writer.WriteLine($@"for (int i = 0; i < aspect{index}ComponentTypeCount; i++)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine($"components[startAddIndex + i] = aspect{index}Components[i];");
                    writer.Indent--;
                    writer.WriteLine("}");

                    if (index < m_AspectTypesInExecuteMethod.Count - 1)
                        writer.WriteLine( $"startAddIndex += aspect{index}ComponentTypeCount;");
                }
            }
            writer.Indent--;
            writer.WriteLine("}");

            writer.WriteLine();
            writer.WriteLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            writer.WriteLine("public static bool QueryHasRequiredComponentsForExecuteMethodToRun(ref EntityQuery userDefinedQuery, ref global::System.Span<global::Unity.Entities.ComponentType> components) =>");
            writer.Indent++;
            writer.WriteLine("global::Unity.Entities.Internal.InternalCompilerInterface.EntityQueryInterface.HasComponentsRequiredForExecuteMethodToRun(ref userDefinedQuery, ref components);");
            writer.Indent--;
        }

        writer.WriteLine();
        writer.WriteLine("public void Init(ref global::Unity.Entities.SystemState state, bool assignDefaultQuery)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("if (assignDefaultQuery)");
        writer.Indent++;
        writer.WriteLine("__AssignQueries(ref state);");
        writer.Indent--;
        writer.WriteLine("__TypeHandle.__AssignHandles(ref state);");
        writer.Indent--;
        writer.WriteLine("}");

        writer.WriteLine();
        writer.WriteLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        writer.WriteLine($"public void Run(ref {FullTypeName} job, global::Unity.Entities.EntityQuery query)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("job.__TypeHandle = __TypeHandle;");
        writer.WriteLine($"{(m_RequiresEntityManager ? internalJobExtensions : jobExtensions)}.{(m_RequiresEntityManager ? "RunByRefWithoutJobs(ref job, query)" : "RunByRef(ref job, query)")};");
        writer.Indent--;
        writer.WriteLine("}");

        writer.WriteLine();
        writer.WriteLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        writer.WriteLine($"public global::Unity.Jobs.JobHandle Schedule(ref {FullTypeName} job, global::Unity.Entities.EntityQuery query, global::Unity.Jobs.JobHandle dependency)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("job.__TypeHandle = __TypeHandle;");
        writer.WriteLine("return global::Unity.Entities.JobChunkExtensions.ScheduleByRef(ref job, query, dependency);");
        writer.Indent--;
        writer.WriteLine("}");

        writer.WriteLine();
        writer.WriteLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        writer.WriteLine($"public global::Unity.Jobs.JobHandle ScheduleParallel(ref {FullTypeName} job, global::Unity.Entities.EntityQuery query, global::Unity.Jobs.JobHandle dependency)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("job.__TypeHandle = __TypeHandle;");
        writer.WriteLine($"return {(m_HasEntityIndexInQuery ? internalJobExtensions : jobExtensions)}.ScheduleParallelByRef(ref job, query, dependency{(m_HasEntityIndexInQuery?", job.__ChunkBaseEntityIndices" : string.Empty)});");
        writer.Indent--;
        writer.WriteLine("}");

        writer.WriteLine();
        writer.WriteLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        writer.WriteLine($"public void UpdateBaseEntityIndexArray(ref {FullTypeName} job, global::Unity.Entities.EntityQuery query, ref global::Unity.Entities.SystemState state)");
        writer.WriteLine("{");
        if (m_HasEntityIndexInQuery)
        {
            writer.Indent++;
            writer.WriteLine( "var baseEntityIndexArray = query.CalculateBaseEntityIndexArray(state.WorldUpdateAllocator);");
            writer.WriteLine( "job.__ChunkBaseEntityIndices = baseEntityIndexArray;");
            writer.Indent--;
        }
        writer.WriteLine("}");

        writer.WriteLine();
        writer.WriteLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        writer.WriteLine($"public global::Unity.Jobs.JobHandle UpdateBaseEntityIndexArray(ref {FullTypeName} job, global::Unity.Entities.EntityQuery query, global::Unity.Jobs.JobHandle dependency, ref global::Unity.Entities.SystemState state)");
        writer.WriteLine("{");
        writer.Indent++;
        if (m_HasEntityIndexInQuery)
        {
            writer.WriteLine( "var baseEntityIndexArray = query.CalculateBaseEntityIndexArrayAsync(state.WorldUpdateAllocator, dependency, out var indexDependency);");
            writer.WriteLine( "job.__ChunkBaseEntityIndices = baseEntityIndexArray;");
            writer.WriteLine( "return indexDependency;");
        }
        else
            writer.WriteLine("return dependency;");
        writer.Indent--;
        writer.WriteLine("}");

        writer.WriteLine();
        writer.WriteLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        writer.WriteLine($"public void AssignEntityManager(ref {FullTypeName} job, global::Unity.Entities.EntityManager entityManager)");
        writer.WriteLine("{");
        if (m_RequiresEntityManager)
        {
            writer.Indent++;
            writer.WriteLine("job.__EntityManager = entityManager;");
            writer.Indent--;
        }
        writer.WriteLine("}");
    }
}
