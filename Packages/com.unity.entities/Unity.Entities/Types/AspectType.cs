using System;
using Unity.Collections;

namespace Unity.Entities
{
    /// <summary>
    /// Native friendly container for managed <see cref="Type"/> derived from <see cref="IAspect"/>.
    /// </summary>
    public struct AspectType : IEquatable<AspectType>
    {
        /// <summary>
        /// The type index of the aspect in the <see cref="TypeManager"/>.
        /// </summary>
        public int TypeIndex;

        /// <summary>
        /// Create an <see cref="AspectType"/> from a type index.
        /// </summary>
        /// <param name="typeIndex">The aspect type index.</param>
        /// <returns>A new AspectType instance.</returns>
        public static AspectType FromTypeIndex(int typeIndex)
        {
            return new AspectType { TypeIndex = typeIndex };
        }

        /// <summary>
        /// Create an <see cref="AspectType"/> from a managed <see cref="Type"/>.
        /// </summary>
        /// <remarks>The managed <see cref="Type"/> must derive from <see cref="IAspect"/> to be valid.</remarks>
        /// <param name="type">The managed <see cref="Type"/> that derives from <see cref="IAspect"/>.</param>
        public AspectType(Type type)
        {
            TypeIndex = TypeManager.GetAspectTypeIndex(type);
        }

        /// <summary>
        /// Retrieve the managed <see cref="Type"/>.
        /// </summary>
        /// <returns>The managed <see cref="Type"/>.</returns>
        [ExcludeFromBurstCompatTesting("Returns managed type")]
        public Type GetManagedType()
        {
            return TypeManager.GetAspectType(TypeIndex);
        }

        /// <summary>
        /// Implicit conversion from managed <see cref="Type"/> to <see cref="AspectType"/>.
        /// </summary>
        /// <param name="type">The managed <see cref="Type"/> that derives from <see cref="IAspect"/>.</param>
        /// <returns>Returns the new AspectType.</returns>
        [ExcludeFromBurstCompatTesting("Returns managed type")]
        public static implicit operator AspectType(Type type)
        {
            return new AspectType(type);
        }

        /// <summary>
        /// Operator less-than by type index
        /// </summary>
        /// <param name="lhs">AspectType on the left side of the operation</param>
        /// <param name="rhs">AspectType on the right side of the operation</param>
        /// <returns>True if lhs has a lower type index than rhs</returns>
        public static bool operator <(AspectType lhs, AspectType rhs)
        {
            return lhs.TypeIndex < rhs.TypeIndex;
        }

        /// <summary>
        /// Operator greater-than by type index
        /// </summary>
        /// <param name="lhs">AspectType on the left side of the operation</param>
        /// <param name="rhs">AspectType on the right side of the operation</param>
        /// <returns>True if lhs has a greater type index than rhs</returns>
        public static bool operator >(AspectType lhs, AspectType rhs)
        {
            return rhs < lhs;
        }

        /// <summary>
        /// Operator equal by type index
        /// </summary>
        /// <param name="lhs">AspectType on the left side of the operation</param>
        /// <param name="rhs">AspectType on the right side of the operation</param>
        /// <returns>True if lhs has the same type index as rhs</returns>
        public static bool operator ==(AspectType lhs, AspectType rhs)
        {
            return lhs.TypeIndex == rhs.TypeIndex;
        }

        /// <summary>
        /// Operator not-equal by type index
        /// </summary>
        /// <param name="lhs">AspectType on the left side of the operation</param>
        /// <param name="rhs">AspectType on the right side of the operation</param>
        /// <returns>True if lhs does not have the same type index as rhs</returns>
        public static bool operator !=(AspectType lhs, AspectType rhs)
        {
            return lhs.TypeIndex != rhs.TypeIndex;
        }
        
        /// <summary>
        /// Name of the aspect type
        /// </summary>
        /// <returns>Name of the aspect type</returns>
        public override string ToString()
        {
            if (TypeIndex == 0)
                return "None";

            return GetManagedType()?.Name ?? string.Empty;
        }

        /// <summary>
        /// Test if this AspectType is equal to another AspectType
        /// </summary>
        /// <param name="other">AspectType to test equality with</param>
        /// <returns>True if this AspectType is equal to another AspectType</returns>
        public bool Equals(AspectType other)
        {
            return TypeIndex == other.TypeIndex;
        }

        /// <summary>
        /// Test if this object is equal to another object
        /// </summary>
        /// <param name="obj">Object to test equality with</param>
        /// <returns>True if this object is equal to another object</returns>
        public override bool Equals(object obj)
        {
            return obj is AspectType aspectType ? Equals(aspectType) : false;
        }

        /// <summary>
        /// Get hash code for this AspectType
        /// </summary>
        /// <returns>hash code for this AspectType</returns>
        public override int GetHashCode()
        {
            return TypeIndex * 5813;
        }
    }
}
