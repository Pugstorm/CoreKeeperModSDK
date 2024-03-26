using Unity.Entities;

namespace Unity.NetCode
{
    /// <summary>
    /// The NetworkProtocolVersion is a singleton entity that is automatically created by the <see cref="NetworkStreamReceiveSystem"/>
    /// and that is used to verify client and server compatibility.
    /// <para>
    /// The protocol version is composed by different part:
    /// <para>- The NetCode package version.</para>
    /// <para>- A user defined <see cref="GameProtocolVersion"/> game version, that identify the version of your game</para>
    /// <para>- A unique hash of all the <see cref="IRpcCommand"/> and <see cref="ICommandData"/> that is used to verify both client and server
    /// recognize the same rpc and command and that can serialize/deserialize them in the same way</para>
    /// <para>- A unique hash of all the replicated <see cref="IComponentData"/> and <see cref="IBufferElementData"/> that is used to verify
    /// both client and server can serialize/deserialize all the replicated component present in the ghosts</para>
    /// </para>
    /// When a client tries to connect to the server, as part of the initial handshake, they exchange their protocol version
    /// to validate they are both using same version. If the version mismatch, the connection is forcibly closed.
    /// </summary>
    public struct NetworkProtocolVersion : IComponentData
    {
        /// <summary>
        /// The integer used to determine a compatible version of the NetCode package.
        /// </summary>
        public const int k_NetCodeVersion = 1;
        /// <summary>
        /// The NetCode package version
        /// </summary>
        public int NetCodeVersion;
        /// <summary>
        /// The user specific game version the server and client are using. 0 by default, unless the <see cref="GameProtocolVersion"/> is used
        /// to customise it.
        /// </summary>
        public int GameVersion;
        /// <summary>
        /// A unique hash computed of all the RPC and commands, used to check if the server and client have the same messages and
        /// with compatible data and serialization.
        /// </summary>
        public ulong RpcCollectionVersion;
        /// <summary>
        /// A unique hash calculated on all the serialized components that can be used to check if the client
        /// can propertly decode the snapshots.
        /// </summary>
        public ulong ComponentCollectionVersion;
    }

    /// <summary>
    /// The game specific version to use for protcol validation when the client and server connects.
    /// If a singleton with this component does not exist 0 will be used instead.
    /// Protocol validation will still validate the <see cref="NetworkProtocolVersion.NetCodeVersion"/>,
    /// <see cref="NetworkProtocolVersion.RpcCollectionVersion"/> and <see cref="NetworkProtocolVersion.ComponentCollectionVersion"/>.
    /// </summary>
    public struct GameProtocolVersion : IComponentData
    {
        /// <summary>
        /// An user defined integer that identify the current game version.
        /// </summary>
        public int Version;
    }
}
