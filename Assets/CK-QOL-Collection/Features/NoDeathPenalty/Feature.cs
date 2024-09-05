using CK_QOL_Collection.Core;
using CK_QOL_Collection.Features.NoDeathPenalty.Patches;
using PugMod;

namespace CK_QOL_Collection.Features.NoDeathPenalty
{
	/// <summary>
	///		Represents the No Death Penalty feature of the mod.
	///		This feature allows players to keep their whole inventory after dying.
	/// </summary>
	internal class Feature : FeatureBase
	{
		/// <summary>
		///		Initializes a new instance of the <see cref="Feature"/> class.
		///		Sets up the No Death Penalty feature using the configuration settings.
		/// </summary>
		public Feature()
			: base(Configuration.Sections.NoDeathPenalty.Name, Configuration.Sections.NoDeathPenalty.IsEnabled)
		{
		}

		/// <summary>
		///		Applies the necessary Harmony patches to enable the No Death Penalty feature.
		///		This method is called to activate the feature's primary functionality, which ensures the player's inventory is preserved after death.
		/// </summary>
		public override void Execute()
		{
			API.ModLoader.ApplyHarmonyPatch(Entry.ModInfo.ModId, typeof(InventoryCreatePatches));
		}
	}
}