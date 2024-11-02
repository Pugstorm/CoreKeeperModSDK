# Create a chunk component

A chunk component's definition is the same as an [unmanaged component](components-unmanaged.md). This means that you create a regular struct that inherits from `IComponentData` to create a chunk component. The difference between chunk components and unmanaged components is how you add them to an entity.

The following code sample shows an unmanaged component:

[!code-cs[Create a chunk component](../DocCodeSamples.Tests/CreateComponentExamples.cs#chunk)]

To use the unmanaged component as a chunk component, use [`EntityManager.AddChunkComponentData<YourChunkComponent>(Entity)`](xref:Unity.Entities.EntityManager.AddChunkComponentData*) to add it to an entity.