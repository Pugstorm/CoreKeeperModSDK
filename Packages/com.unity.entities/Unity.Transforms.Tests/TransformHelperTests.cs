using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.Entities.Tests
{
    [TestFixture]
    partial class TransformHelperTests : ECSTestsFixture
    {
        const float k_Tolerance = 0.01f;
        static void AssertNearlyEqual(float3 expected, float3 actual, float tolerance)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance, $"expected:{expected}, actual:{actual}");
            Assert.AreEqual(expected.y, actual.y, tolerance, $"expected:{expected}, actual:{actual}");
            Assert.AreEqual(expected.z, actual.z, tolerance, $"expected:{expected}, actual:{actual}");
        }
        static void AssertNearlyEqual(float4 expected, float4 actual, float tolerance)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance, $"expected:{expected}, actual:{actual}");
            Assert.AreEqual(expected.y, actual.y, tolerance, $"expected:{expected}, actual:{actual}");
            Assert.AreEqual(expected.z, actual.z, tolerance, $"expected:{expected}, actual:{actual}");
            Assert.AreEqual(expected.w, actual.w, tolerance, $"expected:{expected}, actual:{actual}");
        }

        [Test]
        public void TransformHelpers_TRS_Extraction_Works()
        {
            float3 expectedT = new float3(1, 2, 3);
            quaternion expectedR = math.normalize(quaternion.AxisAngle(new float3(4, 5, 6), 1.0f));
            float3 expectedS = new float3(0.5f, 1, 1.5f);
            float4x4 m = float4x4.TRS(expectedT, expectedR, expectedS);
            AssertNearlyEqual(expectedT, m.Translation(), 0.000001f);
            AssertNearlyEqual(expectedR.value, m.Rotation().value, 0.000001f);
            AssertNearlyEqual(expectedS, m.Scale(), 0.000001f);
        }

        [Test]
        public void TransformHelpers_Direction_Extraction_Works()
        {
            float3 eyePos = new float3(1, 2, 3);
            float3 targetPos = new float3(4, 5, 6);
            float3 lookAtUp = new float3(-1, 0, 1);
            float4x4 m = float4x4.LookAt(eyePos, targetPos, lookAtUp);
            float3 expectedForwardNorm = math.normalize(targetPos - eyePos);
            float3 expectedRightNorm = math.normalize(math.cross(lookAtUp,expectedForwardNorm));
            float3 expectedUpNorm = math.normalize(math.cross(expectedForwardNorm, expectedRightNorm));
            AssertNearlyEqual(expectedForwardNorm, math.normalize(m.Forward()), 0.00001f);
            AssertNearlyEqual(-expectedForwardNorm, math.normalize(m.Back()), 0.00001f);
            AssertNearlyEqual(expectedRightNorm, math.normalize(m.Right()), 0.00001f);
            AssertNearlyEqual(-expectedRightNorm, math.normalize(m.Left()), 0.00001f);
            AssertNearlyEqual(expectedUpNorm, math.normalize(m.Up()), 0.00001f);
            AssertNearlyEqual(-expectedUpNorm, math.normalize(m.Down()), 0.00001f);
        }

        [Test]
        public void TransformHelpers_TransformPoint_Works()
        {
            float3 expectedT = new float3(1, 2, 3);
            quaternion expectedR = math.normalize(quaternion.AxisAngle(new float3(4, 5, 6), 1.0f));
            float3 expectedS = new float3(0.5f, 1, 1.5f);
            float4x4 m = float4x4.TRS(expectedT, expectedR, expectedS);
            AssertNearlyEqual(expectedT, m.TransformPoint(float3.zero), 0.00001f);
        }

        [Test]
        public void TransformHelpers_TransformDirection_Works()
        {
            float3 expectedT = new float3(1, 2, 3);
            quaternion expectedR = math.normalize(quaternion.AxisAngle(new float3(4, 5, 6), 1.0f));
            float3 expectedS = new float3(0.5f, 1, 1.5f);
            float4x4 m = float4x4.TRS(expectedT, expectedR, expectedS);
            AssertNearlyEqual(math.normalize(m.Forward()), math.normalize(m.TransformDirection(math.forward())), 0.00001f);
        }

        [Test]
        public void TransformHelpers_TransformRotation_Works()
        {
            float3 expectedT = new float3(1, 2, 3);
            quaternion expectedR = math.normalize(quaternion.AxisAngle(new float3(4, 5, 6), 1.0f));
            float3 expectedS = new float3(2, 2, 2);
            float4x4 m = float4x4.TRS(expectedT, expectedR, expectedS);
            AssertNearlyEqual(expectedR.value, math.normalize(m.TransformRotation(quaternion.identity)).value, 0.00001f);
        }

        [Test]
        public void TransformHelpers_LookAtRotation_Works()
        {
            float3 eyePos = new float3(1, 2, 3);
            float3 targetPos = new float3(4, 5, 6);
            float3 lookAtUp = new float3(-1, 0, 1);
            float4x4 m = float4x4.LookAt(eyePos, targetPos, lookAtUp);
            quaternion q = math.normalize(TransformHelpers.LookAtRotation(eyePos, targetPos, lookAtUp));
            AssertNearlyEqual(math.normalize(m.Rotation()).value, q.value, 0.00001f);
        }

        [Test]
        public void ComputeWorldTransformMatrix_InvalidTargetEntity_Throws()
        {
            var localTransformLookup = m_Manager.GetComponentLookup<LocalTransform>(true);
            var parentLookup = m_Manager.GetComponentLookup<Parent>(true);
            var scaleLookup = m_Manager.GetComponentLookup<PostTransformMatrix>(true);
            Assert.That(() => TransformHelpers.ComputeWorldTransformMatrix(Entity.Null, out float4x4 ltw0,
                    ref localTransformLookup, ref parentLookup, ref scaleLookup),
                Throws.InvalidOperationException.With.Message.Contains("does not have the required LocalTransform component"));
        }

        [Test]
        public void ComputeWorldTransformMatrix_InvalidParentEntity_Throws()
        {
            Entity e = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform), typeof(Parent));
            Entity parent = m_Manager.CreateEntity();
            m_Manager.SetComponentData(e, new Parent { Value = parent });
            m_Manager.DestroyEntity(parent);
            var localTransformLookup = m_Manager.GetComponentLookup<LocalTransform>(true);
            var parentLookup = m_Manager.GetComponentLookup<Parent>(true);
            var scaleLookup = m_Manager.GetComponentLookup<PostTransformMatrix>(true);
            // Ideally we'd get a more specific error message here for an invalid parent entity, but unfortunately all this method
            // can ask internally is "does this entity have component X?", and the answer for invalid entities is "no".
            Assert.That(() => TransformHelpers.ComputeWorldTransformMatrix(e, out float4x4 ltw0,
                    ref localTransformLookup, ref parentLookup, ref scaleLookup),
                Throws.InvalidOperationException.With.Message.Contains("does not have the required LocalTransform component"));
        }

        [Test]
        public void ComputeWorldTransformMatrix_ParentHasNoTransform_Throws()
        {
            Entity e = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform), typeof(Parent));
            Entity parent = m_Manager.CreateEntity();
            m_Manager.SetComponentData(e, new Parent { Value = parent });
            var localTransformLookup = m_Manager.GetComponentLookup<LocalTransform>(true);
            var parentLookup = m_Manager.GetComponentLookup<Parent>(true);
            var scaleLookup = m_Manager.GetComponentLookup<PostTransformMatrix>(true);
            Assert.That(() => TransformHelpers.ComputeWorldTransformMatrix(e, out float4x4 ltw0,
                    ref localTransformLookup, ref parentLookup, ref scaleLookup),
                Throws.InvalidOperationException.With.Message.Contains("does not have the required LocalTransform component"));
        }

        [Test]
        public void ComputeWorldTransformMatrix_MatchesLocalToWorld([Values] bool withNonUniformScale)
        {
            var e0 = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform));
            var e1 = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform), typeof(Parent));
            var e2 = m_Manager.CreateEntity(typeof(LocalToWorld), typeof(LocalTransform), typeof(Parent));
            m_Manager.SetComponentData(e0, LocalTransform.FromPositionRotationScale(new float3(1, 2, 3),
                quaternion.AxisAngle(new float3(1, 1, 1), 17), 1.0f));
            if (withNonUniformScale)
            {
                m_Manager.AddComponentData(e0, new PostTransformMatrix { Value = float4x4.Scale(5, 3, 1) });
            }

            m_Manager.SetComponentData(e1, LocalTransform.FromPositionRotationScale(new float3(1, 2, 3),
                quaternion.AxisAngle(new float3(1, 1, 1), 17), 1.0f));
            m_Manager.SetComponentData(e1, new Parent { Value = e0 });

            m_Manager.SetComponentData(e2, LocalTransform.FromPositionRotationScale(new float3(1, 2, 3),
                quaternion.AxisAngle(new float3(1, 1, 1), 17), 1.0f));
            m_Manager.SetComponentData(e2, new Parent { Value = e1 });
            if (withNonUniformScale)
            {
                m_Manager.AddComponentData(e2, new PostTransformMatrix { Value = float4x4.Scale(5, 3, 1) });
            }

            World.GetOrCreateSystem<ParentSystem>().Update(World.Unmanaged);
            World.GetOrCreateSystem<LocalToWorldSystem>().Update(World.Unmanaged);
            m_Manager.CompleteAllTrackedJobs();

            var localTransformLookup = m_Manager.GetComponentLookup<LocalTransform>(true);
            var parentLookup = m_Manager.GetComponentLookup<Parent>(true);
            var scaleLookup = m_Manager.GetComponentLookup<PostTransformMatrix>(true);
            TransformHelpers.ComputeWorldTransformMatrix(e0, out float4x4 ltw0, ref localTransformLookup, ref parentLookup, ref scaleLookup);
            TransformHelpers.ComputeWorldTransformMatrix(e1, out float4x4 ltw1, ref localTransformLookup, ref parentLookup, ref scaleLookup);
            TransformHelpers.ComputeWorldTransformMatrix(e2, out float4x4 ltw2, ref localTransformLookup, ref parentLookup, ref scaleLookup);
            Assert.AreEqual(m_Manager.GetComponentData<LocalToWorld>(e0).Value, ltw0);
            Assert.AreEqual(m_Manager.GetComponentData<LocalToWorld>(e1).Value, ltw1);
            Assert.AreEqual(m_Manager.GetComponentData<LocalToWorld>(e2).Value, ltw2);
        }


    }
}
