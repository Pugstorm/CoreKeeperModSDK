#if !UNITY_DISABLE_MANAGED_COMPONENTS && UNITY_EDITOR
using System.Diagnostics;
using Unity.Scenes;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Unity.Entities
{
    internal static class CompanionGameObjectUtility
    {
        static Scene _companionScene;
        static Scene _companionSceneLiveConversion;

        const HideFlags CompanionFlags =
            HideFlags.HideInHierarchy |
            HideFlags.DontSaveInBuild |
            HideFlags.DontUnloadUnusedAsset |
            HideFlags.NotEditable;

#if DOTS_COMPANION_COMPONENTS_DEBUG_NAME
        private static int _companionNameUniqueId = 0;
#endif

        [Conditional("DOTS_COMPANION_COMPONENTS_DEBUG_NAME")]
        internal static void SetCompanionName(Entity entity, GameObject gameObject)
        {
#if DOTS_COMPANION_COMPONENTS_DEBUG_NAME
            gameObject.name = $"Companion of {entity} (UID {_companionNameUniqueId += 1})";
#endif
        }
        internal static GameObject InstantiateCompanionObject(Entity entity, GameObject sourceGameObject)
        {
            var companion = UnityObject.Instantiate(sourceGameObject);
            SetCompanionName(entity, companion);
            return companion;
        }

        internal static Scene GetCompanionScene(bool isLiveConversion)
        {
            if (_companionScene == default || _companionSceneLiveConversion == default)
                CreateCompanionScenes();

            if (isLiveConversion)
                return _companionSceneLiveConversion;
            else
                return _companionScene;
        }

        internal static void SetCompanionFlags(GameObject gameObject)
        {
            var companionFlags = CompanionFlags;
            if (EditorSceneManager.GetPreviewScenesVisibleInHierarchy())
            {
                companionFlags &= ~HideFlags.HideInHierarchy;
            }

            gameObject.hideFlags = companionFlags;
        }

        internal static void MoveToCompanionScene(GameObject gameObject, bool isLiveConversion)
        {
            var scene = GetCompanionScene(isLiveConversion);
            SetCompanionFlags(gameObject);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
        }

        static void CreateCompanionScenes()
        {
            var previewSceneFlags = PreviewSceneFlags.AllowMonoBehaviourEvents | PreviewSceneFlags.AllowCamerasForRendering | PreviewSceneFlags.IsPreviewScene | PreviewSceneFlags.AllowAutoPlayAudioSources;
            _companionScene = EditorSceneManager.NewPreviewScene(true, previewSceneFlags);
            var companionSceneCullingMask = SceneCullingMasks.DefaultSceneCullingMask;
            _companionScene.name = "CompanionScene";
            EditorSceneManager.SetSceneCullingMask(_companionScene, companionSceneCullingMask);

            _companionSceneLiveConversion = EditorSceneManager.NewPreviewScene(true, previewSceneFlags);
            var companionSceneLiveConversionCullingMask = SceneCullingMasks.GameViewObjects;
            _companionSceneLiveConversion.name = "CompanionSceneLiveConversion";
            EditorSceneManager.SetSceneCullingMask(_companionSceneLiveConversion, companionSceneLiveConversionCullingMask);

            AssemblyReloadEvents.beforeAssemblyReload += AssemblyReloadEventsOnbeforeAssemblyReload;
        }

        private static void AssemblyReloadEventsOnbeforeAssemblyReload()
        {
            EditorSceneManager.ClosePreviewScene(_companionScene);
            EditorSceneManager.ClosePreviewScene(_companionSceneLiveConversion);
        }

        internal static void UpdateLiveConversionCulling(LiveConversionMode liveConversionMode)
        {
            if (liveConversionMode == LiveConversionMode.SceneViewShowsAuthoring)
            {
                // When Scene View is showing Editing State, we set the companion scene culling to GameView so that it does NOT render in the Scene View
                EditorSceneManager.SetSceneCullingMask(_companionSceneLiveConversion, SceneCullingMasks.GameViewObjects);
                _companionSceneLiveConversion.name = "CompanionSceneLiveConversion - (Game View Only)";
            }
            else if (liveConversionMode == LiveConversionMode.SceneViewShowsRuntime)
            {
                // When Scene View is showing Live Game State, we set the companion scene culling to Default so show the same in Scene AND Game View
                EditorSceneManager.SetSceneCullingMask(_companionSceneLiveConversion, SceneCullingMasks.DefaultSceneCullingMask);
                _companionSceneLiveConversion.name = "CompanionSceneLiveConversion - (Scene and Game View)";
            }
            else
            {
                _companionSceneLiveConversion.name = "CompanionSceneLiveConversion - (Inactive)";
            }
        }
    }
}
#endif
