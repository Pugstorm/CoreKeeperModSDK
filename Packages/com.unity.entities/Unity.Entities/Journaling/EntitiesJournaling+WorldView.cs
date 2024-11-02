#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    partial class EntitiesJournaling
    {
        /// <summary>
        /// World view into journal buffer.
        /// </summary>
        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
        [DebuggerDisplay("{Name}")]
        [StructLayout(LayoutKind.Sequential)]
        public unsafe readonly struct WorldView : IEquatable<WorldView>
        {
            [NativeDisableUnsafePtrRestriction] readonly ulong* m_SequenceNumberPtr;

            /// <summary>
            /// The world sequence number.
            /// </summary>
            public ulong SequenceNumber => *m_SequenceNumberPtr;

            /// <summary>
            /// The world name.
            /// </summary>
            [ExcludeFromBurstCompatTesting("Returns managed object")]
            public string Name => GetWorldName(SequenceNumber);

            /// <summary>
            /// A reference to the world that matches the sequence number, if it still exists.
            /// </summary>
            [ExcludeFromBurstCompatTesting("Returns managed object")]
            public World Reference => GetWorld(SequenceNumber);

            internal WorldView(ulong* sequenceNumberPtr)
            {
                m_SequenceNumberPtr = sequenceNumberPtr;
            }

            public bool Equals(WorldView other) => SequenceNumber == other.SequenceNumber;
            [ExcludeFromBurstCompatTesting("Returns managed object")] public override bool Equals(object obj) => obj is WorldView type ? Equals(type) : false;
            public override int GetHashCode() => SequenceNumber.GetHashCode();
            public static bool operator ==(WorldView lhs, WorldView rhs) => lhs.SequenceNumber == rhs.SequenceNumber;
            public static bool operator !=(WorldView lhs, WorldView rhs) => !(lhs == rhs);
            [ExcludeFromBurstCompatTesting("Returns managed object")] public override string ToString() => $"{Name}:{SequenceNumber}";
        }
    }
}
#endif
