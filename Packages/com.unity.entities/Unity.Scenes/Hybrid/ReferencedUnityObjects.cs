using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Unity.Scenes
{
    /// <summary>
    /// Holds the references to UnityEngine.Objects which are referenced in a serialized <see cref="Unity.Entities.World"/> object.
    /// </summary>
    [MovedFrom(true, "Unity.Entities", "Unity.Entities.Hybrid")]
    public class ReferencedUnityObjects : ScriptableObject
    {
        /// <summary>
        /// The array of UnityEngine.Object references extracted from a <see cref="Unity.Entities.World"/> during serialization.
        /// </summary>
        public UnityEngine.Object[] Array;
        /// <summary>
        /// Represents the index of the entity referencing the object.
        /// </summary>
        /// <remarks>You can use this to differentiate Prefab references and Companion Objects at runtime deserialization.</remarks>
        public int[] CompanionObjectIndices;
    }
}
