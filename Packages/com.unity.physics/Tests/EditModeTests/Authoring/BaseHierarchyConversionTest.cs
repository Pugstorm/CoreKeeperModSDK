using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Physics.Tests.Authoring
{
    abstract class BaseHierarchyConversionTest
    {
        protected readonly PhysicsWorldIndex k_DefaultWorldIndex = new PhysicsWorldIndex();

        protected static readonly Regex k_NonReadableMeshPattern = new Regex(@"\b((un)?readable|Read\/Write|(non-)?accessible)\b");

        public static readonly TestCaseData[] k_ExplicitPhysicsBodyHierarchyTestCases =
        {
            new TestCaseData(
                BodyMotionType.Static,
                new EntityQueryDesc { All = new ComponentType[] { typeof(PhysicsCollider), typeof(Parent) } }
            ).SetName("Static has parent"),
            new TestCaseData(
                BodyMotionType.Dynamic,
                new EntityQueryDesc { All = new ComponentType[] { typeof(PhysicsCollider) }, None = new ComponentType[] { typeof(Parent), typeof(PreviousParent) } }
            ).SetName("Dynamic is unparented"),
            new TestCaseData(
                BodyMotionType.Kinematic,
                new EntityQueryDesc { All = new ComponentType[] { typeof(PhysicsCollider) }, None = new ComponentType[] { typeof(Parent), typeof(PreviousParent) } }
            ).SetName("Kinematic is unparented"),
        };

        protected static readonly TestCaseData[] k_ExplicitRigidbodyHierarchyTestCases =
        {
            // no means to produce hierarchy of explicit static bodies with legacy
            k_ExplicitPhysicsBodyHierarchyTestCases[1],
            k_ExplicitPhysicsBodyHierarchyTestCases[2]
        };

        protected void CreateHierarchy(
            Type[] rootComponentTypes, Type[] parentComponentTypes, Type[] childComponentTypes
        )
        {
            Root = new GameObject("Root", rootComponentTypes);
            Parent = new GameObject("Parent", parentComponentTypes);
            Child = new GameObject("Child", childComponentTypes);
            Child.transform.parent = Parent.transform;
            Parent.transform.parent = Root.transform;
        }

        protected void CreateHierarchy(
            bool rootStatic,
            Type[] rootComponentTypes, Type[] parentComponentTypes, Type[] childComponentTypes
        )
        {
            Root = new GameObject("Root", rootComponentTypes);
            Parent = new GameObject("Parent", parentComponentTypes);
            Child = new GameObject("Child", childComponentTypes);
            Child.transform.parent = Parent.transform;
            Parent.transform.parent = Root.transform;
            Root.isStatic = rootStatic;
        }

        protected void TransformHierarchyNodes()
        {
            Root.transform.localPosition = new Vector3(1f, 2f, 3f);
            Root.transform.localRotation = Quaternion.Euler(30f, 60f, 90f);
            Root.transform.localScale = new Vector3(3f, 5f, 7f);
            Parent.transform.localPosition = new Vector3(2f, 4f, 8f);
            Parent.transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
            Parent.transform.localScale = new Vector3(2f, 4f, 8f);
            Child.transform.localPosition = new Vector3(3f, 6f, 9f);
            Child.transform.localRotation = Quaternion.Euler(15f, 30f, 45f);
            Child.transform.localScale = new Vector3(-1f, 2f, -4f);
        }

        protected GameObject Root { get; private set; }
        protected GameObject Parent { get; private set; }
        protected GameObject Child { get; private set; }

        internal enum Node { Root, Parent, Child }

        protected GameObject GetNode(Node node)
        {
            switch (node)
            {
                case Node.Root: return Root;
                case Node.Parent: return Parent;
                case Node.Child: return Child;
                default: throw new NotImplementedException($"Unknown node {node}");
            }
        }

        [SetUp]
        public virtual void SetUp()
        {
        }

        [TearDown]
        public virtual void TearDown()
        {
            if (Child != null)
                GameObject.DestroyImmediate(Child);
            if (Parent != null)
                GameObject.DestroyImmediate(Parent);
            if (Root != null)
                GameObject.DestroyImmediate(Root);
        }

        /// <summary>
        /// Gets transformation data for a rigid body entity
        /// </summary>
        protected void GetRigidBodyTransformationData(ref EntityManager inManager, Entity inEntity,
            out RigidTransform outWorldTransform, out float outScale,
            out LocalToWorld outLocalToWorld)
        {
            outLocalToWorld = inManager.GetComponentData<LocalToWorld>(inEntity);
            outScale = 1f;
            var hasLocalTransform = inManager.HasComponent<LocalTransform>(inEntity);
            var hasParent = inManager.HasComponent<Parent>(inEntity);
            if (hasParent || !hasLocalTransform)
            {
                outWorldTransform =
                    new RigidTransform(outLocalToWorld.Rotation, outLocalToWorld.Position);
            }
            else
            {
                var localTransform = inManager.GetComponentData<LocalTransform>(inEntity);
                outScale = localTransform.Scale;
                outWorldTransform = new RigidTransform(localTransform.Rotation, localTransform.Position);
            }
        }

        /// <summary>
        /// Tests that changing scale of the provided rigid body entity will have the expected physical effect
        /// of scaling both the collider geometry and the inertia tensor and mass in the simulation. The latter applies only if the rigid body is dynamic.
        /// </summary>
        protected void TestScaleChange(World world, Entity entity)
        {
            Assert.That(world.EntityManager.HasComponent<LocalTransform>(entity), Is.True);
            var previousTransform = world.EntityManager.GetComponentData<LocalTransform>(entity);

            // build the physics world and inspect the result
            var buildSystem = world.GetOrCreateSystem<BuildPhysicsWorld>();
            buildSystem.Update(world.Unmanaged);
            // wait until all jobs are finished
            world.Unmanaged.ResolveSystemStateRef(buildSystem).CompleteDependency();

            ref var buildData = ref world.EntityManager.GetComponentDataRW<BuildPhysicsWorldData>(buildSystem).ValueRW;
            ref var physicsWorld = ref buildData.PhysicsData.PhysicsWorld;
            int bodyIndex = physicsWorld.GetRigidBodyIndex(entity);
            Assert.That(bodyIndex, Is.Not.EqualTo(-1));

            // extract bounds from simulation data
            var body = physicsWorld.Bodies[bodyIndex];
            var previousBounds = body.CalculateAabb();

            // extract mass properties from simulation data if body is dynamic
            PhysicsMass previousMass = default;
            if (bodyIndex < physicsWorld.NumDynamicBodies)
            {
                var motionVelocity = physicsWorld.MotionVelocities[bodyIndex];
                var motionData = physicsWorld.MotionDatas[bodyIndex];
                previousMass = new PhysicsMass
                {
                    Transform = motionData.BodyFromMotion,
                    InverseInertia = motionVelocity.InverseInertia,
                    InverseMass = motionVelocity.InverseMass,
                    AngularExpansionFactor = motionVelocity.AngularExpansionFactor
                };
            }

            // scale the collider
            var newLocalTransform = previousTransform;
            newLocalTransform.Scale = previousTransform.Scale + 0.42f;
            var changeScale = newLocalTransform.Scale / previousTransform.Scale;
            world.EntityManager.SetComponentData(entity, newLocalTransform);

            // rebuild the world and wait until all jobs are finished
            buildSystem.Update(world.Unmanaged);
            world.Unmanaged.ResolveSystemStateRef(buildSystem).CompleteDependency();

            buildData = ref world.EntityManager.GetComponentDataRW<BuildPhysicsWorldData>(buildSystem).ValueRW;
            physicsWorld = ref buildData.PhysicsData.PhysicsWorld;

            // confirm that mass properties are affected by scale as expected, if body is dynamic
            if (bodyIndex < physicsWorld.NumDynamicBodies)
            {
                // extract mass properties from simulation data
                var motionVelocity = physicsWorld.MotionVelocities[bodyIndex];
                var motionData = physicsWorld.MotionDatas[bodyIndex];
                var newMass = new PhysicsMass
                {
                    Transform = motionData.BodyFromMotion,
                    InverseInertia = motionVelocity.InverseInertia,
                    InverseMass = motionVelocity.InverseMass,
                    AngularExpansionFactor = motionVelocity.AngularExpansionFactor
                };
                var expectedMass = previousMass.ApplyScale(changeScale);
                Assert.That(newMass.InverseMass, Is.PrettyCloseTo(expectedMass.InverseMass));
                Assert.That(newMass.InverseInertia, Is.PrettyCloseTo(expectedMass.InverseInertia));
                Assert.That(newMass.AngularExpansionFactor, Is.PrettyCloseTo(expectedMass.AngularExpansionFactor));
                Assert.That(new float4x4(newMass.Transform), Is.PrettyCloseTo(new float4x4(expectedMass.Transform)));
            }

            // confirm that collider geometry is affected by scale and bounds are scaled as expected
            body = physicsWorld.Bodies[bodyIndex];
            var newBounds = body.CalculateAabb();
            Assert.That(newBounds.Extents, Is.PrettyCloseTo(previousBounds.Extents * changeScale));
        }

        /// <summary>
        /// Tests that non-uniform scale applied to a collider authoring component affects the bounds of the collider.
        /// </summary>
        protected void TestNonUniformScaleOnCollider(Type[] physicsComponentTypes, Action<GameObject> modifyGameObjectToConvert)
        {
            List<Type> childComponentTypes = new List<Type>(physicsComponentTypes);
            childComponentTypes.AddRange(new[] {typeof(MeshFilter), typeof(MeshRenderer) });
            CreateHierarchy(
                Array.Empty<Type>(),
                Array.Empty<Type>(),
                childComponentTypes.ToArray()
            );

            // modify the game object that will be baked into a collider
            modifyGameObjectToConvert(Child);

            var meshRenderer = Child.GetComponent<MeshRenderer>();
            var meshBoundsWorldBeforeScale = meshRenderer.bounds;

            // induce non-uniform scale in the child collider
            Parent.transform.localPosition = new Vector3(1, 2, 3);
            Parent.transform.localScale = new Vector3(0.5f, 0.2f, 0.3f);
            Child.transform.localPosition = new Vector3(0.5f, -1f, 1.5f);
            Child.transform.localScale = new Vector3(1, 2, 3);

            var expectedColliderWorldTransform = (float4x4)Child.transform.localToWorldMatrix;
            Assert.That(expectedColliderWorldTransform.HasNonUniformScale());

            // expect the bounds of the visual mesh to have been affected by the scale
            var meshBoundsWorldAfterScale = meshRenderer.bounds;
            Assert.That((float3)meshBoundsWorldBeforeScale.size, Is.Not.PrettyCloseTo((float3)meshBoundsWorldAfterScale.size));

            var expectedColliderBoundsWorld = meshBoundsWorldAfterScale;

            TestConvertedData<PhysicsCollider>((world, entities, colliders) =>
            {
                // expect there to be a LocalTransform component with identity scale
                var entity = entities[0];
                Assert.That(world.EntityManager.HasComponent<LocalTransform>(entity), Is.True);
                var localTransform = world.EntityManager.GetComponentData<LocalTransform>(entity);
                Assert.That(localTransform.Scale, Is.PrettyCloseTo(1));

                // expect there to be a PostTransformMatrix component
                Assert.That(world.EntityManager.HasComponent<PostTransformMatrix>(entity), Is.True);

                var postTransformMatrix = world.EntityManager.GetComponentData<PostTransformMatrix>(entity);
                // expect the post transform matrix to have non-uniform scale but no shear
                Assert.That(postTransformMatrix.Value.HasNonUniformScale());
                Assert.That(postTransformMatrix.Value.HasShear(), Is.False);

                // check if bounds of the collider are as expected
                ref var collider = ref colliders[0].Value.Value;
                var colliderBoundsWorld = collider.CalculateAabb(new RigidTransform(localTransform.Rotation, localTransform.Position),
                    localTransform.Scale);

                Assert.That((float3)expectedColliderBoundsWorld.size, Is.PrettyCloseTo(colliderBoundsWorld.Extents));
                Assert.That((float3)expectedColliderBoundsWorld.center, Is.PrettyCloseTo(colliderBoundsWorld.Center));

                TestScaleChange(world, entity);
            }, 1);
        }

        protected void TestCorrectColliderSizeInHierarchy(Type[] physicsComponentTypes, Action configureHierarchy, int expectedEntityCount, Action<World, NativeArray<Entity>, NativeArray<PhysicsCollider>, List<Tuple<Bounds, Transform>>> validateColliders)
        {
            List<Type> goComponentTypes = new List<Type>(physicsComponentTypes);
            goComponentTypes.AddRange(new[] {typeof(MeshFilter), typeof(MeshRenderer) });
            CreateHierarchy(
                goComponentTypes.ToArray(),
                goComponentTypes.ToArray(),
                goComponentTypes.ToArray()
            );

            // create a primitive cube which we will assign to all game objects used in the test
            var cubeGameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cubeMeshFilter = cubeGameObject.GetComponent<MeshFilter>();
            var cubeMeshRenderer = cubeGameObject.GetComponent<MeshRenderer>();
            Assert.That(cubeMeshFilter != null && cubeMeshFilter.sharedMesh != null && cubeMeshRenderer != null);

            // Set up the test game object with a mesh filter and renderer of the cube.
            // It is used to calculate the expected bounds of the baked collider geometry.
            foreach (var filter in Root.GetComponentsInChildren<MeshFilter>())
            {
                filter.mesh = cubeMeshFilter.sharedMesh;
            }

            // default hierarchy setup
            Root.transform.localPosition = new Vector3(0, 0, 0);
            Parent.transform.localPosition = new Vector3(0.25f, 1, 0);
            Child.transform.localPosition = new Vector3(-0.5f, 1, 0);

            // action to further configure the hierarchy
            configureHierarchy();

            // auto set-up mesh colliders if any were added
            foreach (var meshCollider in Root.GetComponentsInChildren<UnityEngine.MeshCollider>())
            {
                meshCollider.sharedMesh = cubeMeshFilter.sharedMesh;
            }

            var meshBounds = new List<Tuple<Bounds, Transform>>();
            foreach (var meshRenderer in Root.GetComponentsInChildren<MeshRenderer>())
            {
                meshRenderer.sharedMaterial = cubeMeshRenderer.sharedMaterial;

                meshBounds.Add(new Tuple<Bounds, Transform>(meshRenderer.bounds, meshRenderer.transform));
            }
            UnityEngine.Object.DestroyImmediate(cubeGameObject);

            TestConvertedData<PhysicsCollider>((world, entities, colliders) =>
            {
                // validation of the baked content
                validateColliders(world, entities, colliders, meshBounds);

                // test scale change
                foreach (var entity in entities)
                {
                    TestScaleChange(world, entity);
                }
            }, expectedEntityCount);
        }

        /// <summary>
        /// Tests that colliders in hierarchy have correct size. By default, a unit cube mesh is added to every game object in the hierarchy.
        /// </summary>
        protected void TestCorrectColliderSizeInHierarchy(Type[] physicsComponentTypes, Action configureHierarchy, bool expectedCompoundCollider)
        {
            var expectedEntityCount = expectedCompoundCollider ? 1 : 3;
            TestCorrectColliderSizeInHierarchy(physicsComponentTypes, configureHierarchy, expectedEntityCount,
                expectedCompoundCollider ? TestCompoundColliderHasExpectedUnionBounds : TestCollidersHaveExpectedBounds);
        }

        /// <summary>
        /// Tests that colliders in provided entities have expected bounds.
        /// </summary>
        protected void TestCollidersHaveExpectedBounds(World world, NativeArray<Entity> entities, NativeArray<PhysicsCollider> colliders,
            List<Tuple<Bounds, Transform>> expectedBounds)
        {
            // expect the colliders to have the same size as the mesh bounds
            var foundIndices = new NativeHashSet<int>(entities.Length, Allocator.Temp);
            var manager = world.EntityManager;
            for (int i = 0; i < colliders.Length; i++)
            {
                var entity = entities[i];
                GetRigidBodyTransformationData(ref manager, entity, out var colliderWorldTransform,
                    out var colliderScale, out var colliderLocalToWorld);

                var matrixPrettyCloseTo = new MatrixPrettyCloseConstraint(colliderLocalToWorld.Value);
                // find the mesh bounds that correspond to the collider by comparing the entity's transform
                // with the mesh bounds' transform
                var expectedBoundsIndex = expectedBounds.FindIndex(element =>
                    matrixPrettyCloseTo.ApplyTo((float4x4)element.Item2.localToWorldMatrix).IsSuccess);
                Assert.That(expectedBoundsIndex, Is.Not.EqualTo(-1));

                var notAlreadyPresent = foundIndices.Add(expectedBoundsIndex);
                Assert.That(notAlreadyPresent, NUnit.Framework.Is.True);

                var collider = colliders[i];
                var expectedBoundsElement = expectedBounds[expectedBoundsIndex];
                var colliderBounds = collider.Value.Value.CalculateAabb(colliderWorldTransform, colliderScale);
                Assert.That(colliderBounds.Extents, Is.PrettyCloseTo(expectedBoundsElement.Item1.size));
                Assert.That(colliderBounds.Center, Is.PrettyCloseTo(expectedBoundsElement.Item1.center));
            }
        }

        /// <summary>
        /// Tests that there is only one compound collider and that its bounds correspond to the union of the provided bounds.
        /// </summary>
        protected void TestCompoundColliderHasExpectedUnionBounds(World world, NativeArray<Entity> entities, NativeArray<PhysicsCollider> colliders,
            List<Tuple<Bounds, Transform>> expectedBounds)
        {
            // expect only one compound collider in this case
            Assert.That(colliders.Length, Is.EqualTo(1));

            ref var compoundCollider = ref colliders[0].Value.Value;
            Assert.That(compoundCollider.Type, Is.EqualTo(ColliderType.Compound));

            // calculate union of the expected bounds for comparison
            var expectedUnionBounds = new Bounds();
            foreach (var expectedBound in expectedBounds)
            {
                expectedUnionBounds.Encapsulate(expectedBound.Item1);
            }

            // expect the compound collider to have the same size as the union of the mesh bounds
            var entity = entities[0];
            var manager = world.EntityManager;
            GetRigidBodyTransformationData(ref manager, entity, out var colliderWorldTransform,
                out var colliderScale, out var colliderLocalToWorld);

            var compoundColliderBounds = compoundCollider.CalculateAabb(colliderWorldTransform, colliderScale);
            Assert.That(compoundColliderBounds.Extents, Is.PrettyCloseTo(expectedUnionBounds.size));
            Assert.That(compoundColliderBounds.Center, Is.PrettyCloseTo(expectedUnionBounds.center));
        }

        /// <summary>
        /// Tests that mass in a single rigid body entity is as expected.
        /// </summary>
        protected void TestExpectedMass(float expectedMass, float3 expectedCOM, float3 expectedInertia, quaternion expectedInertiaRot)
        {
            TestConvertedData<PhysicsMass>((world, mass, entity) =>
            {
                // build the physics world and inspect the result
                var buildSystem = world.GetOrCreateSystem<BuildPhysicsWorld>();
                buildSystem.Update(world.Unmanaged);
                // wait until all jobs are finished
                world.Unmanaged.ResolveSystemStateRef(buildSystem).CompleteDependency();

                var query = new EntityQueryBuilder(Allocator.Temp).WithAll<PhysicsWorldSingleton>()
                    .Build(world.EntityManager);
                var physicsWorld = query.GetSingleton<PhysicsWorldSingleton>();
                int bodyIndex = physicsWorld.GetRigidBodyIndex(entity);

                var expectedMassData = new PhysicsMass
                {
                    InverseMass = math.rcp(expectedMass),
                    CenterOfMass = expectedCOM,
                    InverseInertia = math.rcp(expectedInertia * expectedMass),
                    InertiaOrientation = expectedInertiaRot
                };

                var motionVelocity = physicsWorld.MotionVelocities[bodyIndex];
                var motionData = physicsWorld.MotionDatas[bodyIndex];
                var simulatedMass = new PhysicsMass
                {
                    Transform = motionData.BodyFromMotion,
                    InverseInertia = motionVelocity.InverseInertia,
                    InverseMass = motionVelocity.InverseMass,
                    AngularExpansionFactor = motionVelocity.AngularExpansionFactor
                };

                Assert.That(simulatedMass.InverseMass, Is.PrettyCloseTo(expectedMassData.InverseMass));
                Assert.That(simulatedMass.CenterOfMass, Is.PrettyCloseTo(expectedMassData.CenterOfMass));
                Assert.That(simulatedMass.InverseInertia, Is.PrettyCloseTo(expectedMassData.InverseInertia));
                Assert.That(simulatedMass.InertiaOrientation,
                    Is.OrientedEquivalentTo(expectedMassData.InertiaOrientation));

                // Now, apply a change in scale and expect the mass properties to have scaled according to the theory.

                var previousLocalTransform = world.EntityManager.GetComponentData<LocalTransform>(entity);
                var newLocalTransform = previousLocalTransform;
                newLocalTransform.Scale = previousLocalTransform.Scale + 0.42f;
                var changeScale = newLocalTransform.Scale / previousLocalTransform.Scale;
                world.EntityManager.SetComponentData(entity, newLocalTransform);

                // rebuild the world and wait until all jobs are finished
                buildSystem.Update(world.Unmanaged);
                world.Unmanaged.ResolveSystemStateRef(buildSystem).CompleteDependency();

                // re-aquire the physics world
                physicsWorld = query.GetSingleton<PhysicsWorldSingleton>();
                bodyIndex = physicsWorld.GetRigidBodyIndex(entity);

                var newExpectedMassData = expectedMassData.ApplyScale(changeScale);
                motionVelocity = physicsWorld.MotionVelocities[bodyIndex];
                motionData = physicsWorld.MotionDatas[bodyIndex];
                var newSimulatedMass = new PhysicsMass
                {
                    Transform = motionData.BodyFromMotion,
                    InverseInertia = motionVelocity.InverseInertia,
                    InverseMass = motionVelocity.InverseMass,
                    AngularExpansionFactor = motionVelocity.AngularExpansionFactor
                };

                Assert.That(newSimulatedMass.InverseMass, Is.PrettyCloseTo(newExpectedMassData.InverseMass));
                Assert.That(newSimulatedMass.CenterOfMass, Is.PrettyCloseTo(newExpectedMassData.CenterOfMass));
                Assert.That(newSimulatedMass.InverseInertia, Is.PrettyCloseTo(newExpectedMassData.InverseInertia));
                Assert.That(newSimulatedMass.InertiaOrientation,
                    Is.OrientedEquivalentTo(newExpectedMassData.InertiaOrientation));

                // we expect the mass to have scaled cubicly
                Assert.That(newSimulatedMass.InverseMass, Is.PrettyCloseTo(simulatedMass.InverseMass * math.rcp(math.pow(changeScale, 3))));
                // we expect the inertia tensor to have scaled by the power of 5 of the scale
                Assert.That(newSimulatedMass.InverseInertia, Is.PrettyCloseTo(simulatedMass.InverseInertia * math.rcp(math.pow(changeScale, 5))));
                // we expect the center of mass to have scaled linearly
                Assert.That(newSimulatedMass.CenterOfMass, Is.PrettyCloseTo(simulatedMass.CenterOfMass * changeScale));
                // we expect the inertia orientation to be unchanged
                Assert.That(newSimulatedMass.InertiaOrientation, Is.OrientedEquivalentTo(simulatedMass.InertiaOrientation));
                // we expect the angular expansion factor to have scaled linearly
                Assert.That(newSimulatedMass.AngularExpansionFactor, Is.PrettyCloseTo(simulatedMass.AngularExpansionFactor * changeScale));
            });
        }

        protected void TestConvertedData<T>(Action<World, T, Entity> checkValue) where T : unmanaged, IComponentData =>
            TestConvertedData<T>((world, entities, components) => { checkValue(world, components[0], entities[0]); }, 1);

        protected void TestConvertedData<T>(Action<T> checkValue) where T : unmanaged, IComponentData =>
            TestConvertedData<T>((world, entities, components) => { checkValue(components[0]); }, 1);

        protected void TestConvertedData<T>(Action<NativeArray<T>> checkValue, int assumeCount) where T : unmanaged, IComponentData =>
            TestConvertedData<T>((world, entities, components) => { checkValue(components); }, assumeCount);

        public static Entity ConvertBakeGameObject(GameObject go, World world, BlobAssetStore blobAssetStore)
        {
#if UNITY_EDITOR
            // We need to use an intermediate world as BakingUtility.BakeGameObjects cleans up previously baked
            // entities. This means that we need to move the entities from the intermediate world into the final
            // world. As ConvertBakeGameObject returns the main baked entity, we use the EntityGUID to find that
            // entity in the final world
            using var intermediateWorld = new World("BakingWorld");
            BakingUtility.BakeGameObjects(intermediateWorld, new GameObject[] {go}, new BakingSettings(BakingUtility.BakingFlags.AddEntityGUID, blobAssetStore));
            var bakingSystem = intermediateWorld.GetExistingSystemManaged<BakingSystem>();
            var intermediateEntity = bakingSystem.GetEntity(go);
            var intermediateEntityGuid = intermediateWorld.EntityManager.GetComponentData<EntityGuid>(intermediateEntity);
            // Copy the world
            world.EntityManager.MoveEntitiesFrom(intermediateWorld.EntityManager);
            // Search for the entity in the final world by comparing the EntityGuid from entity in the intermediate world
            var query = world.EntityManager.CreateEntityQuery(new ComponentType[] {typeof(EntityGuid)});
            using var entityArray = query.ToEntityArray(Allocator.Temp);
            using var entityGUIDs = query.ToComponentDataArray<EntityGuid>(Allocator.Temp);
            for (int index = 0; index < entityGUIDs.Length; ++index)
            {
                if (entityGUIDs[index] == intermediateEntityGuid)
                {
                    return entityArray[index];
                }
            }
            return Entity.Null;
#endif
        }

        protected void TestConvertedData<T>(Action<World, NativeArray<Entity>, NativeArray<T>> checkValues, int assumeCount) where T : unmanaged, IComponentData
        {
            var world = new World("Test world");

            try
            {
                using (var blobAssetStore = new BlobAssetStore(128))
                {
                    ConvertBakeGameObject(Root, world, blobAssetStore);

                    using var group = world.EntityManager.CreateEntityQuery(typeof(T));
                    using var components = group.ToComponentDataArray<T>(Allocator.Temp);
                    using var entities = group.ToEntityArray(Allocator.Temp);
                    Assume.That(components, Has.Length.EqualTo(assumeCount));
                    checkValues(world, entities, components);
                }
            }
            finally
            {
                world.Dispose();
            }
        }

        protected void TestConvertedSharedData<T, S>(S sharedComponent)
            where T : unmanaged, IComponentData
            where S : unmanaged, ISharedComponentData =>
            TestConvertedSharedData<T, S>((world, entities, components) => {}, 1, sharedComponent);

        protected void TestConvertedSharedData<T, S>(Action<T> checkValue, S sharedComponent)
            where T : unmanaged, IComponentData
            where S : unmanaged, ISharedComponentData =>
            TestConvertedSharedData<T, S>((world, entities, components) =>
            {
                checkValue?.Invoke(components[0]);
            }, 1, sharedComponent);

        protected void TestConvertedSharedData<T, S>(Action<World, Entity, T> checkValue, S sharedComponent)
            where T : unmanaged, IComponentData
            where S : unmanaged, ISharedComponentData =>
            TestConvertedSharedData<T, S>((world, entities, components) =>
            {
                checkValue?.Invoke(world, entities[0], components[0]);
            }, 1, sharedComponent);

        protected void TestConvertedSharedData<T, S>(Action<NativeArray<T>> checkValue, int assumeCount, S sharedComponent)
            where T : unmanaged, IComponentData
            where S : unmanaged, ISharedComponentData =>
            TestConvertedSharedData<T, S>((world, entities, components) =>
            {
                checkValue?.Invoke(components);
            }, assumeCount, sharedComponent);

        protected void TestConvertedSharedData<T, S>(Action<World, NativeArray<Entity>, NativeArray<T>> checkValues, int assumeCount, S sharedComponent)
            where T : unmanaged, IComponentData
            where S : unmanaged, ISharedComponentData
        {
            var world = new World("Test world");

            try
            {
                using (var blobAssetStore = new BlobAssetStore(128))
                {
                    ConvertBakeGameObject(Root, world, blobAssetStore);

                    using (var group = world.EntityManager.CreateEntityQuery(new ComponentType[] { typeof(T), typeof(S) }))
                    {
                        group.AddSharedComponentFilter(sharedComponent);
                        using var components = group.ToComponentDataArray<T>(Allocator.Temp);
                        using var entities = group.ToEntityArray(Allocator.Temp);
                        Assume.That(components, Has.Length.EqualTo(assumeCount));
                        checkValues(world, entities, components);
                    }
                }
            }
            finally
            {
                world.Dispose();
            }
        }

        protected void TestMeshData(int numExpectedMeshSections, int[] numExpectedPrimitivesPerSection, bool[][] quadPrimitiveExpectedFlags)
        {
            TestConvertedSharedData<PhysicsCollider, PhysicsWorldIndex>(meshCollider =>
            {
                unsafe
                {
                    ref var mesh = ref ((MeshCollider*)meshCollider.ColliderPtr)->Mesh;
                    Assume.That(mesh.Sections.Length, Is.EqualTo(numExpectedMeshSections), $"Expected {numExpectedMeshSections} section(s) on mesh collider.");
                    for (int i = 0; i < numExpectedMeshSections; ++i)
                    {
                        ref var section = ref mesh.Sections[i];
                        var numPrimitives = numExpectedPrimitivesPerSection[i];
                        Assume.That(section.PrimitiveFlags.Length, Is.EqualTo(numPrimitives), $"Expected {numPrimitives} primitive(s) in {i}'th mesh section.");
                        for (int j = 0; j < numPrimitives; ++j)
                        {
                            var isQuad = quadPrimitiveExpectedFlags[i][j];
                            Assert.That(section.PrimitiveFlags[j] & Mesh.PrimitiveFlags.IsQuad, isQuad ? Is.EqualTo(Mesh.PrimitiveFlags.IsQuad) : Is.Not.EqualTo(Mesh.PrimitiveFlags.IsQuad), $"Expected primitive {j} in section {i} to be " + (isQuad ? "a quad" : "not a quad") + " on mesh collider.");
                        }
                    }
                }
            }, k_DefaultWorldIndex);
        }

        protected void VerifyLogsException<T>(Regex message = null) where T : Exception
        {
            var world = new World("Test world");
            try
            {
                using (var blobAssetStore = new BlobAssetStore(128))
                {
                    LogAssert.Expect(LogType.Exception, message ?? new Regex($"\b{typeof(T).Name}\b"));
                    ConvertBakeGameObject(Root, world, blobAssetStore);
                }
            }
            finally
            {
                world.Dispose();
            }
        }

        protected void VerifyNoDataProduced<T>() where T : unmanaged, IComponentData
        {
            var world = new World("Test world");

            try
            {
                using (var blobAssetStore = new BlobAssetStore(128))
                {
                    ConvertBakeGameObject(Root, world, blobAssetStore);

                    using var group = world.EntityManager.CreateEntityQuery(typeof(T));
                    using var components = group.ToComponentDataArray<T>(Allocator.Temp);

                    Assert.That(components.Length, Is.EqualTo(0), $"Conversion pipeline produced {typeof(T).Name}");
                }
            }
            finally
            {
                world.Dispose();
            }
        }
    }
}
