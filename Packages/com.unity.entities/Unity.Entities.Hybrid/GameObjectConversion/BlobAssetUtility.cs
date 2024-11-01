using Unity.Collections;

namespace Unity.Entities
{
    internal struct BlobAssetUtility
    {
        /// <summary>
        /// Creates a BlobAsset and return its BlobAssetReference
        /// </summary>
        /// <param name="blobData">The data to put in the BlobAsset</param>
        /// <typeparam name="T">The type of BlobAsset to create</typeparam>
        /// <returns>The create BlobAssetReference</returns>
        internal static  BlobAssetReference<T> CreateBlobAsset<T>(T blobData) where T : unmanaged
        {
            using (var builder = new BlobBuilder(Allocator.TempJob))
            {
                ref var data = ref builder.ConstructRoot<T>();
                data = blobData;
                var blobAssetReference = builder.CreateBlobAssetReference<T>(Allocator.Persistent);
                
                return blobAssetReference;
            }
        }
    }
}
