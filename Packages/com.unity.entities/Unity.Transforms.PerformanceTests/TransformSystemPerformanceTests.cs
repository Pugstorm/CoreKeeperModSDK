using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Tests;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace Unity.Transforms.PerformanceTests
{
    [Category("Performance")]
    public sealed unsafe partial class TransformSystemPerformanceTests : ECSTestsFixture
    {
        private List<SystemHandle> TransformSystems;

        public override void Setup()
        {
            base.Setup();
            TransformSystems = new List<SystemHandle>
            {
                World.GetOrCreateSystem<ParentSystem>(),
                World.GetOrCreateSystem<LocalToWorldSystem>(),
            };
        }

        const float k_Tolerance = 0.001f;

        static bool AreNearlyEqual(float a, float b, float tolerance)
        {
            return math.abs(a - b) <= tolerance;
        }

        static bool AreNearlyEqual(float4 a, float4 b, float tolerance)
        {
            return AreNearlyEqual(a.x, b.x, tolerance) && AreNearlyEqual(a.y, b.y, tolerance) && AreNearlyEqual(a.z, b.z, tolerance) && AreNearlyEqual(a.w, b.w, tolerance);
        }

        static bool AreNearlyEqual(quaternion a, quaternion b, float tolerance)
        {
            return AreNearlyEqual(a.value.x, b.value.x, tolerance) && AreNearlyEqual(a.value.y, b.value.y, tolerance) && AreNearlyEqual(a.value.z, b.value.z, tolerance) && AreNearlyEqual(a.value.w, b.value.w, tolerance);
        }

        static bool AreNearlyEqual(float3 a, float3 b, float tolerance)
        {
            return AreNearlyEqual(a.x, b.x, tolerance) && AreNearlyEqual(a.y, b.y, tolerance) && AreNearlyEqual(a.z, b.z, tolerance);
        }

        static bool AreNearlyEqual(float4x4 a, float4x4 b, float tolerance)
        {
            return AreNearlyEqual(a.c0, b.c0, tolerance) && AreNearlyEqual(a.c1, b.c1, tolerance) && AreNearlyEqual(a.c2, b.c2, tolerance) && AreNearlyEqual(a.c3, b.c3, tolerance);
        }

        // For many of the following tests, we want to measure the time to run all the transform systems, but it's also
        // nice to see a breakdown of how long each system takes in isolation. These methods present a hacky way to take
        // all of these measurements in a single test run, with minimal code duplication.
        // To measure the time of system index N within the transform systems, we run all systems before N in Setup(),
        // then measure only system N's update, then run all systems after N in Cleanup(). If N is -1, we measure all
        // systems end-to-end. Each measurement is captured in its own SampleGroup.
        // Note that the individual system measurement times will likely add up to more than the "All" time, because
        // we measure the time to complete the jobs scheduled by each system as well. In a full group update, these jobs
        // would overlap with the main-thread system update logic (and, ideally, with each other).
        private int CurrentMeasureSystemIndex = 0;
        void UpdateTransformSystems_Setup(int measureSystemIndex)
        {
            CurrentMeasureSystemIndex = measureSystemIndex;
            for(int i=0; i<measureSystemIndex; ++i)
                TransformSystems[i].Update(World.Unmanaged);
            m_Manager.CompleteAllTrackedJobs();
        }
        void UpdateTransformSystems_Measure()
        {
            if (CurrentMeasureSystemIndex == -1)
            {
                foreach (var sys in TransformSystems)
                    sys.Update(World.Unmanaged);
            }
            else
            {
                TransformSystems[CurrentMeasureSystemIndex].Update(World.Unmanaged);
            }

            m_Manager.CompleteAllTrackedJobs();
        }
        void UpdateTransformSystems_Cleanup()
        {
            if (CurrentMeasureSystemIndex == -1)
                return;
            for(int i=CurrentMeasureSystemIndex+1; i<TransformSystems.Count; ++i)
                TransformSystems[i].Update(World.Unmanaged);
            m_Manager.CompleteAllTrackedJobs();
        }
        void UpdateTransformSystems_All()
        {
            foreach (var sys in TransformSystems)
                sys.Update(World.Unmanaged);
            m_Manager.CompleteAllTrackedJobs();
        }
        string GetTransformSystemName(int systemIndex)
        {
            if (systemIndex == -1)
                return "All";
            string fullName = TypeManager.GetSystemName(World.Unmanaged.GetTypeOfSystem(TransformSystems[systemIndex])).ToString();
            return fullName.Substring(1+fullName.LastIndexOf("."));
        }

        //////////////////////////////////////////////

        // Reassign the Parent component of a large number of entities, and measure the time to update the ParentSystem
        // and re-establish parent/child relationships.
        // TODO(DOTS-7135): this test exposes an O(N^2) loop in the ParentSystem that scales very poorly.
        [Test, Performance]
        public void ParentSystem_ChangeParents()
        {
            int childEntityCount = 10000;
            var rootArchetype = m_Manager.CreateArchetype(typeof(LocalToWorld), typeof(LocalTransform));
            var childArchetype = m_Manager.CreateArchetype(typeof(LocalToWorld), typeof(LocalTransform), typeof(Parent), typeof(Prefab));

            var root0 = m_Manager.CreateEntity(rootArchetype);
            var root1 = m_Manager.CreateEntity(rootArchetype);

            m_Manager.SetComponentData(root0,
                LocalTransform.FromPositionRotationScale(new float3(1,2,3), quaternion.RotateX(1.0f), 0.5f));
            m_Manager.SetComponentData(root1,
                    LocalTransform.FromPositionRotationScale(new float3(4,5,6), quaternion.RotateY(1.0f), 1.5f));

            var childPrefab = m_Manager.CreateEntity(childArchetype);
            var rng = new Random(17);
            using (var children = new NativeArray<Entity>(childEntityCount, Allocator.Persistent))
            {
                m_Manager.Instantiate(childPrefab, children);
                var rootIndex = 0;
                Measure.Method(UpdateTransformSystems_All)
                    .SetUp(() =>
                    {
                        var parent = (rootIndex == 0) ? root0 : root1;
                        foreach (var t in children)
                        {
                            m_Manager.SetComponentData(t, new Parent { Value = parent });
                            m_Manager.SetComponentData(t,
                                LocalTransform.FromPositionRotationScale(
                                    rng.NextFloat3(),
                                    rng.NextQuaternionRotation(),
                                    0.1f + rng.NextFloat()));
                        }
                    })
                    .CleanUp(() =>
                    {
                        World.UpdateAllocator.Rewind();

                        // Expensive validation code -- significantly increases this test's running time
                        //var expectedP2w = m_Manager.GetComponentData<WorldTransform>(rootIndex == 0 ? root0 : root1);
                        //foreach (var t in children)
                        //{
                        //    var parentTransform = m_Manager.GetComponentData<ParentTransform>(t);
                        //    Assert.AreEqual(expectedP2w.Position, parentTransform.Position);
                        //    Assert.AreEqual(expectedP2w.Rotation, parentTransform.Rotation);
                        //    Assert.AreEqual(expectedP2w.Scale, parentTransform.Scale);
                        //    var expected = expectedP2w.TransformTransform(m_Manager.GetComponentData<LocalTransform>(t));
                        //    var actual = m_Manager.GetComponentData<WorldTransform>(t);
                        //    Assert.AreEqual(expected, actual);
                        //}

                        rootIndex ^= 1;
                    })
                    .WarmupCount(1)
                    .MeasurementCount(10)
                    .Run();
            }
        }

        struct RngComponent : IComponentData
        {
            public Random Rng;
        }
        struct ExpectedLocalToWorld : IComponentData
        {
            public float4x4 Value;
        }
        partial class RandomizeTransforms : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities
                .ForEach((ref LocalTransform transform, ref LocalToWorld l2w, ref ExpectedLocalToWorld expected, ref RngComponent rng) =>
                {
                    transform.Position = rng.Rng.NextFloat3();
                    transform.Rotation = rng.Rng.NextQuaternionRotation();
                    transform.Scale = rng.Rng.NextFloat();
                    l2w.Value = float4x4.identity;
                    expected.Value = transform.ToMatrix();
                }).ScheduleParallel(default).Complete();
            }
        }

        // Run transform systems on a totally flat hierarchy (no parents/children).
        // Optionally dirty the transforms between each run.
        [Test, Performance]
        public void TransformSystemGroup_Flat([Values] bool dirtyTransforms)
        {
            int entityCount = 10000;
            var archetype = m_Manager.CreateArchetype(typeof(LocalToWorld), typeof(LocalTransform), typeof(RngComponent), typeof(ExpectedLocalToWorld));

            using(var entities = m_Manager.CreateEntity(archetype, entityCount, Allocator.Persistent))
            {
                for(int i=0; i < entities.Length; ++i)
                {
                    m_Manager.SetComponentData(entities[i], new RngComponent {Rng = Random.CreateFromIndex((uint)(i+1))});
                }
                World.GetOrCreateSystemManaged<RandomizeTransforms>().Update();
                // One tick to prime the results
                UpdateTransformSystems_All();

                for (int measureSystemIndex = -1; measureSystemIndex < TransformSystems.Count; ++measureSystemIndex)
                {
                    string measureSystemName = GetTransformSystemName(measureSystemIndex);
                    Measure.Method(UpdateTransformSystems_Measure)
                        .SetUp(() =>
                        {
                            if (dirtyTransforms)
                            {
                                World.GetOrCreateSystemManaged<RandomizeTransforms>().Update();
                            }
                            UpdateTransformSystems_Setup(measureSystemIndex);
                        })
                        .CleanUp(() =>
                        {
                            UpdateTransformSystems_Cleanup();
                            World.UpdateAllocator.Rewind();
                            // Only for validation; enabling will significantly affect performance test results
                            //for (int i = 0; i < entities.Length; ++i)
                            //{
                            //    var expected = m_Manager.GetComponentData<ExpectedLocalToWorld>(entities[i]).Value;
                            //    var actual = m_Manager.GetComponentData<LocalToWorld>(entities[i]).Value;
                            //    Assert.AreEqual(expected, actual);
                            //}
                        })
                        .SampleGroup(new SampleGroup(measureSystemName, SampleUnit.Microsecond))
                        .WarmupCount(1)
                        .MeasurementCount(10)
                        .Run();
                }
            }
        }

        // Run the transform systems on a hierarchy with one root entity and a large number of children.
        // Only the root entity's transform changes if dirtyRootTransform is true.
        [Test, Performance]
        public void TransformSystemGroup_OneRootManyChildren([Values] bool dirtyRootTransform)
        {
            int childEntityCount = 10000;
            var rootArchetype = m_Manager.CreateArchetype(typeof(LocalToWorld), typeof(LocalTransform));
            var childArchetype = m_Manager.CreateArchetype(typeof(LocalToWorld), typeof(LocalTransform), typeof(Parent));

            var rng = new Random(17);
            var rootEnt = m_Manager.CreateEntity(rootArchetype);

            m_Manager.SetComponentData(rootEnt, LocalTransform.FromPositionRotationScale(
                rng.NextFloat3(),
                rng.NextQuaternionRotation(),
                rng.NextFloat()));

            using(var entities = m_Manager.CreateEntity(childArchetype, childEntityCount, Allocator.Persistent))
            {
                for(int i=0; i < entities.Length; ++i)
                {
                    m_Manager.SetComponentData(entities[i], new Parent {Value = rootEnt});
                    m_Manager.SetComponentData(entities[i], LocalTransform.FromPositionRotationScale(
                        rng.NextFloat3(),
                        rng.NextQuaternionRotation(),
                        rng.NextFloat()));

                }
                // One tick to prime the results
                UpdateTransformSystems_All();

                for (int measureSystemIndex = -1; measureSystemIndex < TransformSystems.Count; ++measureSystemIndex)
                {
                    string measureSystemName = GetTransformSystemName(measureSystemIndex);
                    Measure.Method(UpdateTransformSystems_Measure)
                        .SetUp(() =>
                        {
                            if (dirtyRootTransform)
                            {
                                // Move root entity, to force the children to update
                                m_Manager.SetComponentData(rootEnt, LocalTransform.FromPositionRotationScale(
                                    rng.NextFloat3(),
                                    rng.NextQuaternionRotation(),
                                    rng.NextFloat()));

                                UpdateTransformSystems_Setup(measureSystemIndex);
                            }
                        })
                        .CleanUp(() =>
                        {
                            UpdateTransformSystems_Cleanup();
                            World.UpdateAllocator.Rewind();

                            // Only for validation; enabling will significantly affect performance test results
                            //var rootL2w = m_Manager.GetComponentData<WorldTransform>(rootEnt);
                            //for (int i = 0; i < entities.Length; ++i)
                            //{
                            //    var expected = rootL2w.TransformTransform(m_Manager.GetComponentData<LocalTransform>(entities[i])).ToMatrix();
                            //    var actual = m_Manager.GetComponentData<LocalToWorld>(entities[i]).Value;
                            //    Assert.AreEqual(expected, actual);
                            //}
                        })
                        .SampleGroup(new SampleGroup(measureSystemName, SampleUnit.Microsecond))
                        .WarmupCount(1)
                        .MeasurementCount(10)
                        .Run();
                }
            }
        }

        // Run the transform systems on many hierarchies, each reasonably deep
        [Test, Performance]
        public void TransformSystemGroup_ManyDeepHierarchies([Values] bool dirtyChildTransforms)
        {
            int rootEntityCount = 1000;
            int childEntityCountPerRoot = 10;

            var rootArchetype =
                m_Manager.CreateArchetype(typeof(LocalToWorld), typeof(LocalTransform));
            var childArchetype = m_Manager.CreateArchetype(typeof(LocalToWorld),
                typeof(LocalTransform), typeof(Parent));

            var rng = new Random(17);
            using var allChildEntities = new NativeList<Entity>(rootEntityCount * (childEntityCountPerRoot + 1),
                Allocator.Persistent);

            void AddChild(Entity parent, int depth)
            {
                var childEntity = m_Manager.CreateEntity(childArchetype);
                m_Manager.SetComponentData(childEntity, LocalTransform.FromPositionRotationScale(
                    rng.NextFloat3(),
                    rng.NextQuaternionRotation(),
                    0.1f + rng.NextFloat()));

                allChildEntities.Add(childEntity);

                m_Manager.SetComponentData(childEntity, new Parent { Value = parent });

                if (depth < childEntityCountPerRoot)
                    AddChild(childEntity, depth + 1);
            }

            using (var rootEntities =
                   m_Manager.CreateEntity(rootArchetype, rootEntityCount, Allocator.Persistent))
            {
                for (int i = 0; i < rootEntities.Length; i++)
                {
                    m_Manager.SetComponentData(rootEntities[i], LocalTransform.FromPositionRotationScale(
                        rng.NextFloat3(),
                        rng.NextQuaternionRotation(),
                        0.1f + rng.NextFloat()));

                    AddChild(rootEntities[i], 0);
                }
            }

            // One tick to prime the results
            UpdateTransformSystems_All();

            for (int measureSystemIndex = -1; measureSystemIndex < TransformSystems.Count; ++measureSystemIndex)
            {
                int iteration = 0;
                string measureSystemName = GetTransformSystemName(measureSystemIndex);
                Measure.Method(UpdateTransformSystems_Measure)
                    .SetUp(() =>
                    {
                        if (dirtyChildTransforms)
                        {
                            // move all child entities
                            foreach (var childEnt in allChildEntities)
                            {
                                m_Manager.SetComponentData(childEnt, LocalTransform.FromPositionRotationScale(
                                    rng.NextFloat3(),
                                    rng.NextQuaternionRotation(),
                                    0.1f + rng.NextFloat()));
                            }
                        }
                        UpdateTransformSystems_Setup(measureSystemIndex);
                    })
                    .CleanUp(() =>
                    {
                        UpdateTransformSystems_Cleanup();
                        World.UpdateAllocator.Rewind();

                        // Only for validation; enabling will significantly affect performance test results
                        //var localTransformLookup = World.EntityManager.GetComponentLookup<LocalTransform>(true);
                        //var parentLookup = World.EntityManager.GetComponentLookup<Parent>(true);
                        //var postTransformMatrixLookup = World.EntityManager.GetComponentLookup<PostTransformMatrix>(true);
                        //for(int i=0; i<allChildEntities.Length; ++i)
                        //{
                        //    var childEnt = allChildEntities[i];
                        //    var parentEnt = m_Manager.GetComponentData<Parent>(childEnt).Value;
                        //    var parentWorldTransform = m_Manager.GetComponentData<WorldTransform>(parentEnt);
                        //    var childParentTransform = m_Manager.GetComponentData<ParentTransform>(childEnt);
                        //    Assert.AreEqual(parentWorldTransform.Position, childParentTransform.Position);
                        //    Assert.AreEqual(parentWorldTransform.Rotation, childParentTransform.Rotation);
                        //    Assert.AreEqual(parentWorldTransform.Scale, childParentTransform.Scale);
                        //    var expected = parentWorldTransform.TransformTransform(m_Manager.GetComponentData<LocalTransform>(childEnt));
                        //    var actual = m_Manager.GetComponentData<WorldTransform>(childEnt);
                        //    Assert.AreEqual(expected, actual, $"Mismatch in iteration {iteration} on child {i} of {allChildEntities.Length}");
                        //    var childWorldTransform = m_Manager.GetComponentData<WorldTransform>(childEnt);
                        //    var childLocalToWorld = m_Manager.GetComponentData<LocalToWorld>(childEnt);
                        //    Assert.IsTrue(AreNearlyEqual(childWorldTransform.ToMatrix(), childLocalToWorld.Value, 0.00001f));
                        //    LocalTransform.ComputeWorldTransformMatrix(childEnt,
                        //        out var childLocalToWorldSync,
                        //        ref localTransformLookup, ref parentLookup, ref postTransformMatrixLookup);
                        //    Assert.AreEqual(childLocalToWorld.Value, childLocalToWorldSync);
                        //}

                        iteration++;
                    })
                    .SampleGroup(new SampleGroup(measureSystemName, SampleUnit.Microsecond))
                    .WarmupCount(1)
                    .MeasurementCount(10)
                    .Run();
            }
        }

        [Test, Performance]
        public void ComputeWorldTransformMatrix_Perf([Values] bool useNonUniformScale)
        {
            var rootArchetype =
                m_Manager.CreateArchetype(typeof(LocalToWorld), typeof(LocalTransform));
            var childArchetype = m_Manager.CreateArchetype(typeof(LocalToWorld), typeof(LocalTransform), typeof(Parent));
            var rng = new Random(17);
            int hierarchyDepth = 100;
            using var allEntities = new NativeList<Entity>(hierarchyDepth+1, Allocator.Persistent);
            // Create root
            Entity parentEntity = m_Manager.CreateEntity(rootArchetype);
            m_Manager.SetComponentData(parentEntity, LocalTransform.FromPositionRotationScale(
                rng.NextFloat3(),
                rng.NextQuaternionRotation(),
                useNonUniformScale ? 1.0f : 0.1f + rng.NextFloat()));
            if (useNonUniformScale)
            {
                m_Manager.AddComponentData(parentEntity, new PostTransformMatrix{
                    Value = float4x4.Scale(rng.NextFloat3() + new float3(0.1f, 0.1f, 0.1f))
                });
            }
            allEntities.Add(parentEntity);
            // Create children
            for (int i = 0; i < hierarchyDepth; ++i)
            {
                Entity childEntity = m_Manager.CreateEntity(childArchetype);
                m_Manager.SetComponentData(childEntity, new Parent { Value = parentEntity });
                m_Manager.SetComponentData(childEntity, LocalTransform.FromPositionRotationScale(
                    rng.NextFloat3(),
                    rng.NextQuaternionRotation(),
                    useNonUniformScale ? 1.0f : 0.1f + rng.NextFloat()));
                if (useNonUniformScale)
                {
                    m_Manager.AddComponentData(childEntity, new PostTransformMatrix{
                        Value = float4x4.Scale(rng.NextFloat3() + new float3(0.1f, 0.1f, 0.1f))
                    });
                }
                allEntities.Add(childEntity);
                parentEntity = childEntity;
            }

            // One tick to prime the results
            UpdateTransformSystems_All();

            var localTransformLookup = m_Manager.GetComponentLookup<LocalTransform>(true);
            var parentLookup = m_Manager.GetComponentLookup<Parent>(true);
            var scaleLookup = m_Manager.GetComponentLookup<PostTransformMatrix>(true);
            string scaleMode = useNonUniformScale ? "NonUniformScale" : "UniformScale";
            foreach (var d in new[] { 0, 1, 10, 100 })
            {
                Entity e = allEntities[d];
                float4x4 expectedLocalToWorld = m_Manager.GetComponentData<LocalToWorld>(e).Value;
                float4x4 worldTransformMatrix = default;
                int callsPerMeasure = 100;
                Measure
                    .Method(() =>
                    {
                        // Note that this function is a ton of random memory access; calling it 100s of times in a row
                        // on the exact same inputs does not give a realistic indication of its absolute performance.
                        // The goal here is to see how that performance scales with hierarchy depth and/or non-uniform scale.
                        for (int i = 0; i < callsPerMeasure; ++i)
                        {
                            TransformHelpers.ComputeWorldTransformMatrix(e, out worldTransformMatrix,
                                ref localTransformLookup, ref parentLookup, ref scaleLookup);
                        }
                    })
                    .SetUp(() =>
                    {
                        worldTransformMatrix = float4x4.identity;
                    })
                    .CleanUp(() =>
                    {
                        Assert.AreEqual(expectedLocalToWorld, worldTransformMatrix);
                    })
                    .SampleGroup(new SampleGroup($"{scaleMode}_{callsPerMeasure}x_Depth_{d}", SampleUnit.Microsecond))
                    .WarmupCount(1)
                    .MeasurementCount(100)
                    .Run();
            }
        }
    }
}
