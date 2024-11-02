# Create a shared component

You can create both [managed](components-managed.md) and [unmanaged](components-unmanaged.md) shared components.

## Create an unmanaged shared component

To create an unmanaged shared component, create a struct that implements the marker interface `ISharedComponentData`.

The following code sample shows an unmanaged shared component:

[!code-cs[Create an unmanaged shared component](../DocCodeSamples.Tests/CreateComponentExamples.cs#shared-unmanaged)]

## Create a managed shared component

To create a managed shared component, create a struct that implements the marker interface `ISharedComponentData` and `IEquatable<>`, and ensure `public override int GetHashCode()` is implemented. The equality methods are necessary to ensure comparisons do not generate managed allocations unnecessarily due to implict boxing when using the default Equals and GetHashCode implementations.

The following code sample shows a managed shared component:

[!code-cs[Create a managed shared component](../DocCodeSamples.Tests/CreateComponentExamples.cs#shared-managed)]
