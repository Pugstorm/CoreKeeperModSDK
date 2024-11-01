using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS =
    Unity.Entities.SourceGenerators.Test.CSharpSourceGeneratorVerifier<
        Unity.Entities.SourceGen.SystemGenerator.SystemGenerator>;

namespace Unity.Entities.SourceGenerators;

[TestClass]
public class ForEachNoErrorTests
{
    [TestMethod]
    public async Task ForEachIteration_InSystemBase()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public partial class SomeSystem : SystemBase {
                protected override void OnUpdate() {
                    foreach (var aspect in SystemAPI.Query<EcsTestAspect>()) {}
                }
            }";
        await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task DifferentAssemblies_Aspect()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public partial struct SomeSystem : ISystem {
                public void OnUpdate(ref SystemState state) {
                    foreach (var aspect in SystemAPI.Query<EcsTestAspect>()){}
                }
            }";
        await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task DifferentAssemblies_ComponentDataRef()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public partial struct SomeSystem : ISystem {
                public void OnUpdate(ref SystemState state) {
                    foreach (var data in SystemAPI.Query<RefRW<EcsTestData>>()){}
                }
            }";
        await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task DifferentAssemblies_Combined()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public partial struct SomeSystem : ISystem {
                public void OnUpdate(ref SystemState state) {
                    foreach (var (aspect, data) in SystemAPI.Query<EcsTestAspect, RefRW<EcsTestData>>()){}
                }
            }";
        await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task SystemBasePartialTypes_IdiomaticForEach()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public partial class UserWrittenPartial : SystemBase {
                protected override void OnUpdate() {
                    foreach (var (data, data1) in SystemAPI.Query<RefRO<EcsTestData>, RefRO<EcsTestData2>>())
                    {
                        OnUpdate(unusedParameter: true);
                    }
                }
            }

            public partial class UserWrittenPartial : SystemBase {
                protected void OnUpdate(bool unusedParameter) {
                    foreach (var (data, data1) in SystemAPI.Query<RefRO<EcsTestData>, RefRO<EcsTestData2>>())
                    {
                    }
                }
            }";
        await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task ISystemPartialTypes_IdiomaticForEach()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public partial struct UserWrittenPartial : ISystem {
                public void OnUpdate(ref SystemState state) {
                    foreach (var (data, data1) in SystemAPI.Query<RefRO<EcsTestData>, RefRO<EcsTestData2>>())
                    {
                        OnUpdate(unusedParameter: true, ref state);
                    }
                }
            }

            public partial struct UserWrittenPartial : ISystem {
                public void OnUpdate(bool unusedParameter, ref SystemState state) {
                    foreach (var (data, data1) in SystemAPI.Query<RefRO<EcsTestData>, RefRO<EcsTestData2>>())
                    {
                    }
                }
            }";
        await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task IdiomaticForEach_WithErrorTypeParam()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            using Namespace1;
            using Namespace2;

            namespace Namespace1
            {
                public struct Data { }
            }

            namespace Namespace2
            {
                public struct Data { }
            }

            public partial struct WithErrorTypeParam : ISystem
            {
                public void OnUpdate(ref SystemState state)
                {
                    foreach (var data in SystemAPI.Query<{|#0:Data|}>()) {}
                }
            }";

        // We are actually expecting an ambiguous type error, but NOT the SGICE we got before
        var diagnosticResult = DiagnosticResult.CompilerError("CS0104").WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, diagnosticResult);
    }

    [TestMethod]
    public async Task SGFE010_QueryingTypeWithNonGenericTypeArgument()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            partial class TranslationSystem<T> : SystemBase where T : unmanaged, IComponentData
            {
                protected override void OnUpdate()
                {
                    foreach (var component in SystemAPI.Query<{|#0:RefRO<GenericComponentData<int>>|}>())
                    {
                    }
                }
            }";
        await VerifyCS.VerifySourceGeneratorAsync(source);
    }
}
