namespace Unity.Networking.Transport
{
    internal struct PacketMetadata
    {
        public int DataLength;
        public int DataOffset;
        public int DataCapacity;
        public ConnectionId Connection;

        public override bool Equals(object obj)
        {
            return this == (PacketMetadata)obj;
        }

        public override int GetHashCode()
        {
            var hash = 1;
            hash = 31 * hash + DataLength;
            hash = 31 * hash + DataOffset;
            hash = 31 * hash + DataCapacity;
            hash = 31 * hash + Connection.GetHashCode();
            return hash;
        }

        public override string ToString()
        {
            return string.Format("PacketMetadata(offset: {0}, length: {1}, capacity: {2})", DataOffset, DataLength, DataCapacity);
        }

        public static bool operator==(PacketMetadata lhs, PacketMetadata rhs)
        {
            return lhs.DataLength == rhs.DataLength &&
                lhs.DataOffset == rhs.DataOffset &&
                lhs.DataCapacity == rhs.DataCapacity &&
                lhs.Connection == rhs.Connection;
        }

        public static bool operator!=(PacketMetadata lhs, PacketMetadata rhs)
        {
            return lhs.DataLength != rhs.DataLength ||
                lhs.DataOffset != rhs.DataOffset ||
                lhs.DataCapacity != rhs.DataCapacity ||
                lhs.Connection != rhs.Connection;
        }
    }
}
