using System;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Entities.Serialization
{
    /// <summary>
    /// Weak reference to entity prefab. Entity prefabs are GameObjects that have been fully converted into entity data during the import/build process.
    /// </summary>
    [Serializable]
    public struct EntityPrefabReference : IEquatable<EntityPrefabReference>
    {
        [SerializeField]
        internal UntypedWeakReferenceId Id;

        internal EntityPrefabReference(UntypedWeakReferenceId prefabId)
        {
            Id = prefabId;
        }

        /// <inheritdoc/>
        public static bool operator ==(EntityPrefabReference left, EntityPrefabReference right)
        {
            return left.Equals(right);
        }

        /// <inheritdoc/>
        public static bool operator !=(EntityPrefabReference left, EntityPrefabReference right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Construct an EntityPrefabReference from a GUID.
        /// </summary>
        /// <param name="guid">The prefab asset GUID.</param>
        public EntityPrefabReference(Hash128 guid)
        {
            Id = new UntypedWeakReferenceId(new RuntimeGlobalObjectId { AssetGUID = guid, IdentifierType = 1 }, WeakReferenceGenerationType.EntityPrefab);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Construct an EntityPrefabReference from a GameObject.
        /// </summary>
        /// <param name="prefab">The prefab to construct from.</param>
        public EntityPrefabReference(GameObject prefab) : this(
            AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(prefab)))
        {
        }
#endif

        /// <summary>
        /// Returns the prefab GUID.
        /// </summary>
        public Hash128 AssetGUID => Id.GlobalId.AssetGUID;

        /// <summary>
        /// Returns true if the reference has a valid id.  In the editor, additional checks for the correct GenerationType and the existence of the referenced asset are performed. 
        /// </summary>
        public bool IsReferenceValid
        {
            get
            {
                if (!Id.IsValid)
                    return false;
#if UNITY_EDITOR
                if (Id.GenerationType != WeakReferenceGenerationType.EntityPrefab)
                    return false;

                if (UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(UnityEditor.AssetDatabase.GUIDToAssetPath(Id.GlobalId.AssetGUID)) != typeof(GameObject))
                    return false;
#endif
                return true;
            }
        }

        /// <summary>
        /// Checks if this reference holds the same asset GUID as the other reference.
        /// </summary>
        /// <param name="other">The other weak reference object to compare to.</param>
        /// <returns>True if the asset GUID of both are equal.</returns>
        public bool Equals(EntityPrefabReference other)
        {
            return Id.Equals(other.Id);
        }

        /// <summary>
        /// Overrides the default Object.Equals method.
        /// </summary>
        /// <param name="obj">An object to compare for equality.</param>
        /// <returns>True if this EntityPrefabReference and the object are equal.</returns>
        public override bool Equals(object obj)
        {
            return obj is EntityPrefabReference other && Equals(other);
        }

        /// <summary>
        /// Overrides the default Object.GetHashCode method.
        /// </summary>
        /// <returns>The hash code of this EntityPrefabReference.</returns>
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
