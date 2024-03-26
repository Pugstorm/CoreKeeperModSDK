using System;
using Unity.Burst;

namespace Unity.NetCode
{
    ///<summary>
    ///Simple RAII-like wrapper that simplify making C# function delegate burst compatible.
    ///</summary>
    ///<typeparam name="T">the function delegate type</typeparam>
    public struct PortableFunctionPointer<T> where T : Delegate
    {
        /// <summary>
        /// Convert the delegate to a burst-compatible function pointer.
        /// </summary>
        /// <param name="executeDelegate"></param>
        public PortableFunctionPointer(T executeDelegate)
        {
            Ptr = BurstCompiler.CompileFunctionPointer(executeDelegate);
        }

        internal readonly FunctionPointer<T> Ptr;
    }
}
