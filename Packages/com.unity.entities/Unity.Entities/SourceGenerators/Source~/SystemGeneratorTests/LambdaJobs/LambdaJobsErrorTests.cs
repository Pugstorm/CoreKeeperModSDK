using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Unity.Entities.SourceGen.SystemGenerator.Common;
using Unity.Entities.SourceGen.SystemGenerator.LambdaJobs;
using VerifyCS =
    Unity.Entities.SourceGenerators.Test.CSharpSourceGeneratorVerifier<
        Unity.Entities.SourceGen.SystemGenerator.SystemGenerator>;

namespace Unity.Entities.SourceGenerators;

[TestClass]
public class LambdaJobsErrorTests
{
    [TestMethod]
    public async Task DC0085_CapturingInvalidIdentifier()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class SomeSystem : SystemBase {
                protected override void OnUpdate()
                {
                    var foo = {|#0:Foo|}();
                    Entities.ForEach((Entity e) =>
                    {|#1:{
                        var blah = foo;
                    }|}).Schedule();
                }
            }";
        var diagnosticResult1 = DiagnosticResult.CompilerError("CS0103").WithLocation(0);
        var diagnosticResult2 = DiagnosticResult.CompilerError(nameof(LambdaJobsErrors.DC0086)).WithLocation(1);
        await VerifyCS.VerifySourceGeneratorAsync(source, diagnosticResult1, diagnosticResult2);
    }

    [TestMethod]
    public async Task NestedSingletonInGenerated()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class SomeSystem : SystemBase {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.ForEach((Entity e) => {
                            SetComponent(e, GetSingleton<EcsTestData>());
                    }).Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0004)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0003_WithConflictingName()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public partial class WithConflictingName : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.WithName(""VeryCommonName"").ForEach((ref Translation t) => {}).Schedule();
                    Entities.WithName(""VeryCommonName"").ForEach((ref Translation t) => {}).Schedule();
                }
            }";
            var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0003)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0004_JobWithCodeCapturingFieldInSystem()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class JobWithCodeCapturingFieldInSystem_System : SystemBase
            {
                public int _someField;
                protected override void OnUpdate()
                {
                    {|#0:Job|}
                        .WithCode(() => { _someField = 123; })
                        .Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0004)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0004_WithGetComponentAndCaptureOfThisTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class WithGetComponentAndCaptureOfThis : SystemBase
            {
                float someField = 3.0f;

                protected override void OnUpdate()
                {
                    {|#0:Entities|}.ForEach((ref Translation translation) =>
                        {
                            var vel = GetComponent<EcsTestData>(default);
                            translation = new Translation() {Value = someField * vel.value};
                        }).Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0004)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0004_WithGetComponentAndCaptureOfThisAndVarTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class WithGetComponentAndCaptureOfThisAndVar : SystemBase
            {
                float someField = 3.0f;

                protected override void OnUpdate()
                {
                    float someVar = 2.0f;
                    {|#0:Entities|}.ForEach((ref Translation translation) =>
                        {
                            var vel = GetComponent<EcsTestData>(default);
                            translation = new Translation() {Value = someField * vel.value * someVar};
                        }).Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0004)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0004_CaptureFieldInNonLocalCapturingLambdaTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class CaptureFieldInNonLocalCapturingLambda : SystemBase
            {
                private int myfield = 123;

                protected override void OnUpdate()
                {
                    {|#0:Entities|}
                        .ForEach((ref Translation t) => { t.Value = myfield; })
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0004)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0004_CaptureFieldByRefTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class CaptureFieldByRef : SystemBase
            {
                int m_MyField = 123;

                protected override void OnUpdate()
                {
                    {|#0:Entities|}
                        .ForEach((ref Translation t) =>{ NotAProblem(ref m_MyField); })
                        .Schedule();
                }

                static void NotAProblem(ref int a) {}
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0004)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0004_InvokeInstanceMethodInCapturingLambdaTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class InvokeInstanceMethodInCapturingLambda : SystemBase
            {
                public object GetSomething(int i) => default;

                protected override void OnUpdate()
                {
                    int also_capture_local = 1;
                    {|#0:Entities|}
                        .ForEach((ref Translation t) => { GetSomething(also_capture_local); })
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0004)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0004_InvokeInstanceMethodInNonCapturingLambdaTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class InvokeInstanceMethodInNonCapturingLambda : SystemBase
            {
                public object GetSomething(int i) => default;

                protected override void OnUpdate()
                {
                    {|#0:Entities|}
                        .ForEach((ref Translation t) => { GetSomething(3); })
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0004)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }


    [TestMethod]
    public async Task DC0004_CallsMethodInComponentSystemBaseTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class CallsMethodInComponentSystemBase : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}
                        .ForEach((ref Translation t) => { var targetDistance = Time.DeltaTime; })
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0004)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0004_WithCapturedReferenceType()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public partial class WithCapturedReferenceType : SystemBase
            {
                class CapturedClass
                {
                    public float value;
                }

                protected override void OnUpdate()
                {
                    var capturedClass = new CapturedClass() {value = 3.0f};
                    {|#0:Entities|}
                        .ForEach((ref Translation t) => { t.Value = capturedClass.value; })
                        .Schedule();
                }
            }";
            var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0004)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod] // This should use DC0001 but we don't have a good way to do that with source generators yet
    public async Task DC0004_CaptureFieldInLocalCapturingLambdaTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public partial class CaptureFieldInLocalCapturingLambda : SystemBase
            {
                private int field = 123;

                protected override void OnUpdate()
                {
                    int also_capture_local = 1;
                    {|#0:Entities|}
                        .ForEach((ref Translation t) => { t.Value = field + also_capture_local; })
                        .Schedule();
                }
            }";
            var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0004)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0004_InvokeBaseMethodInBurstLambdaTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public partial class InvokeBaseMethodInBurstLambdaTest : SystemBase
            {
                protected override void OnUpdate()
                {
                    int version = 0;
                    {|#0:Entities|}.ForEach((ref Translation t) => { version = EntityManager.EntityOrderVersion; }).Run();
                }
            }";
            var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0004)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0004_InvokeThisMethodInForEach()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class Test : SystemBase
            {
                public void SomeMethod(){}

                protected override void OnUpdate()
                {
                    {|#0:Entities|}.ForEach((in Translation t) => SomeMethod()).Run();
                }
            }";
            var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0004)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0004_InvokeThisMethodInWithCode()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class Test : SystemBase
            {
                public void SomeMethod(){}

                protected override void OnUpdate()
                {
                    {|#0:Job|}.WithCode(() => SomeMethod()).Run();
                }
            }";
            var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0004)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0004_InvokeThisPropertyInForEach()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class Test : SystemBase
            {
                public bool SomeProp{get;set;}

                protected override void OnUpdate()
                {
                    {|#0:Entities|}.ForEach((ref Translation t) =>
                    {
                        var val = !SomeProp;
                    }).Run();
                }
            }";
            var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0004)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0004_InvokeThisFieldInWithCode()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class Test : SystemBase
            {
                public bool SomeProp{get;set;}

                protected override void OnUpdate()
                {
                    {|#0:Job|}.WithCode(() =>
                    {
                        var val = !SomeProp;
                    }).Run();
                }
            }";
            var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0004)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0004_InvokeThisFieldInForEach()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class Test : SystemBase
            {
                public bool SomeField = false;

                protected override void OnUpdate()
                {
                    {|#0:Entities|}.ForEach((ref Translation t) =>
                    {
                        var val = !SomeField;
                    }).Run();
                }
            }";
            var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0004)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0004_InvokeThisPropertyInWithCode()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class Test : SystemBase
            {
                public bool SomeField = false;

                protected override void OnUpdate()
                {
                    {|#0:Job|}.WithCode(() =>
                    {
                        var val = !SomeField;
                    }).Run();
                }
            }";
            var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0004)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0005_WithUnsupportedParameter()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public partial class WithUnsupportedParameter : SystemBase
            {
                protected override void OnUpdate()
                {
                    Entities
                        .ForEach(({|#0:string whoKnowsWhatThisMeans|}, in Translation translation) => {})
                        .Schedule();
                }
            }";
            var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0005)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0008_WithBurstWithNonLiteral()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class WithBurstWithNonLiteral : SystemBase
            {
                protected override void OnUpdate()
                {
                    var floatMode = Unity.Burst.FloatMode.Deterministic;
                    {|#0:Entities
                        .WithBurst(floatMode)|}
                        .ForEach((in Translation translation) => {})
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0008)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0009_UsingConstructionMultipleTimes()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class UsingConstructionMultipleTimes : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}
                        .WithName(""Cannot"")
                        .WithName(""Make up my mind"")
                        .ForEach((in Translation translation) => {})
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0009)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0010_ControlFlowInsideWithChainTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class ControlFlowInsideWithChainSystem : SystemBase
            {
                protected override void OnUpdate()
                {
                    var maybe = false;
                    {|#0:Entities
                        .WithName(maybe ? ""One"" : ""Two"")|}
                        .ForEach(
                            (ref Translation translation, in EcsTestData data) =>
                            {
                                translation.Value += data.value;
                            })
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0010)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0012_WithReadOnly_IllegalArgument_Test()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class WithReadOnly_IllegalArgument : SystemBase
            {
                protected override void OnUpdate()
                {
                    Entities
                        .WithReadOnly({|#0:""stringLiteral""|})
                        .ForEach((in Translation translation) => {})
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0012)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0035_WithReadOnly_NonCapturedVariable_Test()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            using Unity.Collections;
            partial class WithReadOnly_NonCapturedVariable : SystemBase
            {
                protected override void OnUpdate()
                {
                    var myNativeArray = new NativeArray<float>();

                    Entities
                        .WithReadOnly({|#0:myNativeArray|})
                        .ForEach((in Translation translation) => {})
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0035)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0012_WithDisposeOnCompletion_WithRun_NonCapturedVariable_Test()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            using Unity.Collections;
            partial class WithDisposeOnCompletion_WithRun_NonCapturedVariable : SystemBase
            {
                protected override void OnUpdate()
                {
                    var myNativeArray = new NativeArray<float>();

                    Entities
                        .WithDisposeOnCompletion({|#0:myNativeArray|})
                        .ForEach((in Translation translation) => {})
                        .Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0012)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0013_LambdaThatWritesBackToCapturedLocalTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class LambdaThatWritesBackToCapturedLocal : SystemBase
            {
                protected override void OnUpdate()
                {
                    int capture_me = 123;
                    {|#0:Entities|}.ForEach((ref Translation t) => { capture_me++; }).Schedule();
                    var test = capture_me;
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0013)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0014_UseOfUnsupportedIntParamInLambda()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class UseOfUnsupportedIntParamInLambda : SystemBase
            {
                protected override void OnUpdate()
                {
                    Entities.ForEach(({|#0:int NotAValidParam|}, ref Translation t) => { }).Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0014)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0223_UseSharedComponentDataUsingSchedule()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class SharedComponentDataUsingSchedule : SystemBase
            {
                struct MySharedComponentData : ISharedComponentData {}

                protected override void OnUpdate()
                {
                    {|#0:Entities|}
                        .ForEach((MySharedComponentData mydata) => {})
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0223)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0020_SharedComponentDataReceivedByRef()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class SharedComponentDataReceivedByRef : SystemBase
            {
                struct MySharedComponentData : ISharedComponentData {}

                protected override void OnUpdate()
                {
                    {|#0:Entities|}
                        .WithoutBurst()
                        .ForEach((ref MySharedComponentData mydata) => {})
                        .Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0020)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0021_CustomStructArgumentThatDoesntImplementSupportedInterfaceTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class CustomStructArgumentThatDoesntImplementSupportedInterface : SystemBase
            {
                struct ForgotToAddInterface {}

                protected override void OnUpdate()
                {
                    Entities
                        .ForEach(({|#0:ref ForgotToAddInterface t|}) => {})
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0021)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0223_ManagedComponentInBurstJobTest()
    {
        const string source = @"
            using System;
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class ManagedComponentInBurstJobTest : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.ForEach((ManagedComponent t) => {}).Run();
                }
            }
            class ManagedComponent : IComponentData, IEquatable<ManagedComponent>
            {
                public bool Equals(ManagedComponent other) => false;
                public override bool Equals(object obj) => false;
                public override int GetHashCode() =>  0;
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0223)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0223_ManagedComponentInSchedule()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class ManagedComponentInSchedule : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.ForEach((ManagedComponent t) => {}).Schedule();
                }
            }
            class ManagedComponent : IComponentData, System.IEquatable<ManagedComponent>
            {
                public bool Equals(ManagedComponent other) => false;
                public override bool Equals(object obj) => false;
                public override int GetHashCode() =>  0;
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0223)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0024_ManagedComponentByReference()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class ManagedComponentByReference : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.WithoutBurst().ForEach((ref ManagedComponent t) => {}).Run();
                }
            }
            class ManagedComponent : IComponentData, System.IEquatable<ManagedComponent>
            {
                public bool Equals(ManagedComponent other) => false;
                public override bool Equals(object obj) => false;
                public override int GetHashCode() =>  0;
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0024)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0025_SystemWithDefinedOnCreateForCompiler()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class SystemWithDefinedOnCreateForCompiler : SystemBase
            {
                protected override void OnCreateForCompiler() {}

                protected override void OnUpdate() {
                    {|#0:Entities|}.ForEach((in Translation translation) => {}).Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0025)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGQC002_WithAllWithSharedFilterTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class WithAllWithSharedFilter : SystemBase
            {
                struct MySharedComponentData : ISharedComponentData { public int Value; }

                protected override void OnUpdate()
                {
                    {|#0:Entities|}
                        .WithAll<MySharedComponentData>()
                        .WithSharedComponentFilter(new MySharedComponentData() { Value = 3 })
                        .ForEach((in Translation translation) => {})
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(QueryConstructionErrors.SGQC002)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGQC003_WithNoneWithSharedFilterTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class WithAllWithSharedFilter : SystemBase
            {
                struct MySharedComponentData : ISharedComponentData { public int Value; }

                protected override void OnUpdate()
                {
                    {|#0:Entities|}
                        .WithAny<MySharedComponentData>()
                        .WithSharedComponentFilter(new MySharedComponentData() { Value = 3 })
                        .ForEach((in Translation translation) => {})
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(QueryConstructionErrors.SGQC003)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0027_LambdaThatMakesNonExplicitStructuralChangesTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class LambdaThatMakesNonExplicitStructuralChanges : SystemBase
            {
                protected override void OnUpdate()
                {
                    Entities
                        .WithoutBurst()
                        .ForEach((Entity entity, ref Translation t) =>
                        {
                            {|#0:EntityManager.RemoveComponent<Translation>(entity)|};
                        }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0027)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0027_LambdaThatMakesNonExplicitStructuralChangesTestFromCapturedEntityManager()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class LambdaThatMakesNonExplicitStructuralChangesTestFromCapturedEntityManager : SystemBase
            {
                protected override void OnUpdate()
                {
                    var em = EntityManager;
                    Entities
                        .WithoutBurst()
                        .ForEach((ref Translation t) =>
                        {
                            {|#0:em.CreateEntity()|};
                        }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0027)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }


    [TestMethod]
    public async Task DC0027_LambdaThatMakesStructuralChangesWithScheduleTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class LambdaThatMakesStructuralChangesWithSchedule : SystemBase
            {
                protected override void OnUpdate()
                {
                    float delta = 0.0f;
                    Entities.WithoutBurst()
                        .WithStructuralChanges()
                        .ForEach((Entity entity, ref Translation t) =>
                        {
                            float blah = delta + 1.0f;
                            {|#0:EntityManager.RemoveComponent<Translation>(entity)|};
                        }).Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0027)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0029_LambdaThatHasNestedLambdaTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class LambdaThatHasNestedLambda : SystemBase
            {
                protected override void OnUpdate()
                {
                    float delta = 0.0f;
                    Entities
                        .WithoutBurst()
                        .ForEach((Entity e1, ref Translation t1) =>
                        {
                            {|#0:Entities
                                .WithoutBurst()
                                .ForEach((Entity e2, ref Translation t2) => { delta += 1.0f; })|}.Run();
                        }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0029)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0031_LambdaThatTriesToStoreNonValidEntityQueryVariableTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class LambdaThatTriesToStoreNonValidEntityQueryVariable : SystemBase
            {
                partial class EntityQueryHolder
                {
                    public EntityQuery m_Query;
                }

                protected override void OnUpdate()
                {
                    EntityQueryHolder entityQueryHolder = new EntityQueryHolder();

                    float delta = 0.0f;
                    {|#0:Entities|}
                        .WithStoreEntityQueryInField(ref entityQueryHolder.m_Query)
                        .ForEach((Entity e2, ref Translation t2) => { delta += 1.0f; }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0031)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0031_LambdaThatTriesToStoreLocalEntityQueryVariableTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class LambdaThatTriesToStoreLocalEntityQueryVariable : SystemBase
            {
                protected override void OnUpdate()
                {
                    EntityQuery query = default;

                    float delta = 0.0f;
                    {|#0:Entities|}
                        .WithStoreEntityQueryInField(ref query)
                        .ForEach((Entity e2, ref Translation t2) => { delta += 1.0f; }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0031)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0033_IncorrectUsageOfBufferIsDetected()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public struct MyBufferFloat : IBufferElementData
            {
                public float Value;
            }
            partial class IncorrectUsageOfBuffer : SystemBase
            {
                protected override void OnUpdate()
                {
                    Entities
                        .ForEach(({|#0:MyBufferFloat f|}) => {})
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0033)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0034_ReadOnlyWarnsAboutArgumentType_IncorrectReadOnlyUsageWithStruct()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class IncorrectReadOnlyUsageWithStruct : SystemBase
            {
                struct StructWithPrimitiveType { public int field; }
                protected override void OnUpdate()
                {
                    StructWithPrimitiveType {|#0:structWithPrimitiveType|} = default;
                    structWithPrimitiveType.field = default;
                    Entities
                        .WithReadOnly(structWithPrimitiveType)
                        .ForEach((ref Translation t) => { t.Value += structWithPrimitiveType.field; })
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0034)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0034_ReadOnlyWarnsAboutArgumentType_IncorrectReadOnlyUsageWithPrimitiveType()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class IncorrectReadOnlyUsageWithPrimitiveType : SystemBase
            {
                protected override void OnUpdate()
                {
                    var {|#0:myVar|} = 0;
                    Entities
                        .WithReadOnly(myVar)
                        .ForEach((ref Translation t) => { t.Value += myVar; })
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0034)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0036_DisableContainerSafetyRestrictionWarnsAboutArgumentType_IncorrectDisableContainerSafetyRestrictionUsageWithStruct()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class IncorrectDisableContainerSafetyRestrictionUsageWithStruct : SystemBase
            {
                struct StructWithPrimitiveType { public int field; }
                protected override void OnUpdate()
                {
                    StructWithPrimitiveType {|#0:structWithPrimitiveType|} = default;
                    structWithPrimitiveType.field = default;
                    Entities
                        .WithNativeDisableContainerSafetyRestriction(structWithPrimitiveType)
                        .ForEach((ref Translation t) =>
                        {
                            t.Value += structWithPrimitiveType.field;
                        }).Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0034)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0036_DisableContainerSafetyRestrictionWarnsAboutArgumentType_IncorrectDisableContainerSafetyRestrictionUsageWithPrimitiveType()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class IncorrectDisableContainerSafetyRestrictionUsageWithPrimitiveType : SystemBase
            {
                protected override void OnUpdate()
                {
                    var {|#0:myVar|} = 0;
                    Entities
                        .WithNativeDisableContainerSafetyRestriction(myVar)
                        .ForEach((ref Translation t) => { t.Value += myVar; })
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0034)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0037_DisableParallelForRestrictionWarnsAboutArgumentType_IncorrectDisableParallelForRestrictionUsageWithStruct()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class IncorrectDisableParallelForRestrictionUsageWithStruct : SystemBase
            {
                struct StructWithPrimitiveType { public int field; }
                protected override void OnUpdate()
                {
                    StructWithPrimitiveType {|#0:structWithPrimitiveType|} = default;
                    structWithPrimitiveType.field = default;
                    Entities
                        .WithNativeDisableParallelForRestriction(structWithPrimitiveType)
                        .ForEach((ref Translation t) => { t.Value += structWithPrimitiveType.field; }).Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0034)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0037_DisableParallelForRestrictionWarnsAboutArgumentType_IncorrectDisableParallelForRestrictionUsageWithPrimitiveType()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class IncorrectDisableParallelForRestrictionUsageWithPrimitiveType : SystemBase
            {
                protected override void OnUpdate()
                {
                    var {|#0:myVar|} = 0;
                    Entities
                        .WithNativeDisableParallelForRestriction(myVar)
                        .ForEach((ref Translation t) => { t.Value += myVar; }).Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0034)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0043_InvalidJobNamesThrow_InvalidJobNameWithSpaces()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class InvalidJobNameWithSpaces : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}
                    .WithName(""This name may not contain spaces"")
                    .ForEach(
                        (ref Translation translation, in EcsTestData data) =>
                        {
                            translation.Value += data.value;
                        })
                    .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0043)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0043_InvalidJobNamesThrow_InvalidJobNameStartsWithDigit()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class InvalidJobNameStartsWithDigit : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}
                    .WithName(""1job"")
                    .ForEach(
                        (ref Translation translation, in EcsTestData data) =>
                        {
                            translation.Value += data.value;
                        })
                    .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0043)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0043_InvalidJobNamesThrow_InvalidJobNameCompilerReservedName()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class InvalidJobNameCompilerReservedName : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}
                    .WithName(""__job"")
                    .ForEach(
                        (ref Translation translation, in EcsTestData data) =>
                        {
                            translation.Value += data.value;
                        })
                    .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0043)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0044_WithLambdaStoredInFieldTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class WithLambdaStoredInFieldSystem : SystemBase
            {
                Unity.Entities.UniversalDelegates.R<Translation> _translationAction;

                protected override void OnUpdate()
                {
                    _translationAction = (ref Translation t) => {};
                    {|#0:Entities|}.ForEach(_translationAction).Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0044)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0044_WithLambdaStoredInVariableTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class WithLambdaStoredInVariableSystem : SystemBase
            {
                protected override void OnUpdate()
                {
                    Unity.Entities.UniversalDelegates.R<Translation> translationAction = (ref Translation t) => {};
                    {|#0:Entities|}.ForEach(translationAction).Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0044)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0044_WithLambdaStoredInArgTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class WithLambdaStoredInArgSystem : SystemBase
            {
                void Test(Unity.Entities.UniversalDelegates.R<Translation> action)
                {
                    {|#0:Entities|}.ForEach(action).Schedule();
                }

                protected override void OnUpdate()
                {
                    Test((ref Translation t) => {});
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0044)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0044_WithLambdaReturnedFromMethodTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class WithLambdaReturnedFromMethodSystem : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.ForEach(GetAction()).Schedule();
                }

                static Unity.Entities.UniversalDelegates.R<Translation> GetAction()
                {
                    return (ref Translation t) => {};
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0044)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0046_SetComponentWithNotPermittedComponentAccessThatAliasesTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class SetComponentWithNotPermittedComponentAccessThatAliases : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.ForEach((Entity e, in Translation data) => {
                        SetComponent(e, new Translation());
                    }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0046)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0047_SetComponentWithNotPermittedParameterThatAliasesTestTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class SetComponentWithNotPermittedParameterThatAliasesTest : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.ForEach((Entity e, ref Translation data) => {
                        var translation = GetComponent<Translation>(e);
                    }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0047)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0050_ParameterWithInvalidGenericParameterTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class SomeClass<TValue> where TValue : struct
            {
                partial class ParameterWithInvalidGenericParameter : SystemBase
                {
                    protected override void OnUpdate() {}

                    void Test()
                    {
                        Entities
                            .ForEach(({|#0:in TValue generic|}) => {})
                            .Schedule();
                    }
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0050)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0050_ParameterWithInvalidGenericTypeTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class ParameterWithInvalidGenericType : SystemBase
            {
                struct GenericType<TValue> : IComponentData {}

                protected override void OnUpdate()
                {
                    Entities
                        .ForEach(({|#0:in GenericType<int> generic|}) => {})
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0050)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0050_ParameterWithInvalidGenericDynamicBufferTypeTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class ParameterWithInvalidGenericDynamicBufferType : SystemBase
            {
                public struct GenericBufferType<T> : IBufferElementData where T : struct, IComponentData {}

                protected override void OnUpdate()
                {
                    {|#0:Entities|}
                        .ForEach((ref DynamicBuffer<GenericBufferType<EcsTestData>> buffer) => {})
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0050)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0051_WithNoneWithInvalidGenericParameterTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class SomeClass<TValue> where TValue : struct
            {
                partial class WithNoneWithInvalidGenericParameter : SystemBase
                {
                    protected override void OnUpdate() {}

                    void Test()
                    {
                        {|#0:Entities|}
                            .WithNone<TValue>()
                            .ForEach((in Translation translation) => {})
                            .Schedule();
                    }
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(SystemGeneratorErrors.DC0051)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0051_WithNoneWithInvalidGenericTypeTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class WithNoneWithInvalidGenericType : SystemBase
            {
                struct GenericType<TValue> : IComponentData {}
                protected override void OnUpdate()
                {
                    {|#0:Entities|}
                        .WithNone<GenericType<int>>()
                        .ForEach((in Translation translation) => {})
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(SystemGeneratorErrors.DC0051)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGQC001_WithNoneWithInvalidTypeTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class WithNoneWithInvalidType : SystemBase
            {
                class ANonIComponentDataClass
                {
                }
                protected override void OnUpdate()
                {
                    {|#0:Entities|}
                        .WithNone<ANonIComponentDataClass>()
                        .ForEach((in Translation translation) => {})
                        .Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(QueryConstructionErrors.SGQC001)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0053_InGenericSystemTypeTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class InGenericSystemType<T> : SystemBase where T : struct, IComponentData
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.ForEach((Entity e) => {}).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0053)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0054_InGenericMethodThatCapturesTest()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class InGenericMethodThatCapturesType : SystemBase
            {
                void Test<T>()
                {
                    var capture = 3.41f;
                    {|#0:Entities|}.ForEach((ref Translation translation) =>
                    {
                        translation.Value = capture;
                    }).Run();
                }

                protected override void OnUpdate()
                {
                    Test<int>();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0054)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0055_ComponentPassedByValueGeneratesWarning_Test()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class ComponentPassedByValueGeneratesWarning_System : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.ForEach((Translation translation) => { }).Run();
                }
            }";
        var expected = VerifyCS.CompilerWarning(nameof(LambdaJobsErrors.DC0055)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGQC004_InvalidWithNoneComponentGeneratesError_Test_WithNone_WithAll()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class InvalidWithNoneInWithAllComponentGeneratesError_System : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.WithNone<Translation>().WithAll<Translation>().ForEach((Entity entity) => { }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(QueryConstructionErrors.SGQC004)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGQC004_InvalidWithNoneComponentGeneratesError_Test_WithNone_WithAny()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class InvalidWithNoneInWithAnyComponentGeneratesError_System : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.WithNone<Translation>().WithAny<Translation>().ForEach((Entity entity) => { }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(QueryConstructionErrors.SGQC004)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGQC004_InvalidWithNoneComponentGeneratesError_Test_WithNone_LambdaParameter()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class InvalidWithNoneInLambdaParamComponentGeneratesError_System : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.WithNone<Translation>()
                        .ForEach((ref Translation translation) => { }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(QueryConstructionErrors.SGQC004)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGQC004_InvalidWithAnyComponentGeneratesError_Test_WithAny_WithAll()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class InvalidWithAnyInWithAllComponentGeneratesError_System : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.WithAny<Translation>().WithAll<Translation>().ForEach((Entity entity) => { }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(QueryConstructionErrors.SGQC004)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGQC004_InvalidWithAnyComponentGeneratesError_Test_WithAny_LambdaParameter()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class InvalidWithAnyInLambdaParamComponentGeneratesError_System : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.WithAny<Translation>().ForEach((ref Translation translation) => { }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(QueryConstructionErrors.SGQC004)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0057_JobWithCodeAndStructuralChanges_Test()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class JobWithCodeAndStructuralChanges_System : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Job|}.WithStructuralChanges().WithCode(() =>{ }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0057)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0059_GetComponentLookupWithMethodAsParam_ProducesError()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class GetComponentLookupWithMethodAsParam : SystemBase
            {
                static bool MethodThatReturnsBool() => false;
                protected override void OnUpdate()
                {
                    {|#0:Entities|}
                        .ForEach((Entity entity, in Translation tde) =>
                        {
                            GetComponentLookup<EcsTestData>(MethodThatReturnsBool());
                        }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0059)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0059_GetComponentLookupWithVarAsParam_ProducesError()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class GetComponentLookupWithVarAsParam : SystemBase
            {
                protected override void OnUpdate()
                {
                    var localBool = false;
                    {|#0:Entities|}.ForEach((Entity entity, in Translation tde) => { GetComponentLookup<EcsTestData>(localBool); }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0059)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0059_GetComponentLookupWithArgAsParam_ProducesError()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class GetComponentLookupWithArgAsParam : SystemBase
            {
                protected override void OnUpdate() {}
                void Test(bool argBool)
                {
                    {|#0:Entities|}.ForEach((Entity entity, in Translation tde) => { GetComponentLookup<EcsTestData>(argBool); }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0059)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0060_EntitiesForEachInAssemblyNotReferencingBurst()
    {
        const string source = @"
            {|#0:using Unity.Entities;
            using Unity.Entities.Tests;
            partial class EntitiesForEachInAssemblyNotReferencingBurst : SystemBase
            {
                protected override void OnUpdate()
                {
                    Entities.ForEach((Entity entity, in Translation tde) => { }).Run();
                }
            }|}";
        var expected = VerifyCS.CompilerError(nameof(SystemGeneratorErrors.DC0060)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected, typeof(EntitiesMock).Assembly);
    }

    [TestMethod]
    public async Task DC0061_EntitiesForEachInAssemblyNotReferencingCollections()
    {
        const string source = @"
            {|#0:using Unity.Entities;
            using Unity.Entities.Tests;
            partial class EntitiesForEachInAssemblyNotReferencingCollections : SystemBase
            {
                protected override void OnUpdate()
                {

                }
            }|}";
        var expected = VerifyCS.CompilerError(nameof(SystemGeneratorErrors.DC0061)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected, typeof(EntitiesMock).Assembly);
    }

    [TestMethod]
    public async Task DC0063_SetComponentInScheduleParallel()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class SetComponentInScheduleParallel : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.ForEach((Entity entity) =>
                    {
                        SetComponent(entity, new Translation());
                    }).ScheduleParallel();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(SystemGeneratorErrors.DC0063)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0063_GetBufferInScheduleParallel()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class GetBufferInScheduleParallel : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.ForEach((Entity entity) => {
                        var value = GetBuffer<EcsIntElement>(entity)[0].Value;
                    }).ScheduleParallel();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(SystemGeneratorErrors.DC0063)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0063_GetComponentLookupInScheduleParallel()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class GetComponentLookupInScheduleParallel : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.ForEach((Entity entity) => {
                        var lookup = GetComponentLookup<EcsTestData>(false);
                    }).ScheduleParallel();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(SystemGeneratorErrors.DC0063)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0070_EntitiesForEach_IllegalDuplicateTypesUsed()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class DuplicateIComponentDataTypes : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}.ForEach((in Translation translation1, in Translation translation2) => { }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0070)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }


    [TestMethod]
    public async Task DC0073_WithScheduleGranularity_Run()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class TestWithScheduleGranularity : SystemBase
            {

                protected override void OnUpdate()
                {
                    {|#0:Entities|}
                        .WithScheduleGranularity(ScheduleGranularity.Chunk)
                        .ForEach((ref Translation t) =>
                        {
                        }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0073)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }
    [TestMethod]
    public async Task DC0073_WithScheduleGranularity_Schedule()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class TestWithScheduleGranularity : SystemBase
            {
                protected override void OnUpdate()
                {
                    {|#0:Entities|}
                        .WithScheduleGranularity(ScheduleGranularity.Chunk)
                        .ForEach((ref Translation t) =>
                        {
                        }).Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0073)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0074_EcbParameter_MissingPlaybackInstructions()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class MissingPlaybackInstructions : SystemBase
            {
                protected override void OnUpdate()
                {
                    Entities
                        .ForEach(({|#0:EntityCommandBuffer buffer|}) =>
                        {
                        }).Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0074)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0075_EcbParameter_ConflictingPlaybackInstructions()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public class {|#0:TestEntityCommandBufferSystem|} : EntityCommandBufferSystem { protected override void OnUpdate(){}}

            partial class ConflictingPlaybackInstructions : SystemBase
            {
                protected override void OnUpdate()
                {
                    Entities
                        .WithImmediatePlayback()
                        .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                        .ForEach((EntityCommandBuffer buffer) =>
                        {
                        }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0075)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0076_EcbParameter_UsedMoreThanOnce()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public class TestEntityCommandBufferSystem : EntityCommandBufferSystem { protected override void OnUpdate(){} }

            partial class EntityCommandsUsedMoreThanOnce : SystemBase
            {
                protected override void OnUpdate()
                {
                    Entities
                        .WithImmediatePlayback()
                        .ForEach(({|#0:EntityCommandBuffer buffer1|}, EntityCommandBuffer buffer2) =>
                        {
                        }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0076)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0077_EcbParameter_ImmediatePlayback_WithScheduling()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public class TestEntityCommandBufferSystem : EntityCommandBufferSystem { protected override void OnUpdate(){} }

            partial class ImmediatePlayback_WithScheduling : SystemBase
            {
                protected override void OnUpdate()
                {
                    Entities
                        .WithImmediatePlayback()
                        .ForEach(({|#0:EntityCommandBuffer buffer|}) =>
                        {
                        }).Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0077)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0078_EcbParameter_MoreThanOnePlaybackSystemsSpecified()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public class {|#0:MyEntityCommandBufferSystem|} : EntityCommandBufferSystem { protected override void OnUpdate(){} }

            public class YourEntityCommandBufferSystem : EntityCommandBufferSystem { protected override void OnUpdate(){} }

            partial class MoreThanOnePlaybackSystemsSpecified : SystemBase
            {
                protected override void OnUpdate()
                {
                    Entities
                        .WithDeferredPlaybackSystem<MyEntityCommandBufferSystem>()
                        .WithDeferredPlaybackSystem<YourEntityCommandBufferSystem>()
                        .ForEach((EntityCommandBuffer buffer) =>
                        {
                        }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0078)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0079_EcbParameter_UnsupportedEcbMethodUsed()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public class TestEntityCommandBufferSystem : EntityCommandBufferSystem { protected override void OnUpdate(){} }

            partial class UnsupportedEcbMethodUsed : SystemBase
            {
                protected override void OnUpdate()
                {
                    Entities
                        .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                        .ForEach((EntityCommandBuffer buffer) =>
                        {
                            {|#0:buffer.Playback(EntityManager)|};
                        }).Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0079)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0080_EcbParameter_MethodExpectingEntityQuery_NotOnMainThread()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public class TestEntityCommandBufferSystem : EntityCommandBufferSystem { protected override void OnUpdate(){} }

            public struct MyTag : IComponentData { }

            partial class MethodExpectingEntityQuery : SystemBase
            {
                protected override void OnUpdate()
                {
                    var entityQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<MyTag>());

                    Entities
                        .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                        .ForEach((EntityCommandBuffer buffer) =>
                        {
                            {|#0:buffer.RemoveComponentForEntityQuery<MyTag>(entityQuery)|};
                        }).Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0080)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0080_EcbParameter_MethodExpectingComponentDataClass_NotOnMainThread()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public class TestEntityCommandBufferSystem : EntityCommandBufferSystem { protected override void OnUpdate(){} }

            public class MyComponentDataClass : IComponentData { }

            partial class MethodExpectingComponentDataClass : SystemBase
            {
                protected override void OnUpdate()
                {
                    Entities
                        .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                        .ForEach((Entity e, EntityCommandBuffer buffer) =>
                        {
                            {|#0:buffer.AddComponent<MyComponentDataClass>(e)|};
                        }).Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0080)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0081_EcbParallelWriterParameter()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public class TestEntityCommandBufferSystem : EntityCommandBufferSystem { protected override void OnUpdate(){} }

            partial class EcbParallelWriterParameter : SystemBase
            {
                protected override void OnUpdate()
                {
                    Entities
                        .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                        .ForEach(({|#0:EntityCommandBuffer.ParallelWriter parallelWriter|}) =>
                        {
                        }).Schedule();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0081)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0082_AspectPassedByIn()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            public partial class RotationSpeedSystemBase : SystemBase
            {
                protected override void OnUpdate()
                {
                    var count = 0;
                    Entities.ForEach(({|#0:in EcsTestAspect myAspect|}) => { ++count; }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0082)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0082_AspectPassedByRef()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            public partial class RotationSpeedSystemBase : SystemBase
            {
                protected override void OnUpdate()
                {
                    var count = 0;
                    Entities.ForEach(({|#0:ref EcsTestAspect myAspect|}) => { ++count; }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0082)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0083_CaptureLocalFunction()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            public partial class RotationSpeedSystemBase : SystemBase
            {
                protected override void OnUpdate()
                {
                    void Test<T>(){}
                    Entities.ForEach(() => { {|#0:Test<int>()|}; }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0083)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0084_DefineLambda()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            public partial class RotationSpeedSystemBase : SystemBase
            {
                protected override void OnUpdate()
                {
                    Entities.ForEach(()=>{ System.Action a = {|#0:()=>{}|}; }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0084)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0084_DefineDelegateFunction()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            public partial class RotationSpeedSystemBase : SystemBase
            {
                protected override void OnUpdate()
                {
                    Entities.ForEach(()=>{ System.Action a = {|#0:delegate{}|}; }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0084)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task DC0085_DefineLocalFunction()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            public partial class RotationSpeedSystemBase : SystemBase
            {
                protected override void OnUpdate()
                {
                    Entities.ForEach(()=>{ {|#0:void Func(){}|} }).Run();
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(LambdaJobsErrors.DC0085)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }
}
