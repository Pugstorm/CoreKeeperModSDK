using CK_QOL_Collection.Core;
using CK_QOL_Collection.Core.Configuration;
using CoreLib;
using CoreLib.Localization;
using CoreLib.RewiredExtension;
using CoreLib.Util.Extensions;
using PugMod;
using Rewired;
using UnityEngine;
using Logger = CK_QOL_Collection.Core.Logger;

namespace CK_QOL_Collection
{
	/// <summary>
	///     The main entry point for the CK QOL Collection mod. Implements the <see cref="IMod" /> interface for mod lifecycle
	///     management.
	/// </summary>
	public class Entry : IMod
	{
		/// <summary>
		///     Gets the loaded mod information.
		/// </summary>
		internal static LoadedMod ModInfo;

		/// <summary>
		///     Gets the Rewired player instance for input handling.
		/// </summary>
		internal static Player RewiredPlayer { get; private set; }

		#region IMod

		/// <inheritdoc />
		public void EarlyInit()
		{
			Logger.Info($"{ModSettings.Version} - {ModSettings.Author}");

			ModInfo = this.GetModInfo();
			if (ModInfo is null)
			{
				Logger.Error("Failed to load!");
				Shutdown();

				return;
			}

			// Initialize core modules
			CoreLibMod.LoadModules(typeof(LocalizationModule));
			CoreLibMod.LoadModule(typeof(RewiredExtensionModule));

			RewiredExtensionModule.rewiredStart += () => RewiredPlayer = ReInput.players.GetPlayer(0);
			
			ConfigurationManager.Initialize(ModInfo);
			KeyBindManager.Initialize();
			
			if (ConfigurationManager.IsModEnabled)
			{
				return;
			}

			Logger.Error("Disabled by configuration!");
			Shutdown();
		}

		/// <inheritdoc />
		public void Init()
		{
			if (!ConfigurationManager.IsModEnabled)
			{
				return;
			}
			
			FeatureManager.Initialize();
			
			Logger.Info("Loaded successfully.");
		}

		/// <inheritdoc />
		public void Shutdown()
		{
			Logger.Info("Shutdown initiated.");
		}

		/// <inheritdoc />
		public void ModObjectLoaded(Object obj)
		{
		}

		/// <inheritdoc />
		public void Update()
		{
			if (!ConfigurationManager.IsModEnabled)
			{
				return;
			}

			FeatureManager.Instance.Update();
		}

		#endregion IMod
	}
}