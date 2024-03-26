# Time synchronization

Netcode uses a server authoritative model, which means that the server executes a fixed time step based on how much time has passed since the last update. 
As such, the client needs to match the server time at all times for the model to work.

## The NetworkTimeSystem

[NetworkTimeSystem](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.NetworkTimeSystem.html) calculates which server time to present on the client. 
The network time system calculates an initial estimate of the server time based on the round trip time and latest received snapshot from the server. 
When the client receives an initial estimate, it makes small changes to the time progress rather than doing large changes to the current time. 
To make accurate adjustments, the server tracks how long it keeps commands in a buffer before it uses them. 
This is sent back to the client and the client adjusts its time so it receives commands just before it needs them.

The client sends commands to the server. The commands will arrive at some point in the future. When the server receives these commands, it uses them to run the game simulation. 
The client needs to estimate which tick this is going to happen on the server and present that, otherwise the client and server apply the inputs at different simulation step.

The tick the client estimates the server will apply the commands on is called the **prediction tick**. You should only use prediction time for a predicted object like the local player. 

For interpolated objects, the client should present them in a state it has received data for. This time is called **interpolation tick**. The `interpolation tick` is calculated as an offset in respect the `predicted tick`. 
That time offset is called **prediction delay**. <br/> 
The `interpolation delay` is calculated by taking into account round trip time, jitter and packet arrival rate, all data that is generally available on the client.
We also add some additional time, based on the network tick rate, to make sure we can handle some packets being lost. You can visualize the time offsets and scales in the snapshot visualization tool, [NetDbg](ghost-snapshots#Snapshot-visualization-tool).

The `NetworkTimeSystem` slowly adjusts both `prediction tick` and `interpolation delay` in small increments to keep them advancing at a smooth rate and ensure that neither the
interpolation tick nor the prediction tick goes back in time.

### Configuring clients interpolation
A [ClientTickRate](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.ClientTickRate.html) singleton entity in the client World can be used to 
configure how the system estimate both prediction tick and interpolation delay.


| Paramater                    |                                                                                                                                                                                                                                                                                                                                             |
|------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| InterpolationTimeNetTicks    | The number of simulation tick to use as an interpolation buffer for interpolated ghosts.                                                                                                                                                                                                                                                    |
| MaxExtrapolationTimeSimTicks | The maximum time in simulation ticks which the client can extrapolate ahead when data is missing                                                                                                                                                                                                                                            |
| MaxPredictAheadTimeMS        | This is the maximum accepted ping, rtt will be clamped to this value when calculating server tick on the client, which means if ping is higher than this the server will get old commands. <br/>Increasing this makes the client able to deal with higher ping, but the client needs to run more prediction steps which takes more CPU time |
| TargetCommandSlack           | Specifies the number of simulation ticks the client tries to make sure the commands are received by the server before they are used on the server.                                                                                                                                                                                          |

It is possible to further customize the client times calculation. Please read the [ClientTickRate](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.ClientTickRate.html) documentation for more in depth information.

## Retrieving timing information in your application
Netcode for Entities provide a [NetworkTime](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.NetworkTime.html) singleton 
that should be used to retrieve the current simulated/predicted server tick, interpolated tick and other time related properties.

```csharp
var networkTime = SystemAPI.GetSingleton<NetworkTime>();
var currentTick = networkTime.ServerTick; 
...
```

The `NetworkTime` can be used indistinctly on both client and server both inside and outside the prediction loop. <br/>
For the prediction loop in particular, the `NetworkTime` add some flags to the current simulated tick that can be used to implement certain logic:
For example:
- IsFirstPredictionTick : the current server tick is the first one we are predict from the last received snapshot for that entity.
- IsFinalPredictionTick : the current server tick which will be the last tick to predict.
- IsFirstTimeFullyPredictingTick: the current server tick is a full tick and this is the first time it is being predicting as a non-partial tick. Useful to implement actions that should be executed only once. 

And many others. Please check [NetworkTime docs](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.NetworkTime.html) for further information. 

## Client DeltaTime, ElapsedTime and Unscaled time
When the client connect to the server, the elapsed `DeltaTime`, and total `ElapsedTime` are handled differently. </br>
That because the needs for the client to keep the predicted tick in sync with the server; The application perceived `DeltaTime` is scaled up and down, to accelerate or slowdown the simulation. 

The time scaling has some implication:
- **For all systems updating inside the `SimulationSystemGroup`** (and sub-groups) the `Time.DeltaTime` and the `Time.ElapsedTime` will reflects this scaled elapsed time.
- For systems updating in the `PresentationSystemGroup` or `InitializationSystemGroup`, or in general outside the `SimulationSystemGroup`, the reported timing are the one normally reported by the application loop.

Because of that, the `Time.ElapsedTime` seen inside and outside the simulation group is usually different. <br/>

For cases where you need to have access to real, unscaled delta and elapsed time inside the `SimulationSystemGroup`, you can use the
[UnscaledClientTime](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.NetworkTime.html) singleton.<br/>
The values in the `UnscaledClientTime.DeltaTime` and `UnscaledClientTime.ElapsedTime` are the ones normally reported by application loop.
