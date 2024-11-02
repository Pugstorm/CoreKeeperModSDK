# Working with authoring and runtime data

The [Entities Hierarchy window](editor-hierarchy-window.md) and the [Entity inspector](editor-entity-inspector.md) has the following modes which represent the kind of data that you control:

* **Authoring mode**: Contains data that's version controlled (for example, assets, scene GameObjects). Represented by a white ![](images/editor-authoring-mode-dark.png) or gray ![](images/editor-authoring-mode-light.png) circle in the Editor.
* **Runtime mode**: Contains data that the runtime uses and modifies. For example, the data or state Unity destroys when you exit Play mode. Represented by an orange ![](images/editor-runtime-mode-dark.png) or red ![](images/editor-runtime-mode-light.png) circle in the Editor.
* **Mixed mode**: Represents a view that can see both runtime and authoring data, but authoring data takes precedent. Represented by a white and orange ![](images/editor-mixed-mode-dark.png) or gray and red ![](images/editor-mixed-mode-light.png) circle in the Editor.

It's useful to be able to switch between data modes while in Play mode and Edit mode, so that you can make permanent changes to your application without having to enter or exit Play mode. For example, you could make changes to a level's geometry while in Play mode and save it while remaining in Play mode. 

You can also set the Scene view to display only authoring data (**Preferences &gt; Entities &gt; Baking &gt; Scene View Mode**). This is useful if there are a lot of elements that Unity generates at runtime which might clutter the Scene view in Runtime mode. For more information, see [Entities Preferences reference](editor-preferences.md)

You can also switch to Runtime data mode while in Edit mode to see how Unity bakes and optimizes GameObjects without entering Play mode.

The Hierarchy and Inspector windows highlight all runtime data that Unity destroys when you exit Play mode in the following colors:

* Orange if you're using the Editor Dark theme
* Red if you're using the Editor Light theme

This highlight makes it easier to see which data doesn't persist between modes.

![](images/editor-data-modes.png)<br/>_Entities Hierarchy window and Inspector with data modes highlighted. Clockwise, from top left, Entities Hierarchy in Runtime data mode, Inspector in Authoring data mode, and Entities Hierarchy in Mixed data mode. Addionally, Data that Unity destroys when you exit Play mode has an orange or red vertical bar._

## Default behavior

To change the data mode of a window, select the data mode circle in the top right of the window. You can choose from:

* Automatic
* Authoring
* Mixed
* Runtime

In Automatic mode, Unity automatically selects the appropriate data mode based on your selection and whether you're in Edit or Play mode.

|**Operation**|**Default data mode**|
|---|---|
|Select an entity| Entity Inspector set to Runtime data mode.|
|Select a GameObject |In Edit mode: Entity Inspector set to Authoring data mode. <br/>Inside a Sub Scene in Play mode: Entity Inspector set to Authoring data mode. <br/> Outside a Sub Scene in Play mode: Entity Inspector set to Runtime data mode. |
|Enter Play mode|Entities Hierarchy and Entity Inspector set to Mixed data mode.|

When you select any of the other modes, Unity locks your window selection to this mode. To indicate that the mode is locked, an underline appears under the data mode circle.

## Authoring subscenes in Play mode

While in Play mode, you can author subscenes. When you exit Play mode, Unity retains any changes that you make to subscene GameObjects that are converted to entities at runtime.

## Additional resources

* [Entities Hierarchy window reference](editor-hierarchy-window.md)
* [Entity Inspector reference](editor-entity-inspector.md)
