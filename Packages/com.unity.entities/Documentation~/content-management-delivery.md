# Deliver content to an application

To deliver content updates for your application, you create custom content archives and load them into the application from a content delivery service, or from the local device. The [ContentDeliveryService](xref:Unity.Entities.Content.ContentDeliveryService) APIs provide a unified workflow for loading both local and online content on demand. You can use it to load content, check the delivery status, and conditionally run code depending on the content available.

## Set up content delivery

To enable content delivery for your project, set the `ENABLE_CONTENT_DELIVERY` scripting symbol. For information on how to do this, refer to [Custom scripting symbols](xref:CustomScriptingSymbols).

## Load content

The `ContentDeliveryService` APIs load content by URL. You can pass the URL of content stored on an online content delivery service or the local URL of a content archive on the device. After you begin to download the content, you must wait until Unity finishes installing and caching the content. Then, you can load objects from the installed content in the same way you access local content, by weak reference ID. For more information, refer to [Load a weakly-referenced object at runtime](content-management-load-an-object.md).

The following code sample shows how to load content by URL, wait for content delivery to complete, then continue with the application logic. It can use a MonoBehaviour rather than a [system](concepts-systems.md) because the example code performs a full content update once before the application starts. You can also use a system's update method to do this, but the content delivery system APIs aren't Burst-compiled, and you can't use the APIs from jobs, so there is no performance benefit.

[!code-cs[](../DocCodeSamples.Tests/content-management/DeliverContent.cs#example)]

## Additional resources

* [Create custom content archives](content-management-create-content-archives.md)