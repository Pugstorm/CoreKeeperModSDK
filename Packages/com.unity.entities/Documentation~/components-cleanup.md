---
uid: components-cleanup
---

# Cleanup components

Cleanup components are like regular components, but when you destroy an entity that contains one, Unity removes all non-cleanup components instead. The entity still exists until you remove all cleanup components from it. This is useful to tag entities that require cleanup when destroyed.

|**Topic**| **Description** |
|---|---|
| [Introducing cleanup components](components-cleanup-introducing.md)| Understand cleanup components and their use cases.|
| [Create a cleanup component](components-cleanup-create.md) | Create a new cleanup component to use in your application.  |
| [Cleanup shared components](components-cleanup-shared.md)|Cleanup shared components are managed shared components that have the destruction semantics of a cleanup component.|

## Additional resources

* [Shared components](components-shared.md)
* [Managed components](components-managed.md)

