using System.Linq;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities.Tests;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Transforms;

namespace Unity.Entities.PerformanceTests
{
    public struct SpeedModifier : IComponentData
    {
        public float Value;
    }
    public readonly partial struct RotateAspect : IAspect
    {
        readonly RefRW<LocalTransform> Transform;

        public void Rotate(float time, float speedModifier) =>
            Transform.ValueRW.Rotation =
                math.mul(
                    math.normalize(Transform.ValueRO.Rotation),
                    quaternion.AxisAngle(math.up(), time * speedModifier));
    }

    [BurstCompile(CompileSynchronously = true)]
    partial struct IterateAndUseAspectSystem : ISystem
    {
        [BurstCompile(CompileSynchronously = true)]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.Time.DeltaTime;
            foreach (var (rotateAspect, speedModifierRef) in SystemAPI.Query<RotateAspect, RefRO<SpeedModifier>>())
                rotateAspect.Rotate(time, speedModifierRef.ValueRO.Value);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    partial struct IterateAndUseComponentsSystem : ISystem
    {
        [BurstCompile(CompileSynchronously = true)]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.Time.DeltaTime;
            foreach (var (transform, speedModifierRef) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<SpeedModifier>>())
            {
                transform.ValueRW.Rotation =
                    math.mul(
                        math.normalize(transform.ValueRO.Rotation),
                        quaternion.AxisAngle(math.up(), time * speedModifierRef.ValueRO.Value));
            }
        }
    }

    partial class EntitiesForEachThroughComponentsSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var time = SystemAPI.Time.DeltaTime;
            Entities.ForEach((ref LocalTransform localTransform, in SpeedModifier speedModifier) =>
            {
                localTransform.Rotation =
                    math.mul(
                        math.normalize(localTransform.Rotation),
                        quaternion.AxisAngle(math.up(), time * speedModifier.Value));

            }).WithBurst(synchronousCompilation: true).Run();
        }
    }

    public enum IterationType
    {
        Idiomatic,
        EntitiesForEach
    }

    [TestFixture]
    public class IdiomaticForEachISystemPerformanceTests : ECSTestsFixture
    {
        EntityArchetype _archetype;

        [SetUp]
        public void SetUp() =>
            _archetype = m_Manager.CreateArchetype(AspectUtils.GetRequiredComponents<RotateAspect>().Append(ComponentType.ReadWrite<SpeedModifier>()).ToArray());

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IterateAndUseAspects([Values(100, 100000)] int entityCount)
        {
            var system = World.GetOrCreateSystem<IterateAndUseAspectSystem>();
            var systemPtr = &system;
            using var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, World.UpdateAllocator.ToAllocator);
            m_Manager.CreateEntity(_archetype, entities);
            Measure.Method(() => systemPtr->Update(World.Unmanaged))
                .WarmupCount(1)
                .MeasurementCount(100)
                .Run();
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IterateAndUseComponents([Values(100, 100000)] int entityCount, [Values] IterationType iterationType)
        {
            var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, World.UpdateAllocator.ToAllocator);

            switch (iterationType)
            {
                case IterationType.Idiomatic:
                {
                    var system = World.GetOrCreateSystem<IterateAndUseComponentsSystem>();
                    var systemPtr = &system;

                    m_Manager.CreateEntity(_archetype, entities);

                    Measure.Method(() => systemPtr->Update(World.Unmanaged))
                        .WarmupCount(5)
                        .MeasurementCount(100)
                        .Run();

                    entities.Dispose();
                    break;
                }

                case IterationType.EntitiesForEach:
                {
                    var system = World.GetOrCreateSystemManaged<EntitiesForEachThroughComponentsSystem>();

                    m_Manager.CreateEntity(_archetype, entities);

                    Measure.Method(() => system.Update())
                        .WarmupCount(5)
                        .MeasurementCount(100)
                        .Run();

                    entities.Dispose();
                    break;
                }
            }
        }
    }
}
