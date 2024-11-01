using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Entities.Tests;
using UnityEngine;

namespace Unity.Entities.Hybrid.Tests
{
    public class CompanionComponentTestFixture
    {
        protected World m_PreviousWorld;
        protected World m_World;
        protected EntityManager m_Manager;

        protected List<GameObject> m_GameObjects = new List<GameObject>();

        protected GameObject CreateGameObject()
        {
            var go = new GameObject();
            m_GameObjects.Add(go);
            return go;
        }

        [SetUp]
        virtual public void Setup()
        {
            m_PreviousWorld = World.DefaultGameObjectInjectionWorld;
            m_World = TestWorldSetup.CreateEntityWorld("Test World", false);
            World.DefaultGameObjectInjectionWorld = m_World;
            m_Manager = m_World.EntityManager;
        }

        [TearDown]
        virtual public void TearDown()
        {
            if (m_World.IsCreated)
            {
                m_World.Dispose();
                m_World = null;

                World.DefaultGameObjectInjectionWorld = m_PreviousWorld;
                m_PreviousWorld = null;
                m_Manager = default;
            }

            foreach (var go in m_GameObjects)
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
