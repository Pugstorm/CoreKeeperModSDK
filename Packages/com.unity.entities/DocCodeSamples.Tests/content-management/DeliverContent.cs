namespace Doc.CodeSamples.Tests
{
    #region example
    using System;
    using Unity.Entities.Content;
    using UnityEngine;

    public class GameStarter : MonoBehaviour
    {
        public string remoteUrlRoot;
        public string initialContentSet;

        void Start()
        {
#if ENABLE_CONTENT_DELIVERY
            ContentDeliveryGlobalState.Initialize(remoteUrlRoot, Application.persistentDataPath + "/content-cache", initialContentSet, s =>
            {
                if (s >= ContentDeliveryGlobalState.ContentUpdateState.ContentReady)
                    LoadMainScene();
            });
#else
            LoadMainScene();
#endif
        }

        void LoadMainScene()
        {
            //content is ready here...
        }
    }
    #endregion
}
