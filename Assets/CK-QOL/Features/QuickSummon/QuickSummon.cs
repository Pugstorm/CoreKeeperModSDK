using System.Linq;
using CK_QOL.Core;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.QuickSummon
{
	internal sealed class QuickSummon : QuickActionFeatureBase<QuickSummon>
	{
		private readonly ObjectID[] _tomeIDs =
		{
			ObjectID.TomeOfRange,
			ObjectID.TomeOfOrbit,
			ObjectID.TomeOfMelee
		};

		public QuickSummon()
		{
			ConfigBase.Create(this);
			IsEnabled = QuickSummonConfig.ApplyIsEnabled(this);
			EquipmentSlotIndex = QuickSummonConfig.ApplyEquipmentSlotIndex(this);

			RewiredExtensionModule.AddKeybind(KeyBindName, DisplayName, KeyboardKeyCode.X);
		}

		protected override bool IsTargetItem(ObjectDataCD objectData)
		{
			return _tomeIDs.Contains(objectData.objectID);
		}

		#region IFeature

		public override string Name => nameof(QuickSummon);
		public override string DisplayName => "Quick Summon";
		public override string Description => "Quickly equips a summoning tome, casts a summon spell, and swaps back to the previous item.";
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature

		#region Configurations

		public override int EquipmentSlotIndex { get; }
		protected override string KeyBindName => $"{ModSettings.ShortName}_{Name}";

		#endregion Configurations
	}
}