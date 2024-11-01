# Cleanup shared components

Cleanup shared components are [shared components](components-shared.md) that have the destruction semantics of a [cleanup component](components-cleanup.md). They are useful to tag entities that require the same information for clean up.

## Create a cleanup shared component

To create a cleanup shared component, create a struct that inherits from `ICleanupSharedComponentData`.

The following code sample shows an empty system cleanup Component:

[!code-cs[Create a cleanup shared component](../DocCodeSamples.Tests/CreateComponentExamples.cs#system-state-shared)]


