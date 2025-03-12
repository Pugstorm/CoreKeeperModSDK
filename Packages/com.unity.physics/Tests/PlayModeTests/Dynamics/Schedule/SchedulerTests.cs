using NUnit.Framework;
using Unity.Collections;
using static Unity.Physics.DispatchPairSequencer;

namespace Unity.Physics.Tests.Dynamics.Schedule
{
    // Tests to validate scheduler implementation
    [TestFixture]
    class SchedulerTests
    {
        private DispatchPairSequencer Sequencer;
        private SolverSchedulerInfo SolverSchedulerInfo;
        private int NumPhases => Sequencer.m_PhaseLookupTableDynamicDynamicPairs.NumPhases;
        private int BatchSize => CreateDispatchPairPhasesJob.k_MinBatchSize;

        [SetUp]
        public void SetUp()
        {
            Sequencer = DispatchPairSequencer.Create();
            SolverSchedulerInfo = new SolverSchedulerInfo(NumPhases);
        }

        [TearDown]
        public void TearDown()
        {
            Sequencer.Dispose();
            SolverSchedulerInfo.Dispose();
        }

        private void CreatePhasedDispatchPairsAndVerify(int numDynamicBodies, NativeArray<DispatchPair> sortedDispatchPairs)
        {
            var phasedDispatchPairs = new NativeArray<DispatchPair>(sortedDispatchPairs.Length, Allocator.Temp);
            var createDispatchPairsJob = new CreateDispatchPairPhasesJob
            {
                DispatchPairs = sortedDispatchPairs,
                NumDynamicBodies = numDynamicBodies,
                NumPhases = NumPhases,
                PhasedDispatchPairs = phasedDispatchPairs,
                PhaseLookupTableDynamicDynamicPairs = Sequencer.m_PhaseLookupTableDynamicDynamicPairs.Table,
                PhaseLookupTableDynamicStaticPairs = Sequencer.m_PhaseLookupTableDynamicStaticPairs.Table,
                SolverSchedulerInfo = SolverSchedulerInfo
            };
            createDispatchPairsJob.Execute();

            // verify the correctness of the phase info data elements
            CreateDispatchPairPhasesJob.CheckIntegrity(phasedDispatchPairs, numDynamicBodies, ref SolverSchedulerInfo.PhaseInfo);
        }

        // Tests scheduler with full batches.
        // It is not very often in current tests that there are full batches of dispatch pairs in solve phases.
        // There was an issue where having 2 full batches (and some overflow pairs) in 2 consecutive phases lead
        // to a race condition in the solver as wrong pairs were being solved in parallel.
        // This test illustrates one example of such issue and verifies that it doesn't happen anymore.
        [Test]
        public void TestSchedulerWithFullBatches()
        {
            // This test was written when the batch size was 8. It is important to ensure that the batch size hasn't changed for the validity of this test.
            // If the batch size changes, the test needs to be updated accordingly.
            Assert.AreEqual(8, BatchSize);

            int numDynamicBodies = 15;

            var sortedDispatchPairs = new NativeArray<DispatchPair>(20, Allocator.Temp);
            {
                sortedDispatchPairs[0] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 0, BodyIndexB = 1 });
                sortedDispatchPairs[1] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 0, BodyIndexB = 2 });
                sortedDispatchPairs[2] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 0, BodyIndexB = 3 });
                sortedDispatchPairs[3] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 0, BodyIndexB = 4 });
                sortedDispatchPairs[4] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 0, BodyIndexB = 5 });
                sortedDispatchPairs[5] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 0, BodyIndexB = 6 });
                sortedDispatchPairs[6] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 0, BodyIndexB = 7 });
                sortedDispatchPairs[7] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 0, BodyIndexB = 8 });
                sortedDispatchPairs[8] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 0, BodyIndexB = 15 });
                sortedDispatchPairs[9] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 0, BodyIndexB = 16 });
                sortedDispatchPairs[10] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 1, BodyIndexB = 2 });
                sortedDispatchPairs[11] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 1, BodyIndexB = 3 });
                sortedDispatchPairs[12] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 1, BodyIndexB = 4 });
                sortedDispatchPairs[13] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 1, BodyIndexB = 5 });
                sortedDispatchPairs[14] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 1, BodyIndexB = 6 });
                sortedDispatchPairs[15] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 1, BodyIndexB = 7 });
                sortedDispatchPairs[16] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 1, BodyIndexB = 8 });
                sortedDispatchPairs[17] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 1, BodyIndexB = 9 });
                sortedDispatchPairs[18] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 1, BodyIndexB = 15 });
                sortedDispatchPairs[19] = DispatchPair.CreateCollisionPair(new BodyIndexPair { BodyIndexA = 1, BodyIndexB = 16 });
            }

            CreatePhasedDispatchPairsAndVerify(numDynamicBodies, sortedDispatchPairs);
        }

        // Tests if scheduler correctly fills in the sequential phase in cases in which not all dispatch pairs can be organized into data parallel phases.
        [Test]
        public void TestSchedulerWithSequentialPhase()
        {
            // Create a star shaped connectivity graph with one shared center node and more than 16 adjacent nodes, forming 16 incident edges.
            // This will result in a sequential phase with at least 1 element.

            // 1 center node + N+1 adjacent nodes, where N is the number of phases.
            int numDynamicsBodies = NumPhases + 2;

            // With the last phase being reserved for sequential processing, we expect the first 15 incident
            // edges (dispatch pairs) to end up in data parallel phases and the remaining 2 in the sequential phase.
            // However, given that dispatch pairs are added to phases in batches, and that these batches are assumed to be processed sequentially,
            // we need to create "BatchSize" dispatch pairs between each adjacent node and the center node, to force every phase to be minimally full.
            int numDispatchPairBatches = numDynamicsBodies - 1;
            int numDispatchPairs = numDispatchPairBatches * BatchSize;
            var sortedDispatchPairs = new NativeArray<DispatchPair>(numDispatchPairs, Allocator.Temp);
            var bodyIndexA = 0;
            for (int i = 0; i < numDispatchPairBatches; ++i)
            {
                var bodyIndexB = i + 1;
                // Create BatchSize dispatch pairs between each adjacent node and the center node.
                // Note: we are creating joint pairs, not collision pairs, so that we can create more than one between two bodies.
                // For collision pairs (which will trigger a single contact generation function call per pair in the narrowphase),
                // we can only ever create one per body pair.
                for (int j = 0; j < BatchSize; ++j)
                {
                    sortedDispatchPairs[i * BatchSize + j] = DispatchPair.CreateJoint(new BodyIndexPair { BodyIndexA = bodyIndexA, BodyIndexB = bodyIndexB }, 0, 0);
                }
            }

            CreatePhasedDispatchPairsAndVerify(numDynamicsBodies, sortedDispatchPairs);

            // Verify that we get 16 phases, that the last phase is flagged as sequential, and that it contains 2 dispatch pairs.
            Assert.AreEqual(16, SolverSchedulerInfo.NumActivePhases[0]);
            Assert.AreEqual(2 * BatchSize, SolverSchedulerInfo.PhaseInfo[15].DispatchPairCount);
            Assert.IsTrue(SolverSchedulerInfo.PhaseInfo[15].ContainsDuplicateIndices);
        }
    }
}
