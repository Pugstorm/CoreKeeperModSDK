using Unity.Entities;

namespace Unity.NetCode
{
    /// <summary>
    /// <para>Component that automates command "reading and sending" (for clients) or "writing, using, and broadcasting" (for the server).</para>
    /// <para>When the AutoCommandTarget component is <see cref="Enabled"/>, the entity is considered as an input source
    /// for <see cref="ICommandData"/>'s for the client and all non empty command buffers present on the entity are serialized
    /// into the <see cref="OutgoingCommandDataStreamBuffer"/>, along with the id of the ghost they
    /// are sent to.</para> /// <para>On the server side, when a command is deserialized from the <see cref="IncomingCommandDataStreamBuffer"/>,
    /// the corresponding entity is looked up, and if the AutoCommandTarget component is enabled, the commands are added to
    /// the corresponding input command buffer.</para>
    /// </summary>
    /// <remarks>
    /// To use the AutoCommandTarget, the target entity must have a <see cref="GhostOwner"/>.
    /// </remarks>
    [DontSupportPrefabOverrides]
    [GhostComponent(SendDataForChildEntity = true)]
    public struct AutoCommandTarget : IComponentData
    {
        /// <summary>
        /// Enabled/Disable the current entity from sending and receiving commands.
        /// Multiple entities can be enabled at the same time.
        /// </summary>
        [GhostField] public bool Enabled;
    }
}
