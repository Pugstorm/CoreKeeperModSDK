# Query window reference

The Query window displays detailed information about a selected [query](systems-entityquery.md).

To open the Query window, open the Relationship tab in an [Entity, System, or Component Inspector](editor-inspectors.md), and select the button ![](images/editor-new-window.png) next to a query. Unity then opens a window with information about the query's related components and Entities.

![](images/editor-query-window-highlight.png)<br/>_Query window button highlighted in the Relationships tab of an Entity Inspector._

The Query window has two tabs: 

* **Components:** Displays a list of components that the selected query searches for. It also displays the access rights to the component (**Read** or **Read & Write**). 
* **Entities:** Displays a list of entities that match the query.

![](images/editor-query-windows.png)<br/>_Query window, Component view (left), Entities view (right)_

The top of the tab displays the query number, which is the order in which the query is declared in the C# system definition. To see more information about any component or entity in this view, select it and the Inspector displays more information about it. To see information about the system associated with this query, click the icon next to the system name (![](images/editor-go-to.png)) which changes the selection to that system, and displays the [System Inspector](editor-system-inspector.md) where possible.

## Additional resources

* [Entity Query user manual](systems-entityquery.md)
* [System Inspector reference](editor-system-inspector.md)