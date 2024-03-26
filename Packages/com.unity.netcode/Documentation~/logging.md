# Logging

Netcode for Entities comes with a builtin logging component so you can manipulate how much log information is printed. Currently it allows you to control either general logging messages or ghost snapshot / packet logging separately.

## Generic Logging message and levels

Log messages will be printed to the usual log destination Unity is using (i.e. console log and Editor.log when using the Editor). <br/>
You can change the log level by setting `NetDebug.LogLevelType`. The different log levels are:

* Debug
* Notify
* Warning
* Error
* Exception

The default log level is _Notify_ which has informational messages and higher importance (Notify/Warning/etc). In case you want more details about connection flow, received ghosts etc you can select the _Debug_ log level. 
This will emit more informative messages which will be most useful when debugging issues.

## Packet and ghost snapshot logging

You can also enable detailed log messages about ghost snapshots and how they're being written to the packets sent over the network. The `packet dump` is quite verbose and should be used sparingly when debugging issues related to ghost replication. 

The snapshot logging, can be enabled by adding a `EnablePacketLogging` component to the connection entity you want to debug.

For example, to add it to every connection established you would write this in a system:

```c#
protected override void OnUpdate()
{
    var cmdBuffer = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>().CreateCommandBuffer();
    Entities.WithNone<EnablePacketLogging>().ForEach((Entity entity, in NetworkStreamConnection conn) =>
    {
        cmdBuffer.AddComponent<EnablePacketLogging>(entity);
    }).Schedule();
}
```
Packet log dumps will go into the same directory as the normal log file on desktop platforms (Win/Mac/Lin). 
On mobile (Android/iOS) platforms it will go into the persistent file location where the app will have write access. 
- On Android the files are output to _/Android/data/BUNDLE_IDENTIFIER/files_ and a file manager which can see these hidden files is needed to retrieve them. 
- On iOS the files are output to the app container at _/var/mobile/Containers/Data/Application/GUID/Documents_, which can be retrieved via the Xcode _Devices and Simulators_ window (select the app from the _Installed Apps_ list, click the three dots below and select _Download Container..._). <br/>
>![NOTE]These files will not be deleted automatically and will need to be cleaned up manually, they can grow very large so it's good to be aware of this.

### Packet log debug defines
By default, the packet logging works in the editor and in development builds.
The added logging code can affect performance, even when logging is turned off, and it's therefore disabled by default in release builds.
It can be forced off by adding `NETCODE_NDEBUG` define to the project settings, in the _Scripting Define Symbols_ field, in the editor.

To force it off in a player build, the `NETCODE_NDEBUG` needs to be added with the _Additional Scripting Defines_ in the _DOTS_ project settings.

## Simple ways of enabling packet logging and change log levels
You can easily customise the logging level and enable packet dump by either:
- Using the _Playmode Tools Window_ after entering playmode in the editor.
- By adding the `NetCodeDebugConfigAuthoring` component to a game object in a SubScene. 

These "default methods" are mostly for convenience and besides allowing changes to the log level they provide just a toggle to dump packet logs for all connections (or none). <br/>
To debug specific connections code needs to be written for it depending on the use case.

