using System;

namespace Unity.Networking.Transport
{
    internal struct ConnectionId : IEquatable<ConnectionId>
    {
        public int Id;
        public int Version;

        public bool IsCreated => Version > 0;

        internal ConnectionId(int id, int version)
        {
            Id = id;
            Version = version;
        }

        public static bool operator==(ConnectionId lhs, ConnectionId rhs)
        {
            return lhs.Id == rhs.Id && lhs.Version == rhs.Version;
        }

        public static bool operator!=(ConnectionId lhs, ConnectionId rhs)
        {
            return lhs.Id != rhs.Id || lhs.Version != rhs.Version;
        }

        public override bool Equals(object o)
        {
            return this == (ConnectionId)o;
        }

        public bool Equals(ConnectionId o)
        {
            return this == o;
        }

        public override int GetHashCode()
        {
            return (Id << 8) ^ Version;
        }

        public override string ToString()
        {
            return $"ConnectionId[id{Id},v{Version}]";
        }
    }
}
