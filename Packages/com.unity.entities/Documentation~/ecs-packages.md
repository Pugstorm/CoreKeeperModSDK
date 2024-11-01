# Further DOTS and ECS packages

Unity's data oriented technology stack (DOTS) uses several packages (including Entities), and parts of the Unity engine which work together to help you create high-performance code.

The main parts of Unity you need to use are:

* **Entities** (this package): An implementation of the Entity, Component, System (ECS) pattern.
* [C# Job System](https://docs.unity3d.com/Manual/JobSystem.html): A solution for fast, safe, multi-threaded code.
* [Burst compiler](https://docs.unity3d.com/Packages/com.unity.burst@latest): A C# compiler that generates highly optimized code.
* [Collections](https://docs.unity3d.com/Packages/com.unity.collections@latest): A set of unmanaged collection types, such as lists and hash maps. They're useful in jobs and Burst-compiled code because those contexts can only access unmanaged data.
* [Mathematics](https://docs.unity3d.com/Packages/com.unity.mathematics@latest): A math library which is specially optimized in Burst-compiled code.

Built on top of these core parts are the following entity component system (ECS) packages:

* [Unity Physics](https://docs.unity3d.com/Packages/com.unity.physics@latest): A stateless and deterministic physics system for entities. 
* [Havok Physics for Unity](https://docs.unity3d.com/Packages/com.havok.physics@latest): A stateful and deterministic physics system for entities.
* [Netcode for Entities](https://docs.unity3d.com/Packages/com.unity.netcode@latest): A client-server netcode solution for entities.
* [Entities Graphics](https://docs.unity3d.com/Packages/com.unity.entities.graphics@latest): Uses the scriptable render pipeline (SRP) to render entities.
