using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace Unity.Editor.Bridge
{
    class TreeView : VisualElement, ISerializationCallbackReceiver
    {
        static readonly string s_ListViewName = "unity-tree-view__list-view";
        static readonly string s_ItemName = "unity-tree-view__item";
        static readonly string s_ItemToggleName = "unity-tree-view__item-toggle";
        static readonly string s_ItemIndentsContainerName = "unity-tree-view__item-indents";
        static readonly string s_ItemIndentName = "unity-tree-view__item-indent";
        static readonly string s_ItemContentContainerName = "unity-tree-view__item-content";

        Func<VisualElement> m_MakeItem;
        public Func<VisualElement> makeItem
        {
            get { return m_MakeItem; }
            set
            {
                if (m_MakeItem == value)
                    return;
                m_MakeItem = value;
                m_ListView.Refresh();
            }
        }

        Action<VisualElement> m_ReleaseItem;

        public event Action<IEnumerable<ITreeViewItem>> onItemsChosen;
        public event Action<IEnumerable<ITreeViewItem>> onSelectionChange;

        // Avoids recreating a collection each time we want to select a single item (see: Select(int, bool))
        readonly int[] m_CachedSingleSelectionBuffer = new int[1];

        List<ITreeViewItem> m_SelectedItems;

        public ITreeViewItem selectedItem => selectedItems.FirstOrDefault();

        public IEnumerable<ITreeViewItem> selectedItems
        {
            get
            {
                if (m_SelectedItems != null)
                    return m_SelectedItems;

                m_SelectedItems = new List<ITreeViewItem>();
                foreach (var treeItem in items)
                {
                    foreach (var itemId in m_ListView.currentSelectionIds)
                    {
                        if (treeItem.id == itemId)
                            m_SelectedItems.Add(treeItem);
                    }
                }

                return m_SelectedItems;
            }
        }

        Action<VisualElement, ITreeViewItem> m_BindItem;
        public Action<VisualElement, ITreeViewItem> bindItem
        {
            get { return m_BindItem; }
            set
            {
                m_BindItem = value;
                m_ListView.Refresh();
            }
        }

        public Action<VisualElement, ITreeViewItem> unbindItem { get; set; }

        IList<ITreeViewItem> m_RootItems;
        public IList<ITreeViewItem> rootItems
        {
            get { return m_RootItems; }
            set
            {
                m_RootItems = value;
                m_SelectedItems = null;
                Refresh();
            }
        }

        public IEnumerable<ITreeViewItem> items => GetAllItems(m_RootItems);

        public float resolvedItemHeight => m_ListView.resolvedItemHeight;

        public int itemHeight
        {
            get { return m_ListView.itemHeight; }
            set { m_ListView.itemHeight = value; }
        }

        public new string viewDataKey
        {
            get => base.viewDataKey;
            set
            {
                base.viewDataKey = value;
                m_ListView.viewDataKey = value;
            }
        }

        public bool showBorder
        {
            get { return m_ListView.showBorder; }
            set { m_ListView.showBorder = value; }
        }

        public SelectionType selectionType
        {
            get { return m_ListView.selectionType; }
            set { m_ListView.selectionType = value; }
        }

        //public AlternatingRowBackground showAlternatingRowBackgrounds
        //{
        //    get { return m_ListView.showAlternatingRowBackgrounds; }
        //    set { m_ListView.showAlternatingRowBackgrounds = value; }
        //}

        struct TreeViewItemWrapper
        {
            public int id => item.id;
            public int depth;

            public bool hasChildren => item.hasChildren;

            public ITreeViewItem item;
        }

        // R# is confused here: this attribute is indeed required
        // ReSharper disable once Unity.RedundantSerializeFieldAttribute
        [SerializeField]
        List<int> m_SerializedExpandedItemIds;
        HashSet<int> m_ExpandedItemIds;

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            if (m_ExpandedItemIds != null)
                m_SerializedExpandedItemIds = m_ExpandedItemIds.ToList();
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (m_SerializedExpandedItemIds != null)
                m_ExpandedItemIds = new HashSet<int>(m_SerializedExpandedItemIds);
        }

        List<TreeViewItemWrapper> m_ItemWrappers;

        readonly ListView m_ListView;
        readonly ScrollView m_ScrollView;

        public TreeView()
        {
            m_SelectedItems = null;
            m_ExpandedItemIds = new HashSet<int>();
            m_ItemWrappers = new List<TreeViewItemWrapper>();

            m_ListView = new ListView();
            m_ListView.name = s_ListViewName;
            m_ListView.itemsSource = m_ItemWrappers;
            m_ListView.AddToClassList(s_ListViewName);
            hierarchy.Add(m_ListView);

            m_ListView.makeItem = MakeTreeItem;
            m_ListView.releaseItem = ReleaseTreeItem;
            m_ListView.bindItem = BindTreeItem;
            m_ListView.getItemId = GetItemId;

            m_ListView.unbindItem = UnbindTreeItem;
            m_ListView.onItemsChosen += OnItemsChosen;

            m_ScrollView = m_ListView.m_ScrollView;
            m_ScrollView.contentContainer.RegisterCallback<KeyDownEvent>(OnKeyDown);
            m_ListView.onSelectionChange += OnSelectionChange;

            RegisterCallback<MouseUpEvent>(OnTreeViewMouseUp, TrickleDown.TrickleDown);
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        public TreeView(
            IList<ITreeViewItem> items,
            int itemHeight,
            Func<VisualElement> makeItem,
            Action<VisualElement> releaseItem,
            Action<VisualElement, ITreeViewItem> bindItem) : this()
        {
            m_ListView.itemHeight = itemHeight;
            m_MakeItem = makeItem;
            m_ReleaseItem = releaseItem;
            m_BindItem = bindItem;
            m_RootItems = items;

            Refresh();
        }

        static readonly ProfilerMarker k_RegenerateWrappersMarker = new ProfilerMarker($"{nameof(TreeView)}.{nameof(RegenerateWrappers)}()");
        static readonly ProfilerMarker k_RefreshListViewMarker = new ProfilerMarker($"{nameof(TreeView)} Refresh ListView");

        public void Refresh()
        {
            using (k_RegenerateWrappersMarker.Auto())
                RegenerateWrappers();

            using (k_RefreshListViewMarker.Auto())
                m_ListView.Refresh();
        }

        internal override void OnViewDataReady()
        {
            base.OnViewDataReady();

            string key = GetFullHierarchicalViewDataKey();

            OverwriteFromViewData(this, key);

            Refresh();
        }

        public static IEnumerable<ITreeViewItem> GetAllItems(IEnumerable<ITreeViewItem> rootItems)
        {
            if (rootItems == null)
                yield break;

            var iteratorStack = new Stack<IEnumerator<ITreeViewItem>>();
            var currentIterator = rootItems.GetEnumerator();

            while (true)
            {
                bool hasNext = currentIterator.MoveNext();
                if (!hasNext)
                {
                    if (iteratorStack.Count > 0)
                    {
                        currentIterator = iteratorStack.Pop();
                        continue;
                    }

                    // We're at the end of the root items list.
                    break;
                }

                var currentItem = currentIterator.Current;
                yield return currentItem;

                if (currentItem.hasChildren)
                {
                    iteratorStack.Push(currentIterator);
                    currentIterator = currentItem.children.GetEnumerator();
                }
            }
        }

        public void OnKeyDown(KeyDownEvent evt)
        {
            var index = m_ListView.selectedIndex;

            bool shouldStopPropagation = true;

            switch (evt.keyCode)
            {
                case KeyCode.RightArrow:
                    if (!IsExpandedByIndex(index))
                        ExpandItemByIndex(index);
                    break;
                case KeyCode.LeftArrow:
                    if (IsExpandedByIndex(index))
                        CollapseItemByIndex(index);
                    break;
                default:
                    shouldStopPropagation = false;
                    break;
            }

            if (shouldStopPropagation)
                evt.StopPropagation();
        }

        public void SetSelection(int id)
        {
            SetSelection(new[] { id });
        }

        public void SetSelection(IEnumerable<int> ids)
        {
            SetSelectionInternal(ids, true);
        }

        public void SetSelectionWithoutNotify(IEnumerable<int> ids)
        {
            SetSelectionInternal(ids, false);
        }

        internal void SetSelectionInternal(IEnumerable<int> ids, bool sendNotification)
        {
            if (ids == null)
                return;

            var selectedIndexes = ids.Select(id => GetItemIndex(id, true)).ToList();
            m_ListView.Refresh();
            m_ListView.SetSelectionInternal(selectedIndexes, sendNotification);
        }

        public void AddToSelection(int id)
        {
            var index = GetItemIndex(id, true);
            m_ListView.Refresh();
            m_ListView.AddToSelection(index);
        }

        public void RemoveFromSelection(int id)
        {
            var index = GetItemIndex(id);
            m_ListView.RemoveFromSelection(index);
        }

        int GetItemIndex(int id, bool expand = false)
        {
            var item = FindItem(id);
            if (item == null)
                throw new ArgumentOutOfRangeException(nameof(id), id, $"{nameof(TreeView)}: Item id not found.");

            if (expand)
            {
                bool regenerateWrappers = false;
                var itemParent = item.parent;
                while (itemParent != null)
                {
                    if (!m_ExpandedItemIds.Contains(itemParent.id))
                    {
                        m_ExpandedItemIds.Add(itemParent.id);
                        regenerateWrappers = true;
                    }
                    itemParent = itemParent.parent;
                }

                if (regenerateWrappers)
                    RegenerateWrappers();
            }


            var index = 0;
            for (; index < m_ItemWrappers.Count; ++index)
                if (m_ItemWrappers[index].id == id)
                    break;

            return index;
        }

        public void ClearSelection()
        {
            m_ListView.ClearSelection();
        }

        public void Select(int id, bool sendNotification)
        {
            var index = GetItemIndex(id, true);
            Refresh();
            m_ListView.ScrollToItem(index);

            m_CachedSingleSelectionBuffer[0] = index;
            m_ListView.SetSelectionInternal(m_CachedSingleSelectionBuffer, sendNotification);
        }

        public void ScrollTo(VisualElement visualElement)
        {
            m_ListView.ScrollTo(visualElement);
        }

        public void ScrollToItem(int id)
        {
            var index = GetItemIndex(id, true);
            Refresh();
            m_ListView.ScrollToItem(index);
        }

        public bool IsExpanded(int id)
        {
            return m_ExpandedItemIds.Contains(id);
        }

        public void CollapseItem(int id)
        {
            // Make sure the item is valid.
            if (FindItem(id) == null)
                throw new ArgumentOutOfRangeException(nameof(id), id, $"{nameof(TreeView)}: Item id not found.");

            // Try to find it in the currently visible list.
            for (int i = 0; i < m_ItemWrappers.Count; ++i)
                if (m_ItemWrappers[i].item.id == id)
                    if (IsExpandedByIndex(i))
                    {
                        CollapseItemByIndex(i);
                        return;
                    }

            if (!m_ExpandedItemIds.Contains(id))
                return;

            m_ExpandedItemIds.Remove(id);
            Refresh();
        }

        public void ExpandItem(int id)
        {
            // Make sure the item is valid.
            if (FindItem(id) == null)
                throw new ArgumentOutOfRangeException(nameof(id), id, $"{nameof(TreeView)}: Item id not found.");

            // Try to find it in the currently visible list.
            for (int i = 0; i < m_ItemWrappers.Count; ++i)
                if (m_ItemWrappers[i].item.id == id)
                    if (!IsExpandedByIndex(i))
                    {
                        ExpandItemByIndex(i);
                        return;
                    }

            if (m_ExpandedItemIds.Contains(id))
                return;

            m_ExpandedItemIds.Add(id);
            Refresh();
        }

        internal void PrepareItemsToExpand(List<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return;

            foreach (var id in ids)
            {
                m_ExpandedItemIds.Add(id);
            }
        }

        public ITreeViewItem FindItem(int id)
        {
            foreach (var item in items)
                if (item.id == id)
                    return item;

            return null;
        }

        void OnItemsChosen(IEnumerable<object> chosenItems)
        {
            if (onItemsChosen == null)
                return;

            var itemsList = new List<ITreeViewItem>();
            foreach (var item in chosenItems)
            {
                var wrapper = (TreeViewItemWrapper)item;
                itemsList.Add(wrapper.item);
            }

            onItemsChosen.Invoke(itemsList);
        }

        void OnSelectionChange(IEnumerable<object> selectedListItems)
        {
            if (m_SelectedItems == null)
                m_SelectedItems = new List<ITreeViewItem>();

            m_SelectedItems.Clear();
            foreach (var item in selectedListItems)
                m_SelectedItems.Add(((TreeViewItemWrapper)item).item);

            onSelectionChange?.Invoke(m_SelectedItems);
        }

        void OnTreeViewMouseUp(MouseUpEvent evt)
        {
            m_ScrollView.contentContainer.Focus();
        }

        void OnItemMouseUp(MouseUpEvent evt)
        {
            if ((evt.modifiers & EventModifiers.Alt) == 0)
                return;

            var target = evt.currentTarget as VisualElement;
            var toggle = target.Q<Toggle>(s_ItemToggleName);
            var index = (int)toggle.userData;
            var item = m_ItemWrappers[index].item;
            var wasExpanded = IsExpandedByIndex(index);

            if (!item.hasChildren)
                return;


            if (wasExpanded)
                m_ExpandedItemIds.Remove(item.id);
            else
                m_ExpandedItemIds.Add(item.id);

            ItemExpandedStateChanging(item, !wasExpanded);

            foreach (var child in GetAllItems(item.children))
            {
                if (child.hasChildren)
                {
                    if (wasExpanded)
                        m_ExpandedItemIds.Remove(child.id);
                    else
                        m_ExpandedItemIds.Add(child.id);
                    ItemExpandedStateChanging(child, !wasExpanded);
                }
            }

            Refresh();

            evt.StopPropagation();
        }

        VisualElement MakeTreeItem()
        {
            var itemContainer = new VisualElement()
            {
                name = s_ItemName,
                style =
                {
                    flexDirection = FlexDirection.Row
                }
            };
            itemContainer.AddToClassList(s_ItemName);
            itemContainer.RegisterCallback<MouseUpEvent>(OnItemMouseUp);

            var indents = new VisualElement()
            {
                name = s_ItemIndentsContainerName,
                style =
                {
                    flexDirection = FlexDirection.Row
                }
            };
            indents.AddToClassList(s_ItemIndentsContainerName);
            itemContainer.hierarchy.Add(indents);

            var toggle = new Toggle() { name = s_ItemToggleName };
            toggle.AddToClassList(Foldout.toggleUssClassName);
            toggle.RegisterValueChangedCallback(ToggleExpandedState);
            itemContainer.hierarchy.Add(toggle);

            var userContentContainer = new VisualElement()
            {
                name = s_ItemContentContainerName,
                style =
                {
                    flexGrow = 1,
                    flexShrink = 1
                }
            };
            userContentContainer.AddToClassList(s_ItemContentContainerName);
            itemContainer.Add(userContentContainer);

            if (m_MakeItem != null)
                userContentContainer.Add(m_MakeItem());

            return itemContainer;
        }

        void ReleaseTreeItem(VisualElement ve)
        {
            m_ReleaseItem?.Invoke(ve.Q(s_ItemContentContainerName)[0]);
        }

        void UnbindTreeItem(VisualElement element, int index)
        {
            if (unbindItem == null)
                return;

            var item = m_ItemWrappers[index].item;
            var userContentContainer = element.Q(s_ItemContentContainerName).ElementAt(0);
            unbindItem(userContentContainer, item);
        }

        void BindTreeItem(VisualElement element, int index)
        {
            var item = m_ItemWrappers[index].item;

            // Add indentation.
            var indents = element.Q(s_ItemIndentsContainerName);
            indents.Clear();
            for (int i = 0; i < m_ItemWrappers[index].depth; ++i)
            {
                var indentElement = new VisualElement();
                indentElement.AddToClassList(s_ItemIndentName);
                indents.Add(indentElement);
            }

            // Set toggle data.
            var toggle = element.Q<Toggle>(s_ItemToggleName);
            toggle.SetValueWithoutNotify(IsExpandedByIndex(index));
            toggle.userData = index;
            if (item.hasChildren)
                toggle.visible = true;
            else
                toggle.visible = false;

            if (m_BindItem == null)
                return;

            // Bind user content container.
            var userContentContainer = element.Q(s_ItemContentContainerName).ElementAt(0);
            m_BindItem(userContentContainer, item);
        }

        int GetItemId(int index)
        {
            return m_ItemWrappers[index].id;
        }

        bool IsExpandedByIndex(int index)
        {
            return m_ExpandedItemIds.Contains(m_ItemWrappers[index].id);
        }

        void CollapseItemByIndex(int index)
        {
            if (!m_ItemWrappers[index].item.hasChildren)
                return;

            ItemExpandedStateChanging(m_ItemWrappers[index].item, false);
            m_ExpandedItemIds.Remove(m_ItemWrappers[index].item.id);

            int recursiveChildCount = 0;
            int currentIndex = index + 1;
            int currentDepth = m_ItemWrappers[index].depth;
            while (currentIndex < m_ItemWrappers.Count && m_ItemWrappers[currentIndex].depth > currentDepth)
            {
                recursiveChildCount++;
                currentIndex++;
            }

            m_ItemWrappers.RemoveRange(index + 1, recursiveChildCount);

            m_ListView.Refresh();

            SaveViewData();
        }

        void ExpandItemByIndex(int index)
        {
            if (!m_ItemWrappers[index].item.hasChildren)
                return;

            ItemExpandedStateChanging(m_ItemWrappers[index].item, true);
            var childWrappers = new List<TreeViewItemWrapper>();
            CreateWrappers(m_ItemWrappers[index].item.children, m_ItemWrappers[index].depth + 1, childWrappers);

            m_ItemWrappers.InsertRange(index + 1, childWrappers);

            m_ExpandedItemIds.Add(m_ItemWrappers[index].item.id);

            m_ListView.Refresh();

            SaveViewData();
        }

        void ToggleExpandedState(ChangeEvent<bool> evt)
        {
            var toggle = evt.target as Toggle;
            var index = (int)toggle.userData;
            var isExpanded = IsExpandedByIndex(index);

            Assert.AreNotEqual(isExpanded, evt.newValue);

            if (isExpanded)
                CollapseItemByIndex(index);
            else
                ExpandItemByIndex(index);

            // To make sure our TreeView gets focus, we need to force this. :(
            m_ScrollView.contentContainer.Focus();
        }

        public Action<ITreeViewItem, bool> ItemExpandedStateChanging = delegate { };

        public delegate bool OnFilter(ITreeViewItem item);

        public OnFilter Filter
        {
            get => m_Filter;

            set => m_Filter = value;
        }

        OnFilter m_Filter;

        bool CreateWrappers(IEnumerable<ITreeViewItem> treeViewItems, int depth, List<TreeViewItemWrapper> wrappers)
        {
            //Depth First Search
            var added = false;
            foreach (var item in treeViewItems)
            {
                var toAdd = false;
                var wrapper = new TreeViewItemWrapper()
                {
                    depth = depth,
                    item = item
                };

                if (m_Filter != null)
                {
                    toAdd = m_Filter(item);
                }
                else
                {
                    toAdd = true;
                }

                wrappers.Add(wrapper);

                if (m_ExpandedItemIds.Contains(item.id) && item.hasChildren)
                    toAdd |= CreateWrappers(item.children, depth + 1, wrappers);

                // Remove if no match.
                if (!toAdd)
                    wrappers.RemoveAt(wrappers.Count - 1);

                added |= toAdd;
            }
            return added;
        }

        void RegenerateWrappers()
        {
            m_ItemWrappers.Clear();

            if (m_RootItems == null)
                return;

            CreateWrappers(m_RootItems, 0, m_ItemWrappers);
        }

        void OnCustomStyleResolved(CustomStyleResolvedEvent e)
        {
            int height;
            var oldHeight = m_ListView.itemHeight;
            if (!m_ListView.m_ItemHeightIsInline && e.customStyle.TryGetValue(ListView.s_ItemHeightProperty, out height))
                m_ListView.m_ItemHeight = height;

            if (m_ListView.m_ItemHeight != oldHeight)
                m_ListView.Refresh();
        }
    }
}
