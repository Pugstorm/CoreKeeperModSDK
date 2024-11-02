# Unmanaged components

Unmanaged components store the most common data types which means they're useful in the majority of use-cases.

Unmanaged components can store fields of the following types:

* [Blittable types](https://docs.microsoft.com/en-us/dotnet/framework/interop/blittable-and-non-blittable-types)
* `bool`
* `char`
* `BlobAssetReference<T>` (a reference to a Blob data structure)
* `Collections.FixedString` (a fixed-sized character buffer)
* `Collections.FixedList`
* [Fixed array](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/fixed-statement) (only allowed in an [unsafe](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/unsafe) context)
* Other structs that conform to these same restrictions

## Create an unmanaged component

To create an unmanaged component, create a struct that inherits from `IComponentData`.

The following code sample shows an unmanaged component:

[!code-cs[Create an unmanaged component](../DocCodeSamples.Tests/CreateComponentExamples.cs#unmanaged)]

Add fields that use compatible types to the struct to define data for the component. If you don't add any fields to the component, it acts as a [tag component](components-tag.md).

## Additional resources

* [Tag components](components-tag.md)
* [Managed components](components-managed.md)