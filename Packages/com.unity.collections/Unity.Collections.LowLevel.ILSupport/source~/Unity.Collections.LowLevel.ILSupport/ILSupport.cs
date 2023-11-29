using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Unity.Collections")]

namespace Unity.Collections.LowLevel.Unsafe
{
    internal unsafe static class ILSupport
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* AddressOf<T>(in T thing)
            where T : struct
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T AsRef<T>(in T thing)
            where T : struct
        {
            throw new NotImplementedException();
        }
    }
}
