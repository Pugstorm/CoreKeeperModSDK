# Optimizations

Netcode optimizations fall into two categories:

* The amount of CPU time spent on the client and the server (for example, CPU time being spent on serializing components as part of the GhostSendSystem)
* The size and amount of snapshots (which depends on how much and how often Netcode sends data across the network)

This page will describe different strategies for improving both.

## Optimization Mode

Optimization Mode is a setting available in the `Ghost Authoring Component` as described in the [Ghost Snapshots](ghost-snapshots.md) documentation. The setting changes how often Netcode resends the `GhostField` on a spawned entity. It has two modes: **Dynamic** and **Static**. For example, if you spawn objects that never move, you can set Optimization Mode to **Static** to ensure Netcode doesn't resync their transform.

When a GhostField change Netcode will send the changes regardless of this setting. We are optimizing the amount and the size of the snapshots sent.

* `Dynamic` optimization mode is the default mode and tells Netcode that the ghost will change often. It will optimize the ghost for having a small snapshot size when changing and when not changing.
* `Static` optimization mode tells Netcode that the ghost will change rarely. It will not optimize the ghost for having a small snapshot size when changing, but it will not send it at all when not changing.

## Reducing Prediction CPU Overhead

### Physics Scheduling

Context: [Prediction](prediction.md) & [Physics](physics.md).
As the `PhysicsSimulationGroup` is run inside the `PredictedFixedStepSimulationSystemGroup`, you may encounter scheduling overhead when running at a high ping (i.e. re-simulating 20+ frames).
You can reduce this scheduling overhead by forcing the majority of Physics work onto the main thread. Add a `PhysicsStep` singleton to your scene, and set `Multi Threaded` to `false`.
Of course, we are always exploring ways to reduce scheduling overhead._

### Prediction Switching

The cost of prediction increases with each predicted ghost. 
Thus, as an optimization, we can opt-out of predicting a ghost given some set of criteria (e.g. distance to your clients character controller).
See [Prediction Switching](prediction.md#prediction-switching) for details.

## Serialization cost

When a `GhostField` changes, we serialize the data and synchronize it over the network. If a job has write access to a `GhostField`, Netcode will check if the data changed.
First, it serializes the data and compares it with the previous synchronized version of it. If the job did not change the `GhostField`, it discards the serialized result.
For this reason, it is important to ensure that no jobs have write access to data if it will not change (to remove this hidden CPU cost).

## Relevancy

"Ghost Relevancy" (a.k.a. "Ghost Filtering") is a server feature, allowing you to set conditions on whether or not a specific ghost entity is replicated to (i.e. spawned on) a specific client. You can use this to:
* Set a max replication distance (e.g. in an open world FPS), or replication filtering based on which gameplay zone they're in.
* Create a server-side, anti-cheat-capable "Fog of War" (preventing clients from knowing about entities that they should be unable to see).
* Only allow specific clients to be notified of a ghosts state (e.g. an item being dropped in a hidden information game).
* To create client-specific (i.e. "single client") entities (e.g. in MMO games, NPCs that are only visible to a player when they've completed some quest condition. You could even create an AI NPC companion only for a specific player or party, triggered by an escort mission start).
* Temporarily pause all replication for a client, while said client is in a specific state (e.g. post-death, when the respawn timer is long, and the user is unable to spectate).

Essentially: Use Relevancy to avoid replicating entities that the player can neither see, nor interact with.

The `GhostRelevancy` singleton component contains these controls:

The `GhostRelevancyMode` field chooses the behaviour of the entire Relevancy subsystem:
* **Disabled** - The default. No relevancy will applied under any circumstances.
* **SetIsRelevant** - Only ghosts added to relevancy set (`GhostRelevancySet`, below) are considered "relevant to that client", and thus serialized for the specified connection (where possible, obviously, as eventual consistency and importance scaling rules still apply (see paragraphs below)).
_Note that applying this setting will cause **all** ghosts to default to **not be replicated** to **any** client. It's a useful default when it's rare or impossible for a player to be viewing the entire world._
* **SetIsIrrelevant** - Ghosts added to relevancy set (`GhostRelevancySet`, below) are considered "not-relevant to that client", and thus will be not serialized for the specified connection. In other words: Set this mode if you want to specifically ignore specific entities for a given client.

`GhostRelevancySet` is the map that stores a these (connection, ghost) pairs. The behaviour (of adding a (connection, ghost) item) is determined according to the above rule.
`DefaultRelevancyQuery` is a global rule denoting that all ghost chunks matching this query are always considered relevant to all connections (unless you've added the ghosts in said chunk to the `GhostRelevancySet`). This is useful for creating general relevancy rules (e.g. "the entities in charge of tracking player scores are always relevant"). `GhostRelevancySet` takes precedence over this rule. See the [example](https://github.com/Unity-Technologies/EntityComponentSystemSamples/tree/master/NetcodeSamples/Assets/Samples/Asteroids/Authoring/Server/SetAlwaysRelevantSystem.cs) in Asteroids.
```c#
var relevancy = SystemAPI.GetSingletonRW<GhostRelevancy>();
relevancy.ValueRW.DefaultRelevancyQuery = GetEntityQuery(typeof(AsteroidScore));
```
> [!NOTE]~~~~
> If a ghost has been replicated to a client, then is set to **not be** relevant to said client, that client will be notified that this entity has been **destroyed**, and will do so. This misnomer can be confusing, as the entity being despawned does not imply the server entity was destroyed.
> Example: Despawning an enemy monster in a MOBA because it became hidden in the Fog of War should not trigger a death animation (nor S/VFX). Thus, use some other data to notify what kind of entity-destruction state your entity has entered (e.g. enabling an `IsDead`/`IsCorpse` component).

## Limiting Snapshot Size

* The per-connection component `NetworkStreamSnapshotTargetSize` will stop serializing entities into a snapshot if/when the snapshot goes above the specified byte size (`Value`). This is a way to try to enforce a (soft) limit on per-connection bandwidth consumption.

> [!NOTE]
> Snapshots do have a minimum send size. This is because - per snapshot - we ensure that _some_ new and destroyed entities are replicated, and we ensure that at least one ghost has been replicated.

* `GhostSendSystemData.MaxSendEntities` can be used to limit the max number of entities added to any given snapshot.

* Similarly, `GhostSendSystemData.MaxSendChunks` can be used to limit the max number of chunks added to any given snapshot.

* `GhostSendSystemData.MinSendImportance` can be used to prevent a chunks entities from being sent too frequently.
  _For example: A "DroppedItems" ghostType can be told to only replicate on every tenth snapshot, by setting `MinSendImportance` to 10, and dropped item `Importance` to 1._
  `GhostSendSystemData.FirstSendImportanceMultiplier` can be used to bump the priority of chunks containing new entities, to ensure they're replicated quickly, regardless of the above setting.

> [!NOTE]
> The above optimizations are applied on the per-chunk level, and they kick in **_after_** a chunks contents have been added to the snapshot. Thus, in practice, real send values will be higher.
> Example: `MaxSendEntities` is set to 100, but you have two chunks, each with 99 entities. Thus, you'd actually send 198 entities.

## Importance Scaling

The server operates on a fixed bandwidth and sends a single packet with snapshot data of customizable size on every network tick. It fills the packet with the entities of the highest importance. Several factors determine the importance of the entities: you can specify the base importance per ghost type, which Unity then scales by age. You can also supply your own method to scale the importance on a per-chunk basis.

Once a packet is full, the server sends it and the remaining entities are missing from the snapshot. Because the age of the entity influences the importance, it is more likely that the server will include those entities in the next snapshot. Netcode calculates importance only per chunk, not per entity.

### Set-up required

Below is an example of how to set up the built-in distance-based importance scaling. If you want to use a custom importance implementation, you can reuse parts of the built-in solution or replace it with your own.

### GhostImportance

[GhostImportance](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.GhostImportance.html) is the configuration component for setting up importance scaling. `GhostSendSystem` invokes the `ScaleImportanceFunction` only if the `GhostConnectionComponentType` and `GhostImportanceDataType` are created.

The fields you can set on this is:
- `ScaleImportanceFunction` allows you to write and assign a custom scaling function (to scale the importance, with chunk granularity).
- `GhostConnectionComponentType` is the type added per-connection, allowing you to store per-connection data needed in the scaling calculation.
- `GhostImportanceDataType` is an optional singleton component, allowing you to pass in any of your own static data necessary in the scaling calculation.
- `GhostImportancePerChunkDataType` is the shared component added per-chunk, storing any chunk-specific data used in the scaling calculation.

Flow: The function pointer is invoked by the `GhostSendSystem` for each chunk, and returns the importance scaling for the entities contained within this chunk. The signature of the method is of the delegate type `GhostImportance.ScaleImportanceDelegate`.
The parameters are `IntPtr`s, which point to instances of the three types of data described above.

You must add a `GhostConnectionComponentType` component to each connection to determine which tile the connection should prioritize. 
As mentioned, this `GhostSendSystem` passes this per-connection information to the `ScaleImportanceFunction` function.

The `GhostImportanceDataType` is global, static, singleton data, which configures how chunks are constructed. It's optional, and `IntPtr.Zero` will be passed if it is not found. 
**Importantly: This static data _must_ be added to the same entity that holds the `GhostImportance` singleton. You'll get an exception in the editor if this type is not found here.** 
`GhostSendSystem` will fetch this singleton data, and pass it to the importance scaling function.

`GhostImportancePerChunkDataType` is added to each ghost, essentially forcing it into a specific chunk. The `GhostSendSystem` expects the type to be a shared component. This ensures that the elements in the same chunk will be grouped together by the entity system. 
A user-created system is required to update each entity's chunk to regroup them (example below). It's important to think about how entity transfer between chunks actually works (i.e. the performance implications), as regularly changing an entities chunk will not be performant.

## Distance-based importance

The built-in form of importance scaling is distance-based (`GhostDistanceImportance.Scale`). The `GhostDistanceData` component describes the size and borders of the tiles entities are grouped into.

### An example set up for distance-based importance in Asteroids

The [Asteroids Sample](https://github.com/Unity-Technologies/multiplayer/tree/master/sampleproject/Assets/Samples/Asteroids) makes use of this default scaling implementation. The `LoadLevelSystem` sets up an entity to act as a singleton with `GhostDistanceData` and `GhostImportance` added:

```c#
    var gridSingleton = state.EntityManager.CreateSingleton(new GhostDistanceData
    {
        TileSize = new int3(tileSize, tileSize, 256),
        TileCenter = new int3(0, 0, 128),
        TileBorderWidth = new float3(1f, 1f, 1f),
    });
    state.EntityManager.AddComponentData(gridSingleton, new GhostImportance
    {
        ScaleImportanceFunction = GhostDistanceImportance.ScaleFunctionPointer,
        GhostConnectionComponentType = ComponentType.ReadOnly<GhostConnectionPosition>(),
        GhostImportanceDataType = ComponentType.ReadOnly<GhostDistanceData>(),
        GhostImportancePerChunkDataType = ComponentType.ReadOnly<GhostDistancePartitionShared>(),
    });
```
>[!NOTE]
> Again, you _must_ add both singleton components to the same entity.

The `GhostDistancePartitioningSystem` will then split all the ghosts in the World into chunks, based on the tile size above.
Thus, we use the Entities concept of chunks to create spatial partitions/buckets, allowing us to fast cull entire sets of entities based on distance to the connections character controller (or other notable object).

How? Via another user-definable component: `GhostConnectionPosition` can store the position of a players entity (`Ship.prefab` in Asteroids), which (as mentioned) is passed into the `Scale` function via the `GhostSendSystem`, allowing each connection to determine which tiles (i.e. chunks) that connection should prioritize.
In Asteroids, this component is added to the connection entity when the (steroids-specific) `RpcLevelLoaded` RPC is invoked:
```c#
    [BurstCompile(DisableDirectCall = true)]
    [AOT.MonoPInvokeCallback(typeof(RpcExecutor.ExecuteDelegate))]
    private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
    {
        var rpcData = default(RpcLevelLoaded);
        rpcData.Deserialize(ref parameters.Reader, parameters.DeserializerState, ref rpcData);

        parameters.CommandBuffer.AddComponent(parameters.JobIndex, parameters.Connection, new PlayerStateComponentData());
        parameters.CommandBuffer.AddComponent(parameters.JobIndex, parameters.Connection, default(NetworkStreamInGame));
        parameters.CommandBuffer.AddComponent(parameters.JobIndex, parameters.Connection, default(GhostConnectionPosition)); // <-- Here.
    }
```

Which is then updated via the Asteroids server system `UpdateConnectionPositionSystemJob`:
```c#
        [BurstCompile]
        partial struct UpdateConnectionPositionSystemJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> transformFromEntity;
            public void Execute(ref GhostConnectionPosition conPos, in CommandTarget target)
            {
                if (!transformFromEntity.HasComponent(target.targetEntity))
                    return;
                conPos = new GhostConnectionPosition
                {
                    Position = transformFromEntity[target.targetEntity].Position
                };
            }
        }
```

### Writing my own importance scaling function

Crucially: Every component and function used in this process is user-configurable.
You simply need to:
1. Define the above 3 components (a per-connection component, an optional singleton config component, and a per-chunk shared component), and set them in the `GhostImportance` singleton.
2. Define your own Scaling function, and again set it via the `GhostImportance` singleton.
3. Define your own version of a `GhostDistancePartitioningSystem` which moves your entities between chunks (via writing to the shared component).

Job done!
