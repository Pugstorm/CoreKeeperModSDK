using NUnit.Framework;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Tests.Utils;
using Unity.Transforms;
using static Unity.Physics.CompoundCollider;
using Random = Unity.Mathematics.Random;
using Unity.Physics.Aspects;
using static Unity.Entities.SystemAPI;

namespace Unity.Physics.Tests.Aspects
{
    partial class ColliderAspect_UnitTests
    {
        internal enum ColliderAspectType
        {
            CONVEX,
            MESH,
            COMPOUND_CONVEX_CHILDREN,
            NESTED_COMPOUND_CONVEX_MESH_CHILDREN
        }

        internal static Entity CreateBodyComponents(ColliderAspectType type, EntityManager manager)
        {
            // Create default components - index, transform, body, scale
            PhysicsWorldIndex worldIndex = new PhysicsWorldIndex { Value = 0 };

            PhysicsCollider pc = new PhysicsCollider();
            LocalTransform tl = LocalTransform.FromPositionRotationScale(AspectTestUtils.DefaultPos, AspectTestUtils.DefaultRot, 1.0f);
            LocalToWorld ltw = new LocalToWorld { Value = tl.ToMatrix() };


            Entity body = manager.CreateEntity();

            // Add index, transform, scale, localToWorld, collider
            manager.AddSharedComponent<PhysicsWorldIndex>(body, worldIndex);
            manager.AddComponentData<LocalTransform>(body, tl);
            manager.AddComponentData<LocalToWorld>(body, ltw);

            Random rnd = new Random(12345);

            switch (type)
            {
                case ColliderAspectType.CONVEX:
                    pc.Value = TestUtils.GenerateRandomConvex(ref rnd, 1.0f, AspectTestUtils.NonDefaultFilter, AspectTestUtils.Material1);
                    break;
                case ColliderAspectType.MESH:
                    pc.Value = TestUtils.GenerateRandomMesh(ref rnd, 1.0f, AspectTestUtils.NonDefaultFilter, AspectTestUtils.Material1);
                    break;
                case ColliderAspectType.COMPOUND_CONVEX_CHILDREN:
                {
                    using var child1 = TestUtils.GenerateRandomConvex(ref rnd, 1.0f, AspectTestUtils.NonDefaultFilter, AspectTestUtils.Material1);
                    using var child2 = TestUtils.GenerateRandomConvex(ref rnd, 1.0f, AspectTestUtils.DefaultFilter, AspectTestUtils.Material2);
                    NativeArray<ColliderBlobInstance> instances = new NativeArray<ColliderBlobInstance>(2, Allocator.Temp);
                    instances[0] = new ColliderBlobInstance { Collider = child1, CompoundFromChild = RigidTransform.identity, Entity = Entity.Null };
                    instances[1] = new ColliderBlobInstance { Collider = child2, CompoundFromChild = RigidTransform.identity, Entity = Entity.Null };

                    pc.Value = CompoundCollider.Create(instances);
                }
                break;
                case ColliderAspectType.NESTED_COMPOUND_CONVEX_MESH_CHILDREN:
                {
                    using var child1 = TestUtils.GenerateRandomConvex(ref rnd, 1.0f, AspectTestUtils.NonDefaultFilter, AspectTestUtils.Material1);
                    using var child2 = TestUtils.GenerateRandomMesh(ref rnd, 1.0f, AspectTestUtils.DefaultFilter, AspectTestUtils.Material2);
                    NativeArray<ColliderBlobInstance> instances = new NativeArray<ColliderBlobInstance>(2, Allocator.Temp);
                    instances[0] = new ColliderBlobInstance { Collider = child1, CompoundFromChild = RigidTransform.identity, Entity = Entity.Null };
                    instances[1] = new ColliderBlobInstance { Collider = child2, CompoundFromChild = RigidTransform.identity, Entity = Entity.Null };

                    using var compoundChild = CompoundCollider.Create(instances);
                    NativeArray<ColliderBlobInstance> instance = new NativeArray<ColliderBlobInstance>(1, Allocator.Temp);
                    instance[0] = new ColliderBlobInstance { Collider = compoundChild, CompoundFromChild = RigidTransform.identity, Entity = Entity.Null };

                    pc.Value = CompoundCollider.Create(instance);
                }
                break;
                default:
                    break;
            }

            manager.AddComponentData<PhysicsCollider>(body, pc);

            return body;
        }

        internal static void RunTest<S>(ColliderAspectType type)
            where S : SystemBase
        {
            using (var world = new World("Test World"))
            {
                var bodyEntity = CreateBodyComponents(type, world.EntityManager);
                var system = world.GetOrCreateSystemManaged<S>();
                system.Update();

                world.EntityManager.GetComponentData<PhysicsCollider>(bodyEntity).Value.Dispose();
            }
        }

        [Test]
        public void ConvexAspectTest()
        {
            RunTest<ConvexAspectTestSystem>(ColliderAspectType.CONVEX);
        }

        [Test]
        public void MeshAspectTest()
        {
            RunTest<MeshAspectTestSystem>(ColliderAspectType.MESH);
        }

        [Test]
        public void CompoundAspectTest()
        {
            RunTest<CompoundAspectTestSystem>(ColliderAspectType.COMPOUND_CONVEX_CHILDREN);
        }

        [Test]
        public void NestedCompoundAspectTest()
        {
            RunTest<NestedCompoundAspectTestSystem>(ColliderAspectType.NESTED_COMPOUND_CONVEX_MESH_CHILDREN);
        }

        [Test]
        public void ColliderAspectQueryTest()
        {
            RunTest<ColliderAspectQueryTestSystem>(ColliderAspectType.CONVEX);
        }

        [DisableAutoCreation]
        public partial class ConvexAspectTestSystem : SystemBase
        {
            public static void TestNonCompoundProperties(ColliderAspect aspect)
            {
                // Verify data
                {
                    // CollisionFilter
                    {
                        CollisionFilter filter = aspect.GetCollisionFilter();
                        Assert.IsTrue(filter.Equals(AspectTestUtils.NonDefaultFilter));
                        filter = aspect.GetCollisionFilter(new ColliderKey(4, 2));
                        Assert.IsTrue(filter.Equals(AspectTestUtils.NonDefaultFilter));
                    }

                    // CollisionResponse
                    {
                        CollisionResponsePolicy crp = aspect.GetCollisionResponse();
                        Assert.IsTrue(crp == AspectTestUtils.Material1.CollisionResponse);
                        crp = aspect.GetCollisionResponse(new ColliderKey(4, 2));
                        Assert.IsTrue(crp == AspectTestUtils.Material1.CollisionResponse);
                    }

                    // Friction
                    {
                        float friction = aspect.GetFriction();
                        Assert.IsTrue(friction == AspectTestUtils.Material1.Friction);
                        friction = aspect.GetFriction(new ColliderKey(4, 2));
                        Assert.IsTrue(friction == AspectTestUtils.Material1.Friction);
                    }

                    // Restitution
                    {
                        float restitution = aspect.GetRestitution();
                        Assert.IsTrue(restitution == AspectTestUtils.Material1.Restitution);
                        restitution = aspect.GetRestitution(new ColliderKey(4, 2));
                        Assert.IsTrue(restitution == AspectTestUtils.Material1.Restitution);
                    }

                    // Children
                    unsafe
                    {
                        float numChildren = aspect.GetNumberOfChildren();
                        Assert.IsTrue(numChildren == 0);

                        NativeHashMap<ColliderKey, ChildCollider> mapping = new NativeHashMap<ColliderKey, ChildCollider>(1, Allocator.Temp);
                        aspect.GetColliderKeyToChildrenMapping(ref mapping);
                        Assert.IsTrue(mapping.Count == 0);

                        ColliderKey childKey = aspect.ConvertChildIndexToColliderKey(2);
                        Assert.IsTrue(childKey == ColliderKey.Empty);
                    }
                }

                // Modification
                {
                    // CollisionFilter
                    {
                        aspect.SetCollisionFilter(AspectTestUtils.ModificationFilter);
                        Assert.IsTrue(AspectTestUtils.ModificationFilter.Equals(aspect.GetCollisionFilter()));

                        // revert
                        aspect.SetCollisionFilter(AspectTestUtils.NonDefaultFilter, new ColliderKey(4, 2));
                        Assert.IsTrue(AspectTestUtils.NonDefaultFilter.Equals(aspect.GetCollisionFilter(new ColliderKey(4, 2))));
                    }

                    // Collision response
                    {
                        aspect.SetCollisionResponse(CollisionResponsePolicy.RaiseTriggerEvents);
                        Assert.IsTrue(aspect.GetCollisionResponse() == CollisionResponsePolicy.RaiseTriggerEvents);
                        Assert.IsTrue(aspect.GetFriction() == AspectTestUtils.Material1.Friction); // make sure that setting trigger didn't overwrite other values in material

                        // revert
                        aspect.SetCollisionResponse(AspectTestUtils.Material1.CollisionResponse, new ColliderKey(4, 2));
                        Assert.IsTrue(aspect.GetCollisionResponse(new ColliderKey(4, 2)) == AspectTestUtils.Material1.CollisionResponse);
                    }

                    // Friction
                    {
                        aspect.SetFriction(0.285f);
                        Assert.IsTrue(aspect.GetFriction() == 0.285f);
                        Assert.IsTrue(aspect.GetRestitution() == AspectTestUtils.Material1.Restitution); // make sure that setting friction didn't overwrite other values in material

                        // revert
                        aspect.SetFriction(AspectTestUtils.Material1.Friction, new ColliderKey(4, 2));
                        Assert.IsTrue(aspect.GetFriction(new ColliderKey(4, 2)) == AspectTestUtils.Material1.Friction);
                    }

                    // Restitution
                    {
                        aspect.SetRestitution(0.984f);
                        Assert.IsTrue(aspect.GetRestitution() == 0.984f);
                        Assert.IsTrue(aspect.GetFriction() == AspectTestUtils.Material1.Friction); // make sure that setting trigger didn't overwrite other values in material

                        // revert
                        aspect.SetRestitution(AspectTestUtils.Material1.Restitution, new ColliderKey(4, 2));
                        Assert.IsTrue(aspect.GetRestitution(new ColliderKey(4, 2)) == AspectTestUtils.Material1.Restitution);
                    }
                }
            }

            protected override void OnUpdate()
            {
                Entity aspectEntity = SystemAPI.GetSingletonEntity<PhysicsCollider>();
                ColliderAspect aspect = GetAspect<ColliderAspect>(aspectEntity);

                Assert.IsTrue(aspect.CollisionType == CollisionType.Convex);
                TestNonCompoundProperties(aspect);
            }
        }

        [DisableAutoCreation]
        public partial class MeshAspectTestSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entity aspectEntity = SystemAPI.GetSingletonEntity<PhysicsCollider>();
                ColliderAspect aspect = GetAspect<ColliderAspect>(aspectEntity);

                Assert.IsTrue(aspect.CollisionType == CollisionType.Composite);
                Assert.IsTrue(aspect.Type == ColliderType.Mesh);
                ConvexAspectTestSystem.TestNonCompoundProperties(aspect);
            }
        }

        [DisableAutoCreation]
        public partial class CompoundAspectTestSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entity aspectEntity = SystemAPI.GetSingletonEntity<PhysicsCollider>();
                ColliderAspect aspect = GetAspect<ColliderAspect>(aspectEntity);

                Assert.IsTrue(aspect.CollisionType == CollisionType.Composite);
                Assert.IsTrue(aspect.Type == ColliderType.Compound);

                // Root Filter getter
                {
                    CollisionFilter union = CollisionFilter.CreateUnion(AspectTestUtils.DefaultFilter, AspectTestUtils.NonDefaultFilter);

                    // Calling GetFilter without ColliderKey should return union of child filters
                    CollisionFilter rootFilter = aspect.GetCollisionFilter();
                    Assert.IsTrue(union.Equals(rootFilter));
                }


                // Getting friction, restitution and collision response getters
                // without collider key or with invalid one should throw
#if UNITY_EDITOR
                // Tests possible only in Editor, since SafetyChecks do nothing in standalone.
                {
                    Assert.Throws<InvalidOperationException>(() =>
                    {
                        aspect.GetFriction();
                    });

                    Assert.Throws<InvalidOperationException>(() =>
                    {
                        aspect.GetRestitution();
                    });

                    Assert.Throws<InvalidOperationException>(() =>
                    {
                        aspect.GetCollisionResponse();
                    });

                    Assert.Throws<InvalidOperationException>(() =>
                    {
                        aspect.GetFriction(new ColliderKey(12, 4095));
                    });

                    Assert.Throws<InvalidOperationException>(() =>
                    {
                        aspect.GetRestitution(new ColliderKey(12, 4095));
                    });

                    Assert.Throws<InvalidOperationException>(() =>
                    {
                        aspect.GetCollisionResponse(new ColliderKey(12, 4095));
                    });
                }
#endif

                // Children
                {
                    // Check getter methods
                    {
                        Assert.IsTrue(aspect.GetNumberOfChildren() == 2);
                        NativeHashMap<ColliderKey, ChildCollider> map = new NativeHashMap<ColliderKey, ChildCollider>(2, Allocator.Temp);
                        aspect.GetColliderKeyToChildrenMapping(ref map);
                        Assert.IsTrue(map.Count == 2);

                        var keys = map.GetKeyArray(Allocator.Temp);
                        keys.Sort();
                        Assert.IsTrue(keys[0] == aspect.ConvertChildIndexToColliderKey(0));
                        Assert.IsTrue(keys[1] == aspect.ConvertChildIndexToColliderKey(1));

                        // Verify getters
                        Assert.IsTrue(aspect.GetCollisionFilter(keys[0]).Equals(AspectTestUtils.NonDefaultFilter));
                        Assert.IsTrue(aspect.GetCollisionResponse(keys[0]) == AspectTestUtils.Material1.CollisionResponse);
                        Assert.IsTrue(aspect.GetFriction(keys[0]) == AspectTestUtils.Material1.Friction);
                        Assert.IsTrue(aspect.GetRestitution(keys[0]) == AspectTestUtils.Material1.Restitution);

                        Assert.IsTrue(aspect.GetCollisionFilter(keys[1]).Equals(AspectTestUtils.DefaultFilter));
                        Assert.IsTrue(aspect.GetCollisionResponse(keys[1]) == AspectTestUtils.Material2.CollisionResponse);
                        Assert.IsTrue(aspect.GetFriction(keys[1]) == AspectTestUtils.Material2.Friction);
                        Assert.IsTrue(aspect.GetRestitution(keys[1]) == AspectTestUtils.Material2.Restitution);
                    }

                    // Setters
                    {
                        NativeHashMap<ColliderKey, ChildCollider> map = new NativeHashMap<ColliderKey, ChildCollider>(2, Allocator.Temp);
                        aspect.GetColliderKeyToChildrenMapping(ref map);
                        var keys = map.GetKeyArray(Allocator.Temp);
                        keys.Sort();

                        // Collision filter
                        {
                            // Set root filter
                            aspect.SetCollisionFilter(AspectTestUtils.ModificationFilter);
                            Assert.IsTrue(aspect.GetCollisionFilter().Equals(AspectTestUtils.ModificationFilter));

                            // Setting root filter also sets children filter
                            Assert.IsTrue(aspect.GetCollisionFilter(keys[0]).Equals(AspectTestUtils.ModificationFilter));
                            Assert.IsTrue(aspect.GetCollisionFilter(keys[1]).Equals(AspectTestUtils.ModificationFilter));

                            // Revert this by setting proper filters on children
                            aspect.SetCollisionFilter(AspectTestUtils.NonDefaultFilter, keys[0]);
                            aspect.SetCollisionFilter(AspectTestUtils.DefaultFilter, keys[1]);

                            // Check that child and root filters are correct
                            Assert.IsTrue(aspect.GetCollisionFilter(keys[0]).Equals(AspectTestUtils.NonDefaultFilter));
                            Assert.IsTrue(aspect.GetCollisionFilter(keys[1]).Equals(AspectTestUtils.DefaultFilter));
                            Assert.IsTrue(aspect.GetCollisionFilter().Equals(CollisionFilter.CreateUnion(AspectTestUtils.NonDefaultFilter, AspectTestUtils.DefaultFilter)));
                        }

                        // Friction
                        {
                            // Set root friction, should not affect other properties of children material
                            aspect.SetFriction(1.0f);
                            Assert.IsTrue(aspect.GetFriction(keys[0]) == 1.0f);
                            Assert.IsTrue(aspect.GetCollisionResponse(keys[0]) == AspectTestUtils.Material1.CollisionResponse);
                            Assert.IsTrue(aspect.GetRestitution(keys[0]) == AspectTestUtils.Material1.Restitution);

                            Assert.IsTrue(aspect.GetFriction(keys[1]) == 1.0f);
                            Assert.IsTrue(aspect.GetCollisionResponse(keys[1]) == AspectTestUtils.Material2.CollisionResponse);
                            Assert.IsTrue(aspect.GetRestitution(keys[1]) == AspectTestUtils.Material2.Restitution);

                            // Revert
                            aspect.SetFriction(AspectTestUtils.Material1.Friction, keys[0]);
                            aspect.SetFriction(AspectTestUtils.Material2.Friction, keys[1]);

                            // Make sure that they are properly set, and that the other properties remain untouched
                            Assert.IsTrue(aspect.GetFriction(keys[0]) == AspectTestUtils.Material1.Friction);
                            Assert.IsTrue(aspect.GetCollisionResponse(keys[0]) == AspectTestUtils.Material1.CollisionResponse);
                            Assert.IsTrue(aspect.GetRestitution(keys[0]) == AspectTestUtils.Material1.Restitution);

                            Assert.IsTrue(aspect.GetFriction(keys[1]) == AspectTestUtils.Material2.Friction);
                            Assert.IsTrue(aspect.GetCollisionResponse(keys[1]) == AspectTestUtils.Material2.CollisionResponse);
                            Assert.IsTrue(aspect.GetRestitution(keys[1]) == AspectTestUtils.Material2.Restitution);
                        }

                        // Restitution
                        {
                            // Set root restitution, should not affect other properties of children material
                            aspect.SetRestitution(1.0f);
                            Assert.IsTrue(aspect.GetRestitution(keys[0]) == 1.0f);
                            Assert.IsTrue(aspect.GetCollisionResponse(keys[0]) == AspectTestUtils.Material1.CollisionResponse);
                            Assert.IsTrue(aspect.GetFriction(keys[0]) == AspectTestUtils.Material1.Friction);

                            Assert.IsTrue(aspect.GetRestitution(keys[1]) == 1.0f);
                            Assert.IsTrue(aspect.GetCollisionResponse(keys[1]) == AspectTestUtils.Material2.CollisionResponse);
                            Assert.IsTrue(aspect.GetFriction(keys[1]) == AspectTestUtils.Material2.Friction);

                            // Revert
                            aspect.SetRestitution(AspectTestUtils.Material1.Restitution, keys[0]);
                            aspect.SetRestitution(AspectTestUtils.Material2.Restitution, keys[1]);

                            // Make sure that they are properly set, and that the other properties remain untouched
                            Assert.IsTrue(aspect.GetRestitution(keys[0]) == AspectTestUtils.Material1.Restitution);
                            Assert.IsTrue(aspect.GetFriction(keys[0]) == AspectTestUtils.Material1.Friction);
                            Assert.IsTrue(aspect.GetCollisionResponse(keys[0]) == AspectTestUtils.Material1.CollisionResponse);

                            Assert.IsTrue(aspect.GetRestitution(keys[1]) == AspectTestUtils.Material2.Restitution);
                            Assert.IsTrue(aspect.GetFriction(keys[1]) == AspectTestUtils.Material2.Friction);
                            Assert.IsTrue(aspect.GetCollisionResponse(keys[1]) == AspectTestUtils.Material2.CollisionResponse);
                        }

                        // CollisionResponse
                        {
                            // Set root response, should not affect other properties of children material
                            aspect.SetCollisionResponse(CollisionResponsePolicy.RaiseTriggerEvents);
                            Assert.IsTrue(aspect.GetCollisionResponse(keys[0]) == CollisionResponsePolicy.RaiseTriggerEvents);
                            Assert.IsTrue(aspect.GetRestitution(keys[0]) == AspectTestUtils.Material1.Restitution);
                            Assert.IsTrue(aspect.GetFriction(keys[0]) == AspectTestUtils.Material1.Friction);

                            Assert.IsTrue(aspect.GetCollisionResponse(keys[1]) == CollisionResponsePolicy.RaiseTriggerEvents);
                            Assert.IsTrue(aspect.GetRestitution(keys[1]) == AspectTestUtils.Material2.Restitution);
                            Assert.IsTrue(aspect.GetFriction(keys[1]) == AspectTestUtils.Material2.Friction);

                            // Revert
                            aspect.SetCollisionResponse(AspectTestUtils.Material1.CollisionResponse, keys[0]);
                            aspect.SetCollisionResponse(AspectTestUtils.Material2.CollisionResponse, keys[1]);

                            // Make sure that they are properly set, and that the other properties remain untouched
                            Assert.IsTrue(aspect.GetCollisionResponse(keys[0]) == AspectTestUtils.Material1.CollisionResponse);
                            Assert.IsTrue(aspect.GetRestitution(keys[0]) == AspectTestUtils.Material1.Restitution);
                            Assert.IsTrue(aspect.GetFriction(keys[0]) == AspectTestUtils.Material1.Friction);

                            Assert.IsTrue(aspect.GetCollisionResponse(keys[1]) == AspectTestUtils.Material2.CollisionResponse);
                            Assert.IsTrue(aspect.GetRestitution(keys[1]) == AspectTestUtils.Material2.Restitution);
                            Assert.IsTrue(aspect.GetFriction(keys[1]) == AspectTestUtils.Material2.Friction);
                        }
                    }
                }
            }
        }

        [DisableAutoCreation]
        public partial class NestedCompoundAspectTestSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entity aspectEntity = SystemAPI.GetSingletonEntity<PhysicsCollider>();
                ColliderAspect aspect = SystemAPI.GetAspect<ColliderAspect>(aspectEntity);

                Assert.IsTrue(aspect.CollisionType == CollisionType.Composite);
                Assert.IsTrue(aspect.Type == ColliderType.Compound);

                unsafe
                {
                    // Expect 1 child, which is a compound
                    NativeHashMap<ColliderKey, ChildCollider> map = new NativeHashMap<ColliderKey, ChildCollider>(1, Allocator.Temp);
                    Assert.IsTrue(aspect.GetNumberOfChildren() == 1);
                    aspect.GetColliderKeyToChildrenMapping(ref map);
                    Assert.IsTrue(map.Count == 1);
                    Assert.IsTrue(map.GetKeyArray(Allocator.Temp)[0] == aspect.ConvertChildIndexToColliderKey(0));

                    // Get child and verify that it's a compound
                    Assert.IsTrue(map.TryGetValue(aspect.ConvertChildIndexToColliderKey(0), out ChildCollider child));
                    Assert.IsTrue(child.Collider->Type == ColliderType.Compound);

                    // Overlap with a big sphere
                    NativeList<DistanceHit> allHits = new NativeList<DistanceHit>(Allocator.Temp);
                    aspect.OverlapSphere(0.0f, 100.0f, ref allHits, CollisionFilter.Default, QueryInteraction.Default);

                    // Build the path to the convex child
                    ColliderKeyPath convexPath = new ColliderKeyPath(((CompoundCollider*)aspect.Collider.GetUnsafePtr())->ConvertChildIndexToColliderKey(0), ((CompoundCollider*)aspect.Collider.GetUnsafePtr())->NumColliderKeyBits);
                    convexPath.PushChildKey(new ColliderKeyPath(((CompoundCollider*)child.Collider)->ConvertChildIndexToColliderKey(0), ((CompoundCollider*)child.Collider)->NumColliderKeyBits));

                    bool hitTriangleAtLeastOnce = false;
                    bool hitConvex = false;
                    for (int i = 0; i < allHits.Length; i++)
                    {
                        DistanceHit hit = allHits[i];

                        // Here, we could be potentially getting mesh triangles as collider keys
                        // In that case, make sure that calling setter methods with those keys changes the parent collider (Mesh)
                        bool hitTriangle = hit.ColliderKey != convexPath.Key;
                        if (hitTriangle)
                        {
                            hitTriangleAtLeastOnce = true;
                            float frictionToSet = 0.75f;
                            aspect.SetFriction(frictionToSet, hit.ColliderKey);

                            // Assert that we have only set the friction on a mesh
                            CompoundCollider* childCompound = (CompoundCollider*)child.Collider;
                            var meshFriction = child.Collider->GetFriction(childCompound->ConvertChildIndexToColliderKey(1));
                            Assert.IsTrue(frictionToSet == meshFriction);

                            // Also, make sure we didn't touch the friction of the other collider (Convex)
                            Assert.IsTrue(child.Collider->GetFriction(childCompound->ConvertChildIndexToColliderKey(0)) == AspectTestUtils.Material1.Friction);

                            // Revert
                            aspect.SetFriction(AspectTestUtils.Material2.Friction, hit.ColliderKey);
                        }
                        else
                        {
                            // We have hit a convex
                            hitConvex = true;
                            float frictionToSet = 0.50f;

                            aspect.SetFriction(frictionToSet, hit.ColliderKey);

                            // Assert that we have only set the friction on a convex
                            CompoundCollider* childCompound = (CompoundCollider*)child.Collider;
                            var convexFriction = child.Collider->GetFriction(childCompound->ConvertChildIndexToColliderKey(0));
                            Assert.IsTrue(frictionToSet == convexFriction);

                            // Also, make sure we didn't touch the friction of the other collider (Mesh)
                            Assert.IsTrue(child.Collider->GetFriction(childCompound->ConvertChildIndexToColliderKey(1)) == AspectTestUtils.Material2.Friction);

                            // Revert
                            aspect.SetFriction(AspectTestUtils.Material1.Friction, hit.ColliderKey);
                        }
                    }

                    // make sure the test is working
                    Assert.IsTrue(hitTriangleAtLeastOnce);
                    Assert.IsTrue(hitConvex);
                }
            }
        }

        [DisableAutoCreation]
        public partial class ColliderAspectQueryTestSystem : SystemBase
        {
            protected unsafe override void OnUpdate()
            {
                Entity aspectEntity = SystemAPI.GetSingletonEntity<PhysicsCollider>();
                ColliderAspect aspect = GetAspect<ColliderAspect>(aspectEntity);

                // Check that self-filtering works
                {
                    PhysicsWorld world = new PhysicsWorld(1, 0, 0);
                    NativeArray<RigidBody> bodies = world.StaticBodies;

                    bodies[0] = new RigidBody
                    {
                        Collider = aspect.Collider,
                        CustomTags = 0,
                        Scale = aspect.Scale,
                        Entity = aspect.Entity,
                        WorldFromBody = new RigidTransform(aspect.WorldFromCollider.Rotation, aspect.WorldFromCollider.Position)
                    };

                    world.CollisionWorld.Broadphase.Build(world.StaticBodies, world.DynamicBodies, world.MotionVelocities,
                        world.CollisionWorld.CollisionTolerance, 1.0f, -9.81f * math.up());

                    Assert.IsFalse(world.CalculateDistance(aspect, 100.0f, QueryInteraction.Default));

                    world.Dispose();
                }

                // Queries against a world, compare results
                {
                    Random rnd = new Random(56789);
                    PhysicsWorld world = TestUtils.GenerateRandomWorld(ref rnd, 50, 10, 1);

                    // CalculateDistance with aspect
                    {
                        NativeList<DistanceHit> allHitsAspect = new NativeList<DistanceHit>(Allocator.Temp);
                        NativeList<DistanceHit> allHits = new NativeList<DistanceHit>(Allocator.Temp);

                        world.CalculateDistance(aspect, 100.0f, ref allHitsAspect, QueryInteraction.Default);
                        ColliderDistanceInput input = new ColliderDistanceInput() { Collider = (Collider*)aspect.Collider.GetUnsafePtr(), MaxDistance = 100.0f, Scale = aspect.Scale, Transform = new RigidTransform(aspect.Rotation, aspect.Position) };
                        world.CalculateDistance(input, ref allHits);

                        Assert.IsTrue(allHits.Length > 0);
                        Assert.IsTrue(allHits.Length == allHitsAspect.Length);

                        for (int i = 0; i < allHits.Length; i++)
                        {
                            DistanceHit hit = allHits[i];
                            DistanceHit aspectHit = allHitsAspect[i];

                            Assert.IsTrue(hit.Entity == aspectHit.Entity);
                            Assert.IsTrue(hit.RigidBodyIndex == aspectHit.RigidBodyIndex);
                            Assert.IsTrue(hit.ColliderKey == aspectHit.ColliderKey);
                            Assert.IsTrue(hit.QueryColliderKey == aspectHit.QueryColliderKey);
                            Assert.IsTrue(hit.Distance == aspectHit.Distance);
                            Assert.IsTrue(hit.Fraction == aspectHit.Fraction);
                            Assert.IsTrue(hit.Material.Equals(aspectHit.Material));
                            Assert.IsTrue(math.all(hit.Position == aspectHit.Position));
                            Assert.IsTrue(math.all(hit.SurfaceNormal == aspectHit.SurfaceNormal));
                        }
                    }

                    // CastCollider with aspect
                    {
                        NativeList<ColliderCastHit> allHitsAspect = new NativeList<ColliderCastHit>(Allocator.Temp);
                        NativeList<ColliderCastHit> allHits = new NativeList<ColliderCastHit>(Allocator.Temp);

                        world.CastCollider(aspect, math.up(), 100.0f, ref allHitsAspect, QueryInteraction.Default);
                        ColliderCastInput input = new ColliderCastInput(aspect.Collider, aspect.WorldFromCollider.Position, aspect.WorldFromCollider.Position + math.up() * 100.0f, aspect.WorldFromCollider.Rotation, aspect.Scale);
                        world.CastCollider(input, ref allHits);

                        Assert.IsTrue(allHits.Length > 0);
                        Assert.IsTrue(allHits.Length == allHitsAspect.Length);

                        for (int i = 0; i < allHits.Length; i++)
                        {
                            ColliderCastHit hit = allHits[i];
                            ColliderCastHit aspectHit = allHitsAspect[i];

                            Assert.IsTrue(hit.Entity == aspectHit.Entity);
                            Assert.IsTrue(hit.RigidBodyIndex == aspectHit.RigidBodyIndex);
                            Assert.IsTrue(hit.ColliderKey == aspectHit.ColliderKey);
                            Assert.IsTrue(hit.QueryColliderKey == aspectHit.QueryColliderKey);
                            Assert.IsTrue(hit.Fraction == aspectHit.Fraction);
                            Assert.IsTrue(hit.Material.Equals(aspectHit.Material));
                            Assert.IsTrue(math.all(hit.Position == aspectHit.Position));
                            Assert.IsTrue(math.all(hit.SurfaceNormal == aspectHit.SurfaceNormal));
                        }
                    }

                    TestUtils.DisposeAllColliderBlobs(ref world);
                    world.Dispose();
                }
            }
        }
    }
}
