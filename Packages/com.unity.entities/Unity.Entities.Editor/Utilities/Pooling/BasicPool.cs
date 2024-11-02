using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.Entities.Editor
{
    class BasicPool<T>
    {
        readonly Func<T> m_Factory;
        readonly Stack<T> m_Pool = new Stack<T>();

        public BasicPool([NotNull] Func<T> factory)
        {
            m_Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public T Acquire()
        {
            var item = m_Pool.Count > 0 ? m_Pool.Pop() : m_Factory();
            ActiveInstanceCount++;

            return item;
        }

        public void Release(T instance)
        {
            m_Pool.Push(instance);
            ActiveInstanceCount--;
        }

        public void Clear()
        {
            m_Pool.Clear();
            ActiveInstanceCount = 0;
        }

        public int ActiveInstanceCount { get; private set; }
        public int PoolSize => m_Pool.Count;
    }
}
