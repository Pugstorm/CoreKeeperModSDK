using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

// The files in this namespace are used to test the code samples in the documentation.
namespace Doc.CodeSamples.Tests
{

    #region BlobAssetBakerSetup
    struct MarketData
    {
        public float PriceOranges;
        public float PriceApples;
    }

    struct MarketDataComponent : IComponentData
    {
        public BlobAssetReference<MarketData> Blob;
    }

    public class MarketDataAuthoring : MonoBehaviour
    {
        public float PriceOranges;
        public float PriceApples;
    }
    #endregion

    #region SimpleBlobAssetBaker
    class MarketDataBaker : Baker<MarketDataAuthoring>
    {
        public override void Bake(MarketDataAuthoring authoring)
        {
            // Create a new builder that will use temporary memory to construct the blob asset
            var builder = new BlobBuilder(Allocator.Temp);

            // Construct the root object for the blob asset. Notice the use of `ref`.
            ref MarketData marketData = ref builder.ConstructRoot<MarketData>();

            // Now fill the constructed root with the data:
            // Apples compare to Oranges in the universally accepted ratio of 2 : 1 .
            marketData.PriceApples = authoring.PriceApples;
            marketData.PriceOranges = authoring.PriceOranges;

            // Now copy the data from the builder into its final place, which will
            // use the persistent allocator
            var blobReference =
                builder.CreateBlobAssetReference<MarketData>(Allocator.Persistent);

            // Make sure to dispose the builder itself so all internal memory is disposed.
            builder.Dispose();

            // Register the Blob Asset to the Baker for de-duplication and reverting.
            AddBlobAsset<MarketData>(ref blobReference, out var hash);
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new MarketDataComponent() {Blob = blobReference});
        }
    }
    #endregion

    #region CustomHashBlobAssetBaker
    class MarketDataCustomHashBaker : Baker<MarketDataAuthoring>
    {
        public override void Bake(MarketDataAuthoring authoring)
        {
            var customHash = new Unity.Entities.Hash128(
                (uint) authoring.PriceOranges.GetHashCode(),
                (uint) authoring.PriceApples.GetHashCode(), 0, 0);

            if (!TryGetBlobAssetReference(customHash,
                    out BlobAssetReference<MarketData> blobReference))
            {
                // Create a new builder that will use temporary memory to construct the blob asset
                var builder = new BlobBuilder(Allocator.Temp);

                // Construct the root object for the blob asset. Notice the use of `ref`.
                ref MarketData marketData = ref builder.ConstructRoot<MarketData>();

                // Now fill the constructed root with the data:
                // Apples compare to Oranges in the universally accepted ratio of 2 : 1 .
                marketData.PriceApples = authoring.PriceApples;
                marketData.PriceOranges = authoring.PriceOranges;

                // Now copy the data from the builder into its final place, which will
                // use the persistent allocator
                blobReference =
                    builder.CreateBlobAssetReference<MarketData>(Allocator.Persistent);

                // Make sure to dispose the builder itself so all internal memory is disposed.
                builder.Dispose();

                // Register the Blob Asset to the Baker for de-duplication and reverting.
                AddBlobAssetWithCustomHash<MarketData>(ref blobReference, customHash);
            }

            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new MarketDataComponent() {Blob = blobReference});
        }
    }
    #endregion
}
