using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities.Serialization;

namespace Unity.Entities.Content
{
    /// <summary>
    /// Global id for content archives.
    /// </summary>
    [Serializable]
    internal struct ContentArchiveId : IEquatable<ContentArchiveId>
    {
        /// <summary>
        /// The value of the id.
        /// </summary>
        public Hash128 Value;
        /// <summary>
        /// True if the id has a non default value.
        /// </summary>
        public bool IsValid => Value.IsValid;
        /// <inheritdoc/>
        public bool Equals(ContentArchiveId other) => Value.Equals(other.Value);
        /// <inheritdoc/>
        public override int GetHashCode() => Value.GetHashCode();
        /// <inheritdoc/>
        public override string ToString() => Value.ToString();
    }

    /// <summary>
    /// Global id of content files.
    /// </summary>
    [Serializable]
    internal struct ContentFileId : IEquatable<ContentFileId>
    {
        /// <summary>
        /// The value of the id.
        /// </summary>
        public Hash128 Value;
        /// <summary>
        /// True if the id has a non default value.
        /// </summary>
        public bool IsValid => Value.IsValid;
        /// <inheritdoc/>
        public bool Equals(ContentFileId other) => Value.Equals(other.Value);
        /// <inheritdoc/>
        public override int GetHashCode() => Value.GetHashCode();
        /// <inheritdoc/>
        public override string ToString() => Value.ToString();
    }

    /// <summary>
    /// Global id for scenes contained in content archives.
    /// </summary>
    [Serializable]
    internal struct ContentSceneId : IEquatable<ContentSceneId>
    {
        /// <summary>
        /// The value of the id.
        /// </summary>
        public Hash128 Value;
        /// <summary>
        /// True if the id has a non default value.
        /// </summary>
        public bool IsValid => Value.IsValid;
        /// <inheritdoc/>
        public bool Equals(ContentSceneId other) => Value.Equals(other.Value);
        /// <inheritdoc/>
        public override int GetHashCode() => Value.GetHashCode();
        /// <inheritdoc/>
        public override string ToString() => Value.ToString();
    }

    /// <summary>
    /// Content archive information.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ContentArchiveLocation
    {
        /// <summary>
        /// The id of the archive, this is transformed into the archive path at runtime.
        /// </summary>
        public ContentArchiveId ArchiveId;
    }

    /// <summary>
    /// Content file information.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ContentFileLocation
    {
        /// <summary>
        /// The id of the file, this is transformed into the file path at runtime.
        /// </summary>
        public ContentFileId FileId;
        /// <summary>
        /// The index of the archive that contains this file.  This indexes into the ContentCatalogData.Archives array.
        /// </summary>
        public int ArchiveIndex;
        /// <summary>
        /// Index of content file dependencies.
        /// </summary>
        public int DependencyIndex;
    }

    /// <summary>
    /// Content object information.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ContentObjectLocation
    {
        /// <summary>
        /// The object reference id.
        /// </summary>
        public UnsafeUntypedWeakReferenceId ObjectId;
        /// <summary>
        /// The index of the content file to load this object from.  This indexes into the ContentCatalogData.Files array.
        /// </summary>
        public int FileIndex;
        /// <summary>
        /// The local file id of this object in the content file. (NOTE: this is not the same local file id as the WeakObjectReferenceId.ObjectId).
        /// </summary>
        public long LocalIdentifierInFile;
    }

    /// <summary>
    /// Content scene information.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ContentSceneLocation
    {
        /// <summary>
        /// The object reference id.
        /// </summary>
        public UnsafeUntypedWeakReferenceId SceneId;
        /// <summary>
        /// The index of the content file to load this object from.  This indexes into the ContentCatalogData.Files array.
        /// </summary>
        public int FileIndex;
        /// <summary>
        /// The name of the scene.
        /// </summary>
        public FixedString128Bytes SceneName;
    }
    
    /// <summary>
    /// Content blob information.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ContentBlobLocation
    {
        /// <summary>
        /// The blob reference id.
        /// </summary>
        public UnsafeUntypedWeakReferenceId ObjectId;
        /// <summary>
        /// The index of the content file to load this object from.  This indexes into the ContentCatalogData.Files array.
        /// </summary>
        public int FileIndex;
        /// <summary>
        /// The data offset.
        /// </summary>
        public long Offset;
        /// <summary>
        /// The length of the blob data.
        /// </summary>
        public long Length;
    }
    

    /// <summary>
    /// Serialized catalog data.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct RuntimeContentCatalogData
    {
        /// <summary>
        /// The collection of content archives.
        /// </summary>
        public BlobArray<ContentArchiveLocation> Archives;
        /// <summary>
        /// The collection of files that are contained in the archives.
        /// </summary>
        public BlobArray<ContentFileLocation> Files;
        /// <summary>
        /// The collection of objects that are contained in the content files and we're directly referenced by a WeakObjectReferenceId.
        /// </summary>
        public BlobArray<ContentObjectLocation> Objects;
        /// <summary>
        /// The collection of scene that are contained in the content files and we're directly referenced by a WeakObjectReferenceId.
        /// </summary>
        public BlobArray<ContentSceneLocation> Scenes;
        /// <summary>
        /// List of dependency sets for content files.  Many files will potentially share the same set of dependencies so this is separate to allow for that.
        /// </summary>
        public BlobArray<BlobArray<int>> Dependencies;
        /// <summary>
        /// The collection of blob data that are contained in the content files and we're directly referenced by a WeakObjectReferenceId.
        /// </summary>
        public BlobArray<ContentBlobLocation> Blobs;
    }
}
