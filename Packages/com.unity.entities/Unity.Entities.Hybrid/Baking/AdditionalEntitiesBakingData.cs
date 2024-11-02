namespace Unity.Entities.Hybrid.Baking
{
    /// <summary>
    /// AdditionalEntitiesBakingData buffer is added by default to each primary entity and contains the list of additional entities associated
    /// </summary>
    [BakingType]
    public struct AdditionalEntitiesBakingData : IBufferElementData
    {
        /// <summary>
        /// Represents the instance ID of the authoring component that created the Additional Entity in its Baker.
        /// </summary>
        public int AuthoringComponentID;
        /// <summary>
        /// The Additional Entity associated with this Primary Entity
        /// </summary>
        public Entity Value;
    }
}
