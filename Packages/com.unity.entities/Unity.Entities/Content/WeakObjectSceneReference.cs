using System;
using Unity.Entities.Serialization;
using UnityEngine.SceneManagement;

namespace Unity.Entities.Content
{
    /// <summary>
    /// Weak reference to a scene. The Result field can be accessed after the scene has completed loading.
    /// </summary>
    [Serializable]
    public struct WeakObjectSceneReference : IEquatable<WeakObjectSceneReference>
    {
        /// <summary>
        /// The reference Id.
        /// </summary>
        public UntypedWeakReferenceId Id;

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
                if (Id.GenerationType != WeakReferenceGenerationType.GameObjectScene)
                    return false;

                if (UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(UnityEditor.AssetDatabase.GUIDToAssetPath(Id.GlobalId.AssetGUID)) != typeof(UnityEditor.SceneAsset))
                    return false;
#endif
                return true;
            }
        }
        /// <summary>
        /// Loads a scene.
        /// </summary>
        /// <param name="loadParams">The scene loading parameters.</param>
        /// <returns>The scene instance that is loading. It will not necessarily be ready when it is returned.  The scene instance can be checked for its loading state.</returns>
        public Scene LoadAsync(Unity.Loading.ContentSceneParameters loadParams)
        {
            return RuntimeContentManager.LoadSceneAsync(Id, loadParams);
        }


        /// <summary>
        /// Unloads a scene.
        /// </summary>
        /// <param name="scene">The scene to unload.  The scene reference will be invalid when this method returns.</param>
        public void Unload(ref Scene scene)
        {
            RuntimeContentManager.UnloadScene(ref scene);
        }

        /// <summary>
        /// String conversion override.
        /// </summary>
        /// <returns>String representation of reference which includes type, guid and local id.</returns>
        public override string ToString() => $"WeakSceneReference -> {Id}";

        /// <inheritdoc/>
        public bool Equals(WeakObjectSceneReference other)
        {
            return Id.Equals(other.Id);
        }

        /// <summary>
        /// Returns the hash code of this reference.
        /// </summary>
        /// <returns>The hash code of this reference.</returns>
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }



        /// <summary>
        /// True if the object is either being loaded or already finished loading.
        /// </summary>
        [Obsolete("This property is no longer valid.  Check the loading status of the scene returned from LoadAsync.")]
        public Unity.Loading.SceneLoadingStatus LoadingStatus => Loading.SceneLoadingStatus.Failed;

        /// <summary>
        /// The loaded scene object.
        /// </summary>
        [Obsolete("This property is no longer valid.  Use the scene returned from LoadAsync.")]
        public Scene SceneResult => default;

        /// <summary>
        /// The ContentSceneFile that was loaded.  This can be used to manually integrate the loaded scene at the end of the frame.
        /// </summary>
        [Obsolete("This property is no longer valid.  The scene file does not exist in all cases (e.g. play mode).")]
        public Unity.Loading.ContentSceneFile SceneFileResult => default;

        /// <summary>
        /// Releases the object.  This will decrement the reference count of this object.  When an objects reference count reaches 0, the archive file is released.  The archive file is only
        /// unloaded when its reference count reaches zero, which will then release the archive it was loaded from.  Archives will be unmounted when their reference count reaches 0.
        /// </summary>
        [Obsolete("Release has been replaced with Unload(ref Scene scene).  You will need to use the scene returned from LoadAsync to unload the scene and its resources.")]
        public void Release() { }
    }
}
