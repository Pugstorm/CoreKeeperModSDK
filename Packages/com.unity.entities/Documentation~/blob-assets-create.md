## Create a blob asset

To create a [blob asset](blob-assets-concept.md), perform the following steps:

1. Create a [BlobBuilder](xref:Unity.Entities.BlobBuilder). This needs to allocate some memory internally.
1. Use [BlobBuilder.ConstructRoot](xref:Unity.Entities.BlobBuilder.ConstructRoot*) to construct the root of the blob asset. 
1. Fill the structure with your data.
1. Use [BlobBuilder.CreateBlobAssetReference](xref:Unity.Entities.BlobBuilder.CreateBlobAssetReference*) to create a [BlobAssetReference](xref:Unity.Entities.BlobAssetReference`1). This copies the blob asset to its final location.
1. Dispose the `BlobBuilder`.

The following example stores a struct with primitive members as a blob asset:

[!code-cs[blobs](../DocCodeSamples.Tests/BlobAssetExamples.cs#CreateSimpleBlobAsset)]

The `BlobBuilder` constructs the data stored in the blob asset, makes sure that all internal references are stored as offsets, and then copies the finished blob asset into a single allocation referenced by the returned `BlobAssetReference<T>`.

## Arrays in blob assets

You must use the [BlobArray](xref:Unity.Entities.BlobArray`1) type to create an array within a blob asset. This is because arrays are implemented with relative offsets internally. The following is an example of how to allocate an array of blob data and fill it:

[!code-cs[blobs](../DocCodeSamples.Tests/BlobAssetExamples.cs#CreateBlobAssetWithArray)]

## Strings in blob assets
You must use the [BlobString](xref:Unity.Entities.BlobString) type to create a string within a blob asset. The following is an example of a string allocated with the `BlobBuilder` API.

[!code-cs[blobs](../DocCodeSamples.Tests/BlobAssetExamples.cs#CreateBlobAssetWithString)]

## Internal pointers

To manually set an internal pointer, use the [`BlobPtr<T>`](xref:Unity.Entities.BlobPtr`1) type.

[!code-cs[blobs](../DocCodeSamples.Tests/BlobAssetExamples.cs#CreateBlobAssetWithInternalPointer)]

## Accessing blob assets on a component
Once you've made a `BlobAssetReference<T>` to a blob asset, you can store this reference on component and access it. You must access all parts of a blob asset that contain internal pointers [by reference](blob-assets-concept.md#supported-data).

[!code-cs[blobs](../DocCodeSamples.Tests/BlobAssetExamples.cs#BlobAssetOnAComponent)]

## Dispose a blob asset reference

Any blob assets that you allocate at runtime with [BlobBuilder.CreateBlobAssetReference](xref:Unity.Entities.BlobBuilder.CreateBlobAssetReference*) need to be disposed manually. 

However, you don't need to manually dispose of any blob assets that were loaded as part of an entity scene loaded from disk. All of these blob assets are reference counted and automatically released once no component references them anymore.

[!code-cs[blobs](../DocCodeSamples.Tests/BlobAssetExamples.cs#BlobAssetInRuntime)]

## Debugging blob asset contents

Blob assets use relative offsets to implement internal references. This means that copying a `BlobString` struct, or any other type with these internal references, copies the relative offset contained, but not what it's pointing to. The result of this is an unusable `BlobString` that represents a random string of characters. While this is easy to avoid in your own code, debugging utilities often do exactly that. Therefore, the contents of a `BlobString` aren't displayed correctly in a debugger.

However, there is support for displaying the values of a `BlobAssetReference<T>` and all its contents. If you want to look up the contents of a `BlobString`, navigate to the containing `BlobAssetReference<T>` and start debugging from there.

## Blob assets in baking

You can use [bakers and baking systems](baking.md) to create blob assets offline and have them be available in runtime. 

To handle blob assets, the [BlobAssetStore](xref:Unity.Entities.BlobAssetStore) is used. The `BlobAssetStore` keeps internal ref counting and ensures that blob assets are disposed if nothing references it anymore. The Bakers internally have access to a `BlobAssetStore`, but to create blob assets in a Baking System, you need to retrieve the `BlobAssetStore` from the Baking System.

## Register a blob asset with a baker

Because bakers are deterministic and incremental, you need to follow some extra steps to use blob assets in baking. As well as creating a [BlobAssetReference](xref:Unity.Entities.BlobAssetReference`1) with the [BlobBuilder](xref:Unity.Entities.BlobBuilder), you need to register the BlobAsset to the baker. 

To register the blob asset to the baker, you call [AddBlobAsset](xref:Unity.Entities.IBaker.AddBlobAsset*) with the BlobAssetReference:

[!code-cs[blobs](../DocCodeSamples.Tests/BlobAssetBakingExamples.cs#BlobAssetBakerSetup)]

[!code-cs[blobs](../DocCodeSamples.Tests/BlobAssetBakingExamples.cs#SimpleBlobAssetBaker)]

> [!IMPORTANT]
> If you don't register a blob asset to the baker, the ref counting doesn't update and the blob asset could be de-allocated unexpectedly.

The baker uses `BlobAssetStore` to de-duplicate and refcount the blob assets. It also decreases the ref counting of the associated blob assets to revert the blob assets when the baker is re-run. Without this step, the bakers would break incremental behaviour. Because of this, the `BlobAssetStore` isn't available directly from the baker, and only through baker methods.

### De-duplication with custom hashes

The previous example let the baker handle all de-duplication, but that means you have to create the blob asset first before the baker de-duplicates and disposes the extra blob asset. In some cases you might want to de-duplicate before the blob asset is created in the baker. 

To do this, you can use a custom hash instead of letting the baker generate one. If multiple bakers either have access to, or generate the same hash for the same blob assets, you can use this hash to de-duplicate before generating a blob asset. Use [TryGetBlobAssetReference](xref:Unity.Entities.IBaker.TryGetBlobAssetReference*) to check if the custom hash is already registered to the baker:

[!code-cs[blobs](../DocCodeSamples.Tests/BlobAssetBakingExamples.cs#CustomHashBlobAssetBaker)]
