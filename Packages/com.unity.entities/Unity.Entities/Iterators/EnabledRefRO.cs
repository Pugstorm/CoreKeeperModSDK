using Unity.Collections;

namespace Unity.Entities
{
    /// <summary>
    /// Stores a safe reference to a read-only component enable bit.
    /// </summary>
    /// <remarks>Do not store outside of stack</remarks>
    /// <typeparam name="T">Type of enabled component</typeparam>
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleEnableableComponent) }, RequiredUnityDefine = "ENABLE_UNITY_COLLECTIONS_CHECKS")]
    public readonly struct EnabledRefRO<T> : IQueryTypeParameter where T : unmanaged, IEnableableComponent
    {
        readonly SafeBitRef m_Ptr;

        /// <summary>
        /// Constructor for read-only enable reference to enableable component.
        /// This is typically used by generated code inside of Aspects.
        /// </summary>
        /// <param name="ptr">Pointer to single bit and safety handle</param>
        public EnabledRefRO(SafeBitRef ptr)
        {
            m_Ptr = ptr;
        }

        /// <summary>
        /// Null value for this reference.
        /// </summary>
        public static EnabledRefRO<T> Null => new EnabledRefRO<T>(SafeBitRef.Null);

        /// <summary>
        /// Property that returns true if the reference is valid, false otherwise.
        /// </summary>
        public bool IsValid => m_Ptr.IsValid;

        /// <summary>
        /// Property to get enabled value of this reference (true if enabled, false otherwise).
        /// </summary>
        public bool ValueRO => m_Ptr.GetBit();
    }
}
