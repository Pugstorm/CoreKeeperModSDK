using System;
using Unity.Entities;

namespace Unity.NetCode
{
    /// <summary>
    /// Temporary type, used to upgrade to new component type, to be removed before final 1.0
    /// </summary>
    [Obsolete("CommandTargetComponent has been deprecated. Use CommandTarget instead (UnityUpgradable) -> CommandTarget", true)]
    public struct CommandTargetComponent : IComponentData
    {}

    /// <summary>
    /// Component added to all <see cref="NetworkStreamConnection"/>, stores a reference to the entity
    /// where commands should be read from (client) or written to (server).
    /// It is mandatory to set a valid reference to the <see cref="targetEntity"/> in order to receive client
    /// commands if:
    /// <para>- you are not using the <see cref="AutoCommandTarget"/>.</para>
    /// <para>- you want to support thin-clients (because <see cref="AutoCommandTarget"/> does not work in that case)
    /// The use of <see cref="AutoCommandTarget"/> and CommandTarget is complementary. I.e. They can both be used
    /// at the same time.</para>
    /// </summary>
    /// <remarks>
    /// The target entity must have at least one `ICommandData` component on it.
    /// </remarks>
    public struct CommandTarget : IComponentData
    {
        /// <inheritdoc cref="CommandTarget"/>
        public Entity targetEntity;
    }
}
