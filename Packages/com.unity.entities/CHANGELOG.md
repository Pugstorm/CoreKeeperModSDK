---
uid: changelog
---

# Changelog


## [1.2.4] - 2024-08-14

* Debug proxies (used by external debuggers) were sometimes using invalid field offsets when inspecting structs in blob assets. This led to incorrect values being reported in debugger watch windows. In particular, this would be triggered by the use of bool fields in blob asset structs.
* Entity version numbers could go back to 1 after reallocation in some edge cases.
* When building a content update, a temporary path was getting created in the drive root instead of the Library folder.  This would also cause content update builds to grow in size every time they were built.  The folder is now created in the Library correctly.
* Error in build when sprites are contained in subscenes has been removed.
* Regression in compilation time with assemblies with lots of system methods.
* EntityComponentStore leaked memory during domain reload.


## [1.2.3] - 2024-05-30

### Fixed

* Queries are now correctly constructed using `EntityQueryBuilder` when using `[WithDisabled(T)]` on `IJobEntity` with `EnabledRefRW<T>` and/or `RefRW<T>`.
* Cross-world selection in Entities Hierarchy
* Entities windows do not throw exceptions anymore when installing or removing packages from package manager.
* Fixed crash on quit in players due to incorrect RuntimeContentManager cleanup timing.
* respond to much docs feedback
* Fixed a regression which caused subscene section bounding volumes to be computed incorrectly.
* `EntityCommandBuffer.AddComponentForLinkedEntityGroup()` and `EntityCommandBuffer.SetComponentForLinkedEntityGroup()` now correctly process all entities that match the provided `EntityQueryMask`, even if new archetypes matching the query were added earlier during command buffer playback.
* Creating a cycle with `[UpdateInGroup]` attributes now throws an exception instead of crashing the Editor.


## [1.2.1] - 2024-04-26


### Changed

* Updated Burst dependency to version 1.8.13
* Updated entities packages dependencies

### Fixed

* The entities hierarchy view would sometimes throw exceptions when entities were destroyed.
* remove use of UNITY_64 define, as is can not be reliably used to determine 64 bit nature of platforms. Fixes crashes related to pointer truncation.
* The inspector no longer shows stale content when exiting play mode while locked in Authoring mode on a GameObject outside of a subscene.
* Systems now correctly track the implicit read dependency on enableable components in their queries' `Any` lists. These components are optional, but evaluating a query with optional enableable components requires reading these components' enabled state, which requires a read dependency even if nothing else in the system directly manipulates these types.
* `EntityManager.SetComponentEnabled<T>(EntityQuery,bool)` now sets the change version for the target type `T` on all affected chunks.
* `CompanionGameObjectUpdateTransformSystem` expected entities to have the `LocalToWorld` and `CompanionLinkTransform` components, but did not explicitly require them in its query.


## [1.2.0] - 2024-03-22

### Added

* Document counterintuitive behavior when an `EntityQueryMask` is created from an `EntityQuery` that includes a `WithNone<T>()` constraint, where `T` is an `IEnableableComponent`.

### Changed

* Performance improvements in `LocalToWorldSystem` when processing entities with non-dirty hierarchies and no children.

### Deprecated

* `EntityManager.CopyEntities()` is now deprecated, and will be removed from the public API in a future package release. We're not aware of any use cases for this function outside of the Entities package itself; `EntityManager.Instantiate()` is the correct way for higher-level code to instantiate copies of existing entities.

### Fixed

* BakingAnalytics no longer initializes TypeManager [InitializeOnLoad]
* Fixed issue where breakpoints in jobs defined after systems with SystemAPI usage are not hit.
* Fixed an error where the Entities Hierarchy window was accessing a destroyed world during UI bindings.
* Using SystemAPI.GetComponentRO/RW with "using static SystemAPI" within an Entities.ForEach used to confuse the code generator and failed to compile.
* Replace `HierarchyNodeMap`'s indexer setter with explicit add/update logic.
* an issue with hybrid entity baking regarding scales with different signs
* Entities Hierarchy now removes all nodes of a scene and subscenes when the scene is unloaded.
* Exception thrown when entering a prefab from the Entities Hierarchy
* Fixed: Memory leak in certain circumstances in Entity Scene Streaming
* Improved error message for when `SystemAPI.QueryBuilder` users forget to call `.Build()`.
* An assert when using AddMultipleComponentsDuringStructuralChange with more than 10 entities in some cases
* Add filter button was not showing filters in Archetypes and Journaling windows.
* An invalid range check caused the removal of entities from the hierarchy view to sometimes fail in perfectly valid cases.



## [1.2.0-pre.12] - 2024-02-13

### Added

* A file with the code for both snippets in the custom transforms documentation was added to the "DocCodeSamples.Tests" folder.
* The Create Menu now offers ScriptTemplates for the IComponentData, ISystem, IJobEntity and Baker types under Assets/Create/Entities

### Changed

* Fixed infinite loop that could occur due to concurrent use of non-concurrent Dictionary in Aspect Generator.
* Reduce the set/restore frequency of the fixed rate system group allocator.
* Various performance improvements in baking. Baking mesh-heavy scenes now takes 70% of what it did before.
* Significant performance improvements when creating archetypes and entity queries in Worlds with a large number of existing archetypes/queries.
* Updated Burst dependency to version 1.8.12

### Fixed

* EntityQuery singleton methods now correctly handle cases where the query contains enableable components. Note that `GetSingletonEntity()` and `TryGetSingletonEntity()` still can not be used on queries with enableable components, and that the singleton component itself can not be enableable. Both constraints may be lifted in a future release.
* A broken link to a code snippet in the documentation for custom transforms was fixed.
* Archetype window and Entity Memory Profiler module UI initialization.
* Making a player build with define `UNITY_DOTS_DEBUG`, while using an IJobEntity using RefRO/RefRW no longer compile errors!
* Clarified documentation for cleanup components
* Fixed minor memory leak in content delivery system.
* AABB.Contains.
* The entities hierarchy view would sometimes throw exceptions when entities were destroyed.
* remove use of UNITY_64 define, as is can not be reliably used to determine 64 bit nature of platforms. Fixes crashes related to pointer truncation.

## [1.2.0-pre.6] - 2023-12-13

### Changed

* Promotion preparation


## [1.2.0-pre.4] - 2023-11-28

### Changed

* Updated Burst dependency to 1.8.10


### Fixed

* Fixed: NullReferenceException when doing GetComponent in a baker of a monobehaviour that the Editor failed to load the associated script.
* Defensive fix for rare HierarchyWindow.OnCleanup exception (typically when breakpoint debugging).
* The `Unity.Scenes.Editor.Tests.LiveLinkBakingEditorTests.LiveConversion_ReEnableEntityHierarchy_WhenParentGameObjectIsReEnabledFromSavedSubScene` test no longer fails.
* An `EntityQuery` that uses `WithNone<T>` with an enableable component `T` will now insert a read dependency on `T` into the query's system, so that jobs which enable/disable `T` are correctly included in the system's input dependencies.
* Background importing of subscenes using sections would occasionally throw an exception.
* Selecting entities and systems now works again.



## [1.2.0-exp.3] - 2023-11-09

### Added

* Baking user-errors now include additional Authoring GameObject context, to help you diagnose them.
* Analyzer and code fix for missing "ref" keyword before SystemState
* Serialization support for UnityObjectRef<> which works with the build system. This means an unmanaged IComponentData can reference Unity assets and it works in a build or from a loaded SubScene the same as with Managed components.
* IEntitySceneBuildAdditions Interface to force the addition of unreferenced SubScenes into a build.
* `ArchetypeChunk.SetComponentEnabledForAll()` method to enable or disable a component on all entities in the chunk. This is more efficient than calling `SetComponentEnabled()` on each entity individually.

### Changed

* Update package `com.unity.mathematics` from `1.2.6` to `1.3.1` version.
* Error message when attempting to schedule an IJobChunk while an exclusive entity transaction is active.
* obsolete `EventBase.PreventDefault()` removed or changed for `StopPropagation` in `2023.2` or higher.
* obsolete `FindObjectsOfType<T>()` changed for `FindObjectsByType<T>()` in `2023.2` or higher.
* Shortened file names for source generator projects and test verification results.
* Several `EntityCommandBuffer` commands will now correctly fix up Entity references within component values in recorded commands, to replace temporary entities created at record-time with the corresponding "final" entities created at playback time. These include commands that add/set unmanaged shared components, commands targeting an entity's `LinkedEntityGroup`, and `AddComponent<T>(NativeArray<Entity>, T)`.
* The minimum supported editor version is now 2022.3.11f1

### Deprecated

* `CopyAndReplaceEntitiesFrom` is unreliable and should be avoided. Entities are not the same across worlds anymore, so an optional remapping table can be passed to the function in order to look up which entity in the destination world corresponds a given entity in the source world.

### Fixed

* Fixed issue where Entities IL post-processors were causing output assemblies to be non-deterministic between compilations.
* Ensure consistency in how the `==`/`!=` operators and the `Equals()`/`GetHashCode()` methods in `ComponentType` are implemented.
* UnityException: Calls to "AssetDatabase.RegisterCustomDependency" are restricted during importing when saving the EntitiesClientSettings assets.
* Minor performance optimization to EntityQuery.ToComponentArray when used with a managed component type.
* Issue where Burst would fail to compile an Entities.ForEach or Job.WithCode, resulting in a runtime casting error.
* Journaling can now be enabled even if no default world initialization is used.
* You can now exit playmode while having an Entity with a buffer selected in the Entities Hierarchy, without getting any errors.
* Streaming hitches when using a lot of Companion Components (e.g. Lights) in a SubScene
* Fixed false-positive error check in `EntityCommandBuffer.AddComponent<T>(NativeArray<Entity>, T)`.
* Validation for world's existence before accessing within `EntitySelectionProxy`.
* `ArchetypeChunk.SetComponentEnabled(DynamicComponentTypeHandle)` will now throw an exception if the provided component type does not implement `IEnableableComponent`.
* An exception thrown during world deserialization could cause a memory leak.

### Security


## [1.1.0-pre.3] - 2023-10-17

### Added

* We have added `ReadOnlySpan` variants for `CreateEntity` and  `CreateArchetype` which means you can now create the two with burst. E.g. `EntityManager.CreateEntity( stackalloc[] { ComponentType.ReadOnly<Type1>(), ... } )`.

### Fixed

* Iterating through a generic type using `System.Query` now triggers an actionable error message informing users that doing so is unsupported.
* Nested `SystemAPI` invocations that previously trigger the `InvalidOperationException` no longer do so.
* `#if` directives surrounding `using` directives are now correctly handled during source generation.
* Source-generators now correctly patch methods that contain pointer parameters.
* Disabled components that are also used as changed filters are now backed by correctly generated queries.
* The `World` is now validated in the `EntityContainer` before the `EntityManager` is accessed. Meaning you can now exit playmode while having an Entity with a buffer selected in the Entities Hierarchy, without getting any errors.
* Avoid a potential `NullReferenceException` when creating an `EntityQuery` with multiple query descriptions.
* The component dependency manager now completes registered jobs in batches, instead of individually. This reduces the overhead of structural changes and other sync points.
* Managed component types with circular references to other managed types (e.g. TypeA contains a field of TypeB, and TypeB contains a field of TypeA.) previously could have non-deterministic StableTypeHashes causing issues when deserializing entities data. Note, this fix raises the serialization version for all `.entities` files, requiring all previously serialized entity data requiring to be reserialized as the StableTypeHashes generated will no longer match. **Fix requires Unity 2022.3.1f11 or greater to work properly in IL2CPP builds**
* Fixed several memory leaks in the package and its test suite.
* `ScratchpadAllocator.Dispose()` now fully restores the object to its uninitialized state.
* `EntityCommandBuffer` no longer leaks `DynamicBuffers` when using `PlaybackPolicy.Multiplayback`.
* `EntityCommandBuffer.Dispose()` no longer throws an exception when called on a command buffer that was not fully initialized.
* `World.DestroyAllSystemsAndLogException()` (called during `World.Dispose()`) no longer prematurely aborts its loop if a system threw an exception from its `OnDestroy()` method. This prevented all subsequent systems from being destroyed, leaking all of their resources.
* Scheduling `IJobEntity` instances using `IJobEntityExtensions` no longer triggers `SGICE002`.
* When selecting `Publish -> Content Update` in a project that uses the `Entities` package, the `DirectoryNotFoundException` is no longer thrown.


## [1.1.0-exp.1] - 2023-09-18

### Added

* Added a "Custom Transform System" folder in the assets folder of the "EntitiesSamples" project.
* missing documentation on search public api
* Enabled model assets to be baked as prefabs using EntityPrefabReference.
* Error DC0084 is generated when capturing a local variable in an Entities.ForEach that isn't used.
* Add GetSharedComponentIndexManaged API
* Added addition errors around improper use of SystemAPI methods with generic type arguments.
* Search Keyword registration for entities preferences and settings.
* `SystemAPI.Query<T>` now supports `WithSharedComponentFilterManaged<T>(T sharedComponent)` and `WithSharedComponentFilterManaged<T1, T2>(T1 sharedComponent1, T2 sharedComponent2)`.
* `CompleteDependencyBeforeRW(SystemState state)` and `CompleteDependencyBeforeRO(SystemState state)` are added to the public `Unity.Entities.IAspectCreate<T>` interface in order to faciltiate changes to source-generated code. Implementations of these methods, like all existing methods in `Unity.Entities.IAspectCreate<T>`, will automatically be generated on users' behalf by source generators.
* Added `IBaker.CreateAdditionalEntities` for creating multiple additional entities at once.
* filter to search with SharedComponent from within the Hierarchy Window
* filter to search with SharedComponent from within the Search Window
* New `EntityQuery` component type constraint: `Present` components are required to be present on a query's matching archetypes, whether or not they are enabled or disabled on individual entities. This constraint can be added in all the usual places -- `EntityQueryBuilder.WithPresent<T>()`, `EntityQueryDesc.Present[]`, the `[WithPresent(typeof(T))]` attribute on `IJobEntity`, etc.
* checks to see if an exclusive transaction is active while scheduling a job.
* Specific error when capturing a variable in `Entities.ForEach` that relies on relies on source generators (since there is no deterministic order between source generators, this can be an error).

### Changed

* Significantly improved the performance of `EntityManager.SetSharedComponent<T>(EntityQuery,T)` and `EntityManager.SetSharedComponentManaged<T>(EntityQuery,T)`
* `TypeManager.Initialize` will disable synchronous Burst compilation only during initialization of the TypeManager so that large synchronous compilation stalls when compiling function pointers can be avoided when iterating in the Editor.
* BlobBuilder is now partial
* CompanionGameObjectUpdateTransformSystem is now public
* ResetUpdateAllocator is now public
* SubSceneInspectorUtility is now public
* Batch primary entity creation in baking.
* `HasSingleton<T>()` and `TryGetSingleton<T>()` methods will now throw if they find >1 instance of `T`, instead of returning `false`. Having more than one instance of a singleton component indicates a bug in the program, and should fail more obviously.
* The visibility of the `EnabledBitUtility` class was changed from `public` to `internal`. This class was never intended to be part of the public API of the Entities package, and should not be used by application code.
* `EntityManager.AddChunkComponentData(EntityQuery,T)` no longer throws an exception if the component `T` is already present on any of the target chunks. Instead, the new value is assigned to the existing component. This matches the behavior of other AddComponent variants in the Entities package.
* Text for exception that occurs when an entity doesn't exist during EntityCommandBuffer playback.
* `IJobChunk` now allows indexed writes to native containers passed in the job struct. Only writes to the element at `unfilteredChunkIndex` are valid. To disable this check on a per-container basis, add `[NativeDisableParallelForRestriction]` to the relevant field in the job struct.

### Deprecated

* Deprecate GetSharedComponentDataIndex as it is a duplicate of GetSharedComponentIndex

### Removed

* Removed gizmo rendering logic for entities from C#, now this is handled natively in Unity.
* Marked `EntityManager.Instantiate(NativeArray<Entity> srcEntities, NativeArray<Entity> outputEntities)` as obsolete, with the intention to eventually remove it entirely.

### Fixed

* Code fix now available to rewrite offending code that trigger `CS1654` errors.
* Fixed a typo in the "LocalTransform" summary comment.
* Property drawer for arrays and lists of Weak(Object|Scene)Reference types
* Compilation with DISABLE_ENTITIES_JOURNALING works again.
* Ensure Component gizmos are rendered for GameObjects in SubScenes when rendered with Entities. That is when using Preferences->Entities->SceneView Mode -> Runtime Data.
* Added null checks in BlobAssetReferenceData properties to avoid crashing the engine while inspecting variables with the debugger.
* LocalTransform.FromMatrixSafe would throw exceptions for valid matricies
* IJobEntity overwriting files due to colliding filenames.
* Differ discards ChunkComponents added during baking.
* Fixed issue with BakingVersion(true) triggering warnings in the console log about the attribute being missing on the type you placed this on in cases where named BakingVersion attributes are being used in the containing assembly.
* A source generator error is not thrown anymore when using the fully qualified name of `SystemAPI.Time` (e.g. `Unity.Entities.SystemAPI.Time`).
* The main entity in LinkedEntityGroups were not added in incremental baking.
* Fix memory leak in the BakerEntityUsage not disposing properly the list of ReferencedEntityUsage
* ArgumentException on an unknown type when using the GetComponent API in a baker with an abstract type.
* Subscenes no longer redundantly rebake on recompile due to type order changes.
* Serialization of blob asset references in unmanaged shared components
* `SystemGenerator` in the source-generation solution runs in ~48% less time when tested on a small game project shared by one of our users.
* You now no longer get a compile error for methods containing SystemAPI, EFE, or IJE scheduling, that include a signature with nullables, multiple generics, or parameter modifiers.
* KeyNotFoundException thrown by the Entities.Editor.HierarchyWindow when loading a new gameobject scene
* Fixed issue where ambiguous types used in a `SystemAPI.Query<T>()` call would generate a compiler error from source generators.
* Entities Hierarchy: when entering playmode without fast enter playmode the hierarchy was showing the authoring datamode content even though the switch in the window header was showing the mixed datamode.
* Validation for world's existence before accessing within EntityContainer and EntitySelectionProxy.


## [1.0.16] - 2023-09-11

### Added

* checks to see if an exclusive transaction is active while scheduling a job.

### Changed

* Clarified in TransformHelpers API documentation that matrices are expected to be affine.
* Updated Burst dependency to version 1.8.8

### Removed

* Alignment attribute is removed when displaying component attributes in Inspector window.
* Changelog entry for * Added `EntityCommandBuffer.MoveComponent<T>(Entity src, Entity dst)` this will be added in an upcoming minor version.

### Fixed

* Companion objects appearing in the GameObject hierarchy for a frame
* `SystemAPI.Query<EnabledRefRW<MyEnabledComponent>>()` can now be used with `.WithNone<MyEnabledComponent>()`, `.WithAny<MyEnabledComponent>()` and `.WithDisabled<MyEnabledComponent>()`.
* "AssetDatabase.RegisterCustomDependency are restricted during importing" exception thrown by the EntityClientSetting in the OnDisable method, when using 2023.2 or newer.
* The entities journaling window will no longer show an error when a system handle cannot be resolved; this is not an error and simply means that no system was executing when that event was recorded.
* Search window for Systems now shows results from all the worlds instead of default ones.
* Special handling for prefab entity instantiated from prefab asset at runtime.
* Entities Hierarchy: fix an issue that can happen when copying scene entities from different worlds.
* Entities Hierarchy: Fix memory leak in hierarchy backend.
* Entities Hierarchy: Only remove subscene nodes when they are actually not used anymore.
* Fixed `isReadOnly` being ignored in `EntityManager.GetBuffer`.
* Save unsaved opened scenes in the Editor before building a player. (fix issue: Error building Player: InvalidOperationException: ContentCatalogBuildUtility.BuildContentArchives failed with status 'UnsavedChanges')
* Fixed memory leak in some cases when an `EntityCommandBuffer` containing `DynamicBuffer` commands was disposed before it was played back.
* `World.AddSystemManaged<T>(T system)` no longer throws an exception if the system type `T` is not registered. Instead, it registers the type just in time. This matches the existing behavior of `World.CreateSystemManaged()`.
* Fixed a hash mismatch on DependOnParentTransformHierarchy
* Users can now specify duplicate components in the same `IJobEntity.Execute()` method, insofar as exactly one of them is wrapped in `EnabledRef<T>`.
* You can now use SystemAPI.GetBufferTypeHandle and SystemAPI.GetSharedComponentTypeHandle with unspecified types coming from systems. Like a `MySystem<TUnspecifiedType>` using SystemAPI.GetBufferTypeHandle<TUnspecifiedType>.


## [1.0.14] - 2023-07-27

### Added

* Obsolete API containing `evt.PreventDefault()` for `evt.StopPropagation()` in IListElement.cs.
* Added a dependency on the com.unity.test-framework.performance package
* Clean up for ComponentSystemGroup
* Early null entity check in ECB commands
* More error logging in DotsGlobalSettings
* Error logging in UpdateLoadOperation()

### Changed

* Updated Burst dependency to version 1.8.7
* Replaced an erroneous NUnit.Framework dependency in EntitySceneBuildUtility from an Assert.Equal() call with UnityEngine.Assertions
* Obsolete API containing `FindObjectsOfType<>()` for `FindObjectsByType<>(FindObjectsSortMode.None)` in SubSceneInspectorUtility.cs and `FindObjectOfType<>()` for `FindFirstObjectByType<>()` in LiveConversionEditorPerformanceTests.cs and LiveConversionEditorTests.cs.
* Increased allocation size in RuntimeContentManager initialization
* More informative error message in WriteSceneHeader()


### Removed

* Alignment attribute is removed when displaying component attributes in Inspector window.

### Fixed

* Avoid unnecessary sync point in `EntityCommandBufferSystem.OnUpdate()` if no command buffers were recorded.
* Adding and removing shared components and tag components to an entire chunk now correctly updates the per-component order versions. This most commonly manifested as errors in the element order of the data returned by `EntityQuery.GetTransformAccessArray()`.
* Use default inspector when an asset is selected regardless of the data mode.
* Assertion failure when undoing entity selection is fixed.
* Not being able to access Entity item from tuple when using `SystemAPI.Query` and `WithEntityAccess`.
* `BurstCompatibleAspect` is now `readonly`, as required by `IAspect` implementations.
* VisualElement null exception upon entities tooling windows' opening is fixed.
* Using `EnabledRefRW` and `EnabledMask` to set enableable component state to its existing value (e.g. disabling a component that's already disabled) no longer causes an "internal consistency check" failure when the EntityManager is disposed.
* Fixed potential uninitialized memory access when creating an `EntityQuery` with multiple `EntityQueryDesc` elements.
* Components tab update will check the target's existing state first.
* Check windows' initialization state before any actions in `OnDisable()`.
* Crash in EntityManager.SetArchetype() when the new archetype is the same as the current one.
* crash in BlobAssetReference
* Crash in BakingSystemFilterSettings
* Clarified documentation for the unusual semantics of `EntityManager.SetComponentEnabled(EntityQuery)`, which ignores the current status of enableable components and processes all entities in all of the query's matching chunks.
* Better error messages when `EntityManager`'s internal consistency checks fail at shutdown.
* `EntityManager.MoveComponent<T>(Entity src, Entity dst)` now throws if `dst` already has the component `T`. This case has never been supported; previously, the existing value would be quietly leaked.
* Fixed stack overflow that was the result of circular dependency data in the content.


## [1.0.11] - 2023-06-19

### Added

* The new `EntityQueryOptions.IncludeMetaChunks` flag allows queries to match archetypes with the `ChunkHeader` component (which is excluded from queries by default).

### Changed

* Idiomatic `foreach` and `Entities.ForEach` will now only sync jobs read/writing to component data the foreach will iterate over when the underlying EntityQuery used by the foreach indeed has entities to iterate over. Previously jobs would be unilaterally sync'd when using these constructs which could create stalls on the main thread on jobs that did not need to occur.
* Significantly improved the performance of `EntityCommandBuffer.AsParallelWriter()` and `EntityCommandBuffer.Dispose()`.
* Change component attribute name from `Alignment in Chunk` to `Component Type Alignment in Chunk` when displaying component attributes in Inspector window.
* LinkedEntityGroup internal buffer capacity set to 1.

### Deprecated

* `EntityCommandBuffer` methods which target an `EntityQuery` now take a new `EntityQueryCaptureMode` parameter, used to specify whether the provided query should be evaluated at record time (immediately) or at playback time (deferred). `.AtRecord` matches the existing behavior for compatibility, but `.AtPlayback` is up to 200x faster for some commands. The variants which do not include this extra parameter have been deprecated, but their existing behavior and semantics are preserved. The safe and easy fix is to add `EntityQueryCaptureMode.AtRecord` to all call sites; however, users are encouraged to review all call sites to see if the faster `.AtPlayback` mode would be appropriate.

### Removed

* Alignment attribute is removed when displaying component attributes in Inspector window.

### Fixed

* Updated code samples in documentation for building content archives and content delivery.
* Systems window's tree view indents are now enforced to the proper width.
* Systems window's world and namespace columns are now left aligned.
* You now can register generic ISystems so that they can be discovered and created automatically with world creation, or created manually via CreateSystem. Register each generic instance of them with `[assembly: RegisterGenericSystemType(typeof(YourGenericSystem<YourParticularType>))]` to allow such usage.
* Entities Hierarchy: Fix an exception happening when dragging a gameobject over a subscene node.
* Entities Hierarchy: Disable Empty Scene menu item when creating new subscenes where the main scene is not saved.
* Entities Hierarchy: fix a missing dispose in change collectors.
* Current selection is cleared if an object is selected outside Systems window.
* Current selection is cleared if an object is selected outside Components window.
* Current selection is cleared from Components window when entering/exiting play mode.
* Native memory leak when creating EntityQuery with EntityManager.CreateEntityQuery
* Ensures both readers and writer dependencies are completed when accessing read/write components in certain code paths
* Fixed a crash happening in `EntityCommandBuffer.Dispose` due to a use-after-free bug
* `TypeManager` methods such as `GetSystemName` previously could crash after adding new system type information at runtime due to the `TypeManager` referring to invalid memory.
* Bursted generic ISystems defined in one assembly and registered in another no longer break compilation.


## [1.0.10] - 2023-05-23

### Added

* Write a WebGLPreloadedStreamingAssets.manifest file into the Library/PlayerDataCache folder for webgl builds containing all files in the streaming asset folder to be preloaded at runtime by the webgl player build program.
* Added IsReferenceValid property to EntityPrefabReference and EntitySceneReference.
* Added `EntityManager.AddComponentData<T>(SystemHandle, T)` for managed components.

### Changed

* Significantly optimized bulk-structural change operations in `EntityManager`, including:
* `EntityManager.AddComponent<T>(EntityQuery)`
* `EntityManager.AddComponent(EntityQuery, ComponentType)`
* `EntityManager.AddComponent(EntityQuery, ComponentTypeSet)`
* `EntityManager.RemoveComponent<T>(EntityQuery)`
* `EntityManager.RemoveComponent(EntityQuery, ComponentType)`
* `EntityManager.RemoveComponent<T>(EntityQuery, ComponentTypeSet)`
* `EntityManager.AddSharedComponent<T>(EntityQuery, T)`
* `EntityManager.AddSharedComponentManaged<T>(EntityQuery, T)`
* `EntityManager.DestroyEntity(EntityQuery)` is now up to 4x faster in release builds.
* Added analyzer to detect several ways an "Entities.ForEach" or  "Job.WithCode" chain can be malformed.


### Removed

* Entities.ForEach and Job.WithCode now generate an error if their lambda contains other lambdas or local functions. This never fully worked or was fully implemented.  Instead, try using APIs, like SystemAPI.Query, IJobEntity and IJobChunk. These all work with both local functions and lambdas.

### Fixed

* SystemAPI methods now work inside of partial methods.
* All aspect generated type references to their full name beginning with `global::` (Type Shadowing)
* Do not generate aspect from type with shadowed IAspect type
* Fixed sourcegen issues with user types and namespaces that conflict with Unity and Entities types.
* Identically named types in different namespaces no longer trigger `CS0128` and `CS1503` when used as parameters in `IJobEntity.Execute()`.
* If you update multiple packages, create a new section with a new header for the other package.
* Assets loaded in edit mode through the content system are not forcibly unloaded.
* LocalToWorld.Transform now works with nonuniform scale
* Introduce clear error message (`SGJE0023`) when parameter types in `IJobEntity.Execute()` methods are less accessible than the `IJobEntity` types in which they are used.
* `EntityCommandBuffer.AddComponent<T>(Entity)` for managed `T` no longer leaves the managed component store in an invalid state.
* The constructor for `ComponentTypeSet` no longer throws an exception if the provided list of component types is empty.
* `EntityManager.AddSharedComponent<T>(EntityQuery,T)` and `EntityManager.AddSharedComponentManaged<T>(EntityQuery,T)`now set the shared component `T` to the provided value, even if the target chunk already has component `T`. This changes makes this method consistent with other "add component and set to value" operations. *All existing call sites should be reviewed to ensure that they're not relying on the function's previous behavior!*
* `EntityManager.DestroyEntity(EntityQuery)` had an undocumented constraint: if any of the target entities have a `LinkedEntityGroup` buffer component, the entities in that buffer must also match the target query. This constraint is now documented, and consistently applied in all code paths of this function.
* You now can register generic ISystems so that they can be discovered and created automatically with world creation, or created manually via CreateSystem. Register each generic instance of them with `[assembly: RegisterGenericSystemType(typeof(YourGenericSystem<YourParticularType>))]` to allow such usage.


### Known Issues

* Some errors are surfaced when importing the Entities package into a 2D project. To bypass this issue, restart the editor once the package has been imported.


## [1.0.8] - 2023-04-17

### Added

* TypeManager.IsSystemTypeIndex was made internal as this function should not be needed as part of the public API.
* Added `bool EnabledBitUtility.TryGetNextRange(v128 mask, int firstIndexToCheck, out int nextRangeBegin, out int nextRangeEnd)` as a replacement for `bool EnabledBitUtility.GetNextRange(ref v128 mask, ref int beginIndex, ref int endIndex)`. All usages of the latter method have been updated and the latter method deleted.
* `bool EnabledBitUtility.TryGetNextRange(v128 mask, int firstIndexToCheck, out int nextRangeBegin, out int nextRangeEnd)` is introduced as a replacement for `bool EnabledBitUtility.GetNextRange(ref v128 mask, ref int beginIndex, ref int endIndex)`. All usages of the latter method have been updated and the latter method deleted.
* Roslyn analyzer to detect and produce a warning for missing BurstCompile attributes on types containing methods marked with the BurstCompile attribute.
* Message in subscene component to clarify the behaviour of opened subscenes in playmode
* "using" document for TransformHelpers
* missing inverse transform operations in TransformHelpers.
* Use ComponentSystemGroup.GetAllSystems to get the list of systems in update order that live in that group.
* SystemTypeIndex is now the preferred way to refer to systems, rather than using reflection and Type. Many methods now have versions that take a SystemTypeIndex or a NativeList<SystemTypeIndex>, and these avoid unnecessary reflection.
* Property drawers for EntitySceneReference and EntityPrefabReference
* Write a StreamingAssetFiles.manifest file in the build folder of webgl builds containing all files in the streaming asset folder.

### Changed

* Rename Unity.Entities.Transform.Helpers to  Unity.Entities.Transform.TransformHelpers.
* Burst compiled moving unmanaged shared components during integration of subscenes into the target world, which improves streaming performance
* If you use JetBrains Rider, you will now get an error for not using the returned JobHandle of an IJobEntity. This is done to clearly distinquish `IJobEntity.Schedule()` from `IJobEntity.Schedule(default(JobHandle)`. As for the latter, where a JobHandle is returned, you should handle whether you want to do complete the job then and there e.g. `handle.Complete()`. Or continue the chain e.g. `state.Dependency = handle` .
* ComponentSystemGroup.Systems renamed to ManagedSystems, and introduce GetUnmanagedSystems if you only want the unmanaged ones.
* Reduced the amount of memory allocated by allocating based on the maximum number of worker threads the running platform requires rather than defaulting to using a theoretical upper-bound of 128 worker threads.
* You can again have an unlimited number of Create/Update Before/After attributes.
* Includes unit tests.
* No documentation changes or additions were necessary.
* Updated Burst version to 1.8.4
* Updated Serialization version to 3.1.0

### Removed

* Removed ability to get/convert a RefRW as read-only status.  Please use RefRO instead, which should have more type safety.
* Removed ability to an Aspect with read-only status.  It is possible to create another Aspect type that wraps a read-only aspect that communicates read-write access (and maintains type safety).
* Warning about entities with no SceneSection not being serialized.

### Fixed

* Allow components to contain NativeContainers whose element type is or contains a NativeContainer. Previously the TypeManager would throw during initialization if a component contained a a nested NativeContainer field. Note: NativeContainers still cannot be verified to be safely accessed when used in jobs. Thus, if a component contains a nested NativeContainer field, that component can only be accessed from the main thread.
* improved error message when `EntityQuery.GetSingleton()` fails
* Query window minimum size and scrolling behavior.
* Query window internal state initialization.
* Fixed a case where subscene importing could get stuck in some cases when an asset the subscene depends on changes midimport
* Add null checks in Systems window.
* Fixed a potential crash when loading managed component data in sub scenes on PS4 and PS5.
* Companion objects leaking in Editor when loading and unload Entity scenes containing them.
* `.WithSharedComponentFilter()` usages in different `Entities.ForEach` and `IFE` iterations no longer interfere with one another.
* ManualOverride in incremental baking
* Fix internal compiler error if event handler invocations occur in Entities.ForEach.
* Don't wait on synchronously compiling CompileFunctionPointer to load.
* Incremental baking with intermediate entities with TransformUsageFlags.None
* Entities Hierarchy: Potential initialization errors.
* Fix IJobEntityExtensions.Run(job) throwing an ICE.
* No more memory leaks caused by the generated code for `IJobEntity` types, since we are no longer allocating `UnsafeList`s which are never disposed of.
* RuntimeContentSystem was not getting updated when the default set of Worlds was changed
* Bake dependencies on Transform now depends on the hierarchy.
* Incremental baking with ManualOverride when a new component is added.
* Fixes small memory leaks that occurred during domain reload where static native allocations were not cleaned up.
* Fixes memory leak where entity scene catalogs would not be freed when the scene system is destroyed.
* Fixed issue where Entities.ForEach was always supplying burst compilation parameters even if you did not provide them.
* You can now schedule `IJobEntity` instances with custom queries insofar as these custom queries have all the components required for the jobs' `Execute()` methods to run.
* Allow Entities Hierarchy to be reloaded via the hamburger menu "Reload Window" menu item
* Fixed a memory leak that would occur when selecting Entities.
* Companion components with negative scale
* Fix misleading error message when invalid identifier is captured in Entities.ForEach.
* Fixed issue with a managed component in an IJobEntity with a ref/in keyword caused generated C# errors. Using in/ref now generates a valid error.
* When a ComponentSystemGroup calls OnStopRunning, ensure unmanaged systems also call OnStopRunning
* `Job.WithCode()` can now capture multiple local variables correctly.
* Systems with only `Job.WithCode` invocations no longer throw runtime exceptions.
* SystemAPI Issue where using a resolved generic like MyGenericComp<MyData> as a typeargument would give SGSA0001. You can now e.g. do `SystemAPI.GetBufferLookup<MyGenericElement<int>>`.
* Issue where EFE.WithStructuralChange have broken breakpoints and SystemAPI calls were unsupported. Note: those cases now also no longer support lambda expressions.
* Resetting a blob asset reference to null would sometimes lead to an invalid reference in the destination world during live baking.
* A relatively rare hash collision would happen if you had two unmanaged shared components that had the same exact data in them.
* Fixed the types the Drawers are assigning to the WeakReferencesIds.

### Known Issues

* Jobs using using components (such as `IJobEntity` job types or jobs containing `ComponentLookup` types) scheduled may be synchronized more often than necessary.
* `SystemAPI` methods do not work in partial or static methods.
* `Entities.ForEach` with both `WithStructuralChanges` and `SystemAPI` methods can cause exceptions to be thrown and no breakpoints to be hit.
* `RefRO` and `RefRW` parameters in `IJobEntity` that wrap types with the same name can cause compilation errors.
* Throwing an exception in an `Entities.ForEach` or `SystemAPI.Query` block will cause subsequent systems to also throw.
* Using local functions or lambdas inside of `Entities.ForEach` or `Jobs.WithCode` can cause invalid code-generation or run-time errors.
* It is not possible to view or debug generated code yet in Visual Studio (this does work in Rider).
* It is not possible to have an `IJobEntity` withthe same component wrapped in both an `EnabledRefRW`/`EnabledRefRO` and `RefRW`/`RefRO` types.



## [1.0.0-pre.65] - 2023-03-21

### Added

* Added support for automatic data mode switching to the Entities Hierarchy window and the Inspector window.
* Added BlobAsset safety check for passing blob assets into methods without using `ref`.
* Added the `LocalTransform.ComputeWorldTransformMatrix()` which synchronously computes an entity's world-space transform matrix, in the rare cases where an accurate world transform is needed in simulation code and is otherwise be unavailable.
* Added `RefRW<T> SystemAPI.GetComponentRW<T>(Entity,bool)`
* Bulk SetComponentEnabled operations on EntityManager: `EntityManager.SetComponentEnabled<T>(EntityQuery, bool)` and `EntityManager.SetComponentEnabled(EntityQuery, ComponentType, bool)`.
* A `Unity.Transforms.Helpers` class with assorted transform-related helper functions:
  * A simple `float4x4` extension methods for field extraction, such as `.Up()`, `.Forward()` and `.Translation()`
  * Added utilities to apply a transformation matrix to a `float3` point or direction, or to a `quaternion` rotation
  * A method to synchronously compute an entity's world-space transform, `.ComputeWorldTransformMatrix()`
  * A method to compute the "LookAt" `quaternion` rotation for a position that would cause its "forward" direction to point towards some target.
* `TypeIndex.IsChunkSerializable` property has been added to identify if a component type is valid in a chunk that is intended to be serialized. If `SerializeUtility.SerializeWorld` (such as might be called while exporting a subscene) is used to serialize chunks that contain components whose `TypeIndex.IsChunkSerializable` returns false, an exception will be thrown telling you why the component type is inappropriate for serialization.
* Added `WeakSceneReference Unload(Scene scene)` method to unload the scene instance and release its resources.
* Added guidance to GetSingleton error message
* Transform Usage Flags
* Added support for managed (shared) components serialization in DOTS runtime.

### Changed

* Moved the tool for adding missing `partial` keywords to system and job types from Edit &gt; Preferences &gt; Entities into a Roslyn codefix. Your IDE of choice should now be able to fix this for you, and give you a red squiggly line if it's missing.
* IJobEntity no longer gives a compile error if you have a reference type field. This improves iteration time, and has the added benefit that you can now write managed code in an IJobEntity. Simply add a managed component to your IJE's Execute (that forces running without the jobsystem). Your job can now validly use that field. If you try to schedule this job rather than running it on the main thread, you'll correctly get thrown a runtime error for having a reference type in your job.
* Improved performance of IJobEntity generator, speeding up compile times. Attributes like WithAll, WithAny etc. now use syntax information directly. This mean that you can't make your own attribute on an IJobEntity named `WithAll` `WithAny`, `WithNone`, `WithDisabled`, `WithAbsent`, `WithOptions`, or `WithChangeFilter`.
* Updated Burst dependency version to 1.8.3.
* What was PostTransformScale as a float3x3 is now PostTransformMatrix as a float4x4. This is more general and offers maximum flexibility. You can, for example, use it to scale from a secondary pivot.
* ParentSystem removes the Parent component if said component points to an entity that doesn't exist anymore.
* Refactored how additive scenes are handled within the Runtime Content Manager. A scene is now returned, and that is used as the key to unload. This change required some API changes.
* Changed `WeakObjectSceneReference.LoadAsync` to return the Scene instance, which should be used to check the loading status and for unloading.
* Changed `RuntimeContentManager.UnloadScene` method to take the Scene instance as the only parameter.
* The BlobAssetStore used during baking now uses garbage collection instead of an explicit refcount. It is not required anymore to register blobs with authoring GameObjects nor to do explicit cleanup in incremental baking systems.
* Source generators for Systems and Aspects no longer default to outputting generated files in `Temp/GeneratedCode/***`. To turn it on, add `DOTS_OUTPUT_SOURCEGEN_FILES` to your Scripting Defines in Player Settings. Turning it on will cost compilation time. (The source generator for IJobEntity already made this change earlier.)
* Moved InternalCompilerInterface, EntityQueryEnumerator (now InternalEntityQueryEnumerator) and some other types and methods to the Unity.Entities.Internal namespace.  These types and methods are not intended for use by user code. We would make them internal, but source generators won't work correctly that way unfortunately.

### Deprecated

* Deprecated `WeakSceneReference` Release method. Unload should now be used and the scene instance returned by LoadAsync needs to be passed in as a ref.
* `RegisterBindingAttribute(Type runtimeComponent, string runtimeField, bool generated)`. Vector type fields can now be registered automatically without the `generated` option.
* SceneSystem.UnloadParameters and the overload of SceneSystem.UnloadScene receiving SceneSystem.UnloadParameters as parameters.
* `EntityQuery.SetEnabledBitsOnAllChunks` as the only bulk operation on EntityQuery instead of EntityManager. Use the newly added bulk `SetComponentEnabled` overloads instead.
* WeakSceneReference properties LoadingStatus, SceneResult, SceneFileResult.
* RuntimeContentManager methods GetSceneLoadingStatus, GetSceneFileValue, GetSceneValue

### Removed

* `ENABLE_TRANSFORM_V1` define and existing transform v1 code. Transform v2 is now the only transform system.
* Tooling to re-write user files to add missing partial keywords to systems.
* The `TransformAspect` struct was removed. Recent changes to the Entities transform systems made the current implementation of `TransformAspect` much less useful, and we've decided to remove it from the package until we can provide a more valuable abstraction over the DOTS transform components.
* The `EntityQueryEnumerator.EntityCount` field has been removed from the public API. Note that `EntityQueryEnumerator` is only intended for use by DOTS source generators.
* `BlobAssetComputationContext` made internal.

### Fixed

* Assets loaded in edit mode through the RuntimeContentManager will no longer be unloaded.  They will be unloaded by the editor garbage collection.  This is to prevent unloading assets that may be in use in other parts of the project.
* Fixed memory leak in content loading system when scenes are unloaded before fully loading.
* IsReferenceValid now checks for the correct generation type and the existence of the referenced asset when called in the editor.
* Baker IEntitiesPlayerSettings were not setup correctly if the com.unity.platforms package was not installed/present in the project.
* IJobEntity now no longer caches the default query when scheduling with a dynamic query. For example. `new MyJob().Schedule();` will use the query matching its execute signature whereas `new MyJob().Schedule(myQuery)` will now only use myQuery. This is useful in cases like RequireMatchingQueriesForUpdate, where you don't want to accidentally create extra queries.
* Jobs implementing IJobEntity can now be created in one assembly and scheduled in another.
* The `[WithDisabled]` attribute when applied to a job implementing `IJobEntity` now overrides the implicit `All` query defined by the signature of `Execute`. E.g. `Execute(MyComp a)` and `[WithDisabled(typeof(MyComp))]` now defines a query of EntityQuery(all={}, disabled=MyComp). This is useful in cases where you want to enable all components of type X which are present, but disabled.
* `WriteGroup` support in transform v2 `LocalToWorldSystem` code should now work correctly.
* Fixed compilation issue with 23.1/23.2*
* Detection of circular ordering dependencies between systems is now correct.
* Chaining `EntityQuery` methods with bulk operation methods is now supported.
* Docs and samples for ECB systems now accurately reflect recommended usage. Fixed issue during `TypeManager.Initialize` where managed components with a field containing a circular type definition may throw `ArgumentException: An item with the same key has already been added.`
* Calling Release on a `WeakObjectReference<GameObject>` will no longer log errors in the editor.
* Zero-sized ("tag") enableable components were not always correctly enabled by default, when added to an entire chunk (such as via `EntityManager.AddComponent<EnableableTag>(query)`).
* Fixed issue with DotsGlobalSettings reporting the incorrect PlayType when switching from DedicatedServer to another standalone build target.
* Fixed TypeManager initialization causing a crash in the Entities Hierarchy.
* If you schedule an `IJobEntity` instance with a custom query that doesn't contain the components required for the `Execute()` method to run, a readable and actionable runtime exception is now thrown when safety checks are enabled.
* `EntityCommandBuffer.Dispose()` can no longer trigger a stack overflow when disposing large command buffers.
* Throw a readable, actionable compile-time error informing users that `RefRO<T>`, `RefRW<T>`, `EnabledRefRO<T>`,  `EnabledRefRW<T>`, `DynamicBuffer<T>` and `UnityEngineComponent<T>` may not be used with generic types.
* A `foreach` iterating over an `EntityQuery` with enableable components now iterates over the correct entities.
* Re-added obsolete baker functions
* The accidental exposure of package internals to "Assembly-CSharp" was reverted.
* Default the build system to use the client settings if the package com.unity.netcode is not installed when the active  platform is dedicated server.
* `Entities.WithStructuralChanges().ForEach()` now correctly handles enableable components.
* Allow components to contain nested native containers. Previously the TypeManager would throw during initialization if a component contained a a nested NativeContainer field. Note: NativeContainers still cannot be verified to be safely accessed when used in jobs. So, if a component contains a nested NativeContainer field, that component can only be accessed from the main thread.
* Entities Hierarchy correctly selects the subscenes
* Invalid entity warning in Inspector window with runtime data mode is only applied to entities or game objects that can be converted to entities.
* Issue with IJobEntity source-generators not getting re-run in IDE. This could cause Rider and Visual Studio to not be able to find/debug generated code for IJobEntity types.
* Adding managed components to entities via an `EntityCommandBuffer` on the main thread no longer triggers the `NullReferenceException`.
* Fixed an issue where entities with enableable components loaded from a subscene could reference the wrong component's enabled/disabled state.
* Fixed an issue where idiomatic foreach (IFE) would not iterate over all entities that matched its query, if the query contains enableable components
* Issue where recompilation would retrigger baking unnecessarily.

## [1.0.0-pre.47] - 2023-03-01

### Fixed

* Stripping (e.g. on IL2CPP) now won't strip whole assemblies that have important systems, like graphics.
* Generic systems created at runtime no longer break sorting functionality.


## [1.0.0-pre.44] - 2023-02-13

### Added

* Added `RegisterBindingAttribute(string authoringField, Type runtimeComponent, string runtimeField)` to provide better control when registering nested types in authoring components.
* RuntimeContentSystem.LoadContentCatalog allows for starting the content delivery and update process when ENABLE_CONTENT_DELIVERY is defined. The automatic update is no longer triggered when the applications starts.
* Streaming samples.
* RemoteContentCatalogBuildUtility.BuildContent helper method added to allow building player content without having to rebuild the player. This is needed in order for developers to create their own publishing workflow for remote content delivery.
* Added missing SceneSystem.UnloadScene prototype.
* Generic IJobEntity jobs are not yet supported. Added a proper error to indicate this instead of a compiler error.
* SceneSystem.UnloadScene(WorldUnmanaged world, Entity sceneEntity, bool fullyUnload)
* `ManagedAPI.GetComponentTypeHandle` now let's you get a typehandle to `Class IComponentData`.
* Baking systems from the excluded baking assemblies are also filtered out during baking.
* Two new categories of component types can be provided when creating an `EntityQuery`. Components in the `Disabled` list must be present on matching entities, but must be disabled. `Components in the `Absent` list must not be on the entity at all.
* Companion objects need to always instantiate since the content manager reuses loaded objects.
* A new WorldSystemFilterFlags called Streaming to identify all the systems involved in streaming.
* Add AlwaysIncludeBakingSystem internal attribute to run baking systems from excluded baking assemblies
* WorldSystemFilter to the runtime version of ResolveSceneReferenceSystem.
* DependsOnLightBaking can be called from bakers to register a dependency against light mapping data.
* Added support for managed (shared) components serialization in DOTS runtime.
* Debug only check to prevent disposal of blob assets managed by a blob asset store.

### Changed

* `ComponentTypeHandle` and `BufferTypeHandle` now more consistently use their cache of per-archetype metadata to accelerate common operations like `.Has<T>()`, `.DidChange<T>()` and `.GetNativeArray<T>()`.
* `ComponentLookup` and `BufferLookup` now more consistently use their cache of per-archetype metadata to accelerate common operations like `.HasComponent<T>()`, `.IsComponentEnabled<T>()` and `.SetComponentEnabled<T>()`.
* Upgraded to use Roslyn 4.0.1
* Added better support for vector type fields in `BindingRegistry`.
* Unmanaged shared components are serialized as blittable data.
* `ISystem` now doesn't need `BurstCompile` on the struct. To Burst compile a system, put BurstCompile on either `OnCreate`, `OnStartRunning`, `OnUpdate`, `OnStopRunning`, or `OnDestroy`.
* The "a system could not be added to group" error message now contains the name of the World affected for easier debugging
* Nested native containers are protected against in any type attributed with [NativeContainer]
* Unmanaged shared components are no longer boxed when collecting BlobAssetReferences.
* `EditorEntityScenes.GetSubScenes` was made public in order to gather subscenes to pass to the BuildContent API.
* `EntityManager.GetAllUniqueSharedComponents` now takes an `AllocatorManager.AllocatorHandle` instead of an `Allocator` enum parameter allowing for custom allocators to be used when allocating the `NativeList<T>` return value. `Allocator` implicitly converts to `AllocatorManager.AllocatorHandle` so no action is required to call the changed API.
* IJobEntity refactored to IncrementalGenerator.
* IJobEntity now doesn't default to outputting generated files in `Temp/GeneratedCode`. To turn it on use `DOTS_OUTPUT_SOURCEGEN_FILES`. Turning it on costs compilation time.
* Replaced .Name with .FullName for duplicated component message in baking.
* ManagedComponents and Shared Managed Components can now be Scheduled in IJobEntity (ScheduleParallel still not allowed. Runtime error will be thrown if you try.)
* Invalid entities now show their index and version when viewed in the inspector
* In Bakers AddTransformUsageFlags now takes an entity instead of a GameObject or a Component.
* Baking log output to be more succinct.
* In cases where there are no attributes constraining the order of system creation, updating, and destruction, the order will have changed to enable optimizations. If you have mysterious bugs after this update, check for missing [CreateAfter], [CreateBefore], [UpdateAfter], and [UpdateBefore] attributes.

### Deprecated

* Deprecated `RegisterBindingAttribute(Type runtimeComponent, string runtimeField, bool generated)`. Vector type fields can now be registered automatically without the `generated` option.
* SceneSystem.UnloadParameters and SceneSystem.UnloadScene receiving SceneSystem.UnloadParameters as parameters.

### Removed

* SourceGen no longer outputs .cs files in `Temp/GeneratedCode` by default, because most IDEs such as Rider and Visual Studio support SourceGen output. If you want to emit the output (at the cost of significant compilation time), use the `DOTS_OUTPUT_SOURCEGEN_FILES` define.
* From Unity Editor version 2022.2 and later, the **Auto Generate** mode in the Lighting window is unavailable with the Entities package. This is because when you generate lighting in a project, the Unity Editor opens all loaded subscenes, which might slow down Editor performance. On demand baking is still available and is the recommended way of generating lighting.
* Tooling to re-write user files to add missing partial keywords to systems.
* Removed `IIsFullyUnmanaged` due to obtrusiveness when compilation fails. Instead gives runtime error when incorrectly scheduling managed IJobEntity.

### Fixed

* A selection issue with keyboard arrow keys in Entities Hierarchy window.
* GetComponent on Transform triggers now a rebake when the gameobject is reparented.
* Removed unnecessary test assemblies from always being loaded in projects.
* In DOTS Runtime, shared components containing managed references could generate incorrect results when calling `TypeManager.Equals` and/or `TypeManager.GetHashCode`. We also now reinforce that all shared components containing managed references must implement `IEquatable<>`
* Using `EnabledRefRO<T>` and `EnabledRefRW<T>` parameters in `IJobEntity.Execute()` with zero-sized enableable components is now supported.
* The transform system no longer increments the change version of `WorldTransform` and `LocalToWorld` on all world-space entities every frame. This prevents entity hierarchies from being processed redundantly, even if their root entity had not moved since the last update.
* Fixes lightmaps for player builds
* Creating multiple additional entities in a baker now scales linearly
* Fixed the behavior of EntityRemapUtility.HasEntityReferencesManaged for types where Entity or BlobAsset reference fields are succeeded by two or more strings.
* Setting a shared component to its default value using an EntityCommandBuffer could cause the default value to be duplicated and this would prevent query filtering from working correctly.
* Using `EnabledRefXX<T>` and `RefXX<T>` wrappers on the same component in the same `IJobEntity.Execute()` method no longer throws compiler errors.
* It is now possible to force reimport a single or multiple subscenes from the inspector when the last import of the subscene failed.
* Fix SubScene issue with File -> Save As when having a SubScene as the Active Scene.
* `EntityQueryEnumerator.MoveNextEntityRange()` now consistently returns the correct `entityCount` value.
* IJobEntity compile error when using an aspect's lookup type as field.
* Invalid subscene nodes issues in Entities Hierarchy window.
* Some `EntityManager` methods (including `RemoveComponent()` were not calling their Burst-compiled implementations.
* Badly formatted error messages when they are emitted while in burst-compiled code.
* Performance issue in Entities Hierarchy when opening large subscenes.
* An `EntityCommandBuffer` containing references to managed components will no longer throw an exception if it is disposed from a Burst-compiled context.
* Errors for DynamicSharedComponentTypeHandle were reporting as DynamicComponentTypeHandle
* Entities Hierarchy reset properly after test runner execution.
* Fixed a small memory leak that would occur when calling `EntityManager.GetAllUniqueSharedComponents` with an `unmanaged` component `T` type.
* Improve subscene handling in Entities Hierarchy
* Stack overflow in source generators when IComponentData has a cycle.
* Fix a NullReferenceException happening in the BakingFilterSettings when an assembly definition added in the excluded list of baking assemblies is missing.
* First cross reference to section 0 being lost.
* Fix missing subscene sections to load and unload from the subscene inspector.
* Aspect generator cache flush. Nested aspects no longer cause compilation error when their fields are changed.
* Added implicit dependency on transform hierarchy for transform bakers.
* Section 0 unloading is delayed until all the other sections are unloaded.
* Loading scene sections with `BlockOnStreamIn` failed if section 0 wasn't loaded first.
* Components window's list view item height is fixed to the correct value.
* IJobEntityChunkBeginEnd now doesn't update change version if `shouldExecuteChunk` is false.
* The detection of duplicate components during baking was failing between different baking passes.
* Issue with types used in codegen not being found if there if they exist in a more namespace name that aliases with a more immediate namespace name.
* The following `ArchetypeChunk` methods may now be invoked on zero-sized components without triggering any exception: `GetNativeArray<T>(ref ComponentTypeHandle<T> typeHandle)`, `GetComponentDataPtrRO<T>(ref ComponentTypeHandle<T> typeHandle)`, `GetComponentDataPtrRW<T>(ref ComponentTypeHandle<T> typeHandle)`, `GetRequiredComponentDataPtrRO<T>(ref ComponentTypeHandle<T> typeHandle)`, and `GetRequiredComponentDataPtrRW<T>(ref ComponentTypeHandle<T> typeHandle)`.
* Companion components with RequireComponent attributes used to potentially throw an exception during baking when a subset of those components was disabled. (e.g. this was the case with Lights)
* When multiple blob assets are recreated during baking and happen to recycle the same addresses and same hashes (but not the same hash for the same address), it would confuse the differ that patches the main ECS world with the results of live baking. This problematic configuration has been identified and fixed.
* Dependency.Complete() not working in systems' OnStartRunning and OnStopRunning
* Removed redundant dependency on the first transform of the hierarchy in baking.
* Crash when building a Dedicated Server and Netcode package not present.
* ComponentSystemGroup.SortSystems() taking many milliseconds for large number of systems.
* Fixed some spacing issues in the Entity inspector.
* `EntityManager.RemoveComponent(Entity, ComponentTypeSet)` and `EntityCommandBuffer.RemoveComponent(Entity, ComponentTypeSet)` no longer throw an exception if the target entity is invalid, for consistency with other RemoveComponent functions.
* Entities Hierarchy: Potential crash caused by race condition when gathering the changes to update the Entities Hierarchy.
* Entities Hierarchy potential crash when receiving GameObjects' OrderChanged events out of order.
* The Entities Structural Changes Profiler module should no longer cause memory corruption (and crash) when recording from jobs using exclusive entity transactions.
* The number for buffer count in Entity Inspector is not cut anymore.
* Primary entity is secured to be the first to show in the Preview window.
* Potentially incorrect remapping of references when using BlobBuilder to create large blobs. This would lead to corrupted blob data in rare cases.


## [1.0.0-pre.15] - 2022-11-16

### Added

* Support for serializing UnityEngine.AnimationCurves in managed components.
* Changing shared component data (managed or unmanaged) is now tracked by the entities structural changes profiler module.
* WorldUnmanaged.GetAllSystems
* Support for enabling or disabling components in the entity inspector, for components that derives from the `IEnableableComponent` interface.
* `TypeManager.TypeIndex` type providing type safety and improved debugging working with type indices given from the `TypeManager`.
* Missing docs for Scratchpad and UpdateAllocator public APIs.
* `ComponentTypeSet` now has a debugger type proxy, to show the list of components it contains.
* DotsPlayerSettings can provide their own set of custom scripting defines.
* UnityObjectRef<T> now implements IEquatable<UnityObjectRef<T>>.
* Support for `RefRW<T>`, `RefRO<T>`, `EnabledRefRW<T>` and `EnabledRefRO<T>` parameters in `IJobEntity.Execute()`.
* Added convenience methods for adding Chunk Components to an EntityQueryBuilder.
* Docs to provide an overview of prebuilt custom allocators.
* `SystemAPI.ManagedAPI.HasComponent`, `SystemAPI.ManagedAPI.GetComponent`, `SystemAPI.ManagedAPI.TryGetComponent`, `SystemAPI.ManagedAPI.GetSingleton`, `SystemAPI.ManagedAPI.TryGetSingleton`.
* Managed `EntityQuery.TryGetSingleton`
* `SystemAPI.Query<ManagedAPI.UnityEngineComponent<MyUnityEngineComp>>` support.
* `EntityQuery.TryGetSingletonRW` and `SystemAPI.TryGetSingletonRW`
* Workflow for preparing builds for publishing
* Workflow for preparing content updates for published builds
* Runtime functionality to download and update remote content
* Profiler module for the runtime content manager that tracks loading information.
* `SystemAPI.IsComponentEnabled`, `SystemAPI.IsBufferEnabled`, `SystemAPI.ManagedAPI.IsComponentEnabled` to get component enabledness from an entity. To do this in jobs, do so in ComponentLookup/BufferLookup.
* `SystemAPI.SetComponentEnabled`, `SystemAPI.SetBufferEnabled`, `SystemAPI.ManagedAPI.SetComponentEnabled` to set component enabledness from an entity. To do this in jobs, do so in ComponentLookup/BufferLookup.
* `RequireForUpdateWithSystemComponent` to SystemBase and ISystem to help explain that system components won't normally partake in queries without explicitly mentioning it.
* EntityQueryBuilder ChunkComponent calls to SystemAPI.EntityQueryBuilder for better symmetry.
* `ArchetypeChunk.Has<T>` and `ArchetypeChunk.HasChunkComponent<T>` for ease of checking (useful for `IJobEntityChunkBeginEnd`)
* `IJobEntityChunkBeginEnd` - allowing you to run code at the start and end of chunk iteration.
* `SystemAPI.GetXTypeHandle` to easily get cached and `.Update`'d handles :3
* Added `EntityCommandBuffer.ParallelWriter.SetEnabled(Entity,bool)` method, for parity with the main-thread interface.
* A isReadOnly field (with default arg) to `TryGetSingletonBuffer` so it matches its cousin `GetSingletonBuffer`
* The new tag component `PropagateLocalToWorld` must be added to any entity which needs its children to inherit its full `LocalToWorld` matrix, instead of the more compact `WorldTransform` representation. This path is slightly slower, but supports additional features like `PostTransformMatrix` (for non-uniform scale), and interpolation by the Physics and Netcode packages.
* Added `ArchetypeChunk.IsComponentEnabled(ref DynamicComponentTypeHandle)`.
* `SystemAPIQueryBuilder.WithAspect<T>()` so `SystemAPI` support the new `WithAspect<T>()` from `EntityQueryBuilder`
* Added `No aspects` message for Aspects tab in Inspector when no aspect is available for selected entity.
* Roslyn analyzer to report errors when SystemAPI methods and properties are used incorrectly (outside of system types, in static methods or specific method usage inside Entities.ForEach).
* Using `RefRO<T>` and `RefRW<T>` parameters in `IJobEntity.Execute()` with zero-sized components is now supported.
* Added support for sticky data mode to the Entities Hierarchy window and the Inspector window.
* Added support for automatic data mode switching to the Entities Hierarchy window and the Inspector window.


### Changed

* Entities package test components such as `EcsTestData` are no longer part of the package's public API; they are only intended for internal package testing.
* WorldUnmanaged.CurrentTime renamed to WorldUnamanged.Time
* WorldUnmanaged.TryGetSystemStateForId is now internal
* WorldUnmanaged.IsCreated is now public
* SystemState.ShouldRunSystem is now public
* Renamed the fields of `EntityBlobRefResult` to match the C# coding standard.
* Renamed the `DOTS Hierarchy` window to `Entities Hierarchy`.
* Renamed the `DOTS` sub-menu from the top-level `Window` menu to `Entities`.
* Renamed the `DOTS` section in the `Preferences` window to `Entities`.
* Changed the order of the items under `Window>Entities` to be deterministic.
* Moved the baking options to the DOTS Editor preferences page.
* EntityQueries created via EntityManager.CreateEntityQuery or EntityQueryBuilder.Build(EntityManager) will be owned by the EntityManager and be disposed by the EntityManager when it is destroyed.
* RuntimeContentManager API for loading and managing Unity engine objects loaded from Content Archives.
* `WeakObjectReference<T>` can be used to manage weak objects at runtime.
* Asset bundles are no longer build and look for referenced Unity objects. AssetBundleManager class has been removed.
* Bakers for base component types and decorated with `[BakeDerivedTypes]` are evaluated before bakers for derived component types.
* Renamed `EntityCommandBuffer.*ForEntityQuery` methods to be their singular overload equivalents `EntityCommandBuffer.*`. E.g. `EntityCommandBuffer.DestroyEntitiesForEntityQuery` is now an overload in `EntityCommandBuffer.DestroyEntity`. EntityCommandBuffer is now more in line with EntityManager.
* `EntityQuery.CalculateEntityCount(NativeArray<Entity>)`
* `EntityQuery.CalculateEntityCountWithoutFiltering(NativeArray<Entity>)`
* `EntityQuery.MatchesAny(NativeArray<Entity>)`
* `EntityQuery.MatchesAnyIgnoreFilter(NativeArray<Entity>)`
* `EntityQuery.ToEntityArray(NativeArray<Entity>, Allocator)`
* `EntityQuery.ToComponentDataArray(NativeArray<Entity>, Allocator)`
* `Entities.ForEach.WithFilter(NativeArray<Entity>)`
* Renamed `BufferLookup.IsComponentEnabled` to `BufferLookup.SetBufferEnabled` and `BufferLookup.SetComponetEnabled` to `BufferLookup.SetBufferEnabled`.
* Renamed IJobEntity `EntityInQueryIndex` to `EntityIndexInQuery` to match name scheme found in `ChunkIndexInQuery` and `EntityIndexInChunk`
* Renamed `BlobAssetStore.Remove` to `BlobAssetStore.TryRemove`, to better convey its functionality, as it only removes the BlobAsset if it is present.
* Renamed `SystemAPI.QueryBuilder` to `SystemAPI.EntityQueryBuilder` to better indicate that it is just caching a `Unity.Entities.EntityQueryBuilder`
* Baked primary entities no longer have an implicit dependency on the Transform component.
* ContentDeliverySystem to ContentDeliveryGlobalState.  The state is now updated from the RuntimeContentSystem.
* All generic methods handling generic components with the constraint of `where T : class, IComponentData` has been changed to `where T : class, IComponentData, new()` to better indicate that all managed `IComponentData` types must be default constructable.
* `ComponentTypeHandle`, `BufferTypeHandle`, `DynamicComponentTypeHandle`, and `DynamicSharedComponentTypeHandle` arguments to `ArchetypeChunk` methods are now passed by `ref` instead of by value. This facilitates a caching optimization that will be implemented in a future release.
* Implement ISystem methods (OnCreate/OnUpdate/OnDestroy) as default interface methods.  These no longer need to be defined in the struct implementing the ISystem interface if they are not used.
* Searching the Entities hierarchy using the component filter now treats multiple entries as AND rather than OR.
* Renamed the PostTransformMatrix component to PostTransformScale, and changed its value type to float3x3.
* It's now a compile error to schedule with managed code (for IJobEntity and IJobChunk)
* EntityCommandBufferSystem.RegisterSingleton uses the system's entity rather than creating a custom singleton entity with the name of MyEcbSystem
* Improved performance of `EntityManager.DestroyEntity(EntityQuery)`
* The build settings are no longer stored in the Assets folder.
* Blob asset safety verifier rewritten as a Roslyn analyzer (results in domain reload time improvements and IDE support with CodeFixes).
* Blob assets now create a compile error if it contains a pointer e.g. `fixed byte[16]`.
* Blob assets now warn on new assignments e.g. `var test = new MyBlob()` and `var test = default(MyBlob)`.
* SerializeUtility.GetSceneSectionEntity is now burstable.
* `ComponentTypeSet` is now a `readonly` struct, which is passed by `in` instead of by `value`


### Deprecated

* Renamed `EntityManager.CompleteAllJobs` to `EntityManager.CompleteAllTrackedJobs`, to more accurately describe what it is doing.
* SystemState.Time and SystemBase.Time has been deprecated in favor of World.Time and SystemAPI.Time
* `[WithEntityQueryOptions]` for IJobEntity becomes `[WithOptions]` to be consistent with `EntityQueryBuilder` and `SystemAPI.QueryBuilder`
* `SystemAPI.Query.WithEntityQueryOptions` becomes `SystemAPI.Query.WithOptions` to be consistent with `EntityQueryBuilder` and `SystemAPI.QueryBuilder`
* `ArchetypeChunk.ChunkEntityCount` is now deprecated. It is guaranteed to always have the same value as `ArchetypeChunk.Count`, and the latter should be preferred.
* SystemAPI duplicated API in `ComponentSystemBase`. `HasSingleton`, `GetSingleton`, `GetSingletonRW`, `GetSingletonBuffer`, `TryGetSingleton`, `TryGetSingletonBuffer`, `SetSingleton`, `GetSingletonEntity`, `TryGetSingletonEntity`. Use SystemAPI alternatives instead.
* SystemAPI duplicated API in `ComponentSystemBaseManagedComponentExtensions`. `GetSingleton`, `GetSingletonRW`, `SetSingleton`. Use SystemAPI alternatives instead.
* SystemAPI duplicated API in `SystemBase`. `GetComponent`, `SetComponent`, `HasComponent`, `GetBuffer` and `Exists`. Use SystemAPI alternatives instead.

### Removed

* `ISystemBase` as the old name for good, use the new name `ISystem`
* Removed the `Journaling` sub-menu from the `DOTS` top-level menu. `Enable Entities Journaling` can be set through the `Preferences` window or from the `Journaling` window. `Export to CSV` can be triggered from the `Journaling` window.
* The "Add missing partials keyword to systems" menu item has been removed. It was intended to help older DOTS projects update to the 0.50.x, and is no longer necessary for 1.0 and beyond.
* Removed `BufferAccessor` constructor from the public API. Use methods like `GetBufferAccessor()` to create instances of this type.
* Removed unit-test helper method `ArchetypeChunkArray.CalculateEntityCount(NativeArray<ArchetypeChunk>)` from the public API.
* `LayoutUtilityManaged` and `LayoutUtility`. Component equality comparisons are handled by the TypeManager and FastEquality system internally and no longer require the LayoutUtility.
* Removing dependencies on `com.unity.jobs` package.
* Removed the previously-deprecated methods which limited their operation to a caller-provided `NativeArray<Entity>`. These methods were never particularly efficient, and are increasingly prone to producing incorrect results. If necessary, the functionality can be replicated in user-space using a `NativeHashSet<Entity>` as an early-out mechanism. The affected methods:
* Extraneous use of IAspect to workaround previous API limitations
* Unneeded ISystem methods now that it unnecessary to implement them if they are empty.
* Removed the `DotsPlayerSettings` type.
* Removed `View All Components` label for Aspects tab in Inspector.
* Dependencies on `com.unity.platforms`package has been removed.
* Removed the inner TransformData struct from WorldTransform, LocalTransform and ParentTransform (and instead use extension methods and properties).  Position/Rotation/Scale can now be accessed directly on the transform type itself.

### Fixed

* Store `Allocator` in EntityCommandBufferSystem's `Singleton`.
* IJobEntity now gives proper error when trying to add multiple of same type to `Execute` signature.
* Fixed an issue where inspecting an Entity in play mode would mark its chunk as changed every frame.
* WorldUnmanaged.GetAllUnmanagedSystems previously returned all systems rather than just unmanaged ones
* Missing documentation added or updated for various system and EntityManager API
* EntityQueryBuilder.Build(SystemState) made a copy of SystemState which could lead to a NullReferenceException or ObjectDisposedException. It now takes (ref SystemState).
* Entity inspector no longer throws index out of range exception when modifying the content of integer fields.
* Systems window no longer throws exceptions when encountering invalid worlds.
* Using WithChangeFilter on Entities.ForEach or SystemAPI.Query would add the component type as ReadWrite. It now adds ReadOnly.
* Subscene entity names are truncated to 64 characters during incremental baking.
* Components with `long` or `ulong` enum fields will no longer cause an exception when displayed in entity inspector. As temporary measure until 64 bit integers are supported by `UnityEngine.UIElements.EnumFlagsField`, a text field with the numerical value of the enum will be displayed.
* Change API name from `SetAllocatorHandle` to `SetAllocator` for entity command buffer allocator.
* RectTransform components are no longer skipped when baking transform hierarchies.
* Disabled GameObjects are baked and rendered as expected when activated at runtime.
* NetCodeClientSetting should not add FRONTEND_PLAYER_BUILD scripting define.
* EntityQueryBuilder will correctly emit error when used without constructing with an Allocator.
* `EntityQuery.ResetFilter()` now resets order version filtering to its default value (disabled) as well.
* `BlobArray<T>.ToArray()` throws if the element type T contains nested BlobArray or BlobPtr fields.
* IJobEntity scheduling calls no longer ignores calls to `JobHandle.CombineDependencies()` passed as its `dependsOn` parameter.
* IJobEntity scheduling calls now can contain calls to SystemAPI calls and other JobEntity scheduling
* IJobEntity scheduling calls now ensure they only add automatic dependency assignment, when jobhandle is explicitly passed to scheduling functions.
* Chunk.GetEnableMask did not update the lookup cache correctly, causing the cache to invalidated and exception thrown in some situation (ex: more than one chunk passed to the job)
* Issue where SystemAPI, and JobEntity schedules would not work inside an expression bodied statement.
* If changing an entity's `Parent` would leave the previous parent entity's `Child` buffer empty, the empty `Child` component is now automatically removed by `ParentSystem`.
* Differ was missing modified entities under some particular conditions.
* Addressed an exception when streaming in multiple scene sections at once
* Fixed a performance issue in the Entities Hierarchy when it's docked behind another window while GameObject events happen.
* blob asset references would infrequently become invalid after a live baking pass.
* Entities Hierarchy: Dynamically loaded subscenes are now correctly displayed in the hierarchy
* An exception that occurs when opening properties window from the Entities Hierarchy Properties context menu item.
* `SystemAPI.GetBuffer` to get BufferLookup as ReadWrite (as to be consistent with rest of GetBuffer methods.)
* `SystemAPI.Query<RefRO<MyTag>>` where MyTag is a zero-size component, will now return `default(MyTag)` instead of throwing.
* Drag n drop issue that can cause the Editor to enter an infinite loop
* Disable dragging GameObjects from scene to subscene, and from subscene to scene in playmode
* Fixed a bug which could put the "enabled" state of an `IEnaleableComponent` in an undefined state after removing a different `IEnableableComponent`.
* procedurally created renderable entities are not culled from the game view by default anymore.
* On rare occasions, streaming scenes in would fail due to a corrupt free list in the reused streaming world. This has been fixed.
* Content manager and delivery now only get 1 update per frame.
* Entities Hierarchy truncates long node names to fit in 64 bytes instead of throwing errors.
* Component equality functions could produce incorrect results in IL2CPP builds
* Ensure Autocomplete popup is triggered when using c= (component search) in Hierarchy and in Systems Window.
* Styling issue in Entities Hierarchy when switching data modes.
* Issue where having a local function inside a function using SystemAPI/Entities.ForEach/IJobEntity scheduling would cause build failures with IL2CPP
* Leaking when using EntityIndexInQuery with `IJobEntity` or `Entities.ForEach`.
* Entities Hierarchy: GameObject change events are now processed over multiple frames and will not accumulate when the hierarchy is sitting docked behind another window.
* Searching in Entities Hierarchy is now supported when no world currently exists.
* `TypeManager` will now properly log an exception when invalid components are detected during `TypeManager.Initialization` rather than log to console.
* `IBufferElementData` and `ISharedComponentData` types with no fields will now fail to be added to the `TypeManager`. If an empty type is required, please prefer to use `IComponentData`.
* Fixed an issue where Transform live properties do not work sometimes.
* Reverting the baker state of a previous iteration can have unexpected side effects in some cases.
* `ArchetypeChunk.IsComponentEnabled` and `ArchetypeChunk.SetComponentEnabled` now consistently fail if the provided type handle does not correspond to one of the chunk's component types.
* `ArchetypeChunk.IsComponentEnabled` and `ArchetypeChunk.SetComponentEnabled` now consistently fail if the provided `indexInChunk` argument is negative.
* Components window now correctly shows all components.
* Player crash on exit due to releasing assets after cleanup
* Aspect with both a `RefRW/RO<T>` and a `EnabledRefRW/RO` fields now compiles properly.
* Removed an error message being spammed in the editor when closing subscenes.
* Entities Hierarchy visibility detection issue that potentially causes the window to not properly initialize.
* Companion GameObjects (e.g. lights, reflection probes, audio sources, etc.) transforms stopped updating during live baking.
* Under Relationships tab of Entity Inspector, systems with same name will display with added namespace for distinguishment.
* Entities Hierarchy now properly retrieves subscenes' asset names.

## [1.0.0-exp.12] - 2022-10-19

### Changed
* Updates to package dependencies


## [1.0.0-exp.8] - 2022-09-21

### Added

* `GetSingletonBuffer(bool isReadOnly)` method on `ComponentSystemBase` and `EntityQuery`, for use with singleton `DynamicBuffer`s. No `SetSingletonBuffer()` is needed; once you have a copy of the buffer, its contents can be modified directly.
* `IJobEntityBatch` and `IJobEntityBatchWithIndex` now have `RunWithoutJobs()` and `RunByRefWithoutJobs()` extension methods.
* Documentation on EntityCommandBuffer public functions including ParallelWriter and EntityCommandBufferManagedComponentExtensions.
*  GetOrCreateUnamangedSystemsAndLogException that allow to create unmanaged systems in batches like the equivalent GetOrCreateSystemsAndLogException.
*  CreateSystemsAndLogException that allow to create together in batches managed and unmanaged systems such that all the system instances are created before their OnCreate are called.
* Support AudioSource as a companion component in subscenes
* Per-Component Enabled Bits API is now available. This API allows for the runtime enabling and disabling of components on an entity without triggering a structural change. Component types must implement IEnableableComponent to use this feature.
* `TypeOverridesAttribute` can be applied to components to force a type to appear to have no entity and/or BlobAssetReference fields. This attribute is useful for managed components to reduce time taken during deserialization since un-`sealed` managed field types cannot statically be checked for entity/blob references and thus must be scanned at runtime.
* `EntityManager.MoveComponent` is available as a way for managed components to properly transfer to other entities
* `public bool HasBuffer<T>(Entity entity) where T : struct, IBufferElementData` to `EntityManager`, which can be used to check whether an entity has a dynamic buffer of a given `IBufferElementData` type
* `protected internal bool HasBuffer<T>(Entity entity) where T : struct, IBufferElementData` to `SystemBase`, which can be used to check whether an entity has a dynamic buffer of a given `IBufferElementData` type
* DynamicBuffer.Resize(int length, NativeArrayOptions options);
* DynamicBufferHandle.Update(ref SystemState) matching the same methods on ComponentTypeHandle to improve main thread performance
* EntityManager.EntityManagerDebug.GetLastWriterSystemName. This is useful for debugging out which system last touched component data in a chunk
* EntityQueryMask.Matches methods for chunks and archetypes. (Previously only entity) EntityQueryMask.Matches is the most efficient way to test if a component or entire query matches for an entity or chunk.
* EntityArchetype.ToString reports all types on the archetype
* `EntityManager.AddComponent(NativeArray<Entity>, ComponentTypes)` and `EntityManager.RemoveComponent(NativeArray<Entity>, ComponentTypes)` in order to perform batch component operations on a specific set of entities
* New command in the EntityCommandBuffer to SetEnabled(Entity e, bool value).
* Added property `UpdateAllocatorEnableBlockFree` in `World` to enable or disable world update allocator to free individual block when no memory is allocated in that block.
* More detailed error descriptions for Job.WithCode
* More detailed error descriptions for .this capture inside Entities.ForEach and LambdaJobs, now specifies whether it's Entities.ForEach or Job.WithCode additionally it specifies that you might be using a system's field, property or method as you're capturing 'this' system.
* More detailed error description for partial requirement on systems, now specifies ISystem and Systembase, depending on which system is missing the partial
* SystemBase.Exists(Entity e) to check if an Entity still exists. Can be used inside of Entities.ForEach.
* Support for GetStorageInfoFromEntity inside E.FE
* New commands in EntityCommandBuffer to modify components (add/set/replace) of an entity's LinkedEntityGroup based on an EntityQueryMask.
* `ComponentType` now provides a `ToFixedString` method to allow for a BurstCompatible way of generating a component's name and accessmode.
* `Interface Unity.Entities.IAspect<T>` used for declaring aspects.
* `Unity.Entities.ComponentDataRef<T>`. Used inside an aspect struct declaration as a proxy to the component data. It is also used during the generation of aspect code to identify the composition of the aspect.
* Class Unity.Entities.OptionalAttribute used for declaring optional component inside the aspect declaration.
* Class Unity.Entities.DisableGenerationAttribute  used to disable the source generation of aspect declarations.
* Methods Unity.Entities.ComponentDataFromEntity.GetDataRef and GetDataRefOptional used to create ComponentDataRef<T> from entity.
* Methods GetAspect<T>(entity) and GetAspectRO<entity>, in both Unity.Entities.SystemBase and Unity.Entities.SystemState, that retrieve an aspect from an entity.
* AspectGenerator: Generate the code required to declare an aspect.
* Aspect sample in EntitiesSample project. Demonstrate a few use-case using with a simple R.P.G. game design.
* Nested aspects are now supported
* ComponentType.Combine combines multiple arrays of components types and removes duplicates in the process.
* All aspects now have enumerators. You can use foreach(var myAspect in MyAspect.Query(EntityQuery, TypeHandle))(
* EntityQueryEnumerator is a new public low-level API to efficiently enumerate chunks
* TransformAspect in Unity.Transforms
* New `EntityQueryOptions.IgnoreComponentEnabledState` flag forces an `EntityQuery` to match all entities in all matching chunks, regardless of their enabled-bit values.
* `[WithAll]` Attribute that can be added to a struct that implements IJobEntity. Adding additional required components to the existing execute parameter required components.
* `[WithNone]` Attribute that can be added to a struct that implements IJobEntity. Specifying which components shouldn't be on the entity found by the query.
* `[WithAny]` Attribute that can be added to a struct that implements IJobEntity. Specifying that the entity found by this query should have at least one of these components.
* `[WithChangeFilter]` Attribute that can be added to a struct that implements IJobEntity, as well as on component parameters within the signature of the execute method. This makes it so that the query only runs on entities, which has marked a change on the component specified by the `[WithChangeFilter]`.
* `[WithEntityQueryOptions]` Attribute that can be added to a struct that implements IJobEntity. Enabling you to query on disabled entities, prefab entities, and use write groups on entities.
* BlobAssetStore now checks if the blob was allocated with the correct allocator and throws if it wasn't
* `BufferFromEntity.Update`, allowing users to update a reference within a system instead of constructing a new buffer every frame.
* relaxed entity creation structural safety checks.
* `[CreateBefore]` and `[CreateAfter]` attributes to control the explicit ordering for when systems `OnCreate` method is invoked relative to other systems.
* `static AspectQueryEnumerable<T> Query<T>() where T : struct` in the `SystemAPI` class, allowing users to perform `foreach` iteration through a query without having to manually set up any arguments beforehand. This method may only be used inside methods in `ISystem` types.
* IJobEntity supports Aspects in `Execute` parameters.
* EntityManagerDebug.GetSystemProfilerMarkerName is a method useful for extracting the name of a systems profiler marker. This is used in our own test rigs for extracting performance data from systems.
* Warn when using slow API's from ISystem.OnUpdate. (managed SystemBase is unchanged) For Example systemState.GetEntityQuery or systemState.GetTypeHandle etc. We expect users to store them on the system as fields and use ComponentTypeHandle<>.Update in OnUpdate instead
* `.All<TQuery>()`, `.Any<TQuery>()` and `.None<TQuery>()` methods to the `AspectQueryEnumerable<T>` class.
* `Update(SystemBase)` and `Update(SystemState)` to  `DynamicComponentTypeHandle`, `SharedComponentHandle`, `DynamicSharedCompoentHandle`, and `EntityTypeHandle`, in order to allow for incremental updates.
* `SetComponentEnabled()` to allow setting a component enabled by `DynamicComponentTypeHandle`
* `GetComponentEnabledRO` to allow the retrieval of the enabledbits bitarray on a `Chunk`
* New menu item for exporting entities journaling data to CSV.
* Importance scaling: Custom entry for per chunk tile data.
* TypeManager.HasDescendants(), TypeManager.IsDescendantOf(), TypeManager.GetDescendantCount() for checking the inheritance relationship between types.
* `ComponentSystemBaseManagedComponentExtensions.GetSingletonRW<T>` and `ComponentSystemBase.GetSingletonRW<T>()` to access singletons by reference in systems, with read/write access to the data.
* `EntityQuery.GetSingletonRW<T>()` to access singletons by reference from an EntityQuery, with read/write access to the data.
* `EntityQuery.TryGetSingleton<T>(out T)`, `EntityQuery.HasSingleton<T>()`, `EntityQuery.TryGetSingletonBuffer<T>(out DynamicBuffer<T>)`, and `EntityQuery.TryGetSingletonEntity<T>(out Entity)`
* SystemAPI for: `HasSingleton<T>()`, `GetSingleton<T>()`, `GetSingletonBuffer<T>(bool)`, `TryGetSingleton<T>(out T)`, `TryGetSingletonBuffer<T>(out DynamicBuffer<T>)`, `SetSingleton<T>(T value)`, `GetSingletonEntity<T>()`, `TryGetSingletonEntity<T>(out Entity)`, and `GetSingletonRW<T>()`. All of which are now supported inside Systems.
* `.WithFilter(NativeArray entities)` to the `QueryEnumerablee` class. This allows users to supply an array of entities to a query over aspects/components in a `foreach` iteration. Entities without the specified aspects/components will be ignored.
* Idiomatic `foreach` iteration through aspects/components is now supported inside `SystemBase` types.
* `GetEntityDataPtrRO()`, `GetRequiredComponentDataPtrRO()`, and `GetRequiredComponentDataPtrRW()` methods to `ArchetypeChunk` (mostly for internal use, to provide efficient access to a Chunk's `Entity` array for generated job code).
* `RefRO<IComponentData>` is added as a read-only counterpart to `RefRW<IComponentData>`.
* Added an indicator to the items in the DOTS Hierarchy when they are in the Runtime DataMode to differentiate them from items in the Authoring DataMode.
* Added netcode aware WorldSystemFilterFlags and WorldFlags.
* functions involving `IEnableableComponent` can be performed within an `ExclusiveEntityTransaction`
* `SystemAPI.GetComponent`, `SystemAPI.SetComponent`, `SystemAPI.GetBuffer`, `SystemAPI.HasBuffer`, `SystemAPI.GetAspect`, `SystemAPI.GetAspectRO`.
* Add Singleton to Begin- and EndInitialization buffer systems
* Information on which managed fields on a system is preventing it from being unmanaged
* [ChunkIndexInQuery] to acquire the current index of the chunk inside the query an IJobEntity is iterating over.
* [EntityIndexInChunk] to acquire the current index of the entity inside the chunk an IJobEntity is iterating over.
* Entities profiler modules tree views will now show leaf count on nodes that have children.
* Add an opt in define to disable baking by default: `ENABLE_LEGACY_ENTITY_CONVERSION_BY_DEFAULT`
* SystemBase.GetEntityQuery can now take an EntityQueryDescBuilder.
* RequireMatchingQueriesForUpdate attribute on an ECS System will cause the system to only call OnUpdate if any EntityQuery it creates matches an entity.
* New `EntityQuery` methods for asynchronous bulk entity/component copying: `.ToEntityListAsync()`, `.ToComponentDataListAsync()`, and `.CopyFromComponentDataListAsync()`. These methods support enableable components, use `NativeList` instead of `NativeArray`, include additional safety checks, and take an optional `JobHandle` parameter as an input dependency for the jobs they schedule.
* A global scratchpad allocator
* A FixedString Name to WorldUnmanaged
* DOTS hierarchy now supports drag and drop operations similarly to built-in hierarchy
* Test code coverage for burst compiled job scheduling
* Missing XXXXByRef methods for IJobFilter
* A new window that displays the content of the entities journaling data, accessible from the `DOTS -> Journaling` menu item.
* Ability to retrieve Entity when iterating with the `SystemAPI.Query<Entity, T1, ...>()` API.
* ChunkEntityEnumerator allows users to iterate over an `ArchetypeChunk`, usually within the confines of an `IJobChunk`. This is especially useful in cases where components implementing `IEnableableComponent` are involved.
* `EntityQuery.CalculateFilteredChunkIndexArray()` and `EntityQuery.CalculateFilteredChunkIndexArrayAsync()` helper functions, which can be used for backwards compatibility with the previous implementation of `IJobChunk`.
* DOTS Hierarchy now support context menu operations for Scene, SubScene, GameObject and Entity items.
* Added profiler markers around `Entities.ForEach` invocations
* `SceneSectionStreamingSystem.MaximumSectionsUnloadedPerUpdate` now allows you to control how many scene sections are unloaded per frame
* System group allocator in `ComponentSystemGroup` to facilitate fixed step and variable rate simulation systems.
* `EntityQuery.CalculateBaseEntityIndexArray()` and `EntityQuery.CalculateBaseEntityIndexArrayAsync()` helper functions, which can be used to compute an `entityIndexInQuery` for each entity matching a query.
* Add a compact property `state.WorldUpdateAllocator` in system state.
* EntityManager.CreateSingleton<T> and EntityManager.CreateSingletonBuffer<T>
* A static bool for logging operations during an EntityCommandBuffer's playback, PLAYBACK_WITH_TRACE.
* RequireAnyForUpdate API to ComponentSystemBase and SystemState
* `SystemAPI.Time` in Systems (ISystem, SystemBase)
* `SystemAPI.GetComponentDataFromEntity` in Systems (ISystem, SystemBase)
* `SystemAPI.GetComponent` in Systems (ISystem, SystemBase)
* `SystemAPI.SetComponent` in Systems (ISystem, SystemBase)
* `SystemAPI.HasComponent` in Systems (ISystem, SystemBase)
* `EntityManager.CompleteDependencyBeforeRO` to complete all jobs of a given type before readonly access
* `EntityManager.CompleteDependencyBeforeRW` to complete all jobs of a given type before readwrite access
* Added support for Data Modes to the Inspector window when inspecting Entities or GameObjects / Prefabs that are converted to Entities at runtime.
* Changes made to GameObjects inside SubScenes while the Editor is in Play mode will now persist when exiting Play mode.
* SystemAPI now provides `GetBuffer`, `GetBufferFromEntity` and `HasBuffer` methods in SystemBase and ISystem.
* SystemAPI now provides `GetStorageInfoFromEntity` and `Exists` methods in SystemBase and ISystem.
* Implicit syncing in SystemAPI for Systems. So calls to, GetComponent, SetComponent, HasComponent, GetBuffer, GetBufferFromEntity, HasBuffer and GetComponentDataFromEntity will complete other systems if they have that component.
* `Aspect.CompleteDependencyBefore[RO|RW](ref SystemState)` for explicit Aspect syncing so that when on MainThread you can use GetAspect and GetAspectRO and it will complete that dependency.
* SystemAPI now provides `GetAspectRW` and `GetAspectRO` methods in SystemBase and ISystem.
* `EntityQueryBuilder WithAll<T>`, `WithAny<T>`, `WithNone<T> `fluent APIs that accept up to seven type arguments and can be chained together to create an EntityQueryBuilder.
* `EntityQueryBuilder.WithAllRW<T>` and `WithAnyRW<T> `that accept up to two type arguments.
* EntityQueryBuilder WithAll, WithAny, WithNone methods that accept an INativeList for bulk changes, or ComponentTypes that can only be known at runtime. These are compatible with NativeList and FixedList32Bytes et. al.
* EntityQueryBuilder Build(SystemBase), Build(SystemState) and Build(EntityManager) each return a new EntityQuery, owned by the argument passed.
* `SystemAPI.QueryBuilder()` to support building a query easily using fluent syntax inside `ISystem` and `SystemBase` types.
* `SystemAPIQueryBuilder`, whose API matches that of `EntityQueryBuilder` where relevant.
* `SetGroupAllocator` and `RestoreGroupAllocator` in `World` to replace/restore world update allocator with/from system group allocator.
* `WorldUpdateAllocator` and `WorldRewindableAllocator` in `SystemState` and `ComponentSystemBase` to get access of world update allocator or system group allocator.
* `EntityManager.AddSharedComponent` and `EntityManager.SetSharedComponent` can now target a `NativeArray<Entity>`. These new variants are significantly faster than a simple loop over the single-Entity variants.
* Each system has an entity associated with it
* Property EntityManager.UniversalQueryWithSystems for built-in universal entity queries including system entities
* EntityManager.GetComponentDataRW for SystemHandleUntyped only
* EntityManager XXXComponent APIs taking SystemHandleUntyped to access components of system entities
* `SubSceneUtility.EditScene` function which allows marking subscenes as editable
* SystemHandle XXXSystem<T> for managed system types
* SystemHandle XXXSystem(Type) for all system types
* SystemHandle.Null
* SystemAPI.GetComponent(SystemHandleUntyped)
* SystemAPI.GetComponentRW(SystemHandleUntyped)

### Changed

* **API-Breaking Change:** `IJobChunk.Execute()` now takes additional parameters to support per-component enable bits. These extra parameters contain information about which entities in the chunk should be processed or skipped (based on whether the relevant components are enabled or disabled). As a temporary workaround when converting existing `IJobChunk` implementations, we recommend adding a call to `Assert.IsFalse(useEnabledMask)` to their `Execute()` methods.
* Changed `LiveLinkPatcher` and `LiveLinkPlayerSystem` to use `IJobEntityBatch`, due to removal of `IJobForeach`
* Changed docs from `IJobForeach` and `IJobChunk` to refer to `IJobEntity`, and `IJobEntityBatch` respectivly
* Changed IJE out of `DOTS_EXPERIMENTAL`
* Improve Entity, System and Component inspector relationship tabs with dedicated message when there is nothing to show.
* `IJobEntityBatchExtensions.RunWithoutJobs()` and `IJobEntityBatchWithIndexExtensions.RunWithoutJobs()` now pass their `jobData` parameter by value, for consistency with existing Run/Schedule methods. To pass by reference, use `RunByRefWithoutJobs()` instead.
* DOTS Hierarchy now displays SubScenes' state (opened, livelinked, closed or not loaded).
* Make `ScratchpadAllocator` inherit `IAllocator`.
* Both managed and unmanged systems instances are created before their respective OnCreate method are called.
* When using `EntityManager.SetName` with a managed `string` as a parameter, if a string longer than 61 characters is used, the string will be truncated to fit within an `EntityName`,
* Subscene nodes in DOTS Hierarchy now have a similar style as in the GameObject Hierarchy.
* Unity.Transforms systems are fully Burst compiled now
* Updated Emscripten from version 1.38.28.2-unity to 2.0.19-unity.
* `EntityQuery.ToComponentDataArray` can be used with managed component as a generic parameter
* `TypeManager.GetTypeIndexFromStableTypeHash` is now Burst compatible and can be called from Bursted functions.
* Entity bulk operations that take a EntityQuery as first parameter can now be called with the WithAll/WithAny/WithNone API. These bulk operations include DestroyEntity, AddComponent, RemoveComponent, AddComponentData, AddSharedComponentData, AddChunkComponentData, RemoveChunkComponentData
* Added support for `EntityQuery q = Entities.WithAll<Foo>().ToQuery();`
* No longer write out to files in Unity 2021+ from entities source generators.
* Improved the performance of the `EntityQuery` matching chunk cache in applications with many empty archetypes.
* EntityTypeHandle is now marked as a readonly container since the data is never writable. This means is no longer required (But still correct & possible) to mark the EntityTypeHandle as [ReadOnly] in jobs.
* World.AddSystem for ISystem has been renamed to World.CreateSystem. This matches the managed system API.
* World.DestroyUnmanagedSystem is now World.DestroySystem. This matches the managed system API.
* ComponentSystemGroup.AddUnmanagedSystemToUpdateList has been renamed to AddSystemToUpdateList.  This matches the managed system API.
* ComponentSystemGroup.RemoveUnmanagedSystemFromUpdateList. This matches the managed system API.
* `EntityCommandBufferSystem.CreateCommandBuffer()` now uses the `World.UpdateAllocator` to allocate command buffers instead of `Allocator.TempJob`. Allocations from this allocator have a fixed lifetime of two full World Update cycles, rather than being tied to the display frame rate.
* World.Dispose and World.DestroySystem can no longer be called while any system is executing (OnCreate / OnDestroy / OnUpdate) on the same world. This prevents a variety of corner cases where incorrect API usage would lead to a corrupted internal state.
* World.GetExistingUnmanagedSystem no longer throws if the system couldn't be found but returns a null SystemHandle. (This matches the managed system behaviour of GetExistingSystem, which returns null)
* Throwing an exception in OnDestroy now throws the exception in user code as opposed to just logging the exception. This matches the behaviour of SystemBase
* `TypeManager.TypeInfo.DebugTypeName` now returns a `NativeText.ReadOnly` type allowing for burst compatible way to get a type name, reduces garbage and avoids string copies via string interning.
* SystemState is a ref struct type to help avoid potentially dangerous access
*section entirely if it's not needed.
* Chunks are now limited to 128 entities per chunk, this makes enabled components faster since we now use v128 to efficiently cull which entities to process
* EntityManager.Version has been renamed to EntityManager.OrderVersion
* Improved debug visualizers for EntityManager, Entity, Archetype and Chunk
* Static safety ID creation is burst compatible, moving towards more Entities.ForEach job schedule sites burst compatibility.
* Synchronous EntityQuery methods (`.ToEntityArray()`, `ToComponentDataArray()`, and `CopyFromComponentDataArray()`) are no longer implemented in terms of scheduling jobs. For asynchronous job-based implementations (which may be more efficient with extremely large workloads), use the variants of these methods with the `Async()` suffix.
* Updated docs explaining how to use IJobEntity.
* CheckDisposed method in EntityQueryEnumerator is now public
* The Current property for a generated Aspect Enumerator now has a conditional CheckDisposed call to identify when the property is being accessed with a disposed enumerator instance
* Updated com.unity.roslyn to `0.2.1-preview`
* When an ISystem.OnUpdate is executing bursted it now shows up in the profiler as green. The same way that jobs do.
* New Prefab restrictions in the EntityCommandBuffer: Entities with a Prefab component cannot be created or destroyed, entities with a Prefab component cannot have components added or removed, entities with a Prefab component cannot have components modified, and entities cannot have the Prefab component added or removed. These conditions will throw an error at EntityCommandBuffer playback.
* Shared components implementing IRefCounted are no longer treated as managed.
* Systems now track write dependencies for zero-sized ("tag") components. This is necessary to avoid race conditions for jobs that toggle the enabled bits for tag components.
* T constraint on all containers from struct to unmanaged
* Changed `SystemAPI.Query().All/Any/None` methods to be named `SystemAPI.Query().WithAll/WithAny/WithNone` to avoid confusion with `Enumerable.All/Any/None` methods.
* Entities source generators built with dotnet instead of bee.
* `UnsafeBitArray GetUnsafeComponentEnabledRO(this ArchetypeChunk chunk, int indexInTypeArray)` to `unsafe v128 GetEnableableBits(ref DynamicComponentTypeHandle handle)`
* An EntityCommandBuffer can now be created, played back, and destroyed from within a bursted ISystem.
* `IJobEntity` now uses `IJobChunk` for generated job
* Updated the `NullNetworkInterface` class within `NetworkDriverStore.cs` to match the changes within `com.unity.transport`'s `INetworkInterface`.
* `GetSingleton<T>()` does not `CompleteWriteDependency` anymore when invoked. If the dependencies that are acting on the Singleton need to be completed, an explicit invocation to `CompleteDependency` is required.
* SystemBase.GetBuffer registers a job dependency for the IBufferElementData type specified.
* All entities are now created with a `Simulate` component, a zero-size `IEnableableComponent` used by the Netcode package.
* `ComponentDataRef<IComponentData>` is renamed to `RefRW<IComponentData>`, and its property `Value` is likewise renamed to `ValueRW` in the name of explicitness.
* Make initial memory block size of scratchpad allocator configurable.
* Improved performance of `EntityManager.GetallUniqueSharedComponents` in IL2CPP builds
* Significantly improved the performance of `EntityQuery.CalculateEntityCount()`
* Significantly improved the performance of `EntityQuery.IsEmpty`.
* Global system version is now also incremented after a system update in addition to before a system update, so that changes made outside of systems also have their own version number.
* The meaning of WorldSystemFilterFlags.Default can be changed by parent groups.
* Use CollectionHelper to create `NativeArray` in `CreateArchetypeChunkArrayAsync`.
* Increased the maximum number of shared components per entity from 8 to 16.
* ECS Systems now always update by default. To conditionally update a system, either call RequireForUpdate in OnCreate or use the RequireMatchingQueriesForUpdate attribute on a system for the previous behavior, where a system will only update if any EntityQuery it creates matches an entity. Systems that do not use EntityQueries (either through GetEntityQuery, or Entities.ForEach) should not use either of these methods.
* The global system version is now incremented when creating a system in order to properly differentiate them during creation phase.
* Make RunWithoutJobs and RunByRefWithoutJobs internal; users should call Run() instead
* Ensure lazy initialization of IJob types
* Add byte type member for Bool properties, `AllowGetSystem` in `WorldUnmanagedImpl`.
* Synchronous `EntityQuery` operations such as `ToEntityArray()` now automatically complete any running jobs that would affect their output. Previously, these race conditions were detected and reported in in-Editor builds, but it was the caller's responsibility to complete the input dependencies before calling these methods.
* Asynchronous `EntityQuery` methods are now less conservative when computing the input dependencies for the jobs they schedule, which allows more potential parallel execution.
* Updated com.unity.properties to `2.0.0-exp.11`
* Updated com.unity.properties.ui to `2.0.0-exp.11`
* Updated com.unity.serialization to `2.0.0-exp.11`
* Asset bundles associated with entity scenes are now unloaded asynchronously
* Scene streaming system is now taking less time on the main thread in IL2CPP builds
* `ComponentDataFromEntity.TryGetComponent` is now slightly faster than manually calling `HasComponent` and doing a lookup (same for `BufferFromEntity.TryGetBuffer`)
* Improved performance of scene serialization by adding caching to type validation
* Optimized wait times in `EntityManager.MoveEntitiesFrom`, resulting in improved streaming performance
* Renamed many shared component apis to default to unmanaged & burstable codepath. Now unburstable & managed-friendly codepaths have Managed in the name.
* Reduced the main thread cost of loading an entity scene
* Reduced per-frame overhead of `CompanionGameObjectUpdateTransformSystem`
* Reduced main thread streaming overhead by Burst-compiling `SceneSectionStreamingSystem.ExtractEntityRemapRefs`
* More shared component apis have the default version unmanaged, with a Managed variant for when managed shared components are required.
* Ensure nested native containers don't end up in component data types now that they are supported
* EntityQueryDescBuilder now has a fluent syntax.
* Renamed `EntityQueryMask.Matches()` to `.MatchesIgnoreFilter()` when the parameter is an `Entity` or `ArchetypeChunk`. These methods do not have the necessary context to perform chunk- or entity-level filtering, and may return false positives in these cases. The new function name reflects this limitation. To perform a more accurate filter-aware "does entity match query?" check on the main thread, use `EntityQuery.Matches(Entity)`. An equivalent for use in jobs is not currently supported.
* The `ComponentTypes` type has been renamed to `ComponentTypeSet`.
* Global system version is now incremented both before and after system OnCreate.
* Renamed `EntityManager.GetEnabled(Entity)` to `.IsEnabled(Entity)` for consistency.
* EntityQueryDescBuilder has been renamed to EntityQueryBuilder for brevity.
* Added EntityQueryBuilder.AddAdditionalQuery to retain the ability to create EntityQueries that match multiple archetypes.
* It is no longer necessary to call FinalizeQuery on an EntityQueryBuilder. Use AddAdditionalQuery to add multiple archetypes to the query description.
* Options(EntityQueryOptions) has been renamed to WithOptions(EntityQueryOptions) for consistency.
* "System state" components have been renamed to "cleanup" components. This affects `ISystemStateComponentData`, `ISystemStateSharedComponentData`, and `ISystemStateBufferElementData`, as well as various methods like `TypeManager.IsSystemStateComponent`. In all cases, "SystemState" in the name is replaced with "Cleanup".
* `EntityQuery.MatchesNoFilter()` was renamed to `MatchesIgnoreFilter()` for consistency with other methods.
* Improved performance of `EntityManager.SetSharedComponentData` by using Burst
* Improved performance of `EntityPatcher` by using Burst
* SystemBase.CheckedStateRef() made public for ease of use in SystemState based utility methods
* `SystemAPI.GetSingletonRW<T>` (and the ComponentSystemBase equivalent) now return a `RefRW<T>` wrapper struct instead of `ref T` allowing for safety errors to be presented when the underlying reference is invalidated.
* GetExistingSystem, GetOrCreateSystem, and CreateSystem for ISystem types return SystemHandleUntyped only
* All APIs taking an ISystem type work with SystemHandleUntyped
* `EntityQueryOptions.IncludeDisabled` has been renamed to `IncludeDisabledEntities`, to better clarify that it has nothing to do with enableable components. To control the matching of enableable components, use `EntityQueryOptions.IgnoreComponentEnabledState`.
* `ComponentDataFromEntity<T>` was renamed to `ComponentLookup<T>`, and `GetComponentDataFromEntity<T>()` was renamed to `GetComponentLookup<T>()`.
* `BufferFromEntity<T>` was renamed to `BufferLookup<T>`, and `GetBufferFromEntity<T>()` was renamed to `GetBufferLookup<T>()`. In addition, the `HasComponent()` method on this type was renamed to `HasBuffer()`.
* `StorageInfoFromEntity` was renamed to `EntityStorageInfoLookup`, and `GetStorageInfoFromEntity()` was renamed to `GetEntityStorageInfoLookup()`.
* `T XXXSystem<T>` renamed to `T XXXSystemManaged<T>` for managed system types
* SystemHandleUntyped renamed to SystemHandle
* T XXXSystem(Type) renamed to T XXXSystemManaged(Type)
* SystemHandle.Update(WorldUnmanaged) works for managed systems too
* the LoadSceneAsync overload that uses weak scene reference is now static.
* Hybrid assemblies will not be included in DOTS Runtime builds.
* If a component implements `IEnableableComponent`, viewing an Entity within a debugger will now display whether a component is enabled or disabled.
* When viewing an ArchetypeChunk's ComponentTypes within a debugger, a `ComponentType` that implements `IEnableableComponent` will now display how many disabled components there are within a chunk.
* ISystem Entities.ForEach lambdas may access system state through Systems proxy type
* Bakers now support declaring TransformUsageFlags which specifies how the transform component will be used at runtime
* Optimized performance of `IJobEntityBatch` and `Entities.ForEach`
* Some extra `EntityCommandBuffer` checks can now be enabled during playback, by setting `EntityCommandBuffer.ENABLE_PRE_PLAYBACK_VALIDATION` to true.
* `QueryEnumerable<T> SystemAPI.Query<T>()` can now accept up to 8 type arguments, i.e. `QueryEnumerable<(T1, T2)> Query<T1, T2>()`, `QueryEnumerable<(T1, T2, T3)> Query<T1, T2, T3>()`, and so forth. The maximum number of type arguments is set to 8, and correspondingly the maximum number of elements in the returned tuple is 8. This is in accordance with [current C# convention](https://docs.microsoft.com/en-us/dotnet/api/system.tuple-8?view=net-6.0).
* SystemBase.GetBuffer takes an optional isReadOnly parameter.
* The DOTS Hierarchy content is now filtered based on the currently selected DataMode
* When creating an authoring compoenent and the movedfromAttribute is used, now we make sure to add "Authoring" string to the MovedFrom Class parameter.
* Removed the the default JobHandle parameter (it now must be passed in explicitly to match EFE scheduling with the built-in Dependency property).

### Deprecated

* EntityManager.GetEntityQueryMask(EntityQuery) has been deprecated. Use EntityQuery.GetEntityQueryMask() instead.
* Use of `[ExecuteAlways]` on systems is now deprecated (it's still supported on MonoBehaviours). Please use `[WorldSystemFilter(WorldSystemFilterFlags.Editor)]` instead to ensure your system is added and runs in the Editor's default world. If you'd like to ensure your system always updates, please use the [AlwaysUpdateSystemAttribute]` instead
* `RequireSingletonForUpdate<T>()` has been renamed to `RequireForUpdate<T>()` and no longer requires only a single component to exist.
* AlwaysUpdateSystem has been deprecated and systems will now always update by default.
* The `EntityQuery.ToEntityArrayAsync()`, `.ToComponentDataArrayAsync()`, and `.CopyFromComponentDataArrayAsync()` methods have been deprecated, as they do not correctly support enableable components and are prone to safety errors. They should be replaced with calls to the new `.ToEntityListAsync()`, `.ToComponentDataListAsync()`, and `.CopyFromComponentDataListAsync()` methods.
* `EntityQuery.CreateArchetypeChunkArray()` was renamed to `EntityQuery.ToArchetypeChunkArray()`. The new function is also significantly faster.
* `EntityQuery.CreateArchetypeChunkArrayAsync()` has been replaced by `EntityQuery.ToArchetypeChunkListAsync()`. The new function should be faster in most cases, eliminates a sync point if query filtering was enabled, and returns a `NativeList<ArchetypeChunk>` instead of a `NativeArray` (since the exactly number of chunks returned won't be known until the job completes).
* `IJobEntityBatch.ScheduleGranularity` has been deprecated; the previous default behavior of chunk-level schedule granularity will be restored. Jobs using this feature should be migrated to `IJobParallelFor`.
* `IJobEntityBatch` and `IJobEntityBatchWithIndex` variants of `.Run()` and `.Schedule()` which limit processing to a specific `NativeArray<Entity>` have been deprecated. Jobs using this feature can populate a `NativeHashSet<Entity>` with the relevant entities, and add an early-out to the job code if `!hashSet.Contains(entity)`.
* `EntityQuery.CompareComponents` is deprecated. Use `EntityQuery.CompareQuery(EntityQueryDescBuilder)` instead.
* The `EntityManager.AddComponents(Entity, ComponentTypes)` method has been renamed `AddComponent`, for consistency with all other `AddComponent` and `RemoveComponent` variants.
* EntityQueryDescBuilder AddAll, AddAny, AddNone, and FinalizeQuery are all Obsolete now.
* The `IJobEntityBatch` and `IJobEntityBatchWithIndex` job types have been deprecated, and will be removed before the 1.0 package release. New and current implementations of these job types should be conversion to `IJobChunk`, which handles enableable components much more efficiently. Note that the interface to `IJobChunk` has changed since previous DOTS releases; see the upgrade guide for migration tips covering the most common use cases.
* SystemGenerator and LambdaJobs: handling aspect as parameters to lambda jobs in our System Entities.ForEach.
* Correctly cache BufferTypeHandle for any DynamicBuffer used in Entities.ForEach

### Removed

* Removed the LiveLink feature and its build component.
* ComponentTypes.m_Masks & ComponentTypes.Masks are now internal. This implementation detail was accidentally made public.
* Removed element `EnableBlockFree` in enum `WorldFlags` because `EnableBlockFree` does not align with the usage of `WorldFlags`.
* DOTS Compiler Inspector. Functionality is now available via viewing generated code directly from Temp/GeneratedCode in the project directory.
* Entity Debugger (replaced by Entity Inspector, Systems window, DOTS Hierarchy, and Entities Profiler Modules)
* Remove slow singleton API's from SystemState, that do too much when called in OnUpdate. Instead we are going to put those API's into SystemAPI where they can be efficiently code-generated
* Removed GameObjectEntity
* Dependency on com.unity.roslyn package. This is no longer needed as Unity 2022 has built-in support for source-generators.
* The `ArchetypeChunkIterator` type has been removed. To iterate over the chunks that match a query, call `query.CreateArchetypeChunkArray()` and iterate over the output array.
* Removed the following deprecated methods: `BlobAssetReference.TryRead()`, `EntityQuery.CompareQuery(EntityQueryDesc[] queryDesc)`, `ScriptBehaviourUpdateOrder.AddWorldToPlayerLoop()`, `ScriptBehaviourUpdateOrder.AddWorldToCurrentPlayerLoop()`, `ScriptBehaviourUpdateOrder.AppendSystemToPlayerLoopList()`, and `MemoryBinaryReader.MemoryBinaryReader(byte* content)`.
* ComponentSystem, EntityQueryBuilder, EntityQueryCache and the old ForEach methods, along with their tests.
* Entities.ForEach removed from SystemState and access from ISystem.  `IJobEntity` and `foreach` are the supported APIs in ISystem going forward.
* Removed the System.String overload of `EntityManager.SetName` and `EntityCommandBuffer.SetName` (as `System.String` can now implicitly cast to all `FixedStringXXBytes` types). This improves interoperability with Burst. You may now get exceptions if you attempt to set a name that is too large. Use `FixedStringMethods.CopyFromTruncated` to manually truncate them without throwing.
* ECS script templates.
* Removed long-unused `SceneViewWorldPositionAttribute` type.
* Removed expired `AssetBundleManager.UseAssetBundles` API
* `SystemAPI.Query<T>(NativeArray<Entity> entities)` and all its overloads.
* The ability to schedule managed components in IJobEntity was removed due to safety concerns, instead use .Run.
* Removed Entities.ForEach.WithFilter as this does not work correctly with enabled components.  This feature will need to be implement in user code going forward.
* The following `EntityQuery` methods have been removed: `ToEntityArray(NativeArray<Entity>, Allocator)`, `ToComponentDataArray(NativeArray<Entity>, Allocator)`, `CalculateEntityCount(NativeArray<Entity>)`, `CalculateEntityCountWithoutFiltering(NativeArray<Entity>)`, `MatchesAny(NativeArray<Entity>)`, and `MatchesAnyIgnoreFilter(NativeArray<Entity>)`. They are significantly slower than other overloads that do not limit processing to an array of entities, do not work with enableable components, and are prone to false positives. If an application requires these features, it's possible to implement them as wrappers around the remaining overloads, using a `NativeHashSet<Entity>` of the desired entities as a post-processing step.
* `SystemHandle<T>` and `SystemRef`
* Removed `IJobForEach`, due to long notice of deprecation

### Fixed

* `ComponentDataFromEntity` and `GetBufferFromEntity` were incompletely hoisted in a Jobs.WithCode() context
* WorldSystemFilter, DisableAutoCreation, and AlwaysUpdateSystem attributes working with ISystem systems
* Make sure WorldUpdateAllocatorResetSystem is called in Editor.
* `IJobEntityBatch.RunWithoutJobs()` and `IJobEntityBatchWithIndex.RunWithoutJobs()` are now Burst-compatible.
* Bug with EntityCommandBuffer removing multiple components from multiple entities when the number of entities was more than 10.
* Defining `UNITY_DOTS_DEBUG` in standalone builds no longer triggers false positives from `AssertValidArchetype()`.
* OnCreateForCompiler is called before the OnCreate for unmanaged systems.
* Unloading subscenes could sometimes result in an error about a query not including a member of LinkedEntityGroup
* When setting many long strings using `EntityManager.SetName`, the editor will properly handle the storage of these names.
* `EntityQuery.ToComponentDataArray<T>()` and `EntityQuery.CopyFromComponentDataArray<T>()` now detect potential race conditions against running jobs which access the component `T`. These jobs must be completed before the `EntityQuery` methods are called.
* `EntityQuery.CalculateEntityCountWithoutFiltering()` now gives correct results when the query includes enableable types.
* IJobEntityBatchIndex and IJobChunk no longer have separate logic and differing between DOTS Runtime and Hybrid
* Previously DOTS Runtime builds would throw if two assemblies registered the same closed form of a generic component via the `[RegisterGenericComponentType(...)]` attribute.
* `EntityManager` methods that target `EntityQuery` objects now correctly handle per-component enabled bits. However, performance will be reduced for queries that contain enableable component types.
* Shutdown the TypeManager with application exit to ensure memory is freed.
* EntityQuery methods that do not ignore filtering can not run safely with jobs that can write to any enableable components in the query, as these jobs could concurrently affect which entities match the query. The JobsDebugger will now detect and report these race conditions.
* Fixed bug where DOTS/Hierarchy window throws an exception when viewing a conversion / baking staging world
* SubScenes are correctly re-imported when managed component layouts change.
* When LiveConversion is disabled in edit mode, it is now properly disabled and doesn't execute. In PlayMode we continue to always convert since the entities are required to run the game.
* Fixed potential crash in `DotsSerializationWriter` which could occur depending on if the Garbage Collector compacts memory while the writer is in `DotsSerializationWriter` use.
* suppress error when using negative Section values for SceneSections, instead ignore them
* Autocomplete keyboard navigation issue.
* In the editor, fixed `BakeDependencies` to be Burst compilable again by removing the `ValueTuple` use.
* Interface implemented execute methods now work with IJobEntity. Before this point you couldn't make an interface of `interface ITranslationExecute { void Execute(ref Translation translation) }` and implement it in an IJobEntity: `partial struct TranslationJob : IJobEntity, ITranslationExecute { void Execute(ref Translation translation) {} }`
* `.Schedule` and `.ScheduleParallel` Invocations for IJobEntity without a jobhandle now matches Entities.ForEach automatic  chain `SystemBase.Dependency` handling
* IJobEntity to work with ISystem. Scheduled the same way as SystemBase invocations of IJobEntity.
* GhostCollectionSystem burstable.
* Component inspector relationship tab being refreshed too often.
* Dangling files left after a build using buildconfigs under certain circumstances
* Freeing Chunks now occurs in constant time, and not linear time.
* Codegen for `WithEntityQueryOptions` now works with multiple `EntityQueryOptions`.
* DOTS Runtime now correctly sorts systems by creation order when calling TypeManager.GetSystems
* Fixed Bug where GetSingleton and friends with a query that has more than 1 required component was returning the wrong component data
* Certain usage patterns of `ComponentDataFromEntity` and `BufferFromEntity` no longer give incorrect results due to inconsistent `LookupCache` state.
* Replaced Path.GetRandomFileName() usage in some unit tests to resolve occasional test failures stemming from a failed test setup involving folder creation with an invalid name
* If the value of the Parent component of an entity is changed while the previous parent entity was destroyed at the same time, an exception could be thrown during the next update of the transform system.
* `Entities.ForEach` calls that make use of an `Entity` parameter should no longer cause a warning to be logged  (due to generated code)
* `ExclusiveEntityTransaction.AddComponent` and `ExclusiveEntityTransaction.RemoveComponent` will no longer throw  with the error message of `Must be called from the main thread`
* Improved performance of Source Generators when run in IDEs.
* Fix Create &gt; ECS &gt; System template.
* `IJobEntity` inside nested struct now works.
* `IJobEntity` now works inside namespaces that have `using` statements.
* "System" in namespace causing issues with Entities.ForEach and other codegen.
* Use of WithDisposeOnCompletion with Job.WithCode if a `using System.Collections` is missing.
* `EntityQuery.CopyFromComponentDataArray<T>()` and `EntityQuery.CopyFromComponentDataArrayAsync<T>()` now correctly set the change version of any chunks they write to.
* System in System code.
* Nested replacements, like GetComponent(SetComponent);
* `SystemAPI.GetComponentDataFromEntity`, `SystemAPI.GetBufferFromEntity`, `SystemAPI.GetBuffer`, `SystemAPI.TryGetBuffer`, `SystemAPI.TryGetComponent`.
* `SystemAPI.Time` now stores a copy of TimeData, making it deterministic in `Entities.ForEach` again.
* Fixed an issue where UnityEngine.Component, UnityEngine.GameObject, and UnityEngine.ScriptableObject didn't work as ManagedComponents for IJobEntity.
* Many additional debug checks are now run in standalone builds when `UNITY_DOTS_DEBUG` is defined.
* IJobEntity now has support for Managed and Unmanaged SharedComponents in both ISystem and SystemBase
* Entities profiler modules will no longer waste time recording data if they are not active in the profiler window.
* Archetypes window will now refresh properly when archetype data changes are detected.
* ComponentDataFromEntity and GetBufferFromEntity were incompletely hoisted in a Jobs.WithCode() context
* Entities Structural Changes profiler module now support unmanaged systems.
* CreateEntityQuery will throw an ArgumentException if query includes the Entity type. Entity is implicitly included in all queries and adding it explicitly interferes with EntityQuery caching.
* DOTS Entities throws a compilation error when using named arguments.
* Source-gen in methods that take a multi-dimensional array as a parameter.
* `EntityManager.MoveEntitiesFrom` leaked memory per archetype, now it doesn't anymore
* Small memory leak per `EntityQuery`
* Memory leak in `EntityComponentStore` related to entity names
* Memory leak in `EntityPatcher`
* Small memory leak per `World` instance
* Memory leak in `ComponentSafetyHandles`
* `EntityQuery` objects are consistently compared, regardless of which version of `GetEntityQuery` is called.
* Fixed a domain reload time regression introduced in 0.50.
* Source-generated methods now emitted as private.  This allows for sealing of system types.
* Tags and Components in the Entity Inspector are now consistently ordered: TRS components and `LocalToWorld`, `LocalToParent` first, then other components alphabetically.
* Foldouts in entity component inspector can be expanded/collapsed in edit mode as well as in play mode.
* Split `IAspect<T>` into `IAspect` and `IAspectCreate<T>`
* crash disposing PushMeshDataSystem when graphics is not supported.
* RetainBlobAssetSystem no longer leaks memory over time
* `EntityCommandBuffer.Playback` no longer throws an exception when the ECB is played back in Burst but contains managed commands
* World.DestroyAllSystemsAndLogExceptions no longer makes the world unusable
* Fixed an issue where Job Safety Checks might be suppressed making identifying root causes for problems with Native Containers in jobs harder. As such, previously hidden safety issues may now start appearing in your code and should be corrected.
* Removed excessive stalls in IDEs that can occur due to Unity.Entities Source Generators running in the background while typing.
* Entities.ForEach in method with nullable parameter types.
* SetComponent in Entities.ForEach with argument that has an element accessor.

## [0.51.1] - 2022-06-27

### Changed

* Package Dependencies
  * `com.unity.jobs` to version `0.51.1`
  * `com.unity.platforms` to version `0.51.1`
  * `com.unity.collections` to version `1.4.0`
  * `com.unity.jobs` to version `0.70.0`

### Fixed

* An issue with source generator that was causing a compilation error when the generator was unable to create the temporary output folder.
* An issue with netcode source generator that was trying to run on assembly that did not have the right references and also when the project path was not assigned, making impossible to load the templates files.
* Entities.ForEach in method with nullable parameter types.
* SetComponent in Entities.ForEach with argument that has an element accessor.



## [0.51.0] - 2022-05-04

### Changed

* Package Dependencies
  * `com.unity.jobs` to version `0.51.0`
  * `com.unity.platforms` to version `0.51.0`
  * `com.unity.mathematics` to version `1.2.6`
  * `com.unity.collections` to version `1.3.1`
  * `com.unity.burst` to version `1.6.6`
* Increased the maximum number of shared components per entity from 8 to 16.
* Updated dependency on version of com.unity.roslyn package that will work with both Unity 2020 and Unity 2021.

### Fixed

* DOTS Entities throws a compilation error when using named arguments.
* Fix Create &gt; ECS &gt; System template now adds partial keyword.
* Fixed a possible memory stomp triggered by specific sequences of `ComponentDataFromEntity` or `BufferFromEntity` calls.
* EntityQuery.CopyFromComponentDataArray<T>() and EntityQuery.CopyFromComponentDataArrayAsync<T>() now correctly set the change version of any chunks they write to.
* If the value of the Parent component of an entity is changed while the previous parent entity was destroyed at the same time, an exception could be thrown during the next update of the transform system.
* Changes to ComponentData made outside of Systems will be properly detected by EntityQueries with changed version filters.
* `EntityQuery` objects are consistently compared, regardless of which version of `GetEntityQuery` is called.

### Added

* New `BufferTypeHandle.Update()` method. Rather than creating new type handles every frame in `OnUpdate()`, it is more efficient to create the handle once in a system's `OnCreate()`, cache it as a member on the system, and call its `.Update()` method from `OnUpdate()` before using the handle.
* SystemBase.GetEntityQuery can now take an EntityQueryDescBuilder.



## [0.50.1-preview.3] - 2022-04-28

### Changed

Release preparations, no functional changes.

## [0.50.1-preview.2] - 2022-04-20

### Changed

Release preparations, no functional changes.


## [0.50.1-preview.1] - 2022-04-07

### Added

* Documentation on EntityCommandBuffer public functions including ParallelWriter and EntityCommandBufferManagedComponentExtensions.
* Hybrid assemblies will not be included in DOTS Runtime builds.
* `[WithAll]` Attribute that can be added to a struct that implements IJobEntity. Adding additional required components to the existing execute parameter required components.
* `[WithNone]` Attribute that can be added to a struct that implements IJobEntity. Specifying which components shouldn't be on the entity found by the query.
* `[WithAny]` Attribute that can be added to a struct that implements IJobEntity. Specifying that the entity found by this query should have at least one of these components.
* `[WithChangeFilter]` Attribute that can be added to a struct that implements IJobEntity, as well as on component parameters within the signature of the execute method. This makes it so that the query only runs on entities, which has marked a change on the component specified by the `[WithChangeFilter]`.
* `[WithEntityQueryOptions]` Attribute that can be added to a struct that implements IJobEntity. Enabling you to query on disabled entities, prefab entities, and use write groups on entities.
* Diagnostic suppressor to ignore specific generation of CS0282 warnings due to codegen.
* SystemBase.GetBuffer takes an optional isReadOnly parameter.

### Changed

* DOTS Hierarchy now display SubScenes' state (opened, livelinked, closed or not loaded).
* When using `EntityManager.SetName` with a managed `string` as a parameter, if a string longer than 61 characters is used, the string will be truncated to fit within an `EntityName`,
* Improved the performance of the `EntityQuery` matching chunk cache in applications with many empty archetypes.
* Removed `IJobForeach`, due to long notice of deprecation
* Changed `LiveLinkPatcher` and `LiveLinkPlayerSystem` to use `IJobEntityBatch`, due to removal of `IJobForeach`
* Changed docs from `IJobForeach` and `IJobChunk` to refer to `IJobEntity`, and `IJobEntityBatch` respectivly
* Changed IJE out of `DOTS_EXPERIMENTAL`
* Update dependency on com.unity.roslyn to 0.1.3-preview (no longer ignore CS0282 warnings globally).
* Updated docs explaining how to use IJobEntity.
* Updated com.unity.roslyn to `0.2.1-preview`
* CheckDisposed method in EntityQueryEnumerator is now public
* The Current property for a generated Aspect Enumerator now has a conditional CheckDisposed call to identify when the property is being accessed with a disposed enumerator instance
* SystemBase.GetBuffer registers a job dependency for the IBufferElementData type specified.

### Removed

* Remove the LiveLink feature and its build component.
* DOTS Compiler Inspector. Functionality is now available via viewing generated code directly from Temp/GeneratedCode in the project directory.

### Fixed

* Bug with EntityCommandBuffer removing multiple components from multiple entities when the number of entities was more than 10.
* Defining `UNITY_DOTS_DEBUG` in standalone builds no longer triggers false positives from `AssertValidArchetype()`.
* When setting many long strings using `EntityManager.SetName`, the editor will properly handle the storage of these names.
* `EntityQuery.ToComponentDataArray<T>()` and `EntityQuery.CopyFromComponentDataArray<T>()` now detect potential race conditions against running jobs which access the component `T`. These jobs must be completed before the `EntityQuery` methods are called.
* WorldSystemFilter, DisableAutoCreation, and AlwaysUpdateSystem attributes working with ISystem systems
* Interface implemented execute methods now work with IJobEntity. Before this point you couldn't make an interface of `interface ITranslationExecute { void Execute(ref Translation translation) }` and implement it in an IJobEntity: `partial struct TranslationJob : IJobEntity, ITranslationExecute { void Execute(ref Translation translation) {} }`
* `.Schedule` and `.ScheduleParallel` Invocations for IJobEntity without a jobhandle now matches Entities.ForEach automatic  chain `SystemBase.Dependency` handling
* Dangling files left after a build using buildconfigs under certain circumstances
* Remove the double registers of world allocator when creating a world.
* Improved performance of Source Generators when run in IDEs.
* `ExclusiveEntityTransaction.AddComponent` and `ExclusiveEntityTransaction.RemoveComponent` will no longer throw  with the error message of `Must be called from the main thread`
* SGICE002 Issue with nesting `SetComponent(GetComponent)` for replaced syntax in Entities.ForEach.
* "System" in namespace causing issues with Entities.ForEach and other codegen.
* use of WithDisposeOnCompletion with Job.WithCode if a `using System.Collections` is missing.
* `EntityQuery.Singleton` methods work correctly when the query has multiple required component data
* `EntityQuery.ToEntityArray()`, `EntityQuery.ToComponentDataArray<T>()` and `EntityQuery.CopyFromComponentDataArray<T>()` now complete any jobs running against the query's component types before performing the requested operation. This fixes a race condition introduced in Entities 0.17 (and present in Entities 0.50).
* `IJobEntity` inside nested struct now works.
* `IJobEntity` now works inside namespaces that have `using` statements.
* Fixes Issue where UnityEngine.Component didn't work as ManagedComponents for IJobEntity.


## [0.50.0] - 2021-09-17

### Added

* **Window > DOTS > Entities** window to show all Entities in a world in real time, with ability to search, select each, and inspect it via the Inspector.
* **Window > DOTS > Components** window to show all Component types, with ability to search, select each, and inspect it via the Inspector.
* **Window > DOTS > Systems** window to show all Systems running in a world, categorized by System Group, with ability to search, select each, and inspect it via the Inspector.
* Introduced two new ECS specific **Window > Analysis > Profiler** modules:   * **Entities Structural Changes** profiler module can record which world/system produced a structural change, and how much time it cost per frame.   * **Entities Memory** profiler module can record which world/system allocates memory chunks, with additional details per archetype.
* `ArchetypeChunk.GetComponentDataPtrRO()` and `ArchetypeChunk.GetComponentDataPtrRW()` provide unsafe raw access to a chunk's component data, as a lower-overhead alternative to `ArchetypeChunk.GetNativeArray()`
* `ComponentTypeHandle.Update()` allows `ComponentTypeHandle`s to be created once at system creation time, and incrementally updated each frame before use.
* Adds clearer message when TypeManager hasn't been initialized yet, instead of only reporting a component type we don't know about has been requested.
* Disabled entities in Entity Window now have the same style as the disabled gameobjects in the gameobject hierarchy
* Go-to button to update Inspector content to reflect selected system and highlight the system in the Systems window if there is one open.
* It's now possible to specify an alignment when allocating an array with BlobBuilder
* Upgraded to burst 1.5.2
* Added go-to buttons to update Inspector content to reflect selected component and highlight the component in the Components window if there is one open.
* Routines to create unmanaged systems on worlds were made available for public use
* It's now possible for a scene to contain weak asset references to other scenes and prefabs. All referenced scenes and prefabs will automatically be included in a player build. The sample in "EntitiesSamples/Assets/Advanced/WeakAssetReferences" shows how to use weak asset references to scenes and prefabs.
* Incremental conversion now tracks GameObject names to rename Entities when they change.
* New method `CanBeginExclusiveEntityTransaction` on `EntityManager` to check whether or not a new exclusive entity transaction can be made.
* Wrapper functions are added in CollectionHelper to create/allocate NativeArray from custom allocator
* Entities.ForEach() will now accept a lambda with no parameters.
* WithSharedComponentFilter now also works with two shared component parameters.
* `EntityCommandBuffer` has an `IsEmpty` property, which returns true if at least one command has been successfully recorded.
* TryGetComponent in ComponentDataFromEntity
* TryGetBuffer in BufferFromEntity
* Entities journaling, which can record ECS past events and inspected from the static class `EntitiesJournaling` properties.
* Allow for easier viewing of `EntityCommandBuffer` within an IDE through a new debug proxy.
* Within an `EntityCommandBufferDebugView`, each command will have a summary of the action performed before expanding the command.
* SystemRef<T>.Update to allow updating unmanaged systems manually.
* Support WithScheduleGranularity with Entities.ForEach to allow per-entity scheduling
* `EntityCommandBuffer.Instantiate()` can now instantiate more than one `Entity` in a single command, writing the resulting entities to a `NativeArray<Entity>`.
* Support for fully-bursted Entities.ForEach.Run in ISystemBase systems.
* RateUtils.VariableRateManager to facilitate update rate
* DefaultWorld.BeginVariableRateSimulationEntityCommandBufferSystem
* DefaultWorld.VariableRateSimulationSystemGroup
* DefaultWorld.EndVariableRateSimulationEntityCommandBufferSystem
* Element EnableBlockFree is added to enum WorldFlags to indicate whether World.UpdateAllocator is enabled to free individual memory block.
* `ComponentTypes` has a new constructor variant that takes a `FixedList128Bytes<ComponentType>`, suitable for use in Burst-compiled code.
* `EntityCommandBuffer` has several new variants that target a `NativeArray<Entity>`, which may be more efficient in many cases than recording individual commands for individual entities.
* New Archetypes window that can display current archetype memory usage.
* IJob* types use SharedStatic so they can be burst compiled eventually
* Add ability to add missing partials during generation if `DOTS_ADD_PARTIAL_KEYWORD` scripting define is set.

### Changed

* Added a fast path for `IJobEntityBatch.RunWithoutJobs()` and `IJobEntityBatchWithIndex.RunWithoutJobs()` where query filtering is disabled, resulting up to a 30% reduction in performance overhead.
* Merged `com.unity.dots.editor` package into `com.unity.entities` package, effectively deprecating the DOTS Editor as a standalone package. All the DOTS Editor package functionality is now included when referencing the Entities package.
* DOTS Runtime now uses source generators for codegen.
* Make parts of EntityPatcher burst compatible to prepare for burst compilation of EntityPatcher for its performance improvement.
* `Entity.Equals(object compare)` now returns false if the `compare` object is null, rather than throwing a `NullReferenceException`.
* Made `DynamicBuffer` an always blittable type (even in the Editor with safety checks on), so that it can be passed by reference to Burst function pointers.
* BlobAssetStore.ComputeKeyAndTypeHash hash calculation reduced chance of collision
* Capped the maximum number of previewable GameObjects to 100 in the Entity Conversion Preview.
* Capped the maximum number of additional entities shown to 250 in the Entity Conversion Preview.
* Improved overall performance of the Entity Conversion Preview.
* Source generators are now used as the default mode of codegen for Entities.ForEach and Generated Authoring Component.  These can be disabled with `SYSTEM_SOURCEGEN_DISABLED` and `AUTHORINGCOMPONENT_SOURCEGEN_DISABLED` scripting defines if necessary.  The largest change is that generated code can now be inspected and debugged (when not bursted).  Generated code lives in Temp/GeneratedCode and can be stepped into with both Visual Studio and Rider.
* Documentation to highlight necessary prerequisites in the Build Configuration for making a profilable build.
* Entities window now shows prefab entities with a style similar to the one in the GameObject hierarchy
* Systems in the Entity inspector relationships tab are now sorted by scheduling order instead of creation order.
* Subscene headers are now loaded asynchronously and will no longer stall the main thread while loading.
* Performance of LiveTweaking has been improved.
* EntityDiffer capture entity changes when only entity's name is changed.
* With an IDE debugger, EntityQuery will present more information related to it. The raw view
* Debugging output for a `ComponentType` will present clearer info.
* The `batchesPerChunk` parameter to `IJobEntityBatch.ScheduleParallel()` has been replaced with a new `ScheduleGranularity` enum. Pass `ScheduleGranularity.Chunk` to distribute work to worker threads at the level of entire chunks (the default behavior). Pass `ScheduleGranularity.Entity` to distribute individual entities to each worker thread. This can improve load balancing in jobs that perform a large amount of work on a small number of entities.
*Make generate linker xml files deterministic in order.
* Within an IDE debugger, `ComponentSystemGroup` will present more relevant information. The raw view will be available for those who need the precise makeup of the class.
* `ComponentSystemGroup.RemoveSystemFromUpdateList` and `ComponentSystemGroup.RemoveUnmanagedSystemFromUpdateList` can now be used when `ComponentSystemGroup.EnableSystemSorting` is set to false
* Add debug checks to detect "placeholder" Entities created by one `EntityCommandBuffer` from being passed into a different `EntityCommandBuffer`.
* Clarified error message when calling `.Dispose()` on an `EntityQuery` created by `GetEntityQuery()`. This is always an error; these queries belong to the associated system, and should never be manually disposed. They will be cleaned up along with the system itself.
* Within an IDE debugger, `ArchetypeChunk` will present more relevant information. The raw view will be available for those who need the precise makeup of the struct.
* Within an IDE debugger, `EntityArchetype` will present more relevant information. The raw view will be available for those who need the precise makeup of the struct.
* IJobEntityBatch batchIndex parameter has been renamed to batchId. Documentation regarding what values to expect from this parameter have been updated accordingly.
* Changed: Within an IDE debugger, `EntityManager` will present more relevant information. The raw view will be available for those who need the precise makeup of the struct.
* Changed: Within an IDE debugger, an `ArchetypeChunk`'s OrderVersion and ChangeVersions per ComponentType will be easier to view.
* Changed: Within an IDE debugger, `SystemState` will present more relevant information. The raw view will be available for those who need the precise makeup of the struct.
* Changed: Within an IDE debugger, `World` will present more relevant information. The raw view will be available for those who need the precise makeup of the struct.
* `EntityCommandBufferSystem.CreateCommandBuffer()` now uses the `World.UpdateAllocator` to allocate command buffers instead of `Allocator.TempJob`. Allocations from this allocator have a fixed lifetime of two full World Update cycles, rather than being tied to the display frame rate.
* `EntityCommandBuffer.AddComponentForEntityQuery<T>()` now asserts if the provided `T` value contains a reference to a temporary `Entity` created earlier in the same command buffer; these Entities are not yet correctly patched with the correct final Entity during playback. This patching will be implemented in a future change.
*Removed `ComponentSystemBaseManagedComponentExtensions.HasSingleton{T}` - `ComponentSystemBase.HasSingleton{T}` already handles managed components.
* ISystemBase to ISystem
* New Query Window design.
* FixedRateUtils renamed to RateUtils
* IFixedRateManager renamed to IRateManager
* Records in the EntitiesJournaling feature now have OriginSystem that will be populated for which system requested the change. This information is helpful to determine where a deferred EntityCommandBuffer was recorded from.
* Improved diagnostic when a SubScene section entity does not meet one of the constraints during GameObject conversion.

### Deprecated

* In a future release, `IJobEntityBatch.RunWithoutJobsInternal()` and `IJobEntityBatchWithIndex.RunWithoutJobsInternal()` will be removed from the public API; as the names indicate, they are for internal use only. User code should use the non-`Internal()` variants of these functions.
* Several public functions in the EntityDataAccess have been deprecated. The new functions follow this convention <FunctionName>DuringStructuralChange(...)
* Entity Debugger has been marked as deprecated and will be removed in a future release. See new windows under **Window > DOTS**.

### Removed

* Deprecated functions in the EntityCommandBuffer for EntityQueries that were processed at Playback.
* GI Light baking in Closed SubScenes for now to remain consistent with Entity mesh renderers.
* `Unity.Entities.RegisterGenericJobTypeAttribute` has been moved to Unity.Jobs as `Unity.Jobs.RegisterGenericJobTypeAttribute`.
* StreamBinaryReader and StreamBinaryWriter are now internal
* Removed JobComponentSystem.  It has been replaced by SystemBase, which it much better tested and supported.  The Entities 0.5 upgrade guide explains how to upgrade from JobComponentSystem to SystemBase.
* IJobBurstSchedulable
* Job reflection data ILPP

### Fixed

* Fixed bug that caused compiler errors when users wrote multiple parts of the same partial type.
* Fixed a minor typo when generating the name of a conversion World.
* `[DisableAutoCreation]` is no longer inherited by subclasses, as documented.
* Improved the Entity inspector responsiveness.
* In Burst 1.5.0, fixed some player-build warnings that were caused by some entities code that contained `throw` statements not within `[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]` guarded functions.
* Performance of system safety checks greatly improved
* Systems window, Entities window and Components window all use the same minimum size.
* Systems window style issue in minimum size.
* Incremental conversion issue where children of disabled gameobjects are not properly re-enabled when parent is re-enabled.
* Fixed multiple inspectors issue with Entity Inspector where contents are duplicated in the existing inspectors.
* Fixed multiple inspectors issue with System Inspector where only the latest inspector has content while the rests are empty.
* We now use the TypeCache in TypeManager when initializing, which is about twice as fast as previously.
* Sometimes redundant error messages were logged, now fixed
* `EntityQuery` methods which limit their processing to a specific `NativeArray<Entity>` now work correctly if the `EntityQuery` uses chunk filtering.
* Certain code paths of `IJobEntityBatchWithIndex` were not storing the per-batch base entity indices at the correct byte offset.
* `IJobEntityBatchWithIndex.ScheduleInternal()` did not always work correctly with `EntityQuery` chunk filtering and `limitToEntityArray` both enabled.
* The variant of `IJobEntityBatchWithIndex.Run()` that took a `limitToEntityArray` parameter no longer asserts.
* `IJobEntityBatch` was redundantly applying chunk filtering at both schedule-time and execute-time.
* `EntityCommandBuffer` no longer leaks embedded entity arrays whose commands are never played back.
* Methods that take an `EntityQuery` now validate the query's validity.
* Add missing bounds checks for `EntityManager` methods.
* A `ComponentSystemGroup` that disables automatic system sorting no longer sets its "sort order is dirty" flag on every update.
* `EntityQuery.SetSingleton<T>()` will now throw an exception if the query only requested read-only access to type `T`.
* `EntityQuery.GetSingleton<T>()` and `EntityQuery.SetSingleton<T>()` now assert if `T` is a zero-sized component, avoiding a potential out-of-bounds memory access.
* Creating an `EntityQuery` with a non-empty list of `None` types will now match return a reference to an existing query if possible, instead of always creating a new query.
* `EntityCommandBuffer` playback of `*ForEntityQuery()` commands no longer leaks `AtomicSafetyHandle` allocations when collections checks are enabled
* Memory leak in BlobAssets when a World was disposed that had BlobAssets. Primarily seen when entering and exiting Playmode in the Editor.
* XXHash3 could potentially throw exceptions if Burst compilation was disabled. This is no longer that case.
* variant checkbox in GhostAuthoringComponent inspector was disabled if no variants for that component were present, not letting the user select the DoNotSerialize variation.
* SendToOwner not handled correctly by the client. Now both server and client do not send/receive the component only if the ghost present a GhostOwnerComponent.
* Baked lightmaps for SubScenes will no longer appear black due to lack of compiled shader features
* Clamp compute shader support detection to disallow GL < 4.3
* If you update multiple packages, create a new section with a new header for the other package.
* EntityDiffer no longer patches BlobAsset or Entity references from `UnityEngine.Object` types.
* Debugging of source-generated Entities.ForEach
* Some main-threads `EntityCommandBuffer` methods were missing the necessary safety checks.
* StructuralChangeProfiler should now have the proper scope when making changes through the EntityCommandBuffer and EntityManager.



## [0.20.0] - 2021-09-17

### Added

* **Window > DOTS > Entities** window to show all Entities in a world in real time, with ability to search, select each, and inspect it via the Inspector.
* **Window > DOTS > Components** window to show all Component types, with ability to search, select each, and inspect it via the Inspector.
* **Window > DOTS > Systems** window to show all Systems running in a world, categorized by System Group, with ability to search, select each, and inspect it via the Inspector.
* Introduced two new ECS specific **Window > Analysis > Profiler** modules:   * **Entities Structural Changes** profiler module can record which world/system produced a structural change, and how much time it cost per frame.   * **Entities Memory** profiler module can record which world/system allocates memory chunks, with additional details per archetype.
* `ArchetypeChunk.GetComponentDataPtrRO()` and `ArchetypeChunk.GetComponentDataPtrRW()` provide unsafe raw access to a chunk's component data, as a lower-overhead alternative to `ArchetypeChunk.GetNativeArray()`
* `ComponentTypeHandle.Update()` allows `ComponentTypeHandle`s to be created once at system creation time, and incrementally updated each frame before use.
* Adds clearer message when TypeManager hasn't been initialized yet, instead of only reporting a component type we don't know about has been requested.
* Disabled entities in Entity Window now have the same style as the disabled gameobjects in the gameobject hierarchy
* Go-to button to update Inspector content to reflect selected system and highlight the system in the Systems window if there is one open.
* It's now possible to specify an alignment when allocating an array with BlobBuilder
* Upgraded to burst 1.5.2
* Added go-to buttons to update Inspector content to reflect selected component and highlight the component in the Components window if there is one open.
* Routines to create unmanaged systems on worlds were made available for public use
* It's now possible for a scene to contain weak asset references to other scenes and prefabs. All referenced scenes and prefabs will automatically be included in a player build. The sample in "EntitiesSamples/Assets/Advanced/WeakAssetReferences" shows how to use weak asset references to scenes and prefabs.
* Incremental conversion now tracks GameObject names to rename Entities when they change.
* New method `CanBeginExclusiveEntityTransaction` on `EntityManager` to check whether or not a new exclusive entity transaction can be made.
* Wrapper functions are added in CollectionHelper to create/allocate NativeArray from custom allocator
* Entities.ForEach() will now accept a lambda with no parameters.
* `Entities.ForEach()` can now be called with `.WithDeferredPlaybackSystem<T>()` or `WithImmediatePlayback()`.
    - These two methods may only be used in conjunction with the newly added `EntityCommands` type, which should be passed as a parameter to the `Entities.ForEach()` lambda function.
    - `EntityCommands` contains several methods whose counterparts can be found in the `EntityCommandBuffer` type. Using `EntityCommands` inside `Entities.ForEach()` triggers code generation that automatically creates, schedules, plays back, and disposes entity command buffers.
* WithSharedComponentFilter now also works with two shared component parameters.
* `EntityCommandBuffer` has an `IsEmpty` property, which returns true if at least one command has been successfully recorded.
* TryGetComponent in ComponentDataFromEntity
* TryGetBuffer in BufferFromEntity
* Entities journaling, which can record ECS past events and inspected from the static class `EntitiesJournaling` properties.
* Allow for easier viewing of `EntityCommandBuffer` within an IDE through a new debug proxy.
* Within an `EntityCommandBufferDebugView`, each command will have a summary of the action performed before expanding the command.
* SystemRef<T>.Update to allow updating unmanaged systems manually.
* Support WithScheduleGranularity with Entities.ForEach to allow per-entity scheduling
* `EntityCommandBuffer.Instantiate()` can now instantiate more than one `Entity` in a single command, writing the resulting entities to a `NativeArray<Entity>`.
* Support for fully-bursted Entities.ForEach.Run in ISystemBase systems.
* RateUtils.VariableRateManager to facilitate update rate
* DefaultWorld.BeginVariableRateSimulationEntityCommandBufferSystem
* DefaultWorld.VariableRateSimulationSystemGroup
* DefaultWorld.EndVariableRateSimulationEntityCommandBufferSystem
* Element EnableBlockFree is added to enum WorldFlags to indicate whether World.UpdateAllocator is enabled to free individual memory block.
* `ComponentTypes` has a new constructor variant that takes a `FixedList128Bytes<ComponentType>`, suitable for use in Burst-compiled code.
* `EntityCommandBuffer` has several new variants that target a `NativeArray<Entity>`, which may be more efficient in many cases than recording individual commands for individual entities.
* New Archetypes window that can display current archetype memory usage.
* IJob* types use SharedStatic so they can be burst compiled eventually
* Add ability to add missing partials during generation if `DOTS_ADD_PARTIAL_KEYWORD` scripting define is set.

### Changed

* Added a fast path for `IJobEntityBatch.RunWithoutJobs()` and `IJobEntityBatchWithIndex.RunWithoutJobs()` where query filtering is disabled, resulting up to a 30% reduction in performance overhead.
* Merged `com.unity.dots.editor` package into `com.unity.entities` package, effectively deprecating the DOTS Editor as a standalone package. All the DOTS Editor package functionality is now included when referencing the Entities package.
* DOTS Runtime now uses source generators for codegen.
* Make parts of EntityPatcher burst compatible to prepare for burst compilation of EntityPatcher for its performance improvement.
* `Entity.Equals(object compare)` now returns false if the `compare` object is null, rather than throwing a `NullReferenceException`.
* Made `DynamicBuffer` an always blittable type (even in the Editor with safety checks on), so that it can be passed by reference to Burst function pointers.
* BlobAssetStore.ComputeKeyAndTypeHash hash calculation reduced chance of collision
* Capped the maximum number of previewable GameObjects to 100 in the Entity Conversion Preview.
* Capped the maximum number of additional entities shown to 250 in the Entity Conversion Preview.
* Improved overall performance of the Entity Conversion Preview.
* Source generators are now used as the default mode of codegen for Entities.ForEach and Generated Authoring Component.  These can be disabled with `SYSTEM_SOURCEGEN_DISABLED` and `AUTHORINGCOMPONENT_SOURCEGEN_DISABLED` scripting defines if necessary.  The largest change is that generated code can now be inspected and debugged (when not bursted).  Generated code lives in Temp/GeneratedCode and can be stepped into with both Visual Studio and Rider.
* Documentation to highlight necessary prerequisites in the Build Configuration for making a profilable build.
* Entities window now shows prefab entities with a style similar to the one in the GameObject hierarchy
* Systems in the Entity inspector relationships tab are now sorted by scheduling order instead of creation order.
* Subscene headers are now loaded asynchronously and will no longer stall the main thread while loading.
* Performance of LiveTweaking has been improved.
* EntityDiffer capture entity changes when only entity's name is changed.
* With an IDE debugger, EntityQuery will present more information related to it. The raw view
* Debugging output for a `ComponentType` will present clearer info.
* The `batchesPerChunk` parameter to `IJobEntityBatch.ScheduleParallel()` has been replaced with a new `ScheduleGranularity` enum. Pass `ScheduleGranularity.Chunk` to distribute work to worker threads at the level of entire chunks (the default behavior). Pass `ScheduleGranularity.Entity` to distribute individual entities to each worker thread. This can improve load balancing in jobs that perform a large amount of work on a small number of entities.
*Make generate linker xml files deterministic in order.
* Within an IDE debugger, `ComponentSystemGroup` will present more relevant information. The raw view will be available for those who need the precise makeup of the class.
* `ComponentSystemGroup.RemoveSystemFromUpdateList` and `ComponentSystemGroup.RemoveUnmanagedSystemFromUpdateList` can now be used when `ComponentSystemGroup.EnableSystemSorting` is set to false
* Add debug checks to detect "placeholder" Entities created by one `EntityCommandBuffer` from being passed into a different `EntityCommandBuffer`.
* Clarified error message when calling `.Dispose()` on an `EntityQuery` created by `GetEntityQuery()`. This is always an error; these queries belong to the associated system, and should never be manually disposed. They will be cleaned up along with the system itself.
* Within an IDE debugger, `ArchetypeChunk` will present more relevant information. The raw view will be available for those who need the precise makeup of the struct.
* Within an IDE debugger, `EntityArchetype` will present more relevant information. The raw view will be available for those who need the precise makeup of the struct.
* IJobEntityBatch batchIndex parameter has been renamed to batchId. Documentation regarding what values to expect from this parameter have been updated accordingly.
* Changed: Within an IDE debugger, `EntityManager` will present more relevant information. The raw view will be available for those who need the precise makeup of the struct.
* Changed: Within an IDE debugger, an `ArchetypeChunk`'s OrderVersion and ChangeVersions per ComponentType will be easier to view.
* Changed: Within an IDE debugger, `SystemState` will present more relevant information. The raw view will be available for those who need the precise makeup of the struct.
* Changed: Within an IDE debugger, `World` will present more relevant information. The raw view will be available for those who need the precise makeup of the struct.
* `EntityCommandBufferSystem.CreateCommandBuffer()` now uses the `World.UpdateAllocator` to allocate command buffers instead of `Allocator.TempJob`. Allocations from this allocator have a fixed lifetime of two full World Update cycles, rather than being tied to the display frame rate.
* `EntityCommandBuffer.AddComponentForEntityQuery<T>()` now asserts if the provided `T` value contains a reference to a temporary `Entity` created earlier in the same command buffer; these Entities are not yet correctly patched with the correct final Entity during playback. This patching will be implemented in a future change.
*Removed `ComponentSystemBaseManagedComponentExtensions.HasSingleton{T}` - `ComponentSystemBase.HasSingleton{T}` already handles managed components.
* ISystemBase to ISystem
* New Query Window design.
* FixedRateUtils renamed to RateUtils
* IFixedRateManager renamed to IRateManager
* Records in the EntitiesJournaling feature now have OriginSystem that will be populated for which system requested the change. This information is helpful to determine where a deferred EntityCommandBuffer was recorded from.
* Improved diagnostic when a SubScene section entity does not meet one of the constraints during GameObject conversion.

### Deprecated

* In a future release, `IJobEntityBatch.RunWithoutJobsInternal()` and `IJobEntityBatchWithIndex.RunWithoutJobsInternal()` will be removed from the public API; as the names indicate, they are for internal use only. User code should use the non-`Internal()` variants of these functions.
* Several public functions in the EntityDataAccess have been deprecated. The new functions follow this convention <FunctionName>DuringStructuralChange(...)
* Entity Debugger has been marked as deprecated and will be removed in a future release. See new windows under **Window > DOTS**.

### Removed

* Deprecated functions in the EntityCommandBuffer for EntityQueries that were processed at Playback.
* GI Light baking in Closed SubScenes for now to remain consistent with Entity mesh renderers.
* `Unity.Entities.RegisterGenericJobTypeAttribute` has been moved to Unity.Jobs as `Unity.Jobs.RegisterGenericJobTypeAttribute`.
* StreamBinaryReader and StreamBinaryWriter are now internal
* Removed JobComponentSystem.  It has been replaced by SystemBase, which it much better tested and supported.  The Entities 0.5 upgrade guide explains how to upgrade from JobComponentSystem to SystemBase.
* IJobBurstSchedulable
* Job reflection data ILPP

### Fixed

* Fixed a minor typo when generating the name of a conversion World.
* `[DisableAutoCreation]` is no longer inherited by subclasses, as documented.
* Improved the Entity inspector responsiveness.
* In Burst 1.5.0, fixed some player-build warnings that were caused by some entities code that contained `throw` statements not within `[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]` guarded functions.
* Performance of system safety checks greatly improved
* Systems window, Entities window and Components window all use the same minimum size.
* Systems window style issue in minimum size.
* Incremental conversion issue where children of disabled gameobjects are not properly re-enabled when parent is re-enabled.
* Fixed multiple inspectors issue with Entity Inspector where contents are duplicated in the existing inspectors.
* Fixed multiple inspectors issue with System Inspector where only the latest inspector has content while the rests are empty.
* We now use the TypeCache in TypeManager when initializing, which is about twice as fast as previously.
* Sometimes redundant error messages were logged, now fixed
* `EntityQuery` methods which limit their processing to a specific `NativeArray<Entity>` now work correctly if the `EntityQuery` uses chunk filtering.
* Certain code paths of `IJobEntityBatchWithIndex` were not storing the per-batch base entity indices at the correct byte offset.
* `IJobEntityBatchWithIndex.ScheduleInternal()` did not always work correctly with `EntityQuery` chunk filtering and `limitToEntityArray` both enabled.
* The variant of `IJobEntityBatchWithIndex.Run()` that took a `limitToEntityArray` parameter no longer asserts.
* `IJobEntityBatch` was redundantly applying chunk filtering at both schedule-time and execute-time.
* `EntityCommandBuffer` no longer leaks embedded entity arrays whose commands are never played back.
* Methods that take an `EntityQuery` now validate the query's validity.
* Add missing bounds checks for `EntityManager` methods.
* A `ComponentSystemGroup` that disables automatic system sorting no longer sets its "sort order is dirty" flag on every update.
* `EntityQuery.SetSingleton<T>()` will now throw an exception if the query only requested read-only access to type `T`.
* `EntityQuery.GetSingleton<T>()` and `EntityQuery.SetSingleton<T>()` now assert if `T` is a zero-sized component, avoiding a potential out-of-bounds memory access.
* Creating an `EntityQuery` with a non-empty list of `None` types will now match return a reference to an existing query if possible, instead of always creating a new query.
* `EntityCommandBuffer` playback of `*ForEntityQuery()` commands no longer leaks `AtomicSafetyHandle` allocations when collections checks are enabled
* Memory leak in BlobAssets when a World was disposed that had BlobAssets. Primarily seen when entering and exiting Playmode in the Editor.
* XXHash3 could potentially throw exceptions if Burst compilation was disabled. This is no longer that case.
* variant checkbox in GhostAuthoringComponent inspector was disabled if no variants for that component were present, not letting the user select the DoNotSerialize variation.
* SendToOwner not handled correctly by the client. Now both server and client do not send/receive the component only if the ghost present a GhostOwnerComponent.
* Baked lightmaps for SubScenes will no longer appear black due to lack of compiled shader features
* Clamp compute shader support detection to disallow GL < 4.3
* EntityDiffer no longer patches BlobAsset or Entity references from `UnityEngine.Object` types.
* Debugging of source-generated Entities.ForEach
* Some main-threads `EntityCommandBuffer` methods were missing the necessary safety checks.
* StructuralChangeProfiler should now have the proper scope when making changes through the EntityCommandBuffer and EntityManager.

## [0.19.0] - 2021-03-15

### Added

* Usage of `RegisterBindingAttribute` through `[GenerateAuthoringComponent]` when the user opts in to using Sourcegen
* Names assigned to entities are now available by default in all builds, not just within the Editor. To strip Entity debug names from builds, define `DOTS_DISABLE_DEBUG_NAMES` in your project's build configuration.
* Added support for loading entity scene headers asynchronously (disabled by default).
* `IJobChunk`, `IJobEntityBatch`, `IJobEntityBatchWithIndex`, and `IJobParallelForDefer` now have `ByRef()` versions of all `.Schedule()` and `.Run()` methods. These should be used in cases where the corresponding job struct is too large to pass by value to the existing methods (~10KB or larger). Functionality is otherwise the same as the existing methods.
* Unmanaged `EntityQueryDescBuilder` allows Burst code to construct entity queries
* Entity debug names will be disabled by default in release builds. The new build component "Enable Entity Names" can be added to a release build configuration to re-enable these names, if the application has some use for them.
* `SystemRef<T>` and `SystemHandle<T>` offer a way to keep track of unmanaged systems
* Update `com.unity.properties` and `com.unity.serialization` to `1.7.0`
* (EXPERIMENTAL) A new scripting define (`UNITY_DOTS_DEBUG`) enables a subset of inexpensive API validation and error handling in standalone builds.

### Changed

* Updated platform packages to `0.12.0-preview.8`
* Manual testing, repro fixed
* Renamed LiveLink view modes (Under DOTS Menu in the Editor) to something more clear. DOTS->Conversion Settings:
* New version of Roslyn compiler to enable source-generator features.
*Burst compatibility tests added for EntityQueryManager + EntityQuery
* When DOTS_DISABLED_DEBUG_NAMES is enabled, `EntityCommandBuffer.SetName` will have minimal overhead.
*Cleaned up many uses of UNITY_2020_2_OR_NEWER, UNITY_DOTSPLAYER, UNITY_DOTSRUNTIME, and NET_DOTS
*Added `StableHash` to `EntityArchetype`, which represent an archetype stable hash calculated from the component types stable hash.

### Deprecated

* SceneBundleHandle.UseAssetBundles is deprecated. It no longer had any use and was never meant to be public.
* `EntityQuery.CompareQuery()` with managed `EntityQueryDesc`. Use the variant that accepts an `EntityQueryDescBuilder` instead.
* `WordStorage`,`NumberedWords`, and `Words` are marked for deprecation, as these storages are not recommended for public use.

### Removed

* Removed `struct FastEquality.Layout`.
* EntitiesBurstCompatibilityTests has been removed and placed into the Entities test project.

### Fixed

* Enabling a hierarchy would sometimes fail to create a child array on the parent entity.
* Fix entities not rendering past -32785 units on the X Axis.
* Issue with StorageInfoFromEntity which causes exception due to incorrect Read access permissions to the Entity type
* Blob assets built with BlobBuilder should now always be properly aligned.
* Missing 'catalog.bin' file on Android when building a DOTS game with SubScenes.
* `BlobAssetReferenceData` did not implement `IEquality` interface which could result in `BlobAssetReference` comparisons to fail even though the underlying data pointers are the same.
* Added missing closing braces for suggested fixes in ComponentSystemSorter warnings
* scene streaming will no longer raise a NullPointerException if a previous load failed due to an error.
* `EntityCommandBuffer.AddComponent()` for managed components no longer triggers a double-dispose on the component.
* Fix issue where no timing information was displayed for struct systems in the entity debugger
* Struct systems implementing ISystemBaseStartStop now don't receive double stop notifications
* SDF fonts are now rendered with correct anti-aliasing on WASM


## [0.18.0] - 2021-01-26

### Added

* Toggle support
* AddSharedComponentForEntityQuery<T>(EntityQuery, T) and SetSharedComponentForEntityQuery<T>(EntityQuery, T). Both methods 'capture' the set of entities to modify at record time, not playback time.
* `BufferAllocatorVirtualMemory` for virtual memory backed allocations of fixed size buffers.
* `BufferAllocatorHeap` for heap backed allocations of fixed size buffers.
* `EntityCommandBuffer` methods for managed components that perform a query at record time (instead of at playback time): `AddComponentObjectForEntityQuery` and `SetComponentObjectForEntityQuery`.
* Added new method `GetEntityQueryDesc` to `EntityQuery`. It can be used to retrieve an `EntityQueryDesc` from which the query can be re-created.
* Support for adding HybridComponents in conversion using DstEntityManager.AddComponentObject(). Support is limited to built-in types provided by Unity already, and not custom components.
* standalone builds by default opt out of using entity debug name storage. When opted out, `EntityManager.GetName` will return a default string, and `EntityManager.SetName` is a no-op. To override this default and include debug names in standalone builds, define `DOTS_USE_DEBUG_NAMES` in the Player "scripting defines" field.
* `EntityCommandBuffer.SetName`, allowing users to set a debug name on an `Entity` created from `EntityCommandBuffer.CreateEntity`
* GameObjectSceneUtility.AddGameObjectSceneReferences() that can be used in custom Entity Bootstrap code to ensure currently loaded Game Object Scenes are added as references to the GameObjectSceneSystem for cases where these scenes were loaded without the GameObjectSceneSystem (eg in the Editor and pressing Play).
* New `StorageInfoFromEntity` struct which allows reading information about how an entity is stored (such as its `ArchetypeChunk` and index inside of the chunk), from within a job. You can also use `StorageInfoFromEntity` to check if an `Entity` exists, or if it has been destroyed.
* ConcurrentSectionStreamCount & MaximumWorldsMovedPerUpdate can now be set on SceneSectionStreamingSystem in order to tweak the throttling of scene section streaming

### Changed

* Sample scene now has text that updates based on toggle state.
* EntityCommandBuffer.ParallelWriter no longer throws when recording on the main thread. (The throw was added to prevent 'improper' use, but there's nothing actually harmful about recording to an ECB.ParallelWriter on the main thread.)
* Updated platform packages to `0.11.0-preview.10`.
* `BufferAllocator` which selects between `BufferAllocatorVirtualMemory` or `BufferAllocatorHeap`, depending on platform capabilities.
* improved performance of EntityManager.SetName() and EntityManager.GetName()
*Added EntityManager.GetName(FixedString64)
*Added EntityManager.SetName(Entity, out FixedString64)
*Fixed an issue where passing an invalid or deleted `Entity` into `EntityManager.GetName` or `EntityManager.SetName` would result in a valid operation. The functions now throw an `ArgumentException` if the Entity is invalid.
* `EntityManager.GetName()` returns the relevant string containing "ENTITY_NOT_FOUND" when the given `Entity` does not exist in the `World`
* AssetDependencyTracker is now faster when there are multiple async artifacts pointing to the same guid
* ResolveSceneReferenceSystem has a fast path for instantiated entity scenes (eg. for scene tile streaming)
* Update minimum editor version to 2020.2.1f1-dots.3
*Updated platform packages to `0.11.0-preview.11`
*Updated properties package to `1.6.0-preview`
*Updated serialization package to `1.6.2-preview`
* Updated `com.unity.burst` to `1.4.4`
* `EntityQuery` methods (`.ToEntityArray()`, `.ToComponentDataArray()`, and `.CopyFromComponentDataArray()`) distribute their work across multiple worker threads for sufficiently large workloads.
* EntityScene now relies on a new File format that reduces copying of memory while deserializing
* The constructor of MemoryBinaryReader now takes the length of the memory block and checks for out of bounds reads
* BinaryReader and BinaryWriter interfaces now have a Position property that can be set to seek within the stream
* Performance of ResolveSceneReferenceSystem is improved
* Improved performance of `EntityQuery.IsEmptyIgnoreFilter`, `GetSingleton()`, `GetSingletonEntity()`, and `SetSingleton()` for infrequently-changing queries.

### Deprecated

* The `EntityCommandBuffer` methods which perform an `EntityQuery` at playback are now deprecated. Instead use the methods whose names end with "ForEntityQuery". These "ForEntityQuery" methods perform the query at 'record time' (when the method is called).
* GameObjectConversionSystem.AddHybridComponent()
* `StreamBinaryReader` and `StreamBinaryWriter` have been deprecated and will no longer be part of the public API. Please provide your own implementation if you need it.

### Removed

* Removed deprecated `GameObjectEntity.CopyAllComponentsToEntity`, `EntityManager.Instantiate(GameObject)`, `GameObjectConversionUtility.ConvertIncremental`, `ScriptBehaviourUpdateOrder.UpdatePlayerLoop`, `ScriptBehaviourUpdateOrder.IsWorldInPlayerLoop` and `TypeManager.TypeCategory.Class`
* Removed these expired deprecated APIs: `EntitySelectionProxy.EntityControlSelectButtonHandler`, `EntitySelectionProxy.EntityControlSelectButton`, `EntitySelectionProxy.EntityManager`, `EntitySelectionProxy.OnEntityControlSelectButton`, and `EntitySelectionProxy.SetEntity`
* Removed expired APIs `ArchetypeChunk.BatchEntityCount`
* Removed expired fixed time step APIs `ComponentSystemGroup.UpdateCallback`, `FixedStepSimulationSystemGroup.MaximumDeltaTime`, `FixedRateUtils.EnableFixedRateWithCatchup/EnableFixedRateSimple/DisableFixedRate`
* Removed expired `Frozen` component
* Removed expired `GameObjectConversionSettings.Fork` method and `GameObjectConversionSettings.NamespaceId` field
* Removed expired `EntityGuid.NamespaceId` field
* Removed expired `GameObjectConversionUtility.GetEntityGuid` method

### Fixed

* TargetGraphic field in toggle and button now converted
* Removed various testing components from the entities package from the Add Component menu.
* `EntityQuery`'s matching chunk cache could briefly become stale in some cases.
* Inspecting an invalid entity in the inspector will no longer throw an exception.
* Rare issue with SubScenes left visible in a Scene they were not present in anymore (Editor only)
* StableTypeHash for `UnityEngine.Object` component types to not collide when the same `typeof(myObjectType).FullName` is present in multiple assemblies loaded in the editor.
* Fixed potential out-of-bounds memory access when changing a chunk's archetype in-place.
* Fixed a bug where the same name assembly loaded in the editor could result in the EntitiesILPostProcessor throwing an ArgumentException due to duplicate keys being used to add to a dictionary.
* SceneSystem.GetGUID would fail to match equivalent file paths with lowercase/uppercase mismatches.
* Ensure warning against using systems with `[ExcludeAlways]` does not trigger with versions of Unity including and after 2020.2.
* Fixed an issue where precompiledreferences defined to an empty array `[]` in .asmdef files would potentially throw errors in the buildprogram on OSX machines.
* You can now debug the contents of `BlobAssetReference<T>`
* Removed excessive warnings due to incompatible fields not being able to be inserted into the `BindingRegistry`
* You can now group conversion systems and process after load systems by marking their parent groups with the corresponding `WorldFilterFlags`
* EntityDebugger no longer hides worlds with duplicated names
* A bug where `EntityQuery.IsEmpty` did not respect change filters being modified in pending jobs when Job Threads are used
* Entities are now written to YAML in the order that they appear in a chunk
* A `NullReferenceException` when using `IJobEntityBatch` after calling `EntityManager.DestroyEntity(EntityQuery)`.
* EntityManager.GetCreatedAndDestroyedEntities() no longer returns ChunkHeader entities in the new entity list.
* Fixed a bug where streaming in scenes instantiated multiple times with multiple sections and references between those sections, entity ids wouldn't get correctly remapped, thus we would reference entities in a different scene / tile
* Chunk.SequenceNumber (Internal API) is now guranteed to be unique when deserializing a chunk from disk. SequenceNumbers are used in internal world diffing methods to detect changes.
* Fix `GetSingletonEntity()` HRV2 error when destroying all the entities in the EntityManager
* Compilation error when removing the built-in module Particle System
* AutoLoad disabled on SubScenes works correctly again
* Regression where an entity could not be renamed multiple times.
* Fixed Entity inspector throwing when a dynamic buffer is removed from an inspected entity.
* Tiny.UI "Hidden" computation fixed.
* EntityManagerDebugView now also displays meta entities (for chunk components) and shared components.
* using `BlobBuilder` in generic methods no longer raises a safety error
* Many methods that use `IJob` were marked as `[NotBurstCompatible]` to reflect their true Burst compatibility.

## [0.17.0] - 2020-11-13

### Added

* ISystemBase interface for making struct based systems that allow Burst compiling main thread update
* New UnsafeBufferAccessor struct that permit to un-typed and unsafe access the dynamic buffers pointers.
* New ArchetypeChunk.GetUnsafeAccessor public API that allow to retrieve dynamic buffers unsafe accessors using DynamicTypeHandle.
* safety check in DynamicComponentDataArrayReinterpret<T> that throw an ArgumentException if used to access the component data for IBufferDataElement type
* ComponentSystemBase.TryGetSingleton
* ComponentSystemBase.TryGetSingletonEntity
* Tests for Tiny.UI transformations.
* Added documentation for incremental conversion and dependencies
* New scheduling API for `IJobEntityBatch` which limits the resulting batches to an input `NativeArray<Entity>`
* BlobAssetStore. AddUniqueBlobAsset. A much simpler approach to managing blob assets during conversion. BlobAssetComputationContext continues to be the recommended approach for scalable blob asset generation.
* UnsafeUntypedBlobAsset gives a non-generic untyped blob that can be created and later casted to a specific BlobAssetType. This can be used for storing multiple types of blob assets in the same container.
* GameObjectConversionSystem.IsBuildingForEditor returns true when building data that will be loaded by the editor (As opposed to building data that will be loaded by the standalone player). This can be used to strip out editor only debug data.
* GameObjectConversionSystem.BuildConfigGUID returns the GUID of the build config that is used by this conversion context
* Tiny.UI support for 9-Slice (including sprite sheet support)
* `DynamicSharedComponentHandle` and related methods for accessing shared components without compile time type information.
* The public static method `EntitySelectionProxy.CreateInstance` was added. It creates, configures, and returns a valid instance of `EntitySelectionProxy`.
* The public static method `EntitySelectionProxy.SelectEntity` was added. It creates, configures, and selects an instance of `EntitySelectionProxy`, without returning it.
* All the public properties and methods of `EntitySelectionProxy` have been documented.
* Tiny.UI support for text alignment
* Tiny.UI support for multi-line text
* TypeManager will now store whether or not an `ISharedComponentData` is managed or unmanaged.
* EntityScenesInBuild class that allows registering EntityScenes that are generated via a custom asset importer into the build. This is used by the Environment system to generate streamable tile data that is generated procedurally from tile inputs.
* New EntityCommandBuffer methods that affect a set of entities matching a query. Unlike existing methods, these new methods 'capture' the entities from the query at record time rather than playback time: the array of entities is stored in the command, and then playback of the command affects all entities of the array. The methods are `AddComponentForEntityQuery(EntityQuery, ComponentType)`, `AddComponentForEntityQuery(EntityQuery, ComponentTypes)`, `RemoveComponentForEntityQuery(EntityQuery, ComponentType)`, `RemoveComponentForEntityQuery(EntityQuery, ComponentTypes)`, `DestroyEntitiesForEntityQuery(EntityQuery)`.
* EntityManager.Debug.GetEntitiesForAuthoringObject and EntityManager.Debug.GetAuthoringObjectForEntity. They provide a convenient API to map game object authoring & entity runtime representation.
* New `ComponentSystemGroup.EnableSystemSorting` property allows individual system groups to opt out of automatic system sorting. **PLEASE NOTE:** Certain system update order constraints are necessary for correct DOTS functionality. Disabling the automatic system sorting should be only be a last resort, and only on system groups with full control over which systems they contain.
* Entities.WithFilter(NativeArray<Entity> filteredEntities) allows for filtering with a set of specific entities in addition to the EntityQuery requirements
* Added Live Conversion debug logging to more easily see what is reconverted (enable from the menu `DOTS/LiveLink Mode/Incremental Conversion Logging`)

### Changed

* Update burst to 1.4.1.
* Improved the performance of ILPostProcessor type resolution.
* ProcessAfterLoadGroup is now public.  This group runs after a subscene is loaded.
* `Unity.Transforms` systems now use `IJobEntityBatch` instead of `IJobChunk`. Expect modest performance gains due to the new job type's lower scheduling overhead, depending on the workload size.
* When `DOTS/Live Link Mode/Live Conversion in Edit Mode` is active in 2020.2 or later, conversion is now incremental
* Removed deprecated `Entities.ForEach.WithDeallocateOnJobCompletion`.  Please use `Entities.ForEach.WithDisposeOnCompletion` instead.
* Fixed livelink patching for `BlobAssetReference<T>` fields in managed components and shared components.
* Updated package `com.unity.platforms` to version `0.9.0-preview.15`.
* `TypeManager.Equals` and `TypeManager.GetHashCode` performance has been improved when operating on blittable component types.
* BlobAsset and entity patching for managed IComponentData & ISharedComponentData now use an early out if the class is known to not contain any blob assets or entity references
* Managed class IComponentData now supports patching of entity references in EntityCommandBuffer.AddComponent.
* Improved EntityManager.GetCreatedAndDestroyedEntities performance by introducing an internal entity creation / destruction version number that is used to early out when calling GetCreatedAndDestroyedEntities
* TypeManger.TypeInfo.Debug has been removed. TypeName has been moved directly into TypeInfo and renamed to DebugTypeName.
* Nested or variant prefabs used in a scene now correctly trigger reimports on subscenes when the parents parent prefab changes
* Update minimum editor version to 2020.1.2f1
* `EntitySelectionProxy` was streamlined to ensure that its usage does not override inspector locking behaviour and respects the Undo / Redo stack. With the new workflow, there is a 1:1 relationship between an Entity and its EntitySelectionProxy. Static utility methods were added to support this new workflow.
* Rename `TypeManager.IsSharedComponent` to `IsSharedComponentType` and add `IsManagedType`
*Enabled generic systems to be instantiated in non-tiny dots runtime
* Updated platform packages to version `0.10.0-preview.1`.
* Made SystemBase/JobComponentSystem classes partial in preparation of use of Roslyn source generators for code-generation (more to come).

### Deprecated

* Forking of `GameObjectConversionSettings` is no longer supported
* The public delegate `EntitySelectionProxy.EntityControlSelectButtonHandler` has been deprecated.
* The public event `EntitySelectionProxy.EntityControlSelectButton` has been deprecated.
* The public method `EntitySelectionProxy.SetEntity` has been deprecated.
* The public method `EntitySelectionProxy.OnEntityControlSelectButton` has been deprecated.
* The public property `EntitySelectionProxy.EntityManager` has been deprecated. Use `EntitySelectionProxy.World.EntityManager` manager instead. This change was made to remove boilerplate checks in the code.
* Deprecated `Frozen` component as it is no longer in use

### Removed

* Removed deprecated proxy component types. CopyTransformToGameObject, CopyTransformFromGameObject and CopyInitialTransformFromGameObject now use [GenerateAuthoringComponent] instead.
* Removed expired `EntityManager.IsCreated` API
* Removed expired API to compare `EntityManager` to `null` (`EntityManager` is a struct now)
* Removed deprecated types and methods: `NativeArraySharedValue<S>`, implicit `EntityQuery` conversion to `null`,  `ComponentDataFromEntity.Exists` and `BufferFromEntity.Exists`, `ArchetypeChunkArray.GetComponentVersion`, `IJobEntityBatch.ScheduleSingle`, `IJobEntityBatch.ScheduleParallelBatch`, `EntityManager.LockChunk`, `EntityManager.UnlockChunk`, `World.AllWorlds`, `World.CreateSystem`, all `GetArchetypeChunkX` methods, `EntityCommandBuffer.ToConcurrent` and `EntityManager.CreateChunk`
* `ComponentSystemBase.ExecutingSystemType` has been removed. With the introduction of unmanaged systems, this information has been incorrect. Furthermore, there cannot be a `static` global property for this since multiple worlds might execute at the same time. If you need this information, consider passing it manually.

### Fixed

* Wrong query and check in ACS_DynamicComponentDataArrayReinterpret
* Fixed ICE (internal compiler error) thrown when storing into a field in reference type in bursted/scheduled lambda.
* `EntityQuery.ToEntityArray` will work when temp memory is passed in an a parameter for allocator
* `EntityQuery.ToComponentDataArrayAsync` and `EntityQuery.CopyFromComponentDataArrayAsync` will throw errors if user tries to use Temp memory containers.
* Hybrid Component Lights flickering when LiveLink edited.
* Fix crash when using singleton access methods with types with generic arguments.
* Code generation for indexers in structs with [BurstCompatible] attribute.
* Fixed potential `JobHandle` leak if an exception was thrown while scheduling an `IJobForEach`.
* Fixed that `DynamicBuffer.RemoveAtSwapBack` only copied the first byte of its element data
* Updating the shadow world via the EntityDiffer is now using Burst
* The 'New Sub Scene' menu item is no longer missing in the 'Create' drop down of the Hierarchy .
* Overwriting Sub Scene file when creating new Sub Scene no longer logs an error but instead overwrites the user selected file.
* Fixed livelink patching for `BlobAssetReference<T>` fields in managed components and shared components.
* Fixed entities getting lost during LiveLink when moving GameObjects between multiple subscenes
* Deprecated call to UnityWebRequest.isNetworkError in Unity.Scenes.FileUtilityHybrid
* Generic jobs now get reflection data generated in more cases
* Generic jobs will always work to schedule in editor (but may require attributes in Burst scheduling cases)
* Selecting entities in the Entity Debugger window will now respect the locked state of the inspector.
* several bugs where writing EntityBinaryFiles was not resulting in deterministic files. It is now guranteed that if entities are constructed in the same order, it will result in the same binary exact file.
* Fixed a case where LiveLink would sometimes leave dangling entities when a scene is opened and closed repeatedly
* `TypeManager.InitializeAllComponentTypes` no longer uses `DateTime.Now`, which can be very slow in players
* Structural changes right after scheduling a job in a `SystemBase` no longer crash a player
* Sorting a `ComponentSystemGroup` now correctly sorts any child groups, even if the parent group is already sorted.
* The subscene inspector no longer allows you to unload section 0 if another section is still loaded, and it also disallows loading any section before section 0 is loaded
* `EntityManager.CopyAndReplaceEntitiesFrom` no longer fails when the Entity capacity of the destination is larger than the capacity of the source
* Hybrid components on disabled GameObjects are now also converted
* Children of a nested Parent not updating LocalToWorld if Parent's LocalToWorld was changed by a custom system with an archetype containing a WriteGroup for LocalToWorld
* `EntityQuery` APIs which take an input `NativeArray<Entity>` for filtering (such as `ToEntityArray()`) can now be called with ReadOnly `NativeArray<Entity>` without throwing an exception.

### Upgrade guide

* managed class IComponentData now supports patching of entity references in EntityCommandBuffer.AddComponent. This can result in a significant performance regression if there might be an entity on the managed component when playing the command buffer. If a managed component has a reference to another class that is not sealed it is unprovable that there may or may not be entity references on it. Thus we have to walk the whole class data to apply it. If there is in fact no entity reference on a class referenced from a managed component, then it is recommended to mark the referenced class as sealed, so that the type manager can prove that there is no entity references present on the managed component and thus completely skip all relatively slow entity patching code.



## [0.16.0] - 2020-09-24

### Added

* EntityManager method CreateEntity(EntityArchetype, int). Unlike existing overloads of CreateEntity, this new overload takes no Entity array and returns no Entity array, so it avoids waste and bother in cases where callers don't need the actual Entity values.
* Special handling for Cameras and Colliders in preparation for root scene conversion, though they are disabled by default still
* `World.MaximumDeltaTime` now controls the maximum deltaTime that is reported to a World.
* Exception's stacktrace are recorded in conversion logs.
* Add `IFixedRateManager` interface for fixed-timestep implementations. See `FixedRateUtils.cs` for reference implementations.
* Add `ComponentSystemGroup.FixedRateManager` property, to store the current active `IFixedRateManager` implementation.
* Added `SceneSystem.IsSectionLoaded` to enable querying if a specific section of a scene is loaded.
* `EntityQuery.SetOrderVersionFilter()` and `EntityQuery.AddOrderVersionFilter()` which can be used to filter the Order Version independently from the Changed Version of a chunk.
* DOTS naming standards to CONVENTIONS.md
* libcurl Stevedore artifact registration
* Mathematics tests are turned on in CI

### Changed

* Improved performance of `EntityQuery.ToEntityArray()`
* Platform packages updated to `0.9.0-preview.9`
* Properties packages updated to `1.5.0-preview`
* The job safety system has be moved to NativeJobs as C++ code
* The UnsafeUtility memory allocators have been moved to NativeJobs
* improved performance of EntityQuery.CopyFromComponentDataArray
* changed chunk size from 16128 bytes (16 KB - 256 bytes) to exactly 16384 bytes (16 KB).
* `TypeManager.GetFieldInfo` now takes in a `Type` to return an `NativeArray<FieldInfo>`. The passed in type must be registered to have field information generated explicitly via the `[GenerateComponentFieldInfo]` assembly attribute.
* `IJobEntityBatch` and `IJobEntityBatchWithIndex` now quietly skip batches whose size is zero. This can happen legitimately if the requested `batchesPerChunk` value is higher than the entity count for a particular chunk.
*Removed deprecated `ArchetypeChunk.Locked()` method.
*Deprecated `ArchetypeChunk.BatchEntityCount` property. The `.Count` property should be used instead.
* Removed usage of TempAssetCache for some livelink cases. Now these files are under SceneDependencyCache instead, so there is only one magic directory to deal with until we can remove it completely in future Unity versions.
*Fixed Reduced overhead of `IJobEntityBatchWithIndex` prefiltering by up to 20% if `batchesPerChunk` is 1, or if `EntityQuery` filtering is disabled.

### Deprecated

* `FixedStepSimulationSystemGroup.MaximumDeltaTime` has been deprecated. The maximum delta time is now stored in `World.MaximumDeltaTime`. For better compatibility with UnityEngine, the new field applies to both the fixed-rate and variable-rate timesteps.
* `ComponentSystemGroup.UpdateCallback` is deprecated. Instead, the group calls the `ShouldGroupUpdate()` method on its `FixedRateManager` property (if non-null) to accomplish the same effect.
* `FixedRateUtils.EnableFixedRateCatchUp()`, `FixedRateUtils.EnableFixedRateSimple()`, and `FixedRateUtils.DisableFixedRate()`. These functions were used to set the deprecated `ComponentSystemGroup.UpdateCallback` field; instead, just set `ComponentSystemGroup.FixedRateManager` directly.

### Removed

* Old <2020.1 ifdef blocks in LiveLink scene culling code
* Deprecated legacy sort order code in ComponentSystemGroup was removed

### Fixed

* Removed GC-allocations in `SceneSystem` and `SceneSectionStreamingSystem` that were happening every frame
* Issue with invalid GUID in SubScene importer causing Player LiveLink to stall waiting for an asset it will never get
* Hybrid component transform syncing was not working when entity order changed
* Hybrid components being editable when in Preview Scenes (by selecting gizmos)
* Fixed an issue in 2020.2 which caused `NativeContainer` min-max ranges to be incorrectly patched when scheduling and `IJobChunk` or `IJobEntityBatch` with a "Single" or "Run" schedule call.
* Fields marked with `RestrictAuthoringInputTo` can now be set to `None` in the inspector
* The Entities package now uses a faster code path for `CreateArchetypeChunkArray()` more consistently.
* Retroactively added a changelog entry that notes a behavior change in `RemoveComponent(EntityQuery, ComponentTypes)`. See 'Change' entry under 0.14.0.
* Generic job reflection data across assemblies would sometimes not work
* Fixed HLOD component throwing out of bounds exception when setup incorrectly against LODGroup.
* Scene section meta data now works in standalone builds again
* Native memory leak in EditorSubSceneLiveLinkSystem when failing to apply patches
* Generic job registration is more robust when generic parameters
* LiveLink will not generate errors on scenes that have not yet loaded scene sections
* Corrected inverted test in `IJobEntityBatchWithIndex` if EntityQuery filtering is enabled.
* `EntityManger.AddComponent<T>(EntityQuery entityQuery)` and `EntityManger.AddComponentData<T>(EntityQuery entityQuery, NativeArray<T> componentArray)` is 2x faster.
* Reduced overhead of `IJobEntityBatch` execution by 5-10% if `batchesPerChunk` is 1.

## [0.15.0] - 2020-08-26

### Added

* More detailed profiling of individual EntitiesILPostProcessors to Editor log (look for lines with "EILPP" marker).
* Added `EntityQuery.IsEmpty` function which respects the `EntityQueryFilter`s

### Changed

* DOTS Runtime now supports `Burst.CompileFunctionPointer` allowing for lambda job and `EntityCommandBuffer` playback to be Burst compiled.
* `World.Time.ElapsedTime` is now initialized to zero when the World is created.
* Bumped Burst to 1.3.5.
* Updated package `com.unity.platforms` to version `0.9.0-preview.1`.
* Improved performance of `EntityQuery.CreateArchetypeChunkArray()`
* Updated packages `com.unity.properties` and `com.unity.serialization` to version `1.4.3-preview`.
* improved performance of `EntityManager.AddComponent(NativeArray<Entity>,ComponentType)` and `EntityManager.RemoveComponent(NativeArray<Entity>,ComponentType)`
* `TypeCategory.Class` is deprecated in favour of `TypeCategory.UnityEngineObject`
* A `ComponentTypes` value can no longer consist of duplicate types. (The collections safety checks look for duplicates and throw an exception.)

### Deprecated

* Deprecated `RequiresEntityConversion` attribute since it is not used anymore

### Removed
* Removed previously deprecated `LiveLinkBuildImporter.GetHash`

### Fixed

* Limit of 128 million Entities per World instituted.
* Fixed `[GenerateAuthoringComponent]` on `IBufferElementData` throwing a NullReferenceException at initialization when Live Conversion is active.
* Fixed an issue which caused an exception to occur when `IJobEntityBatchWithIndex` is scheduled with `.Run()`
* Fixed a few methods not correctly preserving shared component values: `EntityManager.RemoveComponent(EntityQuery, ComponentTypes)`, `EntityCommandBuffer.RemoveComponent(EntityQuery, ComponentTypes)`, `EntityCommandBuffer.AddComponent(EntityQuery, ComponentTypes)`.
* Fixed a bug with `BufferFromEntity<T>` which caused it to incorrectly update the version number of the buffer when marked `ReadOnly`
* `TypeManager.GetWriteGroupTypes()` no longer leaks `AtomicSafetyHandle` instances each time it is called.
* Fixed an issue where `GetEntityInfo()` can potentially crash the editor if the user passes in an invalid Entity
* Fixed buffer element authoring component not showing up in DOTS Compiler Inspector.
* `TypeManager.Initialize` now uses the TypeCache in Editor, improving the time it takes to enter playmode when no script compilation occurs. (1800ms -> 200ms)
* Fix `Entities.ForEach` `WithDisposeOnJob` method to work correctly with NativeArrays when scheduled with `.Run`.
* Fix IL2CPP build error with local methods used inside of Entities.ForEach lambdas.



## [0.14.0] - 2020-08-04

### Added

* Added `IsEmpty` property to `DynamicBuffer`.
* Added deduplication for asset bundles generated for subscenes.
* Added new `EntityManager` methods: `AddComponent(EntityQuery, ComponentTypes)`, which adds multiple components to all entities matching a query; and `RemoveComponent(Entity, ComponentTypes)`, which removes multiple components from a single entity. (`AddComponent(Entity, ComponentTypes)` and `RemoveComponent(EntityQuery, ComponentTypes)` already existed. This patch just fills in a few 'missing' methods.)
* Added `EntityManagerDifferOptions.UseReferentialEquality` which instructs the Differ to compare entity fields by GUID and blob asset reference fields by hash instead of bitwise equality

### Changed
* `BlockAllocator` is now backed by memory retrieved from platform virtual memory APIs. Platforms which do not support virtual memory will fall back to malloc/free.
* `IJobEntityBatch.ScheduleSingle` is being renamed to `IJobEntityBatch.Schedule` to match our naming guidelines for job scheduling.
* When `DefaultWorldInitialization.Initialize()` adds the default World's system groups to the Unity player loop, it now bases its modifications on the current player loop instead of the default player loop. This prevents the Entities package from accidentally erasing any previous player loop modifications made outside the package.
* `DefaultWorldInitialization.DomainUnloadOrPlayModeChangeShutdown()` now removes all existing `World`s from the Unity player loop before destroying them. If a `World` that was added to the player loop is destroyed manually prior to domain unload, it must also be removed from the player loop manually using `ScriptBehaviourUpdateOrder.RemoveWorldFromPlayerLoop()`.
* Updated package `com.unity.platforms` to version `0.7.0-preview.8`.
* `EntityManager.CreateEntity()`, `EntityManager.SetArchetype()`, and `EntityCommandBuffer.CreateEntity()` no longer accept the value returned by `new EntityArchetype()` because it's invalid. Same for `EntityCommandBuffer.CreateEntity()` and `EntityCommandBuffer.ParallelWriter.CreateEntity()`. Always use `EntityManager.CreateArchetype()` instead of `new EntityArchetype()` to create `EntityArchetype` values. (Ideally, the `EntityArchetype` constructor wouldn't be public, but C# doesn't allow that for a struct.)
* Subscene Inspector now uses a table format to allow easier management of multiple subscenes
* RemoveComponent(EntityQuery, ComponentTypes) now removes all provided components at once from all entities matching the query. Previously, the components were removed one at a time in a loop. This was less efficient and could affect which entities matched the query in subsequent loop iterations in unexpected ways.

### Deprecated
* `IJobEntityBatch.ScheduleParallelBatched` is being deprecated in favor of adding a batching parameter to `IJobEntityBatch.ScheduleParallel`
* `ScriptBehaviourUpdateOrder.UpdatePlayerLoop()` is being deprecated in favor of `ScriptBehaviourUpdateOrder.AddWorldToPlayerLoop()`. Due to slightly different semantics, a direct automated API update is not possible: the new function always takes a `PlayerLoopSystem` object to modify, does not call `UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop()`, and does not create the top-level system groups if they don't exist.
* `ScriptBehaviourUpdateOrder.IsWorldInPlayerLoop(World)` is being deprecated in favor of `ScriptBehaviourUpdateOrder.IsWorldInCurrentPlayerLoop(World)`.

### Removed

* Removed obsolete `ScriptBehaviourUpdateOrder.CurrentPlayerLoop`. Use `UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop()` instead.
* Removed obsolete `ScriptBehaviourUpdateOrder.SetPlayerLoop()`. Use `UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop()` instead.

### Fixed

* Setting the Scene Asset on a Subscene would sometimes fail to trigger an import/conversion because the default ECS world was missing.
* Fixed crash when using Singleton access methods (GetSingleton, SetSingleton, etc.) with a generic parameter as argument.
* Fixed an issue which caused WebGL not to work, and could produce this error message on IL2CPP-based backends:
```
NotSupportedException: To marshal a managed method, please add an attribute named 'MonoPInvokeCallback' to the method definition. The method we're attempting to marshal is: Unity.Entities.SystemBase::UnmanagedUpdate
```


## [0.13.0] - 2020-07-10

### Added
* Added new `EntityCommandBuffer` methods: `AddComponent(Entity, ComponentTypes)` and `AddComponent(EntityQuery, ComponentTypes)` for adding multiple components in one call. (`EntityManager` has an equivalent of the first already and will get an equivalent of the second later.)
* Added new `EntityCommandBuffer` methods: `RemoveComponent(Entity, ComponentTypes)` and `RemoveComponent(EntityQuery, ComponentTypes)` for removing multiple components in one call. (`EntityManager` will get equivalents in the future.)
* Added new `IJobEntityBatchWithIndex` job interface, a variant of `IJobEntityBatch` that provides an additional `indexOfFirstEntityInQuery` parameter, which provides a per-batch index that is the aggregate of all previous batch counts.
* Added `MaximumDeltaTime` property to `FixedStepSimulationSystemGroup`, used similarly to `UnityEngine.Time.maximumDeltaTime` to control how gradually the application should recover from large transient frame time spikes.
* Added new player loop management functions to the `ScriptBehaviourUpdateOrder` class:
  * `AppendSystemToPlayerLoopList()`: adds a single ECS system to a specific point in the Unity player loop.
  * `AddWorldToPlayerLoop()`: adds the three standard top-level system groups to their standard player loop locations.
  * `IsWorldInPlayerLoop(World, PlayerLoopSystem)`: searches the provided player loop for any systems owned by the provided World.
  * `RemoveWorldFromPlayerLoop()`: removes all systems owned by a World from the provided player loop.
  * `AddWorldToCurrentPlayerLoop()`, `IsWorldInCurrentPlayerLoop()`, and `RemoveWorldFromCurrentPlayerLoop()`: wrappers around the above functions that operate directly on the currently active player loop.

### Changed

* Updated minimum Unity Editor version to 2020.1.0b15 (40d9420e7de8)
* Profiler markers for `EntityCommandBuffer.Playback` from `EntityCommandBufferSystem`s now include name of the system that recorded the `EntityCommandBuffer`.
* Bumped burst to 1.3.2 version.
* EntityQuery commands for `AddComponent`, `RemoveComponent`, and `DestroyEntity` in the EntityCommandBuffer now use Burst during Playback.
* IJobChunk and Entities.ForEach ScheduleParallel has been optimized in case there is no EntityQuery filtering necessary (Shared component or change filtering)
* `TypeManager.GetSystems()` now returns an `IReadOnlyList<Type>` rather than a `List<Type>`
* Updated package `com.unity.platforms` to version `0.6.0-preview.4`.
* `EntityContainer` will now allow to write data back to the entity.
* Updated package `com.unity.properties` and `com.unity.serialization` to version `1.3.1-preview`.

### Fixed

* Fixed warning treated as error in the case that a warning is emitted for Entities.ForEach passing a component type as value.
* Fixed paths displayed in IL post-processing error messages to be more consistent with Unity error messages.
* Fixed exceptions being thrown when inspecting an entity with a GameObject added through `EntityManager.AddComponentObject`.
* Fixed `DCICE002` error thrown during IL post-processing when `Entities.ForEach` contains multiple Entities.ForEach in same scope capturing multiple variables.
* `EntityManager`'s `AddComponent()`, `RemoveComponent()`, and `CopyEntitiesFrom()` methods no longer throw an error if their input is a `NativeArray<Entity>` allocated with `Allocator.Temp` whose length is >10 elements.
* Throw error when Entities.ForEach has an argument that is a generic DynamicBuffer.
* Re-adding a system to a `ComponentSystemGroup` immediately after removing it from the group now works correctly.
* `ComponentSystemGroup.Remove()` is now ignored if the target system is already enqueued for removal, or if it isn't in the group's update list in the first place.
* Fixed IL post-processing warnings being emitted with "error" title.
* Fixed "Invalid IL" error when try/finally block occurs in `Entities.ForEach` lambda body or cloned method (usually occurs with `using` or `foreach` and `WithoutBurst`).
* Fixed `Unexpected error` when `Job.WithCode` is used with `WithStructuralChanges` (now throw an error).
* Fixed a bug where `Unity.Scenes.EntityScenesPaths.GetTempCachePath()` could return invalid strings
* Fixed freezing of editor due to accessing the `EntityManager` property within Rider's debugger
* Fixed a bug where calling `SetArchetype` on an entity containing a component with `ISystemStateComponentData` may sometimes incorrectly throw an `ArgumentException`

### Known Issues

* This version is not compatible with 2020.2.0a17. Please update to the forthcoming alpha.

## [0.12.0] - 2020-05-27

### Added
* Added `BufferFromEntity.DidChange()`, with the same semantics as the existing `ComponentDataFromEntity.DidChange()`.
* Added `BufferFromEntity.HasComponent()`, with the same meaning as the existing `.Exists()` call (which is now deprecated).
* Added `WorldSystemFilterFlags.All` flag to include allow calls to `TypeManager.GetSystems()` to return all systems available to the runtime including systems decorated with [DisableAutoCreation].
* Added `DynamicBuffer.RemoveAtSwapBack()` and `DynamicBuffer.RemoveRangeSwapBack()`
* Added `Entities.WithDisposeOnCompletion` to correctly Dispose of types after running an `Entities.ForEach`.
* Added `SystemBase.GetBuffer/GetBufferFromEntity` that are patched so that they can be used inside of `Entities.ForEach`.
* Added BlobAllocator.SetPointer to allow having a blob pointer to an object which already exists in the blob. This can be used for example to reference a parent node in a tree.
* Added `GameObjectConversionSystem.CreateAdditionalEntity` overload that allows to create multiple new entities at once.
* Added a new `FixedStepSimulationSystemGroup`. Systems in this group update with a fixed timestep (60Hz by default), potentially running zero or several times per frame to "catch up" to the actual elapsed time. See the `FixedTimestepSystemUpdate` sample scene for an example of how to use this system group.

### Changed
* Updated minimum Unity Editor version to 2020.1.0b9 (9c0aec301c8d)
* `World.Dispose()` now destroys all the world's systems before removing the `World` from the "all worlds" list.
* Extended `TypeManager.GetSystems()` to support getting systems filtered by any and/or all `WorldSystemFilterFlags`.
* Updated package `com.unity.platforms` to version `0.4.0-preview.5`.
* Updated package `com.unity.burst` to version `1.3.0-preview.12`.
* `Unity.Entities.DefaultWorldInitialization` has been moved from the `Unity.Entities.Hybrid` assembly into the `Unity.Entities` assembly.
* `Unity.Entities.DefaultWorldInitialization.Initialize()` now returns the initialized `World.DefaultGameObjectInjectionWorld` object.
* `ArchetypeChunkComponentType` has been renamed to `ComponentTypeHandle`
* `ArchetypeChunkComponentTypeDynamic` has been renamed to `DynamicComponentTypeHandle`
* `ArchetypeChunkBufferType` has been renamed to `BufferTypeHandle`
* `ArchetypeChunkSharedComponentType` has been renamed to `SharedComponentTypeHandle`
* `ArchetypeChunkEntityType` has been renamed to `EntityTypeHandle`
* `ArchetypeChunkComponentObjects` has been renamed to `ManagedComponentAccessor`
* `Unity.Entities.EditorRenderData` has been moved from the `Unity.Entities.Hybrid` assembly to the `Unity.Entities` assembly.
* `Unity.Scenes.Hybrid` has been renamed to `Unity.Scenes`. Any asmdefs referring to the old assembly name must be updated. The ScriptUpgrader will take care of updating `using` namespace imports.
* `Unity.Entities.SceneBoundingVolume` has moved from the `Unity.Entities.Hybrid` assembly to the `Unity.Scenes` assembly and `Unity.Scenes` namespace. Any asmdefs referring to the old assembly name must be updated. The ScriptUpgrader will take care of updating `using` namespace imports.
* `EntityCommandBuffer.Concurrent` has been renamed to `EntityCommandBuffer.ParallelWriter`.
* `EntityCommandBuffer.ToConcurrent()` has been renamed to `EntityCommandBuffer.AsParallelWriter()` and now returns `EntityCommandBuffer.ParallelWriter` (renamed from `Concurrent`).
* Duplicate query parameters (from `WithAll` and lambda parameters) are now allowed in `Entities.ForEach` (they are now sanitized for the user).
* If a change filter is used in `Entities.ForEach` with `WithChangeFilter`, the component type will automatically get added to the query.
* Add additional warnings around conflicting use of `WithNone`, `WithAll`, `WithAny` and lambda parameters in `Entities.ForEach`.
* Warn if a user passes a struct component parameter by value to their lambda in `Entities.ForEach` (since changes won't be reflected back to the underlying component).
* An exception is now thrown during serialization if a shared component containing entity references is encountered.
* EntityScene generation (Happening in a background process) is now integrated with the async progress bar to indicate when entity data is being generated. The code that tracks dependencies for entity scenes, determines when to regenerated them in the editor is significantly cheaper now.
* When safety checks are enabled `EntityManager.AddComponent(NativeArray<Entity>, ComponentType)` now throws `ArgumentException` instead of `InvalidOperationException` when any of the entities are invalid
* `FixedRateUtils` timesteps outside the range 0.0001 to 10.0f are now clamped, for consistency with `UnityEngine.Time.fixedDeltaTime`.
* Added `[NoAlias]` attributes to the `DynamicBuffer` native container to explain its aliasing to Burst.
* Updated package `com.unity.properties` to version `1.3.0-preview`.
* Updated package `com.unity.serialization` to version `1.3.0-preview`.

### Deprecated

* Deprecated `WithDeallocateOnJobCompletion` for `Entities.ForEach`; Use `WithDisposeOnCompletion` instead.
* Deprecated `ComponentDataFromEntity.Exists()`; Use `.HasComponent()` instead.
* Deprecated `BufferFromEntity.Exists()`; Use `.HasComponent()` instead.

### Fixed
* Fixed data corruption bug in Entities.WithStructuralChange().ForEach() when components on entities that are about to be processed get removed before we process the entity.
* `EntityManager.SetName` now causes less GC memory allocations
* `Entities.WithDeallocateOnJobCompletion()` now correctly deallocates data at the end instead of after the first chunk when used with `Run()` (Note that Entities.WithDeallocateOnJobCompletion() has been deprecated in favor of Entities.WithDisposeOnCompletion().)
* `Entities.WithDeallocateOnJobCompletion()` now deallocates data when used with `WithStructuralChanges()` (Note that Entities.WithDeallocateOnJobCompletion() has been deprecated in favor of Entities.WithDisposeOnCompletion().)
* Creating section section meta data during conversion will no longer trigger an invalid warning about missing SceneSection components
* Fixed a crash that could happen when calling `EntityDataAccess.PlaybackManagedChanges` from bursted code after a domain reload
* Fixed compilation issue when compiling multiple `Entities.ForEach` in the same method that use captured variables from different scopes.
* Fix to unexpected error when using capturing Entities.ForEach inside a method with a generic argument (error now correctly indicates that it is not currently supported).
* UnloadAllAssets will no longer unload assets referenced by shared or managed components.
* Fixed load order of JobReflection static methods which were causing `InvalidOperationException: Reflection data was not set up by code generation` exceptions in player builds.
* Beginning an exclusive entity transaction while another one is in progress now doesn't fail silently anymore but throws an exception
* Fixed race condition in the Chunks component version
* Shared component version is now always based off a global version. Thus Destroying all usage of a specific shared component and recreating it will now result in a changed version number.
* The `Loading Entity Scene failed` message now contains more information for why the scene failed to load
* `GameObjectEntityEditor` no longer throws exceptions when selecting a prefab
* Components on GameObjects with invalid MonoBehaviours no longer cause exceptions when used as hybrid components
* Deleting a GameObject with `ConvertToEntity` before converting it no longer throws an exception
* Errors happening during scene streaming now contain the callstack
* The `jobIndex` parameter passed to EntityCommandBuffer.ParallelWriter methods has been renamed to `sortKey` to better express its purpose. Its functionality is unchanged.
* Invalid uses of the new `OrderFirst` and `OrderLast` fields in the `[UpdateInGroup]` attribute are now detected and reported. Some previous spurious warnings regarding these fields are now suppressed.
* Greatly reduced the garbage generated by redrawing the chunk utilization histograms in the entity debugger
* Improved performance of `EntityManager.AddComponent(NativeArray<Entity>, ComponentType)` when safety checks are enabled
* `EntityManager.RemoveComponent(NativeArray<Entity>, ComponentType)` now always checks that the component can be removed, not just for the case of few entities
* Fixed issues where `FixedRateUtils.FixedRateCatchUpManager` was occasionally not running its first update at elapsedTime = 0.0.

## [0.11.0] - 2020-05-04

### Added

* Added `ArchetypeChunkComponentObjects<T>.Length`

### Changed

* Updated package `com.unity.burst` to version `1.3.0-preview.11`
* Improves `ComponentType.ToString` names in Dots Runtime to provide the full type name when available, and if not, defaults to the StableTypeHash.
* `EntityManager.Version` and `EntityManager.GlobalSystemVersion` will throw if the manager is not valid instead of returning 0.

### Deprecated

* Deprecate system sorting via virtual functions and direct modification of system list. There are now two new properties on the `UpdateInGroup` attribute: `OrderFirst` and `OrderLast`. Setting either of these properties to true will group the system together with others tagged in the same way, and those systems will sort in a subgroup by themselves. This change was needed to enable Burst compatible systems in the future.
* Deprecate `EntityManager.IsCreated` which cannot be efficiently implemented with EntityManager as a struct. For the (hopefully rare) cases where you need to determine if an entity manager is still valid, use `World.IsCreated` instead as the world and entity manager are always created and destroyed in tandem.

### Removed

* Removed expired API `EntityQuery.CreateArchetypeChunkArray(Allocator, out JobHandle)`.
* Removed expired API `EntityQuery.ToEntityArray(Allocator, out JobHandle)`.
* Removed expired API `EntityQuery.ToComponentDataArray<T>(Allocator, out JobHandle)`.
* Removed expired API `EntityQuery.CopyFromComponentDataArray<T>(NativeArray<T>, out JobHandle)`.

### Fixed

* Improved JobsDebugger errors invvolving the `EntityManager`. requires Unity 2020.1.0b5 or later
* Fixed potential infinite loop in `FixedRateUtils.FixedRateCatchUpManager` if these callbacks were enabled on the first frame of execution.
* When `FixedRateUtils.FixedRateCatchUpManager` or `FixedRateUtils.FixedRateSimpleManager` are enabled, the first update is now guaranteed to take place at elapsedTime = 0.0.
* Asset dependencies registered via `GameObjectConversionSystem.DeclareAssetDependency` are now checked for validity, inbuilt assets are ignored
* Improved performance of the `EntityPatcher` when applying changes to large amounts of entities
* The script template for ECS systems now uses `SystemBase` instead of `JobComponentSystem`
* Fixed instantiation of entities with multiple hybrid components causing corruption of the managed store.
* Removed remapping of entity fields in hybrid components during instantiation (this wasn't supposed to happen).
* Fix crash when trying to remap entity references within recursive types.
* Serialization and LiveLink now supports blob asset references in shared components.
* Serialization and LiveLink now supports blob asset references in managed components.
* Fix bug with `EntityQuery.CopyFromComponentDataArray` causing it to behave like `ToComponentDataArray`

## [0.10.0] - 2020-04-28

### Added

* Added `GetOrderVersion()` to ArchetypeChunk. Order version bumped whenever structural change occurs on chunk.
* Added `GetComponentDataFromEntity` method that streamlines access to components through entities when using the `SystemBase` class.  These methods call through to the `ComponentSystemBase` method when in OnUpdate code and codegen access through a stored `ComponentDataFromEntity` when inside of `Entities.ForEach`.
* Added support for WorldSystemFilterFlags.ProcessAfterLoad which enable systems to run in the streaming world after a entity section is loaded.
* Added `DynamicBuffer.CopyFrom()` variant that copies from a `NativeSlice`
* Added `DynamicBuffer.GetUnsafeReadOnlyPtr()`, for cases where only read-only access is required.
* Added PostLoadCommandBuffer component which can be added to scene or section entities to play back a command buffer in the streaming world after a entity section is loaded. Adding it to the scene entity will play back the command buffer on all sections in the scene.
* Added `WorldSystemFilterFlags.HybridGameObjectConversion` and `WorldSystemFilterFlags.DotsRuntimeGameObjectConversion`to annotate conversion systems to be used specifically for hybrid or dots runtime.
* Added missing profiler markers when running an `Entities.ForEach` directly with `.Run`.
* Added support for storing metadata components in the header of converted subscenes. Components can be added to the section entities requested with `GameObjectConversionSystem.GetSceneSectionEntity`. The added components are serialized into the entities header and will be added to the section entities at runtime when the scene is resolved.
* ResolvedSectionEntity buffer component is now public and can be used to access metadata components on a resolved scene entity.
* Added 'Clear Entities Cache' window under the DOTS->Clear Entities Cache menu. By default, all caches are enabled for clearing. Clearing Live Link Player cache wipes the local player cache of a livelink build next time it connects to the editor. Clearing Entity Scene cache invalidates all Entity Scenes (SubScenes) causing them to reimport on next access. Clearing Live Link Assets cache, causing the next Live Link connection to reimport all on-demand live link assets.

### Changed

* Bumped Burst version to improve compile time and fix multiple bugs.
* ChangeVersions behavior more consistent across various entry points.
* Updated package `com.unity.properties` to version `1.1.1-preview`.
* Updated package `com.unity.serialization` to version `1.1.1-preview`.
* Updated package `com.unity.platforms` to version `0.3.0-preview.4`.
* `ConvertToEntity` no longer logs a warning if there are multiples of a given authoring component on the converted GameObject, so it is now compatible with conversion systems that can support multiples.
* Improved the StableTypeHash calculation used when serializing components to be more resilient. The hash will now properly invalidate serialized data should component data layout change as a result of `[StructLayout(LayoutKind.Explict)]`, as well as if a nested component field's data layout changes.
* Make it possible to use Entities.ForEach with >8 parameters if you supply your own delegate type

### Deprecated

* EntityManager.UnlockChunk deprecated
* Adding components to entities converted from GameObjects using proxy components has been deprecated, please use the new conversion workflows using `GameObjectConversionSystem` and `IConvertGameObjectToEntity`
* `ComponentDataProxyBaseEditor`, `DynamicBufferProxyBaseEditor` from `Unity.Entities.Editor` deprecated
* `ComponentDataProxy<T>`, `ComponentDataProxyBase`, `DynamicBufferProxy<T>`, `SharedComponentDataProxy<T>`, `SceneSectionProxy` from `Unity.Entities.Hybrid`deprecated
* `MockDataProxy`, `MockDynamicBufferDataProxy`, `MockSharedDataProxy`, `MockSharedDisallowMultipleProxy` from `Unity.Entities.Tests` deprecated
* `CopyInitialTransformFromGameObjectProxy`, `CopyTransformFromGameObjectProxy`, `CopyTransformToGameObjectProxy`, `LocalToWorldProxy`, `NonUniformScaleProxy`, `RotationProxy`, `TranslationProxy` from `Unity.Transforms` deprecated
* Deprecated `ScriptBehaviourUpdateOrder.CurrentPlayerLoop`; Use `PlayerLoop.GetCurrentPlayerLoop()` instead
* Deprecated `ScriptBehaviourUpdateOrder.SetPlayerLoop`; Use `PlayerLoop.SetPlayerLoop()` instead

### Removed

* Removed expired API `BlobAssetComputationContext.AssociateBlobAssetWithGameObject(Hash128, GameObject)`
* Removed expired API `BlobAssetReference.Release()`
* Removed expired API `BlobAssetStore.UpdateBlobAssetForGameObject<T>(int, NativeArray<Hash128>)`
* Removed expired API `class TerminatesProgramAttribute`
* Removed expired API `EntityManager.LockChunkOrder(ArchetypeChunk)`
* Removed expired API `EntityManager.LockChunkOrder(EntityQuery)`
* Removed expired API `EntityManager.LockChunkOrder(NativeArray<ArchetypeChunk>)`
* Removed expired API `EntityManager.UnlockChunkOrder(ArchetypeChunk)`
* Removed expired API `EntityManager.UnlockChunkOrder(EntityQuery)`
* Removed expired API `GameObjectConversionSettings.BuildSettings`
* Removed expired API `GameObjectConversionSystem.GetBuildSettingsComponent<T>()`
* Removed expired API `GameObjectConversionSystem.TryGetBuildSettingsComponent<T>(out T)`
* Removed expired API `LambdaJobDescriptionConstructionMethods.WithBurst(...)`
* Removed expired API `LambdaJobDescriptionConstructionMethods.WithNativeDisableUnsafePtrRestrictionAttribute(...)`
* Removed expired API `SceneSystem.BuildSettingsGUID`
* Removed expired overload of `BlobBuilder.Allocate<T>(int, ref BlobArray<T>)`
* Removed expired overload of `EntityQuery.CopyFromComponentDataArray<T>(...)`
* Removed expired overload of `EntityQuery.CreateArchetypeChunkArray(...)`
* Removed expired overload of `EntityQuery.ToComponentDataArray<T>(...)`
* Removed expired overload of `EntityQuery.ToEntityArray(...)`

### Fixed

* Fixed the synchronization of transforms for Hybrid Components to handle scale properly.
* Improved JobsDebugger error messages when accessing `ComponentDataFromEntity`, `ArchetypeChunkComponentType`, `ArchetypeChunkComponentTypeDynamic`, `ArchetypeChunkBufferType`, `ArchetypeChunkSharedComponentType`, `ArchetypeChunkEntityType`, and `BufferFromEntity` after a structural change. (requires Unity 2020.1.0b2 or later)
* Fixed scene camera culling masks not being reset in the case of using ConvertToEntity but not any scene conversion
* Fix to IL2CPP compilation errors occuring in IL2CPP builds with Entities.ForEach with nested captures.
* Fixed the entity inspector showing incorrect data for chunk components.
* Fixed entity scene load error caused by type hash mismatch when serializing hybrid components with conditionally compiled fields.
* `LambdaJobTestFixture` and `AutoCreateComponentSystemTests_*` systems are no longer added to the simulation world by default.
* `GameObjectConversionSystem.DependOnAsset` now correctly handles multiple sub-scenes
* Ensure that patched component access methods (`GetComponent/SetComponent/HasComponent`) don't break `Entities.ForEach` when there are a lot of them (due to short branch IL instructions).
* Fixed deactivation of Hybrid Components when the entity was disabled or turned into a prefab.
* Improved performance of singleton access methods (`SetSingleton`/`GetSingleton`).
* Fixed managed components not being serialized during player livelink.
* Fixed `CompanionLink` being incorrectly synced during player livelink.
* Fixed a false-positive in the EntityDiffer when a shared component in a changed chunk has its default value
* Fixed Entities.ForEach lambdas that call static methods as well as component access methods (`GetComponent/SetComponent/HasComponent`).
* Remapping no longer incorrectly visits UnityEngine.Object types (i.e. assets).
* Improved performance for managed object operations (Equality, Cloning and Remapping).

## [0.9.1] - 2020-04-15

### Fixed

* Fixed NullReferenceException issue with Singleton access methods in SystemBase.


## [0.9.0] - 2020-04-09

### Added

* `public void GetCreatedAndDestroyedEntitiesAsync(NativeList<int> state, NativeList<Entity> createdEntities, NativeList<Entity> destroyedEntities)` detects which entities were created and destroyed since the last call to this method.
* Added the ability to reimport a SubScene via an inspector button, which forces reconversion.
* Added `GameObjectConversionSystem.DeclareAssetDependency` which expresses that the conversion result of a GameObject depends on an Asset
* Added `void EntityManager.Instantiate(NativeArray<Entity> srcEntities, NativeArray<Entity> dstEntities)`. It gives explicit control over the set of entities that are instantiated as a set. Entity references on components that are cloned to entities inside the set are remapped to the instantiated entities.
* Added `void EntityManager.CopyEntitiesFrom(EntityManager srcEntityManager, NativeArray<Entity> srcEntities, NativeArray<Entity> outputEntities = default)`. It lets you copy a specific set of entities from one World to another. Entity references on components that are cloned to entities inside the set are remapped to the instantiated entities.
* Added assembly for Mesh Deformation data structures.

### Changed

* Systems are now constructed in two phases. First, ECS creates a new instance of all systems and invokes the constructor. Then, it invokes all `OnCreate` methods. This way, you can now use `World.GetExistingSystem<OtherSystem>()` from inside `OnCreate()`.
* Systems are now destroyed in three phases. First, ECS stops all running systems (i.e. OnStopRunning() is invoked). Then it invokes all `OnDestroy` methods. Finally, ECS destroys all systems. This means you can perform safe and predictable cleanup of systems with cross-references to other systems.
* EntityCommandBuffer Playback now Bursted through function pointers. When there's a mix of unmanaged and managed commands in a single buffer, unmanaged commands will be Bursted. When there are no managed commands, each chain's Playback is fully Bursted.
* `Entities.ForEach` in a `GameObjectConversionSystem` no longer logs a warning if there are multiples of a queried authoring component on a matching GameObject. It now returns the first component instance of the desired type, so conversion systems can optionally call `GetComponents<T>()` in order to handle multiples if desired.
* Declaring a non-Prefab object as a referenced Prefab during conversion now emits a warning
* Improved performance of access to singletons through `SetSingleton` and `GetSingleton` in SystemBase (peformance is also improved through these methods on EntityQuery).
* Updated package `com.unity.properties` to version `1.1.0-preview`.
* Updated package `com.unity.serialization` to version `1.1.0-preview`.
* Updated package `com.unity.platforms` to version `0.2.2-preview.3`.
* Updated package `com.unity.platforms` to version `0.2.2-preview.7`.

### Deprecated

* Deprecated `public T World.CreateSystem<T>(params object[] constructorArguments)`. Please use `World.AddSystem(new MySystem(myParams));` instead.
* Deprecated `LiveLinkBuildImport.GetHash/GetDependencies/GetBundlePath`.

### Removed

* Removed expired API `TypeManager.CreateTypeIndexForComponent<T>()`
* Removed expired API `TypeManager.CreateTypeIndexForSharedComponent<T>()`
* Removed expired API `TypeManager.CreateTypeIndexForBufferElement<T>()`
* Removed expired API `DynamicBuffer.Reserve(int)`
* Removed expired API `World.Active`

### Fixed

* Fix BlobAssetSafetyVerifier to generate a better error message when `readonly` is used with BlobAsset references.
* Fixed incorrect comparison in `EntityChunk.CompareTo()`.
* `SceneManager.IsSceneLoaded` now works for converted entity Scenes and returns whether all sections of an entity Scene have loaded.
* Fixed Exception in conversion code when trying to delete entities that are part of a Prefab.
* Fixed Hybrid Component conversion failing when multiple components were added for the same GameObject.
* Fixed use of component access methods (GetComponent/SetComponent/HasComponent) inside Entities.ForEach with nested captures.
* Fix compilation issue when `ENABLE_SIMPLE_SYSTEM_DEPENDENCIES` is enabled.

### Known Issues

* System groups do not currently apply to systems running as part of `EntitySceneOptimizations`


## [0.8.0] - 2020-03-12

### Added

* Added missing dynamic component version API: `ArchetypeChunk.GetComponentVersion(ArchetypeChunkComponentTypeDynamic)`
* Added missing dynamic component has API: `ArchetypeChunk.Has(ArchetypeChunkComponentTypeDynamic)`
* `EntityArchetype` didn't expose whether it was Prefab or not. Added bool `EntityArchetype.Prefab`. This is needed for meta entity queries, because meta entity queries don't avoid Prefabs.
* Added Build Configurations and Build Pipelines for Linux
* LiveLink now gives an error if a LiveLink player attempts to connect to the wrong Editor, and advises the user on how to correct this.

### Changed
* Renamed `GetComponentVersion()` to `GetChangedVersion()` when referring to version number changes on write access to components.
* Optimized `ArchetypeChunkComponentTypeDynamic` memory layout. 48->40 bytes.
* LiveLink: Editor no longer freezes when sending LiveLink assets to a LiveLinked player.
* LiveLink: No longer includes every Asset from builtin_extra to depend on a single Asset, and sends only what is used. This massively speeds up the first-time LiveLink to a Player.
* Upgraded Burst to fix multiple issues and introduced native debugging feature.

### Fixed

* Fixed LiveLinking with SubScene Sections indices that were not contiguous (0, 1, 2..). Now works with whatever index you use.
* Fixed warning when live converting disabled GameObjects.
* Allow usage of `Entities.WithReadOnly`, `Entities.WithDeallocateOnJobCompletion`, `Entities.WithNativeDisableContainerSafetyRestriction`, and `Entities.WithNativeDisableParallelForRestriction` on types that contain valid NativeContainers.


## [0.7.0] - 2020-03-03

### Added

* Added `HasComponent`/`GetComponent`/`SetComponent` methods that streamline access to components through entities when using the `SystemBase` class.  These methods call through to `EntityManager` methods when in OnUpdate code and codegen access through `ComponentDataFromEntity` when inside of `Entities.ForEach`.
* `SubScene` support for hybrid components, allowing Editor LiveLink (Player LiveLink is not supported yet).
* Added `GameObjectConversionSettings.Systems` to allow users to explicitly specify what systems should be included in the conversion

### Changed

* Fixed an issue where shared component filtering could be broken until the shared component data is manually set/added when using a deserialized world.
* Users can control the update behaviour of a `ComponentSystemGroup` via an update callback.  See the documentation for `ComponentSystemGroup.UpdateCallback`, as well as examples in `FixedRateUtils`.
* `IDisposable` and `ICloneable` are now supported on managed components.
* `World` now exposes a `Flags` field allowing the editor to improve how it filters world to show in various tooling windows.
* `World.Systems` is now a read only collection that does not allocate managed memory while being iterated over.
* Updated package `com.unity.platforms` to version `0.2.1-preview.4`.

### Deprecated

* Property `World.AllWorlds` is now replaced by `World.All` which now returns a read only collection that does not allocate managed memory while being iterated over.

### Removed

* Removed expired API `implicit operator GameObjectConversionSettings(World)`
* Removed expired API `implicit operator GameObjectConversionSettings(Hash128)`
* Removed expired API `implicit operator GameObjectConversionSettings(UnityEditor.GUID)`
* Removed expired API `TimeData.deltaTime`
* Removed expired API `TimeData.time`
* Removed expired API `TimeData.timeSinceLevelLoad`
* Removed expired API `TimeData.captureFramerate`
* Removed expired API `TimeData.fixedTime`
* Removed expired API `TimeData.frameCount`
* Removed expired API `TimeData.timeScale`
* Removed expired API `TimeData.unscaledTime`
* Removed expired API `TimeData.captureDeltaTime`
* Removed expired API `TimeData.fixedUnscaledTime`
* Removed expired API `TimeData.maximumDeltaTime`
* Removed expired API `TimeData.realtimeSinceStartup`
* Removed expired API `TimeData.renderedFrameCount`
* Removed expired API `TimeData.smoothDeltaTime`
* Removed expired API `TimeData.unscaledDeltaTime`
* Removed expired API `TimeData.fixedUnscaledDeltaTime`
* Removed expired API `TimeData.maximumParticleDeltaTime`
* Removed expired API `TimeData.inFixedTimeStep`
* Removed expired API `ComponentSystemBase.OnCreateManager()`
* Removed expired API `ComponentSystemBase.OnDestroyManager()`
* Removed expired API `ConverterVersionAttribute(int)`

### Fixed

* Non-moving children in transform hierarchies no longer trigger transform system updates.
* Fixed a bug where dynamic buffer components would sometimes leak during live link.
* Fixed crash that would occur if only method in a module was generated from a `[GenerateAuthoringComponent]` type.
* `Entities.ForEach` now throws a correct error message when it is used with a delegate stored in a variable, field or returned from a method.
* Fix IL2CPP compilation error with `Entities.ForEach` that uses a tag component and `WithStructuralChanges`.
* `Entities.ForEach` now marshals lambda parameters for DOTS Runtime when the lambda is burst compiled and has collection checks enabled. Previously using `EntityCommandBuffer` or other types with a `DisposeSentinel` field as part of your lambda function (when using DOTS Runtime) may have resulted in memory access violation.
* `.Run()` on `IJobChunk` may have dereferenced null or invalid chunk on filtered queries.
* BlobAssetSafetyVerifier would throw a `ThrowArgumentOutOfRangeException` if a blob asset was using in a struct with a method that yielded (instead of generating a valid error).


### Security

* Throw correct error message if accessing `ToComponentDataArrayAsync` `CopyFromComponentDataArray` or `CopyFromComponentDataArrayAsync` from an unrelated query.



## [0.6.0] - 2020-02-17

### Added

* The `[GenerateAuthoringComponent]` attribute is now allowed on structs implementing `IBufferElementData`. An authoring component is automatically generated to support adding a `DynamicBuffer` of the type implementing `IBufferElementData` to an entity.
* Added new `SystemBase` base class for component systems.  This new way of defining component systems manages dependencies for the user (manual dependency management is still possible by accessing the `SystemBase.Dependency` field directly).
* New `ScheduleParallel` methods in `IJobChunk` and `Entities.ForEach` (in `SystemBase`) to make parallel scheduling of jobs explicit.  `ScheduleSingle` in `IJobChunk` indicates scheduling work to be done in a non-parallel manner.
* New editor workflow to quickly and easily build LiveLink player using the `BuildConfiguration` API.
* Adds Live Link support for `GameObject` scenes.
* The `SceneSystem` API now also loads `GameObject` scenes via `LoadSceneAsync` API.
* Added new build component for LiveLink settings in `Unity.Scenes.Editor` to control how initial scenes are handled (LiveLink all, embed all, embed first).
* Users can now inspect post-procssed IL code inside Unity Editor: `DOTS` -> `DOTS Compiler` -> `Open Inspector`
* `GetAssignableComponentTypes()` can now be called with or without a List&lt;Type&gt; argument to collect the data. When omitted, the list will be allocated, which is the same behavior as before.

### Changed

 * The package `com.unity.build` has been merged into the package `com.unity.platforms`. As such, removed the dependency on `com.unity.build@0.1.0-preview` and replaced it with `com.unity.platforms@0.2.1-preview.1`. Please read the changelog of `com.unity.platforms` for more details.
* Managed components are now stored in a way that will generate less GC allocations when entities change archetype.
* Moved `Unity.Entities.ICustomBootstrap` from Unity.Entities.Hybrid to Unity.Entities.
* `World.Dispose()` now completes all reader/writer jobs on the `World`'s `EntityManager` before releasing any resources, to avoid use-after-free errors.
* Fix `AssemblyResolveException` when loading a project with dependent packages that are using Burst in static initializers or `InitializeOnLoad`.
* `.sceneWithBuildSettings` files that are stored in Assets/SceneDependencyCache are no longer rebuilt constantly. Because they are required for SubScene behaviour to work in the editor, if these are deleted they are recreated by OnValidate of the SubScene in the edited Scene. They should also be recreated on domain reload (restarting unity, entering/exiting playmode, etc).
* `EntityQuery.cs`: Overloads of `CreateArchetypeChunkArray`, `ToComponentDataArray`, `ToEntityArray`, and `CopyFromComponentDataArray` that return a JobHandle (allowing the work to be done asynchronously) have been renamed to add `Async` to the title (i.e. `ToComponentDataArrayAsync`). The old overloads have been deprecated and an API Updater clause has been added.
 * `Entities.WithName` now only accepts names that use letters, digits, and underscores (not starting with a digit, no two consecutive underscores)
 * Updated package `com.unity.properties` to version `0.10.4-preview`.
 * Updated package `com.unity.serialization` to version `0.6.4-preview`.
 * The entity debugger now remembers whether chunk info panel is visible
 * The entity debugger now displays the full name for nested types in the system list
 * The entity debugger now sorts previously used filter components to the top of the filter GUI
 * Bumped burst version to include the new features and fixes including:
 * Fix an issue with function pointers being corrupted after a domain reload that could lead to hard crashes.
 * Fix potential deadlock between Burst and the AssetDatabase if burst is being used when building the database.

### Deprecated

 * Method `GetBuildSettingsComponent` on class `GameObjectConversionSystem` has been renamed to `GetBuildConfigurationComponent`.
 * Method `TryGetBuildSettingsComponent` on class `GameObjectConversionSystem` has been renamed to `TryGetBuildConfigurationComponent`.
 * Member `BuildSettings` on class `GameObjectConversionSettings` has been renamed to `BuildConfiguration`.
 * Member `BuildSettingsGUID` on class `SceneSystem` has been renamed to `BuildConfigurationGUID`.

### Removed

 * Removed expired API `SceneSectionData.SharedComponentCount`
 * Removed expired API `struct SceneData`
 * Removed expired API `SubScene._SceneEntities`
 * Removed expired API `World.Active`

### Fixed

* Ability to open and close SubScenes from the scene hierarchy window (Without having to move cursor to inspector window).
* Ability to create a new empty Sub Scene without first creating a game object.
* Improve performance of SubScene loading and change tracking in the editor.
* Fixed regression where `GetSingleton` would create a new query on every call.
* Fixed SubScenes trying to load an already loaded AssetBundle when loaded multiple times on the same player, but with different Worlds.
* Make it clear that SubScenes in Prefabs are not supported.
* Lambda job codegen tests now fail if the error message does not contain the expected contents.
* Improved performance of setting up the world required for game object conversion
* The `chunkIndex` parameter passed to `IJobChunk.Execute()` now has the correct value.
* Fixed an error which caused entities with `ISystemStateSharedComponentData` components to not be cleaned up correctly.
* Managed components containing `Entity` fields will now correctly serialize.
* Fixed issue where `BlobAssetVerifier` will throw error if it can't resolve a type.
* Exposed the Managed Component extensions for `EntityQuery`.
* `Entities.ForEach` now identifies when `this` of the enclosing system is captured due to calling an extension method on it when compilation fails since the lambda was emitted as a member function
* `Entities.ForEach` now reports when a field of the outer system is captured and used by reference when compilation fails since the lambda was emitted as a member function
* `Entities.ForEach` does not erronously point to calling static functions as the source of the error when compilation fails since the lambda was emitted as a member function
* Debugging inside of `Entities.ForEach` with Visual Studio 2017/2019 (some debugging features will need an upcoming update of the com.unity.ide.visualstudio package).
* `EntityQuery.ToComponentArray<T>` with `T` deriving from `UnityEngine.Component` now correctly collects all data in a chunk
* Fixed an issue with `ComponentSystemBase.GetEntityQuery` and `EntityManager.CreateEntityQuery` calls made with `EntityQueryDesc` not respecting read-only permissions.


## [0.5.1] - 2020-01-28

### Changed

* Constructor-related exceptions thrown during `World.CreateSystem` will now included the inner exception details.
* `DefaultWorldInitialization.GetAllSystems` now returns `IReadOnlyList<Type>` instead of `List<Type>`
* `DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups` now takes `IEnumerable<Type>` instead of `List<Type>`

### Fixed

* Fixed an issue where `BlobAssetReference` types was not guaranteed to be 8-byte aligned on all platforms which could result in failing to read Blob data in components correctly on 32-bit platforms.
* Fixed issue in `MinMaxAABB.Equals()` comparing `Min` to itself rather than `other`.
* `Entities.ForEach` now properly treats `in` parameters of `DynamicBuffer` type as read-only
* Fixed potential crash caused by a leaked job after an exception is thrown during a call to `IJobChunk.Schedule`.
* Fixed regression in `ComponentSystemBase.GetSingleton()` where a new query would be created every timee the function is called.


## [0.5.0] - 2020-01-16

### Added

* Added AndroidHybrid.buildpipeline with RunStepAndroid

### Changed

* `Entities.WithReadOnly`, `Entities.WithNativeDisableParallelForRestriction`, `Entities.WithDeallocateOnJobCompletion`, `Entities.WithNativeDisableSafetyRestriction` and `Entities.WithNativeDisableUnsafePtrRestriction` now check their argument types for the proper attributes (`[NativeContainer]`, `[NativeContainerSupportsDeallocateOnJobCompletion]`) at compile time and throw an error when used on a field of a user defined type.
* Log entries emitted during subscene conversion without a context object are now displayed in the subscene inspector instead of discarded

### Deprecated

* Adding removal dates to the API that have been deprecated but did not have the date set.
* `BlobAssetReference<T>`: `Release()` was deprecated, use `Dispose()` instead.

### Removed

 * Adding removal dates to the API that have been deprecated but did not have the date set.
 * `BlobAssetReference<T>`: `Release()` was deprecated, use `Dispose()` instead.
 * `EntityQuery.cs`: Removed expired API `CalculateLength()`, `SetFilter()` and `SetFilterChanged()`.

### Fixed
* Fixed an issue where trying to perform EntityRemapping on Managed Components could throw if a component field was null.
* `EntityManager.MoveEntitiesFrom` with query was not bumping shared component versions, order versions or dirty versions correctly. Now it does.
* Fixed that adding a Sub Scene component from the Add Components dropdown was not reflected in the Hierarchy.
* Fixed so that Undo/Redo of changes to SceneAsset objectfield in the Sub Scene Inspector is reflected in the Hierarchy.
* Make it clear when Sub Scene duplicates are present: shown in Hierarchy and by showing a warning box in the Inspector.
* Support Undo for 'Create Sub Scene From Selection' context menu item.
* Better file name error handling for the 'New Sub Scene From Selection' context menu item.
* Keep sibling order for new Sub Scene when created using 'New Sub Scene From Selection' (prevents the new Sub Scene from ending as the last sibling).
* Handle if selection contains part of a Prefab instance when creating Sub Scene from Selection.
* Fix dangling loaded Sub Scenes not visualized in the Hierarchy when removing Scene Asset reference in Sub Scene component.
* Fixed an issue with invalid IL generated by `Entities.ForEach` when structs are captured as locals from two different scopes and their fields are accessed.
* Make it clear in the Hierarchy and Sub Scene Inspector that nesting Sub Scenes is not yet supported.
* Fixed an issue with `BinaryWriter` where serializing a `System.String[]` with a single element would throw an exception.
* Fixed an issue with `ComponentSystem.GetEntityQuery` and `JobComponentSystem.GetEntityQuery` which caused improper caching of queries when using "None" or "Any" fields.


## [0.4.0] - 2019-12-16

**This version requires Unity 2019.3.0f1+**

### New Features

* Two new methods added to the public API:
  * `void EntityCommandBuffer.AddComponent<T>(EntityQuery entityQuery)`
  * `void EntityCommandBuffer.RemoveComponent<T>(EntityQuery entityQuery)`
* BlobArray, BlobString & BlobPtr are not allowed to be copied by value since they carry offset pointers that aree relative to the location of the memory. This could easily result in programming mistakes. The compiler now prevents incorrect usage by enforcing any type attributed with [MayOnlyLiveInBlobStorage] to never be copied by value.

### Changes

* Deprecates `TypeManager.CreateTypeIndexForComponent` and it's other component type variants. Types can be dynamically added (in Editor builds) by instead passing the new unregistered types to `TypeManager.AddNewComponentTypes` instead.
* `RequireForUpdate(EntityQuery)` and `RequireSingletonForUpdate` on a system with `[AlwaysUpdate]` will now throw an exception instead of being ignored.
* ChangeVersionUtility.IncrementGlobalSystemVersion & ChangeVersionUtility.InitialGlobalSystemVersion is now internal. They were accidentally public previously.
* Entity inspector now shows entity names and allows to rename the selected entity
* Improved entity debugger UI
* Create WorldRenderBounds for prefabs and disabled entities with renderers during conversion, this make instantiation of those entities significantly faster.
* Reduced stack depth of System.Update / OnUpdate method (So it looks better in debugger)
* Assert when using EntityQuery from another world
* Using an EntityQuery created in one world on another world was resulting in memory corruption. We now detect it in the EntityManager API and throw an argument exception
* Structural changes now go through a bursted codepath and are significantly faster
* DynamicBuffer.Capacity is now settable

### Fixes

* Remove unnecessary & incorrect warning in DeclareReferencedPrefab when the referenced game object is a scene object
* GameObjects with ConvertAndInject won't get detached from a non-converted parent (fixes regression)
* Fixed a crash that could occur when destroying an entity with an empty LinkedEntityGroup.
* Updated performance package dependency to 1.3.2 which fixes an obsoletion warning
* The `EntityCommandBuffer` can be replayed repeatedly.
* Fixed exception in entity binary scene serialization when referencing a null UnityEngine.Object from a shared component
* Moving scripts between assemblies now triggers asset bundle rebuilds where necessary for live link
* Fixed LiveLink on Android


## [0.3.0] - 2019-12-03

### New Features

* ENABLE_SIMPLE_SYSTEM_DEPENDENCIES define can now be used to replace the automatic dependency chaining with a much simplified strategy. With ENABLE_SIMPLE_SYSTEM_DEPENDENCIES it simply chains jobs in the order of the systems against previous jobs. Without ENABLE_SIMPLE_SYSTEM_DEPENDENCIES, dependencies are automatically chained based on read / write access of component data of each system. In cases when there game code is forced to very few cores or there are many systems, this can improve performance since it reduces overhead in calculating optimal dependencies.
* Added `DebuggerTypeProxy` for `MultiListEnumerator<T>` (e.g. this makes the results of `GameObjectConversionSystem.GetEntities` calls readable in the debugger)
* Two new methods added to the public API:
  * EntityManager.CreateEntity(Archetype type, int count, Allocator allocator);
  * EntityManager.Instantiate(Entity entity, int count, Allocator allocator);
  Both methods return a `NativeArray<Entity>`.

### Changes

Removed the following deprecated API as announced in/before `0.1.1-preview`:

* From GameObjectConversionUtility.cs: `ConvertIncrementalInitialize()` and `ConvertScene()`.
* From Translation.cs: `struct Position`.
* From EditorEntityScenes.cs: `WriteEntityScene()`.
* From GameObjectConversionSystem.cs: `AddReferencedPrefab()`, `AddDependency()`, `AddLinkedEntityGroup()`, `DstWorld`.
* From DefaultWorld.cs: `class EndPresentationEntityCommandBufferSystem`.

### Fixes

* ConvertAndInject won't destroy the root GameObject anymore (fixes regression introduced in 0.2.0)
* Fix Android/iOS build when using new build pipeline
  * Provide correct application extension apk, aab or empty for project export when building to Android


## [0.2.0] - 2019-11-22

**This version requires Unity 2019.3 0b11+**

### New Features

* Automatically generate authoring components for IComponentData with IL post-processing. Any component data marked with a GenerateAuthoringComponent attribute will generate the corresponding authoring MonoBehaviour with a Convert method.
* BuildSettings assets are now used to define a single build recipe asset on disk. This gives full control over the build pipeline in a modular way from C# code.
  * BuildSettings let you attach builtin or your own custom IBuildSettingsComponents for full configurability
  * BuildPipelines let you define the exact IBuildStep that should be run and in which order
  * IBuildStep is either builtin or your own custom build step
  * BuildSettings files can be inherited so you can easily make base build settings with most configuration complete and then do minor adjustments per build setting
  * Right now most player configuration is still in the existing PlayerSettings, our plan is to over time expose all Player Settings via BuildSettings as well to ease configuration of complex projects with many build recipes & artifacts
* SubScenes are now automatically converted to entity binary files & cached by the asset pipeline. The entity cache files previously present in the project folder should be removed. Conversion systems can use the ConverterVersion attribute to convert to trigger a reconversion if the conversion system has changed behaviour. The conversion happens asynchronously in another process. Thus on first open the subscenes might not show up immediately.

* Live link builds can be built with the new BuildSettings pipeline.
  Open sub scene
  * Closed Entity scenes are built by the asset pipeline and loaded via livelink on demand
  * Opened Entity scenes are send via live entity patcher with patches on a per component / entity basis based on what has changed
  * Assets referenced by entity scenes are transferred via livelink when saving the asset
  * Scenes loaded as game objects are currently not live linked (This is in progress)
 by assigning the LiveLink build pipeline

* `Entities.ForEach` syntax for supplying jobified code in a `JobComponentSystem`'s `OnUpdate` method directly by using a lambda (instead of supplying an additional `IJobForEach`).

* `EntityQueryMask` has been added, which allows for quick confirmation of if an Entity would be returned by an `EntityQuery` without filters via `EntityQueryMask.Matches(Entity entity)`.  An EntityQueryMask can be obtained by calling `EntityManager.GetEntityQueryMask(EntityQuery query).`
* Unity Entities now supports the _Fast Enter playmode_ which can be enabled in the project settings. It is recommended to be turned on for all dots projects.
* The UnityEngine component `StopConvertToEntity` can be used to interrupt `ConvertToEntity` recursion, and should be preferred over a `ConvertToEntity` set to "convert and inject" for that purpose.
* _EntityDebugger_ now shows IDs in a separate column, so you can still see them when entities have custom names
* Entity references in the Entity Inspector have a "Show" button which will select the referenced Entity in the Debugger.
* An `ArchetypeChunkIterator` can be created by calling `GetArchetypeChunkIterator` on an `EntityQuery`. You may run an `IJobChunk` while bypassing the Jobs API by passing an `ArchetypeChunkIterator` into `IJobChunk.RunWithoutJobs()`.
* The `[AlwaysSynchronizeSystem]` attribute has been added, which can be applied to a `JobComponentSystem` to force it to synchronize on all of its dependencies before every update.
* `BoneIndexOffset` has been added, which allows the Animation system to communicate a bone index offset to the Hybrid Renderer.
* Initial support for using Hybrid Components during conversion, see the HybridComponent sample in the StressTests folder.
* New `GameObjectConversionSystem.ForkSettings()` that provides a very specialized method for creating a fork of the current conversion settings with a different "EntityGuid namespace", which can be used for nested conversions. This is useful for example in net code where multiple root-level variants of the same authoring object need to be created in the destination world.
* `EntityManager` `LockChunkOrder` and `UnlockChunkOrder` are deprecated.
* Entity Scenes can be loaded synchronously (during the next streaming system update) by using `SceneLoadFlags.BlockOnStreamIn` in `SceneSystem.LoadParameters`.
* `EntityCommandBuffer` can now be played back on an `ExclusiveEntityTransaction` as well as an `EntityManager`. This allows ECB playback to   be invoked from a job (though exclusive access to the EntityManager data is still required for the duration of playback).

### Upgrade guide
* If you are using SubScenes you must use the new BuildSettings assets to make a build & run it. SubScenes are not supported from the File -> BuildSettings... & File -> Build and Run workflows.
* Entities requires AssetDatabase V2 for certain new features, we do not provide support for AssetDatabase V1.

### Fixes

* Setting `ComponentSystemGroup.Enabled` to `false` now calls `OnStopRunning()` recursively on the group's member systems, not just on the group itself.
* Updated Properties pacakge to `0.10.3-preview` to fix an exception when showing Physics ComponentData in the inspector as well as fix IL2CPP Ahead of Time linker errors for generic virtual function calls.
* The `LocalToParentSystem` will no longer write to the `LocalToWorld` component of entities that have a component with the `WriteGroup(typeof(LocalToWorld))`.
* Entity Debugger styling work better with Pro theme
* Entity Inspector no longer has runaway indentation
* Fixed issue where `AddSharedComponentData`, `SetSharedComponentData` did not always update `SharedComponentOrderVersion`.
* Fixes serialization issue when reading in managed `IComponentData` containing array types and `UnityEngine.Object` references.
* No exception is thrown when re-adding a tag component with `EntityQuery`.
* `AddComponent<T>(NativeArray<Entity>)` now reliably throws an `ArgumentException` if any of the target entities are invalid.
* Fixed an issue where the Entity Debugger would not repaint in edit mode
* Marking a system as `[UpdateInGroup(typeof(LateSimulationSystemGroup))]` no longer emits a warning about `[DisableAutoCreation]`.
* Fixed rendering of chunk info to be compatible with HDRP
* Fixed issue where `ToComponentDataArray` ignored the filter settings on the `EntityQuery` for managed component types.

### Changes

* Deprecated `DynamicBuffer.Reserve` and made `DynamicBuffer.Capacity` a settable property. `DynamicBuffer.Reserve(10)` should now be `DynamicBuffer.Capacity = 10`.
* Moved `NativeString` code from Unity.Entities to Unity.Collections.
* Updated dependencies for this package.
* Significantly improved `Entity` instantiation performance when running in-Editor.
* Added support for managed `IComponentData` types such as `class MyComponent : IComponentData {}` which allows managed types such as GameObjects or List<>s to be stored in components. Users should use managed components sparingly in production code when possible as these components cannot be used by the Job System or archetype chunk storage and thus will be significantly slower to work with. Refer to the documentation for [component data](xref:components-managed) for more details on managed component use, implications and prevention.
* 'SubSceneStreamingSystem' has been renamed to `SceneSectionStreamingSystem` and is now internal
* Deprecated `_SceneEntities` in `SubScene.cs`. Please use `SceneSystem.LoadAsync` / `Unload` with the respective SceneGUID instead.
* Updated `com.unity.serialization` to `0.6.3-preview`.
* The deprecated `GetComponentGroup()` APIs are now `protected` and can only be called from inside a System like their `GetEntityQuery()` successors.
* All GameObjects with a ConvertToEntity set to "Convert and Destroy" will all be processed within the same conversion pass, this allows cross-referencing.
* Duplicate component adds are always ignored
* When adding component to single entity via EntityQuery, entity is moved to matching chunk instead of chunk achetype changing.
* "Used by Systems" list skips queries with filters
* Managed `IComponentData` no longer require all fields to be non-null after default construction.
* `ISharedComponentData` is serialized inline with entity and managed `IComponentData`. If a shared component references a `UnityEngine.Object` type, that type is serialized separately in an "objrefs" resource asset.
* `EntityManager` calls `EntityComponentStore` via burst delegates for `Add`/`Remove` components.
* `EntityComponentStore` cannot throw exceptions (since called as burst delegate from main thread.)
* `bool ICustomBootstrap.Initialize(string defaultWorldName)` has changed API with no deprecated fallback. It now simply gives you a chance to completely replace the default world initialization by returning true.
* `ICustomBootstrap` & `DefaultWorldInitialization` is now composable like this:
```
class MyCustomBootStrap : ICustomBootstrap
{
    public bool Initialize(string defaultWorldName)
    {
        Debug.Log("Executing bootstrap");
        var world = new World("Custom world");
        World.DefaultGameObjectInjectionWorld = world;
        var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);

        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
        ScriptBehaviourUpdateOrder.UpdatePlayerLoop(world);
        return true;
    }
}
```
* `ICustomBootstrap` can now be inherited and only the most deepest subclass bootstrap will be executed.
* `DefaultWorldInitialization.GetAllSystems` is not affected by bootstrap, it simply returns a list of systems based on the present dlls & attributes.
* `Time` is now available per-World, and is a property in a `ComponentSystem`.  It is updated from the `UnityEngine.Time` during the `InitializationSystemGroup` of each world.  If you need access to time in a sytem that runs in the `InitializationSystemGroup`, make sure you schedule your system after `UpdateWorldTimeSystem`.  `Time` is also a limited `TimeData` struct; if you need access to any of the extended fields available in `UnityEngine.Time`, access `UnityEngine.Time` explicitly`
* Systems are no longer removed from a `ComponentSystemGroup` if they throw an exception from their `OnUpdate`. This behavior was more confusing than helpful.
* Managed IComponentData no longer require implementing the `IEquatable<>` interface and overriding `GetHashCode()`. If either function is provided it will be preferred, otherwise the component will be inspected generically for equality.
* `EntityGuid` is now constructed from an originating ID, a namespace ID, and a serial, which can be safely extracted from their packed form using new getters. Use `a` and `b` fields when wanting to treat this as an opaque struct (the packing may change again in the future, as there are still unused bits remaining). The a/b constructor has been removed, to avoid any ambiguity.
* Updated `com.unity.platforms` to `0.1.6-preview`.
* The default Api Compatibility Level should now be `.NET Standard 2.0` and a warning is generated when the project uses `.NET 4.x`.
* Added `[UnityEngine.ExecuteAlways]` to `LateSimulationSystemGroup`, so its systems run in Edit Mode.


## [0.1.1] - 2019-08-06

### New Features
* EntityManager.SetSharedComponentData(EntityQuery query, T componentData) has been added which lets you efficiently swap a shared component data for a whole query. (Without moving any component data)

### Upgrade guide

* The deprecated `OnCreateManager` and `OnDestroyManager` are now compilation errors in the `NET_DOTS` profile as overrides can not be detected reliably (without reflection).
To avoid the confusion of "why is that not being called", especially when there is no warning issued, this will now be a compilation error. Use `OnCreate` and `OnDestroy` instead.

### Changes

* Updated default version of burst to `1.1.2`

### Fixes

* Fixed potential memory corruption when calling RemoveComponent on a batch of entities that didn't have the component.
* Fixed an issue where an assert about chunk layout compatibility could be triggered when adding a shared component via EntityManager.AddSharedComponentData<T>(EntityQuery entityQuery, T componentData).
* Fixed an issue where Entities without any Components would cause UI errors in the Chunk Info view
* Fixed EntityManager.AddComponent(NativeArray<Entity> entities, ComponentType componentType) so that it handles duplicate entities in the input NativeArray. Duplicate entities are discarded and the component is added only once. Prior to this fix, an assert would be triggered when checking for chunk layout compatibility.
* Fixed invalid update path for `ComponentType.Create`. Auto-update is available in Unity `2019.3` and was removed for previous versions where it would fail (the fallback implementation will work as before).


## [0.1.0] - 2019-07-30

### New Features

* Added the `#UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP_RUNTIME_WORLD` and `#UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP_EDITOR_WORLD` defines which respectively can be used to disable runtime and editor default world generation.  Defining `#UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP` will still disable all default world generation.
* Allow structural changes to entities (add/remove components, add/destroy entities, etc.) while inside of `ForEach` lambda functions.  This negates the need for using `PostUpdateCommands` inside of ForEach.
* `EntityCommandBuffer` has some additional methods for adding components based on `ComponentType`, or for adding empty components of a certain type (`<T>`)
* EntityManagerDiffer & EntityManagerPatcher provides highly optimized diffing & patching functionality. It is used in the editor for providing scene conversion live link.
* Added support for `EntityManager.MoveEntitiesFrom` with managed arrays (Object Components).
* EntityManager.SetArchetype lets you change an entity to a specific archetype. Removing & adding the necessary components with default values. Cleanup components are not allowed to be removed with this method, it throws an exception to avoid accidental system state removal. (Used in incremental live link conversion it made conversion from 100ms -> 40ms for 1000 changed game objects)
* Entity Debugger's system list now has a string filter field. This makes it easier to find a system by name when you have a lot of systems.
* Added IComponentData type `Asset` that will be used by Tiny to convert Editor assets to runtime assets
* Filled in some `<T>` holes in the overloads we provide in `EntityManager`
* New `Entities.WithIncludeAll()` that will include in matching all components that are normally ignored by default (currently `Prefab` and `Disabled`)
* EntityManager.CopyAndReplaceEntitiesFrom has been added it can be used to store & restore a backup of the world for the purposes of general purpose simulation rollback.

### Upgrade guide

* WorldDiff has been removed. It has been replaced by EntityManagerDiff & EntityManagerPatch.
* Renamed `EntityGroupManager` to `EntityQueryManager`.

### Changes

* EntityArchetype.GetComponentTypes no longer includes Entity in the list of components (it is implied). Behaviour now matches the EntityMangager.GetComponentTypes method. This matches the behavior of the corresponding `EntityManager` function.
* `EntityCommandBuffer.AddComponent(Entity, ComponentType)` no longer fails if the target entity already has the specified component.
*  DestroyEntity(EntityQuery entityQuery) now uses burst internally.

### Fixes

* Entity Inspector now shows DynamicBuffer elements in pages of five at a time
* Resources folder renamed to Styles so as not to add editor assets to built player
* `EntityQueryBuilder.ShallowEquals` (used from `Entities.ForEach`) no longer boxes and allocs GC
* Improved error message for unnecessary/invalid `UpdateBefore` and `UpdateAfter`
* Fixed leak in BlobBuilder.CreateBlobAssetReference
* ComponentSystems are now properly preserved when running the UnityLinker. Note this requires 19.3a10 to work correctly. If your project is not yet using 19.3 you can workaround the issue using the link.xml file. https://docs.unity3d.com/Manual//IL2CPP-BytecodeStripping.html
* Types that trigger an exception in the TypeManager won't prevent other types from initializing properly.

## [0.0.12-preview.33] - 2019-05-24

### New Features

* `[DisableAutoCreation]` can now apply to entire assemblies, which will cause all systems contained within to be excluded from automatic system creation. Useful for test assemblies.
* Added `ComponentSystemGroup.RemoveSystemFromUpdateList()`
* `EntityCommandBuffer` has commands for adding/removing components, deleting entities and adding shared components based on an EntityQuery and its filter. Not available in the `Concurrent` version

### Changes

* Generic component data types must now be registered in advance. Use [RegisterGenericComponentType] attribute to register each concrete use. e.g. `[assembly: RegisterGenericComponentType(typeof(TypeManagerTests.GenericComponent<int>))]`
* Attempting to call `Playback()` more than once on the same EntityCommandBuffer will now throw an error.
* Improved error checking for `[UpdateInGroup]`, `[UpdateBefore]`, and `[UpdateAfter]` attributes
* TypeManager no longer imposes alignment requirements on components containing pointers. Instead, it now throws an exception if you try to serialize a blittable component containing an unmanaged pointer, which suggests different alternatives.

### Fixes

* Fixed regression where accessing and destroying a blob asset in a burst job caused an exception
* Fixed bug where entities with manually specified `CompositeScale` were not updated by `TRSLocalToWorldSystem`.
* Error message when passing in invalid parameters to CreateSystem() is improved.
* Fixed bug where an exception due to aggressive pointer restrictions could leave the `TypeManager` in an invalid state
* SceneBoundingVolume is now generated seperately for each subsection
* SceneBoundingVolume no longer throws exceptions in conversion flow
* Fixed regression where calling AddComponent(NativeArray<Entity> entities, ComponentType componentType) could cause a crash.
* Fixed bug causing error message to appear in Inspector header when `ConvertToEntity` component was added to a disabled GameObject.

## [0.0.12-preview.32] - 2019-05-16

### New Features

* Added BlobBuilder which is a new API to build Blob Assets that does not require preallocating one contiguous block of memory. The BlobAllocator is now marked obsolete.
* Added versions of `IJobForEach` that support `DynamicBuffer`s
  * Due to C# language constraints, these overloads needed different names. The format for these overloads follows the following structure:
    * All job names begin with either `IJobForEach` or `IJobForEachEntity`
    * All jobs names are then followed by an underscore `_` and a combination of letter corresponding to the parameter types of the job
      * `B` - `IBufferElementData`
      * `C` - `IComponentData`
      * `E` - `Entity` (`IJobForEachWithEntity` only)
    * All suffixes for `WithEntity` jobs begin with `E`
    * All data types in a suffix are in alphabetical order
  * Here is the complete list of overloads:
    * `IJobForEach_C`, `IJobForEach_CC`, `IJobForEach_CCC`, `IJobForEach_CCCC`, `IJobForEach_CCCCC`, `IJobForEach_CCCCCC`
    * `IJobForEach_B`, `IJobForEach_BB`, `IJobForEach_BBB`, `IJobForEach_BBBB`, `IJobForEach_BBBBB`, `IJobForEach_BBBBBB`
    * `IJobForEach_BC`, `IJobForEach_BCC`, `IJobForEach_BCCC`, `IJobForEach_BCCCC`, `IJobForEach_BCCCCC`, `IJobForEach_BBC`, `IJobForEach_BBCC`, `IJobForEach_BBCCC`, `IJobForEach_BBCCCC`, `IJobForEach_BBBC`, `IJobForEach_BBBCC`, `IJobForEach_BBBCCC`, `IJobForEach_BBBCCC`, `IJobForEach_BBBBC`, `IJobForEach_BBBBCC`, `IJobForEach_BBBBBC`
    * `IJobForEachWithEntity_EB`, `IJobForEachWithEntity_EBB`, `IJobForEachWithEntity_EBBB`, `IJobForEachWithEntity_EBBBB`, `IJobForEachWithEntity_EBBBBB`, `IJobForEachWithEntity_EBBBBBB`
    * `IJobForEachWithEntity_EC`, `IJobForEachWithEntity_ECC`, `IJobForEachWithEntity_ECCC`, `IJobForEachWithEntity_ECCCC`, `IJobForEachWithEntity_ECCCCC`, `IJobForEachWithEntity_ECCCCCC`
    * `IJobForEachWithEntity_BC`, `IJobForEachWithEntity_BCC`, `IJobForEachWithEntity_BCCC`, `IJobForEachWithEntity_BCCCC`, `IJobForEachWithEntity_BCCCCC`, `IJobForEachWithEntity_BBC`, `IJobForEachWithEntity_BBCC`, `IJobForEachWithEntity_BBCCC`, `IJobForEachWithEntity_BBCCCC`, `IJobForEachWithEntity_BBBC`, `IJobForEachWithEntity_BBBCC`, `IJobForEachWithEntity_BBBCCC`, `IJobForEachWithEntity_BBBCCC`, `IJobForEachWithEntity_BBBBC`, `IJobForEachWithEntity_BBBBCC`, `IJobForEachWithEntity_BBBBBC`
    * Note that you can still use `IJobForEach` and `IJobForEachWithEntity` as before if you're using only `IComponentData`.
* EntityManager.SetEnabled API automatically enables & disables an entity or set of entities. If LinkedEntityGroup is present the whole group is enabled / disabled. Inactive game objects automatically get a LinkedEntityGroup added so that EntityManager.SetEnabled works as expected out of the box.
* Add `WithAnyReadOnly` and `WithAllReadyOnly` methods to EntityQueryBuilder to specify queries that filter on components with access type ReadOnly.
* No longer throw when the same type is in a WithAll and ForEach delegate param for ForEach queries.
* `DynamicBuffer` CopyFrom method now supports another DynamicBuffer as a parameter.
* Fixed cases that would not be handled correctly by the api updater.

### Upgrade guide

* Usages of BlobAllocator will need to be changed to use BlobBuilder instead. The API is similar but Allocate now returns the data that can be populated:

  ```csharp
  ref var root = ref builder.ConstructRoot<MyData>();
  var floatArray = builder.Allocate(3, ref root.floatArray);
  floatArray[0] = 0; // root.floatArray[0] can not be used and will throw on access
  ```

* ISharedComponentData with managed fields must implement IEquatable and GetHashCode
* IComponentData and ISharedComponentData implementing IEquatable must also override GetHashCode

### Fixes

* Comparisons of managed objects (e.g. in shared components) now work as expected
* Prefabs referencing other prefabs are now supported in game object entity conversion process
* Fixed a regression where ComponentDataProxy was not working correctly on Prefabs due to a ordering issue.
* Exposed GameObjectConversionDeclarePrefabsGroup for declaring prefab references. (Must happen before any conversion systems run)
* Inactive game objects are automatically converted to be Disabled entities
* Disabled components are ignored during conversion process. Behaviour.Enabled has no direct mapping in ECS. It is recommended to Disable whole entities instead
* Warnings are now issues when asking for a GetPrimaryEntity that is not a game object that is part of the converted group. HasPrimaryEntity can be used to check if the game object is part of the converted group in case that is necessary.
* Fixed a race condition in `EntityCommandBuffer.AddBuffer()` and `EntityCommandBuffer.SetBuffer()`

## [0.0.12-preview.31] - 2019-05-01

### New Features

### Upgrade guide

* Serialized entities file format version has changed, Sub Scenes entity caches will require rebuilding.

### Changes

* Adding components to entities that already have them is now properly ignored in the cases where no data would be overwritten. That means the inspectable state does not change and thus determinism can still be guaranteed.
* Restored backwards compatibility for `ForEach` API directly on `ComponentSystem` to ease people upgrading to the latest Unity.Entities package on top of Megacity.
* Rebuilding the entity cache files for sub scenes will now properly request checkout from source control if required.

### Fixes

* `IJobForEach` will only create new entity queries when scheduled, and won't rely on injection anymore. This avoids the creation of useless queries when explicit ones are used to schedule those jobs. Those useless queries could cause systems to keep updating even though the actual queries were empty.
* APIs changed in the previous version now have better obsolete stubs and upgrade paths.  All obsolete APIs requiring manual code changes will now soft warn and continue to work, instead of erroring at compile time.  These respective APIs will be removed in a future release after that date.
* LODGroup conversion now handles renderers being present in a LOD Group in multipe LOD levels correctly
* Fixed potential memory leak when disposing an EntityCommandBuffer after certain types of playback errors
* Fixed an issue where chunk utilization histograms weren't properly clipped in EntityDebugger
* Fixed an issue where tag components were incorrectly shown as subtractive in EntityDebugger
* ComponentSystem.ShouldRunSystem() exception message now more accurately reports the most likely reason for the error when the system does not exist.

### Known Issues

* It might happen that shared component data with managed references is not compared for equality correctly with certain profiles.


## [0.0.12-preview.30] - 2019-04-05

### New Features
Script templates have been added to help you create new component types and systems, similar to Unity's built-in template for new MonoBehaviours. Use them via the Assets/Create/ECS menu.

### Upgrade guide

Some APIs have been deprecated in this release:

[API Deprecation FAQ](https://forum.unity.com/threads/api-deprecation-faq-0-0-23.636994/)

** Removed obsolete ComponentSystem.ForEach
** Removed obsolete [Inject]
** Removed obsolete ComponentDataArray
** Removed obsolete SharedComponentDataArray
** Removed obsolete BufferArray
** Removed obsolete EntityArray
** Removed obsolete ComponentGroupArray

####ScriptBehaviourManager removal
* The ScriptBehaviourManager class has been removed.
* ComponentSystem and JobComponentSystem remain as system base classes (with a common ComponentSystemBase class)
  * ComponentSystems have overridable methods OnCreateManager and OnDestroyManager. These have been renamed to OnCreate and OnDestroy.
    * This is NOT handled by the obsolete API updater and will need to be done manually.
    * The old OnCreateManager/OnDestroyManager will continue to work temporarily, but will print a warning if a system contains them.
* World APIs have been updated as follows:
  * CreateManager, GetOrCreateManager, GetExistingManager, DestroyManager, BehaviourManagers have been renamed to CreateSystem, GetOrCreateSystem, GetExistingSystem, DestroySystem, Systems.
    * These should be handled by the obsolete API updater.
  * EntityManager is no longer accessed via GetExistingManager. There is now a property directly on World: World.EntityManager.
    * This is NOT handled by the obsolete API updater and will need to be done manually.
    * Searching and replacing Manager<EntityManager> should locate the right spots. For example, world.GetExistingManager<EntityManager>() should become just world.EntityManager.

#### IJobProcessComponentData renamed to IJobForeach
This rename unfortunately cannot be handled by the obsolete API updater.
A global search and replace of IJobProcessComponentData to IJobForEach should be sufficient.

#### ComponentGroup renamed to EntityQuery
ComponentGroup has been renamed to EntityQuery to better represent what it does.
All APIs that refer to ComponentGroup have been changed to refer to EntityQuery in their name, e.g. CreateEntityQuery, GetEntityQuery, etc.

#### EntityArchetypeQuery renamed to EntityQueryDesc
EntityArchetypeQuery has been renamed to EntityQueryDesc

### Changes
* Minimum required Unity version is now 2019.1.0b9
* Adding components to entities that already have them is now properly ignored in the cases where no data would be overwritten.
* UNITY_CSHARP_TINY is now NET_DOTS to match our other NET_* defines

### Fixes
* Fixed exception in inspector when Script is missing
* The presence of chunk components could lead to corruption of the entity remapping during deserialization of SubScene sections.
* Fix for an issue causing filtering with IJobForEachWithEntity to try to access entities outside of the range of the group it was scheduled with.
