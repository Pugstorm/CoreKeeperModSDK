using Unity.Collections;
using UnityEngine;

namespace Unity.Entities.TestComponents
{
    public class BlobAssetStore_Test_Authoring : MonoBehaviour
    {
        public int Field;
    }

    public struct BlobAssetStore_Test_Component : IComponentData
    {
        public BlobAssetReference<int> BlobData;
    }

    public struct BlobAssetStore_Test_Settings
    {
        public int Field;
    }

    public class BlobAssetStore_Test_Baker : Baker<BlobAssetStore_Test_Authoring>
    {
        public override void Bake(BlobAssetStore_Test_Authoring authoring)
        {
            //Same hash, we should generate only one blob asset for each go using a BlobAssetStore_EndToEnd_Test_Authoring component
            var hash = new Hash128(1);
            if (!TryGetBlobAssetReference(hash, out BlobAssetReference<int> blobAssetReference))
            {
                blobAssetReference = BlobAssetReference<int>.Create(3);
                AddBlobAssetWithCustomHash(ref blobAssetReference, hash);
                Debug.Log("Retrieve blobasset from store");
            }

            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new BlobAssetStore_Test_Component() {BlobData = blobAssetReference});
        }
    }
}
