using System;
using System.Linq;
using NUnit.Framework;
using Unity.Entities.Hybrid.Tests;
using UnityEngine.LowLevel;

namespace Unity.Entities.Tests
{
    public class DefaultWorldInitializationTests
    {
        World m_World;
        TestWithCustomDefaultGameObjectInjectionWorld m_CustomInjectionWorld;
        private PlayerLoopSystem m_PrevPlayerLoop;

        [OneTimeSetUp]
        public void Setup()
        {
            m_PrevPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            m_CustomInjectionWorld.Setup();
            DefaultWorldInitialization.Initialize("TestWorld", false);
            m_World = World.DefaultGameObjectInjectionWorld;
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            m_CustomInjectionWorld.TearDown();
            PlayerLoop.SetPlayerLoop(m_PrevPlayerLoop);
        }

        [Test]
        public void Systems_CalledViaGetOrCreateSystem_AreCreated()
        {
            m_World.GetOrCreateSystemManaged<SystemWithGetOrCreate>();
            Assert.IsNotNull(m_World.GetExistingSystemManaged<GetOrCreateTargetSystem>(), $"{nameof(GetOrCreateTargetSystem)} was not automatically created");
        }

        [Test]
        public void Systems_WithCyclicReferences_AreAllCreated()
        {
            m_World.GetOrCreateSystemManaged<CyclicReferenceSystemA>();
            Assert.IsNotNull(m_World.GetExistingSystemManaged<CyclicReferenceSystemA>(), nameof(CyclicReferenceSystemA) + " was not created");
            Assert.IsNotNull(m_World.GetExistingSystemManaged<CyclicReferenceSystemB>(), nameof(CyclicReferenceSystemB) + " was not created");
            Assert.IsNotNull(m_World.GetExistingSystemManaged<CyclicReferenceSystemC>(), nameof(CyclicReferenceSystemC) + " was not created");
        }

        partial class SystemWithGetOrCreate : SystemBase
        {
            protected override void OnCreate()
            {
                base.OnCreate();
                World.GetOrCreateSystemManaged<GetOrCreateTargetSystem>();
            }

            protected override void OnUpdate()
            {
            }
        }

        partial class GetOrCreateTargetSystem : SystemBase
        {
            protected override void OnUpdate()
            {
            }
        }

        partial class CyclicReferenceSystemA : SystemBase
        {
            protected override void OnCreate()
            {
                base.OnCreate();
                World.GetOrCreateSystemManaged<CyclicReferenceSystemB>();
            }

            protected override void OnUpdate() {}
        }

        partial class CyclicReferenceSystemB : SystemBase
        {
            protected override void OnCreate()
            {
                base.OnCreate();
                World.GetOrCreateSystemManaged<CyclicReferenceSystemC>();
            }

            protected override void OnUpdate() {}
        }

        partial class CyclicReferenceSystemC : SystemBase
        {
            protected override void OnCreate()
            {
                base.OnCreate();
                World.GetOrCreateSystemManaged<CyclicReferenceSystemA>();
            }

            protected override void OnUpdate() {}
        }
    }
}
