using Doc.CodeSamples.Tests;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

// The files in this namespace are used to compile/test the code samples in the documentation.
// Snippets used in entity_command_buffer.md
namespace Doc.CodeSamples.Tests
{
    partial struct EcbParallel : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            #region ecb_parallel

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Methods of this writer record commands to
            // the EntityCommandBuffer in a thread-safe way.
            EntityCommandBuffer.ParallelWriter parallelEcb = ecb.AsParallelWriter();

            #endregion
        }
    }

    #region ecb_parallel_for

    public struct HealthLevel : IComponentData
    {
        public int Value;
    }

    partial class EcbParallelFor : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities
                .WithDeferredPlaybackSystem<EndSimulationEntityCommandBufferSystem>()
                .ForEach(
                    (Entity entity, EntityCommandBuffer ecb, in HealthLevel health) =>
                    {
                        if (health.Value == 0)
                        {
                            ecb.DestroyEntity(entity);
                        }
                    }
                ).ScheduleParallel();
        }
    }

    #endregion

    #region ecb_concurrent

    struct Lifetime : IComponentData
    {
        public byte Value;
    }

    [RequireMatchingQueriesForUpdate]
    partial class LifetimeSystem : SystemBase
    {
        EndSimulationEntityCommandBufferSystem m_EndSimulationEcbSystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            // Find the ECB system once and store it for later usage
            m_EndSimulationEcbSystem = World
                .GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            // Acquire an ECB and convert it to a concurrent one to be able
            // to use it from a parallel job.
            var ecb = m_EndSimulationEcbSystem.CreateCommandBuffer().AsParallelWriter();
            Entities
                .ForEach((Entity entity, int entityInQueryIndex, ref Lifetime lifetime) =>
                {
                    // Track the lifetime of an entity and destroy it once
                    // the lifetime reaches zero
                    if (lifetime.Value == 0)
                    {
                        // pass the entityInQueryIndex to the operation so
                        // the ECB can play back the commands in the right
                        // order
                        ecb.DestroyEntity(entityInQueryIndex, entity);
                    }
                    else
                    {
                        lifetime.Value -= 1;
                    }
                }).ScheduleParallel();

            // Make sure that the ECB system knows about our job
            m_EndSimulationEcbSystem.AddJobHandleForProducer(this.Dependency);
        }
    }

    #endregion

    public struct FooComp : IComponentData
    {
        public int Value;
    }

    public struct BarComp : IComponentData
    {
        public Entity TargetEnt;
    }

    [RequireMatchingQueriesForUpdate]
    partial class SingleThreadedSchedule_ECB : SystemBase
    {
        #region ecb_single_threaded

        protected override void OnUpdate()
        {
            // You don't specify a size because the buffer will grow as needed.
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

            // The ECB is captured by the ForEach job.
            // Until completed, the job owns the ECB's job safety handle.
            Entities
                .ForEach((Entity e, in FooComp foo) =>
                {
                    if (foo.Value > 0)
                    {
                        // Record a command that will later add BarComp to the entity.
                        ecb.AddComponent<BarComp>(e);
                    }
                }).Schedule();

            Dependency.Complete();

            // Now that the job is completed, you can enact the changes.
            // Note that Playback can only be called on the main thread.
            ecb.Playback(EntityManager);

            // You are responsible for disposing of any ECB you create.
            ecb.Dispose();
        }

        #endregion
    }

    #region ecb_multi_threaded

    [RequireMatchingQueriesForUpdate]
    partial struct MultiThreadedSchedule_ECB : ISystem
    {
        partial struct ParallelRecordingJob : IJobEntity
        {
            internal EntityCommandBuffer.ParallelWriter ecbParallel;

            // The ChunkIndexInQuery is unique for each chunk in the query and will be
            // consistent regardless of scheduling. This will result in deterministic
            // playback of the ECB.
            void Execute(Entity e, [ChunkIndexInQuery] int sortKey, in FooComp foo)
            {
                if (foo.Value > 0)
                {
                    // The first arg is the 'sort key' recorded with the command.
                    ecbParallel.AddComponent<BarComp>(sortKey, e);
                }
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

            // We need to write to the ECB concurrently across threads.
            new ParallelRecordingJob { ecbParallel = ecb.AsParallelWriter() }.Schedule();

            // Playback is single-threaded as normal. Note that explicitly completing
            // adds a sync point, and is only done here for demonstration purposes.
            // (Having an existing EntityCommandBufferSystem do the playback would not
            // introduce an extra sync point.)
            state.Dependency.Complete();

            // To ensure deterministic playback order,
            // the commands are first sorted by their sort keys.
            ecb.Playback(state.EntityManager);

            ecb.Dispose();
        }
    }

    #endregion

    partial struct SystemContainingAllTheOtherCodeSnippets : ISystem
    {
        partial struct MyParallelRecordingJob : IJobEntity
        {
            internal EntityCommandBuffer.ParallelWriter ecbParallel;

            // The ChunkIndexInQuery is unique for each chunk in the query and will be
            // consistent regardless of scheduling. This will result in deterministic
            // playback of the ECB.
            void Execute(Entity e, [ChunkIndexInQuery] int sortKey, in FooComp foo)
            {
                if (foo.Value > 0)
                {
                    // The first arg is the 'sort key' recorded with the command.
                    ecbParallel.AddComponent<BarComp>(sortKey, e);
                }
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            {
                #region ecb_multi_playback

                // ... in a system update

                EntityCommandBuffer ecb =
                    new EntityCommandBuffer(Allocator.TempJob, PlaybackPolicy.MultiPlayback);

                // ... record commands

                ecb.Playback(state.EntityManager);

                // Additional playbacks are OK because this ECB is MultiPlayback.
                ecb.Playback(state.EntityManager);

                ecb.Dispose();

                #endregion
            }

            {
                #region ecb_from_ecbsystem

                // ... in a system

                // Assume an EntityCommandBufferSystem exists named FooECBSystem.
                // This call to GetSingleton automatically registers the job so that
                // it gets completed by the ECB system.
                var singleton = SystemAPI.GetSingleton<FooECBSystem.Singleton>();

                // Create a command buffer that will be played back
                // and disposed by MyECBSystem.
                EntityCommandBuffer ecb = singleton.CreateCommandBuffer(state.WorldUnmanaged);

                // An IJobEntity with no argument to Schedule implicitly
                // assigns its returned JobHandle to this.Dependency
                new MyParallelRecordingJob() { ecbParallel = ecb.AsParallelWriter() }.Schedule();

                #endregion
            }

            {
                #region ecb_deferred_entities

                // ... in a system

                EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

                Entity placeholderEntity = ecb.CreateEntity();

                // Valid to use placeholderEntity in later commands of same ECB.
                ecb.AddComponent<FooComp>(placeholderEntity);

                // The real entity is created, and
                // FooComp is added to the real entity.
                ecb.Playback(state.EntityManager);

                // Exception! The placeholderEntity has no meaning outside
                // the ECB which created it, even after playback.
                state.EntityManager.AddComponent<BarComp>(placeholderEntity);

                ecb.Dispose();

                #endregion
            }

            {
                #region ecb_deferred_remapping

                // ... in a system

                EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

                // For all entities with a FooComp component...
                foreach (var (f, e) in SystemAPI.Query<FooComp>().WithEntityAccess())
                {
                    // In playback, an actual entity will be created
                    // that corresponds to this placeholder entity.
                    Entity placeholderEntity = ecb.CreateEntity();

                    // (Assume BarComp has an Entity field called TargetEnt.)
                    BarComp bar = new BarComp { TargetEnt = placeholderEntity };

                    // In playback, TargetEnt will be assigned the
                    // actual Entity that corresponds to placeholderEntity.
                    ecb.AddComponent(e, bar);
                }

                // After playback, each entity with FooComp now has a
                // BarComp component whose TargetEnt references a new entity.
                ecb.Playback(state.EntityManager);

                ecb.Dispose();

                #endregion
            }
        }
    }

    public partial class FooSystem : SystemBase
    {
        protected override void OnUpdate() { }
    }

    public partial class FooECBSystem : EntityCommandBufferSystem
    {
        public unsafe struct Singleton : IComponentData, IECBSingleton
        {
            internal UnsafeList<EntityCommandBuffer>* pendingBuffers;
            internal AllocatorManager.AllocatorHandle allocator;

            public EntityCommandBuffer CreateCommandBuffer(WorldUnmanaged world)
            {
                return EntityCommandBufferSystem.CreateCommandBuffer(ref *pendingBuffers, allocator, world);
            }

            // Required by IECBSingleton
            public void SetPendingBufferList(ref UnsafeList<EntityCommandBuffer> buffers)
            {
                pendingBuffers = (UnsafeList<EntityCommandBuffer>*)UnsafeUtility.AddressOf(ref buffers);
            }

            // Required by IECBSingleton
            public void SetAllocator(Allocator allocatorIn)
            {
                allocator = allocatorIn;
            }

            // Required by IECBSingleton
            public void SetAllocator(AllocatorManager.AllocatorHandle allocatorIn)
            {
                allocator = allocatorIn;
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            this.RegisterSingleton<Singleton>(ref PendingBuffers, World.Unmanaged);
        }
    }


    #region ecb_define_ecbsystem

    // You should specify where exactly in the frame this ECB system should update.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FooSystem))]
    public partial class MyECBSystem : EntityCommandBufferSystem
    {
        // The singleton component data access pattern should be used to safely access
        // the command buffer system. This data will be stored in the derived ECB System's
        // system entity.

        public unsafe struct Singleton : IComponentData, IECBSingleton
        {
            internal UnsafeList<EntityCommandBuffer>* pendingBuffers;
            internal AllocatorManager.AllocatorHandle allocator;

            public EntityCommandBuffer CreateCommandBuffer(WorldUnmanaged world)
            {
                return EntityCommandBufferSystem
                    .CreateCommandBuffer(ref *pendingBuffers, allocator, world);
            }

            // Required by IECBSingleton
            public void SetPendingBufferList(ref UnsafeList<EntityCommandBuffer> buffers)
            {
                var ptr = UnsafeUtility.AddressOf(ref buffers);
                pendingBuffers = (UnsafeList<EntityCommandBuffer>*)ptr;
            }

            // Required by IECBSingleton
            public void SetAllocator(Allocator allocatorIn)
            {
                allocator = allocatorIn;
            }

            // Required by IECBSingleton
            public void SetAllocator(AllocatorManager.AllocatorHandle allocatorIn)
            {
                allocator = allocatorIn;
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            this.RegisterSingleton<Singleton>(ref PendingBuffers, World.Unmanaged);
        }
    }

    #endregion

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FooSystem))]
    public partial class NonSingletonECBSystem : EntityCommandBufferSystem {
        // This class is intentionally empty. There is generally no
        // reason to put any code in an EntityCommandBufferSystem.
    }

    // 888       888        d8888 8888888b.  888b    888 8888888 888b    888  .d8888b.
    // 888   o   888       d88888 888   Y88b 8888b   888   888   8888b   888 d88P  Y88b
    // 888  d8b  888      d88P888 888    888 88888b  888   888   88888b  888 888    888
    // 888 d888b 888     d88P 888 888   d88P 888Y88b 888   888   888Y88b 888 888
    // 888d88888b888    d88P  888 8888888P"  888 Y88b888   888   888 Y88b888 888  88888
    // 88888P Y88888   d88P   888 888 T88b   888  Y88888   888   888  Y88888 888    888
    // 8888P   Y8888  d8888888888 888  T88b  888   Y8888   888   888   Y8888 Y88b  d88P
    // 888P     Y888 d88P     888 888   T88b 888    Y888 8888888 888    Y888  "Y8888P88

    // ###########################################################
    // ## DO NOT CHANGE THE CODE BELOW TO USE A SINGLETON,      ##
    // ## IT'S USED IN THE API DOCS FOR AddJobHandleForProducer ##
    // ###########################################################

    #region ecb_addjobhandleforproducer

    public struct ProcessInfo : IComponentData { public float Value; }
    public struct ProcessCompleteTag : IComponentData { }

    [BurstCompile]
    public partial struct ProcessInBackgroundJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ConcurrentCommands;

        public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, in ProcessInfo info)
        {
            // ...hypothetical processing goes here...

            // Remove ProcessInfo and add a ProcessCompleteTag...
            ConcurrentCommands.RemoveComponent<ProcessInfo>(chunkIndex, entity);
            ConcurrentCommands.AddComponent<ProcessCompleteTag>(chunkIndex, entity);
        }
    }

    public partial class AsyncProcessJobSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Get a reference to the system that will play back
            var ecbSystem = World.GetOrCreateSystemManaged<NonSingletonECBSystem>();
            var ecb = ecbSystem.CreateCommandBuffer();

            // Pass the command buffer to the writer job, using its ParallelWriter interface
            var job = new ProcessInBackgroundJob
            {
                ConcurrentCommands = ecb.AsParallelWriter(),
            };
            Dependency = job.ScheduleParallel(Dependency);

            // Register the writer job with the playback system as an input dependency
            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }

    #endregion

}