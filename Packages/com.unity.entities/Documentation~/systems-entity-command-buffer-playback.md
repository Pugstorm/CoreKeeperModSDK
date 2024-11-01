# Entity command buffer playback

If you split the recording of commands in an entity command buffer (ECB) across multiple threads [in a parallel job](systems-entity-command-buffer-use.md#parallel-jobs) it means that the order of the commands is non-deterministic because they depend on job scheduling. 

Determinism isn't always essential, but code which produces deterministic results is easier to debug. There are also networking scenarios which require consistent results across different machines. However, determinism can have an impact on performance, so you might want to accept non-determinism in some projects.

## Deterministic playback in parallel jobs

You can't avoid the non-deterministic order of recording [in parallel jobs](systems-entity-command-buffer-use.md#parallel-jobs), but you can make the playback order of the commands deterministic in the following way:

1. Record an int sort key passed as the first argument to each ECB method.
1. Use the sort keys to sort the commands on playback, before Unity performs the commands.

If the recorded sort keys are independent from the scheduling, then the sorting makes the playback order deterministic. Also, Unity always plays back commands with larger sort keys after commands with smaller sort keys.

In a parallel job, the sort key you need for each entity is a number that has a fixed and unique association with that chunk in the job's query. You can use the `ChunkIndexInQuery` value in a parallel job as an index. The index has a zero-based numbering system, so in the list of archetype chunks that match the job's query, the first chunk has index 0, the second chunk has index 1, and the third chunk has index 2.

The following example code shows an ECB used in a parallel job:

[!code-cs[conversion](../DocCodeSamples.Tests/EntityCommandBuffers.cs#ecb_multi_threaded)]


## Multi playback

If you call the `Playback` method more than once, it throws an exception. To avoid this, create an `EntityCommandBuffer` instance with the `PlaybackPolicy.MultiPlayback` option:

[!code-cs[conversion](../DocCodeSamples.Tests/EntityCommandBuffers.cs#ecb_multi_playback)]

You can use multi-playback if you want to repeatedly spawn a set of entities. To do this, create and configure a set of new entities with an `EntityCommandBuffer`, and then repeat playback to respawn another matching set of entities.

## Additional resources

* [Automatically play back entity command buffers](systems-entity-command-buffer-automatic-playback.md)
