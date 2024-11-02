namespace Unity.Entities.UI
{
    /// <summary>
    /// The different statuses describing the content of a content provider.
    /// </summary>
    public enum ContentStatus
    {
        /// <summary>
        /// Indicates that the content could not be reloaded.
        /// </summary>
        ContentUnavailable = 0,

        /// <summary>
        /// Indicates that the content is not ready for display.
        /// </summary>
        ContentNotReady = 1,

        /// <summary>
        /// Indicates that the content is ready for display.
        /// </summary>
        ContentReady = 2,

        /// <summary>
        /// Indicates that the content should be reloaded.
        /// </summary>
        ReloadContent = 3
    }
}
