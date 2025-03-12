using System;
using System.Runtime.InteropServices;
using AOT;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Networking.Transport
{
    // There are situations where we need to call managed function pointers (delegate *managed<>)
    // from an unmanged context, for instance if we require a function pointer with type arguments -- because
    // unmanaged function pointers don't allow generic types.
    //
    // This struct provides an unmanaged function pointer that receives a managed function pointer
    // with a void* argument. That managed function pointer is then called on Invoke() with whatever
    // arguments we need to pass, and all arguments are passed as a void* pointer + size.
    internal unsafe struct ManagedCallWrapper
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MethodDelegate(void* functionPtr, void* arguments, int argumentsSize);

        [MonoPInvokeCallback(typeof(MethodDelegate))]
        private static void Method(void* functionPtr, void* arguments, int argumentsSize)
            => ((delegate * < void*, int, void >)functionPtr)(arguments, argumentsSize);

        private static IntPtr s_CachedWrapperPtr;

        private static void Initialize()
        {
            if (s_CachedWrapperPtr != default)
                return;

            var methodDelegate = new MethodDelegate(Method);
            GCHandle.Alloc(methodDelegate);
            s_CachedWrapperPtr = Marshal.GetFunctionPointerForDelegate(methodDelegate);
        }

        [NativeDisableUnsafePtrRestriction] IntPtr m_ManagedFunctionPtr;
        [NativeDisableUnsafePtrRestriction] IntPtr m_WrapperPtr;

        public bool IsCreated => m_ManagedFunctionPtr != default;

        /// <summary>
        /// Creates a wrapper for a managed function pointer that can be called from unmanged context
        /// </summary>
        /// <param name="managedFunctionPtr">
        /// The function pointer of a method that receives a void* containing
        /// the arguments and an int containing the size in bytes of those arguments.
        /// </param>
        public ManagedCallWrapper(delegate* < void*, int, void > managedFunctionPtr)
        {
            Initialize();
            m_WrapperPtr = s_CachedWrapperPtr;
            m_ManagedFunctionPtr = new IntPtr(managedFunctionPtr);
        }

        public void Invoke(void* arguments, int argumentsSize)
        {
            if (m_ManagedFunctionPtr == default)
                throw new NullReferenceException("Trying to invoke a null function pointer");

            ((delegate * unmanaged[Cdecl] < void*, void*, int, void >)m_WrapperPtr)(((void*)m_ManagedFunctionPtr), arguments, argumentsSize);
        }

        public void Invoke<T>(ref T arguments) where T : unmanaged
        {
            fixed(void* argumentsPtr = &arguments)
            {
                Invoke(argumentsPtr, UnsafeUtility.SizeOf<T>());
            }
        }

        public static ref A ArgumentsFromPtr<A>(void* argumentsPtr, int size) where A : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (size != UnsafeUtility.SizeOf<A>())
                throw new InvalidOperationException("The requested argument type size does not match the provided one");
#endif
            return ref *(A*)argumentsPtr;
        }
    }
}
