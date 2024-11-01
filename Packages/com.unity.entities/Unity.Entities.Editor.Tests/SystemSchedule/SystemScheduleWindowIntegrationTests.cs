using System.Collections;
using NUnit.Framework;
using System.IO;
using System.Linq;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    partial class SystemScheduleWindowIntegrationTests
    {
        World m_DefaultWorld;
        World m_TestWorld;
        ComponentSystemGroup m_TestSystemGroup;
        ComponentSystemBase m_TestSystem1;
        ComponentSystemBase m_TestSystem2;
        SystemScheduleWindow m_SystemScheduleWindow;
        WorldProxy m_WorldProxy;
        Scene m_Scene;
        SubScene m_SubScene;
        GameObject m_SubSceneRoot;
        bool m_PreviousLiveConversionState;
        const string k_SystemScheduleEditorWorld = "Editor World";
        const string k_SystemScheduleTestWorld = "SystemScheduleTestWorld";
        const string k_AssetsFolderRoot = "Assets";
        const string k_SceneExtension = "unity";
        const string k_SceneName = "SystemsWindowTests";
        const string k_SubSceneName = "SubScene";
        [SerializeField]
        string m_TestAssetsDirectory;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            if (!EditorApplication.isPlaying)
            {
                m_PreviousLiveConversionState = LiveConversionEditorSettings.LiveConversionEnabled;
                LiveConversionEditorSettings.LiveConversionEnabled = true;

                var guid = AssetDatabase.CreateFolder(k_AssetsFolderRoot, nameof(SystemScheduleWindowIntegrationTests));
                m_TestAssetsDirectory = AssetDatabase.GUIDToAssetPath(guid);

                m_Scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                var mainScenePath = Path.Combine(m_TestAssetsDirectory, $"{k_SceneName}.{k_SceneExtension}");

                EditorSceneManager.SaveScene(m_Scene, mainScenePath);
                SceneManager.SetActiveScene(m_Scene);

                // Temp context GameObject, necessary to create an empty subscene
                var targetGO = new GameObject(k_SubSceneName);

                var subsceneArgs = new SubSceneContextMenu.NewSubSceneArgs(targetGO, m_Scene, SubSceneContextMenu.NewSubSceneMode.EmptyScene);
                m_SubScene = SubSceneContextMenu.CreateNewSubScene(targetGO.name, subsceneArgs, InteractionMode.AutomatedAction);
                m_SubSceneRoot = m_SubScene.gameObject;

                UnityEngine.Object.DestroyImmediate(targetGO);
                EditorSceneManager.SaveScene(m_Scene);
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            UnityEngine.Object.DestroyImmediate(m_SubSceneRoot);
            AssetDatabase.DeleteAsset(m_TestAssetsDirectory);
            SceneWithBuildConfigurationGUIDs.ClearBuildSettingsCache();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

            LiveConversionEditorSettings.LiveConversionEnabled = m_PreviousLiveConversionState;
        }

        [SetUp]
        public void SetUp()
        {
            m_DefaultWorld = World.DefaultGameObjectInjectionWorld;
            CreateTestSystems(m_DefaultWorld);
            m_SystemScheduleWindow = !EditorApplication.isPlaying ? SystemScheduleTestUtilities.CreateSystemsWindow() : EditorWindow.GetWindow<SystemScheduleWindow>();
            m_SystemScheduleWindow.SelectedWorld = m_DefaultWorld;
            m_WorldProxy = m_SystemScheduleWindow.WorldProxyManager.GetWorldProxyForGivenWorld(m_DefaultWorld);
        }

        [TearDown]
        public void TearDown()
        {
            m_DefaultWorld = World.DefaultGameObjectInjectionWorld;

            m_TestSystemGroup = m_DefaultWorld.GetOrCreateSystemManaged<SystemScheduleTestGroup>();
            m_TestSystem1 = m_DefaultWorld.GetOrCreateSystemManaged<SystemScheduleTestSystem1>();
            m_TestSystem2 = m_DefaultWorld.GetOrCreateSystemManaged<SystemScheduleTestSystem2>();

            if (m_TestSystemGroup != null)
            {
                m_DefaultWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().RemoveSystemFromUpdateList(m_TestSystemGroup);
                m_DefaultWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().SortSystems();
                m_DefaultWorld.DestroySystemManaged(m_TestSystemGroup);
            }

            if (m_TestSystem1 != null)
                m_DefaultWorld.DestroySystemManaged(m_TestSystem1);

            if (m_TestSystem2 != null)
                m_DefaultWorld.DestroySystemManaged(m_TestSystem2);

            if (!EditorApplication.isPlaying)
                SystemScheduleTestUtilities.DestroySystemsWindow(m_SystemScheduleWindow);

            if (EditorWindow.HasOpenInstances<SystemScheduleWindow>())
                EditorWindow.GetWindow<SystemScheduleWindow>().Close();

            if (m_TestWorld is { IsCreated: true })
                m_TestWorld.Dispose();
        }

        void CreateTestSystems(World world)
        {
            m_TestSystemGroup = world.GetOrCreateSystemManaged<SystemScheduleTestGroup>();
            m_TestSystem1 = world.GetOrCreateSystemManaged<SystemScheduleTestSystem1>();
            m_TestSystem2 = world.GetOrCreateSystemManaged<SystemScheduleTestSystem2>();
            m_TestSystemGroup.AddSystemToUpdateList(m_TestSystem1);
            m_TestSystemGroup.AddSystemToUpdateList(m_TestSystem2);
            m_TestSystemGroup.SortSystems();
            world.GetOrCreateSystemManaged<SimulationSystemGroup>().AddSystemToUpdateList(m_TestSystemGroup);
            world.GetOrCreateSystemManaged<SimulationSystemGroup>().SortSystems();

            if (m_SystemScheduleWindow != null)
                m_SystemScheduleWindow.SelectedWorld = world;
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SearchForSingleComponent()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            m_SystemScheduleWindow.rootVisualElement.Q<SearchElement>().Search("c=SystemScheduleTestData1");

            var systemTreeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            Assert.That(systemTreeView.m_ListViewFilteredItems.Count, Is.EqualTo(1));
            Assert.That(systemTreeView.m_ListViewFilteredItems.FirstOrDefault()?.Node.Name, Is.EqualTo("System Schedule Test System 1"));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SearchForSystemName()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            m_SystemScheduleWindow.rootVisualElement.Q<SearchElement>().Search("SystemScheduleTestSystem1");

            var systemTreeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            Assert.That(systemTreeView.m_ListViewFilteredItems.Count, Is.EqualTo(1));
            Assert.That(systemTreeView.m_ListViewFilteredItems.FirstOrDefault()?.Node.Name, Is.EqualTo("System Schedule Test System 1"));

            var result = m_SystemScheduleWindow.WorldProxyManager.GetWorldProxyForGivenWorld(m_DefaultWorld);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.AllSystems.Any(s => s.NicifiedDisplayName.Equals("System Schedule Test System 1")), Is.True);
            Assert.That(result.AllSystems.Any(s => s.NicifiedDisplayName.Equals("System Schedule Test System 2")), Is.True);
        }

        [Test]
        public void SystemScheduleWindow_VerifyScheduledSystemData()
        {
            var wsd = new ScheduledSystemData(m_TestSystem1, -1);
            Assert.That(wsd.Category, Is.EqualTo(SystemCategory.SystemBase));
            Assert.That(wsd.NicifiedDisplayName, Is.EqualTo("System Schedule Test System 1"));
            Assert.That(wsd.Managed, Is.EqualTo(m_TestSystem1));
            Assert.That(wsd.ChildCount, Is.EqualTo(0));
            Assert.That(wsd.ParentIndex, Is.EqualTo(-1));

            var wsdUnmanaged = new ScheduledSystemData(m_DefaultWorld.GetOrCreateSystem<SystemScheduleTestUnmanagedSystem>(), m_DefaultWorld, -1);
            Assert.That(wsdUnmanaged.Category, Is.EqualTo(SystemCategory.Unmanaged));
        }

        [Test]
        public void SystemScheduleWindow_SearchForNonExistingSystem()
        {
            m_SystemScheduleWindow.rootVisualElement.Q<SearchElement>().Search("raasdfasd");
            Assert.That(m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().m_ListViewFilteredItems.Count, Is.EqualTo(0));
        }

        [Test]
        public void SystemScheduleWindow_GetSelectedWorld()
        {
            Assert.That(m_SystemScheduleWindow.SelectedWorld.Name, Is.EqualTo(k_SystemScheduleEditorWorld));
        }

        [Test]
        public void SystemScheduleWindow_WorldVersionChange()
        {
            var previousWorldVersion = m_DefaultWorld.Version;
            m_DefaultWorld.DestroySystemManaged(m_TestSystem1);
            Assert.That(m_DefaultWorld.Version, Is.Not.EqualTo(previousWorldVersion));

            previousWorldVersion = m_DefaultWorld.Version;
            m_DefaultWorld.GetOrCreateSystemManaged<SystemScheduleTestSystem1>();
            Assert.That(m_DefaultWorld.Version, Is.Not.EqualTo(previousWorldVersion));
        }

        [Test]
        public void SystemScheduleWindow_SearchBuilder_ParseSearchString()
        {
            var searchElement = m_SystemScheduleWindow.rootVisualElement.Q<SearchElement>();
            searchElement.Search("c=Com1 C=Com2 randomName Sd:System1");
            var systemTreeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();
            var parseResult = systemTreeView.SearchFilter;

            Assert.That(parseResult.Input, Is.EqualTo( "c=Com1 C=Com2 randomName Sd:System1" ));
            Assert.That(parseResult.ComponentNames, Is.EquivalentTo(new[] { "Com1", "Com2" }));
            Assert.That(parseResult.DependencySystemNames, Is.EquivalentTo(new[] { "System1" }));
            Assert.That(parseResult.ErrorComponentType, Is.EqualTo( "Com1" ));

            searchElement.Search("c=   com1 C=Com2");
            Assert.That(systemTreeView.SearchFilter.ComponentNames, Is.EquivalentTo(new[] { string.Empty, "Com2"}));
        }

        [Test]
        public void SystemScheduleWindow_SearchBuilder_ParseSearchString_EmptyString()
        {
            m_SystemScheduleWindow.rootVisualElement.Q<SearchElement>().Search(string.Empty);
            var parseResult =  m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().SearchFilter;

            Assert.That(parseResult.Input, Is.EqualTo(string.Empty));
            Assert.That(parseResult.ComponentNames, Is.Empty);
            Assert.That(parseResult.DependencySystemNames, Is.Empty);
            Assert.That(parseResult.ErrorComponentType, Is.EqualTo(string.Empty));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_ContainsThisComponentType()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            var componentTypesInQuery1 = EntityQueryUtility.CollectComponentTypesFromSystemQuery(new SystemProxy(m_TestSystem1, m_WorldProxy));
            var typesInQuery1 = componentTypesInQuery1 as string[] ?? componentTypesInQuery1.ToArray();
            Assert.That(typesInQuery1.Contains(nameof(SystemScheduleTestData1)));
            Assert.That(typesInQuery1.Contains(nameof(SystemScheduleTestData2)));

            var componentTypesInQuery2 = EntityQueryUtility.CollectComponentTypesFromSystemQuery(new SystemProxy(m_TestSystem2, m_WorldProxy));
            var typesInQuery2 = componentTypesInQuery2 as string[] ?? componentTypesInQuery2.ToArray();
            Assert.That(!typesInQuery2.Contains(nameof(SystemScheduleTestData1)));
            Assert.That(!typesInQuery2.Contains(nameof(SystemScheduleTestData2)));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_CustomWorld()
        {
            var previousPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();

            m_TestWorld = new World(k_SystemScheduleTestWorld);
            CreateTestSystems(m_TestWorld);

            var playerLoop = PlayerLoop.GetDefaultPlayerLoop();
            ScriptBehaviourUpdateOrder.AppendWorldToPlayerLoop(m_TestWorld, ref playerLoop);
            PlayerLoop.SetPlayerLoop(playerLoop);

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            Assert.That(m_SystemScheduleWindow.SelectedWorld.Name, Is.EqualTo(k_SystemScheduleTestWorld));
            Assert.That(m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out _), Is.True);

            m_TestWorld.Dispose();
            PlayerLoop.SetPlayerLoop(previousPlayerLoop);

            m_SystemScheduleWindow.Update();
            Assert.That(m_SystemScheduleWindow.SelectedWorld.Name, Is.EqualTo(k_SystemScheduleEditorWorld));

            if (m_TestWorld is { IsCreated: true })
                m_TestWorld.Dispose();
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_HashCodeForTwoSystemsWithSameType()
        {
            var previousPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            m_SystemScheduleWindow.m_Configuration.ShowFullPlayerLoop = false;

            // Create test system in default world.
            var defaultWorld = World.DefaultGameObjectInjectionWorld;
            CreateTestSystems(defaultWorld);

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            Assert.That(m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out var testSystemItemDefaultWorld), Is.True);
            var testSystemInDefaultWorldHash = testSystemItemDefaultWorld.Node.Hash;

            // Create test system in test world.
            m_TestWorld = new World(k_SystemScheduleTestWorld);
            CreateTestSystems(m_TestWorld);

            var playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            ScriptBehaviourUpdateOrder.AppendWorldToPlayerLoop(m_TestWorld, ref playerLoop);
            PlayerLoop.SetPlayerLoop(playerLoop);

            // Wait a frame for the window to update.
            yield return null;

            m_SystemScheduleWindow.RebuildTreeView();

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            Assert.That(m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out var testSystemItemTestWorld), Is.True);

            var testSystemInTestWorldHash = testSystemItemTestWorld.Node.Hash;

            if (m_TestWorld.IsCreated)
                m_TestWorld.Dispose();
            PlayerLoop.SetPlayerLoop(previousPlayerLoop);

            Assert.That(testSystemInDefaultWorldHash, Is.Not.EqualTo(testSystemInTestWorldHash));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_ScheduleSystemInDifferentWorld()
        {
            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));

            var oldSystemCount = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().m_SystemTreeView.items.Count();

            m_TestWorld = new World(k_SystemScheduleTestWorld);
            var managedSystem = m_TestWorld.GetOrCreateSystemManaged<SystemScheduleTestSystem>();

            var simulationSystemGroup = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
            simulationSystemGroup.AddSystemToUpdateList(managedSystem);
            simulationSystemGroup.SortSystems();

            yield return null;
            yield return null;

            var newSystemCount = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().m_SystemTreeView.items.Count();

            Assert.That(oldSystemCount, Is.EqualTo(newSystemCount));

            if (managedSystem != null)
                World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().RemoveSystemFromUpdateList(managedSystem);

            if (m_TestWorld.IsCreated)
                m_TestWorld.Dispose();
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SystemToggleState_AllEnabled()
        {
            m_TestSystemGroup.Enabled = true;
            m_TestSystem1.Enabled = true;
            m_TestSystem2.Enabled = true;

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out var testGroupItem);
            Assert.That(testGroupItem.GetSystemToggleState(), Is.EqualTo(SystemTreeViewItem.SystemToggleState.AllEnabled));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SystemToggleState_MixedStateWithOneChildDisabled()
        {
            m_TestSystemGroup.Enabled = true;
            m_TestSystem1.Enabled = false;
            m_TestSystem2.Enabled = true;

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out var testGroupItem);
            Assert.That(testGroupItem.GetSystemToggleState(), Is.EqualTo(SystemTreeViewItem.SystemToggleState.Mixed));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SystemToggleState_MixedStateWithAllChildrenDisabled()
        {
            m_TestSystemGroup.Enabled = true;
            m_TestSystem1.Enabled = false;
            m_TestSystem2.Enabled = false;

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out var testGroupItem);
            Assert.That(testGroupItem.GetSystemToggleState(), Is.EqualTo(SystemTreeViewItem.SystemToggleState.Mixed));
        }

        [UnityTest]
        public IEnumerator SystemScheduleWindow_SystemToggleState_ParentDisabled()
        {
            m_TestSystemGroup.Enabled = false;
            m_TestSystem1.Enabled = true;
            m_TestSystem2.Enabled = true;

            yield return new SystemScheduleTestUtilities.UpdateSystemGraph(typeof(SystemScheduleTestGroup));
            m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>().CheckIfTreeViewContainsGivenSystemType(typeof(SystemScheduleTestGroup), out var testGroupItem);
            Assert.That(testGroupItem.GetSystemToggleState(), Is.EqualTo(SystemTreeViewItem.SystemToggleState.Disabled));
        }
    }
}
