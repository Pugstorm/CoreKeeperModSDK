#nullable enable
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Unity.Entities.SourceGen.JobEntityGenerator;

public partial class JobEntityModule
{
    internal readonly ref struct SchedulingExpressionCreateInfo
    {
        readonly string _schedulingMethodName;
        readonly ScheduleMode _scheduleMode;
        readonly string _jobArg;
        readonly string _queryToUse;
        readonly string? _userDefinedDependency;
        readonly ExpressionSyntax _systemStateExpression;
        readonly bool _hasUserDefinedQuery;

        public SchedulingExpressionCreateInfo(
            string schedulingMethodName,
            ScheduleMode scheduleMode,
            bool hasUserDefinedQuery,
            string jobArg,
            string queryToUse,
            string? userDefinedDependency,
            ExpressionSyntax systemStateExpression)
        {
            _schedulingMethodName = schedulingMethodName;
            _scheduleMode = scheduleMode;
            _jobArg = jobArg;
            _hasUserDefinedQuery = hasUserDefinedQuery;
            _queryToUse = queryToUse;
            _userDefinedDependency = userDefinedDependency;
            _systemStateExpression = systemStateExpression;
        }

        public string Write()
        {
            var stringBuilder = new StringBuilder();

            // Maybe prefix assignment: `systemState.Dependency = someSchedulingMethod(ref SomeJob, someQuery, someDependency, ref systemState)`
            var assignStateDependency = _userDefinedDependency == null & !_scheduleMode.IsRun();
            if (assignStateDependency)
                stringBuilder.Append($"{_systemStateExpression}.Dependency = ");

            string jobArg = $"{(_scheduleMode.IsByRef() ? $"ref {_jobArg}" : $"{_jobArg}")}";
            string queryArg = _queryToUse;
            string dependencyArg = $"{_userDefinedDependency ?? $"{_systemStateExpression}.Dependency"}";
            string systemStateArg = $"ref {_systemStateExpression}";
            string hasUserDefinedQueryArg = _hasUserDefinedQuery ? "true" : "false";

            stringBuilder.Append($"{_schedulingMethodName}({jobArg}, {queryArg}, {dependencyArg}, {systemStateArg}, {hasUserDefinedQueryArg})");
            return stringBuilder.ToString();
        }
    }
}
