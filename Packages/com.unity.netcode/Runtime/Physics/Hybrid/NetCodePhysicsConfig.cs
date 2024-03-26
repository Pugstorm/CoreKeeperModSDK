using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

namespace Unity.NetCode
{
    /// <summary>
    /// Component used to enable predicted physics automatic world changing(<see cref="PredictedPhysicsNonGhostWorld"/>) and lag compensation (<see cref="EnableLagCompensation"/>) and
    /// tweak their settings.
    /// At conversion time, a singleton entity is added to the scene/subscene if either one of, or both of the features are enabled, and
    /// the <see cref="PredictedPhysicsNonGhostWorld"/>, <see cref="EnableLagCompensation"/> components are automatically added to it based on these settings.
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL(Authoring.HelpURLs.NetCodePhysicsConfig)]
    public sealed class NetCodePhysicsConfig : MonoBehaviour
    {
        /// <summary>
        /// Set to true to enable the use of the LagCompensation system. Server and Client will start recording the physics world state in the PhysicsWorldHistory buffer,
        /// which size can be further configured for by changing the ServerHistorySize and ClientHistorySize properites;
        /// </summary>
        [Tooltip("Enable/Disable the LagCompensation system. Server and Client will start recording the physics world state in the PhysicsWorldHistory buffer")]
        public bool EnableLagCompensation;
        /// <summary>
        /// The number of physics world states that are backed up on the server. This cannot be more than the maximum capacity, leaving it at zero will give you the default which is max capacity.
        /// </summary>
        [Tooltip("The number of physics world states that are backed up on the server. This cannot be more than the maximum capacity, leaving it at zero will give you the default which is max capacity")]
        public int ServerHistorySize;
        /// <summary>
        /// The number of physics world states that are backed up on the client. This cannot be more than the maximum capacity, leaving it at zero will give oyu the default which is one.
        /// </summary>
        [Tooltip("The number of physics world states that are backed up on the client. This cannot be more than the maximum capacity, leaving it at zero will give oyu the default which is one.")]
        public int ClientHistorySize;

        /// <summary>
        /// When using predicted physics all dynamic physics objects in the main physics world on the client
        /// mus be ghosts. Setting this will move any non-ghost in the default physics world to another world.
        /// </summary>
        [Tooltip("The physics world index to use for all dynamic physics objects which are not ghosts.")]
        public uint ClientNonGhostWorldIndex = 0;
    }

    class NetCodePhysicsConfigBaker : Baker<NetCodePhysicsConfig>
    {
        public override void Bake(NetCodePhysicsConfig authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            if (authoring.EnableLagCompensation)
            {
                AddComponent(entity, new LagCompensationConfig
                {
                    ServerHistorySize = authoring.ServerHistorySize,
                    ClientHistorySize = authoring.ClientHistorySize
                });
            }
            if (authoring.ClientNonGhostWorldIndex != 0)
                AddComponent(entity, new PredictedPhysicsNonGhostWorld{Value = authoring.ClientNonGhostWorldIndex});
        }
    }
}
