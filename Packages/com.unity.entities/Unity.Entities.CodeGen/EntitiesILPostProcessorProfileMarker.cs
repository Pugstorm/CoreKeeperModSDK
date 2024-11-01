using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Unity.Entities.CodeGen
{
    class EntitiesILPostProcessorProfileMarker : IDisposable
    {
        internal static bool s_ToTestLog = false;
        internal static bool s_OmitZeroMSTimings = true;
        internal static readonly List<string> s_TestLog = new List<string>();
        readonly Stopwatch m_Stopwatch;
        readonly string m_AssemblyName;
        readonly string m_SectionName;
        readonly List<EntitiesILPostProcessorProfileMarker> m_ChildMarkers = new List<EntitiesILPostProcessorProfileMarker>();
        bool m_IsChild;

        public EntitiesILPostProcessorProfileMarker(string assemblyName, string sectionName = "")
        {
            m_AssemblyName = assemblyName;
            m_SectionName = sectionName;
            m_Stopwatch = Stopwatch.StartNew();
        }

        float GetTotalChildTime()
        {
            return m_ChildMarkers.Sum(child => child.m_Stopwatch.ElapsedMilliseconds + child.GetTotalChildTime());
        }

        static void LogText(string text)
        {
            if (s_ToTestLog)
                s_TestLog.Add(text);
            else
                Console.WriteLine(text);
        }

        void DebugOutput()
        {
            if (s_OmitZeroMSTimings && m_Stopwatch.ElapsedMilliseconds == 0)
                return;
            var discrepancyText = !m_IsChild ? $"(~{m_Stopwatch.ElapsedMilliseconds - GetTotalChildTime()}ms)" : "";
            var logText = $"  - EILPP : {m_AssemblyName} : {m_SectionName}: {m_Stopwatch.ElapsedMilliseconds}ms {discrepancyText}";
            if (m_IsChild)
                logText = "  " + logText;
            LogText(logText);
        }

        public void Dispose()
        {
            m_Stopwatch.Stop();

            if (!m_IsChild)
            {
                DebugOutput();

                foreach (var child in m_ChildMarkers)
                    child.DebugOutput();
                m_ChildMarkers.Clear();
            }
        }

        public EntitiesILPostProcessorProfileMarker CreateChildMarker(string sectionName)
        {
            var newMarker = new EntitiesILPostProcessorProfileMarker(m_AssemblyName, sectionName);
            newMarker.m_IsChild = true;
            m_ChildMarkers.Add(newMarker);
            return newMarker;
        }
    }
}
