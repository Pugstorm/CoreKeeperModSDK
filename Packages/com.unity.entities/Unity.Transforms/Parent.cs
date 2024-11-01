using System;
using Unity.Entities;

namespace Unity.Transforms
{
    /// <summary>
    /// This component specifies the parent entity in a transform hierarchy.
    /// </summary>
    /// <remarks>
    /// If present, this entity's transform is implicitly specified relative to the parent's transform rather than in world-space.
    ///
    /// Add or remove this attribute to your code in order to add, change, or remove a parent/child
    /// relationship. The corresponding <see cref="Child"/> component is automatically added by the <see cref="ParentSystem"/>.
    ///
    /// When adding or modifying this component, add and update the corresponding <see cref="LocalTransform"/> component.
    /// </remarks>
    /// <seealso cref="Child"/>
    [Serializable]
    public struct Parent : IComponentData
    {
        /// <summary>
        /// The parent entity.
        /// </summary>
        /// <remarks>This field must refer to a valid entity. Root level entities should not use <see cref="Entity.Null"/>;
        /// rather, they should not have the <see cref="Parent"/> component at all.</remarks>
        public Entity Value;
    }

    /// <summary>
    /// Utility component used by the <see cref="ParentSystem"/> to detect changes to an entity's <see cref="Parent"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="ParentSystem"/> automatically adds and manages this component.  You shouldn't
    /// add, remove, or modify it in your code.
    /// </remarks>
    [Serializable]
    public struct PreviousParent : ICleanupComponentData
    {
        /// <summary>
        /// The previous parent entity
        /// </summary>
        public Entity Value;
    }

    /// <summary>
    /// Contains a buffer of all elements which have assigned this entity as their <see cref="Parent"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="ParentSystem"/> automatically adds and manages this component and its contents. You can read this
    /// list, but you shouldn't add or remove buffer elements.
    ///
    /// When an entity with this component is destroyed, the <see cref="ParentSystem"/> will automatically remove the
    /// <see cref="Parent"/> components from each child entity.
    /// </remarks>
    [Serializable]
    [InternalBufferCapacity(8)]
    public struct Child : ICleanupBufferElementData
    {
        /// <summary>
        /// A child entity
        /// </summary>
        public Entity Value;
    }
}
