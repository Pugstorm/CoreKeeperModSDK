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
        // Create the default client and server worlds, depending on build type in a player or the Multiplayer PlayMode Tools in the editor
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
