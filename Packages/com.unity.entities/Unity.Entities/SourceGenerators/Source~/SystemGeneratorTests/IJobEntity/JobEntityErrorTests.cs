using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using Unity.Entities.SourceGen.JobEntityGenerator;
using Unity.Entities.SourceGen.SystemGenerator.Common;
using VerifyCS =
    Unity.Entities.SourceGenerators.Test.CSharpIncrementalGeneratorVerifier<
        Unity.Entities.SourceGen.JobEntityGenerator.JobEntityGenerator>;

namespace Unity.Entities.SourceGenerators;

[TestClass]
public class JobEntityErrorTests
{
    [TestMethod]
    public async Task SGJE0003_InvalidValueTypesInExecuteMethod1()
    {
        const string source = @"
            using Unity.Entities;
            public partial struct WithInvalidValueTypeParameters : IJobEntity
            {
                void Execute(Entity entity, float {|#0:invalidFloat|})
                {
                }
            }

            public partial class TestSystem : SystemBase
            {
                protected override void OnUpdate()
                {
                    Dependency = new WithInvalidValueTypeParameters().Schedule(Dependency);
                }
            }";
            var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0003)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0003_InvalidValueTypesInExecuteMethod2()
    {
        const string source = @"
            using Unity.Entities;
            public partial class PlayerVehicleControlSystem : SystemBase
            {
                protected override void OnUpdate()
                {
                    var job = new IllegalThrustJob { DeltaTime = Time.DeltaTime };
                    job.ScheduleParallel();
                }
            }

            partial struct IllegalThrustJob : IJobEntity
            {
                public float DeltaTime;

                public void Execute(int {|#0:NotAValidParam|}, ref Translation translation)
                {
                    translation.Value *= DeltaTime;
                }
            }";
            var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0003)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0006_NonIntegerEntityInQueryParameter()
    {
        const string source = @"
            using Unity.Entities;
            public partial struct NonIntegerEntityInQueryParameter : IJobEntity
            {
                void Execute(Entity entity, [EntityIndexInQuery] bool {|#0:notInteger|})
                {
                }
            }

            public partial class TestSystem : SystemBase
            {
                protected override void OnUpdate()
                {
                    Dependency = new NonIntegerEntityInQueryParameter().Schedule(Dependency);
                }
            }";
           var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0006)).WithLocation(0);
           await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0006()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            partial struct ErrorJob : IJobEntity
            {
                void Execute([Unity.Entities.ChunkIndexInQuery] float {|#0:val|}){}
            }

            partial struct SomeSystem : ISystem {
                public void OnUpdate(ref SystemState state)
                {
                    new ErrorJob().Run();
                }
            }";
           var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0006)).WithLocation(0);
           await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0007()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            partial struct ErrorJob : IJobEntity
            {
                void Execute([Unity.Entities.ChunkIndexInQuery] int val1,[Unity.Entities.EntityIndexInChunk] int val2,[Unity.Entities.ChunkIndexInQuery] int {|#0:val3|}){}
            }

            partial struct SomeSystem : ISystem {
                public void OnUpdate(ref SystemState state)
                {
                    new ErrorJob().Run();
                }
            }";
           var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0007)).WithLocation(0);
           await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0007_TooManyIntegerEntityInQueryParameters()
    {
        const string source = @"
            using Unity.Entities;
            public partial struct TooManyIntegerEntityInQueryParameters : IJobEntity
            {
                void Execute(Entity entity, [EntityIndexInQuery] int first, [EntityIndexInQuery] int {|#0:second|}) {}
            }

            public partial class TestSystem : SystemBase
            {
                protected override void OnUpdate()
                {
                    Dependency = new TooManyIntegerEntityInQueryParameters().Schedule(Dependency);
                }
            }";
           var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0007)).WithLocation(0);
           await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0008_NoExecuteMethod()
    {
        const string source = @"
            using Unity.Entities;
            public partial struct {|#0:NoExecuteMethod|} : IJobEntity
            {
                void NotExecuting(Entity entity) {}
            }

            public partial class TestSystem : SystemBase
            {
                protected override void OnUpdate()
                {
                    new NoExecuteMethod().Schedule();
                }
            }";
            var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0008)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0008_TooManyExecuteMethods()
    {
        const string source = @"
            using Unity.Entities;
            public partial struct {|#0:TooManyExecuteMethods|} : IJobEntity
            {
                void Execute(Entity entity){}
                void Execute([EntityIndexInQuery] int index){}
                void Execute(){}
            }

            public partial class TestSystem : SystemBase
            {
                protected override void OnUpdate()
                {
                    var instance = new TooManyExecuteMethods();
                    Dependency = instance.Schedule(Dependency);
                }
            }";
            var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0008)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0008_MoreThanOneUserDefinedExecuteMethods()
    {
        const string source = @"
            using Unity.Entities;
            public partial class TooManyUserDefinedExecuteMethods : SystemBase
            {
                protected override void OnUpdate()
                {
                    new ThrustJob().ScheduleParallel();
                }

                struct NonIJobEntityStruct
                {
                    public void Execute() {}
                    public void Execute(int someVal) {}
                }

                partial struct {|#0:ThrustJob|} : IJobEntity
                {
                    public void Execute(ref Translation translation) {}
                    public void Execute(int someVal) {}
                }
            }";
            var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0008)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0010_UnsupportedParameterTypeUsed()
    {
        const string source = @"
            using Unity.Entities;
            public partial class PlayerVehicleControlSystem : SystemBase
            {
                protected override void OnUpdate()
                {
                    var job = new IllegalThrustJob { DeltaTime = Time.DeltaTime };
                    job.ScheduleParallel();
                }
            }

            partial struct IllegalThrustJob : IJobEntity
            {
                public float DeltaTime;

                public void Execute(ref Translation translation, IllegalClass {|#0:illegalClass|})
                {
                    translation.Value *= illegalClass.IllegalValue * DeltaTime;
                }
            }

            class IllegalClass
            {
                public int IllegalValue { get; private set; } = 42;
            }";
            var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0010)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0013()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            partial struct SharedComponentJob : IJobEntity
            {
                void Execute(ref EcsTestSharedComp {|#0:e1|}) => e1.value += 1;
            }

            partial struct SomeSystem : ISystem {
                public void OnUpdate(ref SystemState state)
                {
                    new SharedComponentJob().Run();
                }
            }";
           var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0013)).WithLocation(0);
           await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0016_RefTagComponent()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            partial struct SomeSystem : ISystem {
                public void OnUpdate(ref SystemState state)
                {
                    new MyJob().ScheduleParallel();
                }
            }
            public partial struct MyJob : IJobEntity
            {
                void Execute(ref EcsTestTag {|#0:empty|})
                {
                }
            }";
            var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0016)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0017_DuplicateComponent()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            public partial struct MyJob : IJobEntity
            {
                void Execute(ref EcsTestData data1, ref EcsTestData {|#0:data2|})
                {
                }
            }";

        var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0017)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0018_RefWrapperWithKeyword()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial struct SomeSystem : ISystem {
                public void OnUpdate(ref SystemState state)
                {
                    new MyJob().ScheduleParallel();
                }
            }
            public partial struct MyJob : IJobEntity
            {
                void Execute(ref RefRO<EcsTestData> {|#0:e|})
                {
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0018)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0019_RefWrapperWithNonComponent()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial struct SomeSystem : ISystem {
                public void OnUpdate(ref SystemState state)
                {
                    new MyJob().ScheduleParallel();
                }
            }
            public partial struct MyJob : IJobEntity
            {
                void Execute(ref EnabledRefRO<EcsIntElementEnableable> {|#0:e|})
                {
                }
            }";

        var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0019)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0020_GenericJob()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            partial struct SomeSystem : ISystem
            {
                public void OnUpdate(ref SystemState state)
                {
                    new GenericDataJob<EcsTestData>().ScheduleParallel();
                }
            }
            partial struct {|#0:GenericDataJob|}<TData> : IJobEntity
                where TData : struct, IComponentData
            {
                void Execute(in TData data) { }
            }";

        var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0020)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0021_AspectByIn()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            partial struct SomeJob : IJobEntity
            {
                void Execute(in EcsTestAspect {|#0:data|}) { }
            }";

        var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0021)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0021_AspectByRef()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            partial struct SomeJob : IJobEntity
            {
                void Execute(ref EcsTestAspect {|#0:data|}) { }
            }";

        var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0021)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0022_ManagedDataByIn()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial struct SomeJob : IJobEntity
            {
                void Execute(in EcsTestManagedComponent {|#0:data|}) { }
            }";

        var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0022)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0022_ManagedDataByRef()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial struct SomeJob : IJobEntity
            {
                void Execute(ref EcsTestManagedComponent {|#0:data|}) { }
            }";

        var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0022)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGJE0023_LessAccessibleParameterTypes()
    {
        const string source = @"
            using Unity.Entities;

            struct NonPublicComponent : IComponentData
            {
                public float Value;
            }

            public partial struct TestJob : IJobEntity
            {
                void Execute(in NonPublicComponent {|#0:component|})
                {
                }
            }

            public partial struct TestSystem : ISystem
            {
                public void OnUpdate(ref SystemState state) => new TestJob().Schedule();
            }";

        var expected = VerifyCS.CompilerError(nameof(JobEntityGeneratorErrors.SGJE0023)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }
}
