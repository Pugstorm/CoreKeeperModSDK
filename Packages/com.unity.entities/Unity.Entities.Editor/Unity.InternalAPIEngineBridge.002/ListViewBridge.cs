using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Editor.Bridge
{
    static class ListViewBridge
    {
        internal static void SetDragAndDropController(MultiColumnListView listView, ICollectionDragAndDropController controller)
        {
            listView.dragger.dragAndDropController = new CollectionDragAndDropControllerProxy(controller);
        }

        internal static int VirtualizationControllerGetItemIndexFromMousePosition(MultiColumnListView listView, Vector2 position)
        {
            return listView.virtualizationController.GetIndexFromPosition(position);
        }

        class CollectionDragAndDropControllerProxy : UnityEngine.UIElements.ICollectionDragAndDropController
        {
            readonly ICollectionDragAndDropController m_Controller;

            public CollectionDragAndDropControllerProxy(ICollectionDragAndDropController controller)
            {
                m_Controller = controller;
            }

            public bool CanStartDrag(IEnumerable<int> itemIndices)
                => m_Controller.CanStartDrag(itemIndices);

            public UnityEngine.UIElements.StartDragArgs SetupDragAndDrop(IEnumerable<int> itemIndices, bool skipText = false)
            {
                var args = m_Controller.SetupDragAndDrop(itemIndices, skipText);
                return args.ToStartDragArgs();
            }

            public UnityEngine.UIElements.DragVisualMode HandleDragAndDrop(IListDragAndDropArgs args)
            {
                return (UnityEngine.UIElements.DragVisualMode)m_Controller.HandleDragAndDrop(args.target,
                                                                                             args.insertAtIndex,
                                                                                             (DragAndDropPosition)args.dragAndDropPosition,
                                                                                             args.dragAndDropData != null ? new DragAndDropData(args.dragAndDropData) : default);
            }

            public void OnDrop(IListDragAndDropArgs args)
            {
                m_Controller.OnDrop(args.target,
                                    args.insertAtIndex,
                                    (DragAndDropPosition)args.dragAndDropPosition,
                                    args.dragAndDropData != null ? new DragAndDropData(args.dragAndDropData) : default);
            }

            public bool enableReordering { get; set; } = true;
        }
    }

    interface ICollectionDragAndDropController
    {
        bool CanStartDrag(IEnumerable<int> itemIndices);
        StartDragArgs SetupDragAndDrop(IEnumerable<int> itemIndices, bool skipText);
        DragVisualMode HandleDragAndDrop(object target, int insertAtIndex, DragAndDropPosition position, DragAndDropData data);
        void OnDrop(object target, int insertAtIndex, DragAndDropPosition position, DragAndDropData data);
    }

    enum DragVisualMode
    {
        None,
        Copy,
        Move,
        Rejected
    }

    enum DragAndDropPosition
    {
        OverItem,
        BetweenItems,
        OutsideItems
    }

    readonly struct DragAndDropData
    {
        readonly IDragAndDropData m_Data;

        internal DragAndDropData(IDragAndDropData data)
            => m_Data = data;

        public object GetGenericData(string key) => m_Data.GetGenericData(key);
        public object userData => m_Data.userData;
        public IEnumerable<Object> unityObjectReferences => m_Data.unityObjectReferences;
    }

    readonly struct StartDragArgs
    {
        readonly string m_Title;
        readonly object m_UserData;
        readonly Object m_UnityObjectReference;

        public StartDragArgs(string title = "", object userData = null, Object unityObjectReference = null)
        {
            m_Title = title;
            m_UserData = userData;
            m_UnityObjectReference = unityObjectReference;
        }

        public UnityEngine.UIElements.StartDragArgs ToStartDragArgs()
        {
            var args = new UnityEngine.UIElements.StartDragArgs(m_Title, m_UserData);
            if (m_UnityObjectReference != null)
                args.SetUnityObjectReferences(new[] { m_UnityObjectReference });

            return args;
        }
    }
}
