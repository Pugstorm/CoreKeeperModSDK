# Reuse a dynamic buffer for multiple entities

If all entities of an [`IJobEntity`](iterating-data-ijobentity.md) need the same buffer, you can get that buffer as a local variable on the main thread before scheduling the job.

The following code example shows how to use the same dynamic buffer for multiple entities. It assumes a dynamic buffer called `MyElement` exists and another component exists called `OtherComponent`.

```c#
public void DynamicBufferExample(Entity e)
{
    var myBuff = SystemAPI.GetBuffer<MyElement>(e);
    new MyJobEntity{MyBuffer  = myBuf}.Schedule();
}
```

> [!NOTE]
> If you use `ScheduleParallel`, you can't write to the dynamic buffer in parallel. Instead, you can use an `EntityCommandBuffer.ParallelWriter` to record changes in parallel. However, any [structural changes](concepts-structural-changes.md) invalidate the buffer.

## Additional resources

* [Access dynamic buffers from jobs](components-buffer-jobs.md)