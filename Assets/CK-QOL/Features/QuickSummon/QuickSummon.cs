using System.Linq;
using CK_QOL.Core;
using CK_QOL.Core.Features;
using CoreLib.RewiredExtension;
using Rewired;

namespace CK_QOL.Features.QuickSummon
{
	/// <summary>
	///     Provides the "Quick Summon" feature, allowing players to quickly equip a configured summoning tome,
	///     use a summon spell, and swap back to the previously equipped item.
	///     This feature allows players to bind a specific key to execute a summoning action seamlessly.
	///     The following tomes are supported:
	///     <list type="bullet">
	///         <item>
	///             <description>
	///                 <see cref="ObjectID.TomeOfRange" />
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 <see cref="ObjectID.TomeOfOrbit" />
	///             </description>
	///         </item>
	///         <item>
	///             <description>
	///                 <see cref="ObjectID.TomeOfMelee" />
	///             </description>
	///         </item>
	///     </list>
	/// </summary>
	/// <remarks>
	///     The "Quick Summon" feature allows players to rapidly switch to a summoning tome, cast a spell, and revert back to
	///     their previously equipped item. This class inherits from <see cref="QuickActionFeatureBase{TFeature}" /> to provide
	///     common functionality for equipping and using items.
	/// </remarks>
	internal sealed class QuickSummon : QuickActionFeatureBase<QuickSummon>, IKeyBindableFeature
	{
		private readonly ObjectID[] _tomeIDs =
		{
			ObjectID.TomeOfRange,
			ObjectID.TomeOfOrbit,
			ObjectID.TomeOfMelee
		};

		/// <summary>
		///     Initializes a new instance of the <see cref="QuickSummon" /> class, applying configuration settings and binding
		///     the key for the summon action.
		/// </summary>
		public QuickSummon()
		{
			var config = new QuickSummonConfig(this);
			IsEnabled = config.ApplyIsEnabled();
			EquipmentSlotIndex = config.ApplyEquipmentSlotIndex();

			SetupKeyBindings();
		}

		/// <summary>
		///     Checks if the given object data matches one of the predefined summoning tomes.
		/// </summary>
		/// <param name="objectData">The object data to check.</param>
		/// <returns>True if the object is a summoning tome, otherwise false.</returns>
		protected override bool IsTargetItem(ObjectDataCD objectData)
		{
			return _tomeIDs.Contains(objectData.objectID);
		}

		#region IFeature

		/// <inheritdoc />
		public override string Name => nameof(QuickSummon);

		/// <inheritdoc />
		public override string DisplayName => "Quick Summon";

		/// <inheritdoc />
		public override string Description => "Quickly equips or switches the preferred summoning tome, casts a summon spell, and swaps back to the previous item.";

		/// <inheritdoc />
		public override FeatureType FeatureType => FeatureType.Client;

		#endregion IFeature

		#region Configurations

		/// <inheritdoc />
		public override int EquipmentSlotIndex { get; }

		public override string KeyBindName => $"{ModSettings.ShortName}_{Name}";

		public override void SetupKeyBindings()
		{
			RewiredExtensionModule.AddKeybind(KeyBindName, DisplayName, KeyboardKeyCode.X);
		}

		#endregion Configurations

	}
}