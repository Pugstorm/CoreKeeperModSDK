---
uid: components-managed
---

# Managed components

Unlike [unmanaged components](components-unmanaged.md), managed components can store properties of any type. However, they're more resource intensive to store and access, and have the following restrictions:

* You can't access them in [jobs](xref:JobSystem).
* You can't use them in [Burst](https://docs.unity3d.com/Packages/com.unity.burst@latest) compiled code.
* They require garbage collection.
* They must include a constructor with no parameters for serialization purposes.

## Managed type properties

If a property in a managed component uses a managed type, you might need to manually add the ability to clone, compare, and serialize the property.

## Create a managed component

To create a managed component, create a class that inherits from `IComponentData` and either has no constructor, or includes a parameterless constructor.

The following code sample shows a managed component:
[!code-cs[Create a managed Component](../DocCodeSamples.Tests/CreateComponentExamples.cs#managed)]

## Manage the lifecycle of external resources

For managed components that reference external resources, it's best practice to implement [`ICloneable`](https://docs.microsoft.com/en-us/dotnet/api/system.icloneable) and [`IDisposable`](https://docs.microsoft.com/en-us/dotnet/api/system.idisposable), for example, for a managed component that stores a reference to a [`ParticleSystem`](https://docs.unity3d.com/Manual/class-ParticleSystem.html). 

If you duplicate this managed component's entity, by default this creates two managed components that both reference the same particle system. If you implement `ICloneable` for the managed component, you can duplicate the particle system for the second managed component. If you destroy the managed component, by default the particle system remains behind. If you implement `IDisposable` for the managed component, you can destroy the particle system when the component is destroyed.

[!code-cs[Implement ICloneable and IDisposable](../DocCodeSamples.Tests/GeneralComponentExamples.cs#managed-component-external-resource)]

## Optimize managed components

Unlike unmanaged components, Unity doesn't store managed components directly in chunks. Instead, Unity stores them in one big array for the whole `World`. Chunks then store the array indices of the relevant managed components. This means when you access a managed component of an entity, Unity processes an extra index lookup. This makes managed components less optimal than unmanaged components.

The performance implications of managed components mean that you should use [unmanaged components](components-unmanaged.md) instead where possible.

## Additional resources

* [Unmanaged components overview](components-unmanaged.md)