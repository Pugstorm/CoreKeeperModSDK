using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using Unity.Profiling;
using Unity.Profiling.Editor;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine.UIElements;
using Hash128 = Unity.Entities.Hash128;

[System.Serializable]
[ProfilerModuleMetadata("Runtime Content Manager")]
class RuntimeContentManagerProfilerModule : ProfilerModule
{
    static readonly ProfilerCounterDescriptor[] k_Counters = new ProfilerCounterDescriptor[]
    {
        new ProfilerCounterDescriptor(RuntimeContentManagerProfiler.k_LoadedObjectsCounterName, ProfilerCategory.Loading),
        new ProfilerCounterDescriptor(RuntimeContentManagerProfiler.k_LoadedFilesCounterName, ProfilerCategory.Loading),
        new ProfilerCounterDescriptor(RuntimeContentManagerProfiler.k_LoadedArchivesCounterName, ProfilerCategory.Loading),
        new ProfilerCounterDescriptor(RuntimeContentManagerProfiler.k_LoadObjectRequestsCounterName, ProfilerCategory.Loading),
        new ProfilerCounterDescriptor(RuntimeContentManagerProfiler.k_ReleaseObjectRequestsCounterName, ProfilerCategory.Loading),
        new ProfilerCounterDescriptor(RuntimeContentManagerProfiler.k_ObjectRefsCounterName, ProfilerCategory.Loading),
        new ProfilerCounterDescriptor(RuntimeContentManagerProfiler.k_ProcessCommandsFrameTimeCounterName, ProfilerCategory.Loading),
        new ProfilerCounterDescriptor(RuntimeContentManagerProfiler.k_UnloadSceneRequestsCounterName, ProfilerCategory.Loading),
        new ProfilerCounterDescriptor(RuntimeContentManagerProfiler.k_LoadedScenesCounterName, ProfilerCategory.Loading),
        new ProfilerCounterDescriptor(RuntimeContentManagerProfiler.k_LoadSceneRequestsCounterName, ProfilerCategory.Loading),
    };

    public RuntimeContentManagerProfilerModule() : base(k_Counters) { }

    public override ProfilerModuleViewController CreateDetailsViewController()
    {
        return new RuntimeContentManagerProfilerModuleView(ProfilerWindow);
    }
}

class RuntimeContentManagerProfilerModuleView : ProfilerModuleViewController
{
    struct RCMProfilerFrameUIData
    {
        public RuntimeContentManagerProfilerFrameData frameData;
        public int type;
        public int totalRefCount;
        public UntypedWeakReferenceId id => frameData.id;
        public int parent => frameData.parent;
        public int refCount => frameData.refCount;
    }

    public RuntimeContentManagerProfilerModuleView(ProfilerWindow profilerWindow) : base(profilerWindow) { }
    MultiColumnTreeView m_tree;

    protected override VisualElement CreateView()
    {
        var columns = new Columns();
        columns.Add(new Column { bindHeader = e => (e as Label).text = "Content", makeHeader = MakeItemLabel, makeCell = MakeItemLabel, bindCell = BindCellId, destroyCell = DestroyLabel, width = 400 });
        columns.Add(new Column { bindHeader = e => (e as Label).text = "Reference Count", makeHeader = MakeItemLabel, makeCell = MakeItemLabel, bindCell = BindCellValueRefCount, destroyCell = DestroyLabel, width = 20 });
        columns.Add(new Column { bindHeader = e => (e as Label).text = "Reference Count Total", makeHeader = MakeItemLabel, makeCell = MakeItemLabel, bindCell = BindCellValueRefCountTotal, destroyCell = DestroyLabel, width = 20 });
        columns.Add(new Column { bindHeader = e => (e as Label).text = "Details", makeHeader = MakeItemLabel, makeCell = MakeItemLabel, bindCell = BindCellDetail, destroyCell = DestroyLabel, width = 500,  });
        m_tree = new MultiColumnTreeView(columns);
        m_tree.fixedItemHeight = 15;
        m_tree.autoExpand = false;
        
        ReloadData(ProfilerWindow.selectedFrameIndex);
        ProfilerWindow.SelectedFrameIndexChanged += OnSelectedFrameIndexChanged;
        return m_tree;
    }

    void BindCellValueRefCount(VisualElement e, int index)
    {
        var r = m_tree.GetItemDataForIndex<RCMProfilerFrameUIData>(index);
        (e as Label).text = r.refCount.ToString();
    }

    void BindCellValueRefCountTotal(VisualElement e, int index)
    {
        var r = m_tree.GetItemDataForIndex<RCMProfilerFrameUIData>(index);
        (e as Label).text = r.totalRefCount.ToString();
    }

    void BindCellId(VisualElement e, int index)
    {
        var r = m_tree.GetItemDataForIndex<RCMProfilerFrameUIData>(index);
        if (r.type == 2)
        {
            if (r.frameData.id.GenerationType == WeakReferenceGenerationType.SubSceneObjectReferences)
                (e as Label).text = "SubScene Objects";
            else
                (e as Label).text = Path.GetFileName(AssetDatabase.GUIDToAssetPath(r.id.GlobalId.AssetGUID));
        }
        else
            (e as Label).text = r.id.ToString();
    }

    void BindCellDetail(VisualElement e, int index)
    {
        var r = m_tree.GetItemDataForIndex<RCMProfilerFrameUIData>(index);
        if (r.type == 2)
        {
            if (r.frameData.id.GenerationType == WeakReferenceGenerationType.SubSceneObjectReferences)
                (e as Label).text = $"SubScene Objects Reference: {r.frameData.id.GlobalId.AssetGUID}:{r.frameData.id.GlobalId.SceneObjectIdentifier0}";
            else
                (e as Label).text = $"Asset: {AssetDatabase.GUIDToAssetPath(r.id.GlobalId.AssetGUID)}";
        }
        else if (r.type == 1)
            (e as Label).text = $"File: {r.id.GlobalId.AssetGUID}";
        else
            (e as Label).text = $"Archive: {r.id.GlobalId.AssetGUID}";
    }

    Queue<VisualElement> labelCache = new Queue<VisualElement>();
    void DestroyLabel(VisualElement e)
    {
        labelCache.Enqueue(e);
    }

    VisualElement MakeItemLabel()
    {
        if(labelCache.Count == 0)
            return new Label();
        return labelCache.Dequeue();
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
            return;

        ProfilerWindow.SelectedFrameIndexChanged -= OnSelectedFrameIndexChanged;

        base.Dispose(disposing);
    }

    private void OnSelectedFrameIndexChanged(long frame)
    {
        ReloadData(frame);
    }

    struct Sort : IComparer<TreeViewItemData<RCMProfilerFrameUIData>>
    {
        public int Compare(TreeViewItemData<RCMProfilerFrameUIData> x, TreeViewItemData<RCMProfilerFrameUIData> y)
        {
            return y.data.totalRefCount.CompareTo(x.data.totalRefCount);
        }
    }

    private void ReloadData(long frame)
    {
        if (frame < 0)
            return;

        if (EditorApplication.isPlaying && !EditorApplication.isPaused)
            return;
        RuntimeContentManagerProfiler.Initialize();
        using (var frameData = ProfilerDriver.GetHierarchyFrameDataView((int)frame, 0, HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnDontSort, false))
        {
            int itemId = 0;
            var archivesData = frameData.GetFrameMetaData<RuntimeContentManagerProfilerFrameData>(RuntimeContentManagerProfiler.Guid, 0);
            var filesData = frameData.GetFrameMetaData<RuntimeContentManagerProfilerFrameData>(RuntimeContentManagerProfiler.Guid, 1);
            var objectsData = frameData.GetFrameMetaData<RuntimeContentManagerProfilerFrameData>(RuntimeContentManagerProfiler.Guid, 2);
            if (archivesData.IsCreated && archivesData.Length > 0)
            {
                var filesToObjects = new Dictionary<int, List<TreeViewItemData<RCMProfilerFrameUIData>>>(filesData.Length);
                for(int i = 0; i < objectsData.Length; i++)
                {
                    var s = new RCMProfilerFrameUIData { frameData = objectsData[i], type = 2, totalRefCount = objectsData[i].refCount };
                    var item = new TreeViewItemData<RCMProfilerFrameUIData>(itemId++, s);
                    if (!filesToObjects.TryGetValue(s.parent, out var fileObjectList))
                        filesToObjects.Add(s.parent, fileObjectList = new List<TreeViewItemData<RCMProfilerFrameUIData>>());
                    fileObjectList.Add(item);
                }

                var archivesToFiles = new Dictionary<int, List<TreeViewItemData<RCMProfilerFrameUIData>>>(archivesData.Length);
                for (int i = 0; i < filesData.Length; i++)
                {
                    var s = new RCMProfilerFrameUIData { frameData = filesData[i], type = 1, totalRefCount = filesData[i].refCount };
                    if (filesToObjects.TryGetValue(s.id.GlobalId.AssetGUID.GetHashCode(), out var fileObjectList))
                    {
                        fileObjectList.Sort(new Sort());
                        foreach (var o in fileObjectList)
                            s.totalRefCount += o.data.totalRefCount;
                    }
                    var item = new TreeViewItemData<RCMProfilerFrameUIData>(itemId++, s, fileObjectList);
                    if (!archivesToFiles.TryGetValue(s.parent, out var archiveFileList))
                        archivesToFiles.Add(s.parent, archiveFileList = new List<TreeViewItemData<RCMProfilerFrameUIData>>());
                    archiveFileList.Add(item);
                }

                var archiveItemList = archivesData.Select(o =>
                {
                    var s = new RCMProfilerFrameUIData { frameData = o, type = 0, totalRefCount = o.refCount };
                    if (archivesToFiles.TryGetValue(s.id.GlobalId.AssetGUID.GetHashCode(), out var archiveFileList))
                    {
                        archiveFileList.Sort(new Sort());
                        foreach (var f in archiveFileList)
                            s.totalRefCount += f.data.totalRefCount;
                    }
                    return new TreeViewItemData<RCMProfilerFrameUIData>(itemId++, s, archiveFileList);
                }).ToList();
                archiveItemList.Sort(new Sort());
                m_tree.SetRootItems(archiveItemList);
            }
            else
            {
                var objectsItemList = objectsData.Select(s => new TreeViewItemData<RCMProfilerFrameUIData>(itemId++, new RCMProfilerFrameUIData { frameData = s, type = 2, totalRefCount = s.refCount })).ToList();
                objectsItemList.Sort(new Sort());
                m_tree.SetRootItems(objectsItemList);
            }
            m_tree.Rebuild();
        }
    }
}
