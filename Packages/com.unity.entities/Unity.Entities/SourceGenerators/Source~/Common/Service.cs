using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace Unity.Entities.SourceGen.Common
{
    /// <summary>
    /// Provide access to a per-thread global service that implement the type T
    /// Scoped Service:
    ///     A service provider register their implementation using the Scoped/Push/Pop methods.
    ///     The lifetime scope of the implementation provided is tied to the call stack when the service is registered.
    /// Global Service:
    ///     Registering a service outside of a call stack context is not implemented in this current version.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Service<T>
    {
        public struct ScopedInstance : IDisposable
        {
            public T Value;
            public ScopedInstance(T value)
            {
                Value = value;
                Push(Value);
            }
            public void Dispose() => Pop();
        }
        public struct ScopedDisposableInstance<T2> : IDisposable
            where T2 : T, IDisposable
        {
            public T2 Value;
            public ScopedDisposableInstance(T2 value)
            {
                Value = value;
                Push(Value);
            }
            public void Dispose()
            {
                Value.Dispose();
                Pop();
            }
        }
        public class ServiceRegistry
        {
            public string Name = "";
            Stack<T> m_Stack = new Stack<T>();
            public T Peek() => m_Stack.Peek();
            public int Count => m_Stack.Count;
            public void Push(T value) => m_Stack.Push(value);
            public T Pop() => m_Stack.Pop();
            public IEnumerable<T> LIFO
            {
                get
                {   
                    foreach (var v in m_Stack) yield return v;
                }
            }
            public IEnumerable<T> FIFO
            {
                get
                {
                    foreach (var v in m_Stack.Reverse()) yield return v;
                }
            }

        }
        static ThreadLocal<ServiceRegistry> s_Registry = new ThreadLocal<ServiceRegistry>(()=>new ServiceRegistry() { Name = $"Thread[{Thread.CurrentThread.ManagedThreadId}] '{Thread.CurrentThread.Name}'" });
        public static ServiceRegistry Registry => s_Registry.Value;

        /// <summary>
        /// Returns the current available service or null if no service was provided.
        /// </summary>
        public static T Instance => s_Registry.Value.Peek();

        /// <summary>
        /// True if a service is currently available. Instance will return that service.
        /// </summary>
        public static bool Available => s_Registry.Value.Count > 0;

        public static void Push(T service) => s_Registry.Value.Push(service);
        public static void Pop() => s_Registry.Value.Pop();

        /// <summary>
        /// Provide a scoped service implementation of T
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public static ScopedInstance Scoped(T service) => new ScopedInstance(service);

        /// <summary>
        /// Provide a scoped service implementation of T that also implement IDisposable.
        /// When the service is popped out of scope, IDisposable.Dispose() will be called on the provided service
        /// </summary>
        /// <typeparam name="T2"></typeparam>
        /// <param name="service"></param>
        /// <returns></returns>
        public static ScopedDisposableInstance<T2> Scoped<T2>(T2 service) where T2 : T, IDisposable => new ScopedDisposableInstance<T2>(service);



    }

}
