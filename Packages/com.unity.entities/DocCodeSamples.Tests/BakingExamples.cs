using Unity.Burst;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace Doc.CodeSamples.Tests.Baking
{
    namespace SimpleBaker
    {
        #region SimpleBaker

        // This RotationSpeedAuthoring class must follow the MonoBehaviour convention
        // and should live in a file named RotationSpeedAuthoring.cs
        public class RotationSpeedAuthoring : MonoBehaviour
        {
            public float DegreesPerSecond;
        }

        public struct RotationSpeed : IComponentData
        {
            public float RadiansPerSecond;
        }

        public struct Additional : IComponentData
        {
            public float SomeValue;
        }

        public class SimpleBaker : Baker<RotationSpeedAuthoring>
        {
            public override void Bake(RotationSpeedAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new RotationSpeed
                {
                    RadiansPerSecond = math.radians(authoring.DegreesPerSecond)
                });

                var additionalA = CreateAdditionalEntity(TransformUsageFlags.Dynamic, entityName: "Additional A");
                var additionalB = CreateAdditionalEntity(TransformUsageFlags.Dynamic, entityName: "Additional B");

                AddComponent(additionalA, new Additional { SomeValue = 123 });
                AddComponent(additionalB, new Additional { SomeValue = 234 });
            }
        }

        #endregion
    }

    namespace DependenciesBaker
    {
        #region DependenciesBaker

        public struct DependentData : IComponentData
        {
            public float Distance;
            public int VertexCount;
        }

        public class DependentDataAuthoring : MonoBehaviour
        {
            public GameObject Other;
            public Mesh Mesh;
        }

        public class GetComponentBaker : Baker<DependentDataAuthoring>
        {
            public override void Bake(DependentDataAuthoring authoring)
            {
                // Before any early out, declare a dependency towards the external references.
                // Because even if those evaluate to null, they might still be a proper Unity
                // reference to a missing object. The dependency ensures that the baker will
                // be triggered when those objects are restored.

                DependsOn(authoring.Other);
                DependsOn(authoring.Mesh);

                if (authoring.Other == null) return;
                if (authoring.Mesh == null) return;

                var transform = GetComponent<Transform>();
                var transformOther = GetComponent<Transform>(authoring.Other);

                // The checks below that ensure the component exists aren't necessary in this
                // case, because Transform will always be present on any GameObject.
                // As a general principle, checking against missing components is recommended.

                if (transform == null) return;
                if (transformOther == null) return;

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new DependentData
                {
                    Distance = Vector3.Distance(transform.position, transformOther.position),
                    VertexCount = authoring.Mesh.vertexCount
                });
            }
        }

        #endregion
    }

    namespace AddTagToRotationBakingSystem
    {
        #region BakingSystem

        public struct AnotherTag : IComponentData { }

        [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
        partial struct AddTagToRotationBakingSystem : ISystem
        {
            public void OnUpdate(ref SystemState state)
            {
                var queryMissingTag = SystemAPI.QueryBuilder()
                    .WithAll<RotationSpeed>()
                    .WithNone<AnotherTag>()
                    .Build();

                state.EntityManager.AddComponent<AnotherTag>(queryMissingTag);

                // Omitting the second part of this function would lead to inconsistent
                // results during live baking. Added tags would remain on the entity even
                // after removing the RotationSpeed component.

                var queryCleanupTag = SystemAPI.QueryBuilder()
                    .WithAll<AnotherTag>()
                    .WithNone<RotationSpeed>()
                    .Build();

                state.EntityManager.RemoveComponent<AnotherTag>(queryCleanupTag);
            }
        }

        #endregion
    }

    #region ManualCleanupBakingSystem

    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    partial struct ManualCleanupBakingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // For all entities that have SomeComponent, add SomeOtherComponent
            var addQuery = SystemAPI.QueryBuilder()
                .WithAll<SomeComponent>().WithNone<SomeOtherComponent>().Build();
            state.EntityManager.AddComponent<SomeOtherComponent>(addQuery);

            // For all entities that no longer have SomeComponent, remove SomeOtherComponent if it is still there
            var removeCleanupQuery = SystemAPI.QueryBuilder()
                .WithAll<SomeOtherComponent>().WithNone<SomeComponent>().Build();
            state.EntityManager.RemoveComponent<SomeOtherComponent>(removeCleanupQuery);
        }
    }

    #endregion

    #region BakingType

    [BakingType]
    public struct BakingComponent : IComponentData { }

    #endregion

    namespace TemporaryBakingType
    {
    #region TemporaryBakingType

        [TemporaryBakingType]
        public struct TemporaryBakingData : IComponentData
        {
            public float Mass;
        }

        public struct SomeComputedData : IComponentData
        {
            // Computing this is too expensive to do in a Baker, we want to make use of Burst (or maybe Jobs)
            public float ComputedValue;
        }

        public class RigidBodyBaker : Baker<Rigidbody>
        {
            public override void Bake(Rigidbody authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new TemporaryBakingData{Mass = authoring.mass});

                // Even though we don't compute the data, we add the type from the Baker
                AddComponent(entity, new SomeComputedData());
            }
        }

        [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
        [BurstCompile]
        partial struct SomeComputingBakingSystem : ISystem
        {
            // We are doing performance critical work here, so we use Burst
            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                // Because TemporaryBakingData is a [TemporaryBakingType] it only exists the same Bake pass that RigidBodyBaker ran
                // This means this Baking System will only run if the inputs to RigidBodyBaker change and cause it to re-bake
                // Additionally, because we are using no managed types in this system, we can use Burst
                foreach (var (computedComponent, bakingData) in
                         SystemAPI.Query<RefRW<SomeComputedData>, RefRO<TemporaryBakingData>>())
                {
                    var mass = bakingData.ValueRO.Mass;
                    float result = 0;
                    // Do heavy computation here, which is taking advantage of Burst
                    // result = ...
                    computedComponent.ValueRW.ComputedValue = result;
                }
            }
        }

    #endregion
    }

    #region BakingSystemOrder

    public struct SomeComponent : IComponentData
    {
        public int Value;
    }

    public struct SomeOtherComponent : IComponentData
    {
        public int Value;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    partial struct BakingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var query = SystemAPI.QueryBuilder().WithAll<BakedEntity>().Build();
            state.EntityManager.AddComponent<SomeComponent>(query);
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    partial struct ConversionSystemB : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var component in
                     SystemAPI.Query<RefRW<SomeComponent>>().WithAll<SomeOtherComponent>())
            {
                component.ValueRW.Value = 10;
            }
        }
    }

    #endregion
}
