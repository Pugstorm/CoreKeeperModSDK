using Unity.Collections;
using UnityEngine;

namespace Unity.Entities.TestComponents
{
    public class BlobAssetTestSystemAuthoring : MonoBehaviour
    {
        public int blobValue;
    }

    [TemporaryBakingType]
    public struct TempBlobAssetData : IComponentData
    {
        public int blobValue;
        public Hash128 blobHash;
    }

    public struct BlobAssetReferenceFromTestSystem : IComponentData
    {
        public BlobAssetReference<int> blobReference;
    }

    [DisableAutoCreation]
    public class BlobAssetTestSystemBaker : Baker<BlobAssetTestSystemAuthoring>
    {
        public override void Bake(BlobAssetTestSystemAuthoring authoring)
        {
            var customCustomHash = CustomHashHelpers.Compute(authoring.blobValue);

            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new TempBlobAssetData()
            {
                blobValue = authoring.blobValue,
                blobHash = customCustomHash,
            });

            AddComponent<BlobAssetReferenceFromTestSystem>(entity);
        }
    }

    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial class BlobAssetStoreRefCountingBakingSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var bakingSystem = World.GetExistingSystemManaged<BakingSystem>();
            var blobAssetStore = bakingSystem.BlobAssetStore;
            using (var context = new BlobAssetComputationContext<int, int>(blobAssetStore, 16, Allocator.Temp))
            {
                Entities.ForEach((ref BlobAssetReferenceFromTestSystem blobRefComponent, in TempBlobAssetData blobData) =>
                {
                    if(!context.GetBlobAsset(blobData.blobHash, out blobRefComponent.blobReference))
                    {
                        context.AddBlobAssetToCompute(blobData.blobHash, 0);

                        // Create the blob reference
                        BlobBuilder builder = new BlobBuilder(Allocator.TempJob);
                        ref var data = ref builder.ConstructRoot<int>();
                        data = blobData.blobValue;
                        var blobAssetReference = builder.CreateBlobAssetReference<int>(Allocator.Persistent);
                        builder.Dispose();

                        blobRefComponent.blobReference = blobAssetReference;

                        context.AddComputedBlobAsset(blobData.blobHash, blobAssetReference);
                    }
                }).Run();
            }
        }
    }
}
