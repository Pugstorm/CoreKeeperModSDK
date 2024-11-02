#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using System;
using System.Runtime.InteropServices;

namespace Unity.Entities
{
    partial class EntitiesJournaling
    {
        /// <summary>
        /// Information about a record entry.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct Record : IEquatable<Record>
        {
            public readonly int Position;
            public readonly int Length;

            public Record(int position, int length)
            {
                Position = position;
                Length = length;
            }

            public bool Equals(Record other) => Position == other.Position && Length == other.Length;
        }
    }
}
#endif
