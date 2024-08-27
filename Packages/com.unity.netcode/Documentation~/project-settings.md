# Netcode Project Settings reference

Netcode derives classes Entities __DOTS Settings__ to define Netcode-specific settings. 
To open these Project Settings, go to **Edit** &gt; **Project Settings** &gt; **Entities**.

## Netcode Client Target (a.k.a. Client Hosted Servers)
The `Netcode Client Target` dropdown determines whether or not you want the resulting client build to **_support_** hosting server worlds in-process.

| NCT               | Use-cases      |
|-------------------|--------------------------------------------|
| `ClientAndServer` | - You want users to be able to host their own servers (via UI) in the main game executable.<br/>Calling `ClientServerBootstrap.CreateServerWorld` will work.  |
| `ClientOnly`      | - You only want the server to be hosted by you - the developer.<br/>- You want to ship a DGS (Dedicated Game Server) executable alongside your game executable. Use `ClientOnly` for the game client build and `ClientAndServer` for the DGS build (automatic).<br/>Your players won't have access to server server hosting functionality.<br/>Calling `ClientServerBootstrap.CreateServerWorld` throws a NotSupportedException. |

This setting is only valid for non-DGS build targets. We support "client hosted servers" in standalone, console, and mobile.

| Build Type            | Netcode Client Target | Defines                                                                                                |
|-----------------------|-----------------------|-------------------------------------------------------------------------------------------------------|
| Standalone Client     | ClientAndServer      | Neither the `UNITY_CLIENT`, nor the `UNITY_SERVER` are set (i.e. not in built players, nor in-editor). |
| Standalone Client     | ClientOnly           | The `UNITY_CLIENT` define will be set in the build (**but not in-editor**).                            | 
| Dedicated Game Server | n/a                   | The `UNITY_SERVER` define will be set in the build (**but not in-editor**).                           |

For either build type, specific baking filters can be specified in the `DOTS` `ProjectSettings`:

## Excluded Baking System Assemblies
To build a standalone server, you need to switch to a `Dedicated Server` platform. When building a server, the `UNITY_SERVER` define is set automatically (and also automatically set in the editor). <br/>
The `DOTS` project setting will reflect this change, by using the setting for the server build type.

## Additional Scripting Defines
Use the following scripting defines to determine mode-specific baking settings (via `Excluded Baking System Assemblies` and `Additional Scripting Defines`) for both the editor and builds. For example, the inclusion and exclusion of specific C# assemblies.

| Setting                           | Description    |
|---------------------------------------|-------------------|
| **Netcode Client Target**            | Determine whether or not you want the resulting client build to support hosting a game (as a server). |
| **Excluded Baking System Assemblies** | Add assembly definition assets to exclude from the baking system. You can set this for both client and server setups. |
| **Additional Scripting Defines**      | Add additional [scripting defines](https://docs.unity3d.com/Manual/CustomScriptingSymbols.html) to exclude specific client or server code from compilation. |

## Additional resources

* [Entities Project Settings reference](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/manual/editor-project-settings.html)
