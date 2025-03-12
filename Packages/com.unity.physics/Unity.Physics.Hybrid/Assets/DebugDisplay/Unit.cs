using System.Threading;
using Unity.Mathematics;

namespace Unity.DebugDisplay
{
    internal struct Unit
    {
        internal int m_Begin;
        internal int m_Next;
        internal int m_End;

        internal Unit AllocateAtomic(int count)
        {
            var begin = m_Next;
            while (true)
            {
                var end = math.min(begin + count, m_End);
                if (begin == end)
                    return default;
                var found = Interlocked.CompareExchange(ref m_Next, end, begin);
                if (found == begin)
                    return new Unit { m_Begin = begin, m_Next = begin, m_End = end };
                begin = found;
            }
        }

        internal Unit(int count)
        {
            m_Begin = m_Next = 0;
            m_End = count;
        }

        internal void Fill()
        {
            m_Next = m_End;
        }

        internal int Length => m_End - m_Begin;
        internal int Filled => m_Next - m_Begin;
        internal int Remaining => m_End - m_Next;
    }
}
