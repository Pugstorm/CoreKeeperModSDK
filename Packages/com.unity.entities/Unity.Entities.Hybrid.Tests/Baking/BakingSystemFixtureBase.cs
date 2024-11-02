using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Entities.Tests;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.Entities.Hybrid.Tests.Baking
{
    public class BakingSystemFixtureBase : ECSTestsFixture
    {
        protected enum DestructionBy
        {
            Fixture,
            Test
        }

        struct ObjectAndName
        {
            public UnityObject obj;
            public string name;
        }

        readonly List<ObjectAndName> m_ObjectsDestroyedByFixture = new List<ObjectAndName>();
        readonly List<UnityObject> m_ObjectsDestroyedByTest = new List<UnityObject>();
        readonly Dictionary<string, int> m_ObjectNames = new Dictionary<string, int>();

        internal BakingSettings MakeDefaultSettings() => new BakingSettings
        {
            BakingFlags = BakingUtility.BakingFlags.AssignName | BakingUtility.BakingFlags.AddEntityGUID
        };

        [TearDown]
        public override void TearDown()
        {
            // Making a copy so we can clean the lists before running the asserts, otherwise a failure will propagate to the subsequent tests
            var destroyedByFixture = m_ObjectsDestroyedByFixture.ToArray();
            var destroyedByTest = m_ObjectsDestroyedByTest.ToArray();

            m_ObjectsDestroyedByFixture.Clear();
            m_ObjectsDestroyedByTest.Clear();

            Array.ForEach(destroyedByFixture, item =>
                Assert.IsFalse(item.obj == null, $"GameObject {item.name} has been destroyed but was expected to still exist after test completion"));
            Array.ForEach(destroyedByTest, go =>
                Assert.IsTrue(go == null, $"GameObject {go} was expected to be destroyed before test completion but wasn't"));

            Array.ForEach(destroyedByFixture, item => UnityObject.DestroyImmediate(item.obj));
        }

        T RegisterUnityObject<T>(T uobject, DestructionBy destructionBy = DestructionBy.Fixture)
            where T : UnityObject
        {
            if (destructionBy == DestructionBy.Fixture)
                m_ObjectsDestroyedByFixture.Add(new ObjectAndName { obj = uobject, name = uobject.name });
            else if (destructionBy == DestructionBy.Test)
                m_ObjectsDestroyedByTest.Add(uobject);

            return uobject;
        }

        string MakeName(string name)
        {
            // keep unique to help disambiguate in test debugging
            if (m_ObjectNames.TryGetValue(name, out var serial))
                name += serial;
            m_ObjectNames[name] = serial + 1;

            return name;
        }

        protected GameObject CreateGameObject(string name, params Type[] components)
            => CreateGameObject(name, DestructionBy.Fixture, components);

        protected GameObject CreateGameObject(string name, DestructionBy destructionBy, params Type[] components) =>
            RegisterUnityObject(new GameObject(MakeName(name ?? "go"), components), destructionBy);

        protected GameObject CreateGameObject()
            => CreateGameObject(null);

        protected GameObject CreateGameObject(DestructionBy destructionBy)
            => CreateGameObject(null, destructionBy);

        protected GameObject InstantiateGameObject(GameObject go, string name = null)
        {
            var instantiated = UnityObject.Instantiate(go);
            instantiated.name = MakeName($"{name ?? "go"} ({go.name})");
            return RegisterUnityObject(instantiated);
        }

        protected GameObject InstantiatePrefab(string name)
        {
            var prefab = LoadPrefab(name);
            var instantiated = UnityObject.Instantiate(prefab);
            instantiated.name = MakeName($"{name ?? "prefab"} ({prefab.name})");
            return RegisterUnityObject(instantiated);
        }

        protected T CreateScriptableObject<T>(string name = null)
            where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            obj.name = MakeName(name ?? typeof(T).Name.ToLower());
            return RegisterUnityObject(obj);
        }

        protected static T LoadAsset<T>(string name) where T : UnityObject
        {
            var path = $"Packages/com.unity.entities/Unity.Entities.Hybrid.Tests/Prefabs/{name}";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new Exception($"Failed to load asset {typeof(T).Name} at '{path}'");

            return asset;
        }

        protected static T LoadScriptableObject<T>(string name) where T : ScriptableObject
            => LoadAsset<T>($"{name}.asset");
        protected static ScriptableObject LoadScriptableObject(string name)
            => LoadScriptableObject<ScriptableObject>(name);
        protected static GameObject LoadPrefab(string name)
            => LoadAsset<GameObject>($"{name}.prefab");
    }
}
