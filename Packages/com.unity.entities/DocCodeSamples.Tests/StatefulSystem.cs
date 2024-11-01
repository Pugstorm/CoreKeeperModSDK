namespace Doc.CodeSamples.Tests
{
    #region stateful-example

    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Collections;

    public struct GeneralPurposeComponentA : IComponentData
    {
        public int Lifetime;
    }

    public struct StateComponentB : ICleanupComponentData
    {
        public int State;
    }

    [RequireMatchingQueriesForUpdate]
    public partial class StatefulSystem : SystemBase
    {
        private EntityCommandBufferSystem ecbSource;

        protected override void OnCreate()
        {
            ecbSource = World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();

            // Create some test entities
            // This runs on the main thread, but it is still faster to use a command buffer
            EntityCommandBuffer creationBuffer = new EntityCommandBuffer(Allocator.Temp);
            EntityArchetype archetype = EntityManager.CreateArchetype(typeof(GeneralPurposeComponentA));
            for (int i = 0; i < 10000; i++)
            {
                Entity newEntity = creationBuffer.CreateEntity(archetype);
                creationBuffer.SetComponent<GeneralPurposeComponentA>
                (
                    newEntity,
                    new GeneralPurposeComponentA() { Lifetime = i }
                );
            }
            //Execute the command buffer
            creationBuffer.Playback(EntityManager);
        }

        protected override void OnUpdate()
        {
            EntityCommandBuffer.ParallelWriter parallelWriterECB = ecbSource.CreateCommandBuffer().AsParallelWriter();

            // Entities with GeneralPurposeComponentA but not StateComponentB
            Entities
                .WithNone<StateComponentB>()
                .ForEach(
                    (Entity entity, int entityInQueryIndex, in GeneralPurposeComponentA gpA) =>
                    {
                    // Add an ICleanupComponentData instance
                    parallelWriterECB.AddComponent<StateComponentB>
                        (
                            entityInQueryIndex,
                            entity,
                            new StateComponentB() { State = 1 }
                        );
                    })
                .ScheduleParallel();
            ecbSource.AddJobHandleForProducer(this.Dependency);

            // Create new command buffer
            parallelWriterECB = ecbSource.CreateCommandBuffer().AsParallelWriter();

            // Entities with both GeneralPurposeComponentA and StateComponentB
            Entities
                .WithAll<StateComponentB>()
                .ForEach(
                    (Entity entity,
                     int entityInQueryIndex,
                     ref GeneralPurposeComponentA gpA) =>
                    {
                    // Process entity, in this case by decrementing the Lifetime count
                    gpA.Lifetime--;

                    // If out of time, destroy the entity
                    if (gpA.Lifetime <= 0)
                        {
                            parallelWriterECB.DestroyEntity(entityInQueryIndex, entity);
                        }
                    })
                .ScheduleParallel();
            ecbSource.AddJobHandleForProducer(this.Dependency);

            // Create new command buffer
            parallelWriterECB = ecbSource.CreateCommandBuffer().AsParallelWriter();

            // Entities with StateComponentB but not GeneralPurposeComponentA
            Entities
                .WithAll<StateComponentB>()
                .WithNone<GeneralPurposeComponentA>()
                .ForEach(
                    (Entity entity, int entityInQueryIndex) =>
                    {
                    // This system is responsible for removing any ICleanupComponentData instances it adds
                    // Otherwise, the entity is never truly destroyed.
                    parallelWriterECB.RemoveComponent<StateComponentB>(entityInQueryIndex, entity);
                    })
                .ScheduleParallel();
            ecbSource.AddJobHandleForProducer(this.Dependency);

        }

        protected override void OnDestroy()
        {
            // Implement OnDestroy to cleanup any resources allocated by this system.
            // (This simplified example does not allocate any resources, so there is nothing to clean up.)
        }
    }
    #endregion
}
