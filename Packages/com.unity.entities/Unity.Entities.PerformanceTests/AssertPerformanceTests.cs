using NUnit.Framework;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace Unity.Entities.Editor.PerformanceTests
{
    class AssertPerformanceTests
    {
        [Test,Performance]
        public void AssertSpeedFloat([Values(10000)] int numComparisons)
        {
            float a = 5.5f;
            float b = 5.5f;
            Measure.Method(() =>
                {
                    for (int i = 0; i < numComparisons; i++)
                    {
                        Assert.AreEqual(a,b);
                        a += 1.0f;
                        b += 1.0f;
                    }
                }) .WarmupCount(1)
                .MeasurementCount(10)
                .Run();
        }

        [Test,Performance]
        public void AssertSpeedFloat3([Values(10000)] int numComparisons)
        {
            float3 a = new float3(2.5f,3.5f,5.5f);
            float3 b = new float3(2.5f,3.5f,5.5f);
            Measure.Method(() =>
                {
                    for (int i = 0; i < numComparisons; i++)
                    {
                        Assert.AreEqual(a,b);
                        a += 1.0f;
                        b += 1.0f;
                    }
                }).WarmupCount(1)
                .MeasurementCount(10)
                .Run();
        }
    }
}
