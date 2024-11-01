using Unity.Editor.Bridge;

namespace Unity.Entities.Editor
{
    class StructuralChangesProfilerTreeViewItem : TreeViewItem<StructuralChangesProfilerTreeViewItem>
    {
        public string displayName { get; set; }
        public long totalElapsedNanoseconds { get; set; }
        public int totalCount { get; set; }

        public override void Reset()
        {
            base.Reset();
            displayName = null;
            totalElapsedNanoseconds = 0;
            totalCount = 0;
        }
    }
}
