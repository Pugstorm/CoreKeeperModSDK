# Baker overview

A simple baking system uses a baker to read data from the input [authoring scene](conversion-scene-overview.md), and writes the data as components on entities as the output.

To create a baker, you use the [`Baker<TAuthoringType>`](xref:Unity.Entities.Baker`1) managed generic class and pass an authoring component as the type argument. The authoring components are `UnityEngine` components, typically a `MonoBehaviour`. A baker defines a single [`Bake`](xref:Unity.Entities.Baker`1.Bake*) method that takes the authoring component of that type as input.

Unity calls the Bake method on every authoring component that it needs to bake. If Unity performs [full baking](baking-overview.md#full-baking), it bakes all authoring components in the authoring scene. If it performs [incremental baking](baking-overview.md#incremental-baking), it only bakes components that have been directly modified or whose dependencies have been modified.

## Baker architecture

A baker is only instantiated once and its `Bake` method is called many times, in a non-deterministic order. If Unity performs incremental baking, it does this operation over a long period of time. This means that bakers need to be stateless and always access data through methods. In particular, caching any value in the baker violates this invariant and causes the baking process to misbehave.

When an authoring GameObject is modified, Unity must first revert the effects of any earlier baking before it can be re-bake the GameObject in its new state.

Each baker also has to declare its dependencies. By default, a baker only accesses a single authoring component. But when it also pulls data from other components, other GameObjects, or various resources, the baking process needs to know about this. If one of those external sources changes, the baker also has to re-run.

For example, if an authoring GameObject is a cube primitive, Unity bakes it into an entity that renders a cube. If that authoring GameObject is later modified to be a sphere, its ECS equivalent must change to be an entity that renders as a sphere. This means that the Unity must either destroy the entity that rendered the cube, and create a new one that renders a sphere. Or, Unity needs to change the entity to display a sphere. If the GameObject has a material that depends on a scriptable object, it declares a dependency to that asset to ensure that whenever the scriptable object is modified, the object is rebaked.

Therefore, Unity records everything a baker produces so that it can undo it before a rebake. Similarly, Unity records everything a baker accesses to trigger a rebake when required. This process happens automatically. But this is the reason why most of what a baker does is calling its own member functions, because they do the recording without which the logic would break.

## Create a baker 

When you create a baker, you define which MonoBehaviour authoring component it's for and then write code that uses the authoring component data to create and attach ECS components to entities.

Bakers must inherit from the [`Baker`](xref:Unity.Entities.Baker`1) class. A baker can only add components to the primary entity for the authoring component, and to the additional entities that the same baker creates. For example:

[!code-cs[](../DocCodeSamples.Tests/BakingExamples.cs#SimpleBaker)]

### Accessing other data sources in a baker

To keep [incremental baking](baking-overview.md#incremental-baking) working, you need to track what data is used when baking the GameObject. Unity automatically tracks any field in the authoring component and the baker re-runs if any of that data changes.

However, Unity doesn't automatically track data from other sources, such as authoring components or assets. You need to add a dependency to the baker so it can track this kind of data. To do this, use the methods that the `Baker` class provides to access other components and GameObjects instead of the methods provided by the GameObject:

[!code-cs[](../DocCodeSamples.Tests/BakingExamples.cs#DependenciesBaker)]

This example reacts to potential changes and different parts of the code create the required dependencies in the following ways:

* If you change any value in the authoring component, the baker is automatically triggered. You don't need to express any explicit dependency  from the baker code to do this.
* If the asset that the authoring component references goes missing, then comes back, the calls to `DependsOn` triggers the baker. This dependency isn't automatic because the data stored in the authoring component doesn't change: it's something referenced by this data which comes in and out of existence (which is known as a "fake null" in Unity). This dependency is only required when data is extracted through those references. In this example, this is the position from the other GameObject and the vertex count from the mesh. If the reference was only carried over without accessing data through it, the dependency isn't necessary because the baker doesn't process anything differently.
* `GetComponent` also registers a reference on the existence of and the data of the required component. If that component is missing, `GetComponent` returns `null` but it still registers a dependency. This way, when the component is added, the baker is triggered.

## Additional resources

* [`Baker` API documentation](xref:Unity.Entities.Baker`1)
* [Baking overview](baking-overview.md)
