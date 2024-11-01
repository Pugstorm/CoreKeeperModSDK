using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using static Unity.Entities.Editor.MemoryProfilerModule;
using static Unity.Entities.MemoryProfiler;

namespace Unity.Entities.Editor
{
    partial class ArchetypesWindow : DOTSEditorWindow
    {
        static readonly string s_WindowName = L10n.Tr("Archetypes");
        static readonly string s_NoDataAvailable = L10n.Tr("No archetype data available.");

        [MenuItem(Constants.MenuItems.ArchetypesWindow, false, Constants.MenuItems.ArchetypesWindowPriority)]
        static void OpenWindow() => GetWindow<ArchetypesWindow>();

        Stopwatch m_UpdateTimer;
        ArchetypesMemoryDataRecorder m_Recorder;
        NativeList<ulong> m_ArchetypesStableHash;
        NativeList<ArchetypeMemoryData> m_ArchetypesMemoryData;
        MemoryProfilerModuleView m_View;

        public ArchetypesWindow() : base(Analytics.Window.Archetypes)
        {}

        protected override void OnCreate()
        {
            titleContent = new GUIContent(s_WindowName, EditorIcons.Archetype);
            minSize = Constants.MinWindowSize;

            m_Recorder = new ArchetypesMemoryDataRecorder();
            m_ArchetypesStableHash = new NativeList<ulong>(64, Allocator.Persistent);
            m_ArchetypesMemoryData = new NativeList<ArchetypeMemoryData>(64, Allocator.Persistent);
            m_View = new MemoryProfilerModuleView();
            m_View.SearchFinished = () =>
            {
                if (!m_View.HasArchetypesDataSource)
                    m_View.Clear(s_NoDataAvailable);
                else
                    m_View.Rebuild();
            };
            m_UpdateTimer = new Stopwatch();

            var viewElement = m_View.Create();
            m_View.Clear(s_NoDataAvailable);
            rootVisualElement.Add(viewElement);
        }

        protected override void OnCleanup()
        {
            m_ArchetypesMemoryData.Dispose();
            m_ArchetypesStableHash.Dispose();
            m_Recorder.Dispose();
            m_UpdateTimer.Reset();
        }

        protected override void OnUpdate()
        {
            if (m_UpdateTimer.IsRunning && m_UpdateTimer.ElapsedMilliseconds < 500)
                return;

            m_UpdateTimer.Restart();

            m_Recorder.Record();
            if (!MemCmp(m_ArchetypesStableHash.AsArray(), m_Recorder.ArchetypesStableHash) ||
                !MemCmp(m_ArchetypesMemoryData.AsArray(), m_Recorder.ArchetypesMemoryData))
            {
                m_View.ArchetypesDataSource = GetTreeViewData().ToArray();
                m_View.Search();
                m_ArchetypesStableHash.CopyFrom(m_Recorder.ArchetypesStableHash);
                m_ArchetypesMemoryData.CopyFrom(m_Recorder.ArchetypesMemoryData);
            }
        }

        protected override void OnWorldSelected(World world)
        {
        }

        IEnumerable<MemoryProfilerTreeViewItemData> GetTreeViewData()
        {
            var worldsData = m_Recorder.WorldsData.Distinct().ToDictionary(x => x.SequenceNumber, x => x);
            var archetypesData = m_Recorder.ArchetypesData.Distinct().ToDictionary(x => x.StableHash, x => x);
            foreach (var archetypeMemoryData in m_Recorder.ArchetypesMemoryData)
            {
                if (worldsData.TryGetValue(archetypeMemoryData.WorldSequenceNumber, out var worldData) &&
                    archetypesData.TryGetValue(archetypeMemoryData.StableHash, out var archetypeData))
                {
                    yield return new MemoryProfilerTreeViewItemData(worldData.Name, archetypeData, archetypeMemoryData);
                }
            }
        }

        internal static unsafe bool MemCmp<T>(NativeArray<T> lhs, NativeArray<T> rhs)
            where T : unmanaged
        {
            if (lhs.Length != rhs.Length)
                return false;

            return UnsafeUtility.MemCmp(lhs.GetUnsafeReadOnlyPtr(), rhs.GetUnsafeReadOnlyPtr(), sizeof(T) * lhs.Length) == 0;
        }
    }
}
