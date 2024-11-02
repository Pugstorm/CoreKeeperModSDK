# Introducing shared components

Shared components group entities in chunks based on the values of their shared component, which helps with the de-duplication of data. To do this, Unity stores all entities of an archetype that have the same shared component values together. This removes repeated values across entities.

You can create both [managed and unmanaged shared components](components-shared-create.md). Managed shared components have the same advantages and restrictions as [regular managed components](components-managed.md).

## Shared component value storage

For each [world](concepts-worlds.md), Unity stores shared component values in arrays separate from ECS chunks, and chunks in that world store handles to locate the appropriate shared component values for their archetype. Entities in the same chunk share the same shared component value. Multiple chunks can store the same shared component handle which means there is no limit to the number of entities that can use the same shared component value.

If you change the shared component value for an entity, Unity moves the entity to a chunk that uses the new shared component value. This means that changing a shared component value for an entity is a [structural change](concepts-structural-changes.md). If an equal value already exists in the shared component value array, Unity moves the entity to a chunk that stores the index of the existing value. Otherwise, Unity adds the new value to the shared component value array and moves the entity to a new chunk that stores the index of this new value. For information on how to change how ECS compares shared component values, see [Override the default comparison behavior](#override-the-default-comparison-behavior).

Unity stores unmanaged and managed shared components separate from one another and makes unmanaged shared components available to [Burst compiled](https://docs.unity3d.com/Packages/com.unity.burst@latest/index.html) code via the unmanaged shared component APIs (such as [`SetUnmanagedSharedComponentData`](xref:Unity.Entities.EntityManager.SetUnmanagedSharedComponentData*)). For more information, see [Optimize shared components](components-shared-optimize.md).

## Override the default comparison behavior
To change how ECS compares instances of a shared component, implement [`IEquatable<YourSharedComponent>`](https://docs.microsoft.com/en-us/dotnet/api/system.iequatable-1.equals) for the shared component. If you do this, ECS uses your implementation to check if instances of the shared component are equal. If the shared component is unmanaged, you can add the [`[BurstCompile]`](https://docs.unity3d.com/Packages/com.unity.burst@latest/index.html?subfolder=/api/Unity.Burst.BurstCompileAttribute.html) attribute to the shared component struct, the `Equals` method, and the `GetHashCode` method to improve performance.

## Share shared components between worlds

For managed objects that are resource intensive to create and keep, such as a blob asset, you can use shared components to only store one copy of that object across all [worlds](concepts-worlds.md). To do this, implement the [IRefCounted](xref:Unity.Entities.IRefCounted) interface with  [`Retain`](xref:Unity.Entities.IRefCounted.Retain) and [`Release`](xref:Unity.Entities.IRefCounted.Release). Implement `Retain` and `Release` so that these methods properly manage the lifetime of the underlying resource. If the shared component is unmanaged, you can add the [`[BurstCompile]`](https://docs.unity3d.com/Packages/com.unity.burst@latest/index.html?subfolder=/api/Unity.Burst.BurstCompileAttribute.html) attribute to the shared component struct, the `Retain` method, and the `Release` method to improve performance.

## Don't modify objects referenced by a shared component

To work correctly, shared components rely on you using the Entities API to change their values. This includes referenced objects. If a shared component contains a reference type or pointer, be careful not to modify the referenced object without using the Entities API.

## Additional resources

* [Optimize shared components](components-shared-optimize.md)