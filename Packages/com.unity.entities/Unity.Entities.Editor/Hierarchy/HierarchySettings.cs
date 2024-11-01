using JetBrains.Annotations;
using Unity.Properties;
using Unity.Entities.UI;
using UnityEditor;
using UnityEngine.UIElements;
using System;

namespace Unity.Entities.Editor
{
    [DOTSEditorPreferencesSetting(Constants.Settings.Hierarchy), UsedImplicitly]
    class HierarchySettings : ISetting
    {
        [InternalSetting] public HierarchyConfiguration Configuration = new HierarchyConfiguration();

        public event Action UseAdvanceSearchSettingChanged;

        void ISetting.OnSettingChanged(PropertyPath path)
        {
            if (path.Equals(new PropertyPath($"{nameof(Configuration)}.{nameof(Configuration.AdvancedSearch)}")))
                UseAdvanceSearchSettingChanged?.Invoke();
        }

        string[] ISetting.GetSearchKeywords()
        {
            return ISetting.GetSearchKeywordsFromType(Configuration.GetType());
        }

        [UsedImplicitly]
        class Inspector : PropertyInspector<HierarchySettings>
        {
            VisualElement m_MillisecondsPerUpdate;
            VisualElement m_EntityChangeIntegrationBatchSize;
            VisualElement m_GameObjectChangeIntegrationBatchSize;
            VisualElement m_ExportImmutableBatchSize;

            public override VisualElement Build()
            {
                var root = new VisualElement();

                var updateModeType = new VisualElement();
                var millisecondsBetweenUpdateCycles = new VisualElement();
                var excludeUnnamedNodesForSearch = new VisualElement();
                var useAdvanceSearch = new VisualElement();

                DoDefaultGui(updateModeType, nameof(Configuration) + "." + nameof(HierarchyConfiguration.UpdateMode));
                DoDefaultGui(millisecondsBetweenUpdateCycles, nameof(Configuration) + "." + nameof(HierarchyConfiguration.MinimumMillisecondsBetweenHierarchyUpdateCycles));
                DoDefaultGui(excludeUnnamedNodesForSearch, nameof(Configuration) + "." + nameof(HierarchyConfiguration.ExcludeUnnamedNodesForSearch));
                DoDefaultGui(useAdvanceSearch, nameof(Configuration) + "." + nameof(HierarchyConfiguration.AdvancedSearch));

                root.Add(updateModeType);
                root.Add(millisecondsBetweenUpdateCycles);

                if (Unsupported.IsDeveloperMode())
                {
                    m_MillisecondsPerUpdate = new VisualElement();
                    m_EntityChangeIntegrationBatchSize = new VisualElement();
                    m_GameObjectChangeIntegrationBatchSize = new VisualElement();
                    m_ExportImmutableBatchSize = new VisualElement();

                    // Async specific options.
                    DoDefaultGui(m_MillisecondsPerUpdate, nameof(Configuration) + "." + nameof(HierarchyConfiguration.MaximumMillisecondsPerEditorUpdate));
                    DoDefaultGui(m_EntityChangeIntegrationBatchSize, nameof(Configuration) + "." + nameof(HierarchyConfiguration.EntityChangeIntegrationBatchSize));
                    DoDefaultGui(m_GameObjectChangeIntegrationBatchSize, nameof(Configuration) + "." + nameof(HierarchyConfiguration.GameObjectChangeIntegrationBatchSize));
                    DoDefaultGui(m_ExportImmutableBatchSize, nameof(Configuration) + "." + nameof(HierarchyConfiguration.ExportImmutableBatchSize));

                    updateModeType.Q<EnumField>().RegisterValueChangedCallback(evt =>
                    {
                        SetUpdateMode((Hierarchy.UpdateModeType) evt.newValue);
                    });

                    root.Add(m_MillisecondsPerUpdate);
                    root.Add(m_EntityChangeIntegrationBatchSize);
                    root.Add(m_GameObjectChangeIntegrationBatchSize);
                    root.Add(m_ExportImmutableBatchSize);

                    SetUpdateMode(Target.Configuration.UpdateMode);
                }

                root.Add(excludeUnnamedNodesForSearch);
                root.Add(useAdvanceSearch);

                return root;
            }

            void SetUpdateMode(Hierarchy.UpdateModeType type)
            {
                var isAsync = type == Hierarchy.UpdateModeType.Asynchronous;

                m_MillisecondsPerUpdate.SetVisibility(isAsync);
                m_EntityChangeIntegrationBatchSize.SetVisibility(isAsync);
                m_GameObjectChangeIntegrationBatchSize.SetVisibility(isAsync);
                m_ExportImmutableBatchSize.SetVisibility(isAsync);
            }
        }
    }
}
