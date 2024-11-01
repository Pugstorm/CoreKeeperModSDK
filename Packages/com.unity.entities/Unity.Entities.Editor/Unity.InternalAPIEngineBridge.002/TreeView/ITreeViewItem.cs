using System.Collections.Generic;

namespace Unity.Editor.Bridge
{
    interface ITreeViewItem
    {
        int id { get; }
        ITreeViewItem parent { get; }
        IEnumerable<ITreeViewItem> children { get; }
        bool hasChildren { get; }
        void AddChild(ITreeViewItem child);
        void AddChildren(IList<ITreeViewItem> children);
        void RemoveChild(ITreeViewItem child);
    }
}
