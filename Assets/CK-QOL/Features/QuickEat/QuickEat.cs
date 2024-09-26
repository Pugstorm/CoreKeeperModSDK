using System;
using System.Collections.Generic;
using CK_QOL.Core;
using CK_QOL.Core.Features;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.QuickEat
{
	/// <summary>
	///     Provides the "Quick Eat" feature, allowing players to quickly equip and consume an eatable item, such as food, and
	///     then revert to the previously equipped item.
	///     This feature prioritizes cooked food and non-potion items for consumption.
	/// </summary>
	/// <remarks>
	///     The "Quick Eat" feature helps players quickly consume food without manually searching their inventory.
	///     It sorts available items based on whether they are cooked food, ensuring the most beneficial items are consumed
	///     first.
	///     This class inherits from <see cref="QuickActionFeatureBase{TFeature}" /> to provide common functionality for item
	///     consumption.
	/// </remarks>
	internal sealed class QuickEat : QuickActionFeatureBase<QuickEat>
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="QuickEat" /> class, applying configuration settings and binding
		///     the key for the eating action.
		/// </summary>
		public QuickEat()
		{
			var config = new QuickEatConfig(this);
			IsEnabled = config.ApplyIsEnabled();
			EquipmentSlotIndex = config.ApplyEquipmentSlotIndex();

			SetupKeyBindings();
		}

		/// <summary>
		///     Custom sorting function prioritizing cooked food over other food items.
		/// </summary>
		protected override Func<KeyValuePair<int, ObjectDataCD>, object> SortingFunction =>
			item => PugDatabase.HasComponent<CookedFoodCD>(item.Value) ? 0 : 1;

		/// <summary>
		///     Checks if the given object data represents an eatable item, excluding potions.
		/// </summary>
		/// <param name="objectData">The object data to check.</param>
		/// <returns>True if the object is an eatable item, otherwise false.</returns>
		protected override bool IsTargetItem(ObjectDataCD objectData)
		{
			if (objectData.objectID == ObjectID.None || PugDatabase.HasComponent<PotionCD>(objectData))
			{
				return false;
			}

			return PugDatabase.GetObjectInfo(objectData.objectID, objectData.variation) is { objectType: ObjectType.Eatable };
		}

		#region IFeature

		/// <inheritdoc />
		public override string Name => nameof(QuickEat);

		/// <inheritdoc />
		public override string DisplayName => "Quick Eat";

		/// <inheritdoc />
		public override string Description => "Quickly equips or switches the preferred eatable item, consumes it, and swaps back to the previous item.";

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
			RewiredExtensionModule.AddKeybind(KeyBindName, DisplayName, KeyboardKeyCode.F);
		}

		#endregion Configurations
	}
}