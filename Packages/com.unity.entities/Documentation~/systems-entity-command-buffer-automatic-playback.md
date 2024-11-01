# Automatic play back and disposal of entity command buffers

To play back and dispose of entity command buffers (ECBs), you can use [`EntityCommandBufferSystem`](xref:Unity.Entities.EntityCommandBufferSystem), rather than manually doing it yourself. To do this:

1. Get the singleton instance of the` EntityCommandBufferSystem` which you want to do the playback.
1. Use the singleton to create an `EntityCommandBuffer` instance.
1. Records commands to the `EntityCommandBuffer`.

For example:

[!code-cs[conversion](../DocCodeSamples.Tests/EntityCommandBuffers.cs#ecb_from_ecbsystem)]

>[!IMPORTANT] 
>Don't manually play back or dispose of an `EntityCommandBuffer` that you've created with an `EntityCommandBufferSystem`. The `EntityCommandBufferSystem` does both for you when it runs.

In each update, an `EntityCommandBufferSystem`:

1. Completes all registered jobs, plus all jobs scheduled against its [singleton component](components-singleton.md). This ensures that any relevant jobs have finished their recording.
1. Plays back all ECBs created via the system in the same order they were created.
1. Disposes of the `EntityCommandBuffer` instances.

## Default EntityCommandBufferSystem systems

The default [world](concepts-worlds.md) has the following default `EntityCommandBufferSystem` systems:

* `BeginInitializationEntityCommandBufferSystem`
* `EndInitializationEntityCommandBufferSystem`
* `BeginFixedStepSimulationEntityCommandBufferSystem`
* `EndFixedStepSimulationEntityCommandBufferSystem`
* `BeginVariableRateSimulationEntityCommandBufferSystem`
* `EndVariableRateSimulationEntityCommandBufferSystem`
* `BeginSimulationEntityCommandBufferSystem`
* `EndSimulationEntityCommandBufferSystem`
* `BeginPresentationEntityCommandBufferSystem`

Because structural changes can't happen in the frame after Unity gives the rendering data to the renderer, there's no `EndPresentationEntityCommandBufferSystem` system. You can use `BeginInitializationEntityCommandBufferSystem` instead: the end of one frame is the beginning of the next frame.

The `EntityCommandBufferSystem` systems update at the beginning and end of the standard [system groups](concepts-systems.md#system-groups), and at the beginning and end of the fixed and variable rate simulation groups. For more information, refer to the documentation on [System update order](systems-update-order.md).

If you can't use the default systems for your application, then you can create your own `EntityCommandBufferSystem`:

[!code-cs[conversion](../DocCodeSamples.Tests/EntityCommandBuffers.cs#ecb_define_ecbsystem)]


## Deferred entities

The `EntityCommandBuffer` methods `CreateEntity` and `Instantiate` record commands that create entities. These methods only record commands and don't create entities. As such, they return `Entity` values with negative indices that represent placeholder entities that don't exist yet. These placeholder `Entity` values are only meaningful in recorded commands of the same ECB:

[!code-cs[conversion](../DocCodeSamples.Tests/EntityCommandBuffers.cs#ecb_deferred_entities)]

Values recorded in an `AddComponent`, `SetComponent`, or `SetBuffer` command might have `Entity` fields. In playback, Unity remaps any placeholder `Entity` values in these components or buffers to the corresponding actual entities:

[!code-cs[conversion](../DocCodeSamples.Tests/EntityCommandBuffers.cs#ecb_deferred_remapping)]

## Additional resources

* [Playback entity command buffers](systems-entity-command-buffer-playback.md)