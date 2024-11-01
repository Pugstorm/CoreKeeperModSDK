using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace Unity.Entities.PerformanceTests
{
    [Category("Performance")]
    public sealed class TypeManagerPerformanceTests : EntityPerformanceTestFixture
    {
        public struct CustomStruct
        {
            public bool m_Bool;
            public Entity m_Entity;
            public float4 m_Float4;
        }

        public struct NotSmallComponent : IComponentData
        {
            public bool m_Bool;
            public CustomStruct m_CustomStruct1;
            public Entity m_Entity;
            public CustomStruct m_CustomStruct2;
            public float4 m_Float4;
        }

        public struct SmallComponent : IComponentData
        {
            public Entity m_Entity;
        }

        [Test, Performance]
        public void TypeManager_Equals_Blittable_NotSmallComponent_PerformanceTest()
        {
            var a = default(NotSmallComponent);
            var b = default(NotSmallComponent);

            Measure.Method(() =>
                {
                    TypeManager.Equals(ref a, ref b);
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1000)
                .Run();
        }

        [Test, Performance]
        public void TypeManager_Equals_Blittable_SmallComponent_PerformanceTest()
        {
            var a = default(SmallComponent);
            var b = default(SmallComponent);

            Measure.Method(() =>
                {
                    TypeManager.Equals(ref a, ref b);
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1000)
                .Run();
        }

        [Test, Performance]
        public void TypeManager_GetHashCode_Blittable_NotSmallComponent_PerformanceTest()
        {
            var a = default(NotSmallComponent);

            Measure.Method(() =>
                {
                    TypeManager.GetHashCode(ref a);
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1000)
                .Run();
        }

        [Test, Performance]
        public void TypeManager_GetHashCode_Blittable_SmallComponent_PerformanceTest()
        {
            var a = default(SmallComponent);

            Measure.Method(() =>
                {
                    TypeManager.GetHashCode(ref a);
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1000)
                .Run();
        }
    }
}
