using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.Entities
{
    /// <summary>
    /// Purpose of this class is to provide a consistent cache of BlobAsset object in order to avoid rebuilding them when it is not necessary
    /// </summary>
    /// <remarks>
    /// Right now the lifetime scope of this cache is bound to the LiveConversionDiffGenerator's one and it is scoped by SubScene.
    /// In other words the cache is created when we enter edit mode for a given SubScene and it is released when we close edit mode.
    /// And instance of this cache is exposed in `Unity.Entities.GameObjectConversionSettings` to allow users to query and avoid rebuilding assets.
    /// During conversion process the user must rely on the <see cref="BlobAssetComputationContext{TS,TB}"/> to associate the BlobAsset with their corresponding Authoring UnityObject and to determine which ones are to compute.
    /// Thread-safety: nothing is thread-safe, we assume this class is consumed through the main-thread only.
    /// Calling Dispose on an instance will reset the content and dispose all BlobAssetReference object stored.
    /// </remarks>
    public struct BlobAssetStore : IDisposable
    {
        NativeParallelHashMap<Hash128, BlobAssetReferenceData>  m_BlobAssets;
        NativeList<int>                                         m_CacheStats;

        AllocatorManager.AllocatorHandle                        m_Allocator;

        enum CacheStats
        {
            Hit = 0,
            Miss
        }

        internal unsafe void GarbageCollection(EntityManager entityManager)
        {
            var entityDataAccess = entityManager.GetCheckedEntityDataAccess();
            var chunks = entityManager.UniversalQuery.ToArchetypeChunkArray(Allocator.Temp);
            var uniqueBlobs = EntityDiffer.GetBlobAssetsWithDistinctHash(
                entityDataAccess->EntityComponentStore,
                entityDataAccess->ManagedComponentStore,
                chunks, Allocator.TempJob);

            var remove = new NativeList<Hash128>(m_BlobAssets.Capacity, Allocator.Temp);

            foreach (var blobInStore in m_BlobAssets)
            {
                var blobHash = blobInStore.Value.Header->Hash;
                if (!uniqueBlobs.BlobAssetsMap.ContainsKey(blobHash))
                {
                    remove.Add(blobInStore.Key);
                }
            }

            foreach (var fullHash in remove)
            {
                var blobData = m_BlobAssets[fullHash];

                blobData.UnprotectAgainstDisposal();

                if (!m_BlobAssets.Remove(fullHash))
                {
                    throw new InvalidOperationException($"Unknown blob asset for hash {fullHash}");
                }

                blobData.Dispose();
            }

            uniqueBlobs.Dispose();
        }

        /// <summary>
        /// Initializes and returns an instance of BlobAssetStore.
        /// </summary>
        /// <param name="capacity">The initial capacity of the internal native containers.</param>
        public BlobAssetStore(int capacity)
        {
            m_Allocator = Allocator.Persistent;
            m_BlobAssets = new NativeParallelHashMap<Hash128, BlobAssetReferenceData>(capacity, m_Allocator);
            m_CacheStats = new NativeList<int>(2, m_Allocator);
            m_CacheStats.Add(0);
            m_CacheStats.Add(0);
        }

        /// <summary>
        /// Checks if the BlobAssetStoreInternal has been created
        /// </summary>
        /// <returns>True if the BlobAssetStoreInternal has been created</returns>
        public bool IsCreated
        {
            get { return m_BlobAssets.IsCreated; }
        }

        /// <summary>
        /// Returns the number of BlobAssetReferences added to the store.
        /// </summary>
        public int BlobAssetCount
        {
            get { return m_BlobAssets.IsCreated ? m_BlobAssets.Count() : 0; }
        }

        /// <summary>
        /// Call this method to clear the whole content of the Cache
        /// </summary>
        /// <param name="disposeAllBlobAssetReference">If true all BlobAssetReference present in the cache will be dispose. If false they will remain present in memory</param>
        public void ResetCache(bool disposeAllBlobAssetReference)
        {
            if (disposeAllBlobAssetReference)
            {
                using (var blobDataArray = m_BlobAssets.GetValueArray(Allocator.Temp))
                {
                    for (int i = 0; i < blobDataArray.Length; i++)
                    {
                        blobDataArray[i].UnprotectAgainstDisposal();
                        blobDataArray[i].Dispose();
                    }
                }
            }

            m_BlobAssets.Clear();
        }

        /// <summary>
        /// Try to access to a BlobAssetReference from its key
        /// </summary>
        /// <param name="hash">The key associated with the BlobAssetReference when it was added to the cache</param>
        /// <param name="blobAssetReference">The corresponding BlobAssetReference or default if none was found</param>
        /// <typeparam name="T">The type of BlobAsset</typeparam>
        /// <returns>True if the BlobAsset was found and returned, false if it wasn't</returns>
        public bool TryGet<T>(Hash128 hash, out BlobAssetReference<T> blobAssetReference) where T : unmanaged
        {
            var typedHash = ComputeKeyAndTypeHash(hash, typeof(T));

            return TryGetWithFullHash(typedHash, out blobAssetReference);
        }

        /// <summary>
        /// Try to access to a BlobAssetReference from its key
        /// </summary>
        /// <param name="hash">The key associated with the BlobAssetReference when it was added to the cache</param>
        /// <param name="blobAssetReference">The corresponding BlobAssetReference or default if none was found</param>
        /// <param name="typeHash">Hash calculated with ComputeTypeHash for the type of BlobAsset</param>
        /// <typeparam name="T">The type of BlobAsset</typeparam>
        /// <returns>True if the BlobAsset was found and returned, false if it wasn't</returns>
        internal bool TryGet<T>(Hash128 hash, uint typeHash, out BlobAssetReference<T> blobAssetReference) where T : unmanaged
        {
            var typedHash = ComputeKeyAndTypeHash(hash, typeHash);

            return TryGetWithFullHash(typedHash, out blobAssetReference);
        }

        /// <summary>
        /// Try to access to a BlobAssetReference from its key
        /// </summary>
        /// <param name="hash">The key associated with the BlobAssetReference when it was added to the cache</param>
        /// <param name="blobAssetReference">The corresponding BlobAssetReference or default if none was found</param>
        /// <typeparam name="T">The type of BlobAsset</typeparam>
        /// <returns>True if the BlobAsset was found and returned, false if it wasn't</returns>
        internal bool TryGetTest<T>(Hash128 hash, out BlobAssetReference<T> blobAssetReference) where T : unmanaged
        {
            var result =  TryGet(hash, out blobAssetReference);

            if (result)
            {
                ++m_CacheStats[(int)CacheStats.Hit];
            } else
            {
                ++m_CacheStats[(int)CacheStats.Miss];
            }

            return result;
        }

        /// <summary>
        /// Try to access to a BlobAssetReference from its key
        /// </summary>
        /// <param name="fullHash">The full key (object hash + type hash) associated with the BlobAssetReference when it was added to the cache</param>
        /// <param name="blobAssetReference">The corresponding BlobAssetReference or default if none was found</param>
        /// <param name="updateRefCount">If the refCounter of this BlobAsset needs to be increased based on the result (temporary until refactor)</param>
        /// <typeparam name="T">The type of BlobAsset</typeparam>
        /// <returns>True if the BlobAsset was found and returned, false if it wasn't</returns>
        internal bool TryGetWithFullHash<T>(Hash128 fullHash, out BlobAssetReference<T> blobAssetReference, bool updateRefCount = true) where T : unmanaged
        {
            if (m_BlobAssets.TryGetValue(fullHash, out var blobData))
            {
                blobAssetReference = BlobAssetReference<T>.Create(blobData);
                return true;
            }

            blobAssetReference = default;
            return false;
        }

        /// <summary>
        /// Number of times the cache was successfully accessed
        /// </summary>
        /// <remarks>
        /// Each TryGet returning a valid content will increment this counter
        /// </remarks>
        internal int CacheHit => m_CacheStats[(int)CacheStats.Hit];

        /// <summary>
        /// Number of times the cache failed to return a BlobAssetReference for the given key
        /// </summary>
        /// <remarks>
        /// Each TryGet returning false will increment this counter
        /// </remarks>
        internal int CacheMiss => m_CacheStats[(int)CacheStats.Miss];

        /// <summary>
        /// Check if the Store contains a BlobAsset of a given type and hash
        /// </summary>
        /// <param name="key">The hash associated with the BlobAsset</param>
        /// <typeparam name="T">The type of the BlobAsset</typeparam>
        /// <returns>True if the Store contains the BlobAsset or false if it doesn't</returns>
        public bool Contains<T>(Hash128 key)
        {
            var typedHash = ComputeKeyAndTypeHash(key, typeof(T));
            return m_BlobAssets.ContainsKey(typedHash);
        }

        /// <summary>
        /// Check if the Store contains a BlobAsset of a given type and hash
        /// </summary>
        /// <param name="key">The hash associated with the BlobAsset</param>
        /// <param name="typeHash">Hash calculated with ComputeTypeHash for the type of BlobAsset</param>
        /// <returns>True if the Store contains the BlobAsset or false if it doesn't</returns>
        internal bool Contains(Hash128 key, uint typeHash)
        {
            var typedHash = ComputeKeyAndTypeHash(key, typeHash);
            return m_BlobAssets.ContainsKey(typedHash);
        }

        /// <summary>
        /// Add a BlobAssetReference with the default hash key based on the BlobAsset contents itself. If the contents of the generated blob asset is the same as a previously inserted blob asset,
        /// then the passed blobAsset will be disposed and the reference to the blob asset will be replaced with the previously added blob asset
        /// </summary>
        /// <param name="blobAsset">The blob asset that will be inserted or replaced</param>
        /// <typeparam name="T">The type of BlobAsset.</typeparam>
        /// <returns>Returns true if the blob asset was added, returns false if the blob asset was disposed and replaced with the previous blob.</returns>
        unsafe public bool TryAdd<T>(ref BlobAssetReference<T> blobAsset) where T : unmanaged
        {
            return TryAdd<T>(ref blobAsset, out _);
        }

        /// <summary>
        /// Add a BlobAssetReference with a custom hash key
        /// </summary>
        /// <param name="blobAsset">The BlobAssetReference if found or default</param>
        /// <param name="customHash">The key to be associated with the BlobAssetReference</param>
        /// <typeparam name="T">The type of BlobAsset</typeparam>
        /// <returns>true if the BlobAssetReference was found, false if not found</returns>
        public bool TryAdd<T>(Hash128 customHash, ref BlobAssetReference<T> blobAsset) where T : unmanaged
        {
            ValidateBlob(blobAsset.m_data);

            var fullHash = ComputeKeyAndTypeHash(customHash, typeof(T));

            return TryAddWithFullHash<T>(ref blobAsset, fullHash);
        }

        /// <summary>
        /// Add a BlobAssetReference with a custom hash key
        /// </summary>
        /// <param name="blobAsset">The BlobAssetReference if found or default</param>
        /// <param name="customHash">The key to be associated with the BlobAssetReference</param>
        /// <param name="typeHash">Hash calculated with ComputeTypeHash for the type of BlobAsset</param>
        /// <typeparam name="T">The type of BlobAsset</typeparam>
        /// <returns>true if the BlobAssetReference was found, false if not found</returns>
        internal bool TryAdd<T>(Hash128 customHash, uint typeHash, ref BlobAssetReference<T> blobAsset) where T : unmanaged
        {
            ValidateBlob(blobAsset.m_data);

            var fullHash = ComputeKeyAndTypeHash(customHash, typeHash);

            return TryAddWithFullHash<T>(ref blobAsset, fullHash);
        }

        /// <summary>
        /// Add a BlobAssetReference with the default hash key based on the BlobAsset contents itself. If the contents of the generated blob asset is the same as a previously inserted blob asset,
        /// then the passed blobAsset will be disposed and the reference to the blob asset will be replaced with the previously added blob asset
        /// </summary>
        /// <param name="blobAsset">The blob asset that will be inserted or replaced</param>
        /// <param name="objectHash">The hash that is based on the content of the BlobAsset</param>
        /// <typeparam name="T">The type of BlobAsset.</typeparam>
        /// <returns>Returns true if the blob asset was added, returns false if the blob asset was disposed and replaced with the previous blob.</returns>
        unsafe public bool TryAdd<T>(ref BlobAssetReference<T> blobAsset, out Hash128 objectHash) where T : unmanaged
        {
            ValidateBlob(blobAsset.m_data);

            // This used to be a different method. This was changed to the same method as for the custom hash to avoid clashes in the genereated hashes
            Hash128 hash = default;
            UnsafeUtility.MemCpy(&hash, &blobAsset.m_data.Header->Hash, sizeof(ulong));
            objectHash = hash;
            var fullHash = ComputeKeyAndTypeHash(objectHash, typeof(T));

            return TryAddWithFullHash<T>(ref blobAsset, fullHash);
        }

        /// <summary>
        /// Add a BlobAssetReference with the default hash key based on the BlobAsset contents itself. If the contents of the generated blob asset is the same as a previously inserted blob asset,
        /// then the passed blobAsset will be disposed and the reference to the blob asset will be replaced with the previously added blob asset
        /// </summary>
        /// <param name="blobAsset">The blob asset that will be inserted or replaced</param>
        /// <param name="typeHash">Hash calculated with ComputeTypeHash for the type of BlobAsset</param>
        /// <typeparam name="T">The type of BlobAsset.</typeparam>
        /// <returns>Returns true if the blob asset was added, returns false if the blob asset was disposed and replaced with the previous blob.</returns>
        unsafe internal bool TryAdd<T>(ref BlobAssetReference<T> blobAsset, uint typeHash) where T : unmanaged
        {
            ValidateBlob(blobAsset.m_data);

            Hash128 hash = default;
            UnsafeUtility.MemCpy(&hash, &blobAsset.m_data.Header->Hash, sizeof(ulong));
            hash.Value.w = typeHash;

            return TryAddWithFullHash<T>(ref blobAsset, hash);
        }

        /// <summary>
        /// Add a BlobAssetReference with the default hash key based on the BlobAsset contents itself. If the contents of the generated blob asset is the same as a previously inserted blob asset,
        /// then the passed blobAsset will be disposed and the reference to the blob asset will be replaced with the previously added blob asset
        /// </summary>
        /// <param name="blobAssetReference">The BlobAssetReference if found or default</param>
        /// <param name="fullHash">The full key (object hash + type hash) associated with the BlobAssetReference when it was added to the cache</param>
        /// <param name="updateRefCount">If the refCounter of this BlobAsset needs to be increased based on the result (temporary until refactor)</param>
        /// <typeparam name="T">The type of BlobAsset.</typeparam>
        /// <returns>Returns true if the blob asset was added, returns false if the blob asset was disposed and replaced with the previous blob.</returns>
        private bool TryAddWithFullHash<T>(ref BlobAssetReference<T> blobAssetReference, Hash128 fullHash, bool updateRefCount = true) where T : unmanaged
        {
            if (m_BlobAssets.TryGetValue(fullHash, out var existingBlob))
            {
                var existingBlobReference = BlobAssetReference<T>.Create(existingBlob);
                if(existingBlobReference != blobAssetReference)
                    blobAssetReference.Dispose();
                blobAssetReference = existingBlobReference;
                return false;
            }

            blobAssetReference.m_data.ProtectAgainstDisposal();
            m_BlobAssets.Add(fullHash, blobAssetReference.m_data);
            return true;
        }

        /// <summary>
        /// Validate that the BlobAsset has not been disposed
        /// </summary>
        /// <param name="referenceData">The BlobAssetReferenceData to validate</param>
        /// <exception cref="ArgumentException"></exception>
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        unsafe void ValidateBlob(BlobAssetReferenceData referenceData)
        {
            referenceData.ValidateNotNull();
            if (referenceData.Header->Allocator != m_Allocator)
                throw new ArgumentException($"The Allocator for the blob asset must be {m_Allocator} but was {referenceData.Header->Allocator}");

        }

        /// <summary>Obsolete. BlobAssetStore uses garbage collection and doesn't allow removing references anymore.</summary>
        /// <param name="hash">The key associated with the BlobAssetReference</param>
        /// <param name="releaseBlobAsset">If true the BlobAsset data will be released</param>
        /// <typeparam name="T">The type of the BlobAsset</typeparam>
        /// <returns>True if the BlobAsset was removed from the store, false if it wasn't found</returns>
        [Obsolete("Obsolete. BlobAssetStore uses garbage collection and doesn't allow removing references anymore.")]
        public bool TryRemove<T>(Hash128 hash, bool releaseBlobAsset)
        {
            return false;
        }

        /// <summary>
        /// Calling dispose will reset the cache content and release all the BlobAssetReference that were stored
        /// </summary>
        public void Dispose()
        {
            ResetCache(true);
            m_BlobAssets.Dispose();
            m_CacheStats.Dispose();
        }

        /// <summary>
        /// Function to calculate a hash value associated with a Type
        /// </summary>
        /// <param name="type">The BlobAsset type from where the hash is calculated.</param>
        /// <returns>Calculated hash value</returns>
        internal static uint ComputeTypeHash(Type type)
        {
            return (uint)type.GetHashCode();
        }

        /// <summary>
        /// Function to calculate a full hash value based on a BlobAsset hash and the BlobAsset type.
        /// </summary>
        /// <param name="key">The hash associated with the BlobAsset.</param>
        /// <param name="type">The BlobAsset type from where the hash is calculated.</param>
        /// <returns>Calculated hash value</returns>
        internal static Hash128 ComputeKeyAndTypeHash(Hash128 key, Type type)
        {
            return new Hash128(math.hashwide(new uint4x2 { c0 = key.Value, c1 = new uint4(ComputeTypeHash(type))}));
        }

        /// <summary>
        /// Calculates a full hash value based on a hash key and a hash associated with a Type.
        /// </summary>
        /// <param name="key">The hash associated with the BlobAsset.</param>
        /// <param name="typeHash">The hash associated with the BlobAsset type.</param>
        /// <returns>Returns the calculated hash value as a 128-bit hash value.</returns>
        static Hash128 ComputeKeyAndTypeHash(Hash128 key, uint typeHash)
        {
            return new Hash128(math.hashwide(new uint4x2 { c0 = key.Value, c1 = new uint4(typeHash)}));
        }
    }
}
