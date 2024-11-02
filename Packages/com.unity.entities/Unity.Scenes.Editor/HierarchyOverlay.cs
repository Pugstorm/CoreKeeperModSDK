using System.Linq;
using System.Collections.Generic;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEngine;
using SubSceneUtility = Unity.Scenes.Editor.SubSceneUtility;

namespace UnityEditor.UI
{
    internal static class HierarchyOverlay
    {
        static class Styles
        {
            public static float subSceneEditingButtonWidth = 16f;
            public static GUIContent subSceneEditingTooltip = EditorGUIUtility.TrTextContent(string.Empty, "Toggle whether the Sub Scene is open for editing.");
        }

        internal static void HierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            var gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (gameObject != null)
            {
                SubScene subScene;
                if (gameObject.TryGetComponent(out subScene))
                {
                    if (!subScene.CanBeLoaded())
                        return;

                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(subScene.gameObject))
                        return;

                    var evt = Event.current;
                    Rect buttonRect = selectionRect;
                    buttonRect.x = buttonRect.xMax;
                    buttonRect.width = Styles.subSceneEditingButtonWidth;

                    var loaded = subScene.EditingScene.isLoaded;
                    var wantsLoaded = EditorGUI.Toggle(buttonRect, loaded);
                    if (wantsLoaded != loaded)
                    {
                        SubScene[] subScenes;
                        var selectedSubScenes = Selection.GetFiltered<SubScene>(SelectionMode.TopLevel);
                        if (selectedSubScenes.Contains(subScene))
                            subScenes = selectedSubScenes;
                        else
                            subScenes = new[] { subScene };

                        if (wantsLoaded)
                        {
                            SubSceneUtility.EditScene(subScenes);
                        }
                        else
                        {
                            // find child scenes
                            HashSet<SubScene> seenSubScene = new HashSet<SubScene>();
                            List<SubScene> subscenesToUnload = new List<SubScene>();

                            Stack<SubScene> subSceneStack = new Stack<SubScene>();
                            foreach (SubScene ss in subScenes)
                                subSceneStack.Push(ss);

                            while (subSceneStack.Count>0)
                            {
                                SubScene itr = subSceneStack.Pop();
                                if (seenSubScene.Contains(itr) || !itr.EditingScene.isLoaded)
                                    continue;

                                seenSubScene.Add(itr);
                                subscenesToUnload.Add(itr);

                                if (itr.SceneAsset != null)
                                {
                                    foreach (GameObject ssGameObject in itr.EditingScene.GetRootGameObjects())
                                    {
                                        foreach (SubScene childSubScene in ssGameObject.GetComponentsInChildren<SubScene>())
                                            subSceneStack.Push(childSubScene);
                                    }
                                }
                            }

                            // process children before parents
                            subScenes = subscenesToUnload.ToArray();
                            System.Array.Reverse(subScenes);

                            SubSceneInspectorUtility.CloseAndAskSaveIfUserWantsTo(subScenes);
                        }

                        // When opening or closing the scene from the hierarchy, the scene does not become dirty.
                        // Because of that, the SubSceneInspector cannot refresh itself automatically and update the
                        // state of the selected subscenes.
                        SubSceneInspectorUtility.RepaintSubSceneInspector();
                    }

                    if (buttonRect.Contains(evt.mousePosition))
                    {
                        GUI.Label(buttonRect, Styles.subSceneEditingTooltip);
                    }
                }
            }
        }
    }
}
