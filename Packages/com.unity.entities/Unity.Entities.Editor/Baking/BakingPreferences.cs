using Unity.Entities.Conversion;
using Unity.Entities.UI;
using Unity.Properties;
using Unity.Scenes.Editor;
using Unity.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// Editor preferences for baking.
    /// </summary>
    [DOTSEditorPreferencesSetting(Constants.Settings.Baking)]
    class BakingPreferences : ISetting
    {
        public enum SceneViewPresentationMode
        {
            /// <summary>
            /// Display the authoring data in the scene view.
            /// </summary>
            [InspectorName("Authoring Data")]
            [Tooltip("The Scene View window shows the authoring data.")]
            SceneViewShowsAuthoring,
            /// <summary>
            /// Display the result of the conversion in the scene view.
            /// </summary>
            [InspectorName("Runtime Data")]
            [Tooltip("The Scene View window shows the baked runtime data.")]
            SceneViewShowsRuntime,
        }

        [InternalSetting]
        [CreateProperty, DontSerialize]
        public bool LiveBaking
        {
            get => LiveConversionEditorSettings.LiveConversionEnabled;
            set => LiveConversionEditorSettings.LiveConversionEnabled = value;
        }

        [CreateProperty, DontSerialize]
        public SceneViewPresentationMode SceneViewMode
        {
            get => LiveConversionEditorSettings.LiveConversionSceneViewShowRuntime ? SceneViewPresentationMode.SceneViewShowsRuntime : SceneViewPresentationMode.SceneViewShowsAuthoring;
            set => LiveConversionEditorSettings.LiveConversionSceneViewShowRuntime = value == SceneViewPresentationMode.SceneViewShowsRuntime;
        }

        [CreateProperty, DontSerialize]
        public bool LiveBakingLogging
        {
            get => LiveConversionSettings.IsLiveBakingLoggingEnabled;
            set => LiveConversionSettings.IsLiveBakingLoggingEnabled = value;
        }

        public void OnSettingChanged(PropertyPath path)
        {

        }

        public string[] GetSearchKeywords()
        {
            return ISetting.GetSearchKeywordsFromType(GetType());
        }

        class Inspector : PropertyInspector<BakingPreferences>
        {
            public override VisualElement Build()
            {
                var root = new VisualElement();
                var sceneViewMode = new VisualElement();
                var liveBakingLogging = new VisualElement();

                DoDefaultGui(sceneViewMode, nameof(SceneViewMode));
                DoDefaultGui(liveBakingLogging, nameof(LiveBakingLogging));

                root.Add(sceneViewMode);
                root.Add(liveBakingLogging);

                var clearEntitiesCache = new Button(ClearEntitiesCacheWindow.OpenWindow)
                {
                    text = "Clear Entity Cache",
                    style =
                    {
                        alignSelf = Align.FlexStart
                    }
                };

                root.Add(clearEntitiesCache);
                return root;
            }
        }
    }
}
