using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Unity.Entities.Analyzer.Test;

namespace Unity.Entities.Analyzer;

[TestClass]
public class CSharpCompilerErrorCodeFixTests
{
    [TestMethod]
    public async Task CS1654_ModifyingValueTypeElementsNotClassifiedAsVariables()
    {
        const string faultyCode = @"
                using Unity.Entities;
                using Unity.Entities.Tests;
                using static Unity.Entities.SystemAPI;

                partial class TestSystem : SystemBase
                {
                    protected override void OnUpdate()
                    {
                        foreach (var (ecsTestData, db) in SystemAPI.Query<RefRO<EcsTestData>, DynamicBuffer<EcsIntElement>>())
                        {
                            {|#0:db[0]|} = new EcsIntElement { Value = 1 };
                        }
                    }
                }";

        const string fixedCode = @"
                using Unity.Entities;
                using Unity.Entities.Tests;
                using static Unity.Entities.SystemAPI;

                partial class TestSystem : SystemBase
                {
                    protected override void OnUpdate()
                    {
                        foreach (var (ecsTestData, db) in SystemAPI.Query<RefRO<EcsTestData>, DynamicBuffer<EcsIntElement>>())
                        {
                            var __newdb__ = db;
                            __newdb__[0] = new EcsIntElement { Value = 1 };
                        }
                    }
                }";

        await CSharpCodeFixVerifier<BlobAssetAnalyzer, EntitiesCodeFixProvider>.VerifyCodeFixAsync(
            faultyCode,
            DiagnosticResult.CompilerError("CS1654").WithLocation(0).WithArguments("db", "foreach iteration variable"),
            fixedCode);
    }

    [TestMethod]
    public async Task CS1654_ModifyingValueTypeElementsNotClassifiedAsVariables_EdgeCase()
    {
        const string faultyCode = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            using static Unity.Entities.SystemAPI;

            partial class TestSystem : SystemBase
            {
                protected override void OnUpdate()
                {
                    foreach (var (a, b) in SystemAPI.Query<DynamicBuffer<EcsIntElement>, DynamicBuffer<EcsFloatElement>>())
                    {
                        var test = (({|#0:a[0]|} = 5) + ({|#1:b[0]|} = 5.0f));
                    }
                }
            }";

        const string fixedCode = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            using static Unity.Entities.SystemAPI;

            partial class TestSystem : SystemBase
            {
                protected override void OnUpdate()
                {
                    foreach (var (a, b) in SystemAPI.Query<DynamicBuffer<EcsIntElement>, DynamicBuffer<EcsFloatElement>>())
                    {
                        var __newa__ = a;
                        var __newb__ = b;
                        var test = ((__newa__[0] = 5) + (__newb__[0] = 5.0f));
                    }
                }
            }";

        var expected1 = DiagnosticResult.CompilerError("CS1654").WithLocation(0).WithArguments("a", "foreach iteration variable");
        var expected2 = DiagnosticResult.CompilerError("CS1654").WithLocation(1).WithArguments("b", "foreach iteration variable");
        await CSharpCodeFixVerifier<BlobAssetAnalyzer, EntitiesCodeFixProvider>.VerifyCodeFixAsync(faultyCode, new []{expected1, expected2}, fixedCode, numIterationsExpected: 2);
    }
}
