using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Entities;
using Unity.Entities.Conversion;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes.Editor
{
    [CustomEditor(typeof(SubScene))]
    [CanEditMultipleObjects]
    class SubSceneInspector : UnityEditor.Editor
    {
        SceneAsset[] m_PreviousSceneAssets;
        private SubScene[] _selectedSubscenes;

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            _selectedSubscenes = new SubScene[targets.Length];
            var targetsArray = targets;
            targetsArray.CopyTo(_selectedSubscenes, 0);

            SubSceneInspectorUtility.WantsRepaint += this.Repaint;
        }

        private void OnDisable()
        {
            SubSceneInspectorUtility.WantsRepaint -= this.Repaint;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        void OnUndoRedoPerformed()
        {
            // The referenced Scene Asset can have changed when undo/redo happens so we ensure to
            // reload the Hierarchy which depends on the current SubScene state.
            SceneHierarchyHooks.ReloadAllSceneHierarchies();
        }

        void CachePreviousSceneAssetReferences()
        {
            var numTargets = targets.Length;
            if (m_PreviousSceneAssets == null || m_PreviousSceneAssets.Length != numTargets)
            {
                m_PreviousSceneAssets = new SceneAsset[numTargets];
            }
            for (int i = 0; i < numTargets; ++i)
            {
                var subScene = (SubScene)targets[i];
                m_PreviousSceneAssets[i] = subScene.SceneAsset;
            }
        }

        void HandleChangedSceneAssetReferences()
        {
            bool needsHierarchyReload = false;
            var numTargets = targets.Length;
            for (int i = 0; i < numTargets; ++i)
            {
                var subScene = (SubScene)targets[i];
                var prevSceneAsset = m_PreviousSceneAssets[i];
                if (prevSceneAsset != subScene.SceneAsset)
                {
                    if (!needsHierarchyReload)
                    {
                        // First time we see there's a change in Scene Asset,
                        // check if new scene is already loaded but not as a Sub Scene.
                        var scene = subScene.EditingScene;
                        if (scene.IsValid() && !scene.isSubScene)
                        {
                            if (EditorUtility.DisplayDialog("Convert to Sub Scene?", "The Scene is already loaded as a root Scene. Do you want to convert it to a Sub Scene?", "Convert", "Cancel"))
                            {
                                // Make loaded scene a Sub Scene. Only needs to be done once,
                                // since even with multi-editing, user can only have assigned one Scene.
                                SubSceneInspectorUtility.SetSceneAsSubScene(scene);
                            }
                            else
                            {
                                // Cancel assigning new Scene Asset (after the fact).
                                Undo.PerformUndo();
                                break;
                            }
                        }
                    }

                    needsHierarchyReload = true;
                    if (prevSceneAsset != null)
                    {
                        Scene prevScene = SceneManager.GetSceneByPath(AssetDatabase.GetAssetPath(prevSceneAsset));
                        if (prevScene.isLoaded && prevScene.isSubScene)
                        {
                            if (prevScene.isDirty)
                                EditorSceneManager.SaveModifiedScenesIfUserWantsTo(new[] { prevScene });

                            // We need to close the Scene if it is loaded to prevent having scenes loaded
                            // that are not visualized in the Hierarhcy.
                            EditorSceneManager.CloseScene(prevScene, true);
                        }
                    }
                }
            }
            if (needsHierarchyReload)
                SceneHierarchyHooks.ReloadAllSceneHierarchies();
        }

        static readonly GUIContent s_TmpContent = new GUIContent();

        static Rect DrawButtonGridLabelAndGetFirstButtonRect(GUIContent label, int numButtons, out float spacing)
        {
            Rect controlRect;
            if (EditorGUIUtility.wideMode)
            {
                controlRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                controlRect = EditorGUI.PrefixLabel(controlRect, label);
            }
            else
            {
                EditorGUILayout.PrefixLabel(label);
                ++EditorGUI.indentLevel;
                controlRect = EditorGUI.IndentedRect(
                    EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight)
                );
                --EditorGUI.indentLevel;
            }
            spacing = GUI.skin.button.margin.horizontal / 2f;
            controlRect.width = (controlRect.width - spacing * (numButtons - 1)) / numButtons;
            return controlRect;
        }

        private static void DrawOpenSubScenes(SubScene[] subScenes)
        {
            int numOpen = 0;
            for (int i = 0; i < subScenes.Length; i++)
            {
                if (subScenes[i].IsLoaded)
                {
                    numOpen++;
                }
            }

            if (numOpen > 0)
            {
                GUILayout.Space(EditorGUIUtility.singleLineHeight);
                GUILayout.BeginHorizontal();
                GUILayout.Label(Content.OpenSubScenesLabel, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{numOpen}");
                GUILayout.EndHorizontal();

                for (int i = 0; i < subScenes.Length; i++)
                {
                    var scene = subScenes[i];
                    if (!scene.IsLoaded)
                        continue;

                    s_TmpContent.text = scene.SceneName;
                    s_TmpContent.tooltip = scene.EditableScenePath;
                    var buttonRect = DrawButtonGridLabelAndGetFirstButtonRect(s_TmpContent, 3, out var spacing);

                    // add empty space space so buttons are right-aligned
                    buttonRect.x += buttonRect.width + spacing;
                    using (new EditorGUI.DisabledScope(!scene.EditingScene.isDirty))
                    {
                        if (GUI.Button(buttonRect, Content.SaveLabel))
                        {
                            SubSceneInspectorUtility.SaveScene(scene);
                        }
                    }
                    buttonRect.x += buttonRect.width + spacing;
                    if (GUI.Button(buttonRect, Content.CloseLabel))
                    {
                        SubSceneInspectorUtility.CloseAndAskSaveIfUserWantsTo(scene);
                    }
                }

                if (Application.isPlaying)
                {
                    GUILayout.Space(EditorGUIUtility.singleLineHeight);
                    EditorGUILayout.HelpBox(
                        "Opened subscenes are just for live editing in the editor. Opened subscenes don't stream in and their entities are immediately available when entering playmode in the editor.\n\nClosed subscenes are streamed in and their entities will take a few frames to be available when entering playmode. \n\nIn builds, subscenes will behave as closed subscenes in the editor, therefore their entities will not be available immediately.",
                        MessageType.Warning);
                }
            }
        }

        private static bool DrawClosedSubScenes(SubSceneInspectorUtility.LoadableScene[] loadableScenes, SubScene[] closedSubScenes)
        {
            if (World.DefaultGameObjectInjectionWorld != null && closedSubScenes.Length != 0)
            {
                GUILayout.Space(EditorGUIUtility.singleLineHeight);
                var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

                int numScenesLoaded = 0;
                int numScenesImported = 0;
                foreach (var scene in loadableScenes)
                {
                    if (entityManager.HasComponent<RequestSceneLoaded>(scene.Scene))
                        numScenesLoaded++;
                    if (IsSubsceneImported(scene.SubScene, ImportMode.NoImport))
                        numScenesImported++;
                }

                if (EditorGUIUtility.wideMode)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Content.ClosedSubScenesLabel, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(string.Format(Content.ClosedStatusString, numScenesLoaded, loadableScenes.Length, numScenesImported));
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.Label(Content.ClosedSubScenesLabel, EditorStyles.boldLabel);
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(string.Format(Content.ClosedStatusString, numScenesLoaded, loadableScenes.Length, numScenesImported));
                    GUILayout.EndHorizontal();
                }

                if (closedSubScenes.Length > 1 || loadableScenes.Length > 1)
                {
                    GUILayout.BeginHorizontal();
                    bool reimportRequested = GUILayout.Button(Content.ReimportAllLabel);

                    if (GUILayout.Button(Content.LoadAllLabel))
                    {
                        foreach (var scene in loadableScenes)
                        {
                            if (!entityManager.HasComponent<RequestSceneLoaded>(scene.Scene))
                                entityManager.AddComponentData(scene.Scene, new RequestSceneLoaded());
                        }

                        EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
                    }

                    if (GUILayout.Button(Content.UnloadAllLabel))
                    {
                        foreach (var scene in loadableScenes)
                        {
                            if (entityManager.HasComponent<RequestSceneLoaded>(scene.Scene))
                                entityManager.RemoveComponent<RequestSceneLoaded>(scene.Scene);
                        }

                        EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
                    }

                    GUILayout.EndHorizontal();

                    if (reimportRequested && EditorUtility.DisplayDialog(Content.ReimportAllSubScenes, Content.ReimportAllSubScenesDetails, Content.Yes, Content.No))
                        SubSceneInspectorUtility.ForceReimport(closedSubScenes);

                    GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
                }

                bool isImporting = false;
                foreach (var scene in closedSubScenes)
                {
                    var loadableSections = SubSceneInspectorUtility.GetLoadableSections(scene, loadableScenes);
                    if (loadableSections.Length > 0)
                    {
                        foreach (var loadableScene in loadableSections)
                        {
                            DrawSection(entityManager, loadableScene.SubScene, true, loadableScene);
                        }
                    }
                    else
                    {
                        DrawSection(entityManager, scene, false, default);
                    }

                    if (!IsSubsceneImported(scene, ImportMode.NoImport))
                    {
                        isImporting = true;
                    }
                }
                return isImporting;
            }

            return false;
        }


        static void DrawSection(EntityManager entityManager, SubScene subscene, bool hasLoadableSections, SubSceneInspectorUtility.LoadableScene loadableScene)
        {
            if (hasLoadableSections)
            {
                s_TmpContent.text = loadableScene.Name;
                s_TmpContent.tooltip = loadableScene.SubScene.EditableScenePath;
            }
            else
            {
                s_TmpContent.text = subscene.SceneName;
                s_TmpContent.tooltip = subscene.EditableScenePath;
            }

            var buttonRect = DrawButtonGridLabelAndGetFirstButtonRect(s_TmpContent, 3, out var spacing);

            if (!hasLoadableSections)
            {
                // add empty space space so buttons are right-aligned
                buttonRect.x += buttonRect.width + spacing;
            }

            if (GUI.Button(buttonRect, Content.ReimportLabel))
            {
                SubSceneInspectorUtility.ForceReimport(subscene);
            }

            if (hasLoadableSections)
            {
                buttonRect.x += buttonRect.width + spacing;
                if (!loadableScene.IsLoaded)
                {
                    using (new EditorGUI.DisabledScope(!loadableScene.Section0IsLoaded && loadableScene.SectionIndex != 0))
                    {
                        if (GUI.Button(buttonRect, Content.LoadLabel))
                        {
                            // if we load any scene, we also have load section 0
                            entityManager.AddComponentData(loadableScene.Scene, new RequestSceneLoaded());
                            EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
                        }
                    }
                }
                else
                {
                    using (new EditorGUI.DisabledScope(loadableScene.NumSubSceneSectionsLoaded > 1 && loadableScene.SectionIndex == 0))
                    {
                        if (GUI.Button(buttonRect, Content.UnloadLabel))
                        {
                            // if we unload section 0, we also need to unload the entire rest
                            entityManager.RemoveComponent<RequestSceneLoaded>(loadableScene.Scene);
                            EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
                        }
                    }
                }
            }

            buttonRect.x += buttonRect.width + spacing;
            using (new EditorGUI.DisabledScope(!SubSceneInspectorUtility.CanEditScene(subscene)))
            {
                if (GUI.Button(buttonRect, Content.OpenLabel))
                {
                    SubSceneUtility.EditScene(subscene);
                }
            }
        }

        public override void OnInspectorGUI()
        {
            var subScene = target as SubScene;

            if (!subScene.IsInMainStage())
            {
                // In Prefab Mode and when selecting a Prefab Asset in the Project Browser we only show the inspector of data of the
                // SubScene, and not the load/unload/edit/close buttons.
                base.OnInspectorGUI();

                EditorGUILayout.HelpBox($"Only Sub Scenes in the Main Stage can be loaded and unloaded.", MessageType.Info, true);
                EditorGUILayout.Space();
                return;
            }

            var prevColor = subScene.HierarchyColor;
            var prevAutoLoad = subScene.AutoLoadScene;
            CachePreviousSceneAssetReferences();

            base.OnInspectorGUI();

            HandleChangedSceneAssetReferences();

            if (subScene.HierarchyColor != prevColor)
                SceneHierarchyHooks.ReloadAllSceneHierarchies();

            if (prevAutoLoad != subScene.AutoLoadScene)
                subScene.RebuildSceneEntities();

            bool isImportingClosedSubscenes = false;
            DrawOpenSubScenes(_selectedSubscenes);
            var loadableScenes = SubSceneInspectorUtility.GetLoadableScenes(_selectedSubscenes);
            var closedSubScenes = _selectedSubscenes.Where(s => !s.IsLoaded).ToArray();
            if (DrawClosedSubScenes(loadableScenes, closedSubScenes))
            {
                isImportingClosedSubscenes = true;
            }

#if false
            // @TODO: TEMP for debugging
            if (GUILayout.Button("ClearWorld"))
            {
                World.DisposeAllWorlds();
                DefaultWorldInitialization.Initialize("Default World", !Application.isPlaying);

                var scenes = FindObjectsOfType<SubScene>();
                foreach (var scene in scenes)
                {
                    var oldEnabled = scene.enabled;
                    scene.enabled = false;
                    scene.enabled = oldEnabled;
                }

                EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
            }
    #endif

            bool hasDuplicates = subScene.SceneAsset != null && (SubScene.AllSubScenes.Count(s => (s.SceneAsset == subScene.SceneAsset)) > 1);
            if (hasDuplicates)
            {
                EditorGUILayout.HelpBox($"The Scene Asset '{subScene.EditableScenePath}' is used mutiple times and this is not supported. Clear the reference.", MessageType.Warning, true);
                if (GUILayout.Button("Clear"))
                {
                    subScene.SceneAsset = null;
                    SceneHierarchyHooks.ReloadAllSceneHierarchies();
                }
                EditorGUILayout.Space();
            }

            var uncleanHierarchyObject = SubSceneInspectorUtility.GetUncleanHierarchyObject(_selectedSubscenes);
            if (uncleanHierarchyObject != null)
            {
                EditorGUILayout.HelpBox($"Scene transform values are not applied to scenes child transforms. But {uncleanHierarchyObject.name} has an offset Transform.", MessageType.Warning, true);
                if (GUILayout.Button("Clear"))
                {
                    foreach (var scene in _selectedSubscenes)
                    {
                        scene.transform.localPosition = Vector3.zero;
                        scene.transform.localRotation = Quaternion.identity;
                        scene.transform.localScale = Vector3.one;
                    }
                }
                EditorGUILayout.Space();
            }
            if (SubSceneInspectorUtility.HasChildren(_selectedSubscenes))
            {
                EditorGUILayout.HelpBox($"SubScenes can not have child game objects. Close the scene and delete the child game objects.", MessageType.Warning, true);
            }

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            // Initial behaviour with conversion was to trigger an async import for the first target only if it didn't happen successfully before. Let's keep it for now
            if (isImportingClosedSubscenes || !IsSubsceneImported(subScene, ImportMode.Asynchronous))
            {
                GUILayout.Label(Content.ImportingLabel);
                Repaint();
            }
        }

        // Invoked by Unity magically for FrameSelect command.
        // Frames the whole sub scene in scene view
        bool HasFrameBounds()
        {
            return !SubSceneInspectorUtility.GetActiveWorldMinMax(World.DefaultGameObjectInjectionWorld, targets).Equals(MinMaxAABB.Empty);
        }

        Bounds OnGetFrameBounds()
        {
            AABB aabb = SubSceneInspectorUtility.GetActiveWorldMinMax(World.DefaultGameObjectInjectionWorld, targets);
            return new Bounds(aabb.Center, aabb.Size);
        }

        // Visualize SubScene using bounding volume when it is selected.
        [DrawGizmo(GizmoType.Selected)]
        static void DrawSubsceneBounds(SubScene scene, GizmoType gizmoType)
        {
            SubSceneInspectorUtility.DrawSubsceneBounds(scene);
        }

        static bool IsSubsceneImported(SubScene subScene, ImportMode mode)
        {
            foreach (var world in World.All)
            {
                var sceneSystem = world.GetExistingSystem<SceneSystem>();

                if (sceneSystem != SystemHandle.Null)
                {
                    var hash = EntityScenesPaths.GetSubSceneArtifactHash(subScene.SceneGUID, world.EntityManager.GetComponentData<SceneSystemData>(sceneSystem).BuildConfigurationGUID, true, mode);
                    if (!hash.IsValid)
                        return false;
                }
            }

            return true;
        }

        static class Content
        {
            public static readonly GUIContent OpenSubScenesLabel = EditorGUIUtility.TrTextContent("Open SubScenes");
            public static readonly GUIContent ClosedSubScenesLabel = EditorGUIUtility.TrTextContent("Closed SubScenes");
            public static readonly GUIContent CloseLabel = EditorGUIUtility.TrTextContent("Close");
            public static readonly GUIContent SaveLabel = EditorGUIUtility.TrTextContent("Save");
            public static readonly GUIContent LoadAllLabel = EditorGUIUtility.TrTextContent("Load All");
            public static readonly GUIContent UnloadAllLabel = EditorGUIUtility.TrTextContent("Unload All");
            public static readonly GUIContent ReimportAllLabel = EditorGUIUtility.TrTextContent("Reimport All");
            public static readonly GUIContent LoadLabel = EditorGUIUtility.TrTextContent("Load", "You can only load a section if section 0 of that scene is also loaded.");
            public static readonly GUIContent UnloadLabel = EditorGUIUtility.TrTextContent("Unload", "You can only unload section 0 if no other sections of that scene are loaded.");
            public static readonly GUIContent ReimportLabel = EditorGUIUtility.TrTextContent("Reimport");
            public static readonly GUIContent ImportingLabel = EditorGUIUtility.TrTextContent("Importing...");
            public static readonly GUIContent OpenLabel = EditorGUIUtility.TrTextContent("Open");
            public static readonly string ClosedStatusString = L10n.Tr("{0} / {1} loaded, {2} / {1} imported");
            public static readonly string ReimportAllSubScenes = L10n.Tr("Reimport All Selected SubScenes?");
            public static readonly string ReimportAllSubScenesDetails =
                L10n.Tr(
                    "Reimporting all SubScenes could take a considerable amount of time. Do you really want to trigger a reimport?");
            public static readonly string Yes = L10n.Tr("Yes");
            public static readonly string No = L10n.Tr("No");
        }
    }
}
