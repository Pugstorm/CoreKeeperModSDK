using System;

namespace Unity.Entities
{
    internal struct EntityBatchInChunk
    {
        public ChunkIndex Chunk;
        public int StartIndex;
        public int Count;
    }
}
