# Access dynamic buffers from jobs

If a job needs to lookup one or more buffers in its code, the job needs to use a [`BufferLookup`](xref:Unity.Entities.BufferLookup`1) lookup table. You create these in systems and then pass them to jobs that need them.

## Modify the job

In the job that requires random access to a dynamic buffer:

1. Add a [`ReadOnly`](https://docs.unity3d.com/ScriptReference/Unity.Collections.ReadOnlyAttribute.html)  `BufferLookup` member variable.
2. In the [`IJobEntity.Execute`](xref:Unity.Entities.IJobEntity) method, index the `BufferLookup` lookup table by an entity. This provides access to the dynamic buffer attached to the entity.

[!code-cs[](../DocCodeSamples.Tests/DynamicBufferExamples.cs#access-in-ijobentity-job)]

## Modify the systems

In systems that create instances of the job:

1. Add a `BufferLookup` member variable.
2. In [`OnCreate`](xref:Unity.Entities.ISystem.OnCreate*), use [`SystemState.GetBufferLookup`](xref:Unity.Entities.SystemState.GetBufferLookup*) to assign the `BufferLookup` variable.
3. At the beginning of [`OnUpdate`](xref:Unity.Entities.ISystem.OnUpdate*), call `Update` on the `BufferLookup` variable. This updates the lookup table.
4. When you create an instance of the job, pass the lookup table to the job.

[!code-cs[](../DocCodeSamples.Tests/DynamicBufferExamples.cs#access-in-ijobentity-system)]
