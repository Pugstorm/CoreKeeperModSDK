using UnityEngine;

namespace Unity.Entities.TestComponents
{
    public class BlobAssetAddTestAuthoring : MonoBehaviour
    {
        public int blobValue;
    }

    public struct BlobAssetReference : IComponentData
    {
        public int blobValue;
        public Hash128 blobHash;
        public BlobAssetReference<int> blobReference;
    }

    public class AddBlobAssetWithDefaultHashBaker : Baker<BlobAssetAddTestAuthoring>
    {
        public override void Bake(BlobAssetAddTestAuthoring authoring)
        {
            var blobReference = BlobAssetUtility.CreateBlobAsset(authoring.blobValue);
            AddBlobAsset(ref blobReference, out Hash128 objectHash);

            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new BlobAssetReference()
            {
                blobValue = blobReference.Value,
                blobHash = objectHash,
                blobReference = blobReference
            });
        }
    }

    public static class CustomHashHelpers
    {
        public static Hash128 Compute(int value)
        {
            var customHash = value.GetHashCode();
            return new Hash128((uint)customHash, 1, 2, 3);
        }
    }

    [DisableAutoCreation]
    public class AddBlobAssetWithCustomHashBaker : Baker<BlobAssetAddTestAuthoring>
    {
        public override void Bake(BlobAssetAddTestAuthoring authoring)
        {
            var customCustomHash = CustomHashHelpers.Compute(authoring.blobValue);

            var blobReference = BlobAssetUtility.CreateBlobAsset(authoring.blobValue);
            AddBlobAssetWithCustomHash(ref blobReference, customCustomHash);

            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new BlobAssetReference()
            {
                blobValue = blobReference.Value,
                blobHash = customCustomHash,
                blobReference = blobReference
            });
        }
    }

    public struct BlobAssetGetReference : IComponentData
    {
        public int blobValue;
        public Hash128 blobHash;
        public BlobAssetReference<int> blobReference;
    }

    [DisableAutoCreation]
    public class GetBlobAssetWithCustomHashBaker : Baker<BlobAssetAddTestAuthoring>
    {
        public override void Bake(BlobAssetAddTestAuthoring authoring)
        {
            var customCustomHash = CustomHashHelpers.Compute(authoring.blobValue);

            if (TryGetBlobAssetReference(customCustomHash, out BlobAssetReference<int> blobReference))
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BlobAssetReference()
                {
                    blobValue = blobReference.Value,
                    blobHash = customCustomHash,
                    blobReference = blobReference
                });
            }
            else
            {
                blobReference = BlobAssetUtility.CreateBlobAsset(authoring.blobValue);
                AddBlobAssetWithCustomHash(ref blobReference, customCustomHash);
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BlobAssetGetReference()
                {
                    blobValue = blobReference.Value,
                    blobHash = customCustomHash,
                    blobReference = blobReference
                });
            }
        }
    }

    public struct BlobAssetReferenceElement : IBufferElementData
    {
        public Hash128 blobHash;
    }

    public class MultipleTryGetRefCountingBaker : Baker<BlobAssetAddTestAuthoring>
    {
        public override void Bake(BlobAssetAddTestAuthoring authoring)
        {
            var customCustomHash = CustomHashHelpers.Compute(authoring.blobValue);

            // First add the blob asset
            var blobReference = BlobAssetUtility.CreateBlobAsset(authoring.blobValue);
            AddBlobAssetWithCustomHash(ref blobReference, customCustomHash);
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new BlobAssetGetReference()
            {
                blobValue = blobReference.Value,
                blobHash = customCustomHash,
                blobReference = blobReference
            });

            var buffer = AddBuffer<BlobAssetReferenceElement>(entity);

            // TryGet 4 times (adds 4 refcounting in the Baker and BlobAssetStore)
            for (int i = 0; i < 4; i++)
            {
                if (TryGetBlobAssetReference(customCustomHash, out BlobAssetReference<int> secondBlobReference))
                {
                    buffer.Add(new BlobAssetReferenceElement() {blobHash = customCustomHash});
                }
            }
        }
    }
}
