# Native container component support

When writing game and engine code, we often find that we need to maintain and update a single long-lived datastructure, which may potentially be used by multiple systems. We've found it convenient
in engine code to put such containers on [Singleton](components-singleton.md) components.

When a Native container is on a component, we enforce some restrictions on that component to ensure safety. In particular, we do not allow scheduling jobs against those components with [IJobChunk](iterating-data-ijobchunk.md) or [IJobEntity](iterating-data-ijobentity.md). This is because those jobs already access those components via containers, and the job safety system doesn't scan for nested containers, because that would make jobs take too long to schedule.

However, we do allow scheduling jobs against the containers themselves, so we get the component on the main thread, extract the container, and schedule the job against the container itself. The Singleton functions
are specifically designed for this case and avoid completing unnecessary dependencies for this reason, so that you can chain jobs across multiple systems against the same container in a singleton component without
creating a sync point. 
