using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Mathematics;

[assembly: InternalsVisibleTo("Unity.NetCode.Physics.EditorTests")]
namespace Unity.NetCode
{

    /// <summary>
    /// A singleton component from which you can get a physics collision world for a previous tick.
    /// </summary>
    public partial struct PhysicsWorldHistorySingleton : IComponentData
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
        public void GetCollisionWorldFromTick(NetworkTick tick, uint interpolationDelay, ref PhysicsWorld physicsWorld, out CollisionWorld collWorld)
        {
            var delayedTick = tick;
            delayedTick.Subtract(interpolationDelay);
            if (!m_LastStoreTick.IsValid || delayedTick.IsNewerThan(m_LastStoreTick))
            {
                collWorld = physicsWorld.CollisionWorld;
                return;
            }
            m_History.GetCollisionWorldFromTick(tick, interpolationDelay, out collWorld);
        }

        internal NetworkTick m_LastStoreTick;
        internal CollisionHistoryBufferRef m_History;
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
    internal struct CollisionHistoryBuffer : IDisposable
    {
        public const int Capacity = RawHistoryBuffer.Capacity;
        public int Size => m_size;
        public unsafe bool IsCreated => m_bufferCopyPtr != null;
        private int m_size;
        internal NetworkTick m_lastStoredTick;

        private RawHistoryBuffer m_buffer;
        [NativeDisableUnsafePtrRestriction]
        private unsafe void* m_bufferCopyPtr;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        //For job checks
        private AtomicSafetyHandle m_Safety;
        //To avoid accessing the buffer if already disposed
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<CollisionHistoryBuffer>();
#endif

        public CollisionHistoryBuffer(int size)
        {
            if (size > Capacity)
                throw new ArgumentOutOfRangeException($"Invalid size {size}. Must be <= {Capacity}");
            m_size = size;
            m_lastStoredTick = NetworkTick.Invalid;
            var defaultWorld = default(CollisionWorld);
            m_buffer = new RawHistoryBuffer();
            for(int i=0;i<Capacity;++i)
            {
                m_buffer.SetWorldAt(i, defaultWorld);
            }

            unsafe
            {
                m_bufferCopyPtr = UnsafeUtility.Malloc(UnsafeUtility.SizeOf<RawHistoryBuffer>(), 8, Allocator.Persistent);
            }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = AtomicSafetyHandle.Create();
            CollectionHelper.SetStaticSafetyId<CollisionHistoryBuffer>(ref m_Safety, ref s_staticSafetyId.Data);
#endif
        }

        public void GetCollisionWorldFromTick(NetworkTick tick, uint interpolationDelay, out CollisionWorld collWorld)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            // Clamp to oldest physics copy when requesting older data than supported
            if (interpolationDelay > m_size-1)
                interpolationDelay = (uint)m_size-1;
            tick.Subtract(interpolationDelay);
            if (m_lastStoredTick.IsValid && tick.IsNewerThan(m_lastStoredTick))
                tick = m_lastStoredTick;
            var index = (int)(tick.TickIndexForValidTick % m_size);
            GetCollisionWorldFromIndex(index, out collWorld);
        }

        public void DisposeIndex(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_buffer.GetWorldAt(index).Dispose();
        }

        void GetCollisionWorldFromIndex(int index, out CollisionWorld collWorld)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            collWorld = m_buffer.GetWorldAt(index);
        }

        public void CloneCollisionWorld(int index, in CollisionWorld collWorld)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            if (index >= Capacity)
            {
                throw new IndexOutOfRangeException();
            }
            //Always dispose the current world
            m_buffer.GetWorldAt(index).Dispose();
            m_buffer.SetWorldAt(index, collWorld.Clone());
        }

        public unsafe CollisionHistoryBufferRef AsCollisionHistoryBufferRef()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            //First check the CheckExistAndThrow to avoid bad access and return better error
            AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
            //Then validate the write access right
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            UnsafeUtility.AsRef<RawHistoryBuffer>(m_bufferCopyPtr) = m_buffer;
            var bufferRef = new CollisionHistoryBufferRef
            {
                m_ptr = m_bufferCopyPtr,
                m_lastStoredTick = m_lastStoredTick,
                m_size = m_size,
            };
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            bufferRef.m_Safety = m_Safety;
            AtomicSafetyHandle.UseSecondaryVersion(ref bufferRef.m_Safety);
#endif
            return bufferRef;
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckDeallocateAndThrow(m_Safety);
            AtomicSafetyHandle.Release(m_Safety);
#endif
            unsafe
            {
                if (m_bufferCopyPtr != null)
                {
                    UnsafeUtility.Free(m_bufferCopyPtr, Allocator.Persistent);
                    m_bufferCopyPtr = null;
                }
                for (int i = 0; i < Capacity; ++i)
                {
                    m_buffer.GetWorldAt(i).Dispose();
                }
            }
        }
    }

    /// <summary>
    /// A safe reference to the <see cref="CollisionHistoryBuffer"/>.
    /// Avoid copying the large world history data structure when accessing the buffer, and because of that
    /// can easily passed around in function, jobs or used on the main thread without consuming to much stack space.
    /// </summary>
    internal struct CollisionHistoryBufferRef
    {
        [NativeDisableUnsafePtrRestriction]
        unsafe internal void *m_ptr;
        internal NetworkTick m_lastStoredTick;
        internal int m_size;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
#endif
        /// <summary>
        /// Get the <see cref="CollisionWorld"/> state for the given tick and interpolation delay.
        /// </summary>
        /// <param name="tick">The server tick we are simulating</param>
        /// <param name="interpolationDelay">The client interpolation delay, measured in ticks. This is used to look back in time
        /// and retrieve the state of the collision world at tick - interpolationDelay.
        /// The interpolation delay is internally clamped to the current collision history size (the number of saved history state)</param>
        /// <param name="collWorld">The <see cref="CollisionWorld"/> state retrieved from the history</param>
        public void GetCollisionWorldFromTick(NetworkTick tick, uint interpolationDelay, out CollisionWorld collWorld)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            //The error will be misleading (is going to mention a NativeArray) but at least is more consistent
            //Rely only on CheckReadAndThrow give bad error messages
            AtomicSafetyHandle.CheckExistsAndThrow(this.m_Safety);
            AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);
#endif
            // Clamp to oldest physics copy when requesting older data than supported
            if (interpolationDelay > m_size-1)
                interpolationDelay = (uint)m_size-1;
            tick.Subtract(interpolationDelay);
            if (m_lastStoredTick.IsValid && tick.IsNewerThan(m_lastStoredTick))
                tick = m_lastStoredTick;
            var index = (int)(tick.TickIndexForValidTick % m_size);

            unsafe
            {
                collWorld = UnsafeUtility.AsRef<RawHistoryBuffer>(m_ptr).GetWorldAt(index);
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
    public partial struct PhysicsWorldHistory : ISystem
    {
        private NetworkTick m_nextStoreTick;

        CollisionHistoryBuffer m_CollisionHistory;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LagCompensationConfig>();
            state.RequireForUpdate<NetworkId>();
            state.EntityManager.CreateEntity(ComponentType.ReadWrite<PhysicsWorldHistorySingleton>());
            SystemAPI.SetSingleton(default(PhysicsWorldHistorySingleton));
        }
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (m_CollisionHistory.IsCreated)
                m_CollisionHistory.Dispose();
            m_nextStoreTick = NetworkTick.Invalid;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var serverTick = networkTime.ServerTick;
            if (!serverTick.IsValid || !networkTime.IsFirstTimeFullyPredictingTick)
                return;

            if (!m_CollisionHistory.IsCreated)
            {
                var config = SystemAPI.GetSingleton<LagCompensationConfig>();
                int historySize;
                if (state.WorldUnmanaged.IsServer())
                    historySize = config.ServerHistorySize!=0 ? config.ServerHistorySize : RawHistoryBuffer.Capacity;
                else
                    historySize = config.ClientHistorySize;
                if (historySize == 0)
                    return;
                if (historySize < 0 || historySize > RawHistoryBuffer.Capacity)
                {
                    SystemAPI.GetSingleton<NetDebug>().LogWarning($"Invalid LagCompensationConfig, history size ({historySize}) must be > 0 <= {RawHistoryBuffer.Capacity}. Clamping hte value to the valid range.");
                    historySize = math.clamp(historySize, 1, RawHistoryBuffer.Capacity);
                }

                m_CollisionHistory = new CollisionHistoryBuffer(historySize);
            }


            state.CompleteDependency();

            //We need to grab the physics world from a different source based on the physics configuration present or not
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            // The current server tick will generate a new physics world since it is a new full tick
            // Copy all ticks before this one to the buffer using the most recent physics world - which will be what that simulation used
            var lastStoreTick = serverTick;
            lastStoreTick.Decrement();
            if (!m_nextStoreTick.IsValid)
            {
                for (int i = 0; i < CollisionHistoryBuffer.Capacity; i++)
                {
                    m_CollisionHistory.CloneCollisionWorld(i, in physicsWorld.CollisionWorld);
                }
            }
            else
            {
                if (!lastStoreTick.IsNewerThan(m_nextStoreTick))
                    return;

                // Store world for each tick that has not been stored yet (framerate might be lower than tickrate)
                var startStoreTick = m_nextStoreTick;
                // Copying more than m_CollisionHistory.Size would mean we overwrite a tick we copied this frame
                var oldestTickWithUniqueIndex = lastStoreTick;
                oldestTickWithUniqueIndex.Increment();
                oldestTickWithUniqueIndex.Subtract((uint)m_CollisionHistory.Size);
                if (oldestTickWithUniqueIndex.IsNewerThan(startStoreTick))
                    startStoreTick = oldestTickWithUniqueIndex;
                for (var storeTick = startStoreTick; !storeTick.IsNewerThan(lastStoreTick); storeTick.Increment())
                {
                    var index = (int)(storeTick.TickIndexForValidTick % m_CollisionHistory.Size);
                    m_CollisionHistory.CloneCollisionWorld(index, in physicsWorld.CollisionWorld);
                }
            }
            m_CollisionHistory.m_lastStoredTick = lastStoreTick;
            SystemAPI.SetSingleton(new PhysicsWorldHistorySingleton{m_LastStoreTick = lastStoreTick, m_History = m_CollisionHistory.AsCollisionHistoryBufferRef()});
            m_nextStoreTick = serverTick;
        }
    }
}
