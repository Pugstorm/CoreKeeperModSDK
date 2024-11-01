using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Tests;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace Unity.Transforms.PerformanceTests
{
    [Category("Performance")]
    public sealed unsafe partial class TransformDataPerformanceTests : ECSTestsFixture
    {
        [Test, Performance]
        public void TDT_TransformTransform_Perf()
        {
            int count = 1000;
            var rng = new Random(17);

            var matrices = CollectionHelper.CreateNativeArray<float4x4>(count, World.UpdateAllocator.ToAllocator);
            Measure.Method(() =>
                {
                    var m = matrices[0];
                    for (int i = 1; i < count; ++i)
                    {
                        matrices[i] = math.mul(matrices[i], m);
                    }
                })
                .SetUp(() =>
                {
                    for (int i = 0; i < count; ++i)
                    {
                        matrices[i] = float4x4.TRS(rng.NextFloat3(), rng.NextQuaternionRotation(), rng.NextFloat3());
                    }
                })
                .SampleGroup(new SampleGroup($"MatrixMul_{count}x", SampleUnit.Microsecond))
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();

            var transforms =
                CollectionHelper.CreateNativeArray<LocalTransform>(count, World.UpdateAllocator.ToAllocator);
            Measure.Method(() =>
                {
                    var t = transforms[0];
                    for (int i = 1; i < count; ++i)
                    {
                        transforms[i] = transforms[i].TransformTransform(t);
                    }
                })
                .SetUp(() =>
                {
                    for (int i = 0; i < count; ++i)
                    {
                        transforms[i] = LocalTransform.FromPositionRotationScale(
                            rng.NextFloat3(),
                            rng.NextQuaternionRotation(),
                            rng.NextFloat());
                    }
                })
                .SampleGroup(new SampleGroup($"TransformTransform_{count}x", SampleUnit.Microsecond))
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();
        }

        [Test, Performance]
        public void TDT_InverseTransformTransform_Perf()
        {
            int count = 1000;
            var rng = new Random(17);

            var matrices = CollectionHelper.CreateNativeArray<float4x4>(count, World.UpdateAllocator.ToAllocator);
            Measure.Method(() =>
                {
                    var m = matrices[0];
                    for (int i = 1; i < count; ++i)
                    {
                        matrices[i] = math.mul(math.fastinverse(matrices[i]), m);
                    }
                })
                .SetUp(() =>
                {
                    for (int i = 0; i < count; ++i)
                    {
                        matrices[i] = float4x4.TRS(rng.NextFloat3(), rng.NextQuaternionRotation(), 1.0f);
                    }
                })
                .SampleGroup(new SampleGroup($"MatrixMul_fastinverse_{count}x", SampleUnit.Microsecond))
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();

            Measure.Method(() =>
                {
                    var m = matrices[0];
                    for (int i = 1; i < count; ++i)
                    {
                        matrices[i] = math.mul(math.inverse(matrices[i]), m);
                    }
                })
                .SetUp(() =>
                {
                    for (int i = 0; i < count; ++i)
                    {
                        matrices[i] = float4x4.TRS(rng.NextFloat3(), rng.NextQuaternionRotation(), rng.NextFloat3());
                    }
                })
                .SampleGroup(new SampleGroup($"MatrixMul_inverse_{count}x", SampleUnit.Microsecond))
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();

            var transforms =
                CollectionHelper.CreateNativeArray<LocalTransform>(count, World.UpdateAllocator.ToAllocator);
            Measure.Method(() =>
                {
                    var t = transforms[0];
                    for (int i = 1; i < count; ++i)
                    {
                        transforms[i] = transforms[i].InverseTransformTransform(t);
                    }
                })
                .SetUp(() =>
                {
                    for (int i = 0; i < count; ++i)
                    {
                        transforms[i] = LocalTransform.FromPositionRotationScale(
                            rng.NextFloat3(),
                            rng.NextQuaternionRotation(),
                            rng.NextFloat());
                    }
                })
                .SampleGroup(new SampleGroup($"InverseTransformTransform_{count}x", SampleUnit.Microsecond))
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();
        }

        [Test, Performance]
        public void TDT_Inverse_Perf()
        {
            int count = 1000;
            var rng = new Random(17);

            var matrices = CollectionHelper.CreateNativeArray<float4x4>(count, World.UpdateAllocator.ToAllocator);
            Measure.Method(() =>
                {
                    var m = matrices[0];
                    for (int i = 1; i < count; ++i)
                    {
                        matrices[i] = math.fastinverse(matrices[i]);
                    }
                })
                .SetUp(() =>
                {
                    for (int i = 0; i < count; ++i)
                    {
                        matrices[i] = float4x4.TRS(rng.NextFloat3(), rng.NextQuaternionRotation(), 1.0f);
                    }
                })
                .SampleGroup(new SampleGroup($"Matrix_fastinverse_{count}x", SampleUnit.Microsecond))
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();

            Measure.Method(() =>
                {
                    var m = matrices[0];
                    for (int i = 1; i < count; ++i)
                    {
                        matrices[i] = math.inverse(matrices[i]);
                    }
                })
                .SetUp(() =>
                {
                    for (int i = 0; i < count; ++i)
                    {
                        matrices[i] = float4x4.TRS(rng.NextFloat3(), rng.NextQuaternionRotation(), rng.NextFloat3());
                    }
                })
                .SampleGroup(new SampleGroup($"Matrix_inverse_{count}x", SampleUnit.Microsecond))
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();

            var transforms =
                CollectionHelper.CreateNativeArray<LocalTransform>(count, World.UpdateAllocator.ToAllocator);
            Measure.Method(() =>
                {
                    var t = transforms[0];
                    for (int i = 1; i < count; ++i)
                    {
                        transforms[i] = transforms[i].Inverse();
                    }
                })
                .SetUp(() =>
                {
                    for (int i = 0; i < count; ++i)
                    {
                        transforms[i] = LocalTransform.FromPositionRotationScale(
                            rng.NextFloat3(),
                            rng.NextQuaternionRotation(),
                            rng.NextFloat());
                    }
                })
                .SampleGroup(new SampleGroup($"Transform_Inverse_{count}x", SampleUnit.Microsecond))
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();
        }
    }
}
