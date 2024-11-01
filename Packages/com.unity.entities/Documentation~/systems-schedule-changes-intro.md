# Ways to schedule data changes

The entity component system (ECS) has the following ways to manage structural changes within your project:

* [Entity command buffers (ECB)](systems-entity-command-buffers.md)
* [The methods in EntityManager](systems-entitymanager.md)

The difference between the two options are as follows:

* If you want to queue up [structural changes](concepts-structural-changes.md) from a job, you must use an ECB.
* If you want to perform structural changes on the main thread, and have them happen instantly, use the methods in `EntityManager`.
* If you want to perform structural changes on the main thread, and you want them to happen at a later point (such as after a job completes), use an ECB.
* The changes recorded in an ECB only are applied when [`Playback`](xref:Unity.Entities.EntityCommandBuffer.Playback*) is called on the main thread. If you try to record any further changes to the ECB after playback, then Unity throws an exception.

## Additional resources

* [Entity command buffers overview](systems-entity-command-buffers.md)
* [EntityManager overview](systems-entitymanager.md)
