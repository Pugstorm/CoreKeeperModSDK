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
        /// Entity view into journal buffer.
        /// </summary>
        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
        [DebuggerDisplay("{Name}")]
        [StructLayout(LayoutKind.Sequential)]
        public unsafe readonly struct ComponentTypeView : IEquatable<ComponentTypeView>
        {
            [NativeDisableUnsafePtrRestriction] internal readonly TypeIndex* m_TypeIndexPtr;

            /// <summary>
            /// The component type index.
            /// </summary>
            public TypeIndex TypeIndex => *m_TypeIndexPtr;

            /// <summary>
            /// The component type from the type index.
            /// </summary>
            public ComponentType ComponentType => ComponentType.FromTypeIndex(TypeIndex);

            /// <summary>
            /// The component type name.
            /// </summary>
            [ExcludeFromBurstCompatTesting("Returns managed object")]
            public string Name
            {
                get
                {
                    var namePtr = TypeManager.GetTypeName(TypeIndex);
                    return namePtr != null ? namePtr->ToString() : "UnknownType";
                }
            }

            internal ComponentTypeView(TypeIndex* typeIndexPtr)
            {
                m_TypeIndexPtr = typeIndexPtr;
            }

            public bool Equals(ComponentTypeView other) => TypeIndex == other.TypeIndex;
            [ExcludeFromBurstCompatTesting("Takes managed object")] public override bool Equals(object obj) => obj is ComponentTypeView type ? Equals(type) : false;
            public override int GetHashCode() => TypeIndex.GetHashCode();
            public static bool operator ==(ComponentTypeView lhs, ComponentTypeView rhs) => lhs.TypeIndex == rhs.TypeIndex;
            public static bool operator !=(ComponentTypeView lhs, ComponentTypeView rhs) => !(lhs == rhs);
            [ExcludeFromBurstCompatTesting("Returns managed object")] public override string ToString() => $"{Name}:{TypeIndex}";
        }
    }
}
#endif
