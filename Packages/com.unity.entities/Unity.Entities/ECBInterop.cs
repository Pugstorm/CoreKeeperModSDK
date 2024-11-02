using System;
using Unity.Burst;

namespace Unity.Entities
{
    [GenerateBurstMonoInterop("ECBInterop")]
    [BurstCompile]
    internal partial struct ECBInterop
    {
        [BurstMonoInteropMethod]
        [BurstDiscard]
        internal static unsafe void _RemoveManagedReferences(EntityDataAccess* mgr, int* sharedIndex, int count)
        {
            try
            {
                for (var i = 0; i < count; i++)
                {
                    var sharedComponentIndex = sharedIndex[i];
                    if (EntityComponentStore.IsUnmanagedSharedComponentIndex(sharedComponentIndex))
                    {
                        mgr->EntityComponentStore->RemoveSharedComponentReference_Unmanaged(sharedComponentIndex);
                    }
                    else
                    {
                        mgr->ManagedComponentStore.RemoveSharedComponentReference_Managed(sharedComponentIndex);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [BurstMonoInteropMethod]
        [BurstDiscard]
        internal static unsafe void _CleanupManaged(EntityCommandBufferChain* chain)
        {
            var cleanup_list = chain->m_Cleanup->CleanupList;
            while (cleanup_list != null)
            {
                cleanup_list->BoxedObject.Free();
                cleanup_list = cleanup_list->Prev;
            }

            chain->m_Cleanup->CleanupList = null;
        }

        [BurstMonoInteropMethod]
        [BurstDiscard]
        internal static unsafe void _ProcessManagedCommand(void* processor, int processorType, BasicCommand* header)
        {
            if (processorType == (int)EntityCommandBuffer.ECBProcessorType.PlaybackProcessor)
            {
                EntityCommandBuffer.ProcessManagedCommand((EntityCommandBuffer.PlaybackProcessor*) processor, header);
            }
            else if (processorType == (int)EntityCommandBuffer.ECBProcessorType.DebugViewProcessor)
            {
                EntityCommandBuffer.ProcessManagedCommand((EntityCommandBuffer.DebugViewProcessor*) processor, header);
            }
            else if (processorType == (int) EntityCommandBuffer.ECBProcessorType.PlaybackWithTraceProcessor)
            {
                EntityCommandBuffer.ProcessManagedCommand((EntityCommandBuffer.PlaybackWithTraceProcessor*) processor, header);
            }
            else if (processorType == (int) EntityCommandBuffer.ECBProcessorType.PrePlaybackValidationProcessor)
            {
                EntityCommandBuffer.ProcessManagedCommand((EntityCommandBuffer.PrePlaybackValidationProcessor*) processor, header);
            }
        }

        [BurstMonoInteropMethod]
        internal static unsafe void _ProcessChainChunk(void* walker, int processorType,
            ECBChainPlaybackState* chainStates, int currentChain, int nextChain)
        {
            if (processorType == (int)EntityCommandBuffer.ECBProcessorType.PlaybackProcessor)
            {
                var playbackWalker = (EntityCommandBuffer.EcbWalker<EntityCommandBuffer.PlaybackProcessor>*) walker;
                playbackWalker->ProcessChain(chainStates, currentChain, nextChain);
            }
            else if (processorType == (int)EntityCommandBuffer.ECBProcessorType.DebugViewProcessor)
            {
                var debugViewWalker = (EntityCommandBuffer.EcbWalker<EntityCommandBuffer.DebugViewProcessor>*) walker;
                debugViewWalker->ProcessChain(chainStates, currentChain, nextChain);
            }
            else if (processorType == (int) EntityCommandBuffer.ECBProcessorType.PlaybackWithTraceProcessor)
            {
                var playbackWithTraceWalker = (EntityCommandBuffer.EcbWalker<EntityCommandBuffer.PlaybackWithTraceProcessor>*) walker;
                playbackWithTraceWalker->ProcessChain(chainStates, currentChain, nextChain);
            }
            else if (processorType == (int) EntityCommandBuffer.ECBProcessorType.PrePlaybackValidationProcessor)
            {
                var prePlaybackValidationWalker = (EntityCommandBuffer.EcbWalker<EntityCommandBuffer.PrePlaybackValidationProcessor>*) walker;
                prePlaybackValidationWalker->ProcessChain(chainStates, currentChain, nextChain);
            }
        }


    }
}
