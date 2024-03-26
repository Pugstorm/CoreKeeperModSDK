namespace Unity.NetCode
{
    /// <summary>
    /// The message types sent by NetCode.
    /// </summary>
    public enum NetworkStreamProtocol
    {
        /// <summary>
        /// A packet that contains an input command. Sent always from client to server.
        /// </summary>
        Command,
        /// <summary>
        /// The simulation snapshot. Sent from server to client
        /// </summary>
        Snapshot,
        /// <summary>
        /// A message that contains a single RPC. Can be sent by both client and server
        /// </summary>
        Rpc
    }
}
