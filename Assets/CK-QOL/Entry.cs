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
using CK_QOL.Features.QuickSummon;
using CK_QOL.Features.ShiftClick;
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
		private readonly List<IFeature> _features = new();
		internal static LoadedMod ModInfo { get; private set; }
		internal static Player RewiredPlayer { get; private set; }

		#region IMod

		public void EarlyInit()
		{
			ModLogger.Info($"{ModSettings.Name} v{ModSettings.Version} by {ModSettings.Author} with contributors {ModSettings.Contributors}");

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
				QuickEat.Instance,
				QuickSummon.Instance,
				ShiftClick.Instance
			});

			foreach (var feature in _features.OrderBy(feature => feature.IsEnabled))
			{
				ModLogger.Info($"{feature.DisplayName} ({feature.FeatureType})");

				if (feature.IsEnabled)
				{
					switch (feature)
					{
						case CraftingRange { IsEnabled: true } craftingRange:
							ModLogger.Info($"{nameof(craftingRange.MaxRange)}: {craftingRange.MaxRange} ");
							ModLogger.Info($"{nameof(craftingRange.MaxChests)}: {craftingRange.MaxChests}");

							break;
						case QuickStash { IsEnabled: true } quickStash:
							ModLogger.Info($"{nameof(quickStash.MaxRange)}: {quickStash.MaxRange} ");
							ModLogger.Info($"{nameof(quickStash.MaxChests)}: {quickStash.MaxChests}");

							break;
						case ItemPickUpNotifier { IsEnabled: true } itemPickUpNotifier:
							ModLogger.Info($"{nameof(itemPickUpNotifier.AggregateDelay)}: {itemPickUpNotifier.AggregateDelay}");

							break;
						case NoDeathPenalty { IsEnabled: true } noDeathPenalty:
							ModLogger.Info($"{feature.DisplayName}");

							break;
						case NoEquipmentDurabilityLoss { IsEnabled: true } noEquipmentDurabilityLoss:
							ModLogger.Info($"{feature.DisplayName}");

							break;
						case QuickHeal { IsEnabled: true } quickHeal:
							ModLogger.Info($"{nameof(quickHeal.EquipmentSlotIndex)}: {quickHeal.EquipmentSlotIndex}");

							break;
						case QuickEat { IsEnabled: true } quickEat:
							ModLogger.Info($"{nameof(quickEat.EquipmentSlotIndex)}: {quickEat.EquipmentSlotIndex}");

							break;
						case QuickSummon { IsEnabled: true } quickSummon:
							ModLogger.Info($"{nameof(quickSummon.EquipmentSlotIndex)}: {quickSummon.EquipmentSlotIndex}");

							break;
						case ShiftClick { IsEnabled: true } shiftClick:
							ModLogger.Info($"{feature.DisplayName}");

							break;
					}
				}
				else
				{
					ModLogger.Warn("Feature is disabled.");
				}
			}

			ModLogger.Info(".. all features loaded.");
		}

		public void Init()
		{
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