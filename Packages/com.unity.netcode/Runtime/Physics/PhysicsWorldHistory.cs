using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Mathematics;

[assembly: InternalsVisibleTo("Unity.NetCode.Physics.EditorTests")]
namespace Unity.NetCode
{

    /// <summary>
    /// A singleton component from which you can get a physics collision world for a previous tick.
    /// </summary>
    [AssumeReadOnly]
    public struct PhysicsWorldHistorySingleton : IComponentData
    {
        /// <summary>
        /// Get the <see cref="CollisionWorld"/> state for the given tick and interpolation delay.
        /// </summary>
        /// <param name="tick">The server tick we are simulating.</param>
        /// <param name="interpolationDelay">The client interpolation delay, measured in ticks. This is used to look back in time
        /// and retrieve the state of the collision world at tick - interpolationDelay.
        /// The interpolation delay is internally clamped to the current collision history size (the number of saved history state).</param>
        /// <param name="physicsWorld">The physics world which is use to get collision worlds for ticks which are not yet in the history buffer.</param>
        /// <param name="collWorld">The <see cref="CollisionWorld"/> state retrieved from the history.</param>
        public unsafe void GetCollisionWorldFromTick(NetworkTick tick, uint interpolationDelay, ref PhysicsWorld physicsWorld, out CollisionWorld collWorld)
        {
            var delayedTick = tick;
            delayedTick.Subtract(interpolationDelay);
            if (!m_LastStoreTick.IsValid || delayedTick.IsNewerThan(m_LastStoreTick))
            {
                collWorld = physicsWorld.CollisionWorld;
                return;
            }
            ((CollisionHistoryBuffer*)m_History.Ptr)->GetCollisionWorldFromTick(tick, interpolationDelay, out collWorld);
        }

        internal UnsafeList<CollisionHistoryBuffer> m_History;
        internal NetworkTick m_LastStoreTick;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawHistoryBuffer
    {
        public const int Capacity = 16;

        public CollisionWorld world01;
        public CollisionWorld world02;
        public CollisionWorld world03;
        public CollisionWorld world04;
        public CollisionWorld world05;
        public CollisionWorld world06;
        public CollisionWorld world07;
        public CollisionWorld world08;
        public CollisionWorld world09;
        public CollisionWorld world10;
        public CollisionWorld world11;
        public CollisionWorld world12;
        public CollisionWorld world13;
        public CollisionWorld world14;
        public CollisionWorld world15;
        public CollisionWorld world16;
    }
    internal static class RawHistoryBufferExtension
    {
        public static ref CollisionWorld GetWorldAt(this ref RawHistoryBuffer buffer, int index)
        {
            switch (index)
            {
                case 0: return ref buffer.world01;
                case 1: return ref buffer.world02;
                case 2: return ref buffer.world03;
                case 3: return ref buffer.world04;
                case 4: return ref buffer.world05;
                case 5: return ref buffer.world06;
                case 6: return ref buffer.world07;
                case 7: return ref buffer.world08;
                case 8: return ref buffer.world09;
                case 9: return ref buffer.world10;
                case 10: return ref buffer.world11;
                case 11: return ref buffer.world12;
                case 12: return ref buffer.world13;
                case 13: return ref buffer.world14;
                case 14: return ref buffer.world15;
                case 15: return ref buffer.world16;
                default:
                    throw new IndexOutOfRangeException();
            }
        }

        public static void SetWorldAt(this ref RawHistoryBuffer buffer, int index, in CollisionWorld world)
        {
            switch (index)
            {
                case 0: buffer.world01 = world; break;
                case 1: buffer.world02 = world; break;
                case 2: buffer.world03 = world; break;
                case 3: buffer.world04 = world; break;
                case 4: buffer.world05 = world; break;
                case 5: buffer.world06 = world; break;
                case 6: buffer.world07 = world; break;
                case 7: buffer.world08 = world; break;
                case 8: buffer.world09 = world; break;
                case 9: buffer.world10 = world; break;
                case 10:buffer.world11 = world; break;
                case 11:buffer.world12 = world; break;
                case 12:buffer.world13 = world; break;
                case 13:buffer.world14 = world; break;
                case 14:buffer.world15 = world; break;
                case 15:buffer.world16 = world; break;
                default:
                    throw new IndexOutOfRangeException();
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CollisionHistoryBuffer
    {
        public const int Capacity = RawHistoryBuffer.Capacity;
        public int Size => m_size;
        private int m_size;
        internal NetworkTick m_lastStoredTick;
        internal NetworkTick m_nextStoreTick;

        private RawHistoryBuffer m_buffer;

        public CollisionHistoryBuffer(int size)
        {
            if (size > Capacity)
                throw new ArgumentOutOfRangeException($"Invalid size {size}. Must be <= {Capacity}");
            m_size = size;
            m_lastStoredTick = NetworkTick.Invalid;
            m_nextStoreTick = NetworkTick.Invalid;
            var defaultWorld = new CollisionWorld(0, 0);
            m_buffer = new RawHistoryBuffer();
            for(int i=0;i<Capacity;++i)
            {
                m_buffer.SetWorldAt(i, defaultWorld);
            }
        }

        public void GetCollisionWorldFromTick(NetworkTick tick, uint interpolationDelay, out CollisionWorld collWorld)
        {
            // Clamp to oldest physics copy when requesting older data than supported
            if (interpolationDelay > m_size-1)
                interpolationDelay = (uint)m_size-1;
            tick.Subtract(interpolationDelay);
            if (m_lastStoredTick.IsValid && tick.IsNewerThan(m_lastStoredTick))
                tick = m_lastStoredTick;
            var index = (int)(tick.TickIndexForValidTick % m_size);
            GetCollisionWorldFromIndex(index, out collWorld);
        }

        void GetCollisionWorldFromIndex(int index, out CollisionWorld collWorld)
        {
            collWorld = m_buffer.GetWorldAt(index);
        }

        public void CloneCollisionWorld(int index, in CollisionWorld collWorld)
        {
            if (index >= Capacity)
            {
                throw new IndexOutOfRangeException();
            }
            m_buffer.SetWorldAt(index, collWorld.Clone());
        }

        public bool HasCapacity(int index, in CollisionWorld collWorld)
        {
            if (index >= Capacity)
            {
                throw new IndexOutOfRangeException();
            }

            ref var world = ref m_buffer.GetWorldAt(index);
            return world.NumStaticBodiesCapacity >= collWorld.NumStaticBodiesCapacity &&
                   world.NumDynamicBodiesCapacity >= collWorld.NumDynamicBodiesCapacity &&
                   world.NumBodiesCapacity >= collWorld.NumBodiesCapacity;
        }

        public void SetCapacity(int index, in CollisionWorld collWorld)
        {
            if (index >= Capacity)
            {
                throw new IndexOutOfRangeException();
            }
            m_buffer.GetWorldAt(index).Reset(collWorld.NumStaticBodiesCapacity, collWorld.NumDynamicBodiesCapacity);
        }

        public void CopyCollisionWorld(int index, in CollisionWorld collWorld)
        {
            if (index >= Capacity)
            {
                throw new IndexOutOfRangeException();
            }
            m_buffer.GetWorldAt(index).CopyFrom(collWorld);
        }

        public void Dispose()
        {
            for (int i = 0; i < Capacity; ++i)
            {
                m_buffer.GetWorldAt(i).Dispose();
            }
        }
    }

    /// <summary>
    /// A system used to store old state of the physics world for lag compensation.
    /// This system creates a PhysicsWorldHistorySingleton and from that you can
    /// get a physics collision world for a previous tick.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PredictedFixedStepSimulationSystemGroup))]
    [BurstCompile]
    public partial struct PhysicsWorldHistory : ISystem, ISystemStartStop
    {
        private int _historySize;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LagCompensationConfig>();
            state.RequireForUpdate<NetworkId>();
            using var singletonComponents = new NativeArray<ComponentType>(1, Allocator.Temp)
            {
                [0] = ComponentType.ReadWrite<PhysicsWorldHistorySingleton>()
            };
            state.EntityManager.CreateEntity(state.EntityManager.CreateArchetype(singletonComponents));
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton(out PhysicsWorldHistorySingleton singleton) && singleton.m_History.IsCreated)
            {
                singleton.m_History[0].Dispose();
                singleton.m_History.Dispose();
            }
        }

        [BurstCompile]
        public void OnStartRunning(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<LagCompensationConfig>();
            if (state.WorldUnmanaged.IsServer())
                _historySize = config.ServerHistorySize!=0 ? config.ServerHistorySize : RawHistoryBuffer.Capacity;
            else
                _historySize = config.ClientHistorySize;
            if (_historySize == 0)
            {
                state.EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<LagCompensationConfig>());
                return;
            }

            if (_historySize < 0 || _historySize > RawHistoryBuffer.Capacity)
            {
                SystemAPI.GetSingleton<NetDebug>().LogWarning($"Invalid LagCompensationConfig, history size ({_historySize}) must be > 0 <= {RawHistoryBuffer.Capacity}. Clamping hte value to the valid range.");
                _historySize = math.clamp(_historySize, 1, RawHistoryBuffer.Capacity);
            }
        }

        [BurstCompile]
        public void OnStopRunning(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_historySize == 0)
                return;
            
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var serverTick = networkTime.ServerTick;
            if (!serverTick.IsValid || !networkTime.IsFirstTimeFullyPredictingTick)
                return;

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var physicsWorldHistory = SystemAPI.GetSingletonRW<PhysicsWorldHistorySingleton>();

            var physicsWorldHistoryIndicesToCopy = new NativeList<int>(state.WorldUpdateAllocator);
            
            // Can't reallocate in job, even if using Run() so call directly instead
            unsafe
            {
                // The current server tick will generate a new physics world since it is a new full tick
                // Copy all ticks before this one to the buffer using the most recent physics world - which will be what that simulation used
                var currentStoreTick = serverTick;
                currentStoreTick.Decrement();
                CollisionHistoryBuffer* collisionHistory;
                if (!physicsWorldHistory.ValueRO.m_History.IsCreated)
                {
                    physicsWorldHistory.ValueRW.m_History = new UnsafeList<CollisionHistoryBuffer>(1, Allocator.Persistent);
                    physicsWorldHistory.ValueRW.m_History.Add(new CollisionHistoryBuffer(_historySize));
                    collisionHistory = (CollisionHistoryBuffer*)physicsWorldHistory.ValueRW.m_History.Ptr;
                    for (int i = 0; i < CollisionHistoryBuffer.Capacity; i++)
                    {
                        collisionHistory->CloneCollisionWorld(i, in physicsWorld.CollisionWorld);
                        // SetCapacity might increase capacity compared to CloneCollisionWorld (because it sets nearest 2 power)
                        collisionHistory->SetCapacity(i, in physicsWorld.CollisionWorld);
                    }
                }
                else
                {
                    collisionHistory = (CollisionHistoryBuffer*)physicsWorldHistory.ValueRO.m_History.Ptr;
                    var nextStoreTick = collisionHistory->m_nextStoreTick;
                    if (nextStoreTick.IsNewerThan(currentStoreTick))
                        return;

                    // Store world for each tick that has not been stored yet (framerate might be lower than tickrate)
                    var startStoreTick = nextStoreTick;
                    // Copying more than m_CollisionHistory.Size would mean we overwrite a tick we copied this frame
                    var oldestTickWithUniqueIndex = currentStoreTick;
                    oldestTickWithUniqueIndex.Increment();
                    oldestTickWithUniqueIndex.Subtract((uint)collisionHistory->Size);
                    if (oldestTickWithUniqueIndex.IsNewerThan(startStoreTick))
                        startStoreTick = oldestTickWithUniqueIndex;
                    for (var storeTick = startStoreTick; !storeTick.IsNewerThan(currentStoreTick); storeTick.Increment())
                    {
                        var index = (int)(storeTick.TickIndexForValidTick % collisionHistory->Size);
                        physicsWorldHistoryIndicesToCopy.Add(index);
                        if (collisionHistory->HasCapacity(index, in physicsWorld.CollisionWorld))
                            continue;
                        
                        // Access with RW since we are now changing state
                        collisionHistory = (CollisionHistoryBuffer*)physicsWorldHistory.ValueRW.m_History.Ptr;
                        // Since we have a capacity change we need to call SetCapacity on main thread
                        // Need sync point to make sure nobody is using the old one when resizing
                        state.Dependency.Complete();
                        collisionHistory->SetCapacity(index, in physicsWorld.CollisionWorld);
                    }
                }
                
                collisionHistory->m_nextStoreTick = serverTick;
                collisionHistory->m_lastStoredTick = currentStoreTick;
                physicsWorldHistory.ValueRW.m_LastStoreTick = currentStoreTick;
            }

            var updatePhysicsWorldHistoryJob = new CopyPhysicsWorldHistoryJob
            {
                PhysicsWorldIndicesToCopy = physicsWorldHistoryIndicesToCopy,
                PhysicsWorld = physicsWorld,
                PhysicsWorldHistory = physicsWorldHistory.ValueRO,
            };
            
            state.Dependency = updatePhysicsWorldHistoryJob.Schedule(state.Dependency);
        }

        [BurstCompile]
        private unsafe struct CopyPhysicsWorldHistoryJob : IJob
        {
            [ReadOnly] public NativeList<int> PhysicsWorldIndicesToCopy;

            [ReadOnly] public PhysicsWorld PhysicsWorld;
            public PhysicsWorldHistorySingleton PhysicsWorldHistory;
            
            public void Execute()
            {
                var collisionHistory = (CollisionHistoryBuffer*)PhysicsWorldHistory.m_History.Ptr;
                for (int i = 0; i < PhysicsWorldIndicesToCopy.Length; i++)
                {
                    var index = PhysicsWorldIndicesToCopy[i];
                    collisionHistory->SetCapacity(index, in PhysicsWorld.CollisionWorld);
                    collisionHistory->CopyCollisionWorld(index, in PhysicsWorld.CollisionWorld);
                }
            }
        }
    }
}
