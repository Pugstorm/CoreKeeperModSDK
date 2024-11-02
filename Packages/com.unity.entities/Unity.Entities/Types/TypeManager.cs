using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Burst;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Profiling;
using Unity.Core;
using System.Threading;
using UnityEngine;
using System.Text;

namespace Unity.Entities
{
    /// <summary>
    /// Can exclude components which are unknown at the time of creating the query that have been declared
    /// to write to the same component.
    ///
    /// This allows for extending systems of components safely without editing the previously existing systems.
    ///
    /// The goal is to have a way for systems that expect to transform data from one set of components (inputs) to
    /// another (output[s]) be able to declare that explicit transform, and they exclusively know about one set of
    /// inputs. If there are other inputs that want to write to the same output, the query shouldn't match because it's
    /// a nonsensical/unhandled setup. It's both a way to guard against nonsensical components (having two systems write
    /// to the same output value), and a way to "turn off" existing systems/queries by putting a component with the same
    /// write lock on an entity, letting another system handle it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public class WriteGroupAttribute : Attribute
    {
        /// <summary>
        /// <see cref="WriteGroupAttribute"/>
        /// </summary>
        /// <param name="targetType">Output type the component decorated with this attribute (input) expects to be written by.</param>
        public WriteGroupAttribute(Type targetType)
        {
            TargetType = targetType;
        }

        /// <summary>
        /// Output type the component decorated with this attribute (input) expects to be written by.
        /// </summary>
        public Type TargetType;
    }

    /// <summary>
    /// Prevents a Component Type from being registered in the TypeManager during TypeManager.Initialize().
    /// Types that are not registered will not be recognized by EntityManager.
    /// </summary>
    public class DisableAutoTypeRegistrationAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute that indicates that the component should be removed at the end of each baking iteration.
    /// </summary>
    /// <remarks>
    /// Components decorated with the [TemporaryBakingType] attribute are stripped
    /// after each baking iteration, so you can use them in baking systems
    /// to identify entities that a baker has modified during the current baking iteration.
    ///
    /// Components with the [TemporaryBakingType] attribute are not exported in the runtime data.
    /// </remarks>
    /// <seealso cref="BakingTypeAttribute"/>
    public class TemporaryBakingTypeAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute that indicates that a component should persist in the baking world, but shouldn't be exported with the runtime data.
    /// </summary>
    /// <remarks>
    /// During incremental baking, components with the [BakingType] attribute persist in the baking world.
    /// This allows baking systems to process entities which haven't been modified during the current baking iteration,
    /// but which are dependencies to the final result of the baking.
    ///
    /// Components with the [BakingType] attribute are not exported in the runtime data.
    /// </remarks>
    /// <seealso cref="TemporaryBakingTypeAttribute"/>
    public class BakingTypeAttribute : Attribute
    {
    }

    public class AssumeReadOnlyAttribute : Attribute
    {
    }

    /// <summary>
    /// Provides a unique id for component types as well as quick lookup information about the component type itself.
    /// This value is fully deterministic at runtime but should not be considered deterministic across builds
    /// and thus should not be serialized. For serialization, please prefer <see cref="TypeManager.TypeInfo.StableTypeHash"/> instead.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public struct TypeIndex : IComparable<TypeIndex>, IEquatable<TypeIndex>
    {
        /// <summary>
        /// Raw value used to identify Component types at runtime.
        /// <remarks>
        /// This value should not be serialized as it is not guaranteed to be deterministic across builds (but is during runtime).
        /// For deterministic serialization of types please use <seealso cref="TypeManager.TypeInfo.StableTypeHash"/>
        /// </remarks>
        /// </summary>
        [FieldOffset(0)]
        public int Value;

        /// <summary>
        /// An invalid <seealso cref="Unity.Entities.TypeIndex"/> which does not map to a valid component type.
        /// </summary>
        public static TypeIndex Null { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return  default; } }

        /// <summary>
        /// The component type inherits from <seealso cref="IBufferElementData"/>
        /// </summary>
        public bool IsBuffer { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return   (Value & TypeManager.BufferComponentTypeFlag) != 0; } }

        /// <summary>
        /// The component type inherits from <seealso cref="ICleanupComponentData"/>
        /// </summary>
        public bool IsCleanupComponent { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return  (Value & TypeManager.CleanupComponentTypeFlag) != 0; } }

        /// <summary>
        /// The component type inherits from <seealso cref="ICleanupSharedComponentData"/>
        /// </summary>
        public bool IsCleanupSharedComponent { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return  (Value & TypeManager.CleanupSharedComponentTypeFlag) == TypeManager.CleanupSharedComponentTypeFlag; } }

        /// <summary>
        /// The component type inherits from <seealso cref="ICleanupBufferElementData"/>
        /// </summary>
        public bool IsCleanupBufferComponent { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return (Value & TypeManager.CleanupBufferComponentTypeFlag) == TypeManager.CleanupBufferComponentTypeFlag; } }

        /// <summary>
        /// The component type inherits from <seealso cref="IComponentData"/>
        /// </summary>
        public bool IsComponentType { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return (Value & (TypeManager.SharedComponentTypeFlag | TypeManager.BufferComponentTypeFlag)) == 0; } }

        /// <summary>
        /// The component type inherits from <seealso cref="ISharedComponentData"/>
        /// </summary>
        public bool IsSharedComponentType { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return  (Value & TypeManager.SharedComponentTypeFlag) != 0; } }

        /// <summary>
        /// The component type inherits from <seealso cref="System.IEquatable{T}"/>
        /// </summary>
        public bool IsIEquatable { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return  (Value & TypeManager.IEquatableTypeFlag) != 0; } }

        /// <summary>
        /// The component type <seealso cref="TypeIndex.IsManagedType"/> and inherits from <seealso cref="IComponentData"/>
        /// </summary>
        public bool IsManagedComponent { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return  (Value & (TypeManager.ManagedComponentTypeFlag | TypeManager.ChunkComponentTypeFlag | TypeManager.SharedComponentTypeFlag)) == TypeManager.ManagedComponentTypeFlag; } }

        /// <summary>
        /// The component type <seealso cref="TypeIndex.IsManagedType"/> and inherits from <seealso cref="ISharedComponentData"/>
        /// </summary>
        public bool IsManagedSharedComponent { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return  (Value & TypeManager.ManagedSharedComponentTypeFlag) == TypeManager.ManagedSharedComponentTypeFlag; } }

        /// <summary>
        /// The component type requires managed storage due to being a class type, and/or contains reference types
        /// </summary>
        public bool IsManagedType { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return  (Value & TypeManager.ManagedComponentTypeFlag) != 0; } }

        /// <summary>
        /// The component type allocates 0 bytes in Chunk storage
        /// </summary>
        public bool IsZeroSized { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return  (Value & TypeManager.ZeroSizeInChunkTypeFlag) != 0; } }

        /// <summary>
        /// The component type is used as a chunk component (a component mapped to a Chunk rather than <seealso cref="Entity"/>)
        /// </summary>
        public bool IsChunkComponent { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return (Value & TypeManager.ChunkComponentTypeFlag) != 0; } }

        /// <summary>
        /// The component type inherits from <seealso cref="IEnableableComponent"/>
        /// </summary>
        public bool IsEnableable { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return  (Value & TypeManager.EnableableComponentFlag) != 0; } }

        /// <summary>
        /// The component type inherits from <seealso cref="IRefCounted"/>
        /// </summary>
        public bool IsRefCounted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return  (Value & TypeManager.IRefCountedComponentFlag) != 0; } }

        /// <summary>
        /// The component type contains an <seealso cref="Entity"/> member. Entity members found in nested member types will also cause this property to return true.
        /// </summary>
        public bool HasEntityReferences { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return  (Value & TypeManager.HasNoEntityReferencesFlag) == 0; } }

        /// <summary>
        /// The component type contains a <seealso cref="NativeContainerAttribute"/> decorated member. NativeContainer members found in nested member types will also cause this property to return true.
        /// </summary>
        public bool HasNativeContainer { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return (Value & TypeManager.HasNativeContainerFlag) != 0; } }

        /// <summary>
        /// The component type is appropriate for chunk serialization. Such types are blittable without containing pointer types or have been decorated with <seealso cref="ChunkSerializableAttribute"/>.
        /// </summary>
        public bool IsChunkSerializable { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return (Value & TypeManager.IsNotChunkSerializableTypeFlag) == 0; } }

        /// <summary>
        /// The component type is decorated with the <seealso cref="TemporaryBakingTypeAttribute"/> attribute.
        /// </summary>
        public bool IsTemporaryBakingType { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return  (Value & TypeManager.TemporaryBakingTypeFlag) != 0; } }

        /// <summary>
        /// The component type is decorated with the <seealso cref="BakingTypeAttribute"/> attribute.
        /// </summary>
        public bool IsBakingOnlyType { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return  (Value & TypeManager.BakingOnlyTypeFlag) != 0; } }

        /// <summary>
        /// Zero-based index for the <seealso cref="Unity.Entities.TypeIndex"/> stored in Value (the type index with no flags).
        /// </summary>
        public int Index { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return  Value & TypeManager.ClearFlagsMask; } }


        /// <summary>
        /// Type flags stored in Value
        /// </summary>
        public int Flags { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return  Value & ~TypeManager.ClearFlagsMask; } }

        /// <summary>
        /// Implicit conversion from TypeIndex to an int.
        /// </summary>
        /// <param name="ti">TypeIndex to convert.</param>
        /// <returns>TypeIndex.Value integer representation.</returns>
        public static implicit operator int(TypeIndex ti) => ti.Value;

        /// <summary>
        /// Implicit conversion from an int to a TypeIndex.
        /// </summary>
        /// <param name="value">int to convert</param>
        /// <returns>TypeIndex representation of the int</returns>
        public static implicit operator TypeIndex(int value) => new TypeIndex { Value = value };

        /// <summary>
        /// <seealso cref="Unity.Entities.TypeIndex"/> instances are equal if they refer to the same component type instance.
        /// </summary>
        /// <remarks>
        /// Note that two <seealso cref="Unity.Entities.TypeIndex"/> for the same Component Type may not always be equal. For example, a type inheriting from IComponentData could be used as a
        /// Chunk Component in one Archetype but not in another. If those two TypeIndices were compared they would not match even though they are for the same System.Type.
        /// </remarks>
        /// <param name="lhs"><seealso cref="Unity.Entities.TypeIndex"/> on left side of the equality expression</param>
        /// <param name="rhs"><seealso cref="Unity.Entities.TypeIndex"/> on right side of the equality expression</param>
        /// <returns>True, if both TypeIndices are equal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(TypeIndex lhs, TypeIndex rhs)
        {
            return lhs.Value == rhs.Value;
        }

        /// <summary>
        /// <seealso cref="Unity.Entities.TypeIndex"/> instances are equal if they refer to the same component type instance.
        /// </summary>
        /// <remarks>
        /// Note that two <seealso cref="Unity.Entities.TypeIndex"/> for the same Component Type may not always be equal. For example, a type inheriting from <seealso cref="Unity.Entities.IComponentData"/> could be used as a
        /// Chunk Component in one Archetype but not in another. If those two TypeIndices were compared they would not match even though they are for the same System.Type.
        /// </remarks>
        /// <param name="lhs"><seealso cref="Unity.Entities.TypeIndex"/> on left side of the equality expression.</param>
        /// <param name="rhs"><seealso cref="Unity.Entities.TypeIndex"/> on right side of the equality expression.</param>
        /// <returns>True, if both TypeIndices are equal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(TypeIndex lhs, TypeIndex rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>
        /// Evaluates if one <seealso cref="Unity.Entities.TypeIndex"/> is less than the other.
        /// </summary>
        /// <param name="lhs">The left-hand side</param>
        /// <param name="rhs">The right-hand side</param>
        /// <returns>True if the left-hand side's <see cref="TypeIndex"/> is less than the right-hand side's.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator<(TypeIndex lhs, TypeIndex rhs)
        {
            return lhs.Value < rhs.Value;
        }

        /// <summary>
        /// Evaluates if one <seealso cref="Unity.Entities.TypeIndex"/> is greater than the other.
        /// </summary>
        /// <param name="lhs">The left-hand side</param>
        /// <param name="rhs">The right-hand side</param>
        /// <returns>True if the left-hand side's <see cref="TypeIndex"/> is greater than the right-hand side's.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator>(TypeIndex lhs, TypeIndex rhs)
        {
            return lhs.Value > rhs.Value;
        }

        /// <summary>
        /// Evaluates if one <seealso cref="Unity.Entities.TypeIndex"/> is less than or equal to the other.
        /// </summary>
        /// <param name="lhs">The left-hand side</param>
        /// <param name="rhs">The right-hand side</param>
        /// <returns>True if the left-hand side's <see cref="TypeIndex"/> is less than or equal to the right-hand side's.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator<=(TypeIndex lhs, TypeIndex rhs)
        {
            return lhs.Value <= rhs.Value;
        }

        /// <summary>
        /// Evaluates if one <seealso cref="Unity.Entities.TypeIndex"/> is greater than or equal to the other.
        /// </summary>
        /// <param name="lhs">The left-hand side</param>
        /// <param name="rhs">The right-hand side</param>
        /// <returns>True if the left-hand side's <see cref="TypeIndex"/> is greater than or equal to the right-hand side's.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator>=(TypeIndex lhs, TypeIndex rhs)
        {
            return lhs.Value >= rhs.Value;
        }

        /// <summary>
        /// Compare this <seealso cref="Unity.Entities.TypeIndex"/> against a given one
        /// </summary>
        /// <param name="other">The other TypeIndex to compare to</param>
        /// <returns>Difference between TypeIndex values</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(TypeIndex other)
        {
            return Value - other.Value;
        }

        /// <summary>
        /// <seealso cref="Unity.Entities.TypeIndex"/> instances are equal if they refer to the same component type instance.
        /// </summary>
        /// <remarks>
        /// Note that two <seealso cref="Unity.Entities.TypeIndex"/> for the same Component Type may not always be equal. For example, a type inheriting from <seealso cref="Unity.Entities.IComponentData"/> could be used as a
        /// Chunk Component in one Archetype but not in another. If those two TypeIndices were compared they would not match even though they are for the same System.Type.
        /// </remarks>
        /// <param name="compare">The object to compare to this <seealso cref="Unity.Entities.TypeIndex"/>.</param>
        /// <returns>True, if the compare parameter contains a <seealso cref="Unity.Entities.TypeIndex"/> object equal to this <seealso cref="Unity.Entities.TypeIndex"/> instance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object compare)
        {
            return (compare is TypeIndex compareTypeIndex && Equals(compareTypeIndex));
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
        /// <seealso cref="Unity.Entities.TypeIndex"/> instances are equal if they refer to the same component type instance.
        /// </summary>
        /// <remarks>
        /// Note that two  for the <seealso cref="Unity.Entities.TypeIndex"/> same Component Type may not always be equal. For example, a type inheriting from IComponentData could be used as a
        /// Chunk Component in one Archetype but not in another. If those two TypeIndices were compared they would not match even though they are for the same System.Type.
        /// </remarks>
        /// <param name="typeIndex">The other <seealso cref="Unity.Entities.TypeIndex"/>.</param>
        /// <returns>True, if the <seealso cref="Unity.Entities.TypeIndex"/> instances are equal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(TypeIndex typeIndex)
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
            fs.Append(TypeManager.GetTypeInfo(this).DebugTypeName);
            return fs;
        }
    }

    /// <summary>
    /// The TypeManager registers all Components and Systems available at runtime. Information about components and systems
    /// can be retrieved at runtime via static methods on TypeManager. The Initialize() method must be invoked before the
    /// TypeManager can be used.
    /// </summary>
    public static unsafe partial class TypeManager
    {
        /// <summary>
        /// Attribute to force the <see cref="TypeInfo.MemoryOrdering"/> for a component to a specific value.
        /// </summary>
        [AttributeUsage(AttributeTargets.Struct)]
        public class ForcedMemoryOrderingAttribute : Attribute
        {
            /// <summary>
            /// Force the <see cref="TypeInfo.MemoryOrdering"/> for the component to a specific value.
            /// </summary>
            /// <param name="ordering">Value to force the MemoryOrdering to</param>
            public ForcedMemoryOrderingAttribute(ulong ordering)
            {
                MemoryOrdering = ordering;
            }

            /// <summary>
            /// The forced MemoryOrdering value
            /// </summary>
            public ulong MemoryOrdering;
        }

        /// <summary>
        /// [TypeOverrides] can be applied to a component that is known to never contain Entity and/or Blob references,
        /// in order to reduce time taken during serialization operations.
        /// </summary>
        /// <remarks>
        /// For example, a managed component containing a base class type field can only be determined to have entity or blob
        /// references at runtime since the runtime instance might hold a child type instance which does contain Entity and/or
        /// BlobAssetReferences. As such, serializing operations for managed components needs to walk runtime type instances
        /// which might be unnecessary. Use this attribute to prevent this walking to improve managed component serialization
        /// operations when you know the component type will never contain Entity and/or Blob references.
        /// </remarks>
        [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
        public class TypeOverridesAttribute : Attribute
        {
            /// <summary>
            /// Force the component's <see cref="TypeIndex.HasEntityReferences"/> to be false if set to true here.
            /// Otherwise, the value will only be false if the component actually has no entity references.
            /// </summary>
            public bool  HasNoEntityReferences;
            /// <summary>
            /// Force the component's <see cref="TypeInfo.BlobAssetRefOffsetCount"/> to be zero if set to false.
            /// Otherwise, the value will only be zero if the component actually has a blob reference.
            /// </summary>
            public bool  HasNoBlobReferences;

            /// <summary>
            /// <see cref="TypeOverridesAttribute"/>
            /// </summary>
            /// <param name="hasNoEntityReferences">Set to true to ignore entity references in type.</param>
            /// <param name="hasNoBlobReferences">Set to true to ignore blob asset references in type.</param>
            public TypeOverridesAttribute(bool hasNoEntityReferences, bool hasNoBlobReferences)
            {
                HasNoEntityReferences = hasNoEntityReferences;
                HasNoBlobReferences = hasNoBlobReferences;
            }
        }

        /// <summary>
        /// TypeVersion allows you to override the <see cref="TypeManager.TypeInfo.StableTypeHash"/> for a component type
        /// to a specific value rather than using the default generated hash.
        /// </summary>
        [AttributeUsage(AttributeTargets.Struct)]
        public class TypeVersionAttribute : Attribute
        {
            /// <summary>
            /// Override the StableTypeHash for the component type.
            /// </summary>
            /// <param name="version">The version to override the StableTypeHash.</param>
            public TypeVersionAttribute(int version)
            {
                TypeVersion = version;
            }

            /// <summary>
            /// The StableTypeHash override
            /// </summary>
            public int TypeVersion;
        }

        /// <summary>
        /// Specifies categories of types the TypeManager manages.
        /// </summary>
        public enum TypeCategory : int
        {
            /// <summary>
            /// Implements IComponentData (can be either a struct or a class)
            /// </summary>
            ComponentData,
            /// <summary>
            /// Implements IBufferElementData (struct only)
            /// </summary>
            BufferData,
            /// <summary>
            /// Implement ISharedComponentData (can be either a struct or a class)
            /// </summary>
            ISharedComponentData,
            /// <summary>
            /// Is an Entity
            /// </summary>
            EntityData,
            /// <summary>
            /// Inherits from UnityEngine.Object (class only)
            /// </summary>
            UnityEngineObject,
        }

        /// <summary>
        /// Maximum number of unique component types supported by the <seealso cref="TypeManager"/>/>
        /// </summary>
        public const int MaximumTypesCount = 1 << 13;

        /// <summary>
        /// Bitflag set for component types that do not contain an <seealso cref="Entity"/> member.
        /// Entity members found in nested member types will cause this bitflag to not be set.
        /// </summary>
        public const int HasNoEntityReferencesFlag = 1 << 17; // this flag is inverted to ensure the type id of Entity can still be 1

        /// <summary>
        /// Bitflag set if a component is not appropriate to be included in chunk serialization.
        /// </summary>
        public const int IsNotChunkSerializableTypeFlag = 1 << 18; // this flag is inverted to ensure the type id of Entity can still be 1

        /// <summary>
        /// Bitflag set for component types with NativeContainer data <seealso cref="NativeContainerAttribute"/>.
        /// </summary>
        public const int HasNativeContainerFlag = 1 << 19;

        /// <summary>
        /// Bitflag set for component types decorated with the <seealso cref="BakingTypeAttribute"/> attribute.
        /// </summary>
        public const int BakingOnlyTypeFlag = 1 << 20;

        /// <summary>
        /// Bitflag set for component types decorated with the <seealso cref="TemporaryBakingTypeAttribute"/> attribute.
        /// </summary>
        public const int TemporaryBakingTypeFlag = 1 << 21;

        /// <summary>
        /// Bitflag set for component types inheriting from <seealso cref="IRefCounted"/>.
        /// </summary>
        public const int IRefCountedComponentFlag = 1 << 22;

        /// <summary>
        /// Bitflag set for component types inheriting from <seealso cref="System.IEquatable{T}"/>.
        /// </summary>
        public const int IEquatableTypeFlag = 1 << 23;

        /// <summary>
        /// Bitflag set for component types inheriting from <seealso cref="IEnableableComponent"/>.
        /// </summary>
        public const int EnableableComponentFlag = 1 << 24;

        /// <summary>
        /// Obsolete. Use <see cref="CleanupComponentTypeFlag"/> instead.
        /// </summary>
        [Obsolete("SystemStateTypeFlag has been renamed to CleanupComponentTypeFlag. SystemStateTypeFlag will be removed in a future package release. (UnityUpgradable) -> CleanupComponentTypeFlag", false)]
        public const int SystemStateTypeFlag = 1 << 25;

        /// <summary>
        /// Bitflag set for component types inheriting from <seealso cref="ISystemStateComponentData"/>.
        /// </summary>
        public const int CleanupComponentTypeFlag = 1 << 25;

        /// <summary>
        /// Bitflag set for component types inheriting from <seealso cref="IBufferElementData"/>.
        /// </summary>
        public const int BufferComponentTypeFlag = 1 << 26;

        /// <summary>
        /// Bitflag set for component types inheriting from <seealso cref="ISharedComponentData"/>.
        /// </summary>
        public const int SharedComponentTypeFlag = 1 << 27;

        /// <summary>
        /// Bitflag set for component types requiring managed
        /// storage due to being a class type and/or containing managed references.
        /// </summary>
        public const int ManagedComponentTypeFlag = 1 << 28;

        /// <summary>
        /// Bitflag set for component types converted into Chunk Components.
        /// </summary>
        public const int ChunkComponentTypeFlag = 1 << 29;

        /// <summary>
        /// Bitflag set for component types which allocate 0 bytes in Chunk storage
        /// </summary>
        public const int ZeroSizeInChunkTypeFlag = 1 << 30;

        /// <summary>
        /// Obsolete. Use <see cref="CleanupSharedComponentTypeFlag"/>instead.
        /// </summary>
        [Obsolete("SystemStateSharedComponentTypeFlag has been renamed to CleanupSharedComponentTypeFlag. SystemStateSharedComponentTypeFlag will be removed in a future package release. (UnityUpgradable) -> CleanupSharedComponentTypeFlag", false)]
        public const int SystemStateSharedComponentTypeFlag = CleanupComponentTypeFlag | SharedComponentTypeFlag;

        /// <summary>
        /// Bitflag set for component types inheriting from <seealso cref="ICleanupSharedComponentData"/>.
        /// </summary>
        public const int CleanupSharedComponentTypeFlag = CleanupComponentTypeFlag | SharedComponentTypeFlag;

        /// <summary>
        /// Bitflag set for component types inheriting from <seealso cref="ICleanupBufferElementData"/>.
        /// </summary>
        public const int CleanupBufferComponentTypeFlag = CleanupComponentTypeFlag | BufferComponentTypeFlag;

        /// <summary>
        /// Bitflag set for component types inheriting from <seealso cref="ISharedComponentData"/> and requiring managed
        /// storage due to containing managed references.
        /// </summary>
        public const int ManagedSharedComponentTypeFlag = ManagedComponentTypeFlag | SharedComponentTypeFlag;

        // Update this clear mask if a flag is added or removed.
        /// <summary>
        /// Bit mask to clear all flag bits from a <seealso cref="TypeIndex"/> />
        /// </summary>
        public const int ClearFlagsMask = MaximumTypesCount-1;

        /// <summary>
        /// Maximum number of <seealso cref="Entity"/> instances stored in a given <seealso cref="Chunk"/>/>
        /// </summary>
        public const int MaximumChunkCapacity = 128;

        /// <summary>
        /// Maximum platform alignment supported when aligning component data in a <seealso cref="Chunk"/>/>
        /// </summary>
        public const int MaximumSupportedAlignment = 16;

        /// <summary>
        /// BufferCapacity is by default calculated as DefaultBufferCapacityNumerator / sizeof(BufferElementDataType)
        /// thus for a 1 byte component, the maximum number of elements possible to be stored in chunk memory before
        /// the buffer is allocated separately from chunk data, is DefaultBufferCapacityNumerator elements.
        /// For a 2 byte sized component, (DefaultBufferCapacityNumerator / 2) elements can be stored, etc...
        /// </summary>
        public const int DefaultBufferCapacityNumerator = 128;

        const int                                       kInitialComponentCount = 2; // one for 'null' and one for 'Entity'
        static int                                      s_TypeCount;
        static bool                                     s_Initialized;
        static NativeArray<TypeInfo>                    s_TypeInfos;
        static UnsafeParallelHashMap<ulong, TypeIndex>  s_StableTypeHashToTypeIndex;
        static NativeList<EntityOffsetInfo>             s_EntityOffsetList;
        static NativeList<EntityOffsetInfo>             s_BlobAssetRefOffsetList;
        static NativeList<EntityOffsetInfo>             s_WeakAssetRefOffsetList;
        static NativeList<EntityOffsetInfo>             s_UnityObjectRefOffsetList;
        static NativeList<TypeIndex>                    s_WriteGroupList;
        static NativeList<FastEquality.TypeInfo>        s_FastEqualityTypeInfoList;
        static List<Type>                               s_Types;
        static UnsafeList<UnsafeText>                   s_TypeNames;
        static UnsafeList<ulong>                        s_TypeFullNameHashes;
#if UNITY_DOTSRUNTIME
        // This indirection is only needed for DOTS Runtime due to the way it incrementally fills per-assembly type info
        static NativeArray<int>                         s_DescendantIndex;
#endif
        static NativeArray<int>                         s_DescendantCounts;

        /// <summary>
        /// Used by codegen. Function pointer wrapper for unmanaged <seealso cref="ISharedComponentData"/> method overloads
        /// </summary>
        public struct SharedComponentFnPtrs
        {
            /// <summary>
            /// Used by codegen. FunctionPointer to a shared component's Retain method
            /// </summary>
            public FunctionPointer<IRefCounted.RefCountDelegate> RetainFn;

            /// <summary>
            /// Used by codegen. FunctionPointer to a shared component's Release method
            /// </summary>
            public FunctionPointer<IRefCounted.RefCountDelegate> ReleaseFn;

            /// <summary>
            /// Used by codegen. FunctionPointer to a shared component's Equals method
            /// </summary>
            public FunctionPointer<FastEquality.TypeInfo.CompareEqualDelegate> EqualsFn;

            /// <summary>
            /// Used by codegen. FunctionPointer to a shared component's GetHashCode method
            /// </summary>
            public FunctionPointer<FastEquality.TypeInfo.GetHashCodeDelegate> GetHashCodeFn;
        }

        /// <summary>
        /// Used by codegen. Function pointer wrapper for managed <seealso cref="ISharedComponentData"/> method overloads
        /// </summary>
        public struct ManagedSharedComponentFnPtrs
        {
            /// <summary>
            /// Used by codegen. Delegate to a managed shared component's Retain method
            /// </summary>
            public IRefCounted.RefCountDelegate RetainFn;

            /// <summary>
            /// Used by codegen. Delegate to a managed shared component's Release method
            /// </summary>
            public IRefCounted.RefCountDelegate ReleaseFn;

            /// <summary>
            /// Used by codegen. Delegate to a managed shared component's Equals method
            /// </summary>
            public FastEquality.TypeInfo.CompareEqualDelegate EqualsFn;

            /// <summary>
            /// Used by codegen. Delegate to a managed shared component's GetHashCode method
            /// </summary>
            public FastEquality.TypeInfo.GetHashCodeDelegate GetHashCodeFn;
        }

        static ManagedSharedComponentFnPtrs[]   s_SharedComponentFns_gcDefeat;
        private static UnsafeList<SharedComponentFnPtrs> s_SharedComponent_FunctionPointers;

        /// <summary>
        /// Enumerable list of all component <see cref="TypeInfo"/> values.
        /// </summary>
        public static IEnumerable<TypeInfo> AllTypes { get { return s_TypeInfos.GetSubArray(0, s_TypeCount); } }

        /// <summary>
        /// Returns true if the TypeManager has been initialized, otherwise false.
        /// </summary>
        internal static bool IsInitialized => s_Initialized;

#if !UNITY_DOTSRUNTIME
        static bool                         s_AppDomainUnloadRegistered;
        static Dictionary<Type, TypeIndex>  s_ManagedTypeToIndex;
        static Dictionary<Type, Exception>  s_FailedTypeBuildException;

        /// <summary>
        /// Offset into managed objects to read instance data
        /// </summary>
        public static int                   ObjectOffset;
        internal static Type                UnityEngineObjectType;

        // TODO: this creates a dependency on UnityEngine, but makes splitting code in separate assemblies easier. We need to remove it during the biggere refactor.
        struct ObjectOffsetType
        {
            void* v0;
            // Object layout in CoreCLR is different than in Mono or IL2CPP, as it has only one
            // pointer field as the object header. It is probably a bad idea to depend on VM internal
            // like this at all.
#if !ENABLE_CORECLR
            void* v1;
#endif
        }

        /// <summary>
        /// Register a UnityEngine.Object type with the TypeManager.
        /// </summary>
        /// <param name="type">The type to register</param>
        /// <exception cref="ArgumentException">Thrown if the type does not inherit from UnityEngine.Object.</exception>
        public static void RegisterUnityEngineObjectType(Type type)
        {
            if (type == null || !type.IsClass || type.IsInterface || type.FullName != "UnityEngine.Object")
                throw new ArgumentException($"{type} must be typeof(UnityEngine.Object).");
            UnityEngineObjectType = type;
        }

#endif

        /// <summary>
        /// Array of all component <see cref="TypeInfo"/> values.
        /// </summary>
        /// <returns>Array of TypeInfos for all components</returns>
        public static TypeInfo[] GetAllTypes()
        {
            var res = new TypeInfo[s_TypeCount];

            for (var i = 0; i < s_TypeCount; i++)
            {
                res[i] = s_TypeInfos[i];
            }

            return res;
        }

        /// <summary>
        /// Stores the byte offset into a component where an <see cref="Entity"/> member is stored.
        /// </summary>
        public struct EntityOffsetInfo
        {
            /// <summary>
            /// Byte offset into a component's instance data where an Entity member field begins.
            /// </summary>
            public int Offset;
        }


        /// <summary>
        /// Provides information about component types such as their runtime size, how much space they use in <seealso cref="Chunk"/> storage,
        /// their <see cref="TypeIndex"/>, how many entity references they contain, and more.
        /// </summary>
        [GenerateTestsForBurstCompatibility]
        public readonly struct TypeInfo
        {
            /// <summary>
            /// Used internally to construct a TypeInfo.
            /// </summary>
            /// <param name="typeIndex">TypeIndex to use</param>
            /// <param name="category">TypeCategory the component belongs to</param>
            /// <param name="entityOffsetCount">How many entity references a component contains (recursively)</param>
            /// <param name="entityOffsetStartIndex">Index into the entity offset array where this component's entity offset data begins</param>
            /// <param name="memoryOrdering">The memory order for this component</param>
            /// <param name="stableTypeHash">The stable type hash for this component</param>
            /// <param name="bufferCapacity">Number of elements that can be stored in chunk memory before falling back to a native allocation</param>
            /// <param name="sizeInChunk">Number of bytes the component requires in a chunk</param>
            /// <param name="elementSize">Size of the component. For Buffer components, this is the size of the element type</param>
            /// <param name="alignmentInBytes">Alignment of the component</param>
            /// <param name="maximumChunkCapacity">Max number of instances of this component to allow in a single chunk</param>
            /// <param name="writeGroupCount">Number of write groups targeting this component</param>
            /// <param name="writeGroupStartIndex">Index into the write group array where this component's write group info begins</param>
            /// <param name="hasBlobRefs">Value for if this component has any blob references (in case we force this value true/false)</param>
            /// <param name="blobAssetRefOffsetCount">Number of blob asset references the component really contains</param>
            /// <param name="blobAssetRefOffsetStartIndex">Index into the blob asset reference array where this component's blob asset reference data begins</param>
            /// <param name="weakAssetRefOffsetCount">Number of weak asset references this component contains</param>
            /// <param name="weakAssetRefOffsetStartIndex">Index into the weak asset reference array where this component's weak asset reference data begins</param>
            /// <param name="typeSize">Size of the component type</param>
            /// <param name="bloomFilterMask">The bloom filter mask for this component, used to accelerate "is type T in set of types S" checks. The default value of zero is safe, but ineffective.</param>
            public TypeInfo(int typeIndex, TypeCategory category, int entityOffsetCount, int entityOffsetStartIndex,
                            ulong memoryOrdering, ulong stableTypeHash, int bufferCapacity, int sizeInChunk, int elementSize,
                            int alignmentInBytes, int maximumChunkCapacity, int writeGroupCount, int writeGroupStartIndex,
                            bool hasBlobRefs, int blobAssetRefOffsetCount, int blobAssetRefOffsetStartIndex,
                            int weakAssetRefOffsetCount, int weakAssetRefOffsetStartIndex,
                            int unityObjectRefOffsetCount, int unityObjectRefOffsetStartIndex, int typeSize, ulong bloomFilterMask = 0L)
            {
                TypeIndex = new TypeIndex() { Value = typeIndex };
                Category = category;
                EntityOffsetCount = entityOffsetCount;
                EntityOffsetStartIndex = entityOffsetStartIndex;
                MemoryOrdering = memoryOrdering;
                StableTypeHash = stableTypeHash;
                BloomFilterMask = bloomFilterMask;
                BufferCapacity = bufferCapacity;
                SizeInChunk = sizeInChunk;
                ElementSize = elementSize;
                AlignmentInBytes = alignmentInBytes;
                MaximumChunkCapacity = maximumChunkCapacity;
                WriteGroupCount = writeGroupCount;
                WriteGroupStartIndex = writeGroupStartIndex;
                _HasBlobAssetRefs = hasBlobRefs ? 1 : 0;
                BlobAssetRefOffsetCount = blobAssetRefOffsetCount;
                BlobAssetRefOffsetStartIndex = blobAssetRefOffsetStartIndex;
                WeakAssetRefOffsetCount = weakAssetRefOffsetCount;
                WeakAssetRefOffsetStartIndex = weakAssetRefOffsetStartIndex;
                UnityObjectRefOffsetCount = unityObjectRefOffsetCount;
                UnityObjectRefOffsetStartIndex = unityObjectRefOffsetStartIndex;
                TypeSize = typeSize;
            }

            /// <summary>
            /// <seealso cref="TypeIndex"/>
            /// </summary>
            public   readonly TypeIndex     TypeIndex;

            /// <summary>
            /// The number of bytes used in a <seealso cref="Chunk"/> to store an instance of this component.
            /// </summary>
            /// <remarks>Note that this includes internal capacity and header overhead for buffers. Also, note
            /// that components with no member variables will have a SizeInChunk of 0, but will have a
            /// <seealso cref="TypeSize"/> of GREATER than 0 (since C# does not allow for zero-sized types).</remarks>
            public   readonly int           SizeInChunk;

            /// <summary>
            /// The size of an element store in buffer components. For non-buffer component types, this is the same as <see cref="SizeInChunk"/>
            /// </summary>
            public   readonly int           ElementSize;

            /// <summary>
            /// The maximum number of elements that can be stored in a buffer component instance.
            /// </summary>
            public   readonly int           BufferCapacity;

            /// <summary>
            /// Sort order for component types in <see cref="Chunk"/> storage. By default this is equivalent to <seealso cref="StableTypeHash"/>.
            /// Order is sorted from lowest to highest.
            /// </summary>
            public   readonly ulong         MemoryOrdering;

            /// <summary>
            /// Hash used to uniquely identify a component based on its runtime memory footprint.
            /// </summary>
            /// <remarks>
            /// This value is deterministic across builds provided that the underlying type layout
            /// of the component hasn't changed. For example, renaming a member doesn't affect things,
            /// however changing a member's type causes the parent StableTypeHash to change).
            /// </remarks>
            /// <seealso cref="TypeHash"/>
            public   readonly ulong         StableTypeHash;

            /// <summary>
            /// Bitmask used to accelerate "is type set A a subset of type set B?" queries.
            /// </summary>
            /// <remarks>
            /// This value is a much lower-entropy hash than <see cref="StableTypeHash"/>, specialized for one particular
            /// use case. It is not guaranteed to be unique for all types in the application.
            /// </remarks>
            /// <seealso cref="TypeHash"/>
            public   readonly ulong         BloomFilterMask;

            /// <summary>
            /// The alignment requirement for the component. For buffer types, this is the alignment requirement of the element type.
            /// </summary>
            public   readonly int           AlignmentInBytes;

            /// <summary>
            /// <seealso cref="TypeCategory"/>
            /// </summary>
            public   readonly TypeCategory  Category;

            /// <summary>
            /// Number of <seealso cref="Entity"/> references this component can store.
            /// </summary>
            public   readonly int           EntityOffsetCount;

            internal readonly int           EntityOffsetStartIndex;
            private  readonly int           _HasBlobAssetRefs;

            /// <summary>
            /// Number of <seealso cref="BlobAssetReference{T}"/>s this component can store.
            /// </summary>
            public   readonly int           BlobAssetRefOffsetCount;
            internal readonly int           BlobAssetRefOffsetStartIndex;

            /// <summary>
            /// Number of <seealso cref="WeakReference{T}"/>s this component can store.
            /// </summary>
            public   readonly int           WeakAssetRefOffsetCount;
            internal readonly int           WeakAssetRefOffsetStartIndex;

            public   readonly int           UnityObjectRefOffsetCount;
            internal readonly int           UnityObjectRefOffsetStartIndex;

            /// <summary>
            /// Number of components which specify this component as the target type in a <seealso cref="WriteGroupAttribute"/>.
            /// </summary>
            public   readonly int           WriteGroupCount;
            internal readonly int           WriteGroupStartIndex;

            /// <summary>
            /// Maximum number of instances of this component allowed to be stored in a <seealso cref="Chunk"/>.
            /// </summary>
            public   readonly int           MaximumChunkCapacity;

            /// <summary>
            /// Blittable size of the component type.
            /// </summary>
            public   readonly int           TypeSize;

            /// <summary>
            /// Alignment of this type in a chunk.  Normally the same as AlignmentInBytes, but that
            /// might be less than this value for buffer elements, whereas the buffer itself must be aligned to <seealso cref="MaximumSupportedAlignment"/>.
            /// </summary>
            public int  AlignmentInChunkInBytes
            {
                get
                {
                    if (Category == TypeCategory.BufferData)
                        return MaximumSupportedAlignment;
                    return AlignmentInBytes;
                }
            }

            /// <summary>
            /// <seealso cref="TypeIndex.IsTemporaryBakingType"/>
            /// </summary>
            public bool TemporaryBakingType => IsTemporaryBakingType(TypeIndex);

            /// <summary>
            /// <seealso cref="TypeIndex.IsBakingOnlyType"/>
            /// </summary>
            public bool BakingOnlyType => IsBakingOnlyType(TypeIndex);

            /// <summary>
            /// <seealso cref="TypeIndex.IsEnableable"/>
            /// </summary>
            public bool EnableableType => IsEnableableType(TypeIndex);

            /// <summary>
            /// Returns true if the component does not require space in <seealso cref="Chunk"/> memory
            /// </summary>
            public bool IsZeroSized => SizeInChunk == 0;

            /// <summary>
            /// Returns true if a component with a <seealso cref="WriteGroupAttribute"/> specifies this component as it's targetType
            /// </summary>
            public bool HasWriteGroups => WriteGroupCount > 0;

            /// <summary>
            /// For struct IComponentData, a value of true gurantees that there are <seealso cref="BlobAssetReference{T}"/> fields in this component.
            /// For class based IComponentData, a value of true means it is possible, but not guaranteed, that there are blob asset references. (Polymorphic <seealso cref="BlobAssetReference{T}"/> members can not be proven statically)
            /// </summary>
            public bool HasBlobAssetRefs => _HasBlobAssetRefs != 0;

            /// <summary>
            /// For struct IComponentData, a value of true gurantees that there are <seealso cref="WeakReference{T}"/> fields in this component.
            /// For class based IComponentData, a value of true means it is possible, but not guaranteed, that there are WeakReferences. (Polymorphic <seealso cref="WeakReference{T}"/> members can not be proven statically)
            /// </summary>
            public bool HasWeakAssetRefs => WeakAssetRefOffsetCount != 0;

            /// <summary>
            /// For struct IComponentData, a value of true gurantees that there are <seealso cref="WeakReference{T}"/> fields in this component.
            /// For class based IComponentData, a value of true means it is possible, but not guaranteed, that there are WeakReferences. (Polymorphic <seealso cref="WeakReference{T}"/> members can not be proven statically)
            /// </summary>
            public bool HasUnityObjectRefs => UnityObjectRefOffsetCount != 0;

            /// <summary>
            /// Returns the System.Type for the component this <seealso cref="TypeInfo"/> is describing.
            /// </summary>
            /// <remarks>Unlike other properties, this property performs a lookup in order to not store managed data in <seealso cref="TypeInfo"/> as that would prevent Burst compilation.</remarks>
            [ExcludeFromBurstCompatTesting("Returns managed Type")]
            public Type Type => TypeManager.GetType(TypeIndex);

            /// <summary>
            /// Provides a HPC# / Burst compatible name of the component type when building with DEBUG defined. Otherwise the name is empty.
            /// </summary>
            [GenerateTestsForBurstCompatibility]
            public NativeText.ReadOnly DebugTypeName
            {
                get
                {
                    var pUnsafeText = GetTypeName(TypeIndex);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    var ro = new NativeText.ReadOnly(pUnsafeText, SharedSafetyHandle.Ref.Data);
#else
                    var ro = new NativeText.ReadOnly(pUnsafeText);
#endif
                    return ro;
                }
            }
        }

        internal static EntityOffsetInfo* GetEntityOffsetsPointer()
        {
            return (EntityOffsetInfo*) SharedEntityOffsetInfos.Ref.Data;
        }

        internal static EntityOffsetInfo* GetEntityOffsets(in TypeInfo typeInfo)
        {
            return GetEntityOffsetsPointer() + typeInfo.EntityOffsetStartIndex;
        }

        /// <summary>
        /// Gets a pointer to entity offsets for a given type index.
        /// </summary>
        /// <remarks>
        /// This always returns a pointer even if the given type has
        /// no entity offsets. Always check and iterate over the returned pointer using the
        /// returned count.
        /// </remarks>
        /// <param name="typeIndex">The TypeIndex to review.</param>
        /// <param name="count"></param>
        /// <returns>Returns a pointer to the entity offsets.</returns>
        [GenerateTestsForBurstCompatibility]
        public static EntityOffsetInfo* GetEntityOffsets(TypeIndex typeIndex, out int count)
        {
            var typeInfo = GetTypeInfoPointer() + typeIndex.Index;
            count = typeInfo->EntityOffsetCount;
            return GetEntityOffsets(*typeInfo);
        }

        internal static EntityOffsetInfo* GetBlobAssetRefOffsetsPointer()
        {
            return (EntityOffsetInfo*) SharedBlobAssetRefOffsets.Ref.Data;
        }

        // Note this function will always return a pointer even if the given type has
        // no BlobAssetReference offsets. Always check/iterate the returned pointer
        // against the TypeInfo.BlobAssetReferenceCount
        internal static EntityOffsetInfo* GetBlobAssetRefOffsets(in TypeInfo typeInfo)
        {
            return GetBlobAssetRefOffsetsPointer() + typeInfo.BlobAssetRefOffsetStartIndex;
        }

        internal static EntityOffsetInfo* GetWeakAssetRefOffsetsPointer()
        {
            return (EntityOffsetInfo*)SharedWeakAssetRefOffsets.Ref.Data;
        }

        internal static EntityOffsetInfo* GetWeakAssetRefOffsets(in TypeInfo typeInfo)
        {
            return GetWeakAssetRefOffsetsPointer() + typeInfo.WeakAssetRefOffsetStartIndex;
        }

        internal static EntityOffsetInfo* GetUnityObjectRefOffsetsPointer()
        {
            return (EntityOffsetInfo*)SharedUnityObjectRefOffsets.Ref.Data;
        }

        internal static EntityOffsetInfo* GetUnityObjectRefOffsets(in TypeInfo typeInfo)
        {
            return GetUnityObjectRefOffsetsPointer() + typeInfo.UnityObjectRefOffsetStartIndex;
        }

        internal static UnsafeParallelHashMapData* GetStableTypeHashMapPointer()
        {
            return (UnsafeParallelHashMapData*)SharedStableTypeHashes.Ref.Data;
        }

        internal static TypeIndex* GetWriteGroupsPointer()
        {
            return (TypeIndex*)SharedWriteGroups.Ref.Data;
        }

        /// <summary>
        /// Retrieves a pointer to an array of WriteGroups for the provided TypeInfo.
        /// </summary>
        /// <param name="typeInfo">TypeInfo for the component with a WriteGroup attribute</param>
        /// <returns>Returns a pointer to an array of WriteGroups for the provided TypeInfo.</returns>
        [GenerateTestsForBurstCompatibility]
        public  static TypeIndex* GetWriteGroups(in TypeInfo typeInfo)
        {
            if (typeInfo.WriteGroupCount == 0)
                return null;

            return GetWriteGroupsPointer() + typeInfo.WriteGroupStartIndex;
        }

        internal static UnsafeText* GetTypeNamesPointer()
        {
            return (UnsafeText*)SharedTypeNames.Ref.Data;
        }

        internal static UnsafeText* GetTypeName(TypeIndex typeIndex)
        {
#if DEBUG
            return GetTypeNamesPointer() + typeIndex.Index;
#else
            return GetTypeNamesPointer();
#endif
        }

        internal static FixedString128Bytes GetTypeNameFixed(TypeIndex typeIndex)
        {
            return new FixedString128Bytes(*GetTypeName(typeIndex));
        }

        /// <summary>
        /// Retrieve the TypeInfo for the <see cref="TypeIndex"/>.
        /// </summary>
        /// <param name="typeIndex">TypeIndex to review the TypeInfo for</param>
        /// <returns>Returns the TypeInfo for the component corresponding to the TypeInfo.</returns>
        [GenerateTestsForBurstCompatibility]
        public static ref readonly TypeInfo GetTypeInfo(TypeIndex typeIndex)
        {
            return ref GetTypeInfoPointer()[typeIndex.Index];
        }

        /// <summary>
        /// Retrieve the TypeInfo for the component T.
        /// </summary>
        /// <typeparam name="T">Component type to get TypeInfo fo</typeparam>
        /// <returns>The TypeInfo for the component corresponding to type T.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new Type[] { typeof(Entity) })]
        public static ref readonly TypeInfo GetTypeInfo<T>()
        {
            return ref GetTypeInfoPointer()[GetTypeIndex<T>().Index];
        }

        internal static TypeInfo * GetTypeInfoPointer()
        {
            return (TypeInfo*) SharedTypeInfos.Ref.Data;
        }

        internal static FastEquality.TypeInfo* GetFastEqualityTypeInfoPointer()
        {
            return (FastEquality.TypeInfo*) SharedFastEqualityTypeInfo.Ref.Data;
        }

        internal static int* GetDescendantCountPointer()
        {
            return (int*) SharedDescendantCounts.Ref.Data;
        }

        internal static int GetDescendantIndex(TypeIndex typeIndex)
        {
            var descendantIndex = typeIndex.Index;
#if UNITY_DOTSRUNTIME
            descendantIndex = GetDescendantIndexPointer()[descendantIndex];
#endif
            return descendantIndex;
        }

#if UNITY_DOTSRUNTIME
        internal static int* GetDescendantIndexPointer()
        {
            return (int*) SharedDescendantIndices.Ref.Data;
        }
#endif

        internal static ulong* GetFullTypeNameHashesPointer()
        {
            return (ulong*) SharedTypeFullNameHashes.Ref.Data;
        }

        /// <summary>
        /// Gets the System.Type for the component represented by <see cref="TypeIndex"/>.
        /// </summary>
        /// <param name="typeIndex">TypeIndex for a component type</param>
        /// <returns>The System.Type for the component.</returns>
        public static Type GetType(TypeIndex typeIndex)
        {
            return s_Types[typeIndex.Index];
        }

        /// <summary>
        /// Gets the total number of components managed by the <seealso cref="TypeManager"/>.
        /// </summary>
        /// <returns>Returns the total number of components managed by the <seealso cref="TypeManager"/>.</returns>
        public static int GetTypeCount()
        {
            return s_TypeCount;
        }

        /// <summary>
        /// <seealso cref="TypeIndex.IsBuffer"/>
        /// </summary>
        /// <param name="typeIndex">TypeIndex for a component</param>
        /// <returns>Returns if the component is a buffer component</returns>
        public static bool IsBuffer(TypeIndex typeIndex) => (typeIndex.Value & BufferComponentTypeFlag) != 0;

        /// <summary>
        /// Obsolete. Use <see cref="TypeIndex.IsCleanupComponent"/> instead.
        /// </summary>
        /// <param name="typeIndex">TypeIndex for a component</param>
        /// <returns>Returns if the component is a cleanup component</returns>
        [Obsolete("IsSystemStateComponent() has been renamed to IsCleanupComponent(). IsSystemStateComponent() will be removed in a future package release. (UnityUpgradable) -> IsCleanupComponent(*)", false)]
        public static bool IsSystemStateComponent(TypeIndex typeIndex) => typeIndex.IsCleanupComponent;

        /// <summary>
        /// <seealso cref="TypeIndex.IsCleanupComponent"/>
        /// </summary>
        /// <param name="typeIndex">TypeIndex for a component</param>
        /// <returns>Returns if the component is a cleanup component</returns>
        public static bool IsCleanupComponent(TypeIndex typeIndex) => typeIndex.IsCleanupComponent;

        /// <summary>
        /// Obsolete. Use <see cref="TypeIndex.IsCleanupSharedComponent"/> instead.
        /// </summary>
        /// <param name="typeIndex">TypeIndex for a component</param>
        /// <returns>Returns if the component is a cleanup shared component</returns>
        [Obsolete("IsSystemStateSharedComponent() has been renamed to IsCleanupSharedComponent(). IsSystemStateSharedComponent() will be removed in a future package release. (UnityUpgradable) -> IsCleanupSharedComponent(*)", false)]
        public static bool IsSystemStateSharedComponent(TypeIndex typeIndex) => typeIndex.IsCleanupSharedComponent;

        /// <summary>
        /// <seealso cref="TypeIndex.IsCleanupSharedComponent"/>
        /// </summary>
        /// <param name="typeIndex">TypeIndex for a component</param>
        /// <returns>Returns if the component is a cleanup shared component</returns>
        public static bool IsCleanupSharedComponent(TypeIndex typeIndex) => typeIndex.IsCleanupSharedComponent;

        /// <summary>
        /// <seealso cref="TypeIndex.IsSharedComponentType"/>
        /// </summary>
        /// <param name="typeIndex">TypeIndex for a component</param>
        /// <returns>If the component is a shared component</returns>
        public static bool IsSharedComponentType(TypeIndex typeIndex) => typeIndex.IsSharedComponentType;

        /// <summary>
        /// <seealso cref="TypeIndex.IsIEquatable"/>
        /// </summary>
        /// <param name="typeIndex">TypeIndex for a component</param>
        /// <returns>Returns if the component inherits from <seealso cref="IEquatable{T}"/></returns>
        public static bool IsIEquatable(TypeIndex typeIndex) => typeIndex.IsIEquatable;

        /// <summary>
        /// <seealso cref="TypeIndex.IsManagedComponent"/>
        /// </summary>
        /// <param name="typeIndex">TypeIndex for a component</param>
        /// <returns>Returns if the component type <seealso cref="TypeIndex.IsManagedType"/> and inherits from <seealso cref="IComponentData"/></returns>
        public static bool IsManagedComponent(TypeIndex typeIndex) => typeIndex.IsManagedComponent;

        /// <summary>
        /// <seealso cref="TypeIndex.IsManagedSharedComponent"/>
        /// </summary>
        /// <param name="typeIndex">TypeIndex for a component</param>
        /// <returns>Returns if the component type <seealso cref="TypeIndex.IsManagedType"/> and inherits from <seealso cref="ISharedComponentData"/></returns>
        public static bool IsManagedSharedComponent(TypeIndex typeIndex) => typeIndex.IsManagedSharedComponent;

        /// <summary>
        /// <seealso cref="TypeIndex.IsManagedType"/>
        /// </summary>
        /// <param name="typeIndex">TypeIndex for a component</param>
        /// <returns>Returns if the component type requires managed storage due to being a class type, and/or contains reference types</returns>
        public static bool IsManagedType(TypeIndex typeIndex) => typeIndex.IsManagedType;

        /// <summary>
        /// <seealso cref="TypeIndex.IsZeroSized"/>
        /// </summary>
        /// <param name="typeIndex">TypeIndex for a component</param>
        /// <returns>Returns if the component type does not require space in <seealso cref="Chunk"/> memory.</returns>
        public static bool IsZeroSized(TypeIndex typeIndex) => typeIndex.IsZeroSized;

        /// <summary>
        /// <seealso cref="TypeIndex.IsChunkComponent"/>
        /// </summary>
        /// <param name="typeIndex">TypeIndex for a component</param>
        /// <returns>Returns if the component type is used as a chunk component (a component mapped to a Chunk rather than <seealso cref="Entity"/>)</returns>
        public static bool IsChunkComponent(TypeIndex typeIndex) => typeIndex.IsChunkComponent;

        /// <summary>
        /// <seealso cref="TypeIndex.IsEnableable"/>
        /// </summary>
        /// <param name="typeIndex">TypeIndex for a component</param>
        /// <returns>Returns if the component type inherits from <seealso cref="IEnableableComponent"/></returns>
        public static bool IsEnableable(TypeIndex typeIndex) => typeIndex.IsEnableable;

        /// <summary>
        /// <seealso cref="TypeIndex.HasEntityReferences"/>
        /// </summary>
        /// <param name="typeIndex">TypeIndex for a component</param>
        /// <returns>Returns if the component type contains any member fields that are of type <seealso cref="Entity"/></returns>
        public static bool HasEntityReferences(TypeIndex typeIndex) => typeIndex.HasEntityReferences;

        /// <summary>
        /// <seealso cref="TypeIndex.IsTemporaryBakingType"/>
        /// </summary>
        /// <param name="typeIndex">TypeIndex for a component</param>
        /// <returns>Returns if the component type is decorated with the <seealso cref="TemporaryBakingTypeAttribute"/> attribute.</returns>
        public static bool IsTemporaryBakingType(TypeIndex typeIndex) => typeIndex.IsTemporaryBakingType;

        /// <summary>
        /// <seealso cref="TypeIndex.IsEnableable"/>
        /// </summary>
        /// <param name="typeIndex">TypeIndex for a component</param>
        /// <returns>Returns if the component type is decorated with the <seealso cref="EnableableComponentFlag"/> attribute.</returns>
        public static bool IsEnableableType(TypeIndex typeIndex) => typeIndex.IsEnableable;

        /// <summary>
        /// <seealso cref="TypeIndex.IsBakingOnlyType"/>
        /// </summary>
        /// <param name="typeIndex">TypeIndex for a component</param>
        /// <returns>Returns if the component type is decorated with the <seealso cref="BakingTypeAttribute"/> attribute.</returns>
        public static bool IsBakingOnlyType(TypeIndex typeIndex) => typeIndex.IsBakingOnlyType;

        /// <summary>
        /// Creates a new TypeIndex that allows the passed in 'typeindex' to be used as a chunk component.
        /// </summary>
        /// <param name="typeIndex">The TypeIndex to use as a chunk component.</param>
        /// <returns>Returns the new TypeIndex.</returns>
        public static TypeIndex MakeChunkComponentTypeIndex(TypeIndex typeIndex) => new TypeIndex{ Value = typeIndex.Value | ChunkComponentTypeFlag | ZeroSizeInChunkTypeFlag };

        /// <summary>
        /// Used to determine if a component inherits from another component type
        /// </summary>
        /// <param name="typeIndex">Child type to test if it inherits from baseTypeIndex</param>
        /// <param name="baseTypeIndex">Parent type typeIndex may have inherited from</param>
        /// <returns>Returns if child is a subclass of the parent component type</returns>
        [GenerateTestsForBurstCompatibility]
        public static bool IsDescendantOf(TypeIndex typeIndex, TypeIndex baseTypeIndex)
        {
            var descendantIndex = GetDescendantIndex(typeIndex);
            var baseDescendantIndex = GetDescendantIndex(baseTypeIndex);
            var baseTypeEnd = baseDescendantIndex + GetDescendantCountPointer()[baseDescendantIndex];
            return descendantIndex >= baseDescendantIndex && descendantIndex <= baseTypeEnd;
        }

        /// <summary>
        /// Returns how many component types are known to the <see cref="TypeManager"/> that inherit from a given component type
        /// </summary>
        /// <param name="typeIndex">Parent type</param>
        /// <returns>Returns count of component types inheriting from typeIndex's component type</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new Type[] { typeof(Entity) })]
        public static int GetDescendantCount(TypeIndex typeIndex)
        {
            var descendantIndex = GetDescendantIndex(typeIndex);
            return GetDescendantCountPointer()[descendantIndex];
        }

        /// <summary>
        /// Returns if any component inherits from the provided <seealso cref="TypeIndex"/>
        /// </summary>
        /// <param name="baseTypeIndex">Parent type</param>
        /// <returns>Returns if any component inherits from baseTypeIndex</returns>
        [GenerateTestsForBurstCompatibility]
        public static bool HasDescendants(TypeIndex baseTypeIndex)
        {
            var descendantIndex = GetDescendantIndex(baseTypeIndex);
            return GetDescendantCountPointer()[descendantIndex] > 0;
        }

        /// <summary>
        /// Calculate a hash for a component's full type name. Used at TypeManager initialization internally.
        /// </summary>
        /// <param name="fullName">Type.FullName of a component type</param>
        /// <returns>Hash of component type name</returns>
        public static ulong CalculateFullNameHash(string fullName)
        {
            return TypeHash.FNV1A64(fullName);
        }

        /// <summary>
        /// Retrieves a pre-calculated hash for a component's full type name.
        /// </summary>
        /// <param name="typeIndex">TypeIndex for the component to lookup</param>
        /// <returns>Hash of component type name</returns>
        [GenerateTestsForBurstCompatibility]
        public static ulong GetFullNameHash(TypeIndex typeIndex)
        {
            return GetFullTypeNameHashesPointer()[typeIndex.Index];
        }

        [ExcludeFromBurstCompatTesting("Takes a managed Type")]
        private static void AddTypeInfoToTables(Type type, TypeInfo typeInfo, string typeName, int descendantCount)
        {
            if (!s_StableTypeHashToTypeIndex.TryAdd(typeInfo.StableTypeHash, typeInfo.TypeIndex))
            {
                var previousTypeIndexNoFlags = s_StableTypeHashToTypeIndex[typeInfo.StableTypeHash].Index;
                throw new ArgumentException($"{type} and {s_Types[previousTypeIndexNoFlags]} have a conflict in the stable type hash. Use the [TypeVersion(...)] attribute to force a different stable type hash for one of them.");
            }

            s_TypeInfos[typeInfo.TypeIndex.Index] = typeInfo;
            s_DescendantCounts[typeInfo.TypeIndex.Index] = descendantCount;
            s_Types.Add(type);

            var unsafeName = new UnsafeText(Encoding.UTF8.GetByteCount(typeName), Allocator.Persistent);
            unsafeName.CopyFrom(typeName);
            s_TypeNames.Add(unsafeName);
            s_TypeFullNameHashes.Add(TypeHash.FNV1A64(typeName));

            Assert.AreEqual(s_TypeCount, typeInfo.TypeIndex.Index);
            s_TypeCount++;

#if !UNITY_DOTSRUNTIME
            if (type != null)
            {
                SharedTypeIndex.Get(type) = typeInfo.TypeIndex;
                s_ManagedTypeToIndex.Add(type, typeInfo.TypeIndex);
            }
#endif
        }

        /// <summary>
        /// Initializes the TypeManager with all ECS type information. May be called multiple times; only the first call
        /// will do any work. Always must be called from the main thread.
        /// </summary>
        public static void Initialize()
        {
            if (s_Initialized)
                return;

#if UNITY_EDITOR
            // Synchronous Burst compilation takes forever, so in the editor don't waste time for
            // users who are trying to iterate and turn the option off while we generate type info
            bool originalBurstCompileSyncValue = BurstCompiler.Options.EnableBurstCompileSynchronously;
            BurstCompiler.Options.EnableBurstCompileSynchronously = false;

            var stopWatch = new Stopwatch();
            stopWatch.Start();
#endif

            s_Initialized = true;
            try
            {
#if UNITY_EDITOR
                if (!UnityEditorInternal.InternalEditorUtility.CurrentThreadIsMainThread())
                    throw new InvalidOperationException("Must be called from the main thread");
#endif

#if !UNITY_DOTSRUNTIME
                if (!s_AppDomainUnloadRegistered)
                {
                    // important: this will always be called from a special unload thread (main thread will be blocking on this)
                    AppDomain.CurrentDomain.DomainUnload += (_, __) =>
                    {
                        if (s_Initialized)
                            Shutdown();
                    };

                    // There is no domain unload in player builds, so we must be sure to shutdown when the process exits.
                    AppDomain.CurrentDomain.ProcessExit += (_, __) => { Shutdown(); };
                    s_AppDomainUnloadRegistered = true;
                }

                ObjectOffset = UnsafeUtility.SizeOf<ObjectOffsetType>();
                s_ManagedTypeToIndex = new Dictionary<Type, TypeIndex>(1000);
                s_FailedTypeBuildException = new Dictionary<Type, Exception>();
#endif

                s_TypeCount = 0;
                s_TypeInfos = new NativeArray<TypeInfo>(MaximumTypesCount, Allocator.Persistent);
#if UNITY_DOTSRUNTIME
            s_DescendantIndex = new NativeArray<int>(MaximumTypesCount, Allocator.Persistent);
#endif
                s_DescendantCounts = new NativeArray<int>(MaximumTypesCount, Allocator.Persistent);
                s_StableTypeHashToTypeIndex = new UnsafeParallelHashMap<ulong, TypeIndex>(MaximumTypesCount, Allocator.Persistent);
                s_EntityOffsetList = new NativeList<EntityOffsetInfo>(Allocator.Persistent);
                s_BlobAssetRefOffsetList = new NativeList<EntityOffsetInfo>(Allocator.Persistent);
                s_WeakAssetRefOffsetList = new NativeList<EntityOffsetInfo>(Allocator.Persistent);
                s_UnityObjectRefOffsetList = new NativeList<EntityOffsetInfo>(Allocator.Persistent);
                s_WriteGroupList = new NativeList<TypeIndex>(Allocator.Persistent);
                s_FastEqualityTypeInfoList = new NativeList<FastEquality.TypeInfo>(Allocator.Persistent);
                s_Types = new List<Type>();
                s_TypeNames = new UnsafeList<UnsafeText>(MaximumTypesCount, Allocator.Persistent);
                s_TypeFullNameHashes = new UnsafeList<ulong>(MaximumTypesCount, Allocator.Persistent);
                s_SharedComponent_FunctionPointers = new UnsafeList<SharedComponentFnPtrs>(MaximumTypesCount, Allocator.Persistent);
                s_SharedComponentFns_gcDefeat = new ManagedSharedComponentFnPtrs[MaximumTypesCount];

                FastEquality.Initialize();
                InitializeSystemsState();

                // There are some types that must be registered first such as a null component and Entity
                RegisterSpecialComponents();
                RegisterSpecialSystems();
                Assert.IsTrue(kInitialComponentCount == s_TypeCount);

#if !UNITY_DOTSRUNTIME
                InitializeAllComponentTypes();
#else
                // Registers all types and their static info from the static type registry
                RegisterStaticAssemblyTypes();
#endif
                InitializeAllSystemTypes();


                // Must occur after we've constructed s_TypeInfos
                InitializeSharedStatics();

                EntityNameStorage.Initialize();

#if !UNITY_DOTSRUNTIME
                InitializeAspects();
#endif
            }
            catch
            {
                Shutdown();
                throw;
            }
#if UNITY_EDITOR
            finally
            {
                // Save the time since profiler might not catch the first frame.
                stopWatch.Stop();
                Console.WriteLine($"TypeManager.Initialize took: {stopWatch.ElapsedMilliseconds}ms");

                BurstCompiler.Options.EnableBurstCompileSynchronously = originalBurstCompileSyncValue;
            }
#endif
        }

        static void InitializeSharedStatics()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            SharedSafetyHandle.Ref.Data = AtomicSafetyHandle.Create();
#endif
            Shared_SharedComponentData_FnPtrs.Ref.Data = new IntPtr(s_SharedComponent_FunctionPointers.Ptr);
            SharedTypeInfos.Ref.Data = new IntPtr(s_TypeInfos.GetUnsafePtr());
#if UNITY_DOTSRUNTIME
            SharedDescendantIndices.Ref.Data = new IntPtr(s_DescendantIndex.GetUnsafePtr());
#endif
            SharedDescendantCounts.Ref.Data = new IntPtr(s_DescendantCounts.GetUnsafePtr());
            SharedEntityOffsetInfos.Ref.Data = new IntPtr(s_EntityOffsetList.GetUnsafePtr());
            SharedBlobAssetRefOffsets.Ref.Data = new IntPtr(s_BlobAssetRefOffsetList.GetUnsafePtr());
            SharedWeakAssetRefOffsets.Ref.Data = new IntPtr(s_WeakAssetRefOffsetList.GetUnsafePtr());
            SharedUnityObjectRefOffsets.Ref.Data = new IntPtr(s_UnityObjectRefOffsetList.GetUnsafePtr());
            SharedStableTypeHashes.Ref.Data = new IntPtr(s_StableTypeHashToTypeIndex.m_Buffer);
            SharedWriteGroups.Ref.Data = new IntPtr(s_WriteGroupList.GetUnsafePtr());
            SharedFastEqualityTypeInfo.Ref.Data = new IntPtr(s_FastEqualityTypeInfoList.GetUnsafePtr());
            SharedTypeNames.Ref.Data = new IntPtr(s_TypeNames.Ptr);
            SharedTypeFullNameHashes.Ref.Data = new IntPtr(s_TypeFullNameHashes.Ptr);
            InitializeSystemSharedStatics();
        }

        static void ShutdownSharedStatics()
        {
            SharedTypeInfos.Ref.Data = default;
#if UNITY_DOTSRUNTIME
            SharedDescendantIndices.Ref.Data = default;
#endif
            SharedDescendantCounts.Ref.Data = default;
            SharedEntityOffsetInfos.Ref.Data = default;
            SharedBlobAssetRefOffsets.Ref.Data = default;
            SharedWeakAssetRefOffsets.Ref.Data = default;
            SharedStableTypeHashes.Ref.Data = default;
            SharedWriteGroups.Ref.Data = default;
            SharedFastEqualityTypeInfo.Ref.Data = default;
            SharedTypeNames.Ref.Data = default;
            SharedTypeFullNameHashes.Ref.Data = default;
            SharedSystemTypeNames.Ref.Data = default;
            SharedSystemAttributes.Ref.Data = default;
            SharedSystemCount.Ref.Data = default;
            SharedSystemTypeHashes.Ref.Data = default;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // If the TypeManager failed to initialize, this may not have been set
#if UNITY_DOTSRUNTIME
            if(SharedSafetyHandle.Ref.Data.nodePtr != default)
#else
            if(SharedSafetyHandle.Ref.Data.versionNode != default)
#endif
                AtomicSafetyHandle.Release(SharedSafetyHandle.Ref.Data);
#endif
                Shared_SharedComponentData_FnPtrs.Ref.Data = default;
        }

        static void RegisterSpecialComponents()
        {
            // Push Null TypeInfo -- index 0 is reserved for null/invalid in all arrays index by TypeIndex.Index
            AddFastEqualityInfo(null);
            AddTypeInfoToTables(null,
                new TypeInfo(0, TypeCategory.ComponentData, 0, -1,
                    0L, 0L,  -1, 0, 0, 0,
                    TypeManager.MaximumChunkCapacity, 0, -1, false, 0,
                    -1, 0, -1, 0, -1, 0, 0L),
                "null", 0);

            // Push Entity TypeInfo
            var entityTypeIndex = new TypeIndex { Value = 1 };
            ulong entityStableTypeHash;
#if !UNITY_DOTSRUNTIME
            entityStableTypeHash = TypeHash.CalculateStableTypeHash(typeof(Entity));
            AddFastEqualityInfo(typeof(Entity));
#else
            entityStableTypeHash = GetEntityStableTypeHash();
#endif

            // Entity is special and is treated as having an entity offset at 0 (itself)
            s_EntityOffsetList.Add(new EntityOffsetInfo() { Offset = 0 });
            AddTypeInfoToTables(typeof(Entity),
                new TypeInfo(1, TypeCategory.EntityData, entityTypeIndex.Value, 0,
                    0, entityStableTypeHash, -1, UnsafeUtility.SizeOf<Entity>(),
                    UnsafeUtility.SizeOf<Entity>(), CalculateAlignmentInChunk(sizeof(Entity)),
                    TypeManager.MaximumChunkCapacity, 0, -1, false, 0,
                    -1, 0, -1, 0, -1, UnsafeUtility.SizeOf<Entity>(),
                    bloomFilterMask:0L),
                "Unity.Entities.Entity", 0);

            SharedTypeIndex<Entity>.Ref.Data = entityTypeIndex;
        }

        /// <summary>
        /// Removes all ECS type information and any allocated memory. May only be called once globally, and must be
        /// called from the main thread.
        /// </summary>
        public static void Shutdown()
        {
            // TODO, with module loaded type info, we cannot shutdown
#if UNITY_EDITOR
            if (!UnityEditorInternal.InternalEditorUtility.CurrentThreadIsMainThread())
                throw new InvalidOperationException("Must be called from the main thread");
#endif

            if (!s_Initialized)
                return;

            s_Initialized = false;

            s_TypeCount = 0;
            s_Types.Clear();

            ShutdownSystemsState();

#if !UNITY_DOTSRUNTIME
            s_FailedTypeBuildException = null;
            s_ManagedTypeToIndex.Clear();
#endif

            DisposeNative();

            ShutdownSharedStatics();
            EntityNameStorage.Shutdown();

#if !UNITY_DOTSRUNTIME
            ShutdownAspects();
#endif

            FastEquality.Shutdown();
        }

        static void DisposeNative()
        {
            s_TypeInfos.Dispose();
#if UNITY_DOTSRUNTIME
            s_DescendantIndex.Dispose();
#endif
            s_DescendantCounts.Dispose();
            s_StableTypeHashToTypeIndex.Dispose();
            s_EntityOffsetList.Dispose();
            s_BlobAssetRefOffsetList.Dispose();
            s_WeakAssetRefOffsetList.Dispose();
            s_UnityObjectRefOffsetList.Dispose();
            s_WriteGroupList.Dispose();
            s_SharedComponent_FunctionPointers.Dispose();

            foreach (var info in s_FastEqualityTypeInfoList)
                info.Dispose();
            s_FastEqualityTypeInfoList.Dispose();

            foreach (var name in s_TypeNames)
                name.Dispose();
            s_TypeNames.Dispose();
            s_TypeFullNameHashes.Dispose();
        }

        private static TypeIndex FindTypeIndex(Type type)
        {
#if !UNITY_DOTSRUNTIME
            if (type == null)
                return TypeIndex.Null;

            var res = TypeIndex.Null;
            s_ManagedTypeToIndex.TryGetValue(type, out res);

            return res;
#else
            // skip 0 since it is always null
            for (var i = 1; i < s_Types.Count; i++)
                if (type == s_Types[i])
                    return s_TypeInfos[i].TypeIndex;

            throw new ArgumentException("Tried to GetTypeIndex for type that has not been set up by the static type registry.");
#endif
        }

        internal static bool TryGetTypeIndex(Type type, out TypeIndex index)
        {
#if !UNITY_DOTSRUNTIME
            if (type == null)
            {
                index = TypeIndex.Null;
                return true;
            }

            TypeIndex res;
            if (s_ManagedTypeToIndex.TryGetValue(type, out res))
            {
                index = res;
                return true;
            }

            index = TypeIndex.Null;
            return false;
#else
            // skip 0 since it is always null
            for (var i = 1; i < s_Types.Count; i++)
                if (type == s_Types[i])
                {
                    index = s_TypeInfos[i].TypeIndex;
                    return true;
                }
            index = TypeIndex.Null;
            return false;
#endif
        }

        [BurstDiscard]
        static void ManagedException<T>()
        {
            ManagedException(typeof(T));
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        static void BurstException<T>()
        {
            throw new ArgumentException($"Unknown Type:`{typeof(T)}` All ComponentType must be known at compile time & be successfully registered. For generic components, each concrete type must be registered with [RegisterGenericComponentType].");
        }

        static void ManagedException(Type type)
        {
            Assert.IsTrue(s_Initialized, "Ensure TypeManager.Initialize has been called before using the TypeManager");
#if !UNITY_DOTSRUNTIME
            // When the type is known but failed to build, we repeat the reason why it failed to build instead.
            if (type != null && s_FailedTypeBuildException.TryGetValue(type, out var exception))
                throw new ArgumentException(exception.Message);
            // Otherwise it wasn't registered at all
            else
#endif
            throw new ArgumentException($"Unknown Type:`{(type == null ? "null" : type)}` All ComponentType must be known at compile time. For generic components, each concrete type must be registered with [RegisterGenericComponentType].");
        }

        /// <summary>
        /// Fetches the TypeIndex for a given T.
        /// </summary>
        /// <typeparam name="T">A component type</typeparam>
        /// <returns>Returns the TypeIndex for the corresponding T component type. Otherwise <seealso cref="TypeIndex.Null"/> is returned.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new Type[] { typeof(Entity) })]
        public static TypeIndex GetTypeIndex<T>()
        {
            var index = SharedTypeIndex<T>.Ref.Data;

            if (index == TypeIndex.Null)
            {
                ManagedException<T>();
                BurstException<T>();
            }

            return index;
        }

        /// <summary>
        /// Fetches the TypeIndex for a given System.Type.
        /// </summary>
        /// <param name="type">A component type</param>
        /// <returns>Returns the TypeIndex for the corresponding System.Type component type. Otherwise <seealso cref="TypeIndex.Null"/> is returned.</returns>
        public static TypeIndex GetTypeIndex(Type type)
        {
            var index = FindTypeIndex(type);

            if (index == TypeIndex.Null)
                ManagedException(type);

            return index;
        }

#if !UNITY_DOTSRUNTIME
        [BurstDiscard]
        internal static void ManagedEquals<T>(ref T left, ref T right, TypeIndex typeIndex, out int result) where T : struct
        {
            result = FastEquality.ManagedEquals(left, right, GetFastEqualityTypeInfoPointer()[typeIndex.Index]) ? 1 : 0;
        }
#endif

        /// <summary>
        /// Compares two component instances to one another
        /// </summary>
        /// <typeparam name="T">Type of the component instances</typeparam>
        /// <param name="left">Left-hand side of the comparison</param>
        /// <param name="right">Right-hand side of the comparison</param>
        /// <returns>Returns true if the types are equal.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new Type[] { typeof(Entity) })]
        public static bool Equals<T>(ref T left, ref T right) where T : struct
        {
            var typeIndex = GetTypeIndex<T>();
            if (typeIndex.IsSharedComponentType)
            {
#if !UNITY_DOTSRUNTIME
                if (typeIndex.IsManagedType)
                {
                    int result = -1;
                    ManagedEquals(ref left, ref right, typeIndex, out result);

                    // We shouldn't be able to hit this codepath since Burst would have complained by now by the managed T use in the method
                    if(Burst.CompilerServices.Hint.Unlikely(result < 0))
                        throw new ArgumentException($"Attempted to compare managed component type '{typeof(T)}' from Burst compiled method");

                    return result == 1;
                }
                else
#endif
                {
                    if (IsIEquatable(typeIndex))
                    {
                        return SharedComponentEquals(UnsafeUtility.AddressOf(ref left), UnsafeUtility.AddressOf(ref right), typeIndex);
                    }
                }
            }

#if ENABLE_IL2CPP
            return FastEquality.Equals(UnsafeUtility.AddressOf(ref left), UnsafeUtility.AddressOf(ref right), in GetFastEqualityTypeInfoPointer()[typeIndex.Index]);
#else
            return UnsafeUtility.MemCmp(UnsafeUtility.AddressOf(ref left), UnsafeUtility.AddressOf(ref right), UnsafeUtility.SizeOf<T>()) == 0;
#endif
        }

        /// <summary>
        /// Compares two component instances to one another
        /// </summary>
        /// <param name="left">Left-hand side of the comparison</param>
        /// <param name="right">Right-hand side of the comparison</param>
        /// <param name="typeIndex">TypeIndex for the two instances being compared</param>
        /// <returns>Returns true if the types are equal.</returns>
        [GenerateTestsForBurstCompatibility]
        public static bool Equals(void* left, void* right, TypeIndex typeIndex)
        {
            if (typeIndex.IsSharedComponentType)
                return SharedComponentEquals(left, right, typeIndex);

#if !UNITY_DOTSRUNTIME && ENABLE_IL2CPP
            return FastEquality.Equals(left, right, GetFastEqualityTypeInfoPointer()[typeIndex.Index]);
#else
            var typeInfo = GetTypeInfo(typeIndex);
            return UnsafeUtility.MemCmp(left, right, typeInfo.TypeSize) == 0;
#endif
        }

        [BurstDiscard]
        static void SetIfNotBurst(ref bool answer)
        {
            answer = true;
        }

        /// <summary>
        /// Used internally to determine if Burst is compiling the code path calling this function
        /// </summary>
        /// <returns>Returns true if Burst compiled this method</returns>
        static bool IsBursted()
        {
            var ret = false;
            SetIfNotBurst(ref ret);
            return !ret;
        }

        /// <summary>
        /// Compares two shared component instances to one another
        /// </summary>
        /// <param name="left">Left-hand side of the comparison</param>
        /// <param name="right">Right-hand side of the comparison</param>
        /// <param name="typeIndex">TypeIndex for the two instances being compared</param>
        /// <returns>Returns true if the types are equal.</returns>
        [GenerateTestsForBurstCompatibility]
        internal static bool SharedComponentEquals(void* left, void* right, TypeIndex typeIndex)
        {
            if (typeIndex.IsIEquatable)
            {
                CallManagedEquals(left, right, typeIndex, out bool wasequal, out var didwrite);
                if (didwrite)
                    return wasequal;

                return GetIEquatable_EqualsFn(typeIndex).Invoke(left, right);
            }

#if !UNITY_DOTSRUNTIME && ENABLE_IL2CPP
            var typeInfo = GetFastEqualityTypeInfoPointer()[typeIndex.Index];
            return FastEquality.Equals(left, right, typeInfo);
#else
            var typeInfo = GetTypeInfoPointer()[typeIndex.Index];
            return UnsafeUtility.MemCmp(left, right, typeInfo.TypeSize) == 0;
#endif
        }

        [BurstDiscard]
        private static void CallManagedEquals(void* left, void* right, TypeIndex typeIndex, out bool wasequal, out bool didwrite)
        {
            didwrite = true;
            wasequal = s_SharedComponentFns_gcDefeat[typeIndex.Index].EqualsFn.Invoke(left, right);
        }

        /// <summary>
        /// Generates a hash for a given shared component instance
        /// </summary>
        /// <param name="data">Pointer to component instance to hash</param>
        /// <param name="typeIndex">TypeIndex for the component</param>
        /// <returns>Returns true if the types are equal.</returns>
        [GenerateTestsForBurstCompatibility]
        internal static int SharedComponentGetHashCode(void* data, TypeIndex typeIndex)
        {
            if (typeIndex.IsIEquatable)
            {
                CallManagedGetHashCode(data, typeIndex, out var hashCode, out var didwrite);
                if (didwrite)
                    return hashCode;

                var ghc = GetIEquatable_GetHashCodeFn(typeIndex);

                if (ghc.IsCreated)
                    return ghc.Invoke(data);
            }

#if !UNITY_DOTSRUNTIME && ENABLE_IL2CPP
            return FastEquality.GetHashCode((byte*)data, GetFastEqualityTypeInfoPointer()[typeIndex.Index]);
#else
            var typeInfo = GetTypeInfoPointer()[typeIndex.Index];
            return (int)XXHash.Hash32((byte*)data, typeInfo.TypeSize);
#endif
        }

        [BurstDiscard]
        private static void CallManagedGetHashCode(
            void* data,
            TypeIndex typeIndex,
            out int hashCodeWithBurst,
            out bool didwrite)
        {
            var ghc = s_SharedComponentFns_gcDefeat[typeIndex.Index].GetHashCodeFn;
            if (ghc != null)
            {
                hashCodeWithBurst = ghc.Invoke(data);
                didwrite = true;
            }
            else
            {
                hashCodeWithBurst = 0;
                didwrite = false;
            }
        }

        /// <summary>
        /// Compares two component instances to one another
        /// </summary>
        /// <param name="left">Left-hand side of the comparison</param>
        /// <param name="right">Right-hand side of the comparison</param>
        /// <param name="typeIndex">TypeIndex for the two instances being compared</param>
        /// <returns>Returns true if the types are equal.</returns>
        public static bool Equals(object left, object right, TypeIndex typeIndex)
        {
#if !UNITY_DOTSRUNTIME
            if (left == null || right == null)
            {
                return left == right;
            }

            if (IsManagedType(typeIndex))
            {
                var typeInfo = GetFastEqualityTypeInfoPointer()[typeIndex.Index];
                return FastEquality.ManagedEquals(left, right, typeInfo);
            }
            else
            {
                // IL2CPP has a bug where we can fail to detect managed types correctly which will
                // cause the code #else codepath to throw if it contains managed references, so we provide
                // an alternative path until that is fixed (UUM-43422)
                #if ENABLE_IL2CPP
                    var leftptr = (byte*)UnsafeUtility.PinGCObjectAndGetAddress(left, out var lhandle) + ObjectOffset;
                    var rightptr = (byte*)UnsafeUtility.PinGCObjectAndGetAddress(right, out var rhandle) + ObjectOffset;

                    var result = Equals(leftptr, rightptr, typeIndex);

                    UnsafeUtility.ReleaseGCObject(lhandle);
                    UnsafeUtility.ReleaseGCObject(rhandle);

                    return result;
                #else
                    var leftHandle = GCHandle.Alloc(left, GCHandleType.Pinned);
                    var rightHandle = GCHandle.Alloc(right, GCHandleType.Pinned);
                    var leftptr = (byte*)leftHandle.AddrOfPinnedObject();
                    var rightptr = (byte*)rightHandle.AddrOfPinnedObject();

                    var result = Equals(leftptr, rightptr, typeIndex);

                    leftHandle.Free();
                    rightHandle.Free();

                    return result;
                #endif
            }
#else
                return GetBoxedEquals(left, right, typeIndex.Index);
#endif
        }

        /// <summary>
        /// Compares two component instances to one another
        /// </summary>
        /// <param name="left">Left-hand side of the comparison</param>
        /// <param name="right">Right-hand side of the comparison</param>
        /// <param name="typeIndex">TypeIndex for the two instances being compared</param>
        /// <returns>Returns true if the types are equal.</returns>
        public static bool Equals(object left, void* right, TypeIndex typeIndex)
        {
#if !UNITY_DOTSRUNTIME

    // IL2CPP has a bug where we can fail to detect managed types correctly which will
    // cause the code #else codepath to throw if it contains managed references, so we provide
    // an alternative path until that is fixed (UUM-43422)
    #if ENABLE_IL2CPP
            var leftptr = (byte*)UnsafeUtility.PinGCObjectAndGetAddress(left, out var lhandle) + ObjectOffset;

            var result = Equals(leftptr, right, typeIndex);

            UnsafeUtility.ReleaseGCObject(lhandle);
            return result;
    #else
            var leftHandle = GCHandle.Alloc(left, GCHandleType.Pinned);
            var leftptr = (byte*)leftHandle.AddrOfPinnedObject();

            var result = Equals(leftptr, right, typeIndex);

            leftHandle.Free();
            return result;
    #endif
#else
            return GetBoxedEquals(left, right, typeIndex.Index);
#endif
        }

#if !UNITY_DOTSRUNTIME
        [BurstDiscard]
        internal static void ManagedGetHashCode<T>(ref T val, TypeIndex typeIndex, out int hash, out int did_work) where T : struct
        {
            did_work = 1;
            hash = FastEquality.ManagedGetHashCode(val, GetFastEqualityTypeInfoPointer()[typeIndex.Index]);
        }
#endif

        /// <summary>
        /// Generates a hash for a given component instance
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <param name="val">Component instance to hash</param>
        /// <returns>Returns true if the types are equal.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new Type[] { typeof(Entity) })]
        public static int GetHashCode<T>(ref T val) where T : struct
        {
            var typeIndex = GetTypeIndex<T>();

#if !UNITY_DOTSRUNTIME
            if (typeIndex.IsManagedType)
            {
                ManagedGetHashCode(ref val, typeIndex, out int hash, out int did_work);

                // We shouldn't be able to hit this codepath since Burst would have complained by now by the managed T use in the method
                if (Burst.CompilerServices.Hint.Unlikely(did_work == 0))
                    throw new ArgumentException($"Attempted to GetHashCode using managed component type '{typeof(T)}' from Burst compiled method.");

                return hash;
            }
#endif

            return GetHashCode(UnsafeUtility.AddressOf(ref val), typeIndex);

        }

        /// <summary>
        /// Generates a hash for a given component instance
        /// </summary>
        /// <param name="val">Pointer to component instance to hash</param>
        /// <param name="typeIndex">TypeIndex for the component</param>
        /// <returns>Returns true if the types are equal.</returns>
        [GenerateTestsForBurstCompatibility]
        public static int GetHashCode(void* val, TypeIndex typeIndex)
        {
            if (typeIndex.IsSharedComponentType)
                return SharedComponentGetHashCode(val, typeIndex);

#if ENABLE_IL2CPP
            return FastEquality.GetHashCode(val, GetFastEqualityTypeInfoPointer()[typeIndex.Index]);
#else
            var typeInfo = GetTypeInfo(typeIndex);
            return (int)XXHash.Hash32((byte*)val, typeInfo.TypeSize);
#endif
        }

        /// <summary>
        /// Generates a hash for a given component instance
        /// </summary>
        /// <param name="val">Component instance to hash</param>
        /// <param name="typeIndex">TypeIndex for the component</param>
        /// <returns>Returns true if the types are equal.</returns>
        public static int GetHashCode(object val, TypeIndex typeIndex)
        {
#if !UNITY_DOTSRUNTIME
            if (IsManagedType(typeIndex))
            {
                var typeInfo = GetFastEqualityTypeInfoPointer()[typeIndex.Index];
                return FastEquality.ManagedGetHashCode(val, typeInfo);
            }
            else
            {
                // IL2CPP has a bug where we can fail to detect managed types correctly which will
                // cause the code #else codepath to throw if it contains managed references, so we provide
                // an alternative path until that is fixed (UUM-43422)
#if ENABLE_IL2CPP
                var ptr = (byte*)UnsafeUtility.PinGCObjectAndGetAddress(val, out var handle) + ObjectOffset;

                var result = GetHashCode(ptr, typeIndex);

                UnsafeUtility.ReleaseGCObject(handle);
                return result;
#else
                var handle = GCHandle.Alloc(val, GCHandleType.Pinned);
                var ptr = (byte*)handle.AddrOfPinnedObject();
                var result = GetHashCode(ptr, typeIndex);

                handle.Free();
                return result;
#endif
            }
#else
                return GetBoxedHashCode(val, typeIndex.Index);
#endif
        }

        /// <summary>
        /// Used by codegen
        /// </summary>
        /// <param name="indexInTypeArray">TypeIndex.Index</param>
        /// <param name="fn">Equals function</param>
        /// <param name="burstCompile">Is burst enabled</param>
        public static void SetIEquatable_EqualsFn(int indexInTypeArray, Delegate fn, bool burstCompile)
        {
            IntPtr fnPtr;

            if (burstCompile)
                fnPtr = BurstCompiler.CompileFunctionPointer(fn).Value;
            else
                fnPtr = Marshal.GetFunctionPointerForDelegate(fn);

            s_SharedComponentFns_gcDefeat[indexInTypeArray & ClearFlagsMask].EqualsFn =
                (FastEquality.TypeInfo.CompareEqualDelegate) fn;
            s_SharedComponent_FunctionPointers.Ptr[indexInTypeArray & ClearFlagsMask].EqualsFn =
                new FunctionPointer<FastEquality.TypeInfo.CompareEqualDelegate>(fnPtr);
        }

        /// <summary>
        /// Used by codegen
        /// </summary>
        /// <param name="indexInTypeArray">TypeIndex.Index</param>
        /// <param name="fn">GetHashCode function</param>
        /// <param name="burstCompile">Is burst enabled</param>
        public static void SetIEquatable_GetHashCodeFn(int indexInTypeArray, Delegate fn, bool burstCompile)
        {
            IntPtr fnPtr;

            if (burstCompile)
                fnPtr = BurstCompiler.CompileFunctionPointer(fn).Value;
            else
                fnPtr = Marshal.GetFunctionPointerForDelegate(fn);

            s_SharedComponentFns_gcDefeat[indexInTypeArray & ClearFlagsMask].GetHashCodeFn =
                (FastEquality.TypeInfo.GetHashCodeDelegate) fn;

            s_SharedComponent_FunctionPointers.Ptr[indexInTypeArray & ClearFlagsMask].GetHashCodeFn =
                new FunctionPointer<FastEquality.TypeInfo.GetHashCodeDelegate>(fnPtr);
        }


        /// <summary>
        /// Used by codegen
        /// </summary>
        /// <param name="indexInTypeArray">TypeIndex.Index</param>
        /// <param name="fn">Retain function</param>
        /// <param name="burstCompile">Is burst enabled</param>
        public static void SetIRefCounted_RetainFn(int indexInTypeArray, IRefCounted.RefCountDelegate fn, bool burstCompile)
        {
            IntPtr fnPtr;

            if (burstCompile)
                fnPtr = BurstCompiler.CompileFunctionPointer(fn).Value;
            else
                fnPtr = Marshal.GetFunctionPointerForDelegate(fn);

            s_SharedComponentFns_gcDefeat[indexInTypeArray & ClearFlagsMask].RetainFn = fn;

            s_SharedComponent_FunctionPointers.Ptr[indexInTypeArray & ClearFlagsMask].RetainFn =
                new FunctionPointer<IRefCounted.RefCountDelegate>(fnPtr);
        }

        /// <summary>
        /// Used by codegen
        /// </summary>
        /// <param name="indexInTypeArray">TypeIndex.Index</param>
        /// <param name="fn">Release function</param>
        /// <param name="burstCompile">Is burst enabled</param>
        public static void SetIRefCounted_ReleaseFn(int indexInTypeArray, IRefCounted.RefCountDelegate fn, bool burstCompile)
        {
            IntPtr fnPtr;

            if (burstCompile)
                fnPtr = BurstCompiler.CompileFunctionPointer(fn).Value;
            else
                fnPtr = Marshal.GetFunctionPointerForDelegate(fn);

            s_SharedComponentFns_gcDefeat[indexInTypeArray & ClearFlagsMask].ReleaseFn = fn;

            s_SharedComponent_FunctionPointers.Ptr[indexInTypeArray & ClearFlagsMask].ReleaseFn =
                new FunctionPointer<IRefCounted.RefCountDelegate>(fnPtr);
        }

        /// <summary>
        /// Used by codegen
        /// </summary>
        /// <param name="typeIndex">Component type index</param>
        /// <returns>FunctionPointer</returns>
        public static FunctionPointer<FastEquality.TypeInfo.CompareEqualDelegate> GetIEquatable_EqualsFn(TypeIndex typeIndex)
        {
            return ((SharedComponentFnPtrs*) Shared_SharedComponentData_FnPtrs.Ref.Data)[typeIndex.Index].EqualsFn;
        }

        /// <summary>
        /// Used by codegen
        /// </summary>
        /// <param name="typeIndex">Component type index</param>
        /// <returns>FunctionPointer</returns>
        public static FunctionPointer<FastEquality.TypeInfo.GetHashCodeDelegate> GetIEquatable_GetHashCodeFn(TypeIndex typeIndex)
        {
            return ((SharedComponentFnPtrs*) Shared_SharedComponentData_FnPtrs.Ref.Data)[typeIndex.Index].GetHashCodeFn;
        }

        /// <summary>
        /// Used by codegen
        /// </summary>
        /// <param name="typeIndex">Component type index</param>
        /// <param name="data">Component instance</param>
        public static void CallIRefCounted_Retain(TypeIndex typeIndex, IntPtr data)
        {
            if (IsBursted())
                ((SharedComponentFnPtrs*) Shared_SharedComponentData_FnPtrs.Ref.Data)[typeIndex.Index].RetainFn.Invoke(data);
            else
            {
                CallManagedRetain(typeIndex, data);
            }
        }

        [BurstDiscard]
        private static void CallManagedRetain(TypeIndex typeindex, IntPtr data)
        {
            s_SharedComponentFns_gcDefeat[typeindex.Index].RetainFn.Invoke(data);
        }

        /// <summary>
        /// Used by codegen
        /// </summary>
        /// <param name="typeIndex">Component type index</param>
        /// <param name="data">Component instance</param>
        public static void CallIRefCounted_Release(TypeIndex typeIndex, IntPtr data)
        {
            if (IsBursted())
                ((SharedComponentFnPtrs*) Shared_SharedComponentData_FnPtrs.Ref.Data)[typeIndex.Index]
                    .ReleaseFn.Invoke(data);
            else
                CallManagedRelease(typeIndex, data);
        }

        [BurstDiscard]
        private static void CallManagedRelease(TypeIndex typeindex, IntPtr data)
        {
            s_SharedComponentFns_gcDefeat[typeindex.Index].ReleaseFn.Invoke(data);
        }

        /// <summary>
        /// Returns the TypeIndex for a given <seealso cref="TypeInfo.StableTypeHash"/>
        /// </summary>
        /// <param name="stableTypeHash">Component <seealso cref="TypeInfo.StableTypeHash"/></param>
        /// <returns>Component TypeIndex, otherwise <seealso cref="TypeIndex.Null"/></returns>
        [GenerateTestsForBurstCompatibility]
        public static TypeIndex GetTypeIndexFromStableTypeHash(ulong stableTypeHash)
        {
            UnsafeParallelHashMap<ulong, TypeIndex> map = default;
            map.m_Buffer = GetStableTypeHashMapPointer();

            if (map.TryGetValue(stableTypeHash, out var typeIndex))
                return typeIndex;
            return TypeIndex.Null;
        }

        /// <summary>
        /// Used by codegen. Create an instance of a component type from a given buffer of data
        /// </summary>
        /// <param name="typeIndex">Component type index</param>
        /// <param name="data">Component instance data</param>
        /// <returns>Boxed component instance</returns>
        public static unsafe object ConstructComponentFromBuffer(TypeIndex typeIndex, void* data)
        {
#if !UNITY_DOTSRUNTIME
            var tinfo = GetTypeInfo(typeIndex);
            Type type = GetType(typeIndex);
            object obj = Activator.CreateInstance(type);
            if (data!=null && tinfo.TypeSize != 0)
            {
                var handle = GCHandle.Alloc(obj, GCHandleType.Pinned);
                var ptr = ((byte*)handle.AddrOfPinnedObject());
                UnsafeUtility.MemCpy(ptr, data, tinfo.TypeSize);
                handle.Free();
            }

            return obj;
#else
            return ConstructComponentFromBuffer(data, typeIndex.Index);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        internal static void CheckComponentType(Type type)
        {
            int typeCount = 0;
            var interfaces = new Type[]
            {
                typeof(IComponentData),
                typeof(IBufferElementData),
                typeof(ISharedComponentData)
            };

            foreach (Type t in interfaces)
            {
                if (t.IsAssignableFrom(type))
                    ++typeCount;
            }

            if (typeCount > 1)
                throw new ArgumentException($"Component {type} can only implement one of IComponentData, ISharedComponentData and IBufferElementData");
        }

        /// <summary>
        /// Used by codegen. Returns list of all type indices for components who have a WriteGroup on the provided type
        /// </summary>
        /// <param name="typeIndex">Component type index</param>
        /// <returns>List of type indices</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new Type[] { typeof(Entity) })]
        public static NativeArray<TypeIndex> GetWriteGroupTypes(TypeIndex typeIndex)
        {
            var type = GetTypeInfo(typeIndex);
            var writeGroups = GetWriteGroups(type);
            var writeGroupCount = type.WriteGroupCount;
            var arr = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<TypeIndex>(writeGroups, writeGroupCount, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref arr, SharedSafetyHandle.Ref.Data);
#endif
            return arr;
        }

        // TODO: Fix our wild alignment requirements for chunk memory (easier said than done)
        /// <summary>
        /// Our alignment calculations for types are taken from the perspective of the alignment of the type _specifically_ when
        /// stored in chunk memory. This means a type's natural alignment may not match the AlignmentInChunk value. Our current scheme is such that
        /// an alignment of 'MaximumSupportedAlignment' is assumed unless the size of the type is smaller than 'MaximumSupportedAlignment' and is a power of 2.
        /// In such cases we use the type size directly, thus if you have a type that naturally aligns to 4 bytes and has a size of 8, the AlignmentInChunk will be 8
        /// as long as 8 is less than 'MaximumSupportedAlignment'.
        /// </summary>
        /// <param name="sizeOfTypeInBytes"></param>
        /// <returns></returns>
        internal static int CalculateAlignmentInChunk(int sizeOfTypeInBytes)
        {
            int alignmentInBytes = MaximumSupportedAlignment;
            if (sizeOfTypeInBytes < alignmentInBytes && CollectionHelper.IsPowerOfTwo(sizeOfTypeInBytes))
                alignmentInBytes = sizeOfTypeInBytes;

            return alignmentInBytes;
        }

        internal class TypeTreeNode
        {
            public Type Type;
            public TypeTreeNode Parent;
            public List<TypeTreeNode> Children;

            public int IndexInTypeArray;
        }

        private static TypeTreeNode[] BuildTypeTree(Type[] componentTypes, HashSet<Type> componentTypeSet, Dictionary<Type, int> descendantCountByType)
        {
            var typeTree = new Dictionary<Type, TypeTreeNode>(MaximumTypesCount);

            TypeTreeNode Add(Type type)
            {
                var newNode = new TypeTreeNode
                {
                    Type = type,
                    Children = null,
                    Parent = null
                };

                // Find base node if any and add as child
                var baseNode = LookupOrAdd(type.BaseType);
                if (baseNode != null)
                {
                    if(baseNode.Children == null)
                        baseNode.Children = new List<TypeTreeNode>();

                    baseNode.Children.Add(newNode);
                    newNode.Parent = baseNode;
                }

                typeTree[type] = newNode;

                return newNode;
            }

            TypeTreeNode LookupOrAdd(Type type)
            {
                if (type == null)
                    return null;

                if (!componentTypeSet.Contains(type))
                    return null;

                if (typeTree.ContainsKey(type))
                    return typeTree[type];

                return Add(type);
            }

            int DepthFirst(TypeTreeNode node, int index)
            {
                int descendantCount = 1;
                if (node.Children != null)
                {
                    for (int i = 0; i < node.Children.Count; i++)
                    {
                        var childNode = node.Children[i];
                        descendantCount += DepthFirst(childNode, index + descendantCount);
                    }
                }

                // Store type index and descendent count
                node.IndexInTypeArray = index;

                // Take one off to not include itself in the count
                descendantCountByType[node.Type] = descendantCount - 1;

                return descendantCount;
            }

            for (int i = 0; i < componentTypes.Length; i++)
            {
                LookupOrAdd(componentTypes[i]);
            }

            // Depth First traverse nodes
            int nextTypeIndex = s_TypeCount;
            var typeTreeNodes = typeTree.Values.ToArray();

            for (int i = 0; i < typeTreeNodes.Length; i++)
            {
                var node = typeTreeNodes[i];
                if (node.Parent != null)
                    continue;

                nextTypeIndex += DepthFirst(node, nextTypeIndex);
            }

            return typeTreeNodes;
        }


#if !UNITY_DOTSRUNTIME
        private static bool IsSupportedComponentType(Type type)
        {
            return typeof(IComponentData).IsAssignableFrom(type)
                || typeof(ISharedComponentData).IsAssignableFrom(type)
                || typeof(IBufferElementData).IsAssignableFrom(type);
        }

        static void AddUnityEngineObjectTypeToListIfSupported(HashSet<Type> componentTypeSet, Type type)
        {
            if (type.ContainsGenericParameters)
                return;
            if (type.IsAbstract && !typeof(Component).IsAssignableFrom(type))
                return;
            componentTypeSet.Add(type);
        }

        static bool IsInstantiableComponentType(Type type)
        {
            if (type.IsAbstract)
                return false;

#if !UNITY_DISABLE_MANAGED_COMPONENTS
            if (!type.IsValueType && !typeof(IComponentData).IsAssignableFrom(type))
                return false;
#else
            if (!type.IsValueType && typeof(IComponentData).IsAssignableFrom(type))
                throw new ArgumentException($"Type '{type.FullName}' inherits from IComponentData but has been defined as a managed type. " +
                    $"Managed component support has been explicitly disabled via the 'UNITY_DISABLE_MANAGED_COMPONENTS' define. " +
                    $"Change the offending type to be a value type or re-enable managed component support.");

            if (!type.IsValueType)
                return false;
#endif

            // Don't register open generics here.  It's an open question
            // on whether we should support them for components at all,
            // as with them we can't ever see a full set of component types
            // in use.
            if (type.ContainsGenericParameters)
                return false;

            if (Attribute.IsDefined(type, typeof(DisableAutoTypeRegistrationAttribute)))
                return false;

            return true;
        }
        static void AddComponentTypeToListIfSupported(HashSet<Type> typeSet, Type type)
        {
            if (!IsInstantiableComponentType(type))
                return;

            typeSet.Add(type);
        }

        static void InitializeAllComponentTypes()
        {
            try
            {
                Profiler.BeginSample(nameof(InitializeAllComponentTypes));
                var componentTypeSet = new HashSet<Type>();

                // Inject types needed for Hybrid
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                UnityEngineObjectType = typeof(UnityEngine.Object);

#if UNITY_EDITOR
                foreach (var type in UnityEditor.TypeCache.GetTypesDerivedFrom<UnityEngine.Object>())
                    AddUnityEngineObjectTypeToListIfSupported(componentTypeSet, type);
                foreach (var type in UnityEditor.TypeCache.GetTypesDerivedFrom<IComponentData>())
                    AddComponentTypeToListIfSupported(componentTypeSet, type);
                foreach (var type in UnityEditor.TypeCache.GetTypesDerivedFrom<IBufferElementData>())
                    AddComponentTypeToListIfSupported(componentTypeSet, type);
                foreach (var type in UnityEditor.TypeCache.GetTypesDerivedFrom<ISharedComponentData>())
                    AddComponentTypeToListIfSupported(componentTypeSet, type);
#else
                foreach (var assembly in assemblies)
                {
                    IsAssemblyReferencingEntitiesOrUnityEngine(assembly, out var isAssemblyReferencingEntities,
                        out var isAssemblyReferencingUnityEngine);
                    var isAssemblyRelevant = isAssemblyReferencingEntities || isAssemblyReferencingUnityEngine;

                    if (!isAssemblyRelevant)
                        continue;

                    var assemblyTypes = assembly.GetTypes();

                    // Register UnityEngine types (Hybrid)
                    if (isAssemblyReferencingUnityEngine)
                    {
                        foreach (var type in assemblyTypes)
                        {
                            if (UnityEngineObjectType.IsAssignableFrom(type))
                                AddUnityEngineObjectTypeToListIfSupported(componentTypeSet, type);
                        }
                    }

                    // Register ComponentData types
                    if (isAssemblyReferencingEntities)
                    {
                        foreach (var type in assemblyTypes)
                        {
                            if (IsSupportedComponentType(type))
                                AddComponentTypeToListIfSupported(componentTypeSet, type);
                        }
                    }
                }
#endif

                // Register ComponentData concrete generics
                foreach (var assembly in assemblies)
                {
                    foreach (var registerGenericComponentTypeAttribute in assembly.GetCustomAttributes<RegisterGenericComponentTypeAttribute>())
                    {
                        var type = registerGenericComponentTypeAttribute.ConcreteType;

                        if (IsSupportedComponentType(type))
                            componentTypeSet.Add(type);
                    }
                }

                var componentTypeCount = componentTypeSet.Count;
                var componentTypes = new Type[componentTypeCount];
                componentTypeSet.CopyTo(componentTypes);

                var indexByType = new Dictionary<Type, int>();
                var writeGroupByType = new Dictionary<int, HashSet<TypeIndex>>();
                var descendantCountByType = new Dictionary<Type, int>();
                var startTypeIndex = s_TypeCount;

                var typeTreeNodes = BuildTypeTree(componentTypes, componentTypeSet, descendantCountByType);

                // Sort the component types for descendant info
                for (var i = 0; i < typeTreeNodes.Length; i++)
                {
                    var node = typeTreeNodes[i];
                    componentTypes[node.IndexInTypeArray - startTypeIndex] = node.Type;
                    indexByType[node.Type] = node.IndexInTypeArray;
                }

                /*
                 * now that type indices have been built, we can use them as keys in our hash map
                 */
                GatherSharedComponentMethods(indexByType);


                GatherWriteGroups(componentTypes, startTypeIndex, indexByType, writeGroupByType);
                AddAllComponentTypes(componentTypes, startTypeIndex, writeGroupByType, descendantCountByType);
            }
            finally
            {
                // If any components have errors, ensure we log the errors and tell the user as continuing to use invalid components
                // could lead to crashes due to the unsafe nature of how invoke burst instance methods from generic code paths
                if (s_FailedTypeBuildException.Count > 0)
                {
                    throw new ArgumentException("TypeManager initialization failed. Please fix all exceptions logged above before continuing.");
                }

                Profiler.EndSample();
            }
        }

        private static void GatherSharedComponentMethods(Dictionary<Type, int> indexByType)
        {
#if UNITY_EDITOR
            foreach (var type in UnityEditor.TypeCache.GetTypesDerivedFrom<IRefCounted>())
            {
#else
                foreach (var type in indexByType.Keys)
                {
                    if (!typeof(IRefCounted).IsAssignableFrom(type))
                        continue;
#endif

                var indexInTypeArray = indexByType[type];

                var methodInfo = type.GetMethod("__codegen__Retain", BindingFlags.Static | BindingFlags.Public);

                IRefCounted.RefCountDelegate retainDelegate = (IRefCounted.RefCountDelegate)methodInfo
                    .CreateDelegate(typeof(IRefCounted.RefCountDelegate));
                SetIRefCounted_RetainFn(indexInTypeArray,
                    retainDelegate,
                    Attribute.IsDefined(methodInfo, typeof(BurstCompileAttribute)));

                methodInfo = type.GetMethod("__codegen__Release", BindingFlags.Static | BindingFlags.Public);
                IRefCounted.RefCountDelegate releaseDelegate = (IRefCounted.RefCountDelegate)methodInfo
                    .CreateDelegate(typeof(IRefCounted.RefCountDelegate));
                SetIRefCounted_ReleaseFn(indexInTypeArray,
                    releaseDelegate,
                    Attribute.IsDefined(methodInfo, typeof(BurstCompileAttribute)));
            }

            foreach (var type in indexByType.Keys)
            {
                var indexInTypeArray = indexByType[type];


                if (!typeof(ISharedComponentData).IsAssignableFrom(type) ||
                    !type.GetInterfaces().Any(i => i.Name == "IEquatable`1"))
                    continue;

                var methodInfo = type.GetMethod("__codegen__Equals", new[] {typeof(void*), typeof(void*)});

                var equalsDelegate = methodInfo.CreateDelegate(typeof(FastEquality.TypeInfo.CompareEqualDelegate));
                SetIEquatable_EqualsFn(indexInTypeArray,
                    equalsDelegate,
                    Attribute.IsDefined(methodInfo, typeof(BurstCompileAttribute)));

                methodInfo = type.GetMethod("__codegen__GetHashCode", BindingFlags.Static | BindingFlags.Public);
                if (methodInfo != null)
                {
                    var ghcDelegate = methodInfo.CreateDelegate(typeof(FastEquality.TypeInfo.GetHashCodeDelegate));
                    SetIEquatable_GetHashCodeFn(indexInTypeArray,
                        ghcDelegate,
                        Attribute.IsDefined(methodInfo, typeof(BurstCompileAttribute)));
                }

            }
        }

        internal class BuildComponentCache
        {
            public Dictionary<Type, ulong> TypeHashCache;
            public Dictionary<Type, bool> NestedNativeContainerCache;
            public Dictionary<Type, EntityRemapUtility.EntityBlobRefResult> HasEntityOrBlobAssetReferenceCache;
            public Dictionary<Type, List<FastEquality.LayoutInfo>> FastEqualityLayoutInfoCache;
            public Dictionary<Type, (bool, bool)> ChunkSerializableCache;
            public HashSet<Type> AllowedComponentCache;
            public HashSet<Type> CalculateFieldOffsetsUnmanagedCache;

            public BuildComponentCache(int initialCapacity = 0)
            {
                TypeHashCache = new Dictionary<Type, ulong>(initialCapacity);
                NestedNativeContainerCache = new Dictionary<Type, bool>(initialCapacity);
                HasEntityOrBlobAssetReferenceCache = new Dictionary<Type, EntityRemapUtility.EntityBlobRefResult>(initialCapacity);
                FastEqualityLayoutInfoCache = new Dictionary<Type, List<FastEquality.LayoutInfo>>(initialCapacity);
                AllowedComponentCache = new HashSet<Type>(initialCapacity);
                ChunkSerializableCache = new Dictionary<Type, (bool isChunkSerializable, bool hasChunkSerializableAttribute)>();
                CalculateFieldOffsetsUnmanagedCache = new HashSet<Type>(initialCapacity);
            }
        }

        private static void AddAllComponentTypes(Type[] componentTypes, int startTypeIndex, Dictionary<int, HashSet<TypeIndex>> writeGroupByType, Dictionary<Type, int> descendantCountByType)
        {
            BuildComponentCache caches = new BuildComponentCache(componentTypes.Length);
            var expectedTypeIndex = startTypeIndex;

            for (int i = 0; i < componentTypes.Length; i++)
            {
                var type = componentTypes[i];
                try
                {
                    var index = FindTypeIndex(type);
                    if (index != TypeIndex.Null)
                        throw new InvalidOperationException($"ComponentType {type} cannot be initialized more than once.");

                    TypeInfo typeInfo;
                    if (writeGroupByType.ContainsKey(expectedTypeIndex))
                    {
                        var writeGroupSet = writeGroupByType[expectedTypeIndex];
                        var writeGroupCount = writeGroupSet.Count;
                        var writeGroupArray = new TypeIndex[writeGroupCount];
                        writeGroupSet.CopyTo(writeGroupArray);

                        typeInfo = BuildComponentType(type, writeGroupArray, caches);
                    }
                    else
                    {
                        typeInfo = BuildComponentType(type, caches);
                    }

                    var typeIndexNoFlags = typeInfo.TypeIndex.Index;
                    if (expectedTypeIndex != typeIndexNoFlags)
                        throw new InvalidOperationException($"ComponentType.TypeIndex does not match precalculated index for {type}. Expected: {expectedTypeIndex:x8} Actual: {typeIndexNoFlags:x8}");

                    var descendantCount = descendantCountByType[type];

                    AddTypeInfoToTables(type, typeInfo, type.FullName, descendantCount);
                    expectedTypeIndex += 1;
                }
                catch (Exception e)
                {
                    if (type != null)
                    {
                        // Explicitly clear the shared type index.
                        // This is a workaround for a bug in burst where the shared static doesn't get reset to zero on domain reload.
                        // Can be removed once it is fixed in burst.
                        SharedTypeIndex.Get(type) = TypeIndex.Null;
                        s_FailedTypeBuildException[type] = e;
                    }

                    Debug.LogException(e);
                }
            }
        }

        private static void GatherWriteGroups(Type[] componentTypes, int startTypeIndex, Dictionary<Type, int> indexByType,
            Dictionary<int, HashSet<TypeIndex>> writeGroupByType)
        {
            for (int i = 0; i < componentTypes.Length; i++)
            {
                var type = componentTypes[i];
                var indexInTypeArray = new TypeIndex { Value = startTypeIndex + i };

                foreach (var attribute in type.GetCustomAttributes(typeof(WriteGroupAttribute)))
                {
                    var attr = (WriteGroupAttribute)attribute;
                    if (!indexByType.ContainsKey(attr.TargetType))
                    {
                        Debug.LogError($"GatherWriteGroups: looking for {attr.TargetType} but it hasn't been set up yet");
                    }

                    var targetIndexInTypeArray = indexByType[attr.TargetType];

                    if (!writeGroupByType.ContainsKey(targetIndexInTypeArray))
                    {
                        var targetList = new HashSet<TypeIndex>();
                        writeGroupByType.Add(targetIndexInTypeArray, targetList);
                    }

                    writeGroupByType[targetIndexInTypeArray].Add(indexInTypeArray);
                }
            }
        }

        /// <summary>
        /// Determines if an assembly refers to Unity.Entities.dll
        /// </summary>
        /// <param name="assembly">An Assembly</param>
        /// <returns>Returns if assembly refers to Unity.Entities.dll</returns>
        public static bool IsAssemblyReferencingEntities(Assembly assembly)
        {
            const string kEntitiesAssemblyName = "Unity.Entities";
            if (assembly.GetName().Name.Contains(kEntitiesAssemblyName))
                return true;

            var referencedAssemblies = assembly.GetReferencedAssemblies();
            foreach (var referenced in referencedAssemblies)
                if (referenced.Name.Contains(kEntitiesAssemblyName))
                    return true;
            return false;
        }

        internal static void IsAssemblyReferencingEntitiesOrUnityEngine(Assembly assembly, out bool referencesEntities, out bool referencesUnityEngine)
        {
            const string kEntitiesAssemblyName = "Unity.Entities";
            const string kUnityEngineAssemblyName = "UnityEngine";
            var assemblyName = assembly.GetName().Name;

            referencesEntities = false;
            referencesUnityEngine = false;

            if (assemblyName.Contains(kEntitiesAssemblyName))
                referencesEntities = true;

            if (assemblyName.Contains(kUnityEngineAssemblyName))
                referencesUnityEngine = true;

            var referencedAssemblies = assembly.GetReferencedAssemblies();
            foreach (var referencedAssembly in referencedAssemblies)
            {
                var referencedAssemblyName = referencedAssembly.Name;

                if (!referencesEntities && referencedAssemblyName.Contains(kEntitiesAssemblyName))
                    referencesEntities = true;
                if (!referencesUnityEngine && referencedAssemblyName.Contains(kUnityEngineAssemblyName))
                    referencesUnityEngine = true;
            }
        }

        internal static void CheckIsAllowedAsComponentData(Type type, string baseTypeDesc, HashSet<Type> allowedComponentCache = null)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (UnsafeUtility.IsUnmanaged(type))
                return;

            // it can't be used -- so we expect this to find and throw
            ThrowOnDisallowedComponentData(type, type, baseTypeDesc, allowedComponentCache);

            // if something went wrong and the above didn't throw, then throw
            throw new ArgumentException($"{type} cannot be used as component data for unknown reasons (BUG)");
#endif
        }

        internal static void CheckTypeOverrideAndUpdateHasReferences(Type type, Dictionary<Type, EntityRemapUtility.EntityBlobRefResult> cache, ref bool hasEntityReferences, ref bool hasBlobReferences)
        {
            // Components can opt out of certain patching operations as an optimization since with managed components we can't statically
            // know if there are certainly entity or blob references. Check here to ensure the type overrides were done correctly.
            var typeOverride = type.GetCustomAttribute<TypeOverridesAttribute>(true);
            if (typeOverride != null)
            {
                EntityRemapUtility.HasEntityReferencesManaged(type, out var actuallyHasEntityRefs, out var actuallyHasBlobRefs, cache, 128);
                if (typeOverride.HasNoEntityReferences && actuallyHasEntityRefs == EntityRemapUtility.HasRefResult.HasRef)
                {
                    throw new ArgumentException(
                        $"Component type '{type}' has a {nameof(TypeOverridesAttribute)} marking the component as not having " +
                        $"Entity references, however the type does in fact contain a (potentially nested) Entity reference. " +
                        $"This is not allowed. Please refer to the documentation for {nameof(TypeOverridesAttribute)} for how to use this attribute.");
                }

                if (typeOverride.HasNoBlobReferences && actuallyHasBlobRefs == EntityRemapUtility.HasRefResult.HasRef)
                {
                    throw new ArgumentException(
                        $"Component type '{type}' has a {nameof(TypeOverridesAttribute)} marking the component as not having " +
                        $"BlobAssetReferences, however the type does in fact contain a (potentially nested) BlobAssetReference. " +
                        $"This is not allowed. Please refer to the documentation for {nameof(TypeOverridesAttribute)} for how to use this attribute.");
                }
            }

            if (typeOverride != null && typeOverride.HasNoEntityReferences)
                hasEntityReferences = false;
            if (typeOverride != null && typeOverride.HasNoBlobReferences)
                hasBlobReferences = false;
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        internal static void CheckIsAllowedAsManagedComponentData(Type type, string baseTypeDesc, Dictionary<Type, bool> nestedContainerCache, out bool hasNativeContainer)
        {
            if (type.IsClass && typeof(IComponentData).IsAssignableFrom(type))
            {
                hasNativeContainer = DoesComponentContainNativeContainer(type, type, nestedContainerCache);
                ThrowOnDisallowedManagedComponentData(type, baseTypeDesc);
                return;
            }

            // if something went wrong and the above didn't throw, then throw
            throw new ArgumentException($"{type} cannot be used as managed component data for unknown reasons (BUG)");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        internal static void ThrowOnDisallowedManagedComponentData(Type type, string baseTypeDesc)
        {
            // Validate the class component data type is usable:
            // - Has a default constructor
            if (type.GetConstructor(Type.EmptyTypes) == null)
                throw new ArgumentException($"{type} is a class based {baseTypeDesc}. Class based {baseTypeDesc} must implement a default constructor.");
        }
#endif

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        internal static void ThrowOnDisallowedComponentData(Type type, Type baseType, string baseTypeDesc, HashSet<Type> cache)
        {
            if (type.IsPrimitive)
                return;

            // if it's a pointer, we assume you know what you're doing
            if (type.IsPointer)
                return;

            // The cache check is lower than the above early outs since checking the cache is more expensive
            // than the above checks
            if (cache.Contains(type))
                return;

            cache.Add(type);

            if (!type.IsValueType || type.IsByRef || type.IsClass || type.IsInterface || type.IsArray)
            {
                if (type == baseType)
                    throw new ArgumentException(
                        $"{type} is a {baseTypeDesc} and thus must be a struct containing only primitive or blittable members.");

                throw new ArgumentException($"{baseType} contains a field of {type}, which is neither primitive nor blittable.");
            }

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                ThrowOnDisallowedComponentData(field.FieldType, baseType, baseTypeDesc, cache);
            }
        }

        internal static bool DoesComponentContainNativeContainer(Type type, Type baseType, Dictionary<Type, bool> nestedContainerCache)
        {
            if (type.IsPrimitive)
                return false;

            if (nestedContainerCache.TryGetValue(type, out bool hasContainer))
                return hasContainer;

            if (type.IsArray)
            {
                hasContainer = false;
                var elementType = type.GetElementType();
                if (elementType != null)
                    hasContainer = DoesComponentContainNativeContainer(elementType, baseType, nestedContainerCache);
                // We may have inserted the ElementType as part of the recursive call above if the type contains
                // a circular reference (which is not valid for ValueTypes but is possible for ReferenceTypes).
                // Since we don't know the answer for hasContainer, and it can't have changed since the recursive
                // call we use TryAdd, and don't need to do a Contains and then update the value; TryAdd is good enough.
                nestedContainerCache.TryAdd(type, hasContainer);
                return hasContainer;
            }

            hasContainer = Attribute.IsDefined(type, typeof(NativeContainerAttribute));
            nestedContainerCache.Add(type, hasContainer);

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                hasContainer |= DoesComponentContainNativeContainer(field.FieldType, baseType, nestedContainerCache);
            }

            nestedContainerCache[type] = hasContainer;
            return hasContainer;
        }

        // A component type is "chunk serializable" if it meets the following rules:
        // - It is decorated with [ChunkSerializable] attribute (this is an override for all other rules)
        // - The type is blittable AND
        // - The type does not contain any pointer types (including IntPtr) AND
        // - If the type is a shared component, it does not contain an entity reference
        private static bool IsComponentChunkSerializable(Type type, TypeCategory category, bool hasEntityReference, BuildComponentCache cache)
        {
            var isSerializable = IsTypeValidForSerialization(type, cache.ChunkSerializableCache);

            // Shared Components are expected to be handled specially when serializing and are not required to be blittable.
            // They cannot contain an entity reference today as they are not patched however for unmanaged components
            // we should be able to correct this behaviour DOTS-7613
            if (!isSerializable.hasChunkSerializableAttribute && category == TypeCategory.ISharedComponentData && hasEntityReference)
            {
                isSerializable.isSerializable = false;
            }

            return isSerializable.isSerializable;
        }


        // True when a component is valid to using in world serialization. A component IsSerializable when it is valid to blit
        // the data across storage media. Thus components containing pointers have an IsSerializable of false as the component
        // is blittable but no longer valid upon deserialization.
        private static (bool isSerializable, bool hasChunkSerializableAttribute) IsTypeValidForSerialization(Type type, Dictionary<Type, (bool isChunkSerializable, bool hasChunkSerializableAttribute)> cache)
        {
            var result = (false, false);
            if (cache.TryGetValue(type, out result))
                return result;

            if (Attribute.IsDefined(type, typeof(ChunkSerializableAttribute)))
            {
                result = (true, true);
                cache.Add(type, result);
                return result;
            }

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (field.IsStatic)
                    continue;

                if (field.FieldType.IsPointer || (field.FieldType == typeof(UIntPtr) || field.FieldType == typeof(IntPtr)))
                {
                    return result;
                }
                else if (field.FieldType.IsValueType && !field.FieldType.IsPrimitive && !field.FieldType.IsEnum)
                {
                    if (!IsTypeValidForSerialization(field.FieldType, cache).isSerializable)
                    {
                        cache.Add(type, result);
                        return result;
                    }
                }
            }

            result = (true, false);
            cache.Add(type, result);
            return result;
        }

        // https://stackoverflow.com/a/27851610
        static bool IsZeroSizeStruct(Type t)
        {
            return t.IsValueType && !t.IsPrimitive &&
                t.GetFields((BindingFlags)0x34).All(fi => IsZeroSizeStruct(fi.FieldType));
        }

        private static ulong ComputeBloomFilterMask(ulong typeHash)
        {
            // This function effectively computes k different hashes from the input typeHash to a single bit in the output
            // mask. If k is too low, the odds increase that multiple types will have the same bloomFilterMask. If k is
            // too high, the odds increase that bitwise-or'ing multiple masks together will have so many bits set that a
            // missing type is "hidden". Either way, the net result is a higher false positive rate, reducing
            // the effectiveness of the Bloom filter early-out check.
            // k=5 seems to strike a reasonable balance, given the number of unique component types and the number
            // of types per archetype in typical DOTS applications.
            const int k = 5;
            const int maxShift = 8 * sizeof(ulong);
            uint seed = (uint)((typeHash & 0xFFFFFFFF) ^ (typeHash >> 32));
            var rng = new Unity.Mathematics.Random(seed != 0 ? seed : 17);
            ulong mask = 0;
            for(int i = 0; i < k; i++)
            {
                mask |= 1UL << rng.NextInt(maxShift);
            }
            return mask;
        }

        internal static TypeInfo BuildComponentType(Type type, BuildComponentCache caches)
        {
            return BuildComponentType(type, null, caches);
        }

        internal static TypeInfo BuildComponentType(Type type, TypeIndex[] writeGroups, BuildComponentCache caches)
        {
            CheckComponentType(type);

            var sizeInChunk = 0;
            TypeCategory category;
            int bufferCapacity = -1;
            var memoryOrdering = TypeHash.CalculateMemoryOrdering(type, out var hasCustomMemoryOrder, caches.TypeHashCache);
            // The stable type hash is the same as the memory order if the user hasn't provided a custom memory ordering
            var stableTypeHash = !hasCustomMemoryOrder ? memoryOrdering : TypeHash.CalculateStableTypeHash(type, null, caches.TypeHashCache);
            var bloomFilterMask = ComputeBloomFilterMask(stableTypeHash);
            bool isManaged = type.IsClass;
            var isRefCounted = typeof(IRefCounted).IsAssignableFrom(type);
            var maxChunkCapacity = MaximumChunkCapacity;
            var valueTypeSize = 0;

            var maxCapacityAttribute = type.GetCustomAttribute<MaximumChunkCapacityAttribute>();
            if (maxCapacityAttribute != null && maxCapacityAttribute.Capacity < maxChunkCapacity)
                maxChunkCapacity = maxCapacityAttribute.Capacity;

            int entityOffsetIndex = s_EntityOffsetList.Length;
            int blobAssetRefOffsetIndex = s_BlobAssetRefOffsetList.Length;
            int weakAssetRefOffsetIndex = s_WeakAssetRefOffsetList.Length;
            int unityObjectRefOffsetIndex = s_UnityObjectRefOffsetList.Length;

            int elementSize = 0;
            int alignmentInBytes = 0;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (type.IsInterface)
                throw new ArgumentException($"{type} is an interface. It must be a concrete type.");
#endif
            bool hasNativeContainer = DoesComponentContainNativeContainer(type, type, caches.NestedNativeContainerCache);
            bool hasEntityReferences = false;
            bool hasBlobReferences = false;
            bool hasWeakAssetReferences = false;

            if (typeof(IComponentData).IsAssignableFrom(type) && !isManaged)
            {
                CheckIsAllowedAsComponentData(type, nameof(IComponentData), caches.AllowedComponentCache);

                category = TypeCategory.ComponentData;

                valueTypeSize = UnsafeUtility.SizeOf(type);
                alignmentInBytes = CalculateAlignmentInChunk(valueTypeSize);

                if (TypeManager.IsZeroSizeStruct(type))
                    sizeInChunk = 0;
                else
                    sizeInChunk = valueTypeSize;

                EntityRemapUtility.CalculateFieldOffsetsUnmanaged(type, out hasEntityReferences, out hasBlobReferences, out hasWeakAssetReferences, ref s_EntityOffsetList, ref s_BlobAssetRefOffsetList, ref s_WeakAssetRefOffsetList, ref s_UnityObjectRefOffsetList, caches.CalculateFieldOffsetsUnmanagedCache);
            }
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            else if (typeof(IComponentData).IsAssignableFrom(type) && isManaged)
            {
                CheckIsAllowedAsManagedComponentData(type, nameof(IComponentData), caches.NestedNativeContainerCache, out hasNativeContainer);

                category = TypeCategory.ComponentData;
                sizeInChunk = sizeof(int);
                EntityRemapUtility.HasEntityReferencesManaged(type, out var entityRefResult, out var blobRefResult, caches.HasEntityOrBlobAssetReferenceCache);

                hasEntityReferences = entityRefResult > 0;
                hasBlobReferences = blobRefResult > 0;
            }
#endif
            else if (typeof(IBufferElementData).IsAssignableFrom(type))
            {
                CheckIsAllowedAsComponentData(type, nameof(IBufferElementData), caches.AllowedComponentCache);

                category = TypeCategory.BufferData;

                valueTypeSize = UnsafeUtility.SizeOf(type);
                // TODO: Implement UnsafeUtility.AlignOf(type)
                alignmentInBytes = CalculateAlignmentInChunk(valueTypeSize);

                elementSize = valueTypeSize;

                // Empty types will always be 1 bytes in size as per language requirements
                // Check for size 1 first so we don't lookup type fields for all buffer types as it's uncommon
                if (elementSize == 1 && type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Length == 0)
                    throw new ArgumentException($"Type {type} is an IBufferElementData type, however it has no fields and is thus invalid; this will waste chunk space for no benefit. If you want an empty component, consider using IComponentData instead.");

                var capacityAttribute = (InternalBufferCapacityAttribute)type.GetCustomAttribute(typeof(InternalBufferCapacityAttribute));
                if (capacityAttribute != null)
                    bufferCapacity = capacityAttribute.Capacity;
                else
                    bufferCapacity = DefaultBufferCapacityNumerator / elementSize; // Rather than 2*cachelinesize, to make it cross platform deterministic

                sizeInChunk = sizeof(BufferHeader) + bufferCapacity * elementSize;
                EntityRemapUtility.CalculateFieldOffsetsUnmanaged(type, out hasEntityReferences, out hasBlobReferences, out hasWeakAssetReferences, ref s_EntityOffsetList, ref s_BlobAssetRefOffsetList, ref s_WeakAssetRefOffsetList, ref s_UnityObjectRefOffsetList, caches.CalculateFieldOffsetsUnmanagedCache);
            }
            else if (typeof(ISharedComponentData).IsAssignableFrom(type))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (!type.IsValueType)
                    throw new ArgumentException($"{type} is an ISharedComponentData, and thus must be a struct.");
                hasNativeContainer = DoesComponentContainNativeContainer(type, type, caches.NestedNativeContainerCache);
#endif
                valueTypeSize = UnsafeUtility.SizeOf(type);

                // Empty types will always be 1 bytes in size as per language requirements
                // Check for size 1 first so we don't lookup type fields for all buffer types as it's uncommon
                if (valueTypeSize == 1 && type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Length == 0)
                    throw new ArgumentException($"Type {type} is an ISharedComponentData type, however it has no fields and is thus invalid; this will waste chunk space for no benefit. If you want an empty component, consider using IComponentData instead.");

                category = TypeCategory.ISharedComponentData;
                isManaged = !UnsafeUtility.IsUnmanaged(type);

                if (isManaged)
                {
                    EntityRemapUtility.HasEntityReferencesManaged(type, out var entityRefResult, out var blobRefResult, caches.HasEntityOrBlobAssetReferenceCache);

                    // Managed shared components explicitly do not allow patching of entity references
                    hasEntityReferences = false;
                    hasBlobReferences = blobRefResult > 0;
                }
                else
                {
                    EntityRemapUtility.CalculateFieldOffsetsUnmanaged(type, out hasEntityReferences, out hasBlobReferences, out hasWeakAssetReferences, ref s_EntityOffsetList, ref s_BlobAssetRefOffsetList, ref s_WeakAssetRefOffsetList, ref s_UnityObjectRefOffsetList, caches.CalculateFieldOffsetsUnmanagedCache);
                }
            }
            else if (type.IsClass)
            {
                category = TypeCategory.UnityEngineObject;
                sizeInChunk = sizeof(int);
                alignmentInBytes = sizeof(int);
                hasEntityReferences = false;
                hasBlobReferences = false;

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (UnityEngineObjectType == null)
                    throw new ArgumentException(
                        $"{type} cannot be used from EntityManager. If it inherits UnityEngine.Component, you must first register TypeManager.UnityEngineObjectType or include the Unity.Entities.Hybrid assembly in your build.");
                if (!UnityEngineObjectType.IsAssignableFrom(type))
                    throw new ArgumentException($"{type} must inherit {UnityEngineObjectType}.");
#endif
            }
            else
            {
                throw new ArgumentException($"{type} is not a valid component.");
            }

            // If the type explicitly overrides entity/blob reference attributes account for that now
            CheckTypeOverrideAndUpdateHasReferences(type, caches.HasEntityOrBlobAssetReferenceCache, ref hasEntityReferences, ref hasBlobReferences);

            AddFastEqualityInfo(type, category == TypeCategory.UnityEngineObject, caches.FastEqualityLayoutInfoCache);

            int entityOffsetCount = s_EntityOffsetList.Length - entityOffsetIndex;
            int blobAssetRefOffsetCount = s_BlobAssetRefOffsetList.Length - blobAssetRefOffsetIndex;
            int weakAssetRefOffsetCount =  s_WeakAssetRefOffsetList.Length - weakAssetRefOffsetIndex;
            int unityObjectRefOffsetCount =  s_UnityObjectRefOffsetList.Length - unityObjectRefOffsetIndex;

            int writeGroupIndex = s_WriteGroupList.Length;
            int writeGroupCount = writeGroups == null ? 0 : writeGroups.Length;
            if (writeGroups != null)
            {
                foreach (var wgTypeIndex in writeGroups)
                    s_WriteGroupList.Add(wgTypeIndex);
            }

            int typeIndex = s_TypeCount;
            // Cleanup shared components are also considered cleanup components
            bool isCleanupSharedComponent = typeof(ICleanupSharedComponentData).IsAssignableFrom(type);
            bool isCleanupBufferElement = typeof(ICleanupBufferElementData).IsAssignableFrom(type);
            bool isCleanupComponent = isCleanupSharedComponent || isCleanupBufferElement || typeof(ICleanupComponentData).IsAssignableFrom(type);

            bool isEnableableComponent = typeof(IEnableableComponent).IsAssignableFrom(type);
            if (isEnableableComponent)
            {
                if (!(category == TypeCategory.ComponentData || category == TypeCategory.BufferData) || isCleanupComponent)
                    throw new ArgumentException($"IEnableableComponent is not supported for type {type}. Only IComponentData and IBufferElementData can be disabled. Cleanup components are not supported.");
            }

            bool isTemporaryBakingType = Attribute.IsDefined(type, typeof(TemporaryBakingTypeAttribute));
            bool isBakingOnlyType = Attribute.IsDefined(type, typeof(BakingTypeAttribute));
            var isIEquatable = type.GetInterfaces().Any(i => i.Name.Contains("IEquatable"));
            var isChunkSerializable = IsComponentChunkSerializable(type, category, hasEntityReferences, caches);

            if (typeIndex != 0)
            {
                if (sizeInChunk == 0)
                    typeIndex |= ZeroSizeInChunkTypeFlag;

                if (category == TypeCategory.ISharedComponentData)
                    typeIndex |= SharedComponentTypeFlag;

                if (isCleanupComponent)
                    typeIndex |= CleanupComponentTypeFlag;

                if (isCleanupSharedComponent)
                    typeIndex |= CleanupSharedComponentTypeFlag;

                if (bufferCapacity >= 0)
                    typeIndex |= BufferComponentTypeFlag;

                if (!hasEntityReferences)
                    typeIndex |= HasNoEntityReferencesFlag;

                if (hasNativeContainer)
                    typeIndex |= HasNativeContainerFlag;

                if (isManaged)
                    typeIndex |= ManagedComponentTypeFlag;

                if (isEnableableComponent)
                    typeIndex |= EnableableComponentFlag;

                if (isRefCounted)
                    typeIndex |= IRefCountedComponentFlag;

                if (isTemporaryBakingType)
                    typeIndex |= TemporaryBakingTypeFlag;

                if (isBakingOnlyType)
                    typeIndex |= BakingOnlyTypeFlag;

                if (isIEquatable)
                    typeIndex |= IEquatableTypeFlag;

                if (!isChunkSerializable)
                    typeIndex |= IsNotChunkSerializableTypeFlag;
            }

            return new TypeInfo(typeIndex, category, entityOffsetCount, entityOffsetIndex,
                memoryOrdering, stableTypeHash, bufferCapacity, sizeInChunk,
                elementSize > 0 ? elementSize : sizeInChunk, alignmentInBytes,
                maxChunkCapacity, writeGroupCount, writeGroupIndex,
                hasBlobReferences, blobAssetRefOffsetCount, blobAssetRefOffsetIndex,
                weakAssetRefOffsetCount, weakAssetRefOffsetIndex, unityObjectRefOffsetCount,
                unityObjectRefOffsetIndex, valueTypeSize, bloomFilterMask);
        }

        private struct SharedTypeIndex
        {
            public static ref TypeIndex Get(Type componentType)
            {
                return ref SharedStatic<TypeIndex>.GetOrCreate(typeof(TypeManagerKeyContext), componentType).Data;
            }
        }
#endif // #if !UNITY_DOTSRUNTIME

        private static void AddFastEqualityInfo(Type type, bool isUnityEngineObject = false, Dictionary<Type, List<FastEquality.LayoutInfo>> cache = null)
        {
#if !UNITY_DOTSRUNTIME
            if (type == null || isUnityEngineObject)
                s_FastEqualityTypeInfoList.Add(FastEquality.TypeInfo.Null);
            else
                s_FastEqualityTypeInfoList.Add(FastEquality.CreateTypeInfo(type));
#endif
        }


        private struct TypeManagerKeyContext { }
        private struct SharedTypeInfos
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedTypeInfos>();
        }
#if UNITY_DOTSRUNTIME
        private struct SharedDescendantIndices
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedDescendantIndices>();
        }
#endif
        private struct SharedDescendantCounts
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedDescendantCounts>();
        }
        private struct SharedEntityOffsetInfos
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedEntityOffsetInfos>();
        }
        private struct SharedStableTypeHashes
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedStableTypeHashes>();
        }
        private struct SharedBlobAssetRefOffsets
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedBlobAssetRefOffsets>();
        }
        private struct SharedWeakAssetRefOffsets
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedWeakAssetRefOffsets>();
        }
        private struct SharedUnityObjectRefOffsets
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedUnityObjectRefOffsets>();
        }
        private struct SharedWriteGroups
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedWriteGroups>();
        }
        private struct SharedFastEqualityTypeInfo
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedFastEqualityTypeInfo>();
        }
        private struct SharedTypeNames
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedTypeNames>();
        }
        private struct SharedTypeFullNameHashes
        {
            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, SharedTypeFullNameHashes>();
        }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private struct SharedSafetyHandle
        {
            public static readonly SharedStatic<AtomicSafetyHandle> Ref = SharedStatic<AtomicSafetyHandle>.GetOrCreate<TypeManagerKeyContext, AtomicSafetyHandle>();
        }
#endif

        // Marked as internal as this is used by StaticTypeRegistryILPostProcessor
        internal struct SharedTypeIndex<TComponent>
        {
            public static readonly SharedStatic<TypeIndex> Ref = SharedStatic<TypeIndex>.GetOrCreate<TypeManagerKeyContext, TComponent>();
        }

        private sealed class Shared_SharedComponentData_FnPtrs
        {
            private Shared_SharedComponentData_FnPtrs()
            {
            }

            public static readonly SharedStatic<IntPtr> Ref = SharedStatic<IntPtr>.GetOrCreate<TypeManagerKeyContext, Shared_SharedComponentData_FnPtrs>();
        }

#if UNITY_DOTSRUNTIME
        internal static void RegisterStaticAssemblyTypes()
        {
            throw new Exception("To be replaced by codegen");
        }

        static List<int> s_TypeDelegateIndexRanges = new List<int>();
        static List<TypeRegistry.GetBoxedEqualsFn> s_AssemblyBoxedEqualsFn = new List<TypeRegistry.GetBoxedEqualsFn>();
        static List<TypeRegistry.GetBoxedEqualsPtrFn> s_AssemblyBoxedEqualsPtrFn = new List<TypeRegistry.GetBoxedEqualsPtrFn>();
        static List<TypeRegistry.BoxedGetHashCodeFn> s_AssemblyBoxedGetHashCodeFn = new List<TypeRegistry.BoxedGetHashCodeFn>();
        static List<TypeRegistry.ConstructComponentFromBufferFn> s_AssemblyConstructComponentFromBufferFn = new List<TypeRegistry.ConstructComponentFromBufferFn>();

        internal static bool GetBoxedEquals(object lhs, object rhs, int typeIndexNoFlags)
        {
            int offset = 0;
            for (int i = 0; i < s_TypeDelegateIndexRanges.Count; ++i)
            {
                if (typeIndexNoFlags < s_TypeDelegateIndexRanges[i])
                    return s_AssemblyBoxedEqualsFn[i](lhs, rhs, typeIndexNoFlags - offset);
                offset = s_TypeDelegateIndexRanges[i];
            }

            throw new ArgumentException("No function was generated for the provided type.");
        }

        internal static bool GetBoxedEquals(object lhs, void* rhs, int typeIndexNoFlags)
        {
            int offset = 0;
            for (int i = 0; i < s_TypeDelegateIndexRanges.Count; ++i)
            {
                if (typeIndexNoFlags < s_TypeDelegateIndexRanges[i])
                    return s_AssemblyBoxedEqualsPtrFn[i](lhs, rhs, typeIndexNoFlags - offset);
                offset = s_TypeDelegateIndexRanges[i];
            }

            throw new ArgumentException("No function was generated for the provided type.");
        }

        internal static int GetBoxedHashCode(object obj, int typeIndexNoFlags)
        {
            int offset = 0;
            for (int i = 0; i < s_TypeDelegateIndexRanges.Count; ++i)
            {
                if (typeIndexNoFlags < s_TypeDelegateIndexRanges[i])
                    return s_AssemblyBoxedGetHashCodeFn[i](obj, typeIndexNoFlags - offset);
                offset = s_TypeDelegateIndexRanges[i];
            }

            throw new ArgumentException("No function was generated for the provided type.");
        }

        internal static object ConstructComponentFromBuffer(void* buffer, int typeIndexNoFlags)
        {
            int offset = 0;
            for (int i = 0; i < s_TypeDelegateIndexRanges.Count; ++i)
            {
                if (typeIndexNoFlags < s_TypeDelegateIndexRanges[i])
                    return s_AssemblyConstructComponentFromBufferFn[i](buffer, typeIndexNoFlags - offset);
                offset = s_TypeDelegateIndexRanges[i];
            }

            throw new ArgumentException("No function was generated for the provided type.");
        }

        static bool EntityBoxedEquals(object lhs, object rhs, int typeIndexNoFlags)
        {
            Assert.IsTrue(typeIndexNoFlags == 1);
            Entity e0 = (Entity)lhs;
            Entity e1 = (Entity)rhs;
            return e0.Equals(e1);
        }

        static bool EntityBoxedEqualsPtr(object lhs, void* rhs, int typeIndexNoFlags)
        {
            Assert.IsTrue(typeIndexNoFlags == 1);
            Entity e0 = (Entity)lhs;
            Entity e1 = *(Entity*)rhs;
            return e0.Equals(e1);
        }

        static int EntityBoxedGetHashCode(object obj, int typeIndexNoFlags)
        {
            Assert.IsTrue(typeIndexNoFlags == 1);
            Entity e0 = (Entity)obj;
            return e0.GetHashCode();
        }

        static object EntityConstructComponentFromBuffer(void* obj, int typeIndexNoFlags)
        {
            Assert.IsTrue(typeIndexNoFlags == 1);
            return *(Entity*)obj;
        }

        /// <summary>
        /// Registers, all at once, the type registry information generated for each assembly.
        /// </summary>
        /// <param name="registries"></param>
        internal static unsafe void RegisterAssemblyTypes(TypeRegistry[] registries)
        {
            int initializeTypeIndexOffset = s_TypeCount;
            s_TypeDelegateIndexRanges.Add(s_TypeCount);

            s_AssemblyBoxedEqualsFn.Add(EntityBoxedEquals);
            s_AssemblyBoxedEqualsPtrFn.Add(EntityBoxedEqualsPtr);
            s_AssemblyBoxedGetHashCodeFn.Add(EntityBoxedGetHashCode);
            s_AssemblyConstructComponentFromBufferFn.Add(EntityConstructComponentFromBuffer);

            // Data for Descendant count sorting
            var componentTypeSet = new HashSet<Type>();
            var descendantCountByType = new Dictionary<Type, int>();

            foreach (var typeRegistry in registries)
            {
                int typeIndexOffset = s_TypeCount;
                int entityOffsetsOffset = s_EntityOffsetList.Length;
                int blobOffsetsOffset = s_BlobAssetRefOffsetList.Length;
                int fieldInfosOffset = s_FieldInfos.Length;
                int fieldTypesOffset = s_FieldTypes.Count;
                int fieldNamesOffset = s_FieldNames.Count;

                foreach (var type in typeRegistry.Types)
                {
                    s_Types.Add(type);
                    s_TypeFullNameHashes.Add(CalculateFullNameHash(type.FullName));
                }

                foreach (var typeName in typeRegistry.TypeNames)
                {
                    var unsafeName = new UnsafeText(Encoding.UTF8.GetByteCount(typeName), Allocator.Persistent);
                    unsafeName.CopyFrom(typeName);
                    s_TypeNames.Add(unsafeName);
                }

                foreach (var type in typeRegistry.FieldTypes)
                    s_FieldTypes.Add(type);

                foreach (var fieldName in typeRegistry.FieldNames)
                    s_FieldNames.Add(fieldName);

                s_EntityOffsetList.AddRange(typeRegistry.EntityOffsetsPtr, typeRegistry.EntityOffsetsCount);
                s_BlobAssetRefOffsetList.AddRange(typeRegistry.BlobAssetReferenceOffsetsPtr, typeRegistry.BlobAssetReferenceOffsetsCount);
                {
                    var typeInfoOffset = ((TypeInfo*)s_TypeInfos.GetUnsafePtr()) + s_TypeCount;
                    UnsafeUtility.MemCpy(typeInfoOffset, typeRegistry.TypeInfosPtr, typeRegistry.TypeInfosCount * UnsafeUtility.SizeOf<TypeInfo>());

                    TypeIndex* newTypeIndices = stackalloc TypeIndex[typeRegistry.TypeInfosCount];
                    for (int i = 0; i < typeRegistry.TypeInfosCount; ++i)
                    {
                        TypeInfo* pTypeInfo = ((TypeInfo*)s_TypeInfos.GetUnsafePtr()) + i + s_TypeCount;
                        *(&pTypeInfo->TypeIndex.Value) += typeIndexOffset;
                        *(&pTypeInfo->EntityOffsetStartIndex) += entityOffsetsOffset;
                        *(&pTypeInfo->BlobAssetRefOffsetStartIndex) += blobOffsetsOffset;

                        // we will adjust these values when we recalculate the writegroups below
                        *(&pTypeInfo->WriteGroupCount) = 0;
                        *(&pTypeInfo->WriteGroupStartIndex) = -1;

                        // Using TryAdd as we might register the same RegisterGenericComponentType from multiple assemblies
                        // This will result in multiple TypeInfo data being stored for the same type but that is fine so we can keep our indices consistent and
                        // all mappings will look up the last set, _identical_ entry. This keeps incremental builds quick by not needing global scope
                        // of all types, but when we do provide a global pass we can consolidate the typeinfos.
                        // Note when need to make StableTypeHash map to a list of indices when we add support for removing assembly typeinfo
                        // so we can remove type information gracefully
                        if (!s_StableTypeHashToTypeIndex.TryAdd(pTypeInfo->StableTypeHash, pTypeInfo->TypeIndex))
                            s_StableTypeHashToTypeIndex[pTypeInfo->StableTypeHash] = pTypeInfo->TypeIndex;

                        newTypeIndices[i] = pTypeInfo->TypeIndex;


                        // Set for processing Descendant count sorting
                        componentTypeSet.Add(pTypeInfo->Type);
                    }
                    // Setup our new TypeIndices into the appropriately types SharedTypeIndex<TComponent> shared static
                    typeRegistry.SetSharedTypeIndices((int*)newTypeIndices, typeRegistry.TypeInfosCount);
                    s_TypeCount += typeRegistry.TypeInfosCount;
                }

                for (int i = 0; i < typeRegistry.FieldInfos.Length; ++i)
                {
                    var fieldInfo = typeRegistry.FieldInfos[i];
                    fieldInfo.FieldNameIndex += fieldNamesOffset;
                    fieldInfo.FieldTypeIndex += fieldTypesOffset;

                    s_FieldInfos.Add(fieldInfo);
                }

                for (int i = 0; i < typeRegistry.FieldInfoLookups.Length; ++i)
                {
                    var lookup = typeRegistry.FieldInfoLookups[i];

                    lookup.FieldTypeIndex += fieldTypesOffset;
                    lookup.Index += fieldInfosOffset;
                    var fieldType = s_FieldTypes[lookup.FieldTypeIndex];

                    if (!s_TypeToFieldInfosMap.ContainsKey(fieldType))
                        s_TypeToFieldInfosMap.Add(fieldType, lookup);
                }

                if (typeRegistry.Types.Length > 0)
                {
                    s_TypeDelegateIndexRanges.Add(s_TypeCount);

                    s_AssemblyBoxedEqualsFn.Add(typeRegistry.BoxedEquals);
                    s_AssemblyBoxedEqualsPtrFn.Add(typeRegistry.BoxedEqualsPtr);
                    s_AssemblyBoxedGetHashCodeFn.Add(typeRegistry.BoxedGetHashCode);
                    s_AssemblyConstructComponentFromBufferFn.Add(typeRegistry.ConstructComponentFromBuffer);
                }

                // Register system types
                RegisterAssemblySystemTypes(typeRegistry);
            }

            // This sorts and fills the Descendant counts and indirection for DOTS Runtime
            var componentTypes = new Type[componentTypeSet.Count];
            componentTypeSet.CopyTo(componentTypes);

            var typeTreeNodes = BuildTypeTree(componentTypes, componentTypeSet, descendantCountByType);

            foreach (var typeNode in typeTreeNodes)
            {
                var typeIndex = GetTypeIndex(typeNode.Type).Index;
                s_DescendantCounts[typeNode.IndexInTypeArray] = descendantCountByType[typeNode.Type];
                s_DescendantIndex[typeIndex] = typeNode.IndexInTypeArray;
            }

            GatherAndInitializeWriteGroups(initializeTypeIndexOffset, registries);
        }

        static unsafe void GatherAndInitializeWriteGroups(int typeIndexOffset, TypeRegistry[] registries)
        {
            // A this point we have loaded all Types and know all TypeInfos. Now we need to
            // go back through each assembly, determine if a type has a write group, and if so
            // translate the Type of the writegroup component to a TypeIndex. But, we must do this incrementally
            // for all assemblies since AssemblyA can add to the writegroup list of a type defined in AssemblyB.
            // Once we have a complete mapping, generate the s_WriteGroup array and fixup all writegroupStart
            // indices in our type infos

            // We create a list of hashmaps here since we can't put a NativeParallelHashMap inside of a NativeParallelHashMap in debug builds due to DisposeSentinels being managed
            var hashSetList = new List<NativeParallelHashMap<TypeIndex, byte>>();
            NativeParallelHashMap<TypeIndex, int> writeGroupMap = new NativeParallelHashMap<TypeIndex, int>(1024, Allocator.Temp);
            foreach (var typeRegistry in registries)
            {
                for (int i = 0; i < typeRegistry.TypeInfosCount; ++i)
                {
                    var typeInfo = typeRegistry.TypeInfosPtr[i];
                    if (typeInfo.WriteGroupCount > 0)
                    {
                        var typeIndex = new TypeIndex { Value = typeInfo.TypeIndex.Value + typeIndexOffset };

                        for (int wgIndex = 0; wgIndex < typeInfo.WriteGroupCount; ++wgIndex)
                        {
                            var targetType = typeRegistry.WriteGroups[typeInfo.WriteGroupStartIndex + wgIndex];
                            // targetType isn't necessarily from this assembly (it could be from one of its references)
                            // so lookup the actual typeIndex since we loaded all assembly types above
                            var targetTypeIndex = GetTypeIndex(targetType);

                            if (!writeGroupMap.TryGetValue(targetTypeIndex, out var targetSetIndex))
                            {
                                targetSetIndex = hashSetList.Count;
                                writeGroupMap.Add(targetTypeIndex, targetSetIndex);
                                hashSetList.Add(new NativeParallelHashMap<TypeIndex, byte>(typeInfo.WriteGroupCount, Allocator.Temp));
                            }
                            var targetSet = hashSetList[targetSetIndex];
                            targetSet.TryAdd(typeIndex, 0); // We don't have a NativeSet, so just push 0
                        }
                    }
                }

                typeIndexOffset += typeRegistry.TypeInfosCount;
            }

            using (var keys = writeGroupMap.GetKeyArray(Allocator.Temp))
            {
                foreach (var typeIndex in keys)
                {
                    var index = typeIndex.Index;
                    var typeInfo = (TypeInfo*)s_TypeInfos.GetUnsafePtr() + index;

                    var valueIndex = writeGroupMap[typeIndex];
                    var valueSet = hashSetList[valueIndex];
                    using (var values = valueSet.GetKeyArray(Allocator.Temp))
                    {
                        *(&typeInfo->WriteGroupStartIndex) = s_WriteGroupList.Length;
                        *(&typeInfo->WriteGroupCount) = values.Length;

                        foreach (var ti in values)
                            s_WriteGroupList.Add(ti);
                    }

                    valueSet.Dispose();
                }
            }
            writeGroupMap.Dispose();
        }

        static ulong GetEntityStableTypeHash()
        {
            throw new Exception("This call should have been replaced by codegen");
        }

#endif
    }

#if UNITY_DOTSRUNTIME
    internal unsafe class TypeRegistry
    {
        // TODO: Have Burst generate a native function ptr we can invoke instead of using a delegate
        public delegate bool GetBoxedEqualsFn(object lhs, object rhs, int typeIndexNoFlags);
        public unsafe delegate bool GetBoxedEqualsPtrFn(object lhs, void* rhs, int typeIndexNoFlags);
        public delegate int BoxedGetHashCodeFn(object obj, int typeIndexNoFlags);
        public unsafe delegate object ConstructComponentFromBufferFn(void* buffer, int typeIndexNoFlags);
        public unsafe delegate void SetSharedTypeIndicesFn(int* typeInfoArray, int count);
        public delegate Attribute[] GetSystemAttributesFn(Type system);
        public delegate object CreateSystemFn(Type system);

        public GetBoxedEqualsFn BoxedEquals;
        public GetBoxedEqualsPtrFn BoxedEqualsPtr;
        public BoxedGetHashCodeFn BoxedGetHashCode;
        public ConstructComponentFromBufferFn ConstructComponentFromBuffer;
        public SetSharedTypeIndicesFn SetSharedTypeIndices;
        public GetSystemAttributesFn GetSystemAttributes;
        public CreateSystemFn CreateSystem;

#pragma warning disable 0649
        public string AssemblyName;

        public TypeManager.TypeInfo* TypeInfosPtr;
        public int TypeInfosCount;
        public int* EntityOffsetsPtr;
        public int EntityOffsetsCount;
        public int* BlobAssetReferenceOffsetsPtr;
        public int BlobAssetReferenceOffsetsCount;

        public Type[] Types;
        public string[] TypeNames;
        public Type[] WriteGroups;

        public Type[] SystemTypes;
        public WorldSystemFilterFlags[] SystemFilterFlags;
        public string[] SystemTypeNames;
        public int[] SystemTypeSizes;
        public long[] SystemTypeHashes;
        public int[] SystemTypeFlags;

        public delegate int IRefCountedDelegate(IntPtr iRefCountedSelf);

        public FunctionPointer<IRefCountedDelegate>[] iRefCountedRetainFunctions;
        public FunctionPointer<IRefCountedDelegate>[] iRefCountedReleaseFunctions;

        public Type[] FieldTypes;
        public string[] FieldNames;
        public TypeManager.FieldInfo[] FieldInfos;
        public FieldInfoLookup[] FieldInfoLookups;

        public struct FieldInfoLookup
        {
            public FieldInfoLookup(int typeIndex, int infoIndex, int count)
            {
                FieldTypeIndex = typeIndex;
                Index = infoIndex;
                Count = count;
            }

            public int FieldTypeIndex;
            public int Index;
            public int Count;
        }
#pragma warning restore 0649
    }
#endif
}
