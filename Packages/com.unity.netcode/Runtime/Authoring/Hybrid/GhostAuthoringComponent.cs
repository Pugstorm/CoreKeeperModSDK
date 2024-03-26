using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Unity.Entities.Hybrid.Baking;

namespace Unity.NetCode
{
    /// <summary>
    /// <para>The GhostAuthoringComponent is the main entry point to configure and create replicated ghosts types.
    /// The component must be added only to the GameObject hierarchy root.</para>
    /// <para>It allows setting all ghost properties,
    /// such as the replication mode <see cref="SupportedGhostModes"/>, bandwidth optimization strategy (<see cref="OptimizationMode"/>,
    /// the ghost <see cref="Importance"/> (how frequently is sent) and others).</para>
    /// <seealso cref="GhostAuthoringInspectionComponent"/>
    /// </summary>
    [RequireComponent(typeof(LinkedEntityGroupAuthoring))]
    [DisallowMultipleComponent]
    [HelpURL(Authoring.HelpURLs.GhostAuthoringComponent)]
    public class GhostAuthoringComponent : MonoBehaviour
    {
#if UNITY_EDITOR
        void OnValidate()
        {
            if (gameObject.scene.IsValid())
                return;
            var path = UnityEditor.AssetDatabase.GetAssetPath(gameObject);
            if (string.IsNullOrEmpty(path))
                return;
            var guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
            if (!string.Equals(guid, prefabId, StringComparison.OrdinalIgnoreCase))
            {
                prefabId = guid;
            }
        }
#endif

        /// <summary>
        /// Force the ghost baker to treat this GameObject as if it was a prefab. This is used if you want to programmatically create
        /// a ghost prefab as a GameObject and convert it to an Entity prefab with ConvertGameObjectHierarchy.
        /// </summary>
        [NonSerialized] public bool ForcePrefabConversion;

        /// <summary>
        /// The ghost mode used if you do not manually change it using a GhostSpawnClassificationSystem.
        /// If set to OwnerPredicted the ghost will be predicted on the client which owns it and interpolated elsewhere.
        /// You must not change the mode using a classification system if using owner predicted.
        /// </summary>
        [Tooltip("The `GhostMode` used when first spawned (assuming you do not manually change it, using a GhostSpawnClassificationSystem).\n\nIf set to 'Owner Predicted', the ghost will be 'Predicted' on the client which owns it, and 'Interpolated' on all others. If using 'Owner Predicted', you cannot change the ghost mode via a classification system.")]
        public GhostMode DefaultGhostMode = GhostMode.Interpolated;
        /// <summary>
        /// The ghost modes supported by this ghost. This will perform some more optimizations at authoring time but make it impossible to change ghost mode at runtime.
        /// </summary>
        [Tooltip("Every `GhostMode` supported by this ghost. Setting this to either 'Predicted' or 'Interpolated' will allow NetCode to perform some more optimizations at authoring time. However, it makes it impossible to change ghost mode at runtime.")]
        public GhostModeMask SupportedGhostModes = GhostModeMask.All;
        /// <summary>
        /// This setting is only for optimization, the ghost will be sent when modified regardless of this setting.
        /// Optimizing for static makes snapshots slightly larger when they change, but smaller when they do not change.
        /// </summary>
        [Tooltip("Bandwidth and CPU optimization:\n\n - <b>Static</b> - This ghost will only be added to a snapshot when its ghost values actually change.\n<i>Examples: Barrels, trees, dropped items, asteroids etc.</i>\n\n - <b>Dynamic</b> - This ghost will be replicated at a regular interval, regardless of whether or not its values have changed, allowing for more aggressive compression.\n<i>Examples: Character controllers, missiles, important gameplay items like CTF flags and footballs etc.</i>\n\n<i>Marking a ghost as `Static` makes snapshots slightly larger when replicated values change, but smaller when they do not.</i>")]
        public GhostOptimizationMode OptimizationMode = GhostOptimizationMode.Dynamic;
        /// <summary>
        /// If not all ghosts can fit in a snapshot only the most important ghosts will be sent. Higher importance means the ghost is more likely to be sent.
        /// </summary>
        [Tooltip("Importance determines which ghosts are selected to be added to the snapshot, in the case where there is not enough space to include all ghosts in the snapshot. Many caveats apply, but generally, higher values are sent more frequently.\n\n<i>Example: A 'Player' ghost with an Importance of 100 is roughly 100x more likely to be sent in any given snapshot than a 'Barrel' ghost with an Importance of 1. In other words, expect the 'Player' ghost to have been replicated 100 times for every one time the 'Barrel' is replicated.</i>\n\nApplied at the chunk level.")]
        public int Importance = 1;
        /// <summary>
        /// For internal use only, the prefab GUID used to distinguish between different variant of the same prefab.
        /// </summary>
        [SerializeField]internal string prefabId = "";
        /// <summary>
        /// Add a GhostOwner tracking which connection owns this component.
        /// You must set the GhostOwner to a valid NetworkId.Value at runtime.
        /// </summary>
        [Tooltip("Automatically adds a `GhostOwner`, which allows the server to set (and track) which connection owns this ghost. In your server code, you must set the `GhostOwner` to a valid `NetworkId.Value` at runtime.")]
        public bool HasOwner;
        /// <summary>
        /// Automatically send all ICommandData buffers if the ghost is owned by the current connection,
        /// AutoCommandTarget.Enabled is true and the ghost is predicted.
        /// </summary>
        [Tooltip("Automatically sends all `ICommandData` buffers when the following conditions are met: \n\n - The ghost is owned by the current connection.\n\n - AutoCommandTarget is added, and Enabled is true.\n\n - The ghost is predicted.")]
        public bool SupportAutoCommandTarget = true;
        /// <summary>
        /// Add a CommandDataInterpolationDelay component so the interpolation delay of each client is tracked.
        /// This is used for server side lag-compensation.
        /// </summary>
        [Tooltip("Add a `CommandDataInterpolationDelay` component so the interpolation delay of each client is tracked.\n\nThis is used for server side lag-compensation (it allows the server to more accurately estimate how far behind your interpolated ghosts are, leading to better hit registration, for example).\n\nThis should be enabled if you expect to use input commands (from this 'Owner Predicted' ghost) to interact with other, 'Interpolated' ghosts (example: shooting or hugging another 'Player').")]
        public bool TrackInterpolationDelay;
        /// <summary>
        /// Add a GhostGroup component which makes it possible for this entity to be the root of a ghost group.
        /// </summary>
        [Tooltip("Add a `GhostGroup` component, which makes it possible for this entity to be the root of a 'Ghost Group'.\n\nA 'Ghost Group' is a collection of ghosts who must always be replicated in the same snapshot, which is useful (for example) when trying to keep an item like a weapon in sync with the player carrying it.\n\nTo use this feature, you must add the target ghost entity to this `GhostGroup` buffer at runtime (e.g. when the weapon is first picked up by the player).\n\n<i>Note that GhostGroups slow down serialization, as they force entity chunk random-access. Therefore, prefer other solutions.</i>")]
        public bool GhostGroup;
        /// <summary>
        /// Force this ghost to be quantized and copied to the snapshot format once for all connections instead
        /// of once per connection. This can save CPU time in the ghost send system if the ghost is
        /// almost always sent to at least one connection, and it contains many serialized components, serialized
        /// components on child entities or serialized buffers. A common case where this can be useful is the ghost
        /// for the character / player.
        /// </summary>
        [Tooltip("CPU optimization that forces this ghost to be quantized and copied to the snapshot format <b>once for all connections</b> (instead of once <b>per connection</b>). This can save CPU time in the `GhostSendSystem` assuming all of the following:\n\n - The ghost contains many serialized components, serialized components on child entities, or serialized buffers.\n\n - The ghost is almost always sent to at least one connection.\n\n<i>Example use-cases: Players, important gameplay items like footballs and crowns, global entities like map settings and dynamic weather conditions.</i>")]
        public bool UsePreSerialization;
        /// <summary>
        /// Validate the name of the GameObject prefab.
        /// </summary>
        /// <param name="ghostNameHash">Outputs the hash generated from the name.</param>
        /// <returns>The FS equivalent of the gameObject.name.</returns>
        public FixedString64Bytes GetAndValidateGhostName(out ulong ghostNameHash)
        {
            var ghostName = gameObject.name;
            var ghostNameFs = new FixedString64Bytes();
            var nameCopyError = FixedStringMethods.CopyFromTruncated(ref ghostNameFs, ghostName);
            ghostNameHash = TypeHash.FNV1A64(ghostName);
            if (nameCopyError != CopyError.None)
                Debug.LogError($"{nameCopyError} when saving GhostName \"{ghostName}\" into FixedString64Bytes, became: \"{ghostNameFs}\"!", this);
            return ghostNameFs;
        }
        /// <summary>True if we can apply the <see cref="GhostSendType"/> optimization on this Ghost.</summary>
        public bool SupportsSendTypeOptimization => SupportedGhostModes != GhostModeMask.All || DefaultGhostMode == GhostMode.OwnerPredicted;
    }
}
