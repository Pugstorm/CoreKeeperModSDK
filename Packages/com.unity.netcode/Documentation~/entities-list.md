# Entities list

This page contains a list of all entities used by the Netcode package.

## Connection

A connection entity is created for each network connection. You can think of these entities as your network socket, but they do contain a bit more data and configuration for other Netcode systems.

| Component                                                                                                     | Description                                                                                                                                                       | Condition                                                                   |
|---------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------|
| [__NetworkStreamConnection__](xref:Unity.NetCode.NetworkStreamConnection)                                     | The Unity Transport `NetworkConnection` used to send and receive data.                                                                                            |
| [__NetworkSnapshotAck__](xref:Unity.NetCode.NetworkSnapshotAck)                             | Data used to keep track of what data has been received.                                                                                                           |
| [__CommandTarget__](xref:Unity.NetCode.CommandTarget)                                       | A pointer to the entity where commands should be read from or written too. The target entity must have a `ICommandData` component on it.                          |
| [__IncomingRpcDataStreamBuffert__](xref:Unity.NetCode.IncomingRpcDataStreamBuffer)           | A buffer of received RPC commands which will be processed by the `RpcSystem`. Intended for internal use only.                                                     |
| [__IncomingCommandDataStreamBuffer__](xref:Unity.NetCode.IncomingCommandDataStreamBuffer)   | A buffer of received commands which will be processed by a generated `CommandReceiveSystem`. Intended for internal use only.                                      | Server only                                                                 |
| [__OutgoingCommandDataStreamBuffer__](xref:Unity.NetCode.OutgoingCommandDataStreamBuffer)   | A buffer of commands generated be a `CommandSendSystem` which will be sent to the server. Intended for internal use only.                                         | Client only                                                                 |
| [__IncomingSnapshotDataStreamBuffer__](xref:Unity.NetCode.IncomingSnapshotDataStreamBuffer) | A buffer of received snapshots which will be processed by the `GhostReceiveSystem`. Intended for internal use only.                                               | Client only                                                                 |
| [__OutgoingRpcDataStreamBuffer__](xref:Unity.NetCode.OutgoingRpcDataStreamBuffer)           | A buffer of RPC commands which should be sent by the `RpcSystem`. Intended for internal use only, use an `RpcQueue` or `IRpcCommand` component to write RPC data. |
| [__NetworkId__](xref:Unity.NetCode.NetworkId)                                               | The network id is used to uniquely identify a connection. If this component does not exist, the connection process has not yet completed.                         | Added automatically when connection is complete                             |
| [__NetworkStreamInGame__](xref:Unity.NetCode.NetworkStreamInGame)                                             | A component used to signal that a connection should send and receive snapshots and commands. Before adding this component, the connection only processes RPCs.    | Added by game logic to start sending snapshots and commands.                |
| [__NetworkStreamRequestDisconnect__](xref:Unity.NetCode.NetworkStreamRequestDisconnect)                       | A component used to signal that the game logic wants to close the connection.                                                                                     | Added by game logic to disconnect.                                          |
| [__NetworkStreamSnapshotTargetSize__](xref:Unity.NetCode.NetworkStreamSnapshotTargetSize)                     | Used to tell the `GhostSendSystem` on the server to use a non-default packet size for snapshots.                                                                  | Added by game logic to change snapshot packet size.                         |
| [__GhostConnectionPosition__](xref:Unity.NetCode.GhostConnectionPosition)                                     | Used by the distance based importance system to scale importance of ghosts based on distance from the player.                                                     | Added by game logic to specify the position of the player for a connection. |
| [__PrespawnSectionAck__](xref:Unity.NetCode.PrespawnSectionAck)                                               | Used by the server to track which subscenes the client has loaded.                                                                                                | Server only                                                                 |
| [__EnablePacketLogging__](xref:Unity.NetCode.EnablePacketLogging)                                             | Added by game logic to enable packet dumps for a single connection.                                                                                               | Only when enabling packet dumps                                             |

## Ghost

A ghost is an entity on the server which is ghosted (replicated) to the clients. It is always instantiated from a ghost prefab and has user defined data in addition to the components listed here which control its behavior.

| Component                                                                           | Description                                                                                                                                                                                                                                                                                                                      | Condition                                                               |
|-------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------|
| [__Ghost__](xref:Unity.NetCode.Ghost)                                               | Identifying an entity as a ghost.                                                                                                                                                                                                                                                                                                |                                                                         |
| [__GhostType__](xref:Unity.NetCode.GhostType)                                       | The type this ghost belongs to.                                                                                                                                                                                                                                                                                                  |                                                                         |
| __GhostCleanup__                                                                    | This component exists for only for internal use in the Netcode for Entities package. Used to track despawn of ghosts on the server.                                                                                                                                                                                                           | Server only                                                             |
| [__SharedGhostType__](xref:Unity.NetCode.SharedGhostType)                           | A shared component version of the `GhostType` to make sure different ghost types never share the same chunk.                                                                                                                                                                                                            |
| [__SnapshotData__](xref:Unity.NetCode.SnapshotData)                                 | A buffer with meta data about the snapshots received from the server.                                                                                                                                                                                                                                                            | Client only                                                             |
| [__SnapshotDataBuffer__](xref:Unity.NetCode.SnapshotDataBuffer)                     | A buffer with the raw snapshot data received from the server.                                                                                                                                                                                                                                                                    | Client only                                                             |
| [__SnapshotDynamicDataBuffer__](xref:Unity.NetCode.SnapshotDynamicDataBuffer)       | A buffer with the raw snapshot data for buffers received from the server.                                                                                                                                                                                                                                                        | Client only, ghosts with buffers only                                   |
| [__PredictedGhost__](xref:Unity.NetCode.PredictedGhost)                             | Identify predicted ghosts. On the server all ghosts are considered predicted and have this component.                                                                                                                                                                                                                            | Predicted only                                                          |
| [__GhostDistancePartition__](xref:Unity.NetCode.GhostDistancePartition)             | Added to all ghosts with a `LocalTransform`, when distance based importance is used.                                                                                                                                                                                                                                             | Only for distance based importance                                      |
| [__GhostDistancePartitionShared__](xref:Unity.NetCode.GhostDistancePartitionShared) | Added to all ghosts with a `LocalTransform`, when distance based importance is used.                                                                                                                                                                                                                                             | Only for distance based importance                                      |
| [__GhostPrefabMetaData__](xref:Unity.NetCode.GhostPrefabMetaData)                   | The meta data for a ghost, adding during conversion, and used to setup serialization. This is not required on ghost instances, only on prefabs, but it is only removed from pre-spawned right now.                                                                                                                               | Not in pre-spawned                                                      |
| [__GhostChildEntity__](xref:Unity.NetCode.GhostChildEntity)                         | Disable the serialization of this entity because it is part of a ghost group (and therefore will be serialized as part of that group).                                                                                                                                                                                           | Only children in ghost groups                                           |
| [__GhostGroup__](xref:Unity.NetCode.GhostGroup)                                     | Added to all ghosts which can be the owner of a ghost group. Must be added to the prefab at conversion time.                                                                                                                                                                                                                     | Only ghost group root                                                   |
| [__PredictedGhostSpawnRequest__](xref:Unity.NetCode.PredictedGhostSpawnRequest)     | This instance is not a ghost received from the server, but a request to predictively spawn a ghost (which the client expects the server to spawn authoritatively, soon). Prefab entity references on clients will have this component added automatically, so anything they spawn themselves will be by default predict spawned. | 
| [__GhostOwner__](xref:Unity.NetCode.GhostOwner)                                     | Identifies the owner of a ghost, specified as a "Network Id".                                                                                                                                                                                                                                                                    | Optional                                                                |
| [__GhostOwnerIsLocal__](xref:Unity.NetCode.GhostOwnerIsLocal)                       | An enableable tag component used to track if a ghost (with an owner) is owned by the local host or not.                                                                                                                                                                                                                          | Optional                                                                |
| [__AutoCommandTarget__](xref:Unity.NetCode.AutoCommandTarget)                       | Automatically send all `ICommandData` if the ghost is owned by the current connection, `AutoCommandTarget.Enabled` is true, and the ghost is predicted.                                                                                                                                                                          | Optional                                                                |
| [__SubSceneGhostComponentHash__](xref:Unity.NetCode.SubSceneGhostComponentHash)     | The hash of all pre-spawned ghosts in a subscene, used for sorting and grouping. This is a shared component.                                                                                                                                                                                                                     | Only pre-spawned                                                        |
| [__PreSpawnedGhostIndex__](xref:Unity.NetCode.PreSpawnedGhostIndex)                 | Unique index of a pre-spawned ghost within a subscene.                                                                                                                                                                                                                                                                           | Only pre-spawned                                                        |
| [__PrespawnGhostBaseline__](xref:Unity.NetCode.PrespawnGhostBaseline)               | The snapshot data a pre-spawned ghost had in the scene data. Used as a fallback baseline.                                                                                                                                                                                                                                        | Only pre-spawned                                                        |
| [__GhostPrefabRuntimeStrip__](xref:Unity.NetCode.GhostPrefabRuntimeStrip)           | Added to prefabs and pre-spawned during conversion to client and server data to trigger runtime stripping of component.                                                                                                                                                                                                          | Only on prefabs in client and server scenes before they are initialized |
| __PrespawnSceneExtracted__                                                          | Component present in editor on the scene section entity, when the sub-scene is open for edit. Intended for internal use only.                                                                                                                                                                                                    | Only in Editor                                                          |
| [__PreSerializedGhost__](xref:Unity.NetCode.PreSerializedGhost)                     | Enable pre-serialization for a ghost. Added at conversion time based on ghost settings.                                                                                                                                                                                                                                          | Only ghost using pre-serialization                                      |
| [__SwitchPredictionSmoothing__](xref:Unity.NetCode.SwitchPredictionSmoothing)       | Added temporarily when using "Prediction Switching" (i.e. when switching a ghost from predicted to interpolated (or vice-versa), with a transition time to handle transform smoothing.                                                                                                                                           | Only ghost in the process of switching prediction mode                  |
| [__PrefabDebugName__](xref:Unity.NetCode.PrefabDebugName)                           | Name of the prefab, used for debugging.                                                                                                                                                                                                                                                                                          | Only on prefabs when `NETCODE_DEBUG` is enabled                         |

### Placeholder ghost
When a ghost is received but is not yet supposed to be spawned the client will create a placeholder to store the data until it is time to spawn it. The placeholder ghosts only exist on clients and have these components

| Component                                                                     | Description                                                               | Condition                |
|-------------------------------------------------------------------------------|---------------------------------------------------------------------------|--------------------------|
| [__GhostInstance__](xref:Unity.NetCode.GhostInstance)                         | Identifying an entity as a ghost.                                         |
| [__PendingSpawnPlaceholder__](xref:Unity.NetCode.PendingSpawnPlaceholder)     | Identify the ghost as a placeholder and not a proper ghost.               |
| [__SnapshotData__](xref:Unity.NetCode.SnapshotData)                           | A buffer with meta data about the snapshots received from the server.     | Client only              |
| [__SnapshotDataBuffer__](xref:Unity.NetCode.SnapshotDataBuffer)               | A buffer with the raw snapshot data received from the server.             |
| [__SnapshotDynamicDataBuffer__](xref:Unity.NetCode.SnapshotDynamicDataBuffer) | A buffer with the raw snapshot data for buffers received from the server. | Ghosts with buffers only |


### Client-Only physics proxy
it is possible to make "physically simulated" ghosts interact with physics objects present only on the client-only physics world (e.g. particles, debris, cosmetic environmental destruction), by spawning kinematic copies of the colliders present on the predicted, simulated ghosts, synced to them.
Note, however, that this synchronisation can only go one way. I.e. Client-only physics worlds cannot influence the server authoritative ghost (by definition).

| Component                                                                   | Description                                                                                                          |
|-----------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------|
| [__CustomPhysicsProxyDriver__](xref:Unity.NetCode.CustomPhysicsProxyDriver) | A component that reference the ghost which drive the proxy and let configure how the ghost and the proxy are synced. |

## RPC

RPC entities are created with a send request in order to send RPCs. When they are received, the system will create entities with the RPC component, and a "receive request" (i.e. an `ReceiveRpcCommandRequest` component).

| Component                                                                                    | Description                                                                                     | Condition                                                                                                                                                        |
|----------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [__IRpcCommand__](xref:Unity.NetCode.IRpcCommand)                                            | A specific implementation of the IRpcCommand interface.                                         |                                                                                                                                                                  |
| [__SendRpcCommandRequest__](xref:Unity.NetCode.SendRpcCommandRequest)       | Specify that this RPC is to be sent (and thus the RPC entity destroyed).                        | Added by game logic, only for sending. Deleted automatically.                                                                                                    |
| [__ReceiveRpcCommandRequest__](xref:Unity.NetCode.ReceiveRpcCommandRequest) | Specify that this RPC entity has been received (and thus this RPC entity was recently created). | Added automatically, only for receiving. Must be processed and then deleted by game-code, or you'll leak entities into the world. See `WarnAboutStaleRpcSystem`. |

### Netcode RPCs

| Component                              | Description                                                                                                                                         |
|----------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------|
| __RpcSetNetworkId__                    | Special RPC only sent on connect.                                                                                                                   |
| __ClientServerTickRateRefreshRequest__ | Special RPC only sent on connect.                                                                                                                   |
| __StartStreamingSceneGhosts__          | Sent from client to server when a subscene has been loaded. Used to instruct the server to start sending prespawned ghosts for that scene.          |
| __StopStreamingSceneGhosts__           | Sent from client to server when a subscene will be unloaded. Used to instruct the server to stop sending prespawned ghosts that live in that scene. |

### CommandData

Every connection which is receiving commands from a client needs to have an entity to hold the command data. This can be a ghost, the connection entity itself or some other entity.

| Component                                                                             | Description                                                                                                                                                                                          | Condition                           |
|---------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------|
| [__ICommandData__](xref:Unity.NetCode.ICommandData)                                   | A specific implementation of the ICommandData interface. This can be added to any entity, the connections `CommandTarget` must point to an entity containing this.                          |                                     |
| [__CommandDataInterpolationDelay__](xref:Unity.NetCode.CommandDataInterpolationDelay) | Optional component used to access the interpolation delay, in order to implement lag compensation on the server. Also exists on predicted clients, but always has an interpolation delay of 0 there. | Added by game logic, predicted only |

## SceneSection
When using pre-spawned ghosts Netcode will add some components to the SceneSection entity containing the ghosts.

| Component                                                                                   | Description                                                                                  | Condition                     |
|---------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------|-------------------------------|
| [__SubSceneWithPrespawnGhosts__](xref:Unity.NetCode.SubSceneWithPrespawnGhosts)             | Added during conversion to track which section contains pre-spawned ghosts.                  |                               |
| [__SubSceneWithGhostCleanup__](xref:Unity.NetCode.SubSceneWithGhostCleanup)          | Used to track unloading of scenes.                                                           | Processed sections.           |
| [__PrespawnsSceneInitialized__](xref:Unity.NetCode.PrespawnsSceneInitialized)               | Tag to specify that a section has been processed.                                            | Processed sections.           |
| [__SubScenePrespawnBaselineResolved__](xref:Unity.NetCode.SubScenePrespawnBaselineResolved) | Tag to specify that a section has resolved baselines. This is a partially initialized state. | Partially processed sections. |

## Netcode created singletons

### PredictedGhostSpawnList
A singleton with a list of all predicted spawned ghosts which are waiting for a ghost from the server. This is needed when writing logic matching an incoming ghost with a pre-spawned one.

| Component                                                                 | Description                                 |
|---------------------------------------------------------------------------|---------------------------------------------|
| [__PredictedGhostSpawnList__](xref:Unity.NetCode.PredictedGhostSpawnList) | A tag for finding the predicted spawn list. |
| [__PredictedGhostSpawn__](xref:Unity.NetCode.PredictedGhostSpawn)         | A lis of all predictively spawned ghosts.   |

### Ghost Collection
| Component                                                                                 | Description                                                                                                                                                                                                                                           |
|-------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [__GhostCollection__](xref:Unity.NetCode.GhostCollection)                                 | Identify the singleton containing ghost prefabs.                                                                                                                                                                                                      |
| [__GhostCollectionPrefab__](xref:Unity.NetCode.GhostCollectionPrefab)                     | A list of all ghost prefabs which can be instantiated.                                                                                                                                                                                                |
| [__GhostCollectionPrefabSerializer__](xref:Unity.NetCode.GhostCollectionPrefabSerializer) | A list of serializers for all ghost prefabs. The index in this list is identical to `GhostCollectionPrefab`, but it can temporarily have fewer entries when a prefab is loading. This references a range in the `GhostCollectionComponentIndex` list. |
| [__GhostCollectionComponentType__](xref:Unity.NetCode.GhostCollectionComponentType)       | The set of serializers in the `GhostComponentSerializer.State` which can be used for a given type. This is used internally to setup the `GhostCollectionPrefabSerializer`.                                                                            |
| [__GhostCollectionComponentIndex__](xref:Unity.NetCode.GhostCollectionComponentIndex)     | A list of mappings from prefab serializer index to a child entity index and a `GhostComponentSerializer.State` index. This mapping is there to avoid having to duplicate the full serialization state for each prefab using the same component.       |
| [__GhostComponentSerializer.State__](xref:Unity.NetCode.GhostComponentSerializer.State)   | Serialization state - including function pointers for serialization - for a component type and variant. There can be more than one entry for a given component type if there are serialization variants.                                              |

### Spawn queue
| Component                                                                   | Description                                                                                                                                                                                                                                                           |
|-----------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [__GhostSpawnQueueComponent__](xref:Unity.NetCode.GhostSpawnQueueComponent) | Identifier for the ghost spawn queue.                                                                                                                                                                                                                                 |
| [__GhostSpawnBuffer__](xref:Unity.NetCode.GhostSpawnBuffer)                 | A list of ghosts in the spawn queue. This queue is written by the `GhostReceiveSystem` and read by the `GhostSpawnSystem`. A classification system running between those two can change the type of ghost to spawn and match incoming ghosts with pre-spawned ghosts. |
| [__SnapshotDataBuffer__](xref:Unity.NetCode.SnapshotDataBuffer)             | Raw snapshot data for the new ghosts in the `GhostSpawnBuffer`.                                                                                                                                                                                                       |

### NetworkProtocolVersion
| Component                                                               | Description                                                                                                                                                                                     |
|-------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [__NetworkProtocolVersion__](xref:Unity.NetCode.NetworkProtocolVersion) | The network protocol version for RPCs, ghost component serializers, netcode version and game version. At connection time netcode will validate that the client and server has the same version. |

### PrespawnGhostIdAllocator
| Component                                                           | Description                                                                                                                      |
|---------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------|
| [__PrespawnGhostIdRange__](xref:Unity.NetCode.PrespawnGhostIdRange) | The set of ghost ids associated with a subscene. Used by the server to map prespawned ghosts for a subscene to proper ghost ids. |

### PrespawnSceneLoaded
This singleton is a special kind of ghost without a prefab asset.

| Component                                                         | Description                                                                                 |
|-------------------------------------------------------------------|---------------------------------------------------------------------------------------------|
| [__PrespawnSceneLoaded__](xref:Unity.NetCode.PrespawnSceneLoaded) | The set of scenes with pre-spawned ghosts loaded by the server. This is ghosted to clients. |

### MigrationTicket
| Component                                                 | Description                                                                                  |
|-----------------------------------------------------------|----------------------------------------------------------------------------------------------|
| [__MigrationTicket__](xref:Unity.NetCode.MigrationTicket) | Created in the new world when using world migration, triggers the restore part of migration. |

### SmoothingAction
| Component                                                 | Description                                                                                     |
|-----------------------------------------------------------|-------------------------------------------------------------------------------------------------|
| [__SmoothingAction__](xref:Unity.NetCode.SmoothingAction) | Singleton created when a smothing action is registered in order to enable the smoothing system. |

### NetworkTimeSystemData
| Component                  | Description                                                                                                                                |
|----------------------------|--------------------------------------------------------------------------------------------------------------------------------------------|
| __NetworkTimeSystemData__  | Internal singleton, used to store the state of the network time system.                                                                    |
| __NetworkTimeSystemStats__ | Internal singleton, that track the time scaling applied to the predicted and interpolated tick. <br/>Used to report stats to net debugger. |

### NetworkTime
| Component                                         | Description                                                                                         |
|---------------------------------------------------|-----------------------------------------------------------------------------------------------------|
 | [__NetworkTime__](xref:Unity.NetCode.NetworkTime) | Singleton component that contains all the timing characterist of the client/server simulation loop. |

### NetDebug
| Component                                   | Description                                                              |
|---------------------------------------------|--------------------------------------------------------------------------|
| [__NetDebug__](xref:Unity.NetCode.NetDebug) | Singleton that can be used for debug log and managing the logging level. |

### NetworkStreamDriver
| Component                                                         | Description                                                                                                                                           |
|-------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------|
| [__NetworkStreamDriver__](xref:Unity.NetCode.NetworkStreamDriver) | Singleton that can hold a reference to the NetworkDriverStore and that should be used to easily listening for new connection or connecting to server. |

### RpcCollection

### GhostPredictionSmoothing
| Component                    | Description                                                                            |
|------------------------------|----------------------------------------------------------------------------------------|
| __GhostPredictionSmoothing__ | Singleton used to register the smoothing action used to correct the prediction errors. |

### GhostPredictionHistoryState
| Component                       | Description                                                                                |
|---------------------------------|--------------------------------------------------------------------------------------------|
| __GhostPredictionHistoryState__ | Internal singleton that contains the last predicted full tick state of all predicted ghost |

### GhostSnapshotLastBackupTick
| Component                       | Description                                                                                                                    |
|---------------------------------|--------------------------------------------------------------------------------------------------------------------------------|
| __GhostSnapshotLastBackupTick__ | Internal singleton that contains the last full tick for which a snapshot backup is avaiable. Only present on the client world. |

### GhostStats
| Component                               | Description                                                         |
|-----------------------------------------|---------------------------------------------------------------------|
| __GhostStats__                          | State if the NetDbg tools is connected or not.                      |
| __GhostStatsCollectionCommand__         | Internal stats data for commands.                                   |
| __GhostStatsCollectionSnapshot__        | Internal stats data used to track sent/received snapshot data.      |
| __GhostStatsCollectionPredictionError__ | Record the prediction stats for various ghost/component types pair. |
| __GhostStatsCollectionMinMaxTick__      |                                                                     |
| __GhostStatsCollectionData__>           | Contains internal data pools and other stats system related states. |

### GhostSendSystemData
| Component                                                         | Description                                                                       |
|-------------------------------------------------------------------|-----------------------------------------------------------------------------------|
| [__GhostSendSystemData__](xref:Unity.NetCode.GhostSendSystemData) | Singleton entity that contains all the tweakable settings for the GhostSendSystem |

### SpawnedGhostEntityMap
| Component                                                             | Description                                                                       |
|-----------------------------------------------------------------------|-----------------------------------------------------------------------------------|
| [__SpawnedGhostEntityMap__](xref:Unity.NetCode.SpawnedGhostEntityMap) | Singleton that contains the last predicted full tick state of all predicted ghost |


## User create singletons (settings)

### ClientServerTickRate
| Component                                                           | Description                                                                                                                  |
|---------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------|
| [__ClientServerTickRate__](xref:Unity.NetCode.ClientServerTickRate) | The tick rate settings for the server. Automatically sent and set on the client based on the values specified on the server. |

### ClientTickRate
| Component                                               | Description                                                                                                                                                                                        |
|---------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [__ClientTickRate__](xref:Unity.NetCode.ClientTickRate) | The tick rate settings for the client which are not controlled by the server (interpolation time etc.). Use the defaults from `NetworkTimeSystem.DefaultClientTickRate` instead of default values. |

### LagCompensationConfig
| Component                                                             | Description                                                                                                                                                                         |
|-----------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [__LagCompensationConfig__](xref:Unity.NetCode.LagCompensationConfig) | Configuration for the `PhysicsWorldHistory` system which is used to implement lag compensation on the server. If the singleton does not exist `PhysicsWorldHistory` will no be run. |

### GameProtocolVersion
| Component                                                         | Description                                                                                                                                                                                   |
|-------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [__GameProtocolVersion__](xref:Unity.NetCode.GameProtocolVersion) | The game specific version to use for protcol validation on connection. If this does not exist 0 will be used, but the protocol will still validate netcode version, ghost components and rpcs |

### GhostImportance
| Component                                                         | Description                                              |
|-------------------------------------------------------------------|----------------------------------------------------------|
| [__GhostImportance__](xref:Unity.NetCode.GhostDistanceImportance) | Singleton component used to control importance settings. |

### GhostDistanceData
| Component                                                           | Description                                                                                                    |
|---------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------|
| [__GhostDistanceData__](xref:Unity.NetCode.GhostDistanceImportance) | Settings for distance based importance. If the singleton does not exist distance based importance is not used. |

### Predicted Physics
| Component                                                                             | Description                                                                                                  |
|---------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------|
| [__PredictedPhysicsNonGhostWorld__](xref:Unity.NetCode.PredictedPhysicsNonGhostWorld) | Singleton component that declare which physics world to use for simulating the client-only physics entities. |

### NetCodeDebugConfig

| Component                                                       | Description                                                                                                                                                                                      |
|-----------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [__NetCodeDebugConfig__](xref:Unity.NetCode.NetCodeDebugConfig) | Create a singleton with this to configure log level and packet dump for all connections. See `EnabledPacketLogging` on the connection for enabling packet dumps for a subset of the connections. |

### DisableAutomaticPrespawnSectionReporting

| Component                                                                                                   | Description                                                                                                                                                                                                                      |
|-------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [__DisableAutomaticPrespawnSectionReporting__](xref:Unity.NetCode.DisableAutomaticPrespawnSectionReporting) | Disable the automatic tracking of which sub-scenes the client has loaded. When creating this singleton you must implement custom logic to make sure the server does not send pre-spawned ghosts which the client has not loaded. |
