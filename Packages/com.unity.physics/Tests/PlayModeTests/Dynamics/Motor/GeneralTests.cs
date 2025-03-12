using NUnit.Framework;
using Unity.Mathematics;
namespace Unity.Physics.Tests.Motors
{
    // Test cases we want to verify:
    // Test Case A: Adding impulse to accumulation does not exceed threshold
    // Test Case B-/+: accumulation + impulse will push over threshold, so apply the remaining impulse balance
    // Test Case C-/+: No impulses accumulated and the impulse is larger than threshold
    // Test Case D-/+: Accumulation has reached threshold already
    [TestFixture]
    public class GeneralTests
    {
        static readonly TestCaseData[] k_ScalarImpulseTestCases =
        {
            // Input: accumulated impulse, new impulse, step count, expected impulse afterwards
            new TestCaseData(0.0f, 2.0f, 1, 2.0f).SetName("Case A+1: Within threshold"),
            new TestCaseData(2.5f, 2.49999f, 1, 2.49999f).SetName("Case A+2: Within threshold"),

            new TestCaseData(0.0f, -2.0f, 1, -2.0f).SetName("Case A-1: Within threshold"),
            new TestCaseData(-2.5f, -2.49999f, 1, -2.49999f).SetName("Case A-2: Within threshold"),

            new TestCaseData(4.0f, 2.0f, 1, 1.0f).SetName("Case B+1: Impulse capped"),
            new TestCaseData(-4.0f, -2.0f, 1, -1.0f).SetName("Case B-1: Impulse capped"),

            new TestCaseData(0.0f, 6.0f, 1, 5.0f).SetName("Case C+1: Threshold exceeded on first loop"),
            new TestCaseData(0.0f, -6.0f, 1, -5.0f).SetName("Case C-1: Threshold exceeded on first loop"),

            new TestCaseData(5.0f, 2.0f, 1, 0.0f).SetName("Case D+1: Threshold reached"),
            new TestCaseData(5.0f, 5.0f, 1, 0.0f).SetName("Case D+2: Threshold reached"),
            //new TestCaseData(6.0f, 1.0f, 1, 0.0f).SetName("Case D+3: Threshold reached"), //only use if SafetyChecks are enabled (will still fail)
            new TestCaseData(3.0f, 1.5f, 3, 0.0f).SetName("Case D+4AB: Threshold reached"),
            new TestCaseData(3.0f, 2.0f, 2, 0.0f).SetName("Case D+5B: Threshold reached"),

            new TestCaseData(-5.0f, -2.0f, 1, 0.0f).SetName("Case D-1: Threshold reached"),
            new TestCaseData(-5.0f, -5.0f, 1, 0.0f).SetName("Case D-2: Threshold reached"),
            //new TestCaseData(-6.0f, -1.0f, 1, 0.0f).SetName("Case D-3: Threshold reached"), //only use if SafetyChecks are enabled (will still fail)
            new TestCaseData(-3.0f, -1.5f, 3, 0.0f).SetName("Case D-4AB: Threshold reached"),
            new TestCaseData(-3.0f, -2.0f, 2, 0.0f).SetName("Case D-5B: Threshold reached"),
        };

        [TestCaseSource(nameof(k_ScalarImpulseTestCases))]
        public void ScalarImpulseTests(float accumulatedImpulseIn, float impulse, int numSteps, float expectedImpulse)
        {
            float maxImpulseOfMotor = 5.0f;
            float accumulatedImpulse = accumulatedImpulseIn;

            string failureMessage;
            var testThreshold = math.EPSILON;

            for (int i = 0; i < numSteps; i++)
            {
                impulse = JacobianUtilities.CapImpulse(impulse, ref accumulatedImpulse, maxImpulseOfMotor);
            }

            // Always throw an error if the max impulse is exceeded
            var compareToExpected = math.abs(accumulatedImpulse) - maxImpulseOfMotor;
            failureMessage = $"Impulse accumulation exceeded threshold by {compareToExpected}";
            Assert.IsFalse(compareToExpected >= math.EPSILON, failureMessage);

            // Verify the impulse applied is correct for each use case
            compareToExpected = math.abs(expectedImpulse - impulse);
            failureMessage = $"Impulse different from expected {compareToExpected}";
            Assert.Less(compareToExpected, testThreshold, failureMessage);
        }

        static readonly TestCaseData[] k_VectorImpulseTestCases =
        {
            // Input: accumulated impulse, new impulse, step count, expected impulse afterwards
            new TestCaseData(new float3(0f, 0f, 0f), new float3(0f, 0f, 2f), 1, new float3(0f, 0f, 2f)).SetName("Case A+1: Within threshold"),
            new TestCaseData(new float3(0f, 0f, 2.5f), new float3(0f, 0f, 2.49999f), 1, new float3(0f, 0f, 2.49999f)).SetName("Case A+2: Within threshold"),
            new TestCaseData(new float3(1f, 1f, 1f), new float3(0.5f, 0.5f, 0.5f), 1, new float3(0.5f, 0.5f, 0.5f)).SetName("Case A+3: Within threshold"),

            new TestCaseData(new float3(0f, 0f, 0f), new float3(0f, 0f, -2f), 1, new float3(0f, 0f, -2f)).SetName("Case A-1: Within threshold"),
            new TestCaseData(new float3(0f, 0f, -2.5f), new float3(0f, 0f, -2.49999f), 1, new float3(0f, 0f, -2.49999f)).SetName("Case A-2: Within threshold"),
            new TestCaseData(new float3(-1f, -1f, 1f), new float3(-0.5f, -0.5f, 0.5f), 1, new float3(-0.5f, -0.5f, 0.5f)).SetName("Case A-3: Within threshold"),

            new TestCaseData(new float3(0f, 4f, 0f), new float3(0f, 2f, 0f), 1, new float3(0f, 1f, 0f)).SetName("Case B+1: Impulse capped"),
            new TestCaseData(new float3(2f, 3f, 2f), new float3(0.75f, 0.25f, 1f), 1, new float3(0.4253564f, 0.6380343f, 0.4253564f)).SetName("Case B+2: Impulse capped"),
            new TestCaseData(new float3(0f, -4f, 0f), new float3(0f, -2f, 0f), 1, new float3(0f, -1f, 0f)).SetName("Case B-1: Impulse capped"),
            new TestCaseData(new float3(-2f, -3f, -2f), new float3(-0.75f, -0.25f, -1f), 1, new float3(-0.4253564f, -0.6380343f, -0.4253564f)).SetName("Case B-2: Impulse capped"),

            new TestCaseData(new float3(0f, 0f, 0f), new float3(6f, 0f, 0f), 1, new float3(5f, 0f, 0f)).SetName("Case C+1: Threshold exceeded on first loop"),
            new TestCaseData(new float3(0f, 0f, 0f), new float3(3f, 4f, 5f), 1, new float3(2.12132f, 2.828427f, 3.535534f)).SetName("Case C+2: Threshold exceeded on first loop"),
            new TestCaseData(new float3(0f, 0f, 0f), new float3(-6f, 0f, 0f), 1, new float3(-5f, 0f, 0f)).SetName("Case C-1: Threshold exceeded on first loop"),
            new TestCaseData(new float3(0f, 0f, 0f), new float3(-3f, 4f, -5f), 1, new float3(-2.12132f, 2.828427f, -3.535534f)).SetName("Case C-2: Threshold exceeded on first loop"),

            new TestCaseData(new float3(5f, 0f, 0f), new float3(2f, 0f, 0f), 1, new float3(0f, 0f, 0f)).SetName("Case D+1: Threshold reached"),
            new TestCaseData(new float3(5f, 0f, 0f), new float3(5f, 0f, 0f), 1, new float3(0f, 0f, 0f)).SetName("Case D+2: Threshold reached"),
            new TestCaseData(new float3(6f, 0f, 0f), new float3(1f, 0f, 0f), 1, new float3(0f, 0f, 0f)).SetName("Case D+3: Threshold reached"),
            new TestCaseData(new float3(3f, 0f, 0f), new float3(1.5f, 0f, 0f), 3, new float3(0f, 0f, 0f)).SetName("Case D+4AB: Threshold reached"),
            new TestCaseData(new float3(3f, 0f, 0f), new float3(2f, 0f, 0f), 2, new float3(0f, 0f, 0f)).SetName("Case D+5B: Threshold reached"),

            new TestCaseData(new float3(2.886751f, 2.886751f, 2.886751f), new float3(1f, 1f, 1f), 1, new float3(0f, 0f, 0f)).SetName("Case D+6: Threshold reached"),
            new TestCaseData(new float3(3f, 3f, 3f), new float3(1f, 1f, 1f), 1, new float3(0f, 0f, 0f)).SetName("Case D+7: Threshold reached"),
            new TestCaseData(new float3(1f, 1f, 1f), new float3(2f, 1.5f, 2f), 3, new float3(0f, 0f, 0f)).SetName("Case D+8AB: Threshold reached"),
            new TestCaseData(new float3(1f, 1f, 1f), new float3(2f, 2f, 2f), 2, new float3(0f, 0f, 0f)).SetName("Case D+9B: Threshold reached"),

            new TestCaseData(new float3(-5f, 0f, 0f), new float3(-2f, 0f, 0f), 1, new float3(0f, 0f, 0f)).SetName("Case D-1: Threshold reached"),
            new TestCaseData(new float3(-5f, 0f, 0f), new float3(-5f, 0f, 0f), 1, new float3(0f, 0f, 0f)).SetName("Case D-2: Threshold reached"),
            new TestCaseData(new float3(-6f, 0f, 0f), new float3(-1f, 0f, 0f), 1, new float3(0f, 0f, 0f)).SetName("Case D-3: Threshold reached"),
            new TestCaseData(new float3(-3f, 0f, 0f), new float3(-1.5f, 0f, 0f), 3, new float3(0f, 0f, 0f)).SetName("Case D-4AB: Threshold reached"),
            new TestCaseData(new float3(-3f, 0f, 0f), new float3(-2f, 0f, 0f), 2, new float3(0f, 0f, 0f)).SetName("Case D-5B: Threshold reached"),

            new TestCaseData(new float3(-2.886751f, -2.886751f, -2.886751f), new float3(-1f, -1f, -1f), 1, new float3(0f, 0f, 0f)).SetName("Case D-6: Threshold reached"),
            new TestCaseData(new float3(-3f, -3f, -3f), new float3(-1f, -1f, -1f), 1, new float3(0f, 0f, 0f)).SetName("Case D-7: Threshold reached"),
            new TestCaseData(new float3(-1f, -1f, -1f), new float3(-2f, -1.5f, -2f), 3, new float3(0f, 0f, 0f)).SetName("Case D-8AB: Threshold reached"),
            new TestCaseData(new float3(-1f, -1f, -1f), new float3(-2f, -2f, -2f), 2, new float3(0f, 0f, 0f)).SetName("Case D-9B: Threshold reached"),

            new TestCaseData(new float3(0f, 0f, 0f), new float3(0f, 0f, 0f), 3, new float3(0f, 0f, 0f)).SetName("Zero added and accumulated impulse"),
            new TestCaseData(new float3(1f, 0f, 0f), new float3(0f, 0f, 0f), 3, new float3(0f, 0f, 0f)).SetName("Zero added impulse"),
        };

        [TestCaseSource(nameof(k_VectorImpulseTestCases))]
        public void VectorImpulseTests(float3 accumulatedImpulseIn, float3 impulse, int numSteps, float3 expectedImpulse)
        {
            float maxImpulseOfMotor = 5.0f;
            float3 accumulatedImpulse = accumulatedImpulseIn;

            string failureMessage;
            var testThreshold = 1e-5;

            for (int i = 0; i < numSteps; i++)
            {
                impulse = JacobianUtilities.CapImpulse(impulse, ref accumulatedImpulse, maxImpulseOfMotor);
            }

            // Always throw an error if the max impulse is exceeded
            var compareToExpected = math.length(accumulatedImpulse) - maxImpulseOfMotor;
            failureMessage = $"Impulse accumulation exceeded threshold by {compareToExpected}";
            Assert.IsFalse(compareToExpected >= testThreshold, failureMessage);

            // Verify the impulse applied is correct for each use case
            var compareImpulse = math.abs(expectedImpulse - impulse);
            failureMessage = $"Impulse different from expected {compareToExpected}";
            Assert.Less(compareImpulse.x, testThreshold, failureMessage);
            Assert.Less(compareImpulse.y, testThreshold, failureMessage);
            Assert.Less(compareImpulse.z, testThreshold, failureMessage);
        }
    }
}
