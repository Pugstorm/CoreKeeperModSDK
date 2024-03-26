using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.NetCode.LowLevel.Unsafe;

namespace Unity.NetCode
{
    /// <summary>
    /// A special component data interface used for storing player inputs.
    /// </summary>
    /// <remarks> When using the netcode package these inputs will be automatically handled
    /// like command data and will be stored in a buffer synchronized between client and
    /// server. This is compatible with netcode features like prediction.
    /// </remarks>
    public interface IInputComponentData : IComponentData
    {
    }

    /// <summary>
    /// This type can be used inside <see cref="IInputComponentData"/> to store input events.
    /// </summary>
    /// <remarks> When this type is used it's ensured that single input events like jumping or
    /// triggers will be properly detected exactly once by the server.
    /// </remarks>
    public struct InputEvent
    {
        /// <summary>
        /// Returns true when a new input event was detected (last known tick this was unset).
        /// </summary>
        public bool IsSet => Count > 0;

        /// <summary>
        /// Set or enable the input event for current tick.
        /// </summary>
        public void Set()
        {
            Count++;
        }

        /// <summary>
        /// Track if the event has been set for the current frame
        /// </summary>
        /// <remarks> This could be higher than 1 when the inputs are sampled multiple times
        /// before the input is sent to the server. Also if the input is sampled again before
        /// being transmitted the set event will not be overridden to the unset state (count=0).
        /// </remarks>
        public uint Count;
    }

    /// <summary>
    /// Interface used to handle automatic input command data setup with the IInputComponentData
    /// style inputs. This is used internally by code generation, don't use this directly.
    /// </summary>
    public interface IInputBufferData : ICommandData
    {
        /// <summary>
        /// Take the stored input data we have and copy to the given input data pointed to. Decrement
        /// any event counters by the counter value in the previous command buffer data element.
        /// </summary>
        /// <param name="prevInputBufferDataPtr">Command data from the previous tick</param>
        /// <param name="inputPtr">Our stored input data will be copied over to this location</param>
        public void DecrementEventsAndAssignToInput(IntPtr prevInputBufferDataPtr, IntPtr inputPtr);
        /// <summary>
        /// Save the input data with any event counters incremented by the counter from the last stored
        /// input in the command buffer for the current tick. See <see cref="InputEvent"/>.
        /// </summary>
        /// <param name="lastInputBufferDataPtr">Pointer to the last command data in the buffer</param>
        /// <param name="inputPtr">Pointer to input data to be saved in this command data</param>
        public void IncrementEventsAndSetCurrentInputData(IntPtr lastInputBufferDataPtr, IntPtr inputPtr);
    }

    /// <summary>
    /// For internal use only, helper struct that should be used to implement systems that copy the content of an
    /// <see cref="IInputComponentData"/> into the code-generated <see cref="ICommandData"/> buffer.
    /// </summary>
    /// <typeparam name="TInputBufferData"></typeparam>
    /// <typeparam name="TInputComponentData"></typeparam>
    [BurstCompile]
    public partial struct CopyInputToCommandBuffer<TInputBufferData, TInputComponentData>
        where TInputBufferData : unmanaged, IInputBufferData
        where TInputComponentData : unmanaged, IInputComponentData
    {
        private EntityQuery m_TimeQuery;
        private EntityQuery m_ConnectionQuery;
        [ReadOnly] private ComponentTypeHandle<GhostOwner> m_GhostOwnerDataType;
        [ReadOnly] private ComponentTypeHandle<TInputComponentData> m_InputDataType;
        private BufferTypeHandle<TInputBufferData> m_InputBufferTypeHandle;

        /// <summary>
        /// For internal use only, simplify the creation of system jobs that copies <see cref="IInputComponentData"/> data to the underlying <see cref="ICommandData"/> buffer.
        /// </summary>
        [BurstCompile]
        public struct CopyInputToBufferJob
        {
            internal NetworkTick Tick;
            internal int ConnectionId;
            [ReadOnly] internal ComponentTypeHandle<TInputComponentData> InputDataType;
            [ReadOnly] internal ComponentTypeHandle<GhostOwner> GhostOwnerDataType;
            internal BufferTypeHandle<TInputBufferData> InputBufferDataType;

            /// <summary>
            /// Implements the component copy and input event management.
            /// Should be called your job <see cref="Unity.Jobs.IJob.Execute"/> method.
            /// </summary>
            /// <param name="chunk"></param>
            /// <param name="orderIndex"></param>
            [BurstCompile]
            public void Execute(ArchetypeChunk chunk, int orderIndex)
            {
                var inputs = chunk.GetNativeArray(ref InputDataType);
                var owners = chunk.GetNativeArray(ref GhostOwnerDataType);
                var inputBuffers = chunk.GetBufferAccessor(ref InputBufferDataType);

                for (int i = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; ++i)
                {
                    var inputData = inputs[i];
                    var owner = owners[i];
                    var inputBuffer = inputBuffers[i];

                    // Validate owner ID in case all entities are being predicted, only inputs from local player should be collected
                    if (owner.NetworkId != ConnectionId)
                        continue;

                    var input = default(TInputBufferData);
                    input.Tick = Tick;
                    var inputDataPtr = GhostComponentSerializer.IntPtrCast(ref inputData);

                    // Increment event count for current tick. There could be an event and then no event but on the same
                    // predicted/simulated tick, this will still be registered as an event (count > 0) instead of the later
                    // event overriding the event to 0/false.
                    inputBuffer.GetDataAtTick(Tick, out var inputDataElement);
                    var inputDataElementPtr = GhostComponentSerializer.IntPtrCast(ref inputDataElement);
                    input.IncrementEventsAndSetCurrentInputData(inputDataElementPtr, inputDataPtr);

                    inputBuffer.AddCommandData(input);
                }
            }
        }

        /// <summary>
        /// Initialize the CopyInputToCommandBuffer by updating all the component type handles and create a
        /// a new <see cref="CopyInputToBufferJob"/> instance.
        /// </summary>
        /// <param name="state"></param>
        /// <returns>a new <see cref="CopyInputToBufferJob"/> instance.</returns>
        [BurstCompile]
        public CopyInputToBufferJob InitJobData(ref SystemState state)
        {
            m_GhostOwnerDataType.Update(ref state);
            m_InputBufferTypeHandle.Update(ref state);
            m_InputDataType.Update(ref state);

            var jobData = new CopyInputToBufferJob
            {
                Tick =  m_TimeQuery.GetSingleton<NetworkTime>().ServerTick,
                ConnectionId = m_ConnectionQuery.GetSingleton<NetworkId>().Value,
                GhostOwnerDataType = m_GhostOwnerDataType,
                InputBufferDataType = m_InputBufferTypeHandle,
                InputDataType = m_InputDataType,
            };
            return jobData;
        }

        /// <summary>
        /// Creates the internal component type handles, register to system state the component queries.
        /// Very important, add an implicity constraint for running the parent system only when the client
        /// is connected to the server, by requiring at least one connection with a <see cref="NetworkId"/> components.
        /// <remarks>
        /// Should be called inside your the system OnCreate method.
        /// </remarks>
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        [BurstCompile]
        public EntityQuery Create(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TInputBufferData, TInputComponentData, GhostOwner>();
            var query = state.GetEntityQuery(builder);
            m_TimeQuery = state.GetEntityQuery(ComponentType.ReadOnly<NetworkTime>());
            m_ConnectionQuery = state.GetEntityQuery(ComponentType.ReadOnly<NetworkId>());
            m_GhostOwnerDataType = state.GetComponentTypeHandle<GhostOwner>(true);
            m_InputBufferTypeHandle = state.GetBufferTypeHandle<TInputBufferData>();
            m_InputDataType = state.GetComponentTypeHandle<TInputComponentData>(true);
            state.RequireForUpdate<NetworkId>();
            return query;
        }
    }

    /// <summary>
    /// For internal use only, helper struct that should be used to implements systems that copies
    /// commands from the <see cref="ICommandData"/> buffer to the <see cref="IInputComponentData"/> component
    /// present on the entity.
    /// </summary>
    /// <typeparam name="TInputBufferData"></typeparam>
    /// <typeparam name="TInputComponentData"></typeparam>
    [BurstCompile]
    public partial struct ApplyCurrentInputBufferElementToInputData<TInputBufferData, TInputComponentData>
        where TInputBufferData : unmanaged, IInputBufferData
        where TInputComponentData : unmanaged, IInputComponentData
    {
        private EntityQuery m_TimeQuery;
        [ReadOnly] private EntityTypeHandle m_EntityTypeHandle;
        private ComponentTypeHandle<TInputComponentData> m_InputDataType;
        [ReadOnly] private BufferTypeHandle<TInputBufferData> m_InputBufferTypeHandle;

        /// <summary>
        /// Helper struct that should be used to implement jobs that copies commands from an <see cref="ICommandData"/> buffer
        /// to the respective <see cref="IInputComponentData"/>.
        /// </summary>
        [BurstCompile]
        public struct ApplyInputDataFromBufferJob
        {
            internal NetworkTick Tick;
            internal int StepLength;
            internal ComponentTypeHandle<TInputComponentData> InputDataType;
            internal BufferTypeHandle<TInputBufferData> InputBufferTypeHandle;

            /// <summary>
            /// Copy the command for current server tick to the input component.
            /// Should be called your job <see cref="Unity.Jobs.IJob.Execute"/> method.
            /// </summary>
            /// <param name="chunk"></param>
            /// <param name="orderIndex"></param>
            [BurstCompile]
            public void Execute(ArchetypeChunk chunk, int orderIndex)
            {
                var inputs = chunk.GetNativeArray(ref InputDataType);
                var inputBuffers = chunk.GetBufferAccessor(ref InputBufferTypeHandle);

                for (int i = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; ++i)
                {
                    var inputData = inputs[i];
                    var inputBuffer = inputBuffers[i];

                    // Sample tick and tick-StepLength, if tick is not in the buffer it will return the latest input
                    // closest to it, and the same input for tick-StepLength, which is the right result as it should
                    // assume the same tick is repeating
                    inputBuffer.GetDataAtTick(Tick, out var inputDataElement);
                    var prevSampledTick = Tick;
                    prevSampledTick.Subtract((uint)StepLength);
                    inputBuffer.GetDataAtTick(prevSampledTick, out var prevInputDataElement);

                    var prevInputDataElementPtr = GhostComponentSerializer.IntPtrCast(ref prevInputDataElement);
                    var inputDataPtr = GhostComponentSerializer.IntPtrCast(ref inputData);
                    inputDataElement.DecrementEventsAndAssignToInput(prevInputDataElementPtr, inputDataPtr);
                    inputs[i] = inputData;
                }
            }
        }

        /// <summary>
        /// Update the component type handles and create a new <see cref="ApplyInputDataFromBufferJob"/>
        /// that can be passed to your job.
        /// </summary>
        /// <param name="state"></param>
        /// <returns>a new <see cref="ApplyInputDataFromBufferJob"/> instance.</returns>
        [BurstCompile]
        public ApplyInputDataFromBufferJob InitJobData(ref SystemState state)
        {
            m_EntityTypeHandle.Update(ref state);
            m_InputBufferTypeHandle.Update(ref state);
            m_InputDataType.Update(ref state);

            var networkTime = m_TimeQuery.GetSingleton<NetworkTime>();
            var jobData = new ApplyInputDataFromBufferJob
            {
                Tick = networkTime.ServerTick,
                StepLength = networkTime.SimulationStepBatchSize,
                InputBufferTypeHandle = m_InputBufferTypeHandle,
                InputDataType = m_InputDataType
            };
            return jobData;
        }

        /// <summary>
        /// Creates all the internal queries and setup the internal component type handles.
        /// Very important, add an implicity constraint for running the parent system only when the client
        /// is connected to the server, by requiring at least one connection with a <see cref="NetworkId"/> components.
        /// <remarks>
        /// Should be called inside your the system OnCreate method.
        /// </remarks>
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        [BurstCompile]
        public EntityQuery Create(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TInputBufferData, TInputComponentData, PredictedGhost>();
            var query = state.GetEntityQuery(builder);
            m_TimeQuery = state.GetEntityQuery(ComponentType.ReadOnly<NetworkTime>());
            m_EntityTypeHandle = state.GetEntityTypeHandle();
            m_InputBufferTypeHandle = state.GetBufferTypeHandle<TInputBufferData>();
            m_InputDataType = state.GetComponentTypeHandle<TInputComponentData>();
            state.RequireForUpdate<NetworkId>();
            return query;
        }
    }
}
