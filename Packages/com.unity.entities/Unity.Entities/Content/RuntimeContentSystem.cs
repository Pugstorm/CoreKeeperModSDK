using System;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using UnityEngine.LowLevel;

namespace Unity.Entities.Content
{
    /// <summary>
    /// System responsible for initializing and updating the <seealso cref="RuntimeContentManager"/>.
    /// </summary>
    public static class RuntimeContentSystem
    {
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void EditorInitialize()
        {
            AddToEditorLoop();
            UnityEditor.EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
        }

        private static void EditorApplication_playModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            switch (state)
            {
                case UnityEditor.PlayModeStateChange.ExitingEditMode:
                    RemoveFromEditorLoop();
                    break;
                case UnityEditor.PlayModeStateChange.EnteredPlayMode:
                    AddToPlayerLoop();
                    break;
                case UnityEditor.PlayModeStateChange.ExitingPlayMode:
                    RemoveFromPlayerLoop();
                    break;
                case UnityEditor.PlayModeStateChange.EnteredEditMode:
                    AddToEditorLoop();
                    break;
            }
        }

        static void AddToEditorLoop()
        {
            UnityEditor.EditorApplication.update += Update;
        }

        static void RemoveFromEditorLoop()
        {
            UnityEditor.EditorApplication.update -= Update;
        }
#else
        [RuntimeInitializeOnLoadMethod]
        static void RuntimeInitialize()
        { 
#if !ENABLE_CONTENT_DELIVERY
            LoadContentCatalog(null, null, null, false);
#endif
            AddToPlayerLoop();
        }
#endif
        static void RemoveFromPlayerLoop()
        {
            ScriptBehaviourUpdateOrder.RemoveFromCurrentPlayerLoop(Update);
        }

        static void AddToPlayerLoop()
        {
            //cannot use ScriptBehaviourUpdateOrder here bc the current player loop at this point
            //has a null type and it fails to add
            var playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            var oldListLength = playerLoop.subSystemList != null ? playerLoop.subSystemList.Length : 0;
            var newSubsystemList = new PlayerLoopSystem[oldListLength + 1];
            for (var i = 0; i < oldListLength; ++i)
                newSubsystemList[i] = playerLoop.subSystemList[i];
            newSubsystemList[oldListLength] = new PlayerLoopSystem
            {
                type = typeof(RuntimeContentSystem),
                updateDelegate = Update
            };
            playerLoop.subSystemList = newSubsystemList;
            PlayerLoop.SetPlayerLoop(playerLoop);
        }

        static void Update()
        {
            //always update the CDGS in the player so that the catalog can load
#if ENABLE_CONTENT_DELIVERY && !UNITY_EDITOR
            ContentDeliveryGlobalState.Update();
#endif

#if !UNITY_EDITOR  //only update RCM in the player if the catalog has been loaded
            if(RuntimeContentManager.IsReady)
#endif
            RuntimeContentManager.ProcessQueuedCommands();
        }


        /// <summary>
        /// Loads the content catalog data.
        /// </summary>
        /// <remarks>
        /// By default, this loads the catalog from the StreamingAssets folder.
        /// However, if you've set the 'ENABLE_CONTENT_DELIVERY' define, this initiates the content delivery system and updates the content before loading the catalog.
        ///</remarks>
        /// <param name="remoteUrlRoot">The remote URL root for the content. Set null or leave empty, to load the catalog from the local StreamingAssets path.</param>
        /// <param name="localCachePath">Optional path for the local cache. Set null or leave empty to create a folder named 'ContentCache' in the device's Application.persistentDataPath.</param>
        /// <param name="initialContentSet">Initial content set to download.  'all' is generally used to denote the entire content set.</param>
        /// <param name="allowOverrideArgs">Set to true, to use application command line arguments to override the passed in values.</param>
        public static void LoadContentCatalog(string remoteUrlRoot, string localCachePath, string initialContentSet, bool allowOverrideArgs = false)
        {
#if ENABLE_CONTENT_DELIVERY
            if (allowOverrideArgs)
            {
                if (TryGetAppArg("remoteRoot", ref remoteUrlRoot))
                    ContentDeliveryGlobalState.LogFunc?.Invoke($"Overwrote remoteRoot to '{remoteUrlRoot}'");
                if (TryGetAppArg("cachePath", ref localCachePath))
                    ContentDeliveryGlobalState.LogFunc?.Invoke($"Overwrote cachePath to '{localCachePath}'");
                if (TryGetAppArg("contentSet", ref initialContentSet))
                    ContentDeliveryGlobalState.LogFunc?.Invoke($"Overwrote contentSet '{initialContentSet}'");
            }

            if (string.IsNullOrEmpty(remoteUrlRoot))
            {
                if (!string.IsNullOrEmpty(localCachePath))
                {
                    ContentDeliveryGlobalState.Initialize("", localCachePath, null, s =>
                    {
                        if (s >= ContentDeliveryGlobalState.ContentUpdateState.ContentReady)
                            LoadCatalogFunc(ContentDeliveryGlobalState.PathRemapFunc);
                    });
                }
                else
                {
                    //even though ENABLE_CONTENT_DELIVERY is enabled, still allow the local catalog to be used without checking for updates.
                    LoadCatalogFunc(ContentDeliveryGlobalState.PathRemapFunc = p => $"{Application.streamingAssetsPath}/{p}");
                }
            }
            else
            {
                //if no cache specified, set to default
                if (string.IsNullOrEmpty(localCachePath))
                    localCachePath = System.IO.Path.Combine(Application.persistentDataPath, "ContentCache");
                //start content update process
                ContentDeliveryGlobalState.Initialize(remoteUrlRoot, localCachePath, initialContentSet, s =>
                {
                    if (s >= ContentDeliveryGlobalState.ContentUpdateState.ContentReady)
                        LoadCatalogFunc(ContentDeliveryGlobalState.PathRemapFunc);
                });
            }

#else
            LoadCatalogFunc(p => $"{Application.streamingAssetsPath}/{p}");
#endif
        }

        static void LoadCatalogFunc(Func<string, string> remapFunc)
        {
            var catalogPath = remapFunc(RuntimeContentManager.RelativeCatalogPath);
            if (ContentDeliveryGlobalState.FileExists(catalogPath))
                RuntimeContentManager.LoadLocalCatalogData(catalogPath,
                    RuntimeContentManager.DefaultContentFileNameFunc,
                    p => remapFunc(RuntimeContentManager.DefaultArchivePathFunc(p)));
        }

        static bool TryGetAppArg(string name, ref string value)
        {
            if (!Application.HasARGV(name))
                return false;

            value = Application.GetValueForARGV(name);
            return true;
        }
    }
}
