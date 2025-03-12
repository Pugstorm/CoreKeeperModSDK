using System;
using NUnit.Framework;
using Unity.Mathematics;
using static Unity.Physics.SimplexSolver;
using Assert = UnityEngine.Assertions.Assert;

namespace Unity.Physics.Tests.Dynamics.SimplexSolver
{
    // Tests to validate simplex solver implementation
    class SimplexSolverTests
    {
        private static SurfaceConstraintInfo CreateConstraint(
            ColliderKey colliderKey, float3 hitPosition, float3 normal, float distance,
            int priority, int rigidBodyIndex, bool touched, float3 velocity)
        {
            return new SurfaceConstraintInfo
            {
                ColliderKey = colliderKey,
                HitPosition = hitPosition,
                Plane = new Plane(normal, distance),
                Priority = priority,
                RigidBodyIndex = rigidBodyIndex,
                Touched = touched,
                Velocity = velocity
            };
        }

        [Test]
        public void TestSwapPlanes()
        {
            SurfaceConstraintInfo plane1 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(0, 1, 0), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane2 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(0, 1, 0), 1, 0, 0, false, float3.zero);

            SurfaceConstraintInfo originalPlane1 = plane1;
            SurfaceConstraintInfo originalPlane2 = plane2;

            SwapPlanes(ref plane1, ref plane2);
            Assert.IsTrue(originalPlane1.Equals(plane2) && originalPlane2.Equals(plane1));
        }

        [Test]
        public void TestTest1d()
        {
            SurfaceConstraintInfo plane = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(0, 1, 0), 1, 0, 0, false, float3.zero);

            // Velocity towards plane can't be skipped, so it returns true
            bool result1 = Test1d(plane, new float3(0, -1, 0));
            Assert.IsTrue(result1);

            // Velocity away from plane can be skipped, so it returns false
            bool result2 = Test1d(plane, new float3(0, 1, 0));
            Assert.IsFalse(result2);
        }

        [Test]
        public void TestSort2d()
        {
            SurfaceConstraintInfo plane1 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(0, 1, 0), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane2 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(0, 1, 0), 1, 1, 0, false, float3.zero);

            // Leftmost plane should have smaller priority after the sort
            Sort2d(ref plane1, ref plane2);
            Assert.IsTrue(plane1.Priority < plane2.Priority);

            // Leftmost plane should have smaller priority after the sort
            Sort2d(ref plane2, ref plane1);
            Assert.IsTrue(plane2.Priority < plane1.Priority);
        }

        [Test]
        public void TestSort3d()
        {
            SurfaceConstraintInfo plane1 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(0, 1, 0), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane2 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(0, 1, 0), 1, 1, 0, false, float3.zero);
            SurfaceConstraintInfo plane3 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(0, 1, 0), 1, 2, 0, false, float3.zero);

            // Leftmost plane should have smallest priority after the sort
            Sort3d(ref plane1, ref plane2, ref plane3);
            Assert.IsTrue(plane1.Priority < plane2.Priority &&
                plane2.Priority < plane3.Priority);

            // Leftmost plane should have smallest priority after the sort
            Sort3d(ref plane1, ref plane3, ref plane2);
            Assert.IsTrue(plane1.Priority < plane3.Priority &&
                plane3.Priority < plane2.Priority);

            // Leftmost plane should have smallest priority after the sort
            Sort3d(ref plane2, ref plane1, ref plane3);
            Assert.IsTrue(plane2.Priority < plane1.Priority &&
                plane1.Priority < plane3.Priority);

            // Leftmost plane should have smallest priority after the sort
            Sort3d(ref plane2, ref plane3, ref plane1);
            Assert.IsTrue(plane2.Priority < plane3.Priority &&
                plane3.Priority < plane1.Priority);

            // Leftmost plane should have smallest priority after the sort
            Sort3d(ref plane3, ref plane1, ref plane2);
            Assert.IsTrue(plane3.Priority < plane1.Priority &&
                plane1.Priority < plane2.Priority);

            // Leftmost plane should have smallest priority after the sort
            Sort3d(ref plane3, ref plane2, ref plane1);
            Assert.IsTrue(plane3.Priority < plane2.Priority &&
                plane2.Priority < plane1.Priority);
        }

        [Test]
        public void TestSolve1d()
        {
            SurfaceConstraintInfo plane = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(0, 1, 0), 1, 0, 0, false, float3.zero);

            // Approaching velocity should lose all vertical speed
            float3 velocity = new float3(0, -1, 0);
            Solve1d(plane, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 0);
            Assert.AreApproximatelyEqual(velocity.z, 0);

            // Test that only vertical component of velocity is killed
            velocity = new float3(-1, -1, 0);
            Solve1d(plane, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, -1);
            Assert.AreApproximatelyEqual(velocity.y, 0);
            Assert.AreApproximatelyEqual(velocity.z, 0);

            plane = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(0, 1, 0), 1, 0, 0, false, new float3(0, 0.5f, 0));

            // Check that vertical component of velocity changes direction when ground velocity is directed towards the body
            velocity = new float3(0, -1, 0);
            Solve1d(plane, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 0.5f);
            Assert.AreApproximatelyEqual(velocity.z, 0);

            plane = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(0, 1, 0), 1, 0, 0, false, new float3(0, -0.5f, 0));

            // Check that only part of vertical component of velocity is killed when ground velocity is going away
            velocity = new float3(0, -1, 0);
            Solve1d(plane, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, -0.5f);
            Assert.AreApproximatelyEqual(velocity.z, 0);
        }

        [Test]
        public void TestSolve2d()
        {
            float3 up = new float3(0, 1, 0);

            SurfaceConstraintInfo plane1 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(0, 1, 0), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane2 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(0, -1, 0), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane3 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(1, 0, 0), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane4 = CreateConstraint(ColliderKey.Empty, float3.zero, math.normalize(new float3(1.0f, 1.0f, 0)), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane5 = CreateConstraint(ColliderKey.Empty, float3.zero, math.normalize(new float3(-1.0f, 1.0f, 0)), 1, 0, 0, false, float3.zero);

            // Test the parallel planes scenario
            float3 velocity = new float3(0, -1, 0);
            Solve2d(up, plane1, plane2, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 0);
            Assert.AreApproximatelyEqual(velocity.z, 0);

            // Test the not-parallel planes scenario
            velocity = new float3(0, -1, 0);
            Solve2d(up, plane1, plane3, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 0);
            Assert.AreApproximatelyEqual(velocity.z, 0);

            // A little more complicated scenario
            velocity = new float3(0, -1, 0);
            Solve2d(up, plane4, plane5, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 0);
            Assert.AreApproximatelyEqual(velocity.z, 0);

            // Introduce plane velocities
            plane4.Velocity = new float3(0, 1, 0);
            plane5.Velocity = new float3(0, 1, 0);
            velocity = new float3(0, -1, 0);
            Solve2d(up, plane4, plane5, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 1);
            Assert.AreApproximatelyEqual(velocity.z, 0);

            // Case where one plane drifts away
            plane4.Velocity = new float3(0, 1, 0);
            plane5.Velocity = new float3(0, -1, 0);
            velocity = new float3(0, -1, 0);
            Solve2d(up, plane4, plane5, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 1.0f);
            Assert.AreApproximatelyEqual(velocity.y, 0);
            Assert.AreApproximatelyEqual(velocity.z, 0);

            // Case where both planes drift to the side
            plane4.Velocity = new float3(-1, 0, 0);
            plane5.Velocity = new float3(1, 0, 0);
            velocity = new float3(0, -1, 0);
            Solve2d(up, plane4, plane5, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, -1);
            Assert.AreApproximatelyEqual(velocity.z, 0);
        }

        [Test]
        public void TestSolve3d()
        {
            float3 up = new float3(0, 1, 0);

            SurfaceConstraintInfo plane1 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(0, 1, 0), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane2 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(0, -1, 0), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane3 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(1, 0, 0), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane4 = CreateConstraint(ColliderKey.Empty, float3.zero, math.normalize(new float3(1, 1, 0)), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane5 = CreateConstraint(ColliderKey.Empty, float3.zero, math.normalize(new float3(-1, 1, 0)), 1, 0, 0, false, float3.zero);

            // Test the parallel planes scenario
            float3 velocity = new float3(0, -1, 0);
            Solve3d(up, plane1, plane2, plane3, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 0);
            Assert.AreApproximatelyEqual(velocity.z, 0);

            // Test the not-parallel planes scenario
            velocity = new float3(0, -1, 0);
            Solve3d(up, plane1, plane4, plane5, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 0);
            Assert.AreApproximatelyEqual(velocity.z, 0);

            // Introduce plane velocities
            plane1.Velocity = new float3(0, 1, 0);
            plane4.Velocity = new float3(0, 1, 0);
            plane5.Velocity = new float3(0, 1, 0);
            velocity = new float3(0, -1, 0);
            Solve3d(up, plane1, plane4, plane5, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 1);
            Assert.AreApproximatelyEqual(velocity.z, 0);

            // Case where 2 planes drift to the side
            plane4.Velocity = new float3(1, 0, 0);
            plane5.Velocity = new float3(-1, 0, 0);
            velocity = new float3(0, -1, 0);
            Solve3d(up, plane1, plane4, plane5, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 1);
            Assert.AreApproximatelyEqual(velocity.z, 0);
        }

        [Test]
        public unsafe void TestExamineActivePlanes()
        {
            float3 up = new float3(0, 1, 0);

            SurfaceConstraintInfo plane1 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(0, 1, 0), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane2 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(0, -1, 0), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane3 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(1, 0, 0), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane4 = CreateConstraint(ColliderKey.Empty, float3.zero, math.normalize(new float3(1, 1, 0)), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane5 = CreateConstraint(ColliderKey.Empty, float3.zero, math.normalize(new float3(-1, 1, 0)), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane6 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(0, 0, 1), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane7 = CreateConstraint(ColliderKey.Empty, float3.zero, math.normalize(new float3(0, 1, 2)), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane8 = CreateConstraint(ColliderKey.Empty, float3.zero, math.normalize(new float3(0, 1, -2)), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane9 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(1, 0, 0), 1, 0, 0, false, float3.zero);
            SurfaceConstraintInfo plane10 = CreateConstraint(ColliderKey.Empty, float3.zero, new float3(-1, 0, 0), 1, 0, 0, false, new float3(-1, 0, 0));

            // Test the single plane
            SurfaceConstraintInfo* supportPlanes = stackalloc SurfaceConstraintInfo[4];
            int numSupportPlanes = 1;
            supportPlanes[0] = plane1;
            float3 velocity = new float3(0, -1, 0);
            ExamineActivePlanes(up, supportPlanes, ref numSupportPlanes, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 0);
            Assert.AreApproximatelyEqual(velocity.z, 0);
            Assert.AreEqual(1, numSupportPlanes);

            // 2 planes, one unnecessary
            numSupportPlanes = 2;
            supportPlanes[0] = plane1;
            supportPlanes[1] = plane1;
            velocity = new float3(0, -1, 0);
            ExamineActivePlanes(up, supportPlanes, ref numSupportPlanes, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 0);
            Assert.AreApproximatelyEqual(velocity.z, 0);
            Assert.AreEqual(1, numSupportPlanes);

            // 2 planes, both necessary
            numSupportPlanes = 2;
            supportPlanes[0] = plane5;
            supportPlanes[1] = plane4;
            velocity = new float3(0, -1, 0);
            ExamineActivePlanes(up, supportPlanes, ref numSupportPlanes, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 0);
            Assert.AreApproximatelyEqual(velocity.z, 0);
            Assert.AreEqual(2, numSupportPlanes);

            // 3 planes, 2 unnecessary
            numSupportPlanes = 3;
            supportPlanes[0] = plane1;
            supportPlanes[1] = plane1;
            supportPlanes[2] = plane1;
            velocity = new float3(0, -1, 0);
            ExamineActivePlanes(up, supportPlanes, ref numSupportPlanes, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 0);
            Assert.AreApproximatelyEqual(velocity.z, 0);
            Assert.AreEqual(1, numSupportPlanes);

            // 3 planes, 1 unnecessary
            numSupportPlanes = 3;
            supportPlanes[0] = plane1;
            supportPlanes[1] = plane1;
            supportPlanes[2] = plane3;
            velocity = new float3(0, -1, 0);
            ExamineActivePlanes(up, supportPlanes, ref numSupportPlanes, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 0);
            Assert.AreApproximatelyEqual(velocity.z, 0);
            Assert.AreEqual(2, numSupportPlanes);

            // 3 planes, all necessary
            numSupportPlanes = 3;
            supportPlanes[0] = plane4;
            supportPlanes[1] = plane5;
            supportPlanes[2] = plane6;
            velocity = new float3(0, -1, 0);
            ExamineActivePlanes(up, supportPlanes, ref numSupportPlanes, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 0);
            Assert.AreApproximatelyEqual(velocity.z, 0);
            Assert.AreEqual(3, numSupportPlanes);

            // 4 planes, 3 unnecessary
            numSupportPlanes = 4;
            supportPlanes[0] = plane1;
            supportPlanes[1] = plane1;
            supportPlanes[2] = plane1;
            supportPlanes[3] = plane1;
            velocity = new float3(0, -1, 0);
            ExamineActivePlanes(up, supportPlanes, ref numSupportPlanes, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 0);
            Assert.AreApproximatelyEqual(velocity.z, 0);
            Assert.AreEqual(1, numSupportPlanes);

            // 4 planes, 2 unnecessary
            numSupportPlanes = 4;
            supportPlanes[0] = plane1;
            supportPlanes[1] = plane1;
            supportPlanes[2] = plane1;
            supportPlanes[3] = plane3;
            velocity = new float3(0, -1, 0);
            ExamineActivePlanes(up, supportPlanes, ref numSupportPlanes, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 0);
            Assert.AreApproximatelyEqual(velocity.z, 0);
            Assert.AreEqual(2, numSupportPlanes);

            // 4 planes, 1 unnecessary
            numSupportPlanes = 4;
            supportPlanes[0] = plane1;
            supportPlanes[1] = plane4;
            supportPlanes[2] = plane5;
            supportPlanes[3] = plane6;
            velocity = new float3(0, -1, 0);
            ExamineActivePlanes(up, supportPlanes, ref numSupportPlanes, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 0);
            Assert.AreApproximatelyEqual(velocity.z, 0);
            Assert.AreEqual(3, numSupportPlanes);

            // 4 planes, all necessary
            numSupportPlanes = 4;
            supportPlanes[0] = plane7;
            supportPlanes[1] = plane8;
            supportPlanes[2] = plane9;
            supportPlanes[3] = plane10;
            velocity = new float3(1, -1, 0);
            ExamineActivePlanes(up, supportPlanes, ref numSupportPlanes, ref velocity);
            Assert.AreApproximatelyEqual(velocity.x, 0);
            Assert.AreApproximatelyEqual(velocity.y, 0);
            Assert.AreApproximatelyEqual(velocity.z, 0);
            Assert.AreEqual(4, numSupportPlanes);
        }
    }
}
