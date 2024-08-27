# What's new in Netcode for Entities 1.0

This is a summary of the changes present in Netcode For Entities version 1.0.

For a full list of changes, see the [Changelog](xref:changelog). For information on how to upgrade to version 1.0, see the [Upgrade guide](upgrade-guide.md).

## Added

* Added a new unified `NetCodePhysicsConfig` to configure in one place all the netcode physics settings. LagCompensationConfig and PredictedPhysicsConfig are generated from these settings at conversion time.
* Added a new API, `GhostPrefabCreation.ConvertToGhostPrefab` which can be used to create ghost prefabs from code without having an asset for them.
* Added a support for creating multiple network drivers. It is now possible to have a server that listen to the same port using different network interfaces (ex: IPC, Socket, WebSocket at the same time).
* Added: A new NetworkTime component that contains all the time and tick information for the client/server simulation. Please look at the upgrade guide for more information on how to update your project.
* Serializaton support for IEnableableComponent. The component enabled state can be optionally replicated by the server.
* Added a new input interface, IInputCommandData, which can be used to automatically handle input data as networked command data. The input is automatically copied into the command buffer and retrieved from it as appropriate given the current tick.
* Added a new InputEvent type which can be used inside such an input component to reliably synchronize single event type things.
* Added support for running the prediction loop in batches by setting `ClientTickRate.MaxPredictionStepBatchSizeRepeatedTick` and `ClientTickRate.MaxPredictionStepBatchSizeFirstTimeTick`. The batches will be broken on input changes unless the input data that changes is marked with `[BatchPredict]`.
* Added optimisation to reduce the number of predicted tick and interpolation frames when using client/server in the same process and IPC connection.
* Added a `ConnectionState` system state component which can be added to connection to track state changes, new connections and disconnects.
* Added a `NetworkStreamRequestConnect` component which can be added to a new entity to create a new connection instead of calling `Connect` directly.
* Added a `NetworkStreamRequestListen` component which can be added to a new entity to make the server listening instead of calling `Listen` directly.
* Added `IsClient`, `IsServer` and `IsThinClient` helper methods to `World` and `WorldUnmanaged`.
* Added a new API, `Ghost Metrics`, used to retrieve ghosts related stats at runtime.
* Added Helper methods to DefaultDriverBuilder, these allows creation and registering IPC- and Socket drivers. On the server both are used for the editor and only socket for player build. On the client either IPC if server and client is in the same process or socket otherwise.
* Added support for secure connection (dtls) to the RegisterClientDriver and RegisterServerDriver, that accept now also certificates.
* Added support for relay server data to the RegisterClientDriver and RegisterServerDriver.
* Added a default spawn classification system is will now handle client predicted spawns if the spawn isn't handled by a user system first (matches spawns of ghost types within 5 ticks of the spawn tick).
* Added some GhostCollectionSystem optimisation when importing and processing ghost prefabs.

## Updated
* The serialization code is now generated also for Component/Buffers/Commands/Rpcs that have internal visibility.
* Ghosts are now marked-up as Ghosts in the DOTS Hierarchy (Pink = Replicated, Blue = Prefab). The built-in Unity Hierarchy has a similar markup, although limited due to API limitations.
* Support for runtime editing the number of ThinClients.
* Unity.Logging package depdency. The Unity.Logging is now the default logging sulution used by the package.
* The client and server worlds are now updated by the player loop instead of relying on the default world updating the client and server world directly.
* Predicted ghost physics now use multiple physics world: A predicted physics wold simulated the ghost physics and a client-only physics world can be used for effect. For more information please refer to the predicted physics documentation.
* Predicted ghost physics now use custom system to update the physics simulation. The built-in system are instead used for updating the client-only simulatiom.
* The limit of 128 components with serialization is now for actively used components instead of components in the project.
* Netcode source generator templates should now use the NetCodeSourceGenerator.additionalfile and are identified by an unique id (see [templates](ghost-types-templates.md) documentation for more info).
* Various improvements to the `PlayMode Tools Window`, including; simulator "profiles" (which are representative of real-world speeds), runtime thin client creation/destruction support, live modification of simulator parameters, and a tool to simulate lag spikes via shortcut key.

## Further information

* [Upgrade guide](upgrade-guide.md)
* [Changelog](xref:changelog)
