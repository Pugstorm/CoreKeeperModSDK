using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using VerifyTypeAnalyzer = Unity.Entities.Analyzer.Test.CSharpCodeFixVerifier<
    Unity.Entities.Analyzer.TypeAnalyzer,
    Unity.Entities.Analyzer.EntitiesCodeFixProvider>;

using VerifySystemStateByRefAnalyzer = Unity.Entities.Analyzer.Test.CSharpCodeFixVerifier<
    Unity.Entities.Analyzer.SystemStateByRefAnalyzer,
    Unity.Entities.Analyzer.EntitiesCodeFixProvider>;

namespace Unity.Entities.Analyzer
{
    [TestClass]
    public class TypeTests
    {
        [TestMethod]
        public async Task SystemBase()
        {
            const string test = @"
                using Unity.Entities;
                class {|#0:TestSystem|} : SystemBase
                {
                    protected override void OnUpdate(){}
                }";
            const string fixedSource = @"
                using Unity.Entities;
                partial class TestSystem : SystemBase
                {
                    protected override void OnUpdate(){}
                }";
            var expected = VerifyTypeAnalyzer.Diagnostic(EntitiesDiagnostics.k_Ea0007Descriptor).WithLocation(0).WithArguments("SystemBase", "global::TestSystem");
            await VerifyTypeAnalyzer.VerifyCodeFixAsync(test, expected, fixedSource);
        }

        [TestMethod]
        public async Task ISystem()
        {
            const string test = @"
                using Unity.Entities;
                struct {|#0:TestSystem|} : ISystem{}";
            const string fixedSource = @"
                using Unity.Entities;
                partial struct TestSystem : ISystem{}";
            var expected = VerifyTypeAnalyzer.Diagnostic(EntitiesDiagnostics.k_Ea0007Descriptor).WithLocation(0).WithArguments("ISystem", "global::TestSystem");
            await VerifyTypeAnalyzer.VerifyCodeFixAsync(test, expected, fixedSource);
        }

        [TestMethod]
        public async Task Aspect()
        {
            var test = @"
                using Unity.Entities;
                struct {|#0:TestAspect|} : IAspect {}";
            var fixedSource = @"
                using Unity.Entities;
                partial struct TestAspect : IAspect {}";
            var expected = VerifyTypeAnalyzer.Diagnostic(EntitiesDiagnostics.k_Ea0007Descriptor).WithLocation(0).WithArguments("IAspect", "global::TestAspect");
            await VerifyTypeAnalyzer.VerifyCodeFixAsync(test, expected, fixedSource);
        }

        [TestMethod]
        public async Task AspectParent()
        {
            var test = @"
                using Unity.Entities;
                struct {|#0:A|} {
                    struct {|#1:B|} {
                        partial struct TestAspect : IAspect {}
                    }
                }";
            var fixedSource = @"
                using Unity.Entities;
                partial struct A {
                    partial struct B {
                        partial struct TestAspect : IAspect {}
                    }
                }";
            var expectedA = VerifyTypeAnalyzer.Diagnostic(EntitiesDiagnostics.k_Ea0008Descriptor).WithLocation(0).WithArguments("IAspect", "global::A.B.TestAspect", "global::A");
            var expectedB = VerifyTypeAnalyzer.Diagnostic(EntitiesDiagnostics.k_Ea0008Descriptor).WithLocation(1).WithArguments("IAspect", "global::A.B.TestAspect", "global::A.B");
            await VerifyTypeAnalyzer.VerifyCodeFixAsync(test, new[]{expectedA, expectedB}, fixedSource);
        }

        [TestMethod]
        public async Task JobEntity()
        {
            var test = @"
                using Unity.Entities;
                struct {|#0:TestJob|} : IJobEntity {}";
            var fixedSource = @"
                using Unity.Entities;
                partial struct TestJob : IJobEntity {}";
            var expected = VerifyTypeAnalyzer.Diagnostic(EntitiesDiagnostics.k_Ea0007Descriptor).WithLocation(0).WithArguments("IJobEntity", "global::TestJob");
            await VerifyTypeAnalyzer.VerifyCodeFixAsync(test, expected, fixedSource);
        }

        [TestMethod]
        public async Task JobEntityParent()
        {
            var test = @"
                using Unity.Entities;
                struct {|#0:A|} {
                    struct {|#1:B|} {
                        partial struct TestJob : IJobEntity {}
                    }
                }";
            var fixedSource = @"
                using Unity.Entities;
                partial struct A {
                    partial struct B {
                        partial struct TestJob : IJobEntity {}
                    }
                }";
            var expectedA = VerifyTypeAnalyzer.Diagnostic(EntitiesDiagnostics.k_Ea0008Descriptor).WithLocation(0).WithArguments("IJobEntity", "global::A.B.TestJob", "global::A");
            var expectedB = VerifyTypeAnalyzer.Diagnostic(EntitiesDiagnostics.k_Ea0008Descriptor).WithLocation(1).WithArguments("IJobEntity", "global::A.B.TestJob", "global::A.B");
            await VerifyTypeAnalyzer.VerifyCodeFixAsync(test,new[]{expectedA, expectedB}, fixedSource);
        }

        [TestMethod]
        public async Task DisableWarn()
        {
            var test = @"
                using Unity.Entities;
                #pragma warning disable EA0007
                struct TestJob : IJobEntity {}";
            await VerifyTypeAnalyzer.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task RefSystemState()
        {
            const string test = @"
                using Unity.Entities;
                class C
                {
                    void Foo({|#0:SystemState state|}) {}
                }";
            const string fixedSource = @"
                using Unity.Entities;
                class C
                {
                    void Foo(ref SystemState state) {}
                }";
            var expected = VerifyTypeAnalyzer.Diagnostic(EntitiesDiagnostics.k_Ea0016Descriptor).WithLocation(0).WithArguments("RefSystemState", "global::TestSystem");
            await VerifySystemStateByRefAnalyzer.VerifyCodeFixAsync(test, expected, fixedSource);
        }
    }
}