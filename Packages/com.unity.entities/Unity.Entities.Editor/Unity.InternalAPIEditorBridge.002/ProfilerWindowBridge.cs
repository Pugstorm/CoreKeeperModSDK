using UnityEditor;
using UnityEditorInternal;

namespace Unity.Editor.Bridge
{
    class ProfilerWindowBridge
    {
        public static bool IsRecording(ProfilerWindow profilerWindow)
        {
            if (!profilerWindow.IsSetToRecord())
                return false;

            if (ProfilerDriver.IsConnectionEditor())
                return (EditorApplication.isPlaying && !EditorApplication.isPaused) || ProfilerDriver.profileEditor;

            return true;
        }
    }
}
