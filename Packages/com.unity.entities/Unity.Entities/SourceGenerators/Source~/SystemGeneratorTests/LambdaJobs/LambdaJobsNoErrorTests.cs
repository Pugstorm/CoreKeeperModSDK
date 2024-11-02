using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS =
    Unity.Entities.SourceGenerators.Test.CSharpSourceGeneratorVerifier<
        Unity.Entities.SourceGen.SystemGenerator.SystemGenerator>;

namespace Unity.Entities.SourceGenerators;

[TestClass]
public class LambdaJobsNoErrorTests
{
    [TestMethod]
    public async Task PartialTypes_ThreeParts()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public partial class UserWrittenPartial : SystemBase {
                protected override void OnUpdate() {
                    Entities.ForEach((ref EcsTestData data, ref EcsTestData2 data2) => { }).ScheduleParallel();
                    OnUpdate(unusedParameter: true); // We just want to test that having multiple methods with identical names doesn't lead to compile-time errors
                }
            }

            public partial class UserWrittenPartial : SystemBase {
                protected void OnUpdate(bool unusedParameter) {
                    Entities.ForEach((ref EcsTestData data, ref EcsTestData2 data2) => { }).ScheduleParallel();
                    OnUpdate2();
                }
            }

            public partial class UserWrittenPartial : SystemBase {
                protected void OnUpdate2() {
                    Entities.ForEach((ref EcsTestData data, ref EcsTestData2 data2) => { }).ScheduleParallel();
                }
            }";
            await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task PartialTypes_TwoParts_EntitiesForEachWithExactSameComponentDataSet()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public partial class UserWrittenPartial : SystemBase {
                protected override void OnUpdate() {
                    Entities.ForEach((ref EcsTestData data, ref EcsTestData2 data2) => { }).ScheduleParallel();
                    OnUpdate(unusedParameter: true); // We just want to test that having multiple methods with identical names doesn't lead to compile-time errors
                }
            }

            public partial class UserWrittenPartial : SystemBase {
                protected void OnUpdate(bool unusedParameter) {
                    Entities.ForEach((ref EcsTestData data, ref EcsTestData2 data2) => { }).ScheduleParallel();
                }
            }";
            await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task PartialTypes_TwoParts_EntitiesForEachWithOverlappingComponentDataSets()
    {
        // Only EcsTestData2 is used in both Entities.ForEach() invocations
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public partial class UserWrittenPartial : SystemBase {
                protected override void OnUpdate() {
                    Entities.ForEach((ref EcsTestData data, ref EcsTestData2 data2) => { }).ScheduleParallel();
                    OnUpdate(unusedParameter: true); // We just want to test that having multiple methods with identical names doesn't lead to compile-time errors
                }
            }

            public partial class UserWrittenPartial : SystemBase {
                protected void OnUpdate(bool unusedParameter) {
                    Entities.ForEach((ref EcsTestData2 data2, ref EcsTestData3 data3) => { }).ScheduleParallel();
                }
            }";
            await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task PartialTypes_TwoParts_EntitiesForEachWithUniqueComponentDataSets()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public partial class UserWrittenPartial : SystemBase {
                protected override void OnUpdate() {
                    Entities.ForEach((ref EcsTestData data, ref EcsTestData2 data2) => { }).ScheduleParallel();
                    OnUpdate(unusedParameter: true); // We just want to test that having multiple methods with identical names doesn't lead to compile-time errors
                }
            }

            public partial class UserWrittenPartial : SystemBase {
                protected void OnUpdate(bool unusedParameter) {
                    Entities.ForEach((ref EcsTestData3 data3, ref EcsTestData4 data4) => { }).ScheduleParallel();
                }
            }";
            await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task PartialTypes_TwoParts_JobWithCode()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public partial class UserWrittenPartial : SystemBase {
                protected override void OnUpdate() {
                    Job.WithCode(() => { }).Schedule();
                    OnUpdate(unusedParameter: true); // We just want to test that having multiple methods with identical names doesn't lead to compile-time errors
                }
            }

            public partial class UserWrittenPartial : SystemBase {
                protected void OnUpdate(bool unusedParameter) {
                    Job.WithCode(() => { }).Schedule();
                }
            }";
            await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task SealedSystem_With_EntitiesForEach()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            public sealed partial class SealedSystem : SystemBase {
                protected override void OnUpdate() {
                    Entities.ForEach((ref EcsTestData data, ref EcsTestData2 data2) => { }).ScheduleParallel();
                }
            }";
            await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task PatchedMethods_InEntitiesForEach()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            partial class BugSystem : SystemBase
            {
                protected override void OnUpdate()
                {
                    Entities
                        .ForEach((Entity entity) =>
                        {
                            var a = GetBuffer<EcsIntElement>(entity, isReadOnly: true);
                            var b = GetComponentLookup<EcsTestData>(isReadOnly: true);
                            var c = GetBufferLookup<EcsIntElement>(isReadOnly: true);
                            var d = GetComponent<EcsTestData>(entity: entity);
                            var e = HasComponent<EcsTestData>(entity: entity);
                            var f = HasBuffer<EcsIntElement>(entity: entity);
                            var g = Exists(entity: entity);
                            var h = EntityManager.GetAspect<EcsTestAspect>(entity: entity);
                        })
                        .WithoutBurst()
                        .Run();
                }
            }";
        await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task EntitiesForEach_WithXXX_AndUnqueryableParams()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            class TestEntityCommandBufferSystem : EntityCommandBufferSystem
            {
                protected override void OnUpdate()
                {
                }
            }

            public partial class TestSystem : SystemBase
            {
                protected override void OnUpdate()
                {
                }

                public void AddSetSharedComponentToEntity_WithDeferredPlayback(int n)
                {
                    Entities
                        .WithDeferredPlaybackSystem<TestEntityCommandBufferSystem>()
                        .WithAny<EcsTestData>()
                        .ForEach(
                            (Entity e, EntityCommandBuffer ecb) =>
                            {
                            })
                        .ScheduleParallel();
                }
            }";
            await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task EntitiesForEach_WithComponentInSameNamespace()
    {
        const string source = @"
            using SomeName.Module.AnotherName;
            using Unity.Entities;

            namespace SomeName.Module.SomeName
            {
                public partial class ExampleSystem : SystemBase
                {
                    private EntityQuery m_gameModeQuery;

                    protected override void OnUpdate()
                    {
                        Entities
                            .ForEach((in TestComponent testComponent) =>
                            {

                            })
                            .Run();
                    }
                }
            }

            namespace SomeName.Module.AnotherName
            {
                public struct TestComponent : IComponentData
                {
                }
            }";
        await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task EntitiesForEach_InUnityNamespace()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            namespace My.Unity.Namespace
            {
                public partial class MyScript : SystemBase
                {
                    protected override void OnUpdate()
                    {
                        Entities.WithAll<EcsTestData>().ForEach((Entity entity, int entityInQueryIndex) =>
                        {
                        }).ScheduleParallel();
                    }
                }
            }";
        await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task EntitiesForEach_GetComponentRO_WithStaticSystemAPI()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            using static Unity.Entities.SystemAPI;

            namespace My.Unity.Namespace
            {
                public partial class MyScript : SystemBase
                {
                    protected override void OnUpdate()
                    {
                        Entities.WithAll<EcsTestData>().ForEach((Entity entity) =>
                        {
                            var component = GetComponentRO<EcsTestData>(entity);
                        }).Run();
                    }
                }
            }";
        await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task EntitiesForEach_GetComponentRW_WithStaticSystemAPI()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            using static Unity.Entities.SystemAPI;

            namespace My.Unity.Namespace
            {
                public partial class MyScript : SystemBase
                {
                    protected override void OnUpdate()
                    {
                        Entities.WithAll<EcsTestData>().ForEach((Entity entity) =>
                        {
                            var component = GetComponentRW<EcsTestData>(entity);
                        }).Run();
                    }
                }
            }";
        await VerifyCS.VerifySourceGeneratorAsync(source);
    }
}
