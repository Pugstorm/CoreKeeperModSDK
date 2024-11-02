using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Unity.Entities.Tests
{
    [TestFixture]
    partial class TransformTests : ECSTestsFixture
    {
        const float k_Tolerance = 0.01f;

        // Helpers to compare matrices for nearly-equality
        // Note: see also functions in TransformDataTests.cs
        static bool AreNearlyEqual(float a, float b, float tolerance)
        {
            return math.abs(a - b) <= tolerance;
        }

        static bool AreNearlyEqual(float4 a, float4 b, float tolerance)
        {
            return AreNearlyEqual(a.x, b.x, tolerance) && AreNearlyEqual(a.y, b.y, tolerance) && AreNearlyEqual(a.z, b.z, tolerance) && AreNearlyEqual(a.w, b.w, tolerance);
        }

        [Test]
        public void TRS_ChildPosition()
        {
            var parent = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform));
            var child = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform), typeof(Parent));

            m_Manager.SetComponentData(parent, LocalTransform.Identity);
            m_Manager.SetComponentData(child, new Parent { Value = parent });
            m_Manager.SetComponentData(child,
                LocalTransform.FromRotation(quaternion.RotateY(math.PI))
                    .TransformTransform(
                        LocalTransform.FromPosition(new float3(0.0f, 0.0f, 1.0f))));

            World.GetOrCreateSystem<ParentSystem>().Update(World.Unmanaged); // Connect parent and child
            World.GetOrCreateSystem<LocalToWorldSystem>().Update(World.Unmanaged); // Write to the child's LocalToWorld
            m_Manager.CompleteAllTrackedJobs();

            var childWorldPosition = m_Manager.GetComponentData<LocalToWorld>(child).Position;

            Assert.AreEqual(.0f, childWorldPosition.x, k_Tolerance);
            Assert.AreEqual(.0f, childWorldPosition.y, k_Tolerance);
            Assert.AreEqual(-1f, childWorldPosition.z, k_Tolerance);
        }

        [Test]
        public void TRS_RemovedParentDoesNotAffectChildPosition()
        {
            var parent = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform));
            var child = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(Parent), typeof(LocalTransform));

            m_Manager.SetComponentData(parent, LocalTransform.Identity);
            m_Manager.SetComponentData(child, new Parent { Value = parent });
            m_Manager.SetComponentData(child,
                LocalTransform.FromPositionRotation(new float3(0.0f, 0.0f, 1.0f), quaternion.RotateY(math.PI)));

            World.GetOrCreateSystem<ParentSystem>().Update(World.Unmanaged); // Connect parent and child
            World.GetOrCreateSystem<LocalToWorldSystem>().Update(World.Unmanaged); // Write to the child's LocalToWorld
            m_Manager.CompleteAllTrackedJobs();

            var expectedChildWorldPosition = m_Manager.GetComponentData<LocalToWorld>(child).Position;

            m_Manager.RemoveComponent<Parent>(child);

            m_Manager.SetComponentData(parent,
                LocalTransform.FromPositionRotation(new float3(0.0f, 0.0f, 1.0f), quaternion.RotateY((float)math.PI)));

            World.GetOrCreateSystem<ParentSystem>().Update(World.Unmanaged); // Connect parent and child
            World.GetOrCreateSystem<LocalToWorldSystem>().Update(World.Unmanaged); // Write to the child's LocalToWorld
            m_Manager.CompleteAllTrackedJobs();

            var childWorldPosition = m_Manager.GetComponentData<LocalToWorld>(child).Position;

            Assert.AreEqual(childWorldPosition.x, expectedChildWorldPosition.x, k_Tolerance);
            Assert.AreEqual(childWorldPosition.y, expectedChildWorldPosition.y, k_Tolerance);
            Assert.AreEqual(childWorldPosition.z, expectedChildWorldPosition.z, k_Tolerance);
        }

        class TestHierarchy : IDisposable
        {
            private World World;
            private EntityManager m_Manager;

            private quaternion[] rotations;
            private float3[] translations;

            int[] rotationIndices;
            int[] translationIndices;
            int[] parentIndices;

            private NativeArray<Entity> bodyEntities;

            public void Dispose()
            {
                bodyEntities.Dispose();
            }

            public TestHierarchy(World world, EntityManager manager)
            {
                World = world;
                m_Manager = manager;

                rotations = new quaternion[]
                {
                    quaternion.EulerYZX(new float3(0.125f * (float)math.PI, 0.0f, 0.0f)),
                    quaternion.EulerYZX(new float3(0.5f * (float)math.PI, 0.0f, 0.0f)),
                    quaternion.EulerYZX(new float3((float)math.PI, 0.0f, 0.0f)),
                };
                translations = new float3[]
                {
                    new float3(0.0f, 0.0f, 1.0f),
                    new float3(0.0f, 1.0f, 0.0f),
                    new float3(1.0f, 0.0f, 0.0f),
                    new float3(0.5f, 0.5f, 0.5f),
                };

                //  0: R:[0] T:[0]
                //  1:  - R:[1] T:[1]
                //  2:    - R:[2] T:[0]
                //  3:    - R:[2] T:[1]
                //  4:    - R:[2] T:[2]
                //  5:      - R:[1] T:[0]
                //  6:      - R:[1] T:[1]
                //  7:      - R:[1] T:[2]
                //  8:  - R:[2] T:[2]
                //  9:    - R:[1] T:[0]
                // 10:    - R:[1] T:[1]
                // 11:    - R:[1] T:[2]
                // 12:      - R:[0] T:[0]
                // 13:        - R:[0] T:[1]
                // 14:          - R:[0] T:[2]
                // 15:            - R:[0] T:[2]

                rotationIndices = new int[] { 0, 1, 2, 2, 2, 1, 1, 1, 2, 1, 1, 1, 0, 0, 0, 0 };
                translationIndices = new int[] { 0, 1, 0, 1, 2, 0, 1, 2, 2, 0, 1, 2, 0, 1, 2, 2 };
                parentIndices = new int[] { -1, 0, 1, 1, 1, 4, 4, 4, 0, 8, 8, 8, 11, 12, 13, 14 };
            }

            public int Count => rotationIndices.Length;
            public NativeArray<Entity> Entities => bodyEntities;

            public float4x4[] ExpectedLocalToParent()
            {
                var expectedLocalToParent = new float4x4[16];
                for (int i = 0; i < 16; i++)
                {
                    var rotationIndex = rotationIndices[i];
                    var translationIndex = translationIndices[i];
                    var localToParent = new float4x4(rotations[rotationIndex], translations[translationIndex]);
                    expectedLocalToParent[i] = localToParent;
                }

                return expectedLocalToParent;
            }

            public float4x4[] ExpectedLocalToWorld(float4x4[] expectedLocalToParent)
            {
                var expectedLocalToWorld = new float4x4[16];
                for (int i = 0; i < 16; i++)
                {
                    var parentIndex = parentIndices[i];
                    if (parentIndex == -1)
                    {
                        expectedLocalToWorld[i] = expectedLocalToParent[i];
                    }
                    else
                    {
                        expectedLocalToWorld[i] = math.mul(expectedLocalToWorld[parentIndex], expectedLocalToParent[i]);
                    }
                }

                return expectedLocalToWorld;
            }

            public void Create()
            {
                var rootArchetype = m_Manager.CreateArchetype(typeof(LocalToWorld), typeof(LocalTransform));
                var bodyArchetype = m_Manager.CreateArchetype(typeof(LocalToWorld), typeof(Parent), typeof(LocalTransform));

                CreateInternal(bodyArchetype, rootArchetype, 1.0f);
            }

            private void CreateInternal(EntityArchetype bodyArchetype, EntityArchetype rootArchetype, float scaleValue)
            {
                bodyEntities = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(16, ref World.UpdateAllocator);

                m_Manager.CreateEntity(bodyArchetype, bodyEntities);

                // replace the first one for loop convenience below
                m_Manager.DestroyEntity(bodyEntities[0]);
                bodyEntities[0] = m_Manager.CreateEntity(rootArchetype);

                for (int i = 0; i < 16; i++)
                {
                    var rotationIndex = rotationIndices[i];
                    var translationIndex = translationIndices[i];

                    var transform = LocalTransform.FromPositionRotationScale(
                        translations[translationIndex], rotations[rotationIndex], scaleValue);
                    m_Manager.SetComponentData(bodyEntities[i], transform);
                }

                for (int i = 1; i < 16; i++)
                {
                    var parentIndex = parentIndices[i];
                    m_Manager.SetComponentData(bodyEntities[i], new Parent() { Value = bodyEntities[parentIndex] });
                }
            }

            public void CreateWithWorldToLocal()
            {
                var rootArchetype = m_Manager.CreateArchetype(typeof(LocalToWorld), typeof(LocalTransform));
                var bodyArchetype = m_Manager.CreateArchetype(typeof(LocalToWorld), typeof(LocalTransform));

                CreateInternal(bodyArchetype, rootArchetype, 1.0f);
            }

            public Entity CreateWithWriteGroupChildren()
            {
                var rootArchetype = m_Manager.CreateArchetype(typeof(LocalToWorld), typeof(LocalTransform), typeof(Child));
                var childArchetypeWithWriteGroup = m_Manager.CreateArchetype(typeof(LocalToWorld), typeof(LocalTransform), typeof(Parent), typeof(Child), typeof(TestWriteGroupComponent));
                var childArchetype = m_Manager.CreateArchetype(typeof(LocalToWorld), typeof(LocalTransform), typeof(Parent));

                var root = m_Manager.CreateEntity(rootArchetype);
                var child = m_Manager.CreateEntity(childArchetypeWithWriteGroup);
                var childChild = m_Manager.CreateEntity(childArchetype);

                m_Manager.SetComponentData(root, LocalTransform.Identity);

                m_Manager.SetComponentData(child, LocalTransform.Identity);
                m_Manager.SetComponentData(child, new TestWriteGroupComponent { Value = 42 });
                m_Manager.SetComponentData(child, new Parent { Value = root });

                m_Manager.SetComponentData(childChild, LocalTransform.Identity);
                m_Manager.SetComponentData(childChild, new Parent { Value = child });

                return childChild;
            }

            public void Update()
            {
                World.GetOrCreateSystem<ParentSystem>().Update(World.Unmanaged); // Connect parent and child
                World.GetOrCreateSystem<LocalToWorldSystem>().Update(World.Unmanaged); // Write to the child's LocalToWorld

                // Force complete so that main thread (tests) can have access to direct editing.
                m_Manager.CompleteAllTrackedJobs();
            }

            internal static unsafe bool AssertCloseEnough(float4x4 expected, float4x4 actual)
            {
                var expectedp = (float*)&expected.c0.x;
                var actualp = (float*)&actual.c0.x;
                for (int i = 0; i < 16; i++)
                {
                    Assert.AreEqual(expectedp[i], actualp[i], k_Tolerance);
                }

                return true;
            }

            public void TestExpectedLocalToParent()
            {
                var expectedLocalToParent = ExpectedLocalToParent();

                // Check all non-root LocalToParent
                for (int i = 0; i < 16; i++)
                {
                    var entity = Entities[i];
                    var parentIndex = parentIndices[i];
                    if (parentIndex == -1)
                    {
                        Assert.IsFalse(m_Manager.HasComponent<Parent>(entity));

                        continue;
                    }

                    var localToParent = m_Manager.GetComponentData<LocalTransform>(entity).ToMatrix();

                    AssertCloseEnough(expectedLocalToParent[i], localToParent);
                }
            }

            public void TestExpectedLocalToWorld()
            {
                var expectedLocalToParent = ExpectedLocalToParent();
                var expectedLocalToWorld = ExpectedLocalToWorld(expectedLocalToParent);

                for (int i = 0; i < 16; i++)
                {
                    var entity = Entities[i];
                    if (m_Manager.Exists(entity))
                    {
                        var localToWorld = m_Manager.GetComponentData<LocalToWorld>(entity).Value;
                        AssertCloseEnough(expectedLocalToWorld[i], localToWorld);
                    }
                }
            }

            public void RemoveSomeParents()
            {
                parentIndices[1] = -1;
                parentIndices[8] = -1;

                m_Manager.RemoveComponent<Parent>(Entities[1]);
                m_Manager.RemoveComponent<Parent>(Entities[8]);

                m_Manager.SetComponentData(Entities[1], m_Manager.GetComponentData<LocalTransform>(Entities[1]));
                m_Manager.SetComponentData(Entities[8], m_Manager.GetComponentData<LocalTransform>(Entities[8]));
                m_Manager.RemoveComponent<Parent>(Entities[1]);
                m_Manager.RemoveComponent<Parent>(Entities[8]);
            }

            public void ChangeSomeParents()
            {
                parentIndices[4] = 3;
                parentIndices[8] = 7;

                m_Manager.SetComponentData<Parent>(Entities[4], new Parent { Value = Entities[3] });
                m_Manager.SetComponentData<Parent>(Entities[8], new Parent { Value = Entities[7] });
            }

            public void DeleteSomeParents()
            {
                // Effectively puts children of 0 at the root
                parentIndices[1] = -1;
                parentIndices[8] = -1;

                m_Manager.SetComponentData(Entities[1], m_Manager.GetComponentData<LocalTransform>(Entities[1]));
                m_Manager.SetComponentData(Entities[8], m_Manager.GetComponentData<LocalTransform>(Entities[8]));
                m_Manager.RemoveComponent<Parent>(Entities[1]);
                m_Manager.RemoveComponent<Parent>(Entities[8]);

                m_Manager.DestroyEntity(Entities[0]);
            }

            public void DestroyAll()
            {
                m_Manager.DestroyEntity(Entities);
            }
        }

        [Test]
        public void TRS_TestHierarchyFirstUpdate()
        {
            var testHierarchy = new TestHierarchy(World, m_Manager);

            testHierarchy.Create();
            testHierarchy.Update();

            testHierarchy.TestExpectedLocalToParent();
            testHierarchy.TestExpectedLocalToWorld();

            testHierarchy.Dispose();
        }

        [Test]
        public void TRS_TestHierarchyAfterParentRemoval()
        {
            var testHierarchy = new TestHierarchy(World, m_Manager);

            testHierarchy.Create();
            testHierarchy.Update();

            testHierarchy.RemoveSomeParents();
            testHierarchy.Update();

            testHierarchy.TestExpectedLocalToParent();
            testHierarchy.TestExpectedLocalToWorld();

            testHierarchy.Dispose();
        }

        [Test]
        public void TRS_TestHierarchyAfterParentChange()
        {
            var testHierarchy = new TestHierarchy(World, m_Manager);

            testHierarchy.Create();
            testHierarchy.Update();

            testHierarchy.ChangeSomeParents();
            testHierarchy.Update();

            testHierarchy.TestExpectedLocalToParent();
            testHierarchy.TestExpectedLocalToWorld();

            testHierarchy.Dispose();
        }

        [Test]
        public void TRS_TestHierarchyAfterParentDeleted()
        {
            var testHierarchy = new TestHierarchy(World, m_Manager);

            testHierarchy.Create();
            testHierarchy.Update();

            testHierarchy.DeleteSomeParents();
            testHierarchy.Update();

            testHierarchy.TestExpectedLocalToParent();
            testHierarchy.TestExpectedLocalToWorld();

            testHierarchy.Dispose();
        }

        [Test]
        public void TRS_TestHierarchyDestroyAll()
        {
            var testHierarchy = new TestHierarchy(World, m_Manager);

            testHierarchy.Create();
            testHierarchy.Update();

            // Make sure can handle destroying all parents and children on same frame
            testHierarchy.DestroyAll();
            testHierarchy.Update();

            // Make sure remaining cleanup handled cleanly.
            testHierarchy.Update();

            var entities = m_Manager.GetAllEntities();
            Assert.IsTrue(entities.Length == 0);

            testHierarchy.Dispose();
        }

        [Test]
        public void TRS_TestHierarchyAddExtraChildren()
        {
            var testHierarchy = new TestHierarchy(World, m_Manager);

            testHierarchy.Create();
            testHierarchy.Update();

            // Add a lot more than parent count children on same frame
            var childCount = 5;
            for (int i = 0; i < testHierarchy.Entities.Length; i++)
            {
                var parent = testHierarchy.Entities[i];
                for (int j = 0; j < childCount; j++)
                {
                    var child = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(Parent), typeof(LocalTransform));
                    m_Manager.SetComponentData(child, new Parent { Value = parent });
                    m_Manager.SetComponentData(child, LocalTransform.Identity);
                }
            }

            testHierarchy.Update();
            testHierarchy.Update();

            testHierarchy.Dispose();
        }

        [Test]
        public void TRS_TestHierarchyAddExtraChildrenChangeArchetype()
        {
            var testHierarchy = new TestHierarchy(World, m_Manager);

            testHierarchy.Create();
            testHierarchy.Update();

            // Add a lot more than parent count children on same frame
            var childCount = 5;
            for (int i = 0; i < testHierarchy.Entities.Length; i++)
            {
                var parent = testHierarchy.Entities[i];
                for (int j = 0; j < childCount; j++)
                {
                    var child = m_Manager.CreateEntity(typeof(LocalToWorld));
                    m_Manager.AddComponentData(child, new Parent { Value = parent });
                    m_Manager.AddComponentData(child, LocalTransform.Identity);
                }
            }

            testHierarchy.Update();
            testHierarchy.Update();

            testHierarchy.Dispose();
        }

        [Test]
        public void TRS_TestHierarchyAddExtraChildrenTwiceChangeArchetype()
        {
            var testHierarchy = new TestHierarchy(World, m_Manager);

            testHierarchy.Create();
            testHierarchy.Update();

            // Add a lot more than parent count children on same frame
            var childCount = 5;
            for (int i = 0; i < testHierarchy.Entities.Length; i++)
            {
                var parent = testHierarchy.Entities[i];
                for (int j = 0; j < childCount; j++)
                {
                    var child = m_Manager.CreateEntity(typeof(LocalToWorld));
                    m_Manager.AddComponentData(child, new Parent { Value = parent });
                    m_Manager.AddComponentData(child, LocalTransform.Identity);
                }
            }

            testHierarchy.Update();

            // Add more children to same parents
            for (int i = 0; i < testHierarchy.Entities.Length; i++)
            {
                var parent = testHierarchy.Entities[i];
                for (int j = 0; j < childCount; j++)
                {
                    var child = m_Manager.CreateEntity(typeof(LocalToWorld));
                    m_Manager.AddComponentData(child, new Parent { Value = parent });
                    m_Manager.AddComponentData(child, LocalTransform.Identity);
                }
            }

            testHierarchy.Update();

            testHierarchy.Dispose();
        }

        [Test]
        public void TRS_TestHierarchyAddExtraChildrenInnerChangeArchetype()
        {
            var testHierarchy = new TestHierarchy(World, m_Manager);

            testHierarchy.Create();
            testHierarchy.Update();

            // Add a lot more than parent count children on same frame
            var childCount = 5;
            for (int i = 0; i < testHierarchy.Entities.Length; i++)
            {
                var parent = testHierarchy.Entities[i];
                for (int j = 0; j < childCount; j++)
                {
                    var child = m_Manager.CreateEntity(typeof(LocalToWorld));
                    m_Manager.AddComponentData(child, new Parent { Value = parent });
                    m_Manager.AddComponentData(child, LocalTransform.Identity);

                    for (int k = 0; k < childCount; k++)
                    {
                        var nextChild = m_Manager.CreateEntity(typeof(LocalToWorld));
                        m_Manager.AddComponentData(nextChild, new Parent { Value = child });
                        m_Manager.AddComponentData(nextChild, LocalTransform.Identity);
                    }
                }
            }

            testHierarchy.Update();
            testHierarchy.Update();

            testHierarchy.Dispose();
        }

        [Test]
        public void TRS_TestHierarchyAddExtraChildrenChangeParent()
        {
            var testHierarchy = new TestHierarchy(World, m_Manager);

            testHierarchy.Create();
            testHierarchy.Update();

            // Add a lot more than parent count children on same frame
            var childCount = 5;
            var children = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(testHierarchy.Entities.Length * childCount, ref World.UpdateAllocator);
            for (int i = 0; i < testHierarchy.Entities.Length; i++)
            {
                var parent = testHierarchy.Entities[i];
                for (int j = 0; j < childCount; j++)
                {
                    var child = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(Parent), typeof(LocalTransform));
                    m_Manager.SetComponentData(child, new Parent { Value = parent });
                    m_Manager.AddComponentData(child, LocalTransform.Identity);

                    children[(i * childCount) + j] = child;
                }
            }

            testHierarchy.Update();

            // Change parents
            for (int i = 0; i < children.Length; i++)
            {
                var parent = testHierarchy.Entities[0];
                var child = children[i];
                m_Manager.SetComponentData(child, new Parent { Value = parent });
            }

            testHierarchy.Update();

            testHierarchy.Dispose();
        }

        [Test]
        public void TRS_TestHierarchyAddExtraChildrenChangeParentAgain()
        {
            var testHierarchy = new TestHierarchy(World, m_Manager);
            testHierarchy.Create();
            testHierarchy.Update();

            // Add a lot more than parent count children on same frame
            var childCount = 5;

            var children = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(testHierarchy.Entities.Length * childCount, ref World.UpdateAllocator);
            for (int i = 0; i < testHierarchy.Entities.Length; i++)
            {
                var parent = testHierarchy.Entities[i];
                for (int j = 0; j < childCount; j++)
                {
                    var child = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(Parent), typeof(LocalTransform));
                    m_Manager.SetComponentData(child, new Parent { Value = parent });
                    children[(i * childCount) + j] = child;
                }
            }

            testHierarchy.Update();

            // Change parents
            for (int i = 0; i < testHierarchy.Entities.Length; i++)
            {
                var parent = testHierarchy.Entities[i];
                for (int j = 0; j < childCount; j++)
                {
                    var child = children[(i * childCount) + j];
                    m_Manager.SetComponentData(child, new Parent { Value = parent });
                }
            }

            testHierarchy.Update();
            testHierarchy.Dispose();
        }

        [WriteGroup(typeof(LocalToWorld))]
        struct TestWriteGroupComponent : IComponentData
        {
            public int Value;
        }

        [UpdateInGroup(typeof(SimulationSystemGroup))]
        [UpdateBefore(typeof(TransformSystemGroup))]
        public partial class TestTransformWriteGroupSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities.ForEach((ref LocalToWorld localToWorld, in TestWriteGroupComponent test) =>
                {
                    localToWorld.Value = new float4x4(float3x3.identity, new float3(test.Value));
                }).ScheduleParallel();
            }
        }

        [Test]
        public void TRS_TestHierarchyUpdatesNestedParentsChildWithWriteGroups()
        {
            // P
            // -- C1 <- This has an archetype with writegroup for LocalToWorld
            // -- -- C2 <- Normal child, this should be updated by LocalToParentSystem if I modify C1's LocalToWorld in a custom system

            var testHierarchy = new TestHierarchy(World, m_Manager);
            var c2Entity = testHierarchy.CreateWithWriteGroupChildren();
            testHierarchy.Update();
            World.GetOrCreateSystemManaged<TestTransformWriteGroupSystem>().Update();
            testHierarchy.Update();

            var localToWorld = m_Manager.GetComponentData<LocalToWorld>(c2Entity);
            var expectedLocalToWorld = new float4x4(float3x3.identity, new float3(42));

            TestHierarchy.AssertCloseEnough(expectedLocalToWorld, localToWorld.Value);
        }

        struct ScaledEntityHierarchy
        {
            public Entity entA, entB, entC, entD, entE, entF, entG, entH;
            public LocalTransform ltA, ltB, ltC, ltD, ltE, ltF, ltG, ltH;
            public float4x4 ptmC, ptmD, ptmE, ptmF;
        }

        void CreateScaledEntityHierarchy(out ScaledEntityHierarchy h)
        {
            // Create a hierarchy of entities. All have LocalTransform and LocalToWorld. In addition:
            //
            //    A         B has uniform scale
            //   / \        C has a PostTransformMatrix
            //  B   C       D has a PostTransformMatrix, and should combine B's scale and D's PTM
            // /   / \      E has a PostTransformMatrix, and should combine C+E's PTM
            // D  E   F     F has a PostTransformMatrix, and should combine C+F's PTM
            //       / \    G has uniform scale, and should inherit C+F's PTM values
            //      G   H   H should inherit C+F's PTM values
            EntityArchetype archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(LocalTransform),
                typeof(LocalToWorld));
            h = new ScaledEntityHierarchy { };
            h.entA = m_Manager.CreateEntity(archetype);
            h.entB = m_Manager.CreateEntity(archetype);
            h.entC = m_Manager.CreateEntity(archetype);
            h.entD = m_Manager.CreateEntity(archetype);
            h.entE = m_Manager.CreateEntity(archetype);
            h.entF = m_Manager.CreateEntity(archetype);
            h.entG = m_Manager.CreateEntity(archetype);
            h.entH = m_Manager.CreateEntity(archetype);
            m_Manager.AddComponentData(h.entB, new Parent { Value = h.entA });
            m_Manager.AddComponentData(h.entC, new Parent { Value = h.entA });
            m_Manager.AddComponentData(h.entD, new Parent { Value = h.entB });
            m_Manager.AddComponentData(h.entE, new Parent { Value = h.entC });
            m_Manager.AddComponentData(h.entF, new Parent { Value = h.entC });
            m_Manager.AddComponentData(h.entG, new Parent { Value = h.entF });
            m_Manager.AddComponentData(h.entH, new Parent { Value = h.entF });

            // Initialize LocalTransforms
            h.ltA = LocalTransform.FromPositionRotationScale(new float3(1, 2, 3), quaternion.RotateX(math.PI), 1.0f);
            h.ltB = LocalTransform.FromPositionRotationScale(new float3(1, 2, 3), quaternion.RotateX(math.PI), 2.0f);
            h.ltC = LocalTransform.FromPositionRotationScale(new float3(1, 2, 3), quaternion.RotateX(math.PI), 1.0f);
            h.ltD = LocalTransform.FromPositionRotationScale(new float3(1, 2, 3), quaternion.RotateX(math.PI), 1.0f);
            h.ltE = LocalTransform.FromPositionRotationScale(new float3(1, 2, 3), quaternion.RotateX(math.PI), 1.0f);
            h.ltF = LocalTransform.FromPositionRotationScale(new float3(1, 2, 3), quaternion.RotateX(math.PI), 1.0f);
            h.ltG = LocalTransform.FromPositionRotationScale(new float3(1, 2, 3), quaternion.RotateX(math.PI), 3.0f);
            h.ltH = LocalTransform.FromPositionRotationScale(new float3(1, 2, 3), quaternion.RotateX(math.PI), 1.0f);
            m_Manager.SetComponentData(h.entA, h.ltA);
            m_Manager.SetComponentData(h.entB, h.ltB);
            m_Manager.SetComponentData(h.entC, h.ltC);
            m_Manager.SetComponentData(h.entD, h.ltD);
            m_Manager.SetComponentData(h.entE, h.ltE);
            m_Manager.SetComponentData(h.entF, h.ltF);
            m_Manager.SetComponentData(h.entG, h.ltG);
            m_Manager.SetComponentData(h.entH, h.ltH);

            // Add PostTransformMatrix where applicable
            var ptmTypes = new ComponentTypeSet(typeof(PostTransformMatrix));
            m_Manager.AddComponent(h.entC, ptmTypes);
            m_Manager.AddComponent(h.entD, ptmTypes);
            m_Manager.AddComponent(h.entE, ptmTypes);
            m_Manager.AddComponent(h.entF, ptmTypes);
            h.ptmC = float4x4.Scale(1, 2, 3);
            h.ptmD = float4x4.Scale(4, 5, 6);
            h.ptmE = float4x4.Scale(7, 8, 9);
            h.ptmF = float4x4.Scale(10, 11, 12);
            m_Manager.SetComponentData(h.entC, new PostTransformMatrix { Value = h.ptmC });
            m_Manager.SetComponentData(h.entD, new PostTransformMatrix { Value = h.ptmD });
            m_Manager.SetComponentData(h.entE, new PostTransformMatrix { Value = h.ptmE });
            m_Manager.SetComponentData(h.entF, new PostTransformMatrix { Value = h.ptmF });
        }

        void ValidateScaledEntityHierarchy(in ScaledEntityHierarchy h)
        {
            float4x4 ltwA = m_Manager.GetComponentData<LocalToWorld>(h.entA).Value;
            float4x4 ltwB = m_Manager.GetComponentData<LocalToWorld>(h.entB).Value;
            float4x4 ltwC = m_Manager.GetComponentData<LocalToWorld>(h.entC).Value;
            float4x4 ltwD = m_Manager.GetComponentData<LocalToWorld>(h.entD).Value;
            float4x4 ltwE = m_Manager.GetComponentData<LocalToWorld>(h.entE).Value;
            float4x4 ltwF = m_Manager.GetComponentData<LocalToWorld>(h.entF).Value;
            float4x4 ltwG = m_Manager.GetComponentData<LocalToWorld>(h.entG).Value;
            float4x4 ltwH = m_Manager.GetComponentData<LocalToWorld>(h.entH).Value;
            Assert.AreEqual(h.ltA.ToMatrix(), ltwA);
            Assert.AreEqual(h.ltA.TransformTransform(h.ltB).ToMatrix(), ltwB);
            Assert.AreEqual(math.mul(h.ltA.TransformTransform(h.ltC).ToMatrix(), h.ptmC), ltwC);
            Assert.AreEqual(math.mul(h.ltA.TransformTransform(h.ltB).TransformTransform(h.ltD).ToMatrix(), h.ptmD), ltwD);
            Assert.AreEqual(math.mul(math.mul(ltwC, h.ltE.ToMatrix()), h.ptmE), ltwE);
            Assert.AreEqual(math.mul(math.mul(ltwC, h.ltF.ToMatrix()), h.ptmF), ltwF);
            Assert.AreEqual(math.mul(ltwF, h.ltG.ToMatrix()), ltwG);
            Assert.AreEqual(math.mul(ltwF, h.ltH.ToMatrix()), ltwH);
        }

        [Test]
        public void TransformHierarchy_ComplexScale_Works()
        {
            // Create the TransformSystemGroup
            var transformSystemGroup = World.CreateSystemManaged<TransformSystemGroup>();
            var parentSystem = World.CreateSystem<ParentSystem>();
            var localToWorldSystem = World.CreateSystem<LocalToWorldSystem>();

            transformSystemGroup.AddSystemToUpdateList(parentSystem);
            transformSystemGroup.AddSystemToUpdateList(localToWorldSystem);
            transformSystemGroup.SortSystems();

            // Create the hierarchy
            CreateScaledEntityHierarchy(out ScaledEntityHierarchy h);

            // Tick & validate
            transformSystemGroup.Update();
            ValidateScaledEntityHierarchy(h);
        }

        [Test]
        public void EnabledHierarchy_CreatesChildBuffer()
        {
            Entity SimpleHiearchy()
            {
                var parent = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LinkedEntityGroup), typeof(LocalTransform));
                var child = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(Parent), typeof(LocalTransform));

                var linkedEntityGroup = m_Manager.GetBuffer<LinkedEntityGroup>(parent);
                linkedEntityGroup.Add(new LinkedEntityGroup { Value = parent });
                linkedEntityGroup.Add(new LinkedEntityGroup { Value = child });
                m_Manager.SetComponentData(child, new Parent { Value = parent });

                return parent;
            }

            var parentA = SimpleHiearchy();
            var parentB = SimpleHiearchy();

            var parentSystem = World.GetOrCreateSystem<ParentSystem>();

            // with the hierarchy disabled, updating the transform system doesn't create a child buffer
            m_Manager.SetEnabled(parentA, false);

            Assert.IsFalse(m_Manager.HasComponent<Child>(parentA));
            Assert.IsFalse(m_Manager.HasComponent<Child>(parentB));
            parentSystem.Update(World.Unmanaged);
            Assert.IsFalse(m_Manager.HasComponent<Child>(parentA));
            Assert.IsTrue(m_Manager.HasComponent<Child>(parentB));

            // with the hierarchy enabled, updating the transform system creates a child buffer
            m_Manager.SetEnabled(parentA, true);

            Assert.IsFalse(m_Manager.HasComponent<Child>(parentA));
            Assert.IsTrue(m_Manager.HasComponent<Child>(parentB));
            parentSystem.Update(World.Unmanaged);
            Assert.IsTrue(m_Manager.HasComponent<Child>(parentA));
            Assert.IsTrue(m_Manager.HasComponent<Child>(parentB));
        }

        [Test]
        public void EnabledHierarchy_CreatesChildBuffer_ECB()
        {
            Entity SimpleHiearchy()
            {
                var parent = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LinkedEntityGroup), typeof(LocalTransform));
                var child = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(Parent), typeof(LocalTransform));

                var linkedEntityGroup = m_Manager.GetBuffer<LinkedEntityGroup>(parent);
                linkedEntityGroup.Add(new LinkedEntityGroup { Value = parent });
                linkedEntityGroup.Add(new LinkedEntityGroup { Value = child });
                m_Manager.SetComponentData(child, new Parent { Value = parent });

                return parent;
            }

            var parentA = SimpleHiearchy();
            var parentB = SimpleHiearchy();

            var parentSystem = World.GetOrCreateSystem<ParentSystem>();

            // with the hierarchy disabled, updating the transform system doesn't create a child buffer
            using (var ecbDisabled = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                ecbDisabled.SetEnabled(parentA, false);
                ecbDisabled.Playback(m_Manager);
            }

            Assert.IsFalse(m_Manager.HasComponent<Child>(parentA));
            Assert.IsFalse(m_Manager.HasComponent<Child>(parentB));
            parentSystem.Update(World.Unmanaged);
            Assert.IsFalse(m_Manager.HasComponent<Child>(parentA));
            Assert.IsTrue(m_Manager.HasComponent<Child>(parentB));

            // with the hierarchy enabled, updating the transform system creates a child buffer
            using (var ecbEnabled = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                ecbEnabled.SetEnabled(parentA, true);
                ecbEnabled.Playback(m_Manager);
            }

            Assert.IsFalse(m_Manager.HasComponent<Child>(parentA));
            Assert.IsTrue(m_Manager.HasComponent<Child>(parentB));
            parentSystem.Update(World.Unmanaged);
            Assert.IsTrue(m_Manager.HasComponent<Child>(parentA));
            Assert.IsTrue(m_Manager.HasComponent<Child>(parentB));
        }

        [Test]
        public void TRS_SetParent_OldParentIsDestroyed([Values] bool withCleanup)
        {
            var parentSystem = World.GetOrCreateSystem<ParentSystem>();

            var parentA = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform));
            var parentB = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform));
            var child = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(Parent), typeof(LocalTransform));

            if (withCleanup)
            {
                // Having a cleanup component or not is important to validate the check in the
                // parenting system that it should not try to update a child buffer when it doesn't exist.
                // Because that's the difference between having a destroyed parent entity,
                // or a parent entity without the buffer.
                m_Manager.AddComponent<EcsCleanupTag1>(parentA);
            }

            m_Manager.SetComponentData(child, new Parent { Value = parentA });

            parentSystem.Update(World.Unmanaged);

            m_Manager.DestroyEntity(parentA);
            m_Manager.SetComponentData(child, new Parent { Value = parentB });

            parentSystem.Update(World.Unmanaged);

            Assert.AreEqual(parentB, m_Manager.GetComponentData<Parent>(child).Value);

            var children = m_Manager.GetBuffer<Child>(parentB).AsNativeArray();
            CollectionAssert.AreEqual(children.Reinterpret<Entity>(), new[] { child });
        }

        [Test]
        public void TRS_SetParent_AddsExpectedComponents()
        {
            var parentSystem = World.GetOrCreateSystem<ParentSystem>();

            var localToWorldSystem = World.GetOrCreateSystem<LocalToWorldSystem>();
            var parent = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform));
            var child = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform));

            m_Manager.AddComponentData(child, new Parent { Value = parent });

            parentSystem.Update(World.Unmanaged);
            localToWorldSystem.Update(World.Unmanaged);

            // Make sure child has expected components
            Assert.IsTrue(m_Manager.HasComponent<Parent>(child));
            Assert.IsTrue(m_Manager.HasComponent<PreviousParent>(child));
            Assert.AreEqual(parent, m_Manager.GetComponentData<PreviousParent>(child).Value);
            Assert.IsTrue(m_Manager.HasComponent<LocalTransform>(child));

            // Make sure parent has expected components
            Assert.IsTrue(m_Manager.HasComponent<Child>(parent));
            var children = m_Manager.GetBuffer<Child>(parent);
            Assert.AreEqual(1, children.Length);
            Assert.AreEqual(child, children[0].Value);
        }

        [Test]
        public void TRS_ChangeParent_SetsExpectedComponents()
        {
            var parentSystem = World.GetOrCreateSystem<ParentSystem>();

            var localToWorldSystem = World.GetOrCreateSystem<LocalToWorldSystem>();
            var parentA = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform));
            var parentB = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform));
            var child = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform));

            // set initial parent
            m_Manager.AddComponentData(child, new Parent { Value = parentA });

            parentSystem.Update(World.Unmanaged);
            localToWorldSystem.Update(World.Unmanaged);

            // change parent
            m_Manager.SetComponentData(child, new Parent { Value = parentB });
            parentSystem.Update(World.Unmanaged);
            localToWorldSystem.Update(World.Unmanaged);

            // Make sure child has expected components
            Assert.IsTrue(m_Manager.HasComponent<Parent>(child));
            Assert.IsTrue(m_Manager.HasComponent<PreviousParent>(child));
            Assert.AreEqual(parentB, m_Manager.GetComponentData<PreviousParent>(child).Value);
            Assert.IsTrue(m_Manager.HasComponent<LocalTransform>(child));

            // Make sure old parent has expected components.
            Assert.IsFalse(m_Manager.HasComponent<Child>(parentA));

            // Make sure new parent has expected components
            Assert.IsTrue(m_Manager.HasComponent<Child>(parentB));
            var childrenB = m_Manager.GetBuffer<Child>(parentB);
            Assert.AreEqual(1, childrenB.Length);
            Assert.AreEqual(child, childrenB[0].Value);
        }

        [Test]
        public void TRS_RemoveParent_RemovesExpectedComponents()
        {
            var parentSystem = World.GetOrCreateSystem<ParentSystem>();

            var localToWorldSystem = World.GetOrCreateSystem<LocalToWorldSystem>();
            var parent = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform));
            var child = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform));

            // Establish parent/child relationship
            m_Manager.AddComponentData(child, new Parent { Value = parent });

            parentSystem.Update(World.Unmanaged);
            localToWorldSystem.Update(World.Unmanaged);

            // Break parent/child relationship
            m_Manager.RemoveComponent<Parent>(child);
            parentSystem.Update(World.Unmanaged);
            localToWorldSystem.Update(World.Unmanaged);

            // Make sure child has expected components
            Assert.IsFalse(m_Manager.HasComponent<Parent>(child));
            Assert.IsFalse(m_Manager.HasComponent<PreviousParent>(child));

            // Make sure parent has expected components
            Assert.IsFalse(m_Manager.HasComponent<Child>(parent));
        }

        [Test]
        public void TRS_DestroyParent_RemovesExpectedComponents()
        {
            var parentSystem = World.GetOrCreateSystem<ParentSystem>();

            var localToWorldSystem = World.GetOrCreateSystem<LocalToWorldSystem>();
            var parent = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform));
            var child = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform));

            // Establish parent/child relationship
            m_Manager.AddComponentData(child, new Parent { Value = parent });
            parentSystem.Update(World.Unmanaged);
            localToWorldSystem.Update(World.Unmanaged);

            // Delete parent entity
            m_Manager.DestroyEntity(parent);
            parentSystem.Update(World.Unmanaged);
            localToWorldSystem.Update(World.Unmanaged);

            // Make sure child has expected components
            Assert.IsFalse(m_Manager.HasComponent<Parent>(child));
            Assert.IsFalse(m_Manager.HasComponent<PreviousParent>(child));
        }

        [Test]
        public void TRS_LocalToWorldRotationWithNonuniformScale()
        {
            var entity = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform), typeof(PostTransformMatrix));
            m_Manager.SetComponentData(entity, LocalTransform.FromRotation(quaternion.Euler(math.radians(new float3(10f, 20f, 30f)))));
            m_Manager.SetComponentData(entity, new PostTransformMatrix { Value = float4x4.Scale(1f, 2f, 3f) });
            World.GetOrCreateSystem<LocalToWorldSystem>().Update(World.Unmanaged);
            var rotationFromTransform = m_Manager.GetComponentData<LocalTransform>(entity).Rotation;
            var rotationFromMatrix = m_Manager.GetComponentData<LocalToWorld>(entity).Rotation;
            Assert.IsTrue(AreNearlyEqual(rotationFromMatrix.value, rotationFromTransform.value, k_Tolerance));
        }
    }
}
