using System;
using UnityEngine;

namespace Unity.Entities.UI
{
    /// <summary>
    /// Custom parameters to use when a <see cref="ContentProvider"/> is displayed in a window.
    /// </summary>
    [Serializable]
    public struct ContentWindowParameters
    {
        static readonly Vector2 k_MinSize = new Vector2(275, 200);

        /// <summary>
        /// Creates a new instance of <see cref="ContentWindowParameters"/> with the default values.
        /// </summary>
        public static ContentWindowParameters Default = new ContentWindowParameters
        {
            ApplyInspectorStyling = false,
            AddScrollView = true,
            MinSize = k_MinSize
        };

        /// <summary>
        /// Creates a new instance of <see cref="ContentWindowParameters"/> with the default values.
        /// </summary>
        public static ContentWindowParameters Inspector = new ContentWindowParameters
        {
            ApplyInspectorStyling = true,
            AddScrollView = true,
            MinSize = k_MinSize
        };

        /// <summary>
        /// When true, the window will auto-adjusts the width of all labels.
        /// </summary>
        public bool ApplyInspectorStyling;

        /// <summary>
        /// When true, adds a scroll view at the root of the window.
        /// </summary>
        public bool AddScrollView;

        /// <summary>
        /// Sets the minimal size of the window.
        /// </summary>
        public Vector2 MinSize;
    }
}
