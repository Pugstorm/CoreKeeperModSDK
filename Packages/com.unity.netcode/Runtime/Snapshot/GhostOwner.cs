using System;
using Unity.Entities;

namespace Unity.NetCode
{
    /// <summary>
    /// Temporary type, used to upgrade to new component type, to be removed before final 1.0
    /// </summary>
    [Obsolete("GhostOwnerComponent has been deprecated. Use GhostOwner instead (UnityUpgradable) -> GhostOwner", true)]
    [DontSupportPrefabOverrides]
    public struct GhostOwnerComponent : IComponentData
    {}

    /// <summary>
    /// The GhostOwnerComponent is an optional component that can be added to a ghost to create a bond/relationship in
    /// between an entity and a specific client (for example, the client who spawned that entity, a bullet, the player entity).
    /// It is usually added to predicted ghost (see <see cref="PredictedGhost"/>) but can also be present on the interpolated
    /// ones.
    /// <para>
    /// It is mandatory to add a <see cref="GhostOwner"/> in the following cases:
    /// <para>- When a ghost is configured to be owner-predicted <see cref="GhostMode"/>, because it is necessary to distinguish in between who
    /// is predicting (the owner) and who is interpolating the ghost.
    /// </para>
    /// <para>- If you want to enable remote player prediction (see <see cref="ICommandData"/>) or, in general, to allow sending data
    /// based on ownership the <see cref="SendToOwnerType.SendToOwner"/>.
    /// </para>
    /// <para>- If you want to use the <see cref="AutoCommandTarget"/> feature.</para>
    /// </para>
    /// </summary>
    [DontSupportPrefabOverrides]
    [GhostComponent(SendDataForChildEntity = true)]
    public struct GhostOwner : IComponentData
    {
        /// <summary>
        /// The <see cref="NetworkId"/> of the client the entity is associated with.
        /// </summary>
        [GhostField] public int NetworkId;
    }

    /// <summary>
    /// An enableable tag component used to track if a ghost with an owner is owned by the local host or not.
    /// This is enabled for all ghosts on the server and for ghosts where the ghost owner network id matches the connection id on the client.
    /// </summary>
    public struct GhostOwnerIsLocal : IComponentData, IEnableableComponent
    {}
}
