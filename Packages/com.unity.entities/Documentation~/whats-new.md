# What's new in Entities 1.0

This is a summary of the changes in Entities version 1.0.

For a full list of changes, see the [Changelog](xref:changelog). For information on how to upgrade to version 1.0, see the [Upgrade guide](upgrade-guide.md).

## Added

* **Aspects**, which group different components together into a single C# struct. This helps you to organize your component code and simplify queries in your systems. For more information, see the [`IAspect`](xref:Unity.Entities.IAspect) API documentation, and the user manual documentation on [Aspects](aspects-intro.md).
* **Baking** replaces the previous conversion pipeline in Entities. For information on how to use the new system, see the [`Baker<TAuthoringType>`](xref:Unity.Entities.Baker`1) API documentation and the [Baking user manual](baking.md).
* **Enableable components**, which you can use to semantically add or remove a component to an entity without effectively changing its archetype. This avoids [structural changes](concepts-structural-changes.md), which impact on performance. For more information, see the [`IEnableableComponent`](xref:Unity.Entities.IEnableableComponent) API documentation, and the user manual documentation on [enableable components](components-enableable.md).
* The **Journaling editor window**, which provides an in-Editor view of journaling records connected to the various other Entities windows. For more information, see [Journaling](entities-journaling.md)
* New **authoring-runtime workflow**, which affects the [Entities Hierarchy](editor-hierarchy-window.md) and the [Inspector](editor-entity-inspector.md), allowing you to change between authoring and runtime representations without having to change your selection context. For more information, see [Working with authoring and runtime data](editor-authoring-runtime.md).
* [`Query<T>`](xref:Unity.Entities.SystemAPI.Query*), which you use with an idiomatic foreach statement to iterate over entities in `ISystem` based systems. `Entities.ForEach` is now deprecated in `ISystem` systems. For more information, see the information in the [upgrade guide](upgrade-guide.md#remove-entitiesforeach-in-isystem-based-systems).

## Updated

* The way that **Transforms** work in Entities has been changed to reduce the number of components required to represent an affine transform. For more information, see the [`Transform`](xref:Unity.Transforms) API documentation, and the user manual documentation on [Transforms](transforms-intro.md).
* You can now use the [Build Settings window](https://docs.unity3d.com/Manual/BuildSettings.html) to build your project, and don’t need to use Build Configuration assets. For any specialized use cases, you can use the [Scriptable Build Pipeline](https://docs.unity3d.com/Packages/com.unity.scriptablebuildpipeline@1.20/manual/index.html).
* Settings that were previously in the **DOTS menu** have moved to the **Preferences** window. For more information, see the [Entities Preferences reference](editor-preferences.md).

## Additional resources

* [Upgrade guide](upgrade-guide.md)
* [Changelog](xref:changelog)