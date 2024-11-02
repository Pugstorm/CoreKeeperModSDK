using System.Collections.Generic;
using NUnit.Framework;
using Unity.Core;
using Unity.Mathematics;

namespace Unity.Entities.Tests
{
    public partial class FixedStepSimulationSystemGroupTests : ECSTestsFixture
    {
        partial class RecordUpdateTimesSystem : SystemBase
        {
            public List<TimeData> Updates = new List<TimeData>();
            protected override void OnUpdate()
            {
                Updates.Add(World.Time);
            }
        }

        [Test]
        public void FixedStepSimulationSystemGroup_FirstUpdateAtTimeZero_OneUpdateAtZero()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var updateTimesSystem = World.CreateSystemManaged<RecordUpdateTimesSystem>();
            fixedSimGroup.AddSystemToUpdateList(updateTimesSystem);
            fixedSimGroup.SortSystems();

            fixedSimGroup.Timestep = 1.0f;
            // The first fixed-timestep group update always includes an update at elapsedTime=0
            World.PushTime(new TimeData(0.0f, 0.01f));
            fixedSimGroup.Update();
            World.PopTime();
            CollectionAssert.AreEqual(updateTimesSystem.Updates,
                new[]
                {
                    new TimeData(0.0f, 1.0f),
                });
        }

        [Test]
        public void FixedStepSimulationSystemGroup_FirstUpdateAtTimeEpsilon_OneUpdateAtZero()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var updateTimesSystem = World.CreateSystemManaged<RecordUpdateTimesSystem>();
            fixedSimGroup.AddSystemToUpdateList(updateTimesSystem);
            fixedSimGroup.SortSystems();

            fixedSimGroup.Timestep = 1.0f;
            // The first fixed-timestep group update always includes an update at elapsedTime=0,
            // even if the first elapsedTime we see is non-zero.
            World.PushTime(new TimeData(0.02f, 0.01f));
            fixedSimGroup.Update();
            World.PopTime();
            CollectionAssert.AreEqual(updateTimesSystem.Updates,
                new[]
                {
                    new TimeData(0.0f, 1.0f),
                });
        }

        [Test]
        public void FixedStepSimulationSystemGroup_LargeElapsedTime_UpdateTimesAreCorrect()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var updateTimesSystem = World.CreateSystemManaged<RecordUpdateTimesSystem>();
            fixedSimGroup.AddSystemToUpdateList(updateTimesSystem);
            fixedSimGroup.SortSystems();

            fixedSimGroup.Timestep = 1.0f;
            World.MaximumDeltaTime = 10.0f;
            // Simulate a large elapsed time since the previous frame. (the deltaTime here is irrelevant)
            World.PushTime(new TimeData(8.5f, 0.01f));
            fixedSimGroup.Update();
            World.PopTime();
            CollectionAssert.AreEqual(updateTimesSystem.Updates,
                new[]
                {
                    new TimeData(0.0f, 1.0f),
                    new TimeData(1.0f, 1.0f),
                    new TimeData(2.0f, 1.0f),
                    new TimeData(3.0f, 1.0f),
                    new TimeData(4.0f, 1.0f),
                    new TimeData(5.0f, 1.0f),
                    new TimeData(6.0f, 1.0f),
                    new TimeData(7.0f, 1.0f),
                    new TimeData(8.0f, 1.0f),
                });
        }

        [Test]
        public void FixedStepSimulationSystemGroup_ZeroElapsedTime_NoUpdates()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var updateTimesSystem = World.CreateSystemManaged<RecordUpdateTimesSystem>();
            fixedSimGroup.AddSystemToUpdateList(updateTimesSystem);
            fixedSimGroup.SortSystems();

            fixedSimGroup.Timestep = 1.0f;
            World.MaximumDeltaTime = 10.0f;
            // Simulate a large elapsed time since the previous frame. (the deltaTime here is irrelevant)
            World.PushTime(new TimeData(8.5f, 0.01f));
            fixedSimGroup.Update();
            updateTimesSystem.Updates.Clear();
            // A second update at the exact same time should not trigger an update
            fixedSimGroup.Update();
            World.PopTime();
            Assert.AreEqual(0, updateTimesSystem.Updates.Count);
        }

        [Test]
        public void FixedStepSimulationSystemGroup_SmallElapsedTime_UpdateTimesAreCorrect()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var updateTimesSystem = World.CreateSystemManaged<RecordUpdateTimesSystem>();
            fixedSimGroup.AddSystemToUpdateList(updateTimesSystem);
            fixedSimGroup.SortSystems();

            fixedSimGroup.Timestep = 1.0f;
            World.MaximumDeltaTime = 10.0f;
            // Simulate a large elapsed time since the previous frame. (the deltaTime here is irrelevant)
            World.PushTime(new TimeData(8.5f, 0.01f));
            fixedSimGroup.Update();
            updateTimesSystem.Updates.Clear();
            // A small dt at this point should not trigger an update
            World.PushTime(new TimeData(8.8f, 0.3f));
            fixedSimGroup.Update();
            World.PopTime();
            Assert.AreEqual(0, updateTimesSystem.Updates.Count);
            // A second small dt results in enough accumulated elapsedTime to warrant a new update
            World.PushTime(new TimeData(9.1f, 0.3f));
            fixedSimGroup.Update();
            World.PopTime();
            CollectionAssert.AreEqual(updateTimesSystem.Updates,
                new[]
                {
                    new TimeData(9.0f, 1.0f),
                });
        }

        [Test]
        public void FixedStepSimulationSystemGroup_RuntimeTimestepChange_UpdateTimesAreCorrect()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var updateTimesSystem = World.CreateSystemManaged<RecordUpdateTimesSystem>();
            fixedSimGroup.AddSystemToUpdateList(updateTimesSystem);
            fixedSimGroup.SortSystems();

            fixedSimGroup.Timestep = 1.0f;
            World.MaximumDeltaTime = 10.0f;
            // Simulate several seconds at 1 update per second
            World.PushTime(new TimeData(4.6f, 0.01f));
            fixedSimGroup.Update();
            World.PopTime();
            // Switch to a shorter timestep in the middle of a long timestep.
            // The new dt should take effect starting from the most recent "last update time".
            fixedSimGroup.Timestep = 0.125f;
            World.PushTime(new TimeData(5.0f, 0.01f));
            fixedSimGroup.Update();
            World.PopTime();
            CollectionAssert.AreEqual(updateTimesSystem.Updates,
                new[]
                {
                    new TimeData(0.0f, 1.0f),
                    new TimeData(1.0f, 1.0f),
                    new TimeData(2.0f, 1.0f),
                    new TimeData(3.0f, 1.0f),
                    new TimeData(4.0f, 1.0f),
                    new TimeData(4.125f, 0.125f),
                    new TimeData(4.25f, 0.125f),
                    new TimeData(4.375f, 0.125f),
                    new TimeData(4.5f, 0.125f),
                    new TimeData(4.625f, 0.125f),
                    new TimeData(4.75f, 0.125f),
                    new TimeData(4.875f, 0.125f),
                    new TimeData(5.0f, 0.125f),
                });
        }

        [Test]
        public void FixedStepSimulationSystemGroup_TimestepMinMaxRange_IsValid()
        {
            Assert.Less(0.0f, RateUtils.MinFixedDeltaTime, "minimum fixed timestep must be >0");
            Assert.LessOrEqual(RateUtils.MinFixedDeltaTime, RateUtils.MaxFixedDeltaTime,
                "minimum fixed timestep must be <= maximum fixed timestep");

        }

        [Test]
        public void FixedStepSimulationSystemGroup_TimestepTooLow_ClampedToMinimum()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            fixedSimGroup.Timestep = 0.0f;
            Assert.AreEqual(RateUtils.MinFixedDeltaTime, fixedSimGroup.Timestep);
        }

        [Test]
        public void FixedStepSimulationSystemGroup_TimestepTooHigh_ClampedToMaximum()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            fixedSimGroup.Timestep = RateUtils.MaxFixedDeltaTime + 1.0f;
            Assert.AreEqual(RateUtils.MaxFixedDeltaTime, fixedSimGroup.Timestep);
        }

        [Test]
        public void FixedStepSimulationSystemGroup_TimestepInValidRange_NotClamped()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            float validTimestep =
                math.lerp(RateUtils.MinFixedDeltaTime, RateUtils.MaxFixedDeltaTime, 0.5f);
            fixedSimGroup.Timestep = validTimestep;
                Assert.AreEqual(validTimestep, fixedSimGroup.Timestep);
        }

        [Test]
        public void FixedStepSimulationSystemGroup_DisableFixedTimestep_GroupUpdatesOnce()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var updateTimesSystem = World.CreateSystemManaged<RecordUpdateTimesSystem>();
            fixedSimGroup.AddSystemToUpdateList(updateTimesSystem);
            fixedSimGroup.SortSystems();

            fixedSimGroup.Timestep = 1.0f;
            fixedSimGroup.RateManager = null;
            // Simulate a large elapsed time since the previous frame
            World.PushTime(new TimeData(8.5f, 0.01f));
            fixedSimGroup.Update();
            World.PopTime();
            // with fixed timestep disabled, the group should see the same elapsed/delta times as the World.
            CollectionAssert.AreEqual(updateTimesSystem.Updates,
                new[]
                {
                    new TimeData(8.5f, 0.01f),
                });
        }

        [Test]
        public void FixedStepSimulationSystemGroup_ElapsedTimeExceedsMaximumDeltaTime_GradualRecovery()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var updateTimesSystem = World.CreateSystemManaged<RecordUpdateTimesSystem>();
            fixedSimGroup.AddSystemToUpdateList(updateTimesSystem);
            fixedSimGroup.SortSystems();

            float dt = 0.125f;
            fixedSimGroup.Timestep = dt;
            World.MaximumDeltaTime = 2*dt;
            // Simulate a frame spike
            // The recovery should be spread over several frames; instead of 8 ticks after the first Update(),
            // we should see at most two ticks per update until the group catches up to the elapsed time.
            World.PushTime(new TimeData(7*dt, 0.01f));
            fixedSimGroup.Update();
            CollectionAssert.AreEqual(updateTimesSystem.Updates,
                new[]
                {
                    new TimeData(0*dt, dt), // first Update() always ticks at t=0
                    new TimeData(1*dt, dt),
                    new TimeData(2*dt, dt),
                });
            updateTimesSystem.Updates.Clear();

            fixedSimGroup.Update();
            CollectionAssert.AreEqual(updateTimesSystem.Updates,
                new[]
                {
                    new TimeData(3*dt, dt),
                    new TimeData(4*dt, dt),
                });
            updateTimesSystem.Updates.Clear();

            fixedSimGroup.Update();
            CollectionAssert.AreEqual(updateTimesSystem.Updates,
                new[]
                {
                    new TimeData(5*dt, dt),
                    new TimeData(6*dt, dt),
                });
            updateTimesSystem.Updates.Clear();

            // Now that we've caught up, the next Update() should trigger only one tick.
            fixedSimGroup.Update();
            CollectionAssert.AreEqual(updateTimesSystem.Updates,
                new[]
                {
                    new TimeData(7*dt, dt),
                });
            updateTimesSystem.Updates.Clear();
            World.PopTime();
        }

        [Test]
        public void FixedStepSimulationSystemGroup_NullRateManager_TimestepDoesntThrow()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            fixedSimGroup.RateManager = null;
            Assert.DoesNotThrow(() => { fixedSimGroup.Timestep = 1.0f;});
            Assert.AreEqual(0, fixedSimGroup.Timestep);
        }

        // Simple custom rate manager that updates exactly once per frame. The timestep is ignored, but
        // should be correct if queried.
        class CustomRateManager : IRateManager
        {
            private bool m_UpdatedThisFrame;
            public int UpdateCount { get; private set; }

            public bool ShouldGroupUpdate(ComponentSystemGroup group)
            {
                // if this is true, means we're being called a second or later time in a loop
                if (m_UpdatedThisFrame)
                {
                    m_UpdatedThisFrame = false;
                    return false;
                }

                m_UpdatedThisFrame = true;
                UpdateCount += 1;
                return true;
            }

            public float Timestep { get; set; }
        }

        [Test]
        public void FixedStepSimulationSystemGroup_CustomRateManager_TimestepIsCorrect()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            fixedSimGroup.RateManager = new CustomRateManager();
            const float expectedTimestep = 0.125f;
            fixedSimGroup.Timestep = expectedTimestep;
            Assert.AreEqual(expectedTimestep, fixedSimGroup.Timestep);
        }

        [Test]
        public void FixedStepSimulationSystemGroup_CustomRateManager_UpdateLogicIsCorrect()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var customRateMgr = new CustomRateManager();
            fixedSimGroup.RateManager = customRateMgr;
            fixedSimGroup.Timestep = 1.0f; // Ignored in this test, only the timestep in World.Time.DeltaTime matters
            var updateTimesSystem = World.CreateSystemManaged<RecordUpdateTimesSystem>();
            fixedSimGroup.AddSystemToUpdateList(updateTimesSystem);
            fixedSimGroup.SortSystems();

            Assert.AreEqual(0, customRateMgr.UpdateCount);
            float deltaTime = 0.125f;
            double elapsedTime = 0;
            World.SetTime(new TimeData(elapsedTime, deltaTime));
            fixedSimGroup.Update();
            Assert.AreEqual(1, customRateMgr.UpdateCount);

            elapsedTime += deltaTime;
            World.SetTime(new TimeData(elapsedTime, deltaTime));
            fixedSimGroup.Update();
            Assert.AreEqual(2, customRateMgr.UpdateCount);

            CollectionAssert.AreEqual(updateTimesSystem.Updates,
                new[]
                {
                    new TimeData(0.0f, 0.125f),
                    new TimeData(0.125f, 0.125f),
                });
        }
    }
}
