# Use entity command buffers in Entities.ForEach

To use an entity command buffer (ECB) in the `Entities.ForEach` method, pass an `EntityCommandBuffer` parameter to the lambda expression. Only a small subset of `EntityCommandBuffer` methods are supported, and they have the `[SupportedInEntitiesForEach]` attribute:

* `Entity Instantiate(Entity entity)`
* `void DestroyEntity(Entity entity)`
* `void AddComponent<T>(Entity e, T component) where T : unmanaged, IComponentData`
* `void SetComponent<T>(Entity e, T component) where T : unmanaged, IComponentData`
* `void RemoveComponent<T>(Entity e)`

For example, the following code does this:

1. It checks each entity to find out whether its `HealthLevel` is 0.
2. If true, it records a command to destroy the entity.
3. It also specifies that the `EndSimulationEntityCommandBufferSystem` must play back the command.

[!code-cs[conversion](../DocCodeSamples.Tests/EntityCommandBuffers.cs#ecb_parallel_for)]

When you use any of these methods in a `ForEach` method, at runtime the compiler generates the code necessary to create, populate, play back, and dispose of an `EntityCommandBuffer` instance, or an `EntityCommandBuffer.ParallelWriter` instance, if `ScheduleParallel` is called.

Invoking these methods outside of `ForEach()` results in an exception.

## Play back an entity command buffer

To pass an `EntityCommandBuffer` parameter to the `ForEach` method, you must also call one of the following methods to specify when you want to play back the commands:

* Deferred playback: Call `WithDeferredPlaybackSystem<T>`, where `T` identifies the ECB system that plays back the commands. It must be a type that derives from `EntityCommandBufferSystem`.
* Immediate playback: call `WithImmediatePlayback` to execute the commands instantly after the `ForEach` method has finished all iterations. You can only use `WithImmediatePlayback` with `Run`.

The compiler automatically generates code to create and dispose of any `EntityCommandBuffer` instances.

## Additional resources

* [Entity command buffer overview](systems-entity-command-buffers.md)