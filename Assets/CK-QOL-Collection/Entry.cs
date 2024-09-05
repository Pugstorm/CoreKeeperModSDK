using CK_QOL_Collection.Core;
using CK_QOL_Collection.Features;
using CoreLib;
using CoreLib.Data.Configuration;
using CoreLib.Localization;
using CoreLib.ModResources;
using CoreLib.RewiredExtension;
using CoreLib.Util.Extensions;
using PugMod;
using Rewired;
using UnityEngine;
using Logger = CK_QOL_Collection.Core.Logger;

namespace CK_QOL_Collection
{
    /// <summary>
    /// The main entry point for the CK QOL Collection mod. Implements the <see cref="IMod"/> interface for mod lifecycle management.
    /// </summary>
    public class Entry : IMod
    {
        /// <summary>
        /// The version of the mod.
        /// </summary>
        public const string Version = "1.0.1";
        /// <summary>
        /// The name of the mod.
        /// </summary>
        public const string Name = "CK QOF Collection";
        /// <summary>
        /// The author of the mod.
        /// </summary>
        public const string Author = "DrSalzstreuer";
        
        private LoadedMod _modInfo;
        private ConfigFile _modConfig;
        
        public static Player RewiredPlayer { get; private set; }
        
        #region IMod
        
        /// <summary>
        /// Called early in the mod lifecycle to perform initial setup.
        /// </summary>
        /// <inheritdoc />
        public void EarlyInit()
        {
            Logger.Info($"{Version} - {Author}");
            
            _modInfo = this.GetModInfo();
            if (_modInfo is null)
            {
                Logger.Error("Failed to load!");
                Shutdown();
                return;
            }
            
            ResourcesModule.RegisterBundles(_modInfo);
            CoreLibMod.LoadModule(typeof(RewiredExtensionModule));
            CoreLibMod.LoadModules(typeof(LocalizationModule));
            
            _modConfig = Configuration.Initialize(_modInfo);
            
            RewiredExtensionModule.rewiredStart += () => RewiredPlayer = ReInput.players.GetPlayer(0);
            
            if (Configuration.Sections.General.IsEnabled)
            {
                return;
            }
            
            Logger.Error("Mod is disabled by configuration!");
            Shutdown();
        }

        /// <summary>
        /// Called after <see cref="EarlyInit"/> to complete mod initialization.
        /// </summary>
        /// <inheritdoc />
        public void Init()
        {
            Logger.Info("Loaded successfully.");
        }

        /// <summary>
        /// Called when the mod is being shut down.
        /// </summary>
        /// <inheritdoc />
        public void Shutdown()
        {
            Logger.Info("Mod shutdown initiated.");
        }

        /// <summary>
        /// Called when a mod object is loaded.
        /// </summary>
        /// <param name="obj">The loaded object.</param>
        /// <inheritdoc />
        public void ModObjectLoaded(Object obj)
        {
            
        }

        /// <summary>
        /// Called every frame to update the mod state.
        /// </summary>
        /// <inheritdoc />
        public void Update()
        {
            if (!Configuration.Sections.General.IsEnabled)
            {
                return;
            }
            
            FeatureManager.Instance.Update();
        }
        
        #endregion IMod
    }
}