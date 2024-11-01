using System;
using UnityEditor.Profiling;

namespace Unity.Editor.Bridge
{
    class EditorPerformanceTrackerBridge
    {
        public static BridgedEditorPerformanceTracker CreateEditorPerformanceTracker(string name) => new BridgedEditorPerformanceTracker(name);

        public struct BridgedEditorPerformanceTracker : IDisposable
        {
            EditorPerformanceTracker m_Tracker;

            public BridgedEditorPerformanceTracker(string name)
                => m_Tracker = new EditorPerformanceTracker(name);

            public void Dispose()
                => m_Tracker.Dispose();
        }
    }
}
