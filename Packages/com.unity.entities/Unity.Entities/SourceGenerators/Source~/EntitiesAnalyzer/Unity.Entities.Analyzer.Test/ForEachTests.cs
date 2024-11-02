using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using VerifyCS = Unity.Entities.Analyzer.Test.CSharpCodeFixVerifier<
    Unity.Entities.Analyzer.ForEachAnalyzer,
    Unity.Entities.Analyzer.EntitiesCodeFixProvider>;

namespace Unity.Entities.Analyzer
{
    [TestClass]
    public class ForEachTests
    {
        [TestMethod]
        public async Task EA0011Test()
        {
            var test = @"
                using Unity.Entities;
                using Unity.Entities.Tests;
                partial class TestSystem : SystemBase
                {
                    protected override void OnUpdate()
                    {
                        {|#0:Entities.WithAll<EcsTestData>().WithNone<EcsTestData2>()|};
                    }
                }";
            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.k_Ea0011Descriptor).WithLocation(0);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task EA0012Test()
        {
            var test = @"
                using Unity.Entities;
                using Unity.Entities.Tests;
                partial class TestSystem : SystemBase
                {
                    protected override void OnUpdate()
                    {
                        {|#0:Entities.WithAll<EcsTestData>().ForEach(() => { })|};
                    }
                }";
            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.k_Ea0012Descriptor).WithLocation(0);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task EA0013Test()
        {
            var test = @"
                using Unity.Entities;
                using Unity.Entities.Tests;
                partial class TestSystem : SystemBase
                {
                    protected override void OnUpdate()
                    {
                        {|#0:Job.WithCode(() => { })|};
                    }
                }";
            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.k_Ea0013Descriptor).WithLocation(0);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task EA0014Test()
        {
            var test = @"
                using Unity.Entities;
                using Unity.Entities.Tests;
                partial class TestSystem : SystemBase
                {
                    protected override void OnUpdate()
                    {
                        var a = {|#0:Entities|};
                    }
                }";
            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.k_Ea0014Descriptor).WithLocation(0);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task EA0015Test()
        {
            var test = @"
                using Unity.Entities;
                using Unity.Entities.Tests;
                partial class TestSystem : SystemBase
                {
                    protected override void OnUpdate()
                    {
                        var a = {|#0:Job|};
                    }
                }";
            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.k_Ea0015Descriptor).WithLocation(0);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}
