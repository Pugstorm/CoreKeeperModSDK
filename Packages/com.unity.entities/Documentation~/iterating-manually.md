# Iterate manually over data

If you need to manage chunks in a way that isn't appropriate for the simplified model of iterating over all the chunks in an [`EntityQuery`](systems-entityquery.md), you can manually request all the [archetype chunks](concepts-archetypes.md#archetype-chunks) explicitly in a [native array](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.html) and process them with a job such as `IJobParallelFor`. The following is an example of this:

```c#
public partial class RotationSpeedSystem : SystemBase
{
   [BurstCompile]
   struct RotationSpeedJob : IJobParallelFor
   {
       [DeallocateOnJobCompletion] public NativeArray<ArchetypeChunk> Chunks;
       public ComponentTypeHandle<RotationQuaternion> RotationType;
       [ReadOnly] public ComponentTypeHandle<RotationSpeed> RotationSpeedType;
       public float DeltaTime;

       public void Execute(int chunkIndex)
       {
           var chunk = Chunks[chunkIndex];
           var chunkRotation = chunk.GetNativeArray(RotationType);
           var chunkSpeed = chunk.GetNativeArray(RotationSpeedType);
           var instanceCount = chunk.Count;

           for (int i = 0; i < instanceCount; i++)
           {
               var rotation = chunkRotation[i];
               var speed = chunkSpeed[i];
               rotation.Value = math.mul(math.normalize(rotation.Value), quaternion.AxisAngle(math.up(), speed.RadiansPerSecond * DeltaTime));
               chunkRotation[i] = rotation;
           }
       }
   }
   
   EntityQuery m_Query;   

   protected override void OnCreate()
   {
       m_Query = new EntityQueryDescBuilder(Allocator.Temp)
       .WithAllRW<RotationQuaternion>()
       .WithAll<RotationSpeed>()
       .Build();
   }

   protected override void OnUpdate()
   {
       var rotationType = GetComponentTypeHandle<RotationQuaternion>();
       var rotationSpeedType = GetComponentTypeHandle<RotationSpeed>(true);
       var chunks = m_Query.ToArchetypeChunkArray(Allocator.TempJob);
       
       var rotationsSpeedJob = new RotationSpeedJob
       {
           Chunks = chunks,
           RotationType = rotationType,
           RotationSpeedType = rotationSpeedType,
           DeltaTime = Time.deltaTime
       };
       this.Dependency = rotationsSpeedJob.Schedule(chunks.Length,32, this.Dependency);
   }
}
```

## How to manually iterate over data

You can use the `EntityManager` class to manually iterate through entities or archetype chunks, but this is usually inefficient. You should generally only use these iteration methods to test or debug your code, or in an isolated world where you have a controlled set of entities.

For example, the following snippet iterates through all the entities in the active world:

``` c#
var entityManager = World.Active.EntityManager;
var allEntities = entityManager.GetAllEntities();
foreach (var entity in allEntities)
{
   //...
}
allEntities.Dispose();
```

 This snippet iterates through all the chunks in the active world:

``` c#
var entityManager = World.Active.EntityManager;
var allChunks = entityManager.GetAllChunks();
foreach (var chunk in allChunks)
{
   //...
}
allChunks.Dispose();
```
