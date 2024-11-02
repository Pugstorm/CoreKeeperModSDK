---
uid: api-index
---

# Entity Component System API reference

This page contains an overview of some key APIs that make up Unity's Entity Component System (ECS).

| **Entity types** | **Description**|
| :--- | :--- |
| [Entity](xref:Unity.Entities.Entity) | The fundamental identifier in ECS. |
| [EntityArchetype](xref:Unity.Entities.EntityArchetype) | A unique combination of component types. For more information, see [Archetype concepts](xref:concepts-archetypes). |
| [EntityQuery](xref:Unity.Entities.EntityQuery) | Select entities with specific characteristics. For more information, see [Querying entity data with an entity query](xref:systems-entityquery). |
| [EntityQueryBuilder](xref:Unity.Entities.EntityQueryBuilder) | Create [EntityQuery](xref:Unity.Entities.EntityQuery) objects. |
| [EntityManager](xref:Unity.Entities.EntityManager) | Manages entities and provides utility methods. |
| [World](xref:Unity.Entities.World) | An isolated collection of entities. For more information see [World concepts](xref:concepts-worlds) |

| **Component types** |**Description** |
| :--- | :--- |
| [IComponentData](xref:Unity.Entities.IComponentData) | A marker interface for general purpose components. |
| [ISharedComponentData](xref:Unity.Entities.ISharedComponentData) | A marker interface for components that more than one entity shares. For moe information, see [Shared components](xref:components-shared). |
| [ICleanupComponentData](xref:Unity.Entities.ICleanupComponentData) | A marker interface for specialized system components. For more information, see [Cleanup components](xref:components-cleanup). |
| [IBufferElementData](xref:Unity.Entities.IBufferElementData) | A marker interface for buffer elements. For more information, see [Buffer components](xref:components-buffer). |
| [DynamicBuffer](xref:Unity.Entities.DynamicBuffer`1) | Access buffer elements. |
| [BlobAssetReference](xref:Unity.Entities.BlobAssetReference`1) | A reference to a blob asset in a component. |

| **System types** | **Description** |
| :--- | :--- |
| [ISystem](xref:Unity.Entities.ISystem)|An interface to create systems from.|
| [ComponentSystemBase](xref:Unity.Entities.ComponentSystemBase) | Defines a set of basic functionality for systems. For more information, see [Creating systems with SystemBase](xref:systems-systembase). |
| [SystemBase](xref:Unity.Entities.SystemBase) | The base class to extend when writing a system. |
| [ComponentSystemGroup](xref:Unity.Entities.ComponentSystemGroup) | A group of systems that update as a unit. For more information, see [System groups](xref:systems-update-order). |

| **ECS job types** | **Description**                                                                                                                                                                                         |
|:---|:--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [IJobEntity](xref:Unity.Entities.IJobEntity) | An interface to implicitly create a job that iterates over the entities. For more information, see [Iterate over component data with IJobEntity](xref:iterating-data-ijobentity).                       |
| [Entities.ForEach](xref:Unity.Entities.SystemBase.Entities) | An implicitly created job that iterates over a set of entities. For more information, see [Iterate over data with Entities.ForEach](xref:iterating-data-entities-foreach).                              |
| [Job.WithCode](xref:Unity.Entities.SystemBase.Job) | An implicitly created single job.                                                                                                                                                                       |
| [IJobChunk](xref:Unity.Entities.IJobChunk)  | An interface to explicitly create a job that iterates over the chunks matched by an entity query. For more information, see [Iterate over data with IJobChunk](xref:iterating-data-ijobchunk).          |

| **Other important types** |**Description** |
| :--- | :--- |
| [ArchetypeChunk](xref:Unity.Entities.ArchetypeChunk) | The storage unit for entity components. |
| [EntityCommandBuffer](xref:Unity.Entities.EntityCommandBuffer) | A buffer for recording entity modifications used to reduce structural changes. For more information see [Scheduling data changes with an EntityCommandBuffer](xref:systems-entity-command-buffers). |
| [ComponentType](xref:Unity.Entities.ComponentType) | Define types when creating entity queries. |
| [BlobBuilder](xref:Unity.Entities.BlobBuilder) | A utility class to create blob assets, which are immutable data structures that can be safely shared between jobs using [BlobAssetReference](xref:Unity.Entities.BlobAssetReference`1) instances. |
| [ICustomBootstrap](xref:Unity.Entities.ICustomBootstrap) | An interface to implement to create your own system loop. |
| [SystemAPI](xref:Unity.Entities.SystemAPI)|Class that gives access to access to buffers, components, time, enumeration, singletons and more. This also includes any [IAspect](xref:Unity.Entities.IAspect), [IJobEntity](xref:Unity.Entities.IJobEntity), [SystemBase](xref:Unity.Entities.SystemBase), and [ISystem](xref:Unity.Entities.ISystem).|

| **Attributes** | **Description**|
| :--- | :--- |
| [UpdateInGroup](xref:Unity.Entities.UpdateInGroupAttribute) | Defines the [ComponentSystemGroup](xref:Unity.Entities.ComponentSystemGroup) that a system should be added to. |
| [UpdateBefore](xref:Unity.Entities.UpdateBeforeAttribute) | Specifies that one system must update before another. |
| [UpdateAfter](xref:Unity.Entities.UpdateAfterAttribute) | Specifies that one system must update after another|
| [DisableAutoCreation](xref:Unity.Entities.DisableAutoCreationAttribute) | Prevents a system from being automatically discovered and run when your application starts up |
| [ExecuteAlways](xref:UnityEngine.ExecuteAlways) | Specifies that a system's update function must be invoked every frame, even when no entities are returned by the system's entity query. |

