using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.SceneManagement;

namespace UnityEngine.ResourceManagement.ResourceProviders
{
    /// <summary>
    /// How to release the Addressable scene
    /// </summary>
    public enum SceneReleaseMode
    {
        /// <summary>
        /// Release the scene handle when the scene is unloaded
        /// </summary>
        ReleaseSceneWhenSceneUnloaded = 0,

        /// <summary>
        /// Do not release the scene handle on scene unload. Requires manual call to Release in order to ensure
        /// AssetBundle is unloaded properly
        /// </summary>
        OnlyReleaseSceneOnHandleRelease,
    }

    /// <summary>
    /// Wrapper for scenes.  This is used to allow access to the AsyncOperation and delayed activation.
    /// </summary>
    public struct SceneInstance
    {
        Scene m_Scene;
        bool m_ReleaseOnSceneUnloaded;
        internal AsyncOperation m_Operation;

        /// <summary>
        /// The scene instance.
        /// </summary>
        public Scene Scene
        {
            get { return m_Scene; }
            internal set { m_Scene = value; }
        }

        internal bool ReleaseSceneOnSceneUnloaded
        {
            get { return m_ReleaseOnSceneUnloaded; }
            set { m_ReleaseOnSceneUnloaded = value; }
        }

        /// <summary>
        /// Activate the scene via the AsyncOperation.  This is the scene loading AsyncOperation provided by the engine.
        /// The documentation for AsyncOperation can be found here: https://docs.unity3d.com/ScriptReference/AsyncOperation.html
        /// </summary>
        /// <returns>The scene load operation.</returns>
        public AsyncOperation ActivateAsync()
        {
            m_Operation.allowSceneActivation = true;
            return m_Operation;
        }

        ///<inheritdoc cref="Scene"/>
        public override int GetHashCode()
        {
            return Scene.GetHashCode();
        }

        /// <inheritdoc cref="Scene"/>
        public override bool Equals(object obj)
        {
            if (!(obj is SceneInstance))
                return false;
            return Scene.Equals(((SceneInstance)obj).Scene);
        }
    }

    /// <summary>
    /// Interface for scene providers.
    /// </summary>
    public interface ISceneProvider
    {
        /// <summary>
        /// Load a scene at a specified resource location.
        /// </summary>
        /// <param name="resourceManager">The resource manager to use for loading dependencies.</param>
        /// <param name="location">The location of the scene.</param>
        /// <param name="loadMode">Load mode for the scene.</param>
        /// <param name="activateOnLoad">If true, the scene is activated as soon as it finished loading. Otherwise it needs to be activated via the returned SceneInstance object.</param>
        /// <param name="priority">The loading priority for the load.</param>
        /// <returns>An operation handle for the loading of the scene.  The scene is wrapped in a SceneInstance object to support delayed activation.</returns>
        AsyncOperationHandle<SceneInstance> ProvideScene(ResourceManager resourceManager, IResourceLocation location, LoadSceneMode loadMode, bool activateOnLoad, int priority);

        /// <summary>
        /// Load a scene at a specified resource location.
        /// </summary>
        /// <param name="resourceManager">The resource manager to use for loading dependencies.</param>
        /// <param name="location">The location of the scene.</param>
        /// <param name="loadSceneParameters">Load parameters for the scene.</param>
        /// <param name="activateOnLoad">If true, the scene is activated as soon as it finished loading. Otherwise it needs to be activated via the returned SceneInstance object.</param>
        /// <param name="priority">The loading priority for the load.</param>
        /// <returns>An operation handle for the loading of the scene.  The scene is wrapped in a SceneInstance object to support delayed activation.</returns>
        AsyncOperationHandle<SceneInstance> ProvideScene(ResourceManager resourceManager, IResourceLocation location, LoadSceneParameters loadSceneParameters, bool activateOnLoad, int priority);

        /// <summary>
        /// Load a scene at a specified resource location.
        /// </summary>
        /// <param name="resourceManager">The resource manager to use for loading dependencies.</param>
        /// <param name="location">The location of the scene.</param>
        /// <param name="loadSceneParameters">Load parameters for the scene.</param>
        /// <param name="releaseMode">How the scene is handled if it is unloaded due to another scene loading using single mode.</param>
        /// <param name="activateOnLoad">If true, the scene is activated as soon as it finished loading. Otherwise it needs to be activated via the returned SceneInstance object.</param>
        /// <param name="priority">The loading priority for the load.</param>
        /// <returns>An operation handle for the loading of the scene.  The scene is wrapped in a SceneInstance object to support delayed activation.</returns>
        AsyncOperationHandle<SceneInstance> ProvideScene(ResourceManager resourceManager, IResourceLocation location, LoadSceneParameters loadSceneParameters, SceneReleaseMode releaseMode, bool activateOnLoad, int priority);

        /// <summary>
        /// Release a scene.
        /// </summary>
        /// <param name="resourceManager">The resource manager to use for loading dependencies.</param>
        /// <param name="sceneLoadHandle">The operation handle used to load the scene.</param>
        /// <returns>An operation handle for the unload.</returns>
        AsyncOperationHandle<SceneInstance> ReleaseScene(ResourceManager resourceManager, AsyncOperationHandle<SceneInstance> sceneLoadHandle);
    }

    internal interface ISceneProvider2 : ISceneProvider
    {
        /// <summary>
        /// Release a scene.
        /// </summary>
        /// <param name="resourceManager">The resource manager to use for loading dependencies.</param>
        /// <param name="sceneLoadHandle">The operation handle used to load the scene.</param>
        /// <returns>An operation handle for the unload.</returns>
        AsyncOperationHandle<SceneInstance> ReleaseScene(ResourceManager resourceManager, AsyncOperationHandle<SceneInstance> sceneLoadHandle, UnloadSceneOptions unloadOptions);
    }

    static internal class SceneProviderExtensions
    {
        public static AsyncOperationHandle<SceneInstance> ReleaseScene(this ISceneProvider provider, ResourceManager resourceManager, AsyncOperationHandle<SceneInstance> sceneLoadHandle,
            UnloadSceneOptions unloadOptions)
        {
            if (provider is ISceneProvider2)
                return ((ISceneProvider2)provider).ReleaseScene(resourceManager, sceneLoadHandle, unloadOptions);
            return provider.ReleaseScene(resourceManager, sceneLoadHandle);
        }
    }
}
