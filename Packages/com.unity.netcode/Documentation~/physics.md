# Physics

The Netcode package has some integration with Unity Physics which makes it easier to use physics in a networked game. The integration handles interpolated ghosts with physics, and support for predicted ghosts with physics.

This works without any configuration but will assume all dynamic physics objects are ghosts, so either fully simulated by the server (interpolated ghosts), or by both with the client also simulating forward (at predicted/server tick) and server correcting prediction errors (predicted ghosts), the two types can be mixed together. To run the physics simulation only locally for certain objects some setup is required.

## Interpolated ghosts

For interpolated ghosts it is important that the physics simulation only runs on the server.
On the client the ghosts position and rotation are controlled by the snapshots from the server and the client should not run the physics simulation for interpolated ghosts.

In order to make sure this is true Netcode will disable the `Simulate` component data tag on clients on appropriate ghost entities at the beginning on each frame. That make the physics object `kinematic` and they will not be moved by the physics simulation.

In particular:

- The `PhysicsVelocity` will be ignored (set to zero).
- Yhe `Translation` and `Rotation` are preserved.

## Predicted ghosts and physics simulation

By the term _Predicted Physics_ we mean that the physics simulation runs in the prediction loop (possibly multiple times per update from the tick of the last received snapshot update) on the client, as well as running normally on the server.

During initialization Netcode will move the `PhysicsSystemGroup` and all `FixedStepSimulationSystemGroup` systems into the `PredictedFixedStepSimulationSystemGroup`. This group is the predicted version of `FixedStepSimulationSystemGroup`, so everything here will be called multiple times up to the required number of predicted ticks. These groups are then only updated when there is actually a dynamic predicted physics ghost present in the world.

All predicted ghosts with physics components will run this kind of simulation when they are dynamic. Like with interpolated ghosts the `Simulate` tag will be enabled/disabled as appropriate at the beginning of each predicted frame, but this time multiple simulation steps might be needed.

Since the physics simulation can be quite CPU intensive it can spiral out of control when it needs to run multiple times. Needing to predict multiple simulation frames could then result in needing to run multiple ticks in one frame as the fixed timesteps falls behind the simulation tick rate, making the situation worse. On server it may be beneficial to enable simulation batching in the [`ClientServerTickRate`](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.ClientServerTickRate.html) component, see the `MaxSimulationStepBatchSize` and `MaxSimulationStepsPerFrame` options. On clients there are options for prediction batching exposed in the [`ClientTickRate`](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.ClientTickRate.html), see `MaxPredictionStepBatchSizeFirstTimeTick` and `MaxPredictionStepBatchSizeRepeatedTick`.

### Using lag compensation predicted collision worlds

When using predicted physics the client will see his predicted physics objects at a slightly different view as the _correct_ authoritative view seen by the server, since it is forward predicting where objects will be at the current server tick. When interacting with such physics objects there is a lag compensation system available so the server can _look up_ what collision world the client saw at a particular tick (to for example better account for if he hit a particular collider). This is enabled via the `EnableLagCompensation` tick in the [`NetCodePhysicsConfig`](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.NetCodePhysicsConfig.html) component. Then you can use the [`PhysicsWorldHistorySingleton`](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.PhysicsWorldHistorySingleton.html) to query for the collision world at a particular tick.

## Multiple physics worlds

Predicted simulation will work by default and all physics objects in the world should be ghosts. To enable client-only physics simulation (for example to use it run VFX, particles and any other sort of physics interaction that does not need to be replicated) another physics world needs to be created for it.

By default, the main physics world at index 0 will be the _predicted physics world_, but a separate _client-only physics world_ can also be created, running it's own distinct simulation. This can be done by implementing a custom physics system group and providing it with a new physics world index. Creating a client only physics world at index 1 can be done most simply like this:

```c#
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial class VisualizationPhysicsSystemGroup : CustomPhysicsSystemGroup
{
    public VisualizationPhysicsSystemGroup() : base(1, true)
    {}
}
```

Where the arguments to the custom class constructor are the world index and boolean indicating if it should share static colliders with the main physics world. Physics simulation will here run in the usual `FixedUpdateSimulationGroup` as usual. See more about the `CustomPhysicsSystemGroup` in the [Unity Physics documentation](https://docs.unity3d.com/Packages/com.unity.physics@latest/index.html?subfolder=/manual/).

The two simulations can use different fixed-time steps and are not required to be in sync, meaning that in the same frame it is possible to have both, or only one of them to be executed independently.
However, as mentioned in the previous section, for the predicted simulation **when a rollback occurs the simulation may runs multiple times in the same frame, one the for each rollback tick**. The client only simulation of course just runs once as normally.

When a client only physics world exists, all non-ghost dynamic physics objects can be moved to that. This can be configured in the [`NetCodePhysicsConfig`](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.NetCodePhysicsConfig.html) component which can be added to a subscene. By setting the `ClientNonGhostWorldIndex` there to the client only physics world index all dynamic non-ghosts will be moved there.

As part of the entity baking process, a _PhysicsWorldIndex_ shared component is added to all the physics entities, indicating
in which world the entity should be part of.
> [!NOTE]
> It is the responsibility of the user to setup their prefab properly to make them run in the correct physics world. This can be achieved with the `PhysicsWorldIndexAuthoring` component, provided with the Unity Physics package, which allows setting the physics world index for rigid bodies. For more information, please refer to the [Unity Physics documentation](https://docs.unity3d.com/Packages/com.unity.physics@latest/index.html?subfolder=/manual/). 

### Interaction in between predicted and client-only physics entities

There are situation when you would like to make the ghosts interact with physics object that are present only on the client (ex: debris). However, them being a part of a different simulation islands they can't interact with each-other.
The Physics package provides for that use-case a specific workflow using `Custom Physics Proxy` entities.

For each physics entity present in the predicted world where you would like to interact with the client-only world, you need to add the `CustomPhysicsProxyAuthoring` component. The baking process will then automatically create a proxy entity with the necessary physics components (PhysicsBody, PhysicsMass, PhysicsVelocity) along with a [`CustomPhysicsProxyDriver`](https://docs.unity3d.com/Packages/com.unity.physics@latest/index.html?subfolder=/api/Unity.Physics.CustomPhysicsProxyDriver.html) which is the link to the root ghost entity. It will make a copy of the ghosts collider as well and configure the proxy physics body as kinematic. The simulated ghost entity in the predicted world will then be used to _drive_ the proxy by copying the necessary component data and setup the physics velocity to let the proxy move and interact with the other physics entities in the  client-only world.

The ghost proxy position and rotation and are automatically handled by [`SyncCustomPhysicsProxySystem`](https://docs.unity3d.com/Packages/com.unity.physics@latest/index.html?subfolder=/api/Unity.Physics.Systems.SyncCustomPhysicsProxySystem.html) system. 
By default the kinematic physics entity is moved using kinematic velocity, by altering the PhysicsVelocity component. It is possible to change the default behavior for the prefab by setting the 
`GenerateGhostPhysicsProxy.DriveMode` component property. 
Furthermore, it is possible to change that beahviour dynamically at runtime by setting the `PhysicsProxyGhostDriver.driveMode` property to the desired mode.

## Limitations

As mentioned on this page there are some limitations you must be aware of to use physics and netcode together.

- Physics simulation will not use partial ticks on the client, you must use physics interpolation if you want physics to update more frequently than it is simulating.
- The Unity.Physics debug systems does not work correctly in presence of multiple world (only the default physics world is displayed).
