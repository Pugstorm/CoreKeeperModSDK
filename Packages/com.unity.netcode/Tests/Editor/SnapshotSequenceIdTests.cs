using NUnit.Framework;
using Unity.NetCode;
using Unity.NetCode.Tests;
using UnityEngine;

namespace Tests.Editor
{
    public class SnapshotSequenceIdTests
    {
        [Test]
        public void CalculateSequenceIdDelta_Works()
        {
            // Check SSId's that we've confirmed (via ServerTick) are NEW:
            const bool confirmedNewer = true;
            Assert.AreEqual(0, NetworkSnapshotAck.CalculateSequenceIdDelta(5, 5, confirmedNewer));
            Assert.AreEqual(0, NetworkSnapshotAck.CalculateSequenceIdDelta(250, 250, confirmedNewer));
            Assert.AreEqual(1, NetworkSnapshotAck.CalculateSequenceIdDelta(1, 0, confirmedNewer));
            Assert.AreEqual(1, NetworkSnapshotAck.CalculateSequenceIdDelta(2, 1, confirmedNewer));
            Assert.AreEqual(2, NetworkSnapshotAck.CalculateSequenceIdDelta(1, byte.MaxValue, confirmedNewer));
            Assert.AreEqual(10, NetworkSnapshotAck.CalculateSequenceIdDelta(130, 120, confirmedNewer));
            Assert.AreEqual(255, NetworkSnapshotAck.CalculateSequenceIdDelta(5, 6, confirmedNewer));

            // Check SSId's that we've confirmed (via ServerTick) are OLD (i.e. stale):
            const bool confirmedStale = false;
            Assert.AreEqual(0, NetworkSnapshotAck.CalculateSequenceIdDelta(5, 5, confirmedStale));
            Assert.AreEqual(0, NetworkSnapshotAck.CalculateSequenceIdDelta(250, 250, confirmedStale));
            Assert.AreEqual(-1, NetworkSnapshotAck.CalculateSequenceIdDelta(0, 1, confirmedStale));
            Assert.AreEqual(-255, NetworkSnapshotAck.CalculateSequenceIdDelta(0, byte.MaxValue, confirmedStale));
            Assert.AreEqual(-2, NetworkSnapshotAck.CalculateSequenceIdDelta(byte.MaxValue, 1, confirmedStale));
            Assert.AreEqual(-(256 - 10), NetworkSnapshotAck.CalculateSequenceIdDelta(130, 120, confirmedStale));
            Assert.AreEqual(-255, NetworkSnapshotAck.CalculateSequenceIdDelta(6, 5, confirmedStale));
        }

        [Test]
        public void SnapshotSequenceId_Statistics_NetworkPacketLoss_Works()
        {
            // Test transport packet loss:
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.DriverSimulatedDelay = 50;
                testWorld.DriverSimulatedDrop = 20; // Interval, so 5%.

                var stats = RunForAWhile(testWorld);
                // Other kinds of packet loss should not have occurred:
                Assert.Zero(stats.NumPacketsCulledOutOfOrder);
                Assert.Zero(stats.NumPacketsCulledAsArrivedOnSameFrame);
                // Expecting loss here:
                Assert.NotZero(stats.NumPacketsDroppedNeverArrived);
                AssertPercentInRange(stats.NetworkPacketLossPercent, 5, 10); // Could be higher due to low number of samples.
                // Check combined:
                Assert.AreEqual(stats.NumPacketsDroppedNeverArrived, stats.CombinedPacketLossCount);
                AssertPercentInRange(stats.CombinedPacketLossPercent, 5, 10);
            }
        }

        [Test]
        public void SnapshotSequenceId_Statistics_OutOfOrderAndClobbered_Works()
        {
            // Test jitter packet loss (out of order, and multiple arriving on the same frame):
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.DriverSimulatedDelay = 50;
                testWorld.DriverSimulatedJitter = 40;

                var stats = RunForAWhile(testWorld);
                // Other kind of packet loss should not have occurred:
                Assert.LessOrEqual(stats.NumPacketsDroppedNeverArrived, 5); // Statistics will ASSUME there has been some loss, until we confirm it's actually just an out of order packet.
                AssertPercentInRange(stats.NetworkPacketLossPercent, 0, 1);
                // Expecting loss here:
                Assert.NotZero(stats.NumPacketsCulledAsArrivedOnSameFrame);
                AssertPercentInRange(stats.ArrivedOnTheSameFrameClobberedPacketLossPercent, 6, 8);
                Assert.NotZero(stats.NumPacketsCulledOutOfOrder);
                AssertPercentInRange(stats.OutOfOrderPacketLossPercent, 35, 45);
                // Check combined:
                AssertPercentInRange(stats.CombinedPacketLossPercent, 40, 60);
            }
        }



        [Test]
        public void SnapshotSequenceId_Statistics_Combined_Works()
        {
            // Test all of them together:
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.DriverSimulatedDelay = 50;
                testWorld.DriverSimulatedJitter = 40;

                testWorld.DriverSimulatedDrop = 20; // Interval, so 5%.

                var stats = RunForAWhile(testWorld);
                // Expecting loss across all types:
                Assert.NotZero(stats.NumPacketsDroppedNeverArrived);
                AssertPercentInRange(stats.NetworkPacketLossPercent, 2, 4);
                Assert.NotZero(stats.NumPacketsCulledAsArrivedOnSameFrame);
                AssertPercentInRange(stats.ArrivedOnTheSameFrameClobberedPacketLossPercent, 7, 9);
                Assert.NotZero(stats.NumPacketsCulledOutOfOrder);
                AssertPercentInRange(stats.OutOfOrderPacketLossPercent, 30, 50);
                // Check combined:
                AssertPercentInRange(stats.CombinedPacketLossPercent, 45, 55);
            }
        }

        private static void AssertPercentInRange(double perc, int min, int max)
        {
            var percMultiplied = (int)(perc * 100);
            Assert.GreaterOrEqual(percMultiplied, min, $"Percent {perc:P1} within {min} and {max}!");
            Assert.LessOrEqual(percMultiplied, max, $"Percent {perc:P1} within {min} and {max}!");
        }

        private static SnapshotPacketLossStatistics RunForAWhile(NetCodeTestWorld testWorld)
        {
            const float frameTime = 1.0f / 60.0f;
            testWorld.Bootstrap(true);
            var ghostGameObject = new GameObject("RandomGhostToTriggerSnapshotSends");
            ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GhostTypeConverter(GhostTypeConverter.GhostTypes.EnableableComponent, EnabledBitBakedValue.StartEnabledAndWaitForClientSpawn);
            Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));
            testWorld.CreateWorlds(true, 1);
            testWorld.Connect(frameTime, 32); // Packet loss can mess with this!
            testWorld.GoInGame();

            const int seconds = 25;
            for (var i = 0; i < seconds * 60; i++)
                testWorld.Tick(frameTime);

            var stats = testWorld.GetSingleton<NetworkSnapshotAck>(testWorld.ClientWorlds[0]).SnapshotPacketLoss;
            Debug.Log($"Stats after test: {stats.ToFixedString()}!");
            Assert.NotZero(stats.NumPacketsReceived, "Test setup issue!");
            return stats;
        }
    }
}
