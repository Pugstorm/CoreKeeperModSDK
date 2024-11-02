using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Burst;
using static Unity.Burst.BurstRuntime;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;

namespace Unity.Entities
{
    /// <summary>
    /// Provides a unique id for system types as well as quick lookup information about the system type itself.
    /// This value is fully deterministic at runtime but should not be considered deterministic across builds
    /// and thus should not be serialized.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public struct SystemTypeIndex : IComparable<SystemTypeIndex>, IEquatable<SystemTypeIndex>
    {
        /// <summary>
        /// Raw value used to identify System types at runtime.
        /// <remarks>
        /// This value should not be serialized as it is not guaranteed to be deterministic across builds (but is during runtime).
        /// </remarks>
        /// </summary>
        [FieldOffset(0)] public int Value;

        /// <summary>
        /// An invalid <seealso cref="Unity.Entities.SystemTypeIndex"/> which does not map to a valid System type.
        /// </summary>
        public static SystemTypeIndex Null
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return default; }
        }

        /// <summary>
        /// The system type is a class and inherits from <seealso cref="Unity.Entities.ComponentSystemBase"/>.
        /// </summary>
        public bool IsManaged
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Value & TypeManager.SystemTypeInfo.kIsSystemManagedFlag) != 0;
        }

        /// <summary>
        /// The system type inherits from <seealso cref="Unity.Entities.ComponentSystemGroup"/>
        /// </summary>
        public bool IsGroup
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Value & TypeManager.SystemTypeInfo.kIsSystemGroupFlag) != 0;
        }

        /// <summary>
        /// The system type has a default constructor (used for of automatic creation)
        /// </summary>
        internal bool HasDefaultCtor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Value & TypeManager.SystemTypeInfo.kSystemHasDefaultCtor) != 0;
        }

        /// <summary>
        /// The system type inherits from <seealso cref="Unity.Entities.ISystemStartStop"/>
        /// </summary>
        public bool IsISystemStartStop
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Value & TypeManager.SystemTypeInfo.kIsSystemISystemStartStopFlag) != 0;
        }

        /// <summary>
        /// Zero-based index for the <seealso cref="Unity.Entities.SystemTypeIndex"/> stored in Value (the type index with no flags).
        /// </summary>
        public int Index
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Value & TypeManager.SystemTypeInfo.kClearSystemTypeFlagsMask;
        }


        /// <summary>
        /// Type flags stored in Value
        /// </summary>
        public int Flags
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Value & ~TypeManager.SystemTypeInfo.kClearSystemTypeFlagsMask;
            set => Value = (Value & TypeManager.SystemTypeInfo.kClearSystemTypeFlagsMask) | value;
        }

        /// <summary>
        /// Implicit conversion from SystemTypeIndex to an int.
        /// </summary>
        /// <param name="ti">SystemTypeIndex to convert.</param>
        /// <returns>SystemTypeIndex.Value integer representation.</returns>
        public static implicit operator int(SystemTypeIndex ti) => ti.Value;

        /// <summary>
        /// Implicit conversion from an int to a SystemTypeIndex.
        /// </summary>
        /// <param name="value">int to convert</param>
        /// <returns>SystemTypeIndex representation of the int</returns>
        public static implicit operator SystemTypeIndex(int value) => new SystemTypeIndex { Value = value };

        /// <summary>
        /// <seealso cref="Unity.Entities.SystemTypeIndex"/> instances are equal if they refer to the same system type.
        /// </summary>
        /// <param name="lhs"><seealso cref="Unity.Entities.SystemTypeIndex"/> on left side of the equality expression</param>
        /// <param name="rhs"><seealso cref="Unity.Entities.SystemTypeIndex"/> on right side of the equality expression</param>
        /// <returns>True, if both TypeIndices are equal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SystemTypeIndex lhs, SystemTypeIndex rhs)
        {
            return lhs.Value == rhs.Value;
        }

        /// <summary>
        /// <seealso cref="Unity.Entities.SystemTypeIndex"/> instances are equal if they refer to the same system type.
        /// </summary>
        /// <param name="lhs"><seealso cref="Unity.Entities.SystemTypeIndex"/> on left side of the equality expression.</param>
        /// <param name="rhs"><seealso cref="Unity.Entities.SystemTypeIndex"/> on right side of the equality expression.</param>
        /// <returns>True, if both TypeIndices are equal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SystemTypeIndex lhs, SystemTypeIndex rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>
        /// Evaluates if one <seealso cref="Unity.Entities.SystemTypeIndex"/> is less than the other.
        /// </summary>
        /// <param name="lhs">The left-hand side</param>
        /// <param name="rhs">The right-hand side</param>
        /// <returns>True if the left-hand side's <see cref="SystemTypeIndex"/> is less than the right-hand side's.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(SystemTypeIndex lhs, SystemTypeIndex rhs)
        {
            return lhs.Value < rhs.Value;
        }

        /// <summary>
        /// Evaluates if one <seealso cref="Unity.Entities.SystemTypeIndex"/> is greater than the other.
        /// </summary>
        /// <param name="lhs">The left-hand side</param>
        /// <param name="rhs">The right-hand side</param>
        /// <returns>True if the left-hand side's <see cref="SystemTypeIndex"/> is greater than the right-hand side's.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(SystemTypeIndex lhs, SystemTypeIndex rhs)
        {
            return lhs.Value > rhs.Value;
        }

        /// <summary>
        /// Evaluates if one <seealso cref="Unity.Entities.SystemTypeIndex"/> is less than or equal to the other.
        /// </summary>
        /// <param name="lhs">The left-hand side</param>
        /// <param name="rhs">The right-hand side</param>
        /// <returns>True if the left-hand side's <see cref="SystemTypeIndex"/> is less than or equal to the right-hand side's.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(SystemTypeIndex lhs, SystemTypeIndex rhs)
        {
            return lhs.Value <= rhs.Value;
        }

        /// <summary>
        /// Evaluates if one <seealso cref="Unity.Entities.SystemTypeIndex"/> is greater than or equal to the other.
        /// </summary>
        /// <param name="lhs">The left-hand side</param>
        /// <param name="rhs">The right-hand side</param>
        /// <returns>True if the left-hand side's <see cref="SystemTypeIndex"/> is greater than or equal to the right-hand side's.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(SystemTypeIndex lhs, SystemTypeIndex rhs)
        {
            return lhs.Value >= rhs.Value;
        }

        /// <summary>
        /// Compare this <seealso cref="Unity.Entities.SystemTypeIndex"/> against a given one
        /// </summary>
        /// <param name="other">The other SystemTypeIndex to compare to</param>
        /// <returns>Difference between SystemTypeIndex values</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(SystemTypeIndex other)
        {
            return Value - other.Value;
        }

        /// <summary>
        /// <seealso cref="Unity.Entities.SystemTypeIndex"/> instances are equal if they refer to the same system type.
        /// </summary>
        /// <param name="compare">The object to compare to this <seealso cref="Unity.Entities.SystemTypeIndex"/>.</param>
        /// <returns>True, if the compare parameter contains a <seealso cref="Unity.Entities.TypeIndex"/> object equal to this <seealso cref="Unity.Entities.TypeIndex"/> instance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object compare)
        {
            return (compare is SystemTypeIndex compareTypeIndex && Equals(compareTypeIndex));
        }

        /// <summary>
        /// A hash used for comparisons.
        /// </summary>
        /// <returns>A unique hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>
        /// <seealso cref="Unity.Entities.SystemTypeIndex"/> instances are equal if they refer to the same system type.
        /// </summary>
        /// <param name="typeIndex">The other <seealso cref="Unity.Entities.SystemTypeIndex"/>.</param>
        /// <returns>True, if the <seealso cref="Unity.Entities.SystemTypeIndex"/> instances are equal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(SystemTypeIndex typeIndex)
        {
            return typeIndex.Value == Value;
        }

        /// <summary>
        /// Provides a debugging string.
        /// </summary>
        /// <returns>A string containing the entity index and generational version.</returns>
        public override string ToString()
        {
            return ToFixedString().ToString();
        }

        /// <summary>
        /// Provides a Burst compatible debugging string.
        /// </summary>
        /// <returns>A string containing the entity index and generational version.</returns>
        [GenerateTestsForBurstCompatibility]
        public FixedString128Bytes ToFixedString()
        {
            var fs = new FixedString128Bytes();
            fs.Append(TypeManager.GetSystemName(this));
            return fs;
        }
    }

    static public unsafe partial class TypeManager
    {
        static int s_SystemCount;
        static List<Type> s_SystemTypes;
        static Dictionary<Type, SystemTypeIndex> s_ManagedSystemTypeToIndex;


        internal enum SystemAttributeKind
        {
            UpdateBefore,
            UpdateAfter,
            CreateBefore,
            CreateAfter,
            DisableAutoCreation,
            UpdateInGroup,
            RequireMatchingQueriesForUpdate
        }

        internal static SystemAttributeKind AttributeTypeToKind(Type attributeType)
        {
            if (attributeType == typeof(UpdateBeforeAttribute))
                return SystemAttributeKind.UpdateBefore;
            if (attributeType == typeof(UpdateAfterAttribute))
                return SystemAttributeKind.UpdateAfter;
            if (attributeType == typeof(CreateBeforeAttribute))
                return SystemAttributeKind.CreateBefore;
            if (attributeType == typeof(CreateAfterAttribute))
                return SystemAttributeKind.CreateAfter;
            if (attributeType == typeof(DisableAutoCreationAttribute))
                return SystemAttributeKind.DisableAutoCreation;
            if (attributeType == typeof(UpdateInGroupAttribute))
                return SystemAttributeKind.UpdateInGroup;
            if (attributeType == typeof(RequireMatchingQueriesForUpdateAttribute))
                return SystemAttributeKind.RequireMatchingQueriesForUpdate;

            throw new ArgumentException($"Unknown attribute type {attributeType}");
        }

        internal static Type SystemAttributeKindToType(SystemAttributeKind kind)
        {
            switch (kind)
            {
                case SystemAttributeKind.UpdateBefore:
                    return typeof(UpdateBeforeAttribute);
                case SystemAttributeKind.UpdateAfter:
                    return typeof(UpdateAfterAttribute);
                case SystemAttributeKind.CreateBefore:
                    return typeof(CreateBeforeAttribute);
                case SystemAttributeKind.CreateAfter:
                    return typeof(CreateAfterAttribute);
                case SystemAttributeKind.DisableAutoCreation:
                    return typeof(DisableAutoCreationAttribute);
                case SystemAttributeKind.UpdateInGroup:
                    return typeof(UpdateInGroupAttribute);
                case SystemAttributeKind.RequireMatchingQueriesForUpdate:
                    return typeof(RequireMatchingQueriesForUpdateAttribute);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        internal struct SystemAttribute
        {
            public const int kOrderFirstFlag = 1;
            public const int kOrderLastFlag = 1 << 1;
            public SystemAttributeKind Kind;
            public SystemTypeIndex TargetSystemTypeIndex;
            public int Flags;
        }

        internal readonly struct SystemTypeInfo
        {
            public const int kIsSystemGroupFlag = 1 << 30;
            public const int kIsSystemManagedFlag = 1 << 29;
            public const int kIsSystemISystemStartStopFlag = 1 << 28;
            public const int kSystemHasDefaultCtor = 1 << 27;
            public const int kClearSystemTypeFlagsMask = 0x07FFFFFF;

            public readonly int TypeIndex;
            public readonly int Size;
            public readonly long Hash;

            public SystemTypeInfo(int typeIndex, int size, long hash)
            {
                TypeIndex = typeIndex;
                Size = size;
                Hash = hash;
            }

            public bool IsSystemGroup => (TypeIndex & kIsSystemGroupFlag) != 0;
            public Type Type => TypeManager.GetSystemType(TypeIndex);

            [GenerateTestsForBurstCompatibility]
            public NativeText.ReadOnly DebugTypeName
            {
                get
                {
                    var pUnsafeText = GetSystemNameInternal(TypeIndex);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    NativeText.ReadOnly ro = new NativeText.ReadOnly(pUnsafeText, SharedSafetyHandle.Ref.Data);
#else
                    NativeText.ReadOnly ro = new NativeText.ReadOnly(pUnsafeText);
#endif
                    return ro;
                }
            }
        }

        static List<int> s_SystemTypeSizes;
        static UnsafeList<long> s_SystemTypeHashes;
        struct LookupFlags
        {
            public WorldSystemFilterFlags OptionalFlags;
            public WorldSystemFilterFlags RequiredFlags;
        }
        static Dictionary<LookupFlags, NativeList<SystemTypeIndex>> s_SystemFilterTypeMap;

        static UnsafeList<int> s_SystemTypeFlagsList;
        static UnsafeList<WorldSystemFilterFlags> s_SystemFilterFlagsList;
        static UnsafeList<UnsafeText> s_SystemTypeNames;
        static UnsafeList<UnsafeList<SystemAttribute>> s_SystemAttributes;

#if UNITY_DOTSRUNTIME
        static List<int> s_SystemTypeDelegateIndexRanges;
        static List<TypeRegistry.CreateSystemFn> s_AssemblyCreateSystemFn;
        static List<TypeRegistry.GetSystemAttributesFn> s_AssemblyGetSystemAttributesFn;
#endif

        // While we provide a public interface for the TypeManager the init/shutdown
        // of the TypeManager owned by the TypeManager so we mark these functions as internal
        private static void InitializeSystemsState()
        {
            const int kInitialSystemCount = 1024;
            s_SystemCount = 0;
            s_SystemTypes = new List<Type>(kInitialSystemCount);
            s_ManagedSystemTypeToIndex = new Dictionary<Type, SystemTypeIndex>(kInitialSystemCount);
            s_SystemTypeSizes = new List<int>(kInitialSystemCount);
            s_SystemTypeHashes = new UnsafeList<long>(kInitialSystemCount, Allocator.Persistent);
            s_SystemFilterTypeMap = new Dictionary<LookupFlags, NativeList<SystemTypeIndex>>(kInitialSystemCount);

            s_SystemTypeFlagsList = new UnsafeList<int>(kInitialSystemCount, Allocator.Persistent);
            s_SystemFilterFlagsList = new UnsafeList<WorldSystemFilterFlags>(kInitialSystemCount, Allocator.Persistent);

            s_SystemTypeNames = new UnsafeList<UnsafeText>(kInitialSystemCount, Allocator.Persistent);
            s_SystemAttributes = new UnsafeList<UnsafeList<SystemAttribute>>(kInitialSystemCount, Allocator.Persistent);

#if UNITY_DOTSRUNTIME
            s_SystemTypeDelegateIndexRanges = new List<int>(kInitialSystemCount);
            s_AssemblyCreateSystemFn = new List<TypeRegistry.CreateSystemFn>(kInitialSystemCount);
            s_AssemblyGetSystemAttributesFn = new List<TypeRegistry.GetSystemAttributesFn>(kInitialSystemCount);
#endif
        }

        private static void ShutdownSystemsState()
        {
            s_SystemTypes.Clear();
            s_ManagedSystemTypeToIndex.Clear();
            s_SystemTypeSizes.Clear();
            foreach (var v in s_SystemFilterTypeMap.Values)
            {
                v.Dispose();
            }

            s_SystemFilterTypeMap.Clear();

            s_SystemTypeFlagsList.Dispose();
            s_SystemFilterFlagsList.Dispose();

            foreach (var name in s_SystemTypeNames)
                name.Dispose();
            s_SystemTypeNames.Dispose();

            for (int i=0; i<s_SystemAttributes.Length; i++)
            {
                var e = s_SystemAttributes[i];
                if (e.IsCreated)
                {
                    e.Dispose();
                }
            }

            s_SystemAttributes.Dispose();
            s_SystemTypeHashes.Dispose();

#if UNITY_DOTSRUNTIME
            s_SystemTypeDelegateIndexRanges.Clear();
            s_AssemblyCreateSystemFn.Clear();
            s_AssemblyGetSystemAttributesFn.Clear();
#endif
        }

        private static void RegisterSpecialSystems()
        {
            // Reserve index 0 for a null sentinel so we always have at least _some_ system type info
            Assert.IsTrue(s_SystemCount == 0);
            AddSystemTypeToTables(null, "null", 0, 0, 0, 0);
            Assert.IsTrue(s_SystemCount == 1);
        }

        internal static void InitializeAllSystemTypes()
        {
            // DOTS Runtime registers all system info when registering component info
#if !UNITY_DOTSRUNTIME
            try
            {
                Profiler.BeginSample(nameof(InitializeAllSystemTypes));
                var isystemTypes = GetTypesDerivedFrom(typeof(ISystem)).ToList();
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var attr in asm.GetCustomAttributes<RegisterGenericSystemTypeAttribute>())
                    {
                        isystemTypes.Add(attr.ConcreteType);
                    }
                }

                // Used to detect cycles in the UpdateInGroup tree, so we don't recurse infinitely and crash.
                var visitedSystemGroupsSet = new HashSet<Type>(32);

                foreach (var systemType in isystemTypes)
                {
                    if (!systemType.IsValueType)
                        continue;
                    if (systemType
                        .ContainsGenericParameters) // don't register the open versions of generic isystems, only the closed
                        continue;

                    var name = systemType.FullName;
                    var size = UnsafeUtility.SizeOf(systemType);
                    var hash = GetHashCode64(systemType);
                    // isystems can't be groups
                    var flags = GetSystemTypeFlags(systemType);
                    if (typeof(ISystem).IsAssignableFrom(systemType) && ((flags & SystemTypeInfo.kIsSystemManagedFlag) != 0))
                        Debug.LogError($"System {systemType} has managed fields, but implements ISystem, which is not allowed. If you need to use managed fields, please inherit from SystemBase.");
                    var filterFlags = MakeWorldFilterFlags(systemType, ref visitedSystemGroupsSet);

                    AddSystemTypeToTables(systemType, name, size, hash, flags, filterFlags);
                }

                foreach (var systemType in GetTypesDerivedFrom(typeof(ComponentSystemBase)))
                {
                    if (systemType.IsAbstract || systemType.ContainsGenericParameters)
                        continue;

                    var name = systemType.FullName;
                    var size = -1; // Don't get a type size for a managed type
                    var hash = GetHashCode64(systemType);
                    var flags = GetSystemTypeFlags(systemType);

                    var filterFlags = MakeWorldFilterFlags(systemType, ref visitedSystemGroupsSet);

                    AddSystemTypeToTables(systemType, name, size, hash, flags, filterFlags);
                }

                /*
                 * We need to do this after we've added all the systems to all the tables so that system type indices
                 * will all already exist, even for systems later in the list, so that if we find e.g. an UpdateAfter
                 * attr that refers to a system later in the list, we can find the typeindex for said later system
                 * and put it in the table.
                 */
                s_SystemAttributes.Add(new UnsafeList<SystemAttribute>());

                for (int i = 1; i < s_SystemCount; i++)
                {
                    AddSystemAttributesToTable(GetSystemType(i));
                }
            }
            finally
            {
                Profiler.EndSample();
            }
#else
            s_SystemAttributes.Add(new UnsafeList<SystemAttribute>());

            for (int i = 1; i < s_SystemCount; i++)
            {
                AddSystemAttributesToTable(GetSystemType(i));
            }
#endif
        }

        private static void AddSystemAttributesToTable(Type systemType)
        {
            var kDisabledCreationAttribute = typeof(DisableAutoCreationAttribute);
            int j = 0;
            var list = new UnsafeList<SystemAttribute>(16, Allocator.Persistent);
            foreach (var attributeType in new[]
                     {
                         typeof(UpdateBeforeAttribute),
                         typeof(UpdateAfterAttribute),
                         typeof(CreateBeforeAttribute),
                         typeof(CreateAfterAttribute),
                         typeof(DisableAutoCreationAttribute),
                         typeof(UpdateInGroupAttribute),
                         typeof(RequireMatchingQueriesForUpdateAttribute)
                     }
                    )
            {
                var attrKind = (SystemAttributeKind)j;
                j++;
                if (attributeType == kDisabledCreationAttribute)
                {
                    // We do not want to inherit DisableAutoCreation from some parent type (as that attribute explicitly states it should not be inherited)
                    var objArr = systemType.GetCustomAttributes(attributeType, false);

                    var alreadyDisabled = false;
                    for (int i = 0; i < objArr.Length; i++)
                    {
                        var attr = objArr[i] as Attribute;

                        if (attr.GetType() == kDisabledCreationAttribute)
                        {
                            alreadyDisabled = true;
                            list.Add(new SystemAttribute { Kind = attrKind, TargetSystemTypeIndex = -1 });
                            break;
                        }
                    }

                    if (!alreadyDisabled && systemType.Assembly.GetCustomAttribute(attributeType) != null)
                    {
                        list.Add(new SystemAttribute { Kind = attrKind, TargetSystemTypeIndex = -1 });
                    }
                }
                else
                {
                    var objArr = systemType.GetCustomAttributes(attributeType, true);

                    if (objArr.Length == 0) continue;

                    if (attrKind == SystemAttributeKind.CreateAfter)
                    {
                        for (int i = 0; i < objArr.Length; i++)
                        {
                            var myattr = objArr[i] as CreateAfterAttribute;

                            list.Add(new SystemAttribute
                            {
                                Kind = attrKind,
                                TargetSystemTypeIndex = IsSystemType(myattr.SystemType) ? GetSystemTypeIndex(myattr.SystemType) : -1
                            });
                        }
                    }

                    if (attrKind == SystemAttributeKind.CreateBefore)
                    {
                        for (int i = 0; i < objArr.Length; i++)
                        {
                            var myattr = objArr[i] as CreateBeforeAttribute;

                            list.Add(new SystemAttribute
                            {
                                Kind = attrKind,
                                TargetSystemTypeIndex = IsSystemType(myattr.SystemType) ? GetSystemTypeIndex(myattr.SystemType) : -1
                            });
                        }
                    }

                    if (attrKind == SystemAttributeKind.UpdateAfter)
                    {
                        for (int i = 0; i < objArr.Length; i++)
                        {
                            var myattr = objArr[i] as UpdateAfterAttribute;

                            list.Add(new SystemAttribute
                            {
                                Kind = attrKind,
                                TargetSystemTypeIndex = IsSystemType(myattr.SystemType) ? GetSystemTypeIndex(myattr.SystemType) : -1
                            });
                        }
                    }

                    if (attrKind == SystemAttributeKind.UpdateBefore)
                    {
                        for (int i = 0; i < objArr.Length; i++)
                        {
                            var myattr = objArr[i] as UpdateBeforeAttribute;

                            list.Add(new SystemAttribute
                            {
                                Kind = attrKind,
                                TargetSystemTypeIndex = IsSystemType(myattr.SystemType) ? GetSystemTypeIndex(myattr.SystemType) : -1
                            });
                        }
                    }

                    if (attrKind == SystemAttributeKind.UpdateInGroup)
                    {
                        for (int i = 0; i < objArr.Length; i++)
                        {
                            var myattr = objArr[i] as UpdateInGroupAttribute;

                            int flags = 0;

                            if (myattr.OrderFirst) flags |= SystemAttribute.kOrderFirstFlag;
                            if (myattr.OrderLast) flags |= SystemAttribute.kOrderLastFlag;

                            list.Add(new SystemAttribute
                            {
                                Kind = attrKind,
                                TargetSystemTypeIndex =
                                    (IsSystemType(myattr.GroupType) && IsSystemAGroup(myattr.GroupType))
                                        ? GetSystemTypeIndex(myattr.GroupType)
                                        : -1,
                                Flags = flags
                            });

                        }
                    }

                    if (attrKind == SystemAttributeKind.RequireMatchingQueriesForUpdate)
                    {
                        list.Add(new SystemAttribute()
                            { Kind = attrKind, TargetSystemTypeIndex = -1 });
                    }
                }
            }

            var systemTypeIndex = GetSystemTypeIndex(systemType);
            s_SystemAttributes.Add(list);
            Assertions.Assert.IsTrue(systemTypeIndex.Index == s_SystemAttributes.Length - 1);
        }

//need to implement UnsafeUtility.IsUnmanaged in dotsrt for this to work
#if !UNITY_DOTSRUNTIME
        private static int GetSystemTypeFlags(Type systemType)
        {
            var flags = 0;
            if (systemType.GetConstructors().Any(c => c.GetParameters().Length == 0))
                flags |= SystemTypeInfo.kSystemHasDefaultCtor;
            if (systemType.IsSubclassOf(typeof(ComponentSystemGroup)))
                flags |= SystemTypeInfo.kIsSystemGroupFlag;
            if (!UnsafeUtility.IsUnmanaged(systemType))
                flags |= SystemTypeInfo.kIsSystemManagedFlag;
            if (typeof(ISystemStartStop).IsAssignableFrom(systemType))
                flags |= SystemTypeInfo.kIsSystemISystemStartStopFlag;
            return flags;
        }
#endif

        /// <summary>
        /// Construct a System from a Type. Uses the same list in <see cref="GetSystems"/>.
        /// </summary>
        /// <param name="systemType">The system type.</param>
        /// <returns>Returns the new system.</returns>
        public static ComponentSystemBase ConstructSystem(Type systemType)
        {
            if (!typeof(ComponentSystemBase).IsAssignableFrom(systemType))
                throw new ArgumentException($"'{systemType.FullName}' cannot be constructed as it does not inherit from ComponentSystemBase");

            // In cases where we are dealing with generic types, we may need to calculate typeinformation that wasn't collected upfront
            AddSystemTypeToTablesAfterInit(systemType);

            return (ComponentSystemBase)Activator.CreateInstance(systemType);
        }

        /// <summary>
        /// Creates an instance of a system of type T.
        /// </summary>
        /// <typeparam name="T">System type to create</typeparam>
        /// <returns>Returns the newly created system instance</returns>
        public static T ConstructSystem<T>() where T : ComponentSystemBase
        {
            return (T)ConstructSystem(typeof(T));
        }

        /// <summary>
        /// Creates an instance of a system of System.Type.
        /// </summary>
        /// <typeparam name="T">System type to create</typeparam>
        /// <param name="systemType">System type to create</param>
        /// <returns>Returns the newly created system instance</returns>
        public static T ConstructSystem<T>(Type systemType) where T : ComponentSystemBase
        {
            return (T)ConstructSystem(systemType);
        }

        /// <summary>
        /// Return an array of all System types available to the runtime matching the WorldSystemFilterFlags. By default,
        /// all systems available to the runtime is returned. Prefer GetSystemTypeIndices over this to avoid
        /// unnecessary reflection.
        /// </summary>
        /// <param name="filterFlags">Flags the returned systems can have</param>
        /// <param name="requiredFlags">Flags the returned systems must have</param>
        /// <returns>Returns a list of systems meeting the flag requirements provided</returns>
        public static IReadOnlyList<Type> GetSystems(
            WorldSystemFilterFlags filterFlags = WorldSystemFilterFlags.All,
            WorldSystemFilterFlags requiredFlags = 0)
        {
            var ret = new List<Type>();
            var indices = GetSystemTypeIndices(filterFlags, requiredFlags);

            for (int i = 0; i < indices.Length; i++)
            {
                ret.Add(GetSystemType(indices[i]));
            }

            return ret;
        }

        /// <summary>
        /// Return an array of all System types available to the runtime matching the WorldSystemFilterFlags. By default,
        /// all systems available to the runtime is returned. This version avoids unnecessary reflection.
        /// </summary>
        /// <param name="filterFlags">Flags the returned systems can have</param>
        /// <param name="requiredFlags">Flags the returned systems must have</param>
        /// <returns>Returns a list of systems meeting the flag requirements provided</returns>
        public static NativeList<SystemTypeIndex> GetSystemTypeIndices(
            WorldSystemFilterFlags filterFlags = WorldSystemFilterFlags.All,
            WorldSystemFilterFlags requiredFlags = 0)
        {
            // Expand default to proper types
            if ((filterFlags & WorldSystemFilterFlags.Default) != 0)
            {
                filterFlags &= ~WorldSystemFilterFlags.Default;
                filterFlags |= WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.Presentation;
            }

            Assertions.Assert.IsTrue(s_Initialized, "The TypeManager must be initialized before the TypeManager can be used.");
            // By default no flags are required
            requiredFlags &= ~WorldSystemFilterFlags.Default;
            LookupFlags lookupFlags = new LookupFlags() { OptionalFlags = filterFlags, RequiredFlags = requiredFlags };

            if (s_SystemFilterTypeMap.TryGetValue(lookupFlags, out var systemTypeIndices))
                return systemTypeIndices;

            // Use a temp list since we don't know how many systems will be filtered out yet
            var tempFilteredSystemTypes = new NativeList<SystemTypeIndex>(s_SystemTypes.Count-1, Allocator.Temp);

            // Skip index 0 since that is always null
            for (int i = 1; i < s_SystemTypes.Count;++i)
            {
                var systemType = s_SystemTypes[i];
                if (FilterSystemType(i, lookupFlags))
                    tempFilteredSystemTypes.Add(GetSystemTypeIndex(systemType));
            }

            SortSystemTypesInCreationOrder(tempFilteredSystemTypes);

            var persistentSystemList = new NativeList<SystemTypeIndex>(tempFilteredSystemTypes.Length, Allocator.Persistent);
            persistentSystemList.CopyFrom(tempFilteredSystemTypes);

            s_SystemFilterTypeMap[lookupFlags] = persistentSystemList;
            return persistentSystemList;
        }


        internal static void AddSystemTypeToTablesAfterInit(Type systemType)
        {
            Assertions.Assert.IsTrue(SharedSystemCount.Ref.UnsafeDataPointer != null, "This method must not be called until TypeManager initialization is complete.");

            if (systemType == null || s_ManagedSystemTypeToIndex.ContainsKey(systemType))
                return;

            var size = -1; // Don't get a type size for a managed type
#if !UNITY_DOTSRUNTIME
            if(systemType.IsValueType)
                size = UnsafeUtility.SizeOf(systemType);
#else
            // In non NET_DOTS DOTS Runtime builds we should only be calling this function for
            // types that are not statically known
            Assert.IsTrue(systemType.IsGenericType);
#endif
            var hash = GetHashCode64(systemType);
            var name = systemType.FullName;
#if UNITY_DOTSRUNTIME
            var flags = 0; //this is very possibly wrong, but it's also hard to find out
#else
            var flags = GetSystemTypeFlags(systemType);
#endif

            var filterFlags = default(WorldSystemFilterFlags);
            AddSystemTypeToTables(systemType, name, size, hash, flags, filterFlags);
            AddSystemAttributesToTable(systemType);

            // Since we may have regrown our static arrays, ensure the shared statics are updated to use the new addresses
            InitializeSystemSharedStatics();
        }

        /// <summary>
        /// Sorts a list of system types based on rules defined via the <see cref="ISystemOrderAttribute"/>'s.
        /// <see cref="CreateBeforeAttribute"/> and <see cref="CreateAfterAttribute"/>.
        /// </summary>
        /// <param name="systemTypes">A List of system types to sort.</param>
        public static void SortSystemTypesInCreationOrder(List<Type> systemTypes)
        {

            var indices = new NativeList<SystemTypeIndex>(systemTypes.Count, Allocator.Temp);
            for (int i = 0; i < systemTypes.Count; i++)
            {
                indices.Add(GetSystemTypeIndex(systemTypes[i]));
            }

            SortSystemTypesInCreationOrder(indices);

            for (int i = 0; i < indices.Length; ++i)
                systemTypes[i] = GetSystemType(indices[i]);
        }


        /// <summary>
        /// Sort the provided system type indices by their systems' CreateAfter and CreateBefore attributes, for the
        /// purposes of creating them in an order that respects said constraints. For use in implementing custom world
        /// creation, such as with ICustomBootstrap.
        /// </summary>
        /// <param name="indices">The system type indices to sort</param>
        public static void SortSystemTypesInCreationOrder(NativeList<SystemTypeIndex> indices)
        {
            var systemElements = new UnsafeList<ComponentSystemSorter.SystemElement>(indices.Length, Allocator.Temp);
            systemElements.Length = indices.Length;

            for (int i = 0; i < indices.Length; ++i)
            {
                systemElements[i] = new ComponentSystemSorter.SystemElement
                {
                    SystemTypeIndex = indices[i],
                    Index = new UpdateIndex(i, false),
                    OrderingBucket = 0,
                    updateBefore = new NativeList<int>(16, Allocator.Temp),
                    nAfter = 0,
                };
            }

            // Find & validate constraints between systems in the group
            var lookupDictionary = new NativeHashMap<SystemTypeIndex, int>(systemElements.Length, Allocator.Temp);

            var lookupDictionaryPtr = (NativeHashMap<SystemTypeIndex, int>*)UnsafeUtility.AddressOf(ref lookupDictionary);

            var sysElemsPtr = (UnsafeList<ComponentSystemSorter.SystemElement>*)UnsafeUtility.AddressOf(ref systemElements);

            var badSystemTypeIndices = new NativeHashSet<SystemTypeIndex>(16, Allocator.Temp);
            var badSystemTypeIndicesPtr = (NativeHashSet<SystemTypeIndex>*)UnsafeUtility.AddressOf(ref badSystemTypeIndices);
            ComponentSystemSorter.FindConstraints(
                -1,
                sysElemsPtr,
                lookupDictionaryPtr,
                SystemAttributeKind.CreateAfter,
                SystemAttributeKind.CreateBefore,
                badSystemTypeIndicesPtr);

            foreach (var badindex in badSystemTypeIndices)
                ComponentSystemSorter.WarnAboutAnySystemAttributeBadness(badindex, null);

            badSystemTypeIndices.Clear();

            ComponentSystemSorter.Sort(
                sysElemsPtr,
                lookupDictionaryPtr);

            for (int i = 0; i < systemElements.Length; ++i)
                indices[i] = systemElements[i].SystemTypeIndex;
        }

        internal static void AddSystemTypeToTables(Type type, string typeName, int typeSize, long typeHash, int systemTypeFlags, WorldSystemFilterFlags filterFlags)
        {
            if (type != null && s_ManagedSystemTypeToIndex.ContainsKey(type))
                return;

            SystemTypeIndex systemIndex = s_SystemCount++ | systemTypeFlags;

            if (type != null)
            {
                s_ManagedSystemTypeToIndex.Add(type, systemIndex);
                SharedSystemTypeIndex.Get(type) = systemIndex;
            }

            s_SystemTypes.Add(type);
            s_SystemTypeSizes.Add(typeSize);
            s_SystemTypeHashes.Add(typeHash);

            var unsafeName = new UnsafeText(-1, Allocator.Persistent);
            var utf8Bytes = Encoding.UTF8.GetBytes(typeName);
            unsafe
            {
                fixed (byte* b = utf8Bytes)
                    unsafeName.Append(b, (ushort) utf8Bytes.Length);
            }
            s_SystemTypeNames.Add(unsafeName);

            s_SystemTypeFlagsList.Add(systemTypeFlags);
            s_SystemFilterFlagsList.Add(filterFlags);
        }

        internal static void InitializeSystemSharedStatics()
        {
            SharedSystemTypeNames.Ref.Data = new IntPtr(s_SystemTypeNames.Ptr);
            SharedSystemAttributes.Ref.Data = new IntPtr(s_SystemAttributes.Ptr);
            SharedSystemCount.Ref.Data = s_SystemCount;
            SharedSystemTypeHashes.Ref.Data = new IntPtr(s_SystemTypeHashes.Ptr);
        }

        internal static WorldSystemFilterFlags GetSystemFilterFlags(Type type)
        {
            var systemIndex = GetSystemTypeIndex(type).Index;
            var flags = s_SystemFilterFlagsList[systemIndex];

            return flags;
        }

        internal static WorldSystemFilterFlags GetSystemFilterFlags(SystemTypeIndex i)
        {
            return s_SystemFilterFlagsList[i.Index];
        }

        /// <summary>
        /// Determines if a given type is a System type (e.g. ISystem, SystemBase).
        /// </summary>
        /// <param name="type">Type to check</param>
        /// <returns>Returns true if the input type is a System. False otherwise</returns>
        public static bool IsSystemType(Type type)
        {
            return GetSystemTypeIndexNoThrow(type) != -1;
        }

        internal static bool IsSystemTypeIndex(SystemTypeIndex systemTypeIndex)
        {
            var systemTypeIndexNoFlags = systemTypeIndex & SystemTypeInfo.kClearSystemTypeFlagsMask;
            return systemTypeIndexNoFlags >= 0 && systemTypeIndexNoFlags < SharedSystemCount.Ref.Data;
        }


        internal static UnsafeText* GetSystemTypeNamesPointer()
        {
            return (UnsafeText*) SharedSystemTypeNames.Ref.Data;
        }

        internal static UnsafeText* GetSystemNameInternal(SystemTypeIndex systemIndex)
        {
            return GetSystemTypeNamesPointer() + (systemIndex.Index);
        }

        /// <summary>
        /// Retrieve the name of a system via its type.
        /// </summary>
        /// <param name="type">Input type</param>
        /// <returns>System name for the type</returns>
        public static NativeText.ReadOnly GetSystemName(Type type)
        {
            var systemIndex = Math.Max(0, GetSystemTypeIndexNoThrow(type));
            return GetSystemName(systemIndex);
        }


        /// <summary>
        /// Retrives the name of a system via its system index.
        /// </summary>
        /// <param name="systemIndex">System index to lookup the name for</param>
        /// <returns>Returns the name of the system. Otherwise an empty string</returns>
        public static NativeText.ReadOnly GetSystemName(SystemTypeIndex systemIndex)
        {
            var pUnsafeText = GetSystemNameInternal(systemIndex);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeText.ReadOnly ro = new NativeText.ReadOnly(pUnsafeText, SharedSafetyHandle.Ref.Data);
#else
            NativeText.ReadOnly ro = new NativeText.ReadOnly(pUnsafeText);
#endif
            return ro;
        }

        /// <summary>
        /// Returns the system index for System T.
        /// </summary>
        /// <typeparam name="T">System type</typeparam>
        /// <returns>Returns the System index for the input type T</returns>
        public static SystemTypeIndex GetSystemTypeIndex<T>()
        {
            var index = SharedSystemTypeIndex<T>.Ref.Data;

            if (index <= 0)
            {
                ManagedException<T>();
                BurstException<T>();
            }

            return index;
        }

        internal static SystemTypeIndex GetSystemTypeIndexNoThrow<T>()
        {
            return SharedSystemTypeIndex<T>.Ref.Data;
        }


        internal static long GetSystemTypeHash(Type type)
        {
            var systemIndex = GetSystemTypeIndex(type);
            return s_SystemTypeHashes[systemIndex.Index];
        }

        internal static long GetSystemTypeHash(SystemTypeIndex systemTypeIndex)
        {
            return ((long*)SharedSystemTypeHashes.Ref.Data)![systemTypeIndex.Index];
        }

        internal static long GetSystemTypeHash<T>()
        {
            return GetHashCode64<T>();
        }

        internal static int GetSystemTypeSize(Type type)
        {
            var systemIndex = GetSystemTypeIndex(type);
            return s_SystemTypeSizes[systemIndex.Index];
        }

        internal static int GetSystemTypeSize(SystemTypeIndex index)
        {
            return s_SystemTypeSizes[index.Index];
        }

        internal static int GetSystemTypeSize<T>()
        {
            var systemIndex = GetSystemTypeIndex<T>();
            return s_SystemTypeSizes[systemIndex.Index];
        }


        internal static SystemTypeIndex GetSystemTypeIndexNoThrow(Type type)
        {
            if (type == null)
                return 0;

            SystemTypeIndex res;
            if (s_ManagedSystemTypeToIndex.TryGetValue(type, out res))
                return res;
            else
                return -1;
        }

        /// <summary>
        /// Gets the <see cref="SystemTypeIndex"/> for the given system type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static SystemTypeIndex GetSystemTypeIndex(Type type)
        {
            var index = GetSystemTypeIndexNoThrow(type);
            if (index == -1)
            {
                if (!typeof(ISystem).IsAssignableFrom(type) && !typeof(ComponentSystemBase).IsAssignableFrom(type))
                    throw new ArgumentException(
                        $"The passed-in Type {type} is not a type that derives from SystemBase or ISystem");
                else
                {
                    var ret = s_SystemCount;
                    AddSystemTypeToTablesAfterInit(type);
                    return ret;
                }
            }


            return index;
        }

        internal static Type GetSystemType(SystemTypeIndex systemTypeIndex)
        {
            int typeIndexNoFlags = systemTypeIndex.Index;
            Assertions.Assert.IsTrue(typeIndexNoFlags < s_SystemTypes.Count);
            return s_SystemTypes[typeIndexNoFlags];
        }

        /// <summary>
        /// Check if the provided type is a SystemGroup.
        /// </summary>
        /// <param name="type">Type to check</param>
        /// <returns>Returns true if the provided type is a SystemGroup (inherits from  <see cref="ComponentSystemGroup"/> or a sub-class of <see cref="ComponentSystemGroup"/>)</returns>
        public static bool IsSystemAGroup(Type type)
        {
            Assertions.Assert.IsTrue(s_Initialized, "The TypeManager must be initialized before the TypeManager can be used.");

            return GetSystemTypeIndex(type).IsGroup;
        }

        /// <summary>
        /// Check if the provided type is a managed system.
        /// </summary>
        /// <param name="type">Type to check</param>
        /// <returns>Returns true if the system type is managed. Otherwise false.</returns>
        public static bool IsSystemManaged(Type type)
        {
            Assertions.Assert.IsTrue(s_Initialized, "The TypeManager must be initialized before the TypeManager can be used.");

            return GetSystemTypeIndex(type).IsManaged;
        }

        /// <summary>
        /// Retrieve type flags for a system index.
        /// </summary>
        /// <param name="systemTypeIndex">System index to use for flag lookup</param>
        /// <returns>System type flags</returns>
        public static int GetSystemTypeFlags(SystemTypeIndex systemTypeIndex)
        {
            return s_SystemTypeFlagsList[systemTypeIndex.Index];
        }

        internal static NativeList<SystemAttribute> GetSystemAttributes(
            SystemTypeIndex systemTypeIndex,
            SystemAttributeKind kind,
            Allocator allocator = Allocator.Temp)
        {
            Assertions.Assert.IsTrue(s_Initialized, "The TypeManager must be initialized before the TypeManager can be used.");

            var attributesList = (UnsafeList<SystemAttribute>*)SharedSystemAttributes.Ref.Data;

            if (IsSystemTypeIndex(systemTypeIndex))
            {
                var list = attributesList[systemTypeIndex.Index];
                var ret = new NativeList<SystemAttribute>(list.Length, allocator);;

                for (int i = 0; i < list.Length; i++)
                {
                    if (list[i].Kind == kind)
                        ret.Add(list[i]);
                }

                return ret;
            }
            else
            {
                UnityEngine.Debug.LogError($"System type index {systemTypeIndex} is not valid, returning empty attribute list.");
                return new NativeList<SystemAttribute>(0, allocator);
            }
        }

        /// <summary>
        /// Get all the attribute objects of Type attributeType for a System.
        /// </summary>
        /// <param name="systemType">System type</param>
        /// <param name="attributeType">Attribute type to return</param>
        /// <returns>Returns all attributes of type attributeType decorating systemType</returns>
        public static Attribute[] GetSystemAttributes(Type systemType, Type attributeType)
        {
            Attribute[] attributes;

            var kDisabledCreationAttribute = typeof(DisableAutoCreationAttribute);
            if (attributeType == kDisabledCreationAttribute)
            {
                // We do not want to inherit DisableAutoCreation from some parent type (as that attribute explicitly states it should not be inherited)
                var objArr = systemType.GetCustomAttributes(attributeType, false);
                var attrList = new List<Attribute>();

                var alreadyDisabled = false;
                for (int i = 0; i < objArr.Length; i++)
                {
                    var attr = objArr[i] as Attribute;
                    attrList.Add(attr);

                    if (attr.GetType() == kDisabledCreationAttribute)
                        alreadyDisabled = true;
                }

                if (!alreadyDisabled && systemType.Assembly.GetCustomAttribute(attributeType) != null)
                {
                    attrList.Add(new DisableAutoCreationAttribute());
                }
                attributes = attrList.ToArray();
            }
            else
            {
                var objArr = systemType.GetCustomAttributes(attributeType, true);
                attributes = new Attribute[objArr.Length];
                for (int i = 0; i < objArr.Length; i++)
                {
                    attributes[i] = objArr[i] as Attribute;
                }
            }
            return attributes;
        }

        private struct SharedSystemTypeIndex
        {
            public static ref SystemTypeIndex Get(Type systemType)
            {
                return ref SharedStatic<SystemTypeIndex>.GetOrCreate(typeof(TypeManagerKeyContext), systemType).Data;
            }
        }

        private struct SharedSystemTypeNames
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedSystemTypeNames>();
        }

        private struct SharedSystemAttributes
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedSystemAttributes>();
        }

        private struct SharedSystemTypeHashes
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedSystemTypeHashes>();
        }

        // Marked as internal as this is used by StaticTypeRegistryILPostProcessor
        internal struct SharedSystemTypeIndex<TSystem>
        {
            public static readonly SharedStatic<int> Ref = SharedStatic<int>.GetOrCreate<TypeManagerKeyContext, TSystem>();
        }

        private struct SharedSystemCount
        {
            public static readonly SharedStatic<int> Ref = SharedStatic<int>.GetOrCreate<TypeManagerKeyContext, SharedSystemCount>();
        }

        static bool FilterSystemType(SystemTypeIndex systemTypeIndex, LookupFlags lookupFlags)
        {
            var attrs = GetSystemAttributes(systemTypeIndex, SystemAttributeKind.DisableAutoCreation);

            if (systemTypeIndex.IsManaged && !systemTypeIndex.HasDefaultCtor)
            {
                if (attrs.Length == 0)
                    Debug.LogWarning(
                        $"Missing default ctor on {GetSystemName(systemTypeIndex)} (or if you don't want this to be auto-creatable, tag it with [DisableAutoCreation])");
                return false;
            }

            if (attrs.Length > 0)
                return false;

            if (lookupFlags.OptionalFlags == WorldSystemFilterFlags.All)
                return true;


            var systemFlags = GetSystemFilterFlags(systemTypeIndex);

            if ((lookupFlags.RequiredFlags & WorldSystemFilterFlags.Editor) != 0)
                lookupFlags.OptionalFlags |= WorldSystemFilterFlags.Editor;

            return (lookupFlags.OptionalFlags & systemFlags) != 0 && (lookupFlags.RequiredFlags & systemFlags) == lookupFlags.RequiredFlags;
        }

#if !UNITY_DOTSRUNTIME
        internal static IEnumerable<Type> GetTypesDerivedFrom(Type type)
        {
#if UNITY_EDITOR
            return UnityEditor.TypeCache.GetTypesDerivedFrom(type);
#else

            var types = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!TypeManager.IsAssemblyReferencingEntities(assembly))
                    continue;

                try
                {
                    var assemblyTypes = assembly.GetTypes();
                    foreach (var t in assemblyTypes)
                    {
                        if (type.IsAssignableFrom(t))
                            types.Add(t);
                    }
                }
                catch (ReflectionTypeLoadException e)
                {
                    foreach (var t in e.Types)
                    {
                        if (t != null && type.IsAssignableFrom(t))
                            types.Add(t);
                    }

                    Debug.LogWarning($"DefaultWorldInitialization failed loading assembly: {(assembly.IsDynamic ? assembly.ToString() : assembly.Location)}");
                }
            }

            return types;
#endif
        }

        static WorldSystemFilterFlags GetParentGroupDefaultFilterFlags(Type type, ref HashSet<Type> visitedSystemGroupsSet)
        {
            if (!Attribute.IsDefined(type, typeof(UpdateInGroupAttribute), true))
            {
                // Fallback default
                return WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation;
            }
            var attrs = type.GetCustomAttributes<UpdateInGroupAttribute>(true);
            WorldSystemFilterFlags systemFlags = default;
            foreach (var uig in attrs)
            {
                var groupType = ((UpdateInGroupAttribute)uig).GroupType;
                if (!visitedSystemGroupsSet.Add(groupType))
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("The following systems form a cycle in their UpdateInGroup attributes: ");
                    foreach (var gt in visitedSystemGroupsSet)
                        sb.Append($"{gt} ");
                    throw new InvalidOperationException(sb.ToString());
                }
                var groupFlags = WorldSystemFilterFlags.Default;
                if (Attribute.IsDefined(groupType, typeof(WorldSystemFilterAttribute), true))
                {
                    groupFlags = groupType.GetCustomAttribute<WorldSystemFilterAttribute>(true).ChildDefaultFilterFlags;
                }
                if ((groupFlags & WorldSystemFilterFlags.Default) != 0)
                {
                    groupFlags &= ~WorldSystemFilterFlags.Default;
                    groupFlags |= GetParentGroupDefaultFilterFlags(groupType, ref visitedSystemGroupsSet);
                }
                systemFlags |= groupFlags;
            }
            return systemFlags;
        }
        static WorldSystemFilterFlags MakeWorldFilterFlags(Type type, ref HashSet<Type> visitedSystemGroupsSet)
        {
            // IMPORTANT: keep this logic in sync with SystemTypeGen.cs for DOTS Runtime
            WorldSystemFilterFlags systemFlags = WorldSystemFilterFlags.Default;

            if (Attribute.IsDefined(type, typeof(WorldSystemFilterAttribute), true))
                systemFlags = type.GetCustomAttribute<WorldSystemFilterAttribute>(true).FilterFlags;

            if ((systemFlags & WorldSystemFilterFlags.Default) != 0)
            {
                systemFlags &= ~WorldSystemFilterFlags.Default;
                visitedSystemGroupsSet.Clear();
                systemFlags |= GetParentGroupDefaultFilterFlags(type, ref visitedSystemGroupsSet);
            }

            if (Attribute.IsDefined(type, typeof(ExecuteInEditMode)))
                Debug.LogWarning($"{type} is decorated with {typeof(ExecuteInEditMode)}. Support for this attribute on systems is deprecated as it is meant for MonoBehaviours only. " +
                    $"Please use [WorldSystemFilter({nameof(WorldSystemFilterFlags.Editor)})] instead to ensure your system is added and runs in the Editor's default world.");

            if (Attribute.IsDefined(type, typeof(ExecuteAlways)))
            {
                // Until we formally deprecate ExecuteAlways, add in the Editor flag as this has the same meaning
                // When we deprecate uncomment the log error below
                Debug.LogWarning($"{type} is decorated with {typeof(ExecuteAlways)}. Support for this attribute on systems is deprecated as it is meant for MonoBehaviours only. " +
                    $"Please use the [WorldSystemFilter] attribute to specify if you want to ensure your system is added and runs in the Editor ({nameof(WorldSystemFilterFlags.Editor)}) " +
                    $"and/or Player ({nameof(WorldSystemFilterFlags.Default)}) default world. You can specify [WorldSystemFilter({nameof(WorldSystemFilterFlags.Editor)} | {nameof(WorldSystemFilterFlags.Default)}] for both.");

                systemFlags |= WorldSystemFilterFlags.Editor;
            }

            return systemFlags;
        }
#else

        static object CreateSystem(Type systemType)
        {
            int systemIndex = 1; // 0 is reserved for null
            for (; systemIndex < s_SystemTypes.Count; ++systemIndex)
            {
                if (s_SystemTypes[systemIndex] == systemType)
                    break;
            }

            for (int i = 0; i < s_SystemTypeDelegateIndexRanges.Count; ++i)
            {
                if (systemIndex < s_SystemTypeDelegateIndexRanges[i])
                    return s_AssemblyCreateSystemFn[i](systemType);
            }

            throw new ArgumentException("No function was generated for the provided type.");
        }

        internal static Attribute[] GetSystemAttributes(Type system)
        {
            int typeIndexNoFlags = 1; // 0 is reserved for null
            for (; typeIndexNoFlags < s_SystemTypes.Count; ++typeIndexNoFlags)
            {
                if (s_SystemTypes[typeIndexNoFlags] == system)
                    break;
            }

            for (int i = 0; i < s_SystemTypeDelegateIndexRanges.Count; ++i)
            {
                if (typeIndexNoFlags < s_SystemTypeDelegateIndexRanges[i])
                    return s_AssemblyGetSystemAttributesFn[i](system);
            }

            throw new ArgumentException("No function was generated for the provided type.");
        }

        internal static void RegisterAssemblySystemTypes(TypeRegistry typeRegistry)
        {
            // Todo: consolidate this all to a SystemInfo Struct
            for(int i = 0; i < typeRegistry.SystemTypes.Length; ++i)
            {
                var type = typeRegistry.SystemTypes[i];
                var typeName = typeRegistry.SystemTypeNames[i];
                var typeSize = typeRegistry.SystemTypeSizes[i];
                var typeHash = typeRegistry.SystemTypeHashes[i];
                AddSystemTypeToTables(type, typeName, typeSize, typeHash, typeRegistry.SystemTypeFlags[i], typeRegistry.SystemFilterFlags[i]);
            }

            if (typeRegistry.SystemTypes.Length > 0)
            {
                s_SystemTypeDelegateIndexRanges.Add(s_SystemCount);

                s_AssemblyCreateSystemFn.Add(typeRegistry.CreateSystem);
                s_AssemblyGetSystemAttributesFn.Add(typeRegistry.GetSystemAttributes);
            }
        }

#endif
    }
}
