using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    partial class SystemScheduleWindowIntegrationTests
    {
        List<string> m_ExpandedNodeNamesToTest =
            new List<string> {
                "Live Conversion Editor System Group",
                "Scene System Group",
                "Simulation System Group",
                "System Schedule Test Group",
                "Presentation System Group"
                };

        [UnityTest]
        public IEnumerator SystemScheduleWindow_TreeViewFoldingState_PlayToEditorMode()
        {
            yield return new EnterPlayMode();
            if (EditorSettings.enterPlayModeOptionsEnabled &&
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0)
            {
                CreateTestSystems(World.DefaultGameObjectInjectionWorld);
            }

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            var systemTreeViewPlayMode = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            foreach(var item in systemTreeViewPlayMode.m_TreeViewRootItems)
            {
                SystemScheduleTestUtilities.ExpandAllGroupNodes(systemTreeViewPlayMode, item);
            }

            // Editor mode
            yield return new ExitPlayMode();
            CreateTestSystems(World.DefaultGameObjectInjectionWorld);

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var systemTreeViewEditorMode = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            var resultList = new List<string>();
            foreach(var item in systemTreeViewPlayMode.m_TreeViewRootItems)
            {
                SystemScheduleTestUtilities.CollectExpandedGroupNodeNames(systemTreeViewEditorMode, item, resultList);
            }
            Assert.That(resultList.Count, Is.GreaterThanOrEqualTo(m_ExpandedNodeNamesToTest.Count));
            Assert.That(m_ExpandedNodeNamesToTest, Is.SubsetOf(resultList));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_TreeViewFoldingState_EditorToPlayMode()
        {
            // Editor mode
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            var systemTreeViewEditorMode = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            foreach(var item in systemTreeViewEditorMode.m_TreeViewRootItems)
            {
                SystemScheduleTestUtilities.ExpandAllGroupNodes(systemTreeViewEditorMode, item);
            }

            // Play mode
            yield return new EnterPlayMode();
            if (EditorSettings.enterPlayModeOptionsEnabled &&
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0)
            {
                CreateTestSystems(World.DefaultGameObjectInjectionWorld);
            }

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var systemTreeViewPlayMode = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            var resultList = new List<string>();

            foreach(var item in systemTreeViewPlayMode.m_TreeViewRootItems)
            {
                SystemScheduleTestUtilities.CollectExpandedGroupNodeNames(systemTreeViewPlayMode, item, resultList);
            }

            Assert.That(resultList.Count, Is.GreaterThanOrEqualTo(m_ExpandedNodeNamesToTest.Count));
            Assert.That(m_ExpandedNodeNamesToTest, Is.SubsetOf(resultList));

            yield return new ExitPlayMode();
        }
    }
}
