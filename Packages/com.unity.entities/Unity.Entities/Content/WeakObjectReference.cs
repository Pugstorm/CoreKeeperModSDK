using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities.Serialization;
using UnityEngine;

namespace Unity.Entities.Content
{
    /// <summary>
    /// Weak reference to an object.  This allows control over when an object is loaded and unloaded.
    /// </summary>
    /// <typeparam name="TObject">The type of UnityEngine.Object this reference points to.</typeparam>
    [Serializable]
    public struct WeakObjectReference<TObject> : IEquatable<WeakObjectReference<TObject>> where TObject : UnityEngine.Object
    {
        [SerializeField]
        internal UntypedWeakReferenceId Id;

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
                if (Id.GenerationType != WeakReferenceGenerationType.UnityObject)
                    return false;

                if (UntypedWeakReferenceId.GetEditorObject(Id) == null)
                    return false;
#endif
                return true;
            }
        }

        /// <summary>
        /// Get the loading status of the referenced object.
        /// </summary>
        public ObjectLoadingStatus LoadingStatus => RuntimeContentManager.GetObjectLoadingStatus(Id);

        /// <summary>
        /// The value of the referenced object.  This returns a valid object if IsLoaded is true.
        /// </summary>
        public TObject Result
        {
            get
            {
                return RuntimeContentManager.GetObjectValue<TObject>(Id);
            }
        }

#if UNITY_EDITOR
        public WeakObjectReference(TObject unityObject)
        {
            this.Id = UntypedWeakReferenceId.CreateFromObjectInstance(unityObject);
        }
#endif
        /// <summary>
        /// Weak reference to an object.
        /// </summary>
        /// <param name="id">The UntypedWeakReferenceId of the object.</param>
        public WeakObjectReference(UntypedWeakReferenceId id)
        {
            this.Id = id;
        }

        /// <summary>
        /// Directs the object to begin loading.  This will increase the reference count for each call to the same id.  Release must be called for each Load call to properly release resources.
        /// </summary>
        public void LoadAsync()
        {
            RuntimeContentManager.LoadObjectAsync(Id);
        }

        /// <summary>
        /// Releases the object.  This will decrement the reference count of this object.  When an objects reference count reaches 0, the archive file is released.  The archive file is only
        /// unloaded when its reference count reaches zero, which will then release the archive it was loaded from.  Archives will be unmounted when their reference count reaches 0.
        /// </summary>
        public void Release()
        {
            RuntimeContentManager.ReleaseObjectAsync(Id);
        }

        /// <summary>
        /// Wait for object load in main thread.  This will force synchronous loading and may cause performance issues.
        /// </summary>
        /// <param name="timeoutMs">The number of milliseconds to wait.  If set to 0, the load will either complet or fail before returning.</param>
        /// <returns>True if the load completes within the timeout.</returns>
        public bool WaitForCompletion(int timeoutMs = 0)
        {
            return RuntimeContentManager.WaitForObjectCompletion(Id, timeoutMs);
        }

        /// <summary>
        /// String conversion override.
        /// </summary>
        /// <returns>String representation of reference which includes type, guid and local id.</returns>
        public override string ToString() => $"WeakObjectReference<{typeof(TObject)}> -> {Id}";

        /// <inheritdoc/>
        public bool Equals(WeakObjectReference<TObject> other)
        {
            return Id.Equals(other.Id);
        }

        /// <summary>
        /// Gets the hash code of this reference.
        /// </summary>
        /// <returns>The hash code of this reference.</returns>
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
