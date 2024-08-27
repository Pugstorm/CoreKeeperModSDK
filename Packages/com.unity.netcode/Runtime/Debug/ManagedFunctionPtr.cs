#if UNITY_EDITOR && !NETCODE_NDEBUG
#define NETCODE_DEBUG
#endif
#if NETCODE_DEBUG
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;

namespace Unity.NetCode.LowLevel.Unsafe
{
    internal static class ManagedFunctionPtr<T, Key>
        where T: Delegate
    {
        private static T m_delegate;
        static readonly SharedStatic<FunctionPointer<T>> s_SharedStatic = SharedStatic<FunctionPointer<T>>.GetOrCreate<FunctionPointer<T>, Key>(16);
        public static bool IsCreated => s_SharedStatic.Data.IsCreated;

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        static void CheckIsCreated()
        {
            if (IsCreated == false)
                throw new InvalidOperationException("ManagedFunctionPtrCall was not created!");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        static void CheckIsNotCreated()
        {
            if (IsCreated)
                throw new InvalidOperationException("ManagedFunctionPtrCall was already created!");
        }
        public static void Init(T funDelegate)
        {
            CheckIsNotCreated();
            m_delegate = funDelegate;
            s_SharedStatic.Data = new FunctionPointer<T>(Marshal.GetFunctionPointerForDelegate(funDelegate));
        }
        public static IntPtr Ptr
        {
            get
            {
                CheckIsCreated();
                return s_SharedStatic.Data.Value;
            }
        }
    }
}
#endif
