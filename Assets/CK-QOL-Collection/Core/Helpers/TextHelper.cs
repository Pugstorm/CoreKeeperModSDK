using System.Collections.Generic;

namespace CK_QOL_Collection.Core.Helpers
{
	internal static class TextHelper
	{
		internal static void DisplayText(string text, Rarity rarity)
		{
			Manager.ui.ShowDiscoveredItemText(new List<string> { $"-CK-QOL-{text}"}, rarity);
		}
	}
}