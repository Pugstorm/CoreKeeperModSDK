using System.Collections.Generic;
using Unity.Entities.Serialization;

namespace Unity.Entities.Content
{
    /// <summary>
    /// Interface for source catalog data.
    /// </summary>
    internal interface IRuntimeCatalogDataSource
    {
        /// <summary>
        /// Get the set of archive ids.
        /// </summary>
        /// <returns>The set of archive ids.</returns>
        IEnumerable<ContentArchiveId> GetArchiveIds();
        /// <summary>
        /// Get the set of content files contained within a specific archive.
        /// </summary>
        /// <param name="archiveId">The id of the archive.</param>
        /// <returns>The set of content file ids.</returns>
        IEnumerable<ContentFileId> GetFileIds(ContentArchiveId archiveId);
        /// <summary>
        /// Get the set of dependencies of a content file.
        /// </summary>
        /// <param name="fileId">The id of the content file.</param>
        /// <returns>The set of dependencies for the content file.</returns>
        IEnumerable<ContentFileId> GetDependencies(ContentFileId fileId);
        /// <summary>
        /// Get the set of object ids and file identifiers.
        /// </summary>
        /// <param name="fileId">The id of the content file.</param>
        /// <returns>The set of object ids and file ids.</returns>
        IEnumerable<(UntypedWeakReferenceId, long)> GetObjects(ContentFileId fileId);
        /// <summary>
        /// Get the set of scene ids and names.
        /// </summary>
        /// <param name="fileId">The content file id.</param>
        /// <returns>The set of scene ids and names.</returns>
        IEnumerable<(UntypedWeakReferenceId, string)> GetScenes(ContentFileId fileId);
    }
}
