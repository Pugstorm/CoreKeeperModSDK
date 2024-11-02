using UnityEditor.SceneManagement;

namespace Unity.Editor.Bridge
{
    static class StageNavigationBridge
    {
        public static void NavigateBack()
        {
            StageNavigationManager.instance.NavigateBack(StageNavigationManager.Analytics.ChangeType.NavigateBackViaHierarchyHeaderLeftArrow);
        }
    }
}
