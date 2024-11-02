using System;
using System.Collections.Generic;
using Unity.Editor.Bridge;
using UnityEngine.UIElements;
using HierarchyModel = Unity.Entities.Editor.Hierarchy;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// THe main list view driving the hierarchy list elements.
    /// </summary>
    class HierarchyMultiColumnListView : MultiColumnListView
    {
        readonly HierarchyModel m_Model;
        readonly List<SortColumnDescription> m_SortedColumns = new();
        readonly HierarchyNameColumn m_NameColumn;

        public Action<HierarchyListViewItem> OnMakeItem { get; set; }

        internal void SetDecorators(HierarchyDecoratorCollection decorators)
        {
            m_NameColumn.SetDecorators(decorators);
        }

        public HierarchyMultiColumnListView(HierarchyModel model, HierarchyContextMenu contextMenu)
        {
            m_Model = model;
            fixedItemHeight = 16;

            // The name column acts as the primary column. This drives the expand and collapse toggle.
            m_NameColumn = new HierarchyNameColumn(model, contextMenu)
            {
                OnMakeCell = e => OnMakeItem?.Invoke(e)
            };

            columns.Add(m_NameColumn);

            reorderable = true;
            ListViewBridge.SetDragAndDropController(this, new HierarchyDragAndDropController(m_Model));
        }

        public void SetHeaderVisible(bool value)
        {
            var header = this.Q(className: "unity-multi-column-header");

            if (null != header)
            {
                // As it turns out the internals of the multi column view read the Display style of the column to drive the visibility of the cells.
                // As a workaround we use the height here instead.
                header.style.height = value ? 20 : 0;
            }
        }

        public HierarchyListViewItem GetItem(HierarchyNodeHandle handle)
        {
            return m_NameColumn.GetItem(handle);
        }

        protected override CollectionViewController CreateViewController() => new HierarchyMultiColumnListViewController(m_Model, columns, sortColumnDescriptions, m_SortedColumns);
    }

    class HierarchyNameColumn : Column
    {
        readonly BasicPool<HierarchyListViewItem> m_Pool;
        readonly HierarchyModel m_Model;

        /// <summary>
        /// The set of user defined decorators to modify the hierarchy elements.
        /// </summary>
        HierarchyDecoratorCollection m_Decorators;

        /// <summary>
        /// Cached list of created items. This is used to late initialize any decorators that hav been added after the view is already created.
        /// </summary>
        readonly List<HierarchyListViewItem> m_Items = new List<HierarchyListViewItem>();

        /// <summary>
        /// Callback invoked whenever a new row element is created.
        /// </summary>
        public Action<HierarchyListViewItem> OnMakeCell { get; set; }

        public HierarchyNameColumn(Hierarchy model, HierarchyContextMenu contextMenu)
        {
            m_Model = model;
            m_Pool = new BasicPool<HierarchyListViewItem>(() =>
            {
                var element = new HierarchyListViewItem(m_Model);
                contextMenu.RegisterCallbacksOnTarget(element);
                OnMakeCell?.Invoke(element);
                return element;
            });

            name = "Hierarchy";
            resizable = false;
            stretchable = true;

            makeHeader += MakeHeader;
            destroyHeader += DestroyHeader;
            makeCell += MakeCell;
            destroyCell += DestroyCell;
            bindCell += BindCell;
            unbindCell += UnbindCell;
        }

        public HierarchyListViewItem GetItem(HierarchyNodeHandle handle)
        {
            foreach (var item in m_Items)
                if (item.Handle == handle)
                    return item;

            return null;
        }

        public void SetDecorators(HierarchyDecoratorCollection decorators)
        {
            // Teardown existing decorators.
            if (null != m_Decorators)
            {
                foreach (var decorator in m_Decorators)
                    OnDecoratorRemoved(decorator);

                m_Decorators.OnAdd -= OnDecoratorAdded;
                m_Decorators.OnRemove -= OnDecoratorRemoved;
            }

            m_Decorators = decorators;

            // Initialize newly added decorators.
            if (null != m_Decorators)
            {
                foreach (var decorator in m_Decorators)
                    OnDecoratorAdded(decorator);

                m_Decorators.OnAdd += OnDecoratorAdded;
                m_Decorators.OnRemove += OnDecoratorRemoved;
            }
        }

        void OnDecoratorAdded(IHierarchyItemDecorator decorator)
        {
            foreach (var item in m_Items)
            {
                decorator.OnCreateItem(item);

                if (item.Index != -1)
                    decorator.OnBindItem(item, m_Model.GetNodes()[item.Index]);
            }
        }

        void OnDecoratorRemoved(IHierarchyItemDecorator decorator)
        {
            foreach (var item in m_Items)
            {
                if (item.Index != -1)
                    decorator.OnUnbindItem(item);

                decorator.OnDestroyItem(item);
            }
        }

        VisualElement MakeHeader()
        {
            return new VisualElement();
        }

        void DestroyHeader(VisualElement element)
        {
        }

        VisualElement MakeCell()
        {
            var item = m_Pool.Acquire();
            m_Items.Add(item);
            item.RegisterEventListeners();
            OnMakeCell?.Invoke(item);

            if (null != m_Decorators)
                foreach (var decorator in m_Decorators)
                    decorator.OnCreateItem(item);

            return item;
        }

        void BindCell(VisualElement element, int index)
        {
            if (m_Model.World is {IsCreated: false})
                return;

            if (element is not HierarchyListViewItem item)
                return;

            var node = m_Model.GetNodes()[index];

            if (!item.IsChanged(node))
                return;

            item.Bind(index, node);

            if (null != m_Decorators)
                foreach (var decorator in m_Decorators)
                    decorator.OnBindItem(item, node);
        }

        void UnbindCell(VisualElement element, int index)
        {
            if (element is not HierarchyListViewItem item)
                return;

            if (null != m_Decorators)
                foreach (var decorator in m_Decorators)
                    decorator.OnUnbindItem(item);

            item.Unbind();
        }

        void DestroyCell(VisualElement element)
        {
            if (element is not HierarchyListViewItem item)
                return;

            if (null != m_Decorators)
                foreach (var decorator in m_Decorators)
                    decorator.OnDestroyItem(item);


            m_Items.Remove(item);
            item.UnregisterEventListeners();
            m_Pool.Release(item);
        }
    }
}
