using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Editor.Bridge;
using UnityEngine.UIElements;
using ListView = Unity.Editor.Bridge.ListView;
using TreeView = Unity.Editor.Bridge.TreeView;

namespace Unity.Entities.Editor.Tests
{
    [TestFixture]
    class TreeViewTests
    {
        const int s_RootItemCount = 5;
        const int s_TreeViewSize = 200;

        const string s_LabelIdName = "label-id";
        const string s_LabelSiblingIndexName = "label-sibling-index";

        int m_FinalId;
        TreeView m_TreeView;
        ListView m_ListView;
        ScrollView m_ScrollView;
        IList<ITreeViewItem> m_RawItemList;

        [SetUp]
        public void TestsSetup()
        {
            int nextId = 0;
            m_RawItemList = GenerateItemList(s_RootItemCount, ref nextId);
            m_FinalId = nextId;

            Func<VisualElement> makeItem = () =>
            {
                var box = new VisualElement();
                box.style.flexDirection = FlexDirection.Row;
                box.style.flexGrow = 1f;
                box.style.flexShrink = 0f;
                box.style.flexBasis = 0f;

                var labelId = new Label() { name = s_LabelIdName };
                var labelSiblingIndex = new Label() { name = s_LabelSiblingIndexName };

                box.Add(labelId);
                box.Add(labelSiblingIndex);
                return box;
            };

            Action<VisualElement, ITreeViewItem> bindItem = (e, i) =>
            {
                e.Q<Label>(s_LabelIdName).text = i.id.ToString();
                e.Q<Label>(s_LabelSiblingIndexName).text = ((Unity.Editor.Bridge.TreeViewItemData<int>) i).data.ToString();
            };

            m_TreeView = new TreeView(m_RawItemList, 20, makeItem, ve => { }, bindItem);
            m_ListView = m_TreeView.Q<ListView>();
            m_ScrollView = m_ListView.Q<ScrollView>();

            m_TreeView.selectionType = SelectionType.Single;
            m_TreeView.style.height = s_TreeViewSize;
            m_TreeView.style.width = s_TreeViewSize;
        }

        IList<ITreeViewItem> GenerateItemList(int count, ref int nextId)
        {
            var items = new List<ITreeViewItem>(count);

            for (int i = 0; i < count; ++i)
            {
                var currentId = nextId;
                nextId++;

                var newItem = new Unity.Editor.Bridge.TreeViewItemData<int>
                {
                    id = currentId,
                    data = i
                };

                if (count > 2)
                    newItem.AddChildren(GenerateItemList(count / 2, ref nextId));

                items.Add(newItem);
            }

            return items;
        }

        void CheckFirstItemExpansion()
        {
            Assert.AreEqual(s_RootItemCount + s_RootItemCount / 2, m_ListView.contentContainer.childCount);

            // Look at the first item, plus its immediate children.
            var iterator = m_ListView.contentContainer.Children().GetEnumerator();
            for (int i = 0; i < 1 + s_RootItemCount / 2; i++)
            {
                iterator.MoveNext();
                var currentElement = iterator.Current;
                Assert.AreEqual(i.ToString(), currentElement.Q<Label>(s_LabelIdName).text);
            }

            // Next item should be the second root item.
            iterator.MoveNext();
            Assert.AreEqual(1.ToString(), iterator.Current.Q<Label>(s_LabelSiblingIndexName).text);
        }

        [Test]
        public void NoDataSource()
        {
            var emptyTreeView = new TreeView();

            Assert.Throws<ArgumentOutOfRangeException>(() => emptyTreeView.SetSelection(0));
            // Nothing should happen.
            emptyTreeView.Refresh();
        }

        [Test]
        public void CycleThroughAllItems()
        {
            int i = 0;
            foreach (var item in m_TreeView.items)
            {
                Assert.AreEqual(i, item.id);
                i++;
            }
            Assert.AreEqual(m_FinalId, i);
        }

        [Test]
        public void TriggerExpandedStateChangingEvent()
        {
            var events = new List<(ITreeViewItem item, bool isExpanded)>();
            m_TreeView.ItemExpandedStateChanging += (item, isExpanded) => events.Add((item, isExpanded));

            var itemToExpand = m_RawItemList[0];
            m_TreeView.ExpandItem(itemToExpand.id);
            Assert.That(events, Is.EquivalentTo(new[] { (itemToExpand, true) }));
            events.Clear();

            m_TreeView.ExpandItem(itemToExpand.id);
            Assert.That(events, Is.Empty);

            m_TreeView.CollapseItem(itemToExpand.id);
            Assert.That(events, Is.EquivalentTo(new[] { (itemToExpand, false) }));
            events.Clear();

            m_TreeView.CollapseItem(itemToExpand.id);
            Assert.That(events, Is.Empty);
        }

        [Test]
        public void ShowBorderOption()
        {
            m_TreeView.showBorder = false;
            Assert.IsFalse(m_ListView.ClassListContains(ListView.borderUssClassName));
            m_TreeView.showBorder = true;
            Assert.IsTrue(m_ListView.ClassListContains(ListView.borderUssClassName));
            m_TreeView.showBorder = false;
            Assert.IsFalse(m_ListView.ClassListContains(ListView.borderUssClassName));
        }

        [Test]
        public void SelectedItem_RefreshAfterRootItemsChanged()
        {
            m_TreeView.Select(0, false);

            var oldSelection = m_TreeView.selectedItem;
            Assert.That(oldSelection, Is.Not.Null);
            Assert.That(oldSelection.id, Is.Zero);

            var nextId = 0;
            m_TreeView.rootItems = GenerateItemList(s_RootItemCount, ref nextId);

            var newSelection = m_TreeView.selectedItem;
            Assert.That(newSelection, Is.Not.Null);
            Assert.That(newSelection, Is.Not.EqualTo(oldSelection));
            Assert.That(newSelection.id, Is.Zero);

            m_TreeView.rootItems = m_RawItemList;
            Assert.That(m_TreeView.selectedItem, Is.EqualTo(oldSelection));
        }
    }
}
