# RPCs

Netcode uses a limited form of RPCs to handle events. A job on the sending side can issue RPCs, and they then execute on a job on the receiving side. 
This limits what you can do in an RPC; such as what data you can read and modify, and what calls you are allowed to make from the engine. 
For more information on the Job System see the Unity User Manual documentation on the [C# Job System](https://docs.unity3d.com/2019.3/Documentation/Manual/JobSystem.html).

To make the system a bit more flexible, you can use the flow of creating an entity that contains specific netcode components such as 
`SendRpcCommandRequest` and `ReceiveRpcCommandRequest`, which this page outlines.

## Extend IRpcCommand

To start, create a command by extending the [IRpcCommand](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.IRpcCommand.html):

```c#
public struct OurRpcCommand : IRpcCommand
{
}
```

Or if you need some data in your RPC:

```c#
public struct OurRpcCommand : IRpcCommand
{
    public int intData;
    public short shortData;
}
```

This will generate all the code you need for serialization and deserialization as well as registration of the RPC.

## Sending and receiving commands

To complete the example, you must create some entities to send and recieve the commands you created. 
To send the command you need to create an entity and add the command and the special component [SendRpcCommandRequest](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.SendRpcCommandRequest.html) to it. 
This component has a member called `TargetConnection` that refers to the remote connection you want to send this command to.

> [!NOTE]
> If `TargetConnection` is set to `Entity.Null` you will broadcast the message. On a client you don't have to set this value because you will only send to the server.


The following is an example of a simple send system:

```c#
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public class ClientRpcSendSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<NetworkId>();
    }

    protected override void OnUpdate()
    {
        if (Input.GetKey("space"))
        {
            EntityManager.CreateEntity(typeof(OurRpcCommand), typeof(SendRpcCommandRequest));
        }
    }
}
```

This system sends a command if the user presses the space bar on their keyboard.

When the rpc is received, an entity that you can filter on is created by a code-generated system. To test if this works, the following example creates a system that receives the `OurRpcCommand`:

```c#
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public class ServerRpcReceiveSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((Entity entity, ref OurRpcCommand cmd, ref ReceiveRpcCommandRequest req) =>
        {
            PostUpdateCommands.DestroyEntity(entity);
            Debug.Log("We received a command!");
        }).Run();
    }
}
```

The RpcSystem automatically finds all of the requests, sends them, and then deletes the send request. On the remote side they show up as entities with the same `IRpcCommand` and a `ReceiveRpcCommandRequestComponent` which you can use to identify which connection the request was received from.

## Creating an RPC without generating code

The code generation for RPCs is optional, if you do not wish to use it you need to create a component and a serializer. These can be the same struct or two different ones. To create a single struct which is both the component and the serializer you would need to add:

```c#
[BurstCompile]
public struct OurRpcCommand : IComponentData, IRpcCommandSerializer<OurRpcCommand>
{
    public void Serialize(ref DataStreamWriter writer, in OurRpcCommand data)
    {
    }

    public void Deserialize(ref DataStreamReader reader, ref OurRpcCommand data)
    {
    }

    public PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute()
    {
    }

    [BurstCompile(DisableDirectCall = true)]
    private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
    {
    }

    static readonly PortableFunctionPointer<RpcExecutor.ExecuteDelegate> InvokeExecuteFunctionPointer = new PortableFunctionPointer<RpcExecutor.ExecuteDelegate>(InvokeExecute);
}
```

The [IRpcCommandSerializer](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.IRpcCommandSerializer.html) interface has three methods: __Serialize, Deserialize__, and __CompileExecute__. __Serialize__ and __Deserialize__ store the data in a packet, while __CompileExecute__ uses Burst to create a `FunctionPointer`. The function it compiles takes a [RpcExecutor.Parameters](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.RpcExecutor.Parameters.html) by ref that contains:

* `DataStreamReader` reader
* `Entity` connection
* `EntityCommandBuffer.Concurrent` commandBuffer
* `int` jobIndex

Because the function is static, it needs to use `Deserialize` to read the struct data before it executes the RPC. The RPC then either uses the command buffer to modify the connection entity, or uses it to create a new request entity for more complex tasks. It then applies the command in a separate system at a later time. This means that you don’t need to perform any additional operations to receive an RPC; its `Execute` method is called on the receiving end automatically.

To create an entity that holds an RPC, use the function `ExecuteCreateRequestComponent<T>`. To do this, extend the previous `InvokeExecute` function example with:

```c#
[BurstCompile(DisableDirectCall = true)]
private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
{
    RpcExecutor.ExecuteCreateRequestComponent<OurRpcCommand, OurRpcCommand>(ref parameters);
}
```

This creates an entity with a [ReceiveRpcCommandRequest](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.ReceiveRpcCommandRequest.html) and `OurRpcCommand` components.

Once you create an [IRpcCommandSerializer](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.IRpcCommanSerializer.html), you need to make sure that the [RpcCommandRequest](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.RpcCommandRequestSystem-1.html) system picks it up. To do this, you can create a system that invokes the `RpcCommandRequest`, as follows:

```c#
[UpdateInGroup(typeof(RpcCommandRequestSystemGroup))]
[CreateAfter(typeof(RpcSystem))]
[BurstCompile]
partial struct OurRpcCommandRequestSystem : ISystem
{
    RpcCommandRequest<OurRpcCommand, OurRpcCommand> m_Request;
    [BurstCompile]
    struct SendRpc : IJobChunk
    {
        public RpcCommandRequest<OurRpcCommand, OurRpcCommand>.SendRpcData data;
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            Assert.IsFalse(useEnabledMask);
            data.Execute(chunk, unfilteredChunkIndex);
        }
    }
        public void OnCreate(ref SystemState state)
        {
            m_Request.OnCreate(ref state);
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var sendJob = new SendRpc{data = m_Request.InitJobData(ref state)};
            state.Dependency = sendJob.Schedule(m_Request.Query, state.Dependency);
        }
}
```

The `RpcCommandRequest` system uses an [RpcQueue](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.RpcQueue-1.html) internally to schedule outgoing RPCs.

## A note about serialization

You might have data that you want to attach to the RpcCommand. To do this, you need to add the data as a member of your command and then use the `Serialize` and `Deserialize` functions to decide on what data should be serialized. See the following code for an example of this:

```c#
[BurstCompile]
public struct OurDataRpcCommand : IComponentData, IRpcCommandSerializer<OurDataRpcCommand>
{
    public int intData;
    public short shortData;

    public void Serialize(ref DataStreamWriter writer, in OurDataRpcCommand data)
    {
        writer.WriteInt(data.intData);
        writer.WriteShort(data.shortData);
    }

    public void Deserialize(ref DataStreamReader reader, ref OurDataRpcCommand data)
    {
        data.intData = reader.ReadInt();
        data.shortData = reader.ReadShort();
    }

    public PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute()
    {
    }

    [BurstCompile(DisableDirectCall = true)]
    private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
    {
        RpcExecutor.ExecuteCreateRequestComponent<OurDataRpcCommand, OurDataRpcCommand>(ref parameters);
    }

    static readonly PortableFunctionPointer<RpcExecutor.ExecuteDelegate> InvokeExecuteFunctionPointer = new PortableFunctionPointer<RpcExecutor.ExecuteDelegate>(InvokeExecute);
}
```

> [!NOTE]
> To avoid problems, make sure the `serialize` and `deserialize` calls are symmetric. The example above writes an `int` then a `short`, so your code needs to read an `int` then a `short` in that order.  If you omit reading a value, forget to write a value, or change the order of the way the code reads and writes, you might have unforeseen consequences.


## RpcQueue

The [RpcQueue](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/api/Unity.NetCode.RpcQueue-1.html) is used internally to schedule outgoing RPCs. 
However, you can manually create your own queue and use it to schedule RPCs. 
To do this, call `GetSingleton<RpcCollection>().GetRpcQueue<OurRpcCommand>();`. You can either call it in `OnUpdate` or call it in `OnCreate` and cache the value through the lifetime of your application. 
If you do call it in `OnCreate` you must make sure that the system calling it is created after `RpcSystem`. 

When you have the queue, get the `OutgoingRpcDataStreamBuffer` from an entity to schedule events in the queue and then call `rpcQueue.Schedule(rpcBuffer, new OurRpcCommand);`, as follows:

```c#
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public class ClientQueueRpcSendSystem : ComponentSystem
{
    protected override void OnCreate()
    {
        RequireForUpdate<NetworkId>();
    }

    protected override void OnUpdate()
    {
        if (Input.GetKey("space"))
        {
            var rpcQueue = GetSingleton<RpcCollection>().GetRpcQueue<OurRpcCommand, OurRpcCommand>();
            Entities.ForEach((Entity entity, ref NetworkStreamConnection connection) =>
            {
            	var rpcFromEntity = GetBufferLookup<OutgoingRpcDataStreamBuffer>();
                if (rpcFromEntity.Exists(entity))
                {
                    var buffer = rpcFromEntity[entity];
                    rpcQueue.Schedule(buffer, new OurRpcCommand());
                }
            });
        }
    }
}
```

This example sends an RPC using the `RpcQueue` when the user presses the space bar on their keyboard.
