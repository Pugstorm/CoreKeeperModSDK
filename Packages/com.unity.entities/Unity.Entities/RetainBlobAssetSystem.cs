using Unity.Burst;
using Unity.Collections;

namespace Unity.Entities
{
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    partial class RetainBlobAssetSystem : SystemBase
    {
        protected unsafe override void OnUpdate()
        {
            Entities.WithNone<RetainBlobAssetBatchPtr>().WithoutBurst().WithStructuralChanges().ForEach((Entity e, BlobAssetOwner blobOwner, ref RetainBlobAssets retain) =>
            {
                BlobAssetBatch.Retain(blobOwner.BlobAssetBatchPtr);
                EntityManager.AddComponentData(e, new RetainBlobAssetBatchPtr { BlobAssetBatchPtr = blobOwner.BlobAssetBatchPtr});
            }).Run();

            Entities.WithNone<BlobAssetOwner>().WithoutBurst().WithStructuralChanges().ForEach((Entity e, ref RetainBlobAssets retain, ref RetainBlobAssetBatchPtr retainPtr) =>
            {
                if (retain.FramesToRetainBlobAssets-- <= 0)
                {
                    BlobAssetBatch.Release(retainPtr.BlobAssetBatchPtr);
                    EntityManager.RemoveComponent<RetainBlobAssets>(e);
                    EntityManager.RemoveComponent<RetainBlobAssetBatchPtr>(e);
                }
            }).Run();

            Entities.WithNone<BlobAssetOwner>().WithoutBurst().WithStructuralChanges().ForEach((Entity e, ref RetainBlobAssets retain, ref RetainBlobAssetPtr retainPtr) =>
            {
                if (retain.FramesToRetainBlobAssets-- <= 0)
                {
                    retainPtr.BlobAsset->Invalidate();
                    Memory.Unmanaged.Free(retainPtr.BlobAsset, Allocator.Persistent);
                    EntityManager.RemoveComponent<RetainBlobAssets>(e);
                    EntityManager.RemoveComponent<RetainBlobAssetPtr>(e);
                }
            }).Run();
        }

        protected override unsafe void OnDestroy()
        {
            Entities.ForEach((Entity e, ref RetainBlobAssets retain, ref RetainBlobAssetBatchPtr retainPtr) =>
            {
                BlobAssetBatch.Release(retainPtr.BlobAssetBatchPtr);
            }).Run();

            Entities.ForEach((Entity e, ref RetainBlobAssets retain, ref RetainBlobAssetPtr retainPtr) =>
            {
                retainPtr.BlobAsset->Invalidate();
                Memory.Unmanaged.Free(retainPtr.BlobAsset, Allocator.Persistent);
            }).Run();
        }
    }
}


