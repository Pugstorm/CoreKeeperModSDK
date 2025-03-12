using System;
using Unity.Mathematics;

namespace Unity.Physics.Authoring
{
    /// <summary>
    /// Custom physics Body Tags.<para/>
    ///
    /// A lightweight type containing eight tags that can be individually toggled.
    /// Designed for tagging of rigid bodies.
    /// </summary>
    [Serializable]
    public struct CustomPhysicsBodyTags
    {
        /// <summary>
        /// Gets a <see cref="CustomPhysicsBodyTags"/> instance with all tags set to true.
        /// </summary>
        public static CustomPhysicsBodyTags Everything => new CustomPhysicsBodyTags { Value = unchecked((byte)~0) };
        /// <summary>
        /// Gets a <see cref="CustomPhysicsBodyTags"/> instance with all tags set to false.
        /// </summary>
        public static CustomPhysicsBodyTags Nothing => new CustomPhysicsBodyTags { Value = 0 };

        /// <summary> Tag 0 </summary>
        public bool Tag00;
        /// <summary> Tag 1 </summary>
        public bool Tag01;
        /// <summary> Tag 2 </summary>
        public bool Tag02;
        /// <summary> Tag 3 </summary>
        public bool Tag03;
        /// <summary> Tag 4 </summary>
        public bool Tag04;
        /// <summary> Tag 5 </summary>
        public bool Tag05;
        /// <summary> Tag 6 </summary>
        public bool Tag06;
        /// <summary> Tag 7 </summary>
        public bool Tag07;

        internal bool this[int i]
        {
            get
            {
                SafetyChecks.CheckInRangeAndThrow(i, new int2(0, 7), nameof(i));
                switch (i)
                {
                    case 0: return Tag00;
                    case 1: return Tag01;
                    case 2: return Tag02;
                    case 3: return Tag03;
                    case 4: return Tag04;
                    case 5: return Tag05;
                    case 6: return Tag06;
                    case 7: return Tag07;
                    default: return default;
                }
            }
            set
            {
                SafetyChecks.CheckInRangeAndThrow(i, new int2(0, 7), nameof(i));
                switch (i)
                {
                    case 0: Tag00 = value; break;
                    case 1: Tag01 = value; break;
                    case 2: Tag02 = value; break;
                    case 3: Tag03 = value; break;
                    case 4: Tag04 = value; break;
                    case 5: Tag05 = value; break;
                    case 6: Tag06 = value; break;
                    case 7: Tag07 = value; break;
                }
            }
        }

        /// <summary>
        /// A compact bitarray property representing the value of the tags 0 to 7 in a single byte.
        ///
        /// The i'th bit in the byte represents the value of the i'th tag, with a 1 corresponding to the true state.
        /// </summary>
        public byte Value
        {
            get
            {
                var result = 0;
                result |= (Tag00 ? 1 : 0) << 0;
                result |= (Tag01 ? 1 : 0) << 1;
                result |= (Tag02 ? 1 : 0) << 2;
                result |= (Tag03 ? 1 : 0) << 3;
                result |= (Tag04 ? 1 : 0) << 4;
                result |= (Tag05 ? 1 : 0) << 5;
                result |= (Tag06 ? 1 : 0) << 6;
                result |= (Tag07 ? 1 : 0) << 7;
                return (byte)result;
            }
            set
            {
                Tag00 = (value & (1 << 0)) != 0;
                Tag01 = (value & (1 << 1)) != 0;
                Tag02 = (value & (1 << 2)) != 0;
                Tag03 = (value & (1 << 3)) != 0;
                Tag04 = (value & (1 << 4)) != 0;
                Tag05 = (value & (1 << 5)) != 0;
                Tag06 = (value & (1 << 6)) != 0;
                Tag07 = (value & (1 << 7)) != 0;
            }
        }
    }
}
