using System.Collections.Generic;
using System.Linq;
using CK_QOL.Core;
using CK_QOL.Core.Features;
using CK_QOL.Features.CraftingRange;
using CK_QOL.Features.ItemPickUpNotifier;
using CK_QOL.Features.NoDeathPenalty;
using CK_QOL.Features.NoEquipmentDurabilityLoss;
using CK_QOL.Features.QuickEat;
using CK_QOL.Features.QuickHeal;
using CK_QOL.Features.QuickStash;
using CoreLib;
using CoreLib.Localization;
using CoreLib.RewiredExtension;
using CoreLib.Util.Extensions;
using PugMod;
using Rewired;
using UnityEngine;

namespace CK_QOL
{
	public class Entry : IMod
	{
		internal static LoadedMod ModInfo { get; private set; }
		internal static Player RewiredPlayer { get; private set; }
		
		private readonly List<IFeature> _features = new();

		#region IMod

		public void EarlyInit()
		{
			ModLogger.Info($"{ModSettings.Name} v{ModSettings.Version} by {ModSettings.Author}");

			ModInfo = this.GetModInfo();
			if (ModInfo is null)
			{
				ModLogger.Error("Failed to load!");
				Shutdown();

				return;
			}

			CoreLibMod.LoadModules(typeof(LocalizationModule));
			CoreLibMod.LoadModule(typeof(RewiredExtensionModule));

			RewiredExtensionModule.rewiredStart += () => RewiredPlayer = ReInput.players.GetPlayer(0);
			
			ModLogger.Info("Loading features..");
			
			_features.AddRange(new IFeature[]
			{
				CraftingRange.Instance,
				QuickStash.Instance,
				ItemPickUpNotifier.Instance,
				NoDeathPenalty.Instance,
				NoEquipmentDurabilityLoss.Instance,
				QuickHeal.Instance,
				QuickEat.Instance
			});

			foreach (var feature in _features)
			{
				ModLogger.Info($"{feature.DisplayName} ({feature.FeatureType})");
			}
			
			ModLogger.Info(".. all features loaded.");
		}

		public void Init()
		{
			ModLogger.Info("Enabled features with their configuration:");

			foreach (var feature in _features.Where(feature => feature.IsEnabled))
			{
				switch (feature)
				{
					case CraftingRange { IsEnabled: true } craftingRange:
						ModLogger.Info($"{feature.DisplayName} | {nameof(craftingRange.MaxRange)}: {craftingRange.MaxRange} ");
						ModLogger.Info($"{feature.DisplayName} | {nameof(craftingRange.MaxChests)}: {craftingRange.MaxChests}");
						break;
					case QuickStash { IsEnabled: true } quickStash:
						ModLogger.Info($"{feature.DisplayName} | {nameof(quickStash.MaxRange)}: {quickStash.MaxRange} ");
						ModLogger.Info($"{feature.DisplayName} | {nameof(quickStash.MaxChests)}: {quickStash.MaxChests}");
						break;
					case ItemPickUpNotifier { IsEnabled: true } itemPickUpNotifier:
						ModLogger.Info($"{feature.DisplayName} | {nameof(itemPickUpNotifier.AggregateDelay)}: {itemPickUpNotifier.AggregateDelay}");
						break;
					case NoDeathPenalty { IsEnabled: true } noDeathPenalty:
						ModLogger.Info($"{feature.DisplayName}");
						break;
					case NoEquipmentDurabilityLoss { IsEnabled: true } noEquipmentDurabilityLoss:
						ModLogger.Info($"{feature.DisplayName}");
						break;
					case QuickHeal { IsEnabled: true } quickHeal:
						ModLogger.Info($"{feature.DisplayName} | {nameof(quickHeal.EquipmentSlotIndex)}: {quickHeal.EquipmentSlotIndex}");
						break;
					case QuickEat { IsEnabled: true } quickEat:
						ModLogger.Info($"{feature.DisplayName} | {nameof(quickEat.EquipmentSlotIndex)}: {quickEat.EquipmentSlotIndex}");
						break;
				}
			}
			
			ModLogger.Info("Loaded successfully.");
		}

		public void Shutdown()
		{
			ModLogger.Warn("Shutdown initiated.");
		}

		public void ModObjectLoaded(Object obj)
		{
		}

		public void Update()
		{
			foreach (var feature in _features.Where(feature => feature.IsEnabled))
			{
				feature.Update();
			}
		}

		#endregion IMod
	}
}