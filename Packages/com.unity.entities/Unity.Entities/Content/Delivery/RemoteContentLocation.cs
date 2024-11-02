using System;
using Unity.Collections;

namespace Unity.Entities.Content
{
    /// <summary>
    /// This struct contains all information needed to download a remote content file.
    /// </summary>
    [Serializable]
    public struct RemoteContentLocation : IEquatable<RemoteContentLocation>
    {
        /// <summary>
        /// The type of location.  
        /// </summary>
        public enum LocationType
        {
             /// <summary>
            /// Specifies that the Path property is a remote URL.  This enum is intended to be expanded as other download service types are added.
            /// </summary>
            RemoteURL
        }

        /// <summary>
        /// The type of location.  This can be used by download services to determine if they are compatible.
        /// </summary>
        public LocationType Type;

        /// <summary>
        /// The path of the remote content - this is typically the url.
        /// </summary>
        public FixedString512Bytes Path;

        /// <summary>
        /// The hash of the contents of the remote data.
        /// </summary>
        public Hash128 Hash;

        /// <summary>
        /// The CRC value for the remote data.  This is used to detect data corruption.
        /// </summary>
        public uint Crc;

        /// <summary>
        /// The size, in bytes, of the remote data.
        /// </summary>
        public long Size;

        /// <summary>
        /// Returns true if the Hash is valid.
        /// </summary>
        public bool IsValid => Hash.IsValid;

        /// <summary>Checks if the path to a remote file is equal to another.</summary>
        /// <param name="other">The location to compare.</param>
        /// <returns>True if the paths are equal.</returns>
        public bool Equals(RemoteContentLocation other)
        {
            if (!Hash.IsValid && !other.Hash.IsValid)
                return Path.Equals(other.Path);
            return Hash.Equals(other.Hash);
        }

        /// <summary>
        /// Gets the path to the hash code.
        /// </summary>
        /// <returns>The path to the hash code.</returns>
        public override int GetHashCode()
        {
            return Hash.IsValid ? Hash.GetHashCode() : Path.GetHashCode();
        }
    }
}
