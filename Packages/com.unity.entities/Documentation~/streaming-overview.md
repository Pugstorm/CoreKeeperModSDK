# Scene streaming overview

Loading large scenes might take several frames. To avoid stalls, all scene loading in Entities is asynchronous. This is called **streaming**.

The main advantages of streaming are:

* Your application can remain responsive while Unity streams scenes in the background.
* Unity can dynamically load and unload scenes in seamless worlds that are larger than can fit memory without interrupting gameplay.
* In Play mode, if an entity scene file is missing or outdated, Unity converts the scene on demand. Because the [baking](baking-overview.md) and loading of the entity scene happens asynchronously and in a separate process, the Editor remains responsive.

The main disadvantages of streaming are:

* Your application can't assume scene data is present, particularly at startup. This might make your code a bit more complicated.
* Systems load scenes from the scene system group, which is part of the [initialization group](systems-update-order.md#default-system-groups). Systems that update later in the frame receive the loaded data in the same frame, but systems that update earlier than that group don't receive the loaded data until the next frame. Your code might need to take this into consideration.


## Additional resources

* [Load a scene](streaming-loading-scenes.md)
* [Subscenes](conversion-subscenes.md)