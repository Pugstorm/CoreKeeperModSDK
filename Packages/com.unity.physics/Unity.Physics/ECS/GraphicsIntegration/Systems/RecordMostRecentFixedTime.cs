using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Physics.GraphicsIntegration
{
    //Declaring big capacity since buffers with RigidBodySmoothingWorldIndex and MostRecentFixedTime will be stored together in a singleton Entity
    //and that Entity will get a whole Chunk allocated anyway. This capacity is just a limit for keeping the buffer inside the Chunk, reducing it does not affect
    //memory consumption

    /// <summary>
    /// Singleton dynamic buffer that record the last <c>Time.ElapsedTime</c> and <c>Time.DeltaTime</c>
    /// of the most recent tick for each stepped Physics World. The <seealso cref="PhysicsWorldIndex"/>
    /// value is used as index to store and retrieve the timing data. Because of that, the dynamic
    /// buffer size is always equals to the largest PhysicsWorldIndex value set by the application.
    /// </summary>
    [InternalBufferCapacity(256)]
    public struct MostRecentFixedTime : IBufferElementData
    {
        /// <summary>   The delta time. </summary>
        public double DeltaTime;
        /// <summary>   The elapsed time. </summary>
        public double ElapsedTime;
    }

    /// <summary>
    /// A system to keep track of the time values in the most recent tick of the <c>
    /// PhysicsSystemGroup</c>.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsInitializeGroup)), UpdateBefore(typeof(ExportPhysicsWorld))]
    [BurstCompile]
    public partial struct RecordMostRecentFixedTime : ISystem
    {
        private NativeHashSet<PhysicsWorldIndex> m_initializedWorlds;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_initializedWorlds = new NativeHashSet<PhysicsWorldIndex>(8, Allocator.Persistent);
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<MostRecentFixedTime>();
        }

        public void OnDestroy(ref SystemState state)
        {
            m_initializedWorlds.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var worldIndex = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorldIndex;
            if (!m_initializedWorlds.Contains(worldIndex))
            {
                var mostRecentTimeEntity = SystemAPI.GetSingletonEntity<MostRecentFixedTime>();
                //Let the graphics system smooth the rigid body motion for the physics world using recent smooth time.
                SmoothRigidBodiesGraphicalMotion.RegisterPhysicsWorldForSmoothRigidBodyMotion(ref state, mostRecentTimeEntity, worldIndex);
                m_initializedWorlds.Add(worldIndex);
            }
            
            var setMostRecentFixedTimeJob = new SetMostRecentFixedTimeJob
            {
                ElapsedTime = SystemAPI.Time.ElapsedTime,
                DeltaTime = SystemAPI.Time.DeltaTime,
                WorldIndex = (int)worldIndex.Value,
                MostRecentFixedTimeBufferEntity = SystemAPI.GetSingletonEntity<MostRecentFixedTime>(),
                MostRecentFixedTimeBufferLookup = state.GetBufferLookup<MostRecentFixedTime>()
            };

            state.Dependency = setMostRecentFixedTimeJob.Schedule(state.Dependency);
        }

        [BurstCompile]
        private partial struct SetMostRecentFixedTimeJob : IJob
        {
            public double ElapsedTime;
            public float DeltaTime;
            public int WorldIndex;

            public Entity MostRecentFixedTimeBufferEntity;
            public BufferLookup<MostRecentFixedTime> MostRecentFixedTimeBufferLookup;
            
            public void Execute()
            {
                var mostRecentFixedTimeBuffer = MostRecentFixedTimeBufferLookup[MostRecentFixedTimeBufferEntity];
                
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (WorldIndex >= mostRecentFixedTimeBuffer.Length)
                {
                    throw new IndexOutOfRangeException("WorldIndex is out of range.");
                }
#endif
                mostRecentFixedTimeBuffer[WorldIndex] = new MostRecentFixedTime
                {
                    ElapsedTime = ElapsedTime,
                    DeltaTime = DeltaTime
                };
            }
        }
    }
}
