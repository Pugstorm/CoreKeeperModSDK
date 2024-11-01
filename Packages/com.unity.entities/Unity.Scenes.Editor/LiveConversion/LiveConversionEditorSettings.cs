using System;
using UnityEditor;

namespace Unity.Scenes.Editor
{
    /// <summary>
    /// Settings controlling the live conversion.
    /// </summary>
    public static class LiveConversionEditorSettings
    {
        /// <summary>
        /// The current live conversion mode.
        /// </summary>
        public static LiveConversionMode LiveConversionMode
        {
            get
            {
                if (EditorApplication.isPlaying || LiveConversionEnabled)
                    return LiveConversionSceneViewShowRuntime ? LiveConversionMode.SceneViewShowsRuntime : LiveConversionMode.SceneViewShowsAuthoring;
                else
                    return LiveConversionMode.Disabled;
            }
        }

        /// <summary>
        /// Whether or not the live conversion is enabled in the editor preferences.
        /// </summary>
        public static bool LiveConversionEnabled
        {
            // Keep this a session state, so nobody is accidentally leaving this off while hidden behind internal settings
            get => SessionState.GetBool("Unity.Entities.Streaming.SubScene.LiveBakingEnabled", true);
            set
            {
                if (LiveConversionEnabled == value)
                    return;

                SessionState.SetBool("Unity.Entities.Streaming.SubScene.LiveBakingEnabled", value);
                LiveConversionConnection.GlobalDirtyLiveConversion();
                LiveConversionModeChanged();
            }
        }

        /// <summary>
        /// If true, the scene view displays the conversion result. Otherwise, the scene view displays the authoring state.
        /// </summary>
        internal static bool LiveConversionSceneViewShowRuntime
        {
            get => EditorPrefs.GetBool("Unity.Entities.Streaming.SubScene.LiveConversionSceneViewShowRuntime", false);
            set
            {
                if (LiveConversionSceneViewShowRuntime == value)
                    return;

                EditorPrefs.SetBool("Unity.Entities.Streaming.SubScene.LiveConversionSceneViewShowRuntime", value);
                LiveConversionConnection.GlobalDirtyLiveConversion();
                LiveConversionModeChanged();
            }
        }

        internal static event Action LiveConversionModeChanged = delegate {};
    }
}
