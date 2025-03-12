using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Networking.Transport
{
    [StructLayout(LayoutKind.Explicit)]
    internal unsafe struct ConnectionToken : IEquatable<ConnectionToken>, IComparable<ConnectionToken>
    {
        public const int k_Length = 8;
        [FieldOffset(0)] public fixed byte Value[k_Length];

        public static bool operator==(ConnectionToken lhs, ConnectionToken rhs)
        {
            return lhs.Compare(rhs) == 0;
        }

        public static bool operator!=(ConnectionToken lhs, ConnectionToken rhs)
        {
            return lhs.Compare(rhs) != 0;
        }

        public bool Equals(ConnectionToken other)
        {
            return Compare(other) == 0;
        }

        public int CompareTo(ConnectionToken other)
        {
            return Compare(other);
        }

        public override bool Equals(object other)
        {
            return other != null && this == (ConnectionToken)other;
        }

        public override int GetHashCode()
        {
            fixed(byte* p = Value)
            unchecked
            {
                var result = 0;

                for (int i = 0; i < k_Length; i++)
                {
                    result = (result * 31) ^ (int)p[i];
                }

                return result;
            }
        }

        public override string ToString()
        {
            fixed(byte* p = Value)
            return $"0x{*(long*)p:x16}";
        }

        int Compare(ConnectionToken other)
        {
            fixed(void* p = Value)
            {
                return UnsafeUtility.MemCmp(p, other.Value, k_Length);
            }
        }
    }
}
