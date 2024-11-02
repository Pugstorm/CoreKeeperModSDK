# Component types

To serve a variety of use cases, there are multiple types of ECS components. This section of the documentation describes the ECS component types, their use cases and performance considerations, and how to create them.

| **Topic**                                                 | **Description**                                           |
|-----------------------------------------------------------|-----------------------------------------------------------|
| [Unmanaged components](components-unmanaged.md)           | Understand unmanaged components and how to use them.      |
| [Managed components](components-managed.md)               | Understand managed components and how to use them.        |
| [Shared components](components-shared.md)                 | Understand shared components and how to use them.         |
| [Cleanup components](components-cleanup.md)               | Understand cleanup components and how to use them.        |
| [Tag components](components-tag.md)                       | Understand tag components and how to use them.            |
| [Buffer components](components-buffer.md)                 | Understand buffer components and how to use them.         |
| [Chunk components](components-chunk.md)                   | Understand chunk components and how to use them.          |
| [Enableable components](components-enableable.md)| Understand enableable components and how to use them.|
| [Singleton components](components-singleton.md)| Understand singleton components, which are components that only have one instance.|

## Component types in the Editor

In the Editor, the following icons represent the different types of components. These appear in relevant [Entities windows and Inspectors](editor-workflows.md).

| **Icon**                                 | **Component type**   |
| ---------------------------------------- | -------------------- |
| ![](images/editor-managed-component.png) | A managed component. |
| ![](images/editor-shared-component.png)  | A shared component.  |
| ![](images/editor-tag-component.png)     | A tag component.     |
| ![](images/editor-buffer-component.png)  | A buffer component.  |
| ![](images/editor-chunk-component.png)   | A chunk component.   |

> [!NOTE]
> Unity uses a generic component icon for component types not in the list.

## Additional resources

* [Components concepts](concepts-components.md)
