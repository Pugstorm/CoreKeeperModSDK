#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

namespace Unity.Entities.Build
{
    /// <summary>
    /// Interface for build player settings assets.
    /// </summary>
    public interface IEntitiesPlayerSettings
    {
        /// <summary>
        /// Gets the list of assemblies to be excluded during baking.
        /// </summary>
        /// <returns>Returns the serialized <see cref="BakingSystemFilterSettings"/>.</returns>
        public BakingSystemFilterSettings GetFilterSettings();

        /// <summary>
        /// Gets the set of scripting defines applied while building the player.
        /// </summary>
        /// <returns>Returns the array of scripting defines.</returns>
        public string[] GetAdditionalScriptingDefines();

        /// <summary>
        /// Returns the backing ScriptableObject.
        /// </summary>
        /// <returns>Returns a ScriptableObject.</returns>
        public ScriptableObject AsScriptableObject();

        /// <summary>
        /// Register the hash of the object as a custom dependency in the AssetDatabase.
        /// </summary>
        /// <remarks>This allows the AssetDatabase to reimport the subscenes that depend on these settings.
        /// Make sure this call is not made during the Asset import process or it will throw the exception "AssetDatabase.RegisterCustomDependency are restricted during importing"</remarks>
        public void RegisterCustomDependency();

        /// <summary>
        /// Gets the hash of the settings asset.
        /// </summary>
        /// <returns>Returns the hash as a <see cref="Hash128"/> value.</returns>
        public UnityEngine.Hash128 GetHash();

        /// <summary>
        /// The custom dependency key.
        /// </summary>
        public string CustomDependency { get; }

        /// <summary>
        /// The unique ID of the settings asset.
        /// </summary>
        public Hash128 GUID { get; }
    }

    /// <summary>
    /// Base class for creating an Entities player asset in the Assets folder.
    /// </summary>
    public abstract class DotsPlayerSettingsProvider
    {
        protected IEntitiesPlayerSettings m_SettingsOverride;

        /// <summary>
        /// Importance of the asset compared with other potential assets of the same player type.
        /// </summary>
        /// <remarks>The provider with the higher importance value only will be taken into account in the project settings.</remarks>
        public virtual int Importance
        {
            get { return 0; }
        }

        /// <summary>
        /// Provides extra scripting defines to add into the player build.
        /// </summary>
        /// <returns>Returns the array of scripting defines.</returns>
        public virtual string[] GetExtraScriptingDefines()
        {
            return Array.Empty<string>();
        }

        /// <summary>
        /// Provides additional BuildOptions to the player build.
        /// </summary>
        /// <returns></returns>
        public virtual BuildOptions GetExtraBuildOptions()
        {
            return BuildOptions.None;
        }

        /// <summary>
        /// Create the UI of the asset in the project settings window.
        /// </summary>
        /// <param name="type">The player type.</param>
        /// <param name="rootElement">The root element for the UI.</param>
        public abstract void OnActivate(DotsGlobalSettings.PlayerType type, VisualElement rootElement);

        /// <summary>
        /// Returns the settings object for this provider.
        /// </summary>
        /// <returns>Returns the serialized instance of <see cref="IEntitiesPlayerSettings"/>.</returns>
        protected abstract IEntitiesPlayerSettings DoGetSettingAsset();

        /// <summary>
        /// Returns the settings object for this provider.
        /// </summary>
        /// <returns>Returns the serialized instance of <see cref="IEntitiesPlayerSettings"/>.</returns>
        /// <remarks>The settings object can be overridden.
        /// If an override is active, this method will return it instead of the default settings object.</remarks>
        public IEntitiesPlayerSettings GetSettingAsset()
        {
            return m_SettingsOverride ?? DoGetSettingAsset();
        }

        /// <summary>
        /// Override the settings object defined by this provider.
        /// </summary>
        /// <param name="newSettings">The <see cref="IEntitiesPlayerSettings"/> asset.</param>
        /// <remarks>
        /// Use this method to customize the build settings from a script.<br/>
        /// Note: The override reference is not preserved during domain reloads and does not affect the default
        /// settings which can be edited through the project settings.
        /// </remarks>
        public void SetSettingAsset(IEntitiesPlayerSettings newSettings)
        {
            m_SettingsOverride = newSettings;
        }

        /// <summary>
        /// Returns the GUID of the asset.
        /// </summary>
        /// <returns>Returns the GUID of the player settings asset.</returns>
        /// <remarks>If there is a settings override, the GUID will correspond to the override rather than the default settings asset.</remarks>
        public Hash128 GetPlayerSettingGUID()
        {
            var settingsAsset = GetSettingAsset();

            if (settingsAsset == null)
            {
                UnityEngine.Debug.LogError("GetPlayerSettingGUID() - GetSettingsAsset() returned null");
                return default;
            }

            return settingsAsset.GUID;        }

        /// <summary>
        /// Return the PlayerType of the asset (Client or Server).
        /// </summary>
        /// <returns>Returns the <see cref="DotsGlobalSettings.PlayerType"/> of the asset.</returns>
        public abstract DotsGlobalSettings.PlayerType GetPlayerType();

        /// <summary>
        /// Forcibly reload the setting asset from disk. Ensure the in-memory state is consistent with the serialized data.
        /// </summary>
        internal void ReloadSettingAsset()
        {
            DoReloadAsset();
        }

        /// <summary>
        /// Forcibly reload the setting asset from disk. Concrete class can implement their own restoring strategy.
        /// </summary>
        protected virtual void DoReloadAsset()
        {
            ReloadAsset(GetSettingAsset());
        }

        protected void ReloadAsset(IEntitiesPlayerSettings settings)
        {
            if (settings != null)
            {
                UnityEngine.Object.DestroyImmediate(settings.AsScriptableObject());
                var settingsType = settings.GetType();
                var instanceField = settingsType.BaseType.GetField("s_Instance", BindingFlags.Static | BindingFlags.NonPublic);
                instanceField.SetValue(null, null);
                var loadMethod = settingsType.BaseType.GetMethod("CreateAndLoad", BindingFlags.Static | BindingFlags.NonPublic);
                loadMethod.Invoke(null, null);
            }
        }
    }

    /// <summary>
    /// Base class for accessing Dots project settings.
    /// </summary>
    public class DotsGlobalSettings
    {
        /// <summary>
        /// The type of player that will be built.
        /// </summary>
        public enum PlayerType
        {
            /// <summary>
            /// The player instance is a client.
            /// </summary>
            Client,
            /// <summary>
            /// The player instance is a server.
            /// </summary>
            Server
        }

        /// <summary>
        /// The client player settings provider.
        /// </summary>
        public DotsPlayerSettingsProvider ClientProvider { get; private set; }
        /// <summary>
        /// The server player settings provider.
        /// </summary>
        public DotsPlayerSettingsProvider ServerProvider { get; private set; }

        static DotsGlobalSettings s_Instance;
        internal List<DotsPlayerSettingsProvider> Providers = new List<DotsPlayerSettingsProvider>();

        /// <summary>
        /// Returns the <see cref="PlayerType"/> Client or Server based on if a dedicated server platform is selected in the build settings or not.
        /// </summary>
        /// <returns>Returns the <see cref="PlayerType"/> for the build.</returns>
        public PlayerType GetPlayerType()
        {
            //When switching from dedicated server to other standalone platform, the standaloneBuildSubtarget
            //is not reset. At the moment this is the safest and suggest check to verify we are building for the
            //dedicated server platform.
            #if UNITY_SERVER
            return PlayerType.Server;
            #else
            return PlayerType.Client;
            #endif
        }

        /// <summary>
        /// The <see cref="DotsGlobalSettings"/> instance.
        /// </summary>
        public static DotsGlobalSettings Instance {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = new DotsGlobalSettings();
                    s_Instance.AddExtraDotsSettings();
                }

                return s_Instance;
            }
        }

        void AddExtraDotsSettings()
        {
            s_Instance.Providers = new List<DotsPlayerSettingsProvider>();

            var types = TypeCache.GetTypesDerivedFrom<DotsPlayerSettingsProvider>();

            if (ClientProvider == null)
            {
                foreach (var type in types)
                {
                    var instance = (DotsPlayerSettingsProvider) Activator.CreateInstance(type);
                    if (instance.GetPlayerType() == PlayerType.Client)
                    {
                        if (ClientProvider == null)
                            ClientProvider = instance;
                        else if (instance.Importance > ClientProvider.Importance)
                            ClientProvider = instance;

                        instance.GetSettingAsset().RegisterCustomDependency();
                    }
                }
            }

            if (ClientProvider != null)
            {
                Providers.Add(ClientProvider);
            }

            if (ServerProvider == null) // TODO: skip if a server build target is not installed
            {
                foreach (var type in types)
                {
                    var instance = (DotsPlayerSettingsProvider) Activator.CreateInstance(type);
                    if (instance.GetPlayerType() == PlayerType.Server)
                    {
                        if (ServerProvider == null)
                            ServerProvider = instance;
                        else if (instance.Importance > ServerProvider.Importance)
                            ServerProvider = instance;

                        instance.GetSettingAsset().RegisterCustomDependency();
                    }
                }
            }

            if (ServerProvider != null)
            {
                Providers.Add(ServerProvider);
            }
        }

        /// <summary>
        /// Gets the <see cref="IEntitiesPlayerSettings"/> settings asset corresponding to the guid.
        /// </summary>
        /// <param name="guid">The unique ID of the build settings asset.</param>
        /// <returns>Returns the settings asset corresponding to the guid, or null if not found.</returns>
        public IEntitiesPlayerSettings GetSettingsAsset(Hash128 guid)
        {
            foreach (var p in Providers)
            {
                var asset = p.GetSettingAsset();
                if (asset.GUID == guid)
                    return asset;
            }

            return null;
        }

        /// <summary>
        /// Gets the Client Setting asset.
        /// </summary>
        /// <returns>Returns the <see cref="IEntitiesPlayerSettings"/> asset for the client.
        /// If there is no provider for the client target, returns null.</returns>
        public IEntitiesPlayerSettings GetClientSettingAsset()
        {
            return ClientProvider?.GetSettingAsset();
        }

        /// <summary>
        /// Gets the GUID of the Client asset.
        /// </summary>
        /// <returns>Returns the GUID of the Client asset.</returns>
        /// <remarks>If there is no provider for the client target, returns an invalid <see cref="Entities.Hash128"/> value.</remarks>
        public Hash128 GetClientGUID()
        {
            if (ClientProvider == null)
            {
                Debug.LogError($"GetClientGUID() - ClientProvider is NULL");
                return default;
            }
            return ClientProvider.GetPlayerSettingGUID();
        }

        /// <summary>
        /// Gets the Server Setting asset.
        /// </summary>
        /// <returns>Returns the <see cref="IEntitiesPlayerSettings"/> asset for the server.</returns>
        /// <remarks>If there is no provider for the server target, returns null.</remarks>
        public IEntitiesPlayerSettings GetServerSettingAsset()
        {
            return ServerProvider?.GetSettingAsset();
        }

        /// <summary>
        /// Gets the GUID of the Server asset.
        /// </summary>
        /// <returns>Returns the GUID of the Server asset.</returns>
        public Hash128 GetServerGUID()
        {
            if (ServerProvider == null)
                return default;
            return ServerProvider.GetPlayerSettingGUID();
        }

        internal void ReloadSettingsObjects()
        {
            foreach (var provider in Providers)
            {
                provider.ReloadSettingAsset();
            }
        }
    }
}
#endif
