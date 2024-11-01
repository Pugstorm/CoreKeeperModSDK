#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using System.Runtime.InteropServices;

namespace Unity.Entities
{
    partial class EntitiesJournaling
    {
        /// <summary>
        /// Data header written in buffer.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        readonly struct Header
        {
            public readonly ulong Index;
            public readonly RecordType RecordType;
            public readonly int FrameIndex;
            public readonly ulong WorldSequenceNumber;
            public readonly SystemHandle ExecutingSystem;
            public readonly SystemHandle OriginSystem;
            public readonly int EntityCount;
            public readonly int TypeCount;
            public readonly int DataLength;

            public Header(ulong index, RecordType recordType, int frameIndex, ulong worldSeqNumber, in SystemHandle executingSystem, in SystemHandle originSystem, int entityCount, int typeCount, int dataLength)
            {
                Index = index;
                RecordType = recordType;
                FrameIndex = frameIndex;
                WorldSequenceNumber = worldSeqNumber;
                ExecutingSystem = executingSystem;
                OriginSystem = originSystem;
                EntityCount = entityCount;
                TypeCount = typeCount;
                DataLength = dataLength;
            }
        }
    }
}
#endif
