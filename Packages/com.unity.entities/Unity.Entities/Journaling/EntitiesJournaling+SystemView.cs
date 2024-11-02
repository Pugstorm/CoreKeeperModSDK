#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    partial class EntitiesJournaling
    {
        /// <summary>
        /// System view into journal buffer.
        /// </summary>
        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
        [DebuggerDisplay("{Name}")]
        [StructLayout(LayoutKind.Sequential)]
        public unsafe readonly struct SystemView : IEquatable<SystemView>
        {
            [NativeDisableUnsafePtrRestriction] readonly SystemHandle* m_HandlePtr;

            /// <summary>
            /// The system untyped handle.
            /// </summary>
            public SystemHandle Handle => *m_HandlePtr;

            /// <summary>
            /// The system type index.
            /// </summary>
            [ExcludeFromBurstCompatTesting("uses managed Dictionary")]
            public SystemTypeIndex Type => GetSystemType(Handle);

            /// <summary>
            /// The system name.
            /// </summary>
            [ExcludeFromBurstCompatTesting("Returns managed object")]
            public string Name => GetSystemName(Handle);

            internal SystemView(SystemHandle* handlePtr)
            {
                m_HandlePtr = handlePtr;
            }

            public bool Equals(SystemView other) => Handle == other.Handle;
            [ExcludeFromBurstCompatTesting("Takes managed object")] public override bool Equals(object obj) => obj is SystemView type ? Equals(type) : false;
            public override int GetHashCode() => Handle.GetHashCode();
            public static bool operator ==(SystemView lhs, SystemView rhs) => lhs.Handle == rhs.Handle;
            public static bool operator !=(SystemView lhs, SystemView rhs) => !(lhs == rhs);
            [ExcludeFromBurstCompatTesting("Returns managed object")] public override string ToString() => $"{Name}:{Handle.GetHashCode()}";
        }
    }
}
#endif
