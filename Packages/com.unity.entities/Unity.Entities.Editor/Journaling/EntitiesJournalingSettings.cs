using Unity.Properties;
using Unity.Serialization;

namespace Unity.Entities.Editor
{
    [DOTSEditorPreferencesSetting(Constants.Settings.Journaling)]
    class EntitiesJournalingSettings : ISetting
    {
        [CreateProperty, DontSerialize]
        public bool Enabled
        {
#if !DISABLE_ENTITIES_JOURNALING
            get => EntitiesJournaling.Preferences.Enabled;
            set
            {
                EntitiesJournaling.Preferences.Enabled = value;
                EntitiesJournaling.Enabled = value;
            }
#else
            get => false;
            set { }
#endif
        }

        [CreateProperty, DontSerialize]
        public int TotalMemoryMB
        {
#if !DISABLE_ENTITIES_JOURNALING
            get => EntitiesJournaling.Preferences.TotalMemoryMB;
            set => EntitiesJournaling.Preferences.TotalMemoryMB = value;
#else
            get => 0;
            set { }
#endif
        }

        [CreateProperty, DontSerialize]
        public bool PostProcess
        {
#if !DISABLE_ENTITIES_JOURNALING
            get => EntitiesJournaling.Preferences.PostProcess;
            set => EntitiesJournaling.Preferences.PostProcess = value;
#else
            get => false;
            set { }
#endif
        }

        public void OnSettingChanged(PropertyPath path)
        {
        }

        public string[] GetSearchKeywords()
        {
            return ISetting.GetSearchKeywordsFromType(GetType());
        }
    }


#if DISABLE_ENTITIES_JOURNALING
    class EntitiesJournalingSettingsInspector : Unity.Entities.UI.Inspector<EntitiesJournalingSettings>
    {
        public override UnityEngine.UIElements.VisualElement Build()
        {
            var root = base.Build();
            root.SetEnabled(false);
            return root;
        }
    }
#endif
}
