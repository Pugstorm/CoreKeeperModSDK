# Changelog


## [1.0.15] - 2023-07-27

### Changed

* Updated com.unity.entities dependency to 1.0.14
* Use of non required TempJob allocation and use Allocator.Temp instead.

### Fixed

* Runtime EntityQuery leaks and reduce runtime memory pressure due to continuously allocating queries without disposing.
* Reduced memory usage in Editor tests, by avoiding allocating queries continuously in hot paths.


## [1.0.12] - 2023-06-19

### Changed
* Updated com.unity.entities dependency to 1.0.11

### Fixed
* `MultiplayerPlayModeWindow > Dump Packet Logs` now works more reliably, now works with NUnit tests, and dump files are named with more context.
* Fixed bug in `GhostSendSystem` that caused it to not replicate ghosts when enabling packet dumps. `GhostValuesAreSerialized_WithPacketDumpsEnabled` test added.


## [1.0.11] - 2023-06-02

### Fixed

* Updated logging dependency


## [1.0.10] - 2023-05-23

### Added

* What's New and Upgrade Guide section in the docs.
* New NetworkRequestListenResult cleanup component, that can be used to track the result of a listen request.

### Changed

* documentation index page with up-to-date info and links.
* Removed forcing local client/server to alway use the loopback address to connect.
* It is now possible to listen to the NetworkEndPoint.Any also for IPC connection.
* The NetworkStreamDriver.GetRemoteAddress always return a consistent address for the connection when the NetworkDriver is configured to use the Unity Relay. Before, an invalid address was returned after the connection has been established, that was incorrect.
* Exposed all the internal state of the NetworkTimeSystem as public API

### Fixed

* exceptions when NetworkRequestListen and/or. NetworkRequestConnect are handled and proper handling of multiple (erroneous) requests presents.
* A problem with InterpolatedTick, going back and not recovering correctly in presence of large application, either the server or the client, stalls (i.e after loading).
* `MultiplayerPlayModeWindow > Dump Packet Logs` now works more reliably, now works with NUnit tests, and dump files are named with more context.
* Fixed bug in `GhostSendSystem` that caused it to not replicate ghosts when enabling packet dumps. `GhostValuesAreSerialized_WithPacketDumpsEnabled` test added.


## [1.0.8] - 2023-04-17

### Changed

* Reduced the amount of memory allocated by allocating based on the maximum number of worker threads the running platform requires rather than defaulting to using a theoretical upper-bound of 128 worker threads.
* Removed the additional entity created for each predicted ghost prefab, that was necessary to support predicted spawning. This has the addition benefit to cut almost in half (in case all ghost prefabs support all modes) the number of required archetypes.


### Fixed

* An issue with pre-spawned ghost not working correctly because sub-scene hash is calculated differently for client and server
* an issue when sub-scene are opened for live-conversion and baking, causing spawned ghosts to contains invalid blob asset references (i.e colliders), introducing potential crashes and other problems (i.e missing collision and mis-prediction)
* An issue with baking, not using the correct NetCodeClientTarget (either client or client/server) when baking a sub-scene for a client standalone build.
* An issue with the Entities/Build project settings UI that was not updating the ClientTarget to use is the ProjectSettings window was not closed, or another settings "tab" was selected.
* An issue with HasServerWorld reporting the presence of a server world even though no server was created.if it's not needed.
* A sporadic InvalidOperationException: GetSingleton<Unity.NetCode.LowLevel.SnapshotDataLookupCache>() thrown when retrieving the Unity.NetCode.LowLevel.SnapshotDataLookupCache.
* GhostCollectionSystem InvalidOperationException thrown when Ghost prefab validation fails, trying accessing invalidated DynamicBuffer.
* An issue in the GhostChunkSerializer, that was overwriting the snapshot data with some enable bits masks.
* An issue in the GhostUpdateSystem, that was reading and applying the wrong enable bits.
* An issue when restoring enable bits state from the predicted ghost history buffer.
* Fixed a "System Creation Order" bug causing components with `[GhostField]` fields (or the `[GhostEnableBit]` attribute) to silently default to the `DontSerializeVariant`, especially in cases where Ghost Prefabs are created at runtime (via `GhostPrefabCreation.ConvertToGhostPrefab`).
  * "Ghost Registration" and "Default Variant Registration" Systems now use `[CreateBefore(typeof(DefaultVariantSystemGroup))]`, so that user-code can add `[CreateAfter(typeof(DefaultVariantSystemGroup))]` when accessing `GhostComponentSerializerCollectionData` data.
  * We now also guard all of these calls, giving explicit (fatal) errors if used improperly.
* An issue in `GhostDistancePartitioningSystem`, which caused Netcode to add a shared component ECB entry for every single ghost containing a `LocalTransform`, every single frame, when `GhostDistanceImportance` was enabled in a users project.


### Deprecated

* Now that the `GhostAuthoringInspectionComponent` shows all replicated components, you shouldn't have to opt-into prefab overrides. Thus, deprecated the `SupportsPrefabOverrides` attribute.


## [1.0.0-pre.66] - 2023-03-21

### Added

* Validate and sanitise connect and listen addresses when using IPCNetworkInterface. That was causing some nasty crash in the Transport without users understanding the actual problem.

### Changed

* The following components have been renamed:
NetworkSnapshotAckComponent: NetworkSnapshotAck,
IncomingSnapshotDataStreamBufferComponent: IncomingSnapshotDataStreamBuffer,
IncomingRpcDataStreamBufferComponent: IncomingRpcDataStreamBuffer,
OutgoingRpcDataStreamBufferComponent: OutgoingRpcDataStreamBuffer,
IncomingCommandDataStreamBufferComponent: IncomingCommandDataStreamBuffer,
OutgoingCommandDataStreamBufferComponent: OutgoingCommandDataStreamBuffer,
NetworkIdComponent: NetworkId,
CommandTargetComponent: CommandTarget,
GhostComponent: GhostInstance,
GhostChildEntityComponent: GhostChildEntity,
GhostOwnerComponent: GhostOwner,
PredictedGhostComponent: PredictedGhost,
GhostTypeComponent: GhostType,
SharedGhostTypeComponent: GhostTypePartition,
GhostCleanupComponent: GhostCleanup,
GhostPrefabMetaDataComponent: GhostPrefabMetaData,
PredictedGhostSpawnRequestComponent: PredictedGhostSpawnRequest,
PendingSpawnPlaceholderComponent: PendingSpawnPlaceholder,
ReceiveRpcCommandRequestComponent: ReceiveRpcCommandRequest,
SendRpcCommandRequestComponent: SendRpcCommandRequest,
MetricsMonitorComponent: MetricsMonitor,

### Removed

* internal ListenAsync/ConnectAsync methods (no visible API changes for users)

### Fixed

* a very unfrequent exception thrown in presence of a ghost with a replicated component that does not present any prediction errors names (i.e an Entity reference).
* source generator crash when logging missing assembly dependency.
* source generator requiring Unity.Transport package dependency for generating serialization code.
* Snapshot history buffer not restore correctly, causing entities component to be stomped with random data.
* Fixed an issue when ClientServerBootstrap.AutoConnectPort is 0 indicating autoconnecting should be disabled and you will connect manually via the driver connect API, but the playmode tools ip/port fields would still triggering (so you get two connections set up and errors). We also now prevent attempts to make a connection while one is already established.
* an issue with source generator, validating incorrectly custom templates that uses overrides.
* removed warning for old temp allocation when converting sub-scene with pre-spawned ghosts.
* Forced all `ICommandData`'s `InternalBufferCapacity` to be zero, because we were constantly wasting hundreds of bytes per entity to store data that we know for certain will not fit into the internal capacity (as the dynamic buffer required length is hardcoded to 64, for netcode input buffers).
* Fixed potential crash in players when send queue is full
* Fixed exceptions when trying to use invalid interpolation ticks (could happen during snapshot updates or in predicted spawning system on disconnection)


## [1.0.0-pre.44] - 2023-02-13

### Added

* Validation checks to the `GhostDistanceData` `TileSize` to prevent invalid tile assignment or DivideByZeroException.
* Added a HelpURL (linking to docs) for `DisableAutomaticPrespawnSectionReportingAuthoring`, `GhostAuthoringComponent`, `GhostAuthoringInspectionComponent`, `DefaultSmoothingActionUserParamsAuthoring`, `GhostPresentationGameObjectAuthoring`, `NetCodeDebugConfigAuthoring`, `GhostAnimationController`, `GhostPresentationGameObjectEntityOwner`, and `NetCodePhysicsConfig`.
* New GetLocalEndPoint API to NetworkStreamDriver

### Changed

* Make EnablePacketLogging component public to allow for per connection debug information.
* Updated `com.unity.transport` dependency to version 2.0.0-pre.6.

### Deprecated
* `ProjectSettings / NetCodeClientTarget` was not actually saved to the ProjectSettings. Instead, it was saved to `EditorPrefs`, breaking build determinism across machines. Now that this has been fixed, your EditorPref has been clobbered, and `ClientSettings.NetCodeClientTarget` has been deprecated (in favour of `NetCodeClientSettings.instance.ClientTarget`).

### Fixed

* An issue with the `NetworkEmulator` in the editor when enabling domain reload (while switching play-mode) that was causing the game to forcibly immediately exit the the play state.
* An issue with pre-spawned ghost baking when the baked entity has not LocalTransform (position/rotation for transform v1) component.
* "Ghost Distance Importance Scaling" is now working again. Ensure you read the updated documentation.
* Missing field write in `NetworkStreamListenSystem.OnCreate`, fixing Relay servers.
* Code-Generated Burst-compiled Serializer methods will now only compile inside worlds with `WorldFlag.GameClient` and `WorldFlag.GameServer` WorldFlags. This improves exit play-mode speeds (when Domain Reload is enabled), baking (in all cases), and recompilation speeds.
* Fixed an issue where multiple ghost types with the same archetype but difference data could sometime trigger errors about ghosts changing type.
* Fix a mistake where the relay sample will create a client driver rather than a server driver
* Fix logic for relay set up on the client. Making sure when calling DefaultDriverConstructor.RegisterClientDriver with relay settings that we skip this unless, requested playtype is client or clientandserver (if no server is found), the simulator is enabled, or on a client only build.
* Fixed `ArgumentException: ArchetypeChunk.GetDynamicComponentDataArrayReinterpret<System.Byte> cannot be called on zero-sized IComponentData` in `GhostPredictionHistorySystem.PredictionBackupJob`. Added comprehensive test coverage for the `GhostPredictionHistorySystem` (via adding a predicted ghost version of the `GhostSerializationTestsForEnableableBits` tests).
* Fixed serialization of components on child entities in the case where `SentForChildEntities = true`. This fix may introduce a small performance regression in baking and netcode world initialization. Contact us with all performance related issues.
* `GhostUpdateSystem` now supports Change Filtering, so components on the client will now only be marked as changed _when they actually are changed_. We strongly recommend implementing change filtering when reading components containing `[GhostField]`s and `[GhostEnabledBit]`s on the client.
* Fixed input component codegen issue when the type is nested in a parent class


## [1.0.0-exp.8] - 2022-09-21

### Added

* Added a new unified `NetCodePhysicsConfig` to configure in one place all the netcode physics settings. LagCompensationConfig and PredictedPhysicsConfig are generated from these settings at conversion time.
* Predicted ghost physics now use multiple physics world: A predicted physics wold simulated the ghost physics and a client-only physics world can be used for effect. For more information please refer to the predicted physics documentation.
* When there is a protocol version mismatch error when connecting, the versions and hashes used for the protocol are dumped to the log so it's easier to see why the versions don't match
* added some sanity check to prevent updating invalid ghosts
* Added a new method, `GhostPrefabCreation.ConvertToGhostPrefab` which can be used to create ghost prefabs from code without having an asset for them.
* Added a support for creating multiple network drivers. It is now possible to have a server that listen to the same port using different network interfaces (ex: IPC, Socket, WebSocket at the same time).
* Hybrid assemblies will not be included in DOTS Runtime builds.
* code generation documentation
* RegisterPredictedPhysicsRuntimeSystemReadWrite and RegisterPredictedPhysicsRuntimeSystemReadOnly extension methods, for tracking dependencies when using predicted networked physics systems.
* Support for runtime editing the number of ThinClients.
* Added: A new NetworkTime component that contains all the time and tick information for the client/server simulation. Please look at the upgrade guide for more information on how to update your project.
* Support for enabledbits.
* An input interface, IInputData, has been added which can be used to automatically handle input data as networked command data. The input is automatically copied into the command buffer and retrieved from it as appropriate given the current tick. Also added an InputEvent type which can be used inside such an input component to reliably synchronize single event type things.
* Added support for running the prediction loop in batches by setting `ClientTickRate.MaxPredictionStepBatchSizeRepeatedTick` and `ClientTickRate.MaxPredictionStepBatchSizeFirstTimeTick`. The batches will be broken on input changes unless the input data that changes is marked with `[BatchPredict]`.
* Some optimisation to reduce the number of predicted tick and interpolation frames when using InProc client/server and IPC connection.
* Added a `ConnectionState` system state component which can be added to connection to track state changes, new connections and disconnects.
* Added a `NetworkStreamRequestConnect` component which can be added to a new entity to create a new connection sintead of calling `Connect`.
* Added `IsClient`, `IsServer` and `IsThinClient` helper methods to `World` and `WorldUnmanaged`.
* Dependency on Unity.Logging package.
* Ghosts are now marked-up as Ghosts in the DOTS Hierarchy (Pink = Replicated, Blue = Prefab). The built-in Unity Hierarchy has a similar markup, although limited due to API limitations.
* The GhostAuthoringComponent now uses a ghost icon.
* Update API documentation for importance scaling functions and types
* Predicted Physics API documentation
* Helper methods to DefaultDriverBuilder, these allows creation and registering IPC- and Socket drivers. On the server both are used for the editor and only socket for player build. On the client either IPC if server and client is in the same process or socket otherwise.
* A Singleton API for Ghost Metrics.
* Helper methods RegisterClientDriver and RegisterServerDriver added to DefaultDriverBuilder. These takes certificate and keys necessary to initialize a secure connection.
* Improved the `GhostAuthoringComponent` window, and moved `ComponentOverrides` to a new, optional component; `GhostAuthoringInspectionComponent`.
* Source generators now use a CancellationToken to early exit execution when cancellation is requested.
* NetworkStreamRequestListen to start listening to a new connection instead of invoking NetworkStreamDriver.Listen
* Helper methods RegisterClientDriver and RegisterServerDriver added to DefaultDriverBuilder. These takes relay server data to connect using a relay server.
* Analytics callback for ghost configuration as well as scene setup scaling.
* A default spawn classification system is will now handle client predicted spawns if the spawn isn't handled by a user system first (matches spawns of ghost types within 5 ticks of the spawn tick).
* GhostCollectionSystem optimisation when importing and processing ghost prefabs.
* A new sample that show how to backup/rollback non replicated components as part of the prediction loop.
* ChangeMaskArraySizeInBytes and SnapshotHeaderSizeInBytes utility methods
* internal extension to dynamic buffer, ElementAtRO, that let to get a readonly reference for a buffer element.

### Changed

* hybrid will tick the client and server world using the player loop instead of relying on the default world updating the client and server world via the Tick systems.
* Predicted ghost physics now use custom system to update the physics simulation. The built-in system are instead used for updating the client-only simulatiom.
* The limit of 128 components with serialization is now for actively used components instead of components in the project.
* all errors are now reporting the location and is possible to go the offending source code file / class by clicking the error in the console log.
* removed unused __GHOST_MASK_BATCH__ region from all templates
* PhysicsWorldHistory register readonly dependencies to the predicted runtime physics data when predicted physics is enabled.
* fixed crash in source generator if package cache folder contains temporary or invalid directory names.
* refactored source generators and added support for .additionalfile (2021.2+)
* Renamed `ClientServerTickRate.MaxSimulationLongStepTimeMultiplier` to `ClientServerTickRate.MaxSimulationStepBatchSize`
* `NetDebugSystem.NetDebug` has been replaced by a `NetDebug` singleton.
* `GhostSimulationSystemGroup.SpawnedGhostEntityMap` has been replaced by a `SpawnedGhostEntityMap` singleton.
* The interpolation delay is now calculated based on the perceived average snapshot ratio perceived by client, that help compensate for packet loss and jitter.
* Update to use StreamCompressionModel rather than deprecated type NetworkCompressionModel.
* Various improvements to the `Multiplayer PlayMode Tools Window`, including; simulator "profiles" (which are representative of real-world speeds), runtime thin client creation/destruction support, live modification of simulator parameters, and a tool to simulate lag spikes via shortcut key.
* The ghost relevancy map and mode has moved from the `GhostSendSystem` to a `GhostRelevancy` singleton.
* The `Connect` and `Listen` methods have moved to the `NetworkStreamDriver` singleton.
* The utility method `GhostPredictionSystemGroup.ShouldPredict` has been moved to the `PredictedGhostComponent`.
* `GhostCountOnServer` and `GhostCountOnClient` has been moved from `GhostReceiveSystem` to a singleton API `GhostCount`
* The API to register smoothing functions for prediction has moved from the `GhostPredictionSmoothingSystem` system to the `GhostPredictionSmoothing` singleton.
* The API to register RPCs and get RPC queues has moved from `RpcSystem` to the singleton `RpcCollection`
* Removed use of the obsolete AlwaysUpdateSystem attribute. The new RequireMatchingQueriesForUpdate attribute has been added where appropriate.
* Convert GhostDistancePartitioningSystem to ISystem
* GhostReceiveSystem converted to ISystem.
* Convert GhostSendSystem to ISystem. Public APIs have been moved to SingletonEntity named GhostSendSystemData
* PredictedPhysicsWorldHelper class visibility is internal.
* CommandReceiveClearSystem and CommandSendPacketSystem are not internal
* StartStreamingSceneGhosts and StopStreamingSceneGhosts to be internal RPC. If user wants to customise the prespawn scene flow, they need to add their own RPC.
* PrespawnsSceneInitialized, SubScenePrespawnBaselineResolved, PrespawnGhostBaseline,PrespawnSceneLoaded, PrespawnGhostIdRange have internal visibility.
* PrespawnSubsceneElementExtensions has internal visibility.
* LiveLinkPrespawnSectionReference are now internal. Used only in the Editor as a work around to entities conversion limitation. It should not be a public component that can be added by the user.
* Serialization code is now generated also for Component/Buffers/Commands/Rpcs that have internal visibility.
* The GhostCollectionSystem.CreatePredictedSpawnPrefab API is deprecated as clients will now automatically have predict spawned ghost prefabs set up for them. They can instantiate prefabs the normal way and don't need to call this API.
* Child entities in Ghosts now default to the `DontSerializeVariant` as serializing child ghosts is relatively expensive (due to poor 'locality of reference' of child entities in other chunks, and the random-access nature of iterating child entities). Thus, `GhostComponentAttribute.SendDataForChildEntity = false` is now the default, and you'll need to set this flag to true for all types that should be sent for children. If you'd like to replicate hierarchies, we strongly encourage you to create multiple ghost prefabs, with custom, faked transform parenting logic that keeps the hierarchy flat. Explicit child hierarchies should only be used if the snapshot updates of one hierarchy must be in sync.
* `RegisterDefaultVariants` has changed signature to now use a `Rule`. This forces users to be explicit about whether or not they want their user-defined defaults to apply to child entities too.
* You must now opt-into "Prefab Override" customization for a specific type, via either:
  **a)** Explicitly adding the `[SupportPrefabOverride]` attribute to the component.
  **b)** Explicitly adding a custom variant of a Component via `[GhostComponentVariation]`.
  **c)** Explicitly adding a default variant via `DefaultVariantSystemBase.RegisterDefaultVariant`.
  **Note:** You may also explicitly ban all overrides via the `[DontSupportPrefabOverride]` attribute.
* `GhostComponentAttribute.OwnerPredictedSendType` has been renamed to `GhostComponentAttribute.SendTypeOptimization`.
* Replaced obsolete EntityQueryBuilder APIs with current ones.
* SnapshotSizeAligned, ChangeMaskArraySizeInUInts moved to the GhostComponentSerializer class.
* DefaultUserParams has been renamed to DefaultSmoothingActionUserParams.
* DefaultTranslateSmoothingAction has been renamed to DefaultTranslationSmoothingAction.


### Removed

* The static bool `RpcSystem.DynamicAssemblyList` has been removed, replaced by a non-static property with the same name. See upgrade guide (below).
* `ClientServerBootstrap.RequestedAutoConnect` (an editor only property) has been replaced with `ClientServerBootstrap.TryFindAutoConnectEndPoint`.
* The custom client/server top-level groups `ClientSimulationSystemGroup` and similar have been removed, use `[WorldSystemFilter]` and the built-in top-level groups instead.
* `[UpdateInWorld]` has been removed, use `[WorldSystemFilter]` instead.
* `ThinClientComponent` has been removed, use `World.IsThinClient()` instead.
* PopulateList overload taking SystemBase, calls should use ref SystemState instead from ISystem. Internalized DynamicTypeList, this should not be used in user code.

### Fixed

* An issue with prediction system that calculate on client the wrong number of tick to predict when a rollback occurs and the predicting tick wraps around 0. - A sudden increment in delta time and elapsed time when the client exit from game our disconnect from the server.
* SourceGenerator errors not showing in the editor
* Ghost physics proxy rotation not synched correctly in some cases (large angles)
* A rare issue where predicted ghost entities might be spawned on a client before it had reached the correct predicted tick
* Some rare interpolation tick rollback
* restoring components and buffers from the backup didn't check the SendToOnwer settings.
* Crash on Android/iOS when using il2cpp, caused by packet logger
* OnUpdate for GhostSendSystem is now burst compiled
* Ensure unique serial number when patching up entity guids
* Ensure that we do not count zero update length in analytic results. Fix assertion error when entering and exiting playmode
* Compilation errors when the DedicatedServer platform is selected. NOTE: this does not imply the dedicated server platform is supported by the NetCode package or any other packages dependencies.

### Upgrade guide

* Prefer using the new unified `NetCodePhysicsConfig` authoring component instead of using the `LagCompensationConfig` authoring component to enable lag compensation.
* Any calls to the static `RpcSystem.DynamicAssemblyList` should be replaced with instanced calls to the property with the same name. Ensure you do so during world creation, before `RpcSystem.OnUpdate` is called.   See `SetRpcSystemDynamicAssemblyListSystem` for an example of this.
* `ClientServerTickRate.MaxSimulationLongStepTimeMultiplier` is renamed to `ClientServerTickRate.MaxSimulationStepBatchSize`.
* Any editor-only calls to `ClientServerBootstrap.RequestedAutoConnect` should be replaced with `ClientServerBootstrap.TryFindAutoConnectEndPoint`, which handles all `PlayTypes`.
* The `NetworkStreamDisconnected` component has been removed, add a `ConnectionState` component to connections you want to detect disconnects for and use a reactive system.
* When using the netcode logging system calls to `GetExistingSystem<NetDebugSystem>().NetDebug` must be replaced with `GetSingleton<NetDebug>()`, or `GetSingletonRW<NetDebug>` if you are changing the log level.
* Calls to `GetExistingSystem<GhostSimulationSystemGroup>().SpawnedGhostEntityMap` must be replaced with `GetSingleton<SpawnedGhostEntityMap>().Value`. Waiting for or setting `LastGhostMapWriter` is no longer required and should be removed.
* Calls to `GetExistingSystem<GhostSendSystem>().GhostRelevancySet` and `GetExistingSystem<GhostSendSystem>().GhostRelevancyMode` must be replaced with `GetSingletonRW<GhostRelevancy>.GhostRelevancySet` and `GetSingletonRW<GhostRelevancy>.GhostRelevancyMode`. Waiting for or setting `GhostRelevancySetWriteHandle` is no longer required and should be removed.
* Calls to `GetExistingSystem<NetworkStreamReceiveSystem>().Connect` and `GetExistingSystem<NetworkStreamReceiveSystem>().Listen` must be replaced with `GetSingletonRW<NetworkStreamDriver>.Connect` and `GetSingletonRW<NetworkStreamDriver>.Listen`.
* Usage of `ThinClientComponent` must be replaced with calls to `World.IsThinClient()`.
* The netcode specific top-level system groups and `[UpdateInWorld]` have been removed, the replacement is `[WorldSystemFilter]` and the mappings are   * `[UpdateInGroup(typeof(ClientInitializationSystemGroup))]` => `[UpdateInGroup(typeof(InitializationSystemGroup))][WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]`   * `[UpdateInGroup(typeof(ClientSimulationSystemGroup))]` => `[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]`   * `[UpdateInGroup(typeof(ClientPresentationSystemGroup))]` => `[UpdateInGroup(typeof(PresentationSystemGroup)]`   * `[UpdateInGroup(typeof(ServerInitializationSystemGroup))]` => `[UpdateInGroup(typeof(InitializationSystemGroup))][WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]`   * `[UpdateInGroup(typeof(ServerSimulationSystemGroup))]` => `[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]`   * `[UpdateInGroup(typeof(ClientAndServerInitializationSystemGroup))]` => `[UpdateInGroup(typeof(InitializationSystemGroup))][WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]`   * `[UpdateInGroup(typeof(ClientAndServerSimulationSystemGroup))]` => `[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]`   * `[UpdateInWorld(TargetWorld.Client)]` => `[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]`   * `[UpdateInWorld(TargetWorld.Server)]` => `[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]`   * `[UpdateInWorld(TargetWorld.ClientAndServer)]` => `[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]`   * `[UpdateInWorld(TargetWorld.Default)]` => `[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]`   * `if (World.GetExistingSystem<ServerSimulationSystemGroup>()!=null)` => `if (World.IsServer())`   * `if (World.GetExistingSystem<ClientSimulationSystemGroup>()!=null)` => `if (World.IsClient())`
* The `GhostCollectionSystem.CreatePredictedSpawnPrefab` API has been removed as clients will now automatically have predict spawned ghost prefabs set up for them. They can instantiate prefabs the normal way and don't need to call this API.
* `GhostPredictionSystemGroup` has been renamed to `PredictedSimulationSystemGroup`.
* All `GhostAuthoringComponent` `ComponentOverrides` have been clobbered during the upgrade (apologies!). Please re-apply all `ComponentOverrides` via the new (optional) `GhostAuthoringInspectionComponent`. Caveat: Prefer to use attributes wherever possible, as this "manual" form of overriding should only be used for one-off differences that you're unable to express via attributes.
* Inside your `RegisterDefaultVariants` method, replace all `defaultVariants.Add(new ComponentType(typeof(SomeType)), typeof(SomeTypeDefaultVariant));` with `defaultVariants.Add(new ComponentType(typeof(SomeType)), Rule.OnlyParent(typeof(SomeTypeDefaultVariant)));`, unless you _also_ want this variant to be applied to children (in which case, use `Rule.ParentAndChildren(typeof(SomeTypeDefaultVariant))`).

###  Use the new NetworkTime component

All the information in regards the current simulated tick MUST be retrieved from the singleton NetworkTime. In particular:
* The GhostPredictionSystemGroup.PredictingTick has been removed. You must always use the NetworkTime.ServerTick instead. The ServerTick value will correcly reflect the current predicted tick when inspected inside the prediction loop.
* The GhostPredictionSystemGroup.IsFinalPredictionTick has been removed. Use the NetworkTime.IsFinalPredictionTick property instead.
* The ClientSimulationSystemGroup ServerTick, ServerTickFraction, InterpolationTick and InterpolationTickFraction has been removed. You can retrieve the same properties from the NetworkTime singleton. Please refer to the `NetworkTime` component documentation for further information about the different timing properties and the flags behaviours.


## [0.51.1] - 2022-06-27

### Changed

* Package Dependencies
    * `com.unity.entities` to version `0.51.1`


## [0.51.0] - 2022-05-04

### Changed

* Package Dependencies
    * `com.unity.entities` to version `0.51.0`
* Updated transport dependency to 1.0.0.

### Added

* prevent the netcode generator running if the assembly compilation that does not references netcode package.


## [0.50.1] - 2022-03-18

### Added

* Hybrid assemblies will not be included in DOTS Runtime builds.

### Changed

* Changed: Tick systems (Initialization, Simulation, Presentation) are not created as part of the default client-server bootstrap for Hybrid and the Client and Server worlds are updated by the PlayerLoop instead.

### Fixed

* Fixed an exception in `PhysicsWorldHistory` when enabling lag compensation.
* Fixed a rare compile error when source generators found invalid data in the package cache.
* Fixed issue that prevent systems been shown in System Hierarchy window.
* Fixed an issue where RPCs could be lost in rare cases when sending too many of them.
* Fix an incorrect overflow exception when pre-spawned or predicted spawned ghost serialize a subset of the fields.


## [0.50.0] - 2021-09-17

### Added

* Added new methods `GhostSpawnSystem.ConvertGhostToInterpolated` and `GhostSpawnSystem.ConvertGhostToPredicted` for switching the prediction mode of a ghost. The methods have an optional transition time parameter which when not zero will smoothly transition the visual transform from the old to the new state.
* Made it possible for clients to on demand load ghost prefabs by setting `GhostCollectionPrefab.Loading` to `GhostCollectionPrefab.LoadingState.LoadingActive` while the prefab is being loaded.
* Added the possibility to dynamically load new sub-scenes with pre-spawned ghosts at runtime, while the both server and client are in game.
* Added the possibility for a client to have only a sub-set of scenes loaded in respect to the server. Client will be able to load / unload them on demand. Creating a singleton with a DisableAutomaticPrespawnSectionReporting component lets you disable the built-in sub-scene synchronisation and implement your own logic. Can be used to implement more complex streaming scenario or other special needs.
* Support for FirstSendImportanceMultiplier, which can be used to artificially inflate the importance of new (to the client) ghosts. I.e. Allows all ghosts to be sent to the client quickly, even if MinSendImportance is high.
* A DriverMigrationSystem to allow migration of a NetworkDriver and related Connection Entities. To see a working example look into the `WorldMigrationTests`
* Netcode bootstrap can now handle ISystemBase systems.
* The NetDbg will now auto-connect when you focus on it, or when it's first opened, unless you manually call disconnect.
* It is now possible to send commands without setting the `CommandTargetComponent` if the `ICommandData` is on a ghost which is predicted, owned by the current connection and has `SupportAutoCommandTarget` enabled in the authoring component. `SupportAutoCommandTarget` will add a `AutoCommandTarget` component, the server can set the `Enabled` member to false to prevent commands from being sent. The `AutoCommandTarget` can be used to send commands for multiple entities. It is also possible to have multiple `ICommandData` on the same entity, both for `AutoCommandTarget` and `CommandTargetComponent`.
* Added `ClientServerTickRate.MaxSimulationLongStepTimeMultiplier` which allows you to run server ticks with longer delta time instead of, or in addition to, running more ticks in a frame.
* Added `ClientServerTickRate.SendSnapshotsForCatchUpTicks` to decide if the server should send snapshots for all ticks or just the last when it needs multiple ticks in a frame. The default is to only send snapshot for the last tick.

### Changed

* Changed `GhostFieldAttribute.MaxSmoothingDistance` from `int` to `float`
* Changed `ConnectionAcceptJob.debugPrefix` from `FixedString32` to `FixedString128` to account for longer world names.
* Made sure despawning can handle large number of ghosts desapwning at the same time. This also reduces the bandwidth required to despawn, but can increase the latency of despawns if there is packet-loss.
* UpdateInWorld renamed to UpdateInWorldAttribute
* UpdateInWorld.TargetWorld enum move to Unity.NetCode namespace.
* Client can now enter/exit from "in game" without the need to disconnect from the server.
* Server can now stop streaming ghosts to all clients (exit from game), load a new scene/subscene and start streaming ghost again.
* `GhostPredictionDebugSystem` only runs when NetDbg is connected and processes more errors in parallel to improve performance.
* Use stopwatch instead of TimeSpan for dots-runtime portability
* Improve the handling of ticks when applying ghost state to avoid errors about not having a state to roll back to.
* Server is now responsible to assign to all the pre-spawned ghosts their unique id.
* All types in the generated serialiser now use qualified names
* Debug logging is implemented using com.unity.logging
* Added validation check on the server side that verify the command target entity, when set, has a ICommandData buffer.
* Fixed command age not updated on the server if a non null entity target is set but no command data buffer is present. That was causing problem on the clients that were constantly increasing the prediction loop count and dropping the frame rate.
* Pre-spawned ghost entities are disabled during conversion and re-enabled at runtime after their baseline are initialised. This should prevent user code to modify components before the entities are ready and consequently avoiding pre-spawned ghost hash validation failures.
* An error is reported if a fields in ICommandData/IRpcCommand or replicated fields in IComponentData/IBufferElement starts with the reserver prefix __GHOST or __COMMAND
* Replaced the out-out `DisableLagCompensation` with an opt-in `LagCompensationConfig`.
* Removed previously deperecated `GhostCollectionAuthoringComponent`.
* Undeprecated `ConvertToClientServerEntity`. It was deprecated because the old source gen could no support runtime conversion of ghosts, that is not a problem in the new source generator. We still recommend using subscenes for everything involving ghosts.
* `NetworkStreamCloseSystem` has been moved to `NetworkReceiveSystemGroup`.
* Network connection entities now has `LinkedEntityGroup` which makes it easier to delete ghosts on disconnection in simple cases.
* The `GhostAuthoringComponent` has a new checkbox for adding a `GhostOwnerComponent` to a ghost without additional authoring components.
* SceneLoadingTests are not Editor only tests
* Websocket's DebugWebSocket code fixed for il2cpp tests

### Fixed

* Fixed GhostAuthoringEditor not showing the correct default variant assigned to a component.
* Fixed memory leak. GhostVariantAssignmentCollection blob data not disposed.
* Fixed issue with ghost variant cache. GhostComponentVariation attribute where collected only if the annotated struct was public.
* Stale inputs are no longer stored in the input buffer on the server. This makes it more reliable to compare current input state to last frames state.
* Avoid overflow in RTT calculation if reported processing time is greater than the calculated delta time
* Fixed hash calculation for child entities
* Fixed an inconsistency error thrown when registering a generic job with a generic RPC type by changing the accessibility of 'RpcCommandRequest.SendRpcData' from protected to public
* Fixed wrong stats packet size that was causing random crashes.
* Fix GhostStatsSystem try access a NetworkAckComponent singleton when it does not exists (client only)
* Typo in GhostSnapshotValueULong that cause compilation error when an RPC contains unsigned long fields.
* LogAssert.ignoreFailingMessages not reset to true, causing some failing tests not being reported.
* IrrelevantImportanceDownScale is now guarded to not go below 1.
* `SnapshotDataBuffer` and `SnapshotDynamicDataBuffer` now use `[InternalBufferCapacity(0)]`, which will reduce entity size in a chunk.
* Compilation error due to generated serializer class trying to cast types without prepending the correct namespace.
* Ghost gen fails with GhostCodeGen failed for fragment.. if you have a namespace, typename or field name start with double underscores. An error is actually reported if __GHOSTXXX__ or __COMMANDXXX__ keywords are present.
* UX improvement when creating an invalid Ghost Authoring.
* No error reported if an component implement multiple interfaces at the same time, causing generating code for the wrong one.
* PacketLogger output files are now saved for standalone player in Application.consoleLogPath instead of current folder, causing errors in some platform/environment.
* No compilation errors for missing assemblies references are reported anymore if the assembly that does not contains types which require code-generated serializers.
* Overriding nested component in a prefab will be assigned correct GameObject reference

### Upgrade guide

* TargetWorld enum is now part of the Unity.NetCode namespace. Find and replace all the `UpdateInWorld.TargetWorld` occurrences with `TargetWorld` and continue to keep the enum old value.
* `DisableLagCompensation` no longer exists. If you were note using lag compensation you can remove it, if you were using lag compensation you must add a `LagCompensationConfig` in order for it to run.
* `GhostCollectionAuthoringComponent` is now removed, see previous upgrade guide and the getting started doc page for information on what to do instead.



## [0.8.0] - 2021-03-23
### New features
* New code-generation system based on Roslyn source generators.
* Added pre-serialization support to ghosts which can reduce CPU time for serializing complex ghosts which are sent to multiple connections every frame.
* Added parameters to control how much data the server serializes based on CPU time in addition to bandwith. The parameters are MinSendImportance, MinDistanceScaledSendImportance, MaxSendChunks and MaxSendEntities.
* Added default baselines for pre-spawned ghosts
* Added bandwidth (and cpu) optimization for pre-spawned ghosts when a new client connect to the server. Only pre-spawned ghosts which have changed in respect their default baseline are sent.
  If static optimization is turned on, no data is sent for the prespawns unless changed.
* Added runtime client/server validation to verify that pre-spawned ghosts baselines and sub-scenes has the same data on both client and server.

### Changes
* Entities created by NetCode now has appropriate names
* Removed IGhostDefaultOverridesModifier.
  * To modify or changed the component/buffer serialization GhostComponentVariation must be used instead.
  * To add custom templates you should implement the partial class `UserDefinedTemplates`
* NetCode generated classes are not presents in the project anymore.
* NetCode code generation windows has been removed
* It is now possible to keep snapshot history on structural changes in some cases when `GhostSendSystem.KeepSnapshotHistoryOnStructuralChange` is set to true (the default)
* GhostId for prespawn and GhostId for normal spawned ghosts are now two disjoint set. Prespawn ghosts ids have the 31st bit set and as such are negative integers values.

### Fixes
* Fixed bad codegen when using entities in ICommandData structs
* Made sure CreatePredictedSpawnPrefab does not instantiate child entities
* Fixed an issue with disconnect messages not being send on shutdown
* Fixed a very rare issue where invalid baselines could be used when an entity had structural changes
* Translation and Rotation of predicted ghosts are not modified if physics runs in the ghost prediction loop and PhysicMassOverride.IsKinematic is set to 1.
* Entities which have never been sent to a client no longer requires despawn messages when they become irrelevant
* Fixed dynamic buffer change masks not properly cleanup and buffers always reported as changed
* Fix latestSnapshotEstimate not reset when client is not in game
* Fix PredictionHistoryBuffer not updated for predicted ghost with static optimization turned on
* Fix GhostSendSystem not properly cleanup if last client exit the game

### Upgrade guide
* The `Assets/NetCodeGenerated` folder must be removed before/after the upgrade. Compilation errors may be present if you remove
  the folder after the upgrade.

If your project was customizing the code-generation by using Modifiers and/or templates extra steps are necessary.

#### If you are using custom templates in your project
Create a new folder inside your project add an assembly reference to NetCode. For example:
```text
+ CodeGenCustomization/
   + NetCodeRef/
       NetCode.asmref
   + Templates/
       Templates.asmdef (has NETCODE_CODEGEN_TEMPLATES define constraints)
```
You are going to put here your templates and subtypes definition. The steps are outline below but please reference to the updated docs for more information.

##### Re-implementing template registration
Create a new file and add a partial class for the `UserDefinedTemplates` inside the folder with the netcode.asmref (in the example is NetCodeRef).
Then implement the `static partial void RegisterTemplates(...)` method, you will register here your templates.

```csharp
using System.Collections.Generic;
namespace Unity.NetCode.Generators
{
    public static partial class UserDefinedTemplates
    {
        static partial void RegisterTemplates(List<TypeRegistryEntry> templates, string defaultRootPath)
        {
            templates.AddRange(new[]{

                new TypeRegistryEntry
                {
                    Type = "Unity.Mathematics.float3",
                    SubType = Unity.NetCode.GhostFieldSubType.Translation2d,
                    Quantized = true,
                    Smoothing = SmoothingAction.InterpolateAndExtrapolate,
                    SupportCommand = false,
                    Composite = false,
                    Template = "Assets/Samples/NetCodeGen/Templates/Translation2d.cs",
                    TemplateOverride = "",
                },
            }
        }
    }
}
```
##### New Subtype definition
If your template uses sub-types (as in the example above), you need add a partial class for __Unity.NetCode.GhostFieldSubType__ type inside the netcode assembly reference folder.
For example:
```c#
namespace Unity.NetCode
{
    static public partial class GhostFieldSubType
    {
        public const int MySubType = 1;
    }
}
```
The new subtypes will now be available in your project everywhere you are referencing the Unity.NetCode assembly now.

#### How to reimplement GhostComponentModifiers
ComponentModifiers has been removed and you should create a ghost component variant instead using __GhostComponentVariation__ attribute.
<br>
1) Create a new file that will contains your variants in an assembly that has visibility / access to the types
you are going to add variation for. Then for each modifier you had before, just create its respective variant implementation as in the following example.

```csharp
  // Old modifier
  new GhostComponentModifier
  {
      typeFullName = "Unity.Transforms.Translation",
      attribute = new GhostComponentAttribute{PrefabType = GhostPrefabType.All, SendTypeOptimization = GhostSendType.All, SendDataForChildEntity = false},
      fields = new[]
      {
          new GhostFieldModifier
          {
              name = "Value",
              attribute = new GhostFieldAttribute{Quantization = 100, Smoothing=SmoothingAction.InterpolateAndExtrapolate}
          }
      },
      entityIndex = 0
  };

// The new variant
[GhostComponentVariation(typeof(Translation))]
public struct MyTranslationVariant
{
  [GhostField(Quantization=100, Smoothing=SmoothingAction.InterpolateAndExtrapolate)] public float3 Value;
}
```

2) Then you must declare these variants __as the default to use for that component.__
   You must create a concrete system implementation for the `DefaultVariantSystemBase` by implementing the `RegisterDefaultVariants` method.
```csharp
class MyDefaultVariantSystem : DefaultVariantSystemBase
{
    protected override void RegisterDefaultVariants(Dictionary<ComponentType, System.Type> defaultVariants)
    {
        defaultVariants.Add(new ComponentType(typeof(Translation)), typeof(MyTranslationVariant));
        ...
    }
}
```
There are no particular restriction where to put this system. Please refer to the updated docs for more information.


## [0.7.0] - 2021-02-05
### New features
* Added network logging functionality which can be used to get more detailed netcode debug information (`Debug` level) in general or to enable ghost snapshot or packet logging per connection. Has simple toggles in the Playmode Tools Window and in the `NetCodeDebugConfigAuthoring` component. Can programmatically be changed via `NetCodeDebugSystem.LogLevel` or by adding the `EnablePacketLogging` component to connection entities.
* Added support for running physics in the ghost prediciton loop. Create a singleton with a `PredictedPhysicsConfig` component to enable it. See the physics section of the manual for more information.

### Changes
* Disconnect reason is now properly passed from transport to netcode and the `NetworkStreamDisconnectReason` enum now matches `Unity.Networking.Transport.DisconnectReason`

### Deprecated
* Deprecated `GhostCollectionAuthoringComponent`, please create a component with references to the prefabs you want to spawn and make sure the component exists on an entity in a scene. There is no need to store pre spawned ghosts in a collection.

### Fixes
* Fixed an issue causing interpolation time to jitter and sometime moving backwards on the client.
* Fixed an issue whith packet loss in the network condition simulator not working reliably.
* Fixed an issue with il2cpp stripping causing errors at runtime.
* Fixed an issue with fragmented snapshots in release builds.

### Upgrade guide
* User specified bootstraps (classes extending ClientServerBootstrap) must have a `[Preserve]` attribute on the class.

## [0.6.0] - 2020-11-26
### New features
* Added DynamicBuffers serialization support to ghosts. Like IComponentData, is now possible to annotate IBufferElementData with GhostComponentAttribute and members with GhostFieldAttribute
and having the buffers replicated through the network.
* ICommandData are now serializable and can be sent to the remote players.
* Added new SendToOwner property to the GhostComponentAttribute that can be used to configure to which
subset of players the component should be sent to: only to the owner, only to the non owner, or all.
* Ghost Component Serialization Variant. A new GhostComponentVariation attribute has been introduced that let you to specify different serialization options for a component or buffer, by overriding
the `[GhostField]` and `[GhostComponent]` properties present in the original type definition.
* It is possible to prevent a component to support variation by using the `[DontSupportVariation]` attribute. When present, if a GhostComponentVariation is defined for that type, an exception is triggered.
* Ghost components attributes and serialization variants can be customized per prefabs. For every component in a ghost prefab, it is possible to change:
    * PrefabType
    * GhostSendType
    * The variant to use, if variants for that component exist.
* Is possible to prevent a component to support per-prefab overrides by using the [DontSupportPrefabOverride] attribute. When present, the component can't be further customized in the inspector.
* It's now possible to register a prediction smoothing function, by calling the `GhostPredictionSmoothingSystem.RegisterSmoothingAction<ComponentType>(SmoothingActionDelegate)` and supplying a `ComponentType` and `GhostPredictionSmoothingSystem.SmoothingActionDelegate` (see Runtime/Snapshot/DefaultUserParams.cs for an example).
* Added a new `ClientTickRate` component which when added to a singleton entity controls the interpolation times used to calculate time on the client. The default values can be accessed through the static `NetworkTimeSystem.DefaultClientTickRate`.
* Added support for extrapolation when the tick being applied to interpolated ghosts on the client has not been received yet and is outside the interpolation delay. Set the new `Smoothing` field in the `GhostField` attribute to `SmoothingAction.InterpolateAndExtrapolate` to enable extrapolation.
* Added a `MaxSmoothingDistance` parameter to the `[GhostField]` attribute. If specified interpolation will be disabled when the values change more than that limit between two snapshots. This is useful for dealing with teleportation and similar changes which should not be interpolated.

### Changes
* It is no longer required to create a ghost collection, as long as there is a prefab for a ghost it will be picked up automatically. You can create a prefab by referencing it in a spawner component or by placing a pre spawned instance of a ghost.

### Fixes
* Fixed an issue where the elapsed time was not using the max simulation rate - causing the fixed time step physics to take more and more time.
* Fixed an issue causing time rollbacks when running client and server in the editor if performance is too low.

### Upgrade guide
The `Interpolate` bool in the `GhostField` attribute has been replaced with `Smoothing`. Replace `Interpolate=true` with `Smoothing=SmoothingAction.Interpolate` to keep the old value, or set it to `SmoothingAction.InterpolateAndExtrapolate` to enable extrapolation.

## [0.5.0] - 2020-10-01
### New features
* Added RpcSystem.DynamicAssemblyList which can be used to delay the checksums for RPCs and ghost components when the set of assemblies are different on the client and server.
* Added to RPC and Command the possiblity to send Entity reference from both client and server.

### Changes
* Change the system ordering to be compatible with latest physics. `NetworkTimeSystem` has moved to `ClientInitializationSystemGroup`. The SimulationSystemGroup runs `GhostSpawnSystemGroup` (client), `GhostReceiveSystemGroup` and `GhostSimulationSystemGroup` before `FixedStepSimulationSystemGroup` where physics is running. `RpcCommandRequestSystemGroup`, `RpcSystem` and `GhostSendSystem` (server) is running at the end of the frame, after all simulation code. Other systems has been moved into one of the groups.
* Created a new `GhostInputSystemGroup` where systems adding inputs to the input buffer should run.

### Fixes
### Upgrade guide
* The systems adding input to the `ICommandData` buffer needs to be moved to `GhostInputSystemGroup`

## [0.4.0] - 2020-09-10
### New features
* Code gen support for ICommandData, serialization for command data can now be generated instead of hand-written. You can opt out of code generation by adding `[NetCodeDisableCommandCodeGen]`.
* `NetCodeConversionSettings` has a new Client And Server mode, which makes it possible to build a single standalong build supporting both client and server.
* There is a new static method to generate predicted spawn version of a prefab, `GhostCollectionSystem.CreatePredictedSpawnPrefab`.

### Changes
* When not using code-gen for rpcs or commands the systems for registering them (the ones extending `RpcCommandRequestSystem<TActionSerializer, TActionRequest>`, `CommandSendSystem<TCommandDataSerializer, TCommandData>` and `CommandReceiveSystem<TCommandDataSerializer, TCommandData>`) need some more code to setup the jobs.
* The `ICommandData` interface no longer takes an additional generic type.
* Added a `CommandSendSystemGroup` and a `CommandReceiveSystemGroup` which can be used for dependencies when generating code for `ICommandData`.
* Moved the GameObjects used for authoring to a separate assembly.
* Fixed tickrate on the client is no longer supported. This also means that the render interpolation has been removed.
* Using multiple rendering clients in the editor is no longer supported, thin clients are still supported.
* The `GhostPrefabCollectionComponent` now only contains a single prefab list, and the `GhostPrefabBuffer` for it is attached to the same entity.

### Deprecated
* Deprecated `ConvertToClientServerEntity`, please use the sub-scene conversion workflow instead.

### Fixes
* Fixed a compile error in the generated code for components containing multiple ghosted Entity references.
* Fixed a bug where predicted spawn ghosts were not destroyed on mis-prediction.
* Fixed a bug where data for child entities on predicted ghosts could be corrupted.

### Upgrade guide
* The predicted spawn code must switch to using the new `GhostCollectionSystem.CreatePredictedSpawnPrefab` utility method since there is only a single prefab on the client and it requires some patching before it can be used.
* When using the `GhostPrefabCollectionComponent` to find a prefab to find a ghost prefab on the server you must change the code to read the `GhostPrefabBuffer` from the same entity as `GhostPrefabCollectionComponent`.
* If you are using fixed tickrate mode on the client you need to remove the creation of the `FixedClientTickRate` singleton and remove the `CurrentSimulatedPosition` and `CurrentSimulatedRotation` if using them.
* If you are using "Num Clients" in the PlayMode tools you need to move to using "Num Thin Clients" instead.
* RPCs not using code-gen needs to add more code to the `RpcCommandRequestSystem`. The new implementation should look like this:
```c#
class MyRequestRpcCommandRequestSystem : RpcCommandRequestSystem<MyRequestSerializer, MyRequest>
{
    [BurstCompile]
    protected struct SendRpc : IJobEntityBatch
    {
        public SendRpcData data;
        public void Execute(ArchetypeChunk chunk, int orderIndex)
        {
            data.Execute(chunk, orderIndex);
        }
    }
    protected override void OnUpdate()
    {
        var sendJob = new SendRpc{data = InitJobData()};
        ScheduleJobData(sendJob);
    }
}
```
* The `Tick` property in `ICommandData` now requires both a getter and a setter.
* ICommandData structs no longer need serialization or implementaions of `CommandSendSystem` and `CommandReceiveSystem` if you are using code-gen, and the interface changed from `ICommandData<T>` to `ICommandData`.
* When manually writing serialization code for `ICommandData` you need to move the serialization code to a struct implementing `ICommandDataSerialize<T>`, and the `CommandSendSystem` and `CommandReceiveSystem` implementations need code to schedule the jobs like this:
```c#
public class MyCommandSendCommandSystem : CommandSendSystem<MyCommandSerializer, MyCommand>
{
    [BurstCompile]
    struct SendJob : IJobEntityBatch
    {
        public SendJobData data;
        public void Execute(ArchetypeChunk chunk, int orderIndex)
        {
            data.Execute(chunk, orderIndex);
        }
    }
    protected override void OnUpdate()
    {
        var sendJob = new SendJob{data = InitJobData()};
        ScheduleJobData(sendJob);
    }
}
public class MyCommandReceiveCommandSystem : CommandReceiveSystem<MyCommandSerializer, MyCommand>
{
    [BurstCompile]
    struct ReceiveJob : IJobEntityBatch
    {
        public ReceiveJobData data;
        public void Execute(ArchetypeChunk chunk, int orderIndex)
        {
            data.Execute(chunk, orderIndex);
        }
    }
    protected override void OnUpdate()
    {
        var recvJob = new ReceiveJob{data = InitJobData()};
        ScheduleJobData(recvJob);
    }
}
```

## [0.3.0-preview.3] - 2020-08-21
### New features
* New workflow for generating serialization code for ghosts. In the new workflow code is generated per component and there is no codegen per ghost type prefab.
  * The ghost fields and ghost components are now configured in code with `GhostField` and `GhostComponent` attributes where you can configure parameters for prediction, interpolation, quantization and so on.
  * The ghost component and collection inspectors now only show you how the ghosts have been configured in code.
  * Ghosts can now be generated on demand and you don't need to explicitly push a button to do it.
  * A new ghost compiler windows allows you to change how the generation is handled, like changing from on demand generation to manual) and shows you if any ghost is out of sync.
* Code gen support for RPCs, an RPC can be created without writing serialization by hand. Implement the `IRpcCommand` interface and write it like you would a regular `IComponentData` - the serialization code will be generated for you.
* GhostGroups are now supported. A ghost prefab can have a `GhostGroup` buffer added to it at authoring time, all ghosts listed in that buffer are guaranteed to be sent together with the main entity. In order to be in a group the child ghost must have the `GhostChildEntityComponent` component added to it. The `GhostChildEntityComponent` can be added at runtime when moving the child entity into the group.
* Relevancy support has been added. By changing `GhostSendSystem.GhostRelevancyMode` to `GhostRelevancyMode.SetIsRelevant` or `GhostRelevancyMode.SetIsIrrelevant` on the server and adding ghosts to `GhostSendSystem.GhostRelevancySet` you can limit the set of ghosts which are sent to a specific client.
* Added an optimization mode to ghosts, the new static optimization mode will use less aggressive delta compression which allows us to stop sending data completely when no entities in a chunk have been modified.
* Added visualization of prediction errors to the NetDbg.
* A connection entity on the server can have a `NetworkStreamSnapshotTargetSize` which is used to control the target size for snapshots.
* Added `GhostReceiveSystem.GhostCountOnServer` and `GhostReceiveSystem.GhostCountOnClient` which can be used to check how many ghosts a client should have and how many it does have.

### Changes
* Support for `NativeString64` has been replaced by support for `FixedString64`. Support for `FixedString32`, `FixedString128`, `FixedString512` and `FixedString4096` has also been added.
* In dynamic timestep mode it is now possible to resume prediction from the last full predicted tick instead of rolling back to the latest received snapshot when no new data has been received.
* Added a `DisableLagCompensationComponent` which when added as a singleton prevents the lag compensation system from running.

### Fixes
* Quaternions are renormalized after dequantization to make sure they are still valid rotations.
* Floats are rounded to the nearest int after quantization to improve acuracy.
* It is now possible to send more than one packet with RPC commands per frame, previously commands could be silently dropped when doing that.

### Upgrade guide
* `NativeString64` is no longer uspported, change your code to use `FixedString64` instead.
* `GhostUpdateSystemGroup` no longer exists, references to it for update order should be replaced with `GhostUpdateSystem`
* NetCode now requires Unity 2020.1.2.

#### New ghost workflow
* Change all `[GhostDefaultField]` to `[GhostField]` and all `[GhostDefaultComponent]` to `[GhostComponent]` in your components. The parameters to the constructors have also changed, you need to specify `[GhostField(Quantization=100, Interpolate=true)]` instead of `[GhostDefaultField(100, true)]`.
* For all ghosts which manually adds fields you must add `GhostField` attributes to the component since manual overrides are no longer supported.
* For all ghosts which removes a component from `Server`, `Interpolated Client` or `Predicted Client` you must add a `[GhostComponent(PrefabType=<type>)]` attribute to the component where `<type>` matches what you had before.
* For all components which you do not want to synchronize when they are on child entities of a ghost you need to add `[GhostComponent(SendDataForChildEntity = false)]`.
* Open all prefabs and verify that `Name`, `Importance` and `Default ghost mode` are still correct. `Supported Ghost Mode` and `Optimization Mode` are new fields and the default values matches what the old workflow did.
* For all ghosts which uses the owner predicted mode you must add a `GhostOwnerComponent` and make sure your code sets the `NetworkId` of that component correctly. Previously you could store the network id in any component and point the `GhostAuthoringComponent` to it.
* For all components which you were only being sent to either interpolated or predicted ghosts when used on owner predicted ghosts you need to add `[GhostComponent(OwnerPredictedSendType = <type>)]` where `<type>` is either `GhostSendType.Interpolated` or `GhostSendType.Predicted`.
* Delete the generated code from the old NetCode version.
* If you are using predictive spawning the new way to request a predictive spawn is to instantiate the predicted client version of the ghost prefab and add a `PredictedGhostSpawnRequestComponent` to the entity.
* Any custom spawn behavior - including matching entities for pre-spawned ghosts - previously implemented in `MarkPredictedGhosts` must be moved to a spawn classification system.
* Any custom code to modify spawned ghosts previously implemented in `UpdateNewPredictedEntities` or `UpdateNewInterpolatedEntities` must be moved to systems running in the `GhostSpawnSystemGroup` after `GhostSpawnSystem`. Use tag components to deterct which ghosts are new.

#### RPC
* If your `IRpcCommand` component only uses `RpcExecutor.ExecuteCreateRequestComponent` in the execute method you can remove the implementations for `Serialize`, `Deserialize`, `CompileExecute` along with the execute method and burst function pointer for it. You also need to remove the `CommandRequestSystem` implementationf for your component. All of those will be generated by code-gen.
* All RPC implementations which still needs manual serialization or execute must be changed to implement `public struct MyRequest : IComponentData, IRpcCommandSerializer<MyRequest>` instead of `public stuct MyRequest : IRpcCommand`.
* The signature for RPC serialization has changed to `void Serialize(ref DataStreamWriter writer, in MyRequest data)` and deserialization has changed to `void Deserialize(ref DataStreamReader reader, ref MyRequest data)`.
* The CommandRequestSystem for rpcs with manual serialization/execute must be changed from `class MyRequestCommandRequestSystem : RpcCommandRequestSystem<MyRequest>` to `class MyRequestCommandRequestSystem : RpcCommandRequestSystem<MyRequest, MyRequest>`

## [0.2.0-preview.5] - 2020-06-05
### New features
* Support for pre-spawned ghosts. When prefab ghost instances are placed into subscenes they will be present on server and clients when they load the scene. They are then automatically connected together and will work just like normally spawned ghosts after that.

### Changes
* Changed how snapshot size is limited to make it more robust and give more clear errors.
* Added `Name` field to the `GhostAuthoringComponent`  which is used during code generation to identify the ghost prefab. By default this is the prefab name but can be changed.
* `ClientServerBootstrap` now correctly use two-phase initialization to initialise all the systems
* Changed `PhysicsWorldHistory.CollisionHistoryBuffer` to return a safe memory reference to the `CollisionHistoryBuffer` instead of copy a large amount of data on the stack.
* Upgrade to Entities 0.11

### Fixes
* Fixed issue with ghost prefabs when they were Variant or Model Prefabs.
* Fixed issue with datastream going out of sync when snapshot desync was detected
* Fixed an issue with `RegisterRPC` in case you try to register a malformed RPC with an invalid pointer
* Fixed an issue with `ServerTick` that does not monotonically increase in presence of high ping
* Fixed an issue with `ClientServerTickRate` being created multiple times if the client connect and disconnect from the server
* Fixed an issue with `ClientServerTickRate` not reused by the client if it was already present in the world
* Fixed an issue with `ClientServerBootstrap` and the fact `TypeManager` was not initialised when we generate client/server world's the system lists

### Upgrade guide

* A `Name` field was added to the `GhostAuthoringComponent` and as a result all prefabs with this component need to be opened and close to serialize the field. It's used as a prefix name during code generation so it might also be neccessary to press the _Generate Code_ button again

## [0.1.0-preview.6] - 2020-02-24
### New features
* Added integration with UnityPhysics, including the lag compensation from DotsSample. To use it you must have the UnityPhysics added to your project.

### Changes
* Unity Transport has been upgraded to 0.3.0 which required some API changes - see "Upgrade guide".
* All `FunctionPointer` instances are cached in statics to reduce the number of calls to compile.
* The helper method RpcExecutor.ExecuteCreateRequestComponent returns the entity it creates.
* Added an interface to NetworkStreamReceiveSystem which is used when creating the driver. It is possible to set NetworkStreamReceiveSystem.s_DriverConstructor to a custom instance during bootstrapping to create drivers in a custom way.
* Removed World.Active workaround since it has been deprecated for a while and is causing problems with conversion at runtime.
* Slightly improved performance by ensuring that all jobs that can be Burst compiled are
* Ghost types are now selected based on the guid of the ghosts prefab asset instead of the archetype. This makes it possible to have multiple different ghosts with the same archetype. If a ghost is not a valid prefab you will get an error during conversion.

### Fixes
* Fixed an issue where ghost prefabs created from GameObject instances were processed by all systems.
* The code gen now only writes files if they are modified.
* Disposing a client or server world will now unregister it from the tick system to prevent errors.
* Take the latency of command age updates into account when calculating time scale to get more stable inputs with high ping.

### Upgrade guide
Unity Transport has been upgraded to 0.3.0 which changes the API for `DataStreamReader` and `DataStreamWriter`.

The `IRpcCommand` and `ICommandData` have been changed to not take a `DataStreamReader.Context`.

The `ISnapshotData` and GhostCollection interfaces have been changed to not take a `DataStreamReader.Context`, all ghosts and collections must be regenerated.

`GhostDistanceImportance.NoScale` and `GhostDistanceImportance.DefaultScale` have been replaced by `GhostDistanceImportance.NoScaleFunctionPointer` and `GhostDistanceImportance.DefaultScaleFunctionPointer` which are compiled function pointers rather than methods.

## [0.0.4-preview.0] - 2019-12-12
### New features
### Changes
* Changed the codegen for NativeString64 to use the serialization in DataStream.

### Fixes
### Upgrade guide

## [0.0.3-preview.2] - 2019-12-05
### New features
### Changes
* Updated the documentation and added a section about prediction.
* Upgraded entities to 0.3.0.

### Fixes
* Fixed a crash when multiple clients disconnected on the same frame.
* Fixed read / write access specifiers in AfterSimulationInterpolationSystem.
* Fixed build errors in non-development standalone builds.

### Upgrade guide

## [0.0.2-preview.1] - 2019-11-28
### New features
### Changes
### Fixes
* Fix compile error in generated serialization code for strings.
* Fix warning when entering playmode with the netcode disabled.

### Upgrade guide

## [0.0.1-preview.6] - 2019-11-26
### New features
* Made it possible to scale importance based on distance to support more ghosts.
* Nested entities constaining replicated data are now supported.
* Entity references now supported as ghost fields. The references are weak references which will resolve to Entity.Null if the target is not guaranteed to exist.
* NativeString64, enums and bools are supported as ghost fields.
* `ClientServerTickRate` added where you can configure certain behavior relating to timestepping. A headless server can be configured to sleep after hitting target framerate to conserve CPU usage.
* Send different data depending on if the entity is predicted or interpolated, some savings can be done in the predicted case.
* Added a protocol version which must match for the connection to succeed.
* Added time graphs and server view to the network debugger.
* Network simulator now supports jitter.

### Changes
* The authoring flow has been improved.
  * `GhostAuthoringComponent` now automatically detects what components an entity has after conversion runs and automatically populates them when you press the "Update component list" button. You no longer need to manually type in each component name.
  * Default values can be defined for certain component types, for example with Translation components you usually want to synchronize the Value field. When default handling has been defined the ghost authoring component uses that when it parses the entity component list.
  * `[GhostDefaultField]` attribute added. This can be added to ghost variable which are to be synchronized. The GhostAuthoringComponent detects these fields.
  * `[GhostDefaultComponent]` attribute added. This can be used to define default behavior for how a component should be synchronized, InterpolatedClient, PredictedClient and Server.
  * `GhostCollectionAuthoringComponent` added. This is where all spawned prefabs can be registered
  * Paths are easier in general as you can set up the root of where you want generated files placed and defaults can be specified in code.
  * Which components result in variable data being replicated are marked in bold in the inspector, so it's easier to see how much data will be sent per ghost.
* Improved snapshot prediction handling
  * Uses the servers delta times now instead of the clients.
  * Support for dynamic timestep and fractional tick prediction.
  * Handles stalls and won't try to replay to far back in the rollback (64 frames is maximum)
  * Less boilerplate code needed to setup a predicted entity, more default handling moved to code generation
  * Added `GhostPredictionSystemGroup` with better calculations for the currently predicting tick on the client
  * Interpolation time is an offset from prediction time to make sure they do not drift.
* Multiple inputs are sent together so dropped inputs have less effect on misprediction.
* Thin clients added, these use fewer resources than full client simulations so it's easier to test with many clients now.
* An RPC heartbeat system was added which only runs when nothing is being sent from the client to the server, preventing a disconnect timeout from happening. As soon as inputs are sent and snapshot synchronization starts, the system stops running.
* RPC boilerplate code reduced, when a component inheriting`IRpcCommandRequestComponentData` is added to an entity along with `SendRpcCommandRequestComponent` it will be sent automatically.
* The client/server world bootstrapping has been simplified, you can now use your own custom bootstrapping more easily and create the client/server worlds depending on which one you want at runtime. By default the world creation in the editor is controlled by the playmode tools.
* `NetCodeConversionSettings` added which makes it possible to specify which type of build you want (client/server) in the subscene build settings workflow.
* Detect when ackmask desyncs occur
* Improved ghost code generation to make it able to regenerate code when there are compile errors.
* Snapshots are now acknowleged when there is no CommandSendSystem.
* Use the entities TimeData struct instead of getting time from Client/ServerSimulationSystemGroup

### Fixes
* The code generation in Ghost Authoring Components now generate imports for user namespaces.
* Code generation triggers an asset database refresh so the modified files are compiled.
* Command inputs now correctly respects the `NetworkStreamInGame` being present before starting transmission.
* Acks can now arrive with bigger interval than 64 ticks
* Generated code no longer requires unsafe code to be enabled in the project.

### Upgrade guide
* Unity 2019.3 is now required (beta 11 minimum) and Entities 0.2.0-preview.
* The `NetCode` folder was moved to a proper package, `com.unity.netcode` which should now be used instead.
* All the netcode was moved to a `Unity.NetCode` namespace.
* `[NotClientServerSystem]` attribute removed, use `[UpdateInWorld(UpdateInWorld.TargetWorld.Default)]` instead, it will do the same thing.
* `GhostPrefabAuthoringComponent` removed, use the new `GhostCollectionAuthoringComponent` instead for setting up ghost data.
* `ClientServerSubScene` removed, it's not needed anymore.
* `NetworkTimeSystem.predictTargetTick` removed, use `GhostPredictionSystemGroup.PredictingTick` instead.
* The interface for RPCs has changed and they no longer require a generated collection.

## [0.0.1-preview.2] - 2019-07-17
### New features
* Added a prefab based workflow for specifying ghosts. A prefab can contain a `GhostAuthoringComponent` which is used to generate code for a ghost. A `GhostPrefabAuthoringComponent` can be used to instantiate the prefab when spawning ghosts on the client. This replaces the .ghost files, all projects need to be updated to the new ghost definitions.
* Added `ConvertToClientServerEntity` which can be used instead of `ConvertToEntity` to target the client / server worlds in the conversion workflow.
* Added a `ClientServerSubScene` component which can be used together with `SubScene` to trigger sub-scene streaming in the client/ server worlds.

### Changes
* Changed the default behavior for systems in the default groups to be included in the client and server worlds unless they are marked with `[NotClientServerSystem]`. This makes built-in systems work in multiplayer projects.
* Made standalone player use the same network simulator settings as the editor when running a development player
* Made the Server Build option (UNITY_SERVER define) properly set up the right worlds for a dedicated server setup. Setting UNITY_CLIENT in the player settings define results in a client only build being made.
* Debugger now shows all running servers and clients.

### Fixes
* Change `World.Active` to the executing world when updating systems.
* Improve time calculations between client and server.

### Upgrade guide
All ghost definitions specified in .ghost files needs to be converted to prefabs. Create a prefab containing a `GhostAuthoringComponent` and authoring components for all required components. Use the `GhostAuthoringComponent` to update the component list and generate code.

## [0.0.1-preview.1] - 2019-06-05
### New features
* Added support systems for prediction and spawn prediction in the NetCode. These can be used to implement client-side prediction for networked objects.
* Added some support for generating the code required for replicated objects in the NetCode.
* Generalized input handling in the NetCode.
* New fixed timestep code custom for multiplayer worlds.

### Changes
* Split the NetCode into a separate assembly and improved the folder structure to make it easier to use it in other projects.
* Split the Asteroids sample into separate assemblies for client, server and mixed so it is easier to build dedicated servers without any client-side code.
* Upgraded Entities to preview 33.

### Fixes
### Upgrade guide

## [0.0.1-preview.0] - 2019-04-16
### New features
* Added a new sample asteroids game which we will be using to develop the new netcode.

### Changes
* Update to Unity.Entities preview 26

### Fixes
### Upgrade guide
Unity 2019.1 is now required.
