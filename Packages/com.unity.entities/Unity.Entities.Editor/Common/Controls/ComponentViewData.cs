using System;

namespace Unity.Entities.Editor
{
    readonly struct ComponentViewData : IEquatable<ComponentViewData>, IComparable<ComponentViewData>
    {
        public readonly Type InComponentType;
        public readonly string Name;
        public readonly ComponentType.AccessMode AccessMode;
        public readonly ComponentKind Kind;

        public ComponentViewData(Type inComponentType, string name, ComponentType.AccessMode accessMode, ComponentKind componentKind)
        {
            InComponentType = inComponentType;
            Name = name;
            AccessMode = accessMode;
            Kind = componentKind;
        }

        public int CompareTo(ComponentViewData other)
        {
            var accessModeComparison = SortOrderFromAccessMode(AccessMode).CompareTo(SortOrderFromAccessMode(other.AccessMode));
            return accessModeComparison != 0 ? accessModeComparison : string.Compare(Name, other.Name, StringComparison.Ordinal);
        }

        public bool Equals(ComponentViewData other)
            => AccessMode == other.AccessMode && string.Equals(Name, other.Name, StringComparison.InvariantCultureIgnoreCase);

        static int SortOrderFromAccessMode(ComponentType.AccessMode mode)
        {
            return mode switch
            {
                ComponentType.AccessMode.ReadWrite => 0,
                ComponentType.AccessMode.ReadOnly => 1,
                ComponentType.AccessMode.Exclude => 2,
                _ => throw new ArgumentException("Unrecognized AccessMode")
            };
        }

        internal enum ComponentKind : byte
        {
            Default = 0,
            Tag = 1,
            Buffer = 2,
            Shared = 3,
            Chunk = 4,
            Managed = 5
        }
    }
}
