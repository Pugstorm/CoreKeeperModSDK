# Entities Preferences reference

The **Preferences** window in the Editor contains some specific Entities settings, as follows:

|**Property**|**Function**|
|---|---|
|**Baking**||
|Scene View Mode|Choose the [data mode](editor-authoring-runtime.md) for Scene view. You can choose from **Authoring Data** or **Runtime Data**. |
|Live Baking Logging| Enable this property to output a log of live baking triggers. This can help diagnose what causes [baking](baking-overview.md) to happen.|
|Clear Entity cache|Forces Unity to re-bake all Sub Scenes the next time they're loaded in the Editor, or when making a standalone player build.|
|**Advanced**||
|Show Advanced Worlds|Enable this property to show advanced worlds in the different world dropdowns. Advanced worlds are specialized worlds like the Staging world or the Streaming world which serve as support to the [main worlds](concepts-worlds.md).|
|**Hierarchy Window**||
|Update Mode|Set how to update the [Entities Hierarchy](editor-hierarchy-window.md) window. <br/><br/>**Synchronous:** Updates the hierarchy in a blocking manner. Data is always up to date, but might impact performance.<br/><br/>**Asynchronous:** Updates the hierarchy in a non-blocking manner, over multiple frames if needed. Data might be stale for a few frames, but the impact on performance is minimized.|
|Minimum Milliseconds Between Hierarchy Update Cycle|Set the minimum amount of time to wait between hierarchy update cycles in milliseconds. Increase this value to update the [Entities Hierarchy window](editor-hierarchy-window.md) less frequently, which has a lower impact on performance.|
|Exclude Unnamed Nodes For Search|Enable this property to exclude unnamed entities in the results of searching by string. If there are a lot of unnamed entities, this can speed up searching.|
|**Journaling**||
|Enabled|Enable this property to enable [Journaling data](entities-journaling.md) recording.|
|Total Memory MB|Set the amount of memory in megabytes allocated to store Journaling record data. Once full, new records overwrite older records.|
|Post Process|Enable this property to post-process journaling data in the Journaling window. This includes operations such as converting `GetComponentDataRW` to `SetComponentData` when possible.|
|**Systems Window**||
|Show 0s in Entity Count And Time Column|Enable this property to display `0` in the `Entity Count` column when a system doesn't match any entities. If you disable this property, Unity displays nothing in the `Entity Count` column when a system doesn't match any entities.|
|Show More Precision For Running Time|Enable this property to increase the precision from 2 to 4 decimal places for the system running times in the `Time (ms)` column.|
