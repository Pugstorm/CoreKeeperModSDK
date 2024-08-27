#if UNITY_EDITOR && !NETCODE_NDEBUG
#define NETCODE_DEBUG
#endif
using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting.APIUpdating;

#if NETCODE_DEBUG
namespace Unity.NetCode
{
    /// <summary>
    /// The name of ghost prefab. Used for debugging purpose to pretty print ghost names. Available only once the
    /// NETCODE_DEBUG define is set.
    /// </summary>
    public struct PrefabDebugName : IComponentData
    {
        /// <summary>
        /// The name of the prefab.
        /// </summary>
        [Obsolete("The PrefabDebugName.Name field has been deprecated. Please use the PrefabName instea.", false)]
        public FixedString64Bytes Name;

        /// <summary>
        /// The name of the prefab.
        /// </summary>
        public LowLevel.BlobStringText PrefabName;
    }
}
#endif
