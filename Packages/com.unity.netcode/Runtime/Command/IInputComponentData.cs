using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
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
    [Obsolete("The IInputBufferData interface has been deprecated. It was meant for internal use and any reference to it is considered an error. " +
              "Please always use ICommandData instead.", false)]
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
     [Obsolete("CopyInputToCommandBuffer has been deprecated. There is no replacement, being the method meant to be used only by code-generated systems.", false)]
     public partial struct CopyInputToCommandBuffer<TInputBufferData, TInputComponentData>
         where TInputBufferData : unmanaged, IInputBufferData
         where TInputComponentData : unmanaged, IInputComponentData
     {
         /// <summary>
         /// For internal use only, simplify the creation of system jobs that copies <see cref="IInputComponentData"/> data to the underlying <see cref="ICommandData"/> buffer.
         /// </summary>
         [Obsolete("CopyInputToBufferJob has been deprecated.", false)]
         public struct CopyInputToBufferJob
         {
             /// <summary>
             /// Implements the component copy and input event management.
             /// Should be called your job <see cref="Unity.Jobs.IJob.Execute"/> method.
             /// </summary>
             /// <param name="chunk"></param>
             /// <param name="orderIndex"></param>
             public void Execute(ArchetypeChunk chunk, int orderIndex)
             {
             }
         }

         /// <summary>
         /// Initialize the CopyInputToCommandBuffer by updating all the component type handles and create a
         /// a new <see cref="CopyInputToBufferJob"/> instance.
         /// </summary>
         /// <param name="state"></param>
         /// <returns>a new <see cref="CopyInputToBufferJob"/> instance.</returns>
         public CopyInputToBufferJob InitJobData(ref SystemState state)
         {
             return default;
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
         public EntityQuery Create(ref SystemState state)
         {
             return default;
         }
     }

     /// <summary>
     /// For internal use only, helper struct that should be used to implements systems that copies
     /// commands from the <see cref="ICommandData"/> buffer to the <see cref="IInputComponentData"/> component
     /// present on the entity.
     /// </summary>
     /// <typeparam name="TInputBufferData"></typeparam>
     /// <typeparam name="TInputComponentData"></typeparam>
     [Obsolete("ApplyCurrentInputBufferElementToInputData has been deprecated. There is no replacement, being the method meant to be used only by code-generated systems.", false)]
     public partial struct ApplyCurrentInputBufferElementToInputData<TInputBufferData, TInputComponentData>
         where TInputBufferData : unmanaged, IInputBufferData
         where TInputComponentData : unmanaged, IInputComponentData
     {
         /// <summary>
         /// Helper struct that should be used to implement jobs that copies commands from an <see cref="ICommandData"/> buffer
         /// to the respective <see cref="IInputComponentData"/>.
         /// </summary>
         [Obsolete("ApplyInputDataFromBufferJob has been deprecated.", false)]
         public struct ApplyInputDataFromBufferJob
         {
             /// <summary>
             /// Copy the command for current server tick to the input component.
             /// Should be called your job <see cref="Unity.Jobs.IJob.Execute"/> method.
             /// </summary>
             /// <param name="chunk"></param>
             /// <param name="orderIndex"></param>
             public void Execute(ArchetypeChunk chunk, int orderIndex)
             {
             }
         }

         /// <summary>
         /// Update the component type handles and create a new <see cref="ApplyInputDataFromBufferJob"/>
         /// that can be passed to your job.
         /// </summary>
         /// <param name="state"></param>
         /// <returns>a new <see cref="ApplyInputDataFromBufferJob"/> instance.</returns>
         public ApplyInputDataFromBufferJob InitJobData(ref SystemState state)
         {
             return default;
         }
     }

     /// <summary>
    /// The underlying <see cref="ICommandData"/> buffer used to store the <see cref="IInputComponentData"/>.
    /// </summary>
    /// <remarks>
    /// The buffer replication behaviour cannot be overriden on per-prefab basis and it is by default sent also
    /// for child entities.
    /// </remarks>
    /// <typeparam name="T">An unmanaged struct implementing the <see cref="IInputComponentData"/> interface"/></typeparam>
    [DontSupportPrefabOverrides]
    [GhostComponent(SendDataForChildEntity = true)]
    [InternalBufferCapacity(0)]
    public struct InputBufferData<T> : ICommandData where T: unmanaged, IInputComponentData
    {
        /// <summary>
        /// The tick the command should be executed. It is mandatory to set the tick before adding the command to the
        /// buffer using <see cref="CommandDataUtility.AddCommandData{T}"/>.
        /// </summary>
        [DontSerializeForCommand]
        public NetworkTick Tick { get; set; }
        /// <summary>
        /// The <see cref="IInputComponentData"/> struct that hold the input data.
        /// </summary>
        public T InternalInput;
    }

    /// <summary>
    /// Internal use only, interface implemented by code-generated helpers to increment and decrement
    /// <see cref="IInputComponentData"/> events when copy to/from the underlying <see cref="InputBufferData{T}"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IInputEventHelper<T> where T: unmanaged, IInputComponentData
    {
        /// <summary>
        /// Take the stored input data we have and copy to the given input data pointed to. Decrement
        /// any event counters by the counter value in the previous command buffer data element.
        /// </summary>
        /// <param name="prevInputData">Command data from the previous tick</param>
        /// <param name="inputData">Our stored input data will be copied over to this location</param>
        public void DecrementEvents(ref T inputData, in T prevInputData);
        /// <summary>
        /// Save the input data with any event counters incremented by the counter from the last stored
        /// input in the command buffer for the current tick. See <see cref="InputEvent"/>.
        /// </summary>
        /// <param name="lastInputData">Pointer to the last command data in the buffer</param>
        /// <param name="inputData">Pointer to input data to be saved in this command data</param>
        public void IncrementEvents(ref T inputData,  in T lastInputData);
    }
}
