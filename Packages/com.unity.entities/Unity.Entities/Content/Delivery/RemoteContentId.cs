using System;
using Unity.Collections;

namespace Unity.Entities.Content
{
    /// <summary>
    /// Identifier of a remote content resource.  This is used to find the <seealso cref="RemoteContentLocation"/> of a resource.
    /// </summary>
    [Serializable]
    public struct RemoteContentId : IEquatable<RemoteContentId>
    {
        /// <summary>
        /// The name of the content.  This is ususally set to the path of the asset.
        /// </summary>
        public FixedString512Bytes Name;

        /// <summary>
        /// The hash, used to compare ids.  This is either set as the has of the name or to a custom hash.
        /// </summary>
        public Hash128 Hash
        {
            private set;
            get;
        }

        /// <summary>
        /// Construct an id with only the path.  The Hash is computed from the name.
        /// </summary>
        /// <param name="name">The name of the content.</param>
        unsafe public RemoteContentId(in FixedString512Bytes name)
        {
            Name = name;
            Hash = UnityEngine.Hash128.Compute(Name.GetUnsafePtr(), Name.utf8LengthInBytes);
        }

        /// <summary>
        /// Construct an id with a name and custom hash.
        /// </summary>
        /// <param name="name">The name of the content.</param>
        /// <param name="hash">The hash for the id.</param>
        unsafe public RemoteContentId(in FixedString512Bytes name, Hash128 hash)
        {
            Assertions.Assert.IsTrue(hash.IsValid, "Cannot create a RemoteContentId with an invalid hash.");
            Name = name;
            Hash = hash;
        }
        /// <summary>
        /// True if the id is valid.
        /// </summary>
        public bool IsValid => Hash.IsValid;

        ///<inheritdoc/>
        public bool Equals(RemoteContentId other) => Hash.Equals(other.Hash);

        /// <summary>
        /// Gets the path to the hash code.
        /// </summary>
        /// <returns>The path to the hash code.</returns>
        public override int GetHashCode() => Hash.GetHashCode();
    }
}
