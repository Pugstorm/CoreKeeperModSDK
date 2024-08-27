using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.UIElements;

namespace Unity.NetCode
{
    /// <summary>
    /// Internal job (don't use directly) used to copy the input data for struct implementing the
    /// <see cref="IInputComponentData"/> to the underlying <see cref="InputBufferData{T}"/> command data
    /// buffer. The job is also responsible to increment the <see cref="InputEvent"/> counters, in case the input
    /// component contains input events.
    /// </summary>
    /// <typeparam name="TInputComponentData"></typeparam>
    /// <typeparam name="TInputHelper"></typeparam>
    [BurstCompile]
    public struct CopyInputToBufferJob<TInputComponentData, TInputHelper> : IJobChunk
        where TInputComponentData : unmanaged, IInputComponentData
        where TInputHelper : unmanaged, IInputEventHelper<TInputComponentData>
    {
        internal NetworkTick Tick;
        internal int ConnectionId;
        [ReadOnly] internal ComponentTypeHandle<TInputComponentData> InputDataType;
        [ReadOnly] internal ComponentTypeHandle<GhostOwner> GhostOwnerDataType;
        internal BufferTypeHandle<InputBufferData<TInputComponentData>> InputBufferDataType;

        /// <summary>
        /// Copy the input component for current server tick to the command buffer.
        /// </summary>
        /// <param name="chunk"></param>
        /// <param name="unfilteredChunkIndex"></param>
        /// <param name="useEnabledMask"></param>
        /// <param name="chunkEnabledMask"></param>
        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var inputs = chunk.GetNativeArray(ref InputDataType);
            var owners = chunk.GetNativeArray(ref GhostOwnerDataType);
            var inputBuffers = chunk.GetBufferAccessor(ref InputBufferDataType);
            var helper = new TInputHelper();
            for (int i = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; ++i)
            {
                var inputData = inputs[i];
                var owner = owners[i];
                var inputBuffer = inputBuffers[i];

                // Validate owner ID in case all entities are being predicted, only inputs from local player should be collected
                if (owner.NetworkId != ConnectionId)
                    continue;
                //Why this work: because when the tick transition to new one, the GetDataAtTick will return the value for
                //the previous tick. Thus this method is always guaranteed to increment (using the current counter for
                //for the event) in respect to the previous tick, that is the requirement for having an always incrementing
                //counter.
                inputBuffer.GetDataAtTick(Tick, out var inputDataElement);
                // Increment event count for current tick. There could be an event and then no event but on the same
                // predicted/simulated tick, this will still be registered as an event (count > 0) instead of the later
                // event overriding the event to 0/false.
                var input = default(InputBufferData<TInputComponentData>);
                input.Tick = Tick;
                input.InternalInput = inputData;
                helper.IncrementEvents(ref input.InternalInput, inputDataElement.InternalInput);

                inputBuffer.AddCommandData(input);
            }
        }
    }

    /// <summary>
    /// For internal use only, system that that copy the content of an <see cref="IInputComponentData"/> into
    /// <see cref="InputBufferData{T}"/> buffer present on the entity.
    /// </summary>
    /// <typeparam name="TInputComponentData"></typeparam>
    /// <typeparam name="TInputHelper"></typeparam>
    [BurstCompile]
    [UpdateInGroup(typeof(CopyInputToCommandBufferSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct CopyInputToCommandBufferSystem<TInputComponentData, TInputHelper> : ISystem
        where TInputComponentData : unmanaged, IInputComponentData
        where TInputHelper : unmanaged, IInputEventHelper<TInputComponentData>
    {
        private EntityQuery m_EntityQuery;
        private EntityQuery m_TimeQuery;
        private EntityQuery m_ConnectionQuery;
        private ComponentTypeHandle<GhostOwner> m_GhostOwnerDataType;
        private ComponentTypeHandle<TInputComponentData> m_InputDataType;
        private BufferTypeHandle<InputBufferData<TInputComponentData>> m_InputBufferTypeHandle;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<InputBufferData<TInputComponentData>, TInputComponentData, GhostOwner>();
            m_EntityQuery = state.GetEntityQuery(builder);
            m_TimeQuery = state.GetEntityQuery(ComponentType.ReadOnly<NetworkTime>());
            m_ConnectionQuery = state.GetEntityQuery(ComponentType.ReadOnly<NetworkId>());
            m_GhostOwnerDataType = state.GetComponentTypeHandle<GhostOwner>(true);
            m_InputBufferTypeHandle = state.GetBufferTypeHandle<InputBufferData<TInputComponentData>>();
            m_InputDataType = state.GetComponentTypeHandle<TInputComponentData>(true);
            state.RequireForUpdate<NetworkId>();
            state.RequireForUpdate(m_EntityQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_GhostOwnerDataType.Update(ref state);
            m_InputBufferTypeHandle.Update(ref state);
            m_InputDataType.Update(ref state);

            var job = new CopyInputToBufferJob<TInputComponentData, TInputHelper>
            {
                Tick =  m_TimeQuery.GetSingleton<NetworkTime>().ServerTick,
                ConnectionId = m_ConnectionQuery.GetSingleton<NetworkId>().Value,
                GhostOwnerDataType = m_GhostOwnerDataType,
                InputBufferDataType = m_InputBufferTypeHandle,
                InputDataType = m_InputDataType,
            };
            state.Dependency = job.Schedule(m_EntityQuery, state.Dependency);
        }
    }

    /// <summary>
    /// For internal use only, system that copies commands from the <see cref="InputBufferData{T}"/> buffer
    /// to the <see cref="IInputComponentData"/> component present on the entity.
    /// </summary>
    /// <typeparam name="TInputComponentData"></typeparam>
    /// <typeparam name="TInputHelper"></typeparam>
    // This needs to run early to ensure the input data has been applied from buffer to input data
    // struct before the input processing system runs
    [BurstCompile]
    [UpdateInGroup(typeof(CopyCommandBufferToInputSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PredictedFixedStepSimulationSystemGroup))]
    public partial struct ApplyCurrentInputBufferElementToInputDataSystem<TInputComponentData, TInputHelper> : ISystem
        where TInputComponentData : unmanaged, IInputComponentData
        where TInputHelper : unmanaged, IInputEventHelper<TInputComponentData>
    {
        private EntityQuery m_EntityQuery;
        private EntityQuery m_TimeQuery;
        private EntityTypeHandle m_EntityTypeHandle;
        private ComponentTypeHandle<TInputComponentData> m_InputDataType;
        private BufferTypeHandle<InputBufferData<TInputComponentData>> m_InputBufferTypeHandle;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<InputBufferData<TInputComponentData>, TInputComponentData, PredictedGhost>();
            m_EntityQuery = state.GetEntityQuery(builder);
            m_TimeQuery = state.GetEntityQuery(ComponentType.ReadOnly<NetworkTime>());
            m_EntityTypeHandle = state.GetEntityTypeHandle();
            m_InputBufferTypeHandle = state.GetBufferTypeHandle<InputBufferData<TInputComponentData>>();
            m_InputDataType = state.GetComponentTypeHandle<TInputComponentData>();
            state.RequireForUpdate<NetworkId>();
            state.RequireForUpdate(m_EntityQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_EntityTypeHandle.Update(ref state);
            m_InputBufferTypeHandle.Update(ref state);
            m_InputDataType.Update(ref state);

            var networkTime = m_TimeQuery.GetSingleton<NetworkTime>();
            var jobData = new ApplyInputDataFromBufferJob<TInputComponentData, TInputHelper>
            {
                Tick = networkTime.ServerTick,
                StepLength = networkTime.SimulationStepBatchSize,
                InputBufferTypeHandle = m_InputBufferTypeHandle,
                InputDataType = m_InputDataType
            };
            state.Dependency = jobData.Schedule(m_EntityQuery, state.Dependency);
        }
    }

    /// <summary>
    /// Internal job (don't use directly), run inside the prediction loop and copy the
    /// input data from an <see cref="InputBufferData{T}"/> command buffer to an <see cref="IInputComponentData"/>
    /// component for the current simulated tick.
    /// The job is responsible to recalculate any <see cref="InputEvent"/> count, such that any events occurred
    /// since last tick (or batch, see also <see cref="NetworkTime.SimulationStepBatchSize"/>) are correctly reported as
    /// set (see <see cref="InputEvent.IsSet"/>
    /// </summary>
    /// <typeparam name="TInputComponentData"></typeparam>
    /// <typeparam name="TInputHelper"></typeparam>
    [BurstCompile]
    public struct ApplyInputDataFromBufferJob<TInputComponentData, TInputHelper> : IJobChunk
        where TInputComponentData : unmanaged, IInputComponentData
        where TInputHelper : unmanaged, IInputEventHelper<TInputComponentData>
    {
        internal NetworkTick Tick;
        internal int StepLength;
        internal ComponentTypeHandle<TInputComponentData> InputDataType;
        internal BufferTypeHandle<InputBufferData<TInputComponentData>> InputBufferTypeHandle;

        /// <summary>
        /// Copy the command for current server tick to the input component.
        /// </summary>
        /// <param name="chunk"></param>
        /// <param name="unfilteredChunkIndex"></param>
        /// <param name="useEnabledMask"></param>
        /// <param name="chunkEnabledMask"></param>
        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var inputs = chunk.GetNativeArray(ref InputDataType);
            var inputBuffers = chunk.GetBufferAccessor(ref InputBufferTypeHandle);
            var helper = default(TInputHelper);
            for (int i = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; ++i)
            {
                var inputBuffer = inputBuffers[i];
                inputBuffer.GetDataAtTick(Tick, out var inputDataElement);
                // Sample tick and tick-StepLength, if tick is not in the buffer it will return the latest input
                // closest to it, and the same input for tick-StepLength, which is the right result as it should
                // assume the same tick is repeating
                var prevSampledTick = Tick;
                prevSampledTick.Subtract((uint)StepLength);
                inputBuffer.GetDataAtTick(prevSampledTick, out var prevInputDataElement);
                //reset the input data to match the current input and decrement the event counts
                var inputData = inputDataElement.InternalInput;
                helper.DecrementEvents(ref inputData, prevInputDataElement.InternalInput);
                inputs[i] = inputData;
            }
        }
    }
}
