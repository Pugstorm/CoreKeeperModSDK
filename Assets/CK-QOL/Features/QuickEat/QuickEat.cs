using System;
using System.Collections.Generic;
using CK_QOL.Core;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.QuickEat
{
	internal sealed class QuickEat : QuickActionFeatureBase<QuickEat>
	{
		public QuickEat()
		{
			ConfigBase.Create(this);
			IsEnabled = QuickEatConfig.ApplyIsEnabled(this);
			EquipmentSlotIndex = QuickEatConfig.ApplyEquipmentSlotIndex(this);

			RewiredExtensionModule.AddKeybind(KeyBindName, DisplayName, KeyboardKeyCode.F);
		}

		protected override Func<KeyValuePair<int, ObjectDataCD>, object> SortingFunction =>
			item => PugDatabase.HasComponent<CookedFoodCD>(item.Value) ? 0 : 1;

		protected override bool IsTargetItem(ObjectDataCD objectData)
		{
			if (objectData.objectID == ObjectID.None || PugDatabase.HasComponent<PotionCD>(objectData))
			{
				return false;
			}

			return PugDatabase.GetObjectInfo(objectData.objectID, objectData.variation) is { objectType: ObjectType.Eatable };
		}

		#region IFeature

		public override string Name => nameof(QuickEat);
		public override string DisplayName => "Quick Eat";
		public override string Description => "Quickly equips or switches the preferred eatable item, consumes it, and swaps back to the previous item.";
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature

		#region Configurations

		public override int EquipmentSlotIndex { get; }
		protected override string KeyBindName => $"{ModSettings.ShortName}_{Name}";

		#endregion Configurations
	}
}