namespace Unity.Entities.UI
{
    /// <summary>
    /// Inherit from this class to display arbitrary content in either an editor window or the inspector.
    /// </summary>
    /// <remarks>The type must have an explicit or implicit default constructor.</remarks>
    public abstract partial class ContentProvider
    {
        ContentStatus m_State = ContentStatus.ContentNotReady;

        internal ContentStatus MoveNext()
            => m_State = GetStatus();

        internal bool IsValid()
            => m_State != ContentStatus.ContentUnavailable;

        internal bool IsReady()
            => m_State == ContentStatus.ContentReady;

        /// <summary>
        /// Implement this property to assign the name to display in the title bar of the owning editor window or as the
        /// name to display when the content is not ready.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Implement this method to return the value representing the content.
        /// </summary>
        /// <returns>The content value to display.</returns>
        public abstract object GetContent();

        /// <summary>
        /// Override this method if your content may not be immediately available.
        /// </summary>
        /// <returns>The status of the content.</returns>
        protected virtual ContentStatus GetStatus() => ContentStatus.ContentReady;

        /// <summary>
        /// Override this method to be notified when the UI detects a change.
        /// </summary>
        /// <param name="context">Context object allowing to retrieve information about the change.</param>
        protected internal virtual void OnContentChanged(ChangeContext context)
        {
        }
    }
}
