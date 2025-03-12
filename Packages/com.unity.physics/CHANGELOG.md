---
uid: unity-physics-changelog
---

# Changelog

## [1.2.4] - 2024-08-14

### Changed

* Updated entities packages dependencies


## [1.2.3] - 2024-05-30

### Changed

* `IntegrityChecks` is now under `ProjectSettings` > `Physics` > `Unity Physics` > `Enable Integrity Checks`.
* Updated entities packages dependencies


## [1.2.1] - 2024-04-26

### Changed

* Updated Burst dependency to version 1.8.13
* Updated entities packages dependencies


## [1.2.0] - 2024-03-22

### Fixed

* Fix a number of memory leaks in the package and its test code.
* Make sure that the `ColliderBlobCleanupSystem` does not dispose the same collider blob multiple times in Netcode environments, preventing a crash.


## [1.2.0-pre.12] - 2024-02-13

### Added

* ScheduleUpdateBroadphase and UpdateBroadphaseImmediate to update the Broadphase instead of doing a full rebuild.
* ScheduleUpdateMotionData and UpdateMotionDataImmediate to update the pre-existing MotionData without recreating them.
* BuildPhysicsWorldData.CompleteInputDependency method to complete the InputDependency if necessary.
* Simulaton.ResetSimulationContext to make it possible reset the current simulation context.

### Changed

* Updated Burst dependency to version 1.8.12

### Fixed

* Fixed errors caused by memory corruption when selecting mesh-based custom Physics Shape Authoring components in the editor.

### Updated

* Upgraded Test Framework version to 1.4.3

## [1.2.0-pre.6] - 2023-12-13

### Changed

* Promotion preparation


## [1.2.0-pre.4] - 2023-11-28

### Changed

* Updated Burst dependency to 1.8.10


## [1.2.0-exp.3] - 2023-11-09

### Added

* Added extension functions `PhysicsCollider.ToMesh()` and `Collider.ToMesh()` for the creation of `UnityEngine.Mesh` objects from colliders.
* use of `PhysicsMaterial` instead of `PhysicMaterial` and `PhysicMaterialCombine` to `PhysicsMaterialCombine` API when the editor is newer than `2023.3`.
* The `Layer Overrides` properties specified in `Collider` and `Rigidbody` authoring components are now baked into the `CollisionFilter` of the resultant Unity Physics colliders. For each individual `Collider` authoring component, the layer overrides on its `Rigidbody` and the collider itself are combined and together form the `CollidesWith` mask in the `CollisionFilter` of the baked collider. The collider collides with layers which are included, and does not collide with layers which are excluded. Furthermore, exclusions have precedence over inclusions.
* `MassProperties.Scale` function allows uniformly scaling mass properties in a physically correct manner, assuming unit mass.

### Changed

* Update package `com.unity.mathematics` from `1.2.6` to `1.3.1` version.
* Analytics API update to `SceneSimulationAnalytics.cs` file.
* collider files renamed to `BoxCollider.cs`, `CapsuleCollider.cs`, `Collider.cs`, `MeshCollider.cs`, `SphereCollider.cs` and `TerrainCollider.cs`.
* The `EnsureUniqueColliderSystem` now runs first in the `BeforePhysicsSystemGroup` instead of after the `AfterPhysicsSystemGroup`. A system that instantiates prefabs using unique colliders during runtime should run in the `BeforePhysicsSystemGroup` to avoid a bug where colliders would not be unique during prefab instantiation.
* The minimum supported editor version is now 2022.3.11f1

### Removed

* `RayCastNode` and `ColliderCastNode`

### Fixed

* Prevent crash in debug display when exiting editor application.


## [1.1.0-pre.3] - 2023-10-17

### Added

* The `Physics Debug Display` component can now display colliders of type `TerrainCollider`.
* The `Layer Overrides` properties specified in `Collider` and `Rigidbody` authoring components are now baked into the `CollisionFilter` of the resultant Unity Physics colliders. For each individual `Collider` authoring component, the layer overrides on its `Rigidbody` and the collider itself are combined and together form the `CollidesWith` mask in the `CollisionFilter` of the baked collider. The collider collides with layers which are included, and does not collide with layers which are excluded. Furthermore, exclusions have precedence over inclusions.
* `MassProperties.Scale` function allows uniformly scaling mass properties in a physically correct manner, assuming unit mass.
* `MassProperties.CreateSphere` function for creation of the mass properties of a sphere with the provided radius, assuming unit mass.

### Changed

* Significantly improved performance of `Physics Debug Display` through a reduced need for thread synchronization via batching of debug display data.
* The `Physics Debug Display` now automatically resizes its debug draw data buffers dynamically to ensure all entities are drawn.
* Game objects with built-in or custom collider authoring components that have a purely uniform scale at edit-time, will now have the scale carried over into their `LocalTransform` component's `Scale` property during entity baking. Thus far, any scale, including a purely uniform scale, was baked into the Unity Physics collider geometry instead and the corresponding entity's `LocalTransform.Scale` property was set to 1 rather than to the desired uniform scale value. This is no longer the case, and users can now expect to find the uniform edit-time scale they assign to their game objects also in the resultant, baked entities during run-time, making run-time modifications of already uniformly scaled objects much more intuitive and less cumbersome.
* Rigid bodies baked from game objects which have any world-space scale or shear at edit-time can now be scaled at runtime using their `LocalTransform` component's `Scale` property. Previously, this was not possible. Runtime scaling using the `LocalTransform.Scale` property was only possible when the edit-time scale of the baked game object was identity, and no shear was present.
* `Entity` references in `CompoundCollider` children are no longer automatically set during baking since these references are not guaranteed to be valid in the `World` after baking. Only those entity references that appear in components and buffers, such as the `PhysicsColliderKeyEntityPair` buffer, are always guaranteed to be valid. Note that the `PhysicsColliderKeyEntityPair` buffer is still present on entities which contain a baked compound collider. Via collider keys, this buffer provides a mapping between the child colliders and the original entities that they were in at bake time.

### Fixed

* Prevent race condition between the systems that produce the debug draw data and the display system that renders the data. This allows the debug data to be fully produced before the display system attempts to render it.
* Fix draw of collider entities without `LocalToWorld` component when selecting `PostIntegration` in the Physics Debug Display.
* Avoid leftover debug draw when switching scenes and new scene has no `Physics Debug Display`.
* Mass properties debug display now correctly considers the rigid body scale, and correctly handles cases with unphysical inertia tensors.
* Custom mass properties specified using the `Override Default Mass Distribution` option in the custom `Physics Body Authoring` component now work correctly even if no collider is present.
* A rigid body's uniform scale value (`LocalTransform.Scale`) is now always considered correctly in the simulation. Previously, when the rigid body entity also contained a `PostTransformMatrix` component, its uniform scale was not applied to its collider and mass properties, leading to erroneous mass properties and missed collisions (if uniform scale > 1) or ghost collisions (if uniform scale < 1).
* Collider debug display now correctly displays colliders with uniform scale other than 1 in accordance with their `LocalTransform` component's `Scale` value.
* Collider debug display now correctly displays rigid body entities with `Parent` component.


## [1.1.0-exp.1] - 2023-09-18

### Added

* Tests for ensuring proper joint anchor and mass property baking
* new demo scene (5m. Collider Modifications) demonstrating how to create colliders during runtime
* Utility functions were added for the creation of MeshCollider blob assets from UnityEngine.Mesh, UnityEngine.MeshData and UnityEngine.MeshDataArray. These functions are located in the `MeshCollider` class as `MeshCollider.Create` variants with different function signatures.
* Users can now verify if a collider blob is unique, and make it unique easily if required. The newly introduced `PhysicsCollider.IsUnique` property lets users check if a `PhysicsCollider` is unique and turn it into a unique collider if desired via the function `PhysicsCollider.MakeUnique()`. Making a collider unique with this function also takes care of the collider blob lifetime management and will automatically dispose it if it is no longer needed.
* Added a custom entity inspector for the collider blob asset stored in the `PhysicsCollider` component. This inspector allows for two-way interaction with the collider. The displayed values update in accordance with the collider's latest runtime state, and the UI can be used in order to interact with the collider manually when it is unique (see `PhysicsCollider.IsUnique`). Among others, this lets users try out different material properties at runtime, such as friction and restitution, or modify the collider's size, local position or orientation.

### Changed

* Changed the `bool` flags in the `Physics Debug Display` authoring component for drawing colliders, collider edges and axis-aligned bounding boxes of colliders to an enum called `DisplayMode`. With the display mode you can now choose whether to draw these elements at the beginning of the simulation step or at the end of the simulation step after the rigid bodies have been integrated, meaning, they have been moved forward in time.
* Convert SystemBase to ISystems.
* Joint baking for built-in 3D physics joints has been improved, leading to the expected simulation results. Now, when the `Spring` and `Damping` properties in Configurable and Character Joints are both set to 0 for limits, a hard limit is modeled. This is equivalent to the behavior in built-in 3D physics. Also, the `Damping` parameter is now correctly converted from damping coefficient to the Unity Physics damping ratio for joints, yielding the correct damping force. Furthermore, joint baking now considers the scale of game objects. Anchor points are now affected by the scale accordingly.
* The formula which converts the user-specified joint relaxation parameters (spring frequency and damping ratio) to the internal constraint regularization parameters (tau and damping) was rewritten as an optimized closed-form expression with constant time complexity. The regularization parameters can now be efficiently computed regardless of the chosen solver iteration count.
* `PhysicsGraphicalSmoothing` has been added to `.WithAll<>()` from `.WithAllRW<>()` in the `SmoothedDynamicBodiesQuery` variable within `SmoothRigidBodiesGraphicalMotion.cs` system file.
* Updating APIs to `GetScriptingDefineSymbols()` and `SetScriptingDefineSymbols()`.
* Included ragdoll authoring in documentation
* Prefab instances will now contain unique colliders if the "force unique" collider authoring option is used. This allows collider runtime modifications without manual collider blob cloning now also on prefab instances. Note that prefab instances that contain "force unique" colliders will be made unique only after the next physics system group update following the prefab instantiation. Until then, the `PhysicsCollider.IsUnique` property will be false. If users require a unique collider immediately after prefab instantiation for runtime collider modifications, they can safely use the new `PhysicsCollider.MakeUnique()` function immediately after instantiation.
* The internal component `DrawComponent`, required by the `Physics Debug Display`, is now hidden in the hierarchy.

### Deprecated

* The `Constraint.DefaultSpringDamping` variable was deprecated. Use `Constraint.DefaultDampingRatio` instead. The same applies to the `Constraint.SpringDamping` property which was deprecated. Instead the new `Constraint.DampingRatio` property should be used.

### Removed

* Remove unused internal debug draw functionalities which were causing slowdowns during world initialization

### Fixed

* BuildCompoundCollidersBakingSystem no longer leaks memory when the world is disposed.
* Convert SystemBase to ISystems
* When using the built-in `Rigidbody` and `Collider` authoring components, the inertia tensor of the resultant rigid body in Unity Physics is now set correctly in all situations. Previously, in certain cases, the default inertia tensor was used.
* A problem which prevented the solver to respect the user-specified joint spring frequency and damping ratios in certain cases has been fixed, enabling physically-plausible modeling of joints under all operating conditions.
* Link to changelog in documentation now fixed
* Physics Shape components with type Mesh now correctly only use the Custom Mesh for MeshCollider creation if specified rather than also incorrectly including the game object's render mesh and any render mesh found in the game object's children. The previous erroneous behavior could lead to significant performance problems in the narrow phase (contact creation) of the physics simulation group for affected meshes.
* Updated documentation to reflect that the Built-In TerrainCollider is not yet supported by Unity Physics


## [1.0.16] - 2023-09-11

### Changed

* Updated Burst dependency to version 1.8.8

### Fixed

* Bugfix: The `AngularDamping` component of `RigidbodyAspect` is now writing to the correct value instead of to `LinearDamping`


## [1.0.14] - 2023-07-27

### Added

* Added support for overriding mass properties when baking `Rigidbody` authoring components. Now, when setting the `RigidBody.automaticCenterOfMass` or `RigidBody.automaticInertiaTensor` properties to false, the corresponding mass property data values are correctly baked into the Unity Physics rigid body entity and appear as expected in the entity's `PhysicsMass` component.

### Changed

* Updated Burst dependency to version 1.8.7


## [1.0.11] - 2023-06-19

### Changed

* Prevent spawning `ParallelSolverJobs` unnecessarily ahead of time, which was leading to a potentially high overhead in time consumption. Instead, schedule the right number of jobs for the dispatch pair phases created by the scheduler to prevent scheduling and processing overheads. This leads to speed-ups in the time consumed by jobs in the `SolveAndIntegrateSystem` specifically in cases with low to medium joint and contact counts.
* The `PhysicsColliderKeyEntityPair` buffer is now added only when needed and its internal capacity of the buffer is set to zero, ensuring its content always lives outside of the chunk. This way, we don't unnecessarily increase the rigid body sizes in chunks, allowing for a larger number of rigid bodies in a single chunk, which improves performance.

### Fixed

* Fixed regression in accessibility of `PhysicsShapeAuthoring` API. The functions `GetCapsuleProperties()` and `SetCapsule()` were made internal by accident during the move of the custom physics authoring components from the package API to a package sample and are now public again.


## [1.0.10] - 2023-05-23

### Added

* Added missing API documentation and tooltips.
* preprocessors against performance tests package

### Changed

* Changed visibility of `BaseJointBaker` class. It is now internal.
* Changed visibility of `ColliderGeometry` struct. It is now internal.
* Changed visibility of `PrimitiveColliderGeometries` struct. It is now internal.

### Fixed

* The relative velocity in the angular velocity motor is now calculated correctly and the relative orientation between the two connected rigid bodies is correctly taken into account. This makes the motor work properly in all configurations.
* Prevent issues with update order for `ModifyJointLimitsSystem` in netcode multiplayer use case in which the system could not be placed after the `PhysicsSystemGroup` since both were no longer in the same group.


## [1.0.8] - 2023-04-17

### Added

* Add `PhysicsWorldIndexAuthoring` component which allows specifying non-default world indices for bodies which are modeled using a `Rigidbody` component.

### Changed

* With the removal of the custom Unity Physics authoring experience, a behavior change has been introduced when mixing built-in physics authoring components with custom physics authoring components. It is now no longer supported to add built-in colliders, such as the Box Collider, to a rigid body created using the `PhysicsBodyAuthoring` component. The inverse however, adding `PhysicsShapeAuthoring` components to a rigid body created using the built-in `Rigidbody` component, is still supported.
* Updated Burst version to 1.8.4

### Removed

* UpgradePhysicsData window has been removed.
* The custom Unity Physics authoring experience, built around the `PhysicsBodyAuthoring` and `PhysicsShapeAuthoring` components, has been removed from the package and turned into a package sample. It is recommended to use the built-in physics authoring components instead, e.g., the `Rigidbody` and collider components. To continue using the custom authoring experience in your projects, simply import the _Custom Physics Authoring_ sample from the Unity Physics package into your project via the Package Manager window.
* Depedendency on com.unity.test-framework

### Fixed

* Colliders created from PhysicsShapeAuthoring components with the "Force Unique" flag set to true now are ensured to have unique collider blobs that are not shared across rigid bodies when they have identical properties, thus enabling runtime modification of individual colliders.

## [1.0.0-pre.65] - 2023-03-21

### Changed

* Updated Burst version in use to 1.8.3
* Debug display systems now only update when the PhysicsDebugDisplayData component is present (e.g., through the PhysicsDebugDisplayAuthoring game object component) and are only created within the editor.

### Fixed

* Physics Debug Display for enabled Collider Edges now draws correctly if the collider scale is modified during runtime
* Debug display systems no longer stall and instead execute their jobs asynchronously
* Debug draw of collider faces and AABBs now account for uniform scaling of the rigid body
* Rigidbody components that move in PlayMode will now correctly snap back to their original position when exiting PlayMode while the containing sub scene is open for editing. As part of the fix, the classic PhysX-based physics simulation is now temporarily and globally disabled while in PlayMode with an open sub scene that contains classic Rigidbody authoring components. The Unity Physics-based physics simulation is unaffected during that time.


## [1.0.0-pre.44] - 2023-02-13

### Added

* legacy icons to the physics authoring components `PhysicsShape` and `PhysicsBody`.
* Help icon now always points to latest version of documentation for physics authoring components
* Unit tests for Motors
* Unit tests for some Jacobian methods
* Internal API to help with connecting bodyB to bodyA for joints/motor configuration

### Changed

* Added functions that allow you to set impulse event threshold on all constraint joints, or only one of them.
* Replaced PhysicsTransformAspect with TransformAspect
* Increased testing of position motor in Position Motor demo scene, and re-enabled Position Motor package test.
* Cleanup and simplification of position motor joint code
* Use of `TransformAspect.WorldPosition`, `TransformAspect.WorldRotation`, `TransformAspect.WorldScale` when using Transform_V2 instead of `TransformAspect.Position`, `TransformAspect.Rotation`, `TransformAspect.Scale`.
* `BaseShapeBakingSystem` and `BuildCompoundCollidersBakingSystem` have been modified to use `IJobEntity` instead of `Entities.ForEach()`.

### Removed

* `Attributes.cs` script has been removed since the `com.unity.properties` package is part of the editor as a module.
* the gap left due to the old references being removed on the .asmdef files.

### Fixed

* Fixed bug when an extra ConfigurableJoint constraint was created when baking a motored Configurable Joint
* Fixed bug where target wasn't calculated correctly when baking a motored Configurable Joint
* Duplicate component error when switching Smoothing type to anything but None in Physics Body
* Immediately reset component in PhysicsShapeAuthoring's Reset() function to avoid sequential coupling issues
* During conversion from Game Object physics joints to Unity Physics joints the joint's spring coefficient is correctly considered.


## [1.0.0-pre.15] - 2022-11-16

### Added

* For all 4 motor types, added a new field for the maximum impulse that can be exerted by the motor constraint
* GameObjects that use axis-aligned motors are now converted during baking
* Support for validation of position motors in Simulation Validation System
* `UnityPhysicsStep` component does not show Havok physics engine in the dropdown field when `com.havok.physics` package is not installed.
* Functions that allow you to set an impulse event threshold on all constraint joints, or only one of them.


### Changed

* The UI for each motor type in the Physics Samples has been simplified to allow for more intuitive configuration
* When creating the constraints for motors, the ordering has been changed so that the motor is always last.
* The physics baking systems obey the GameObject static flag, in addition to the StaticOptimizeEntity component.
* Adding '.AsArray()' method to do explicit cast for the test scenes.
* API for motor creation have an additional argument for the max impulse of a motor. If this argument is not specified, a default value of infinity will be used.
* Naming path for Unity Physics Authoring components and material components.

### Removed

* Dependancy on `com.unity.jobs` package.
* Dependency on `com.unity.test-framework.performance` package.

### Fixed

* For a linear velocity motor using Unity.Physics, fixed a bug where the target wasn't accurate if body was rotated
* Linear Velocity Motor target calculation fixed for when bodyA is rotated
* Fixed bug in Position Motor target error calculation for when bodyA is rotated
* Fixed bug in Linear Velocity Motor regarding the timestep
* Fixed bug in Position Motor regarding the timestep
* Motion should be not exported for physics entities that have the Simulate component disabled.
* Fixed a bug in which scale value was read from LocalTransform array even if the array had zero size.
* It is now possible to enable impulse events feature using Constraint creation methods.
* When baking a configurable joint into a motor, the Break Force and Break Torque now update the Max Impulse for breakable events
* Duplicate component error when switching Smoothing type to anything but None in Physics Body
* Immediately reset component in PhysicsShapeAuthoring's Reset() function to avoid sequential coupling issues
* During conversion from Game Object physics joints to Unity Physics joints the joint's spring coefficient is correctly considered.
* Physics debug display now shows colliders again in edit mode and not only in play mode


## [1.0.0-exp.12] - 2022-10-19

### Added

* `[CreateBefore(typeof(BroadphaseSystem))]`  for `PhysicsSimulationPickerSystem ` within `UnityPhysicsSimulationSystems.cs` script file.


## [1.0.0-exp.8] - 2022-09-21

### Upgrade guide

* The physics pipeline has been reworked. 	- `PhysicsSystemGroup` is introduced. It is a `ComponentSystemGroup` that covers all physics jobs. It consists of `PhysicsInitializeGroup`, `PhysicsSimulationGroup`, and `ExportPhysicsWorld`. `PhysicsSimulationGroup` further consists of `PhysicsCreateBodyPairsGroup`, `PhysicsCreateContactsGroup`, `PhysicsCreateJacobiansGroup`, `PhysicsSolveAndIntegrateGroup` which run in that order. See [documentation](xref:interacting-with-physics) for details. 	- `StepPhysicsWorld` and `EndFramePhysicsSystem` systems have been removed, `BuildPhysicsWorld` has been moved to `PhysicsInitializeGroup`: 	    - If you had `Update(Before|After)StepPhysicsWorld`, replace it with: `[UpdateInGroup(typeof(PhysicsSystemGroup))][Update(After|Before)(typeof(PhysicsSimulationGroup))]`. 	    - If you had `Update(Before|After)BuildPhysicsWorld`, replace it with: `[UpdateBefore(typeof(PhysicsSystemGroup))]` or `[UpdateInGroup(typeof(PhysicsSystemGroup))][UpdateAfter(typeof(PhysicsInitializeGroup))]` 	    - If you had `Update(Before|After)ExportPhysicsWorld` replace it with: `[UpdateInGroup(typeof(PhysicsSystemGroup))][UpdateBefore(typeof(ExportPhysicsWorld))]` or `[UpdateAfter(typeof(PhysicsSystemGroup))]` 	    - If you had `[Update(Before|After)EndFramePhysicsSystem]` replace it with: `[UpdateAfter(typeof(PhysicsSystemGroup))]` 	    - If you had combination of those (e.g. `[UpdateAfter(typeof(BuildPhysicsWorld))][UpdateBefore(typeof(StepPhysicsWorld))`) take a look at the diagram in [documentation](xref:interacting-with-physics). 	- All new systems are unmanaged, which means that they are more efficient, and their `OnUpdate()` is Burst friendly. You shouldn't call `World.GetOrCreateSystem<AnyPhysicsSystem>()` as of this release and should be using singletons (see below).
* Retrieval of `PhysicsWorld` is achieved differently. Previously, it was necessary to get it directly from `BuildPhysicsWorld` system. Now, `PhysicsWorld` is retrieved by calling (`SystemAPI|SystemBase|EntityQuery`).GetSingleton<PhysicsWorldSingleton>().PhysicsWorld in case read-only access is required, and by calling (`SystemAPI|SystemBase|EntityQuery`).GetSingletonRW<PhysicsWorldSingleton>().PhysicsWorld in case of a read-write access. It is still possible to get the world from `BuildPhysicsWorld`, but is not recommended, as it can cause race conditions. This is only affecting the `PhysicsWorld` managed by the engine. Users still can create and manage their own `PhysicsWorld`. Check out [documentation](xref:interacting-with-physics) for more information.
* Retrieval of `Simulation` is achieved differently. Previously, it was neccessary to get it directly from `StepPhysicsWorld` system. Now, `Simulation` is retrieved by calling (`SystemAPI|SystemBase|EntityQuery`).GetSingleton<SimulationSingleton>().AsSimulation() in case read-only access is required, and by calling (`SystemAPI|SystemBase|EntityQuery`).GetSingletonRW<SimulationSingleton>().AsSimulation() in case of read-write access. Check out [documentation](xref:interacting-with-physics) for more information.
* The dependencies between physics systems now get sorted automatically as long as `GetSingleton<>()` approach is used for retrieving `PhysicsWorld` and `Simulation`. There is no need to call `RegisterPhysicsSystems(ReadOnly|ReadWrite)`, `AddInputDependency()` or `AddInputDependencyToComplete()` and these functions were removed.
* `ITriggerEventsJob`, `ICollisionEventsJob`, `IBodyPairsJob`, `IContactsJob` and `IJacobiansJob` no longer take `ISimulation` as an argument for `Schedule()` method, but instead take `SimulationSingleton`. Use `GetSingleton<SimulationSingleton>()` for `ITriggerEventsJob` and `ICollisionEventsJob`, `GetSingletonRW<SimulationSingleton>()` for `IBodyPairsJob`, `IContactsJob` and `IJacobiansJob`. All of these jobs can be now scheduled in Burst friendly way.
* Callbacks between simulation stages have been removed. To get the same functionality, you now need to:
    - Create a system
    - Make it `[UpdateInGroup(typeof(PhysicsSimulationGroup))]` and make it `[UpdateBefore]` and `[UpdateAfter]` one of 4 `PhysicsSimulationGroup` subgroups.
    - In `OnUpdate()` of the system, recreate the functionality of a callback by scheduling one of the specialised jobs: `IBodyPairsJob`, `IContactsJob`, `IJacobiansJob`.
    - See [documentation](xref:simulation-modification) for details and examples.
* Uniform scale is now supported.     - `Scale` component is now taken into account when creating physics bodies. The component doesn't get created by `Baking` (previously known as `Conversion`) in the Editor. Scale set in Editor gets baked into the collider geometry. If you want to dynamically scale bodies, add this component to physics body entities.     - You might get problems if you were creating `RigidBody` struct instances directly, since the scale will be initialized to zero. Set it to `1.0f` to return to previous behaviour.     - `ColliderCast` and `ColliderDistance` queries now support uniform scale for colliders that you are querying with. `ColliderDistanceInput` and `ColliderCastInput` therefore have a new field that enables you to set it. Same as `RigidBody`, you might get problems since the scale will be initialized to zero. Set it to `1.0f` to return to previous behaviour.     - Positive and negative values of scale are supported.
* Multiple worlds support has been reworked. To support this use case previously, it was necessary to create a physics pipeline on your own, by using helpers such as `PhysicsWorldData`, `PhysicsWorldStepper` and `PhysicsWorldExporter`. Now it is possible to instantiate a `CustomPhysicsSystemGroup` with a proper world index, which will run the physics simulation on non-default world index bodies. Check out the [documentation](xref:interacting-with-bodies) for more information.

### Added

* Reference to com.unity.render-pipelines.universal version 10.7
* new shaders in the sammpler that are SRP batcher and universal render pipeline compliant compliant
* New struct - `Unity.Physics.Math.ScaledMTransform`: Provides the same utility as `Unity.Physics.Math.MTransform` but supports uniform scale.
* Operator which converts a `float4` into a `Unity.Physics.Plane`.
* `PhysicsComponentExtensions.ApplyScale(in this PhysicsMass pm, in Scale scale)` - an extension method which scales up the `PhysicsMass` component.
* The following extension methods have recieved a version which takes a `Scale` argument. The old versions are not deprecated, and they assume identity scale.     - `PhysicsComponentExtensions.GetEffectiveMass(in this PhysicsMass bodyMass, in Translation bodyPosition, in Rotation bodyOrientation, in Scale bodyScale, float3 impulse, float3 point)`     - `PhysicsComponentExtensions.GetCenterOfMassWorldSpace(in this PhysicsMass bodyMass, in Scale bodyScale, in Translation bodyPosition, in Rotation bodyOrientation)`     - `PhysicsComponentExtensions.GetImpulseFromForce(in this PhysicsMass bodyMass, in Scale bodyScale, in float3 force, in ForceMode mode, in float timestep, out float3 impulse, out PhysicsMass impulseMass)`     - `PhysicsComponentExtensions.ApplyExplosionForce(ref this PhysicsVelocity bodyVelocity, in PhysicsMass bodyMass, in PhysicsCollider bodyCollider, in Translation bodyPosition, in Rotation bodyOrientation, in Scale bodyScale,`         `float explosionForce, in float3 explosionPosition, in float explosionRadius, in float timestep, in float3 up, in CollisionFilter explosionFilter, in float upwardsModifier = 0, ForceMode mode = ForceMode.Force)`     - `PhysicsComponentExtensions.ApplyImpulse(ref this PhysicsVelocity pv, in PhysicsMass pm, in Translation t, in Rotation r, in Scale bodyScale, in float3 impulse, in float3 point)`     - `PhysicsComponentExtensions.ApplyLinearImpulse(ref this PhysicsVelocity velocityData, in PhysicsMass massData, in Scale bodyScale, in float3 impulse)`     - `PhysicsComponentExtensions.ApplyAngularImpulse(ref this PhysicsVelocity velocityData, in PhysicsMass massData, in Scale bodyScale, in float3 impulse)`
* `bool OverlapAabb(OverlapAabbInput input, ref NativeList<int> allHits)` has been added to `PhysicsWorld`.
* `SimulationSingleton` IComponentData is added:     - Use `AsSimulation()` to get the simulation that is stored in it.     - Use `InitializeFromSimulation(ref Simulation)` if you need to create it.      >Note - Physics engine internally manages one `SimulationSingleton`, so be careful if using `SetSingleton<>()` with the newly created `SimulationSingleton`, as it can override the one stored by the engine. You should be using this method if you are managing a local simulation and need a singleton to use events and simulation modification API.
* `PhysicsWorldSingleton` IComponentData is added. It implements `ICollidable` and has access to the stored `PhysicsWorld` and it's utility methods.
* `NativeReference<int> HaveStaticBodiesChanged` get property is added to `BuildPhysicsWorld`.
* The following system groups are introduced:     - `PhysicsSystemGroup` - covers all physics systems     - `PhysicsInitializeGroup`, `PhysicsSimulationGroup` - subgroups of `PhysicsSystemGroup`     - `PhysicsCreateBodyPairsGroup`, `PhysicsCreateContactsGroup`, `PhysicsCreateJacobiansGroup` and `PhysicsSolveAndIntegrateGroup` - subgroups of `PhysicsSimulationGroup`
* Impulse events to allow users to break joints
* Supports the following types of motors: rotational, linear velocity, rotational, angular velocity
* `CustomPhysicsSystemGroup` and `CustomPhysicsSystemGroupBase` for providing mulitple worlds support.
* `CustomPhysicsProxyDriver` IComponentData, with it's authoring (`CustomPhysicsProxyAuthoring`) and a system (`SyncCustomPhysicsProxySystem`), which enable you to drive an entity from one world by an entity from another, using kinematic velocities.

### Changed

* All materials in the samples to be universal render pipeline compliant
* Restored many Gizmo/Mesh methods from DisplayCollidersSystem.cs, but placed in a Utility file instead
* Changed allocator label to `Allocator.Temp` internally when building `CollisionWorld` from `CollisionWorldProxy`.
* Physics Debug Display: Performance improvements when drawing colliders (faces, edges, AABBs), broadphase, mass properties and contacts
* Physics Debug Display: Drawing collider faces for Mesh and Convex Hull types use different rendering method
* Physics Debug Display: The original Collider Edge drawing code that uses Gizmos has been moved to class 'DisplayGizmoColliderEdges' and to class 'AppendMeshColliders'
* Using built-in resources for the reference mesh used by cube and icosahedron
* Resources/ (used by Debug Draw) has been renamed DebugDisplayResources/ and now loads assets differently
* Removed use of the obsolete AlwaysUpdateSystem attribute. The new RequireMatchingQueriesForUpdate attribute has been added where appropriate.
* `ColliderCastInput` now has a property `QueryColliderScale`. It defaults to `1.0f`, and represents the scale of the passed in input collider.
* `ColliderCastInput` constructor has changed to take in uniform scale of the query collider as the last parameter. It defaults to `1.0f`.
* The following methods have a uniform scale argument added as the last argument (defaults to `1.0f`), and their arguments are reordered. The old versions are deprecated:     -  `AppendMeshColliders.GetMeshes.AppendSphere(SphereCollider* sphere, RigidTransform worldFromCollider, ref List results) has been deprecated.` `Use AppendSphere(ref List results, SphereCollider* sphere, RigidTransform worldFromCollider, float uniformScale = 1)`.     - `AppendMeshColliders.GetMeshes.AppendCapsule(CapsuleCollider* capsule, RigidTransform worldFromCollider, ref List results) has been deprecated. Use AppendCapsule(ref List results, CapsuleCollider* capsule, RigidTransform worldFromCollider, float uniformScale = 1).`     - `AppendMeshColliders.GetMeshes.AppendMesh(MeshCollider* meshCollider, RigidTransform worldFromCollider, ref List results) has been deprecated. Use AppendMesh(ref List results, MeshCollider* meshCollider, RigidTransform worldFromCollider, float uniformScale = 1).`     - `AppendMeshColliders.GetMeshes.AppendCompound(CompoundCollider* compoundCollider, RigidTransform worldFromCollider, ref List results) has been deprecated. Use AppendCompound(ref List results, CompoundCollider* compoundCollider, RigidTransform worldFromCollider, float uniformScale = 1).`     - `AppendMeshColliders.GetMeshes.AppendTerrain(TerrainCollider* terrainCollider, RigidTransform worldFromCollider, ref List results) has been deprecated. Use AppendTerrain(ref List results, TerrainCollider* terrainCollider, RigidTransform worldFromCollider, float uniformScale = 1).`     - `AppendMeshColliders.GetMeshes.AppendCollider(Collider* collider, RigidTransform worldFromCollider, ref List results) has been deprecated. Use AppendCollider(ref List results, Collider* collider, RigidTransform worldFromCollider, float uniformScale = 1).`     - `ColliderDistanceInput.ColliderDistanceInput(BlobAssetReference collider, RigidTransform transform, float maxDistance) has been deprecated. Use ColliderDistanceInput(BlobAssetReference collider, float maxDistance, RigidTransform transform, float uniformScale = 1).`     - `Collider.GetLeafCollider(Collider* root, RigidTransform rootTransform, ColliderKey key, out ChildCollider leaf) has been deprecated. Use Use GetLeafCollider(out ChildCollider leaf, Collider* root, ColliderKey key, RigidTransform rootTransform, float rootUniformScale = 1) instead.`     - `Math.TransformAabb(RigidTransform transform, Aabb aabb) hase been deprecated. Use Math.TransformAabb(Aabb aabb, RigidTransform transform, float uniformScale = 1) instead.`     - `Math.TransformAabb(MTransform transform, Aabb aabb) hase been deprecated. Use Math.TransformAabb(Aabb aabb, MTransform transform, float uniformScale = 1) instead.` - `Schedule()` signatures that accept `ISimulation` have been removed from `IBodyPairsJob`, `IContactsJob`, `IJacobiansJob`, `ICollisionEventsJob` and `ITriggerEventsJob`. New signatures accept `SimulationSingleton` instead. - `ColliderCastNode.KernelDefs.CollisionWorld` has been removed and replaced with `ColliderCastNode.KernelDefs.PhysicsWorld`. - `RaycastNode.KernelDefs.CollisionWorld` has been removed and replaced with `RaycastNode.KernelDefs.PhysicsWorld`. - `DispatchPairSequencer` is now a `struct` instead of a `class`. Use `DispatchPairSequencer.Create()` to create an instance of this struct, as empty constructor calls will not properly initialize it. - `Simulation` is now a `struct` instead of a `class`. Use `Simulation.Create()` to create an instance of this struct, as empty constructor calls will not properly initialize it. -  The following methods and interfaces have `SimulationCallbacks` argument removed.     - `ISimulation.ScheduleStepJobs(SimulationStepInput input, SimulationCallbacks callbacksIn, JobHandle inputDeps, bool multiThreaded = true)`     - `Simulation.ScheduleStepJobs(SimulationStepInput input, SimulationCallbacks callbacksIn, JobHandle inputDeps, bool multiThreaded = true)` - `Simulation` has new methods that enable stepping the simulation on a granularity of individual simulation phases:     - `Simulation.ScheduleBroadphaseJobs(SimulationStepInput input, JobHandle inputDeps, bool multiThreaded = true)`     - `Simulation.ScheduleNarrowphaseJobs(SimulationStepInput input, JobHandle inputDeps, bool multiThreaded = true)`     - `Simulation.ScheduleCreateJacobiansJobs(SimulationStepInput input, JobHandle inputDeps, bool multiThreaded = true)`     - `Simulation.ScheduleSolveAndIntegrateJobs(SimulationStepInput input, JobHandle inputDeps, bool multiThreaded = true)`
* `BuildPhysicsWorld` is now a `struct` instead of a `class`, and implements `ISystem` instead of `SystemBase`.
* `ExportPhysicsWorld` is now a `struct` instead of a `class`, and implements `ISystem` instead of `SystemBase`.
* `PhysicsWorldBuilder.SchedulePhysicsWorldBuild(SystemBase system, ref PhysicsWorldData physicsData, in JobHandle inputDep, float timeStep, bool isBroadphaseBuildMultiThreaded, float3 gravity, uint lastSystemVersion )` signature has changed to `PhysicsWorldBuilder.SchedulePhysicsWorldBuild(ref SystemState systemState, ref PhysicsWorldData physicsData, in JobHandle inputDep, float timeStep, bool isBroadphaseBuildMultiThreaded, float3 gravity, uint lastSystemVersion )`.
* `PhysicsWorldBuilder.SchedulePhysicsWorldBuild(SystemBase system, ref PhysicsWorld world, ref NativeArray<int> haveStaticBodiesChanged, ref PhysicsWorld world, ref NativeReference<int> haveStaticBodiesChanged, in PhysicsWorldData.PhysicsWorldComponentHandles componentHandles, in JobHandle inputDep, float timeStep, bool isBroadphaseBuildMultiThreaded, float3 gravity, uint lastSystemVersion, EntityQuery dynamicEntityGroup, EntityQuery staticEntityGroup, EntityQuery jointEntityGroup)` signature has changed to `PhysicsWorldBuilder.SchedulePhysicsWorldBuild(ref PhysicsWorld world, ref NativeReference<int> haveStaticBodiesChanged, in PhysicsWorldData.PhysicsWorldComponentHandles componentHandles, in JobHandle inputDep, float timeStep, bool isBroadphaseBuild,MultiThreadedfloat3 gravity, uint lastSystemVersion, EntityQuery dynamicEntityGroup, EntityQuery staticEntityGroup, EntityQuery jointEntityGroup)`.
* `PhysicsWorldBuilder.ScheduleBroadphaseBVHBuild(ref PhysicsWorld world, ref NativeArray<int> haveStaticBodiesChanged, in JobHandle inputDep, float timeStep, bool isBroadphaseBuildMultiThreaded, float3 gravity)` signature has changed to `PhysicsWorldBuilder.ScheduleBroadphaseBVHBuild(ref PhysicsWorld world, NativeReference<int> haveStaticBodiesChanged, in JobHandle inputDep, float timeStep, bool isBroadphaseBuildMultiThreaded, float3 gravity)`.
* `PhysicsWorldBuilder.BuildPhysicsWorldImmediate(SystemBase system, ref PhysicsWorldData data, float timeStep, float3 gravity, uint lastSystemVersion)`signature has changed to `PhysicsWorldBuilder.BuildPhysicsWorldImmediate(ref SystemState systemState, ref PhysicsWorldData data, float timeStep, float3 gravity, uint lastSystemVersion)`.
* `PhysicsWorldBuilder.BuildPhysicsWorldImmediate(SystemBase system, ref PhysicsWorld world, ref NativeArray<int> haveStaticBodiesChanged, float timeStep, float3 gravity, uint lastSystemVersion, EntityQuery dynamicEntityGroup, EntityQuery staticEntityGroup, EntityQuery jointEntityGroup)` signature has changed to `PhysicsWorldBuilder.BuildPhysicsWorldImmediate(ref PhysicsWorld world, NativeReference<int> haveStaticBodiesChanged, in PhysicsWorldData.PhysicsWorldComponentHandles, float timeStep, float3 gravity, uint lastSystemVersion, EntityQuery dynamicEntityGroup, EntityQuery staticEntityGroup, EntityQuery jointEntityGroup)`.
* `PhysicsWorldData.HaveStaticBodiesChanged` is now a `NativeReference<int>` instead of `NativeArray<int>`.
* `PhysicsWorldData.PhysicsWorldComponentHandles` struct is added. It contains component handles to data types needed to create a PhysicsWorld.     - Added `PhysicsWorldComponentHandles(ref SystemState systemState)` constructor. Call it from the system in `OnCreate()` where you plan to use `PhysicsWorldData` in `OnUpdate()`, and not from some other system (can cause race conditions).     - Added `Update(ref SystemState)` method. Call it from a system in `OnUpdate()`. `PhysicsWorldBuilder` methods already update component handles, and there is no need to call this method prior to calling `PhysicsWorldBuilder.ScheduleBulilPhysicsWorld()/BuildPhysicsWorldImmediate())`
* `PhysicsWorldData` also has `Update(ref SystemState)` method which just calls `Update(ref SystemState)` on`PhysicsWorldComponentHandles`.
* `PhysicsWorldExporter.SchedulePhysicsWorldExport(SystemBase system, in PhysicsWorld world, in JobHandle inputDep, EntityQuery dynamicEntities)` signature has changed to `PhysicsWorldExporter.SchedulePhysicsWorldExport(ref SystemState systemState, ref ExportPhysicsWorldTypeHandles componentTypeHandles, in PhysicsWorld world, in JobHandle inputDep, EntityQuery dynamicEntities)`.
* `PhysicsWorldExporter.ExportPhysicsWorldImmediate(SystemBase system, in PhysicsWorld world, EntityQuery dynamicEntities)` signature has changed to `PhysicsWorldExporter.ExportPhysicsWorldImmediate(ref SystemState systemState, ref ExportPhysicsWorldTypeHandles componentTypeHandles, in PhysicsWorld world, EntityQuery dynamicEntities)`.
* `PhysicsWorldExporter.ExportPhysicsWorldTypeHandles` struct is added. It contains component handles to data types needed to export a PhysicsWorld to ECS data.     - Added `ExportPhysicsWorldTypeHandles(ref SystemState systemState)` constructor. Call it from the system in `OnCreate()` where you plan to export `PhysicsWorld` in `OnUpdate()`, and not from some other system (can cause race conditions).     - Added `Update(ref SystemState systemState) ` method. Call it from a system in `OnUpdate()`. `PhysicsWorldExporter` methods already update component handles, and there is no need to call this method prior to calling `PhysicsWorldExporter.ScheduleExportPhysicsWorld()/ExportPhysicsWorldImmediate())`.
* Joint Constraints container changed from FixedList128 to a custom internal container. Users will get a FixedList512 returned when retrieving constraints
* Replaced obsolete EntityQueryBuilder APIs with current ones.
* ISystem implementations with public data converted to using components for data access

### Deprecated

* `CollisionWorld.CalculateAabb(RigidTransform transform) has been deprecated. Use CollisionWorld.CalculateAabb() without a parameter.`
* `RigidBody.CalculateAabb(RigidTransform transform) has been deprecated. Use RigidBody.CalculateAabb() without a parameter.`
* `PhysicsWorld.CalculateAabb(RigidTransform transform) has been deprecated. Use PhysicsWorld.CalculateAabb() without a parameter.`

### Removed

* Removed `ICollider.CalculateAabb(RigidTransform transform)`. All `ICollider` implementations will still be able to call `CalculateAabb(RigidTransform transform, float uniformScale = 1)`, except `RigidBody`, `PhysicsWorld` and `CollisionWorld`, where these methods are deprecated.
* Removed `CollisionWorldProxy`. Use `PhysicsWorldSingleton` to achieve the same functionality.
* Simulation callback mechanism has been removed. As a consequence, the following APIs are removed as well:     - class `SimulationCallbacks` is removed.     - enum `SimulationCallbacks.Phase` is removed.     - callback delegate : `public delegate JobHandle Callback(ref ISimulation simulation, ref PhysicsWorld world, JobHandle inputDeps)` has been removed.
* Removed `PhysicsWorld` getter from `BuildPhysicsWorld`. It is still possible to get a `PhysicsWorld` reference through `BuildPhysicsWorld.PhysicsData.PhysicsWorld` but it isn't recommended since it can cause race conditions.
* Removed `StepPhysicsWorld` system.
* Removed `EndFramePhysicsSystem` system.
* Removed `BuildPhysicsWorld.AddInputDependencyToComplete()` from public API.
* Removed `BuildPhysicsWorld.AddInputDependency()` method.
* Removed `BuildPhysicsWorld.GetOutputDependency()` method.
* Removed `ExportPhysicsWorld.AddInputDependency()` method.
* Removed `ExportPhysicsWorld.GetOutputDependency()` method.
* Removed `static class` `PhysicsRuntimeExtenstions`, as a consequence, the following extension methods are removed as well:     - `public static void RegisterPhysicsRuntimeSystemReadOnly(this SystemBase system)`     - `public static void RegisterPhysicsRuntimeSystemReadWrite(this SystemBase system)`     - `public static void RegisterPhysicsRuntimeSystemReadOnly<T>(this SystemBase system) where T : unmanaged, IComponentData`     - `public static void RegisterPhysicsRuntimeSystemReadWrite<T>(this SystemBase system) where T : unmanaged, IComponentData`
* Removed `PhysicsWorldExporter.SharedData` struct.
* Removed `PhysicsWorldExporter.ScheduleCollisionWorldProxy()` method.
* Removed `PhysicsWorldExporter.ScheduleCollisionWorldCopy()` method.
* Removed `PhysicsWorldExporter.CopyCollisionWorldImmediate()` method.
* Removed `PhysicsWorldStepper` class.

### Fixed

* SingleThreadedRagdoll test was broken
* Physics Debug Display: improved Sphere and Capsule Collider Edges drawing
* Physics Debug Display: corrected z-ordering when drawing collider faces
* Simplified math to improve performance in `CalculateTwistAngle()`
* Fixed a bug in `ConvexHullBuilder.Compact()` where triangle indices in links were not properly updated after remapping.

### Changed
* Package Dependencies
    * `com.unity.entities` to version `0.51.1`
    * `com.unity.physics` to version `0.51.1`
    * `com.unity.collections` to version `1.4.0`

## [0.51.0] - 2022-05-04

### Changed
* Package Dependencies
    * `com.unity.burst` to version `1.6.6`
    * `com.unity.entities` to version `0.51.0`
    * `com.unity.mathematics` to version `1.2.6`
    * `com.unity.physics` to version `0.51.0`
    * `com.unity.collections` to version `1.3.1`

## [0.50.0] - 2021-09-17

### Changed

* Upgraded com.unity.burst to 1.5.5
* Adjusted code to remove obsolete APIs across all jobs inheriting IJobEntityBatch
* Resources/ (used by Debug Draw) has been renamed DebugDisplayResources/ and now loads assets differently

### Removed

* All usages of PhysicsExclude from Demo and Runtime code.

### Fixed

* An issue with the rendering pipeline used for the package samples, which caused none of the samples to render post conversion
* An issue with the materials present in the samples as their colors were no longer correct


## [0.10.0] - 2021-09-17

### Changed

* Upgraded com.unity.burst to 1.5.5
* Adjusted code to remove obsolete APIs across all jobs inheriting IJobEntityBatch

### Removed

* All usages of PhysicsExclude from Demo and Runtime code.


## [0.10.0-preview.1] - 2021-06-25
### Upgrade guide
* Added `PhysicsWorldIndex` shared component, which is required on every Entity that should be involved in physics simulation (body or joint). Its `Value` denotes the index of physics world that the Entity belongs to (0 for default `PhysicsWorld` processed by `BuildPhysicsWorld`, `StepPhysicsWorld` and `ExportPhysicsWorld` systems). Note that Entities for different physics worlds will be stored in separate chunks, due to different values of shared component.
* `PhysicsExclude` component is obsolete, but will still work at least until 2021-10-01. Instead of adding `PhysicsExclude` when you want to exclude an Entity from physics simulation, you can achieve the same thing by removing the required `PhysicsWorldIndex` shared component.
* `HaveStaticBodiesChanged` was added to `SimulationStepInput`. It's a NativeArray of size 1, used for optimization of static body synchronization.
### Changes
* Dependencies
* Run-Time API
    * Added `BlobAssetReferenceColliderExtension` functions for ease of use and to help avoid unsafe code
        * Added reinterpret_cast-like logic via `BlobAssetReference<Collider>.As<To>` where `To` is the destination collider struct type. The extension will return a reference to the desired type.
        * Added reinterpret_cast-like logic via `BlobAssetReference<Collider>.AsPtr<To>` where `To` is the destination collider struct type. The extension will return a pointer to the desired type.
        * Added an easy conversion helper to `PhysicsCollider` via `BlobAssetReference<Collider>.AsComponent()`
        * Added `ColliderCastInput` and `ColliderDistanceInput` constructors that do not require unsafe code, along with `SetCollider` function to change a collider after creation of the input struct.
    * `PhysicsWorldData` is a new structure encapsulating `PhysicsWorld` and other data and queries that are necessary for simulating a physics world.
    * `PhysicsWorldBuilder` and `PhysicsWorldExporter` are new utility classes providing methods for building a `PhysicsWorld` and exporting its data to ECS components, with options to tweak queries that fetch Entities for the physics world.
    * `PhysicsWorldStepper` is a new helper class for scheduling physics simulation jobs. Its `SimulationCreator` delegate and methods that need to instantiate an `ISimulation` require physics world index to be passed in.
    * `BuildPhysicsWorld` was refactored to keep the data in `PhysicsWorldData` and use `PhysicsWorldBuilder`. `WorldFilter` field holds its physics world index (0).
    * `StepPhysicsWorld` was refactored to use `PhysicsWorldStepper`.
    * `ExportPhysicsWorld` was refactored to use `PhysicsWorldExporter`, which knows how to copy `CollisionWorld` and export data to Entities fetched by its queries.
    * Optimized `Collider.Clone()` to no longer create an extra copy of the `Collider` memory during the clone process.
* Authoring/Conversion API
    * Physics Body authoring component has a new field `WorldIndex` in Advanced section, with default value of 0 (meaning that it belongs to the default `PhysicsWorld`). Objects that don't have Physics Body authoring component on them or on any parent in the hierarchy will also get the default value. `CustomTags` field was moved to the Advanced section.
    * `PhysicsRuntimeExtensions` has new template methods `RegisterPhysicsRuntimeSystem*` which can be used in system's `OnStartRunning()` method for automatic dependency management for non-default physics runtime data. They are analoguous to the existing non-templated counterparts, just require a separate `ComponentData` type for each non-default physics world.
* Run-Time Behavior
    * Added support for multiple `PhysicsWorlds`, where each body in each world is represented by a separate Entity. Each entity must have all components that are needed for physics simulation in its world.
    * Non-default physics worlds require custom systems that will processs (build, simulate and export) them, from Entities that are marked with appropriate `PhysicsWorldIndex` shared component. Storage of `PhysicsWorld` is also controlled by the user. A number of utilities was added to make this easier.
* Authoring/Conversion Behavior
### Fixes
* Fixed a bug in Graphical Interpolation where `LocalToWorld` was not updated if rendering and physics were exactly in sync.
* Fixed the spring constant calculation during joint conversion
* Fixed the configurable joint linear limit during joint conversion
* Physics Debug Display: Draw Collider Edges performance improved
* Physics Debug Display: Draw Collider Edges for sphere colliders improved
### Known Issues


## [0.9.0-preview.4] - 2021-05-19
### Upgrade guide
* An extra check was added to verify that the data provided to the 'Start/End' properties of 'RayCastInput/ColliderCastInput' does not generate a cast length that is not too long. The maximum length allowed is half of 'float.MaxValue'
* Integrity checks can be now be enabled and disabled by toggling the new "DOTS/Physics/Enable Integrity Checks" menu item. Integrity checks should be enabled when checking simulation quality and behaviour. Integrity checks should be disabled when measuring performance. When enabled, Integrity checks will be included in a in Development build of a standalone executable, but are always excluded in release builds.
* An extra check was added to verify that the data provided to the `Start` & `End` properties of `RayCastInput` & `ColliderCastInput` does not generate a cast length that is too long. The maximum length allowed is half of `float.MaxValue`
### Changes
* Dependencies
    * Updated Burst to `1.5.3`
    * Updated Collections to `0.17.0-preview.18`
    * Updated Entities to `0.19.0-preview.30`
    * Updated Jobs to `0.10.0-preview.18`
    * Updated Test Framework to `1.1.24`
* Added `partial` keyword to all `SystemBase`-derived classes
### Fixes
* Fixed the condition for empty physics world, to return from `BuildPhysicsWorld.OnUpdate()` before calling `PhysicsWorld.Reset()`.
* Fixed a bug in `DebugDisplay.Managed.Instance.Render()` where only half of Debug Display lines were rendered for a MeshTopology.Lines type
* Fixed a bug in `CalculateAabb` method for cylinder collider that was increasing the AABB height axis by radius
* Integrity checks can now be toggled via an Editor menu item, and can be run in Development builds.
* Fixed a bug where PhysicsDebugDisplay lines would disappear if editor was paused in PlayMode

### Known Issues
## [0.8.0-preview.1] - 2021-03-26
### Upgrade guide
### Changes
* Dependencies
    * Updated Collections from `0.16.0-preview.22` to `0.17.0-preview.10`
    * Updated Entities from `0.18.0-preview.42` to `0.19.0-preview.17`
    * Updated Jobs from `0.9.0-preview.24` to `0.10.0-preview.10`

* Run-Time API
    * Made `CollisionEvent` and `TriggerEvent` implement a common `ISimulationEvent` interface allowing them to be equatable and comparable.
    * Made `ColliderKey` comparable.
    * Exposed a `MotionVelocity.IsKinematic` property.
    * Exposed a `PhysicsMass.IsKinematic` property.
    * Added a `PhysicsMassOverride.SetVelocityToZero` field. If `PhysicsMassOverride.IsKinematic`, is a non-zero value, then a body's mass and inertia are made infinite, though it's motion will still be integrated. If `PhysicsMassOverride.SetVelocityToZero` is also a non-zero value, the kinematic body will ignore the values in the associated `PhysicsVelocity` component and no longer move.

* Run-Time Behavior
	* If a body has infinite mass and/or inertia, then linear and/or angular damping will no longer be applied respectively.

### Fixes

* Fixed a bug in `ExportPhysicsWorld.CheckColliderFilterIntegrity()` method that was giving false integrity errors due to the fact that it had incorrectly handled compound colliders, if they (or their children) had a `GroupIndex` on the `Filter` that is not 0.
* Added an extra change check on the `Parent` component type to `CheckStaticBodyChangesJob` in the `BuildPhysicsWorld` system.
### Known Issues


## [0.7.0-preview.3] - 2021-02-24

### Upgrade guide
* `RegisterPhysicsRuntimeSystemReadOnly()` and `RegisterPhysicsRuntimeSystemReadWrite()` (both registered as extensions of `SystemBase`) should be used to manage physics data dependencies instead of the old `AddInputDependency()` and `GetOutputDependency()` approach. Users should declare their `UpdateBefore` and `UpdateAfter` systems as before and additionally only call one of the two new functions in their system's `OnStartRunning()`, which will be enough to get an automatic update of the `Dependency` property without the need for manually combining the dependencies as before. Note that things have not changed if you want to read or write physics runtime data directly in your system's `OnUpdate()` - in that case, you still need to ensure that jobs from previous systems touching physics runtime data are complete, by completing the `Dependency`. Also note that `BuildPhysicsWorld.AddInputDependencyToComplete()` still remains needed for jobs that need to finish before any changes are made to the PhysicsWorld, if your system is not scheduled in between other 2 physics systems that will do that for you.

* The `Constraint.DefaultSpringFrequency` and `Constraint.DefaultSpringDamping` values have been changed. The original defaults were setup to match the default `fixedDeltaTime` and therefore assumed a 50hz simulation timestep. The current default simulation step is now 60hz and so the default spring parameters have been changed to match this assumption. This change may affect more complex Joint setups that are close to being overconstrained, but generally it should not break the original intent of the setup.

### Changes

* Dependencies

    * Updated minimum Unity Editor version from `2020.1.0f1` to `2020.2.4f1`
    * Updated Collections from `0.15.0-preview.21` to `0.16.0-preview.22`
    * Updated Entities from `0.17.0-preview.41` to `0.18.0-preview.42`
    * Updated Jobs from `0.8.0-preview.23` to `0.9.0-preview.24`

* Run-Time API

    * Added a `CompoundCollider.Child.Entity` & associated `ChildCollider.Entity` field. This field is useful in creating a link back to the original Entity from which the Child was built.
    * Added `Integrator.Integrate()` which takes a `RigidTransform`, `MotionVelocity` and time step and integrates the transform in time. This can be used to integrate forward in time, but also to undo integration if necessary (by providing the negative time).
    * Removed `ModifiableJacobianHeader.HasColliderKeys`, `ModifiableJacobianHeader.ColliderKeyA` and `ModifiableJacobianHeader.ColliderKeyB` as they are not meant to be read by users, but to fill the data for collision events.
    * Removed `SimulationCallbacks.Phase.PostSolveJacobians` as it doesn't have real use cases and it's causing inconsistencies between the two engines. Instead, users should schedule their jobs after `StepPhysicsWorld` and before `ExportPhysicsWorld` to achieve a similar effect.
    * Added `ClampToMaxLength()` to `Unity.Physics.Math`, which reduces the length of specified `float3` vector to specified maxLength if it was bigger.
	* `ColliderCastHit` and `DistanceHit` now have `QueryColliderKey` field. If the input of `ColliderCast` or `ColliderDistance` queries contains a non-convex collider, this field will contain the collider key of the input collider. Otherwise, its value will be `ColliderKey.Empty`.
    * Removed `AddInputDependency()` and `GetOutputDependency()` from all physics systems as dependencies are now handled in a different way (see below).
    * Added `RegisterPhysicsRuntimeSystemReadOnly()` and `RegisterPhysicsRuntimeSystemReadWrite()` that are both extension methods for `SystemBase` and are used to declare interaction with the runtime physics data (stored in PhysicsWorld). One should call one of these in their system's `OnStartRunning()` (using `this.RegisterPhysicsRuntimeSystemReadOnly()` or `this.RegisterPhysicsRuntimeSystemReadWrite()`) to declare interaction with the physics data, which will translate to automatic data dependencies being included in the `SystemBase.Dependency` property of any system.
    * Previously internal functions in `ColliderKey`, `ColliderKeyPath` and `ChildCollider` are now public to aid the traversal of a `Collider` hierarchy.

* Authoring/Conversion API

    * A new `CompoundCollider.Child.Entity` field is automatically populated through the conversion system, to point at the converted Entity associated with the GameObject that contacted the `PhysicsShapeAuthoring` component.
        * This default setting can be overriden by adding a `PhysicsRenderEntity` component data. The common use case for this is where no `MeshRenderer` component is on the converted child Entity, and it is desirable to redirect to another Entity with a graphical representation in a different branch of the hierarchy.
    * Added a `PhysicsRenderEntity` component data. This is primarily used for populating a `CompoundCollider.Child.Entity` field where there was no graphical representation on the same GameObject that has a `PhysicsShapeAuthoring` component.

* Run-Time Behavior
	* `ColliderDistance` and `ColliderCast` queries now support non-convex input colliders (Compounds, Meshes and Terrains).

* Authoring/Conversion Behavior

### Fixes

* Fixed a bug in `ExportPhysicsWorld.OnCreate()` method that relied on the fact that `BuildPhysicsWorld.OnCreate()` was executed before it.
* Fixed `CompoundCollider.RefreshCollisionFilter()` when compound contains compound children.

### Known Issues

## [0.6.0-preview.3] - 2021-01-18

### Upgrade guide
* `PhysicsStep.ThreadCountHint` has now been removed, so if you had it set to a value less or equal to 0 (meaning you wanted single threaded simulation with small number of jobs), you now need to set the new field `PhysicsStep.MultiThreaded` to false. Otherwise, it will be set to true, meaning you'll get a default multi threaded simulation as if `PhysicsStep.ThreadCountHint` is a positive number.

### Changes

* Dependencies
    * Updated minimum Unity Editor version from `2020.1.0f1` to `2020.1.9f1`
    * Updated Burst from `1.3.7` to `1.4.1`
    * Updated Collections from `0.14.0-preview.16` to `0.15.0-preview.21`
    * Updated Entities from `0.16.0-preview.21` to `0.17.0-preview.41`
    * Updated Jobs from `0.7.0-preview.17` to `0.8.0-preview.23`

* Run-Time API
    * Added a `Collider.Clone()` function.
    * Added `Material` to `IQueryResult` interface and its implementations (`RaycastHit`,  `ColliderCastHit`, `DistanceHit`). All hits now have material information of the  primitive that was hit.
    * Added the following interfaces to `ICollidable` and all its implementations:
        * These API's represent the equivalent of the familiar GameObjects' query interface, with the addition of Custom version, which takes a collector and enables one to use custom filtering logic when accepting query hits.
        * `bool CheckSphere(float3 position, float radius, CollisionFilter filter, QueryInteraction interaction)`
        * `bool OverlapSphere(float3 position, float radius, ref NativeList<DistanceHit> outHits, CollisionFilter filter, QueryInteraction interaction)`
        * `bool OverlapSphereCustom<T>(float3 position, float radius, ref T collector, CollisionFilter filter, QueryInteraction interaction) where T : struct, ICollector<DistanceHit>`
        * `bool CheckCapsule(float3 point1, float3 point2, float radius, CollisionFilter filter, QueryInteraction queryInteraction)`
        * `bool OverlapCapsule(float3 point1, float3 point2, float radius, ref NativeList<DistanceHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction)`
        * `bool OverlapCapsuleCustom<T>(float3 point1, float3 point2, float radius, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction) where T : struct, ICollector<DistanceHit>`
        * `bool CheckBox(float3 center, quaternion orientation, float3 halfExtents, CollisionFilter filter, QueryInteraction queryInteraction)`
        * `bool OverlapBox(float3 center, quaternion orientation, float3 halfExtents, ref NativeList<DistanceHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction)`
        * `bool OverlapBoxCustom<T>(float3 center, quaternion orientation, float3 halfExtents, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction  where T : struct, ICollector<DistanceHit>`
        * `bool SphereCast(float3 origin, float radius, float3 direction, float maxDistance, CollisionFilter filter, QueryInteraction queryInteraction)`
        * `bool SphereCast(float3 origin, float radius, float3 direction, float maxDistance, out ColliderCastHit hitInfo, CollisionFilter filter, QueryInteraction queryInteraction)`
        * `bool SphereCastAll(float3 origin, float radius, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction)`
        * `bool SphereCastCustom<T>(float3 origin, float radius, float3 direction, float maxDistance, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction) where T : struct, ICollector<ColliderCastHit>`
        * `bool BoxCast(float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, CollisionFilter filter, QueryInteraction queryInteraction)`
        * `bool BoxCast(float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, out ColliderCastHit hitInfo, CollisionFilter filter, QueryInteraction queryInteraction)`
        * `bool BoxCastAll(float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction)`
        * `bool BoxCastCustom<T>(float3 center, quaternion orientation, float3 halfExtents, float3 direction, float maxDistance, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction) where T : struct, ICollector<ColliderCastHit>`
        * `bool CapsuleCast(float3 point1, float3 point2, float radius, float3 direction, float maxDistance, CollisionFilter filter, QueryInteraction queryInteraction)`
        * `bool CapsuleCast(float3 point1, float3 point2, float radius, float3 direction, float maxDistance, out ColliderCastHit hitInfo, CollisionFilter filter, QueryInteraction queryInteraction)`
        * `bool CapsuleCastAll(float3 point1, float3 point2, float radius, float3 direction, float maxDistance, ref NativeList<ColliderCastHit> outHits, CollisionFilter filter, QueryInteraction queryInteraction)`
        * `bool CapsuleCastCustom<T>(float3 point1, float3 point2, float radius, float3 direction, float maxDistance, ref T collector, CollisionFilter filter, QueryInteraction queryInteraction) where T : struct, ICollector<ColliderCastHit>`
    * Exposed the following collider initialization functions:
        * `SphereCollider.Initialize(SphereGeometry geometry, CollisionFilter filter, Material material)`
        * `CapsuleCollider.Initialize(CapsuleGeometry geometry, CollisionFilter filter, Material material)`
        * `BoxCollider.Initialize(BoxGeometry geometry, CollisionFilter filter, Material material)`
        * `CylinderCollider.Initialize(CylinderGeometry geometry, CollisionFilter filter, Material material)`
        * These functions enable the creation of colliders on stack, as opposed to only creating them using `BlobAssetReference<Collider>.Create()` methods.
    * Replaced all instances of `IJobChunk` with `IJobEntityBatch` or `IJobEntityBatchWithIndex` for better performance.
    * `CollisionWorld` and `DynamicsWorld` now store index maps linking Entity with RigidBody and Joint indices.
        * Use `PhysicsWorld.GetRigidBodyIndex(Entity)` to get a RigidBody index for the Bodies array. This replaces the variant from `PhysicsWorldExtensions`.
        * Use `PhysicsWorld.GetJointIndex(Entity)` to get a Joint index for the Joints array
        * If map is invalid or Entity is not in map then an index of -1 is returned.
        * `BuildPhysicsWorld` system updates the maps on update. If updating the world manually then call `PhysicsWorld.UpdateIndexMaps()` to refresh.
    * Removed `PhysicsStep.ThreadCountHint` since the value is now retrieved from `JobsUtility.JobWorkerCount`.
    * Added `PhysicsStep.SingleThreaded` to request the simulation with a very small number of single threaded jobs (previously `PhysicsStep.ThreadCountHint` <= 0).
    * Added `MeshCollider.Filter` and `CompoundCollider.Filter` setters that set the collision filter on all triangles of the mesh and children of the compound. Furthermore, added the `CompoundCollider.RefreshCollisionFilter()` to be called when a child filter changes, so that the root level of the compound collider can be updated.
    * `Collider` now has a `Filter` setter regardless of the type of the collider.
    * `Collider` now has a `RespondsToCollision` getter that shows if it will participate in collision, or only move and intercept queries.

* Authoring/Conversion API

* Run-Time Behavior
	* `ExportPhysicsWorld` system should now only get updated when there is at least one entity satisfying `BuildPhysicsWorld.DynamicEntityGroup` entity query.

* Authoring/Conversion Behavior

### Fixes
* Fixed the issue of `BuildPhysicsWorld` system not being run when there are not entities in the scene, leading to `StepPhysicsWorld` system operating on stale data.
* Fixed write-back in `ContactJacobian.SolveContact()` to only affect linear and angular velocity. This prevents `JacobianHeader`'s mass factors from affecting `MotionVelocity`'s mass factors, which used to have multiplicative effect on those mass factors over time.
* Fixed the issue where `ExportPhysicsWorld` system would not get run if there wasn't at least one entity that has `PhysicsCollider` component.
* Fixed a crash on Android 32 with Bursted calls to create a `Hash128`.
* Fixed a bug that was causing AABB of the compound collider to be incorrectly calculated if one of the child colliders were a TerrainCollider.

### Known Issues

## [0.5.1-preview.2] - 2020-10-14

### Upgrade guide

### Changes

* Dependencies
    * Updated Burst from `1.3.2` to `1.3.7`
    * Updated Mathematics from `1.1.0` to `1.2.1`
    * Updated Collections from `0.11.0-preview.17` to `0.14.0-preview.16`
    * Updated Entities from `0.13.0-preview.24` to `0.16.0-preview.21`
    * Updated Jobs from `0.4.0-preview.18` to `0.7.0-preview.17`

* Run-Time API
    * Added the `TerrainCollider.Filter` setter
    * Removed the `CompoundCollider.Filter` setter as it was doing the wrong thing (composite collider filters should be the union of their children)
    * Added `BuildPhysicsWorld.AddDependencyToComplete()` which takes a job dependency that the `BuildPhysicsWorld` system should complete immediately in its `OnUpdate()` call (before scheduling new jobs). The reason is that this system does reallocations in the `OnUpdate()` immediately (not in jobs), and any previous jobs that are being run before this system could rely on that data being reallocated. This way, these jobs can provide their dependency to `BuildPhysicsWorld` and make sure it will wait until they are finished before doing the reallocations.
	* Added the option to provide a custom explosion filter in 'PhysicsVelocity.ApplyExplosionForce'.

* Authoring/Conversion API

* Run-Time Behavior
    * Changed Graphical Interpolation default to simpler implementation that doesn't try and consider velocities
    * `BuildPhysicsWorld.CreateMotions` now gives Kinematic bodies a zero Gravity Factor (i.e. they will not be affected by gravity)
    * Setting the `Collider.Filter` is now allowed for Terrain colliders as well, as opposed to previously only working for Convex colliders

* Authoring/Conversion Behavior

### Fixes
* Fixed the potential issues if more than one job implements IBodyPairsJob.
* DebugStream.DrawComponent now cleans up its associated GameObject
* Fixed a bug in `PhysicsVelocity.ApplyExplosionForce` where the provided collider's collision filter could prevent the explosion from happening.
* Fixed a memory leak in the Collider debug display gizmo

### Known Issues

## [0.5.0-preview.1] - 2020-09-15

### Upgrade guide

* Physics systems now update in the `FixedStepSimulationSystemGroup`, using a fixed timestep provided by the group. This ensures that the results of the physics simulation do not depend on the application's display frame rate. Important consequences for applications:
    * Application systems that need to run at the same rate as the physics systems should also be moved into `FixedStepSimulationSystemGroup` (e.g., systems that handle collision or trigger events).
    * Application systems that should continue to run once per display frame should remove any `[UpdateAfter]` attributes targeting the physics systems. Any update order constraints for such systems should instead be expressed with respected to `FixedStepSimulationSystemGroup`.
    * The DOTS fixed timestep is get/set using the `Timestep` property on the `FixedStepSimulationSystemGroup`. The group's default timestep value is 1/60 second. The group's timestep is not affected by changes to the fixed timestep specified in the Project Settings. If this behavior is desired, applications can set `FixedStepSimulationSystemGroup.Timestep` to `UnityEngine.Time.fixedDeltaTime` every frame.
    * Care must be taken when processing input events during the FixedStepSimulationSystemGroup's update. The group may update zero, or one, or many times per display frame. If input events are polled once per display frame, naive input processing may lead to input events being skipped or processed multiple times.
    * If your application's frame rate is faster than the fixed timestep, rigid bodies may appear to move in stop motion by default (as with classic GameObject-base physics). You can enable smoothing options for their graphics representations as needed.
        * Application systems that must update once per display frame, yet which query the `Translation` or `Rotation` of dynamic rigid bodies, should instead query `LocalToWorld` when smoothed graphical transformations are required.
* It is possible to see integrity failures when updating to this release. `BuildPhysicsWorld` and `ExportPhysicsWorld` expect the chunk layouts for rigid bodies to be the same at both ends of the physics pipeline in order to write the simulation results to component data. These messages indicate that you are making some structural change or modifying rigid bodies' component data (e.g., `Translation`, `Rotation`, `PhysicsCollider`) between these systems.
* Old, unused serialized data have been removed from `PhysicsShapeAuthoring` and `PhysicsMaterialTemplate`. Ensure you have run the upgrade utility in your project before updating to this version of the package (Window -> DOTS -> Physics -> Upgrade Data).

### Changes

* Dependencies
    * Updated minimum Unity Editor version from `2019.4.0f1` to `2020.1.0f1`
    * Updated Burst from `1.3.0` to `1.3.2`
    * Updated Collections from `0.9.0-preview.6` to `0.11.0-preview.17`
    * Updated Entities from `0.11.1-preview.4` to `0.13.0-preview.24`
    * Updated Jobs from `0.2.10-preview.12` to `0.4.0-preview.18`
    * Updated Performance Testing API from `2.0.8-preview` to `2.2.0-preview`

* Run-Time API
    * Added the following new types:
        * `PhysicsGraphicsIntegration` namespace
            * `PhysicsGraphicalSmoothing` and `PhysicsGraphicalInterpolationBuffer` components, which can be used to smooth a rigid body's graphical motion when rendering and physics are out of sync
            * `RecordMostRecentFixedTime`, which stores time values from the most recent tick of `FixedStepSimulationSystemGroup`
            * `BufferInterpolatedRigidBodiesMotion`, which stores dynamic rigid bodies' motion properties at the start of the frame when when smoothed bodies use interpolation
            * `CopyPhysicsVelocityToSmoothing`, which stores dynamic rigid bodies' velocities after physics has finished in the current frame
            * `SmoothRigidBodiesGraphicalMotion`, which writes dynamic rigid bodies' `LocalToWorld` when smoothing is enabled
            * `GraphicalSmoothingUtility` class, which contains various methods for smoothing the motion of rigid bodies
    * Renamed `ComponentExtensions` to `PhysicsComponentExtensions`
    * Changed the following members/types:
        * `PhysicsComponentExtensions.GetCenterOfMassWorldSpace()` now passes `PhysicsMass` as `in` rather than `ref`.
        * `PhysicsComponentExtensions.GetLinearVelocity()` now passes `PhysicsVelocity` as `in`.
        * `PhysicsWorldExtensions.CalculateVelocityToTarget()` is now implemented and passes a `RigidTransform` for the target rather than a separate `float3` and `quaternion`.
    * Removed the following expired members/types:
        * `BodyIndexPair.BodyAIndex`
        * `BodyIndexPair.BodyBIndex`
        * Body pair interfaces on events and modifiers (`CollisionEvent`, `TriggerEvent`, `ModifiableContactHeader`, `ModifiableBodyPair` and `ModifiableJacobianHeader`)
            * `BodyCustomTags`
            * `BodyIndices`
            * `ColliderKeys`
            * `Entities`
        * `ComponentExtensions.GetCenterOfMass()`
        * `ComponentExtensions.SetCenterOfMass()`
        * `ComponentExtensions.GetAngularVelocity()`
        * `ComponentExtensions.SetAngularVelocity()`
        * `EndFramePhysicsSystem.HandlesToWaitFor`
        * `FinalJobHandle` on all core physics systems (`BuildPhysicsWorld`, `StepPhysicsWorld`, `ExportPhysicsWorld` and `EndFramePhysicsSystem`)
        * `Joint.JointData`
        * `JointData`
        * `JointFrame`
        * `Material.IsTrigger`
        * `Material.EnableCollisionEvents`
        * `MotionData.GravityFactor`
        * `PhysicsJoint.JointData`
        * `PhysicsJoint.EntityA`
        * `PhysicsJoint.EntityB`
        * `PhysicsJoint.EnableCollision`
        * `SimulationContext.Reset()` passing `PhysicsWorld`
        * `Solver.ApplyGravityAndCopyInputVelocities()` passing `NativeArray<MotionData>`
        * `Solver.SolveJacobians()` not passing explicit `StabilizationData`
    * Added `JointType.LimitedDegreeOfFreedom` and associated creation and control functions
	* Added `Aabb.Intersect()` function
	* Added `PhysicsComponentExtensions.GetEffectiveMass()`

* Authoring/Conversion API
    * Removed the following expired members/types:
        * `LegacyJointConversionSystem` (now internal)
        * `PhysicsMaterialTemplate.IsTrigger`
        * `PhysicsMaterialTemplate.RaisesCollisionEvents`
        * `PhysicsShapeAuthoring.IsTrigger` and `OverrideIsTrigger`
        * `PhysicsShapeAuthoring.RaisesCollisionEvents` and `OverrideRaisesCollisionEvents`
    * Added the following new types:
        * `BodySmoothing` enum
    * Added the following new members:
        * `PhysicsBodyAuthoring.Smoothing`, to enable motion smoothing
    * `PhysicsBodyAuthoring.LinearDamping` and `AngularDamping` setters now clamp incoming values to be at least 0.

* Run-Time Behavior
    * Debug rendering is now significantly faster, in the case of 3D lines.
    * Physics systems now update in the `FixedStepSimulationSystemGroup`.

* Authoring/Conversion Behavior
    * Classic `Rigidbody` interpolation mode is now supported during conversion.
    * Inspector help button for built-in authoring components and assets now opens the corresponding page in the API reference.

### Fixes
* Fixed issue where orientation in Physics Shape component would get dirtied when nothing changed
* Fixed issue with `PhysicsJoint.CreateHinge()` only working on a single axes.
* Fixed `Constraint.Dimension` not returning 0 when it should.
* Reduced size of compound collider/mesh collider AABB in cases where their bodies are rotated by some angle.
* Added optional integrity checks for physics ECS data consistency between BuildPhysicsWorld and ExportPhysicsWorld. Checks can be run in editor only.
* Fixed regression that caused additional sub-meshes to be ignored when converting mesh colliders.

### Known Issues
* Physics debug display may not work properly while stepping frame by frame in Editor.

## [0.4.1-preview] - 2020-07-28

### Changes

* Run-Time API
    * Added the following members:
        * `FloatRange.Mid`
        * `AABB.ClosestPoint`
    * Changed the following members/types:
        * All systems now inherit `SystemBase` instead of `ComponentSystem`.

### Fixes

* When using Unity 2020.1.0b13 or newer, it is now possible to convert mesh colliders inside of sub-scenes when their input meshes do not have read/write enabled. Meshes converted at run-time must still have read/write enabled.
* Stopped emitting warning messages about physics material properties being upgraded when creating new objects from editor scripts.
* Fixed warnings from exceptions thrown in Bursted code paths when using Burst 1.4.0.
* Fixed issue with static layer not being rebuilt when order of entities in chunk changes.
* Fixed issue with invalid colliders with empty AABB breaking bounding volume hierarchy and making objects "disappear" from the world.
* Fixed an editor crash when maximum level of composite collider nesting is breached.

## [0.4.0-preview.5] - 2020-06-18

### Upgrade guide

* `RigidBody` queries may no longer work, since they required inputs to be transformed to body space. As of this release, body queries require input in world space.
* Core physics systems now expose `AddInputDependency()` and `GetOutputDependency()` methods for user code to be able to plug in user systems between any of the core physics systems (`BuildPhysicsWorld`, `StepPhysicsWorld`, `ExportPhysicsWorld` and `EndFramePhysicsSystem`).
* The serialization layout has changed for `PhysicsShapeAuthoring` and `PhysicsMaterialTemplate`. Because data are upgraded before Prefab overrides are applied, you must manually re-apply any Prefab overrides that existed on scene objects, nested Prefabs, or Prefab variants for the migrated properties. It is recommended you use the data upgrade utility under the menu item Window -> DOTS -> Physics -> Upgrade Data.
    * `m_IsTrigger` and `m_RaisesCollisionEvents` have been replaced with `m_CollisionResponse`.
    * `m_BelongsTo` has been replaced with `m_BelongsToCategories`
    * `m_CollidesWith` has been replaced with `m_CollidesWithCategories`
    * `m_CustomTags` has been replaced with `m_CustomMaterialTags`

### Changes

* Dependencies
    * Updated minimum Unity Editor version from `2019.3.0f1` to `2019.4.0f1`
    * Updated Burst from `1.3.0-preview.7` to `1.3.0`
    * Updated Collections from `0.7.1-preview.3` to `0.9.0-preview.6`
    * Updated Entities from `0.9.0-preview.6` to `0.11.1-preview.4`
    * Updated Jobs from `0.2.8-preview.3` to `0.2.10-preview.12`
    * Updated Performance Testing API from `1.3.3-preview` to `2.0.8-preview`

* Run-Time API
    * Added the following new types:
        * `BuildPhysicsWorld.CollisionWorldProxyGroup`
        * `CollisionResponsePolicy` (introduces a new value, `None`, which allows a shape to move and participate in queries, without generating a collision response or overlap events)
        * `CollisionWorldProxy` to synchronize `CollisionWorld` with data flow graph node sets
        * `ColliderCastNode` to perform queries in a data flow graph node
        * `ForceMode` to use with extension methods analogous to those used with classic `Rigidbody`
        * `JointComponentExtensions` for use with `PhysicsJoint`
        * `JointType` to serve as a hint to code modifying joint limits
        * `PhysicsExclude` to easily exclude bodies from physics temporarily
        * `PhysicsMassOverride` to easily make dynamic bodies enter and exit a kinematic state
        * `PhysicsConstrainedBodyPair`
        * `PhysicsJointCompanion` to keep track of sets of joints used to stabilize complex joint configurations
        * `RaycastNode` to perform queries in a data flow graph node
        * `Solver.StabilizationData`
        * `Solver.StabilizationHeuristicSettings`
    * Added the following members:
        * `ComponentExtensions.ApplyExplosionForce()` equivalent to classic `Rigidbody.ApplyExplosionForce()` method.
        * `ComponentExtensions.GetImpulseFromForce()` to map `ForceMode` variants to impulses.
        * `FloatRange.Sorted()` to ensure `Min` is not larger than `Max`.
        * `PhysicsStep.SolverStabilizationHeuristicSettings` to enable and configure solver stabilization heuristic. This can improve behavior in stacking scenarios, as well as overall stability of bodies and piles, but may result in behavior artifacts. It is off by default to avoid breaking existing behavior. Setting it to true enables the heuristic and its default parameters.
        * `PhysicsStep.SynchronizeCollisionWorld` to enable rebuild of the dynamic bodies bounding volume hierarchy after the step. This enables precise query results before the next `BuildPhysicsWorld` update call. Note that `BuildPhysicsWorld` will do this work on the following frame anyway, so only use this option when another system must know about the results of the simulation before the end of the frame (e.g., to destroy or create some other body that must be present in the following frame). In most cases, tolerating a frame of latency is easier to work with and is better for performance.
        * `PhysicsVelocity.CalculateVelocityToTarget()` to move kinematic bodies to desired target locations without teleporting them.
        * `SurfaceConstraintInfo.IsMaxSlope`
        * Implemented `ToString()` on some types
            * `ColliderCastHit`
            * `ColliderCastInput`
            * `ColliderKey`
            * `RaycastHit`
            * `RaycastInput`
    * Renamed the following members/type:
        * `BodyAIndex` and `BodyBIndex` are now `BodyIndexA` and `BodyIndexB` across the codebase for consistency.
        * `ComponentExtensions.GetAngularVelocity()` and `SetAngularVelocity()` are now `GetAngularVelocityWorldSpace()` and `SetAngularVelocityWorldSpace()`, respectively.
        * `ComponentExtensions.GetCenterOfMass()` and `SetCenterOfMass()` are now `GetCenterOfMassWorldSpace()` and `SetCenterOfMassWorldSpace()`.
        * `JointFrame` is now `BodyFrame`
    * Changed the following members/types:
        * Replaced all usages of `NativeSlice` with `NativeArray`.
        * Replaced pair interfaces on events and modifiers (`CollisionEvent`, `TriggerEvent`, `ModifiableContactHeader`, `ModifiableBodyPair` and `ModifiableJacobianHeader`) with direct accessors:
            * `EntityPair` is now `EntityA` and `EntityB`
            * `BodyIndexPair` is now `BodyAIndex` and `BodyBIndex`
            * `ColliderKeyPair` is now `ColliderKeyA` and `ColliderKeyB`
            * `CustomTagsPair` is now `CustomTagsA` and `CustomTagsB`
        * `Material.MaterialFlags` is now internal. Access flags through individual properties (`CollisionResponse`, `EnableMassFactors` and `EnableSurfaceVelocity`).
        * `Joint` is now a fixed size.
            * `Joint.EnableCollision` is now `byte` instead of `int`.
            * `Joint.JointData` has been deprecated. Use `AFromJoint`, `BFromJoint`, `Constraints`, and `Version` instead.
        * `PhysicsJoint` is now mutable.
        * `PhysicsJoint.CreatePrismatic()` factory does not take a `distanceFromAxis` parameter (in contrast to `JointData` factory)
        * `PhysicsJoint.CreateRagdoll()` factory now takes perpendicular angular limits in the range (-pi/2, pi/2) instead of (0, pi) in old `JointData` factory.
    * Deprecated the following members/types:
        * `EndFramePhysicsSystem.HandlesToWaitFor` (use `AddInputDependency()` instead).
        * `FinalJobHandle` on all core physics systems (`BuildPhysicsWorld`, `StepPhysicsWorld`, `ExportPhysicsWorld` and `EndFramePhysicsSystem`) (use `GetOutputDependency()` instead).
        * `JointData`
        * `Material.IsTrigger` (use `CollisionResponse` instead)
        * `Material.EnableCollisionEvents` (use `CollisionResponse` instead)
        * `PhysicsJoint.EntityA` (now defined on `PhysicsConstrainedBodyPair`)
        * `PhysicsJoint.EntityB` (now defined on `PhysicsConstrainedBodyPair`)
        * `PhysicsJoint.EnableCollision` (now defined on `PhysicsConstrainedBodyPair`)
        * `PhysicsJoint.JointData` (use `BodyAFromJoint`, `BodyBFromJoint`, and `GetConstraints()`/`SetConstraints()` instead).
        * `SimulationContext.Reset()` passing `PhysicsWorld` (use signature passing `SimulationStepInput` instead).
    * Removed the following members/types:
        * `CollisionFilter.IsValid`
        * `CollisionWorld.ScheduleUpdateDynamicLayer()`
        * `Constraint` factory signatures passing `float` values for ranges:
            * `Cone()`
            * `Cylindrical()`
            * `Planar()`
            * `Twist()`
        * `ISimulation.ScheduleStepJobs()` signature without callbacks and thread count hint (as well as all implementations)
        * `JointData` factory signatures passing `float3` and `quaternion` pairs for joint frames, as well as `Constraint[]`:
            * `CreateFixed()`
            * `CreateHinge()`
            * `CreateLimitedHinge()`
            * `CreatePrismatic()`
            * `CreateRagdoll()`
        * `MassFactors.InvInertiaAndMassFactorA`
        * `MassFactors.InvInertiaAndMassFactorB`
        * `MotionData.GravityFactor` (use `MotionVelocity.GravityFactor` instead)
        * `MotionVelocity.InverseInertiaAndMass`
        * `RigidBody.HasCollider`
        * `SimplexSolver.Solve()` signature passing `PhysicsWorld`
        * `SimulationStepInput.ThreadCountHint`

* Authoring/Conversion API
    * Added the following types
        * `BeginJointConversionSystem`
        * `EndJointConversionSystem` (allows other conversion systems to find joint entities created during conversion)
        * `PhysicsMaterialTemplate.CollisionResponse`
        * `PhysicsShapeAuthoring.CollisionResponse`
        * `PhysicsShapeAuthoring.OverrideCollisionResponse`
    * Added the following members
        * `PhysicsStepAuthoring.EnableSolverStabilizationHeuristic` to enable solver stabilization heuristic with default settings
        * `PhysicsStepAuthoring.SynchronizeCollisionWorld`
    * Deprecated the following members/types:
        * `LegacyJointConversionSystem` has been marked obsolete and will be made internal in the future. Use `BeginJointConversionSystem` and `EndJointConversionSystem` to schedule system updates as needed.
        * `PhysicsMaterialTemplate.IsTrigger` (use `CollisionResponse` property instead)
        * `PhysicsMaterialTemplate.RaisesCollisionEvents` (use `CollisionResponse` property instead)
        * `PhysicsShapeAuthoring.IsTrigger` (use `CollisionResponse` property instead)
        * `PhysicsShapeAuthoring.RaisesCollisionEvents` (use `CollisionResponse` property instead)
        * `PhysicsShapeAuthoring.OverrideIsTrigger` (use `OverrideCollisionResponse` property instead)
        * `PhysicsShapeAuthoring.OverrideRaisesCollisionEvents` (use `OverrideCollisionResponse` property instead)
    * Removed the following expired members:
        * `LegacyColliderConversionSystem.ProduceMaterial()` (removed without expiration; class is not intended to be sub-classed outside the package)
        * `PhysicsShapeAuthoring.GetCapsuleProperties()` signature returning `CapsuleGeometry`
        * `PhysicsShapeAuthoring.SetCapsule()` signature passing `CapsuleGeometry`

* Run-Time Behavior
    * `BuildPhysicsWorld.JointEntityGroup` now requires both a `PhysicsJoint` and `PhysicsConstrainedBodyPair` component.
    * `Constraint` factory methods taking a `FloatRange` swizzle the input value if needed to ensure that `Min` cannot be greater than `Max`.
    * `RigidBody` queries (`Raycast()`, `ColliderCast()`, `PointDistance()`, and `ColliderDistance()`) now use world space input, and provide world space output, instead of using body space.

* Authoring/Conversion Behavior
    * It is now possible to create a compound collider by adding multiple `PhysicsShapeAuthoring` components to a single GameObject.
    * It is now possible to convert a GameObject with multiple `Joint` components on it.

### Fixes

* When using Unity 2019.3.1f1 and newer, making changes to a `PhysicsMaterialTemplate` asset or a `Mesh` asset will now trigger a reimport of any sub-scenes containing shapes that reference it.
* `IBodyPairsJob` now skips all joint pairs, when previously it didn't skip the joint pair if it was the first one in the list.
* Fixed the issue of the `CompoundCollider` with no colliding children (all children are triggers) having invalid mass properties.
* Fixed a potential race condition when `SynchronizeCollisionWorld` was set to true in the `SimulationStepInput`.
* `CollisionFilter` and `Advanced` material properties in the Inspector can now be expanded by clicking the label.

## [0.3.2-preview] - 2020-04-16

### Upgrade guide

* All `PhysicsShapeAuthoring` components that were newly added after version 0.3.0 were incorrectly initialized to have a bevel radius of 0. It is recommended that you audit recently authored content to assign reasonable non-zero values.

### Changes

* Dependencies
    * Updated Collections from `0.5.2-preview.8` to `0.7.1-preview.3`
    * Updated Entities from `0.6.0-preview.24` to `0.9.0-preview.6`
    * Updated Jobs from `0.2.5-preview.20` to `0.2.8-preview.3`

* Run-Time Behavior
    * In order to test gameplay end to end determinism users should:
        * Navigate to UnityPhysicsEndToEndDeterminismTest.cs (and HavokPhysicsEndToEndDeterminsmTest.cs if using HavokPhysics)
        * Remove #if !UNITY_EDITOR guards around [TestFixture] and [UnityTest] attributes (alternatively, users can run the tests
          in standalone mode without the need to remove #ifs)
        * Add scene that needs to be tested (File -> Build Settings -> Add Open Scenes)
        * Make sure that synchronous burst is enabled
        * Run the test in Test Runner

* Authoring/Conversion Behavior
    * Compound conversion system is now deterministic between runs.

### Fixes

* Volume of dynamic meshes is now being approximated with the volume of the mesh AABB, as opposed to previously being set to 0.
* Fixed regression causing newly added `PhysicsShapeAuthoring` components to initialize with a bevel radius of 0.

### Known Issues

## [0.3.1-preview] - 2020-03-19

### Upgrade guide

 * User implemented query collectors (`ICollector<T>`) may no longer work. The reason is that `ICollector<T>.TransformNewHits()` was removed. To get the collectors working again, move all logic from
 `ICollector<T>.TransformNewHits()` to `ICollector<T>.AddHit()`. All the information is now available in `ICollector<T>.AddHit()`. Also, `IQueryResult.Transform()` was removed, and user implementations of it will not get called anywhere in the engine.

### Changes

* Dependencies
    * Updated Burst from `1.3.0-preview.3` to `1.3.0-preview.7`

* Run-Time API
    * The following properties are added to `IQueryResult` interface:
        * `RigidBodyIndex`
        * `ColliderKey`
        * `Entity`
    * `ICollector.AddHit()` now has all the information ready to perform custom logic, instead of waiting for TransformNewHits().
    * Removed `Transform()` from `IQueryResult` interface and from all its implementations.
    * Removed both `TransformNewHits()` methods from `ICollector` interface and from all its implementations. All the information is now ready in `ICollector.AddHit()`.

### Fixes

* Setting `Collider.Filter` now increments the header version so that the simulation backends can recognise the change.
* Asking for collision/trigger events in scenes with no dynamic bodies no longer throws errors.
* Updated to new version of Burst, which fixes a regression that caused `ConvexCollider.Create()` to produce hulls with a very small bevel radius.
* DOTS Run-time failures due to multiple inheritance of jobs have now been fixed.
* Changed `Math.IsNormalized` to use a larger tolerance when comparing float3 length.

## [0.3.0-preview.1] - 2020-03-12

### Upgrade guide

* In order to lead to more predictable behavior and allow for repositioning instantiated prefabs, static bodies are no longer un-parented during conversion. For best performance and guaranteed up-to-date values, it is still recommended that static bodies either be root-level entities, or that static shapes be set up as compounds. Dynamic and kinematic bodies are still un-parented. If your game code assumes static bodies are converted into world space, you may need to decompose `LocalToWorld` using `Math.DecomposeRigidBodyTransform()` rather than reading directly from `Translation` and `Rotation`.

### Changes

* Dependencies
    * Updated Entities from `0.3.0-preview.4` to `0.6.0-preview.24`
    * Added Burst `1.3.0-preview.3`
    * Added Collections `0.5.2-preview.8`
    * Added Jobs `0.2.5-preview.20`
    * Added Mathematics `1.1.0`

* Run-Time API
    * Added the following new types:
        * `CollisionEvents` (allows iterating through events directly using a `foreach` loop, rather than only via `ICollisionEventsJob`)
        * `DispatchPairSequencer`
        * `IBodyPairsJobBase`
        * `ICollisionEventsJobBase`
        * `IContactsJobBase`
        * `IJacobiansJobBase`
        * `ITriggerEventsJobBase`
        * `Integrator`
        * `JointFrame`
        * `Math.FloatRange`
        * `NarrowPhase`
        * `SimulationContext`
        * `SimulationJobHandles`
        * `Solver`
        * `TriggerEvents` (allows iterating through events directly using a `foreach` loop, rather than only via `ITriggerEventsJob`)
        * `Velocity`
    * Added the following members:
        * `CollisionWorld.DynamicBodies`
        * `CollisionWorld.NumDynamicBodies`
        * `CollisionWorld.NumStaticBodies`
        * `CollisionWorld.StaticBodies`
        * `CollisionWorld.BuildBroadphase()`
        * `CollisionWorld.FindOverlaps()`
        * `CollisionWorld.Reset()`
        * `CollisionWorld.ScheduleBuildBroadphaseJobs()`
        * `CollisionWorld.ScheduleFindOverlapsJobs()`
        * `CollisionWorld.ScheduleUpdateDynamicTree()`
        * `CollisionWorld.UpdateDynamicTree()`
        * `DynamicsWorld.Reset()`
        * `Math.DecomposeRigidBodyOrientation()`
        * `Math.DecomposeRigidBodyTransform()`
        * `MassFactors.InverseInertiaFactorA`
        * `MassFactors.InverseInertiaFactorB`
        * `MassFactors.InverseMassFactorA`
        * `MassFactors.InverseMassFactorB`
        * `MotionVelocity.InverseInertia`
        * `MotionVelocity.InverseMass`
        * `Simulation.CollisionEvents`
        * `Simulation.TriggerEvents`
        * `Simulation.StepImmediate()`
    * Changed the following members/types:
        * `CollisionWorld` constructor now requires specifying the number of dynamic and static bodies separately
        * `CollisionWorld.NumBodies` is now read-only
        * `DynamicsWorld.NumJoints` is now read-only
        * `DynamicsWorld.NumMotions` is now read-only
        * `ISimulation.ScheduleStepJobs()` now requires callbacks and thread count hint
        * `ITreeOverlapCollector.AddPairs()` now has optional bool parameter to swap the bodies
        * `Joint.JointData` is now `BlobAssetReference<Joint>` instead of `Joint*`
        * `RigidBody.Collider` is now `BlobAssetReference<Collider>` instead of `Collider*`
        * The following types no longer implement `ICloneable`. Their `Clone()` methods now return instances of their respective types rather than `object`:
            * `CollisionWorld`
            * `DynamicsWorld`
            * `PhysicsWorld`
        * The following types now implement `IEquatable<T>`:
            * `Constraint`
    * Deprecated the following members:
        * `CollisionFilter.IsValid` (use its opposite, `CollisionFilter.IsEmpty` instead)
        * `CollisionWorld.ScheduleUpdateDynamicLayer()` (use `ScheduleUpdateDynamicTree()` instead)
        * `Constraint.StiffSpring()` (use `Constraint.LimitedDistance()` instead)
        * `Constraint` factory signatures passing `float` values for ranges (use new signatures taking `FloatRange` instead):
            * `Cone()`
            * `Cylindrical()`
            * `Planar()`
            * `Twist()`
        * `JointData.Create()` signature passing `MTransform` and `Constraint[]` (use new signature that takes `JointFrame` and `NativeArray<Constraint>` instead)
        * `JointData.CreateStiffSpring()` (use `JointData.CreateLimitedDistance()` instead)
        * `JointData` factory signatures passing `float3` and `quaternion` pairs for joint frames (use new signatures taking `JointFrame` instead):
            * `CreateFixed()`
            * `CreateHinge()`
            * `CreateLimitedHinge()`
            * `CreatePrismatic()`
            * `CreateRagdoll()`
        * `MassFactors.InvInertiaAndMassFactorA` (use `InverseInertiaFactorA` and/or `InverseMassFactorA` instead)
        * `MassFactors.InvInertiaAndMassFactorB` (use `InverseInertiaFactorB` and/or `InverseMassFactorB` instead)
        * `MotionVelocity.InverseInertiaAndMass` (use `InverseInertia` and/or `InverseMass` instead)
        * `RigidBody.HasCollider` (use `Collider.IsCreated` instead)
        * `SimplexSolver.Solve()` signature passing `PhysicsWorld` (use new signature that does not require one, as it is not used)
        * `SimulationStepInput.ThreadCountHint` (supply desired value directly to `ScheduleStepJobs()`)
    * Removed the following expired members:
        * `Broadphase.ScheduleBuildJobs()` signature that does not specify gravity
        * `CollisionWorld.ScheduleUpdateDynamicLayer()` signature that does not specify gravity
        * `JointData.NumConstraints`
        * `MeshCollider.Create()` signatures taking `NativeArray<int>`
        * `PhysicsWorld.CollisionTolerance`
        * `SimplexSolver.Solve()` signature taking `NativeArray<SurfaceConstraintInfo>`
        * `SimulationStepInput.Callbacks` (removed without expiration; pass callbacks to `ScheduleStepJobs()`)

* Authoring/Conversion API
    * Added the following new types:
        * `LegacyJointConversionSystem`
        * `CapsuleGeometryAuthoring`
    * Added the following members:
        * `PhysicsShapeAuthoring.GetCapsuleProperties()` signature returning `CapsuleGeometryAuthoring`
        * `PhysicsShapeAuthoring.SetCapsule()` signature passing `CapsuleGeometryAuthoring`
    * Deprecated the following members:
        * `PhysicsShapeAuthoring.GetCapsuleProperties()` signature returning `CapsuleGeometry`
        * `PhysicsShapeAuthoring.SetCapsule()` signature passing `CapsuleGeometry`
    * Removed the following expired members/types:
        * `BaseShapeConversionSystem<T>.ProduceColliderBlob()`
        * `DisplayCollisionEventsSystem.DisplayCollisionEventsJob`
        * `DisplayCollisionEventsSystem.FinishDisplayCollisionEventsJob`
        * `DisplayCollisionEventsSystem.ScheduleCollisionEventsJob()`
        * `DisplayContactsSystem.DisplayContactsJob`
        * `DisplayContactsSystem.FinishDisplayContactsJob`
        * `DisplayContactsSystem.ScheduleContactsJob()`
        * `DisplayTriggerEventsSystem.DisplayTriggerEventsJob`
        * `DisplayTriggerEventsSystem.FinishDisplayTriggerEventsJob`
        * `DisplayTriggerEventsSystem.ScheduleTriggerEventsJob()`
        * `PhysicsShapeAuthoring.GetMeshProperties()` using `NativeList<int>`. Use the signature taking `NativeList<int3>` instead

* Run-Time Behavior
    * The data protocol for static rigid bodies has changed so that at least one of `Translation`, `Rotation`, or `LocalToWorld` is required.
        * If a static body as a `Parent`, then its transform is always decomposed from its `LocalToWorld` component (which may be the result of last frame's transformations if you have not manually updated it).
        * If there is no `Parent`, then the body is assumed to be in world space and its `Translation` and `Rotation` are read directly as before, if they exist; otherwise corresponding values are decomposed from `LocalToWorld` if it exists.
        * In any case where a value must be decomposed from `LocalToWorld` but none exists, then a corresponding identity value is used (`float3.zero` for `Translation` and `quaternion.identity` for `Rotation`).
    * The following `CollisionWorld` queries no longer assert if the supplied collision filter is empty:
        * `CalculateDistance()`
        * `CastCollider()`
        * `CastRay()`
        * `OverlapAabb()`
    * `Simulation.ScheduleStepJobs()` can now schedule different types of simulation:
        * Providing a valid `threadCountHint` (>0) as the last parameter will schedule a multi-threaded simulation
        * Not providing a `threadCount` or providing an invalid value (<=0) will result in a simulation with a very small number of jobs being spawned (one per step phase)
        * Both simulation types offer the exact same customization options (callbacks) and outputs (events)
    * `ISimulation.Step()` is intended to allow you to step a simulation immediately in the calling code instead of spawning jobs
        * Unfortunately, `Unity.Physics.Simulation` is currently not Burst-compatible, and therefore can't be wrapped in a Burst compiled job
        * `Simulation.StepImmediate()` should be wrapped in a single Burst compiled job for lightweight stepping logic; however, it doesn't support callbacks
        * If callbacks are needed, one should implement the physics step using a set of function calls that represent different phases of the physics engine and add customization logic in between these calls; this should all be wrapped in a Burst compiled job; check `Simulation.StepImmediate()` for the full list of functions that need to be called
    * Previously, if an Entity referenced by a `PhysicsJoint` component was deleted, `BuildPhysicsWorld.CreateJoints()` would assert after failing to find a valid rigid body index. Now, if no valid body is found, the `Joint.BodyIndexPair` is marked as invalid instead and the simulation will ignore them.
    * Triggers no longer influence the center of mass or inertia of any compound colliders that they are part of.

* Authoring/Conversion Behavior
    * Implicitly static shapes (as well as explicitly static bodies) in a hierarchy are no longer un-parented during the conversion process. Dynamic and kinematic bodies still are.
    * Bevel radius and convex hull simplification tolerance are no longer modified by an object's scale when it is converted.
    * Improved performance of converting mesh colliders and convex hulls, particularly when they appear as multiple child instances in a compound collider.
    * Improved visualization of primitive shapes in the SceneView.
    * `PhysicsShapeAuthoring` components are now editable from within the SceneView.
    * `PhysicsShapeAuthoring` primitives are now oriented more correctly when non uniformly scaled.

### Fixes

* Fixed regression where displaying a mesh or convex hull preview on a shape authoring component immediately after a domain reload would trigger an assert.
* Fixed possible bug where classic `MeshCollider` components might convert to the incorrect shape.
* Frame Selected (F key in scene view) now properly focuses on the collision shape bounds.
* `ConvexCollider.Create()` had been allocating too much memory for `FaceVerticesIndex` array.
* `MotionVelocity.CalculateExpansion()` formula changed to reduce expansion of AABB due to rotation.
* `ForceUnique` checkbox in the Inspector is no longer disabled for primitive `PhysicsShape` types.
* Fixed a bug in scheduling resulting in race conditions during the solving phase.
* `PhysicsStepAuthoring` and `PhysicsDebugDisplayAuthoring` no longer throw exceptions when embedded in a sub-scene.

### Known Issues

* Due to a bug in the `TRSToLocalToParent` system, static bodies in a hierarchy will always cause the broad phase to be rebuilt. This will be fixed in the next version of entities after 0.6.0.
* Not all properties on classic joint components are converted yet:
    * Converted properties include connected bodies, axes, collisions, anchors, and linear/angular limits.
    * Motors, spring forces, mass scale, break limits, and projection settings are currently ignored.
    * Swapping bodies on `ConfigurableJoint` is not yet supported.
    * `ConfigurableJoint` setups with at least one free axis of angular motion are only stable if both other axes are locked (hinge), or both other axes are free (ball-and-socket).
    * Because solver behavior differs, some configurations also currently have a different feel from their classic counterparts.
* Modifying classic joints with Live Link enabled will currently leak memory.
* SceneView editing of `PhysicsShapeAuthoring` components currently only affects their size, but not their center offset.

## [0.2.5-preview.1] - 2019-12-05

### Fixes

* Fixed a bug that could cause compound colliders to not update with Live Link when moving around a mesh collider in the compound's hierarchy.
* Fixed possible incorrect instancing of colliders with different inputs

## [0.2.5-preview] - 2019-12-04

### Upgrade guide

* By default, `PhysicsColliders` that share the same set of inputs and that originate from the same sub-scene should reference the same data at run-time. This change not only reduces memory pressure, but also speeds up conversion. If you were altering `PhysicsCollider` data at run-time, you need to enable the `ForceUnique` setting on the respective `PhysicsColliderAuthoring` component. This setting guarantees the object will always convert into a unique instance.
* Any usages of `BlockStream` should be replaced with the `NativeStream` type from the com.unity.collections package.

### Changes

* Run-Time API
    * Removed `BlockStream` and migrated all usages to `NativeSteam`.
    * Access to `JointData.Constraints` has changed to an indexer. This means that to change the values of a `Constraint`, a copy should be made first. E.g.,
        ```c#
        var c = JointData.Constraints[index];
        c.Min = 0;
        JointData.Constraints[index] = c;
        ```
    * Added `maxVelocity` parameter to `SimplexSolver.Solve()` to clamp the solving results to the maximum value.
    * Added `SurfaceConstraintInfo.IsTooSteep` to indicate that a particular surface constraint has a slope bigger than the slope character controller supports.
    * `MeshCollider.Create()` now takes grouped triangle indices (NativeArray<int3>) instead of a flat list of indices (NativeArray<int>) as input.
    * Removed the following expired members/types:
        * `BoxCollider.ConvexRadius` (renamed to `BevelRadius`)
        * `CyllinderCollider.ConvexRadius` (renamed to `BevelRadius`)
        * Collider factory methods passing nullable types and numeric primitives:
            * `BoxCollider.Create()`
            * `CyllinderCollider.Create()`
            * `CapsuleCollider.Create()`
            * `ConvexCollider.Create()`
            * `MeshCollider.Create()`
            * `PolygonCollider.CreateQuad()`
            * `PolygonCollider.CreateTriangle()`
            * `SphereCollider.Create()`
            * `TerrainCollider.Create()`
        * `ComponentExtensions` members passing Entity (use components or variants passing component data instead):
            * `ApplyAngularImpulse()`
            * `ApplyImpulse()`
            * `ApplyLinearImpulse()`
            * `GetAngularVelocity()`
            * `GetCenterOfMass()`
            * `GetCollisionFilter()`
            * `GetEffectiveMass()`
            * `GetLinearVelocity()`
            * `GetMass()`
            * `GetPosition()`
            * `GetRotation()`
            * `GetVelocities()`
            * `SetAngularVelocity()`
            * `SetLinearVelocity()`
            * `SetVelocities()`
        * `CustomDataPair`
        * `ISimulation.ScheduleStepJobs()`
        * `JacobianFlags.EnableEnableMaxImpulse`
        * `MaterialFlags.EnableMaxImpulse`
        * `ModifiableContactHeader.BodyCustomDatas`
        * `ModifiableJacobianHeader.HasMaxImpulse`
        * `ModifiableJacobianHeader.MaxImpulse`
        * `ModifiableContactJacobian.CoefficientOfRestitution`
        * `ModifiableContactJacobian.FrictionEffectiveMassOffDiag`
        * `PhysicsCustomData`
        * `SimplexSolver.c_SimplexSolverEpsilon`
        * `SimplexSolver.Solve()` without `minDeltaTime` parameter

* Authoring/Conversion API
    * Added `PhysicsShapeAuthoring.ForceUnique`.
    * Added the following conversion systems:
        * `BeginColliderConversionSystem`
        * `BuildCompoundCollidersConversionSystem`
        * `EndColliderConversionSystem`
    * `PhysicsShapeAuthoring.GetMeshProperties()` now populates a `NativeList<int3>` for indices, instead of a `NativeList<int>`.
    * The following public members have been made protected:
        * `DisplayCollisionEventsSystem.FinishDisplayCollisionEventsJob`
        * `DisplayTriggerEventsSystem.FinishDisplayTriggerEventsJob`
    * Removed the following expired members/types:
        * `BaseShapeConversionSystem<T>.GetCustomFlags()`
        * `DisplayCollidersSystem.DrawComponent`
        * `DisplayCollidersSystem.DrawComponent.DisplayResult`
        * `DisplayContactsJob`
        * `DisplayJointsJob`
        * `FinishDisplayContactsJob`
        * `PhysicsShapeAuthoring` members:
            * `SetConvexHull()` passing only a mesh
            * `GetMesh()` replaced with `GetMeshProperties`
            * `ConvexRadius` replaced with `BevelRadius`
            * `GetBoxProperties()` returning `void`
            * `SetBox()` passing a box center, size and orientation. Pass `BoxGeometry` instead.
            * `GetCapsuleProperties()` returning `void`
            * `SetCapsule()` passing a capsule center, height, radius and orientation. Pass `CapsuleGeometry` instead.
            * `GetCylinderProperties()` returning `void`
            * `SetCylinder()` passing a cylinder center, height, radius and orientation. Pass `CylinderGeometry` instead.
            * `GetSphereProperties()` returning `void`
            * `SetSphere()` passing a sphere center, radius and orientation. Pass `SphereGeometry` instead.
        * The following components have been removed:
            * `PhysicsBody` (renamed to `PhysicsBodyAuthoring`)
            * `PhysicsShape` (renamed to `PhysicsShapeAuthoring`)
            * `PhysicsStep` (renamed to `PhysicsStepAuthoring`)
            * `PhysicsDebugDisplay` (renamed to `PhysicsDebugDisplayAuthoring`)

* Run-Time Behavior
    * `CompoundCollider.Create()` is now compatible with Burst.
    * `CompoundCollider.Create()` now correctly shares memory among repeated instances in the list of children.

* Authoring/Conversion Behavior
    * If mesh and convex `PhysicsShapeAuthoring` components have not explicitly opted in to `ForceUnique`, they may share the same `PhysicsCollider` data at run-time, if their inputs are the same.
    * Classic `MeshCollider` instances with the same inputs will always share the same data at run-time when converted in a sub-scene.
    * Further improved performance of collider conversion for all types.

### Fixes

* `JointData.Version` was not being incremented with changes to its properties.
* Fixed the issue of uninitialized array when scheduling collision event jobs with no dynamic bodies in the scene.
* Fixed the issue of `CollisionEvent.CalculateDetails()` reporting 0 contact points in some cases.
* Fixed the issue of joints with enabled collisions being solved after contacts for the same body pair.
* Fixed exception and leak caused by trying to display a convex hull preview for a physics shape with no render mesh assigned.
* Fixed bug causing incremental changes to a compound collider to accumulate ghost colliders when using Live Link.
* Fixed an issue where kinematic (i.e. infinite mass dynamic) bodies did not write to the collision event stream correctly.

### Known Issues

* Compound collider mass properties are not correctly updated while editing using Live Link.


## [0.2.4-preview] - 2019-09-19

### Changes

* Updated dependency on `com.unity.entities` to version `0.1.1-preview`. If you need to stay on entities version `0.0.12-preview.33` then you can use the previous version of this package, `0.2.3-preview`, which is feature equivalent:



## [0.2.3-preview] - 2019-09-19

### Upgrade guide

* Implicitly static shapes (i.e. those without a `PhysicsBodyAuthoring` or `Rigidbody`) in a hierarchy under a GameObject with `StaticOptimizeEntity` are now converted into a single compound `PhysicsCollider` on the entity with the `Static` tag. If your queries or contact events need to know about data associated with the entities from which these leaf shapes were created, you need to explicitly add `PhysicsBodyAuthoring` components with static motion type, in order to prevent them from becoming part of the compound.

### Changes

* Run-Time API
    * Deprecated `Broadphase.ScheduleBuildJobs()` and provided a new implementation that takes gravity as input.
    * Deprecated `CollisionWorld.ScheduleUpdateDynamicLayer()` and provided a new implementation that takes gravity as input.
    * Deprecated `PhysicsWorld.CollisionTolerance` and moved it to `CollisionWorld`.
    * Deprecated `ManifoldQueries.BodyBody()` and provided a new implementation that takes two bodies and two velocities.
    * Added `CollisionEvent.CalculateDetails()` which provides extra information about the collision event:
        * Estimated impulse
        * Estimated impact position
        * Array of contact point positions
    * Removed `CollisionEvent.AccumulatedImpulses` and provided `CollisionEvent.CalculateDetails()` which gives a more reliable impulse value.

* Authoring/Conversion API
    * Removed the following expired ScriptableObject:
        * `CustomFlagNames`
    * Removed the following expired members:
        * `PhysicsShapeAuthoring.GetBelongsTo()`
        * `PhysicsShapeAuthoring.SetBelongsTo()`
        * `PhysicsShapeAuthoring.GetCollidesWith()`
        * `PhysicsShapeAuthoring.SetCollidesWith()`
        * `PhysicsShapeAuthoring.OverrideCustomFlags`
        * `PhysicsShapeAuthoring.CustomFlags`
        * `PhysicsShapeAuthoring.GetCustomFlag()`
        * `PhysicsShapeAuthoring.SetCustomFlag()`
        * `PhysicsMaterialTemplate.GetBelongsTo()`
        * `PhysicsMaterialTemplate.SetBelongsTo()`
        * `PhysicsMaterialTemplate.GetCollidesWith()`
        * `PhysicsMaterialTemplate.SetCollidesWith()`
        * `PhysicsMaterialTemplate.CustomFlags`
        * `PhysicsMaterialTemplate.GetCustomFlag()`
        * `PhysicsMaterialTemplate.SetCustomFlag()`

* Run-Time Behavior
    * Gravity is now applied at the beginning of the step, as opposed to previously being applied at the end during integration.

* Authoring/Conversion Behavior
    * Implicitly static shapes in a hierarchy under a GameObject with `StaticOptimizeEntity` are now converted into a single compound `PhysicsCollider`.

### Fixes

* Fixed issues preventing compatibility with DOTS Runtime.
* Fixed occasional tunneling of boxes through other boxes and mesh triangles.
* Fixed incorrect AABB sweep direction during collisions with composite colliders, potentially allowing tunneling.
* Fixed obsolete `MeshCollider.Create()` creating an empty mesh.
* Fixed obsolete `ConvexCollider.Create()` resulting in infinite recursion.
* Fixed simplex solver bug causing too high output velocities in 3D solve case.
* Fixed bug causing custom meshes to be ignored on convex shapes when the shape's transform was bound to a skinned mesh.
* Fixed Burst incompatibilities in the following types:
    * `BoxGeometry`
    * `CapsuleGeometry`
    * `CollisionFilter`
    * `ConvexHullGenerationParameters`
    * `CylinderGeometry`
    * `Material`
    * `SphereGeometry`
* Improved performance of `MeshConnectivityBuilder.WeldVertices()`.
* Fixed a potential assert when joints are created with a static and dynamic body (in that order).

### Known Issues



## [0.2.2-preview] - 2019-09-06

### Fixes

* Added internal API extensions to work around an API updater issue with Unity 2019.1 to provide a better upgrading experience.



## [0.2.1-preview] - 2019-09-06

### Upgrade guide

* A few changes have been made to convex hulls that require double checking convex `PhysicsShapeAuthoring` components:
    * Default parameters for generating convex hulls have been tweaked, which could result in minor differences.
    * Bevel radius now applies a shrink to the shape rather than an expansion (as with primitive shape types).
* Mesh `PhysicsShapeAuthoring` objects with no custom mesh assigned now include points from enabled mesh renderers on their children (like convex shapes). Double check any mesh shapes in your projects.
* Due to a bug in version 0.2.0, any box colliders added to uniformly scaled objects had their scale baked into the box size parameter when initially added and/or when fit to render geometry. Double check box colliders on any uniformly scaled objects and update them as needed (usually by just re-fitting them to the render geometry).
* The serialization layout of `PhysicsShapeAuthoring` has changed. Values previously saved in the `m_ConvexRadius` field will be migrated to `m_ConvexHullGenerationParameters.m_BevelRadius`, and a `m_ConvexRadius_Deprecated` field will then store a negative value to indicate the old data have been migrated. Because this happens automatically when objects are deserialized, prefab instances may mark this field dirty even if the prefab has already been migrated. Double check prefab overrides for Bevel Radius on your prefab instances.

### Changes

* Run-Time API
    * Added the following new members:
        * `BodyIndexPair.IsValid`
        * `Math.Dotxyz1()` using double
        * `Plane.Projection()`
        * `Plane.SignedDistanceToPoint()`
    * Added the following structs:
        * `BoxGeometry`
        * `CapsuleGeometry`
        * `CylinderGeometry`
        * `SphereGeometry`
        * `ConvexHullGenerationParameters`
    * `Constraint` now implements `IEquatable<Constraint>` to avoid boxing allocations.
    * All previous `SphereCollider`, `CapsuleCollider`, `BoxCollider` and `CylinderCollider` properties are now read only. A new `Geometry` property allows reading or writing all the properties at once.
    * `BoxCollider.Create()` now uses `BoxGeometry`. The signature passing nullable types has been deprecated.
    * `CapsuleCollider.Create()` now uses `CapsuleGeometry`. The signature passing nullable types has been deprecated.
    * `ConvexCollider.Create()` now uses `ConvexHullGenerationParameters`. The signature passing nullable types has been deprecated.
    * `CylinderCollider.Create()` now uses `CylinderGeometry`. The signature passing nullable types has been deprecated.
    * `MeshCollider.Create()` now uses native containers. The signature using managed containers has been deprecated.
    * `PolygonCollider.CreateQuad()` signature passing nullable types has been deprecated.
    * `PolygonCollider.CreateTriangle()` signature passing nullable types has been deprecated.
    * `SphereCollider.Create()` now uses `SphereGeometry`. The signature passing nullable types has been deprecated.
    * `TerrainCollider.Create()` signature passing pointer and nullable types has been deprecated.
    * `SimplexSolver.Solve()` taking the `respectMinDeltaTime` has been deprecated. Use the new `SimplexSolver.Solve()` method that takes `minDeltaTime` instead.
    * Renamed `BoxCollider.ConvexRadius` to `BevelRadius`.
    * Renamed `CylinderCollider.ConvexRadius` to `BevelRadius`.
    * Deprecated `SimplexSolver.c_SimplexSolverEpsilon`.
    * Deprecated the following methods in `ComponentExtensions` taking an `Entity` as the first argument.
        * `GetCollisionFilter()`
        * `GetMass()`
        * `GetEffectiveMass()`
        * `GetCenterOfMass()`
        * `GetPosition()`
        * `GetRotation()`
        * `GetVelocities()`
        * `SetVelocities()`
        * `GetLinearVelocity()`
        * `SetLinearVelocity()`
        * `GetAngularVelocity()`
        * `SetAngularVelocity()`
        * `ApplyImpulse()`
        * `ApplyLinearImpulse()`
        * `ApplyAngularImpulse()`
    * Removed the following expired members:
        * `ColliderCastInput.Direction`
        * `ColliderCastInput.Position`
        * `Ray(float3, float3)`
        * `Ray.Direction`
        * `Ray.ReciprocalDirection`
        * `RaycastInput.Direction`
        * `RaycastInput.Position`
* Authoring/Conversion API
    * Renamed `PhysicsBody` to `PhysicsBodyAuthoring`.
    * Renamed `PhysicsShape` to `PhysicsShapeAuthoring`.
    * `PhysicsShapeAuthoring.BevelRadius` now returns the serialized bevel radius data in all cases, instead of returning the shape radius when the type was either sphere or capsule, or a value of 0 for meshes. Its setter now only clamps the value if the shape type is box or cylinder.
    * `PhysicsShapeAuthoring.GetBoxProperties()` now returns `BoxGeometry`. The signature containing out parameters has been deprecated.
    * `PhysicsShapeAuthoring.SetBox()` now uses `BoxGeometry`. The signature passing individual parameters has been deprecated.
    * `PhysicsShapeAuthoring.GetCapsuleProperties()` now returns `CapsuleGeometry`. The signature containing out parameters has been deprecated.
    * `PhysicsShapeAuthoring.SetCapsule()` now uses `CapsuleGeometry`. The signature passing individual parameters has been deprecated.
    * `PhysicsShapeAuthoring.GetCylinderGeometry()` now returns `CylinderGeometry`. The signature containing out parameters has been deprecated.
    * `PhysicsShapeAuthoring.SetCylinder()` now uses `CylinderGeometry`. The signature passing individual parameters has been deprecated.
    * `PhysicsShapeAuthoring.GetSphereProperties()` now returns `SphereGeometry`. The signature containing out parameters has been deprecated.
    * `PhysicsShapeAuthoring.SetSphere()` now uses `SphereGeometry`. The signature passing individual parameters has been deprecated.
    * `PhysicsShapeAuthoring.SetConvexHull()` now uses `ConvexHullGenerationParameters`. The old signature has been deprecated.
    * `PhysicsShapeAuthoring.ConvexRadius` has been deprecated. Instead use `BevelRadius` values returned by geometry structs.
    * `PhysicsShapeAuthoring.GetConvexHullProperties()` now returns points from `SkinnedMeshRenderer` components that are bound to the shape's transform, or to the transforms of its children that are not children of some other shape, when no custom mesh has been assigned.
    * Added `PhysicsShapeAuthoring.SetConvexHull()` signature specifying minimum skinned vertex weight for inclusion.
    * Added `PhysicsShapeAuthoring.ConvexHullGenerationParameters` property.
    * `PhysicsShapeAuthoring.GetMesh()` has been deprecated.
    * Added `PhysicsShapeAuthoring.GetMeshProperties()`. When no custom mesh has been assigned, this will return mesh data from all render geometry in the shape's hierarchy, that are not children of some other shape.
    * `PhysicsShapeAuthoring.FitToEnabledRenderMeshes()` now takes an optional parameter for specifying minimum skinned vertex weight for inclusion.
    * Removed the `OutputStream` field from various deprecated debug drawing jobs. These will be redesigned in a future release, and you are advised to not try to extend them yet.
    * Removed the following expired members:
        * `PhysicsShapeAuthoring.FitToGeometry()`.
        * `PhysicsShapeAuthoring.GetCapsuleProperties()` returning raw points.
        * `PhysicsShapeAuthoring.GetPlaneProperties()` returning raw points.
        * `FirstPassLegacyRigidbodyConversionSystem`.
        * `FirstPassPhysicsBodyConversionSystem`.
        * `SecondPassLegacyRigidbodyConversionSystem`.
        * `SecondPassPhysicsBodyConversionSystem`.
* Run-Time Behavior
    * `BoxCollider.Create()` is now compatible with Burst.
    * `CapsuleCollider.Create()` is now compatible with Burst.
    * `ConvexCollider.Create()` is now compatible with Burst.
    * `CylinderCollider.Create()` is now compatible with Burst.
    * `MeshCollider.Create()` is now compatible with Burst.
    * `SphereCollider.Create()` is now compatible with Burst.
    * `TerrainCollider.Create()` is now compatible with Burst.
* Authoring/Conversion Behavior
    * Converting mesh and convex shapes is now several orders of magnitude faster.
    * Convex meshes are more accurate and less prone to jitter.
    * `PhysicsShapeAuthoring` components set to convex now display a wire frame preview at edit time.
    * `PhysicsShapeAuthoring` components set to cylinder can now specify how many sides the generated hull should have.
    * Inspector controls for physics categories, custom material tags, and custom body tags now have a final option to select and edit the corresponding naming asset.

### Fixes

* Body hierarchies with multiple shape types (e.g., classic collider types and `PhysicsShapeAuthoring`) now produce a single flat `CompoundCollider` tree, instead of a tree with several `CompoundCollider` leaves.
* Fixed issues causing dynamic objects to tunnel through thin static objects (most likely meshes)
* Fixed incorrect behavior of Constraint.Twist() with limitedAxis != 0
* Fixed regression introduced in 0.2.0 causing box shapes on uniformly scaled objects to always convert into a box with size 1 on all sides.
* Fixed exception when calling `Dispose()` on an uninitialized `CollisionWorld`.

### Known Issues

* Wire frame previews for convex `PhysicsShapeAuthoring` components can take a while to generate.
* Wire frame previews for convex `PhysicsShapeAuthoring` components do not currently illustrate effects of bevel radius in the same way as primitives.
* The first time you convert convex or mesh shapes in the Editor after a domain reload, you will notice a delay while the conversion jobs are Burst compiled. all subsequent conversions should be significantly faster until the next domain reload.
* Updated dependency on `com.unity.burst` to version `1.1.2`.



## [0.2.0-preview] - 2019-07-18

### Upgrade guide

* If you created per-body custom data using `PhysicsShape.CustomFlags` then you should instead do it using `PhysicsBody.CustomTags`.
* Some public API points were _removed_, either because they were not intended to be public or because they introduce other problems (see Changes below).
    * Some of these API points may later be reintroduced on a case-by-case basis to enable customization for advanced use cases. Please provide feedback on the forums if these removals have affected current use cases so we can prioritize them.
* Some public API points were _replaced_ with another one in a way that cannot be handled by the script updater, so they must be manually fixed in your own code (see Changes below).
* All public types in test assemblies were never intended to be public and have been made internal.
* Some properties on `PhysicsMaterialTemplate` and `PhysicsShape` now return `PhysicsCategoryTags` instead of `int`. Use its `Value` property if you need to get/set a raw `int` value (see Changes below).

### Changes

* Run-Time API
    * Added first draft of a new `TerrainCollider` struct. Terrain geometry is defined by a height field. It requires less memory than an equivalent mesh and is faster to query. It also offers a fast, low-quality option for collision detection.
        * Added `ColliderType.Terrain`.
        * Added `CollisionType.Terrain`.
        * Added `DistanceQueries.ConvexTerrain<T>()`.
        * Added `DistanceQueries.PointTerrain<T>()`.
    * Shapes and bodies how have their own separate custom data.
        * Added `Material.CustomTags` (for shapes).
        * Replaced `PhysicsCustomData` with `PhysicsCustomTags` (for bodies).
        * Replaced `ContactHeader.BodyCustomDatas` with `ContactHeader.BodyCustomTags`.
        * Replaced `CustomDataPair` with `CustomTagsPair`.
        * Replaced `RigidBody.CustomData` with `RigidBody.CustomTags`.
    * Removed coefficient of restitution concept from Jacobians. All restitution calculations are approximated and applied before the solve, so restitution changes at this point in the simulation have no effect.
        * `ModifiableContactJacobian.CoefficientOfRestitution` is now obsolete.
    * `ModifiableContactJacobian.FrictionEffectiveMassOffDiag` is now obsolete. It was not possible to make any meaningful changes to it without fully understanding friction solving internals.
    * Removed max impulse concept from Jacobians. Solver design implies impulses are pretty unpredictable, making it difficult to choose maximum impulse value in practice.
        * `JacobianFlags.EnableMaxImpulse` is now obsolete.
            * Underlying values of `JacobianFlags` have been changed.
            * Added `JacobianFlags.UserFlag2`.
        * `Material.EnableMaxImpulse` is now obsolete.
        * `MaterialFlags.EnableMaxImpulse` is now obsolete.
        * `ModifiableJacobianHeader.HasMaxImpulse` is now obsolete.
        * `ModifiableJacobianHeader.MaxImpulse` is now obsolete.
    * Removed the following members:
        * `CollisionFilter.CategoryBits`
        * `CollisionFilter.MaskBits`
    * Removed the following types from public API and made them internal:
        * `AngularLimit1DJacobian`
        * `AngularLimit2DJacobian`
        * `AngularLimit3DJacobian`
        * `BaseContactJacobian`
        * `BoundingVolumeHierarchy.BuildBranchesJob`
        * `BoundingVolumeHierarchy.BuildFirstNLevelsJob`
        * `BoundingVolumeHierarchy.FinalizeTreeJob`
        * `Broadphase`
        * `ColliderCastQueries`
        * `CollisionEvent`
        * `CollisionEvents`
        * `ContactHeader`
        * `ContactJacobian`
        * `ConvexConvexDistanceQueries`
        * `ConvexHull`
        * `ConvexHullBuilder`
        * `ConvexHullBuilderExtensions`
        * `DistanceQueries`
        * `ElementPool<T>` (Unity.Collections)
        * `Integrator`
        * `IPoolElement` (Unity.Collections)
        * `JacobianHeader`
        * `JacobianIterator`
        * `JacobianUtilities`
        * `LinearLimitJacobian`
        * `ManifoldQueries`
        * `Mesh`
        * `MotionExpansion`
        * `NarrowPhase`
        * `OverlapQueries`
        * `QueryWrappers`
        * `RaycastQueries`
        * `Scheduler`
        * `Simulation.Context`
        * `Solver`
        * `StaticLayerChangeInfo`
        * `TriggerEvent`
        * `TriggerEvents`
        * `TriggerJacobian`
    * Removed the following members from public API and made them internal:
        * Aabb.CreateFromPoints(float3x4)
        * `BoundingVolumeHierarchy.BuildBranch()`
        * `BoundingVolumeHierarchy.BuildCombinedCollisionFilter()`
        * `BoundingVolumeHierarchy.BuildFirstNLevels()`
        * `BoundingVolumeHierarchy.CheckIntegrity()`
        * All explicit `ChildCollider` constructors
        * `ColliderKeyPath(ColliderKey, uint)`
        * `ColliderKeyPath.Empty`
        * `ColliderKeyPath.GetLeafKey()`
        * `ColliderKeyPath.PopChildKey()`
        * `ColliderKeyPath.PushChildKey()`
        * `CollisionWorld.Broadphase`
        * `CompoundCollider.BoundingVolumeHierarchy`
        * `Constraint.ConstrainedAxis1D`
        * `Constraint.Dimension`
        * `Constraint.FreeAxis2D`
        * `ConvexCollider.ConvexHull`
        * `MeshCollider.Mesh`
        * `MotionVelocity.CalculateExpansion()`
        * `SimulationCallbacks.Any()`
        * `SimulationCallbacks.Execute()`
    * `ChildCollider.TransformFromChild` is now a readonly property instead of a field.
    * Removed `BuildPhysicsWorld.m_StaticLayerChangeInfo`.
    * Added `FourTranposedAabbs.DistanceFromPointSquared()` signatures passing scale parameter.
    * Changed `ISimulation` interface (i.e. `Simulation` class).
        * Added `ISimulation.FinalJobJandle`.
        * Added `ISimulation.FinalSimulationJobJandle`.
        * Added simpler `ISimulation.ScheduleStepJobs()` signature and marked previous one obsolete.
    * Added `RigidBody.HasCollider`.
    * `SimplexSolver.Solve()` now takes an optional bool to specify whether it should respect minimum delta time.
    * `SurfaceVelocity.ExtraFrictionDv` has been removed and replaced with more usable `LinearVelocity` and `AngularVelocity` members.
* Authoring/Conversion API
    * Added `CustomBodyTagNames`.
    * Renamed `CustomMaterialTagNames.FlagNames` to `CustomMaterialTagNames.TagNames`.
    * Renamed `CustomFlagNames` to `CustomPhysicsMaterialTagNames`.
    * Renamed `CustomPhysicsMaterialTagNames.FlagNames` to `CustomPhysicsMaterialTagNames.TagNames`.
    * Added `PhysicsCategoryTags`, `CustomBodyTags`, and `CustomMaterialTags` authoring structs.
    * The following properties now return `PhysicsCategoryTags` instead of `int`:
        * `PhysicsMaterialTemplate.BelongsTo`
        * `PhysicsMaterialTemplate.CollidesWith`
        * `PhysicsShape.BelongsTo`
        * `PhysicsShape.CollidesWith`
    * The following members on `PhysicsMaterialTemplate` are now obsolete:
        * `GetBelongsTo()`
        * `SetBelongsTo()`
        * `GetCollidesWith()`
        * `SetCollidesWith()`
    * The following members on `PhysicsShape` are now obsolete:
        * `GetBelongsTo()`
        * `SetBelongsTo()`
        * `GetCollidesWith()`
        * `SetCollidesWith()`
    * Added `PhysicsMaterialTemplate.CustomTags`.
        * `CustomFlags`, `GetCustomFlag()` and `SetCustomFlag()` are now obsolete.
    * Added `PhysicsShape.CustomTags`.
        * `CustomFlags`, `GetCustomFlag()` and `SetCustomFlag()` are now obsolete.
    * Added `PhysicsBody.CustomTags`.
    * Renamed `PhysicsShape.OverrideCustomFlags` to `PhysicsShape.OverrideCustomTags`.
    * Renamed `PhysicsShape.CustomFlags` to `PhysicsShape.CustomTags`.
    * Renamed `PhysicsShape.GetCustomFlag()` to `PhysicsShape.GetCustomTag()`.
    * Renamed `PhysicsShape.SetCustomFlag()` to `PhysicsShape.SetCustomTag()`.
    * Overload of `PhysicsShape.GetCapsuleProperties()` is now obsolete.
    * Overload of `PhysicsShape.GetPlaneProperties()` is now obsolete.
    * Removed `PhysicsBody.m_OverrideDefaultMassDistribution` (backing field never intended to be public).
    * `PhysicsShape.GetBoxProperties()` now returns underlying serialized data instead of reorienting/resizing when aligned to local axes.
    * `BaseShapeConversionSystem.GetCustomFlags()` is now obsolete.
    * Parameterless constructors have been made private for the following types because they should not be used (use instead ScriptableObjectCreateInstance<T>() or GameObject.AddComponent<T>() as applicable):
        * `CustomPhysicsMaterialTagNames`
        * `PhysicsCategoryNames`
        * `PhysicsMaterialTemplate`
        * `PhysicsBody`
        * `PhysicsShape`
    * The following types have been made sealed:
        * `LegacyBoxColliderConversionSystem`
        * `LegacyCapsuleColliderConversionSystem`
        * `LegacySphereColliderConversionSystem`
        * `LegacyMeshColliderConversionSystem`
        * `LegacyRigidbodyConversionSystem`
        * `PhysicsBodyConversionSystem`
        * `PhysicsShapeConversionSystem`
    * `DisplayContactsJob` has been deprecated in favor of protected `DisplayContactsSystem.DisplayContactsJob`.
    * `FinishDisplayContactsJob` has been deprecated in favor of protected `DisplayContactsSystem.FinishDisplayContactsJob`.
    * `DisplayJointsJob` has been deprecated in favor of protected `DisplayJointsSystem.DisplayJointsJob`.
    * `FinishDisplayTriggerEventsJob` has been deprecated in favor of protected `DisplayTriggerEventsSystem.FinishDisplayTriggerEventsJob`.
    * The following types have been deprecated and will be made internal in a future release:
        * `DisplayBodyColliders.DrawComponent`
        * `DisplayCollisionEventsSystem.FinishDisplayCollisionEventsJob`
* Run-Time Behavior
    * Collision events between infinite mass bodies (kinematic-kinematic and kinematic-static) are now raised. The reported impulse will be 0.
    * The default value of `Unity.Physics.PhysicsStep.ThreadCountHint` has been increased from 4 to 8.
    * `EndFramePhysicsSystem` no longer waits for `HandlesToWaitFor`, instead it produces a `FinalJobHandle` which is a combination of those jobs plus the built-in physics jobs. Subsequent systems that depend on all physics jobs being complete can use that as an input dependency.
* Authoring/Conversion Behavior
    * `PhysicsCustomData` is now converted from `PhysicsBody.CustomTags` instead of using the flags common to all leaf shapes.
    * `PhysicsShape.CustomTags` is now converted into `Material.CustomTags`.
    * If a shape conversion system throws an exception when producing a `PhysicsCollider`, then it is simply skipped and logs an error message, instead of causing the entire conversion process to fail.
    * `PhysicsShape` Inspector now displays suggestions of alternative primitive shape types if a simpler shape would yield the same collision result as the current configuration.
    * `PhysicsShape` Inspector now displays error messages if a custom mesh or discovered mesh is not compatible with run-time conversion.

### Fixes

* Draw Collider Edges now supports spheres, capsules, cylinders and compound colliders.
* Fixed bug causing editor to get caught in infinite loop when adding `PhysicsShape` component to a GameObject with deactivated children with `MeshFilter` components.
* Fixed bug resulting in the creation of `PhysicMaterial` objects in sub-scenes when converting legacy colliders.
* Fixed bug when scaling down friction on bounce in Jacobian building. Once a body was declared to bounce (apply restitution), all subsequent body Jacobians had their friction scaled down to 25%.
* Fixed bug resulting in the broadphase for static bodies possibly not being updated when adding or moving a static body, causing queries and collisions to miss.
* Fixed bug with restitution impulse during penetration recovery being too big due to wrong units used.
* Fixed bug with energy gain coming from restitution impulse with high restitution values.
* Fixed Jacobian structures being allocated at non 4 byte aligned addresses, which caused crashes on Android
* Removed Solver & Scheduler asserts from Joints between two static bodies #383.
* Fixed bug preventing the conversion of classic `BoxCollider` components with small sizes.
* Fixed bug where `PhysicsShape.ConvexRadius` setter was clamping already serialized value instead of newly assigned value.
* Fixed bug where `PhysicsShape` orientation, size, and convex radius values might undergo changes during conversion resulting in identical volumes; only objects inheriting non-uniform scale now exhibit this behavior.
* Fixed bug causing minor drawing error for cylinder `PhysicsShape` with non-zero convex radius.
* Fixed crash when trying to run-time convert a `MeshCollider` or `PhysicsShape` with a non-readable mesh assigned. Conversion system now logs an exception instead.
* Fixed crash when constructing a `MeshCollider` with no input triangles. A valid (empty) mesh is still created.
* Fixed bugs building to IL2CPP resulting in unresolved external symbols in `BvhLeafProcessor`, `ConvexCompoundLeafProcessor`, and `ConvexMeshLeafProcessor`.
* Fixed bug causing physics IJob's to not be burst compiled (ICollisionEventsJob, ITriggerEventsJob, IBodyPairsJob, IContactsJob, IJacobiansJob)



## [0.1.0-preview] - 2019-05-31

### Upgrade guide

* Any run-time code that traversed the transform hierarchy of physics objects to find other entities must instead store references to entity IDs of interest through a different mechanism.
* Baking of non-uniform scale for parametric `PhysicsShape` types has changed to more predictably approximate skewed render meshes. Double check the results on any non-uniformly scaled objects in your projects.
* Angular Velocity is currently set in Motion Space. The Motion Space orientation of dynamic bodies may have changed with the changes to the conversion pipeline. If dynamic bodies are now rotating differently, check the values of `Initial Angular Velocity` in the `PhysicsBody` component, or values set to `Angular` in the `PhysicsVelocity` component.
* Convex `PhysicsShape` objects with no custom mesh assigned now include points from enabled mesh renderers on their children. Double check any convex objects in your projects.
* The `RaycastInput` and `ColliderCastInput` structs have changed. See below for details.

### Changes

* Run-Time API
    * Renamed `CollisionFilter.CategoryBits` to `CollisionFilter.BelongsTo`.
    * Renamed `CollisionFilter.MaskBits` to `CollisionFilter.CollidesWith`.
    * `RaycastInput` and `ColliderCastInput` have changed:
        * At the input level, start and end positions are now specified rather then an initial position and a displacement.
        * `Start` replaces `Position`.
        * `End` replaces `Direction` which had been confusing as it was actually a displacement vector to a second point on the ray.
        * `Ray` has been made internal as a lower level interface and updated to be less ambiguous.
    * Added job interfaces for easier reading of simulation events, instead of having to work directly with block streams.
        * `ICollisionEventsJob` calls `Execute()` for every collision event produced by the solver.
        * `ITriggerEventsJob` calls `Execute()` for every trigger event produced by the solver.
        * These events now also include the Entity's involved, not just the rigid body indices.
    * Added job interfaces for easier modification of simulation data, instead of having to work directly with block streams.
        * `IBodyPairsJob` calls `Execute()` for every body pair produced by the broad phase.
        * `IContactsJob` calls `Execute()` for every contact manifold produced by the narrowphase.
        * `IJacobiansJob` calls `Execute()` for every contact jacobian before it is solved.
* Authoring/Conversion API
    * Renamed `SecondPassLegacyRigidbodyConversionSystem` to `LegacyRigidbodyConversionSystem`.
    * Deprecated `FirstPassPhysicsBodyConversionSystem`.
    * Renamed `SecondPassPhysicsBodyConversionSystem` to `PhysicsBodyConversionSystem`.
    * Deprecated `FirstPassLegacyRigidbodyConversionSystem`.
    * Deprecated overload of `PhysicsShape.GetCapsuleProperties()` returning raw points.
    * Deprecated overload of `PhysicsShape.GetPlaneProperties()` returning raw points.
    * Renamed `PhysicsShape.FitToGeometry()` to `PhysicsShape.FitToEnabledRenderMeshes()`. New method accounts for enabled `MeshRenderer` components on child objects with the same physics body and leaf shape.
    * `PhysicsShape.GetConvexHullProperties()` now includes points from enabled `MeshRenderer` components on child objects with the same physics body and leaf shape when no custom mesh has been assigned.
* Run-Time Behavior
    * Added a fixed angle constraint to prismatic joints so they no longer rotate freely.
    * Any Entity with a `PhysicsVelocity` component will be added to the `PhysicsWorld` and integrated, even if it has no `PhysicsCollider`.
* Authoring/Conversion Behavior
    * Physics data on deactivated objects in the hierarchy are no longer converted.
    * All physics objects (i.e. bodies) are now un-parented (required since all simulation happens in world space).
    * Legacy `Collider` components are no longer converted if they are disabled.
    * Converted `PhysicsShape` objects with non-uniform scale now bake more predictable results when their `Transform` is skewed.
    * All menu paths for custom assets and authoring components have changed from 'DOTS Physics' to 'DOTS/Physics'.

### Fixes

* Fixed trigger events not being raised between kinematic-vs-kinematic or kinematic-vs-static body pairs.
* Fixed crash in BuildPhysicsWorld when creating a dynamic body without a `PhysicsMass` component
* Cylinder/sphere GameObjects no longer appear in first frame when draw colliders is enabled on `PhysicsDebugDisplay`.
* Fix bugs in `BoundingVolumeHierarchy.Build` which now produces `BoundingVolumeHierarchy` of greater quality. This will affect performance of BVH queries, and most notably it will significantly improve performance of `DynamicVsDynamicFindOverlappingPairsJob`
* Fixed incorrect 3DOF angular constraint solving with non-identity joint orientation
* Fixed bug where converted `SphereCollider` would apply incorrect center offset when in a hierarchy with non-uniform scale.
* Fixed bug where converted `PhysicsShape` would not become part of a compound collider at run-time if the first `PhysicsBody` ancestor found higher up the hierarchy was disabled.
* Fixed bug where leaves of compound shapes in a hierarchy might be added to the wrong entity if it had disabled `UnityEngine.Collider` components.
* Fixed bug causing leaves of compound shapes on prefabs to convert into individual static colliders.
* Fixed restitution response to be more bouncy for convex objects with high restitution values
* Fixed bug where a converted GameObject in a hierarchy would have the wrong translation and rotation at run-time.
* Fixed bug where objects with `StaticOptimizeEntity` would not be converted into physics world.
* Preview for Mesh `PhysicsShape` no longer culls back faces.
* Inspector control for convex radius on `PhysisShape` now appears when shape type is convex hull.

### Known Issues

* Attempting to fit a non-uniformly scaled `PhysicsShape` to its render meshes may produce unexpected results.
* Physics objects loaded from sub-scenes may have the wrong transformations applied on Android.
* Dragging a control label to modify the orientation of a `PhysicsShape` sometimes produces small changes in its size.
* If gizmos are enabled in the Game tab, the 'Physics Debug Display' viewers are incorrectly rendered. Debug viewers render correctly in the Scene tab.



## [0.0.2-preview] - 2019-04-08

### Upgrade guide

* Any assembly definitions referencing `Unity.Physics.Authoring` assembly by name must be updated to instead reference `Unity.Physics.Hybrid`.

### Changes

* Renamed `Unity.Physics.Authoring` assembly to `Unity.Physics.Hybrid`. (All of its types still exist in the `Unity.Physics.Authoring` namespace.)
* Radius of cylinder `PhysicsShape` is no longer non-uniformly scaled when converted.

### Fixes

* Fixed exception when converting a box `PhysicsShape` with negative scale.
* Fixed incorrect orientation when fitting capsule, cylinder, or plane `PhysicsShape` to render mesh.
* Fixed convex radius being too large when switching `PhysicsShape` from plane to box or cylinder.
* Fixed calculation of minimum half-angle between faces in convex hulls.
* Fixed collision/trigger event iterators skipping some events when iterating.
* Fixed memory leak when enabling "Draw colliders" in the Physics Debug Display.

### Known Issues

* Physics systems are tied to (variable) rendering frame rate when using automatic world bootstrapping. See `FixedTimestep` examples in ECS Samples project for currently available approaches.
* IL2CPP player targets have not yet been fully validated.
* Some tests might occasionally fail due to JobTempAlloc memory leak warnings.
* Swapping `PhysicsShape` component between different shape types may produce non-intuitive results when nested in hierarchies with non-uniform scale.
* Some `PhysicsShape` configurations do not bake properly when nested in hierarchies with non-uniform scale.
* `PhysicsShape` does not yet visualize convex hull shapes at edit-time.
* Drag values on classic `Rigidbody` components are not currently converted correctly.



## [0.0.1-preview] - 2019-03-12

* Initial package version
