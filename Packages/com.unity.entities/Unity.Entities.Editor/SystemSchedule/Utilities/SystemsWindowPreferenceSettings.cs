using JetBrains.Annotations;
using Unity.Properties;
using Unity.Entities.UI;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    [DOTSEditorPreferencesSetting(Constants.Settings.SystemsWindow), UsedImplicitly]
    class SystemsWindowPreferenceSettings : ISetting
    {
        [InternalSetting] public SystemScheduleWindow.SystemsWindowConfiguration Configuration = new SystemScheduleWindow.SystemsWindowConfiguration();

        void ISetting.OnSettingChanged(PropertyPath path)
        {
        }

        string[] ISetting.GetSearchKeywords()
        {
            return ISetting.GetSearchKeywordsFromType(Configuration.GetType());
        }

        [UsedImplicitly]
        class Inspector : PropertyInspector<SystemsWindowPreferenceSettings>
        {
            public override VisualElement Build()
            {
                var root = new VisualElement();

                var showZeroValues = new VisualElement();
                var showMorePrecision = new VisualElement();

                DoDefaultGui(showZeroValues, nameof(Configuration) + "." + nameof(SystemScheduleWindow.SystemsWindowConfiguration.Show0sInEntityCountAndTimeColumn));
                DoDefaultGui(showMorePrecision, nameof(Configuration) + "." + nameof(SystemScheduleWindow.SystemsWindowConfiguration.ShowMorePrecisionForRunningTime));

                root.Add(showZeroValues);
                root.Add(showMorePrecision);

                return root;
            }
        }
    }
}
