using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Editor.Bridge
{
    class TreeViewItem : ITreeViewItem
    {
        List<ITreeViewItem> m_Children;

        public int id { get; set; }

        public ITreeViewItem parent { get; set; }

        public IEnumerable<ITreeViewItem> children => m_Children ?? Enumerable.Empty<ITreeViewItem>();

        public bool hasChildren => m_Children?.Count > 0;

        public void AddChild(ITreeViewItem child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            if (child == this)
                throw new ArgumentException($"{nameof(child)} cannot be self.");

            if (!(child is TreeViewItem item))
                throw new ArgumentException($"{nameof(child)} must derive from {nameof(TreeViewItem)}.");

            if (m_Children == null)
                m_Children = new List<ITreeViewItem>();

            item.parent = this;
            m_Children.Add(child);
        }

        public void AddChildren(IList<ITreeViewItem> children)
        {
            if (children == null)
                throw new ArgumentNullException(nameof(children));

            foreach (var child in children)
                AddChild(child);
        }

        public void RemoveChild(ITreeViewItem child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            if (child == this)
                throw new ArgumentException($"{nameof(child)} cannot be self.");

            if (!(child is TreeViewItem item))
                throw new ArgumentException($"{nameof(child)} must derive from {nameof(TreeViewItem)}.");

            if (m_Children == null || m_Children.Count == 0)
                return;

            item.parent = null;
            m_Children.Remove(child);
        }

        public void SortChildren<TKey>(Func<ITreeViewItem, TKey> keySelector, bool ascending = true)
        {
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            if (m_Children == null || m_Children.Count == 0)
                return;

            if (ascending)
                m_Children = m_Children.OrderBy(keySelector).ToList();
            else
                m_Children = m_Children.OrderByDescending(keySelector).ToList();
        }

        public void SortChildrenRecursive<TKey>(Func<ITreeViewItem, TKey> keySelector, bool ascending = true)
        {
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            if (m_Children == null || m_Children.Count == 0)
                return;

            SortChildren(keySelector, ascending);
            foreach (TreeViewItem child in m_Children)
                child.SortChildrenRecursive(keySelector, ascending);
        }

        public virtual void Reset()
        {
            id = 0;
            parent = null;
            m_Children = null;
        }
    }

    class TreeViewItem<TItem> : ITreeViewItem
        where TItem : TreeViewItem<TItem>
    {
        List<TItem> m_Children;

        public int id { get; set; }

        public TItem parent { get; set; }

        public IEnumerable<TItem> children => m_Children ?? Enumerable.Empty<TItem>();

        public bool hasChildren => m_Children?.Count > 0;

        public int childCount => m_Children?.Count ?? 0;

        public void AddChild(TItem child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            if (child == this)
                throw new ArgumentException($"{nameof(child)} cannot be self.");

            if (m_Children == null)
                m_Children = new List<TItem>();

            child.parent = (TItem)this;
            m_Children.Add(child);
        }

        public void AddChildren(IList<TItem> children)
        {
            if (children == null)
                throw new ArgumentNullException(nameof(children));

            foreach (var child in children)
                AddChild(child);
        }

        public void RemoveChild(TItem child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            if (child == this)
                throw new ArgumentException($"{nameof(child)} cannot be self.");

            if (m_Children == null || m_Children.Count == 0)
                return;

            m_Children.Remove(child);
        }

        public void SortChildren<TKey>(Func<TItem, TKey> keySelector, bool ascending = true)
        {
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            if (m_Children == null || m_Children.Count == 0)
                return;

            if (ascending)
                m_Children = m_Children.OrderBy(keySelector).ToList();
            else
                m_Children = m_Children.OrderByDescending(keySelector).ToList();
        }

        public void SortChildrenRecursive<TKey>(Func<TItem, TKey> keySelector, bool ascending = true)
        {
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            if (m_Children == null || m_Children.Count == 0)
                return;

            SortChildren(keySelector, ascending);
            foreach (TreeViewItem<TItem> child in m_Children)
                child.SortChildrenRecursive(keySelector, ascending);
        }

        public virtual void Reset()
        {
            id = 0;
            parent = null;
            m_Children = null;
        }

        #region ITreeViewItem

        int ITreeViewItem.id => id;

        ITreeViewItem ITreeViewItem.parent => parent;

        IEnumerable<ITreeViewItem> ITreeViewItem.children => children;

        bool ITreeViewItem.hasChildren => hasChildren;

        void ITreeViewItem.AddChild(ITreeViewItem child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            if (child == this)
                throw new ArgumentException($"{nameof(child)} cannot be self.");

            if (!(child is TItem item))
                throw new ArgumentException($"{nameof(child)} must derive from {nameof(TItem)}.");

            AddChild(item);
        }

        void ITreeViewItem.AddChildren(IList<ITreeViewItem> children)
        {
            if (children == null)
                throw new ArgumentNullException(nameof(children));

            if (!children.All(child => child is TItem))
                throw new ArgumentException($"all elements in {nameof(children)} must derive from {nameof(TItem)}.");

            AddChildren((IList<TItem>)children);
        }

        void ITreeViewItem.RemoveChild(ITreeViewItem child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            if (child == this)
                throw new ArgumentException($"{nameof(child)} cannot be self.");

            if (!(child is TItem item))
                throw new ArgumentException($"{nameof(child)} must derive from {nameof(TItem)}.");

            RemoveChild(item);
        }

        #endregion
    }

    class TreeViewItemData<T> : TreeViewItem
    {
        public T data { get; set; }

        public override void Reset()
        {
            base.Reset();
            data = default(T);
        }
    }
}
