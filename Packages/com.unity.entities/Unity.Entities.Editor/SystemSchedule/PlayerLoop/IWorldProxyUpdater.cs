using System;

namespace Unity.Entities.Editor
{
    interface IWorldProxyUpdater
    {
        /// <summary>
        /// Populate world proxy.
        /// </summary>
        void PopulateWorldProxy();

        /// <summary>
        /// Reset world proxy.
        /// </summary>
        void ResetWorldProxy();

        /// <summary>
        /// Update frame data.
        /// </summary>
        void UpdateFrameData();

        /// <summary>
        /// Register updater to EditorApplication.update to update frame data and detect change.
        /// </summary>
        void EnableUpdater();

        /// <summary>
        /// Unregister updater from EditorApplication.update.
        /// </summary>
        void DisableUpdater();

        /// <summary>
        /// Remember the updater is registered to EditorApplication.update or not.
        /// </summary>
        bool IsActive();

        /// <summary>
        /// Remember the updater has changed or not.
        /// </summary>
        bool IsDirty();

        /// <summary>
        /// Set updater clean after dealing with the changes.
        /// </summary>
        void SetClean();
    }
}
