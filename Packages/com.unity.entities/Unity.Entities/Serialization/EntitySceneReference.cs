using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Entities.Serialization
{
    /// <summary>
    /// Encapsulates a serializable reference to a Scene asset.
    /// </summary>
    [Serializable]
    public struct EntitySceneReference : IEquatable<EntitySceneReference>
    {
        [SerializeField]
        internal UntypedWeakReferenceId Id;

        internal EntitySceneReference(UntypedWeakReferenceId sceneId)
        {
            Id = sceneId;
        }

        /// <summary>
        /// Initializes and returns an instance of EntitySceneReference.
        /// </summary>
        /// <param name="guid">The guid of the scene asset.</param>
        /// <param name="sectionIndex">The subscene section index.</param>
        public EntitySceneReference(Hash128 guid, int sectionIndex)
        {
            Id = new UntypedWeakReferenceId(new RuntimeGlobalObjectId { AssetGUID = guid, IdentifierType = 1, SceneObjectIdentifier0 = sectionIndex }, WeakReferenceGenerationType.EntityScene);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Initializes and returns an instance of EntitySceneReference.
        /// </summary>
        /// <param name="sceneAsset">The referenced <see cref="SceneAsset"/>.</param>
        public EntitySceneReference(SceneAsset sceneAsset) : this(
            AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(sceneAsset)), 0)
        {
        }
#endif
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
                if (Id.GenerationType != WeakReferenceGenerationType.EntityScene)
                    return false;

                if (UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(UnityEditor.AssetDatabase.GUIDToAssetPath(Id.GlobalId.AssetGUID)) != typeof(UnityEditor.SceneAsset))
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
        public bool Equals(EntitySceneReference other)
        {
            return Id .Equals(other.Id );
        }

        /// <summary>
        /// Overrides the default Object.Equals method.
        /// </summary>
        /// <param name="obj">An object to compare for equality.</param>
        /// <returns>True if this EntitySceneReference and the object are equal.</returns>
        public override bool Equals(object obj)
        {
            return obj is EntitySceneReference other && Equals(other);
        }

        /// <summary>
        /// Overrides the default Object.GetHashCode method.
        /// </summary>
        /// <returns>The hash code of this EntitySceneReference.</returns>
        public override int GetHashCode()
        {
            return Id .GetHashCode();
        }
    }
}
