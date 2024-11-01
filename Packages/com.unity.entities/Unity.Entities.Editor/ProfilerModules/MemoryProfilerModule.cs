using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine.UIElements;
using Unity.Editor.Bridge;
using static Unity.Entities.EntitiesProfiler;
using static Unity.Entities.MemoryProfiler;

using Unity.Profiling.Editor;

namespace Unity.Entities.Editor
{
    [ProfilerModuleMetadata("Entities Memory", IconPath = "Profiler.Memory")]
    partial class MemoryProfilerModule : ProfilerModule
    {
        class MemoryProfilerViewController : ProfilerModuleViewController
        {
            readonly MemoryProfilerModuleView m_View;
            long m_FrameIndex = -1;

            public bool IsRecording => ProfilerWindowBridge.IsRecording(ProfilerWindow);

            public MemoryProfilerViewController(ProfilerWindow profilerWindow) :
                base(profilerWindow)
            {
                m_View = new MemoryProfilerModuleView();
                m_View.SearchFinished = () => Update();
                ProfilerWindow.SelectedFrameIndexChanged += OnSelectedFrameIndexChanged;
            }

            protected override VisualElement CreateView()
            {
                Analytics.SendEditorEvent(Analytics.Window.Profiler, Analytics.EventType.ProfilerModuleCreate, Analytics.MemoryProfilerModuleName);
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

                m_View.ArchetypesDataSource = GetFrames(index).SelectMany(GetTreeViewData).ToArray();
                m_View.Search();
            }

            public void Update()
            {
                if (m_FrameIndex == -1 || IsRecording || !m_View.HasArchetypesDataSource)
                    m_View.Clear(IsRecording ? s_DisplayingFrameDataDisabled : s_NoFrameDataAvailable);
                else
                    m_View.Rebuild();
            }
        }

        static ProfilerCounterDescriptor[] ProfilerCounters = new[]
        {
            new ProfilerCounterDescriptor(k_AllocatedMemoryCounterName, k_CategoryName),
            new ProfilerCounterDescriptor(k_UnusedMemoryCounterName, k_CategoryName)
        };

        public MemoryProfilerModule() :
            base(ProfilerCounters, ProfilerModuleChartType.Line, new[] { k_CategoryName })
        {
        }

        public override ProfilerModuleViewController CreateDetailsViewController() => new MemoryProfilerViewController(ProfilerWindow);
    }

    partial class MemoryProfilerModule
    {
        static readonly string s_NoFrameDataAvailable = L10n.Tr("No frame data available. Select a frame from the charts above to see its details here.");
        static readonly string s_DisplayingFrameDataDisabled = L10n.Tr("Displaying of frame data disabled while recording. To see the data, pause recording.");

        static IEnumerable<MemoryProfilerTreeViewItemData> GetTreeViewData(RawFrameDataView frame)
        {
            var worldsData = GetSessionMetaData<WorldData>(frame, EntitiesProfiler.Guid, (int)DataTag.WorldData).Distinct().ToDictionary(x => x.SequenceNumber, x => x);
            var archetypesData = GetSessionMetaData<ArchetypeData>(frame, EntitiesProfiler.Guid, (int)DataTag.ArchetypeData).Distinct().ToDictionary(x => x.StableHash, x => x);
            foreach (var archetypeMemoryData in GetFrameMetaData<ArchetypeMemoryData>(frame, MemoryProfiler.Guid, 0))
            {
                if (worldsData.TryGetValue(archetypeMemoryData.WorldSequenceNumber, out var worldData) &&
                    archetypesData.TryGetValue(archetypeMemoryData.StableHash, out var archetypeData))
                {
                    yield return new MemoryProfilerTreeViewItemData(worldData.Name, archetypeData, archetypeMemoryData);
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

                if (frame.GetFrameMetaDataCount(MemoryProfiler.Guid, 0) > 0)
                    yield return frame;
            }
        }
    }
}
