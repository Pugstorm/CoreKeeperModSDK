# Entity Inspector reference

When you select an entity, the [Inspector](https://docs.unity3d.com/Manual/UsingTheInspector.html) displays its information in different ways depending on whether you're in [Authoring, Runtime, or Mixed data mode](editor-authoring-runtime.md).

## Authoring data mode

In Authoring data mode, represented by a white ![](images/editor-authoring-mode-dark.png) or gray ![](images/editor-authoring-mode-light.png) circle, the Inspector window displays information about the selected GameObject, which you can then use to [edit and change the properties of the GameObject](https://docs.unity3d.com/Manual/UsingTheInspector.html). If you select an entity in the [Entity Hierarchy window](editor-hierarchy-window.md), the Inspector window displays information about the entity's corresponding authoring GameObject.

## Runtime data mode

In Runtime data mode, represented by an orange ![](images/editor-runtime-mode-dark.png) or red ![](images/editor-runtime-mode-light.png) circle, the Inspector window displays data about the selected entity in three tabs:

* **Components:** Displays all the [components](concepts-components.md) on an entity, similar to displaying the MonoBehaviours on a GameObject. 
* **Aspects:** Displays information about the [aspects](aspects-intro.md) associated with the selected entity.
* **Relationships:** Displays all the [systems](concepts-systems.md) that interact with the selected entity. This tab displays information only if the entity has one or more components that satisfy a [system query](systems-entityquery.md).

### Components and Aspects tab

The fields in the **Components** and **Aspects** tab have two states:

* In Edit mode, they're read-only. 
* In Play mode, you can edit them for debugging purposes. When you exit Play mode, the GameObject conversion process overrides any changes you made, indicated by the orange or red vertical bars next to the fields.

![](images/editor-entity-inspector-runtime-mode.png)<br/>_Entity Inspector in Edit mode (Left), and Play mode (Right). Note the orange vertical bars in Play mode that indicate Unity destroys the data when you exit Play mode._

### Relationships tab

The Relationships tab displays the System queries that match the selected Entity. This view also displays the System’s access rights to the Components (**Read** or **Read & Write**). 

Click on the icon to the right of a System or Component name (![](images/editor-go-to.png)), to change the selection to that System or Component. Unity also opens the respective [System Inspector](editor-system-inspector.md) or [Component Inspector](editor-component-inspector.md) where possible.

To see a list of all the Entities that match a query, click on the icon (![](images/editor-new-window.png)) next to a query. Unity opens the [Query window](editor-query-window.md).

![](images/editor-entity-inspector-relationships.png)<br/>_Entity Inspector Relationship tab_

## Mixed data mode

Mixed data mode is available only in Play mode and is represented by a white and orange ![](images/editor-mixed-mode-dark.png) or gray and red ![](images/editor-mixed-mode-light.png) circle. In this mode, properties that have an orange vertical bar have their values overwritten by the corresponding value on the entity. These indicators mean that if you edit these fields, they change the data on the corresponding entity. However, Unity doesn't keep this data when you exit Play mode. Editing any fields that don't have an orange vertical bar edits the authoring value on the GameObject, and Unity retains this data when you exit Play mode.

![](images/editor-entity-inspector-mixed-mode.png)<br/>_Entity Inspector in Mixed data mode_


## Additional resources

* [Entities user manual](concepts-entities.md)
* [Entities Hierarchy window reference](editor-hierarchy-window.md)
* [System Inspector reference](editor-system-inspector.md)
* [Component Inspector reference](editor-component-inspector.md)
* [Query window reference](editor-query-window.md)