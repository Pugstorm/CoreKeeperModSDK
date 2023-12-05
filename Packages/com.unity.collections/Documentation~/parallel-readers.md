# Parallel readers and writers

Several of the collection types have nested types to read and write from [parallel jobs](xref:JobSystemParallelForJobs). For example, to write safely to a `NativeList<T>` from a parallel job, you need to use [`NativeList<T>.ParallelWriter`](xref:Unity.Collections.LowLevel.Unsafe.UnsafeList`1.ParallelWriter):

[!code-cs[parallel_writer](../DocCodeSamples.Tests/CollectionsExamples.cs#parallel_writer)]

[!code-cs[parallel_writer_job](../DocCodeSamples.Tests/CollectionsExamples.cs#parallel_writer_job)]

Note that these parallel readers and writers don't support the full functionality of the collection. For example, a `NativeList` can't grow its capacity in a parallel job because there's no way to safely allow this without incurring a synchronization overhead.

## Deterministic reading and writing

Although a `ParallelWriter` ensures the safety of concurrent writes, the order of the concurrent writes is indeterminstic because it depends on thread scheduling. The operating system and other factors outside of your program's control determine thread scheduling.

Likewise, although a `ParallelReader` ensures the safety of concurrent reads, the order of the concurrent reads is indeterminstic, you can't know which threads read which values.

To get around this, you can use either [`NativeStream`](xref:Unity.Collections.NativeStream) or [`UnsafeStream`](xref:Unity.Collections.LowLevel.Unsafe.UnsafeStream), which splits reads and writes into a separate buffer for each thread and avoids indeterminism.

Alternatively, you can effectively get a deterministic order of parallel reads if you deterministically divide the reads into separate ranges and process each range in its own thread.

You can also get a deterministic order if you deterministically sort the data after it has been written to the list.
