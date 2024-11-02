---
uid: blobassets
---

# Blob assets

Blob assets represent immutable binary data. Because blob assets are immutable, it means that you can access them safely in parallel. Additionally, blob assets are restricted to unmanaged types, which means they're compatible with the Burst compiler. Unity stores blob assets in a memory-ready format, and because they're unmanaged it means that serializing and deserializing is much faster than other formats of data. 

Blob assets can be referenced by [component](concepts-components.md) on an [entity](concepts-entities.md). 


## Supported data

Blob assets mustn't contain any managed data such as regular arrays, strings, or any other managed object. Blob asset data is read-only, which means that it doesn't change at runtime. 

To quickly load blob assets, the data they contain must be **value types**. Because a blob asset must only contain value types, it can't contain absolute references to itself, which means you can't use internal pointers. In addition to the standard value types, three special data types are supported; [BlobArray, BlobPtr, and BlobString](blob-assets-create.md).

The compiler throws an error if you try to use a blob asset that has internal pointers by value. This impacts interaction with blob assets in the following ways:

* To create a blob asset, you must use a [`BlobBuilder`](xref:Unity.Entities.BlobBuilder) type, which takes care of computing the relative offsets for you.
* You must access and pass blob assets by reference using the `ref` keyword or [`BlobAssetReference`](xref:Unity.Entities.BlobAssetReference`1). This ensures that any relative offsets within the blob asset still resolve to the right absolute address. This is because the data in a blob asset must be relocatable. Blob assets can be relocated as a whole in memory, but accessing them by value instead of by reference doesn't guarantee that the whole blob asset is copied.