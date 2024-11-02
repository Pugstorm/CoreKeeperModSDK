using UnityEditor;
using UnityEditor.SceneManagement;

namespace Unity.Editor.Bridge
{
    static class PreferencesProviderBridge
    {
        public static PrefabStage.Mode GetDefaultPrefabModeForHierarchy()
        {
            return PreferencesProvider.GetDefaultPrefabModeForHierarchy();
        }
    }
}
