# Collections overview

The Collections package extends the concepts and collections in the core Unity engine, including adding more [native container objects](xref:JobSystemNativeContainer) and [job types](https://docs.unity3d.com/Manual/job-system-jobs.html). The collections in this package fall into the following categories:

* Collection types which have safety checks that make sure that Unity properly disposes of the type, and you use them in a thread-safe way. These types are in the [`Unity.Collections`](xref:Unity.Collections) namespace and their names start with `Native`.
* Collection types which don't have safety checks. These types are in the [`Unity.Collections.LowLevel.Unsafe`](xref:Unity.Collections.LowLevel.Unsafe) namespace and their names start with `Unsafe`.

The remaining collection types which don't fit into these categories aren't allocated and don't contain any pointers. These types only contain small amounts of data, and their disposal and thread safety aren't a concern.

## Native and unsafe comparison

`Native` collection types perform safety checks to make sure that indices passed to their methods are in bounds, but the other types don't have these kind of checks.

Several `Native` types have `Unsafe` equivalents, for example, [`NativeList`](xref:Unity.Collections.INativeList`1) has [`UnsafeList`](xref:Unity.Collections.LowLevel.Unsafe.UnsafeList`1), and [`NativeHashMap`](xref:Unity.Collections.NativeHashMap`2) has [`UnsafeHashMap`](xref:Unity.Collections.LowLevel.Unsafe.UnsafeHashMap`2).

It's best practice to use `Native` collections over the `Unsafe` equivalents. However, it's sometimes necessary to use the `Unsafe` equivalents because `Native` collection types can't contain other `Native` collections. This is because of how Unity implements the `Native` safety checks. For example, if you want to get a list of lists, you can use either `NativeList<UnsafeList<T>>` or `UnsafeList<UnsafeList<T>>`, but you can't use `NativeList<NativeList<T>>`.

If you've disabled safety checks, then there isn't a significant performance difference between a `Native` type and its `Unsafe` equivalent. In fact, most `Native` collections are implemented as wrappers of their `Unsafe` counterparts. For example, `NativeList` is made up of an `UnsafeList` plus some handles that the safety checks use. 

## Additional resources

* [Collection types overview](collection-types.md)
* [Parallel readers and writers](parallel-readers.md)