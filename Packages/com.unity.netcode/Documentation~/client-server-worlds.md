# Client server Worlds

The Netcode for Entities Package has a separation between client and server logic, and thus, splits logic into multiple Worlds (the "Client World", and the "Server World").
It does this using concepts laid out in the [hierarchical update system](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/systems-update-order.html) of Unity’s Entity Component System (ECS).

## Declaring in which world the system should update.
By default, systems are create into (and updated in) the `SimulationSystemGroup`, and created for both client and server worlds. In cases where you want to override that behaviour (i.e. have your system
created and run only on the client world), you have two different way to do it:

### Targeting specific system groups
By specifying that your system belongs in a specific system group (that is present only on the desired world), your system will automatically **not** be created in worlds where this system group is not present.
In other words: Systems in a system group inherit system group world filtering. For example:
```csharp
[UpdateInGroup(typeof(GhostInputSystemGroup))]
public class MyInputSystem : SystemBase
{
  ...
}
```
Because the `GhostInputSystemGroup` exists only for Client worlds, the `MyInputSystem` will **only** be present on the client world (caveat: this includes both `Client` and `Thin Client` worlds).
> [!NOTE]
> Systems that update in the `PresentationSystemGroup` are only added to the client World, since the `PresentationSystemGroup` is not created for `Server` and `Thin Client` worlds.


### Use WorldSystemFilter
When more granularity is necessary (or you just want to be more explicit about which World type(s) the system belongs to), you should use the
[WorldSystemFilter](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.WorldSystemFilter.html) attribute.

Context: When an entity `World` is created, users tag it with specific [WorldFlags](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.WorldFlags.html), 
that can then be used by the Entities package to distinguish them (e.g. to apply filtering and update logic).

By using the `WorldSystemFilter`, you can declare (at compile time) which world types your system belongs to:
- `LocalSimulation`: a world that does not run any Netcode systems, and that it is not used to run the multiplayer simulation.
- `ServerSimulation`: A world used to run the server simulation.
- `ClientSimulation`: A world used to run the client simulation.
- `ThinClientSimulation`: A world used to run the thin clients simulation. 

```csharp
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public class MySystem : SystemBase
{
  ...
}
```
In the example above, we declared that the `MySystem` system should **only** be present for worlds that can be used for running the `client simulation`; That it, the world has the `WorldFlags.GameClient` set.
`WorldSystemFilterFlags.Default` is used when this attribute is not present.

## Bootstrap
When the Netcode for Entities package is added to your project, a new default [bootstrap](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.ClientServerBootstrap.html) is added to the project.

The default bootstrap creates client server Worlds automatically at startup. 
It populates them with the systems defined in the attributes you have set. This is useful when you are working in the Editor and you enter play-mode with your game scene opened. 
But in a standalone game, or when you want to use some sort of frontend menu, you might want to delay the World creation, i.e you can use the same executable as both a client and server.

It it possible to create your own bootstrap class and customise your game flow by creating a class that extends `ClientServerBootstrap` and override the default `Initialize` method implementation. 
You can re-use in your class mostly of the provided helper methods that can let you create `client`, `server`, `thin-client` and `local simulation` worlds. See for more details [ClientServerBootstrap methods](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.ClientServerBootstrap.html).

The following code example shows how to override the default bootstrap to prevent automatic creation of the client server worlds:

```c#
public class ExampleBootstrap : ClientServerBootstrap
{
    public override bool Initialize(string defaultWorldName)
    {
        //Create only a local simulation world without any multiplayer and netcode system in it.
        CreateLocalWorld(defaultWorldName);
        return true;
    }

}
```

## Fixed and dynamic time-step

When you use Netcode for Entities, the server always updates **at a fixed time-step**. The package also limits the maximum number of fixed-step iterations per frame, to make sure that the server does not end up in a state where it takes several seconds to simulate a single frame.

It is therefore important to understand that the fixed update does not use the [standard Unity update frequency](https://docs.unity3d.com/Manual/class-TimeManager.html). 

### Configuring the Server fixed update loop.
The [ClientServerTickRate](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.ClientServerTickRate.html) singleton component (in the server World) controls this tick-rate. 

By using the `ClientServerTickRate`, you can control different aspects of the server simulation loop. For example:
- The `SimulationTickRate` lets you configure the number of simulation ticks per second.
- The `NetworkTickRate` lets you configure how frequently the server sends snapshots to the clients (by default the `NetworkTickRate` is identical to the `SimulationTickRate`). 

**The default number of simulation ticks is 60**.

If the server updates at a lower rate than the simulation tick rate, it will perform multiple ticks in the same frame. For example, if the last server update took 50ms (instead of the usual 16ms), the server will need to `catch-up`, and thus it will do ~3 simulation steps on the next frame (16ms * 3 ≈ 50ms). 

This behaviour can lead to what is known as `the spiral of death`; the server update becomes slower and slower (because it is executing more steps per update, to catch up), thus, ironically, putting it further behind (creating more problems). 
The `ClientServerTickRate` allows you to customise how the server runs in this particular situation (i.e. when the server cannot maintain the desired tick-rate). 

By setting the [MaxSimulationStepsPerFrame](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.ClientServerTickRate.html#ClientServerTickRate_MaxSimulationStepsPerFrame) 
you can control how many simulation steps the server can run in a single frame. <br/>
By using the [MaxSimulationStepBatchSize](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.ClientServerTickRate.html#MaxSimulationStepBatchSize)
you can instruct the server loop to `batch` together multiple ticks into a single step, but with a multiplier on the delta time. For example, instead of running two step, you can run only one (but with double the delta time).
> [!NOTE]
> This batching only works under specific conditions, and has its own nuances and considerations. Ensure that your game does not make any assumptions that one simulation step is "1 tick" (nor should you hardcode deltaTime).

Finally, you can configure how the server should consume the the idle time to target the desired frame rate. 
The [TargetFrameRateMode](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.ClientServerTickRate.html#TargetFrameRateMode) controls how the server should keep the tick rate. Available values are:
* `BusyWait` to run at maximum speed
* `Sleep` for `Application.TargetFrameRate` to reduce CPU load
* `Auto` to use `Sleep` on headless servers and `BusyWait` otherwise


### Configuring the Client update loop.
The client updates at a dynamic time step, with the exception of prediction code (which always runs at the same fixed time step as the server, attempting to maintain a "somewhat deterministic" relationship between the two simulations). 
The prediction runs in the [PredictedSimulationSystemGroup](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.PredictedSimulationSystemGroup.html), which applies this unique fixed time step for prediction.

**The `ClientServerTickRate` configuration is sent (by the server, to the client) during the initial connection handshake. The client prediction loop runs at the exact same `SimulationTickRate` as the server (as mentioned).**

## Standalone builds
When you build a standalone game, Netcode uses the __DOTS Settings__ in the __Project Settings__ window to:
- To decide which type of build to make (only valid for standalone player builds).
- To choose mode-specific baking settings.

### Building standalone servers
In order to build standalone server, you need to switch to a `Dedicated Server` platform. When building a server, the `UNITY_SERVER` define is set automatically (**and also automatically set in the editor**). <br/> 
The `DOTS` project setting will reflect this change, by using the setting for the server build type.

### Building standalone client
When using a normal standalone player target (i.e Windows), it is possible to select the type of build to make (in the `DOTS` project setting):
- A `client-only` build. The `UNITY_CLIENT` define will be set in the build (**but not in-editor**).
- A `client/server` build. Neither the `UNITY_CLIENT`, nor the `UNITY_SERVER` are set (i.e. not in built players, nor in-editor).

For either build type, specific baking filters can be specified in the `DOTS` project setting.


## World migration
Sometimes you want to be able to destroy the world you are in and spin up another world without loosing your connection state. In order to do this we supply a DriverMigrationSystem, that allows a user to Store and Load Transport related information so a smooth world transition can be made.

```
public World MigrateWorld(World sourceWorld)
{
    DriverMigrationSystem migrationSystem = default;
    foreach (var world in World.All)
    {
        if ((migrationSystem = world.GetExistingSystem<DriverMigrationSystem>()) != null)
            break;
    }

    var ticket = migrationSystem.StoreWorld(sourceWorld);
    sourceWorld.Dispose();

    var newWorld = migrationSystem.LoadWorld(ticket);

    // NOTE: LoadWorld must be executed before you populate your world with the systems it needs!
    // This is because LoadWorld creates a `MigrationTicket` Component that the NetworkStreamReceiveSystem needs in order to be able to Load
    // the correct Driver.

    return ClientServerBootstrap.CreateServerWorld(DefaultWorld, newWorld.Name, newWorld);
}
```

## Thin Clients

Thin clients are a tool to help test and debug in the editor by running simulated dummy clients with your normal client and server worlds. See the _Playmode Tools_ section above for how to configure them

These clients are heavily stripped down and should run as little logic as possible so they don't put a heavy load on the CPU while testing. Each thin client added adds a little bit of extra work to be computed each frame.

Only systems which have explicitly been set up to run on thin client worlds will run, marked with the `WorldSystemFilterFlags.ThinClientSimulation` flag on the `WorldSystemFilter` attribute. No rendering is done for thin client data so they are invisible to the presentation.

In some cases like in `MonoBehaviour` scripts you might need to check if it's running on a thin client and then early out or cancel processing, the `World.IsThinClient()` can be used in those cases.

Most commonly the only important work they need to do is generate random inputs for the server to process. These inputs usually need to be added to a manually created dummy entity as no ghost spawning is done on thin clients. Not even for it's own local ghost/player.
