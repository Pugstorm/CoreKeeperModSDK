# Network connection
## Netcode + Unity Transport

The network connection uses the [Unity Transport package](https://docs.unity3d.com/Packages/com.unity.transport@latest) and stores each connection as an entity. Each connection entity has a [NetworkStreamConnection](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.NetworkStreamConnection.html) component with the `Transport` handle for the connection. When the connection is closed, either because the server disconnected the user or the client request to disconnect, the the entity is destroyed.

To request disconnect, add a `NetworkStreamRequestDisconnect` component to the entity. Direct disconnection through the driver is not supported. Your game can mark a connection as being in-game, with the `NetworkStreamInGame` component. Your game must do this; it is never done automatically.

> [!NOTE]
> Before the `NetworkStreamInGame` component is added to the connection, the client does not send commands, nor does the server send snapshots.

To target which entity should receive the player commands, when not using the `AutoCommandTarget` feature or for having a more manual control, 
each connection has a [CommandTarget](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.CommandTarget.html) 
which must point to the entity where the received commands need to be stored. Your game is responsible for keeping this entity reference up to date.

### Ingoing buffers
Each connection can have up to three incoming buffers, one for each type of stream: commands, RPCs and snapshot (client-only).
[IncomingRpcDataStreamBuffer](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.IncomingRpcDataStreamBuffer.html)
[IncomingCommandDataStreamBuffer](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.IncomingCommandDataStreamBuffer.html)
[IncomingSnapshotDataStreamBuffer](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.IncomingSnapshotDataStreamBuffer.html)

When a client receive a snapshot from the server, the message is queued into the buffer and processed later by the [GhostReceiveSystem](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.IncomingSnapshotDataStreamBuffer.html).
Similarly, RPCs and Commands follow the sample principle. The messages are gathered first by the [NetworkStreamReceiveSystem](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.NetworkStreamReceiveSystem.html) and consumed then by
the respective rpc and command receive system.
> [!NOTE]
> Server connection does not have an IncomingSnapshotDataStreamBuffer.

### Outgoing buffers
Each connection can have up to two outgoing buffers: one for RPCs and one for commands (client only).
[OutgoingRpcDataStreamBuffer](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.OutgoingRpcDataStreamBuffer.html)
[OutgoingCommandDataStreamBuffer](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.OutgoingCommandDataStreamBuffer.html)

When commands are produced, they are first queued into the outgoing buffer, that is flushed by client at regular interval (every new tick). Rpc messages follow the sample principle: they are gathered first by their respective send system,
that encode them into the buffer first. Then, the [RpcSystem](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.OutgoingCommandDataStreamBuffer.html) will flush the RPC in queue
(by coalescing multiple messages in one MTU) at regular interval.

## Connection Flow
When your game starts, the Netcode for Entities package neither automatically connect the client to server, nor make the server start listening to a specific port. In particular the default `ClientServerBoostrap` just create the client and 
server worlds. It is up to developer to decide how and when the server and client open their communication channel.

There are different way to do it:
- Manually start listening for a connection on the server, or connect to a server from the client using the `NetworkStreamDriver`.
- Automatically connect and listen by using the `AutoConnectPort` (and relative `DefaultConnectAddress`).
- By creating a `NetworkStreamRequestConnect` and/or `NetworkStreamRequestListen` request in the client and/ot server world respectively. 

> [!NOTE]
> Regardless of how you choose to connect to the server, we strongly recommend ensuring `Application.runInBackground` is `true` while connected.
> You can do so by a) setting `Application.runInBackground = true;` directly, or b) project-wide via "Project Settings > Player > Resolution and Presentation". 
> If you don't, your multiplayer will stall (and likely disconnect) if and when the application loses focus (e.g. by the player tabbing out), as netcode will be unable to tick.
> The server should likely always have this enabled.
> We provide error warnings for both via `WarnAboutApplicationRunInBackground`.

### Manually Listen/Connect 
To establish a connection, you must get the [NetworkStreamDriver](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.NetworkStreamDriver.html) singleton (present on both client and server worlds) 
and then call either `Connect` or `Listen` on it.

### Using the AutoConnectPort
The `ClientServerBoostrap` contains two special properties that can be used to instruct the boostrap the server and client to automatically listen and connect respectively. 
- [AutoConnectPort](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.ClientServerBootstrap.html#Unity_NetCode_ClientServerBootstrap_AutoConnectPort)
- [DefaultConnectAddress](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.ClientServerBootstrap.html#Unity_NetCode_ClientServerBootstrap_DefaultConnectAddress)
- [DefaultListenAddress](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.ClientServerBootstrap.html#Unity_NetCode_ClientServerBootstrap_DefaultListenAddress)

In order to setup the `AutoConnectPort` you should create you custom [bootstrap](client-server-worlds.md#bootstrap) and setting a value different than 0 for the `AutoConnectPort`
before creating your worlds. For example:

```c#
public class AutoConnectBootstrap : ClientServerBootstrap
{
    public override bool Initialize(string defaultWorldName)
    {
        // This will enable auto connect.       
        AutoConnectPort = 7979;
        // Create the default client and server worlds, depending on build type in a player or the PlayMode Tools in the editor
        CreateDefaultClientServerWorlds();
        return true;
    }
}
```
The server will start listening at the wildcard address (`DefaultConnectAddress`:`AutoConnectPort`). The `DefaultConnectAddress` is by default set to `NetworkEndpoint.AnyIpv4`.<br/> 
The client will start connecting to server address (`DefaultConnectAddress`:`AutoConnectPort`). The `DefaultConnectAddress` is by default set to to `NetworkEndpoint.Loopback`.

> [!NOTE]
> In the editor, the Playmode tool allow you to "override" both the AutoConnectAddress and AutoConnectPort. **The value is the playmode tool take precedence.** <br>
> [!NOTE]
> When AutoConnectPort is set to 0 the Playmode tools override functionality will not be used. The intent is then you need to manually trigger connection.

### Controlling the connection flow using NetworkStreamRequest
Instead of invoking and calling methods on the [NetworkStreamDriver](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.NetworkStreamDriver.html) you can instead create:

- A [NetworkStreamRequestConnect](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.NetworkStreamRequestConnect.html) singleton to request a connection to the desired server address/port.
- A [NetworkStreamRequestListen](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.NetworkStreamRequestListen.html) singleton to make the server start listening at the desired address/port. 

```csharp
//On the client world, create a new entity with a NetworkStreamRequestConnect. It will be consumed by NetworkStreamReceiveSystem later.
var connectRequest = clientWorld.EntityManager.CreatEntity(typeof(NetworkStreamRequestConnect));
EntityManager.SetComponentData(connectRequest, new NetworkStreamRequestConnect { Endpoint = serverEndPoint });

//On the server world, create a new entity with a NetworkStreamRequestConnect. It will be consumed by NetworkStreamReceiveSystem later.
var listenRequest = serverWorld.EntityManager.CreatEntity(typeof(NetworkStreamRequestListen));
EntityManager.SetComponentData(connectRequest, new NetworkStreamRequestListen { Endpoint = serverEndPoint });

```

The request will be then consumed at runtime by the [NetworkStreamReceiveSystem](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.NetworkStreamReceiveSystem.html).

## Network Simulator
Unity Transport provides a [SimulatorUtility](playmode-tool.md#networksimulator), which is available (and configurable) in the Netcode package. Access it via `Multiplayer > PlayMode Tools`.

We strongly recommend that you frequently test your gameplay with the simulator enabled, as it more closely resembles real-world conditions.

## Listening for Client Connection Events
We provide a `public NativeArray<NetCodeConnectionEvent>.ReadOnly ConnectionEventsForTick` collection (via the `NetworkStreamDriver` singleton), allowing you to iterate over (and thus react to) client connection events on the Client & Server.

```csharp
// Example System:
[UpdateAfter(typeof(NetworkReceiveSystemGroup))]
[BurstCompile]
public partial struct NetCodeConnectionEventListener : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var connectionEventsForClient = SystemAPI.GetSingleton<NetworkStreamDriver>().ConnectionEventsForTick;
        foreach (var evt in connectionEventsForClient)
        {
            UnityEngine.Debug.Log($"[{state.WorldUnmanaged.Name}] {evt.ToFixedString()}!");
        }
    }
}
```
> [!NOTE]
> These events will only live for a single `SimulationSystemGroup` tick, and are reset during `NetworkStreamConnectSystem` and `NetworkStreamListenSystem` respectively.
> Therefore, if your system runs **_after_** these aforementioned system's job's execute, you'll receive notifications on the same tick that they were raised.
> However, if you query this collection **_before_** this system's job's execute, you'll be iterating over the **_previous_** tick's values.

> [!NOTE]
> Because the Server runs on a fixed delta-time, the `SimulationSystemGroup` may tick any number of times (including zero times) on each render frame.
> Because of this, `ConnectionEventsForTick` is only valid to be read inside a system running inside the `SimulationSystemGroup`.
> I.e. Trying to access it outside the `SimulationSystemGroup` can lead to a) either **_only_** seeing events for the current tick (meaning you miss events for previous ticks) or b) receiving events multiple times, if the simulation doesn't tick on this render frame.
> Therefore, do not access `ConnectionEventsForTick` inside the `InitializationSystemGroup`, nor inside the `PresentationSystemGroup`, nor inside any `MonoBehaviour` Unity method (non-exhaustive list!).

### NetCodeConnectionEvent's on the Client
| Connection Status | Invocation Rules                                                                                                                                        |
|-------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Unknown`         | Never raised.                                                                                                                                           |
| `Connecting`      | Raised once for your own client, once the `NetworkStreamReceiveSystem` registers your `Connect` call (which may be one frame after you call `Connect`). |
| `Handshake`       | Raised once for your own client, once your client has received a message from the server notifying your client that its connection was accepted.        |
| `Connected`       | Raised once for your own client, once the server sends you your `NetworkId`.                                                                            | 
| `Disconnected`    | Raised once for your own client, once you disconnect from / timeout from / are disconnected by the server. The `DisconnectReason` will be set.          |

> [!NOTE]
> Clients do **_not_** receive events for other clients. Any events raised in a client world will only be for it's own client connection.

### NetCodeConnectionEvent's on the Server
| Connection Status | Invocation Rules                                                                                                                                          |
|-------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Unknown`         | Never raised.                                                                                                                                             |
| `Connecting`      | Never raised on the server, as the server does not know when a client begins to connect.                                                                  |
| `Handshake`       | Never raised on the server, as accepted clients are assigned `NetworkId`'s immediately. I.e. Handshake is instant.                                        |
| `Connected`       | Raised once for every accepted client, on the frame the server accepts the connection (and assigns said client a `NetworkId`).                            | 
| `Disconnected`    | Raised once for every accepted client, which then disconnects, on the frame we receive the Disconnect event or state. The `DisconnectReason` will be set. |

> [!NOTE]
> The server does not raise any events when it successfully `Binds`, nor when it begins to `Listen`. Use existing APIs to query these statuses.
