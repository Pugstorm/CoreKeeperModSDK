# Version numbers

You can use the version numbers (also known as generations) of the parts of the ECS architecture to detect potential changes and to implement efficient optimization strategies, such as skipping processing when data hasn't changed since the last frame of the application. It's useful to perform quick version checks on entities to improve the performance of your application.

This page outlines all the different version numbers ECS uses, and the conditions that causes them to change.

## Version number structure

All version numbers are 32-bit signed integers. They always increase unless they wrap around: signed integer overflow is a defined behavior in C#. This means that to compare version numbers, you should use the equality (`==`) or inequality (`!=`) operator, not relational operators.

For example, the correct way to check if `VersionB` is more recent than `VersionA` is to use the following:

```c#
bool VersionBIsMoreRecent == (VersionB - VersionA) > 0;
```

There is no guarantee how much a version number increases by.

## Entity version numbers

An `EntityId` contains an index and a version number. Because ECS recycles indices, it increases the version number in [`EntityManager`](xref:Unity.Entities.EntityManager) every time the entity it destroys an entity. If there's a mismatch in the version numbers when an `EntityId` is looked up in `EntityManager`, it means that the entity referred to doesn’t exist anymore.

For example, before you fetch the position of the enemy that a unit is tracking via an `EntityId`, you can call `ComponentDataFromEntity.Exists`. This uses the version number to check if the entity still exists.

## World version numbers

ECS increases the version number of a world every time it creates or destroys a manager (such as a system).

## System version numbers

`EntityDataManager.GlobalVersion` is increased before every system update.

You should use this version number in conjunction with `System.LastSystemVersion`. This takes the value of `EntityDataManager.GlobalVersion` after every system update.

You should use this version number in conjunction with `Chunk.ChangeVersion[]`.

### Chunk.ChangeVersion

For each component type in the archetype, this array contains the value of `EntityDataManager.GlobalVersion` at the time the component array was last accessed as writeable within this chunk. This doesn't guarantee that anything has changed, only that it might have changed.

You can't access shared components as writeable, even if there is a version number stored for those too: it serves no purpose.

When you use the `WithChangeFilter()` method in an `Entities.ForEach` construction, ECS compares the `Chunk.ChangeVersion` for that specific component to `System.LastSystemVersion`, and it only processes chunks whose component arrays have been accessed as writeable after the system last started running.

For example, if the amount of health points of a group of units is guaranteed not to have changed since the previous frame, you can skip checking if those units should update their damage model.

If a system manually calls another system's `Update()` method from inside its own `OnUpdate()` method, `EntityQuery` objects in the caller system might see unexpected and incorrect change version numbers based on the processing performed in the target system. For this reason, you shouldn't manually update one system from another if both systems are processing entity data, especially if either uses `EntityQuery.SetChangedVersionFilter()`. This guidance doesn't apply to `ComponentSystemGroup` or other "pass-through" systems which only update other systems without manipulating entity data.

## Non-shared component version numbers 

For each non-shared component type, ECS increases the `EntityManager.m_ComponentTypeOrderVersion[]` version number every time an iterator involving that type becomes invalid. In other words, anything that might modify arrays of that type (not instances).

For example, if you have static objects that a particular component identifies, and a per-chunk bounding box, you only need to update those bounding boxes if the type order version changes for that component.

## Shared component version numbers 

The `SharedComponentDataManager.m_SharedComponentVersion[]` version number increases when any structural change happens to the entities stored in a chunk that reference the shared component.

For example, if you keep a count of entities per shared component, you can rely on that version number to only redo each count if the corresponding version number changes.
