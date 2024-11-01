# Job dependencies

Unity analyzes the data dependencies of each system based on the ECS components that the system reads and writes. If a system that updates earlier in the frame reads data that a later system writes, or writes data that a later system reads, then the second system depends on the first. To prevent [race conditions](https://en.wikipedia.org/wiki/Race_condition), the job scheduler makes sure that all the jobs a system depends on have finished before it runs that system's jobs. 

## Job dependency update order

A system's [`Dependency`](xref:Unity.Entities.SystemBase.Dependency) property is a [`JobHandle`](https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.html) that represents the ECS-related dependencies of the system. Before [`OnUpdate()`](xref:Unity.Entities.SystemBase.OnUpdate*), the `Dependency` property reflects the incoming dependencies that the system has on prior jobs. By default, the system updates the `Dependency` property based on the components that each job reads and writes as you schedule jobs in a system. 

### Override the default order

To override this default behavior, use the overloaded versions of [`Entities.ForEach`](xref:Unity.Entities.SystemBase.Entities) and [`Job.WithCode`](xref:Unity.Entities.SystemBase.Job) that take job dependencies as a parameter and returns the updated dependencies as a `JobHandle`. When you use the explicit versions of these constructions, ECS doesn't automatically combine the job handles with the system's `Dependency` property. You must combine them manually when required. 

The `Dependency` property doesn't track the dependencies that a job might have on data passed through a [`NativeArray`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.html) or other similar containers. If you write a `NativeArray` in one job, and read that array in another, you must manually add the `JobHandle` of the first job as a dependency of the second. You can use [`JobHandle.CombineDependencies`](https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.CombineDependencies.html) to do this.

### Job dependency order with `Entities.ForEach`

When you call [`Entities.ForEach.Run()`](xref:Unity.Entities.SystemBase.Entities) the job scheduler completes all scheduled jobs that the system depends on before starting the `ForEach` iteration. If you also use [`WithStructuralChanges()`](xref:Unity.Entities.SystemBase.Entities) as part of the construction, then the job scheduler completes all running and scheduled jobs. Structural changes also invalidate any direct references to component data. For more information, see the documentation on [Structural changes](concepts-structural-changes.md).

## Further resources
* [JobHandle and dependencies](https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.CombineDependencies.html)
* [Unity's job system](https://docs.unity3d.com/Manual/JobSystem.html)
