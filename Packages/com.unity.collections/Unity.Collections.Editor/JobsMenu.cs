using System;
using UnityEditor;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;

// This menu is available from the editor directly on later versions
#if !UNITY_2022_2_14F1_OR_NEWER
class JobsMenu
{
    private static int savedJobWorkerCount = JobsUtility.JobWorkerCount;

    [SettingsProvider]
    private static SettingsProvider JobsPreferencesItem()
    {
        var provider = new SettingsProvider("Preferences/Jobs", SettingsScope.User)
        {
            label = "Jobs",
            keywords = new[]{"Jobs"},
            guiHandler = (searchContext) =>
            {
                var originalWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 200f;
                EditorGUILayout.BeginVertical();

                GUILayout.BeginVertical();
                EditorGUILayout.LabelField("For safety, these values are reset on editor restart.");

                bool madeChange = false;

                bool oldWorkerCount = (JobsUtility.JobWorkerCount > 0);
                bool newWorkerCount = EditorGUILayout.Toggle(new GUIContent("Use Job Threads:"), oldWorkerCount);
                if (newWorkerCount != oldWorkerCount)
                {
                    madeChange = true;
                    SwitchUseJobThreads();
                }

                bool oldUseJobsDebugger = JobsUtility.JobDebuggerEnabled;
                var newUseJobsDebugger = EditorGUILayout.Toggle(new GUIContent("Enable Jobs Debugger"), JobsUtility.JobDebuggerEnabled);
                if (newUseJobsDebugger != oldUseJobsDebugger)
                {
                    madeChange = true;
                    SetUseJobsDebugger(newUseJobsDebugger);
                }

                var oldLeakDetectionMode = NativeLeakDetection.Mode;
                var newLeakDetectionMode = (NativeLeakDetectionMode) EditorGUILayout.EnumPopup(new GUIContent("Leak Detection Level"), oldLeakDetectionMode);
                if (newLeakDetectionMode != oldLeakDetectionMode)
                {
                    madeChange = true;
                    SetLeakDetection(newLeakDetectionMode);
                }

                if (madeChange)
                    Telemetry.LogMenuPreferences(new Telemetry.MenuPreferencesEvent { useJobsThreads = newUseJobsDebugger, enableJobsDebugger = newUseJobsDebugger, nativeLeakDetectionMode = newLeakDetectionMode });

                GUILayout.EndVertical();
                EditorGUILayout.EndVertical();

                EditorGUIUtility.labelWidth = originalWidth;
            }

        };

        return provider;
    }

    static void SwitchUseJobThreads()
    {
        if (JobsUtility.JobWorkerCount > 0)
        {
            savedJobWorkerCount = JobsUtility.JobWorkerCount;
            JobsUtility.JobWorkerCount = 0;
        }
        else
        {
            JobsUtility.JobWorkerCount = savedJobWorkerCount;
            if (savedJobWorkerCount == 0)
            {
                JobsUtility.ResetJobWorkerCount();
            }
        }
    }

    static void SetUseJobsDebugger(bool value)
    {
        JobsUtility.JobDebuggerEnabled = value;
    }

    static void SetLeakDetection(NativeLeakDetectionMode value)
    {
        switch(value)
        {
            case NativeLeakDetectionMode.Disabled:
            {
                // In the case where someone enables, disables, then re-enables leak checking, we might miss some frees
                // while disabled. So to avoid spurious leak warnings, just forgive the leaks every time someone disables
                // leak checking through the menu.
                UnsafeUtility.ForgiveLeaks();
                Debug.LogWarning("Leak detection has been disabled. Leak warnings will not be generated, and all leaks up to now are forgotten.");
                break;
            }
            case NativeLeakDetectionMode.Enabled:
            {
                Debug.Log("Leak detection has been enabled. Leak warnings will be generated upon exiting play mode.");
                break;
            }
            case NativeLeakDetectionMode.EnabledWithStackTrace:
            {
                Debug.Log("Leak detection with stack traces has been enabled. Leak warnings will be generated upon exiting play mode.");
                break;
            }
            default:
            {
                throw new Exception($"Unhandled {nameof(NativeLeakDetectionMode)}");
            }
        }

        NativeLeakDetection.Mode = value;
    }
}
#endif
