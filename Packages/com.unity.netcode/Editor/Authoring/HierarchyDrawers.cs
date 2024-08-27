using System;
using Unity.Entities;
using Unity.Entities.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.NetCode.Editor
{
    /// <summary>Decorator that draws NetCode related data into the DOTS Hierarchy VisualElements.</summary>
    class DotsHierarchyItemDecorator : IHierarchyItemDecorator
    {
        const string k_GhostIconElement = "Ghost Icon";

        public static bool SetNetCodeLabelsToPink = true;

        [InitializeOnLoadMethod]
        static void Register()
        {
            HierarchyWindow.AddDecorator(new DotsHierarchyItemDecorator());
        }

        void IHierarchyItemDecorator.OnCreateItem(HierarchyListViewItem item)
        {
            var ghostIcon = new VisualElement
            {
                name = k_GhostIconElement,
                style =
                {
                    display = DisplayStyle.None,
                    width = 16, // TODO - Hardcoded styles.
                    height = 16,
                    flexBasis = new StyleLength(StyleKeyword.Initial)
                }
            };
            ghostIcon.style.backgroundImage = LegacyHierarchyDrawer.GhostIcon;

            // Insert the Ghost after the "Ping Source" and "System Button", but before the "Prefab Arrow".
            item.Column2.Insert(2, ghostIcon);
        }

        void IHierarchyItemDecorator.OnBindItem(HierarchyListViewItem item, HierarchyNode.Immutable node)
        {
            var isNetCode = false;
            var isReplicated = false;
            var itemEntity = item.Entity;
            if (itemEntity != Entity.Null)
            {
                var world = item.World;
                if (world.EntityManager.HasComponent<GhostInstance>(itemEntity))
                {
                    // Entity view.
                    isNetCode = true;
                    isReplicated = !world.EntityManager.HasComponent<Prefab>(itemEntity);
                }
            }
            else
            {
                var itemGameObject = item.GameObject;
                if (itemGameObject)
                {
                    if (itemGameObject.GetComponent<GhostAuthoringComponent>())
                    {
                        // GameObject view.
                        isNetCode = true;
                        isReplicated = item.PrefabType != Unity.Entities.Editor.Hierarchy.HierarchyPrefabType.None;
                    }
                }
            }

            if (isNetCode)
            {
                item.Q<VisualElement>(k_GhostIconElement).style.display = DisplayStyle.Flex;
                item.NameLabel.style.color = isReplicated && SetNetCodeLabelsToPink ?  LegacyHierarchyDrawer.NetcodeColor : new StyleColor(StyleKeyword.Null);
            }
            else
            {
                RevertStyleIfModified(item);
            }
        }

        void IHierarchyItemDecorator.OnUnbindItem(HierarchyListViewItem item)
        {
            RevertStyleIfModified(item);
        }

        void IHierarchyItemDecorator.OnDestroyItem(HierarchyListViewItem item)
        {
            var element = item.Q<VisualElement>(k_GhostIconElement);
            element.RemoveFromHierarchy();
        }

        /// <summary>Revert style back to how it was before we touched it.</summary>
        static void RevertStyleIfModified(HierarchyListViewItem item)
        {
            var ghostIcon = item.Q<VisualElement>(k_GhostIconElement);
            if (ghostIcon.style.display != DisplayStyle.None)
            {
                ghostIcon.style.display = DisplayStyle.None;

                // Setting to null resets the style to the USS value: https://forum.unity.com/threads/resetting-c-styles-to-defaults.969942/
                item.NameLabel.style.color = new StyleColor(StyleKeyword.Null);
            }
        }
    }

    /// <summary>Draws NetCode related data onto the Hierarchy Window.</summary>
    [InitializeOnLoad]
    static class LegacyHierarchyDrawer
    {
        const string k_IconDirectory = "Packages/com.unity.netcode/EditorIcons/";

        static Texture2D s_GhostIconDark;
        static Texture2D s_GhostIconLight;

        static LegacyHierarchyDrawer()
        {
            EditorApplication.hierarchyWindowItemOnGUI -= GhostAuthoringSetCustomColors;
            EditorApplication.hierarchyWindowItemOnGUI += GhostAuthoringSetCustomColors;
        }

        public static Texture2D GhostIcon => EditorGUIUtility.isProSkin
            ? s_GhostIconDark ??= (Texture2D) AssetDatabase.LoadAssetAtPath($"{k_IconDirectory}d_Ghost@64.png", typeof(Texture2D))
            : s_GhostIconLight ??= (Texture2D) AssetDatabase.LoadAssetAtPath($"{k_IconDirectory}Ghost@64.png", typeof(Texture2D));

        public static Color NetcodeColor => EditorGUIUtility.isProSkin ? new Color(0.91f, 0.55f, 0.86f) : new Color(0.8f, 0.14f, 0.5f);

        static void GhostAuthoringSetCustomColors(int instanceID, Rect selectionRect)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceID);
            if (obj != null && obj is GameObject go)
            {
                if (go.GetComponent<GhostAuthoringComponent>())
                {
                    // Ghost icon on right-hand side:
                    var iconRectRight = selectionRect;
                    iconRectRight.x += iconRectRight.width - iconRectRight.height;
                    iconRectRight.y -= 1.5f;
                    iconRectRight.height += 1.5f;
                    iconRectRight.width -= 70; // Icon will shrink into invisibility earlier.
                    GUI.Label(iconRectRight, GhostIcon);
                }

                // TODO - Consider showing the GhostInspectionComponent too.
            }
        }
    }
}
