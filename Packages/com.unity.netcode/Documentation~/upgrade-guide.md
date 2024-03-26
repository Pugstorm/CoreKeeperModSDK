# Upgrading from Entities 0.51 to 1.0

The Netcode for Entities introduces many changes and the upgrade process from 0.51 to 1.0 can be a little laborious.

## Classed renamed and moved in other assemblies

* The following components have been renamed and will be automatically updated:

| Original Name                             | New Name                         |
|-------------------------------------------|----------------------------------|
| NetworkSnapshotAckComponent               | NetworkSnapshotAck               |
| IncomingSnapshotDataStreamBufferComponent | IncomingSnapshotDataStreamBuffer |
| IncomingRpcDataStreamBufferComponent      | IncomingRpcDataStreamBuffer      |
| OutgoingRpcDataStreamBufferComponent      | OutgoingRpcDataStreamBuffer      |
| IncomingCommandDataStreamBufferComponent  | IncomingCommandDataStreamBuffer  |
| OutgoingCommandDataStreamBufferComponent  | OutgoingCommandDataStreamBuffer  |
| NetworkIdComponent                        | NetworkId                        |
| CommandTargetComponent                    | CommandTarget                    |
| GhostComponent                            | GhostInstance                    |
| GhostChildEntityComponent                 | GhostChildEntity                 |
| GhostOwnerComponent                       | GhostOwner                       |
| PredictedGhostComponent                   | PredictedGhost                   |
| GhostTypeComponent                        | GhostType                        |
| SharedGhostTypeComponent                  | GhostTypePartition               |
| GhostCleanupComponent                     | GhostCleanup                     |
| GhostPrefabMetaDataComponent              | GhostPrefabMetaData              |
| PredictedGhostSpawnRequestComponent       | PredictedGhostSpawnRequest       |
| PendingSpawnPlaceholderComponent          | PendingSpawnPlaceholder          |
| ReceiveRpcCommandRequestComponent         | ReceiveRpcCommandRequest         |
| SendRpcCommandRequestComponent            | SendRpcCommandRequest            |

* The `DefaultUserParams` has been renamed to `DefaultSmoothingActionUserParams`.
* The `DefaultTranslateSmoothingAction` has been renamed to `DefaultTranslationSmoothingAction`.
* `ClientServerTickRate.MaxSimulationLongStepTimeMultiplier` has been renamed to `ClientServerTickRate.MaxSimulationStepBatchSize`
* The `NetworkCompressionModel` has been moved to Unity.Collection and renamed to `StreamCompressionModel`.
* The utility method `GhostPredictionSystemGroup.ShouldPredict` has been moved to the `PredictedGhostComponent`.
* `GhostComponentAttribute.OwnerPredictedSendType` has been renamed to `GhostComponentAttribute.SendTypeOptimization`.
* `ClientServerTickRate.MaxSimulationLongStepTimeMultiplier` is renamed to `ClientServerTickRate.MaxSimulationStepBatchSize`.
* `GhostPredictionSystemGroup` has been renamed to `PredictedSimulationSystemGroup`.

## PredictedTick, ServerTick and in general time information.
All the information in regards the current simulated tick MUST be retrieved from the `NetworkTim` singleton. In particular:
* The `GhostPredictionSystemGroup.PredictedTick` has been removed. 
You must always use the `NetworkTime.ServerTick` instead, that will always correcly reflect the current predicted tick when inspected inside the prediction loop.
* The `GhostPredictionSystemGroup.IsFinalPredictionTick` has been removed. Use the `NetworkTime.IsFinalPredictionTick` property instead.
* The `ClientSimulationSystemGroup ServerTick`, `ServerTickFraction`, `InterpolationTick` and `InterpolationTickFraction` has been removed. You can retrieve the same properties from the `NetworkTime` singleton. 

Please refer to the `NetworkTime` component documentation for further information about the different timing properties and the flags behaviours.

## Use the new singletons to access APIs and shared data.
All Netcode systems (apart some exception) should be considered stateless. All the public and accessible data is store inside entities singletons. We removed many APIs from system and moved instead into this new singleton components:

* When using the netcode logging system calls to `GetExistingSystem<NetDebugSystem>().NetDebug` must be replaced with `GetSingleton<NetDebug>()`, or `GetSingletonRW<NetDebug>` if you are changing the log level.
* The `Connect` and `Listen` methods have moved to the `NetworkStreamDriver` singleton.
* `GhostSimulationSystemGroup.SpawnedGhostEntityMap` has been replaced by a `SpawnedGhostEntityMap` singleton.
* The ghost relevancy map and mode has moved from the `GhostSendSystem` to a `GhostRelevancy` singleton.
* `GhostCountOnServer` and `GhostCountOnClient` has been moved from `GhostReceiveSystem` to a singleton API `GhostCount`
* The API to register smoothing functions for prediction has moved from the `GhostPredictionSmoothingSystem` system to the `GhostPredictionSmoothing` singleton.
* The API to register RPCs and get RPC queues has moved from `RpcSystem` to the singleton `RpcCollection`
* Calls to `GetExistingSystem<GhostSimulationSystemGroup>().SpawnedGhostEntityMap` must be replaced with `GetSingleton<SpawnedGhostEntityMap>().Value`. Waiting for or setting `LastGhostMapWriter` is no longer required and should be removed.
* Calls to `GetExistingSystem<GhostSendSystem>().GhostRelevancySet` and `GetExistingSystem<GhostSendSystem>().GhostRelevancyMode` must be replaced with `GetSingletonRW<GhostRelevancy>.GhostRelevancySet` and `GetSingletonRW<GhostRelevancy>.GhostRelevancyMode`. Waiting for or setting `GhostRelevancySetWriteHandle` is no longer required and should be removed.
* Calls to `GetExistingSystem<NetworkStreamReceiveSystem>().Connect` and `GetExistingSystem<NetworkStreamReceiveSystem>().Listen` must be replaced with `GetSingletonRW<NetworkStreamDriver>.Connect` and `GetSingletonRW<NetworkStreamDriver>.Listen`.

## Changes in visiblity and depracted APIs.
* The `LagCompensationConfig` has been removed. Use the unified`NetCodePhysicsConfig` authoring component instead of using the `LagCompensationConfig` authoring component to enable lag compensation.
* Any calls to the static `RpcSystem.DynamicAssemblyList` should be replaced with instanced calls to the property with the same name. Ensure you do so during world creation, before `RpcSystem.OnUpdate` is called. You can see an exaple of this in our NetcodeSamples.
* Any editor-only calls to `ClientServerBootstrap.RequestedAutoConnect` should be replaced with `ClientServerBootstrap.TryFindAutoConnectEndPoint`, which handles all `PlayTypes`.

* The `GhostCollectionSystem.CreatePredictedSpawnPrefab` API has been removed as clients will now automatically have predict spawned ghost prefabs set up for them. They can instantiate prefabs the normal way and don't need to call this API.
* The `PrespawnsSceneInitialized`, `SubScenePrespawnBaselineResolved`, `PrespawnGhostBaseline`, `PrespawnSceneLoaded`, `PrespawnGhostIdRange` have internal visibility.
* The `PrespawnSubsceneElementExtensions` has internal visibility.
* The `LiveLinkPrespawnSectionReference` are now internal. Used only in the Editor as a work around to entities conversion limitation. It should not be a public component that can be added by the user.
* The `GhostCollectionSystem.CreatePredictedSpawnPrefab` API has been deprected. The clients will now automatically have predict spawned ghost prefabs set up for them and just instantiate prefabs the normal way.
* The static bool `RpcSystem.DynamicAssemblyList` has been removed, replaced by a non-static property with the same name.
* `ClientServerBootstrap.RequestedAutoConnect` (an editor only property) has been replaced with `ClientServerBootstrap.TryFindAutoConnectEndPoint`.
* `ThinClientComponent` has been removed, use `World.IsThinClient()` instead.
* The `NetworkStreamDisconnected` component has been removed, add a `ConnectionState` component to connections you want to detect disconnects for and use a reactive system.
* The `CommandReceiveClearSystem` and `CommandSendPacketSystem` are not internal
* The `StartStreamingSceneGhosts` and `StopStreamingSceneGhosts` to be internal RPC. If user wants to customise the prespawn scene flow, they need to add their own RPC.

## New way to pass templates to source generator
* Netcode source generator templates should now use the passed to the generators using `additional files`. The template must have a `NetCodeSourceGenerator.additionalfile` extension, and should be identified using a unique id, that must be present in the first line of the template. </br>
  Find more information in the [templates](ghost-types-templates.md#writing-the-template) and [templates](ghost-types-templates.md#registering-your-new-template-with-netcode) documentation.


## Netcode groups, world filtering and detect world types.
* Use `IsClient`, `IsServer` and `IsThinClient` helper methods on `World` and `WorldUnmanaged` to inspect if a world is client, server or thin-client respectively.
* The netcode specific top-level system groups and `[UpdateInWorld]` have been removed, the replacement is `[WorldSystemFilter]` and the mappings are

| Old                                                                 | New                                                                                                                                                           |
|---------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `[UpdateInGroup(typeof(ClientInitializationSystemGroup))]`          | `[UpdateInGroup(typeof(InitializationSystemGroup))][WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]`                                              |  
| `[UpdateInGroup(typeof(ClientSimulationSystemGroup))]`              | `[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]`                                                                                                | 
| `[UpdateInGroup(typeof(ClientPresentationSystemGroup))]`            | `[UpdateInGroup(typeof(PresentationSystemGroup)]`                                                                                                             | 
| `[UpdateInGroup(typeof(ServerInitializationSystemGroup))]`          | `[UpdateInGroup(typeof(InitializationSystemGroup))][WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]`                                              | 
| `[UpdateInGroup(typeof(ServerSimulationSystemGroup))]`              | `[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]`                                                                                                | 
| `[UpdateInGroup(typeof(ClientAndServerInitializationSystemGroup))]` | `[UpdateInGroup(typeof(InitializationSystemGroup))][WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation&#124;WorldSystemFilterFlags.ClientSimulation)]` |    
 | `[UpdateInGroup(typeof(ClientAndServerSimulationSystemGroup))]`     | `[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation&#124;WorldSystemFilterFlags.ClientSimulation)]`                                                   |    
| `[UpdateInWorld(TargetWorld.Client)]`                               | `[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]`                                                                                                | 
| `[UpdateInWorld(TargetWorld.Server)]`                               | `[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]`                                                                                                |
| `[UpdateInWorld(TargetWorld.ClientAndServer)]`                      | `[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation&#124;WorldSystemFilterFlags.ClientSimulation)]`                                                   |    
| `[UpdateInWorld(TargetWorld.Default)]`                              | `[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]`                                                                                                 |    
| `if (World.GetExistingSystem<ServerSimulationSystemGroup>()!=null)` | `if (World.IsServer())`                                                                                                                                       |    
| `if (World.GetExistingSystem<ClientSimulationSystemGroup>()!=null)` | `if (World.IsClient())`                                                                                                                                       | 

## Major changes for ghost field serialization 
* All child entities in Ghosts now default to the `DontSerializeVariant` as serializing child ghosts is relatively expensive (due to poor 'locality of reference' of child entities in other chunks, and the random-access nature of iterating child entities). Thus, `GhostComponentAttribute.SendDataForChildEntity = false` is now the default, and you'll need to set this flag to true for all types that should be sent for children. If you'd like to replicate hierarchies, we strongly encourage you to create multiple ghost prefabs, with custom, faked transform parenting logic that keeps the hierarchy flat. Explicit child hierarchies should only be used if the snapshot updates of one hierarchy must be in sync.
* `RegisterDefaultVariants` has changed signature to now use a `Rule`. This forces users to be explicit about whether or not they want their user-defined defaults to apply to child entities too.
* All `GhostAuthoringComponent` `ComponentOverrides` have been clobbered during the upgrade (apologies!). Please re-apply all `ComponentOverrides` via the new (optional) `GhostAuthoringInspectionComponent`.
* Inside your `RegisterDefaultVariants` method, replace all `defaultVariants.Add(new ComponentType(typeof(SomeType)), typeof(SomeTypeDefaultVariant));` with `defaultVariants.Add(new ComponentType(typeof(SomeType)), Rule.OnlyParent(typeof(SomeTypeDefaultVariant)));`, unless you _also_ want this variant to be applied to children (in which case, use `Rule.ParentAndChildren(typeof(SomeTypeDefaultVariant))`). 
Caveat: Prefer to use attributes wherever possible, as this "manual" form of overriding should only be used for one-off differences that you're unable to express via attributes.




