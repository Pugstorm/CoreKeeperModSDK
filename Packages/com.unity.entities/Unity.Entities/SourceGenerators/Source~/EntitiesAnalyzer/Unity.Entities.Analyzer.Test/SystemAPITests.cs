using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using VerifyCS = Unity.Entities.Analyzer.Test.CSharpCodeFixVerifier<
    Unity.Entities.Analyzer.SystemAPIAnalyzer,
    Unity.Entities.Analyzer.EntitiesCodeFixProvider>;

namespace Unity.Entities.Analyzer
{
    [TestClass]
    public class SystemAPITests
    {
        public static IEnumerable<object[]> EFEAllowedAPIMethods() => SystemAPIMethods.EFEAllowedAPIMethods;

        [DataTestMethod]
        [DynamicData(nameof(EFEAllowedAPIMethods), DynamicDataSourceType.Method)]
        [DataRow("Time", @"var time = {|#0:SystemAPI.Time|}.DeltaTime")]
        public async Task AllowedSystemAPIUseInEFE_NoError(string memberName, string apiMethodInvocation)
        {
            var test = @$"
                using Unity.Entities;
                using Unity.Entities.Tests;
                using static Unity.Entities.SystemAPI;

                partial class TestSystem : SystemBase
                {{
                    protected override void OnUpdate()
                    {{
                        Entities
                            .ForEach((Entity entity) =>
                            {{
                                {apiMethodInvocation};
                            }})
                            .ScheduleParallel();
                    }}
                }}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [DataTestMethod]
        [DataRow("HasSingleton", @"{|#0:SystemAPI.HasSingleton<EcsTestData>()|}")]
        [DataRow("HasSingleton", @"{|#0:HasSingleton<EcsTestData>()|}")]
        [DataRow("Time", @"var time = {|#0:SystemAPI.Time|}.DeltaTime")]
        [DataRow("Time", @"var time = {|#0:Time|}.DeltaTime")]
        public async Task SystemAPIUseInNonSystemType_Error(string memberName, string apiMethodInvocation)
        {
            var test = @$"
                using Unity.Entities;
                using Unity.Entities.Tests;
                using static Unity.Entities.SystemAPI;

                partial class NonSystemType
                {{
                    protected void OnUpdate()
                    {{
                        {apiMethodInvocation};
                    }}
                }}";

            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0004).WithLocation(0).WithArguments(memberName);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [DataTestMethod]
        [DataRow("HasSingleton", @"{|#0:SystemAPI.HasSingleton<EcsTestData>()|}")]
        [DataRow("HasSingleton", @"{|#0:HasSingleton<EcsTestData>()|}")]
        // [DataRow("Time", @"var time = {|#0:Time|}.DeltaTime")] Invalid test as always find SystemBase.Time currently
        public async Task SystemAPIUseInEFE_Error(string memberName, string apiMethodInvocation)
        {
            var test = @$"
                using Unity.Entities;
                using Unity.Entities.Tests;
                using static Unity.Entities.SystemAPI;

                partial class TestSystem : SystemBase
                {{
                    protected override void OnUpdate()
                    {{
                        Entities
                            .ForEach((Entity entity) =>
                            {{
                                {apiMethodInvocation};
                            }})
                            .ScheduleParallel();
                    }}
                }}";

            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0005).WithLocation(0).WithArguments(memberName);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [DataTestMethod]
        [DataRow("HasSingleton", @"{|#0:SystemAPI.HasSingleton<EcsTestData>()|}")]
        [DataRow("HasSingleton", @"{|#0:HasSingleton<EcsTestData>()|}")]
        [DataRow("Time", @"var time = {|#0:SystemAPI.Time|}.DeltaTime")]
        [DataRow("Time", @"var time = {|#0:Time|}.DeltaTime")]
        public async Task SystemAPIUseInStaticMethod_Error(string memberName, string apiMethodInvocation)
        {
            var test = @$"
                using Unity.Entities;
                using Unity.Entities.Tests;
                using static Unity.Entities.SystemAPI;

                partial struct TestSystem : ISystem
                {{
                    static void StaticMethod()
                    {{
                        {apiMethodInvocation};
                    }}
                }}";

            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.ID_EA0006).WithLocation(0).WithArguments(memberName);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}
