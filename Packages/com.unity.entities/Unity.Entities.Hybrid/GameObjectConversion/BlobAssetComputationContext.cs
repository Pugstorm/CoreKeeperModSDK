using System;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.Entities
{
    /// <summary>
    /// The BlobAssetComputationContext must be used during Authoring to ECS conversion process to detect which BlobAsset should be computed and to declare their association with a UnityObject
    /// </summary>
    /// <typeparam name="TS">The type of the setting struct to be used to generate the BlobAsset</typeparam>
    /// <typeparam name="TB">The type of the BlobAsset to generate</typeparam>
    /// <remarks>
    /// The context must typically be used in a three stages conversion process, for given type of BlobAsset to process.
    /// Multiple context can be used if multiple BlobAsset types are generated.
    /// Stages:
    ///  1) Each Authoring component to convert are evaluated>
    ///     The user calls <see cref="AssociateBlobAssetWithUnityObject"/> to declare the association between the UnityObject owning the Authoring component and the BlobAsset being processed.
    ///     Then <see cref="NeedToComputeBlobAsset"/> is called to determine if the BlobAsset needs to be computed or if it's already in the store (or registered for computation).
    ///     The user creates the setting object that contains the necessary information to create the BlobAsset later on and calls <see cref="AddBlobAssetToCompute"/>.
    ///  2) The user creates a job to compute all BlobAsset and calls <see cref="GetSettings"/> to feed the job with the settings of each BlobAsset to compute.
    ///     During the job execution, the BlobAsset will be created and typically stored in a result array.
    ///     After the job is done, the user must call <see cref="AddComputedBlobAsset"/> to add the newly created BlobAsset to the context (and the Store)
    ///  3) The user create ECS Components and attaches the BlobAsset by calling<see cref="GetBlobAsset"/>.
    /// When the context will be disposed (typically after the conversion process is done), the store will be updated with the new associations between the BlobAsset and the UnityObject(s) that use them.
    /// If a BlobAsset is no longer used by any UnityObject, it will be disposed.
    /// Thread-safety: main thread only.
    /// </remarks>
    struct BlobAssetComputationContext<TS, TB> : IDisposable where TS : unmanaged where TB : unmanaged
    {
        /// <summary>
        /// Initializes and returns an instance of BlobAssetComputationContext.
        /// </summary>
        /// <param name="blobAssetStore">The BlobAssetStore used by the BlobAssetComputationContext.</param>
        /// <param name="initialCapacity">The initial capacity of the internal native containers.</param>
        /// <param name="allocator">The allocator used to initialize the internal native containers.</param>
        /// <exception cref="ArgumentNullException">Thrown if an invalid BlobAssetStore is passed.</exception>
        public BlobAssetComputationContext(BlobAssetStore blobAssetStore, int initialCapacity, Allocator allocator)
        {
            if (!blobAssetStore.IsCreated)
                throw new ArgumentNullException(nameof(blobAssetStore), "A valid BlobAssetStore must be passed to construct a BlobAssetComputationContext");
            m_BlobAssetStore = blobAssetStore;
            m_ToCompute = new NativeParallelHashMap<Hash128, TS>(initialCapacity, allocator);
            m_Computed = new NativeParallelHashMap<Hash128, BlobAssetReference<TB>>(initialCapacity, allocator);
            m_BlobAssetStoreTypeHash = BlobAssetStore.ComputeTypeHash(typeof(TB));
        }

        /// <summary>
        /// Checks if the BlobAssetComputationContext exists and its native containers are allocated.
        /// </summary>
        /// <returns>Returns true if BlobAssetComputationContext has been created, and its native containers are allocated. Otherwise returns false.</returns>
        public bool IsCreated => m_ToCompute.IsCreated;

        private BlobAssetStore m_BlobAssetStore;
        private NativeParallelHashMap<Hash128, TS> m_ToCompute;
        private NativeParallelHashMap<Hash128, BlobAssetReference<TB>> m_Computed;
        private uint m_BlobAssetStoreTypeHash;

        /// <summary>
        /// Gets all the BlobAssetSettings with a specified allocator.
        /// </summary>
        /// <param name="allocator">The allocator to get the BlobAssetSettings with.</param>
        /// <returns>Returns BlobAssetSettings as a native array.</returns>
        public NativeArray<TS> GetSettings(Allocator allocator) => m_ToCompute.GetValueArray(allocator);

        /// <summary>
        /// Dispose the Computation context, update the BlobAssetStore with the new BlobAsset/UnityObject associations
        /// </summary>
        /// <remarks>
        /// This method will calls <see cref="UpdateBlobStore"/> to ensure the store is up to date.
        /// </remarks>
        public void Dispose()
        {
            if (!m_ToCompute.IsCreated)
            {
                return;
            }

            m_ToCompute.Dispose();
            m_Computed.Dispose();
        }

        /// <summary>
        /// During the conversion process, the user must call this method for each BlobAsset being processed, to determine if it requires to be computed
        /// </summary>
        /// <param name="hash">The hash associated to the BlobAsset</param>
        /// <returns>true if the BlobAsset must be computed, false if it's already in the store or the computing queue</returns>
        public bool NeedToComputeBlobAsset(Hash128 hash)
        {
            return !m_ToCompute.ContainsKey(hash) && !m_BlobAssetStore.Contains(hash, m_BlobAssetStoreTypeHash);
        }

        /// <summary>
        /// Call this method to record a setting object that will be used to compute a BlobAsset
        /// </summary>
        /// <param name="hash">The hash associated with the BlobAsset</param>
        /// <param name="settings">The setting object to store</param>
        public void AddBlobAssetToCompute(Hash128 hash, TS settings)
        {
            if (!m_ToCompute.TryAdd(hash, settings))
            {
                throw new ArgumentException($"The hash: {hash} already as a setting object. You shouldn't add a setting object more than once.");
            }
        }

        /// <summary>
        /// Add a newly created BlobAsset in the context and its Store.
        /// </summary>
        /// <param name="hash">The hash associated to the BlobAsset</param>
        /// <param name="blob">The BlobAsset to add</param>
        public void AddComputedBlobAsset(Hash128 hash, BlobAssetReference<TB> blob)
        {
            if (!m_Computed.TryAdd(hash, blob) || !m_BlobAssetStore.TryAdd(hash, m_BlobAssetStoreTypeHash, ref blob))
            {
                throw new ArgumentException($"There is already a BlobAsset with the hash: {hash} in the Store or the Computed list. You should add a newly computed BlobAsset only once.");
            }
        }

        /// <summary>
        /// Get the blob asset for the corresponding hash
        /// </summary>
        /// <param name="hash">The hash associated with the BlobAsset</param>
        /// <param name="blob">The BlobAsset corresponding to the given Hash</param>
        /// <returns>true if the blob asset was found, false otherwise</returns>
        public bool GetBlobAsset(Hash128 hash, out BlobAssetReference<TB> blob)
        {
            return m_Computed.TryGetValue(hash, out blob) || m_BlobAssetStore.TryGet(hash, m_BlobAssetStoreTypeHash, out blob);
        }
    }
}
