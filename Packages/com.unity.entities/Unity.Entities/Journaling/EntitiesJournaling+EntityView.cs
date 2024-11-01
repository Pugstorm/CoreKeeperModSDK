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
        public unsafe readonly struct EntityView : IEquatable<EntityView>
        {
            [NativeDisableUnsafePtrRestriction] internal readonly Entity* m_EntityPtr;
            [NativeDisableUnsafePtrRestriction] readonly ulong* m_WorldSequencePtr;

            /// <summary>
            /// The entity index.
            /// </summary>
            public int Index => m_EntityPtr->Index;

            /// <summary>
            /// The entity version.
            /// </summary>
            public int Version => m_EntityPtr->Version;

            /// <summary>
            /// The entity world sequence number.
            /// </summary>
            public ulong WorldSequenceNumber => *m_WorldSequencePtr;

            /// <summary>
            /// An entity that matches the index, version and world sequence number, if it exists.
            /// </summary>
            [ExcludeFromBurstCompatTesting("Managed collections")]
            public Entity Reference => GetEntity(Index, Version, WorldSequenceNumber);

            /// <summary>
            /// The entity display name.
            /// </summary>
            [ExcludeFromBurstCompatTesting("Returns managed object")]
            public string Name
            {
                get
                {
                    var entity = new Entity { Index = Index, Version = Version };
                    if (entity == Entity.Null)
                        return Entity.Null.ToString();

#if !DOTS_DISABLE_DEBUG_NAMES
                    var world = GetWorld(WorldSequenceNumber);
                    if (world != null && world.EntityManager.Exists(entity))
                    {
                        var name = world.EntityManager.GetName(entity);
                        if (!string.IsNullOrEmpty(name))
                            return $"{name} ({Index}:{Version})";
                    }
#endif

                    return $"Entity({Index}:{Version})";
                }
            }

            internal EntityView(Entity* entityPtr, ulong* worldSequencePtr)
            {
                m_EntityPtr = entityPtr;
                m_WorldSequencePtr = worldSequencePtr;
            }

            public bool Equals(EntityView other) => Index == other.Index && Version == other.Version && WorldSequenceNumber == other.WorldSequenceNumber;
            [ExcludeFromBurstCompatTesting("Takes managed object")] public override bool Equals(object obj) => obj is EntityView entity ? Equals(entity) : false;
            public override int GetHashCode()
            {
                var hash = 5381;
                hash = hash * 31 + Index.GetHashCode();
                hash = hash * 31 + Version.GetHashCode();
                hash = hash * 31 + WorldSequenceNumber.GetHashCode();
                return hash;
            }
            public static bool operator ==(EntityView lhs, EntityView rhs) => lhs.Index == rhs.Index && lhs.Version == rhs.Version && lhs.WorldSequenceNumber == rhs.WorldSequenceNumber;
            public static bool operator !=(EntityView lhs, EntityView rhs) => !(lhs == rhs);
            [ExcludeFromBurstCompatTesting("Returns managed object")] public override string ToString() => Name;
        }
    }
}
#endif
