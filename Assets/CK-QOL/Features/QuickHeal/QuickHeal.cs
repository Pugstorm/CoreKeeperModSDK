using CK_QOL.Core;
using CK_QOL.Core.Config;
using CK_QOL.Core.Features;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.QuickHeal
{
    internal sealed class QuickHeal : QuickActionFeatureBase<QuickHeal>
    {
        public QuickHeal()
        {
            ConfigBase.Create(this);
            RewiredExtensionModule.AddKeybind(KeyBindName, DisplayName, KeyboardKeyCode.G);
        }

        protected override bool IsTargetItem(ObjectDataCD objectData)
        {
            return objectData.objectID is ObjectID.HealingPotion or ObjectID.GreaterHealingPotion;
        }

        #region IFeature

        public override string Name => nameof(QuickHeal);
        public override string DisplayName => "Quick Heal";
        public override string Description => "Quickly equips a healable item (prefers potion), consumes it, and swaps back to the previous item.";
        public override FeatureType FeatureType => FeatureType.Client;

        #endregion IFeature

        #region Configurations

        public override bool IsEnabled => QuickHealConfig.ApplyIsEnabled(this);
        public override int EquipmentSlotIndex => QuickHealConfig.ApplyEquipmentSlotIndex(this);
        protected override string KeyBindName => $"{ModSettings.ShortName}_{Name}";

        #endregion Configurations

    }
}