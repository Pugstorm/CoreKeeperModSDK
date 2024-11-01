using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine.UIElements;
using Unity.Editor.Bridge;
using static Unity.Entities.EntitiesProfiler;
using static Unity.Entities.StructuralChangesProfiler;

using Unity.Profiling.Editor;

namespace Unity.Entities.Editor
{
    [ProfilerModuleMetadata("Entities Structural Changes", IconPath = "Profiler.CPU")]
    partial class StructuralChangesProfilerModule : ProfilerModule
    {
        class StructuralChangesProfilerViewController : ProfilerModuleViewController
        {
            readonly StructuralChangesProfilerModuleView m_View;
            long m_FrameIndex = -1;

            public bool IsRecording => ProfilerWindowBridge.IsRecording(ProfilerWindow);

            public StructuralChangesProfilerViewController(ProfilerWindow profilerWindow) :
                base(profilerWindow)
            {
                m_View = new StructuralChangesProfilerModuleView();
                m_View.SearchFinished = () => Update();
                ProfilerWindow.SelectedFrameIndexChanged += OnSelectedFrameIndexChanged;
            }

            protected override VisualElement CreateView()
            {
                Analytics.SendEditorEvent(Analytics.Window.Profiler, Analytics.EventType.ProfilerModuleCreate, Analytics.StructuralChangesProfilerModuleName);
                return m_View.Create();
            }

            protected override void Dispose(bool disposing)
            {
                if (!disposing)
                    return;

                ProfilerWindow.SelectedFrameIndexChanged -= OnSelectedFrameIndexChanged;
                base.Dispose(disposing);
            }

            void OnSelectedFrameIndexChanged(long index)
            {
                m_FrameIndex = index;
                if (IsRecording)
                    return;

                m_View.StructuralChangesDataSource = GetFrames(index).SelectMany(GetTreeViewData).ToArray();
                m_View.Search();
            }

            public void Update()
            {
                if (m_FrameIndex == -1 || IsRecording || !m_View.HasStructuralChangesDataSource)
                    m_View.Clear(IsRecording ? s_DisplayingFrameDataDisabled : s_NoFrameDataAvailable);
                else
                    m_View.Update();
            }
        }

        static readonly ProfilerCounterDescriptor[] ProfilerCounters = new[]
        {
            new ProfilerCounterDescriptor(k_CreateEntityCounterName, k_CategoryName),
            new ProfilerCounterDescriptor(k_DestroyEntityCounterName, k_CategoryName),
            new ProfilerCounterDescriptor(k_AddComponentCounterName, k_CategoryName),
            new ProfilerCounterDescriptor(k_RemoveComponentCounterName, k_CategoryName),
            new ProfilerCounterDescriptor(k_SetSharedComponentCounterName, k_CategoryName),
        };

        public StructuralChangesProfilerModule() :
            base(ProfilerCounters, ProfilerModuleChartType.StackedTimeArea, new[] { k_CategoryName })
        {
        }

        public override ProfilerModuleViewController CreateDetailsViewController() => new StructuralChangesProfilerViewController(ProfilerWindow);
    }

    partial class StructuralChangesProfilerModule
    {
        static readonly string s_NoFrameDataAvailable = L10n.Tr("No frame data available. Select a frame from the charts above to see its details here.");
        static readonly string s_DisplayingFrameDataDisabled = L10n.Tr("Displaying of frame data disabled while recording. To see the data, pause recording.");

        static IEnumerable<StructuralChangesProfilerTreeViewItemData> GetTreeViewData(RawFrameDataView frame)
        {
            var worldsData = GetSessionMetaData<WorldData>(frame, EntitiesProfiler.Guid, (int)DataTag.WorldData).Distinct().ToDictionary(x => x.SequenceNumber, x => x);
            var systemsData = GetSessionMetaData<SystemData>(frame, EntitiesProfiler.Guid, (int)DataTag.SystemData).Distinct().ToDictionary(x => x.System, x => x);
            foreach (var structuralChangeData in GetFrameMetaData<StructuralChangeData>(frame, StructuralChangesProfiler.Guid, 0))
            {
                if (worldsData.TryGetValue(structuralChangeData.WorldSequenceNumber, out var worldData))
                {
                    systemsData.TryGetValue(structuralChangeData.ExecutingSystem, out var systemData);
                    yield return new StructuralChangesProfilerTreeViewItemData(worldData, systemData, structuralChangeData);
                }
            }
        }

        static IEnumerable<T> GetSessionMetaData<T>(RawFrameDataView frame, Guid guid, int tag) where T : unmanaged
        {
            var metaDataCount = frame.GetSessionMetaDataCount(guid, tag);
            for (var metaDataIter = 0; metaDataIter < metaDataCount; ++metaDataIter)
            {
                var metaDataArray = frame.GetSessionMetaData<T>(guid, tag, metaDataIter);
                for (var i = 0; i < metaDataArray.Length; ++i)
                    yield return metaDataArray[i];
            }
        }

        static IEnumerable<T> GetFrameMetaData<T>(RawFrameDataView frame, Guid guid, int tag) where T : unmanaged
        {
            var metaDataCount = frame.GetFrameMetaDataCount(guid, tag);
            for (var metaDataIter = 0; metaDataIter < metaDataCount; ++metaDataIter)
            {
                var metaDataArray = frame.GetFrameMetaData<T>(guid, tag, metaDataIter);
                for (var i = 0; i < metaDataArray.Length; ++i)
                    yield return metaDataArray[i];
            }
        }

        static IEnumerable<RawFrameDataView> GetFrames(long index)
        {
            for (var threadIndex = 0; ; ++threadIndex)
            {
                var frame = ProfilerDriver.GetRawFrameDataView((int)index, threadIndex);
                if (!frame.valid)
                    yield break;

                if (frame.GetFrameMetaDataCount(StructuralChangesProfiler.Guid, 0) > 0)
                    yield return frame;
            }
        }
    }
}
