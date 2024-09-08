using Unity.Collections;

namespace CK_QOL_Collection.Core.Extensions
{
	internal static class PickUpItemStateExtensions
	{
		private const string None = nameof(PickUpItemState.None);
		private const string PickedUp = nameof(PickUpItemState.PickedUp);
		private const string ForcePickUp = nameof(PickUpItemState.ForcePickUp);
		private const string BlockPickupUntilReEnterStart = nameof(PickUpItemState.BlockPickupUntilReEnterStart);
		private const string BlockPickupUntilReEnterHasMovedAway = nameof(PickUpItemState.BlockPickupUntilReEnterHasMovedAway);

		internal static FixedString32Bytes ToFixedString(this PickUpItemState value)
			=> value switch
			{
				PickUpItemState.None => (FixedString32Bytes)None,
				PickUpItemState.PickedUp => (FixedString32Bytes)PickedUp,
				PickUpItemState.ForcePickUp => (FixedString32Bytes)ForcePickUp,
				PickUpItemState.BlockPickupUntilReEnterStart => (FixedString32Bytes)BlockPickupUntilReEnterStart,
				PickUpItemState.BlockPickupUntilReEnterHasMovedAway => (FixedString32Bytes)BlockPickupUntilReEnterHasMovedAway,
				_ => (FixedString32Bytes)None,
			};
	}
}