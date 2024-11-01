#if !UNITY_DISABLE_MANAGED_COMPONENTS
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities.Conversion;
using Unity.Scenes.Editor;
using Unity.Scenes.Editor.Tests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Unity.Entities.Tests
{
    [Serializable]
    public class TestWithSceneCameraCulling
    {
        [SerializeField] private GameObject[] GameObjects;
        [SerializeField] private Dictionary<GameObject, bool> GameObjectRenderedMap;
        [SerializeField] private bool SceneCameraRendered;
        [SerializeField] private bool IsSetUp;

        public void SetUp(GameObject[] gameObjects)
        {
            if (IsSetUp)
                return;

            IsSetUp = true;
            GameObjects = gameObjects;
            GameObjectRenderedMap = new Dictionary<GameObject, bool>();
            SceneCameraRendered = false;

            Camera.onPostRender += onPostRender;
#if UNITY_2023_2_OR_NEWER
            RenderPipelineManager.endContextRendering += endContextRendering;
#else
            RenderPipelineManager.endFrameRendering += endFrameRendering;
#endif
        }

        public void TearDown()
        {
            if (IsSetUp)
            {
                Camera.onPostRender -= onPostRender;
#if UNITY_2023_2_OR_NEWER
                RenderPipelineManager.endContextRendering -= endContextRendering;
#else
                RenderPipelineManager.endFrameRendering -= endFrameRendering;
#endif
                IsSetUp = false;
            }
        }

        public bool IsGameObjectRendered(GameObject gameObject)
        {
            return GameObjectRenderedMap[gameObject];
        }

        public IEnumerator WaitForSceneCameraRender()
        {
            while (!SceneCameraRendered)
                yield return null;
        }

#if UNITY_2023_2_OR_NEWER
        private void endContextRendering(ScriptableRenderContext arg1, List<Camera> cameras)
        {
            foreach (Camera camera in cameras)
            {
                onPostRender(camera);
            }
        }
#else
        private void endFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
            foreach (Camera camera in cameras)
            {
                onPostRender(camera);
            }
        }
#endif

        private void onPostRender(Camera cam)
        {
            if (!SceneCameraRendered)
            {
                if (cam.cameraType == CameraType.SceneView && GameObjects.Length > 0)
                {
                    foreach(var gameObject in GameObjects)
                    {
                        GameObjectRenderedMap.Add(gameObject, StageUtility.IsGameObjectRenderedByCamera(gameObject, cam));
                    }

                    SceneCameraRendered = true;
                }
            }
        }

    }

    [Serializable]
    [TestFixture]
    class CompanionComponentsEditorTests_Baking
    {
        [SerializeField] public TestWithEditorLiveConversion TestWithEditorLiveConversion;
        [SerializeField] public TestWithSceneCameraCulling TestWithSceneCameraCulling = new TestWithSceneCameraCulling();
        bool _WasBaking;
        bool _WasConversionEnabled;
        bool _WasSceneView;
        SceneView _CreatedSceneView;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            BakingUtility.AddAdditionalCompanionComponentType(typeof(ConversionTestCompanionComponent));
            TestWithEditorLiveConversion.IsBakingEnabled = true;
            if (!TestWithEditorLiveConversion.OneTimeSetUp())
                return;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            TestWithEditorLiveConversion.OneTimeTearDown();

            if (_CreatedSceneView != null)
                _CreatedSceneView.Close();

        }

        [SetUp]
        public void SetUp()
        {
            _WasSceneView = LiveConversionEditorSettings.LiveConversionSceneViewShowRuntime;

            TestWithEditorLiveConversion.SetUp();
        }

        [TearDown]
        public void TearDown()
        {
            LiveConversionEditorSettings.LiveConversionSceneViewShowRuntime = _WasSceneView;

            TestWithSceneCameraCulling.TearDown();
        }

    #if false
        [UnityTest]
        public IEnumerator CompanionComponent_SceneCulling([Values]bool sceneViewShowRuntime)
        {
            LiveConversionEditorSettings.LiveConversionSceneViewShowRuntime = sceneViewShowRuntime;

            var subScene = TestWithEditorLiveConversion.CreateSubSceneFromObjects("TestSubScene", true, () =>
            {
                var go = new GameObject("TestGameObject");
                go.AddComponent<ConversionTestCompanionComponent>();
                return new List<GameObject> {go};
            });

            var world = TestWithEditorLiveConversion.GetLiveConversionWorldForEditMode();
            world.Update();

            SceneView sceneView;
            if (SceneView.sceneViews.Count == 0)
            {
                sceneView = EditorWindow.CreateWindow<SceneView>();
                _CreatedSceneView = sceneView;
            }
            else
            {
                Assert.AreEqual(1, SceneView.sceneViews.Count, "There should only be 1 Scene View for this test");
                sceneView = (SceneView)SceneView.sceneViews[0];
            }

            Assert.IsNotNull(sceneView, "Scene view failed to be created, thus this test can't run");
            sceneView.Focus();

            {
                var objs = Resources.FindObjectsOfTypeAll<ConversionTestCompanionComponent>();

                // Why is it 4?
                // 1- Authoring object
                // 2- Companion object
                Assert.AreEqual(2, objs.Length, "Incorrect number of game objects.");

                var sceneCameras = SceneView.GetAllSceneCameras();
                Assert.AreEqual(1, sceneCameras.Length, "This test should contain one Scene Camera.");

                var expectAuthoringRenderedInSceneView = !sceneViewShowRuntime;
                var expectRuntimeRenderedInSceneView = sceneViewShowRuntime;

                var gameObjects = new GameObject[objs.Length];
                for (int i = 0; i < objs.Length; i++)
                {
                    gameObjects[i] = objs[i].gameObject;
                }
                TestWithSceneCameraCulling.SetUp(gameObjects);

                yield return TestWithSceneCameraCulling.WaitForSceneCameraRender();

                foreach (var obj in objs)
                {
                    var go = obj.gameObject;
                    var goScene = go.scene;

                    var isGameObjectRenderedByCamera = TestWithSceneCameraCulling.IsGameObjectRendered(go);

                    // This is the authoring object
                    if (goScene == subScene.EditingScene)
                    {
                        Assert.AreEqual(expectAuthoringRenderedInSceneView, isGameObjectRenderedByCamera,
                            $"Expect Authoring Object rendered in Scene View={expectAuthoringRenderedInSceneView} but was Rendered={isGameObjectRenderedByCamera}");
                    }
                    else
                    {
                        // This is the CompanionLink object
                        Assert.AreEqual(expectRuntimeRenderedInSceneView, isGameObjectRenderedByCamera,
                            $"Expect Runtime Object rendered in Scene View={expectRuntimeRenderedInSceneView} but was Rendered={isGameObjectRenderedByCamera}");
                    }
                }
            }
        }
    #endif

    }
}
#endif
