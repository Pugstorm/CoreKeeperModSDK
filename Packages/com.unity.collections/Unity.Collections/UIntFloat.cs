using System.Runtime.InteropServices;

namespace Unity.Collections
{
    /// <summary>
    /// Declares a union object where all members start at the same location in memory.
    /// Allows for retrieving the bits for i.e. the floatValue.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct UIntFloat
    {
        [FieldOffset(0)] public float floatValue;

        [FieldOffset(0)] public uint intValue;

        [FieldOffset(0)] public double doubleValue;

        [FieldOffset(0)] public ulong longValue;
    }
}
