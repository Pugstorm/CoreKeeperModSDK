# Prediction

Prediction in a multiplayer games is when the client is running the same simulation code as the server for a given entity (example: simulating the character controller (and any applied inputs) for the local player entity). 
The purpose of this "predicted simulation" is to apply raised input commands **_immediately_**, reducing the input latency, dramatically improving "game feel".
In other words, your character controller can react to inputs on the frame they are raised (awesome!), without having to wait for the server authoritative snapshot to arrive (which would contain data confirming that your character controller movement **_has actually_** been applied).

Prediction only runs for entities which have the [PredictedGhost](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.PredictedGhost.html). 
Unity adds this component to all predicted ghosts on the client, and to all ghosts on the server. On the client, the component also contains some data it needs for the prediction - such as which snapshot has been applied to the ghost.

The prediction is based on a fixed time-step loop, controlled by the [PredictedSimulationSystemGroup](https://docs.unity3d.com/Packages/com.unity.netcode@0latest/index.html?subfolder=/api/Unity.NetCode.PredictedSimulationSystemGroup.html), 
which runs on both client and server, and that usually contains the core part of the deterministic ghosts simulation.

## Client

The basic flow on the client is:
* Netcode applies the latest snapshot it received from the server, to all predicted entities.
* While applying the snapshots, Netcode also finds the oldest snapshot it applied to any entity.
* Once Netcode applies the snapshots, the [PredictedSimulationSystemGroup](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.PredictedSimulationSystemGroup.html) runs from the oldest tick applied to any entity, to the tick the prediction is targeting (this is called "rollback").
* When the prediction runs, the `PredictedSimulationSystemGroup` sets the correct time for the current prediction tick in the ECS TimeData struct. It also sets the `ServerTick` in the [NetworkTime](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.NetworkTime.html) singleton to the tick being predicted.

> [!NOTE]
> This "rollback" and prediction re-simulation can become a **substantial** overhead to each frame.
> Example: For a 300ms connection, expect ~22 frames of re-simulation. I.e. Physics, and all other systems in the `PredictedSimulationSystemGroup`, will tick ~22 times in a single frame.
> You can test this (via setting a high simulated ping in the `Multiplayer PlayMode Tools Window`).
> See the [Optimizations](optimizations.md) page for further details.

Because the prediction loop runs from the oldest tick applied to any entity, and some entities might already have newer data, **you must check whether each entity needs to be simulated or not**. There are two distinct ways to do this check:

### Check which entities to predict using the Simulate tag component (PREFERRED)
The client use the `Simulate` tag, present on all entities in world, to set when a ghost entity should be predicted or not.
- At the beginning of the prediction loop, the `Simulate` tag is disabled the simulation of all `Predicted` ghosts.
- For each prediction tick, the `Simulate` tag is enabled for all the entities that should be simulate for that tick.
- At the end of the prediction loop, all predicted ghost entities `Simulate` components are guarantee to be enabled.

In your systems that run in the `PredictedSimulationSystemGroup` (or any of its sub-groups) you should add to your queries, EntitiesForEach (deprecated) and idiomatic foreach a `.WithAll&lt;Simulate&gt;>` condition.  This will automatically give to the job (or function) the correct set of entities you need to work on.

For example:

```c#

Entities
    .WithAll<PredictedGhost, Simulate>()
    .ForEach(ref Translation trannslation)
{                 
      ///Your update logic here
}
```

### Check which entities to predict using the PredictedGhost.ShouldPredict helper method (LEGACY)
The old way To perform these checks, calling the static method  [PredictedGhost.ShouldPredict](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.PredictedGhost.html#Unity_NetCode_PredictedGhost_ShouldPredict_System_UInt32_) before updating an entity
is still supported. In this case the method/job that update the entity should looks something like this:

```c#

var serverTick = GetSingleton<NetworkTime>().ServerTick;
Entities
    .WithAll<PredictedGhost, Simulate>()
    .ForEach(ref Translation trannslation)
{                 
      if!(PredictedGhost.ShouldPredict(serverTick))
           return;
                  
      ///Your update logic here
}
```

If an entity did not receive any new data from the network since the last prediction ran, and it ended with simulating a full tick (which is not always true when you use a dynamic time-step), the prediction continues from where it finished last time, rather than applying the network data.

## Server

On the server, the prediction loop always runs **_exactly once_**, and does not update the TimeData struct (because it is already correct).
I.e. It's not "predicted" any more: It's the actual authoritative simulation being run on the server.
The `ServerTick` in the `NetworkTime` singleton also has the correct value, so the exact same code can be run on both the client and server.

Thus, the `PredictedGhost.ShouldPredict` always returns true when called on the server, and the `Simulate` component is also always enabled. 

> [!NOTE]
> Therefore, for predicted gameplay systems, you can write the code **once**, and it'll "just work" (without needing to make a distinction about whether or not it's running on the server, or the client).

## Remote Players Prediction
If commands are configured to be serialized to the other players (see [GhostSnapshots](ghost-snapshots.md#icommandData-serialization)) it is possible to use client-side prediction for the remote players using the remote players commands, the same way you do for the local player.
When a new snapshot is received by client, the `PredictedSimulationSystemGroup` runs from the oldest tick applied to any entity, to the tick the prediction is targeting.  It might vary depending on the entity what need to be predicted and you must always check if the entity need to update/apply the input for a specific tick by only processing entities with
the `Simulate` component.

```c#
    protected override void OnUpdate()
    {
        var tick = GetSingleton<NetworkTime>().ServerTick;
        Entities
            .WithAll<Simulate>()
            .ForEach((Entity entity, ref Translation translation, in DynamicBuffer<MyInput> inputBuffer) =>
            {
                if (!inputBuffer.GetDataAtTick(tick, out var input))
                    return;

                //your move logic
            }).Run();
    }
```

### Remote player prediction with the new IInputComponentData
By using the new `IInputComponentData`, you don't need to check or retrieve the input buffer anymore. Your input data for
the current simulated tick will provide for you. 

```c#
    protected override void OnUpdate()
    {
        Entities
            .WithAll<PredictedGhost, Simulate>()
            .ForEach((Entity entity, ref Translation translation, in MyInput input) =>
        {                 
              ///Your update logic here
        }).Run();
    }   
```

# Prediction Smoothing
Prediction errors are always presents for many reason: slightly different logic in between clients and server, packet drops, quantization errors etc.
For predicted entities the net effect is that when we rollback and predict again from the latest available snapshot, more or large delta in between the recomputed values and the current predicted one can be present.
The __GhostPredictionSmoothingSystem__ system provide a way to reconcile and reduce these errors over time, making the transitions smoother.
For each component it is possible to configure how to reconcile and reducing these errors over time by registering a `Smoothing Action Function` on the __GhostPredictionSmoothing__ singleton.
```c#
    public delegate void SmoothingActionDelegate(void* currentData, void* previousData, void* userData);
    //pass null as user data
    GhostPredictionSmoothing.RegisterSmoothingAction<Translation>(EntityManager, MySmoothingAction);
    //will pass as user data a pointer to a MySmoothingActionParams chunk component
    GhostPredictionSmoothing.RegisterSmoothingAction<Translation, MySmoothingActionParams>(EntityManager, DefaultTranslateSmoothingAction.Action);
```
The user data must be a chunk-component present in the entity. A default implementation for smoothing out Translation prediction error is provided by the package.

```c#
world.GetSingleton<GhostPredictionSmoothing>().RegisterSmootingAction<Translation>(EntityManager, CustomSmoothing.Action);

[BurstCompile]
public unsafe class CustomSmoothing
{
    public static readonly PortableFunctionPointer<GhostPredictionSmoothing.SmoothingActionDelegate>
        Action =
            new PortableFunctionPointer<GhostPredictionSmoothing.SmoothingActionDelegate>(SmoothingAction);

    [BurstCompile(DisableDirectCall = true)]
    private static void SmoothingAction(void* currentData, void* previousData, void* userData)
    {
        ref var trans = ref UnsafeUtility.AsRef<Translation>(currentData);
        ref var backup = ref UnsafeUtility.AsRef<Translation>(previousData);

        var dist = math.distance(trans.Value, backup.Value);
        //UnityEngine.Debug.Log($"Custom smoothing, diff {trans.Value - backup.Value}, dist {dist}");
        if (dist > 0)
            trans.Value = backup.Value + (trans.Value - backup.Value) / dist;
    }
}
```

# Prediction Switching

In a typical multiplayer game, you often **_only_** want to predict ghosts (via `GhostMode.Predicted`) that the client is **_specifically interacting_** with (because prediction is expensive on the CPU). Examples include:
- Your own character controller (typically `GhostMode.OwnerPredicted`).
- Dynamic objects your character controller is colliding with (like crates, balls, platforms and vehicles). 
- Interactable items that your client is triggering (like weapons), and any related entities (like projectiles).

In contrast; you'd like the majority of the ghosts in your world to be Interpolated (via `GhostMode.Interpolated`). Therefore, the NetCode package supports opting into Prediction on a per-client, per-ghost basis, based on some criteria (e.g. "predict all ghosts inside this radius of my clients character controller").
This feature is called "Prediction Switching".

### The Client Singleton
The `GhostPredictionSwitchingQueues` client singleton provides two queues that you can subscribe ghosts to:
- `ConvertToPredictedQueue`: Take an Interpolated ghost and make it Predicted (via `GhostPredictionSwitchingSystem.ConvertGhostToPredicted`).
- `ConvertToInterpolatedQueue`: Take a Predicted ghost and make it Interpolated (via `GhostPredictionSwitchingSystem.ConvertGhostToInterpolated`).

The `GhostPredictionSwitchingSystem` will then convert these ghosts for you, automatically (thus changing a ghosts `GhostMode` live).
In practice, this is represented as either adding (or removing) the `PredictedGhost`.

### Rules when using Prediction Switching Queues
- The entity must be a ghost.
- The ghost type (prefab) must have its `Supported Ghost Modes` set to `All` (via the [GhostAuthoringComponent](ghost-snapshots.md#authoring-ghosts)).
- And its `CurrentGhostMode` must not be set to `OwnerPredicted` (as `OwnerPredicted` ghosts already switch prediction (based on ownership)).
- If switching to `Predicted`, the Ghost must currently be `Interpolated` (and vice versa).
- The ghost must not currently be switching prediction (see the transitions section below, and the `SwitchPredictionSmoothing` component)

> [!NOTE]
> These rules are guarded in the switching system, and thus an invalid queue entry will be harmlessly ignored (with an error/warning log).

### Timeline Issues with Prediction Switching
One problem is that an "Interpolated Ghost" is not on the same timeline as a "Predicted Ghost".
- "Predicted Ghosts" run on the same timeline as the client (roughly your ping _ahead_ of the server).
- "Interpolated Ghosts" run on a timeline behind the server (roughly your ping _behind_ the server).

Thus, switching an "Interpolated Ghost" to a "Predicted Ghost" live will cause that object to jump forward in time (over `2 x Ping` milliseconds).
Therefore, any moving objects will teleport.

### The `SwitchPredictionSmoothing` Component, and Prediction Switching Transitions
We mitigate this timeline jump by providing "Prediction Switching Smoothing" (via the transient component `SwitchPredictionSmoothing`, and the system that acts upon it, `SwitchPredictionSmoothingSystem`).

This smoothing will automatically linearly interpolate the `Position` and `Rotation` values of your entity `Transform`, over a user-specified period of time (defined when adding the entity to one of the queues. See `ConvertPredictionEntry.TransitionDurationSeconds`).
This smoothing obviously isn't perfect, and fast-moving objects (which change direction frequently) will still have visual artifacts.

Best practices are is to set `TransitionDurationSeconds` high enough to avoid teleporting, but low enough to minimize the frequency of sudden changes in direction (which will invalidate the smoothing).

### Prediction Switching also modifies Components on the Ghost
One other difficulty with "Prediction Switching" is the fact that you may have removed specific components from the Predicted or Interpolated versions of a ghost (via the `GhostAuthoringInspectionComponent` and/or Variants).
Therefore, whenever a Ghost switches prediction live, we need to add or remove these components to keep in sync with your rules (via method `AddRemoveComponents`).

> [!NOTE]
> This, again, happens automatically, but you should be aware that when re-adding components, the component value will be reset to the value baked at authoring time.

### Example Code
```c#
// Fetch the singleton as RW as we're modifying singleton collection data. 
ref var ghostPredictionSwitchingQueues = ref testWorld.GetSingletonRW<GhostPredictionSwitchingQueues>(firstClientWorld).ValueRW;

// Converts ghost entityA to Predicted, instantly (i.e. as soon as the `GhostPredictionSwitchingSystem` runs). If this entity is moving, it will teleport.
ghostPredictionSwitchingQueues.ConvertToPredictedQueue.Enqueue(new ConvertPredictionEntry
{
    TargetEntity = entityA,
    TransitionDurationSeconds = 0f,
});

// Converts ghost entityB to Interpolated, over 1 second. 
// A lerp is applied to the Transform (both Position and Rotation) automatically, smoothing (and somewhat hiding) the change in timelines.
ghostPredictionSwitchingQueues.ConvertToInterpolatedQueue.Enqueue(new ConvertPredictionEntry
{
    TargetEntity = entityA,
    TransitionDurationSeconds = 1f,
});
```

