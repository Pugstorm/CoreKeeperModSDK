using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Tests.Authoring;
using Unity.Physics;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.SceneManagement;

namespace Tests.EditModeTests
{
    class ColliderConversionSystemSubSceneIntegrationTests : BaseHierarchyConversionTest
    {
        UnityEngine.Mesh NonReadableMesh { get; set; }
        World PreviousGameObjectInjectionWorld { get; set; }
        PlayerLoopSystem PreviousPlayerLoop { get; set; }
        string TemporaryAssetsPath { get; set; }
        private static readonly Regex k_NonWords = new Regex(@"\W");

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // set up temporary GameObject injection world
            PreviousGameObjectInjectionWorld = World.DefaultGameObjectInjectionWorld;
            PreviousPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            World.DefaultGameObjectInjectionWorld = default;
            DefaultWorldInitialization.DefaultLazyEditModeInitialize();

            // create folder for temporary assets
            TemporaryAssetsPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder("Assets", "SubScene_IntegrationTests"));

            // create non-readable mesh asset
            NonReadableMesh = UnityEngine.Mesh.Instantiate(Resources.GetBuiltinResource<UnityEngine.Mesh>("New-Cylinder.fbx"));
            NonReadableMesh.UploadMeshData(true);
            Assume.That(NonReadableMesh.isReadable, Is.False, $"{NonReadableMesh} was readable.");
            AssetDatabase.CreateAsset(NonReadableMesh, $"{TemporaryAssetsPath}/NonReadableMesh.asset");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // restore GameObject injection world
            if (World.DefaultGameObjectInjectionWorld != default)
                World.DefaultGameObjectInjectionWorld.Dispose();
            if (PreviousGameObjectInjectionWorld != default && !PreviousGameObjectInjectionWorld.IsCreated)
                PreviousGameObjectInjectionWorld = default;
            World.DefaultGameObjectInjectionWorld = PreviousGameObjectInjectionWorld;
            PlayerLoop.SetPlayerLoop(PreviousPlayerLoop);

            // open an empty scene
            EditorSceneManager.SetActiveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene));

            //if (NonReadableMesh != null)
            //    UnityMesh.DestroyImmediate(NonReadableMesh);

            // clean up scene dependency cache
            const string k_SceneDependencyCachePath = "Assets/SceneDependencyCache";
            if (AssetDatabase.IsValidFolder(k_SceneDependencyCachePath))
                AssetDatabase.DeleteAsset(k_SceneDependencyCachePath);

            // delete all temporary assets
            AssetDatabase.DeleteAsset(TemporaryAssetsPath);
        }

        string TestNameWithoutSpecialCharacters =>
            k_NonWords.Replace(TestContext.CurrentContext.Test.Name, string.Empty);

        SubScene CreateSubScene(Action createSubSceneObjects)
        {
            // create sub-scene with objects
            var subScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var subScenePath = $"{TemporaryAssetsPath}/{TestNameWithoutSpecialCharacters}-subScene.unity";
            AssetDatabase.DeleteAsset(subScenePath);
            createSubSceneObjects.Invoke();
            EditorSceneManager.SaveScene(subScene, subScenePath);

            // create parent scene
            var parentScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var parentScenePath = $"{TemporaryAssetsPath}/{TestNameWithoutSpecialCharacters}-parentScene.unity";
            AssetDatabase.DeleteAsset(parentScenePath);

            // create GameObject with SubScene component
            var subSceneMB = new GameObject(subScene.name).AddComponent<SubScene>();
            Undo.RecordObject(subSceneMB, "Assign sub-scene");
            subSceneMB.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(subScenePath);
            subSceneMB.AutoLoadScene = true;
            EditorSceneManager.SaveScene(parentScene, parentScenePath);
            SceneManager.SetActiveScene(parentScene);

            return subSceneMB;
        }

        protected void CreateSubSceneAndAssert<T>(Action<T> configureSubSceneObject, ColliderType expectedColliderType)
            where T : Component
        {
            // create sub-scene
            var subScene = CreateSubScene(() =>
            {
                var shape = new GameObject(TestNameWithoutSpecialCharacters).AddComponent<T>();
                configureSubSceneObject(shape);
            });

            // convert and load sub-scene
            var world = World.DefaultGameObjectInjectionWorld;
            var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, subScene.SceneGUID, new SceneSystem.LoadParameters
            {
                Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
            });
            // TODO: Editor doesn't update if it doesn't have focus, so we must explicitly update the world to process the load.
            world.Update();

            // check result
            using (var group = world.EntityManager.CreateEntityQuery(typeof(PhysicsCollider)))
            using (var bodies = group.ToComponentDataArray<PhysicsCollider>(Allocator.Persistent))
            {
                Assume.That(bodies, Has.Length.EqualTo(1));
                Assume.That(bodies[0].IsValid, Is.True);
                Assert.That(bodies[0].Value.Value.Type, Is.EqualTo(expectedColliderType));
            }

            SceneSystem.UnloadScene(world.Unmanaged, sceneEntity);
        }

        [Test]
        public void ColliderConversionSystem_WhenColliderIsConvexWithNonReadableMesh_IsInSubScene_DoesNotThrow() =>
            CreateSubSceneAndAssert<UnityEngine.MeshCollider>(
                shape =>
                {
                    shape.sharedMesh = NonReadableMesh;
                    shape.convex = true;
                },
                ColliderType.Convex
            );

        [Test]
        public void ColliderConversionSystem_WhenColliderIsMeshWithNonReadableMesh_IsInSubScene_DoesNotThrow() =>
            CreateSubSceneAndAssert<UnityEngine.MeshCollider>(
                shape =>
                {
                    shape.sharedMesh = NonReadableMesh;
                    shape.convex = false;
                },
                ColliderType.Mesh
            );
    }
}
