using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Unity.Entities.Tests
{
    class CompanionComponentsRuntimeTestFixture : ECSTestsFixture
    {
        readonly List<Object> m_ObjectsRequiringDestruction = new List<Object>();

        protected void MarkForAutoDestructionAfterTest(Object o) => m_ObjectsRequiringDestruction.Add(o);

        [TearDown]
        public void DestroyCreatedGameObjects()
        {
            foreach (var o in m_ObjectsRequiringDestruction)
            {
                if (o != null)
                    Object.DestroyImmediate(o);
            }

            m_ObjectsRequiringDestruction.Clear();
        }
    }
}
