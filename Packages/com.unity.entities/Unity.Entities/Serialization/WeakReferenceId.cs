using System;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.InteropServices;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Entities.Serialization
{
    /// <summary>
    /// Enumeration of weak reference types.
    /// </summary>
    public enum WeakReferenceGenerationType : short
    {
        /// <summary>
        /// Unknown generation type, this usually indicates that the <seealso cref="UntypedWeakReferenceId"/> has not been initialized.
        /// </summary>
        Unknown,
        /// <summary>
        /// Reference to an object derived from <seealso cref="UnityEngine.Object"/>.
        /// </summary>
        UnityObject,
        /// <summary>
        /// Texture asset for DOTS Runtime.
        /// </summary>
        Texture,
        /// <summary>
        /// Reference to a GameObject based scene.
        /// </summary>
        GameObjectScene,
        /// <summary>
        /// Reference to an Entity based scene.
        /// </summary>
        EntityScene,
        /// <summary>
        /// Reference to a converted prefab.
        /// </summary>
        EntityPrefab,
        /// <summary>
        /// Reference to a collection of referenced <seealso cref="UnityEngine.Object"/>s from a sub scene.
        /// </summary>
        SubSceneObjectReferences
    };

    // Workaround to be able to store UntypedWeakReferenceId in a blob.
    // Usually we want to issue an error when storing UntypedWeakReferenceId in a blob to alert users that this is not yet supported
    // But in this specific case we know what we are doing. This type must be binary compatible with  UntypedWeakReferenceId
    internal struct UnsafeUntypedWeakReferenceId
    {
        public UnsafeUntypedWeakReferenceId(UntypedWeakReferenceId weakAssetRef)
        {
            GlobalId = weakAssetRef.GlobalId;
            GenerationType = weakAssetRef.GenerationType;
        }
        public RuntimeGlobalObjectId GlobalId;
        public WeakReferenceGenerationType GenerationType;
    }

    /// <summary>
    /// Used to identify weakly referenced data.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct UntypedWeakReferenceId : IEquatable<UntypedWeakReferenceId>
    {
        /// <summary>
        /// The global object id.  This matches the editor global id.
        /// </summary>
        public RuntimeGlobalObjectId GlobalId;

        /// <summary>
        /// The type of reference this is.
        /// It is aliased to RuntimeGlobalObjectId.SceneObjectIdentifier1.
        /// </summary>
        public WeakReferenceGenerationType GenerationType;

        /// <summary>
        /// Construct a reference given the global id and generation type.
        /// </summary>
        /// <param name="globalObjId">The global object id.</param>
        /// <param name="genType">The generation type.</param>
        public UntypedWeakReferenceId(RuntimeGlobalObjectId globalObjId, WeakReferenceGenerationType genType)
        {
            GlobalId = globalObjId;
            GenerationType = genType;
        }

        /// <summary>
        /// Construct new UntypedWeakReferenceId.
        /// </summary>
        /// <param name="guid">The asset guid.</param>
        /// <param name="localFileIdentifier">The object local identifier.</param>
        /// <param name="idType">The object type.</param>
        /// <param name="genType">The id generation type.</param>
        public UntypedWeakReferenceId(Hash128 guid, long localFileIdentifier, int idType, WeakReferenceGenerationType genType)
        {
            GlobalId = new RuntimeGlobalObjectId { AssetGUID = guid, SceneObjectIdentifier0 = localFileIdentifier, IdentifierType = idType };
            GenerationType = genType;
        }

        /// <summary>
        /// Checks if this reference is equal to another reference.
        /// </summary>
        /// <param name="other">The id to compare to.</param>
        /// <returns>True if the GenerationType and GlobalId are equal.</returns>
        public bool Equals(UntypedWeakReferenceId other) => GenerationType == other.GenerationType && GlobalId.Equals(other.GlobalId);

        /// <summary>
        /// Converts the id to a string.
        /// </summary>
        /// <returns>The string representation of this id.</returns>
        public override string ToString() => GlobalId.ToString();

        /// <summary>
        /// Checks if this reference is equal to another object.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>True if the other object is an <seealso cref="UntypedWeakReferenceId"/> and is equal.</returns>
        public override bool Equals(object obj)
        {
            return obj is UntypedWeakReferenceId other && Equals(other);
        }

        /// <inheritdoc/>
        public bool IsValid => GlobalId.IsValid;

        /// <summary>
        /// Returns the hash code of this id.
        /// </summary>
        /// <returns>The hash code of this id.</returns>
        public override int GetHashCode() => GlobalId.GetHashCode();

        /// <inheritdoc/>
        public static bool operator ==(UntypedWeakReferenceId left, UntypedWeakReferenceId right)
        {
            return left.Equals(right);
        }

        /// <inheritdoc/>
        public static bool operator !=(UntypedWeakReferenceId left, UntypedWeakReferenceId right)
        {
            return !left.Equals(right);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Get a weak reference from a Unity object reference.
        /// </summary>
        /// <param name="obj">The object to reference.</param>
        /// <returns>The ide if the reference.</returns>
        public static UntypedWeakReferenceId CreateFromObjectInstance(UnityEngine.Object obj)
        {
            if (obj == null)
                return default;

            var goid = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(obj.GetInstanceID());
            var rtgoid = UnsafeUtility.As<GlobalObjectId, RuntimeGlobalObjectId>(ref goid);
            return new UntypedWeakReferenceId(rtgoid, typeof(SceneAsset) == obj.GetType() ? WeakReferenceGenerationType.GameObjectScene : WeakReferenceGenerationType.UnityObject);
        }

        /// <summary>
        /// Gets an object from its id.
        /// </summary>
        /// <param name="id">The object id.</param>
        /// <returns>The object referenced by the id or null if the id is invalid.</returns>
        public static UnityEngine.Object GetEditorObject(UntypedWeakReferenceId id)
        {
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(UnsafeUtility.As<RuntimeGlobalObjectId, GlobalObjectId>(ref id.GlobalId));
        }
#endif
    }
}
