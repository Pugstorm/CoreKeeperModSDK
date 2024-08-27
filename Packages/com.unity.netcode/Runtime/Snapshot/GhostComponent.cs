using Unity.Entities;
using Unity.Collections;
using System;
using Unity.Burst;

namespace Unity.NetCode
{
    /// <summary>
    /// Temporary type, used to upgrade to new component type, to be removed before final 1.0
    /// </summary>
    [Obsolete("GhostComponent has been deprecated. Use GhostInstance instead (UnityUpgradable) -> GhostInstance", true)]
    [DontSupportPrefabOverrides]
    public struct GhostComponent : IComponentData
    {
    }
    /// <summary>
    /// Temporary type, used to upgrade to new component type, to be removed before final 1.0
    /// </summary>
    [Obsolete("GhostChildEntityComponent has been deprecated. Use GhostChildEntity instead (UnityUpgradable) -> GhostChildEntity", true)]
    [DontSupportPrefabOverrides]
    public struct GhostChildEntityComponent : IComponentData
    {
    }
    /// <summary>
    /// Temporary type for upgradability, to be removed before 1.0
    /// </summary>
    [Obsolete("GhostTypeComponent has been deprecated. Use GhostType instead (UnityUpgradable) -> GhostType", true)]
    [DontSupportPrefabOverrides]
    public struct GhostTypeComponent : IComponentData
    {
    }
    /// <summary>
    /// Temporary type, used to upgrade to new component type, to be removed before final 1.0
    /// </summary>
    [Obsolete("SharedGhostTypeComponent has been deprecated. Use GhostTypePartition instead (UnityUpgradable) -> GhostTypePartition", true)]
    public struct SharedGhostTypeComponent : IComponentData
    {
        /// <summary>
        /// Ghost type used for the this entity.
        /// </summary>
        public GhostType SharedValue;
    }
    /// <summary>
    /// Temporary type, used to upgrade to new component type, to be removed before final 1.0
    /// </summary>
    [Obsolete("PredictedGhostComponent has been deprecated. Use PredictedGhost instead (UnityUpgradable) -> PredictedGhost", true)]
    [DontSupportPrefabOverrides]
    public struct PredictedGhostComponent : IComponentData
    {
    }
    /// <summary>
    /// Temporary type, used to upgrade to new component type, to be removed before final 1.0
    /// </summary>
    [Obsolete("PredictedGhostSpawnRequestComponent has been deprecated. Use PredictedGhostSpawnRequest instead (UnityUpgradable) -> PredictedGhostSpawnRequest", true)]
    public struct PredictedGhostSpawnRequestComponent : IComponentData
    {
    }
    /// <summary>
    /// Temporary type, used to upgrade to new component type, to be removed before final 1.0
    /// </summary>
    [Obsolete("PendingSpawnPlaceholderComponent has been deprecated. Use PendingSpawnPlaceholder instead (UnityUpgradable) -> PendingSpawnPlaceholder", true)]
    public struct PendingSpawnPlaceholderComponent : IComponentData
    {
    }

    /// <summary>
    /// Component signaling an entity which is replicated over the network
    /// </summary>
    [DontSupportPrefabOverrides]
    public struct GhostInstance : IComponentData
    {
        /// <summary>
        /// The id assigned to the ghost by the server. When a ghost is destroyed, its id is recycled and can assigned to
        /// new ghosts. For that reason the ghost id cannot be used an unique identifier.
        /// The <see cref="ghostId"/>, <see cref="spawnTick"/> pairs is instead guaratee to be unique, since at any given
        /// point in time, there can be one and only one ghost that have a given id that has been spawned at that specific tick.
        /// </summary>
        public int ghostId;
        /// <summary>
        /// The ghost prefab type, as index inside the ghost prefab collection.
        /// </summary>
        public int ghostType;
        /// <summary>
        /// The tick the entity spawned on the server. Together with <see cref="ghostId"/> is guaranteed to be always unique.
        /// </summary>
        public NetworkTick spawnTick;

        /// <summary>
        /// Implicitly convert a GhostComponent to a <see cref="SpawnedGhost"/> instance.
        /// </summary>
        /// <param name="comp"></param>
        /// <returns></returns>
        public static implicit operator SpawnedGhost(in GhostInstance comp)
        {
            return new SpawnedGhost(comp.ghostId, comp.spawnTick);
        }

        /// <summary>
        /// Returns a human-readable GhostComponent FixedString, containing its values.
        /// </summary>
        /// <returns>A human-readable GhostComponent FixedString, containing its values.</returns>
        public FixedString128Bytes ToFixedString()
        {
            return $"GhostComponent[type:{ghostType}|id:{ghostId},st:{spawnTick.ToFixedString()}]";
        }
    }

    /// <summary>
    /// A tag added to child entities in a ghost with multiple entities. It should also be added to ghosts in a group if the ghost is not the root of the group.
    /// </summary>
    [DontSupportPrefabOverrides]
    public struct GhostChildEntity : IComponentData
    {}

    /// <summary>
    /// Component storing the guid of the prefab the ghost was created from. This is used to lookup ghost type in a robust way which works even if two ghosts have the same archetype
    /// </summary>
    [DontSupportPrefabOverrides]
    [Serializable]
    public struct GhostType : IComponentData,
        IEquatable<GhostType>
    {
        /// <summary>
        /// The first 4 bytes of the prefab guid
        /// </summary>
        [UnityEngine.SerializeField]
        internal uint guid0;
        /// <summary>
        /// The second 4 bytes of the prefab guid
        /// </summary>
        [UnityEngine.SerializeField]
        internal uint guid1;
        /// <summary>
        /// The third 4 bytes of the prefab guid
        /// </summary>
        [UnityEngine.SerializeField]
        internal uint guid2;
        /// <summary>
        /// The forth 4 bytes of the prefab guid
        /// </summary>
        [UnityEngine.SerializeField]
        internal uint guid3;

        /// <summary>
        /// Construct a new <see cref="GhostType"/> from a <see cref="Hash128"/> guid string.
        /// </summary>
        /// <param name="guid">a guid string. Either Hash128 or Unity.Engine.GUID strings are valid.</param>
        /// <returns>a new GhostType instance</returns>
        [BurstDiscard]
        internal static GhostType FromHash128String(string guid)
        {
            var hash = new Hash128(guid);
            return new GhostType
            {
                guid0 = hash.Value.x,
                guid1 = hash.Value.y,
                guid2 = hash.Value.z,
                guid3 = hash.Value.w,
            };
        }

        /// <summary>
        /// Create a new <see cref="GhostType"/> from the give <see cref="Hash128"/> guid.
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        internal static GhostType FromHash128(Hash128 guid)
        {
            return new GhostType
            {
                guid0 = guid.Value.x,
                guid1 = guid.Value.y,
                guid2 = guid.Value.z,
                guid3 = guid.Value.w,
            };
        }

        /// <summary>
        /// Convert a <see cref="GhostType"/> to a <see cref="Hash128"/> instance. The hash will always match the prefab guid
        /// from which the ghost has been created.
        /// </summary>
        /// <param name="ghostType"></param>
        /// <returns></returns>
        public static explicit operator Hash128(GhostType ghostType)
        {
            return new Hash128(ghostType.guid0, ghostType.guid1, ghostType.guid2, ghostType.guid3);

        }

        /// <summary>
        /// Returns whether or not two GhostType are identical.
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns>True if the the types guids are the same.</returns>
        public static bool operator ==(GhostType lhs, GhostType rhs)
        {
            return lhs.guid0 == rhs.guid0 && lhs.guid1 == rhs.guid1 && lhs.guid2 == rhs.guid2 && lhs.guid3 == rhs.guid3;
        }
        /// <summary>
        /// Returns whether or not two GhostType are distinct.
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns>True if the the types guids are the different.</returns>
        public static bool operator !=(GhostType lhs, GhostType rhs)
        {
            return lhs.guid0 != rhs.guid0 || lhs.guid1 != rhs.guid1 || lhs.guid2 != rhs.guid2 || lhs.guid3 != rhs.guid3;
        }
        /// <summary>
        /// Returns whether or not the <see cref="other"/> reference is identical to the current instance.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(GhostType other)
        {
            return this == other;
        }
        /// <summary>
        /// Returns whether or not the <see cref="obj"/> reference is of type `GhostType`, and
        /// whether or not it's identical to the current instance.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>True if equal to the passed in `GhostType`.</returns>
        public override bool Equals(object obj)
        {
            if(obj is GhostType aGT) return Equals(aGT);
            return false;
        }

        /// <summary>
        /// Return an hashcode suitable for inserting the component into a dictionary or a sorted container.
        /// </summary>
        /// <returns>True if equal to the passed in `GhostType`.</returns>
        public override int GetHashCode()
        {
            var result = guid0.GetHashCode();
            result = (result*31) ^ guid1.GetHashCode();
            result = (result*31) ^ guid2.GetHashCode();
            result = (result*31) ^ guid3.GetHashCode();
            return result;
        }
    }


    /// <summary>
    /// Component used on the server to make sure the ghosts of different ghost types are in different chunks,
    /// even if they have the same archetype (regardless of component data).
    /// </summary>
    [DontSupportPrefabOverrides]
    public struct GhostTypePartition : ISharedComponentData
    {
        /// <summary>
        /// Ghost type used for the this entity.
        /// </summary>
        public GhostType SharedValue;
    }



    /// <summary>
    /// Component on client signaling that an entity is predicted (as opposed to interpolated).
    /// <seealso cref="GhostMode"/>
    /// <seealso cref="GhostModeMask"/>
    /// </summary>
    [DontSupportPrefabOverrides]
    public struct PredictedGhost : IComponentData
    {
        /// <summary>
        /// The last server snapshot that has been applied to the entity.
        /// </summary>
        public NetworkTick AppliedTick;
        /// <summary>
        /// <para>The server tick from which the entity should start predicting.</para>
        /// <para>When a new ghost snapshot is received, the entity is synced to the server state,
        /// and the PredictionStartTick is set to the snapshot server tick.</para>
        /// <para>Otherwise, the PredictionStartTick should correspond to:
        /// <para>- The last simulated full tick by the client (see <see cref="ClientServerTickRate"/>)
        /// if a prediction backup (see <see cref="GhostPredictionHistoryState"/>) exists </para>
        /// <para>- The last received snapshot tick if a continuation backup is not found.</para>
        /// </para>
        /// </summary>
        public NetworkTick PredictionStartTick;

        /// <summary>
        /// Query if the entity should be simulated (predicted) for the given tick.
        /// </summary>
        /// <param name="tick"></param>
        /// <returns>True if the entity should be simulated.</returns>
        public bool ShouldPredict(NetworkTick tick)
        {
            return !PredictionStartTick.IsValid || tick.IsNewerThan(PredictionStartTick);
        }
    }

    /// <summary>
    /// Optional component, used to request predictive spawn of a ghosts by the client.
    /// The component is automatically added to the authored ghost prefabs when:
    /// <span>
    /// - The baking target is <see cref="NetcodeConversionTarget.Client"/> or <see cref="NetcodeConversionTarget.ClientAndServer"/>.
    /// - When using the hybrid authoring workflow, if the <see cref="GhostAuthoringComponent.SuypportedGhostModes"/>
    /// is <see cref="GhostModeMask.Predicted"/> or <see cref="GhostModeMask.All"/>.
    /// - When using the <see cref="GhostPrefabCreation.ConvertToGhostPrefab"/>, if the
    /// <see cref="GhostPrefabCreation.Config.SupportedGhostModes"/> is set to <see cref="GhostModeMask.Predicted"/> or <see cref="GhostModeMask.All"/>.
    /// </span>
    /// The predicted spawn request is consumed by <see cref="PredictedGhostSpawnSystem"/>, which will remove the component from the
    /// instantiated entity after an initial setup. <br/>
    /// The package provides a default handling for predictive spawning (<see cref="DefaultGhostSpawnClassificationSystem"/>).
    /// In case you need a custom or more accurate way to match the predicted spawned entities with the authoritive server spawned ones,
    /// you can implement a custom spawn classification system. See <see cref="GhostSpawnClassificationSystem"/> for further details.
    /// </summary>
    public struct PredictedGhostSpawnRequest : IComponentData
    {
    }

    /// <summary>
    /// Component on the client signaling that an entity is a placeholder for a "not yet spawned" ghost.
    /// I.e. Not yet a "real" ghost.
    /// </summary>
    public struct PendingSpawnPlaceholder : IComponentData
    {
    }

    /// <summary>
    /// Utility methods for working with GhostComponents.
    /// </summary>
    public static class GhostComponentUtilities
    {
        /// <summary>
        /// Find the first valid ghost type id in an array of ghost components.
        /// Pre-spawned ghosts have type id -1.
        /// </summary>
        /// <param name="self"></param>
        /// <returns>The ghost type index if a ghost with a valid type is found, -1 otherwise</returns>
        public static int GetFirstGhostTypeId(this NativeArray<GhostInstance> self)
        {
            return self.GetFirstGhostTypeId(out _);
        }

        /// <summary>
        /// Find the first valid ghost type id in an array of ghost components.
        /// Pre-spawned ghosts have type id -1.
        /// This method returns -1 if no ghost type id is found.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="firstGhost">The first valid ghost type index found will be stored in this variable.</param>
        /// <returns>A valid ghost type id, or -1 if no ghost type id was found.</returns>
        public static int GetFirstGhostTypeId(this NativeArray<GhostInstance> self, out int firstGhost)
        {
            firstGhost = 0;
            int ghostTypeId = self[0].ghostType;
            while (ghostTypeId == -1 && ++firstGhost < self.Length)
            {
                ghostTypeId = self[firstGhost].ghostType;
            }
            return ghostTypeId;
        }

        /// <summary>
        /// Retrieve the component name as <see cref="NativeText"/>. The method is burst compatible.
        /// </summary>
        /// <param name="self"></param>
        /// <returns>The component name.</returns>
        public static NativeText.ReadOnly GetDebugTypeName(this ComponentType self)
        {
            return TypeManager.GetTypeInfo(self.TypeIndex).DebugTypeName;
        }
    }
}
