using System;
using System.Collections.Generic;
using Unity.Editor.Bridge;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    partial class HierarchyContextMenu
    {
        readonly Hierarchy m_Hierarchy;
        readonly HierarchyElement m_HierarchyElement;
        bool m_WeStartedTheDrag;

        readonly List<ManipulatorActivationFilter> m_Activators;
        ManipulatorActivationFilter m_CurrentActivator;

        public HierarchyContextMenu(Hierarchy hierarchy, HierarchyElement hierarchyElement)
        {
            m_Hierarchy = hierarchy;
            m_HierarchyElement = hierarchyElement;
            m_WeStartedTheDrag = false;

            m_Activators = new List<ManipulatorActivationFilter> { new() { button = MouseButton.RightMouse } };
            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
            {
                m_Activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, modifiers = EventModifiers.Control });
            }
        }

        #region Event registration dance
        public void RegisterCallbacksOnTarget(VisualElement target)
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            target.RegisterCallback<ContextualMenuPopulateEvent, VisualElement>(BuildElementContextualMenu, target);
            target.RegisterCallback<DetachFromPanelEvent>(UnregisterCallbacksFromTarget);
        }

        void UnregisterCallbacksFromTarget(DetachFromPanelEvent evt)
        {
            var target = (VisualElement)evt.target;

            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            target.RegisterCallback<ContextualMenuPopulateEvent, VisualElement>(BuildElementContextualMenu, target);
            target.RegisterCallback<DetachFromPanelEvent>(UnregisterCallbacksFromTarget);
        }

        void OnMouseDown(MouseDownEvent evt)
        {
            if (!CanStartManipulation(evt))
                return;

            var target = evt.currentTarget as VisualElement;
            target.CaptureMouse();
            m_WeStartedTheDrag = true;
            evt.StopPropagation();
        }

        void OnMouseUp(MouseUpEvent evt)
        {
            var target = evt.currentTarget as VisualElement;

            if (!target.HasMouseCapture() || !m_WeStartedTheDrag)
                return;

            if (!CanStopManipulation(evt))
                return;

            DisplayContextMenu(evt, target);

            target.ReleaseMouse();
            m_WeStartedTheDrag = false;
            evt.StopPropagation();
        }

        static void DisplayContextMenu(EventBase triggerEvent, VisualElement target)
        {
            if (target.panel?.contextualMenuManager != null)
            {
                target.panel.contextualMenuManager.DisplayMenu(triggerEvent, target);
#if !UNITY_2023_2_OR_NEWER
                triggerEvent.PreventDefault();
#else
                triggerEvent.StopPropagation();
#endif
            }
        }

        bool CanStartManipulation(IMouseEvent evt)
        {
            foreach (var activator in m_Activators)
            {
                if (activator.Matches(evt))
                {
                    m_CurrentActivator = activator;
                    return true;
                }
            }

            return false;
        }

        bool CanStopManipulation(IMouseEvent evt)
        {
            if (evt == null)
            {
                return false;
            }

            return ((MouseButton)evt.button == m_CurrentActivator.button);
        }
        #endregion

        void BuildElementContextualMenu(ContextualMenuPopulateEvent evt, VisualElement target)
        {
            evt.StopImmediatePropagation();

            var currentNode = target is HierarchyListViewItem listViewItem ? listViewItem.Handle : HierarchyNodeHandle.Root;

            if (currentNode.Kind is NodeKind.Scene)
            {
                var scene = EditorSceneManagerBridge.GetSceneByHandle(currentNode.Index);
                BuildSceneContextMenu(evt.menu, scene);
                // Let users add extra items.
                SceneHierarchyHooksBridge.AddCustomSceneHeaderContextMenuItems(evt.menu, scene);
            }
            else if(currentNode.Kind is NodeKind.GameObject)
            {
                var gameObject = currentNode.ToGameObject();
                BuildGameObjectContextMenu(evt.menu, gameObject, target);
            }
            else if (currentNode.Kind is NodeKind.SubScene)
            {
                var subScene = m_Hierarchy.SubSceneMap.GetSubSceneMonobehaviourFromHandle(currentNode);
                if (subScene != null)
                {
                    BuildSubSceneContextMenu(evt.menu);
                    evt.menu.AppendSeparator();
                    if (subScene.EditingScene.IsValid())
                    {
                        // Sub scenes where the scene object exists can reuse menu for regular scenes.
                        BuildSceneContextMenu(evt.menu, subScene.EditingScene);
                    }
                    else
                    {
                        // Sub scenes where only the info exists, but not the scene object, need special handling.
                        SubSceneGUIBridge.CreateClosedSubSceneContextClick(evt.menu, subScene.SceneAsset);
                    }

                    // Let users add extra items.
                    SceneHierarchyHooksBridge.AddCustomSubSceneHeaderContextMenuItems(evt.menu, subScene);
                }
                else
                {
                    evt.menu.AppendAction(L10n.Tr("Copy name"), _ => ClipboardUtilityBridge.SetString(m_Hierarchy.GetName(currentNode)));
                }
            }
            else if (currentNode.Kind is NodeKind.Entity)
            {
                evt.menu.AppendAction(L10n.Tr("Copy name"), _ => ClipboardUtilityBridge.SetString(m_Hierarchy.GetName(currentNode)));
            }
            else
            {
                BuildGameObjectContextMenu(evt.menu, null, null);
            }

            RemoveDuplicateAndTrailingSeparators(evt.menu);
        }

        static void RemoveDuplicateAndTrailingSeparators(DropdownMenu evtMenu)
        {
            var menuItems = evtMenu.MenuItems();
            if (menuItems.Count <= 1)
                return;

            for (var i = 1; i < menuItems.Count; i++)
            {
                if (menuItems[i] is DropdownMenuSeparator && (i == menuItems.Count - 1 || menuItems[i - 1] is DropdownMenuSeparator))
                {
                    evtMenu.RemoveItemAt(i);
                }
            }
        }

        public void HandleCommand(HierarchyNodeHandle selectedHandle, string commandName)
        {
            var selectedGameObject = selectedHandle.Kind is NodeKind.GameObject ? selectedHandle.ToGameObject() : null;

            if (selectedHandle.Kind is NodeKind.SubScene)
            {
                var subScene = m_Hierarchy.SubSceneMap.GetSubSceneMonobehaviourFromHandle(selectedHandle);
                if (subScene)
                    selectedGameObject = subScene.gameObject;
            }

            switch (commandName)
            {
                case EventCommandNamesBridge.Delete:
                case EventCommandNamesBridge.SoftDelete:
                    if (selectedGameObject)
                        DeleteGameObject(selectedGameObject);
                    break;
                case EventCommandNamesBridge.Duplicate:
                    ClipboardUtilityBridge.DuplicateGameObject(null);
                    break;
                case EventCommandNamesBridge.Rename:
                    var item = m_HierarchyElement.HierarchyMultiColumnListView.GetItem(selectedHandle);
                    item.BeginRename();
                    break;
                case EventCommandNamesBridge.Cut:
                    ClipboardUtilityBridge.CutGameObject();
                    break;
                case EventCommandNamesBridge.Copy:
                    ClipboardUtilityBridge.CopyGameObject();
                    break;
                case EventCommandNamesBridge.Paste:
                    ClipboardUtilityBridge.PasteGameObject(null);
                    break;

                // not implemented, we need to support multiselect for this
                case EventCommandNamesBridge.SelectAll:
                case EventCommandNamesBridge.DeselectAll:
                    m_HierarchyElement.HierarchyMultiColumnListView.ClearSelection();
                    break;
                case EventCommandNamesBridge.InvertSelection:
                case EventCommandNamesBridge.SelectChildren:
                    break;

                case EventCommandNamesBridge.SelectPrefabRoot:
                    SelectPrefabRoot(selectedGameObject);
                    break;
            }
        }
    }
}
