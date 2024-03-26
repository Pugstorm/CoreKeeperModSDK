using Unity.Entities;

namespace Unity.NetCode
{
    /// <summary>
    /// Singleton entity that allow to configure the NetCode LagCompensation system.
    /// If the singleton does not exist the PhysicsWorldHistory system will not run.
    /// If you want to use PhysicsWorldHistory in a prediction system the config must
    /// exist in both client and server worlds, but in the client world HistorySize can
    /// be different from the server - usually 1 is enough on the client.
    /// </summary>
    public struct LagCompensationConfig : IComponentData
    {
        /// <summary>
        /// The number of physics world states that are backed up on the server. This cannot be more than the maximum capacity, leaving it at zero will give you the default which is max capacity.
        /// </summary>
        public int ServerHistorySize;
        /// <summary>
        /// The number of physics world states that are backed up on the client. This cannot be more than the maximum capacity, leaving it at zero will give you the default which is one.
        /// </summary>
        public int ClientHistorySize;
    }
}
