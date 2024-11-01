using System;
using Unity.Scenes.Editor;

namespace Unity.Entities.Editor
{
    static class LiveConversionConfigHelper
    {
        public static bool LiveConversionEnabledInEditMode
        {
            get => LiveConversionEditorSettings.LiveConversionEnabled;
            set => LiveConversionEditorSettings.LiveConversionEnabled = value;
        }

        public static event Action LiveConversionEnabledChanged
        {
            add => LiveConversionEditorSettings.LiveConversionModeChanged += (value);
            remove => LiveConversionEditorSettings.LiveConversionModeChanged -= (value);
        }
    }
}
