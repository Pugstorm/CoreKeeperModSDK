using CK_QOL.Core;
using CK_QOL.Core.Features;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.QuickHeal
{
	/// <summary>
	///     Provides the "Quick Heal" feature, allowing players to quickly equip and use a healing item, such as a healing
	///     potion, and then automatically revert to the previously equipped item.
	///     The following items are supported:
	///     <list type="bullet">
	///         <item>
	///             <description>
	///                 <see cref="ObjectID.HealingPotion" />
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 <see cref="ObjectID.GreaterHealingPotion" />
	///             </description>
	///         </item>
	///     </list>
	/// </summary>
	/// <remarks>
	///     The "Quick Heal" feature allows players to heal rapidly during combat without manually searching through their
	///     inventory.
	///     This class inherits from <see cref="QuickActionFeatureBase{TFeature}" /> to provide common functionality for item
	///     management.
	/// </remarks>
	internal sealed class QuickHeal : QuickActionFeatureBase<QuickHeal>
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="QuickHeal" /> class, applying configuration settings and binding
		///     the key for the healing action.
		/// </summary>
		public QuickHeal()
		{
			var config = new QuickHealConfig(this);
			IsEnabled = config.ApplyIsEnabled();
			EquipmentSlotIndex = config.ApplyEquipmentSlotIndex();

			SetupKeyBindings();
		}

		/// <summary>
		///     Checks if the given object data matches a healing potion.
		/// </summary>
		/// <param name="objectData">The object data to check.</param>
		/// <returns>True if the object is a healing item, otherwise false.</returns>
		protected override bool IsTargetItem(ObjectDataCD objectData)
		{
			return objectData.objectID is ObjectID.HealingPotion or ObjectID.GreaterHealingPotion;
		}

		#region IFeature

		/// <inheritdoc />
		public override string Name => nameof(QuickHeal);

		/// <inheritdoc />
		public override string DisplayName => "Quick Heal";

		/// <inheritdoc />
		public override string Description =>
			"Quickly equips or switches the preferred healable item, consumes it, and swaps back to the previous item.";

		/// <inheritdoc />
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature

		#region Configurations

		/// <inheritdoc />
		public override int EquipmentSlotIndex { get; }

		/// <inheritdoc />
		public override string KeyBindName => $"{ModSettings.ShortName}_{Name}";

		public override void SetupKeyBindings()
		{
			RewiredExtensionModule.AddKeybind(KeyBindName, DisplayName, KeyboardKeyCode.G);
		}

		#endregion Configurations

	}
}