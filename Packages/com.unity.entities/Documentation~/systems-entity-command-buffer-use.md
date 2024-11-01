# Use an entity command buffer

You can record [entity command buffers](systems-entity-command-buffers.md) (ECBs) in jobs, and on the main thread.

## Use an entity command buffer in a job

You can't perform [structural changes](concepts-structural-changes.md) in a job, except inside an `ExclusiveEntityTransaction`, so you can use an ECB to record structural changes to play back after the job is complete. For example:

[!code-cs[conversion](../DocCodeSamples.Tests/EntityCommandBuffers.cs#ecb_single_threaded)]

### Parallel jobs

If you want to use an ECB in a [parallel job](xref:JobSystemParallelForJobs), use [`EntityCommandBuffer.ParallelWriter`](xref:Unity.Entities.EntityCommandBuffer.ParallelWriter), which concurrently records in a thread-safe way to a command buffer:

[!code-cs[conversion](../DocCodeSamples.Tests/EntityCommandBuffers.cs#ecb_parallel)]

>[!NOTE] 
>Only recording needs to be thread-safe for concurrency in parallel jobs. Playback is always single-threaded on the main thread.

For information on deterministic playback in parallel jobs, refer to the documentation on [Entity command buffer playback](systems-entity-command-buffer-playback.md#deterministic-playback-in-parallel-jobs)

## Use an entity command buffer on the main thread

You can record ECB changes on the main thread, such as in the following situations:

* To delay your changes.
* To play back a set of changes multiple times. To do this, refer to the information on [multi-playback](systems-entity-command-buffer-playback.md#multi-playback).
* To play back a lot of different kinds of changes in one consolidated place. This is more efficient than interspersing the changes across different parts of the frame.

Every structural change operation triggers a [sync point](concepts-structural-changes.md#sync-points), which means that the operation must wait for some or all scheduled jobs to complete. If you combine the structural changes into an ECB, the frame has fewer sync points.

>[!NOTE] 
> If you have a lot of the same types of commands in an ECB, and you can afford to make the change instantly, it can be faster to use the EntityManager variants on whole batches of entities at once.

