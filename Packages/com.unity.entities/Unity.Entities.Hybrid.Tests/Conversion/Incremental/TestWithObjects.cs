using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Entities.Tests.Conversion
{
    [System.Serializable]
    internal struct TestWithObjects
    {
        private List<Object> m_ObjectsToDestroy;

        public GameObject CreateGameObject(Type component)
        {
            var go = new GameObject("Object", component);
            m_ObjectsToDestroy.Add(go);
            return go;
        }

        public GameObject CreateGameObject(string name, Type component)
        {
            var go = new GameObject(name, component);
            m_ObjectsToDestroy.Add(go);
            return go;
        }

        public GameObject CreateGameObject(string name) {
            var go = new GameObject(name);
            m_ObjectsToDestroy.Add(go);
            return go;
        }

        public GameObject CreateGameObject() {
            var go = new GameObject();
            m_ObjectsToDestroy.Add(go);
            return go;
        }

        public GameObject CreatePrimitive(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            RegisterForDestruction(go);
            return go;
        }

        public void RegisterForDestruction(Object obj) => m_ObjectsToDestroy.Add(obj);

        public void SetUp()
        {
            if (m_ObjectsToDestroy != null)
                m_ObjectsToDestroy.Clear();
            else
                m_ObjectsToDestroy = new List<Object>();
        }

        public void TearDown()
        {
            foreach (var go in m_ObjectsToDestroy)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            m_ObjectsToDestroy.Clear();
        }
    }
}
