using System;
using NUnit.Framework;
using Unity.Entities.Hybrid.Tests;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Unity.Entities.Tests
{
    public class WorldScriptUpdateOrderTests
    {
        TestWithCustomDefaultGameObjectInjectionWorld m_DefaultWorld = default;
        PlayerLoopSystem m_PrevPlayerLoop;

        [SetUp]
        public void Setup()
        {
            m_PrevPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());
            m_DefaultWorld.Setup();
        }

        [Test, Explicit]
        public void AddRemoveScriptUpdate()
        {
            DefaultWorldInitialization.Initialize("Test World", true);

            using var newWorld = new World("WorldA");
            newWorld.CreateSystemManaged<InitializationSystemGroup>();
            newWorld.CreateSystemManaged<SimulationSystemGroup>();
            newWorld.CreateSystemManaged<PresentationSystemGroup>();
            Assert.IsFalse(ScriptBehaviourUpdateOrder.IsWorldInCurrentPlayerLoop(newWorld));

            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(newWorld);
            Assert.IsTrue(ScriptBehaviourUpdateOrder.IsWorldInCurrentPlayerLoop(newWorld));

            PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());
            Assert.IsFalse(ScriptBehaviourUpdateOrder.IsWorldInCurrentPlayerLoop(newWorld));

            var playerLoop = PlayerLoop.GetDefaultPlayerLoop();
            ScriptBehaviourUpdateOrder.AppendWorldToPlayerLoop(World.DefaultGameObjectInjectionWorld, ref playerLoop);
            PlayerLoop.SetPlayerLoop(playerLoop);
            Assert.IsTrue(ScriptBehaviourUpdateOrder.IsWorldInCurrentPlayerLoop(World.DefaultGameObjectInjectionWorld));
        }

        [Test]
        public void IsInPlayerLoop_WorldNotInPlayerLoop_ReturnsFalse()
        {
            using (var world = new World("Test World"))
            {
                world.CreateSystemManaged<InitializationSystemGroup>();
                var playerLoop = PlayerLoop.GetDefaultPlayerLoop();
                Assert.IsFalse(ScriptBehaviourUpdateOrder.IsWorldInPlayerLoop(world, playerLoop));
            }
        }

        [Test]
        public void IsInPlayerLoop_WorldInPlayerLoop_ReturnsTrue()
        {
            using (var world = new World("Test World"))
            {
                world.CreateSystemManaged<InitializationSystemGroup>();
                var playerLoop = PlayerLoop.GetDefaultPlayerLoop();
                ScriptBehaviourUpdateOrder.AppendWorldToPlayerLoop(world, ref playerLoop);
                Assert.IsTrue(ScriptBehaviourUpdateOrder.IsWorldInPlayerLoop(world, playerLoop));
            }
        }

        [Test]
        public void RemoveFromPlayerLoop_WorldNotInPlayerLoop_DoesntThrow()
        {
            using (var world = new World("Test World"))
            {
                world.CreateSystemManaged<InitializationSystemGroup>();
                var playerLoop = PlayerLoop.GetDefaultPlayerLoop();
                ScriptBehaviourUpdateOrder.RemoveWorldFromPlayerLoop(world, ref playerLoop);
                Assert.IsFalse(ScriptBehaviourUpdateOrder.IsWorldInPlayerLoop(world, playerLoop));
            }
        }

        [Test]
        public void RemoveFromPlayerLoop_WorldInPlayerLoop_Works()
        {
            using (var world = new World("Test World"))
            {
                world.CreateSystemManaged<InitializationSystemGroup>();
                var playerLoop = PlayerLoop.GetDefaultPlayerLoop();
                ScriptBehaviourUpdateOrder.AppendWorldToPlayerLoop(world, ref playerLoop);
                Assert.IsTrue(ScriptBehaviourUpdateOrder.IsWorldInPlayerLoop(world, playerLoop));
                ScriptBehaviourUpdateOrder.RemoveWorldFromPlayerLoop(world, ref playerLoop);
                Assert.IsFalse(ScriptBehaviourUpdateOrder.IsWorldInPlayerLoop(world, playerLoop));
            }
        }

        [Test]
        public void AddToPlayerLoop_AddTwoWorlds_BothAreAdded()
        {
            using (var worldA = new World("Test World A"))
            using (var worldB = new World("Test World B"))
            {
                worldA.CreateSystemManaged<InitializationSystemGroup>();
                worldB.CreateSystemManaged<InitializationSystemGroup>();
                var playerLoop = PlayerLoop.GetDefaultPlayerLoop();
                ScriptBehaviourUpdateOrder.AppendWorldToPlayerLoop(worldA, ref playerLoop);
                Assert.IsTrue(ScriptBehaviourUpdateOrder.IsWorldInPlayerLoop(worldA, playerLoop));
                ScriptBehaviourUpdateOrder.AppendWorldToPlayerLoop(worldB, ref playerLoop);
                Assert.IsTrue(ScriptBehaviourUpdateOrder.IsWorldInPlayerLoop(worldA, playerLoop));
                Assert.IsTrue(ScriptBehaviourUpdateOrder.IsWorldInPlayerLoop(worldB, playerLoop));
            }
        }

        [Test]
        public void RemoveFromPlayerLoop_OtherWorldsInPlayerLoop_NotAffected()
        {
            using (var worldA = new World("Test World A"))
            using (var worldB = new World("Test World B"))
            {
                worldA.CreateSystemManaged<InitializationSystemGroup>();
                worldB.CreateSystemManaged<InitializationSystemGroup>();
                var playerLoop = PlayerLoop.GetDefaultPlayerLoop();
                ScriptBehaviourUpdateOrder.AppendWorldToPlayerLoop(worldA, ref playerLoop);
                ScriptBehaviourUpdateOrder.AppendWorldToPlayerLoop(worldB, ref playerLoop);
                Assert.IsTrue(ScriptBehaviourUpdateOrder.IsWorldInPlayerLoop(worldA, playerLoop));
                Assert.IsTrue(ScriptBehaviourUpdateOrder.IsWorldInPlayerLoop(worldB, playerLoop));

                ScriptBehaviourUpdateOrder.RemoveWorldFromPlayerLoop(worldA, ref playerLoop);
                Assert.IsFalse(ScriptBehaviourUpdateOrder.IsWorldInPlayerLoop(worldA, playerLoop));
                Assert.IsTrue(ScriptBehaviourUpdateOrder.IsWorldInPlayerLoop(worldB, playerLoop));
            }
        }

        class InvalidPlayerLoopSystemType
        {
        }

        partial class TestSystem : ComponentSystemBase
        {
            public override void Update()
            {
                throw new System.NotImplementedException();
            }
        }

        [Test]
        public void AppendSystemToPlayerLoopList_InvalidPlayerLoopSystemType_Throws()
        {
            using (var world = new World("Test World"))
            {
                var sys = world.CreateSystemManaged<TestSystem>();
                var playerLoop = PlayerLoop.GetCurrentPlayerLoop();
                Assert.That(
                    () => ScriptBehaviourUpdateOrder.AppendSystemToPlayerLoop(sys, ref playerLoop,
                        typeof(InvalidPlayerLoopSystemType)),
                    Throws.ArgumentException.With.Message.Matches(
                        @"Could not find PlayerLoopSystem with type=.+InvalidPlayerLoopSystemType"));
            }
        }

        bool IsSystemInSubsystemList(PlayerLoopSystem[] subsystemList, ComponentSystemBase system)
        {
            if (subsystemList == null)
                return false;
            for (int i = 0; i < subsystemList.Length; ++i)
            {
                var pls = subsystemList[i];
                if (typeof(ComponentSystemBase).IsAssignableFrom(pls.type))
                {
                    var wrapper = pls.updateDelegate.Target as ScriptBehaviourUpdateOrder.DummyDelegateWrapper;
                    if (wrapper.System == system)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        void ValidatePostAppendPlayerLoop(PlayerLoopSystem playerLoop, Type targetStageType, ComponentSystemBase system)
        {
            if (playerLoop.type == targetStageType)
                Assert.IsTrue(IsSystemInSubsystemList(playerLoop.subSystemList, system));
            else
                Assert.IsFalse(IsSystemInSubsystemList(playerLoop.subSystemList, system));

            if (playerLoop.subSystemList != null)
            {
                for (int i = 0; i < playerLoop.subSystemList.Length; ++i)
                {
                    ValidatePostAppendPlayerLoop(playerLoop.subSystemList[i], targetStageType, system);
                }
            }
        }

        [Test]
        public void AppendSystemToPlayerLoopList_AddToNestedList_Works()
        {
            using (var world = new World("Test World"))
            {
                var playerLoop = PlayerLoop.GetDefaultPlayerLoop();
                var sys = world.CreateSystemManaged<TestSystem>();
                Type targetStageType = typeof(PreLateUpdate.LegacyAnimationUpdate);
                ScriptBehaviourUpdateOrder.AppendSystemToPlayerLoop(sys, ref playerLoop, targetStageType);
                ValidatePostAppendPlayerLoop(playerLoop, targetStageType, sys);
            }
        }

        [Test]
        public void CurrentPlayerLoopWrappers_Work()
        {
            using (var world = new World("Test World"))
            {
                // world must have at least one of the default top-level groups to add
                var initSysGroup = world.CreateSystemManaged<InitializationSystemGroup>();

                Assert.IsFalse(ScriptBehaviourUpdateOrder.IsWorldInCurrentPlayerLoop(world));
                ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
                Assert.IsTrue(ScriptBehaviourUpdateOrder.IsWorldInCurrentPlayerLoop(world));
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(world);
                Assert.IsFalse(ScriptBehaviourUpdateOrder.IsWorldInCurrentPlayerLoop(world));
            }
        }

        [TearDown]
        public void TearDown()
        {
            m_DefaultWorld.TearDown();
            PlayerLoop.SetPlayerLoop(m_PrevPlayerLoop);
        }
    }
}
