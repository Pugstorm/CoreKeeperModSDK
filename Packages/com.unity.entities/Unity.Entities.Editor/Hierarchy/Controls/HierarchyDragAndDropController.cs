using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Editor.Bridge;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Unity.Entities.Editor
{
    class HierarchyDragAndDropController : ICollectionDragAndDropController
    {
        readonly Hierarchy m_Hierarchy;

        public HierarchyDragAndDropController(Hierarchy hierarchy)
        {
            m_Hierarchy = hierarchy;
        }

        public bool CanStartDrag(IEnumerable<int> itemIndices) => true;

        public StartDragArgs SetupDragAndDrop(IEnumerable<int> itemIndices, bool skipText)
        {
            if (!itemIndices.Any())
                return new StartDragArgs(string.Empty);

            // Hierarchy doesn't support multiselect yet, this will only take care of the first item of itemIndices
            var itemIndex = itemIndices.First();
            var handle = m_Hierarchy.GetNodes()[itemIndex].GetHandle();

            return new StartDragArgs(string.Empty, handle, GetUnityObject(handle));
        }

        Object GetUnityObject(HierarchyNodeHandle handle)
        {
            switch (handle.Kind)
            {
                case NodeKind.GameObject:
                    return EditorUtility.InstanceIDToObject(handle.Index);
                case NodeKind.SubScene:
                    var scene = m_Hierarchy.SubSceneMap.GetSubSceneMonobehaviourFromHandle(handle);
                    return scene?.gameObject;
                default:
                    return null;
            }
        }

        public DragVisualMode HandleDragAndDrop(object target, int insertAtIndex, DragAndDropPosition position, DragAndDropData data)
            => Handle(target, insertAtIndex, position, data, false);

        public void OnDrop(object target, int insertAtIndex, DragAndDropPosition position, DragAndDropData data)
            => Handle(target, insertAtIndex, position, data, true);

        DragVisualMode Handle(object target, int insertAtIndex, DragAndDropPosition position, DragAndDropData data, bool perform)
        {
            if (data.userData is HierarchyNodeHandle { Kind: NodeKind.Entity })
                return DragVisualMode.Rejected;
            if (data.userData is HierarchyNodeHandle { Kind: NodeKind.Scene } sceneHandle)
                return HandleSceneReorder(EditorSceneManagerBridge.GetSceneByHandle(sceneHandle.Index), insertAtIndex, position, data, perform);
            if (data.unityObjectReferences.Any(o => o is SceneAsset))
                return HandleLoadScene(data.unityObjectReferences.Where(o => o is SceneAsset).Cast<SceneAsset>(), insertAtIndex, position, data, perform);

            var destination = GetDestinationNode(insertAtIndex, position, out var dropFlags);
            var destinationHandle = destination.GetHandle();
            if (destinationHandle.Kind is NodeKind.Entity)
                return DragVisualMode.Rejected;

            if (dropFlags is HierarchyDropFlags.DropUpon or HierarchyDropFlags.DropBetween
                && destinationHandle.Kind is NodeKind.SubScene
                && m_Hierarchy.SubSceneMap.GetSubSceneStateImmediate(destinationHandle, m_Hierarchy.World) is not (SubSceneLoadedState.Opened or SubSceneLoadedState.LiveConverted))
                return DragVisualMode.Rejected;

            var targetDropId = GetDestinationInstanceId(destination);
            if (targetDropId == 0)
                return DragVisualMode.Rejected;

            // In playmode reject dragging a node from scene to a subscene, or from a subscene to a scene
            if (EditorApplication.isPlaying && data.userData is HierarchyNodeHandle { Kind: NodeKind.GameObject  } handle)
            {
                var nodeSceneContainerKind = GetNodeContainerKind(handle);
                var destinationSceneContainerKind = GetNodeContainerKind(destinationHandle);

                if ((nodeSceneContainerKind == NodeKind.Scene && destinationSceneContainerKind is NodeKind.SubScene)
                    || (nodeSceneContainerKind == NodeKind.SubScene && destinationSceneContainerKind is NodeKind.Scene))
                    return DragVisualMode.Rejected;
            }

            var visualMode = DragAndDropBridge.DropOnHierarchyWindow(targetDropId, dropFlags, null, perform);
            return Convert(visualMode);
        }

        DragVisualMode HandleSceneReorder(Scene scene, int insertAtIndex, DragAndDropPosition position, DragAndDropData data, bool perform)
        {
            if (!perform)
                return DragVisualMode.Move;

            if (position == DragAndDropPosition.OutsideItems)
            {
                if (insertAtIndex == 0)
                    EditorSceneManager.MoveSceneBefore(scene, SceneManager.GetSceneAt(0));
                else
                    EditorSceneManager.MoveSceneAfter(scene, SceneManager.GetSceneAt(SceneManager.sceneCount - 1));
                return DragVisualMode.Move;
            }

            if (insertAtIndex == 0 && position is DragAndDropPosition.BetweenItems)
            {
                EditorSceneManager.MoveSceneBefore(scene, SceneManager.GetSceneAt(0));
                return DragVisualMode.Move;
            }

            var destination = GetDestinationNode(insertAtIndex, position, out _);
            if (destination.GetHandle().Kind is NodeKind.Scene)
            {
                var destinationScene = EditorSceneManagerBridge.GetSceneByHandle(destination.GetHandle().Index);
                EditorSceneManager.MoveSceneAfter(scene, destinationScene);
                return DragVisualMode.Move;
            }

            destination = destination.GetParent();
            while (destination.GetHandle().Kind is not (NodeKind.Scene or NodeKind.Root))
            {
                destination = destination.GetParent();
            }

            if (destination.GetHandle().Kind == NodeKind.Root)
            {
                EditorSceneManager.MoveSceneBefore(scene, SceneManager.GetSceneAt(0));
            }
            else
            {
                var destinationScene = EditorSceneManagerBridge.GetSceneByHandle(destination.GetHandle().Index);
                EditorSceneManager.MoveSceneAfter(scene, destinationScene);
            }

            return DragVisualMode.Move;
        }

        DragVisualMode HandleLoadScene(IEnumerable<SceneAsset> scenes, int insertAtIndex, DragAndDropPosition position, DragAndDropData data, bool perform)
        {
            if (perform)
            {
                foreach (var sceneAsset in scenes)
                {
                    var scene = EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(sceneAsset), OpenSceneMode.Additive);
                    HandleSceneReorder(scene, insertAtIndex, position, data, true);
                }
            }

            return DragVisualMode.Copy;
        }

        HierarchyNode.Immutable GetDestinationNode(int insertAtIndex, DragAndDropPosition position, out HierarchyDropFlags dropFlags)
        {
            var hierarchyNodes = m_Hierarchy.GetNodes();
            if (insertAtIndex > hierarchyNodes.Count - 1)
                position = DragAndDropPosition.OutsideItems;

            switch (position)
            {
                case DragAndDropPosition.OutsideItems when insertAtIndex != 0:
                    dropFlags = HierarchyDropFlags.DropUpon;
                    var scene = GetOwningSceneNode(hierarchyNodes[^1]);
                    return scene;

                case DragAndDropPosition.OutsideItems:
                    dropFlags = HierarchyDropFlags.DropAbove;
                    return hierarchyNodes[insertAtIndex];

                case DragAndDropPosition.OverItem:
                    dropFlags = HierarchyDropFlags.DropUpon;
                    return hierarchyNodes[insertAtIndex];

                case DragAndDropPosition.BetweenItems:
                    if (insertAtIndex == 0)
                    {
                        dropFlags = HierarchyDropFlags.DropBetween | HierarchyDropFlags.DropAbove;
                        return hierarchyNodes[insertAtIndex];
                    }

                    var nodeAfter = hierarchyNodes[insertAtIndex];
                    var nodeBefore = hierarchyNodes[insertAtIndex - 1];
                    if (GetDestinationInstanceId(nodeAfter) == 0)
                    {
                        dropFlags = HierarchyDropFlags.DropBetween;
                        return nodeBefore;
                    }

                    if (nodeAfter.GetHandle().Kind == NodeKind.Scene)
                    {
                        dropFlags = HierarchyDropFlags.DropUpon;
                        return GetOwningSceneNode(nodeBefore);
                    }

                    if (nodeBefore.GetDepth() < nodeAfter.GetDepth())
                    {
                        dropFlags = HierarchyDropFlags.DropBetween | HierarchyDropFlags.DropAbove | HierarchyDropFlags.DropAfterParent;
                        return nodeAfter;
                    }

                    if (nodeAfter.GetDepth() < nodeBefore.GetDepth())
                    {
                        dropFlags = HierarchyDropFlags.DropBetween | HierarchyDropFlags.DropAbove;
                        return nodeAfter;
                    }

                    dropFlags = HierarchyDropFlags.DropBetween;
                    return nodeBefore;

                default:
                    throw new NotSupportedException($"{nameof(position)} value: {position} is not supported");
            }

        }

        int GetDestinationInstanceId(HierarchyNode.Immutable destination) => destination.GetHandle().Kind switch
        {
            NodeKind.GameObject => destination.GetHandle().Index,
            NodeKind.Scene => destination.GetHandle().Index,
            NodeKind.SubScene => m_Hierarchy.SubSceneMap.GetSubSceneMonobehaviourFromHandle(destination.GetHandle()).gameObject.GetInstanceID(),
            _ => 0
        };

        static DragVisualMode Convert(DragAndDropVisualMode visualMode)
        {
            switch (visualMode)
            {
                case DragAndDropVisualMode.None:
                    return DragVisualMode.None;
                case DragAndDropVisualMode.Copy:
                    return DragVisualMode.Copy;
                case DragAndDropVisualMode.Link:
                    return DragVisualMode.Copy;
                case DragAndDropVisualMode.Move:
                    return DragVisualMode.Move;
                case DragAndDropVisualMode.Generic:
                    return DragVisualMode.Copy;
                case DragAndDropVisualMode.Rejected:
                    return DragVisualMode.Rejected;
                default:
                    throw new ArgumentOutOfRangeException(nameof(visualMode), visualMode, null);
            }
        }

        HierarchyNode.Immutable GetOwningSceneNode(HierarchyNode.Immutable node)
        {
            if (node.GetDepth() > 0)
            {
                var parent = node.GetParent();
                for (;;)
                {
                    var parentKind = parent.GetHandle().Kind;
                    if (parentKind is NodeKind.Root)
                        break;

                    if (parentKind is NodeKind.Scene)
                        return parent;

                    parent = parent.GetParent();
                }
            }

            // if we're over a root node
            // find the last scene node in the hierarchy
            if (node.GetHandle().Kind is NodeKind.Scene)
                return node;

            var scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            var handle = HierarchyNodeHandle.FromScene(scene);
            var nodes = m_Hierarchy.GetNodes();
            var index = nodes.IndexOf(handle);
            return index != -1 ? nodes[index] : node;
        }

        NodeKind GetNodeContainerKind(HierarchyNodeHandle handle)
        {
            var nodes = m_Hierarchy.GetNodes();
            var node = nodes[handle];
            var parent = node.GetParent();
            for (;;)
            {
                var parentKind = parent.GetHandle().Kind;
                if (parentKind is NodeKind.Root or NodeKind.Scene or NodeKind.SubScene)
                    return parentKind;

                parent = parent.GetParent();
            }
        }

    }
}
