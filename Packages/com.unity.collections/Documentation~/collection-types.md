# Collection types

The collection types in this package extend and compliment the collections available in the Unity engine. This section outlines some key collection types you might want to use in your jobs and Burst compiled code.

## Array-like types

The array-like types in this package extend the key array types in the [`UnityEngine.CoreModule`](https://docs.unity3d.com/ScriptReference/UnityEngine.CoreModule) namespace, which include [`Unity.Collections.NativeArray<T>`](xref:Unity.Collections.NativeArray`1) and [`Unity.Collections.NativeSlice<T>`](xref:Unity.Collections.NativeSlice`1).  This package has the following array-like types:

|**Data structure**|**Description**|
|---|---|
|[`NativeList<T>`](xref:Unity.Collections.NativeList`1)| A resizable list. Has thread and disposal safety checks.|
|[`UnsafeList<T>`](xref:Unity.Collections.LowLevel.Unsafe.UnsafeList`1)| A resizable list.|
|[`UnsafePtrList<T>`](xref:Unity.Collections.LowLevel.Unsafe.UnsafePtrList`1)| A resizable list of pointers.|
|[`NativeStream`](xref:Unity.Collections.NativeStream)| A set of append-only, untyped buffers. Has thread and disposal safety checks.|
|[`UnsafeStream`](xref:Unity.Collections.LowLevel.Unsafe.UnsafeStream)| A set of append-only, untyped buffers.|
|[`UnsafeAppendBuffer`](xref:Unity.Collections.LowLevel.Unsafe.UnsafeAppendBuffer)| An append-only untyped buffer.|
|[`NativeQueue<T>`](xref:Unity.Collections.NativeQueue`1)| A resizable queue. Has thread and disposal safety checks.|
|[`NativeRingQueue<T>`](xref:Unity.Collections.NativeRingQueue`1)| A fixed-size circular buffer. Has disposal safety checks.|
|[`UnsafeRingQueue<T>`](xref:Unity.Collections.LowLevel.Unsafe.UnsafeRingQueue`1) | A fixed-size circular buffer.|
|[`FixedList32Bytes<T>`](xref:Unity.Collections.FixedList32Bytes`1)| A 32-byte list, which includes 2 bytes of overhead, so 30 bytes are available for storage. Max capacity depends upon `T`. `FixedList32Bytes<T>` has variants of larger sizes:<br/>- `FixedList64Bytes<T>`<br/>- `FixedList128Bytes<T>`<br/>- `FixedList512Bytes<T>`<br/>- `FixedList4096Bytes<T>`|

There aren't any multi-dimensional array types, but you can pack all the data into a single dimension. For example, for an `int[4][5]` array, use an `int[20]` array instead (because `4 * 5` is `20`).

If you're using the [Entities package](https://docs.unity3d.com/Packages/com.unity.entities@latest), a [DynamicBuffer](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.DynamicBuffer-1.html) component is often the best choice for an array or list like collection.

Additionally, there are various extension methods in [`NativeArrayExtensions`](xref:Unity.Collections.NativeArrayExtensions), [`ListExtensions`](xref:Unity.Collections.ListExtensions), and [`NativeSortExtension`](xref:Unity.Collections.NativeSortExtension).

## Map and set types

Use these collection types in single threads when there is a low memory overhead:

|**Data structure**|**Description**|
|---|---|
|[`NativeHashMap<TKey, TValue>`](xref:Unity.Collections.NativeHashMap`2)| An unordered associative array of key-value pairs. Has thread and disposal safety checks.|
|[`UnsafeHashMap<TKey, TValue>`](xref:Unity.Collections.LowLevel.Unsafe.UnsafeHashMap`2)| An unordered associative array of key-value pairs.|
|[`NativeHashSet<T>`](xref:Unity.Collections.NativeHashSet`1)| A set of unique values. Has thread and disposal safety checks.|
| [`UnsafeHashSet<T>`](xref:Unity.Collections.LowLevel.Unsafe.UnsafeHashSet`1)| A set of unique values.|

Use these collection types in multithreaded situations, when there is a high memory overhead.

|**Data structure**|**Description**|
|---|---|
|[`NativeParallelHashMap<TKey, TValue>`](xref:Unity.Collections.NativeParallelHashMap`2)|An unordered associative array of key-value pairs. Has thread and disposal safety checks.|
|[`UnsafeParallelHashMap<TKey, TValue>`](xref:Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashMap`2)|An unordered associative array of key value pairs.|
| [`NativeParallelHashSet<T>`](xref:Unity.Collections.NativeParallelHashSet`1)| A set of unique values. Has thread and disposal safety checks.|
| [`UnsafeParallelHashSet<T>`](xref:Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashSet`1)|A set of unique values.|
|[`NativeParallelMultiHashMap<TKey, TValue>`](xref:Unity.Collections.NativeParallelMultiHashMap`2)|An unordered associative array of key value pairs. The keys don't have to be unique. For example, two pairs can have equal keys. Has thread and disposal safety checks.|
|[`UnsafeParallelMultiHashMap<TKey, TValue>`](xref:Unity.Collections.LowLevel.Unsafe.UnsafeParallelMultiHashMap`2)| An unordered associative array of key value pairs. The keys don't have to be unique. For example, two pairs can have equal keys.|

Additionally, there are various extension methods in [`NotBurstCompatible.Extensions`](xref:Unity.Collections.NotBurstCompatible.Extensions) and [`Unsafe.NotBurstCompatible.Extensions`](xref:Unity.Collections.LowLevel.Unsafe.NotBurstCompatible.Extensions).

## Bit arrays and bit fields

The following are arrays of bits:

|**Data structure**|**Description**|
|---|---|
|[`BitField32`](xref:Unity.Collections.BitField32)| A fixed-size array of 32 bits.|
|[`BitField64`](xref:Unity.Collections.BitField64 )| A fixed-size array of 64 bits.|
|[`NativeBitArray`](xref:Unity.Collections.NativeBitArray)| An arbitrary sized array of bits. Has thread and disposal safety checks.|
|[`UnsafeBitArray`](xref:Unity.Collections.LowLevel.Unsafe.UnsafeBitArray)| An arbitrary-sized array of bits.|

## String types

The following are string types:

|**Data structure**|**Description**|
|---|---|
|[`NativeText`](xref:Unity.Collections.NativeText)| A UTF-8 encoded string. Mutable and resizable. Has thread and disposal safety checks.|
|[`UnsafeText`](xref:Unity.Collections.LowLevel.Unsafe.UnsafeText)| A UTF-8 encoded string. Mutable and resizable.|
|[`FixedString32Bytes`](xref:Unity.Collections.FixedString32Bytes) | A 32-byte UTF-8 encoded string, including 3 bytes of overhead, so 29 bytes available for storage.|
|[`FixedString64Bytes`](xref:Unity.Collections.FixedString64Bytes)| A 64-byte UTF-8 encoded string, including 3 bytes of overhead, so 61 bytes available for storage.|
|[`FixedString128Bytes`](xref:Unity.Collections.FixedString128Bytes)| A 128-byte UTF-8 encoded string, including 3 bytes of overhead, so 125 bytes available for storage.|
|[`FixedString512Bytes`](xref:Unity.Collections.FixedString512Bytes)| A 512-byte UTF-8 encoded string, including 3 bytes of overhead, so 509 bytes available for storage.|
|[`FixedString4096Bytes`](xref:Unity.Collections.FixedString4096Bytes) | A 4096-byte UTF-8 encoded string, including 3 bytes of overhead, so 4093 bytes available for storage.|

There are further extension methods in [`FixedStringMethods`](xref:Unity.Collections.FixedStringMethods).
  
## Other types

|**Data structure**|**Description**|
|---|---|
|[`NativeReference<T>`](xref:Unity.Collections.NativeReference`1) | A reference to a single value. Functionally equivalent to an array of length 1. Has thread and disposal safety checks.|
|[`UnsafeAtomicCounter32`](xref:Unity.Collections.LowLevel.Unsafe.UnsafeAtomicCounter32) | A 32-bit atomic counter.|
|[`UnsafeAtomicCounter64`](xref:Unity.Collections.LowLevel.Unsafe.UnsafeAtomicCounter64) | A 64-bit atomic counter.|

## Enumerators

Most of the collections have a `GetEnumerator` method, which returns an implementation of `IEnumerator<T>`. The enumerator's `MoveNext` method advances its `Current` property to the next element:

[!code-cs[enumerator](../DocCodeSamples.Tests/CollectionsExamples.cs#enumerator)]

