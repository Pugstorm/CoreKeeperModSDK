using System;

namespace Unity.Entities
{
    /// <summary>
    /// Controls how Transform components on GameObjects are converted to entity data.
    /// </summary>
    /// <remarks>
    /// These flags help to reduce the number of unnecessary transform components in baked entities based on their
    /// intended use at runtime.
    ///
    /// The Dynamic flag replicates as close as possible the GameObject data and structure.
    /// These flags are used whenever an entity is requested during conversion, for example, via
    /// <see cref="IBaker.GetEntity"/>. Bakers for default GameObject components add the appropriate
    /// TransformUsageFlags too. For example, in the case of the baker for MeshRenderer, Renderable is added as a
    /// TransformUsageFlag.
    ///
    /// More than one baker can indicate different TransformUsageFlags for the same entity. All those flags are combined
    /// together before the transform components are added to the entity. For example if a baker requests for an entity
    /// to be Dynamic and another baker requests for the same entity to be in WorldSpace, the entity is considered
    /// to be Dynamic and in WorldSpace when the transform components are added to the entity.
    ///
    /// These are a few examples on how to use TransformUsageFlags can help to reduce the number of unnecessary
    /// transforms components on entities:
    ///
    /// If you have a GameObject representing a building and that building contains a child GameObject that
    /// is a window (these entities aren't going to move at runtime). If both entities are only marked as Renderable,
    /// then they don't need to be in a hierarchy and all their transform information can be combined in a
    /// <see cref="Unity.Transforms.LocalToWorld"/> component (in WorldSpace). In this case both entities only have a <see cref="Unity.Transforms.LocalToWorld"/>
    /// component and they don't have <see cref="Unity.Transforms.LocalTransform"/> and <see cref="Unity.Transforms.Parent"/> unnecessarily.
    ///
    /// In another example, the same window GameObject could be part of a ship instead of a building. In that case,
    /// the ship is marked as Dynamic and the window is still Renderable. The window entity ends up
    /// with all the required transform components to follow the ship around when it sails (<see cref="Unity.Transforms.LocalToWorld"/>,
    /// <see cref="Unity.Transforms.LocalTransform"/> and <see cref="Unity.Transforms.Parent"/>).
    ///
    /// A building GameObject could have a helicopter GameObject on the roof (as a child). In this case the building is
    /// still Renderable, but the helicopter is marked as Dynamic because it can take off. The helicopter has the
    /// transform components to be moved (<see cref="Unity.Transforms.LocalToWorld"/>, <see cref="Unity.Transforms.LocalTransform"/>), but it isn't
    /// parented to the building and its transform data is in world space.
    ///
    /// In the case of a ship with a helicopter, both are marked as dynamic and the helicopter entity is a
    /// child of the ship, so it follows the ship around when it sails. In this particular case when the helicopter
    /// takes off, the hierarchy needs to be broken manually at runtime and the transform data converted to world
    /// space.
    ///
    /// If the helicopter can shoot some bullets, the bullet entity prefab will automatically be marked as Dynamic so the instances can be placed in the world.
    ///
    /// There is also a case where an Entity might be stripped out from the final world during baking. This happens
    /// when there is no baker adding a TransformUsageFlag to it (TranformUsageFlags.None counts as adding a
    /// TransformUsageFlag). An example of this is a GameObject that's created in the Editor Hierarchy to group their
    /// children at authoring time for organizational purposes, but has no use at runtime. In this case, the children
    /// are moved to world space. There is an exception to this stripping rule, if an entity that has no
    /// TransformUsageFlags has a Dynamic parent and Dynamic children, then that entity is considered Dynamic as
    /// well and it isn't stripped out.
    ///</remarks>

    [Flags]
    public enum TransformUsageFlags : int
    {
        /// <summary>
        /// Specifies that the entity doesn't need transform components.
        /// </summary>
        /// <remarks>
        /// Unless something else requests other flags, this entity doesn't have any transform related components and isn't part of a hierarchy.
        ///
        /// This doesn't affect its membership in any <see cref="LinkedEntityGroup"/> components that might be created
        /// based on the source GameObject hierarchy.
        /// </remarks>
        None = 0,

        /// <summary>
        /// Indicates that an entity requires the necessary transform components to be rendered (<see cref="Unity.Transforms.LocalToWorld"/>),
        /// but it doesn't require the transform components needed to move the entity at runtime.
        /// </summary>
        /// <remarks>
        /// Renderable entities are placed in WorldSpace if none of their parents in the hierarchy are Dynamic.
        /// </remarks>
        Renderable = 1,

        /// <summary>
        /// Indicates that an entity requires the necessary transform components to be moved at runtime (<see cref="Unity.Transforms.LocalTransform"/>, <see cref="Unity.Transforms.LocalToWorld"/>).
        /// </summary>
        /// <remarks>
        /// Renderable children of a Dynamic entity are also treated as Dynamic and they receive a <see cref="Unity.Transforms.Parent"/> component.
        /// A Dynamic usage also implies Renderable, therefore there you don't need to indicate that a Dynamic entity is
        /// also Renderable.
        /// </remarks>
        Dynamic = 1 << 1,

        /// <summary>
        /// Indicates that an entity needs to be in world space, even if they have a Dynamic entity as a parent.
        /// </summary>
        /// <remarks>
        /// This means that an entity doesn't have a <see cref="Unity.Transforms.Parent"/> component and all their
        /// transform component data is baked in world space.
        /// A WorldSpace usage implies Renderable but not Dynamic, but Dynamic can be used with the WorldSpace flag.
        /// </remarks>
        WorldSpace = 1 << 2,

        /// <summary>
        /// Indicates that an entity requires transform components to represent non uniform scale.
        /// </summary>
        /// <remarks>
        /// For Dynamic entities, all the scale information is stored in a <see cref="Unity.Transforms.PostTransformMatrix"/> component.
        /// For Renderable only entities, the scale information is combined into the <see cref="Unity.Transforms.LocalToWorld"/> component.
        /// If a GameObject contains a non uniform scale, this flag is considered implicitly for Renderable and
        /// Dynamic entities.
        /// </remarks>
        NonUniformScale = 1 << 3,

        /// <summary>
        /// Indicates that you want to take full manual control over the transform conversion of an entity.
        /// </summary>
        /// <remarks>
        /// This flag is an override: when it is set, all other flags are ignored. Baking doesn't
        /// add any transform related components to the entity. The entity doesn't have a parent, and it doesn't
        /// have any children attached to it.
        /// </remarks>
        ManualOverride = 1 << 4,
    }

    [Flags]
    internal enum RuntimeTransformComponentFlags : int
    {
        None = 0,

        LocalToWorld = 1,
        LocalTransform = 1 << 1,
        RequestParent = 1 << 2,
        PostTransformMatrix = 1 << 3,

        ManualOverride =  1 << 4,
    }

    /// <summary>
    /// Stores a set of TransformUsageFlags as counters for each bit. This way TransformUsageFlags can be added and removed reliably.
    /// </summary>
    unsafe struct TransformUsageFlagCounters : IEquatable<TransformUsageFlagCounters>
    {
        const int ManualOverrideIndex = 4;
        const int Length = 5;
        fixed int Counts[Length];
        int       IsUsed;

        public void Add(TransformUsageFlags value)
        {
            IsUsed++;
            for (int i = 0; i != Length; i++)
                Counts[i] += ((int)value & (1 << i)) != 0 ? 1 : 0;
        }

        public void Remove(TransformUsageFlags value)
        {
            IsUsed--;
            for (int i = 0; i != Length; i++)
                Counts[i] -= ((int)value & (1 << i)) != 0 ? 1 : 0;
        }

        public void Add(in TransformUsageFlagCounters value)
        {
            IsUsed += value.IsUsed;
            for (int i = 0; i != Length; i++)
                Counts[i] += value.Counts[i];
        }

        public void Remove(in TransformUsageFlagCounters value)
        {
            IsUsed -= value.IsUsed;
            for (int i = 0; i != Length; i++)
                Counts[i] -= value.Counts[i];
        }

        public bool Equals(TransformUsageFlagCounters value)
        {
            if (IsUsed != value.IsUsed)
                return false;

            for (int i = 0; i != Length; i++)
            {
                if (Counts[i] != value.Counts[i])
                    return false;
            }

            return true;
        }

        public TransformUsageFlags Flags
        {
            get
            {
                int flags = 0;
                for (int i = 0; i != Length; i++)
                    flags |= Counts[i] != 0 ? 1 << i : 0;
                return (TransformUsageFlags) flags;
            }
        }

        public bool HasManualOverrideFlag()
        {
            return Counts[ManualOverrideIndex] > 0;
        }

        /// <summary>
        /// TransformUsage.None is different from Unused
        /// TransformUsage.None simply means that no transforms are required (For example, a manager singleton)
        /// IsUnused means that there is no valid reference to this entity, hence the entity shouldn't exist in the game world.
        /// </summary>
        public bool IsUnused => IsUsed == 0;
    }
}
