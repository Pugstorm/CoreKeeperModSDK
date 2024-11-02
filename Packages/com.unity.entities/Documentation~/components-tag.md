# Tag components

Tag components are [unmanaged components](components-unmanaged.md) that store no data and take up no space. 

Conceptually, tag components fulfil a similar purpose to [GameObject tags](https://docs.unity3d.com/Manual/Tags.html) and they're useful in queries because you can filter entities by whether they have a tag component. For example, you can use them alongside [cleanup components](components-cleanup.md) and filter entities to perform cleanup.

## Create a tag component

To create a tag component, create an [unmanaged component](components-unmanaged.md) without any properties.

The following code sample shows a tag component:

[!code-cs[Create a tag component](../DocCodeSamples.Tests/CreateComponentExamples.cs#tag)]

## Additional resources

* [Unmanaged components](components-unmanaged.md)
* [Cleanup components](components-cleanup.md)