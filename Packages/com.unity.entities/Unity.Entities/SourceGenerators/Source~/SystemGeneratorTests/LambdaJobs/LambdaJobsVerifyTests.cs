using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS =
    Unity.Entities.SourceGenerators.Test.CSharpSourceGeneratorVerifier<
        Unity.Entities.SourceGen.SystemGenerator.SystemGenerator>;

namespace Unity.Entities.SourceGenerators;

[TestClass]
public class LambdaJobsVerifyTests
{
    [TestMethod]
    public async Task BasicEFE()
    {
        const string source = @"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;

public struct TestData : IComponentData
{
    public int value;
}

public partial class BasicEFESystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref TestData testData) =>
            {
                testData.value++;
            })
            .WithBurst(Unity.Burst.FloatMode.Deterministic, Unity.Burst.FloatPrecision.Low, true)
            .ScheduleParallel();

        Entities.ForEach((ref TestData testData) =>
            {
                testData.value++;
            })
            .ScheduleParallel();
    }
}";

        await VerifyCS.VerifySourceGeneratorAsync(source, nameof(BasicEFE), "Test0__System_19875963020.g.cs");
    }
}
