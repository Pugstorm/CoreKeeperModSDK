# Systems window reference

The Systems window displays information about the system update order of each [world](concepts-worlds.md) in your project. The window displays a hierarchy of systems, shown inside their [system groups](concepts-systems.md#system-groups), and updates when the systems in your application run and update. 

To open the Systems window, go to **Window &gt; Entities &gt; Systems**. 

![](images/editor-system-window.png)<br/>_Systems window with some systems temporarily disabled_

This view shows:

|**Column**|**Description**|
|---|---|
|Systems|A list of the systems in your application. When you select a system, Unity displays its information in the [Inspector window](editor-system-inspector.md). There are several icons that represent the system types: |
||![](images/editor-system-group.png) A system group|
||![](images/editor-system.png) A system|
||![](images/editor-system-start-step.png) An [entity command buffer](systems-entity-command-buffers.md) system that runs at the beginning of a system group using the [OrderFirst](xref:Unity.Entities.UpdateInGroupAttribute.OrderFirst) argument.|
||![](images/editor-system-end-step.png) An [entity command buffer system](systems-entity-command-buffers.md) that runs at the end of a system group using the [OrderLast](xref:Unity.Entities.UpdateInGroupAttribute.OrderLast) argument.|
|World|The world where the system operates.<br/><br/>**Note:** The world that calls `Update` on a system, and the world that this system operates on, might be different. This is because the ECS framework only automatically ticks the Main World (in Play mode), and the Editor World (in Edit mode). <br/><br/>This means that it doesn’t automatically run any worlds that you’ve created. If you want your systems to run automatically, you must add them to the Main World, even though they still run against the entities in your custom world. |
|Namespace|The namespace that the system type belongs to.|
|Entity Count|The number of entities that match the system’s [queries](systems-entityquery.md) at the end of the frame.|
|Time (ms)|The amount of time in milliseconds that the system took during this frame. |

To hide a column, select the More menu (⋮) and disable any of the columns you want to hide. 

To help debug your application, you might want to temporarily disable a system. To do this, click the left-most column, which is darker than the other columns, next to the system you want to disable. This change doesn’t persist across Editor sessions. 

To see all the low-level methods that form part of the Unity [player loop](https://docs.unity3d.com/ScriptReference/LowLevel.PlayerLoop.html), including non Entities-related methods, in the More menu (⋮) enable the **Show Full Player Loop** setting. When you enable this setting, any non Entities-related methods are displayed in the window, but are grayed out, and you can't click on them to display further information in the Inspector window.

## Additional resources

* [System user manual](concepts-systems.md)
* [System Inspector reference](editor-system-inspector.md)
* [World user manual](concepts-worlds.md)
* [Entity Query user manual](systems-entityquery.md)


