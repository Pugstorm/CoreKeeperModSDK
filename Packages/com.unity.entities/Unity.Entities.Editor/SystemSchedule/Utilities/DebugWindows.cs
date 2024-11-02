using JetBrains.Annotations;
using Unity.Properties;
using Unity.Entities.UI;
using UnityEditor;
using UnityEngine.LowLevel;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class SystemsWindowDebugContentProvider : ContentProvider
    {
        public override string Name => "Systems (Debug)";
        public override object GetContent() => new SystemWindowDebugData();
        protected override ContentStatus GetStatus()
            => !EditorWindow.HasOpenInstances<SystemScheduleWindow>() ? ContentStatus.ReloadContent : ContentStatus.ContentReady;
    }

    class SystemWindowDebugData
    {
        [CreateProperty, UsedImplicitly]
        public int SystemTreeViewItemActiveInstanceCount => SystemTreeViewItem.Pool.ActiveInstanceCount;

        [CreateProperty, UsedImplicitly] public int SystemTreeViewItemPoolSize => SystemTreeViewItem.Pool.PoolSize;

        [CreateProperty, UsedImplicitly]
        public int SystemInformationVisualElementActiveInstanceCount =>
            SystemInformationVisualElement.Pool.ActiveInstanceCount;

        [CreateProperty, UsedImplicitly]
        public int SystemInformationVisualElementPoolSize => SystemInformationVisualElement.Pool.PoolSize;

        [UsedImplicitly]
        class Inspector : PropertyInspector<SystemWindowDebugData>
        {
            SystemScheduleWindow m_SystemScheduleWindow;
            World m_TestWorld;
            PlayerLoopSystem m_PreviousPlayerLoop;

            public override VisualElement Build()
            {
                var root = Resources.Templates.SystemsDebugWindow.Clone();
                Resources.Templates.DebugWindow.AddStyles(root);
                EditorApplication.playModeStateChanged += ResetPreviousPlayerLoop;

                // Add or remove test system.
                var buttonAdd = root.Q<Button>("add-system");
                root.Q<Label>("system-name-label").text =
                    "Add or Remove 'Systems Window Test System'";
                var indicationLabel = root.Q<Label>("indication-label");

                buttonAdd.RegisterCallback<MouseUpEvent>((evt) =>
                {
                    CreateTestSystem();
                    indicationLabel.text = "Test system added.";
                });

                var buttonRemove = root.Q<Button>("remove-system");
                buttonRemove.RegisterCallback<MouseUpEvent>((evt) =>
                {
                    DestroyTestSystem();
                    indicationLabel.text = "Test system removed.";
                });

                // Create or destroy test world.
                m_SystemScheduleWindow = EditorWindow.GetWindow<SystemScheduleWindow>();

                var createWorldButton = root.Q<Button>("create-test-world");
                root.Q<Label>("world-name-label").text = "Create or Destroy 'Systems Window Test World'";
                var worldIndicationLabel = root.Q<Label>("indication-world-label");

                m_PreviousPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();

                createWorldButton.RegisterCallback<MouseUpEvent>((evt) =>
                {
                    CreateTestWorld();
                    worldIndicationLabel.text = "Test world created.";
                });

                var destroyTestWorld = root.Q<Button>("destroy-test-world");
                destroyTestWorld.RegisterCallback<MouseUpEvent>((evt) =>
                {
                    DestroyTestWorld();
                    worldIndicationLabel.text = "Test world destroyed.";
                });

                return root;
            }

            void ResetPreviousPlayerLoop(PlayModeStateChange stateChange)
            {
                m_PreviousPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            }

            void CreateTestSystem()
            {
                var world = World.DefaultGameObjectInjectionWorld;
                var testSystem = world.GetOrCreateSystemManaged<SystemsWindowTestSystem>();

                world.GetOrCreateSystemManaged<SimulationSystemGroup>().AddSystemToUpdateList(testSystem);
                world.GetOrCreateSystemManaged<SimulationSystemGroup>().SortSystems();
            }

            void DestroyTestSystem()
            {
                var world = World.DefaultGameObjectInjectionWorld;
                var testSystem = world.GetExistingSystemManaged<SystemsWindowTestSystem>();
                if (testSystem == null)
                    return;

                world.GetOrCreateSystemManaged<SimulationSystemGroup>().RemoveSystemFromUpdateList(testSystem);
                world.GetOrCreateSystemManaged<SimulationSystemGroup>().SortSystems();
                world.DestroySystemManaged(testSystem);
            }

            void CreateTestWorld()
            {
                if (m_TestWorld != null && m_TestWorld.IsCreated)
                    return;

                m_TestWorld = new World("Systems Window Test World");

                var testSystem = m_TestWorld.GetOrCreateSystemManaged<SystemsWindowTestSystem>();
                m_TestWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().AddSystemToUpdateList(testSystem);
                m_TestWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().SortSystems();

                var playerLoop = PlayerLoop.GetCurrentPlayerLoop();
                ScriptBehaviourUpdateOrder.AppendWorldToPlayerLoop(m_TestWorld, ref playerLoop);
                PlayerLoop.SetPlayerLoop(playerLoop);

                m_SystemScheduleWindow.SelectedWorld = m_TestWorld;
            }

            void DestroyTestWorld()
            {
                if (m_TestWorld != null && m_TestWorld.IsCreated)
                    m_TestWorld.Dispose();

                PlayerLoop.SetPlayerLoop(m_PreviousPlayerLoop);
            }
        }
    }

    [DisableAutoCreation]
    partial class SystemsWindowTestSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.WithoutBurst().WithAll<SceneTag>().ForEach((in SceneTag g) => { }).Run();
        }

        protected override void OnCreate()
        {
        }

        protected override void OnDestroy()
        {
        }
    }
}
