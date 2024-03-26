using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Properties;

namespace Unity.NetCode
{
    /// <summary>
    /// A simple struct used to represent a network tick. This is using a uint internally, but it has special
    /// logic to deal with invalid ticks, and it handles wrap around correctly.
    /// </summary>
    [Serializable]
    public struct NetworkTick : IEquatable<NetworkTick>
    {
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckValid()
        {
            if(!IsValid)
                throw new InvalidOperationException("Cannot perform calculations with invalid ticks");
        }
        /// <summary>
        /// A value representing an invalid tick, this is the same as 'default' but provide more context in the code.
        /// </summary>
        public static NetworkTick Invalid => default;
        /// <summary>
        /// Compare two ticks, also works for invalid ticks.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator ==(in NetworkTick a, in NetworkTick b)
        {
            return a.m_Value == b.m_Value;
        }
        /// <summary>
        /// Compare two ticks, also works for invalid ticks.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator !=(in NetworkTick a, in NetworkTick b)
        {
            return a.m_Value != b.m_Value;
        }
        /// <summary>
        /// Compare two ticks, also works for invalid ticks.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj) => obj is NetworkTick && Equals((NetworkTick) obj);
        /// <summary>
        /// Compare two ticks, also works for invalid ticks.
        /// </summary>
        /// <param name="compare"></param>
        /// <returns></returns>
        public bool Equals(NetworkTick compare)
        {
            return m_Value == compare.m_Value;
        }
        /// <summary>
        /// Get a hash for the tick.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return (int)m_Value;
        }

        /// <summary>
        /// Constructor, the start tick can be 0. Use this instead of the default constructor since that will
        /// generate an invalid tick.
        /// </summary>
        /// <param name="start">The tick index to initialize the NetworkTick with.</param>
        public NetworkTick(uint start)
        {
            m_Value = (start<<1) | 1u;
        }
        /// <summary>
        /// Check if the tick is valid. Not all operations will work on invalid ticks.
        /// </summary>
        public bool IsValid => (m_Value&1)!=0;
        /// <summary>
        /// Get the tick index assuming the tick is valid. Should be used with care since ticks will wrap around.
        /// </summary>
        public uint TickIndexForValidTick
        {
            get
            {
                CheckValid();
                return m_Value>>1;
            }
        }
        /// <summary>
        /// The serialized data for a tick. Includes both validity and tick index.
        /// </summary>
        public uint SerializedData
        {
            get
            {
                return m_Value;
            }
            set
            {
                m_Value = value;
            }
        }
        /// <summary>
        /// Add a delta to the tick, assumes the tick is valid.
        /// </summary>
        /// <param name="delta">The value to add to the tick</param>
        public void Add(uint delta)
        {
            CheckValid();
            m_Value += delta<<1;
        }
        /// <summary>
        /// Subtract a delta from the tick, assumes the tick is valid.
        /// </summary>
        /// <param name="delta">The value to subtract from the tick</param>
        public void Subtract(uint delta)
        {
            CheckValid();
            m_Value -= delta<<1;
        }
        /// <summary>
        /// Increment the tick, assumes the tick is valid.
        /// </summary>
        public void Increment()
        {
            CheckValid();
            m_Value += 2;
        }
        /// <summary>
        /// Decrement the tick, assumes the tick is valid.
        /// </summary>
        public void Decrement()
        {
            CheckValid();
            m_Value -= 2;
        }
        /// <summary>
        /// Compute the number of ticks which passed since an older tick. Assumes both ticks are valid.
        /// If the passed in tick is newer this will return a negative value.
        /// </summary>
        /// <param name="older">The tick to compute passed ticks from</param>
        /// <returns></returns>
        public int TicksSince(NetworkTick older)
        {
            CheckValid();
            older.CheckValid();
            // Convert to int first to make sure negative values stay negative after shift
            int delta = (int)(m_Value-older.m_Value);
            return delta>>1;
        }
        /// <summary>
        /// Check if this tick is newer than another tick. Assumes both ticks are valid.
        /// </summary>
        /// <remarks>
        /// The ticks wraps around, so if either tick is stored for too long (several days assuming 60hz)
        /// the result might not be correct.
        /// </remarks>
        /// <param name="old">The tick to compare with</param>
        /// <returns></returns>
        public bool IsNewerThan(NetworkTick old)
        {
            CheckValid();
            old.CheckValid();
            // Invert the check so same does not count as newer
            return !(old.m_Value - m_Value < (1u << 31));
        }
        /// <summary>
        /// Convert the tick to a fixed string. Also handles invalid ticks.
        /// </summary>
        /// <returns>The tick index as a fixed string, or "Invalid" for invalid ticks.</returns>
        public FixedString32Bytes ToFixedString()
        {
            if (IsValid)
            {
                FixedString32Bytes val = default;
                val.Append(m_Value>>1);
                return val;
            }
            return "Invalid";
        }
        /// <summary>
        /// Helper property to enable exception-free visibility in the Entity Inspector
        /// </summary>
        [CreateProperty]
        public FixedString32Bytes TickValue => ToFixedString();

        private uint m_Value;
    }
}
