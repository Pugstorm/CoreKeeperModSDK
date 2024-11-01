using System;
using NUnit.Framework;
using Unity.Mathematics;

namespace Unity.Entities.Tests
{
    public class WorldTimeTests : ECSTestsFixture
    {
        [Test]
        public void WorldTime_NewWorld_ElapsedTimeIsZero()
        {
            using (var world = new World("World A"))
            {
                Assert.AreEqual(0, world.Time.ElapsedTime, "Brand new World does not have Time.ElapsedTime=0");
            }
        }

        [Test]
        public void WorldTime_FirstTick_ElapsedTimeIsZero()
        {
            using (var world = new World("World A"))
            {
                var init = world.GetOrCreateSystemManaged<InitializationSystemGroup>();

                var unityTimeSys = world.GetOrCreateSystemManaged(typeof(UpdateWorldTimeSystem));
                init.AddSystemToUpdateList(unityTimeSys);

                world.Update();
                Assert.AreEqual(0, world.Time.ElapsedTime, "ElapsedTime for first World update is not zero");
                Assert.AreEqual(math.min(UnityEngine.Time.deltaTime, world.MaximumDeltaTime), world.Time.DeltaTime);
            }
        }

        [Test]
        public void WorldTime_UnityEngineDeltaTimeDrivesTime()
        {
            using (var world = new World("World A"))
            {
                var init = world.GetOrCreateSystemManaged<InitializationSystemGroup>();

                var unityTimeSys = world.GetOrCreateSystemManaged(typeof(UpdateWorldTimeSystem));
                init.AddSystemToUpdateList(unityTimeSys);

                float lastDeltaTime = math.min(UnityEngine.Time.deltaTime, world.MaximumDeltaTime);
                double expectedElapsedTime = 0;

                // Ideally this should be a playmode test that we run for several frames, so that UnityEngine.Time.deltaTime
                // would vary for each update. For now we'll just pretend the deltaTime is the same for each frame.
                for (int i = 0; i < 10; ++i)
                {
                    world.Update();
                    Assert.AreEqual(expectedElapsedTime, world.Time.ElapsedTime);
                    float newDeltaTime = math.min(UnityEngine.Time.deltaTime, world.MaximumDeltaTime);
                    Assert.AreEqual(newDeltaTime, world.Time.DeltaTime);
                    lastDeltaTime = newDeltaTime;
                    expectedElapsedTime += lastDeltaTime;
                }
            }
        }

        [Test]
        public void WorldTime_MaximumDeltaTime_ClampsDeltaTime()
        {
            // Sometimes, the engine reports a deltaTime of 0 while running this test, and we can't possibly
            // clamp that. Ideally we'd be able to substitute in an arbitrary deltaTime from the engine;
            // Instead, we just skip the test if the engine's reported deltaTime would be too small to clamp.
            const float kMaxDeltaTime = 1.0f / 256.0f;
            if (UnityEngine.Time.deltaTime <= kMaxDeltaTime)
                return;
            using (var world = new World("World A"))
            {
                var init = world.GetOrCreateSystemManaged<InitializationSystemGroup>();

                var unityTimeSys = world.GetOrCreateSystemManaged(typeof(UpdateWorldTimeSystem));
                init.AddSystemToUpdateList(unityTimeSys);

                // Ideally this should be a playmode test that we run for several frames, so that UnityEngine.Time.deltaTime
                // would vary for each update. For now we'll just pretend the deltaTime is the same for each frame.
                world.MaximumDeltaTime = kMaxDeltaTime;
                Assert.Greater(UnityEngine.Time.deltaTime, world.MaximumDeltaTime); // max delta time *must* affect this test
                double expectedElapsedTime = 0;

                for (int i = 0; i < 10; ++i)
                {
                    world.Update();
                    Assert.AreEqual(expectedElapsedTime, world.Time.ElapsedTime);
                    Assert.AreEqual(world.MaximumDeltaTime, world.Time.DeltaTime);
                    expectedElapsedTime += world.Time.DeltaTime;
                }
            }
        }
    }
}
