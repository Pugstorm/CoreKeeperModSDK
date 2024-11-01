using NUnit.Framework;
using Unity.Entities.Hybrid.Tests;
using UnityEngine.LowLevel;
using UnityEngine.TestTools;

namespace Unity.Entities.Tests
{
    public class DefaultWorldInitializationEndToEndTests
    {
        TestWithCustomDefaultGameObjectInjectionWorld m_DefaultWorld;
        private PlayerLoopSystem m_PrevPlayerLoop;

        [SetUp]
        public void Setup()
        {
            m_PrevPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            m_DefaultWorld.Setup();
        }

        [Test]
        public void Initialize_ShouldLogNothing()
        {
            DefaultWorldInitialization.Initialize("Test World", true);

            LogAssert.NoUnexpectedReceived();
        }

        [TearDown]
        public void TearDown()
        {
            m_DefaultWorld.TearDown();
            PlayerLoop.SetPlayerLoop(m_PrevPlayerLoop);
        }
    }
}
