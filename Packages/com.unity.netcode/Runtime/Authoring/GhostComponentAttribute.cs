using System;

namespace Unity.NetCode
{
    /// <summary>
    /// This attribute can be used to tag components to control which ghost prefab variants they are included in and where they are sent for owner predicted ghosts.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
    public class GhostComponentAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the type of prefab where this component should be included on the main entity of the prefab.
        /// </summary>
        public GhostPrefabType PrefabType { get; set; } = GhostPrefabType.All;
        /// <summary>
        /// Gets or sets the type of ghost this component should be sent to if the ghost is owner predicted.
        /// Formerly: "OwnerPredictedSendType".
        /// </summary>
        public GhostSendType SendTypeOptimization { get; set; } = GhostSendType.AllClients;

        /// <summary>
        /// Get or sets if a component should be be sent to the prediction owner or not. Some combination
        /// of the parameters and OwnerSendType may result in an error or warning at code-generation time.
        /// </summary>
        public SendToOwnerType OwnerSendType { get; set; } = SendToOwnerType.All;

        /// <summary>
        /// Denotes whether or not this component - when added to a child entity - should send (i.e. replicate) its data.
        /// The default behaviour is that Netcode will NOT replicate component and buffer data on children.
        /// Why not? It's expensive, as it involves finding child entities in other chunks.
        /// Thus, setting this flag to true will enable this (more expensive) serialization of child entities (unless overridden via another "Variant").
        /// Setting to false has no effect (as is the default).
        /// </summary>
        public bool SendDataForChildEntity { get; set; } = false;
    }
}
