#if UNITY_ANDROID && !UNITY_64
#define UNITY_ANDROID_ARM7V
#endif

using NUnit.Framework;
using Unity.Mathematics;
using Unity.Collections;
using Random = Unity.Mathematics.Random;
using static Unity.Physics.Math;
using Unity.Physics.Tests.Utils;

namespace Unity.Physics.Tests.Collision.Queries
{
    class QueryTests
    {
        // These tests mostly work by comparing the results of different methods of calculating the same thing.
        // The results will not be exactly the same due to floating point inaccuracy, approximations in methods like convex-convex collider cast, etc.
        // Collider cast conservative advancement is accurate to 1e-3, so this tolerance must be at least a bit higher than that to allow for other
        // sources of error on top of it.
        const float tolerance = 1.1e-3f;

        //
        // Query result validation
        //

        static unsafe float3 getSupport(ref ConvexHull hull, float3 direction)
        {
            float4 best = new float4(hull.Vertices[0], math.dot(hull.Vertices[0], direction));
            for (int i = 1; i < hull.Vertices.Length; i++)
            {
                float dot = math.dot(hull.Vertices[i], direction);
                best = math.select(best, new float4(hull.Vertices[i], dot), dot > best.w);
            }
            return best.xyz;
        }

        static void ValidateDistanceResult(DistanceQueries.Result result, ref ConvexHull a, ref ConvexHull b, ScaledMTransform aFromB, float referenceDistance, float queryFromWorldScale, string failureMessage)
        {
            // Calculate the support distance along the separating normal
            float3 tempA = getSupport(ref a, -result.NormalInA);
            float3 supportQuery = tempA - result.NormalInA * a.ConvexRadius;
            float3 tempB = Mul(aFromB, getSupport(ref b, math.mul(aFromB.InverseRotation, math.sign(aFromB.Scale) * result.NormalInA)));

            float absScale = math.abs(aFromB.Scale);
            float3 supportTarget = tempB + result.NormalInA * b.ConvexRadius * absScale;
            float supportQueryDot = math.dot(supportQuery, result.NormalInA);
            float supportTargetDot = math.dot(supportTarget, result.NormalInA);
            float supportDistance = supportQueryDot - supportTargetDot;

            // Increase the tolerance in case of core penetration
            float adjustedTolerance = tolerance;
            float zeroCoreDistance = -a.ConvexRadius - b.ConvexRadius * absScale; // Distance of shapes including radius at which core shapes are penetrating
            if (result.Distance < zeroCoreDistance || referenceDistance < zeroCoreDistance || supportDistance < zeroCoreDistance)
            {
                // Core shape penetration distances are less accurate, and error scales with the number of vertices (as well as shape size). See stopThreshold in ConvexConvexDistanceQueries.
                // This is not usually noticeable in rigid body simulation because accuracy improves as the penetration resolves.
                // The tolerance is tuned for these tests, it might require further tuning as the tests change.
                adjustedTolerance = 1e-2f + 1e-3f * (a.NumVertices + b.NumVertices);
            }

            float toleranceInQuerySpace = adjustedTolerance * math.abs(queryFromWorldScale);

            // Check that the distance is consistent with the reference distance
            Assert.AreEqual(result.Distance, referenceDistance, toleranceInQuerySpace, failureMessage + ": incorrect distance");

            // Check that the separating normal and closest point are consistent with the distance
            Assert.AreEqual(result.Distance, supportDistance, toleranceInQuerySpace, failureMessage + ": incorrect normal");
            float positionDot = math.dot(result.PositionOnAinA, result.NormalInA);
            Assert.AreEqual(supportQueryDot, positionDot, toleranceInQuerySpace, failureMessage + ": incorrect position");
        }

        //
        // Reference implementations of queries using simple brute-force methods
        //

        static unsafe float RefConvexConvexDistance(ref ConvexHull a, ref ConvexHull b, MTransform aFromB)
        {
            bool success = false;
            if (a.NumVertices + b.NumVertices < 64) // too slow without burst
            {
                // Build the minkowski difference in a-space
                int maxNumVertices = a.NumVertices * b.NumVertices;
                Aabb aabb = Aabb.Empty;
                for (int iB = 0; iB < b.NumVertices; iB++)
                {
                    float3 vertexB = Math.Mul(aFromB, b.Vertices[iB]);
                    for (int iA = 0; iA < a.NumVertices; iA++)
                    {
                        float3 vertexA = a.Vertices[iA];
                        aabb.Include(vertexA - vertexB);
                    }
                }
                ConvexHullBuilderStorage diffStorage = new ConvexHullBuilderStorage(maxNumVertices, Allocator.Temp, aabb, 0.0f, ConvexHullBuilder.IntResolution.Low);
                ref ConvexHullBuilder diff = ref diffStorage.Builder;
                success = true;
                for (int iB = 0; iB < b.NumVertices; iB++)
                {
                    float3 vertexB = Math.Mul(aFromB, b.Vertices[iB]);
                    for (int iA = 0; iA < a.NumVertices; iA++)
                    {
                        float3 vertexA = a.Vertices[iA];
                        diff.AddPoint(vertexA - vertexB, (uint)(iA | iB << 16));
                    }
                }

                float distance = 0.0f;
                if (success && diff.Dimension == 3)
                {
                    // Find the closest triangle to the origin
                    distance = float.MaxValue;
                    bool penetrating = true;
                    foreach (int t in diff.Triangles.Indices)
                    {
                        ConvexHullBuilder.Triangle triangle = diff.Triangles[t];
                        float3 v0 = diff.Vertices[triangle.GetVertex(0)].Position;
                        float3 v1 = diff.Vertices[triangle.GetVertex(1)].Position;
                        float3 v2 = diff.Vertices[triangle.GetVertex(2)].Position;
                        float3 n = diff.ComputePlane(t).Normal;
                        DistanceQueries.Result result = DistanceQueries.TriangleSphere(v0, v1, v2, n, float3.zero, 0.0f, MTransform.Identity);
                        if (result.Distance < distance)
                        {
                            distance = result.Distance;
                        }
                        penetrating = penetrating & (math.dot(n, -result.NormalInA) < 0.0f); // only penetrating if inside of all planes
                    }

                    if (penetrating)
                    {
                        distance = -distance;
                    }

                    distance -= a.ConvexRadius + b.ConvexRadius;
                }
                else
                {
                    success = false;
                }

                diffStorage.Dispose();

                if (success)
                {
                    return distance;
                }
            }

            // Fall back in case hull isn't 3D or hull builder fails
            // Most of the time this happens for cases like sphere-sphere, capsule-capsule, etc. which have special implementations,
            // so comparing those to GJK still validates the results of different API queries against each other.
            return DistanceQueries.ConvexConvex(ref a, ref b, aFromB).Distance;
        }

        private static unsafe void TestConvexConvexDistance(ConvexCollider* target, ConvexCollider* query, MTransform queryFromTarget, string failureMessage)
        {
            // Do the query, API version and reference version, then validate the result
            DistanceQueries.Result result = DistanceQueries.ConvexConvex((Collider*)query, (Collider*)target, queryFromTarget);
            float referenceDistance = RefConvexConvexDistance(ref query->ConvexHull, ref target->ConvexHull, queryFromTarget);
            ValidateDistanceResult(result, ref query->ConvexHull, ref target->ConvexHull, new ScaledMTransform(queryFromTarget, 1.0f), referenceDistance, 1.0f, failureMessage);
        }

        // This test generates random shape pairs, queries the distance between them, and validates some properties of the results:
        // - Distance is compared against a slow reference implementation of the closest distance query
        // - Closest points are on the plane through the support vertices in the normal direction
        // If the test fails, it will report a seed.  Set dbgTest to the seed to run the failing case alone.
        [Test]
        public unsafe void ConvexConvexDistanceTest()
        {
            Random rnd = new Random(0x12345678);
            uint dbgTest = 0;

            int numTests = 5000;
            if (dbgTest > 0)
            {
                numTests = 1;
            }

            for (int i = 0; i < numTests; i++)
            {
                // Save state to repro this query without doing everything that came before it
                if (dbgTest > 0)
                {
                    rnd.state = dbgTest;
                }
                uint state = rnd.state;

                // Generate random query inputs
                using var target = TestUtils.GenerateRandomConvex(ref rnd, 1.0f);
                using var query = TestUtils.GenerateRandomConvex(ref rnd, 1.0f);
                MTransform queryFromTarget = new MTransform(
                    (rnd.NextInt(10) > 0) ? rnd.NextQuaternionRotation() : quaternion.identity,
                    rnd.NextFloat3(-3.0f, 3.0f));
                TestConvexConvexDistance((ConvexCollider*)target.GetUnsafePtr(), (ConvexCollider*)query.GetUnsafePtr(), queryFromTarget, "ConvexConvexDistanceTest failed " + i + " (" + state.ToString() + ")");
            }
        }

        // This test generates random shapes and queries distance to themselves using small or identity transforms.  This hits edge
        // cases in collision detection routines where the two shapes being tested have equal or nearly equal features.  The results
        // are validated in the same way as those in ConvexConvexDistanceTest().
        // If the test fails, it will report a pair of seeds.  Set dbgShape to the first and dbgTest to the second to run the failing case alone.
        [Test]
        [Timeout(600000)]
        public unsafe void ConvexConvexDistanceEdgeCaseTest()
        {
            Random rnd = new Random(0x90456148);
            uint dbgShape = 0;
            uint dbgTest = 0;

            int numShapes = 500;
            int numTests = 50;
            if (dbgShape > 0)
            {
                numShapes = 1;
                numTests = 1;
            }

            for (int iShape = 0; iShape < numShapes; iShape++)
            {
                if (dbgShape > 0)
                {
                    rnd.state = dbgShape;
                }
                uint shapeState = rnd.state;

                // Generate a random collider
                using var collider = TestUtils.GenerateRandomConvex(ref rnd, 1.0f);

                for (int iTest = 0; iTest < numTests; iTest++)
                {
                    if (dbgTest > 0)
                    {
                        rnd.state = dbgTest;
                    }
                    uint testState = rnd.state;

                    // Generate random transform
                    float distance = math.pow(10.0f, rnd.NextFloat(-15.0f, -1.0f));
                    float angle = math.pow(10.0f, rnd.NextFloat(-15.0f, 0.0f));
                    MTransform queryFromTarget = new MTransform(
                        (rnd.NextInt(10) > 0) ? quaternion.AxisAngle(rnd.NextFloat3Direction(), angle) : quaternion.identity,
                        (rnd.NextInt(10) > 0) ? rnd.NextFloat3Direction() * distance : float3.zero);
                    TestConvexConvexDistance((ConvexCollider*)collider.GetUnsafePtr(), (ConvexCollider*)collider.GetUnsafePtr(), queryFromTarget, "ConvexConvexDistanceEdgeCaseTest failed " + iShape + ", " + iTest + " (" + shapeState + ", " + testState + ")");
                }
            }
        }

        static DistanceQueries.Result DistanceResultFromDistanceHit(DistanceHit hit, ScaledMTransform queryFromWorld)
        {
            return new DistanceQueries.Result
            {
                PositionOnAinA = Mul(queryFromWorld, hit.Position + hit.SurfaceNormal * hit.Distance),
                NormalInA = math.mul(queryFromWorld.Rotation, math.sign(queryFromWorld.Scale) * hit.SurfaceNormal),
                Distance = hit.Distance * math.abs(queryFromWorld.Scale)
            };
        }

        static unsafe void GetQueryLeaf(Collider* root, ColliderKey colliderKey, RigidTransform worldFromRoot, float rootScale, out ScaledMTransform leafFromWorld, out ChildCollider leaf)
        {
            Collider.GetLeafCollider(out leaf, root, colliderKey, worldFromRoot, rootScale);
            leafFromWorld = Inverse(new ScaledMTransform(leaf.TransformFromChild, rootScale));
        }

        static unsafe void GetHitLeaf(ref Physics.PhysicsWorld world, int rigidBodyIndex, ColliderKey colliderKey, ScaledMTransform queryFromWorld, out ChildCollider leaf, out ScaledMTransform queryFromTarget)
        {
            Physics.RigidBody body = world.Bodies[rigidBodyIndex];
            Collider.GetLeafCollider(out leaf, (Collider*)body.Collider.GetUnsafePtr(), colliderKey, body.WorldFromBody, body.Scale);
            ScaledMTransform worldFromLeaf = new ScaledMTransform(leaf.TransformFromChild, body.Scale);
            queryFromTarget = Mul(queryFromWorld, worldFromLeaf);
        }

        // Does distance queries and checks some properties of the results:
        // - Closest hit returned from the all hits query has the same fraction as the hit returned from the closest hit query
        // - Any hit and closest hit queries return a hit if and only if the all hits query does
        // - Hit distance is the same as the support distance in the hit normal direction
        // - Fetching the shapes from any world query hit and querying them directly gives a matching result
        static unsafe void WorldCalculateDistanceTest(ref Physics.PhysicsWorld world, ColliderDistanceInput input, ref NativeList<DistanceHit> hits, string failureMessage)
        {
            // Do an all-hits query
            hits.Clear();
            world.CalculateDistance(input, ref hits);

            // Check each hit and find the closest
            float closestDistance = float.MaxValue;
            ScaledMTransform queryFromWorld = Inverse(new ScaledMTransform(input.Transform, input.Scale));

            for (int iHit = 0; iHit < hits.Length; iHit++)
            {
                DistanceHit hit = hits[iHit];
                closestDistance = math.min(closestDistance, hit.Distance);

                // Fetch leaf collider of query shape
                GetQueryLeaf(input.Collider, hit.QueryColliderKey, input.Transform, input.Scale, out ScaledMTransform queryLeafFromWorld, out ChildCollider queryLeaf);

                // Fetch leaf collider of target shape and query it directly
                GetHitLeaf(ref world, hit.RigidBodyIndex, hit.ColliderKey, queryLeafFromWorld, out ChildCollider targetLeaf, out ScaledMTransform queryFromTarget);
                float referenceDistance = DistanceQueries.ConvexConvex(queryLeaf.Collider, targetLeaf.Collider, queryFromTarget.Transform, queryFromTarget.Scale).Distance;

                // Compare to the world query result
                DistanceQueries.Result result = DistanceResultFromDistanceHit(hit, queryLeafFromWorld);

                ValidateDistanceResult(result, ref ((ConvexCollider*)queryLeaf.Collider)->ConvexHull, ref ((ConvexCollider*)targetLeaf.Collider)->ConvexHull, queryFromTarget, referenceDistance,
                    queryLeafFromWorld.Scale, failureMessage + ", hits[" + iHit + "]");
            }

            //Do a closest - hit query and check that the distance matches
            DistanceHit closestHit;
            bool hasClosestHit = world.CalculateDistance(input, out closestHit);
            if (hits.Length == 0)
            {
                Assert.IsFalse(hasClosestHit, failureMessage + ", closestHit: no matching result in hits");
            }
            else
            {
                // Fetch leaf collider of query shape
                GetQueryLeaf(input.Collider, closestHit.QueryColliderKey, input.Transform, input.Scale, out ScaledMTransform queryLeafFromWorld, out ChildCollider queryLeaf);

                // Fetch leaf collider of target shape
                GetHitLeaf(ref world, closestHit.RigidBodyIndex, closestHit.ColliderKey, queryLeafFromWorld, out ChildCollider leaf, out ScaledMTransform queryFromTarget);

                DistanceQueries.Result result = DistanceResultFromDistanceHit(closestHit, queryLeafFromWorld);

                // Transform distance into query space
                float scaledClosestDistance = closestDistance * math.abs(queryLeafFromWorld.Scale);
                ValidateDistanceResult(result, ref ((ConvexCollider*)queryLeaf.Collider)->ConvexHull, ref ((ConvexCollider*)leaf.Collider)->ConvexHull, queryFromTarget, scaledClosestDistance,
                    queryLeafFromWorld.Scale, failureMessage + ", closestHit");
            }

            // Do an any-hit query and check that it is consistent with the others
            bool hasAnyHit = world.CalculateDistance(input);
            Assert.AreEqual(hasAnyHit, hasClosestHit, failureMessage + ": any hit result inconsistent with the others");

            // TODO - this test can't catch false misses.  We could do brute-force broadphase / midphase search to cover those.
        }

        static unsafe void CheckColliderCastHit(ref Physics.PhysicsWorld world, ColliderCastInput input, ColliderCastHit hit, string failureMessage)
        {
            ScaledMTransform worldFromQuery = new ScaledMTransform(new RigidTransform(input.Orientation, math.lerp(input.Start, input.End, hit.Fraction)), input.QueryColliderScale);

            // Fetch input leaf collider
            GetQueryLeaf(input.Collider, hit.QueryColliderKey, new RigidTransform(worldFromQuery.Rotation, worldFromQuery.Translation), input.QueryColliderScale, out ScaledMTransform queryLeafFromWorld, out ChildCollider queryLeaf);

            // Fetch the leaf collider and convert the shape cast result into a distance result at the hit transform
            GetHitLeaf(ref world, hit.RigidBodyIndex, hit.ColliderKey, queryLeafFromWorld, out ChildCollider targetLeaf, out ScaledMTransform queryFromTarget);

            DistanceQueries.Result result = new DistanceQueries.Result
            {
                PositionOnAinA = Mul(queryLeafFromWorld, hit.Position),
                NormalInA = math.mul(queryLeafFromWorld.Rotation, math.sign(queryLeafFromWorld.Scale) * hit.SurfaceNormal),
                Distance = 0.0f
            };

            //If the fraction is zero then the shapes should penetrate, otherwise they should have zero distance
            if (hit.Fraction == 0.0f)
            {
                // Do a distance query to verify initial penetration
                result.Distance = DistanceQueries.ConvexConvex(queryLeaf.Collider, targetLeaf.Collider, queryFromTarget.Transform, queryFromTarget.Scale).Distance;
                Assert.Less(result.Distance, tolerance * math.abs(queryLeafFromWorld.Scale), failureMessage + ": zero fraction with positive distance");
            }

            // Verify the distance at the hit transform
            ValidateDistanceResult(result, ref ((ConvexCollider*)queryLeaf.Collider)->ConvexHull, ref ((ConvexCollider*)targetLeaf.Collider)->ConvexHull, queryFromTarget, result.Distance, queryLeafFromWorld.Scale, failureMessage);
        }

        // Does collider casts and checks some properties of the results:
        // - Closest hit returned from the all hits query has the same fraction as the hit returned from the closest hit query
        // - Any hit and closest hit queries return a hit if and only if the all hits query does
        // - Distance between the shapes at the hit fraction is zero
        static unsafe void WorldColliderCastTest(ref Physics.PhysicsWorld world, ColliderCastInput input, ref NativeList<ColliderCastHit> hits, string failureMessage)
        {
            // Do an all-hits query
            hits.Clear();
            world.CastCollider(input, ref hits);

            // Check each hit and find the earliest
            float minFraction = float.MaxValue;
            for (int iHit = 0; iHit < hits.Length; iHit++)
            {
                ColliderCastHit hit = hits[iHit];
                minFraction = math.min(minFraction, hit.Fraction);

                CheckColliderCastHit(ref world, input, hit, failureMessage + ", hits[" + iHit + "]");
            }

            //Do a closest - hit query and check that the fraction matches
            ColliderCastHit closestHit;
            bool hasClosestHit = world.CastCollider(input, out closestHit);
            if (hits.Length == 0)
            {
                Assert.IsFalse(hasClosestHit, failureMessage + ", closestHit: no matching result in hits");
            }
            else
            {
                Assert.AreEqual(closestHit.Fraction, minFraction, tolerance * math.length(input.Ray.Displacement), failureMessage + ", closestHit: fraction does not match");
                CheckColliderCastHit(ref world, input, closestHit, failureMessage + ", closestHit");
            }

            // Do an any-hit query and check that it is consistent with the others
            bool hasAnyHit = world.CastCollider(input);
            Assert.AreEqual(hasAnyHit, hasClosestHit, failureMessage + ": any hit result inconsistent with the others");

            // TODO - this test can't catch false misses.  We could do brute-force broadphase / midphase search to cover those.
        }

        static unsafe void CheckRaycastHit(ref Physics.PhysicsWorld world, RaycastInput input, RaycastHit hit, string failureMessage)
        {
            // Fetch the leaf collider
            Physics.RigidBody body = world.Bodies[hit.RigidBodyIndex];
            ChildCollider leaf;
            Collider.GetLeafCollider(out leaf, (Collider*)body.Collider.GetUnsafePtr(), hit.ColliderKey, body.WorldFromBody, body.Scale);

            // Check that the hit position matches the fraction
            float3 hitPosition = math.lerp(input.Start, input.End, hit.Fraction);
            Assert.Less(math.length(hitPosition - hit.Position), tolerance, failureMessage + ": inconsistent fraction and position");

            Math.ScaledMTransform worldFromChild = new ScaledMTransform(leaf.TransformFromChild, body.Scale);
            var childFromWorld = Inverse(worldFromChild);

            // Query the hit position and check that it's on the surface of the shape
            PointDistanceInput pointInput = new PointDistanceInput
            {
                Position = Mul(childFromWorld, hit.Position),
                MaxDistance = float.MaxValue,
                Filter = CollisionFilter.Default
            };

            DistanceHit distanceHit;

            // Validate that the hit happens
            Assert.IsTrue(leaf.Collider->CalculateDistance(pointInput, out distanceHit), failureMessage + ": hit not detected");

            if (hit.Fraction != 0)
            {
                if (((ConvexCollider*)leaf.Collider)->ConvexHull.ConvexRadius > 0.0f)
                {
                    // Convex raycast approximates radius, so it's possible that the hit position is not exactly on the shape, but must at least be outside
                    Assert.Greater(distanceHit.Distance * math.abs(body.Scale), -tolerance, failureMessage);
                }
                else
                {
                    Assert.AreEqual(distanceHit.Distance * math.abs(body.Scale), 0.0f, tolerance, failureMessage);
                }
            }
        }

        // Does raycasts and checks some properties of the results:
        // - Closest hit returned from the all hits query has the same fraction as the hit returned from the closest hit query
        // - Any hit and closest hit queries return a hit if and only if the all hits query does
        // - All hits are on the surface of the hit shape
        static unsafe void WorldRaycastTest(ref Physics.PhysicsWorld world, RaycastInput input, ref NativeList<RaycastHit> hits, string failureMessage)
        {
            // Do an all-hits query
            hits.Clear();
            world.CastRay(input, ref hits);

            // Check each hit and find the earliest
            float minFraction = float.MaxValue;
            for (int iHit = 0; iHit < hits.Length; iHit++)
            {
                RaycastHit hit = hits[iHit];
                minFraction = math.min(minFraction, hit.Fraction);
                CheckRaycastHit(ref world, input, hit, failureMessage + ", hits[" + iHit + "]");
            }

            // Do a closest-hit query and check that the fraction matches
            RaycastHit closestHit;
            bool hasClosestHit = world.CastRay(input, out closestHit);
            if (hits.Length == 0)
            {
                Assert.IsFalse(hasClosestHit, failureMessage + ", closestHit: no matching result in hits");
            }
            else
            {
                Assert.AreEqual(closestHit.Fraction, minFraction, tolerance * math.length(input.Ray.Displacement), failureMessage + ", closestHit: fraction does not match");
                CheckRaycastHit(ref world, input, closestHit, failureMessage + ", closestHit");
            }

            // Do an any-hit query and check that it is consistent with the others
            bool hasAnyHit = world.CastRay(input);
            Assert.AreEqual(hasAnyHit, hasClosestHit, failureMessage + ": any hit result inconsistent with the others");

            // TODO - this test can't catch false misses.  We could do brute-force broadphase / midphase search to cover those.
        }

        // This test generates random worlds, queries them, and validates some properties of the query results.
        // See WorldCalculateDistanceTest, WorldColliderCastTest, and WorldRaycastTest for details about each query.
        // If the test fails, it will report a pair of seeds.  Set dbgWorld to the first and dbgTest to the second to run the failing case alone.
        [Test]
#if UNITY_ANDROID_ARM7V || UNITY_IOS
        [Ignore("This test causes out of memory crashes on armv7 builds, due to the memory restrictions on such devices.")]
#endif
        [Timeout(600000)]
        public unsafe void WorldQueryTest()
        {
            // todo.papopov: switch the seed back to 0x12345678 when [UNI-281] is resolved
            const uint seed = 0x12345677;
            uint dbgWorld = 0; // set dbgWorld, dbgTest to the seed reported from a failure message to repeat the failing case alone
            uint dbgTest = 0;

#if UNITY_ANDROID || UNITY_IOS
            int numWorlds = 50;
            int numTests = 1250;
#else
            int numWorlds = 200;
            int numTests = 5000;
#endif
            if (dbgWorld > 0)
            {
                numWorlds = 1;
                numTests = 1;
            }

            Random rnd = new Random(seed);
            NativeList<DistanceHit> distanceHits = new NativeList<DistanceHit>(Allocator.Temp);
            NativeList<ColliderCastHit> colliderCastHits = new NativeList<ColliderCastHit>(Allocator.Temp);
            NativeList<RaycastHit> raycastHits = new NativeList<RaycastHit>(Allocator.Temp);

            System.Collections.Generic.List<int> numThreadsHints = new System.Collections.Generic.List<int>(5);
            numThreadsHints.Add(-1);
            numThreadsHints.Add(0);
            numThreadsHints.Add(1);
            foreach (int numThreadsHint in numThreadsHints)
            {
                for (int iWorld = 0; iWorld < numWorlds; iWorld++)
                {
                    // Save state to repro this query without doing everything that came before it
                    if (dbgWorld > 0)
                    {
                        rnd.state = dbgWorld;
                    }
                    uint worldState = rnd.state;
                    Physics.PhysicsWorld world = TestUtils.GenerateRandomWorld(ref rnd, rnd.NextInt(1, 20), 10.0f, numThreadsHint);

                    for (int iTest = 0; iTest < (numTests / numWorlds); iTest++)
                    {
                        if (dbgTest > 0)
                        {
                            rnd.state = dbgTest;
                        }
                        uint testState = rnd.state;
                        string failureMessage = iWorld + ", " + iTest + " (" + worldState.ToString() + ", " + testState.ToString() + ")";

                        // Generate common random query inputs, including composite/terrain colliders

                        // Create colliders with 1 / queryInputScale, so that the combination of the two leads to totalScale being 1
                        float queryInputScale = rnd.NextBool() ? 1 : math.pow(10, rnd.NextFloat(-2f, 2f));
                        queryInputScale = queryInputScale > 1.0f ? 1.0f / queryInputScale : queryInputScale;
                        float colliderCreationScale = 1.0f / queryInputScale;
                        if (queryInputScale != 1.0f)
                        {
                            queryInputScale = rnd.NextBool() ? queryInputScale : -queryInputScale;
                        }

                        using var collider = TestUtils.GenerateRandomCollider(ref rnd, colliderCreationScale);
                        RigidTransform transform = new RigidTransform
                        {
                            pos = rnd.NextFloat3(-10.0f, 10.0f),
                            rot = (rnd.NextInt(10) > 0) ? rnd.NextQuaternionRotation() : quaternion.identity,
                        };
                        var startPos = transform.pos;
                        var endPos = startPos + rnd.NextFloat3(-5.0f, 5.0f);

                        // Distance test
                        {
                            ColliderDistanceInput input = new ColliderDistanceInput
                            {
                                Collider = (Collider*)collider.GetUnsafePtr(),
                                Transform = transform,
                                MaxDistance = (rnd.NextInt(4) > 0) ? rnd.NextFloat(5.0f) : 0.0f,
                                Scale = queryInputScale
                            };
                            WorldCalculateDistanceTest(ref world, input, ref distanceHits, "WorldQueryTest failed CalculateDistance " + failureMessage);
                        }

                        // Collider cast test
                        {
                            ColliderCastInput input = new ColliderCastInput
                            {
                                Collider = (Collider*)collider.GetUnsafePtr(),
                                Start = startPos,
                                End = endPos,
                                Orientation = transform.rot,
                                QueryColliderScale = queryInputScale
                            };
                            WorldColliderCastTest(ref world, input, ref colliderCastHits, "WorldQueryTest failed ColliderCast " + failureMessage);
                        }

                        // Ray cast test
                        {
                            RaycastInput input = new RaycastInput
                            {
                                Start = startPos,
                                End = endPos,
                                Filter = CollisionFilter.Default
                            };
                            WorldRaycastTest(ref world, input, ref raycastHits, "WorldQueryTest failed Raycast " + failureMessage);
                        }
                    }

                    TestUtils.DisposeAllColliderBlobs(ref world);
                    world.Dispose(); // TODO leaking memory if the test fails
                }
            }

            distanceHits.Dispose(); // TODO leaking memory if the test fails
            colliderCastHits.Dispose();
            raycastHits.Dispose();
        }

        // Tests that a contact point is on the surface of its shape
        static unsafe void CheckPointOnSurface(ref ChildCollider leaf, float3 position, string failureMessage)
        {
            float3 positionLocal = math.transform(math.inverse(leaf.TransformFromChild), position);
            leaf.Collider->CalculateDistance(new PointDistanceInput { Position = positionLocal, MaxDistance = float.MaxValue, Filter = Physics.CollisionFilter.Default }, out DistanceHit hit);
            Assert.Less(hit.Distance, tolerance, failureMessage + ": contact point outside of shape");
            Assert.Greater(hit.Distance, -((ConvexCollider*)leaf.Collider)->ConvexHull.ConvexRadius - tolerance, failureMessage + ": contact point inside of shape");
        }

        // Tests that the points of a manifold are all coplanar
        static unsafe void CheckManifoldFlat(ref ConvexConvexManifoldQueries.Manifold manifold, float3 normal, string failureMessage)
        {
            float3 point0 = manifold[0].Position + normal * manifold[0].Distance;
            float3 point1 = manifold[1].Position + normal * manifold[1].Distance;
            for (int i = 2; i < manifold.NumContacts; i++)
            {
                // Try to calculate a plane from points 0, 1, iNormal
                float3 point = manifold[i].Position + normal * manifold[i].Distance;
                float3 cross = math.cross(point - point0, point - point1);
                if (math.lengthsq(cross) > 1e-6f)
                {
                    // Test that each point in the manifold is on the plane
                    float3 faceNormal = math.normalize(cross);
                    float dot = math.dot(point0, faceNormal);
                    for (int j = 2; j < manifold.NumContacts; j++)
                    {
                        float3 testPoint = manifold[j].Position + normal * manifold[j].Distance;
                        Assert.AreEqual(dot, math.dot(faceNormal, testPoint), tolerance, failureMessage + " contact " + j);
                    }
                    break;
                }
            }
        }

        // This test generates random worlds, generates manifolds for every pair of bodies in the world, and validates some properties of the manifolds:
        // - should contain the closest point
        // - each body's contact points should be on that body's surface
        // - each body's contact points should all be coplanar
        // If the test fails, it will report a seed.  Set dbgWorld to that seed to run the failing case alone.
        [Test]
        public unsafe void ManifoldQueryTest()
        {
            const uint seed = 0x98765432;
            Random rnd = new Random(seed);
            int numWorlds = 1000;

            uint dbgWorld = 0;
            if (dbgWorld > 0)
            {
                numWorlds = 1;
            }

            for (int iWorld = 0; iWorld < numWorlds; iWorld++)
            {
                // Save state to repro this query without doing everything that came before it
                if (dbgWorld > 0)
                {
                    rnd.state = dbgWorld;
                }
                uint worldState = rnd.state;
                Physics.PhysicsWorld world = TestUtils.GenerateRandomWorld(ref rnd, rnd.NextInt(1, 20), 3.0f, 1);

                // Manifold test
                // TODO would be nice if we could change the world collision tolerance
                for (int iBodyA = 0; iBodyA < world.NumBodies; iBodyA++)
                {
                    for (int iBodyB = iBodyA + 1; iBodyB < world.NumBodies; iBodyB++)
                    {
                        Physics.RigidBody bodyA = world.Bodies[iBodyA];
                        Physics.RigidBody bodyB = world.Bodies[iBodyB];
                        if (bodyA.Collider.Value.Type == ColliderType.Mesh && bodyB.Collider.Value.Type == ColliderType.Mesh)
                        {
                            continue; // TODO - no mesh-mesh manifold support yet
                        }

                        // Build manifolds
                        var contacts = new NativeStream(1, Allocator.Temp);
                        NativeStream.Writer contactWriter = contacts.AsWriter();
                        contactWriter.BeginForEachIndex(0);

                        MotionVelocity motionVelocityA = iBodyA < world.MotionVelocities.Length ?
                            world.MotionVelocities[iBodyA] : MotionVelocity.Zero;
                        MotionVelocity motionVelocityB = iBodyB < world.MotionVelocities.Length ?
                            world.MotionVelocities[iBodyB] : MotionVelocity.Zero;

                        ManifoldQueries.BodyBody(bodyA, bodyB, motionVelocityA, motionVelocityB,
                            world.CollisionWorld.CollisionTolerance, 1.0f, new BodyIndexPair { BodyIndexA = iBodyA, BodyIndexB = iBodyB }, ref contactWriter);
                        contactWriter.EndForEachIndex();

                        // Read each manifold
                        NativeStream.Reader contactReader = contacts.AsReader();
                        contactReader.BeginForEachIndex(0);
                        int manifoldIndex = 0;
                        while (contactReader.RemainingItemCount > 0)
                        {
                            string failureMessage = iWorld + " (" + worldState + ") " + iBodyA + " vs " + iBodyB + " #" + manifoldIndex;
                            manifoldIndex++;

                            // Read the manifold header
                            ContactHeader header = contactReader.Read<ContactHeader>();
                            ConvexConvexManifoldQueries.Manifold manifold = new ConvexConvexManifoldQueries.Manifold();
                            manifold.NumContacts = header.NumContacts;
                            manifold.Normal = header.Normal;

                            // Get the leaf shapes
                            ChildCollider leafA, leafB;
                            {
                                Collider.GetLeafCollider(out leafA, (Collider*)bodyA.Collider.GetUnsafePtr(), header.ColliderKeys.ColliderKeyA, bodyA.WorldFromBody, bodyA.Scale);
                                Collider.GetLeafCollider(out leafB, (Collider*)bodyB.Collider.GetUnsafePtr(), header.ColliderKeys.ColliderKeyB, bodyB.WorldFromBody, bodyB.Scale);
                            }

                            // Read each contact point
                            int minIndex = 0;
                            for (int iContact = 0; iContact < header.NumContacts; iContact++)
                            {
                                // Read the contact and find the closest
                                ContactPoint contact = contactReader.Read<ContactPoint>();
                                manifold[iContact] = contact;
                                if (contact.Distance < manifold[minIndex].Distance)
                                {
                                    minIndex = iContact;
                                }

                                // Check that the contact point is on or inside the shape
                                CheckPointOnSurface(ref leafA, contact.Position + manifold.Normal * contact.Distance, failureMessage + " contact " + iContact + " leaf A");
                                CheckPointOnSurface(ref leafB, contact.Position, failureMessage + " contact " + iContact + " leaf B");
                            }

                            // Check the closest point
                            // TODO - Box-box and box-triangle manifolds have special manifold generation code that trades some accuracy for performance, see comments in
                            // ConvexConvexManifoldQueries.BoxBox() and BoxTriangle(). It may change later, until then they get an exception from the closest point distance test.
                            ColliderType typeA = leafA.Collider->Type;
                            ColliderType typeB = leafB.Collider->Type;
                            bool skipClosestPointTest =
                                (typeA == ColliderType.Box && (typeB == ColliderType.Box || typeB == ColliderType.Triangle)) ||
                                (typeB == ColliderType.Box && typeA == ColliderType.Triangle);
                            if (!skipClosestPointTest)
                            {
                                ContactPoint closestPoint = manifold[minIndex];
                                ScaledMTransform aFromWorld = Inverse(new ScaledMTransform(leafA.TransformFromChild, bodyA.Scale));
                                DistanceQueries.Result result = new DistanceQueries.Result
                                {
                                    PositionOnAinA = Mul(aFromWorld, closestPoint.Position + manifold.Normal * closestPoint.Distance),
                                    NormalInA = math.mul(aFromWorld.Rotation, manifold.Normal),
                                    Distance = closestPoint.Distance * aFromWorld.Scale
                                };

                                ScaledMTransform aFromB = Mul(aFromWorld, new ScaledMTransform(leafB.TransformFromChild, bodyB.Scale));

                                float referenceDistance = DistanceQueries.ConvexConvex(leafA.Collider, leafB.Collider, aFromB.Transform, aFromB.Scale).Distance;

                                ValidateDistanceResult(result, ref ((ConvexCollider*)leafA.Collider)->ConvexHull, ref ((ConvexCollider*)leafB.Collider)->ConvexHull, aFromB, referenceDistance, 1.0f, failureMessage + " closest point");
                            }

                            // Check that the manifold is flat
                            CheckManifoldFlat(ref manifold, manifold.Normal, failureMessage + ": non-flat A");
                            CheckManifoldFlat(ref manifold, float3.zero, failureMessage + ": non-flat B");
                        }

                        contacts.Dispose();
                    }
                }

                TestUtils.DisposeAllColliderBlobs(ref world);
                world.Dispose(); // TODO leaking memory if the test fails
            }
        }
    }
}
