using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Physics.Authoring
{
    static class HashUtility
    {
        static bool s_Initialized;

        internal static unsafe void Initialize()
        {
            if (s_Initialized)
                return;
            byte data = 0;
#if !(UNITY_ANDROID && !UNITY_64) // !Android32
            // ensure xxHash3 Burst delegate has been compiled on the main thread before it is used anywhere
            xxHash3.Hash128(UnsafeUtility.AddressOf(ref data), 1);
#else
            // ensure internal UnityEngine.SpookyHash class is initialized on the main thread before it is used anywhere
            var hash128 = new UnityEngine.Hash128();
            UnityEngine.HashUnsafeUtilities.ComputeHash128(UnsafeUtility.AddressOf(ref data), 1, &hash128);
#endif
            s_Initialized = true;
        }

        public static unsafe void Append<T>(ref this NativeList<byte> bytes, ref T value) where T : unmanaged
        {
            var size = UnsafeUtility.SizeOf<T>();
            bytes.AddRange(UnsafeUtility.AddressOf(ref value), size);
        }

        public static void Append<T>(ref this NativeList<byte> bytes, in NativeArray<T> data) where T : unmanaged
        {
            if (data.Length != 0)
                bytes.AddRange(data.Reinterpret<T, byte>());
        }

        const string k_InitializedMessage =
            "HashUtility was not initialized. Ensure you call HashUtility.Initialize() from the main thread first.";

        public static unsafe Hash128 Hash128(void* ptr, int length)
        {
            UnityEngine.Assertions.Assert.IsTrue(s_Initialized, k_InitializedMessage);
#if !(UNITY_ANDROID && !UNITY_64) // !Android32
            // Getting memory alignment errors from HashUtility.Hash128 on Android32
            return new Hash128(xxHash3.Hash128(ptr, length));
#else
            var result = new UnityEngine.Hash128();
            UnityEngine.HashUnsafeUtilities.ComputeHash128(ptr, (ulong)length, &result);
            return new Hash128(new HashUnion { UnityEngine_Hash = result }.Entities_Hash);
#endif
        }

#if (UNITY_ANDROID && !UNITY_64) // Android32
        [StructLayout(LayoutKind.Explicit)]
        struct HashUnion
        {
            [FieldOffset(0)]
            public UnityEngine.Hash128 UnityEngine_Hash;
            [FieldOffset(0)]
            public uint4 Entities_Hash;
        }
#endif
    }
}
