using NUnit.Framework;
using Unity.Mathematics;

namespace Unity.Physics.Tests.Joints
{
    class BodyFrame_UnitTests
    {
        static readonly float3 k_XAxis = new float3(1f, 0f, 0f);
        static readonly float3 k_YAxis = new float3(0f, 1f, 0f);
        static readonly (float3, float3)k_DefaultAxes = (k_XAxis, k_YAxis);

        static readonly TestCaseData[] k_ValidateAxesTestCases =
        {
            new TestCaseData(BodyFrame.Identity)
                .Returns(k_DefaultAxes)
                .SetName("Identity => Default axes"),
            new TestCaseData(default(BodyFrame))
                .Returns(k_DefaultAxes)
                .SetName("Both axes uninitialized => Default axes"),
            new TestCaseData(new BodyFrame { Axis = k_XAxis, PerpendicularAxis = k_XAxis })
                .Returns(k_DefaultAxes)
                .SetName("Both axes default X => Default axes"),
            new TestCaseData(new BodyFrame { Axis = k_XAxis, PerpendicularAxis = default })
                .Returns(k_DefaultAxes)
                .SetName("Axis default X, perpendicular uninitialized => Default axes"),
            new TestCaseData(new BodyFrame { Axis = default, PerpendicularAxis = k_XAxis })
                .Returns(k_DefaultAxes)
                .SetName("Axis uninitialized, perpendicular default X => Default axes")
        };

        [TestCaseSource(nameof(k_ValidateAxesTestCases))]
        public (float3, float3) ValidateAxes_ReturnsExpectedValue(BodyFrame bodyFrame)
        {
            var validatedAxes = bodyFrame.ValidateAxes();
            return (validatedAxes.c0, validatedAxes.c1);
        }

        static float3[] k_ValidateAxesPerpendicularTestCases =
        {
            float3.zero,
            k_XAxis,
            k_YAxis,
            new float3(1f, -1f, -1f)
        };

        [Test]
        public void ValidateAxes_ResultingAxesAreOrthoNormal(
            [ValueSource(nameof(k_ValidateAxesPerpendicularTestCases))] float3 axis,
            [ValueSource(nameof(k_ValidateAxesPerpendicularTestCases))] float3 perpendicularAxis
        )
        {
            var bodyFrame = new BodyFrame { Axis = axis, PerpendicularAxis = perpendicularAxis };

            var validatedAxes = bodyFrame.ValidateAxes();
            Assume.That(math.length(validatedAxes.c2), Is.EqualTo(1f).Within(0.0001f));
            var dot = math.dot(validatedAxes.c0, validatedAxes.c1);
            Assert.That(dot, Is.EqualTo(0f).Within(0.0001f));
        }
    }
}
