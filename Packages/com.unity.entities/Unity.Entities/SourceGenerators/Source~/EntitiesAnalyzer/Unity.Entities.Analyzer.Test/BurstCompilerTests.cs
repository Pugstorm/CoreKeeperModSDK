using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = Unity.Entities.Analyzer.Test.CSharpCodeFixVerifier<
    Unity.Entities.Analyzer.BurstCompilerAnalyzer,
    Unity.Entities.Analyzer.EntitiesCodeFixProvider>;

namespace Unity.Entities.Analyzer
{
    [TestClass]
    public class BurstCompilerTests
    {
        [TestMethod]
        public async Task MissingBurstCompileAttributeOnStaticClass()
        {
            const string test = @"
                using Unity.Burst;
                static class {|#0:ContainingType|}
                {
                    [BurstCompile]
                    public static void BurstMethod(){}
                }";
            const string fixedSource = @"
                using Unity.Burst;
                [BurstCompile]
                static class ContainingType
                {
                    [BurstCompile]
                    public static void BurstMethod(){}
                }";

            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.k_Ea0010Descriptor).WithLocation(0)
                .WithArguments("ContainingType", "BurstMethod");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedSource);
        }

        [TestMethod]
        public async Task MissingBurstCompileAttributeOnSecondContainingClass()
        {
            const string test = @"
                using Unity.Burst;
                static class {|#0:ContainingType2|}
                {
                    [BurstCompile]
                    static class ContainingType1
                    {
                        [BurstCompile]
                        public static void BurstMethod(){}
                    }
                }";
            const string fixedSource = @"
                using Unity.Burst;
                [BurstCompile]
                static class ContainingType2
                {
                    [BurstCompile]
                    static class ContainingType1
                    {
                        [BurstCompile]
                        public static void BurstMethod(){}
                    }
                }";

            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.k_Ea0010Descriptor).WithLocation(0)
                .WithArguments("ContainingType2", "BurstMethod");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedSource);
        }

        [TestMethod]
        public async Task MissingBurstCompileAttributeOnStruct()
        {
            const string test = @"
                using Unity.Burst;
                struct {|#0:ContainingType|}
                {
                    [BurstCompile]
                    public static void BurstMethod(){}
                }";
            const string fixedSource = @"
                using Unity.Burst;
                [BurstCompile]
                struct ContainingType
                {
                    [BurstCompile]
                    public static void BurstMethod(){}
                }";

            var expected = VerifyCS.Diagnostic(EntitiesDiagnostics.k_Ea0010Descriptor).WithLocation(0)
                .WithArguments("ContainingType", "BurstMethod");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedSource);
        }

        [TestMethod]
        public async Task BurstCompileAttributeOnSinglePartialNoError()
        {
            const string test = @"
                using Unity.Burst;
                partial struct ContainingType
                {
                    [BurstCompile]
                    public static void BurstMethod1(){}
                }

                [BurstCompile]
                partial struct ContainingType
                {
                    [BurstCompile]
                    public static void BurstMethod2(){}
                }";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MissingBurstCompileAttributeOnISystemNoError()
        {
            const string test = @"
                using Unity.Burst;
                using Unity.Entities;
                partial struct SomeSystem : ISystem
                {
                    [BurstCompile]
                    public void OnUpdate(ref SystemState state)
                    {}
                }";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
