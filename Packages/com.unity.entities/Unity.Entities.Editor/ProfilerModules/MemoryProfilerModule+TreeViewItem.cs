using Unity.Editor.Bridge;

namespace Unity.Entities.Editor
{
    partial class MemoryProfilerModule
    {
        class MemoryProfilerTreeViewItem : TreeViewItem<MemoryProfilerTreeViewItem>
        {
            public string displayName { get; set; }
            public ulong totalAllocatedBytes { get; set; }
            public ulong totalUnusedBytes { get; set; }
            public MemoryProfilerTreeViewItemData data { get; set; }

            public override void Reset()
            {
                base.Reset();
                displayName = null;
                totalAllocatedBytes = 0;
                totalUnusedBytes = 0;
                data = default;
            }
        }
    }
}
