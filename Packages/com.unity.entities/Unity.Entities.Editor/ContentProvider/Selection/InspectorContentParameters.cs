using System;

namespace Unity.Entities.UI
{
    /// <summary>
    /// Custom parameters to use when a <see cref="ContentProvider"/> is displayed in the inspector.
    /// </summary>
    [Serializable]
    public struct InspectorContentParameters
    {
        /// <summary>
        /// Creates a new instance of <see cref="InspectorContentParameters"/> with the default values.
        /// </summary>
        public static InspectorContentParameters Default = new InspectorContentParameters
        {
            ApplyInspectorStyling = true,
            UseDefaultMargins = true
        };

        /// <summary>
        /// Creates a new instance of <see cref="InspectorContentParameters"/> with the default values.
        /// </summary>
        public static InspectorContentParameters NoStyling = new InspectorContentParameters
        {
            ApplyInspectorStyling = false,
            UseDefaultMargins = false
        };

        /// <summary>
        /// When true, the inspector will auto-adjusts the width of all labels.
        /// </summary>
        public bool ApplyInspectorStyling;

        /// <summary>
        /// When true, the inspector content will be offset by 15 pixels.
        /// </summary>
        public bool UseDefaultMargins;
    }
}
