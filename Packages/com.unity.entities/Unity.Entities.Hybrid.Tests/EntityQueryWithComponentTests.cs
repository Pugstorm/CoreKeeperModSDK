using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Entities.Tests
{
    class EntityQueryWithUnityComponentTests : ECSTestsFixture
    {
        [Test]
        public void ToComponentArrayContainsAllInstances()
        {
            var go1 = new GameObject();
            var go2 = new GameObject();

            try
            {
                {
                    var entity = m_Manager.CreateEntity();
                    m_Manager.AddComponentObject(entity, go1.transform);
                }
                {
                    var entity = m_Manager.CreateEntity();
                    m_Manager.AddComponentObject(entity, go2.transform);
                }

                var query = EmptySystem.GetEntityQuery(typeof(Transform));
                var arr = query.ToComponentArray<Transform>();
                Assert.AreEqual(2, arr.Length);
                Assert.That(arr.Any(t => ReferenceEquals(t, go1.transform)), "Output doesn't contain transform 1");
                Assert.That(arr.Any(t => ReferenceEquals(t, go2.transform)), "Output doesn't contain transform 2");
            }
            finally
            {
                Object.DestroyImmediate(go1);
                Object.DestroyImmediate(go2);
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void ToComponentArrayManagedComponent()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddComponentData(entity, new EcsTestManagedComponent
            {
                value = "entity 1"
            });
            var entity2 = m_Manager.CreateEntity();
            m_Manager.AddComponentData(entity2, new EcsTestManagedComponent
            {
                value = "entity 2"
            });
            var entity3 = m_Manager.CreateEntity();
            m_Manager.AddComponentObject(entity3, new EcsTestManagedComponent2
            {
                value = "entity 3"
            });

            var query = EmptySystem.GetEntityQuery(typeof(EcsTestManagedComponent));
            var arr = query.ToComponentArray<EcsTestManagedComponent>();
            Assert.AreEqual(2, arr.Length);
            Assert.AreEqual("entity 1",arr[0].value);
            Assert.AreEqual("entity 2",arr[1].value);

        }
#endif

    }
}
